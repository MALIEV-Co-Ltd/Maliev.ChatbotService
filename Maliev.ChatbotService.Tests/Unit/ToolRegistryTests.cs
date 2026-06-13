using Maliev.ChatbotService.Infrastructure.Tools;
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

        Assert.Contains("quote_get_state", quoteNames);
        Assert.Contains("quote_get_project_summary", quoteNames);
        Assert.Contains("quote_get_connectors", quoteNames);
        Assert.Contains("quote_register_uploads", quoteNames);
        Assert.Contains("quote_resume_project", quoteNames);
        Assert.Contains("quote_search_customer_data", quoteNames);
        Assert.Contains("quote_get_auth_handoff", quoteNames);
        Assert.Contains("quote_duplicate_project", quoteNames);
        Assert.Contains("quote_pin_project", quoteNames);
        Assert.Contains("quote_archive_project", quoteNames);
        Assert.Contains("quote_update_checkout_details", quoteNames);
        Assert.Contains("quote_calculate_estimate", quoteNames);
        Assert.Contains("quote_prepare_formal_quote", quoteNames);
        Assert.Contains("quote_approve_quote", quoteNames);
        Assert.DoesNotContain("search_customers", quoteNames);
        Assert.DoesNotContain("get_employee", quoteNames);

        var websiteDeclarations = ToolRegistry.GetToolDeclarationsForProfile("website");
        Assert.Empty(websiteDeclarations);
    }
}
