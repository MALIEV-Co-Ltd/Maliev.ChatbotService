using System.Text.Json.Serialization;

namespace Maliev.ChatbotService.Api.Models.Responses;

/// <summary>Structured LLM assessment of design complexity for pricing estimation.</summary>
public class DesignAnalysisResult
{
    /// <summary>
    /// Primary CAD modeling technique required.
    /// Values: "StandardParametric" (XYZ extrude/loft/revolve),
    /// "NonStandardPlane" (custom angled planes),
    /// "ComplexSurface" (freeform surface modeling),
    /// "MeshSculpting" (art toys, organic shapes — ZBrush),
    /// "SheetMetal" (fold/unfold patterns for laser cutting).
    /// </summary>
    [JsonPropertyName("modelingTechnique")]
    public required string ModelingTechnique { get; init; }

    /// <summary>Estimated hours for the engineer to create the 3D CAD model.</summary>
    [JsonPropertyName("estimatedDesignHours")]
    public decimal EstimatedDesignHours { get; init; }

    /// <summary>Whether 3D scanning of a physical object is required before design can begin.</summary>
    [JsonPropertyName("requiresPreScan")]
    public bool RequiresPreScan { get; init; }

    /// <summary>Explanation of why a pre-scan is needed, if applicable.</summary>
    [JsonPropertyName("preScanReason")]
    public string? PreScanReason { get; init; }

    /// <summary>
    /// How complete the supplied design requirements are.
    /// Values: "Complete", "PartiallyComplete", "Insufficient".
    /// </summary>
    [JsonPropertyName("informationCompleteness")]
    public required string InformationCompleteness { get; init; }

    /// <summary>Details about missing information that requires customer clarification.</summary>
    [JsonPropertyName("missingInformationNotes")]
    public string? MissingInformationNotes { get; init; }

    /// <summary>Estimated additional hours for customer communication to gather missing info.</summary>
    [JsonPropertyName("additionalCommunicationHours")]
    public decimal AdditionalCommunicationHours { get; init; }

    /// <summary>Whether 2D technical drawings are included in scope.</summary>
    [JsonPropertyName("requiresDrawings")]
    public bool RequiresDrawings { get; init; }

    /// <summary>Estimated hours to produce 2D technical drawings.</summary>
    [JsonPropertyName("estimatedDrawingHours")]
    public decimal EstimatedDrawingHours { get; init; }

    /// <summary>Estimated count of repetitive features (ribs, bosses, holes) that add design time.</summary>
    [JsonPropertyName("estimatedRepetitiveFeatureCount")]
    public int EstimatedRepetitiveFeatureCount { get; init; }

    /// <summary>Notes about repetitive features.</summary>
    [JsonPropertyName("repetitiveFeatureNotes")]
    public string? RepetitiveFeatureNotes { get; init; }

    /// <summary>
    /// Recommended CAD software for this design.
    /// Values: "Fusion360", "Fusion360FormWorkspace", "Fusion360SheetMetal", "ZBrush".
    /// </summary>
    [JsonPropertyName("recommendedSoftware")]
    public required string RecommendedSoftware { get; init; }

    /// <summary>LLM confidence in this assessment, 0.0 to 1.0.</summary>
    [JsonPropertyName("confidenceLevel")]
    public decimal ConfidenceLevel { get; init; }

    /// <summary>Free-text notes and observations from the analysis.</summary>
    [JsonPropertyName("notes")]
    public string Notes { get; init; } = string.Empty;
}
