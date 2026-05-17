# yana-uniffi

[uniffi-rs](https://github.com/mozilla/uniffi-rs) FFI bindings for [`yana-core`](../yana-core), targeting Swift (Mac, iOS) and Kotlin (Android).

This crate is the FFI boundary between YANA's pure-Rust core and platform-native UI code on Apple and Android. Windows uses a separate `csbindgen`-based binding crate (not yet present).

## Status

Bootstrap stage. Currently exposes only the heading-extraction primitives from `yana-core` to validate the binding generation pipeline end-to-end. The full FFI surface — `VaultProvider` trait via callback interfaces, `VaultSession`, operation log, query engine — will land per [`docs/plans/05_locked_architecture_decisions.md`](../../docs/plans/05_locked_architecture_decisions.md).

## Building

```sh
cargo build -p yana-uniffi
```

Produces `target/<profile>/libyana_uniffi.dylib` (Mac), `.so` (Linux), or `.dll` (Windows), plus the `uniffi-bindgen` binary used to generate foreign-language bindings.

## Generating Swift bindings

After building, run:

```sh
cargo run -p yana-uniffi --bin uniffi-bindgen -- \
    generate \
    --library target/debug/libyana_uniffi.dylib \
    --language swift \
    --out-dir target/generated/swift
```

This emits three files into `target/generated/swift/`:

- `yana_uniffi.swift` — Swift API surface.
- `yana_uniffiFFI.h` — C header for the low-level FFI symbols.
- `yana_uniffiFFI.modulemap` — Swift module map exposing the header.

The Swift smoke-test client in [`examples/swift-cli/`](../../examples/swift-cli/) shows how to compile a Swift program against these bindings.

## Generating Kotlin bindings

```sh
cargo run -p yana-uniffi --bin uniffi-bindgen -- \
    generate \
    --library target/debug/libyana_uniffi.dylib \
    --language kotlin \
    --out-dir target/generated/kotlin
```

Kotlin client examples are not yet wired up (Android is the fourth shipping platform — see `docs/plans/05` §3).
