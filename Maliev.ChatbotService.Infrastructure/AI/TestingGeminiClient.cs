using System.Text.Json;
using Maliev.ChatbotService.Application.Interfaces;
using System.Runtime.CompilerServices;

namespace Maliev.ChatbotService.Infrastructure.AI;

/// <summary>
/// Deterministic Gemini client used only in the local Testing environment.
/// </summary>
public sealed class TestingGeminiClient : IGeminiClient
{
    /// <inheritdoc />
    public Task<GeminiResponse> SendMessageAsync(GeminiRequest request, CancellationToken cancellationToken = default)
    {
        var lastMessage = request.Messages.LastOrDefault()?.Content ?? request.Prompt ?? string.Empty;
        var normalizedMessage = lastMessage.ToLowerInvariant();

        var content = BuildContent(request, normalizedMessage);

        return Task.FromResult(new GeminiResponse
        {
            Success = true,
            Content = content,
            TokenUsage = new GeminiTokenUsage { TotalTokens = 42 }
        });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<GeminiStreamEvent> StreamMessageAsync(
        GeminiRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new GeminiStreamEvent { Type = "started" };
        var response = await SendMessageAsync(request, cancellationToken);
        foreach (var chunk in Chunk(response.Content))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new GeminiStreamEvent { Type = "delta", Delta = chunk };
        }

        yield return new GeminiStreamEvent { Type = "final", Response = response };
    }

    private static string BuildContent(GeminiRequest request, string normalizedMessage)
    {
        if (request.SystemInstruction.Contains("intent classifier", StringComparison.OrdinalIgnoreCase))
        {
            var intent = "General";
            if (normalizedMessage.Contains("customer", StringComparison.OrdinalIgnoreCase))
            {
                intent = "CustomerManagement";
            }
            else if (normalizedMessage.Contains("quote", StringComparison.OrdinalIgnoreCase) ||
                     normalizedMessage.Contains("quotation", StringComparison.OrdinalIgnoreCase))
            {
                intent = "Quotation";
            }

            return JsonSerializer.Serialize(new
            {
                intent,
                confidence = 0.99,
                additionalTopics = Array.Empty<string>()
            });
        }

        if (request.ResponseMimeType == "application/json" &&
            (request.SystemInstruction.Contains("determine if they need customer data", StringComparison.OrdinalIgnoreCase) ||
             request.SystemInstruction.Contains("customer data lookup", StringComparison.OrdinalIgnoreCase) ||
             request.SystemInstruction.Contains("needs_customer_data", StringComparison.OrdinalIgnoreCase)))
        {
            return JsonSerializer.Serialize(new
            {
                needs_customer_data = normalizedMessage.Contains("customer", StringComparison.OrdinalIgnoreCase),
                customer_search_term = (string?)null,
                needs_history = normalizedMessage.Contains("history", StringComparison.OrdinalIgnoreCase)
            });
        }

        if (request.ResponseMimeType == "application/json" &&
            request.SystemInstruction.Contains("system instruction refiner", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                persona_definition = "Refined system instruction prompt for the MALIEV assistant.",
                business_constraints = "Refined customer-safe guardrails for MALIEV manufacturing conversations.",
                summary = "Clarified instruction scope and tightened customer-safe constraints."
            });
        }

        if (request.ResponseMimeType == "application/json" &&
            request.SystemInstruction.Contains("Extract customer information", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                first_name = "Aspire",
                last_name = "Customer",
                email = "aspire-customer@example.com",
                company_name = "Aspire Manufacturing Co.",
                segment = "Manufacturing",
                addresses = new[]
                {
                    new
                    {
                        type = "Shipping",
                        address_line_1 = "100 Testing Road",
                        city = "Bangkok",
                        postal_code = "10500"
                    }
                }
            });
        }

        return normalizedMessage.Contains("สวัสดี", StringComparison.OrdinalIgnoreCase)
            ? "สวัสดีครับ นี่คือคำตอบทดสอบสำหรับสภาพแวดล้อม Aspire"
            : "This is a deterministic Aspire Testing response from the MALIEV assistant.";
    }

    private static IEnumerable<string> Chunk(string text)
    {
        const int chunkSize = 16;
        for (var index = 0; index < text.Length; index += chunkSize)
        {
            yield return text.Substring(index, Math.Min(chunkSize, text.Length - index));
        }
    }
}
