use std::env;
use std::ffi::OsString;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::{Command, ExitCode};

#[derive(Clone, Copy)]
enum Builder {
    Cargo,
    ZigBuild,
    Xwin,
}

#[derive(Clone, Copy)]
struct TargetSpec {
    triple: &'static str,
    rid: &'static str,
    lib_name: &'static str,
    builder: Builder,
}

const TARGETS: &[TargetSpec] = &[
    TargetSpec {
        triple: "aarch64-apple-darwin",
        rid: "osx-arm64",
        lib_name: "libzxcvbn_ffi.dylib",
        builder: Builder::Cargo,
    },
    TargetSpec {
        triple: "x86_64-apple-darwin",
        rid: "osx-x64",
        lib_name: "libzxcvbn_ffi.dylib",
        builder: Builder::Cargo,
    },
    TargetSpec {
        triple: "x86_64-unknown-linux-gnu",
        rid: "linux-x64",
        lib_name: "libzxcvbn_ffi.so",
        builder: Builder::ZigBuild,
    },
    TargetSpec {
        triple: "aarch64-unknown-linux-gnu",
        rid: "linux-arm64",
        lib_name: "libzxcvbn_ffi.so",
        builder: Builder::ZigBuild,
    },
    TargetSpec {
        triple: "x86_64-unknown-linux-musl",
        rid: "linux-musl-x64",
        lib_name: "libzxcvbn_ffi.so",
        builder: Builder::ZigBuild,
    },
    TargetSpec {
        triple: "aarch64-unknown-linux-musl",
        rid: "linux-musl-arm64",
        lib_name: "libzxcvbn_ffi.so",
        builder: Builder::ZigBuild,
    },
    TargetSpec {
        triple: "x86_64-pc-windows-msvc",
        rid: "win-x64",
        lib_name: "zxcvbn_ffi.dll",
        builder: Builder::Xwin,
    },
    TargetSpec {
        triple: "aarch64-pc-windows-msvc",
        rid: "win-arm64",
        lib_name: "zxcvbn_ffi.dll",
        builder: Builder::Xwin,
    },
];

fn workspace_root() -> PathBuf {
    // rust_crates/xtask/Cargo.toml -> two ancestors up is repository root
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .ancestors()
        .nth(2)
        .expect("Failed to locate workspace root from CARGO_MANIFEST_DIR")
        .to_path_buf()
}

fn rustup_bin_dir() -> Option<PathBuf> {
    let home = env::var_os("CARGO_HOME")
        .map(PathBuf::from)
        .map(|p| p.join("bin"))
        .or_else(|| env::var_os("HOME").map(|h| PathBuf::from(h).join(".cargo/bin")));
    home.filter(|p| p.is_dir())
}

fn tool_path(name: &str) -> PathBuf {
    if let Some(bin_dir) = rustup_bin_dir() {
        let candidate = bin_dir.join(name);
        if candidate.exists() {
            return candidate;
        }
    }
    PathBuf::from(name)
}

fn env_path_with_rustup_first() -> OsString {
    let current = env::var_os("PATH").unwrap_or_default();
    if let Some(bin_dir) = rustup_bin_dir() {
        let mut paths = vec![bin_dir];
        paths.extend(env::split_paths(&current));
        return env::join_paths(paths).unwrap_or(current);
    }
    current
}

fn host_rid() -> &'static str {
    match (env::consts::OS, env::consts::ARCH) {
        ("macos", "aarch64") => "osx-arm64",
        ("macos", "x86_64") => "osx-x64",
        ("linux", "x86_64") => "linux-x64",
        ("linux", "aarch64") => "linux-arm64",
        ("windows", "x86_64") => "win-x64",
        ("windows", "aarch64") => "win-arm64",
        (os, arch) => panic!("Unsupported host OS/arch combination: {os}/{arch}"),
    }
}

fn installed_targets(path_env: &OsString) -> Vec<String> {
    let Ok(output) = Command::new(tool_path("rustup"))
        .env("PATH", path_env)
        .args(["target", "list", "--installed"])
        .output()
    else {
        return Vec::new();
    };
    if !output.status.success() {
        return Vec::new();
    }
    String::from_utf8_lossy(&output.stdout)
        .lines()
        .map(str::trim)
        .filter(|s| !s.is_empty())
        .map(ToOwned::to_owned)
        .collect()
}

fn ensure_rustup_target(triple: &str, path_env: &OsString) -> Result<(), String> {
    if installed_targets(path_env).iter().any(|t| t == triple) {
        return Ok(());
    }
    println!("==> Installing rustup target {triple}...");
    let status = Command::new(tool_path("rustup"))
        .env("PATH", path_env)
        .args(["target", "add", triple])
        .status()
        .map_err(|e| format!("Failed to invoke rustup: {e}"))?;
    if !status.success() {
        return Err(format!("`rustup target add {triple}` failed"));
    }
    Ok(())
}

fn build_target(root: &Path, spec: TargetSpec, path_env: &OsString) -> Result<(), String> {
    ensure_rustup_target(spec.triple, path_env)?;

    let mut cmd = Command::new(tool_path("cargo"));
    cmd.current_dir(root);
    cmd.env("PATH", path_env);

    match spec.builder {
        Builder::Cargo => {
            cmd.arg("build");
        }
        Builder::ZigBuild => {
            cmd.arg("zigbuild");
            if spec.triple.contains("musl") {
                let mut rustflags = env::var("RUSTFLAGS").unwrap_or_default();
                if !rustflags.is_empty() {
                    rustflags.push(' ');
                }
                rustflags.push_str("-C target-feature=-crt-static");
                cmd.env("RUSTFLAGS", rustflags);
            }
        }
        Builder::Xwin => {
            cmd.args(["xwin", "build"]);
        }
    }

    cmd.args([
        "--release",
        "--package",
        "zxcvbn_ffi",
        "--target",
        spec.triple,
    ]);

    println!("==> Building {} ({})...", spec.rid, spec.triple);
    let status = cmd
        .status()
        .map_err(|e| format!("Failed to invoke cargo for {}: {e}", spec.rid))?;

    if !status.success() {
        return Err(format!("Build failed for target {} ({})", spec.rid, spec.triple));
    }

    let built_path = root
        .join("target")
        .join(spec.triple)
        .join("release")
        .join(spec.lib_name);

    if !built_path.exists() {
        return Err(format!(
            "Expected built artifact at {}, but it does not exist",
            built_path.display()
        ));
    }

    let dest_dir = root.join("runtimes").join(spec.rid).join("native");
    fs::create_dir_all(&dest_dir)
        .map_err(|e| format!("Failed to create {}: {e}", dest_dir.display()))?;
    let dest_path = dest_dir.join(spec.lib_name);
    fs::copy(&built_path, &dest_path).map_err(|e| {
        format!(
            "Failed to copy {} to {}: {e}",
            built_path.display(),
            dest_path.display()
        )
    })?;

    println!("    Packaged -> {}", dest_path.display());
    Ok(())
}

fn parse_selected_rids(args: &[String]) -> Result<Vec<&'static str>, String> {
    let mut rids = Vec::new();
    let mut i = 0;
    while i < args.len() {
        match args[i].as_str() {
            "--host" => {
                rids.push(host_rid());
                i += 1;
            }
            "--rid" => {
                i += 1;
                let val = args
                    .get(i)
                    .ok_or_else(|| "--rid requires a value".to_string())?;
                rids.push(find_rid(val)?);
                i += 1;
            }
            "--rids" => {
                i += 1;
                let val = args
                    .get(i)
                    .ok_or_else(|| "--rids requires a comma-separated list".to_string())?;
                for item in val.split(',').map(str::trim).filter(|s| !s.is_empty()) {
                    rids.push(find_rid(item)?);
                }
                i += 1;
            }
            other => return Err(format!("Unknown flag: {other}")),
        }
    }

    if rids.is_empty() {
        Ok(TARGETS.iter().map(|t| t.rid).collect())
    } else {
        Ok(rids)
    }
}

fn find_rid(value: &str) -> Result<&'static str, String> {
    TARGETS
        .iter()
        .find(|t| t.rid == value)
        .map(|t| t.rid)
        .ok_or_else(|| {
            let valid: Vec<_> = TARGETS.iter().map(|t| t.rid).collect();
            format!("Unknown RID '{value}'. Valid RIDs: {}", valid.join(", "))
        })
}

fn main() -> ExitCode {
    let args: Vec<String> = env::args().skip(1).collect();
    if args.is_empty() || args[0] != "package-native" {
        eprintln!("Usage: cargo xtask package-native [--host | --rid <rid> | --rids <rid1,rid2,...>]");
        return ExitCode::from(2);
    }

    let selected_rids = match parse_selected_rids(&args[1..]) {
        Ok(rids) => rids,
        Err(msg) => {
            eprintln!("Error: {msg}");
            return ExitCode::from(2);
        }
    };

    let root = workspace_root();
    let path_env = env_path_with_rustup_first();
    for rid in selected_rids {
        let spec = *TARGETS
            .iter()
            .find(|t| t.rid == rid)
            .expect("RID validated above");
        if let Err(err) = build_target(&root, spec, &path_env) {
            eprintln!("Error: {err}");
            return ExitCode::FAILURE;
        }
    }

    println!(
        "==> Done packaging native libraries under {}",
        root.join("runtimes").display()
    );
    ExitCode::SUCCESS
}
