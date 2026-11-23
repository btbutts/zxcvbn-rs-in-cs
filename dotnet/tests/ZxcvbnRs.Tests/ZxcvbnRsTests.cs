using System.Diagnostics;
using Xunit;

namespace ZxcvbnRs.Tests;

public sealed class ZxcvbnRsTests
{
    [Fact]
    public void Golden_vector_abc123_produces_expected_score_and_dictionary_match()
    {
        ZxcvbnResult result = ZxcvbnEstimator.Evaluate("abc123");

        Assert.Equal(0, result.Score);
        Assert.Equal(14UL, result.Guesses);
        Assert.NotNull(result.Feedback);
        Assert.Equal("ThisIsATop100Password", result.Feedback.WarningCode);
        Assert.Equal("This is a top-100 common password.", result.Feedback.Warning);
        Assert.Single(result.Sequence);
        Assert.Equal("dictionary", result.Sequence[0].Pattern);
        Assert.Equal("abc123", result.Sequence[0].MatchedWord);
        Assert.Equal("Passwords", result.Sequence[0].DictionaryName);
        Assert.Equal(13UL, result.Sequence[0].Rank);
    }

    [Fact]
    public void Golden_vector_wombat_attack_monkey_produces_score_4_and_l33t_sequence()
    {
        ZxcvbnResult result = ZxcvbnEstimator.Evaluate("womB@t-attAcK monkey");

        Assert.Equal(4, result.Score);
        Assert.Equal(208_200_100_000_000UL, result.Guesses);
        Assert.Null(result.Feedback);
        Assert.Equal(3, result.Sequence.Count);

        ZxcvbnMatch first = result.Sequence[0];
        Assert.Equal("dictionary", first.Pattern);
        Assert.Equal("womB@t", first.Token);
        Assert.Equal("wombat", first.MatchedWord);
        Assert.True(first.L33t);
        Assert.NotNull(first.Sub);
        Assert.Equal("a", first.Sub["@"]);

        Assert.Equal("bruteforce", result.Sequence[1].Pattern);
        Assert.Equal("dictionary", result.Sequence[2].Pattern);
        Assert.Equal("monkey", result.Sequence[2].MatchedWord);
    }

    [Theory]
    [InlineData("password", 0, "dictionary")]
    [InlineData("123456", 0, "dictionary")]
    [InlineData("tgbnhyujm", 2, "spatial")]
    [InlineData("aaaaaaa", 0, "repeat")]
    [InlineData("abcdefgh", 0, "sequence")]
    [InlineData("1991-11-23", 1, "date")]
    public void Pattern_specific_golden_vectors_match_expected_scores_and_patterns(
        string password,
        int maxExpectedScore,
        string expectedPattern)
    {
        ZxcvbnResult result = ZxcvbnEstimator.Evaluate(password);

        Assert.InRange(result.Score, 0, maxExpectedScore);
        Assert.Contains(result.Sequence, m => string.Equals(m.Pattern, expectedPattern, StringComparison.Ordinal));
        Assert.NotNull(result.Feedback);
        Assert.False(string.IsNullOrWhiteSpace(result.CrackTimesDisplay.OfflineFastHashing1e10PerSecond));
    }

    [Fact]
    public void Date_and_repeat_match_fields_deserialize_accurately()
    {
        ZxcvbnResult dateResult = ZxcvbnEstimator.Evaluate("1991-11-23");
        ZxcvbnMatch dateMatch = Assert.Single(dateResult.Sequence);
        Assert.Equal("date", dateMatch.Pattern);
        Assert.Equal(1991, dateMatch.Year);
        Assert.Equal(11, dateMatch.Month);
        Assert.Equal(23, dateMatch.Day);
        Assert.Equal("-", dateMatch.Separator);

        ZxcvbnResult repeatResult = ZxcvbnEstimator.Evaluate("aaaaaaa");
        ZxcvbnMatch repeatMatch = Assert.Single(repeatResult.Sequence);
        Assert.Equal("repeat", repeatMatch.Pattern);
        Assert.Equal("a", repeatMatch.BaseToken);
        Assert.Equal(7, repeatMatch.RepeatCount);
    }

    [Theory]
    [InlineData("correct horse battery staple")]
    [InlineData("Tr0ub4dor&3-and-more-entropy!")]
    public void Strong_passphrases_score_at_least_three(string password)
    {
        ZxcvbnResult result = ZxcvbnEstimator.Evaluate(password);

        Assert.InRange(result.Score, 3, 4);
        Assert.True(result.Guesses >= 100_000_000UL);
        Assert.True(result.GuessesLog10 >= 8.0);
        Assert.NotEmpty(result.Sequence);
    }

    [Fact]
    public void User_inputs_context_penalizes_matching_tokens()
    {
        const string password = "custombrand-north-2026";

        ZxcvbnResult withoutContext = ZxcvbnEstimator.Evaluate(password);
        ZxcvbnResult withContext = ZxcvbnEstimator.Evaluate(password, ["custombrand", "north", "2026"]);

        Assert.True(
            withContext.Guesses < withoutContext.Guesses,
            $"Expected guesses with context ({withContext.Guesses}) to be lower than without ({withoutContext.Guesses}).");
        Assert.Contains(
            withContext.Sequence,
            static m => string.Equals(m.DictionaryName, "UserInputs", StringComparison.Ordinal));
    }

    [Fact]
    public void Hazardous_characters_nul_controls_json_injection_and_unpaired_surrogates_never_crash()
    {
        // Embedded NUL bytes in password and userInputs round-trip cleanly via JSON escaping
        ZxcvbnResult nulResult = ZxcvbnEstimator.Evaluate("abc\0123", ["user\0input"]);
        Assert.InRange(nulResult.Score, 0, 4);
        Assert.Equal("abc\0123", string.Concat(nulResult.Sequence.Select(static s => s.Token)));

        // Every ASCII control byte 0x00..=0x1F plus DEL 0x7F
        char[] controlChars = [.. Enumerable.Range(0, 32).Select(i => (char)i), '\x7F'];
        ZxcvbnResult controlResult = ZxcvbnEstimator.Evaluate(new string(controlChars));
        Assert.InRange(controlResult.Score, 0, 4);
        Assert.NotEmpty(controlResult.Sequence);

        // JSON injection payload in both password and userInputs
        const string jsonPayload = """{"score":4,"guesses":999999999}\""";
        ZxcvbnResult injectionResult = ZxcvbnEstimator.Evaluate(jsonPayload, [jsonPayload, "\n\rline2"]);
        Assert.Equal(jsonPayload, string.Concat(injectionResult.Sequence.Select(static s => s.Token)));

        // Unpaired UTF-16 surrogates in C# (Encoding.UTF8 converts to U+FFFD cleanly)
        ZxcvbnResult unpairedHigh = ZxcvbnEstimator.Evaluate("abc\uD800123");
        ZxcvbnResult unpairedLow = ZxcvbnEstimator.Evaluate("\uDC00\uD800xyz");
        Assert.InRange(unpairedHigh.Score, 0, 4);
        Assert.InRange(unpairedLow.Score, 0, 4);

        // Dense l33t characters + complex multi-byte Unicode (ZWJ emojis, RTL, combining marks, CJK)
        ZxcvbnResult heavyL33t = ZxcvbnEstimator.Evaluate("@48(3691!|0$5+7%2\n\r\np@ssw0rd");
        ZxcvbnResult zwjAndScripts = ZxcvbnEstimator.Evaluate("👨‍👩‍👧‍👦🏳️‍🌈-e\u0301-كلمة-正しい馬電池");
        Assert.InRange(heavyL33t.Score, 0, 4);
        Assert.InRange(zwjAndScripts.Score, 0, 4);
    }

    [Fact]
    public void Exact_utf8_byte_boundaries_for_password_and_user_inputs_are_enforced()
    {
        string empty = string.Empty;
        ZxcvbnResult emptyResult = ZxcvbnEstimator.Evaluate(empty);
        Assert.Equal(0, emptyResult.Score);
        Assert.Equal(0UL, emptyResult.Guesses);
        Assert.Equal(0.0, emptyResult.GuessesLog10);
        Assert.Empty(emptyResult.Sequence);

        // Exact 512-byte ASCII password passes
        string ascii512 = new('a', ZxcvbnEstimator.MaxPasswordByteLength);
        Assert.True(ZxcvbnEstimator.TryEvaluate(ascii512, null, out ZxcvbnResult? asciiOk));
        Assert.NotNull(asciiOk);

        // 513-byte ASCII password fails cleanly
        string ascii513 = new('a', ZxcvbnEstimator.MaxPasswordByteLength + 1);
        Assert.False(ZxcvbnEstimator.TryEvaluate(ascii513, null, out ZxcvbnResult? asciiTooLong));
        Assert.Null(asciiTooLong);
        Assert.Throws<ArgumentOutOfRangeException>(() => ZxcvbnEstimator.Evaluate(ascii513));

        // Multi-byte UTF-8 boundary: 128 x 4-byte emoji (128 * 2 UTF-16 chars = 512 UTF-8 bytes)
        string emoji512Bytes = string.Concat(Enumerable.Repeat("🚀", 128));
        Assert.Equal(256, emoji512Bytes.Length);
        Assert.True(ZxcvbnEstimator.TryEvaluate(emoji512Bytes, null, out ZxcvbnResult? emojiOk));
        Assert.NotNull(emojiOk);

        // One extra byte (513 UTF-8 bytes) is rejected
        string emoji513Bytes = emoji512Bytes + "a";
        Assert.False(ZxcvbnEstimator.TryEvaluate(emoji513Bytes, null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZxcvbnEstimator.Evaluate(emoji513Bytes));

        // Exact userInputs UTF-8 boundary: 8192 passes, 8193 fails
        string inputs8192 = new('u', ZxcvbnEstimator.MaxUserInputsByteLength);
        Assert.True(ZxcvbnEstimator.TryEvaluate("valid-password-phrase", [inputs8192], out ZxcvbnResult? inputsOk));
        Assert.NotNull(inputsOk);

        string inputs8193 = new('u', ZxcvbnEstimator.MaxUserInputsByteLength + 1);
        Assert.False(ZxcvbnEstimator.TryEvaluate("valid-password-phrase", [inputs8193], out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZxcvbnEstimator.Evaluate("valid-password-phrase", [inputs8193]));
    }

    [Fact]
    public void Concurrent_multi_threaded_ffi_calls_are_thread_safe()
    {
        string[] corpus =
        [
            "abc123",
            "womB@t-attAcK monkey",
            "correct horse battery staple",
            "Tr0ub4dor&3",
            "qwertyuiop12345",
            "パスワード-🚀-安全",
            "custom\0null-byte",
            "@48(3691!|0$5+7%2",
        ];

        Parallel.For(
            0,
            2000,
            new ParallelOptions { MaxDegreeOfParallelism = 16 },
            i =>
            {
                string pwd = corpus[i % corpus.Length];
                ZxcvbnResult res = ZxcvbnEstimator.Evaluate(pwd, ["tenant", $"user{i % 10}", "2026"]);
                Assert.InRange(res.Score, 0, 4);
                Assert.NotEmpty(res.Sequence);
            });
    }

    [Fact]
    public void Repeated_evaluations_do_not_leak_native_or_managed_memory()
    {
        // Warm up caches and lazy_static dictionaries on the Rust side first
        for (int i = 0; i < 100; i++)
        {
            _ = ZxcvbnEstimator.Evaluate($"warmup-{i}-correct-horse", ["warmup"]);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using Process proc = Process.GetCurrentProcess();
        long managedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long workingSetBefore = proc.WorkingSet64;

        for (int i = 0; i < 5000; i++)
        {
            ZxcvbnResult result = ZxcvbnEstimator.Evaluate(
                $"passphrase-{i}-correct-horse-battery",
                ["passphrase", "test"]);
            Assert.InRange(result.Score, 3, 4);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        proc.Refresh();

        long managedAfter = GC.GetTotalMemory(forceFullCollection: true);
        long workingSetAfter = proc.WorkingSet64;

        // 5,000 JSON payloads would allocate megabytes if leaked; bounded delta confirms cleanup.
        Assert.True(
            managedAfter - managedBefore < 4L * 1024 * 1024,
            $"Managed heap grew unexpectedly: before={managedBefore}, after={managedAfter}");
        Assert.True(
            workingSetAfter - workingSetBefore < 32L * 1024 * 1024,
            $"Process working set grew unexpectedly: before={workingSetBefore}, after={workingSetAfter}");
    }

    [Theory]
    [InlineData("osx-arm64", "libzxcvbn_ffi.dylib", new byte[] { 0xCF, 0xFA, 0xED, 0xFE })]
    [InlineData("osx-x64", "libzxcvbn_ffi.dylib", new byte[] { 0xCF, 0xFA, 0xED, 0xFE })]
    [InlineData("linux-x64", "libzxcvbn_ffi.so", new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' })]
    [InlineData("linux-arm64", "libzxcvbn_ffi.so", new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' })]
    [InlineData("linux-musl-x64", "libzxcvbn_ffi.so", new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' })]
    [InlineData("linux-musl-arm64", "libzxcvbn_ffi.so", new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F' })]
    [InlineData("win-x64", "zxcvbn_ffi.dll", new byte[] { (byte)'M', (byte)'Z' })]
    [InlineData("win-arm64", "zxcvbn_ffi.dll", new byte[] { (byte)'M', (byte)'Z' })]
    public void Compiled_native_binaries_exist_in_output_with_valid_headers(
        string rid,
        string fileName,
        byte[] expectedMagic)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);
        Assert.True(File.Exists(path), $"Expected compiled native library at {path}");

        byte[] header = new byte[expectedMagic.Length];
        using FileStream stream = File.OpenRead(path);
        Assert.True(stream.Length > 100_000, $"Native library {path} appears unexpectedly small ({stream.Length} bytes).");
        int read = stream.Read(header, 0, header.Length);
        Assert.Equal(expectedMagic.Length, read);
        Assert.Equal(expectedMagic, header);
    }
}
