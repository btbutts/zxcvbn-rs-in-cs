using Xunit;

namespace ZxcvbnRs.Tests;

public sealed class ZxcvbnRsTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("123456")]
    [InlineData("qwertyuiop")]
    public void Common_or_keyboard_passwords_score_low_with_feedback(string password)
    {
        ZxcvbnResult result = ZxcvbnEstimator.Evaluate(password);

        Assert.InRange(result.Score, 0, 1);
        Assert.NotNull(result.Feedback);
        Assert.False(string.IsNullOrWhiteSpace(result.Feedback.Warning) && result.Feedback.Suggestions.Count == 0);
        Assert.NotEmpty(result.Sequence);
        Assert.False(string.IsNullOrWhiteSpace(result.CrackTimesDisplay.OfflineFastHashing1e10PerSecond));
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
    public void Empty_and_unicode_passwords_evaluate_cleanly()
    {
        ZxcvbnResult empty = ZxcvbnEstimator.Evaluate(ReadOnlySpan<char>.Empty);
        Assert.Equal(0, empty.Score);
        Assert.Equal(0UL, empty.Guesses);
        Assert.Equal(0.0, empty.GuessesLog10);
        Assert.Empty(empty.Sequence);

        ZxcvbnResult unicode = ZxcvbnEstimator.Evaluate("パスワード-正しい馬-電池-ホッチキス-🚀");
        Assert.InRange(unicode.Score, 0, 4);
        Assert.True(unicode.Guesses > 0);
    }

    [Fact]
    public void Oversized_password_is_rejected_without_crashing()
    {
        string oversized = new('a', ZxcvbnEstimator.MaxPasswordByteLength + 1);

        Assert.False(ZxcvbnEstimator.TryEvaluate(oversized, null, out ZxcvbnResult? ignored));
        Assert.Null(ignored);
        Assert.Throws<ArgumentOutOfRangeException>(() => ZxcvbnEstimator.Evaluate(oversized));
    }

    [Fact]
    public void Repeated_evaluations_do_not_leak_or_corrupt_memory()
    {
        for (int i = 0; i < 1000; i++)
        {
            ZxcvbnResult result = ZxcvbnEstimator.Evaluate($"passphrase-{i}-correct-horse-battery", ["passphrase", "test"]);
            Assert.InRange(result.Score, 3, 4);
        }
    }
}
