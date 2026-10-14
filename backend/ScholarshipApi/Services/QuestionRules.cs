using System.Text.Json;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public static class QuestionRules
{
    /// <summary>A long answer with the <c>richText</c> rule: answered with the Tiptap editor, stored as sanitized HTML.</summary>
    public static bool IsRichText(Question q)
    {
        if (q.Type != "long_answer" || string.IsNullOrEmpty(q.ValidationRules)) return false;
        using var rules = JsonDocument.Parse(q.ValidationRules);
        return rules.RootElement.TryGetProperty("richText", out var v) && v.ValueKind == JsonValueKind.True;
    }
}
