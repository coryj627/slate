# XB-8 — A primed function's argument is spoken as its argument

Issues: #1585 (closes here, or `Refs` with a mac follow-up, per XD4). Size S–M. Lanes: rust, `windows.yml` (`rust-tests`), `swift-tests.yml` (the golden fixture is read by nothing Swift, but `crates/slate-core/**` is in its paths; compile only). AT: NVDA spot check here; VoiceOver pass by the owner (XD4). Wave: X4, slot 1. Depends on: XD4; XB-7 merged (one golden diff at a time). Parallel with: XB-9, XB-10a. Decision: XD4.

## Goal

`v'(x)` is heard as "v prime of x", and `f'(g(x))` as "f prime of g of x", under every speech setting, because core gives MathCAT the MathML MathJax would (`<msup><mi>v</mi><mo>′</mo></msup>`) instead of pulldown-latex's loose `<mi>v</mi><mi>′</mi>`. The golden corpus records the new speech and braille; what VoiceOver and NVDA speak is checked, not assumed.

## Current state on main

- **Where the prime comes from.** pulldown-latex 0.8.0 maps `'` to `ordinary('′')` (`pulldown-latex-0.8.0/src/parser/primitives.rs:111`, `fn ordinary` `:2273-2278` → `Event::Content(Content::Ordinary { content, stretchy: false })`); `\prime` at `:696`, `\dprime` and friends `:697-702`. Today's golden shows `f'` as `<mi>f</mi><mi>′</mi>` followed by a separate `(` (`tests/fixtures/math_golden/expected.txt:34-44`, entry `#4`), spoken "f prime, g of x times g prime times x".
- **The rewrite seam.** `crates/slate-core/src/math.rs:670-703` `latex_to_mathml`: refuses `UNRENDERABLE_COMMANDS` (`:708`, `uses_unrenderable_command` `:713-737`), collects the whole event stream (`:677-681`), pre-escapes function names (`:682-688`), maps the stream once (`:690-697`: the only rule, `Event::Content(Content::Function(_))` → the escaped name; `other => other`), and calls pulldown-latex's `push_mathml` (`:699`, imported `:672`), then `escape_stray_markup` (`:700`, `:749-770`). This is the path #1557 added.
- **The golden.** `crates/slate-core/tests/math_golden.rs:277-307` `math_speech_and_braille_match_the_golden_corpus`; `UPDATE = "SLATE_MATH_GOLDEN_UPDATE"` (`:28`), update branch `:281`, CRLF `:289`; corpus = the demo-vault notes, the field-pass note, `extra-expressions.txt` (`corpus()` `:81`, `render_corpus()` `:155-218`, all twelve settings). Regen: `SLATE_MATH_GOLDEN_UPDATE=1 cargo test -p slate-core --test math_golden` (README `:33`).
- **The affected lines.** `demo-vault/learning/Calculus notes — derivatives.md:9` (`f'(g(x)) \cdot g'(x)`, golden `#4` `expected.txt:34-44`), `:31` (`u'(x) \cdot v(x) + u(x) \cdot v'(x)`, `#18` `:188-198`), `:15` (inline `$f'$`, `$g'$`, `#7`/`#8` `:67-88`), `:23` (`f'(g(h(x))) \cdot g'(h(x)) \cdot h'(x)`), `:41` (`u' v w + u v' w + u v w'`). The issue says Nemeth keeps the parentheses today (`⠧⠄⠷⠭⠾`); the rewrite may change braille too — the golden diff shows it, and any braille change is added to #1560's transcriber list.
- **Speech surfaces.** Both hosts speak `MathBlock.speech` (FFI `slate-uniffi/src/lib.rs:7083-7092`): Windows `ReadingMathElement`'s Name (`Reading/ReadingMathElement.cs:28`) and the Ctrl+Alt+M landing; mac `MathView.swift`. No host code changes.

## Reference

MathJax's output for `v'(x)` (`<msup><mi>v</mi><mo>′</mo></msup>`); MathCAT's function-application rules (a `<mi>` base followed by `<mo>(</mo>` is "of"; the issue's evidence that `<mi>′</mi>` breaks it). `docs/plans/39_at_navigation_contracts.md` (braille correctness is owner-deferred).

## Design

0. **Probe before committing (step 0, in the PR body).** Through `mathml_to_speech_and_braille` (`math.rs:788-811`) with the demo's settings, render by hand `<msup><mi>v</mi><mo>′</mo></msup><mo>(</mo><mi>x</mi><mo>)</mo>` and `<msup><mi>f</mi><mo>′</mo></msup><mo>(</mo><mi>g</mi><mo>(</mo><mi>x</mi><mo>)</mo><mo>)</mo>` under all twelve settings and record the speech. If MathCAT says "of" for both, continue. If it still says "times", stop: comment the probe on #1585 and on the upstream MathCAT report (the fix is then theirs), and this chunk closes nothing (`Refs #1585`), with the golden untouched.
1. **The rewrite.** In `latex_to_mathml`, a pass over the collected `events` before the escape map: whenever an `Event::Content(Content::Ordinary { content, .. })` whose `content` is `′`, `″`, `‴` or `⁗` (or a run of `′`) directly follows a base event (an `Ordinary` identifier, a `Function`, or the `End` of a group), replace the pair with the superscript shape pulldown-latex itself emits for `v^{\prime}`: read the events `Parser::new("v^{\\prime}(x)")` yields (the builder prints them in a scratch test) and emit exactly that sequence (`Event::Script { ty: Superscript, position: Right }` and its base/script grouping, whatever the 0.8 event model is), so that `v'(x)` and `v^{\prime}(x)` produce byte-identical MathML. Consecutive primes become one superscript whose content is the primes joined. A prime that does not follow a base (a bare `'`) is left as it is. No change to `push_mathml` or the escapers.
2. **The golden.** Regenerate; read the diff: only the primed entries change (list them in the PR); if any braille cell changed, note each on #1560. 
3. **Upstream.** File the pulldown-latex report (prime as `<msup>`, the MathJax comparison) and link it in the PR and on #1585.
4. **Field passes.** NVDA spot here on the two calculus lines (ClearSpeak Medium and SimpleSpeak Medium: Ctrl+Alt+M landings and Enter re-reads), launched per plan 44 §4 rule 4 (back up `%LOCALAPPDATA%\Slate` first; WB-1's launcher does not exist yet). VoiceOver per XD4: the owner on the PR branch; under XD4's fallback the PR merges `Refs #1585` with `not delivered: VoiceOver pass` and a mac follow-up issue is filed ("mac: VoiceOver pass for the primed-function speech change (#1585)") that closes #1585.

## Tests

- `math::tests::a_prime_after_an_identifier_is_a_superscript_in_mathml` — `latex_to_mathml("v'(x)") == latex_to_mathml("v^{\\prime}(x)")` and contains `<msup>`; mutation: skip the pass → inequality.
- `math::tests::consecutive_primes_fold_into_one_superscript` — `f''(x)` equals `f^{\prime\prime}(x)` (or `f^{″}`, whichever pulldown-latex emits for the doubled form; pin the one chosen).
- `math::tests::a_bare_prime_is_left_alone` — `'x` renders the same as before (no panic, no `<msup>`).
- `math::tests::a_primed_function_is_spoken_as_a_function` — `render_math` of `v'(x)` under ClearSpeak Medium contains "of x" and not "times x" (the step-0 result pinned; `#[ignore]`-free because MathCAT is in-process).
- The golden test with the regenerated `expected.txt`.

## Docs and registers

- `crates/slate-core/tests/fixtures/math_golden/expected.txt` regenerated; `README.md` unchanged.
- #1560: a comment listing any braille cells the diff changed.
- `docs/plans/18_windows_port/w_c_matrix.md:22` (Math blocks): no change (the contract describes the shape, not the text).
- PR body: the NVDA phrases heard; the VoiceOver phrases (owner) or `not delivered`.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb8'
cargo test -p slate-core --lib -- math::tests::a_prime           # the new facts, red first
cargo test -p slate-core --lib -- math::tests
$env:SLATE_MATH_GOLDEN_UPDATE='1'; cargo test -p slate-core --test math_golden; Remove-Item Env:SLATE_MATH_GOLDEN_UPDATE
git diff --stat crates/slate-core/tests/fixtures/math_golden/expected.txt; git diff crates/slate-core/tests/fixtures/math_golden/expected.txt | Select-String '^[-+]' | Select-String -NotMatch '^(---|\+\+\+)' | Measure-Object -Line
cargo test -p slate-core --test math_golden
cargo fmt --all -- --check; cargo clippy --all-targets --workspace -- -D warnings; cargo test --workspace --no-fail-fast; cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
# NVDA spot: Release app with regenerated bindings, demo vault copy, the calculus note in reading mode, Ctrl+Alt+M to #4 and #18 under ClearSpeak Medium and SimpleSpeak Medium; nvda.log recipe (plan 44 §4 rule 4)
```

CI lanes: `rust.yml`, `windows.yml`, `swift-tests.yml` (compile). AT: NVDA spot (recorded phrases), VoiceOver pass (owner, XD4).

## Acceptance

- [ ] Step-0 probe recorded in the PR; the rewrite makes `v'(x)` and `v^{\prime}(x)` identical MathML.
- [ ] The four facts green, mutations recorded; the golden regenerated with only primed entries changed, braille changes noted on #1560.
- [ ] Upstream pulldown-latex report linked.
- [ ] NVDA phrases in the PR; VoiceOver phrases or `not delivered` per XD4.
- [ ] `Closes #1585` (or `Refs #1585` + the mac follow-up). Not delivered: per XD4.

## Brief

Read `crates/slate-core/src/math.rs:640-790`, `:874-900`; `crates/slate-core/tests/math_golden.rs:1-60`, `:150-220`, `:270-307`; `tests/fixtures/math_golden/README.md`, `expected.txt:30-45`, `:185-200`; pulldown-latex 0.8.0's `event.rs` (the `Event`, `Content`, `ScriptType`, `ScriptPosition` shapes) and `parser/primitives.rs:105-115`, `:690-705`, `:2270-2280` in the cargo registry. Step 0: render the `<msup>` MathML by hand through `mathml_to_speech_and_braille` under all twelve settings; if MathCAT still says "times", stop and comment (no golden change, `Refs #1585`). Build: a pre-pass in `latex_to_mathml` over the collected events: a `′`-family `Ordinary` directly after a base (identifier, function, or group end) becomes the exact event sequence `Parser::new("v^{\\prime}(x)")` emits (print it in a scratch test, then emit it), consecutive primes folded, a bare prime untouched; then regenerate the golden (`SLATE_MATH_GOLDEN_UPDATE=1 cargo test -p slate-core --test math_golden`) and list every changed entry in the PR, braille changes on #1560. Facts: `a_prime_after_an_identifier_is_a_superscript_in_mathml`, `consecutive_primes_fold_into_one_superscript`, `a_bare_prime_is_left_alone`, `a_primed_function_is_spoken_as_a_function` ("of x", not "times x"). File the pulldown-latex report; NVDA spot on the two calculus lines (back up `%LOCALAPPDATA%\Slate`); the owner's VoiceOver pass on the branch, else `Refs #1585` + a mac follow-up issue. Done when the Rust gates are clean, `rust.yml`/`windows.yml`/`swift-tests.yml` green, the phrases recorded. Commit: `fix(core): a prime after a function name is a superscript in mathml, so mathcat speaks the function's argument`.
