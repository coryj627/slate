# Coding Conventions

**Analysis Date:** 2026-05-28

## Naming Patterns

**Files:**
- `PascalCase` for all Swift source files matching the primary type they define: `AppState.swift`, `CommandPaletteModel.swift`, `NoteEditorView.swift`
- Rust files use `snake_case`: `links.rs`, `frontmatter.rs`, `links_db.rs`
- Test files mirror the type under test with a `Tests` suffix: `AppStateTests.swift`, `CommandRegistryTests.swift`
- Integration test files follow `Milestone{Letter}IntegrationTests.swift` pattern

**Types and Enums:**
- `PascalCase` for all types: `SearchState`, `TaskReviewFilter`, `LinkActivationOutcome`
- Enum cases use `camelCase`: `.idle`, `.searching`, `.results(rows:summary:)`, `.openedInternal(_:)`
- FFI-bridged Rust types retain Rust naming at the boundary, then get Swift-side extensions: `MathPrefs`, `CodePrefs`

**Functions and Methods:**
- `camelCase` for all methods: `loadCommands(_:recents:)`, `handleQueryChange()`, `openRecent(_:)`
- Boolean properties prefixed with `is` or `has`: `isVaultOpen`, `isScanning`, `isCommandPaletteOpen`, `hasUnsavedChanges`
- Async task handles follow the `<action>Task` pattern: `scanTask`, `noteLoadTask`, `linksLoadTask`, `searchTask`

**Variables:**
- `camelCase` for all variables and properties
- `@Published` properties on `AppState`/`ObservableObject` are the source of truth for UI state
- `private(set)` used widely on `@Published` vars to enforce write discipline

**Constants:**
- Static string keys namespaced under reverse-DNS prefix: `"slate.prefs.math"`, `"slate.prefs.code"`
- Command IDs follow `slate.<section>.<verb>` convention enforced by test

**Rust:**
- `snake_case` for all items (types, functions, modules, variables)
- Module-level `//!` doc comments explain the module's scope and design decisions

## Code Style

**Formatting:**
- No explicit Prettier/SwiftFormat config file found — Swift indentation is 4 spaces throughout (consistent with Xcode defaults)
- Rust: `cargo fmt` enforced in CI via `cargo fmt --all --check`

**Linting:**
- Rust: `cargo clippy --workspace --all-targets -- -D warnings` (warnings-as-errors, all targets including benches)
- Swift: `a11y-check` static analyzer enforced in CI (34 rules, 19 WCAG criteria, minimum score 100/100)
- No `.eslintrc` or `.swiftlint.yml` found — a11y-check is the primary static analyzer for Swift

## Import Organization

**Swift — order observed:**
1. System/Apple frameworks first, alphabetically: `import AppKit`, `import Combine`, `import Foundation`, `import SwiftUI`
2. Third-party packages: `import LaTeXSwiftUI`, `import SwiftDraw`
3. `@testable import SlateMac` last in test files

**Rust — standard pattern:**
- External crates first
- `use super::*` in test modules

## Error Handling

**Swift — layered approach:**
- FFI errors mapped to `@Published` error properties on `AppState` (e.g., `noteLoadError`, `scanError`, `propertyEditError`, `embedsLoadError`) — never propagated raw to SwiftUI
- `guard` used for early exits: `guard isVaultOpen else { ... }`, `guard let session = currentSession else { ... }`
- `do { try ... } catch { ... }` with error stored on the observable state object, not thrown to callers
- Preference decode failures silently return defaults (not logged, not surfaced to users) — documented in comments as intentional graceful degradation
- Store writes use temp-file + atomic rename pattern (`PrefsJsonStore`) to prevent half-written files on kill
- `XCTUnwrap` in tests preferred over force-unwrap: `let preview = try XCTUnwrap(state.pendingRenameReport)`

**Rust — `Result`-based:**
- Functions that can fail return `Result<T, SomeError>`; the caller pattern-matches
- The frontmatter parser never returns an error to callers — parse failures yield an empty result + a `PropertyParseWarning`

## Logging

**Framework:** No logging framework — no `os.log`, no `print` statements found in reviewed source files

**Pattern:** Errors are surfaced through `@Published` properties on `AppState`. Accessibility announcements (via `postAccessibilityAnnouncement`) serve as the UI-visible feedback channel for async operations.

## Comments

**When to Comment:**
- Every public/internal type gets a `///` doc comment explaining purpose, design rationale, and references to GitHub issue numbers
- Inline comments explain non-obvious decisions, especially around race conditions, cancellation, and accessibility trade-offs
- Design constraints noted inline with cross-references: `// Codoki callout on PR 79`, `// closes #308`, `// Milestone J UI`
- Timezone caveats, V1 limitations, and "follow-up issue" notes are documented at the call site

**Doc comment style:**
- `///` for API-level comments on types, methods, properties
- `//!` (Rust module-level) for file-scope module docs explaining scope, algorithm choices, crate selection rationale
- Section headers use `// MARK: - Section Name` (source files) and `// --- Section name ---` (Rust tests)
- Verbose explanations of accessibility decisions are common and expected

**What NOT to comment:** Obvious one-liners — the codebase avoids noise comments on self-explanatory code

## Function Design

**Size:** Functions are generally small and focused. `AppState` is the exception — it is very large and uses `// MARK: -` sections to organize its responsibilities by feature area. This is documented as a V1 limitation.

**Parameters:**
- Dependency injection via constructor parameters: `AppState(recentsStore:externalOpener:preferencesStore:commandPaletteRecentsStore:)` — all external dependencies injectable for testing
- Closures injected for side effects: `externalOpener: @escaping (URL) -> Bool`
- Clock injection for deterministic rate-guard tests: `state.scanClock = { now }`

**Return Values:**
- Async task handles returned and stored as `Task<Void, Never>?` properties so tests can `await` them
- Pure model methods return values directly; state-mutating methods have `Void` return

**Accessibility pattern:**
- Every custom UI component includes an `accessibilityLabel` parameter or property
- VoiceOver announcements posted via `postAccessibilityAnnouncement` for async state transitions
- Rate-guard pattern on scan progress announcements to avoid VoiceOver flooding

## Module Design

**Swift:**
- Single module: `SlateMac` — all app source in `Sources/SlateMac/`
- No barrel files (`index.swift`) — each type in its own file
- `@testable import SlateMac` used universally in test target — no explicit export annotations needed
- `final class` used for `ObservableObject`s and test fixtures; `struct` used for value types (`PrefsJsonStore`, `NoteEditorView`)

**Rust:**
- Workspace with two crates: `slate-core` (business logic) and `slate-uniffi` (FFI bridge)
- Tests in `#[cfg(test)] mod tests { ... }` blocks at the bottom of each source file
- `use super::*` pattern inside test modules — no separate test helper crates

## License Headers

Every Swift and Rust file begins with:
```swift
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
```
CI enforces this via `.github/workflows/license-headers.yml`.

---

*Convention analysis: 2026-05-28*
