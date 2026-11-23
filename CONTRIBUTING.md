# Contributing & Build Environment Guide

Because `zxcvbn-rs.cs` packages a native Rust shared library (`cdylib` via C ABI) alongside a `.NET 10` managed wrapper across **8 OS/architecture RIDs** (`osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `win-x64`, `win-arm64`), full multi-platform packaging requires both the **.NET 10 SDK** and a **Rust cross-compilation toolchain**.

If you only want to run tests on your local machine architecture, you can build just the host target (`cargo xtask package-native --host`). If you want to produce the full multi-RID NuGet package from a single machine, set up the cross-compilation tools below.

---

## Build Environment Setup

### Core Requirements (All Platforms)

| Tool | Purpose |
| --- | --- |
| **.NET 10 SDK** | Compiles `ZxcvbnRs`, runs `[LibraryImport]` + `System.Text.Json` source generators, runs xUnit tests, and creates `.nupkg`. |
| **`rustup` (with `cargo` & `rustc`)** | Manages Rust toolchains and downloads cross-compilation standard libraries (`rust-std`) via `rustup target add`. *(Standalone package-manager Rust installs such as Homebrew's single-target `rust` formula do not include `rustup` target management.)* |
| **`zig`** | Provides bundled cross-platform C/C++ sysroots, `musl`/`glibc` headers, and LLVM `lld` linker used by `cargo-zigbuild`. |
| **`cargo-zigbuild`** | Cross-compiles `zxcvbn_ffi` to `linux-x64`, `linux-arm64`, `linux-musl-x64`, and `linux-musl-arm64` from macOS, Linux, or Windows. |
| **`cargo-xwin`** *(and/or `cargo-xtool`)* | Downloads and caches Windows MSVC CRT/SDK headers and links `win-x64` / `win-arm64` (`zxcvbn_ffi.dll`) headlessly from non-Windows hosts (`cargo xtask` invokes `cargo xwin build`). |

> **Important (`PATH` order):** Make sure `~/.cargo/bin` (from `rustup`) appears **before** any Homebrew or distro-packaged `cargo`/`rustc` binary in your `PATH` so `rustup` manages cross-targets properly. (`rust_crates/xtask` also automatically prioritizes `~/.cargo/bin` at runtime.)

---

### macOS (Apple Silicon / Intel)

Example setup using **Homebrew** and **`rustup`**:

```bash
# 1. Install .NET 10 SDK (or download the installer from Microsoft)
brew install --cask dotnet-sdk

# 2. Install Zig (for Linux glibc + musl cross-compilation)
brew install zig

# 3. Install rustup (includes cargo + rustc + multi-target support)
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
source "$HOME/.cargo/env"

# 4. Install Rust cross-compilation helpers
cargo install cargo-zigbuild
cargo install cargo-xwin
```

*(Note: If you also use Xcode Command Line Tools, install them once via `xcode-select --install` for native `osx-arm64` and `osx-x64` linking.)*

Verify your toolchain:

```bash
dotnet --version
zig version
rustup --version
cargo-zigbuild --version
cargo xwin --version
```

---

### Linux (Debian / Ubuntu / Fedora / Arch)

Example setup on **Ubuntu / Debian**:

```bash
# 1. Install .NET 10 SDK (via Microsoft package feed or dotnet-install.sh)
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0

# 2. Install build essentials and Zig
sudo apt-get update && sudo apt-get install -y build-essential curl pkg-config
# Install Zig (official tarball or snap, e.g. Zig >= 0.13):
sudo snap install zig --classic --beta

# 3. Install rustup (includes cargo + rustc + multi-target support)
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
source "$HOME/.cargo/env"

# 4. Install Rust cross-compilation helpers
cargo install cargo-zigbuild
cargo install cargo-xwin
```

*(Note: Cross-compiling macOS `osx-arm64` / `osx-x64` `.dylib` artifacts from Linux or Windows is not supported by default without an Apple SDK; use `--rids linux-x64,linux-arm64,linux-musl-x64,linux-musl-arm64,win-x64,win-arm64` on non-macOS hosts or build all 8 targets on macOS / CI matrix runners.)*

---

### Windows (PowerShell)

Example setup using **`winget`** in an elevated or developer PowerShell terminal:

```powershell
# 1. Install .NET 10 SDK
winget install Microsoft.DotNet.SDK.10

# 2. Install Zig
winget install zig.zig

# 3. Install rustup (includes cargo + rustc + MSVC/xwin support)
winget install Rustlang.Rustup
```

Restart your terminal so `PATH` picks up `%USERPROFILE%\.cargo\bin`, then install the Rust cross-compilation tools:

```powershell
cargo install cargo-zigbuild
cargo install cargo-xwin
```

*(For native `win-x64` / `win-arm64` MSVC builds without `xwin`, having the Visual Studio C++ Build Tools workload installed is also recommended.)*

---

## Building & Packaging

### 1. Build Native Rust Libraries (`cargo xtask package-native`)

From the repository root, run `cargo xtask package-native` to compile `zxcvbn_ffi` in `--release` mode, automatically add missing `rustup` targets, and copy the resulting shared libraries into `runtimes/<rid>/native/`:

```bash
# Full 8-RID cross-compilation build (macOS + Linux glibc/musl + Windows MSVC)
cargo xtask package-native
```

To build only the current host platform or a specific subset of RIDs:

```bash
# Current host machine only (no zig/xwin required)
cargo xtask package-native --host

# Specific RID(s)
cargo xtask package-native --rid linux-arm64
cargo xtask package-native --rids osx-arm64,linux-x64,linux-musl-x64
```

Valid RID values:
`osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `win-x64`, `win-arm64`.

### 2. Run Rust Unit Tests

```bash
cargo test
```

### 3. Run .NET Tests & Native AOT Checks

Ensure the native library for your host OS/architecture has been generated under `runtimes/<host-rid>/native/` first, then run:

```bash
dotnet test dotnet/ZxcvbnRs.sln
```

`ZxcvbnRs.csproj` enables `<IsAotCompatible>true</IsAotCompatible>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, verifying trim and Native AOT analyzer cleanliness on every build.

### 4. Pack the Multi-RID NuGet Package

```bash
dotnet pack dotnet/src/ZxcvbnRs/ZxcvbnRs.csproj -c Release -o artifacts/nuget
```

This creates `artifacts/nuget/zxcvbn-rs.cs.<version>.nupkg` bundling `lib/net10.0/ZxcvbnRs.dll` together with all packaged native binaries under `runtimes/<rid>/native/`.
