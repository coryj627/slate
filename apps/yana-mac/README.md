# yana-mac (smoke test)

Minimal SwiftUI app that calls into the Rust `yana-core` library via the `yana-uniffi` bindings. Validates that the Rust → uniffi → SwiftUI toolchain works end-to-end on Mac.

## What it does

- Opens a window with a list of headings parsed from an embedded Markdown sample.
- Lets you open a Markdown file from disk via `⌘O` — the Rust core reads and parses the file; Swift just renders the result.
- Each heading row exposes `Level N heading: <text>` to VoiceOver so screen-reader users get a meaningful announcement, not the visual `H1`/`H2` chrome.

## Build and run

From the workspace root:

```sh
./scripts/build-mac-app.sh
```

That script:

1. Builds `yana-uniffi` so the dylib exists at `target/debug/libyana_uniffi.dylib`.
2. Runs `uniffi-bindgen` to emit Swift bindings into `target/generated/swift/`.
3. Copies the generated `yana_uniffi.swift` and `yana_uniffiFFI.h` into this package's `Sources/` directories.
4. Runs `swift build` against this package, linking the executable to the Rust dylib.
5. Optionally launches the resulting binary with `DYLD_LIBRARY_PATH` set (pass `--run` to the script).

After the script succeeds, the executable lives at `.build/debug/YanaMac` inside this package. Launch it from the script (`./scripts/build-mac-app.sh --run`) or directly:

```sh
DYLD_LIBRARY_PATH="$(pwd)/../../target/debug" .build/debug/YanaMac
```

## Why a SwiftPM executable and not an Xcode project

Smoke-test simplicity. SwiftPM with a single `executableTarget` + `@main App` gives a real SwiftUI window without `.xcodeproj` bookkeeping. When the project moves to a proper distributable Mac app — code signing, sandbox entitlements, `.app` bundle metadata, Info.plist — it'll graduate to an Xcode project alongside this one (or replacing it).

## Why two Sources directories

`Sources/YanaUniffiFFI/` is a `systemLibrary` target that exposes the uniffi-generated C header to Swift via a `module.modulemap`. `Sources/YanaMac/` is the SwiftUI executable target that imports `YanaUniffiFFI`. SwiftPM requires this split because the C header can't live in the same target as Swift sources.

The `module.modulemap` is committed; the generated header (`yana_uniffiFFI.h`) and the generated Swift API (`yana_uniffi.swift`) are not — they're build artifacts copied in by `scripts/build-mac-app.sh` and gitignored.

## What this is not

- Not a real Mac app — no proper bundle, no entitlements, no code signing, no menu bar customization.
- Not exercising any of the architecture in `docs/plans/05` beyond the heading-extraction primitives. `VaultProvider`, `VaultSession`, the operation log, the query engine — all later.
- Not accessibility-complete. The list rows are labeled correctly for VoiceOver, but the app's broader a11y story (keyboard navigation between regions, custom rotors, focus management) is a later iteration.
