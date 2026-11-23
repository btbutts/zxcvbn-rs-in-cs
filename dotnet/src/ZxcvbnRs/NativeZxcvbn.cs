using System.Reflection;
using System.Runtime.InteropServices;

namespace ZxcvbnRs;

internal static partial class NativeZxcvbn
{
    private const string LibraryName = "zxcvbn_ffi";

    static NativeZxcvbn()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeZxcvbn).Assembly, ResolveLibrary);
    }

    [LibraryImport(LibraryName, EntryPoint = "zxcvbn_evaluate_json")]
    internal static partial nint EvaluateJson(
        ReadOnlySpan<byte> pwdPtr,
        nuint pwdLen,
        ReadOnlySpan<byte> inputsPtr,
        nuint inputsLen);

    [LibraryImport(LibraryName, EntryPoint = "zxcvbn_free_string")]
    internal static partial void FreeString(nint ptr);

    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return 0;
        }

        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out nint directHandle))
        {
            return directHandle;
        }

        string fileName = GetPlatformLibraryFileName();
        string baseDir = AppContext.BaseDirectory;

        // Also check direct file beside the executable/assembly.
        string adjacentPath = Path.Combine(baseDir, fileName);
        if (File.Exists(adjacentPath) && NativeLibrary.TryLoad(adjacentPath, out nint adjacentHandle))
        {
            return adjacentHandle;
        }

        foreach (string rid in CandidateRids())
        {
            string candidate = Path.Combine(baseDir, "runtimes", rid, "native", fileName);
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out nint ridHandle))
            {
                return ridHandle;
            }
        }

        return 0;
    }

    private static string GetPlatformLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "zxcvbn_ffi.dll";
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst())
        {
            return "libzxcvbn_ffi.dylib";
        }

        return "libzxcvbn_ffi.so";
    }

    private static IEnumerable<string> CandidateRids()
    {
        yield return RuntimeInformation.RuntimeIdentifier;

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };

        if (OperatingSystem.IsMacOS())
        {
            yield return $"osx-{arch}";
        }
        else if (OperatingSystem.IsWindows())
        {
            yield return $"win-{arch}";
        }
        else if (OperatingSystem.IsLinux())
        {
            if (IsMuslLinux())
            {
                yield return $"linux-musl-{arch}";
            }

            yield return $"linux-{arch}";
        }
    }

    private static bool IsMuslLinux() =>
        File.Exists("/lib/ld-musl-x86_64.so.1") ||
        File.Exists("/lib/ld-musl-aarch64.so.1") ||
        File.Exists("/etc/alpine-release");
}
