# XB-3 — The Windows Rust lane runs clippy on the code it builds, and the dir_tree censuses pass on Windows

Issues: #1570 #1571. Size S. Lanes: windows (`rust-tests`, with its new clippy step), rust (`rust.yml`'s own clippy and the census facts on Linux). AT: —. Wave: X1, slot 2. Depends on: nothing. Parallel with: XB-1, XB-4a. Decision: none. Interlock: plan 44's WB-12 edits `vault/watch.rs` (`translate`), WB-24 and WB-28 edit `vault/fs.rs`, `windows_entry.rs` and `session.rs`; this chunk changes only `cfg` attributes and one borrow in those files and lands first, so each of them rebases over a few lines.

## Goal

`cargo clippy --all-targets --workspace -- -D warnings` is clean on Windows, by gating the four Windows-dead items to the platforms that build them (never a blanket `allow`), and the Windows lane runs that command so `cfg(windows)` code is linted from now on. The five `dir_tree` censuses generate only names Windows accepts, without changing what Linux and macOS generate, and pass on this machine.

## Current state on main

- **#1570, the four findings** (`cargo clippy --all-targets --workspace -- -D warnings` on Windows, Rust 1.99.1):
  1. `crates/slate-core/src/session/tests/watch.rs:18-22` imports `WATCH_REARM_ENABLED` (`:19`), defined at `session/watch.rs:108` (`pub const WATCH_REARM_ENABLED: bool = false;`, the #1513 switch; re-exported `session.rs:47`, `lib.rs:184`; FFI `slate-uniffi/src/lib.rs:6858`). Its only code uses in the test file are inside two `#[cfg(unix)]` facts (`:594` in `a_legitimate_root_rename_rearms_at_the_new_path`, `#[cfg(unix)]` at `:591`; `:663` in `a_legitimate_root_rename_ends_the_watch_while_the_rearm_is_off`, `:660`). On Windows the import is unused.
  2. `crates/slate-core/src/vault/watch.rs:90-102` `enum Relocation { Intact, Moved(PathBuf), Swapped, Lost }`; the only constructor of `Moved` is `:205` inside the `Self::Held` arm of `relocation()` (`:189`), gated `#[cfg(all(any(target_os = "macos", target_os = "linux"), target_pointer_width = "64"))]` (`:191-194`), the same gate as the `Held` variant (`:115-118`) and `fs_root::ANCHORED` (`fs_root.rs:49`). Consumers `session/watch.rs:623`, `:735`, `:736` match `Moved` without a gate; `rearm` (`:236-256`) already carries `#[allow(unreachable_patterns)]` (`:247`).
  3. `crates/slate-core/src/session.rs:1151-1152` `pub(crate) struct OpenSwapSeamGuard;` (`#[cfg(test)]`), its `Drop` (`:1154-1159`), and `install_open_swap_seam` (`:1163-1169`, `#[cfg(test)]`); every caller is in `session/tests/descriptor_interior.rs` (`:85`, `:152`, `:196`), a file that is `#![cfg(unix)]` (`:16`). `OpenSwapPoint` (`:1124-1139`), `OPEN_SWAP_SEAM` (`:1144-1148`) and `fire_open_swap_seam` (`:1171-1172`, fired at `:2633`, `:2776`, `:3210`, `:3258`) are used on every platform and stay.
  4. `crates/slate-core/src/vault/fs.rs:318-337` in `pin_mutation_target`: `#[cfg(unix)] let root = self.pinned_root()?;` (`:326-327`, a `PinnedRoot` by value) / `#[cfg(windows)] let root = self.root.as_path();` (`:328-329`, already `&Path`), then `PinnedMutationTarget::open(&root, …)` (`:330-331`). The Unix `open` takes `&PinnedRoot` (`:888-898`); the Windows `open` takes `&Path` (`:1055-1064`), so `&root` is `&&Path` on Windows: `clippy::needless_borrow`.
- **No lane runs clippy on Windows.** `rust.yml:146-150` runs `cargo clippy --workspace --all-targets --locked -- -D warnings` on a Linux ARM64 runner (job `lint`, `:65-66`). `windows.yml`'s `rust-tests` job (`:248-410`: route-dependent `runs-on` `:252`, checkout `:260`, arch assert `:263`, Namespace cache `:279` (skipped on the home pool `:280`), cache attestation `:291`, ARM64 `cargo check` `:340-344`, `cargo test -p slate-core --profile ci --locked -- --skip census_ …` `:355-360`, `cargo test -p slate-cli --profile ci --locked` `:365-366`, footprint `:368`) has no clippy step.
- **#1571.** `crates/slate-core/src/session/tests/dir_tree.rs` (included at `session.rs:22202-22203`, no OS cfg; header `#![allow(clippy::too_many_lines)]` `:13`; the doc at `:7-8` still names the pre-chunking `census_dir_tree_matches_filesystem`). Names come from `NAME_ATOMS` (`:571-582`: `alpha`, `Beta`, `gamma note`, `é` precomposed, `é` decomposed, `star*`, `br[ack]`, `q?mark`, `Zed`, `mix ED`), one atom plus a second with probability 1/3 (`:622-625`), deduped per level on NFC-lowercase (`:626-633`, `continue` on a collision), directories with no suffix, files `{rel}.md` (`:647`), written with `create_dir_all`/`write` `.unwrap()` (`:642`, `:648`). `star*` and `q?mark` contain `*` and `?`, which NTFS refuses (`Os { code: 123, kind: InvalidFilename }`); no atom can end in `.` or space or spell a reserved device name. The facts: `census_dir_tree_matches_filesystem_seeds_000_125` … `_375_500` from `dir_tree_census_chunks!` (`:760-787`, seeds `:740`, body `dir_tree_matches_filesystem_for_seeds` `:789-814`, `generate_random_vault(tmp, rng, 6, 12)` `:793`) and `census_dir_ids_stable_across_rescans` (`:890-952`, 120 seeds, `(5, 8)`). None is cfg-gated or ignored; CI skips them on Windows by name (`windows.yml:358` `--skip census_`, `windows-ci-pilot.yml:133`); `rust.yml:256` runs them on Linux and `nightly.yml:145` too. The file already splits one Windows case the same way: `glob_hazard_names_do_not_break_range_scan` (`:480-536`) writes `star*dir` and `q?dir` only `#[cfg(unix)]` (`:491-494`, comment `:485`).

## Reference

CONTRIBUTING.md:76-98 (the Rust gates); `rust.yml:146-150` (the exact clippy invocation to mirror); the `glob_hazard_names_do_not_break_range_scan` precedent for host-valid names.

## Design

1. **Import.** `session/tests/watch.rs:19` drops `WATCH_REARM_ENABLED` from the shared `use`; a separate `#[cfg(unix)] use crate::session::watch::WATCH_REARM_ENABLED;` (or the path the file's other imports use) sits beside the two facts. The switch itself (`session/watch.rs:108`) is untouched (#1513's).
2. **`Relocation::Moved`.** The variant (`vault/watch.rs:96`) gets the same `#[cfg(all(any(target_os = "macos", target_os = "linux"), target_pointer_width = "64"))]` as the `Held` variant (`:115-118`); the two consuming arms at `session/watch.rs:623` and `:735` get the same attribute, and `:736` becomes `Relocation::Swapped | Relocation::Lost => false` with a `#[cfg(...)] Relocation::Moved(_) => false` arm above it (the `rearm` `allow(unreachable_patterns)` at `:247` is then re-examined: keep it only if a fact proves it still needed, else drop it in the same commit). `Lost` stays ungated (`session/watch.rs:654`, `:671` construct it on every platform).
3. **The seam guard.** `session.rs:1150-1169`: `OpenSwapSeamGuard`, its `Drop` and `install_open_swap_seam` become `#[cfg(all(test, unix))]` with a one-line comment pointing at `descriptor_interior.rs:16`. `OpenSwapPoint`, the thread-local and `fire_open_swap_seam` keep `#[cfg(test)]`.
4. **`fs.rs:326-331`.** Make both bindings a reference: `#[cfg(unix)] let root = self.pinned_root()?; #[cfg(unix)] let root = &root;` and `#[cfg(windows)] let root = self.root.as_path();` then `PinnedMutationTarget::open(root, …)`. No behaviour change.
5. **The lane.** `windows.yml`, `rust-tests` job, a step between the ARM64 check (`:340-344`) and the core suite (`:355`):
   ```yaml
         - name: clippy (windows x64, -D warnings)
           run: cargo clippy --workspace --all-targets --locked -- -D warnings
   ```
   (the `rust.yml:150` invocation verbatim, default profile as there). The cache attestation and footprint steps need no change. `Workflows.Tests.ps1` gains a fact that the `rust-tests` job contains that line (`Get-JobBlock $windows 'rust-tests'`, `Get-JobLine`).
6. **The censuses.** In `generate_into` (`dir_tree.rs:619-633`), after the two atoms are drawn and before the collision key: `if !host_accepts(&name) { continue; }` where `fn host_accepts(name: &str) -> bool` is `cfg!(windows)`-aware (`!name.contains(['*', '?', '<', '>', ':', '"', '|', '\\'])` on Windows, `true` elsewhere) with a doc comment naming #1571 and the `glob_hazard_names_do_not_break_range_scan` precedent. The draws stay in the same order, so Linux and macOS generate exactly what they generate today (the `continue` consumes the same RNG calls as a collision does). Fix the stale doc name at `:7-8`. `windows.yml:358`'s `--skip census_` stays (runtime, not correctness); the PR body records the local Windows run of the five facts.

## Tests

- No new product facts. The five censuses are the #1571 witnesses: red on Windows before step 6 (`Os { code: 123 }`), green after; unchanged on Linux (`rust.yml`'s nextest `test(census_)` selection `:256` must still pass with identical seed sets — the chunk adds a fact `census_name_generation_is_identical_off_windows` only if a cheap one exists: on `cfg(not(windows))`, a fixed seed's generated set hashed and pinned; skip it if the hash would need maintenance on every atom change).
- A Windows-only fact `host_accepts_refuses_the_two_glob_atoms_on_windows` (`#[cfg(windows)]`) pins `host_accepts("star*") == false`, `host_accepts("q?mark") == false`, `host_accepts("br[ack]") == true`.
- Clippy itself is the #1570 witness: red on `main` for the four findings, green after steps 1–4, and from then on red on the lane for any new one.
- `Workflows.Tests.ps1`: `It 'the windows rust lane runs clippy with warnings as errors'`.

## Docs and registers

- `windows.yml` (one step). `ci/windows-runner/tests/Workflows.Tests.ps1` (one fact).
- `CONTRIBUTING.md:76-98`: one sentence that the Windows lane now runs clippy too (no command change).
- Nothing in contracts or the parity matrix.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb3'
cargo clippy --all-targets --workspace -- -D warnings           # red on main (4 findings), green after
cargo test -p slate-core --profile ci --lib -- session::tests::dir_tree::   # 5 censuses red on main, green after; ~minutes
cargo test -p slate-core --lib -- host_accepts
cargo fmt --all -- --check
cargo test --workspace
cargo bench --workspace --no-run
& "$env:ProgramFiles\Git\bin\bash.exe" scripts/check-license-headers.sh
Import-Module Pester -RequiredVersion 5.9.1 -Force; ./ci/windows-runner/tests/Invoke-Tests.ps1
```

CI lanes: `windows.yml` (the new clippy step must run green on the PR; quote its step in the body), `rust.yml` (clippy on Linux still clean; the censuses still pass with the same seeds), `windows-runner-tests.yml` (the Pester fact). AT: none.

## Acceptance

- [ ] Clippy clean on Windows and Linux; the four fixes are `cfg` attributes and one borrow, no `allow` added.
- [ ] The `rust-tests` job runs clippy with `-D warnings`; the Pester fact pins it; the PR's run shows the step green.
- [ ] The five `dir_tree` censuses pass on this machine (run recorded); Linux generation unchanged (`rust.yml` green).
- [ ] `Closes #1570`, `Closes #1571`. Not delivered: nothing.

## Brief

Read `crates/slate-core/src/session/tests/watch.rs:15-25`, `:586-600`, `:654-668`; `crates/slate-core/src/vault/watch.rs:88-120`, `:186-230`, `:236-256`; `crates/slate-core/src/session/watch.rs:615-630`, `:730-740`; `crates/slate-core/src/session.rs:1120-1175`, `:2630-2636`, `:3205-3212`; `crates/slate-core/src/vault/fs.rs:310-347`, `:885-900`, `:1052-1066`; `crates/slate-core/src/session/tests/dir_tree.rs:1-15`, `:480-536`, `:560-660`, `:740-815`, `:890-952`; `.github/workflows/windows.yml:248-372`, `.github/workflows/rust.yml:140-152`; `ci/windows-runner/tests/Workflows.Tests.ps1:15-45`, `:94-113`. Build: (1) `#[cfg(unix)] use … WATCH_REARM_ENABLED;` as its own import, out of the shared `use` at `:19`; (2) `Relocation::Moved` and its two consuming arms (`session/watch.rs:623`, `:735`) under `#[cfg(all(any(target_os = "macos", target_os = "linux"), target_pointer_width = "64"))]`, `:736` split so `Moved(_)` is gated; re-examine `rearm`'s `allow(unreachable_patterns)` (`:247`); (3) `OpenSwapSeamGuard`, its `Drop` and `install_open_swap_seam` under `#[cfg(all(test, unix))]`; (4) `fs.rs:326-331`: bind `root` as a reference on both platforms and pass `root`; (5) `windows.yml` `rust-tests`: `- name: clippy (windows x64, -D warnings)` / `run: cargo clippy --workspace --all-targets --locked -- -D warnings` after the ARM64 check; a `Workflows.Tests.ps1` fact pins the line; (6) `dir_tree.rs`: `fn host_accepts(name) -> bool` (Windows refuses `* ? < > : " | \`), `if !host_accepts(&name) { continue; }` after the atoms are drawn and before the collision key, so non-Windows draws are unchanged; Windows-only fact `host_accepts_refuses_the_two_glob_atoms_on_windows`; fix the doc name at `:7-8`; leave `windows.yml:358`'s `--skip census_`. Done when `cargo clippy --all-targets --workspace -- -D warnings` is clean here, the five censuses pass here (`cargo test -p slate-core --profile ci --lib -- session::tests::dir_tree::`, run quoted in the PR), `cargo fmt --all -- --check` / `cargo test --workspace` / `cargo bench --workspace --no-run` / the license script are clean, Pester passes, and `windows.yml` (new step green), `rust.yml` and `windows-runner-tests.yml` are green. `Closes #1570`, `Closes #1571`. Commit: `fix(core): windows-only code is clippy-clean and linted on the windows lane, and the dir_tree censuses generate only names the host accepts`.
