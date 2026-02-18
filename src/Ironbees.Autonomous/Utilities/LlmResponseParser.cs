using System.Text.Json;

namespace Ironbees.Autonomous.Utilities;

/// <summary>
/// Utility for parsing LLM responses, extracting JSON from mixed content.
/// Handles common LLM output patterns like markdown code blocks.
/// </summary>
public static class LlmResponseParser
{
    private static readonly JsonDocumentOptions LenientOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Extract JSON from LLM response (may contain markdown, text, etc.)
    /// </summary>
    public static string? ExtractJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        // Try to find JSON in markdown code block first
        var codeBlockStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (codeBlockStart >= 0)
        {
            var jsonStart = content.IndexOf('\n', codeBlockStart) + 1;
            var codeBlockEnd = content.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (codeBlockEnd > jsonStart)
            {
                return content[jsonStart..codeBlockEnd].Trim();
            }
        }

        // Try to find raw JSON object
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            return content[start..(end + 1)];
        }

        // Try JSON array
        start = content.IndexOf('[');
        end = content.LastIndexOf(']');

        if (start >= 0 && end > start)
        {
            return content[start..(end + 1)];
        }

        return null;
    }

    /// <summary>
    /// Parse JSON from LLM response into a JsonDocument
    /// </summary>
    public static JsonDocument? ParseJson(string content)
    {
        var json = ExtractJson(content);
        if (json == null) return null;

        try
        {
            return JsonDocument.Parse(json, LenientOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract a string property from LLM JSON response
    /// </summary>
    public static string? ExtractProperty(string content, string propertyName)
    {
        using var doc = ParseJson(content);
        if (doc == null) return null;

        return doc.RootElement.TryGetProperty(propertyName, out var prop)
            ? prop.GetString()
            : null;
    }

    /// <summary>
    /// Extract a boolean property from LLM JSON response
    /// </summary>
    public static bool ExtractBoolProperty(string content, string propertyName, bool defaultValue = false)
    {
        using var doc = ParseJson(content);
        if (doc == null) return defaultValue;

        return doc.RootElement.TryGetProperty(propertyName, out var prop)
            ? prop.GetBoolean()
            : defaultValue;
    }

    /// <summary>
    /// Extract a double property from LLM JSON response
    /// </summary>
    public static double ExtractDoubleProperty(string content, string propertyName, double defaultValue = 0.0)
    {
        using var doc = ParseJson(content);
        if (doc == null) return defaultValue;

        return doc.RootElement.TryGetProperty(propertyName, out var prop)
            ? prop.GetDouble()
            : defaultValue;
    }

    /// <summary>
    /// Try to deserialize JSON from LLM response to a specific type
    /// </summary>
    public static T? Deserialize<T>(string content, JsonSerializerOptions? options = null) where T : class
    {
        var json = ExtractJson(content);
        if (json == null) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultSerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract first question from content (ends with ?)
    /// </summary>
    public static string? ExtractQuestion(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var questionEnd = content.IndexOf('?');
        if (questionEnd < 0) return null;

        // Find start of question (after newline or start of content)
        var searchStart = Math.Max(0, questionEnd - 200);
        var questionStart = content.LastIndexOf('\n', questionEnd, questionEnd - searchStart);
        if (questionStart < 0) questionStart = 0;

        return content[(questionStart + 1)..(questionEnd + 1)].Trim();
    }

    /// <summary>
    /// Check if content contains a "yes" answer
    /// </summary>
    public static bool IsYesAnswer(string content)
    {
        var lower = content.ToLowerInvariant();
        return lower.Contains("\"yes\"") ||
               lower.StartsWith("yes", StringComparison.Ordinal) ||
               lower.Contains("\"answer\": \"yes\"") ||
               lower.Contains("\"answer\":\"yes\"");
    }

    /// <summary>
    /// Check if content contains a "no" answer
    /// </summary>
    public static bool IsNoAnswer(string content)
    {
        var lower = content.ToLowerInvariant();
        return lower.Contains("\"no\"") ||
               lower.StartsWith("no", StringComparison.Ordinal) ||
               lower.Contains("\"answer\": \"no\"") ||
               lower.Contains("\"answer\":\"no\"");
    }

    /// <summary>
    /// Default JSON serializer options
    /// </summary>
    public static JsonSerializerOptions DefaultSerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
