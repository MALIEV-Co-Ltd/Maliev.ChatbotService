using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Authorization;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Costing;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for AI-powered data extraction operations.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("chatbot/v{version:apiVersion}/extraction")]
public class ExtractionController : ControllerBase
{
    private const int MaxFiles = 5;
    private const int MaxStoragePaths = 10;
    private const int MaxRawTextLength = 12_000;
    private const int MaxBase64CharactersPerFile = 14_000_000;

    private static readonly HashSet<string> AllowedExtractionMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp",
        "text/plain"
    };

    private readonly ExtractCustomerCommandHandler _handler;
    private readonly ExtractCustomerIntentCommandHandler _intentHandler;
    private readonly CleanDictationSpeechCommandHandler _speechHandler;
    private readonly IUsageBudgetService _usageBudgetService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionController"/> class.
    /// </summary>
    /// <param name="handler">The extract customer command handler.</param>
    /// <param name="intentHandler">The extract customer intent command handler.</param>
    /// <param name="speechHandler">The clean dictation speech command handler.</param>
    /// <param name="usageBudgetService">The usage budget service.</param>
    public ExtractionController(
        ExtractCustomerCommandHandler handler,
        ExtractCustomerIntentCommandHandler intentHandler,
        CleanDictationSpeechCommandHandler speechHandler,
        IUsageBudgetService usageBudgetService)
    {
        _handler = handler;
        _intentHandler = intentHandler;
        _speechHandler = speechHandler;
        _usageBudgetService = usageBudgetService;
    }

    /// <summary>
    /// Extracts customer data from uploaded documents or text using AI.
    /// </summary>
    /// <param name="request">The extraction request containing file paths and/or text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted customer data.</returns>
    [HttpPost("customer")]
    [RequirePermission(ChatbotPermissions.ExtractionsRun)]
    [ProducesResponseType(typeof(ExtractCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtractCustomerResponse>> ExtractCustomer(
        [FromBody] ExtractCustomerRequest request,
        CancellationToken ct)
    {
        var hasFiles = request.Files != null && request.Files.Count > 0;
        var hasStoragePaths = request.StoragePaths != null && request.StoragePaths.Count > 0;
        var hasText = !string.IsNullOrWhiteSpace(request.RawText);

        if (!hasFiles && !hasStoragePaths && !hasText)
        {
            return BadRequest("No files or text provided for extraction.");
        }

        var validationError = ValidateExtractionRequest(request);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        var budgetExceededResult = await TryBuildDailyBudgetExceededResultAsync(ct);
        if (budgetExceededResult is not null)
        {
            return budgetExceededResult;
        }

        var command = new ExtractCustomerCommand
        {
            StoragePaths = request.StoragePaths ?? [],
            RawText = request.RawText,
            Files = request.Files?.Select(f => new Application.Commands.ExtractionFileData
            {
                FileName = f.FileName,
                Base64Data = f.Base64Data,
                MimeType = f.MimeType
            }).ToList()
        };

        var result = await _handler.HandleAsync(command, ct);

        if (!result.Success)
        {
            return StatusCode(500, result.ErrorMessage);
        }

        await RecordModelUsageAsync(result.TokenUsage, result.CostEstimate, ct);

        var response = MapToResponse(result.Data!);
        return Ok(response);
    }

    /// <summary>
    /// Extracts customer intent (need for customer data, search terms) from a user message using AI.
    /// </summary>
    /// <param name="request">The intent extraction request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted customer intent.</returns>
    [HttpPost("customer-intent")]
    [RequirePermission(ChatbotPermissions.ExtractionsRun)]
    [ProducesResponseType(typeof(ExtractCustomerIntentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtractCustomerIntentResponse>> ExtractCustomerIntent(
        [FromBody] ExtractCustomerIntentRequest request,
        CancellationToken ct)
    {
        var budgetExceededResult = await TryBuildDailyBudgetExceededResultAsync(ct);
        if (budgetExceededResult is not null)
        {
            return budgetExceededResult;
        }

        var command = new Application.Commands.ExtractCustomerIntentCommand
        {
            UserMessage = request.UserMessage
        };

        var result = await _intentHandler.HandleAsync(command, ct);

        if (!result.Success)
        {
            return StatusCode(500, result.ErrorMessage);
        }

        await RecordModelUsageAsync(result.TokenUsage, result.CostEstimate, ct);

        return Ok(new ExtractCustomerIntentResponse
        {
            NeedsCustomerData = result.NeedsCustomerData,
            CustomerSearchTerm = result.CustomerSearchTerm,
            NeedsHistory = result.NeedsHistory
        });
    }

    /// <summary>
    /// Cleans up raw dictated speech text using Gemini directly, bypassing the agent pipeline.
    /// </summary>
    /// <param name="request">The speech cleanup request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cleaned speech text.</returns>
    [HttpPost("clean-speech")]
    [RequirePermission(ChatbotPermissions.ExtractionsRun)]
    [ProducesResponseType(typeof(CleanDictationSpeechResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CleanDictationSpeechResponse>> CleanSpeech(
        [FromBody] CleanDictationSpeechRequest request,
        CancellationToken ct)
    {
        var budgetExceededResult = await TryBuildDailyBudgetExceededResultAsync(ct);
        if (budgetExceededResult is not null)
        {
            return budgetExceededResult;
        }

        var command = new Application.Commands.CleanDictationSpeechCommand
        {
            Speech = request.Speech,
            Language = request.Language
        };

        var result = await _speechHandler.HandleAsync(command, ct);

        if (!result.Success)
        {
            return StatusCode(500, result.ErrorMessage);
        }

        await RecordModelUsageAsync(result.TokenUsage, result.CostEstimate, ct);

        return Ok(new CleanDictationSpeechResponse { CleanedText = result.CleanedText });
    }

    private async Task RecordModelUsageAsync(
        GeminiTokenUsage? tokenUsage,
        GeminiCostEstimate? costEstimate,
        CancellationToken cancellationToken)
    {
        if (tokenUsage?.TotalTokens is not > 0 ||
            !TryGetAuthenticatedUserProfileId(out var userProfileId))
        {
            return;
        }

        await _usageBudgetService.RecordModelUsageAsync(
            userProfileId,
            new UsageBudgetCharge
            {
                Tokens = tokenUsage.TotalTokens,
                CostMicroUsd = costEstimate?.TotalMicroUsd ?? 0
            },
            cancellationToken);
    }

    private async Task<ActionResult?> TryBuildDailyBudgetExceededResultAsync(CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserProfileId(out var userProfileId))
        {
            return null;
        }

        var usageSnapshot = await _usageBudgetService.GetDailyTokenUsageSnapshotAsync(userProfileId, cancellationToken);
        return usageSnapshot.IsExceeded
            ? StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = MessagePipelinePolicy.BuildDailyBudgetExceededMessage(Language.English)
            })
            : null;
    }

    private bool TryGetAuthenticatedUserProfileId(out Guid userProfileId)
    {
        userProfileId = default;
        var userProfileIdClaim = User.FindFirst("sub")?.Value
            ?? User.FindFirst("UserProfileId")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return !string.IsNullOrWhiteSpace(userProfileIdClaim) &&
            Guid.TryParse(userProfileIdClaim, out userProfileId);
    }

    private static string? ValidateExtractionRequest(ExtractCustomerRequest request)
    {
        if (request.RawText?.Length > MaxRawTextLength)
        {
            return $"RawText must be {MaxRawTextLength} characters or fewer.";
        }

        if (request.StoragePaths.Count > 0 &&
            string.IsNullOrWhiteSpace(request.RawText) &&
            request.Files is not { Count: > 0 })
        {
            return "Storage paths must be resolved to file content before AI extraction.";
        }

        if (request.StoragePaths.Count > MaxStoragePaths)
        {
            return $"No more than {MaxStoragePaths} storage paths can be extracted at once.";
        }

        if (request.Files is not { Count: > 0 })
        {
            return null;
        }

        if (request.Files.Count > MaxFiles)
        {
            return $"No more than {MaxFiles} files can be extracted at once.";
        }

        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.FileName) || file.FileName.Length > 255)
            {
                return "Each extraction file must include a file name of 255 characters or fewer.";
            }

            if (!AllowedExtractionMimeTypes.Contains(file.MimeType))
            {
                return $"Unsupported extraction file MIME type: {file.MimeType}";
            }

            if (string.IsNullOrWhiteSpace(file.Base64Data) || file.Base64Data.Length > MaxBase64CharactersPerFile)
            {
                return "Each extraction file must include bounded base64 data.";
            }
        }

        return null;
    }

    private static ExtractCustomerResponse MapToResponse(ExtractedCustomerData data)
    {
        return new ExtractCustomerResponse
        {
            FirstName = data.FirstName,
            LastName = data.LastName,
            Email = data.Email,
            Mobile = data.Mobile,
            Landline = data.Landline,
            Extension = data.Extension,
            Segment = data.Segment,
            CompanyName = data.CompanyName,
            CompanyPhone = data.CompanyPhone,
            VatNumber = data.VatNumber,
            BranchNumber = data.BranchNumber,
            Addresses = data.Addresses?.Select(a => new ExtractedAddressDto
            {
                Type = a.Type,
                AddressLine1 = a.AddressLine1,
                AddressLine2 = a.AddressLine2,
                AddressLine3 = a.AddressLine3,
                District = a.District,
                City = a.City,
                StateProvince = a.StateProvince,
                PostalCode = a.PostalCode,
                RecipientName = a.RecipientName,
                RecipientPhone = a.RecipientPhone
            }).ToList()

        };
    }
}
