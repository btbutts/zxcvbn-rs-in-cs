using System.Text.Json.Serialization;

namespace ZxcvbnRs;

/// <summary>Estimated crack times in seconds under four standard attacker models.</summary>
public sealed record ZxcvbnCrackTimesSeconds
{
    /// <summary>Online attack rate-limited to 100 attempts per hour.</summary>
    [JsonPropertyName("online_throttling_100_per_hour")]
    public double OnlineThrottling100PerHour { get; init; }

    /// <summary>Unthrottled online attack at 10 attempts per second.</summary>
    [JsonPropertyName("online_no_throttling_10_per_second")]
    public double OnlineNoThrottling10PerSecond { get; init; }

    /// <summary>Offline attack against a slow KDF (bcrypt/scrypt/Argon2) at 10,000 guesses per second.</summary>
    [JsonPropertyName("offline_slow_hashing_1e4_per_second")]
    public double OfflineSlowHashing1e4PerSecond { get; init; }

    /// <summary>Offline attack against a fast hash at 10 billion guesses per second.</summary>
    [JsonPropertyName("offline_fast_hashing_1e10_per_second")]
    public double OfflineFastHashing1e10PerSecond { get; init; }
}

/// <summary>Human-readable crack time display strings under four standard attacker models.</summary>
public sealed record ZxcvbnCrackTimesDisplay
{
    /// <summary>Online attack rate-limited to 100 attempts per hour.</summary>
    [JsonPropertyName("online_throttling_100_per_hour")]
    public string OnlineThrottling100PerHour { get; init; } = string.Empty;

    /// <summary>Unthrottled online attack at 10 attempts per second.</summary>
    [JsonPropertyName("online_no_throttling_10_per_second")]
    public string OnlineNoThrottling10PerSecond { get; init; } = string.Empty;

    /// <summary>Offline attack against a slow KDF at 10,000 guesses per second.</summary>
    [JsonPropertyName("offline_slow_hashing_1e4_per_second")]
    public string OfflineSlowHashing1e4PerSecond { get; init; } = string.Empty;

    /// <summary>Offline attack against a fast hash at 10 billion guesses per second.</summary>
    [JsonPropertyName("offline_fast_hashing_1e10_per_second")]
    public string OfflineFastHashing1e10PerSecond { get; init; } = string.Empty;
}
