# swift-cli

Swift command-line smoke test for the `slate-uniffi` bindings. Validates the full Rust → uniffi → Swift toolchain on Mac.

## Build and run

From the workspace root:

```sh
./scripts/build-swift-cli.sh
```

That script:

1. Builds `slate-uniffi` (`cargo build -p slate-uniffi`).
2. Runs `uniffi-bindgen` to emit Swift bindings into `target/generated/swift/`.
3. Compiles `main.swift` against those bindings with `swiftc`, linking against `libslate_uniffi.dylib`.
4. Executes the produced binary.

Output looks like:

```
Extracting headings from embedded sample (pass a path to read a file):
Got 3 headings:
  # Hello, Slate
  ## A subheading
  ### Deeper still
```

## Reading a file

```sh
./target/swift-cli/slate-swift-cli path/to/note.md
```

## Manual build

If you want to build without the script:

```sh
cargo build -p slate-uniffi

cargo run -p slate-uniffi --bin uniffi-bindgen -- \
    generate \
    --library target/debug/libslate_uniffi.dylib \
    --language swift \
    --out-dir target/generated/swift

swiftc \
    -emit-executable \
    -o target/swift-cli/slate-swift-cli \
    -L target/debug \
    -lslate_uniffi \
    -I target/generated/swift \
    -import-objc-header target/generated/swift/slate_uniffiFFI.h \
    target/generated/swift/slate_uniffi.swift \
    examples/swift-cli/main.swift

DYLD_LIBRARY_PATH=target/debug ./target/swift-cli/slate-swift-cli
```

## Why this exists

This is a smoke test, not a product. It exists to validate the toolchain (Rust → uniffi-rs → Swift on Apple Silicon) before any real UI work begins. Once the SwiftUI shell starts, this directory may be deleted or kept as a diagnostic.
