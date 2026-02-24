using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Api.Models.Requests;
using Maliev.ChatbotService.Api.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Maliev.ChatbotService.Api.Controllers.V1;

/// <summary>
/// Controller for vision-related tasks such as bank slip analysis and design complexity analysis.
/// </summary>
[ApiController]
[Route("chatbot/v1/vision")]
public class VisionController : ControllerBase
{
    private readonly ILogger<VisionController> _logger;
    private readonly IGeminiClient _geminiClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="VisionController"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="geminiClient">The Gemini client instance.</param>
    public VisionController(ILogger<VisionController> logger, IGeminiClient geminiClient)
    {
        _logger = logger;
        _geminiClient = geminiClient;
    }

    /// <summary>
    /// Analyzes a bank transfer slip image to extract payment details.
    /// </summary>
    /// <param name="request">The analysis request containing the image URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the slip analysis.</returns>
    [HttpPost("analyze-slip")]
    public async Task<ActionResult<SlipAnalysisResult>> AnalyzeSlip(
        [FromBody] AnalyzeSlipRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing slip image: {ImageUrl}", request.ImageUrl);
        
        try
        {
            const string systemPrompt = """
                Analyze this Thai bank transfer slip image. Extract the following information and return ONLY a JSON object:
                {
                  "isValid": boolean (true if this is a legitimate bank transfer slip),
                  "extractedAmountThb": number or null (transfer amount in Thai Baht),
                  "bankName": string or null (name of the sending or receiving bank),
                  "transferDate": string or null (date in YYYY-MM-DD format),
                  "notes": string (any relevant observations)
                }
                """;

            var geminiRequest = new GeminiRequest
            {
                SystemInstruction = systemPrompt,
                Messages = new List<GeminiMessage>
                {
                    new GeminiMessage
                    {
                        Role = "user",
                        Content = "Analyze this bank slip.",
                        Attachments = new List<GeminiAttachment>
                        {
                            new GeminiAttachment
                            {
                                ContentType = "Image",
                                Data = request.ImageUrl,
                                MimeType = "image/jpeg"
                            }
                        }
                    }
                },
                ResponseMimeType = "application/json"
            };

            var response = await _geminiClient.SendMessageAsync(geminiRequest, cancellationToken);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
            {
                _logger.LogWarning("Gemini analysis failed or returned empty: {ErrorMessage}", response.ErrorMessage);
                return Ok(new SlipAnalysisResult
                {
                    IsValid = false,
                    Notes = $"Vision analysis failed: {response.ErrorMessage ?? "Empty response"}. Manual verification required."
                });
            }

            _logger.LogInformation("Gemini response: {Response}", response.Content);

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var result = JsonSerializer.Deserialize<SlipAnalysisResult>(response.Content, options);

                if (result == null)
                {
                    throw new JsonException("Deserialized result is null");
                }

                return Ok(result);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini response as SlipAnalysisResult: {Content}", response.Content);
                return Ok(new SlipAnalysisResult
                {
                    IsValid = false,
                    Notes = "Failed to parse vision analysis. Manual verification required."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during slip analysis for {ImageUrl}", request.ImageUrl);
            return Ok(new SlipAnalysisResult
            {
                IsValid = false,
                Notes = $"Vision analysis unavailable: {ex.Message}. Manual verification required."
            });
        }
    }

    /// <summary>Analyze design images/drawings to estimate CAD modeling complexity and effort.</summary>
    [HttpPost("analyze-design")]
    public async Task<ActionResult<DesignAnalysisResult>> AnalyzeDesign(
        [FromBody] AnalyzeDesignRequest request, CancellationToken cancellationToken)
    {
        if (request.ImageUrls == null || request.ImageUrls.Count == 0)
        {
            return BadRequest("At least one image URL is required.");
        }

        try
        {
            const string systemPrompt = """
                You are a manufacturing design analyst at a 3D printing and CNC service bureau.
                Analyze the provided images/drawings and description to assess the CAD design effort required.

                Evaluate these factors:

                1. MODELING TECHNIQUE - What primary CAD approach is needed?
                   - "StandardParametric": Standard XYZ-axis extrude, loft, revolve commands only
                   - "NonStandardPlane": Features on custom/angled planes (e.g., extrude at 15° from X-axis)
                   - "ComplexSurface": Freeform surface modeling techniques required
                   - "MeshSculpting": Art toy or organic shape better suited for direct mesh modeling
                   - "SheetMetal": Sheet metal design requiring fold/unfold patterns for laser cutting

                2. ESTIMATED DESIGN HOURS - How many hours for an engineer to create the 3D CAD model?
                   Consider feature count, complexity, and non-standard operations.

                3. PRE-SCAN REQUIREMENT - Does this require 3D scanning a physical object first?
                   Examples: housing for a custom PCB, part that must fit an unknown external component.

                4. INFORMATION COMPLETENESS - Does the provided info contain all necessary dimensions?
                   - "Complete": All dimensions and specs present, ready to design
                   - "PartiallyComplete": Some dimensions missing but reasonable assumptions possible
                   - "Insufficient": Major info gaps requiring customer follow-up

                5. REPETITIVE FEATURES - Count of similar features (ribs, bosses, holes, fillets) that
                   each require individual creation. E.g., a part with 100+ ribs takes significant time.

                6. RECOMMENDED SOFTWARE:
                   - "Fusion360" for standard parametric mechanical parts
                   - "Fusion360FormWorkspace" for organic shapes achievable with T-splines
                   - "Fusion360SheetMetal" for sheet metal with fold/unfold patterns
                   - "ZBrush" for complex art toys and sculptures

                7. 2D DRAWINGS - If the customer requests 2D drawings, estimate additional hours.

                Return ONLY a JSON object with these exact fields:
                {
                  "modelingTechnique": string,
                  "estimatedDesignHours": number,
                  "requiresPreScan": boolean,
                  "preScanReason": string or null,
                  "informationCompleteness": string,
                  "missingInformationNotes": string or null,
                  "additionalCommunicationHours": number,
                  "requiresDrawings": boolean,
                  "estimatedDrawingHours": number,
                  "estimatedRepetitiveFeatureCount": number,
                  "repetitiveFeatureNotes": string or null,
                  "recommendedSoftware": string,
                  "confidenceLevel": number (0.0-1.0),
                  "notes": string
                }

                Be conservative with time estimates. A simple bracket is ~1-2 hours.
                A complex multi-feature enclosure is ~8-15 hours.
                An art toy character is ~15-30 hours.
                """;

            var userMessage = string.IsNullOrWhiteSpace(request.Description)
                ? "Analyze the attached design images."
                : $"Description: {request.Description}\n\nAnalyze the attached design images.";

            var attachments = request.ImageUrls.Select(url => new GeminiAttachment
            {
                ContentType = "Image",
                Data = url,
                MimeType = "image/jpeg"
            }).ToList();

            var geminiRequest = new GeminiRequest
            {
                SystemInstruction = systemPrompt,
                Messages = new List<GeminiMessage>
                {
                    new GeminiMessage
                    {
                        Role = "user",
                        Content = userMessage,
                        Attachments = attachments
                    }
                },
                ResponseMimeType = "application/json",
                TimeoutSeconds = 60
            };

            var geminiResponse = await _geminiClient.SendMessageAsync(geminiRequest, cancellationToken);

            if (geminiResponse.Success && !string.IsNullOrWhiteSpace(geminiResponse.Content))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<DesignAnalysisResult>(geminiResponse.Content, options);
                if (result != null)
                {
                    return Ok(result);
                }
            }

            _logger.LogWarning("Gemini design analysis failed or returned empty: {ErrorMessage}", geminiResponse.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during design complexity analysis");
        }

        // Conservative defaults on failure
        return Ok(new DesignAnalysisResult
        {
            ModelingTechnique = "StandardParametric",
            EstimatedDesignHours = 5.0m,
            RequiresPreScan = false,
            InformationCompleteness = "PartiallyComplete",
            AdditionalCommunicationHours = 1.0m,
            RequiresDrawings = request.RequiresDrawings,
            EstimatedDrawingHours = request.RequiresDrawings ? 2.0m : 0m,
            EstimatedRepetitiveFeatureCount = 0,
            RecommendedSoftware = "Fusion360",
            ConfidenceLevel = 0.3m,
            Notes = "LLM analysis failed. Returning conservative defaults."
        });
    }
}
