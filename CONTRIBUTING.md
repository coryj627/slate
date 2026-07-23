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

The Rust → C# binding for the Windows frontend
(`apps/slate-windows/src/SlateUniffi/generated/slate_uniffi.cs`) follows the
same rule: generated, git-ignored, never committed. Regenerate with
`make regenerate-bindings-windows`, or without make (see below):

```powershell
./apps/slate-windows/generate-bindings.ps1
```

The Windows generator always builds and stages the **Release** native DLL.
That keeps local and CI editor measurements on the shipped `DocumentBuffer`
path; do not substitute `target/debug/slate_uniffi.dll` for app or test builds.

**If you change the FFI surface, both platforms' bindings must keep
generating and compiling.** You don't need a Mac (or a Windows box) to prove
the other platform: CI regenerates and compiles the Swift bindings in
`swift-tests.yml` and the C# bindings in `windows.yml` — a generator or
compile break on either side fails the PR.

### Windows local development (no make required)

The `Makefile` and `scripts/*.sh` assume a unix shell; on Windows, work in
PowerShell with the `dotnet` CLI directly. Prerequisites: the repo-pinned
Rust toolchain (`rustup` reads `rust-toolchain.toml` automatically), the
.NET 10 SDK, and the pinned bindings generator:

```powershell
cargo install --git https://github.com/NordSecurity/uniffi-bindgen-cs --tag v0.11.0+v0.31.0 uniffi-bindgen-cs --locked
```

The daily loop, from the repo root:

```powershell
./apps/slate-windows/generate-bindings.ps1          # after any FFI change
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet format apps/slate-windows/SlateWindows.slnx  # pre-push; CI enforces --verify-no-changes
```

Rust-side checks (`cargo fmt --all -- --check`, `cargo clippy --all-targets
--workspace -- -D warnings`, `cargo test --workspace`) run the same commands
as `make ci` and work unchanged in PowerShell.

## Before you open a PR

CI mirrors these; running them locally avoids a round-trip
(see [`.github/workflows/`](.github/workflows/)):

- **`make ci`** — `fmt-check` + `clippy` (warnings are errors) + `test` +
  `bench-check` + license-header check. Run this before every push.
- **License headers.** Every tracked `.rs`/`.swift`/`.cs` file needs an
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
