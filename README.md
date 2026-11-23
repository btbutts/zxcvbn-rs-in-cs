# zxcvbn-rs.cs (`zxcvbn-rs-in-cs`)

High-performance, Native AOT-compatible .NET 10 wrapper around the Rust [`zxcvbn`](https://github.com/shssoichiro/zxcvbn-rs) (`3.1.1`) password strength estimator via a minimal C ABI and source-generated `System.Text.Json`.

## Architecture

- **Input (Zero Extra Managed Password Strings):** Passwords are UTF-8 encoded into a stack-allocated buffer (`ReadOnlySpan<byte>`), evaluated natively in Rust, and immediately scrubbed in C# via `CryptographicOperations.ZeroMemory`.
- **Output (`serde` JSON + Source Generator):** Rust serializes `zxcvbn::Entropy` (including score, guesses, crack times in seconds and display form, feedback warning/suggestions + enum codes, and the matched pattern sequence) into a null-terminated `CString` wrapped in `std::panic::catch_unwind`. C# deserializes directly from the native UTF-8 pointer and frees it via paired `zxcvbn_free_string`.
- **Native AOT Ready:** Uses `[LibraryImport("zxcvbn_ffi")]` and `ZxcvbnJsonContext : JsonSerializerContext` (`<IsAotCompatible>true</IsAotCompatible>`).

## Supported NuGet RIDs

| NuGet RID | Rust Target Triple | Artifact |
| --- | --- | --- |
| `osx-arm64` | `aarch64-apple-darwin` | `runtimes/osx-arm64/native/libzxcvbn_ffi.dylib` |
| `osx-x64` | `x86_64-apple-darwin` | `runtimes/osx-x64/native/libzxcvbn_ffi.dylib` |
| `linux-x64` | `x86_64-unknown-linux-gnu` | `runtimes/linux-x64/native/libzxcvbn_ffi.so` |
| `linux-arm64` | `aarch64-unknown-linux-gnu` | `runtimes/linux-arm64/native/libzxcvbn_ffi.so` |
| `linux-musl-x64` | `x86_64-unknown-linux-musl` | `runtimes/linux-musl-x64/native/libzxcvbn_ffi.so` |
| `linux-musl-arm64` | `aarch64-unknown-linux-musl` | `runtimes/linux-musl-arm64/native/libzxcvbn_ffi.so` |
| `win-x64` | `x86_64-pc-windows-msvc` | `runtimes/win-x64/native/zxcvbn_ffi.dll` |
| `win-arm64` | `aarch64-pc-windows-msvc` | `runtimes/win-arm64/native/zxcvbn_ffi.dll` |

## Usage

```csharp
using ZxcvbnRs;

ZxcvbnResult result = ZxcvbnEstimator.Evaluate(
    "correct horse battery staple",
    userInputs: ["alice", "alice@example.com"]);

Console.WriteLine($"Score: {result.Score}/4 (offline fast: {result.CrackTimesDisplay.OfflineFastHashing1e10PerSecond})");
```

## Building from Source & Contributing

Multi-target native packaging relies on **.NET 10 SDK**, **`rustup`** (with `cargo`), **`zig`**, **`cargo-zigbuild`**, and **`cargo-xwin`**.

See **[CONTRIBUTING.md](./CONTRIBUTING.md)** for complete macOS, Linux, and Windows build environment setup instructions, `cargo xtask package-native` usage, test execution, and NuGet packaging commands.
