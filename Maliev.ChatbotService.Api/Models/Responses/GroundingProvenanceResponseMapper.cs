using Maliev.ChatbotService.Application.Models;

namespace Maliev.ChatbotService.Api.Models.Responses;

internal static class GroundingProvenanceResponseMapper
{
    internal static GroundingProvenanceResponse? Map(GroundingProvenance? provenance)
    {
        if (provenance is null)
        {
            return null;
        }

        return new GroundingProvenanceResponse
        {
            Purpose = provenance.Purpose,
            Provider = provenance.Provider,
            Status = provenance.Status,
            Queries = provenance.Queries.ToList(),
            ErrorCode = provenance.ErrorCode,
            Sources = provenance.Sources
                .Select(source => new GroundingSourceResponse
                {
                    Title = source.Title,
                    Url = source.Url,
                    Domain = source.Domain
                })
                .ToList()
        };
    }
}
