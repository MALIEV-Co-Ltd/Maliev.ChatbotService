using Maliev.Aspire.ServiceDefaults.Caching;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Entities;
using Maliev.ChatbotService.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class SystemInstructionDefaultPromptTests
{
    [Fact]
    public async Task GetMergedInstructionsAsync_QuoteEngineDefaultPrompt_InstructsTrustedAuthHandoffForCheckout()
    {
        var repository = new Mock<ISystemInstructionRepository>();
        repository
            .Setup(item => item.GetActiveCoreAsync("quote-engine", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);
        repository
            .Setup(item => item.GetActiveCoreAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);

        var cache = new Mock<ICacheService>();
        cache
            .Setup(item => item.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        cache
            .Setup(item => item.GetAsync<SystemInstruction>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);

        var metrics = new Mock<IConversationMetrics>();
        var service = new SystemInstructionService(
            repository.Object,
            cache.Object,
            metrics.Object,
            NullLogger<SystemInstructionService>.Instance);

        var prompt = await service.GetMergedInstructionsAsync([], "quote-engine");

        Assert.Contains("quote_get_project_summary", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_get_auth_handoff", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_get_connector_handoff", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_get_settings", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_update_settings", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_get_account_context", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_update_account_profile", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_generate_3d_preview", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_start_design", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_apply_operations", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_observe_design", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_finalize_preview", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_calculate_estimate", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_request_employee_review", prompt, StringComparison.Ordinal);
        Assert.Contains("MALIEV project review queue", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quote_prepare_formal_quote", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_approve_quote", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_create_order", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_start_payment", prompt, StringComparison.Ordinal);
        Assert.Contains("confirmation-gated", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supersedes_part_id", prompt, StringComparison.Ordinal);
        Assert.Contains("corrected CAD/3D revision", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never paste raw tool JSON", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("default checkout addresses", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not ask customers to retype billing or shipping details", prompt, StringComparison.Ordinal);
        Assert.Contains("sign-in or sign-up", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checkout", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never collect credentials in chat", prompt, StringComparison.Ordinal);
        Assert.Contains("reorder", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("duplicate the project", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fresh Make Studio session", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not mutate completed order history", prompt, StringComparison.Ordinal);
        Assert.Contains("ask for one focused dimension confirmation instead of generating a 3D preview", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not generate a 3D preview from an unlabeled sketch or photo with no scale reference", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not ask for quantity, lead time, finish, tolerance", prompt, StringComparison.Ordinal);
        Assert.Contains("state the assumption and continue", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_focus_ui", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_ask_customer", prompt, StringComparison.Ordinal);
        Assert.Contains("genuinely blocking ambiguity", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2-4 discrete mutually exclusive options", prompt, StringComparison.Ordinal);
        Assert.Contains("never say that you opened, displayed, loaded, or showed a viewer", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one active quote workbench artifact", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bounded CAD workbench", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("base_revision", prompt, StringComparison.Ordinal);
        Assert.Contains("80 CAD operations", prompt, StringComparison.Ordinal);
        Assert.Contains("3 operation batches", prompt, StringComparison.Ordinal);
        Assert.Contains("full revised cad_commands", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("say the preview is available in the quote workbench", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("call quote_calculate_estimate in the same turn", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not end a response with \"I will now calculate an estimate\"", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMergedInstructionsAsync_QuoteEngineDefaultPrompt_KeepsGateTermsInternal()
    {
        var repository = new Mock<ISystemInstructionRepository>();
        repository
            .Setup(item => item.GetActiveCoreAsync("quote-engine", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);
        repository
            .Setup(item => item.GetActiveCoreAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);

        var cache = new Mock<ICacheService>();
        cache
            .Setup(item => item.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        cache
            .Setup(item => item.GetAsync<SystemInstruction>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemInstruction?)null);

        var metrics = new Mock<IConversationMetrics>();
        var service = new SystemInstructionService(
            repository.Object,
            cache.Object,
            metrics.Object,
            NullLogger<SystemInstructionService>.Instance);

        var prompt = await service.GetMergedInstructionsAsync([], "quote-engine");

        Assert.Contains("internal", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer-friendly next steps", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("state which gate is blocking", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuoteEnginePromptFile_KeepsGateTermsInternal()
    {
        var promptPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Maliev.ChatbotService.Api",
            "Prompts",
            "core",
            "quote-engine-assistant.md"));
        var prompt = File.ReadAllText(promptPath);

        Assert.Contains("internal", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer-friendly next steps", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quote_generate_3d_preview", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_request_employee_review", prompt, StringComparison.Ordinal);
        Assert.Contains("MALIEV project review queue", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quote_prepare_formal_quote", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_approve_quote", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_create_order", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_start_payment", prompt, StringComparison.Ordinal);
        Assert.Contains("confirmation-gated", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supersedes_part_id", prompt, StringComparison.Ordinal);
        Assert.Contains("duplicate the project", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fresh Make Studio session", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not mutate completed order history", prompt, StringComparison.Ordinal);
        Assert.Contains("ask for one focused dimension confirmation instead of generating a 3D preview", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not generate a 3D preview from an unlabeled sketch or photo with no scale reference", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not ask for quantity, lead time, finish, tolerance", prompt, StringComparison.Ordinal);
        Assert.Contains("state the assumption and continue", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_focus_ui", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_ask_customer", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_start_design", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_apply_operations", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_observe_design", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_cad_finalize_preview", prompt, StringComparison.Ordinal);
        Assert.Contains("quote_calculate_estimate", prompt, StringComparison.Ordinal);
        Assert.Contains("genuinely blocking ambiguity", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2-4 discrete mutually exclusive options", prompt, StringComparison.Ordinal);
        Assert.Contains("never say that you opened, displayed, loaded, or showed a viewer", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one active quote workbench artifact", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bounded CAD workbench", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("base_revision", prompt, StringComparison.Ordinal);
        Assert.Contains("80 CAD operations", prompt, StringComparison.Ordinal);
        Assert.Contains("3 operation batches", prompt, StringComparison.Ordinal);
        Assert.Contains("full revised cad_commands", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("say the preview is available in the quote workbench", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("call `quote_calculate_estimate` in the same turn", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not end a response with \"I will now calculate an estimate\"", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("state which gate is blocking", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
