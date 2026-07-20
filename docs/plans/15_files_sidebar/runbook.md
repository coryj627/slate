# Milestone FL — Runbook

Operational reference for the Files sidebar: how to build, test, verify, and
diagnose it. Recorded values (benchmarks, census seeds, machine context) are
filled at close-out on the merged tree; commands are exact.

## Build and test

```sh
# Rust core (workspace root). MUST run on an otherwise-idle machine —
# parallel builds on the same box skew the perf guards into false failures.
make ci

# Swift suite (apps/slate-mac). The debug dylib path is required.
cd apps/slate-mac
DYLD_LIBRARY_PATH="$PWD/../../target/debug" swift test

# Targeted sidebar suites
DYLD_LIBRARY_PATH="$PWD/../../target/debug" swift test \
  --filter "SidebarListPaneTests|SidebarActionParityTests|SidebarDualPaneContainerTests"

# Accessibility-gated app build — 100.0 A+ (0 issues) is the merge floor.
# CI's checker can be newer than local: gate every PR's own tip in CI.
./scripts/build-mac-app.sh
```

After ANY crates/ change or merge that includes one, regenerate the
gitignored UniFFI bindings before building Swift:

```sh
make regenerate-bindings
```

Symptom of staleness: Swift compile errors on types that exist in Rust
(e.g. "cannot find type 'X' in scope") right after a pull.

## Verification inventory (close-out)

| Gate | Command | Recorded result |
| --- | --- | --- |
| Rust workspace | `make ci` (idle box) | _fill at close-out_ |
| Swift suite | `swift test` (full) | _fill at close-out_ |
| Accessibility | `./scripts/build-mac-app.sh` | _fill at close-out_ |
| Censuses (release) | see below | _fill at close-out_ |
| Benchmarks vs FL budgets | see below | _fill at close-out_ |

FL budgets (program §NFR): filter ≤ 50 ms @10k files; tag tree ≤ 25 ms @10k;
root metadata listing ≤ 10 ms; scan regression ≤ 5%.

Census / property suites guarding sidebar invariants (all release-mode at
close-out; the ci profile arms assertions):

```sh
cargo test -p slate-core --release --test tag_tree_exec
cargo test -p slate-core --release --test folder_notes_exec
cargo test -p slate-core --release -- session::tests::file_meta   # random-walk census incl. TagAdd/TagRemove
cargo test -p slate-core --release -- session::tests::dir_tree    # perf guards (best-of-3 sampling)
```

## Diagnosis map

| Symptom | First place to look |
| --- | --- |
| Sidebar rows missing metadata (dates/previews) | Scan completed? `scanInitial` errors surface in the sidebar's scan-error state; previews/task counts come from the index, not the file on disk. |
| Filter returns nothing for a valid-looking query | The grammar is committed-only: check the field actually committed (⏎ or debounce). Then run the same query through `filterFiles` in a Rust test — the engine is deterministic. |
| Tag missing from the tree | The tree counts **indexed Markdown** files only; check the file's tags parsed (hostile frontmatter shapes are skipped with per-file reasons — see the batch-edit report). |
| Batch tag edit skipped files | The report's per-file reason strings are exact; "inline" reasons mean body occurrences intentionally survive frontmatter removal. |
| Folder note badge wrong | `has_folder_note` is one indexed probe per child dir (`<Folder>/<Folder>.md`, markdown only). Rescan; then check the index row for that exact path. |
| Compound folder rename left pieces | The operation degrades to a plain rename when no note is present at operation time; rollback messages state exactly what was and wasn't restored — read the structural report, and `undo_op_ids` lists both undo rows. |
| Dual-pane list stale after a mutation | Refresh triggers are value-typed: `treeMutation` (structural) and `sidebarOrganization` (pins/sort/overrides). If neither changed, the mutation didn't go through a funnel — that's the bug. |
| Dual-pane list truncated | The drain caps at 10,000 files and the header says "first 10,000 files". Scope down (or filter); the cap is a deliberate ceiling, not a failure. |
| Layout/divider/recents didn't follow the vault | They're device-local by design. Vault-owned state is exactly what's in `.slate/sidebar.json` (sort/grouping/folder overrides incl. preview/density/descendants, pins, shortcuts). |
| Perf guard failed in a local run | Was the box busy? The guards sample best-of-3 but a saturated machine still fails falsely; re-run idle. CI's isolated runner is authoritative. |

## Persistence boundaries

- **Vault (`.slate/sidebar.json`)**: vault-default sort/grouping; per-folder
  overrides (sort, grouping, previewLines 0–3, density, descendants); pins
  (authored order, per folder); shortcuts. Unknown keys are preserved on
  write; malformed files disable organization commands with a spoken reason
  rather than being clobbered.
- **Device (UserDefaults)**: sidebar layout (tree/dual-pane), divider
  fraction, Recents, section expansion, filter's committed-query restore.

## Failure / recovery procedures

- **Read-only `.slate/sidebar.json`**: organization commands disable with
  the notice's reason; fix permissions and re-run the command — no state is
  lost (mutations replay through the locked-write funnel).
- **Stale pins/shortcuts after external file moves**: pins/shortcuts follow
  structural transforms only for in-app operations; externally-moved paths
  are pruned lazily on the next level fetch (announced once per session).
- **Import interrupted**: the Finder-import flow is copy-then-index with
  per-item reports; re-running the import is safe (exclusive creation
  refuses to clobber and reports per item).

## Review / merge protocol (as run for FL-06…FL-15)

One adversarial review round per PR (`codex-companion.mjs adversarial-review
--background --base <merged-main-sha> --model gpt-5.6-sol`); fix
high/critical and genuinely-assessed mediums with regressions, document
accepted findings with rationale in the PR body. Push, then monitor at 90 s
(CI roster total-gated — verify the full check roster explicitly before any
merge; Codoki dedup on comment id + body length with a fresh fetch before
acting). Merge on all-green + explicit Codoki safe-to-merge.
