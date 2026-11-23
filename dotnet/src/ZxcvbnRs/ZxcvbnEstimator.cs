using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ZxcvbnRs;

/// <summary>Evaluates password strength using the native Rust <c>zxcvbn</c> (3.1.1) library.</summary>
public static class ZxcvbnEstimator
{
    /// <summary>Maximum UTF-8 encoded password byte length accepted by the native evaluator.</summary>
    public const int MaxPasswordByteLength = 512;

    /// <summary>Maximum UTF-8 encoded newline-joined user-inputs byte length accepted by the native evaluator.</summary>
    public const int MaxUserInputsByteLength = 8192;

    /// <summary>
    /// Evaluates <paramref name="password"/> with optional domain/user context strings (<paramref name="userInputs"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the UTF-8 encoding of <paramref name="password"/> exceeds <see cref="MaxPasswordByteLength"/>
    /// or <paramref name="userInputs"/> exceeds <see cref="MaxUserInputsByteLength"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when native evaluation fails or returns malformed JSON.</exception>
    public static ZxcvbnResult Evaluate(ReadOnlySpan<char> password, IEnumerable<string>? userInputs = null)
    {
        if (!TryEvaluateCore(password, userInputs, out ZxcvbnResult? result, out bool oversized, out string? error))
        {
            if (oversized)
            {
                throw new ArgumentOutOfRangeException(nameof(password), error);
            }

            throw new InvalidOperationException(error);
        }

        return result;
    }

    /// <summary>
    /// Attempts to evaluate <paramref name="password"/> without throwing on oversized input.
    /// </summary>
    public static bool TryEvaluate(
        ReadOnlySpan<char> password,
        IEnumerable<string>? userInputs,
        [NotNullWhen(true)] out ZxcvbnResult? result) =>
        TryEvaluateCore(password, userInputs, out result, out _, out _);

    private static unsafe bool TryEvaluateCore(
        ReadOnlySpan<char> password,
        IEnumerable<string>? userInputs,
        [NotNullWhen(true)] out ZxcvbnResult? result,
        out bool oversized,
        out string? error)
    {
        result = null;
        oversized = false;
        error = null;

        int maxPwdBytes = Encoding.UTF8.GetMaxByteCount(password.Length);
        if (maxPwdBytes > MaxPasswordByteLength)
        {
            // Check exact UTF-8 byte count before rejecting passwords near the threshold.
            int exactPwdBytes = Encoding.UTF8.GetByteCount(password);
            if (exactPwdBytes > MaxPasswordByteLength)
            {
                oversized = true;
                error = $"Password UTF-8 length ({exactPwdBytes} bytes) exceeds the {MaxPasswordByteLength}-byte limit.";
                return false;
            }

            maxPwdBytes = exactPwdBytes;
        }

        Span<byte> pwdBuffer = maxPwdBytes == 0
            ? Span<byte>.Empty
            : stackalloc byte[maxPwdBytes];

        byte[]? inputBytes = null;
        if (userInputs is not null)
        {
            List<string> cleaned = [];
            foreach (string input in userInputs)
            {
                if (!string.IsNullOrWhiteSpace(input))
                {
                    cleaned.Add(input.Replace('\n', ' ').Replace('\r', ' '));
                }
            }

            if (cleaned.Count > 0)
            {
                inputBytes = Encoding.UTF8.GetBytes(string.Join('\n', cleaned));
                if (inputBytes.Length > MaxUserInputsByteLength)
                {
                    oversized = true;
                    error = $"User inputs UTF-8 length ({inputBytes.Length} bytes) exceeds the {MaxUserInputsByteLength}-byte limit.";
                    return false;
                }
            }
        }

        try
        {
            int pwdWritten = Encoding.UTF8.GetBytes(password, pwdBuffer);
            ReadOnlySpan<byte> pwdSpan = pwdBuffer[..pwdWritten];
            ReadOnlySpan<byte> inputsSpan = inputBytes is null ? ReadOnlySpan<byte>.Empty : inputBytes;

            nint jsonPtr = NativeZxcvbn.EvaluateJson(
                pwdSpan,
                (nuint)pwdSpan.Length,
                inputsSpan,
                (nuint)inputsSpan.Length);

            if (jsonPtr == 0)
            {
                error = "Native zxcvbn evaluation failed.";
                return false;
            }

            try
            {
                ReadOnlySpan<byte> utf8Json = MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)jsonPtr);
                result = JsonSerializer.Deserialize(utf8Json, ZxcvbnJsonContext.Default.ZxcvbnResult);
                if (result is null)
                {
                    error = "Failed to deserialize zxcvbn JSON payload.";
                    return false;
                }

                return true;
            }
            finally
            {
                NativeZxcvbn.FreeString(jsonPtr);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pwdBuffer);
        }
    }
}
