use serde::Serialize;
use std::ffi::{CString, c_char};
use std::panic::{self, AssertUnwindSafe};
use std::ptr;
use std::slice;
use std::str;
use zxcvbn::feedback::{Suggestion, Warning};
use zxcvbn::time_estimates::CrackTimeSeconds;
use zxcvbn::{Entropy, Match, zxcvbn};

const MAX_PASSWORD_BYTES: usize = 512;
const MAX_INPUTS_BYTES: usize = 8192;

#[derive(Serialize)]
struct CrackTimesSecondsDto {
    online_throttling_100_per_hour: f64,
    online_no_throttling_10_per_second: f64,
    offline_slow_hashing_1e4_per_second: f64,
    offline_fast_hashing_1e10_per_second: f64,
}

#[derive(Serialize)]
struct CrackTimesDisplayDto {
    online_throttling_100_per_hour: String,
    online_no_throttling_10_per_second: String,
    offline_slow_hashing_1e4_per_second: String,
    offline_fast_hashing_1e10_per_second: String,
}

#[derive(Serialize)]
struct FeedbackDto {
    warning: Option<String>,
    warning_code: Option<Warning>,
    suggestions: Vec<String>,
    suggestion_codes: Vec<Suggestion>,
}

#[derive(Serialize)]
struct EvaluationResultDto<'a> {
    score: u8,
    guesses: u64,
    guesses_log10: f64,
    crack_times_seconds: CrackTimesSecondsDto,
    crack_times_display: CrackTimesDisplayDto,
    calc_time_ms: u64,
    feedback: Option<FeedbackDto>,
    sequence: &'a [Match],
}

fn crack_time_to_f64(value: CrackTimeSeconds) -> f64 {
    let raw = match value {
        CrackTimeSeconds::Integer(i) => i as f64,
        CrackTimeSeconds::Float(f) => f,
    };
    if raw.is_finite() { raw } else { f64::MAX }
}

fn to_dto<'a>(entropy: &'a Entropy) -> EvaluationResultDto<'a> {
    let ct = entropy.crack_times();
    let online_throttled = ct.online_throttling_100_per_hour();
    let online_unthrottled = ct.online_no_throttling_10_per_second();
    let offline_slow = ct.offline_slow_hashing_1e4_per_second();
    let offline_fast = ct.offline_fast_hashing_1e10_per_second();

    let guesses_log10 = entropy.guesses_log10();
    let normalized_log10 = if guesses_log10.is_finite() {
        guesses_log10
    } else {
        0.0
    };

    let calc_time_ms = u64::try_from(entropy.calculation_time().as_millis()).unwrap_or(u64::MAX);

    let feedback = entropy.feedback().map(|fb| FeedbackDto {
        warning: fb.warning().map(|w| w.to_string()),
        warning_code: fb.warning(),
        suggestions: fb.suggestions().iter().map(|s| s.to_string()).collect(),
        suggestion_codes: fb.suggestions().to_vec(),
    });

    EvaluationResultDto {
        score: u8::from(entropy.score()),
        guesses: entropy.guesses(),
        guesses_log10: normalized_log10,
        crack_times_seconds: CrackTimesSecondsDto {
            online_throttling_100_per_hour: crack_time_to_f64(online_throttled),
            online_no_throttling_10_per_second: crack_time_to_f64(online_unthrottled),
            offline_slow_hashing_1e4_per_second: crack_time_to_f64(offline_slow),
            offline_fast_hashing_1e10_per_second: crack_time_to_f64(offline_fast),
        },
        crack_times_display: CrackTimesDisplayDto {
            online_throttling_100_per_hour: online_throttled.to_string(),
            online_no_throttling_10_per_second: online_unthrottled.to_string(),
            offline_slow_hashing_1e4_per_second: offline_slow.to_string(),
            offline_fast_hashing_1e10_per_second: offline_fast.to_string(),
        },
        calc_time_ms,
        feedback,
        sequence: entropy.sequence(),
    }
}

/// Evaluates a password (passed as raw UTF-8 bytes) and optional newline-delimited `user_inputs`
/// (passed as raw UTF-8 bytes), returning a newly allocated null-terminated JSON string (`*mut c_char`),
/// or `null` on invalid input / caught panic.
///
/// # Safety
/// - When `pwd_len > 0`, `pwd_ptr` must be a valid pointer to at least `pwd_len` readable bytes.
/// - When `inputs_len > 0`, `inputs_ptr` must be a valid pointer to at least `inputs_len` readable bytes.
/// - The caller MUST free any non-null returned pointer by calling `zxcvbn_free_string`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn zxcvbn_evaluate_json(
    pwd_ptr: *const u8,
    pwd_len: usize,
    inputs_ptr: *const u8,
    inputs_len: usize,
) -> *mut c_char {
    let outcome = panic::catch_unwind(AssertUnwindSafe(|| -> Option<*mut c_char> {
        if pwd_len > MAX_PASSWORD_BYTES || inputs_len > MAX_INPUTS_BYTES {
            return None;
        }
        if (pwd_len > 0 && pwd_ptr.is_null()) || (inputs_len > 0 && inputs_ptr.is_null()) {
            return None;
        }

        let password = if pwd_len == 0 {
            ""
        } else {
            let pwd_bytes = unsafe { slice::from_raw_parts(pwd_ptr, pwd_len) };
            str::from_utf8(pwd_bytes).ok()?
        };

        let user_inputs: Vec<&str> = if inputs_len == 0 {
            Vec::new()
        } else {
            let input_bytes = unsafe { slice::from_raw_parts(inputs_ptr, inputs_len) };
            let inputs_str = str::from_utf8(input_bytes).ok()?;
            inputs_str.split('\n').filter(|s| !s.is_empty()).collect()
        };

        let entropy = zxcvbn(password, &user_inputs);
        let dto = to_dto(&entropy);
        let json = serde_json::to_string(&dto).ok()?;
        let c_string = CString::new(json).ok()?;
        Some(c_string.into_raw())
    }));

    match outcome {
        Ok(Some(ptr)) => ptr,
        _ => ptr::null_mut(),
    }
}

/// Frees a `*mut c_char` previously allocated by `zxcvbn_evaluate_json`.
///
/// # Safety
/// - `ptr` must be either null or a pointer returned by `zxcvbn_evaluate_json` that has not yet been freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn zxcvbn_free_string(ptr: *mut c_char) {
    let _ = panic::catch_unwind(AssertUnwindSafe(|| {
        if !ptr.is_null() {
            unsafe {
                drop(CString::from_raw(ptr));
            }
        }
    }));
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CStr;

    fn eval_ffi_raw(
        pwd_ptr: *const u8,
        pwd_len: usize,
        inputs_ptr: *const u8,
        inputs_len: usize,
    ) -> Option<String> {
        let raw = unsafe { zxcvbn_evaluate_json(pwd_ptr, pwd_len, inputs_ptr, inputs_len) };
        if raw.is_null() {
            return None;
        }
        let owned = unsafe { CStr::from_ptr(raw) }.to_str().unwrap().to_owned();
        unsafe { zxcvbn_free_string(raw) };
        Some(owned)
    }

    fn eval_ffi(pwd: &[u8], inputs: Option<&[u8]>) -> Option<String> {
        let (in_ptr, in_len) = match inputs {
            Some(b) => (b.as_ptr(), b.len()),
            None => (ptr::null(), 0),
        };
        eval_ffi_raw(pwd.as_ptr(), pwd.len(), in_ptr, in_len)
    }

    #[test]
    fn evaluates_golden_vectors_accurately() {
        let abc = eval_ffi(b"abc123", None).unwrap();
        let wombat = eval_ffi(b"womB@t-attAcK monkey", None).unwrap();
        let weak = eval_ffi(b"password", None).unwrap();
        let strong = eval_ffi(b"correct horse battery staple", None).unwrap();
        let contextual = eval_ffi(b"acme2026!", Some(b"acme\n2026")).unwrap();
        let empty = eval_ffi(b"", None).unwrap();

        assert!(abc.contains(r#""score":0"#), "abc123: {abc}");
        assert!(wombat.contains(r#""score":4"#), "wombat: {wombat}");
        assert!(weak.contains(r#""score":0"#));
        assert!(weak.contains(r#""warning":"This is a top-10 common password.""#));
        assert!(weak.contains(r#""warning_code":"ThisIsATop10Password""#));
        assert!(strong.contains(r#""score":4"#));
        assert!(contextual.contains(r#""matched_word":"acme""#));
        assert!(empty.contains(r#""guesses_log10":0.0"#));

        println!("=== ABC123 ===\n{abc}");
        println!("=== WOMBAT ===\n{wombat}");
    }

    #[test]
    fn handles_nul_bytes_control_chars_l33t_and_unicode_without_panic() {
        let embedded_nul = eval_ffi(b"abc\0123", Some(b"user\0input")).unwrap();
        assert!(embedded_nul.contains(r"\u0000"), "{embedded_nul}");

        let only_nuls = eval_ffi(b"\0\0\0", None).unwrap();
        assert!(only_nuls.contains(r#""score":0"#), "{only_nuls}");

        // All ASCII control characters 0x00..=0x1F + 0x7F
        let mut all_controls: Vec<u8> = (0u8..=0x1F).collect();
        all_controls.push(0x7F);
        let controls_res = eval_ffi(&all_controls, None).unwrap();
        assert!(controls_res.contains(r#""sequence":"#));

        // Heavy l33t table exercise & newlines inside password
        let l33t_heavy = eval_ffi(b"@48(3691!|0$5+7%2\n\r\np@ssw0rd", None).unwrap();
        assert!(l33t_heavy.contains(r#""score":"#));

        // JSON injection attempt inside password and user_inputs
        let json_inj = eval_ffi(
            br#"{"score":4,"guesses":1}\""#,
            Some(br#"{"warning":"fake"}"#),
        )
        .unwrap();
        let parsed: serde_json::Value = serde_json::from_str(&json_inj).unwrap();
        assert!(parsed["score"].as_u64().is_some());

        // Long 40-token password up to MAX_PASSWORD_BYTES
        let max_valid = vec![b'x'; MAX_PASSWORD_BYTES];
        assert!(eval_ffi(&max_valid, None).is_some());

        let max_inputs = vec![b'u'; MAX_INPUTS_BYTES];
        assert!(eval_ffi(b"test-password-check", Some(&max_inputs)).is_some());
    }

    #[test]
    fn validates_null_pointers_length_caps_and_malformed_utf8() {
        // Null password pointer with 0 length is accepted as empty string
        assert!(eval_ffi_raw(ptr::null(), 0, ptr::null(), 0).is_some());

        // Null pointer paired with non-zero length MUST be rejected safely
        assert!(eval_ffi_raw(ptr::null(), 1, ptr::null(), 0).is_none());
        assert!(eval_ffi_raw(b"abc".as_ptr(), 3, ptr::null(), 4).is_none());

        // Exceeding byte caps
        let oversized_pwd = vec![b'a'; MAX_PASSWORD_BYTES + 1];
        assert!(eval_ffi(&oversized_pwd, None).is_none());
        let oversized_inputs = vec![b'b'; MAX_INPUTS_BYTES + 1];
        assert!(eval_ffi(b"valid-password", Some(&oversized_inputs)).is_none());

        // Malformed UTF-8 sequences in both password and user_inputs
        let bad_utf8_cases: &[&[u8]] = &[
            &[0x80],                   // lone continuation byte
            &[0xC0, 0xAF],             // overlong slash
            &[0xE2, 0x82],             // truncated 3-byte char
            &[0xF0, 0x9F, 0x9A],       // truncated 4-byte emoji
            &[0xFF, 0xFE, 0xFD],       // invalid prefix bytes
        ];
        for bad in bad_utf8_cases {
            assert!(eval_ffi(bad, None).is_none());
            assert!(eval_ffi(b"valid-password", Some(bad)).is_none());
        }

        // Null pointer passed to free is a no-op
        unsafe { zxcvbn_free_string(ptr::null_mut()) };
    }

    #[test]
    fn catch_unwind_contains_panics_across_ffi_boundary() {
        extern "C" fn panicking_ffi_guard() -> *mut c_char {
            let outcome = panic::catch_unwind(AssertUnwindSafe(|| -> Option<*mut c_char> {
                panic!("simulated internal panic");
            }));
            match outcome {
                Ok(Some(ptr)) => ptr,
                _ => ptr::null_mut(),
            }
        }

        assert!(panicking_ffi_guard().is_null());
    }
}
