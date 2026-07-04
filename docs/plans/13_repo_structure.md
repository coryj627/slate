# 13 — Repository Structure: Single Monorepo for Core + All Frontends

**Status:** Locked (2026-07-04).
**Scope:** How Slate's Rust core and its per-platform native frontends are organized across git repositories. Companion to [`05_locked_architecture_decisions.md`](05_locked_architecture_decisions.md) (the "one core, many native frontends" architecture) and [`07_portability_review.md`](07_portability_review.md) (the FFI/portability risks this decision protects).

## Context

Slate is a Rust core (`crates/slate-core`) exposed through a single FFI surface (`crates/slate-uniffi`) and consumed by native per-platform frontends. Today the repository holds the core, the FFI, and the macOS app (`apps/slate-mac`). A **Windows** frontend (Milestone W: WPF + AvalonEdit) is imminent, with **iOS** and **Android** to follow. Before the first non-Mac frontend lands, we need a settled answer to: **keep everything in one repository, or split into a `slate-core` repo consumed by separate per-frontend repositories?** The optimization target is long-term maintainability and support.

### Forces (as of this decision)

- **Maintainers:** open-source contributors, changes reviewed by the project owner. No independent teams with hard permission boundaries.
- **Core reuse:** `slate-core` is **internal to Slate only**. It is *not* planned to ship as a third-party library, SDK, published crate, or headless server.
- **Release cadence:** **unified** — all platforms share one version (`0.0.1` today).
- **FFI maturity:** **unstable (v0.0.1).** Per `07_portability_review.md`, editor spans, command-palette ranking, and the accessibility-announcement vocabulary still need to migrate *into* `slate-core`. The FFI surface is expected to change materially as the non-Mac frontends are built.
- **Frontends are asymmetric consumers of one library, not of a published artifact.** mac/iOS/Android bind via `uniffi-rs` (Swift/Kotlin); Windows binds via `csbindgen` / (candidate) `uniffi-bindgen-cs` (C#). Each frontend generates its *own* language bindings from the same Rust `cdylib`/`staticlib`. There is no single shared binary artifact all frontends pull.
- **Coupling today is minimal:** one workspace version, FFI bindings generated at build time and git-ignored (never committed), the frontend links the dylib by relative path, and CI builds core → bindings → frontend in a single job. No git submodules, no cross-repo dependencies.

## Decision

**Keep Slate as a single polyglot monorepo.** Each native frontend is a sibling directory under `apps/`; the *only* shared code is the Rust workspace under `crates/`.

```
slate/
├── crates/                 # Rust workspace — the ONLY shared code across platforms
│   ├── slate-core/         #   pure domain logic + accessibility artifacts (no platform code)
│   └── slate-uniffi/       #   the single FFI boundary; feeds every binding generator
├── apps/
│   ├── slate-mac/          # SwiftUI + AppKit          (exists)
│   ├── slate-windows/      # WPF + AvalonEdit           (Milestone W — new dir, same pattern)
│   ├── slate-ios/          # SwiftUI + UIKit            (later)
│   └── slate-android/      # Jetpack Compose            (later)
├── docs/ · scripts/ · Makefile · one LICENSE (AGPL-3.0-or-later)
```

**Directory convention (normative):** one native app per `apps/slate-<platform>/` directory. Frontends **do not** share UI code with one another; the only thing they share is `crates/slate-uniffi`. A frontend must never reach into another frontend's directory.

## Rationale

1. **Every force that normally justifies extracting a core is absent.** A core gets pulled out of a monorepo when (a) third parties consume it, (b) frontends ship on independent cadences, or (c) independent teams need hard permission/CI isolation. None hold here. Splitting now buys real cost (cross-repo version pinning, artifact publishing, coordinated multi-repo releases) with no offsetting benefit.

2. **An unstable FFI is the worst thing to split across a repo boundary.** A repo boundary ossifies the interface it straddles. While spans / command-palette / a11y-vocabulary are still migrating into the core, a polyrepo would turn every core change that touches the FFI into a coordinated, multi-repo, multi-PR release — the opposite of "ease of maintainability." In one repo, a contributor changes the FFI surface **and every consumer in a single, atomically-tested PR.**

3. **Long-term supportability comes from thin frontends over a stable core boundary — not from repo count.** With N frontends, maintenance cost is dominated by logic duplicated per platform. Keeping ranking/spans/a11y in `slate-core` (the `07_portability_review.md` items) makes each new frontend mostly UI binding. The monorepo makes that discipline *enforceable*: one CI run proves every frontend still binds the current core. A polyrepo makes drift the default.

4. **Open-source governance is simpler in one repo.** One `CONTRIBUTING`, one issue tracker, one CI surface, one `LICENSE`. A Windows-only contributor needs the Rust toolchain regardless (the C# app links the Rust dylib), so a separate repo would not spare them the core — it would only add a second clone, a version pin, and an artifact-sync step. Per-area ownership is expressed with `CODEOWNERS`, not with separate repositories.

5. **Deferring costs nothing.** Because the only coupling is the FFI surface and bindings are generated (never committed), extracting `crates/` into its own repository later is a mechanical `git filter-repo` plus switching path-deps to version-deps — a day, not a project. There is no lock-in penalty for staying monorepo now.

## Consequences

- New frontends are added as `apps/slate-<platform>/` and wired into path-filtered CI; no repo-provisioning ceremony.
- The FFI surface (`crates/slate-uniffi`) is the single source of truth feeding all binding generators. Prefer one FFI definition per language generator over hand-written per-platform shims (for Windows, evaluate `uniffi-bindgen-cs` vs a hand-written `csbindgen` shim — see `07_portability_review.md`; this is a Milestone-W spike, not part of this decision).
- FFI bindings remain **generated and git-ignored**. Contributors who change the FFI must regenerate (`make regenerate-bindings`); see `CONTRIBUTING.md`.
- Ownership is expressed via `.github/CODEOWNERS` per top-level area (`crates/**`, `apps/slate-<platform>/**`), not via repository boundaries.

## Revisit triggers

Re-open this decision only if one of these becomes true (none are today):

- `slate-core` gains a **third-party / external consumer** (published crate, headless server, plugin SDK). → Extract the core into its own versioned repository.
- A frontend needs a **genuinely independent release cadence** with its own version. → Prefer per-app release tags within the monorepo first; separate repo only if that proves insufficient.
- A frontend needs **hard permission isolation** that `CODEOWNERS` cannot express.
- Repo scale hits real tooling limits (checkout time, CI minutes). Implausible at the current ~85K LOC.
