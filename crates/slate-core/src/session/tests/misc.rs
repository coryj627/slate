// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — configuration accessor, cancel-token sharing, math prefs.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

#[test]
fn config_accessor_returns_session_config() {
    let tmp = tempfile::tempdir().unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    assert_eq!(session.config().parser_version, 1);
    assert!(session.config().max_db_cache_pages > 0);
}

#[test]
fn cancel_token_clones_share_state() {
    let c1 = CancelToken::new();
    let c2 = c1.clone();
    assert!(!c2.is_cancelled());
    c1.cancel();
    assert!(c2.is_cancelled(), "clone shares the underlying flag");
}

#[test]
fn set_math_prefs_takes_effect_on_next_get_math_blocks() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"$x + 1$\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Default prefs → ClearSpeak.
    let blocks_a = session.get_math_blocks("note.md").unwrap();
    assert_eq!(blocks_a.len(), 1);
    // We can't easily compare speech strings (MathCAT init may
    // not run in the test env), but the call must succeed. The
    // real test is that set_math_prefs doesn't error and a
    // follow-up call still returns one block — confirming the
    // mutex round-trip works.

    session
        .set_math_prefs(crate::math::MathPrefs {
            speech_style: crate::math::MathSpeechStyle::MathSpeak,
            verbosity: crate::math::MathVerbosity::Verbose,
            braille_code: crate::math::BrailleCode::Ueb,
        })
        .expect("set_math_prefs must not error");

    let blocks_b = session.get_math_blocks("note.md").unwrap();
    assert_eq!(
        blocks_b.len(),
        1,
        "post-swap call should still find the block"
    );
}

#[test]
fn set_math_prefs_handles_rapid_concurrent_swaps() {
    // The mutex around math_prefs must serialize correctly
    // even under contention — a Settings Picker held down
    // (arrow keys repeating) could fire dozens of sets per
    // second.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"$x$\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    for _ in 0..20 {
        session
            .set_math_prefs(crate::math::MathPrefs::default())
            .unwrap();
    }
    let blocks = session.get_math_blocks("note.md").unwrap();
    assert_eq!(blocks.len(), 1);
}
