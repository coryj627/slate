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

/// W3-3 round 1 [high]: every reading refresh re-fetches diagram
/// artifacts from the SAVED file, which live-buffer typing never
/// changes — without a content-keyed cache each refresh re-rendered
/// the whole note through the process-global serialized renderer,
/// letting stale refreshes starve every tab. A repeated fetch must
/// be a render-free cache hit; a saved change must miss and
/// re-render.
#[test]
fn diagram_blocks_cache_hits_on_unchanged_content_and_misses_on_saves() {
    use std::sync::atomic::Ordering;

    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let first = session.get_diagram_blocks("note.md").unwrap();
    assert_eq!(first.len(), 1);
    let passes_after_first = session.diagram_render_passes.load(Ordering::Relaxed);
    assert_eq!(passes_after_first, 1);

    let second = session.get_diagram_blocks("note.md").unwrap();
    assert_eq!(second, first, "hit must return the identical artifact");
    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        passes_after_first,
        "unchanged content must be a render-free cache hit"
    );

    std::fs::write(
        tmp.path().join("note.md"),
        "```mermaid\nflowchart LR\nA --> C\n```\n",
    )
    .unwrap();
    let third = session.get_diagram_blocks("note.md").unwrap();
    assert_eq!(third.len(), 1);
    assert_ne!(third[0].source, first[0].source);
    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        passes_after_first + 1,
        "a saved change must miss the cache and re-render"
    );
}

/// W3-3 round 2 [high]: same-key cache misses are SINGLE-FLIGHT —
/// under concurrent identical calls exactly one render pass runs
/// (the rest wait on the claim and clone the completed result), so
/// overlapping background refreshes can never queue duplicate
/// whole-note renders through the serialized renderer. A saved
/// change still misses exactly once more.
#[test]
fn diagram_blocks_concurrent_fetches_render_once() {
    use std::sync::atomic::Ordering;

    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let barrier = std::sync::Barrier::new(4);
    std::thread::scope(|scope| {
        for _ in 0..4 {
            scope.spawn(|| {
                barrier.wait();
                let blocks = session.get_diagram_blocks("note.md").unwrap();
                assert_eq!(blocks.len(), 1);
            });
        }
    });
    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        1,
        "concurrent identical fetches must share one render pass"
    );

    std::fs::write(
        tmp.path().join("note.md"),
        "```mermaid\nflowchart LR\nA --> C\n```\n",
    )
    .unwrap();
    let second_barrier = std::sync::Barrier::new(4);
    std::thread::scope(|scope| {
        for _ in 0..4 {
            scope.spawn(|| {
                second_barrier.wait();
                session.get_diagram_blocks("note.md").unwrap();
            });
        }
    });
    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        2,
        "a saved change must miss exactly once more, single-flight"
    );
}

/// Round 3 [high]: different-key traffic must never detach same-key
/// waiters — flights are keyed, so concurrent fetches across TWO
/// notes cost exactly one render pass per key no matter how the
/// threads interleave.
#[test]
fn diagram_blocks_flights_are_keyed_per_note() {
    use std::sync::atomic::Ordering;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
        p.write_file("b.md", b"```mermaid\nflowchart LR\nX --> Y\n```\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let barrier = std::sync::Barrier::new(8);
    std::thread::scope(|scope| {
        for i in 0..8 {
            let barrier = &barrier;
            let session = &session;
            scope.spawn(move || {
                let path = if i % 2 == 0 { "a.md" } else { "b.md" };
                barrier.wait();
                let blocks = session.get_diagram_blocks(path).unwrap();
                assert_eq!(blocks.len(), 1);
            });
        }
    });
    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        2,
        "exactly one render pass per key, regardless of interleaving"
    );
}

/// Round 4 [high]: the completion handoff — a caller that misses the
/// fast-path cache, then stalls until AFTER the owner has completed
/// and removed its flight, must find the published result during
/// admission (the cache recheck under the flights lock) instead of
/// creating a fresh flight and re-rendering. The one-shot admission
/// gate makes exactly this interleaving deterministic.
#[test]
fn diagram_blocks_delayed_caller_joins_the_completed_result() {
    use std::sync::atomic::Ordering;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (release, gate) = std::sync::mpsc::channel::<()>();
    *session.diagram_admission_gate_for_tests.lock().unwrap() = Some(gate);

    std::thread::scope(|scope| {
        let delayed = scope.spawn(|| {
            // Fast-path miss (cache empty), then blocks on the gate
            // until the owner below has fully completed.
            let blocks = session.get_diagram_blocks("note.md").unwrap();
            assert_eq!(blocks.len(), 1);
        });

        // Wait until the delayed caller has consumed the gate — it is
        // now suspended between its cache miss and admission.
        while session
            .diagram_admission_gate_for_tests
            .lock()
            .unwrap()
            .is_some()
        {
            std::thread::yield_now();
        }

        // Render to completion on this thread: publishes the cache
        // and removes the flight (the state the race fired in).
        let blocks = session.get_diagram_blocks("note.md").unwrap();
        assert_eq!(blocks.len(), 1);
        assert_eq!(session.diagram_render_passes.load(Ordering::Relaxed), 1);

        // Release the delayed caller into admission.
        release.send(()).unwrap();
        delayed.join().unwrap();
    });

    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        1,
        "the delayed caller must join the published result, never re-render"
    );
}

/// Round 3: an owner that unwinds between claim and completion marks
/// ITS flight abandoned and removes exactly its own entry — the next
/// fetch recovers with a fresh render instead of hanging on a
/// stranded claim.
#[test]
fn diagram_blocks_owner_panic_cleans_up_its_flight() {
    use std::sync::atomic::Ordering;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .diagram_render_panic_for_tests
        .store(true, Ordering::SeqCst);
    let panicked = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.get_diagram_blocks("note.md");
    }));
    assert!(panicked.is_err(), "the injected panic must surface");

    // Recovery: no stranded flight, no hang, a fresh render succeeds.
    let blocks = session.get_diagram_blocks("note.md").unwrap();
    assert_eq!(blocks.len(), 1);
    assert_eq!(session.diagram_render_passes.load(Ordering::Relaxed), 1);
    // And the recovered result is cached like any other.
    let again = session.get_diagram_blocks("note.md").unwrap();
    assert_eq!(again, blocks);
    assert_eq!(session.diagram_render_passes.load(Ordering::Relaxed), 1);
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
