using Maliev.ChatbotService.Application.Interfaces;

namespace Maliev.ChatbotService.Application.Handlers;

/// <summary>
/// Recovers tool calls that the model wrote as text (Gemini "tool_code" style, e.g.
/// <c>print(quote_calculate_estimate())</c>) instead of emitting native function-call parts.
/// Only invocations whose names match a declared tool are recovered, and only when every
/// argument is a parseable Python-style keyword literal, so prose can never be mis-executed.
/// </summary>
public static class LeakedToolCallParser
{
    private const int MaxRecoveredCalls = 5;

    /// <summary>
    /// Parses leaked textual tool invocations out of <paramref name="content"/>.
    /// </summary>
    /// <param name="content">The model's text response.</param>
    /// <param name="declaredToolNames">Names of tools declared on the request.</param>
    /// <returns>Recovered calls in source order; empty when nothing parseable was found.</returns>
    public static List<GeminiFunctionCall> Parse(string? content, IReadOnlyCollection<string> declaredToolNames)
    {
        var calls = new List<GeminiFunctionCall>();
        if (string.IsNullOrWhiteSpace(content) || declaredToolNames is not { Count: > 0 })
        {
            return calls;
        }

        var names = declaredToolNames as ISet<string> ?? new HashSet<string>(declaredToolNames, StringComparer.Ordinal);
        var index = 0;
        while (index < content.Length && calls.Count < MaxRecoveredCalls)
        {
            if (!IsIdentifierStart(content[index]))
            {
                index++;
                continue;
            }

            var identifierStart = index;
            while (index < content.Length && IsIdentifierPart(content[index]))
            {
                index++;
            }

            var identifier = content[identifierStart..index];
            if (!names.Contains(identifier))
            {
                continue;
            }

            var openParen = SkipWhitespace(content, index);
            if (openParen >= content.Length || content[openParen] != '(')
            {
                continue;
            }

            if (!TryExtractBalancedArguments(content, openParen, out var argsText, out var closeParen))
            {
                continue;
            }

            if (!TryParseKeywordArguments(argsText, out var args))
            {
                continue;
            }

            calls.Add(new GeminiFunctionCall { Name = identifier, Args = args });
            index = closeParen + 1;
        }

        return calls;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool TryExtractBalancedArguments(string text, int openParen, out string argsText, out int closeParen)
    {
        argsText = string.Empty;
        closeParen = -1;
        var depth = 0;
        var inQuote = '\0';
        for (var i = openParen; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuote != '\0')
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == inQuote)
                {
                    inQuote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    inQuote = c;
                    break;
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    if (depth == 0)
                    {
                        if (c != ')')
                        {
                            return false;
                        }

                        argsText = text[(openParen + 1)..i];
                        closeParen = i;
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool TryParseKeywordArguments(string argsText, out Dictionary<string, object> args)
    {
        args = new Dictionary<string, object>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(argsText))
        {
            return true;
        }

        foreach (var part in SplitTopLevel(argsText))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            var equalsIndex = FindTopLevelEquals(trimmed);
            if (equalsIndex <= 0)
            {
                return false;
            }

            var name = trimmed[..equalsIndex].Trim();
            if (name.Length == 0 || !IsIdentifierStart(name[0]) || !name.All(IsIdentifierPart))
            {
                return false;
            }

            if (!TryParseLiteral(trimmed[(equalsIndex + 1)..].Trim(), out var value))
            {
                return false;
            }

            // Python None / JSON null kwargs are omitted; tool args are JSON objects where
            // an absent key and a null value are equivalent.
            if (value is not null)
            {
                args[name] = value;
            }
        }

        return true;
    }

    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var inQuote = '\0';
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuote != '\0')
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == inQuote)
                {
                    inQuote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    inQuote = c;
                    break;
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    parts.Add(text[start..i]);
                    start = i + 1;
                    break;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static int FindTopLevelEquals(string text)
    {
        var depth = 0;
        var inQuote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuote != '\0')
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == inQuote)
                {
                    inQuote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '\'' or '"':
                    inQuote = c;
                    break;
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    break;
                case '=' when depth == 0:
                    // Reject comparison operators (==, <=, >=, !=) so prose never parses.
                    if (i + 1 < text.Length && text[i + 1] == '=')
                    {
                        return -1;
                    }

                    return i > 0 && text[i - 1] is '<' or '>' or '!' ? -1 : i;
            }
        }

        return -1;
    }

    private static bool TryParseLiteral(string text, out object? value)
    {
        value = null;
        if (text.Length == 0)
        {
            return false;
        }

        if (text.Length >= 2 && (text[0] == '\'' || text[0] == '"') && text[^1] == text[0])
        {
            value = UnescapeString(text[1..^1], text[0]);
            return true;
        }

        switch (text)
        {
            case "True" or "true":
                value = true;
                return true;
            case "False" or "false":
                value = false;
                return true;
            case "None" or "null":
                value = null;
                return true;
        }

        if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var longValue))
        {
            value = longValue;
            return true;
        }

        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue))
        {
            value = doubleValue;
            return true;
        }

        if (text[0] == '[' && text[^1] == ']')
        {
            var items = new List<object?>();
            var inner = text[1..^1].Trim();
            if (inner.Length == 0)
            {
                value = items;
                return true;
            }

            foreach (var part in SplitTopLevel(inner))
            {
                if (!TryParseLiteral(part.Trim(), out var item))
                {
                    return false;
                }

                items.Add(item);
            }

            value = items;
            return true;
        }

        return false;
    }

    private static string UnescapeString(string text, char quote)
    {
        if (!text.Contains('\\'))
        {
            return text;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c != '\\' || i + 1 >= text.Length)
            {
                builder.Append(c);
                continue;
            }

            i++;
            builder.Append(text[i] switch
            {
                'n' => '\n',
                't' => '\t',
                'r' => '\r',
                '\\' => '\\',
                _ when text[i] == quote => quote,
                _ => text[i]
            });
        }

        return builder.ToString();
    }
}
