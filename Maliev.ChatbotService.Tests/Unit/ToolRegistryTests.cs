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
            "quote_cad_start_design",
            "quote_cad_apply_operations",
            "quote_cad_observe_design",
            "quote_cad_finalize_preview",
            "quote_resume_project",
            "quote_search_customer_data",
            "quote_get_auth_handoff",
            "quote_get_settings",
            "quote_update_settings",
            "quote_update_account_profile",
            "quote_get_reference_data",
            "quote_update_part_configuration",
            "quote_calculate_estimate",
            "quote_get_shipping_couriers",
            "quote_get_shipping_rates",
            "quote_select_shipping_rate",
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
    public void GetToolDeclarationsForProfile_QuoteCadWorkbenchDeclaresBoundedIterativeTools()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var tools = quoteDeclarations[0].FunctionDeclarations!.ToDictionary(declaration => declaration.Name);

        Assert.Contains("bounded iterative CAD design session", tools["quote_cad_start_design"].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("80 CAD operations", tools["quote_cad_apply_operations"].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("base_revision", tools["quote_cad_apply_operations"].Description, StringComparison.Ordinal);
        Assert.Contains("Observe", tools["quote_cad_observe_design"].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finalize", tools["quote_cad_finalize_preview"].Description, StringComparison.OrdinalIgnoreCase);

        var startJson = JsonSerializer.Serialize(tools["quote_cad_start_design"].Parameters);
        using var startDocument = JsonDocument.Parse(startJson);
        var startProperties = startDocument.RootElement.GetProperty("properties");
        Assert.True(startProperties.TryGetProperty("description", out _));
        Assert.True(startProperties.TryGetProperty("process_hint", out _));
        Assert.True(startProperties.TryGetProperty("units", out _));

        var applyJson = JsonSerializer.Serialize(tools["quote_cad_apply_operations"].Parameters);
        using var applyDocument = JsonDocument.Parse(applyJson);
        var applyProperties = applyDocument.RootElement.GetProperty("properties");
        Assert.True(applyProperties.TryGetProperty("design_id", out _));
        Assert.True(applyProperties.TryGetProperty("base_revision", out _));
        Assert.True(applyProperties.TryGetProperty("stage", out _));
        Assert.True(applyProperties.TryGetProperty("operations", out var operations));
        Assert.Equal("ARRAY", operations.GetProperty("type").GetString());

        var finalizeJson = JsonSerializer.Serialize(tools["quote_cad_finalize_preview"].Parameters);
        using var finalizeDocument = JsonDocument.Parse(finalizeJson);
        var finalizeProperties = finalizeDocument.RootElement.GetProperty("properties");
        Assert.True(finalizeProperties.TryGetProperty("design_id", out _));
        Assert.True(finalizeProperties.TryGetProperty("base_revision", out _));
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

        Assert.Contains("only when the part shape and dimensions are explicit", tool.Description, StringComparison.Ordinal);
        Assert.Contains("Do not use this tool for an unlabeled sketch or photo with no scale reference", tool.Description, StringComparison.Ordinal);
        Assert.Contains("ask for one focused dimension confirmation first", tool.Description, StringComparison.Ordinal);

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
        Assert.True(commandProperties.TryGetProperty("type", out _));
        Assert.True(commandProperties.TryGetProperty("shape", out _));
        Assert.True(commandProperties.TryGetProperty("name", out _));
        Assert.True(commandProperties.TryGetProperty("shape_id", out _));
        Assert.True(commandProperties.TryGetProperty("parameters", out _));
        Assert.True(commandProperties.TryGetProperty("dimensions", out _));
        Assert.True(commandProperties.TryGetProperty("width", out _));
        Assert.True(commandProperties.TryGetProperty("height", out _));
        Assert.True(commandProperties.TryGetProperty("thickness", out _));
        Assert.True(commandProperties.TryGetProperty("diameter", out _));
        Assert.True(commandProperties.TryGetProperty("target", out _));
        Assert.True(commandProperties.TryGetProperty("target_shape_id", out _));
        Assert.True(commandProperties.TryGetProperty("tool", out _));
        Assert.True(commandProperties.TryGetProperty("tool_shape_id", out _));
        Assert.True(commandProperties.TryGetProperty("result", out _));
        Assert.True(commandProperties.TryGetProperty("result_shape_id", out _));
        Assert.True(commandProperties.TryGetProperty("size", out _));
        Assert.True(commandProperties.TryGetProperty("cornerRadius", out _));
        Assert.True(commandProperties.TryGetProperty("edgeRadius", out _));
        Assert.True(commandProperties.TryGetProperty("translation", out _));
        Assert.True(commandProperties.TryGetProperty("position", out _));
        Assert.True(commandProperties.TryGetProperty("location", out _));
        Assert.True(commandProperties.TryGetProperty("x", out _));
        Assert.True(commandProperties.TryGetProperty("y", out _));
        Assert.True(commandProperties.TryGetProperty("z", out _));
        Assert.True(commandProperties.TryGetProperty("axisX", out _));
        Assert.True(commandProperties.TryGetProperty("axisY", out _));
        Assert.True(commandProperties.TryGetProperty("axisZ", out _));
        Assert.True(commandProperties.TryGetProperty("rotationAxis", out _));
        Assert.True(commandProperties.TryGetProperty("angleDegrees", out _));
        Assert.True(commandProperties.TryGetProperty("degrees", out _));

        Assert.True(dimensions.TryGetProperty("x", out _));
        Assert.True(dimensions.TryGetProperty("y", out _));
        Assert.True(dimensions.TryGetProperty("z", out _));
        Assert.True(dimensions.TryGetProperty("thickness", out _));
        Assert.True(dimensions.TryGetProperty("diameter", out _));
        Assert.True(dimensions.TryGetProperty("d", out _));
        Assert.True(dimensions.TryGetProperty("radiusBottom", out _));
        Assert.True(dimensions.TryGetProperty("radiusTop", out _));

        Assert.True(profile.TryGetProperty("type", out _));
        Assert.True(profile.TryGetProperty("parameters", out _));
        Assert.True(profile.TryGetProperty("size", out _));
        Assert.True(profile.TryGetProperty("width", out _));
        Assert.True(profile.TryGetProperty("height", out _));
        Assert.True(profile.TryGetProperty("radius", out _));
        Assert.True(profile.TryGetProperty("diameter", out _));
        Assert.True(profile.TryGetProperty("points", out _));
        Assert.True(profile.TryGetProperty("polyline", out _));
        Assert.True(profile.TryGetProperty("vertices", out _));

        Assert.True(segment.TryGetProperty("parameters", out _));
        Assert.True(segment.TryGetProperty("x", out _));
        Assert.True(segment.TryGetProperty("y", out _));
        Assert.True(segment.TryGetProperty("dx", out _));
        Assert.True(segment.TryGetProperty("dy", out _));
        Assert.True(segment.TryGetProperty("length", out _));

        var cadCommandsDescription = document.RootElement
            .GetProperty("properties")
            .GetProperty("cad_commands")
            .GetProperty("description")
            .GetString();
        Assert.Contains("plate/slot/cutout", cadCommandsDescription, StringComparison.Ordinal);
        Assert.Contains("hole/boss/standoff", cadCommandsDescription, StringComparison.Ordinal);

        var opDescription = commandProperties.GetProperty("op").GetProperty("description").GetString();
        Assert.Contains("plate", opDescription, StringComparison.Ordinal);
        Assert.Contains("standoff", opDescription, StringComparison.Ordinal);

        var profileTypeDescription = profile.GetProperty("type").GetProperty("description").GetString();
        Assert.Contains("square", profileTypeDescription, StringComparison.Ordinal);
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

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteStartPaymentDeclaresCheckoutAttemptId()
    {
        var quoteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("quote-engine");
        var tool = Assert.Single(
            quoteDeclarations[0].FunctionDeclarations!,
            declaration => declaration.Name == "quote_start_payment");

        var json = JsonSerializer.Serialize(tool.Parameters);
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.GetProperty("properties");

        Assert.True(properties.TryGetProperty("checkout_attempt_id", out var checkoutAttemptId));
        Assert.Equal("STRING", checkoutAttemptId.GetProperty("type").GetString());
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteShippingToolsDeclareAddressRateAndSelectionSchema()
    {
        var tools = ToolRegistry.GetToolDeclarationsForProfile("quote-engine")[0]
            .FunctionDeclarations!
            .ToDictionary(declaration => declaration.Name);

        Assert.Contains("SHIPPOP", tools["quote_get_shipping_rates"].Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("markdown table", tools["quote_get_shipping_rates"].Description, StringComparison.OrdinalIgnoreCase);

        var ratesJson = JsonSerializer.Serialize(tools["quote_get_shipping_rates"].Parameters);
        using var ratesDocument = JsonDocument.Parse(ratesJson);
        var rateProperties = ratesDocument.RootElement.GetProperty("properties");
        Assert.True(rateProperties.TryGetProperty("address", out var address));
        Assert.Equal("STRING", address.GetProperty("type").GetString());
        Assert.True(rateProperties.TryGetProperty("postcode", out var postcode));
        Assert.Equal("STRING", postcode.GetProperty("type").GetString());
        Assert.True(rateProperties.TryGetProperty("tel", out var tel));
        Assert.Equal("STRING", tel.GetProperty("type").GetString());
        Assert.True(rateProperties.TryGetProperty("weight", out var weight));
        Assert.Equal("NUMBER", weight.GetProperty("type").GetString());

        var selectJson = JsonSerializer.Serialize(tools["quote_select_shipping_rate"].Parameters);
        using var selectDocument = JsonDocument.Parse(selectJson);
        var selectProperties = selectDocument.RootElement.GetProperty("properties");
        Assert.True(selectProperties.TryGetProperty("courier_code", out var courierCode));
        Assert.Equal("STRING", courierCode.GetProperty("type").GetString());
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteToolDescriptionsCarryUsageGuidance()
    {
        var tools = ToolRegistry.GetToolDeclarationsForProfile("quote-engine")[0]
            .FunctionDeclarations!
            .ToDictionary(declaration => declaration.Name, declaration => declaration.Description ?? string.Empty);

        // Read tools state when to use them and what they return.
        Assert.Contains("prefer quote_get_project_summary", tools["quote_get_state"], StringComparison.Ordinal);
        Assert.Contains("order status", tools["quote_get_project_summary"], StringComparison.OrdinalIgnoreCase);

        // Descriptions tell the agent where required args come from (data flow).
        Assert.Contains("quote_get_account_context", tools["quote_update_checkout_details"], StringComparison.Ordinal);
        Assert.Contains("quote_update_checkout_details", tools["quote_get_account_context"], StringComparison.Ordinal);
        Assert.Contains("quote_search_customer_data", tools["quote_resume_project"], StringComparison.Ordinal);
        Assert.Contains("quote_get_project_summary", tools["quote_acknowledge_dfm"], StringComparison.Ordinal);
        Assert.Contains("order_number", tools["quote_start_payment"], StringComparison.Ordinal);
        Assert.Contains("quote_get_reference_data", tools["quote_update_part_configuration"], StringComparison.Ordinal);

        // Estimate tool explains its blocker behavior; finalization sequence is spelled out.
        Assert.Contains("blocker", tools["quote_calculate_estimate"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finalization sequence", tools["quote_prepare_formal_quote"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetToolDeclarationsForProfile_QuoteToolsNeverAcceptCustomerIdentityOverrideParameters()
    {
        // SECURITY: the agent must have exactly the signed-in customer's permissions and only ever
        // see that customer's own data. The BFF resolves the customer identity server-side from the
        // authenticated request (CustomerSessionResolver reads the customer_id claim) and never trusts
        // tool arguments for identity. A tool that DECLARED an identity-override parameter would invite
        // a prompt-injected message or a hallucinating model to target another customer's projects,
        // orders, or account. Enforce that no quote-engine tool exposes such a parameter at any depth.
        // (Resource references the BFF ownership-checks - project_id, order_number, address_id - are
        // fine; only identity-of-the-actor overrides are forbidden.)
        string[] forbidden =
        [
            "customer_id", "customerId",
            "owner_id", "ownerId",
            "tenant_id", "tenantId",
            "user_id", "userId",
            "account_id", "accountId",
            "on_behalf_of", "onBehalfOf",
            "impersonate", "as_customer", "asCustomer"
        ];

        var tools = ToolRegistry.GetToolDeclarationsForProfile("quote-engine")[0].FunctionDeclarations!;
        foreach (var tool in tools)
        {
            var json = JsonSerializer.Serialize(tool.Parameters);
            using var document = JsonDocument.Parse(json);
            var propertyNames = new List<string>();
            CollectObjectKeys(document.RootElement, propertyNames);

            foreach (var forbiddenName in forbidden)
            {
                Assert.DoesNotContain(
                    propertyNames,
                    name => string.Equals(name, forbiddenName, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static void CollectObjectKeys(JsonElement element, List<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    keys.Add(property.Name);
                    CollectObjectKeys(property.Value, keys);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectObjectKeys(item, keys);
                }

                break;
        }
    }
}
