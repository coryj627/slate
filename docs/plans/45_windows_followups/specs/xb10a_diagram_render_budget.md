# XB-10a — A diagram render has a time budget, and a diagram past it degrades with core's sentence

Issues: #1580. Size M. Lanes: rust, windows (`rust-tests`, plus the unit lane for the one Windows display fact), swift (compile: a new FFI function). AT: — (the Windows failed state already speaks `failed.Message`; no new reader-facing path here; XB-10b carries the spot check). Wave: X4, slot 3. Depends on: XD3. Parallel with: XB-8, XB-9. Interlock: plan 44's WB-11 and WB-20 touch the reading view's diagram handling on Windows; this chunk's host change is one test; whichever lands second rebases. Codex adversarial review per plan 44 §4 rule 5 (size M).

## Goal

No diagram render can hold a reading refresh for longer than its budget. A render past 5 s stops being waited on and its block degrades to `RenderFailed` with a core sentence that names the budget; a note whose renders have used 20 s degrades the rest without rendering; at most two abandoned renders run in the background at once, and while that cap holds a new render is refused with the same sentence. Both hosts show it through the failed state they already render.

## Current state on main

- `crates/slate-core/src/diagram.rs`: `DiagramRenderStatus::{Ok, UnsupportedDialect{reason}, RenderFailed{message}}` (`:28-38`); `render_diagram` (`:120`) → `render_mermaid_with_validation` (`:123`; source cap rechecked `:401-410`); `render_diagram_blocks` (`:198`) → `render_diagram_blocks_bounded` (`:207-238`; called from `session.rs:9774` in `VaultSession::get_diagram_blocks`, `:9637`, a keyed single-flight cache); `DiagramBudgets` (`:177-194`); caps `MAX_RENDERED_DIAGRAMS_PER_NOTE` 2,000 (`:145`), `MAX_DIAGRAM_SOURCE_BYTES` 64 KiB (`:150`), `MAX_DIAGRAM_SVG_BYTES` 2 MiB (`:158`), `MAX_RETAINED_SVG_BYTES_PER_NOTE` 8 MiB (`:163`); `budget_degraded` (`:243-256`).
- **The thread (#1579).** `mermaid_renderer_render` (`:514-527`): `std::thread::Builder` named `slate-diagram-render` with a 256 MiB stack (`:507`, `:516-519`), `renderer.join()` **unbounded** (`:521`), `resume_unwind` on panic (`:525`). `try_render_mermaid` (`:460-499`): `catch_unwind` (`:471`), `RENDERER_POISONED` (`:390`) short-circuit (`:463-465`, `:481-483`), the panic message (`:494-496`). mermaid-rs-renderer's process-global `TEXT_MEASURER` mutex is held for the render (comments `:386-389`, `:456-459`); tests serialize on `RENDERER_TEST_LOCK` (`:633`).
- **Messages** are all composed in `diagram.rs` (`:216`, `:219`, `:224`, `:229`, `:416`, `:463-465`, `:481-483`, `:494-496`, `:520`, `:597-610`); `a11y.rs` has none for diagrams. Hosts show `RenderFailed.message` verbatim: Windows `ReadingDocumentBuilder.cs:903-906` (header "Diagram could not be rendered", reason = message; failure paragraph `:968-990` with the description element), mac `MermaidView.swift:46`, `:133-139`.
- **Measured** (the issue, mermaid-rs-renderer 0.3.1, release, Linux): 500 steps ≈ 1 s, 1,000 ≈ 3.6 s, 2,000 ≈ 7.6 s, a 64 KiB chain ≈ 17 s, a single-line chain ≈ 34 s; super-linear.
- **FFI.** `get_diagram_blocks` (`slate-uniffi/src/lib.rs:2344`), `DiagramRenderStatus` (`:7557-7575`), `DiagramBlock` (`:7578-7602`). No status or message change is needed for the hosts.
- **Tests** (`diagram.rs` `mod tests`): the budget facts `:761`, `:799`, `:865`; `:1066` `render_runs_on_its_own_stack_whatever_the_caller_has`; `:1085`, `:1105`, `:1134`; the poison facts `:1000-1045`. Windows: `ReadingDiagramTests.cs` (`:26`–`:382`); none drives a core `RenderFailed` status.

## Reference

XD3 (5 s / 20 s / 2). #1579's own pattern (a dedicated thread, structured degradation). `BENCHMARKS.md` for the render sample.

## Design

> **Delivered shape (2026-10-24).** As designed, with four changes from the two reviews and one owner call:
>
> - **A refused render has its own sentence** (owner, on #1580). Its form is `"busy"`: "Diagram not drawn: earlier diagrams are still rendering past their 5-second budget. The source follows." It is not the timeout sentence with a clause appended.
> - **A refusal is transient and not cached.** `get_diagram_blocks` hands it to the flight's waiters but skips the cache insert, so the next request renders again. A real timeout is cached like any failed render (item 6).
> - **The note's budget is a deadline.** Each render's wait is the per-diagram budget or what is left of the note's, whichever is shorter. A render cut by the note's deadline gets the `"note"` sentence, so no note holds a refresh past 20 s.
> - **The deadline is checked where a render would start,** after validation. A note's unsupported or empty blocks keep their own reasons past it.
>
> The test seam applies only to renders the setting thread asks for, so session tests elsewhere in the process render for real. mermaid-rs-renderer 0.3.1 locks its text measurer per measurement, not for a whole render: what the cap of two bounds is the cores and memory abandoned renders keep.

1. **Budgets.** `DiagramBudgets` gains `max_render_time_per_diagram: Duration` (5 s), `max_render_time_per_note: Duration` (20 s) and `max_abandoned_renders: usize` (2), as `pub const`s beside the byte caps, documented with the issue's measurements.
2. **A bounded wait.** `mermaid_renderer_render` returns through a channel instead of `join`: the thread sends its `Result` and the caller does `rx.recv_timeout(budget)`. On `Ok(result)` the behaviour is today's (including `resume_unwind` on a panic payload, which the thread sends as `Err(payload)`). On `Timeout`: increment `ABANDONED_RENDERS` (a `static AtomicUsize`), leave the thread running (it decrements when it finishes, through a guard dropped at the end of the closure, so a panic also releases), and return `RenderFailed { message: render_time_exceeded_message("diagram", 5) }`. Before spawning, if `ABANDONED_RENDERS >= max_abandoned_renders`, return the same message without spawning ("…; an earlier diagram is still rendering" appended). The `TEXT_MEASURER` consequence is documented: an abandoned render holds mermaid's mutex until it ends, so the next render waits on it and is itself budget-bounded — the cap of two bounds how long that can go on.
3. **A per-note budget.** `render_diagram_blocks_bounded` (`:207-238`) keeps an `Instant`; once the elapsed render time of the note exceeds `max_render_time_per_note`, every remaining block degrades with `render_time_exceeded_message("note", 20)` without spawning.
4. **One sentence, core-owned.** `pub fn render_time_exceeded_message(what: &str, seconds: u64) -> String` in `diagram.rs`: "Diagram not drawn: rendering took longer than {seconds} seconds. The source follows." for `"diagram"`, "Diagram not drawn: this note's diagrams used their {seconds}-second render budget. The source follows." for `"note"`, and a third form for the SVG preview XB-10b will pass (`"image"`: "Image not shown: decoding took longer than {seconds} seconds."). Exposed through uniffi as `render_time_exceeded_message(what: String, seconds: u64) -> String` (a free function beside the other free FFI helpers; regenerate both bindings; Swift compiles with nothing consuming it yet).
5. **A test seam.** `#[cfg(test)]` `RENDER_SEAM: Mutex<Option<Box<dyn Fn(&str) -> RenderOutcome + Send>>>` consulted by `mermaid_renderer_render` before spawning the real renderer, so the facts can make a render sleep (on the spawned thread) for a chosen time without a real slow diagram; a seam-set render still goes through the thread, the channel and the counters.
6. `BENCHMARKS.md`: a "diagram render budget" entry with a 500-step chain's time on this machine, which must stay under half the per-diagram budget; `get_diagram_blocks`' cache (`session.rs:9637`) is unchanged (a timed-out block is cached as failed like any failed render; a Refresh re-renders as today).
7. Not changed: `MAX_RENDERED_DIAGRAMS_PER_NOTE`, the byte caps, the style caps, poisoning, the hosts' failed-state rendering, `DiagramBlock`'s shape.

## Tests

`diagram.rs` `mod tests`, under `RENDERER_TEST_LOCK`, with the seam:
- `a_render_past_the_per_diagram_budget_degrades_with_the_budget_sentence` — seam sleeps 300 ms, budget overridden to 100 ms (a `#[cfg(test)]` override on `DiagramBudgets`): `RenderFailed` with the "diagram" sentence; the call returns in < 250 ms; the thread finishes later and `ABANDONED_RENDERS` returns to 0 (poll ≤ 1 s). Mutation: `recv` instead of `recv_timeout` → the call takes ≥ 300 ms.
- `a_render_within_the_budget_is_unchanged` — seam returns Ok fast: `Ok` status, SVG present.
- `a_note_past_its_render_budget_degrades_the_rest_without_rendering` — three blocks, per-note budget 150 ms, each seam render 100 ms: block 1 Ok, block 2 Ok or timed, block 3 the "note" sentence with the seam never called for it (count calls).
- `abandoned_renders_are_capped_and_new_renders_are_refused_while_the_cap_holds` — two seam renders parked on a barrier, both abandoned; a third returns the sentence immediately with the seam not called; release the barrier; the counter returns to 0 and a fourth renders Ok.
- `an_abandoned_render_that_panics_still_releases_its_slot` — seam panics after the timeout; counter back to 0.
- `render_time_exceeded_message_has_the_three_forms` — the three strings pinned (the Windows and mac failed states show them verbatim).
- Windows: `ReadingDiagramTests.ACoreRenderFailedBlockShowsTheHonestHeaderAndCoresReason` — a `DiagramBlock` with `RenderFailed { message: <the diagram sentence> }` renders the header "Diagram could not be rendered", the reason text in range, the source, and a `diagram` element whose Name is the description.
- Existing budget and poison facts unchanged.

## Docs and registers

- `BENCHMARKS.md` entry. `docs/plans/05_locked_architecture_decisions.md` is not edited (budgets are not a boundary).
- `crates/slate-uniffi/src/lib.rs` (the free function); both bindings regenerated (build products).
- Contract 38/39: nothing (no new announcement; the failed state's reason was already spoken as text in range).
- #1581: a comment that the sentence's `"image"` form exists for XB-10b.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb10a'
cargo test -p slate-core --lib -- diagram::tests          # new facts red first (the seam is new; write it with the first fact)
cargo test -p slate-core --no-fail-fast
cargo fmt --all -- --check; cargo clippy --all-targets --workspace -- -D warnings; cargo test --workspace; cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
./apps/slate-windows/generate-bindings.ps1 -Locked           # the new FFI function
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ReadingDiagramTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.Tests/ReadingDiagramTests.cs
# BENCHMARKS.md sample: a 500-step chain through render_diagram in release (cargo test --release -- --ignored <bench fact> or the existing bench harness)
```

CI lanes: `rust.yml`, `windows.yml`, `swift-tests.yml` (compile). AT: none.

## Acceptance

- [ ] Per-diagram, per-note and abandoned-render bounds in `DiagramBudgets`; a timed-out render degrades with the core sentence; the cap refuses new renders; counters release on finish and on panic.
- [ ] `render_time_exceeded_message` exists in core and over the FFI with its three forms.
- [ ] Seven core facts green (red first, mutations recorded); the Windows display fact green; `BENCHMARKS.md` entry.
- [ ] `Closes #1580`. Not delivered: nothing.

## Brief

Read `crates/slate-core/src/diagram.rs:20-60`, `:115-260`, `:380-530`, `:625-640`, `:755-800`, `:1060-1085`; `crates/slate-core/src/session.rs:9630-9780`; `crates/slate-uniffi/src/lib.rs:2340-2350`, `:7555-7605` and the free-function area; `apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs:845-991`; `tests/SlateWindows.Tests/ReadingDiagramTests.cs:20-80`, `:328-380`; `BENCHMARKS.md`. Build per XD3: `DiagramBudgets` + `max_render_time_per_diagram` (5 s), `max_render_time_per_note` (20 s), `max_abandoned_renders` (2); `mermaid_renderer_render` waits on a channel with `recv_timeout`, abandons on timeout (counter + drop-guard release, thread left running), refuses at the cap; `render_diagram_blocks_bounded` enforces the note budget; `render_time_exceeded_message(what, seconds)` with the "diagram", "note" and "image" forms, exposed over uniffi; a `#[cfg(test)]` render seam that still goes through the thread and counters; `BENCHMARKS.md` sample. Facts (names above) under `RENDERER_TEST_LOCK`, each red first with its mutation; Windows `ReadingDiagramTests.ACoreRenderFailedBlockShowsTheHonestHeaderAndCoresReason`. Regenerate bindings; keep Swift compiling. Done when the Rust gates are clean, `rust.yml`/`windows.yml`/`swift-tests.yml` green, the bench sample recorded. `Closes #1580`. Commit: `fix(core): a diagram render has a time budget per diagram and per note, and one past it degrades with core's sentence`.
