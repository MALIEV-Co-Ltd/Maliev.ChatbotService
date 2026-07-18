using System.Text.Json;

namespace Maliev.ChatbotService.Application.Handlers;

internal static class ToolTerminalResponseBuilder
{
    internal static string Build(IReadOnlyList<ToolExecutionResult> toolResults)
    {
        for (var index = toolResults.Count - 1; index >= 0; index--)
        {
            if (TryBuild(toolResults[index], out var response))
            {
                return response;
            }
        }

        return "I couldn't prepare a reliable customer-facing summary from the tool result. Please try again.";
    }

    private static bool TryBuild(ToolExecutionResult toolResult, out string response)
    {
        response = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(toolResult.ResponseJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var markdownTable = ReadJsonString(root, "markdownTable", "markdown_table");
            if (TrySanitizeCustomerText(markdownTable, out var safeTable))
            {
                var selectionInstructions = ReadJsonString(
                    root,
                    "selectionInstructions",
                    "selection_instructions");
                response = TrySanitizeCustomerText(selectionInstructions, out var safeInstructions)
                    ? $"{safeTable}\n\n{safeInstructions}"
                    : safeTable;
                return true;
            }

            if (toolResult.Name.Equals("quote_calculate_estimate", StringComparison.Ordinal) &&
                TryReadEstimateSummary(root, out var estimateSummary))
            {
                response = estimateSummary;
                return true;
            }

            var customerMessage = ReadJsonString(
                root,
                "customerMessage",
                "customer_message",
                "displayMessage",
                "display_message");
            if (TrySanitizeCustomerText(customerMessage, out var safeMessage))
            {
                response = safeMessage;
                return true;
            }

            if (TryGetProperty(root, "error", out _))
            {
                response = "I couldn't complete that step with the available information. Please check the required details and try again.";
                return true;
            }

            if (TryGetProperty(root, "success", out var success) && success.ValueKind == JsonValueKind.True)
            {
                response = "The requested step completed successfully.";
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryReadEstimateSummary(JsonElement root, out string response)
    {
        response = string.Empty;
        var estimate = root;
        if (TryGetProperty(root, "estimate", out var nestedEstimate) &&
            nestedEstimate.ValueKind == JsonValueKind.Object)
        {
            estimate = nestedEstimate;
        }

        if (!TryReadJsonDecimal(
                estimate,
                out var total,
                "totalPrice",
                "total_price",
                "estimatedTotal",
                "estimated_total",
                "grandTotal",
                "grand_total",
                "total") ||
            total < 0)
        {
            return false;
        }

        var currency = ReadJsonString(estimate, "currency") ?? ReadJsonString(root, "currency");
        currency = new string((currency ?? string.Empty)
            .Where(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
            .Take(4)
            .ToArray())
            .ToUpperInvariant();
        if (currency.Length != 3)
        {
            return false;
        }

        response = $"The current estimate is {total.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture)} {currency}.";
        return true;
    }

    private static bool TryReadJsonDecimal(
        JsonElement element,
        out decimal value,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var candidate))
            {
                continue;
            }

            if (candidate.ValueKind == JsonValueKind.Number && candidate.TryGetDecimal(out value))
            {
                return true;
            }

            if (candidate.ValueKind == JsonValueKind.String &&
                decimal.TryParse(
                    candidate.GetString(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static string? ReadJsonString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TrySanitizeCustomerText(string? value, out string sanitized)
    {
        sanitized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        sanitized = new string(value
            .Where(character => !char.IsControl(character) || character is '\r' or '\n' or '\t')
            .Take(6000)
            .ToArray())
            .Trim();
        return sanitized.Length > 0;
    }
}

internal sealed record ToolExecutionResult(string Name, string ResponseJson);
