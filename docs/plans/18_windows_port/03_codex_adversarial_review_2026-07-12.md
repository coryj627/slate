# Codex adversarial review — Milestone W remediation (2026-07-12)

*Reviewer: Codex (model `gpt-5.6-sol`), invoked over the 2026-07-12 remediation working tree + the 8 post-edit issue bodies. Brief: fact-check every claim the edits introduced, judge fix-efficacy per audit finding (F-1..F-27), hunt new contradictions, check issue↔spec consistency, and find what both the audit and the fixes missed. Output below is Codex's deliverable, verbatim. Dispositions applied the same day are recorded in `02_handoff_audit_2026-07-11.md` §Remediation log.*

---

## Findings

1. **P0 — Issue #714 still specifies the obsolete two-callback spike.**

   The executable spec now requires all three foreign callbacks, including the sustained, multi-method `VaultEventListener` (w0_spec.md:13, w0_spec.md:33). But live issue **#714**, the one a developer can start now, still lists only `ScanProgressListener` and `CommandAction`, omits `VaultEventListener` and `CommandRegistry`, and retains the stale `CommandAction` anchor `lib.rs:3607`. The permanent §W-E gate also still enumerates only concurrent `ScanProgressListener` callbacks (00_program.md:119), while the scoring dimensions do not explicitly test listener registration/unregistration lifetime, all three listener methods, or synchronous `CommandAction` error return (w0_spec.md:35).

   **Remedy:** Update #714, §W-E, and the scoring rubric to require all three callback shapes, `CommandRegistry`, listener lifetime/unregistration, three-method delivery, and `CommandAction` success/error round-trips.

2. **P1 — Issue #719 still directs the announcement migration at the wrong surface, and the new 135-site claim is false.**

   Live issue **#719** says "Inventory every `AnnouncementPosting` call site," which reaches only the injected poster seam and misses the dominant global-helper surface. Meanwhile w0_spec.md:22 and w0_spec.md:101 call 135/29 a call-site count. Executed source counts found:

   - 135 raw textual occurrences in 29 files;
   - 127 call-shaped occurrences, including the function definition;
   - therefore 126 call expressions across 28 files.

   The six `.post(` matches are also not six independent vocabulary triggers: they include the `NSAccessibility.post` implementation and internal forwarding. The definition itself is correctly identified at WelcomeView.swift:186.

   **Remedy:** Rewrite #719 around global-helper calls, injected `AnnouncementPosting`, and `CanvasAnnouncer`; replace the hard count with an accurate, reproducible inventory query.

3. **P1 — The corrected #715 dependency contradicts the artifacts W0-3 consumes.**

   The program and spec now run W0-2 and W0-3 in parallel (00_program.md:97, w0_spec.md:6); updated issue **#715** consequently says it depends only on #714. But W0-2 owns the .NET solution, xUnit project, and `windows.yml` (w0_spec.md:47, w0_spec.md:49); W0-3 immediately requires that xUnit harness, Windows runner, app logging, and CI harness (w0_spec.md:61–64). A standalone W0-3 PR has nowhere to put or run its deliverables.

   **Remedy:** Make W0-3 depend on W0-2, or split a minimal solution/xUnit/CI scaffold from W0-2 that lands before both remaining slices.

4. **P1 — The Wave-1/Wave-5 dispatcher split is not executable under the one-issue/one-PR contract.**

   W7 declares one PR per issue except W7-4 (w7_spec.md:3). It now says W7-2's core lands in Wave 1 but its complete dispatcher/census closes in Wave 5 (w7_spec.md:6, w7_spec.md:18). W1-1 consumes that core (w1_spec.md:15), but live issue **#748** still describes one dispatcher plus the full census in one PR and contains no Wave-1 landing or split-delivery rule.

   **Remedy:** Split the dispatcher core into a Wave-1 issue/PR with an explicit W1-1 dependency, or grant #748 a documented stacked/rolling-PR exception.

5. **P1 — The deferred W3 rows decorate the W3↔W4 cycle but do not resolve issue closure or wave gates.**

   The program now calls the table and `.base`-embed work "matrix-tracked, not wave-blocking" (00_program.md:100). Yet §W-C still requires each surface's rows before its wave closes (00_program.md:117, w7_spec.md:32). W3-1 says it closes with a temporary plain table (w3_spec.md:12), but its acceptance still requires all block rows green (w3_spec.md:15). W3-5 likewise defers `.base` embeds while requiring all embed rows green (w3_spec.md:42, w3_spec.md:45). Live issue **#728** still directly requires W4-1-backed tables; #732 mentions deferral but not who owns the later completion PR.

   **Remedy:** Transfer the deferred acceptance rows explicitly to W4-1/W4-6 and exclude them from W3 closure, or keep the W3 issues/wave open until their dependencies land.

6. **P1 — The early harness has no early fixture owner, while #754 and #755 retain the old ordering story.**

   W0-3 now owns only a skeleton over "probe fixtures" (w0_spec.md:64), but W2-2 must already make editor-span rows green (w2_spec.md:31). The missing general Markdown fixtures are still assigned to late W8-4 (w8_spec.md:25). Live issue **#754** remains materially stale: its title says "two-job," its body says "seeded randomized vault," and it still says the skeleton merely "should exist early." Live issue **#755** depends only on W0-4 even though the spec orders W8-4 → W8-5 (w8_spec.md:6). The program also still calls the vault randomized (00_program.md:115).

   **Remedy:** Move the minimum Markdown/cross-surface fixture corpus into W0-3 or W2-2, refresh #754, and either add #754 to #755's dependencies or declare W8-4/W8-5 parallel.

7. **P1 — "Good-enough" provisional tokens cannot satisfy Wave-6's APCA-verified contract.**

   W1-1 seeds token structure with "good-enough values" (w1_spec.md:18); W6-1 then requires APCA-verified canvas fills from those tokens (w6_spec.md:11), while the actual APCA gate does not arrive until W8-2 (w8_spec.md:16). That conflicts with the program's APCA requirement and per-wave closure (00_program.md:61, 00_program.md:117). Live #720 does not mention its new token deliverable, while #752 still reads as though W8 owns the token theme wholesale.

   **Remedy:** Require the W1 seed values to pass an initial APCA gate and make W8-2 extend/finalize that gate, then sync #720/#752.

8. **P2 — F-2 was fixed by replacing one unsupported certainty with another.**

   The named `sync_detect.rs` Unix uses are correctly shown to be gated, but w0_spec.md:32 now states that any Windows failure "will come from the dependency tree." The same spec admits no Windows target has ever compiled (w0_spec.md:15); therefore untested target-specific Slate code can still fail. The next clause even anticipates additional `#[cfg]` work, contradicting the dependency-only prediction.

   **Remedy:** Say the previously named blockers are already gated, while the first msvc build may reveal either dependency-tree or remaining first-party target errors.

9. **P2 — The refreshed O header turns a historical plan into a contradictory "source of truth."**

   10_local_history/00_plan.md:3 says shipped behavior is the source of truth for W, but the unchanged body says `StructuredDiff`, recovery, and UI do not exist (line 29) and still lists `.canvas`/`.base` history and Restore As as deferred (line 109). Those are exactly the shipped capabilities W4-7 now consumes (w4_spec.md:40). The header also misleadingly associates the milestone's "0 open / 9 closed" count with follow-ups #795–#802/#831: live milestone 15's nine issues are #539–#544, #832, #835, and #837; the nine follow-ups are closed but have no milestone.

   **Remedy:** Label the body explicitly as the pre-implementation baseline and point consumers to current tests/help/specs, while separating milestone membership from the post-close follow-up ledger.

10. **P2 — `dotnet format pre-push` is prose, not an existing gate.**

    00_program.md:8 is the only repository occurrence of `dotnet format`. No hook, workflow, acceptance item, or issue owns `dotnet format --verify-no-changes`; W0-2's permanent workflow only says build/generate/test (w0_spec.md:49).

    **Remedy:** Add `dotnet format --verify-no-changes` to #714's probe workflow and W0-2's permanent `windows.yml`, with the local command documented in CONTRIBUTING.

11. **P2 — Several docs fixes were not propagated to their owning live issue bodies.**

    Beyond the blockers above:

    - **#721/#744** still say generic core mutations and omit mandatory `create_exclusive`, despite w1_spec.md:27 and w5_spec.md:26.
    - **#739** retains the old minimal O surface despite w4_spec.md:40.
    - **#751** omits history-retention settings despite w8_spec.md:10.

    Of the eight exported edited bodies, #381, #603, #723, #740, #746, and #753 are consistent; #715 is affected by finding 3, and #732 by finding 5. The strike-through treatment in #381/#603 preserves rather than falsifies their historical record.

    **Remedy:** Run a sibling issue-body sync for every load-bearing spec addition, particularly #720/#721/#728/#739/#744/#748/#751/#752/#754/#755.

12. **P2 — The claimed F-27 factual refresh left contradictory sibling anchors and pipeline wording.**

    gap_analysis.md:9 still calls `CommandAction` the "second" foreign trait at stale line 3607, then appends a note saying a third trait exists. gap_analysis.md:12 still calls the three-job pipeline "two-job." w2_spec.md:10 still anchors `DocumentBuffer` at 2811 instead of 3422.

    **Remedy:** Refresh every corpus occurrence, not only the w0 baseline—or remove volatile numeric anchors where symbol names suffice.

## Remediation efficacy ledger

- **Not closed:** F-5.
- **Partially closed:** F-1, F-3, F-6, F-7, F-8, F-10, F-11, F-14, F-17, F-24, F-27.
- **Resolved as originally framed:** F-2, F-4, F-9, F-12, F-13, F-15, F-16, F-18–F-22, F-25, F-26. F-2 nevertheless introduced the new overclaim in finding 8.
- **Deferred as requested:** F-23.

The PD adapter clause is sufficiently bounded by "explicitly designated" locked upstream decisions and "today only PD"; it does not authorize arbitrary C# content producers. The LF-canonical/no-normalization stance is internally compatible with tolerating CRLF input, provided W1's host-write fixtures enforce LF.

## Verified claims

- Exactly three `with_foreign` traits and anchors are correct: `ScanProgressListener` 1976, `VaultEventListener` 2077 with all three methods, and `CommandAction` 4223; the four object anchors, including `CommandRegistry` 4272, are correct.
- Workspace UniFFI is 0.31. The cited `sync_detect.rs` uses are platform-gated; the non-Unix fallbacks, Unix-only `libc`, and `vault/fs.rs:417` Windows arm exist.
- `AnnouncementPriority:9`, `AnnouncementPosting:30`, `WelcomeView:186`, and `CommandPaletteModel.fuzzyScore:302` are correct. Only the 135-call-site characterization is wrong.
- Both vault generators accept only `file_count`; fixtures contain only `bases`, `canvas`, `dql`, and `oplog`; CODEOWNERS line 19 already covers Windows.
- The shipped chord map is correct: Quick Open ⌘O, Duplicate Tab ⌘T, Reopen Closed Tab ⇧⌘T, Tasks Review ⌘R.
- `create_exclusive` exists at UniFFI line 617; `set_history_prefs` and `StructuredDiff` are FFI-exposed.
- O's nine follow-up issues are closed and PRs #838–#846/#865 merged on 2026-07-11; the only error is presenting them as milestone 15's nine counted issues.
- The PD engine seam, generated-vault signatures, O surface claims, and three-job architecture otherwise fact-check.

## Verdict

**No — the corpus is not safe to hand to a fresh developer starting issue #714.** The P0 is directly in #714's executable body: it still evaluates the binding choice without the hardest callback shape and without the complete permanent safety rubric. The Wave-0 dependency problem and the unresolved dispatcher/harness/wave-closure ownership would then stall execution shortly afterward.

The review remained read-only. `git diff --check` completed successfully, and final status matched the intentionally dirty review subject.
