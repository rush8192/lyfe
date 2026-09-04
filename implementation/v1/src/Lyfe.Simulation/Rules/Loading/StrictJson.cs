using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Lyfe.Simulation.Rules.Loading;

internal static class StrictJson
{
    private const int MaximumDepth = 64;
    private const int MaximumStringTokenBytes = 16 * 1024;

    public static T? Deserialize<T>(
        string sourceFile,
        ReadOnlyMemory<byte> content,
        JsonTypeInfo<T> typeInfo,
        ICollection<RuleDiagnostic> diagnostics)
    {
        if (!ValidateTokens(sourceFile, content.Span, diagnostics))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize(content.Span, typeInfo);
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new RuleDiagnostic(
                RuleDiagnosticSeverity.Error,
                "LYFE-CONTENT-JSON-001",
                exception.Message,
                sourceFile,
                checked((int)(exception.LineNumber ?? 0) + 1),
                checked((int)(exception.BytePositionInLine ?? 0) + 1),
                exception.Path));
            return default;
        }
    }

    private static bool ValidateTokens(
        string sourceFile,
        ReadOnlySpan<byte> content,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var valid = true;
        var objectProperties = new Stack<HashSet<string>>();
        var reader = new Utf8JsonReader(
            content,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumDepth,
            });

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;
                    case JsonTokenType.EndObject:
                        objectProperties.Pop();
                        break;
                    case JsonTokenType.PropertyName:
                        {
                            var propertyName = reader.GetString() ?? string.Empty;
                            if (objectProperties.Count > 0 && !objectProperties.Peek().Add(propertyName))
                            {
                                var location = GetLocation(content, reader.TokenStartIndex);
                                diagnostics.Add(new RuleDiagnostic(
                                    RuleDiagnosticSeverity.Error,
                                    "LYFE-CONTENT-JSON-002",
                                    $"Duplicate property '{propertyName}' is not allowed.",
                                    sourceFile,
                                    location.Line,
                                    location.Column));
                                valid = false;
                            }

                            if (reader.ValueSpan.Length > MaximumStringTokenBytes)
                            {
                                AddTokenTooLongDiagnostic(sourceFile, content, reader, diagnostics);
                                valid = false;
                            }

                            break;
                        }
                    case JsonTokenType.String:
                        if (reader.ValueSpan.Length > MaximumStringTokenBytes)
                        {
                            AddTokenTooLongDiagnostic(sourceFile, content, reader, diagnostics);
                            valid = false;
                        }

                        break;
                    case JsonTokenType.Number:
                        if (reader.ValueSpan.IndexOfAny((byte)'.', (byte)'e', (byte)'E') >= 0)
                        {
                            var location = GetLocation(content, reader.TokenStartIndex);
                            diagnostics.Add(new RuleDiagnostic(
                                RuleDiagnosticSeverity.Error,
                                "LYFE-CONTENT-JSON-004",
                                "Authoritative numeric values must use integer JSON tokens without decimals or exponents.",
                                sourceFile,
                                location.Line,
                                location.Column));
                            valid = false;
                        }

                        break;
                }
            }
        }
        catch (JsonException exception)
        {
            diagnostics.Add(new RuleDiagnostic(
                RuleDiagnosticSeverity.Error,
                "LYFE-CONTENT-JSON-001",
                exception.Message,
                sourceFile,
                checked((int)(exception.LineNumber ?? 0) + 1),
                checked((int)(exception.BytePositionInLine ?? 0) + 1)));
            return false;
        }

        return valid;
    }

    private static void AddTokenTooLongDiagnostic(
        string sourceFile,
        ReadOnlySpan<byte> content,
        Utf8JsonReader reader,
        ICollection<RuleDiagnostic> diagnostics)
    {
        var location = GetLocation(content, reader.TokenStartIndex);
        diagnostics.Add(new RuleDiagnostic(
            RuleDiagnosticSeverity.Error,
            "LYFE-CONTENT-JSON-003",
            $"String token exceeds the {MaximumStringTokenBytes}-byte limit.",
            sourceFile,
            location.Line,
            location.Column));
    }

    private static (int Line, int Column) GetLocation(ReadOnlySpan<byte> content, long tokenStartIndex)
    {
        var line = 1;
        var column = 1;
        var end = checked((int)tokenStartIndex);
        for (var index = 0; index < end; index++)
        {
            if (content[index] == (byte)'\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        return (line, column);
    }
}

