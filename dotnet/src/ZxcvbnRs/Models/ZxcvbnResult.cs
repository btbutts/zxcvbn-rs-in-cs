using System.Text.Json.Serialization;

namespace ZxcvbnRs;

/// <summary>Result of evaluating a password with the Rust <c>zxcvbn</c> estimator.</summary>
public sealed record ZxcvbnResult
{
    /// <summary>Overall strength score in the range <c>0</c> (weakest) to <c>4</c> (strongest).</summary>
    [JsonPropertyName("score")]
    public int Score { get; init; }

    /// <summary>Estimated number of guesses needed to crack the password.</summary>
    [JsonPropertyName("guesses")]
    public ulong Guesses { get; init; }

    /// <summary>Base-10 logarithm of <see cref="Guesses"/> (normalized to <c>0</c> for empty inputs).</summary>
    [JsonPropertyName("guesses_log10")]
    public double GuessesLog10 { get; init; }

    /// <summary>Back-of-the-envelope crack time estimates in seconds across four attack scenarios.</summary>
    [JsonPropertyName("crack_times_seconds")]
    public ZxcvbnCrackTimesSeconds CrackTimesSeconds { get; init; } = new();

    /// <summary>Human-readable display strings for the four attack crack time estimates.</summary>
    [JsonPropertyName("crack_times_display")]
    public ZxcvbnCrackTimesDisplay CrackTimesDisplay { get; init; } = new();

    /// <summary>Elapsed calculation time in milliseconds reported by <c>zxcvbn</c>.</summary>
    [JsonPropertyName("calc_time_ms")]
    public ulong CalcTimeMs { get; init; }

    /// <summary>Optional actionable feedback generated when the password score is low (<c>&lt;= 2</c>).</summary>
    [JsonPropertyName("feedback")]
    public ZxcvbnFeedback? Feedback { get; init; }

    /// <summary>Ordered sequence of matched patterns that produced the optimal guess calculation.</summary>
    [JsonPropertyName("sequence")]
    public IReadOnlyList<ZxcvbnMatch> Sequence { get; init; } = [];
}
