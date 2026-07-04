# Contributing to Slate

Thanks for your interest in Slate — an accessibility-first knowledge workspace.

> **Status: bootstrap.** Slate is not yet a usable product and the FFI surface
> is still changing. See [`README.md`](README.md) for what exists today and
> [`docs/plans/`](docs/plans/) for the locked architecture.

## Repository structure (read this first)

Slate is a **single monorepo**: one Rust core, one FFI surface, and one native
frontend per platform. The full rationale — and when we would ever split it — is
in [`docs/plans/13_repo_structure.md`](docs/plans/13_repo_structure.md).

```
crates/slate-core/     Pure Rust. Domain logic + accessibility artifacts. No platform code.
crates/slate-uniffi/   The single FFI boundary. Feeds every frontend's binding generator.
apps/slate-mac/        SwiftUI + AppKit frontend (more platforms land as apps/slate-<platform>/).
```

Two rules that keep the monorepo maintainable:

1. **The only code shared across platforms is `crates/`.** Frontends must not
   share UI code with each other, and a frontend must never reach into another
   frontend's directory. Anything a second platform would need belongs in
   `slate-core` (produce accessible representations and shared logic there, not
   in the UI layer — this is the doctrine in
   [`docs/plans/05_locked_architecture_decisions.md`](docs/plans/05_locked_architecture_decisions.md)).
2. **`crates/slate-uniffi` is the single source of truth for the FFI.** Every
   frontend generates its own language bindings from it. Change the API in one
   place; all consumers are updated in the same PR.

## Prerequisites & build

See [`README.md`](README.md#building) for prerequisites (Rust stable via
`rustup`; Xcode / Command Line Tools for the Mac frontend) and the common
`make` targets. In short: `make check`, `make test`, `make mac-app-run`.

## The FFI bindings workflow (important)

The Rust → Swift bindings (`slate_uniffi.swift`, `slate_uniffiFFI.h`) are
**generated from the built dylib and are git-ignored — they are never
committed.** The build scripts regenerate and stage them automatically, so a
normal `make mac-app` / `make swift-cli` always uses fresh bindings.

If you change the FFI surface in `crates/slate-uniffi` and want the Swift
frontend to see the new API without a full app build, regenerate explicitly:

```sh
make regenerate-bindings   # rebuilds slate-uniffi, regenerates + stages the Swift bindings
```

Because the bindings are git-ignored, **do not commit** `slate_uniffi.swift` or
`slate_uniffiFFI.h`. If Swift fails to see an FFI change, stale staged bindings
are the usual cause — rerun `make regenerate-bindings` (or `make clean` first).

## Before you open a PR

CI mirrors these; running them locally avoids a round-trip
(see [`.github/workflows/`](.github/workflows/)):

- **`make ci`** — `fmt-check` + `clippy` (warnings are errors) + `test` +
  `bench-check` + license-header check. Run this before every push.
- **License headers.** Every tracked `.rs`/`.swift` file needs an
  `SPDX-License-Identifier` header. Add them with
  `python3 scripts/apply-license-header.py`.
- **Accessibility (Mac frontend).** SwiftUI changes are checked by
  `a11y-check` (the [a11y-check workflow](.github/workflows/a11y-check.yml)
  enforces a minimum score). Accessibility is a hard requirement here, not a
  nice-to-have.

Keep changes scoped: a PR that touches the FFI plus its consumers is welcome
(that's the monorepo advantage), but unrelated cross-area churn is not.

## Review & ownership

Reviews route through [`.github/CODEOWNERS`](.github/CODEOWNERS) by top-level
area (`crates/**`, `apps/slate-<platform>/**`, etc.). If you'd like to help
maintain a specific platform, say so in your PR.

## License

Slate is licensed under the **GNU AGPL-3.0-or-later**. By contributing, you
agree your contributions are licensed under the same terms. See
[`LICENSE`](LICENSE).
