# XB-7 — A panic after the first math render prints its message, and malformed-code highlighting is pinned

Issues: #1583 #1576. Size S. Lanes: rust (and `windows.yml`'s `rust-tests`, which runs `cargo test -p slate-core`). AT: —. Wave: X3, slot 1. Depends on: XD5. Parallel with: XB-11, XB-9 step 0. Decision: XD5 (decided 2026-10-24: "pin snapshots"). Interlock: XB-8 edits `math.rs:670-703`; this chunk edits `math.rs:507-537` (the worker loop). XB-7 lands first; one golden diff at a time.

## Goal

Once MathCAT initializes, every later panic in the process keeps printing `panicked at …` to stderr, in tests and in the app, because `slate-core` chains MathCAT's hook to the hook that was there before. And the token classes tree-sitter 0.27 derives for malformed code are pinned in a golden, so the next grammar bump shows its effect as a diff instead of silently changing what a screen reader hears for a broken snippet.

## Current state on main

- **#1583.** `crates/slate-core/src/math.rs:492-501` `worker_handle()` spawns the single `slate-mathcat` thread (`OnceLock`, `:489-490`) running `mathcat_worker_loop(rx)` (`:507-537`): `libmathcat::set_rules_dir("Rules")` at `:513` (Ok → `MATHCAT_INITIALIZED` `:515`; Err → `log::warn!` `:522`), then the request loop (`:533-536`). `ensure_mathcat_initialized_on_this_thread` (`:583-590`) calls `set_rules_dir` again on other threads (`:587`). MathCAT 0.7.7's `init_panic_handler` (`mathcat-0.7.7/src/interface.rs:62-69`, called from `set_rules_dir` `:146-148`) runs once, `take_hook`s the current hook, installs its own that records into a thread-local, and calls the previous hook only `if cfg!(test)` (false in every Slate build). No `set_hook`/`take_hook` exists in `crates/`. The golden test already works around the silence: `crates/slate-core/tests/math_golden.rs:53-62` `fail()` does `eprintln!` then `panic!` with an `#[expect(clippy::disallowed_macros)]` and a doc naming #1583. Panics that cross the FFI are unaffected (uniffi reads the payload). The diagram renderer's `catch_unwind` (`diagram.rs:470-499`) and the watcher's (`session/watch.rs:765`) catch their own; their messages are what the app loses.
- **#1576.** `crates/slate-core/src/code.rs:281-301` `grammar_for_tag` has sixteen arms (rust, swift, python, javascript, typescript, tsx, markdown, yaml, json, bash, sql, html, css, go, c, cpp); `highlight_code` (`:227-277`, "never panics" `:224`) walks the tree (`walk_tree` `:332`, `classify_node_kind` `:419`, per-language `classify_*` `:441-682`). The only malformed-input test is `no_panic_on_malformed_source` (`:825-834`, source `"fn ("`, language rust, no assertion). Per-grammar tests `:865-1084` use well-formed snippets; none for tsx. No `insta`/`expect_test`; the repo's golden pattern is `tests/math_golden.rs` (`UPDATE` env `:28`, update branch `:281`, CRLF normalization `:289`, fixtures under `tests/fixtures/math_golden/`). Highlight spans are also pinned cross-host in `tests/fixtures/parity_golden/code.md.json` (from `fixtures/markdown/code.md`, one rust fence) by `ParityHarnessCensus` and the Swift twin — well-formed only. The issue's probe (20 malformed snippets × 16 entries, 8 changed: JS/TS/TSX `shorthand_property_identifier_pattern` → `shorthand_property_identifier`; C `type_identifier` ↔ `identifier`) was not committed.

## Reference

The issue's two fix options for #1583 (chain in `slate-core`; upstream). `tests/math_golden.rs` as the golden template. `docs/plans/05_locked_architecture_decisions.md` §6.4 (the locked language list).

## Design

1. **Chain the hook, once, on the worker.** In `mathcat_worker_loop` (`:507-537`), before `set_rules_dir`: `let previous = std::panic::take_hook();` (this installs the default hook for the instant of init); after a successful `set_rules_dir` (`:513-515`): `let mathcat = std::panic::take_hook(); std::panic::set_hook(Box::new(move |info| { mathcat(info); previous(info); }));`. On the Err arm (`:522`) restore `previous` with `set_hook(previous)`. Because `init_panic_handler` is a `Once`, the second `set_rules_dir` on other threads (`:587`) installs nothing, so the chain is installed exactly once, from the one place that first initializes MathCAT in a process. Document in a comment why `previous` is taken before init (MathCAT's `take_hook` would otherwise capture ours and we would capture MathCAT's twice). `catch_unwind` sites are unchanged: a caught panic still runs the hook, so its message prints again.
2. **Tidy the workaround.** `tests/math_golden.rs:53-62`: keep `fail()` (it is a clearer failure) but drop the #1583 sentence from its doc and the `eprintln!` if the `expect` lint then fires; a one-line comment says the chain in `math.rs` now prints the message.
3. **Highlight golden (XD5).** New `crates/slate-core/tests/highlight_golden.rs` modelled on `math_golden.rs`: an input list `tests/fixtures/highlight_golden/malformed.txt` (one block per snippet: a `=== <tag> #<n>` header line then the snippet; at least one malformed snippet per `grammar_for_tag` arm, sixteen, plus the eight the issue names reproduced: an object-pattern shorthand with a missing brace for js, ts and tsx; a C declaration with a missing semicolon or type) and `expected.txt` holding, per snippet, one line per token: `class start_byte..end_byte` from `highlight_code`'s public result (whatever the public token type exposes: read `code.rs:181-227` for the type and pick the fields a screen reader's class depends on). `SLATE_HIGHLIGHT_GOLDEN_UPDATE=1` regenerates; CRLF normalized as `math_golden.rs:289` does; a README in the fixture dir with the regen command and the rule "a diff here is a behaviour change for malformed code; say so in the PR".
4. Not changed: `grammar_for_tag`, any classifier, the parity golden, `no_panic_on_malformed_source` (it keeps its role).

## Tests

- `math::tests::a_panic_after_mathcat_initializes_still_reaches_the_previous_hook` (in `math.rs`'s `mod tests`, `:874`): install a probe hook (`set_hook` writing the panic message into an `Arc<Mutex<Vec<String>>>`) **before** any math render in the test process — since other math tests may already have initialized the worker, the fact must work either way: it takes the current hook with `take_hook`, wraps it (`probe` then the taken hook), sets it, renders one block (`render_math`), then `catch_unwind`s a panic with a unique message on a new thread and asserts the probe saw it. Under `cargo test` (one process) this proves the chain survives init; under nextest it proves it in its own process. Mutation: drop the `previous(info)` call → the probe never sees the message when the render was the first init in the process (run the fact alone, `--exact`, to hold that ordering).
- `highlight_golden::malformed_code_tokens_match_the_golden` — fails until `expected.txt` exists; mutation: change one classifier arm (`code.rs:441-682`) → a diff naming the snippet.
- Existing: `no_panic_on_malformed_source` (`:825`), the per-grammar tests, `math_golden`.

## Docs and registers

- `crates/slate-core/tests/fixtures/highlight_golden/README.md` (new) and the fixture files.
- `BENCHMARKS.md`: nothing. Contract registers: nothing (no spoken change).
- `docs/plans/05_locked_architecture_decisions.md` §6.4 is cited, not edited (the doc at `code.rs:278-280` says fifteen entries where the match has sixteen arms because TS and TSX are separate; leave it).

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb7'
cargo test -p slate-core --lib -- math::tests::a_panic_after_mathcat_initializes_still_reaches_the_previous_hook --exact   # red first, then green
cargo test -p slate-core --lib -- math::tests                       # the whole module, in one process
$env:SLATE_HIGHLIGHT_GOLDEN_UPDATE='1'; cargo test -p slate-core --test highlight_golden; Remove-Item Env:SLATE_HIGHLIGHT_GOLDEN_UPDATE
cargo test -p slate-core --test highlight_golden                     # green; then mutate a classifier arm and watch it diff
cargo test -p slate-core --test math_golden
cargo fmt --all -- --check
cargo clippy --all-targets --workspace -- -D warnings
cargo test --workspace --no-fail-fast
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh   # new .rs files: git add, then python3 scripts/apply-license-header.py
```

CI lanes: `rust.yml`, `windows.yml` (`rust-tests`). AT: none.

## Acceptance

- [ ] After MathCAT initializes, a panic on any thread reaches the previously installed hook (fact green; mutation red).
- [ ] The highlight golden exists with ≥16 malformed snippets and the eight changed cases; the test is green and diffs under a classifier mutation.
- [ ] The golden test's #1583 workaround note is updated.
- [ ] `Closes #1583`, `Closes #1576` (under XD5 "pin"). Not delivered: nothing.

## Brief

Read `crates/slate-core/src/math.rs:440-540`, `:583-590`, `:874-900`; `crates/slate-core/tests/math_golden.rs:1-70`, `:270-307`; `crates/slate-core/src/code.rs:175-300`, `:420-445`, `:820-840`; the MathCAT source `interface.rs:55-75` and `:140-150` in the cargo registry (`mathcat-0.7.7`). Build: (1) in `mathcat_worker_loop`, `let previous = std::panic::take_hook();` before `set_rules_dir`; on Ok, `let mathcat = take_hook(); set_hook(Box::new(move |info| { mathcat(info); previous(info); }))`; on Err, `set_hook(previous)`; a comment on the ordering and on MathCAT's `Once`; (2) `math_golden.rs:53-62` doc updated; (3) under XD5, `tests/highlight_golden.rs` + `tests/fixtures/highlight_golden/{README.md,malformed.txt,expected.txt}` modelled on `math_golden.rs` (`SLATE_HIGHLIGHT_GOLDEN_UPDATE`, CRLF normalization), ≥1 malformed snippet per `grammar_for_tag` arm and the issue's eight cases, output `class start..end` per token. Facts: `math::tests::a_panic_after_mathcat_initializes_still_reaches_the_previous_hook` (probe hook wrapped around the current one, one render, a caught panic on another thread, the probe saw it; mutation: drop `previous(info)`), `highlight_golden::malformed_code_tokens_match_the_golden` (mutation: one classifier arm). New files: `git add`, then `python3 scripts/apply-license-header.py`. Done when the facts are green, the mutations recorded, the five Rust gates clean, `rust.yml` and `windows.yml` green; `Closes #1583`, `Closes #1576`. Commit: `fix(core): mathcat's panic hook is chained to the previous one, and malformed-code highlighting is pinned in a golden`.
