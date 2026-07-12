# slate-mac

SwiftUI app that hosts the Slate UI on macOS. Calls into the Rust `slate-core` library via the `slate-uniffi` bindings.

## What it does

- **Welcome screen** with an "Open Vault…" button (Shift-Command-O; plain Command-O on the welcome screen also opens the picker). Focus lands on the button on launch.
- **Directory-mode picker** (`NSOpenPanel`) selects a folder to use as the vault.
- **Main split view** opens after a vault is selected: sidebar + detail placeholders today; the file list lands in a follow-up issue.
- Errors from `VaultSession.open` surface in an accessible alert.

## Build and run

From the workspace root:

```sh
./scripts/build-mac-app.sh        # build only
./scripts/build-mac-app.sh --run  # build and launch
```

The script:

1. Builds `slate-uniffi` so the dylib exists at `target/debug/libslate_uniffi.dylib`.
2. Runs `uniffi-bindgen` to emit Swift bindings into `target/generated/swift/`.
3. Copies the generated `slate_uniffi.swift` and `slate_uniffiFFI.h` into this package's `Sources/` directories.
4. Runs `swift build` against this package, linking the executable to the Rust dylib.
5. Runs `a11y-check` (if installed) over `Sources/SlateMac` so accessibility regressions surface locally rather than waiting for CI.
6. Optionally launches the resulting binary with `DYLD_LIBRARY_PATH` set (pass `--run`).

After the script succeeds, the executable lives at `.build/debug/SlateMac` inside this package. Launch it from the script or directly:

```sh
DYLD_LIBRARY_PATH="$(pwd)/../../target/debug" .build/debug/SlateMac
```

## Tests

Run the Swift test target after building (so the Rust dylib is available to link against):

```sh
./scripts/build-mac-app.sh
cd apps/slate-mac
DYLD_LIBRARY_PATH="$(pwd)/../../target/debug" swift test
```

The test target lives at `Tests/SlateMacTests/` and uses `@testable import SlateMac` to reach internal types. It depends on the same `slate_uniffi` dylib as the executable target, hence the `DYLD_LIBRARY_PATH` setup.

## Accessibility checks

`a11y-check` from [cvs-health/ios-swiftui-accessibility-techniques](https://github.com/cvs-health/ios-swiftui-accessibility-techniques) statically analyzes SwiftUI sources for WCAG 2.2 issues. The repo's CI (`.github/workflows/a11y-check.yml`) runs it on every PR that touches Mac sources and fails the run if the score drops or any errors land.

To install locally so `scripts/build-mac-app.sh` runs the same check:

```sh
brew tap cvs-health/ios-swiftui-accessibility-techniques \
  https://github.com/cvs-health/ios-swiftui-accessibility-techniques.git
brew install --HEAD cvs-health/ios-swiftui-accessibility-techniques/a11y-check
```

## Why a SwiftPM executable and not an Xcode project

Bootstrap simplicity. SwiftPM with a single `executableTarget` + `@main App` gives a real SwiftUI window without `.xcodeproj` bookkeeping. When the project needs proper distribution — code signing, sandbox entitlements, `.app` bundle metadata, Info.plist — it'll graduate to an Xcode project alongside this one (or replacing it).

## Consuming the FFI from Swift

This app calls into the Rust core through the types defined in [`crates/slate-uniffi`](../../crates/slate-uniffi/README.md#ffi-surface) — see that README for the full surface. A few types deserve special attention on the Mac side because they drive UI patterns:

- **`CancelToken`** — pass into long-running calls (scans, full-text search). Hold a clone on the `AppState` actor so the UI can cancel without re-entering the call.
- **`ScanProgressListener`** — Swift callback trait. Implement it (`ScanProgressAdapter` does) to update the scan strip / progress UI as files index.
- **`SaveReport`** — returned by `save_text`. Carries the new `content_hash` and op-log id; surface conflicts (`VaultError::WriteConflict`) via the save-conflict resolver UI.
- **`VaultError`** — surfaces as `throws` on every Swift call. Map known variants (`Cancelled`, `FileTooLarge`, `WriteConflict`) explicitly; fall back to a generic accessible alert for the rest.

## Why two Sources directories

`Sources/slate_uniffiFFI/` is a `systemLibrary` target that exposes the uniffi-generated C header to Swift via a `module.modulemap`. `Sources/SlateMac/` is the SwiftUI executable target that imports `slate_uniffiFFI`. SwiftPM requires this split because the C header can't live in the same target as Swift sources.

The `module.modulemap` is committed; the generated header (`slate_uniffiFFI.h`) and the generated Swift API (`slate_uniffi.swift`) are not — they're build artifacts copied in by `scripts/build-mac-app.sh` and gitignored.

## Out of scope for now

- Not yet a distributable bundle (no `.app`, no entitlements, no code signing).
- Sidebar/detail placeholders only — file list, reading, search etc. land in subsequent milestones.
