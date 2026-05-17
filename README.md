# YANA

**An accessibility-first knowledge workspace.** Reads existing Obsidian-style Markdown vaults. Built for blind, keyboard-only, and voice-control users from day one — not as an afterthought.

> **Status: bootstrap.** Not yet a usable product. The architectural design is locked and the toolchain is wired up end-to-end (Rust → uniffi-rs → SwiftUI on Mac); the first real V1 feature work has not yet begun.

## What this is

YANA is a long-running effort to build a personal knowledge management app that is accessibility-first by design. The defining decisions are documented at length in [`docs/plans/05_locked_architecture_decisions.md`](docs/plans/05_locked_architecture_decisions.md). The short version:

- **Native UI per platform**, not webview-based. Mac first, then Windows, then iOS, then Android. Linux deferred. SwiftUI + AppKit (`NSTextView`) on Apple, WPF + AvalonEdit on Windows, Jetpack Compose + `EditText` on Android.
- **Shared Rust backend** providing the parser, metadata index, query engine, and FFI surface. `uniffi-rs` for Apple + Android bindings; `csbindgen` for Windows.
- **Accessibility as a structural property**, not a UI-layer concern. The backend produces accessible representations alongside visual artifacts: MathML + speech + Nemeth/UEB braille for math (via [MathCAT](https://github.com/NSoiffer/MathCAT), the same library NVDA uses), structured descriptions for Mermaid diagrams, semantic spans for code, structured speech-text for citations.
- **Obsidian-vault-compatible** at the data level — read existing vaults, preserve unknown files. **Not Obsidian-plugin-compatible**, by design; a documented migration path exists for popular plugins.
- **No third-party JavaScript plugins, ever.** A three-tier extensibility model (declarative config V1, CLI/HTTP API V1.x, WASM sandbox V2) replaces the Obsidian-style plugin model. Plugins never draw UI.

## Why

Obsidian has had open screen-reader accessibility issues for [5+ years](https://forum.obsidian.md/t/accessibility-obsidian-with-screen-readers/19669) with no real progress. Logseq, SiYuan, and other open-source alternatives have similar gaps. The accessibility-first PKM is a real, persistent gap in the market, and one the existing open-source communities have shown they aren't prioritizing.

The full justification — including a red-team review of the original Flutter-based plan, the decision to abandon webview-based UI stacks for stability reasons, the engagement strategy with the math accessibility community, and concrete release-gate performance targets — is in [`docs/plans/`](docs/plans/).

## Repository layout

```
yana/
├── crates/
│   ├── yana-core/        Pure-Rust API. Vault, parser, metadata index (in progress).
│   └── yana-uniffi/      FFI wrapper exposing yana-core to Swift + Kotlin via uniffi-rs.
├── apps/
│   └── yana-mac/         SwiftUI smoke-test for Mac. Loads the Rust core via uniffi.
├── examples/
│   └── swift-cli/        Swift command-line smoke-test for the FFI.
├── scripts/
│   ├── build-swift-cli.sh
│   └── build-mac-app.sh
├── docs/
│   ├── plans/            Roadmap, locked architecture decisions, phase plans.
│   ├── diagrams/         Mermaid architecture diagrams.
│   └── research/         Competitive landscape and other research.
├── Cargo.toml            Workspace.
├── LICENSE               MIT.
├── Makefile              Common commands. `make help` to list.
└── rust-toolchain.toml   Pins to stable + rustfmt + clippy.
```

## Building

Prerequisites:

- **Rust stable.** If you don't have it, [`rustup`](https://rustup.rs/) is the recommended installer.
- **Xcode Command Line Tools** for `swiftc` (Mac smoke tests only). Install with `xcode-select --install`.

Common commands via `make`:

```sh
make help               # list available targets
make check              # cargo check --workspace
make test               # cargo test --workspace (8 tests)
make ci                 # fmt-check + clippy + test
make swift-cli          # build and run the Swift CLI smoke test
make mac-app            # build the SwiftUI smoke-test app
make mac-app-run        # build and launch the SwiftUI app
make clean              # cargo clean + remove SwiftPM build artifacts
```

The Makefile is a thin wrapper. The actual build pipelines live in [`scripts/`](scripts/) and can be invoked directly.

After `make mac-app-run`, the SwiftUI smoke-test window opens with headings parsed from an embedded sample. `⌘O` opens a file picker — the Rust core parses the selected Markdown file; Swift just renders the result. Each heading row exposes a VoiceOver-correct label of the form `Level N heading: <text>`.

## License

MIT. See [LICENSE](LICENSE).

## Don't try to use this yet

The codebase right now is design documentation plus a small bootstrap of the technical foundation. The architectural pack is locked at the level needed to start implementation; the implementation itself is at hour zero. **Don't try to use YANA as your notes app yet.** When a usable alpha is ready for AT-user testing, it will be announced through the project's issue tracker and discussions.
