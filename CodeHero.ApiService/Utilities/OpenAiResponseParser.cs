using System.Text.Json;

namespace CodeHero.ApiService.Utilities;

internal static class OpenAiResponseParser
{
    // Try to extract choices[0].message.content as string. Returns null if not present or parse error.
    public static string? TryGetFirstChoiceContent(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)) return null;
            if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return null;
            var first = choices[0];
            if (first.ValueKind != JsonValueKind.Object) return null;
            if (!first.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return null;
            if (!message.TryGetProperty("content", out var contentEl) || contentEl.ValueKind != JsonValueKind.String) return null;
            return contentEl.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }
}