using System.Text.Json.Serialization;

namespace ZxcvbnRs;

/// <summary>A single pattern match inside the optimal <c>zxcvbn</c> guess sequence.</summary>
public sealed record ZxcvbnMatch
{
    /// <summary>Zero-based inclusive start character index of the match.</summary>
    [JsonPropertyName("i")]
    public int I { get; init; }

    /// <summary>Zero-based inclusive end character index of the match.</summary>
    [JsonPropertyName("j")]
    public int J { get; init; }

    /// <summary>Substring token matched by this pattern.</summary>
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    /// <summary>Pattern category (e.g., <c>dictionary</c>, <c>spatial</c>, <c>repeat</c>, <c>sequence</c>, <c>regex</c>, <c>date</c>, <c>bruteforce</c>).</summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; init; } = string.Empty;

    /// <summary>Estimated guesses for this individual token match.</summary>
    [JsonPropertyName("guesses")]
    public ulong? Guesses { get; init; }

    /// <summary>Matched dictionary word (for <c>dictionary</c> matches).</summary>
    [JsonPropertyName("matched_word")]
    public string? MatchedWord { get; init; }

    /// <summary>Frequency rank of <see cref="MatchedWord"/> within its dictionary.</summary>
    [JsonPropertyName("rank")]
    public ulong? Rank { get; init; }

    /// <summary>Name of the matched dictionary (e.g., <c>Passwords</c>, <c>English</c>, <c>UserInputs</c>).</summary>
    [JsonPropertyName("dictionary_name")]
    public string? DictionaryName { get; init; }

    /// <summary>Whether a dictionary match was reversed.</summary>
    [JsonPropertyName("reversed")]
    public bool? Reversed { get; init; }

    /// <summary>Whether a dictionary match used l33t character substitutions.</summary>
    [JsonPropertyName("l33t")]
    public bool? L33t { get; init; }

    /// <summary>Map of l33t substitution characters to plaintext characters, when applicable.</summary>
    [JsonPropertyName("sub")]
    public IReadOnlyDictionary<string, string>? Sub { get; init; }

    /// <summary>Formatted display summary of l33t substitutions.</summary>
    [JsonPropertyName("sub_display")]
    public string? SubDisplay { get; init; }

    /// <summary>Multiplier for uppercase variations (dictionary matches).</summary>
    [JsonPropertyName("uppercase_variations")]
    public ulong? UppercaseVariations { get; init; }

    /// <summary>Multiplier for l33t variations (dictionary matches).</summary>
    [JsonPropertyName("l33t_variations")]
    public ulong? L33tVariations { get; init; }

    /// <summary>Base guesses prior to variations (dictionary and repeat matches).</summary>
    [JsonPropertyName("base_guesses")]
    public ulong? BaseGuesses { get; init; }

    /// <summary>Keyboard adjacency graph name (spatial matches).</summary>
    [JsonPropertyName("graph")]
    public string? Graph { get; init; }

    /// <summary>Number of directional turns on the keyboard graph (spatial matches).</summary>
    [JsonPropertyName("turns")]
    public int? Turns { get; init; }

    /// <summary>Number of shifted characters (spatial matches).</summary>
    [JsonPropertyName("shifted_count")]
    public int? ShiftedCount { get; init; }

    /// <summary>Repeating base token (repeat matches).</summary>
    [JsonPropertyName("base_token")]
    public string? BaseToken { get; init; }

    /// <summary>Sub-matches describing <see cref="BaseToken"/> (repeat matches).</summary>
    [JsonPropertyName("base_matches")]
    public IReadOnlyList<ZxcvbnMatch>? BaseMatches { get; init; }

    /// <summary>Number of times <see cref="BaseToken"/> repeats (repeat matches).</summary>
    [JsonPropertyName("repeat_count")]
    public int? RepeatCount { get; init; }

    /// <summary>Name of the recognized sequence, e.g., <c>lower</c> or <c>digits</c> (sequence matches).</summary>
    [JsonPropertyName("sequence_name")]
    public string? SequenceName { get; init; }

    /// <summary>Cardinality of the sequence space (sequence matches).</summary>
    [JsonPropertyName("sequence_space")]
    public int? SequenceSpace { get; init; }

    /// <summary>Whether the matched sequence ascends (sequence matches).</summary>
    [JsonPropertyName("ascending")]
    public bool? Ascending { get; init; }

    /// <summary>Name of the matched regular expression pattern (regex matches).</summary>
    [JsonPropertyName("regex_name")]
    public string? RegexName { get; init; }

    /// <summary>Captured regex match strings (regex matches).</summary>
    [JsonPropertyName("regex_match")]
    public IReadOnlyList<string>? RegexMatch { get; init; }

    /// <summary>Separator string inside a matched date (date matches).</summary>
    [JsonPropertyName("separator")]
    public string? Separator { get; init; }

    /// <summary>Matched year component (date matches).</summary>
    [JsonPropertyName("year")]
    public int? Year { get; init; }

    /// <summary>Matched month component (date matches).</summary>
    [JsonPropertyName("month")]
    public int? Month { get; init; }

    /// <summary>Matched day component (date matches).</summary>
    [JsonPropertyName("day")]
    public int? Day { get; init; }
}
