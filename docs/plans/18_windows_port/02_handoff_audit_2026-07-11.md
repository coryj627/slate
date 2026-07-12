# Milestone W handoff audit — gap analysis of the program, specs & issues (2026-07-11)

**Scope:** completeness + correctness audit of the Milestone W (Windows port) planning corpus — `00_program.md`, `01_milestone_brief.md`, `specs/w0–w8`, `specs/gap_analysis.md`, and all 45 GH issues (#714–#756, #603, #381; milestone 22) — with the intent that another developer can pick up the work. Four independent audit passes: spec internal coherence, issue-set quality, drift since authoring (2026-07-06 → 2026-07-11), and ground-truth verification of every factual claim against the repo at `9192ca5` (post-#886).

## Verdict

The corpus is **genuinely handoff-grade in structure**: work-item ↔ spec ↔ issue mapping is exactly bijective (45 ↔ 45 ↔ 45, no orphans either direction), every DoD gate (§W-A–§W-G) traces to an owner, all 20 locked decisions are operationalized, every referenced doc/section/test/script/FFI symbol resolves, all 45 issues are metadata-clean and correctly park-marked except the two absorbed legacy issues, and the corpus's riskiest architectural claim — Mermaid SVG is canonical in Rust, no JS engine anywhere — is **fully true in today's code**.

It is **not yet safe to hand off for the one thing a developer can start today**: the pre-unpark-eligible binding spike (#714) executes from w0's "baseline facts" block, and that block is now wrong in two load-bearing ways (a third foreign-callback trait exists; the predicted Windows-build failure was already fixed before authoring). Beyond that, the defects are: one cross-milestone doctrinal contradiction (PD OCR), four wave-sequencing contradictions, one shipped-chord staleness that survived a partial sync pass, and a set of issue-body hygiene items. **Everything found is fixable in two small docs PRs plus one issue-editing pass (~half a day); nothing is missing at the level of scope, decisions, or acceptance mechanisms.**

## Where the milestone actually stands (verified 2026-07-11)

| # | Entry criterion | Current state | Met? |
|---|---|---|---|
| 1 | T residual closed (milestone 20 closes) | Milestone 20 open, 0 open / 35 closed — human AT smoke pass on canvas still unrecorded | ❌ |
| 2 | P shipped w/ graph textual repr canonical in Rust | Milestone 16: 13 open / 0 closed — not started; repr confirmed absent from core/FFI (correctly so) | ❌ |
| 3 | Standing queue majority-shipped (owner call) | Shipped since authoring: **O** (closed 2026-07-11), **N** de facto (0 open / 23 closed, milestone open). Unshipped: P(13), V(15), X(16), FL(21), XD(13), E(15), PD(7), R/S (no issues filed). ≈2 of 11 | ❌ |
| 4 | W0.5 landed (#717–#719) | All open, zero PRs; ranking/announcement logic verified still Swift-side | ❌ (pre-unpark-eligible, unstarted) |
| 5 | W0-1 spike concluded (#714) | Open; no probe exists (`examples/` has only `swift-cli`); w0 §Decision still "OPEN" | ❌ (pre-unpark-eligible, unstarted) |

**Workable today:** exactly #714 (W0-1 spike), #717/#718/#719 (W0.5 canonicalization). Everything else is parked, and the issue set marks this correctly (except #381 — see F-5).

---

## Findings

Severity: **P0** = would cause wrong work on the pre-unpark items startable today · **P1** = doctrinal contradiction or would stall/mislead execution · **P2** = costs time, stale/imprecise pointers, hygiene · **P3** = polish/awareness.

### A. Pre-unpark-critical (fix before anyone touches #714)

**F-1 · P0 · w0 baseline & probe surface omit the FFI's third foreign callback.**
Program decision 3, gap-row G3, w0 baseline (`w0_spec.md:13`), and the §W-E census list all say **two** `with_foreign` traits (`ScanProgressListener`, `CommandAction`). Since PR #791 (O-2 op-log compaction, commit `88efbdd`, broadened by #802/PR #846) there are **three**: `ScanProgressListener` (lib.rs:1976), **`VaultEventListener` (lib.rs:2077 — `on_error`/`on_file_change`/`on_index_phase`)**, `CommandAction` (lib.rs:4223). `VaultEventListener` is a sustained background-thread, multi-method callback — arguably the hardest marshalling case of the three. A dev running the spike as written evaluates binding generators against an incomplete callback story, and the W0-1 verdict is the program's #1-risk decision.
*Fix:* update w0 baseline facts; add a `VaultEventListener` subscription round-trip to the probe surface (w0 rule 1) and to the §W-E census list; note in W1-1 that the Windows host must install the listener like mac does. Also add the fourth `uniffi::Object` the probe necessarily touches: `CommandRegistry` (lib.rs:4272), omitted from the baseline's Arc-lifetime list.

**F-2 · P0 · w0 baseline misdiagnoses the Windows-build blocker (was already false at authoring).**
`w0_spec.md:15` claims `sync_detect.rs` uses `OsStrExt` "outside test code" and predicts "`#[cfg(windows)]` work in W0-1 before anything links." In fact all non-test `OsStrExt` uses (:197/:226/:767) sit inside platform-gated fns with `#[cfg(not(unix))]` fallbacks that landed in the M-milestone PRs #634/#635 (merged the day before authoring); `libc` is unix-gated in Cargo.toml, and `vault/fs.rs:417` already has a `cfg(windows)` arm. The genuinely unverified risk is different: **no CI has ever compiled any crate for an msvc target**, so dependency-tree compilation on `x86_64-pc-windows-msvc` is the real unknown. W0-1 rule 0's instruction (run `cargo check` for the msvc target first) stands; its attached diagnosis misdirects.
*Fix:* replace the sync_detect claim with "core code appears cfg-clean; the unverified risk is dependency compilation for msvc (never CI-exercised)."

**F-3 · P1 · W0.5-3's inventory pointer under-scopes the announcement vocabulary ~20×.**
`w0_spec.md:99` says inventory "every `post(_:priority:)` call site." Verified: `.post(` has **6** call sites in 4 files, but the actual vocabulary lives behind `postAccessibilityAnnouncement` — **135 call sites across 29 files** — and that free function is defined in `WelcomeView.swift:186`, not `AnnouncementPosting.swift`. Also §W-D's "canonical a11y-event corpus" does not exist today and neither §W-D nor the program states outright that W0.5-3 *creates* it (only implied).
*Fix:* correct the inventory pointer + counts; add one sentence to §W-D: "the corpus is the W0.5-3 deliverable."

### B. Cross-milestone doctrinal contradiction

**F-4 · P1 · Milestone PD (accessible image OCR) contradicts two W doctrinal texts and has no workstream home.**
PD locked decision 4 (`docs/plans/19_image_ocr/00_plan.md`, authored after W): Apple Vision runs in `slate-mac`; "`Windows.Media.Ocr` becomes another engine identity writing into the same store" — and its §W note calls the engine seam "the deliberate slot" for W. But (a) W decision 4 pins C# content to "UI state machines, view models, UIA peers, marshalling, platform I/O adapters — **and nothing else**" (no seat for a content-producing C# OCR worker), and (b) §W-A requires byte-identical outputs of "**every** read-side FFI surface" while PD's reconciliation design (engine identities, `failed_engines`, cross-engine retry) is *built on* engine-dependent, non-identical output. The generated-matrix mechanism absorbs feature rows; it cannot absorb a DoD gate whose universal quantifier a queued milestone is designed to violate.
*Fix (three lines, all in W docs):* extend decision 4's "C# may contain" list with "platform engine adapters designated by upstream milestone specs (PD dec. 4)"; add OCR-derived surfaces to §W-A's normalization/exclusion list (w8_spec W8-4); give PD a feature-conditional home (W3 render/AT rows + W4 panel rows) in the G1-style mapping.

### C. Sequencing contradictions (would stall a literal-minded executor at wave boundaries)

**F-5 · P1 · W3 ⇄ W4 item-level cycle vs wave order.** W3-1 renders tables on the W4-1 grid substrate (`w3_spec.md:12`) and W3-5's `.base` embeds render "via W4-6's grid" (`w3_spec.md:42`), while W4-6 embeds need W3-5 (`w4_spec.md:36`) and the wave table gates Wave 4 on "W3 for embedded content" (`00_program.md:100`). *Fix:* declare the table/`.base`-embed rows explicitly deferred cross-wave rows, or move W4-1 into Wave 3's gate.

**F-6 · P1 · W8-2 theme tokens consumed by Waves 2 and 6.** `w2_spec.md:29` ("theme values from W8-2 tokens") and `w6_spec.md:11` forward-depend on a Wave-8 deliverable gated on "everything." *Fix:* state that a provisional token set ships in W1-1/W2-2 and W8-2 finalizes + contrast-gates it.

**F-7 · P1 · §W-A harness skeleton needed in Wave 2 has no owner.** W2-2/W4 acceptance requires "§W-A rows green … serialized via the harness," but the harness is W8-4 and w8's parenthetical ("should exist much earlier in skeleton form", `w8_spec.md:6`) assigns the skeleton to nobody. *Fix:* add the skeleton deliverable to W0-3 (or W2-2); W8-4 hardens.

**F-8 · P1 · W7-2 notification dispatcher needed by Wave-1 acceptance, scheduled with Wave 5.** W1-1/W1-2/W1-4 checkboxes require canonical-event announcements via `RaiseNotificationEvent` (`w1_spec.md:16,27,45`), but the one dispatcher is pinned "W7-2/3 with W5" (`00_program.md:103`, `w7_spec.md:6`). *Fix:* land the dispatcher core with Wave 1; keep the §W-D census at Wave 5; say so in both files.

### D. Drift since authoring (2026-07-06 → 2026-07-11)

**F-9 · P1 · W1-4's normative chord is stale after #863.** `w1_spec.md:44` pins the quick switcher to **Ctrl+T**. Shipped mac (PR #885, `SlateCommands.swift:1150-1152`): Quick Open = **⌘O** ("#863; was ⌘T"); ⌘T = Duplicate Tab; ⇧⌘T = Reopen Closed Tab; ⌘R = Tasks Review; Open Vault → ⇧⌘O. Under decision 12's ⌘→Ctrl rule, W1-4 currently binds the switcher to the Duplicate-Tab chord. The `w1_spec.md:17` exemplar "(Ctrl+T, Ctrl+F, …)" and issue #723's body carry the same stale chord. Note: PR #886 did a partial W-corpus sync (w5's F2 note) and missed this. *Fix:* same italic stale-note convention on W1-4 + #723 (Ctrl+O; or make the sentence chord-free and table-referencing).

**F-10 · P2 · O's host-consumption disciplines post-date W's mutation text.** O shipped a never-clobber create protocol — FFI `create_exclusive` (lib.rs:617), adopted by every mac create flow (#796/PR #838) — but W1-2/W5-4 say only "all mutations route through existing core session APIs." A C# host calling a plain save/create path would silently break O's marker/op-log correctness, and no W gate catches API *choice* (§W-G only catches re-implementation). *Fix:* name `create_exclusive` in W1-2/W5-4; add a "host obligations" line to w0/w1 (install `VaultEventListener` + `host_logging` sink).

**F-11 · P2 · W4-7 and W8-1 under-describe shipped O (no longer a moving target).** Shipped O = `Leaf.history` with two segments ("This note" + "Deleted" = deleted-file recovery), changes-since-last-open (opt-in), Restore As…, day-grouping, markers toggle, `.canvas`/`.base` history coverage, plus a **retention Settings tab** (`set_history_prefs`) absent from W8-1's settings enumeration. The "accessible-diff representation" claim is accurate (`StructuredDiff` FFI verified). *Fix:* one precision pass over W4-7 + W8-1 now that O is final.

**F-12 · P2 · Queue enumerations omit E and PD.** The moving-target list (9 milestones) and G1's workstream mapping predate milestones 35 (PD) and 36 (E — note export); entry criterion 3's denominator is now 11. E is benign (deterministic core-side IR + CLI verb → §W-A-compatible; natural W5/W8 home just unnamed); PD is F-4. *Fix:* one-line additions to the moving-target list and G1.

**F-13 · P3 · Chord-collision rule is intact but now example-less, with three fresh unadjudicated cases.** W5-1.2's "documented exceptions where Windows conventions win" rule survives, but its only worked example (F2) was retired by #886's own annotation, and #863 creates the first real collisions the table must adjudicate at W5-1: Ctrl+T Duplicate-Tab vs Windows new-tab; Ctrl+R Tasks-Review vs refresh; Ctrl+Shift+T Reopen lands *on* convention; Ctrl+O Quick-Open vs open-dialog (defensible — mirrors mac's own repurposing). *Fix:* optional — seed one live example.

**F-14 · P3 · Adjacent-corpus staleness:** `docs/plans/10_local_history/00_plan.md` still reads "🚧 In progress… O-2–O-5 open" though milestone 15 closed 2026-07-11 — and W devs are directed to shipped-milestone docs as behavior source #2. *Fix:* refresh that header when touching W4-7.

### E. Issue-set hygiene (7 bodies + 2 titles)

**F-15 · P1 · #381 (W2-2) is the only issue with zero park language and reads workable-now.** Its 2026-07-06 footer even says the old dependency is satisfied ("#377 landed…; the remaining dependency is W2-1 (#724)") — no ⏸ banner, no entry-criteria pointer. *Fix:* standard park banner + "(not pre-unpark-eligible)" + strike superseded prose ("csbindgen / evaluate uniffi-bindgen-cs", "#374's review").

**F-16 · P1 · #603 (W0-2) still contains #714's scope and stale park criteria.** Footer affirms "the task list above remains valid" while a checkbox carries the binding-path spike (moved to W0-1/#714 by decision 3), and the park text names T+P only instead of the five entry criteria. *Fix:* strike/annotate the spike checkbox "superseded → #714"; replace park text with the standard banner.

**F-17 · P2 · Dependency-line inconsistencies (4 issues).** #715 claims "Depends on: W0-1, W0-2" but program + w0 run W0-2 ∥ W0-3; #740 lacks the "Depends on: W4-1" all its siblings carry; #732 lacks the W3-1 dependency the program names (and program "mostly parallel" vs w3_spec "W3-1 first" wording should be reconciled); #746 omits W5-1 from deps despite the Wave-6 gate row (sibling #745 lists both; w6_spec also never mentions W5-1 — add a deps line there). *Fix:* four one-line edits + w6 header line.

**F-18 · P2 · #753 (W8-3) body omits its headline deliverables** — no signed MSIX, no x64+ARM64, no auto-update, no pointer to the owner-provided signing identity the program flags as a prerequisite. *Fix:* expand body from w8_spec §W8-3.

**F-19 · P3 · #381/#603 titles lack W-codes** (all 43 others end "(Wx-y)"); `blocked` label applied only to #603 of 41 parked issues. *Fix:* append "(W2-2)"/"(W0-2)"; apply or drop the label uniformly.

### F. Minor pointer/mechanism gaps

**F-20 · P2 · CRLF discipline has no owning work item.** Decision 9's "writes LF, tolerates CRLF on read" appears nowhere else in the corpus (grep-confirmed) — not in W1-1's path-adapter checklist, not §W-A normalization. *Fix:* add CRLF fixtures to W1-1 core-side checklist or a §W-A row.

**F-21 · P2 · §W-G has an owner (W8-6) but no mechanism** — "audit recorded" with no named tool, unlike §W-A/§W-B. *Fix:* name it (dependency-manifest deny-list for WebView2 in `windows.yml` + committed grep-audit note).

**F-22 · P2 · W3-2's "documented UIA property route" for MathML has no pointer** (not a standard UIA pattern; unresolvable from the corpus). *Fix:* cite the concrete route (custom UIA property registration as consumed by NVDA/MathCAT) or the owning 05 section.

**F-23 · P2 · W2-4 places an obligation on unshipped Milestone V recorded nowhere V would see it** ("V ships its a11y announcement contract via the canonical vocabulary", `w2_spec.md:46`). *Fix:* file it on V (or a gap_analysis row marking it a W-imposed constraint).

**F-24 · P2 · W8-4 mischaracterizes the vault generators.** `generate_vault`/`generate_tasks_vault` (`benches/common/mod.rs:95/:211`) take only `file_count` — no seed parameter ("given a fixed seed" implies a knob that isn't there; content is deterministic by construction). Also `crates/slate-core/tests/fixtures/**` is domain-narrow ({bases, canvas, dql, oplog}) — no general Markdown corpus for editor-span/structure/search/backlink §W-A rows. *Fix:* note "add a seed knob or accept the fixed corpus" + plan fixture additions in W8-4.

**F-25 · P2 · W0-4's "command-registry dump" requires driving the mac app — unstated.** `CommandRegistry.list()` exists (lib.rs:4136), but the registry is populated at runtime by the Swift host; `slate-cli` verbs can't produce it. *Fix:* one sentence in W0-4 ("the dump runs via the mac app/test target").

**F-26 · P3 · MathCAT conditioning is textual, already true in code.** Decision 6 pins "MathML + MathCAT speech" unconditionally; W3-2 conditions it ("only if the canonical speech artifact delegates to it by then") with no G-row. Verified: core's `math.rs` already uses mathcat — the condition is satisfied today. *Fix:* record it (G-row or decision-6 wording) so the texts stop disagreeing.

**F-27 · P3 · Small true-ups:** CODEOWNERS already contains the `/apps/slate-windows/` line W0-2 plans to add (`.github/CODEOWNERS:19`) · every lib.rs/Swift line anchor has drifted (VaultSession 282→291, CancelToken 984→1129, DocumentBuffer 2811→3422, CommandAction 3607→4223, fuzzyScore 300→302; symbols all grep-resolve; W0-4 should re-verify the baseline block at unpark) · `05_locked_architecture_decisions.md` contains two "### 8.7" headings (:1337 grid matrix — the one W cites — and :1687) · program calls the three-job §W-A pipeline "two-job" (`00_program.md:112`) · `w8_spec.md:21` "leaves no vault data behind ambiguities" is garbled · program ASCII art "(pre-unpark OK)" visually spans all of Wave 0 though only W0-1/W0.5-* qualify (wave table corrects it) · W3-5's asterisk covers Excalidraw but not its N-conditional `.base` row · gap ledger silently drops the brief's "months 9–12" claim and its "`CommandRegistry` objects" mis-fact (one-line G3/G13 additions) · entry criterion 5 could cross-ref w0 rule 4's GitHub-hosted-runner answer to the decision-16 "circularity" (it is already resolved, just not self-evidently).

---

## What was verified sound (handoff confidence)

- **Bijection exact:** 45 phase-map items ↔ 45 spec sections ↔ 45 issues; spec↔issue back-references all correct; reserved W-E1..E6 correctly issue-less; no stray issues in milestone 22.
- **DoD traceability:** §W-A→W8-4 · §W-B→W0-4+W8-5 · §W-C→W7-4 · §W-D→W7-2 (anchored by W0.5-3) · §W-E→W0-3+W2-1 · §W-F→W8-6 · §W-G→W8-6 (mechanism thin: F-21).
- **Doctrine ground truth:** FFI is proc-macro/UDL-less (zero `.udl`; 27 `#[uniffi::export]`; workspace uniffi **0.31** — the spike's version-compat input); **Mermaid SVG canonical in Rust** (`diagram.rs`, `mermaid-rs-renderer 0.2`, zero WKWebView/JavaScriptCore in the mac app); math `{LaTeX, MathML, speech, braille}` artifact real (`math.rs`, mathcat); code `{source, tokens, semantic_spans}` real; `canvas_apply` FFI real; graph repr correctly absent (entry criterion 2); W0.5 targets genuinely Swift-side today; `PrefsJsonStore` + tests exist; three command-drift tests exist (`SlateCommandsTests.swift:41/:103/:1177+`); `slate.cli.v1` envelope at `output.rs:31`; census convention (59 `census_*` fns) real; `BENCHMARKS.md` matches w2's quoted 245 µs; drift guard exists both sides (`editor_spans.rs:341`, `NoteEditorView.swift:675/:761`); `EditorSpanMappingTests.swift:13-15` fixtures as cited.
- **All external doc refs resolve** — every 05/07/13/06/08/14/17 section cited, t0 interaction contract, `at_smoke_checklist.md`, `docs/help/`, `scripts/build-and-launch.sh`, CONTRIBUTING, every named Swift behavioral-reference file.
- **Expected absences confirmed:** no `apps/slate-windows/`, no windows.yml, no msvc target anywhere in CI (all runners Namespace arm64/x64/mac), license gate `.rs`/`.swift`-only exactly as w0 states (W0-2.6 covers it).
- **Issue metadata:** 45/45 open + milestone 22 + `windows` label; 4/4 pre-unpark issues marked; 39/41 parked issues banner-marked (the 2 exceptions are F-15/F-16).

## Recommended remediation (ordered)

1. **PR 1 — w0 baseline refresh (do before anyone starts #714):** F-1, F-2, F-3, plus the F-27 anchor/CommandRegistry/CODEOWNERS true-ups confined to w0. ~1 hour.
2. **PR 2 — sequencing + doctrine reconciliation:** F-5..F-8 (four scheduling fixes), F-4 (PD three-liner), F-20..F-26 as one docs sweep, F-12 queue enumerations, F-9 chord stale-note, F-10/F-11 O precision, F-14 local-history header. ~2–3 hours.
3. **Issue-editing pass:** F-15..F-19 — seven bodies (#381, #603, #723, #715, #740, #732, #746, #753) + two titles. ~30 min.
4. **One cross-milestone filing:** F-23's vocabulary obligation onto Milestone V.

## Orientation for the incoming developer

Read in this order: `00_program.md` (decisions + waves + DoD) → `specs/w0_spec.md` (your first real work) → `gap_analysis.md` (why the program diverges from the brief) → the wX spec for whatever wave is live. The "Working this program independently" section of the program is accurate and the source hierarchy it names (mac test suites → shipped-milestone program docs → help docs → running the app) fully resolves — trust it. Owner-provided prerequisites you cannot self-serve (flag early): MSIX signing identity, JAWS license, ARM64 hardware/VM, owner availability at unpark for the W0-4 snapshot. Facts you'd otherwise rediscover: workspace uniffi is 0.31; slate-core is likely already cfg-clean for Windows but no CI has ever compiled an msvc target; the parity matrix does not exist yet by design (W0-4 generates it at unpark); §Decision in w0 is "OPEN" on purpose — your spike fills it.

*Audit passes: 4 independent agents (spec coherence · issue quality · drift · ground truth), findings cross-confirmed; F-1 and F-9 confirmed by 2–3 passes independently. No files or issues were modified by this audit.*

---

## Remediation log (2026-07-12)

Applied in the docs + issue tracker the day after the audit:

- **Docs** — F-1/F-2/F-3 (w0 baseline: three callbacks + `CommandRegistry`, msvc-dependency risk replaces the sync_detect misdiagnosis, 135-site announcement inventory + §W-D corpus provenance), F-5..F-8 (deferred cross-wave rows for W3↔W4; provisional token set seeded in W1-1; §W-A skeleton assigned to W0-3 item 5; W7-2 dispatcher core moved to Wave 1), F-4 (decision-4 engine-adapter clause + §W-A exclusion + PD/E mapping in G1), F-9 (W1-4 → Ctrl+O with stale-note; chord table declared normative over spec sentences), F-10 (`create_exclusive` named in W1-2/W5-4; host obligations in w0/W1-1), F-11 (W4-7 O precision; W8-1 history-retention tab), F-12 (queue enumerations + G1), F-13 (live collision examples in W5-1), F-14 (10_local_history header → shipped), F-20 (CRLF fixtures in W1-1; LF-canonical note in §W-A/W8-4), F-21 (§W-G deny-list + grep-audit mechanism in W8-6), F-22 (MathML UIA route = W3-2 first task, candidates named), F-24 (generator seed-knob + fixture-corpus notes in W8-4), F-25 (registry dump runs via mac app/test target), F-26 (G14), F-27 (anchors refreshed + dated; CODEOWNERS pre-exists; three-job pipeline wording; W8-3 uninstall wording; ASCII pre-unpark label; W3-5 asterisk scope; G13 calendar-claim note; entry-criterion 5 runner cross-ref).
- **Issues** — F-15 (#381 park banner + strike pass + title code), F-16 (#603 banner, spike checkbox superseded → #714, CODEOWNERS note, title code, `blocked` label dropped for banner-convention uniformity), F-9 (#723 chord), F-17 (#715 dep corrected; #732/#740/#746 dep lines added), F-18 (#753 expanded to headline deliverables + owner prerequisites).
- **Deferred → closed 2026-07-12** — F-23's obligation is now filed on Milestone V as [#888](https://github.com/coryj627/slate/issues/888) (was gap-row-only pending owner approval). W7-4's per-wave instrument, §W-B budget pinning, and all baseline re-verification at unpark remain W0-4/W7-4 duties by design.

### Codex adversarial pass (gpt-5.6-sol, 2026-07-12)

The applied remediation was adversarially reviewed by Codex (verbatim output: [`03_codex_adversarial_review_2026-07-12.md`](03_codex_adversarial_review_2026-07-12.md)). 12 findings; initial verdict **not safe to hand off** — the docs pass had not been propagated into the live issue bodies (above all #714) and two fixes had moved contradictions rather than resolved them. Dispositions, all applied same-day unless noted:

1. **Accepted (P0)** — #714 body rewritten: three callbacks incl. `VaultEventListener`, `CommandRegistry`, current anchors, widened scoring dims; program §W-E bullet now enumerates all three traits + listener lifetime + `CommandAction` error round-trips.
2. **Accepted, adjusted** — count corrected to **126 call expressions / 28 files** (135 was raw occurrences; independently re-verified) and replaced with a reproducible `rg` query in w0 + #719; #719 redirected to the global-helper surface.
3. **Accepted (chose sequential)** — the program's `{ W0-2 ∥ W0-3 }` parallelism was the defect, not #715's original dep: W0-3's censuses/CI/log-sink live in W0-2's artifacts. Order is now **W0-1 → W0-2 → W0-3** everywhere (program ASCII + wave table, w0 execution order, #715, #754).
4. **Accepted (chose two-slice exception)** — #748/w7 now document W7-2 as two PR slices against one issue (core with Wave 1, census with Wave 5), the W7-4 rolling precedent.
5. **Accepted** — deferred rows now *transfer*: excluded from W3-1/W3-5 acceptance, explicitly owned by W4-1/W4-6 (specs + #728/#732).
6. **Accepted** — minimal general-Markdown fixture set added to W0-3's skeleton deliverable; #754 refreshed (three-job title, generator reality, skeleton ownership); #755 gains the W8-4 dependency; program's "randomized vault" wording fixed.
7. **Accepted, adjusted** — rather than an early CI gate: W1-1 seed values must meet Lc ≥ 75 (ad hoc check), W6-1's fills are APCA-verified as part of its own acceptance, W8-2 moves the check into CI; #720/#752 synced.
8. **Accepted** — w0 no longer claims failures "will come from the dependency tree"; both dependency-tree and first-party target gaps are named as open possibilities.
9. **Accepted** — 10_local_history header now labels the body a pre-implementation baseline superseded by shipped behavior, and separates milestone-15 membership (#539–#544, #832, #835, #837) from the out-of-milestone follow-ups.
10. **Accepted for W0-2, rejected for W0-1** — `dotnet format --verify-no-changes` is now a named `windows.yml` step in W0-2 (+ CONTRIBUTING); not added to the spike's throwaway workflow — the spike is disposable by design and the gate belongs to the permanent pipeline.
11. **Accepted** — full sibling sync applied: #714, #719, #720, #721, #728, #732, #739, #744, #748, #751, #752, #754 (+title), #755.
12. **Accepted** — G3 rephrased (no ordinal, no stale anchor), G6 two-job→three-job, w2 baseline anchor 2811→3422; stale-string sweep over the corpus comes back clean (remaining hits are historical evidence in this audit or deliberate correction notes).

Codex also **cleared two deliberate stances**: the PD engine-adapter clause is bounded enough ("explicitly designated… today only PD"), and LF-canonical/no-CRLF-normalization is internally consistent given W1-1's write-LF fixtures.
