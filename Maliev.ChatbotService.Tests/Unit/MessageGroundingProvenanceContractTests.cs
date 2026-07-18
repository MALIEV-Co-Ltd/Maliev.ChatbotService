using System.Reflection;
using System.Text.Json;
using Maliev.ChatbotService.Api.Controllers.V1;
using Maliev.ChatbotService.Api.Models.Responses;
using Maliev.ChatbotService.Application.Handlers;
using Maliev.ChatbotService.Application.Interfaces;
using Maliev.ChatbotService.Application.Models;
using Maliev.ChatbotService.Domain.Enums;

namespace Maliev.ChatbotService.Tests.Unit;

public sealed class MessageGroundingProvenanceContractTests
{
    [Fact]
    public void FinalStreamMessage_MapsNullableGroundingProvenanceWithSnakeCaseSources()
    {
        var result = new SendMessageResult
        {
            MessageId = Guid.NewGuid(),
            Content = "The address was grounded.",
            Role = MessageRole.Assistant,
            Language = Language.English,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var resultProperty = typeof(SendMessageResult).GetProperty("GroundingProvenance");
        Assert.NotNull(resultProperty);
        resultProperty!.SetValue(result, new GroundingProvenance
        {
            Purpose = "shipping_address_validation",
            Status = "grounded",
            Queries = ["Nonthaburi 11120 address"],
            Sources =
            [
                new GeminiGroundingSource
                {
                    Title = "Public address source",
                    Url = "https://example.com/address",
                    Domain = "example.com"
                }
            ]
        });

        var mapper = typeof(MessagesController).GetMethod(
            "MapMessageResponse",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(mapper);
        var response = Assert.IsType<MessageResponse>(mapper!.Invoke(null, [result]));
        var responseProperty = typeof(MessageResponse).GetProperty("GroundingProvenance");
        Assert.NotNull(responseProperty);
        var mappedProvenance = responseProperty!.GetValue(response);
        Assert.NotNull(mappedProvenance);
        Assert.Equal("grounded", mappedProvenance!.GetType().GetProperty("Status")?.GetValue(mappedProvenance));

        var json = JsonSerializer.Serialize(
            new MessageStreamEvent { Type = "final", Message = response },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        Assert.Contains("\"grounding_provenance\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"grounded\"", json, StringComparison.Ordinal);
        Assert.Contains("\"purpose\":\"shipping_address_validation\"", json, StringComparison.Ordinal);
        Assert.Contains("\"provider\":\"google_search\"", json, StringComparison.Ordinal);
        Assert.Contains("\"queries\":[\"Nonthaburi 11120 address\"]", json, StringComparison.Ordinal);
        Assert.Contains("\"url\":\"https://example.com/address\"", json, StringComparison.Ordinal);
        Assert.Contains("\"domain\":\"example.com\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistedAssistantMetadata_RestoresGroundingProvenanceForConversationHistory()
    {
        var metadata = JsonSerializer.Serialize(new
        {
            groundingMetadata = new
            {
                provenance = new GroundingProvenance
                {
                    Purpose = "shipping_address_validation",
                    Provider = "google_search",
                    Status = "grounded",
                    ShippingAddress = new GroundedShippingAddressEvidence
                    {
                        Subdistrict = "khlongkhoi",
                        District = "pakkret",
                        Province = "nonthaburi",
                        Postcode = "11120"
                    },
                    Queries = ["Khlong Khoi Pak Kret Nonthaburi 11120"],
                    Sources =
                    [
                        new GeminiGroundingSource
                        {
                            Title = "Public address source",
                            Url = "https://example.com/address",
                            Domain = "example.com"
                        }
                    ]
                }
            }
        });

        var provenance = MessageGroundingMetadata.TryReadProvenance(metadata);
        var historyMessage = new ConversationHistoryMessage
        {
            MessageId = Guid.NewGuid(),
            Role = "assistant",
            Content = "The address was validated.",
            ContentType = "text",
            CreatedAt = DateTimeOffset.UtcNow,
            GroundingProvenance = provenance
        };

        Assert.NotNull(historyMessage.GroundingProvenance);
        Assert.Equal("grounded", historyMessage.GroundingProvenance.Status);
        Assert.NotNull(historyMessage.GroundingProvenance.ShippingAddress);
        Assert.Equal("khlongkhoi", historyMessage.GroundingProvenance.ShippingAddress.Subdistrict);
        Assert.Equal("pakkret", historyMessage.GroundingProvenance.ShippingAddress.District);
        Assert.Equal("nonthaburi", historyMessage.GroundingProvenance.ShippingAddress.Province);
        Assert.Equal("11120", historyMessage.GroundingProvenance.ShippingAddress.Postcode);
        Assert.Equal("example.com", Assert.Single(historyMessage.GroundingProvenance.Sources).Domain);
    }

    [Fact]
    public void PersistedAssistantMetadata_DiscardsMalformedShippingPostcodeEvidence()
    {
        var metadata = JsonSerializer.Serialize(new
        {
            groundingMetadata = new
            {
                provenance = new GroundingProvenance
                {
                    Purpose = "shipping_address_validation",
                    Provider = "google_search",
                    Status = "grounded",
                    ShippingAddress = new GroundedShippingAddressEvidence
                    {
                        Subdistrict = "khlongkhoi",
                        District = "pakkret",
                        Province = "nonthaburi",
                        Postcode = "111200"
                    }
                }
            }
        });

        var provenance = MessageGroundingMetadata.TryReadProvenance(metadata);

        Assert.NotNull(provenance);
        Assert.Null(provenance.ShippingAddress);
    }
}
