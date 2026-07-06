# Slate

**An accessibility-first knowledge workspace.** Reads existing Obsidian-style Markdown vaults. Built for blind, keyboard-only, and voice-control users from day one — not as an afterthought.

> **Status: pre-release alpha, macOS.** The V1 feature set is built and tested on Mac — vault + file management, reading/editing editor, backlinks, properties, full-text search, tasks, templates, embeds, math/code/Mermaid pipelines, citations, command palette, sync detection, the `slate` CLI, a workspace shell (tabs + splits + right-pane leaves), and an accessible Canvas. It builds and runs from source (`make mac-app-run`); it is **not yet packaged as a downloadable release**, and Windows/iOS/Android are still ahead. See [Don't try to use this yet](#dont-try-to-use-this-yet).

## What this is

Slate is a long-running effort to build a personal knowledge management app that is accessibility-first by design. The defining decisions are documented at length in [`docs/plans/05_locked_architecture_decisions.md`](docs/plans/05_locked_architecture_decisions.md). The short version:

- **Native UI per platform**, not webview-based. Mac first, then Windows, then iOS, then Android. Linux deferred. SwiftUI + AppKit (`NSTextView`) on Apple, WPF + AvalonEdit on Windows, Jetpack Compose + `EditText` on Android.
- **Shared Rust backend** providing the parser, metadata index, query engine, and FFI surface — one core, many native frontends. `uniffi-rs` for Apple + Android bindings; the Windows C# binding is a Milestone-W spike (`uniffi-bindgen-cs` vs a hand-written `csbindgen` shim — see [`docs/plans/07_portability_review.md`](docs/plans/07_portability_review.md) §4.1).
- **Accessibility as a structural property**, not a UI-layer concern. The backend produces accessible representations alongside visual artifacts: MathML + speech + Nemeth/UEB braille for math (via [MathCAT](https://github.com/NSoiffer/MathCAT), the same library NVDA uses), structured descriptions for Mermaid diagrams, semantic spans for code, structured speech-text for citations.
- **Obsidian-vault-compatible** at the data level — read existing vaults, preserve unknown files. **Not Obsidian-plugin-compatible**, by design; a documented migration path exists for popular plugins.
- **No third-party JavaScript plugins, ever.** A three-tier extensibility model (declarative config V1, CLI/HTTP API V1.x, WASM sandbox V2) replaces the Obsidian-style plugin model. Plugins never draw UI.

## Why

Obsidian has had open screen-reader accessibility issues for [5+ years](https://forum.obsidian.md/t/accessibility-obsidian-with-screen-readers/19669) with no real progress. Logseq, SiYuan, and other open-source alternatives have similar gaps. The accessibility-first PKM is a real, persistent gap in the market, and one the existing open-source communities have shown they aren't prioritizing.

The full justification — including a red-team review of the original Flutter-based plan, the decision to abandon webview-based UI stacks for stability reasons, the engagement strategy with the math accessibility community, and concrete release-gate performance targets — is in [`docs/plans/`](docs/plans/). Start with [`docs/README.md`](docs/README.md) for a status-tagged index of every program and milestone spec, or [`docs/plans/05_locked_architecture_decisions.md`](docs/plans/05_locked_architecture_decisions.md) for the canonical decisions.

End-user feature guides live in [`docs/help/`](docs/help/) (first entry: [Canvas](docs/help/canvas.md), drafted with Milestone T's specs).

## Repository layout

```
slate/
├── crates/                 Rust workspace — the ONLY code shared across platforms.
│   ├── slate-core/         Domain logic + accessibility artifacts: vault, parser,
│   │                       metadata index (SQLite), FTS, editor DocumentBuffer,
│   │                       Canvas, citations, tasks, content pipelines.
│   ├── slate-uniffi/       The single FFI boundary; feeds every binding generator.
│   └── slate-cli/          The `slate` command-line tool (Milestone M).
├── apps/
│   └── slate-mac/          SwiftUI + AppKit app (NSTextView editor). Binds the
│                           Rust core via uniffi. (slate-windows is Milestone W.)
├── examples/
│   └── swift-cli/          Swift command-line smoke-test for the FFI.
├── docs/                   See docs/README.md for the full index.
│   ├── plans/              Locked architecture decisions + per-milestone programs & specs.
│   ├── help/               End-user feature guides.
│   ├── runbooks/           Operational procedures (e.g. VoiceOver feature-test).
│   ├── diagrams/           Mermaid architecture diagrams.
│   └── research/           Competitive landscape and other research.
├── scripts/                Build pipelines (build-mac-app.sh, build-swift-cli.sh, …).
├── .github/                CI workflows, CODEOWNERS.
├── CONTRIBUTING.md         Contributor guide (incl. `make regenerate-bindings`).
├── Cargo.toml              Workspace.
├── LICENSE                 GNU AGPL v3.0 (or later).
├── Makefile                Common commands. `make help` to list.
└── rust-toolchain.toml     Pins to stable + rustfmt + clippy.
```

## Building

Prerequisites:

- **Rust stable.** If you don't have it, [`rustup`](https://rustup.rs/) is the recommended installer.
- **Xcode Command Line Tools** for `swiftc` (Mac smoke tests only). Install with `xcode-select --install`.

Common commands via `make` (run `make help` for the full list):

```sh
make check                # cargo check --workspace
make test                 # cargo test --workspace
make ci                   # fmt-check + clippy + test + bench-check + license headers
make bench                # criterion benchmarks (see BENCHMARKS.md)
make regenerate-bindings  # rebuild slate-uniffi + regenerate/stage the Swift FFI bindings
make swift-cli            # build and run the Swift CLI smoke test
make mac-app-run          # build and launch the SwiftUI app
make clean                # cargo clean + remove SwiftPM build artifacts
```

The Makefile is a thin wrapper; the actual build pipelines live in [`scripts/`](scripts/) and can be invoked directly. FFI bindings are generated at build time and git-ignored — contributors who change the FFI surface run `make regenerate-bindings` (see [`CONTRIBUTING.md`](CONTRIBUTING.md)).

`make mac-app-run` builds and launches the macOS app against a vault of your choosing. Accessibility is a first-class acceptance gate: the CI suite includes an automated a11y check (APCA Lc ≥ 75, VoiceOver-correct semantics) alongside the Rust and Swift test suites.

## License

Copyright (C) 2026 Cory Joseph.

Slate is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See [LICENSE](LICENSE) for the full text.

Slate is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Affero General Public License for details.

## Don't try to use this yet

The macOS app has a substantial feature set built and tested, but Slate is still pre-release: there is no signed, downloadable build; features are landing and changing weekly; and the accessibility work is verified continuously but has not been through a full AT-user testing round. **Don't try to use Slate as your daily notes app yet.** When a packaged alpha is ready for AT-user testing, it will be announced through the project's issue tracker and discussions.
