using Maliev.ChatbotService.Application.Handlers;

namespace Maliev.ChatbotService.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LeakedToolCallParser"/>.
/// </summary>
public class LeakedToolCallParserTests
{
    private static readonly string[] QuoteTools =
    [
        "quote_update_configuration",
        "quote_set_project_name",
        "quote_calculate_estimate"
    ];

    /// <summary>
    /// Reproduces the production leak: gemini-2.5-flash wrote a tool_code block as chat text.
    /// All three calls must be recovered with correctly typed arguments.
    /// </summary>
    [Fact]
    public void Parse_LeakedToolCodeBlock_RecoversAllCallsWithTypedArgs()
    {
        var content = "Sure! Let's get that quote for you. tool_code\n" +
            "print(quote_update_configuration(part_id='case.stl', material='ABS', quantity=6))\n" +
            "print(quote_set_project_name(name='case - FDM ABS'))\n" +
            "print(quote_calculate_estimate())";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Equal(3, calls.Count);
        Assert.Equal("quote_update_configuration", calls[0].Name);
        Assert.Equal("case.stl", calls[0].Args["part_id"]);
        Assert.Equal("ABS", calls[0].Args["material"]);
        Assert.Equal(6L, calls[0].Args["quantity"]);
        Assert.Equal("quote_set_project_name", calls[1].Name);
        Assert.Equal("case - FDM ABS", calls[1].Args["name"]);
        Assert.Equal("quote_calculate_estimate", calls[2].Name);
        Assert.Empty(calls[2].Args);
    }

    /// <summary>Fenced markdown variants of the leak must also be recovered.</summary>
    [Fact]
    public void Parse_FencedToolCodeBlock_RecoversCall()
    {
        var content = "```tool_code\nprint(quote_calculate_estimate())\n```";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Single(calls);
        Assert.Equal("quote_calculate_estimate", calls[0].Name);
    }

    /// <summary>Compact tool names with a tools namespace resolve to their exact declarations.</summary>
    [Theory]
    [InlineData("tools.quotecalculateestimate()", "quote_calculate_estimate")]
    [InlineData("tools.quotegetshippingrates(addressline1='36/1', postalcode='12345')", "quote_get_shipping_rates")]
    [InlineData("tools.quoteprepareformal_quote()", "quote_prepare_formal_quote")]
    public void Parse_CompactToolsPrefix_ResolvesDeclaredCanonicalName(string text, string expected)
    {
        var calls = LeakedToolCallParser.Parse(text,
            ["quote_calculate_estimate", "quote_get_shipping_rates", "quote_prepare_formal_quote"]);

        Assert.Single(calls);
        Assert.Equal(expected, calls[0].Name);
    }

    /// <summary>A tools namespace used as a dotted identifier suffix is not a supported boundary.</summary>
    [Theory]
    [InlineData("other.tools.quotecalculateestimate()")]
    [InlineData("window.tools.quotecalculateestimate()")]
    public void Parse_ToolsNamespaceAfterDottedIdentifier_ReturnsEmpty(string text)
    {
        var calls = LeakedToolCallParser.Parse(text, ["quote_calculate_estimate"]);

        Assert.Empty(calls);
    }

    /// <summary>Whitespace and non-dot punctuation remain valid boundaries for the tools namespace.</summary>
    [Theory]
    [InlineData(" tools.quotecalculateestimate()")]
    [InlineData(";tools.quotecalculateestimate()")]
    public void Parse_ToolsNamespaceAtTokenBoundary_RecoversDeclaredTool(string text)
    {
        var calls = LeakedToolCallParser.Parse(text, ["quote_calculate_estimate"]);

        var call = Assert.Single(calls);
        Assert.Equal("quote_calculate_estimate", call.Name);
    }

    /// <summary>Canonical aliases that match more than one exact declaration are never recovered.</summary>
    [Fact]
    public void Parse_CanonicalDeclarationCollision_ReturnsEmpty()
    {
        var calls = LeakedToolCallParser.Parse(
            "tools.quotecalculateestimate()",
            ["quote_calculate_estimate", "quotecalculateestimate"]);

        Assert.Empty(calls);
    }

    /// <summary>A compact name still must match a declaration on the current request.</summary>
    [Fact]
    public void Parse_UndeclaredCompactToolName_ReturnsEmpty()
    {
        var calls = LeakedToolCallParser.Parse(
            "tools.quotecalculateestimate()",
            ["quote_get_state"]);

        Assert.Empty(calls);
    }

    /// <summary>Invocations of tools that are not declared on the request must be ignored.</summary>
    [Fact]
    public void Parse_UndeclaredToolName_ReturnsEmpty()
    {
        var content = "print(delete_everything(target='all'))";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Empty(calls);
    }

    /// <summary>Ordinary prose, including Thai text, must never produce calls.</summary>
    [Fact]
    public void Parse_PlainProse_ReturnsEmpty()
    {
        var content = "ระบบกำลังอัปเดตการกำหนดค่าและคำนวณราคาให้อยู่นะคะ อาจใช้เวลาสักครู่ค่ะ";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Empty(calls);
    }

    /// <summary>Python literals map to CLR values; None kwargs are omitted.</summary>
    [Fact]
    public void Parse_PythonLiterals_MapToClrValues()
    {
        var content = "quote_update_configuration(rush=True, finish=None, tolerance=1.5, notes=['a', 'b'])";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Single(calls);
        var args = calls[0].Args;
        Assert.Equal(true, args["rush"]);
        Assert.False(args.ContainsKey("finish"));
        Assert.Equal(1.5, args["tolerance"]);
        var notes = Assert.IsType<List<object?>>(args["notes"]);
        Assert.Equal(["a", "b"], notes.Cast<string>());
    }

    /// <summary>Escaped quotes inside string literals must be unescaped.</summary>
    [Fact]
    public void Parse_EscapedQuote_Unescapes()
    {
        var content = @"quote_set_project_name(name='it\'s a case')";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Single(calls);
        Assert.Equal("it's a case", calls[0].Args["name"]);
    }

    /// <summary>
    /// Positional arguments cannot be mapped to parameter names, so such calls are not recovered.
    /// </summary>
    [Fact]
    public void Parse_PositionalArguments_NotRecovered()
    {
        var content = "quote_set_project_name('case - FDM ABS')";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Empty(calls);
    }

    /// <summary>Comparison expressions must not be mistaken for keyword arguments.</summary>
    [Fact]
    public void Parse_ComparisonExpression_NotRecovered()
    {
        var content = "quote_update_configuration(quantity == 6)";

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Empty(calls);
    }

    /// <summary>Recovery is capped so a pathological response cannot fan out unbounded calls.</summary>
    [Fact]
    public void Parse_ManyCalls_CapsAtFive()
    {
        var content = string.Concat(Enumerable.Repeat("print(quote_calculate_estimate())\n", 12));

        var calls = LeakedToolCallParser.Parse(content, QuoteTools);

        Assert.Equal(5, calls.Count);
    }

    /// <summary>Null or empty content and empty declarations short-circuit safely.</summary>
    [Fact]
    public void Parse_EmptyInputs_ReturnEmpty()
    {
        Assert.Empty(LeakedToolCallParser.Parse(null, QuoteTools));
        Assert.Empty(LeakedToolCallParser.Parse(string.Empty, QuoteTools));
        Assert.Empty(LeakedToolCallParser.Parse("print(quote_calculate_estimate())", []));
    }
}
