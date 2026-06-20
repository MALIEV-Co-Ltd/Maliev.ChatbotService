using Maliev.ChatbotService.Infrastructure.Tools;
using System.Text.Json;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class ToolRegistryTests
{
    [Fact]
    public void GetAllToolDeclarations_ReturnsOneToolDeclaration()
    {
        var declarations = ToolRegistry.GetAllToolDeclarations();
        Assert.NotNull(declarations);
        Assert.Single(declarations);
    }

    [Fact]
    public void GetAllToolDeclarations_HasFunctionDeclarations()
    {
        var declarations = ToolRegistry.GetAllToolDeclarations();
        var fns = declarations[0].FunctionDeclarations;
        Assert.NotNull(fns);
        Assert.NotEmpty(fns);
    }

    [Fact]
    public void GetAllToolDeclarations_ContainsExpectedFunctions()
    {
        var declarations = ToolRegistry.GetAllToolDeclarations();
        var names = declarations[0].FunctionDeclarations!.Select(f => f.Name).ToList();

        Assert.Contains("search_customers", names);
        Assert.Contains("get_customer", names);
        Assert.Contains("search_orders", names);
        Assert.Contains("get_order", names);
        Assert.Contains("search_quotations", names);
        Assert.Contains("search_invoices", names);
        Assert.Contains("search_materials", names);
        Assert.Contains("search_suppliers", names);
    }

    [Fact]
    public void GetAllToolDeclarations_AllFunctionsHaveNameAndDescription()
    {
        var declarations = ToolRegistry.GetAllToolDeclarations();
        foreach (var fn in declarations[0].FunctionDeclarations!)
        {
            Assert.False(string.IsNullOrEmpty(fn.Name), $"Function has empty name");
            Assert.False(string.IsNullOrEmpty(fn.Description), $"Function '{fn.Name}' has empty description");
        }
    }

    [Fact]
    public void GetToolDeclarationsForProfile_ReturnsQuoteEngineWorkflowToolsOnlyForQuoteEngine()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var quoteNames = quoteDeclarations[0].FunctionDeclarations!.Select(f => f.Name).ToList();

        var expectedQuoteTools = new[]
        {
            "quote_get_state",
            "quote_get_project_summary",
            "quote_get_connectors",
            "quote_get_connector_handoff",
            "quote_register_uploads",
            "quote_generate_3d_preview",
            "quote_resume_project",
            "quote_search_customer_data",
            "quote_get_auth_handoff",
            "quote_get_settings",
            "quote_update_settings",
            "quote_update_account_profile",
            "quote_get_reference_data",
            "quote_update_part_configuration",
            "quote_calculate_estimate",
            "quote_update_checkout_details",
            "quote_prepare_draft_project",
            "quote_duplicate_project",
            "quote_pin_project",
            "quote_unpin_project",
            "quote_archive_project",
            "quote_request_employee_review",
            "quote_prepare_formal_quote",
            "quote_approve_quote",
            "quote_acknowledge_dfm",
            "quote_create_order",
            "quote_start_payment",
            "quote_get_account_context",
            "quote_set_ui_language",
            "quote_set_project_name",
            "quote_focus_ui",
            "quote_ask_customer"
        };

        Assert.Equal(
            expectedQuoteTools.OrderBy(name => name, StringComparer.Ordinal),
            quoteNames.OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain("search_customers", quoteNames);
        Assert.DoesNotContain("get_employee", quoteNames);

        var websiteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("website");
        Assert.Empty(websiteDeclarations);
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteAccountProfileToolDeclaresSnakeCaseFieldsOnly()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var tool = Assert.Single(
            quoteDeclarations[0].FunctionDeclarations!,
            declaration => declaration.Name == "quote_update_account_profile");

        var json = JsonSerializer.Serialize(tool.Parameters);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.GetProperty("properties");

        // Snake_case is the single canonical casing; camelCase duplicates were removed (T2).
        Assert.True(properties.TryGetProperty("display_name", out _));
        Assert.True(properties.TryGetProperty("company_name", out _));
        Assert.True(properties.TryGetProperty("vat_number", out _));
        Assert.True(properties.TryGetProperty("preferred_language", out _));
        Assert.True(properties.TryGetProperty("preferred_currency", out _));

        Assert.False(properties.TryGetProperty("displayName", out _));
        Assert.False(properties.TryGetProperty("companyName", out _));
        Assert.False(properties.TryGetProperty("vatNumber", out _));
        Assert.False(properties.TryGetProperty("preferredLanguage", out _));
        Assert.False(properties.TryGetProperty("preferredCurrency", out _));
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteRegisterUploadsDeclaresSupersedeFields()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var tool = Assert.Single(
            quoteDeclarations[0].FunctionDeclarations!,
            declaration => declaration.Name == "quote_register_uploads");

        var json = JsonSerializer.Serialize(tool.Parameters);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.GetProperty("properties");
        var fileProperties = properties.GetProperty("files").GetProperty("items").GetProperty("properties");

        Assert.True(properties.TryGetProperty("supersedes_part_id", out _));
        Assert.True(properties.TryGetProperty("supersedes_upload_id", out _));
        Assert.True(properties.TryGetProperty("supersedes_file_name", out _));
        Assert.True(fileProperties.TryGetProperty("supersedes_part_id", out _));
        Assert.True(fileProperties.TryGetProperty("supersedes_upload_id", out _));
        Assert.True(fileProperties.TryGetProperty("supersedes_file_name", out _));
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteGenerate3dPreviewDeclaresNormalizedCommandAliases()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var tool = Assert.Single(
            quoteDeclarations[0].FunctionDeclarations!,
            declaration => declaration.Name == "quote_generate_3d_preview");

        var json = JsonSerializer.Serialize(tool.Parameters);
        using var document = JsonDocument.Parse(json);
        var commandProperties = document.RootElement
            .GetProperty("properties")
            .GetProperty("cad_commands")
            .GetProperty("items")
            .GetProperty("properties");
        var dimensions = commandProperties.GetProperty("dimensions").GetProperty("properties");
        var profile = commandProperties.GetProperty("profile").GetProperty("properties");
        var segment = profile
            .GetProperty("segments")
            .GetProperty("items")
            .GetProperty("properties");

        Assert.True(commandProperties.TryGetProperty("operation", out _));
        Assert.True(commandProperties.TryGetProperty("shape", out _));
        Assert.True(commandProperties.TryGetProperty("parameters", out _));
        Assert.True(commandProperties.TryGetProperty("dimensions", out _));
        Assert.True(commandProperties.TryGetProperty("width", out _));
        Assert.True(commandProperties.TryGetProperty("height", out _));
        Assert.True(commandProperties.TryGetProperty("diameter", out _));
        Assert.True(commandProperties.TryGetProperty("target", out _));
        Assert.True(commandProperties.TryGetProperty("tool", out _));
        Assert.True(commandProperties.TryGetProperty("result", out _));
        Assert.True(commandProperties.TryGetProperty("x", out _));
        Assert.True(commandProperties.TryGetProperty("y", out _));
        Assert.True(commandProperties.TryGetProperty("z", out _));
        Assert.True(commandProperties.TryGetProperty("axisX", out _));
        Assert.True(commandProperties.TryGetProperty("axisY", out _));
        Assert.True(commandProperties.TryGetProperty("axisZ", out _));

        Assert.True(dimensions.TryGetProperty("x", out _));
        Assert.True(dimensions.TryGetProperty("y", out _));
        Assert.True(dimensions.TryGetProperty("z", out _));
        Assert.True(dimensions.TryGetProperty("diameter", out _));
        Assert.True(dimensions.TryGetProperty("d", out _));
        Assert.True(dimensions.TryGetProperty("radiusBottom", out _));
        Assert.True(dimensions.TryGetProperty("radiusTop", out _));

        Assert.True(profile.TryGetProperty("type", out _));
        Assert.True(profile.TryGetProperty("parameters", out _));
        Assert.True(profile.TryGetProperty("width", out _));
        Assert.True(profile.TryGetProperty("height", out _));
        Assert.True(profile.TryGetProperty("radius", out _));

        Assert.True(segment.TryGetProperty("parameters", out _));
        Assert.True(segment.TryGetProperty("x", out _));
        Assert.True(segment.TryGetProperty("y", out _));
        Assert.True(segment.TryGetProperty("dx", out _));
        Assert.True(segment.TryGetProperty("dy", out _));
        Assert.True(segment.TryGetProperty("length", out _));
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteCreateOrderDeclaresQuoteEnginePoField()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var tool = Assert.Single(
            quoteDeclarations[0].FunctionDeclarations!,
            declaration => declaration.Name == "quote_create_order");

        var json = JsonSerializer.Serialize(tool.Parameters);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.GetProperty("properties");

        Assert.True(properties.TryGetProperty("customer_po_number", out _));
        Assert.False(properties.TryGetProperty("purchase_order", out _));
    }
}
