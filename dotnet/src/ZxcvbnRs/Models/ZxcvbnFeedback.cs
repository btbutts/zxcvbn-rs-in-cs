using System.Text.Json.Serialization;

namespace ZxcvbnRs;

/// <summary>User-facing warning and improvement suggestions produced by <c>zxcvbn</c>.</summary>
public sealed record ZxcvbnFeedback
{
    /// <summary>Primary warning explaining why the password is weak, if any.</summary>
    [JsonPropertyName("warning")]
    public string? Warning { get; init; }

    /// <summary>Machine-readable warning identifier matching the Rust <c>Warning</c> enum variant, if any.</summary>
    [JsonPropertyName("warning_code")]
    public string? WarningCode { get; init; }

    /// <summary>Human-readable suggestions for strengthening the password.</summary>
    [JsonPropertyName("suggestions")]
    public IReadOnlyList<string> Suggestions { get; init; } = [];

    /// <summary>Machine-readable suggestion identifiers matching the Rust <c>Suggestion</c> enum variants.</summary>
    [JsonPropertyName("suggestion_codes")]
    public IReadOnlyList<string> SuggestionCodes { get; init; } = [];
}
