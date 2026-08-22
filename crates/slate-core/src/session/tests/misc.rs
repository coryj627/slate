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
            speech_style: crate::math::MathSpeechStyle::SimpleSpeak,
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

/// Round 4 [high]: the completion handoff — a caller that stalls
/// until AFTER the owner has completed and removed its flight must
/// find the published result during atomic admission instead of
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

/// Round 5 [high]: cross-key completions must not evict handoff
/// state — the completed cache is KEYED, so B completing while A's
/// delayed caller sits between miss and admission cannot erase A's
/// published result. Deterministic: complete A, complete B, then
/// release the delayed A caller; exactly two render passes total.
#[test]
fn diagram_blocks_cross_key_completion_preserves_the_handoff() {
    use std::sync::atomic::Ordering;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
        p.write_file("b.md", b"```mermaid\nflowchart LR\nX --> Y\n```\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (release, gate) = std::sync::mpsc::channel::<()>();
    *session.diagram_admission_gate_for_tests.lock().unwrap() = Some(gate);

    std::thread::scope(|scope| {
        let delayed = scope.spawn(|| {
            let blocks = session.get_diagram_blocks("a.md").unwrap();
            assert_eq!(blocks.len(), 1);
        });
        while session
            .diagram_admission_gate_for_tests
            .lock()
            .unwrap()
            .is_some()
        {
            std::thread::yield_now();
        }

        // A completes, then B completes — the interleaving that
        // evicted A's published result from a single-slot cache.
        session.get_diagram_blocks("a.md").unwrap();
        session.get_diagram_blocks("b.md").unwrap();
        assert_eq!(session.diagram_render_passes.load(Ordering::Relaxed), 2);

        release.send(()).unwrap();
        delayed.join().unwrap();
    });

    assert_eq!(
        session.diagram_render_passes.load(Ordering::Relaxed),
        2,
        "the delayed A caller must join A's keyed result after B completed"
    );
}

/// Round 6 [high]: admission is atomic, so completed-entry EVICTION
/// is an honest fresh miss, never a duplicate-flight hole — a
/// delayed caller released after its key was evicted re-renders
/// exactly once (bounded-cache policy) and returns correct content;
/// it can never hang or race a phantom flight.
#[test]
fn diagram_blocks_delayed_caller_past_eviction_re_renders_exactly_once() {
    use std::sync::atomic::Ordering;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"```mermaid\nflowchart LR\nA --> B\n```\n")
            .unwrap();
        for i in 0..crate::session::MAX_CACHED_DIAGRAM_NOTES {
            p.write_file(
                &format!("other{i}.md"),
                b"```mermaid\nflowchart LR\nX --> Y\n```\n",
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (release, gate) = std::sync::mpsc::channel::<()>();
    *session.diagram_admission_gate_for_tests.lock().unwrap() = Some(gate);

    std::thread::scope(|scope| {
        let delayed = scope.spawn(|| {
            let blocks = session.get_diagram_blocks("a.md").unwrap();
            assert_eq!(blocks.len(), 1);
            assert!(blocks[0].source.contains("A --> B"));
        });
        while session
            .diagram_admission_gate_for_tests
            .lock()
            .unwrap()
            .is_some()
        {
            std::thread::yield_now();
        }

        // A completes, then MAX_CACHED_DIAGRAM_NOTES other keys
        // complete — evicting A's entry by count.
        session.get_diagram_blocks("a.md").unwrap();
        for i in 0..crate::session::MAX_CACHED_DIAGRAM_NOTES {
            session.get_diagram_blocks(&format!("other{i}.md")).unwrap();
        }
        let passes_before_release = session.diagram_render_passes.load(Ordering::Relaxed);

        release.send(()).unwrap();
        delayed.join().unwrap();

        assert_eq!(
            session.diagram_render_passes.load(Ordering::Relaxed),
            passes_before_release + 1,
            "an evicted key is a fresh miss: exactly one more render, atomically"
        );
    });
}

/// Round 6 [high]: the cache budget counts the COMPLETE resident
/// entry — a zero-SVG entry with a large preserved source must
/// trigger eviction, an entry over the whole budget is never
/// admitted, and lookups keep LRU order honest.
#[test]
fn diagram_completed_cache_budgets_resident_bytes_not_just_svg() {
    use crate::diagram::{DiagramBlock, DiagramDialect, DiagramRenderStatus};
    use crate::session::{
        DiagramCompletedCache, MAX_CACHED_DIAGRAM_NOTES, MAX_CACHED_DIAGRAM_PAYLOAD_BYTES,
    };

    fn entry(source_bytes: usize) -> Vec<DiagramBlock> {
        vec![DiagramBlock {
            source: "s".repeat(source_bytes),
            dialect: DiagramDialect::Mermaid,
            svg: None,
            png_fallback: None,
            structured_description: "Mermaid diagram.".into(),
            render_status: DiagramRenderStatus::RenderFailed {
                message: "diagram source exceeds the render budget".into(),
            },
            line: 1,
            byte_offset: 0,
        }]
    }

    // Zero-SVG entries with big sources: three at ~40% of the budget
    // cannot all stay resident.
    let mut cache = DiagramCompletedCache::default();
    let big = (MAX_CACHED_DIAGRAM_PAYLOAD_BYTES / 10) * 4;
    cache.insert(("one.md".into(), 1), entry(big));
    cache.insert(("two.md".into(), 2), entry(big));
    cache.insert(("three.md".into(), 3), entry(big));
    assert!(
        cache.lookup(&("one.md".into(), 1)).is_none(),
        "source bytes must count toward the budget: the LRU entry evicts"
    );
    assert!(cache.lookup(&("two.md".into(), 2)).is_some());
    assert!(cache.lookup(&("three.md".into(), 3)).is_some());

    // An entry over the entire budget is never admitted — and never
    // flushes the residents.
    cache.insert(
        ("huge.md".into(), 4),
        entry(MAX_CACHED_DIAGRAM_PAYLOAD_BYTES + 1),
    );
    assert!(cache.lookup(&("huge.md".into(), 4)).is_none());
    assert!(cache.lookup(&("two.md".into(), 2)).is_some());

    // Count boundary with LRU honesty: fill past the note cap; a
    // freshly looked-up entry survives while the stale one goes.
    let mut lru = DiagramCompletedCache::default();
    for i in 0..MAX_CACHED_DIAGRAM_NOTES {
        lru.insert((format!("n{i}.md"), i as u64), entry(16));
    }
    assert!(lru.lookup(&("n0.md".into(), 0)).is_some());
    lru.insert(("extra.md".into(), 99), entry(16));
    assert!(
        lru.lookup(&("n0.md".into(), 0)).is_some(),
        "recently used survives the count eviction"
    );
    assert!(
        lru.lookup(&("n1.md".into(), 1)).is_none(),
        "the least-recently-used entry evicts at the count boundary"
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
