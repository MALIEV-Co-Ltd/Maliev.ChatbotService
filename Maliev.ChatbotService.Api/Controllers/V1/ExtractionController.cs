using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Commands;
using Maliev.ChatbotService.Application.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for AI-powered data extraction operations.
/// </summary>
[ApiController]
[Route("chatbot/v1/extraction")]
[Authorize]
public class ExtractionController : ControllerBase
{
    private readonly ExtractCustomerCommandHandler _handler;
    private readonly ExtractCustomerIntentCommandHandler _intentHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionController"/> class.
    /// </summary>
    /// <param name="handler">The extract customer command handler.</param>
    /// <param name="intentHandler">The extract customer intent command handler.</param>
    public ExtractionController(ExtractCustomerCommandHandler handler, ExtractCustomerIntentCommandHandler intentHandler)
    {
        _handler = handler;
        _intentHandler = intentHandler;
    }

    /// <summary>
    /// Extracts customer data from uploaded documents or text using AI.
    /// </summary>
    /// <param name="request">The extraction request containing file paths and/or text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted customer data.</returns>
    [HttpPost("customer")]
    [ProducesResponseType(typeof(ExtractCustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(typeof(ExtractCustomerIntentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExtractCustomerIntentResponse>> ExtractCustomerIntent(
        [FromBody] ExtractCustomerIntentRequest request,
        CancellationToken ct)
    {
        var command = new Application.Commands.ExtractCustomerIntentCommand
        {
            UserMessage = request.UserMessage
        };

        var result = await _intentHandler.HandleAsync(command, ct);

        if (!result.Success)
        {
            return StatusCode(500, result.ErrorMessage);
        }

        return Ok(new ExtractCustomerIntentResponse
        {
            NeedsCustomerData = result.NeedsCustomerData,
            CustomerSearchTerm = result.CustomerSearchTerm,
            NeedsHistory = result.NeedsHistory
        });
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
