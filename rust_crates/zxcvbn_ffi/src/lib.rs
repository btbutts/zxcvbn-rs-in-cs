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
    match value {
        CrackTimeSeconds::Integer(i) => i as f64,
        CrackTimeSeconds::Float(f) => f,
    }
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
/// - When `inputs_len > 0` and `inputs_ptr` is non-null, `inputs_ptr` must be a valid pointer to at least `inputs_len` readable bytes.
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
        if pwd_len > 0 && pwd_ptr.is_null() {
            return None;
        }

        let password = if pwd_len == 0 {
            ""
        } else {
            let pwd_bytes = unsafe { slice::from_raw_parts(pwd_ptr, pwd_len) };
            str::from_utf8(pwd_bytes).ok()?
        };

        let user_inputs: Vec<&str> = if !inputs_ptr.is_null() && inputs_len > 0 {
            let input_bytes = unsafe { slice::from_raw_parts(inputs_ptr, inputs_len) };
            let inputs_str = str::from_utf8(input_bytes).ok()?;
            inputs_str.split('\n').filter(|s| !s.is_empty()).collect()
        } else {
            Vec::new()
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

    fn eval_ffi(pwd: &[u8], inputs: Option<&[u8]>) -> Option<String> {
        let (in_ptr, in_len) = match inputs {
            Some(b) => (b.as_ptr(), b.len()),
            None => (ptr::null(), 0),
        };
        let raw = unsafe { zxcvbn_evaluate_json(pwd.as_ptr(), pwd.len(), in_ptr, in_len) };
        if raw.is_null() {
            return None;
        }
        let s = unsafe { CStr::from_ptr(raw) }.to_str().unwrap().to_owned();
        unsafe { zxcvbn_free_string(raw) };
        Some(s)
    }

    #[test]
    fn evaluates_weak_strong_contextual_and_empty_passwords() {
        let weak = eval_ffi(b"password", None).unwrap();
        let strong = eval_ffi(b"correct horse battery staple", None).unwrap();
        let contextual = eval_ffi(b"acme2026!", Some(b"acme\n2026")).unwrap();
        let empty = eval_ffi(b"", None).unwrap();

        assert!(weak.contains(r#""score":0"#));
        assert!(weak.contains(r#""warning":"This is a top-10 common password.""#));
        assert!(weak.contains(r#""warning_code":"ThisIsATop10Password""#));
        assert!(strong.contains(r#""score":4"#));
        assert!(contextual.contains(r#""matched_word":"acme""#));
        assert!(empty.contains(r#""guesses_log10":0.0"#));

        println!("=== WEAK DTO ===\n{weak}");
        println!("=== STRONG DTO ===\n{strong}");
        println!("=== CONTEXTUAL DTO ===\n{contextual}");
        println!("=== EMPTY DTO ===\n{empty}");
    }

    #[test]
    fn rejects_oversized_and_invalid_utf8_and_null_free_is_safe() {
        let oversized = vec![b'a'; MAX_PASSWORD_BYTES + 1];
        assert!(eval_ffi(&oversized, None).is_none());

        let invalid_utf8 = [0xff, 0xfe, 0xfd];
        assert!(eval_ffi(&invalid_utf8, None).is_none());

        unsafe { zxcvbn_free_string(ptr::null_mut()) };
    }
}
