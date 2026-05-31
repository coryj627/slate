# Testing Patterns

**Analysis Date:** 2026-05-28

## Test Framework

**Runner (Swift):**
- XCTest — built into the Swift toolchain
- Config: `apps/slate-mac/Package.swift` (`.testTarget(name: "SlateMacTests", dependencies: ["SlateMac"])`)
- All tests run under `@MainActor` when they test `AppState` (which is `@MainActor final class`)

**Runner (Rust):**
- Built-in `cargo test` — inline `#[cfg(test)] mod tests` blocks in each source file
- Benchmarks: Criterion (`crates/slate-core/benches/scan_bench.rs`)

**Assertion Libraries:**
- Swift: `XCTest` (`XCTAssertEqual`, `XCTAssertTrue`, `XCTAssertNil`, `XCTAssertNotNil`, `XCTFail`, `XCTUnwrap`)
- Rust: standard `assert_eq!`, `assert!`, `assert_ne!`

**Accessibility static analysis:**
- `a11y-check` (cvs-health/ios-swiftui-accessibility-techniques, pinned commit `bcaddd5`)
- 34 rules, 19 WCAG 2.2 criteria
- Minimum score enforced: **100/100** (zero errors permitted)

**Run Commands:**
```bash
# Swift tests
cd apps/slate-mac
DYLD_LIBRARY_PATH="$REPO_ROOT/target/debug" swift test

# Rust tests
cargo test --workspace

# Rust benchmarks (compile-only, as in CI)
cargo bench --no-run --workspace

# Rust benchmarks (run all)
cargo bench --workspace

# a11y static analysis
a11y-check apps/slate-mac/Sources/SlateMac
```

## Test File Organization

**Location:**
- Swift: separate test target `apps/slate-mac/Tests/SlateMacTests/` — not co-located with source
- Rust: inline `#[cfg(test)] mod tests { ... }` at the bottom of each source file (e.g., `links.rs:417`, `frontmatter.rs`)
- Rust benchmarks: `crates/slate-core/benches/` (separate files, not inline)

**Naming (Swift):**
- Unit tests: `<TypeName>Tests.swift` — e.g., `AppStateTests.swift`, `CommandRegistryTests.swift`, `RecentVaultsStoreTests.swift`
- Integration tests: `Milestone{Letter}IntegrationTests.swift` — one per shipped milestone: `MilestoneIIntegrationTests.swift`, `MilestoneQIntegrationTests.swift`
- Shared test helpers: plain Swift files without `Tests` suffix — `APCAContrast.swift`, `SwiftSourceStripping.swift`

**Directory structure:**
```
apps/slate-mac/Tests/SlateMacTests/
├── APCAContrast.swift                    # shared APCA helper (no XCTestCase)
├── SwiftSourceStripping.swift            # shared stripping helper
├── AppStateTests.swift                   # largest test file (~3854 lines)
├── CommandPaletteViewTests.swift
├── CommandRegistryTests.swift
├── MilestoneIIntegrationTests.swift
├── MilestoneJIntegrationTests.swift
├── MilestoneKIntegrationTests.swift
├── MilestoneLIntegrationTests.swift
├── MilestoneQIntegrationTests.swift
├── EditorSyntaxPaletteTests.swift
├── EditorSyntaxSpansTests.swift
├── EditorEmbedSpansTests.swift
├── NoteSectionSlicerTests.swift
├── NoteEditorCoordinatorTests.swift
├── PreferencesStoreTests.swift
├── PrefsJsonStoreTests.swift
├── RecentVaultsStoreTests.swift
├── CommandPaletteRecentsStoreTests.swift
├── HotkeySpokenTests.swift
├── SlateCommandsTests.swift
├── CodeBlockViewTests.swift
├── MathViewTests.swift
├── MermaidViewTests.swift
├── EmbedViewTests.swift
├── BibliographyPanelTests.swift
├── CitationsPanelTests.swift
├── CitationSummaryTests.swift
└── CloseVaultSheetParityTests.swift
```

## Test Structure

**Suite Organization (Swift):**
```swift
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import XCTest
@testable import SlateMac

/// Doc comment explaining what's covered and what's intentionally NOT covered.
@MainActor                    // required when testing @MainActor types
final class FooTests: XCTestCase {
    private var tempDir: URL!
    private var storeFile: URL!

    override func setUpWithError() throws {
        try super.setUpWithError()
        tempDir = FileManager.default.temporaryDirectory
            .appendingPathComponent("slate-foo-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: tempDir, withIntermediateDirectories: true)
    }

    override func tearDownWithError() throws {
        try? FileManager.default.removeItem(at: tempDir)
        try super.tearDownWithError()
    }

    // MARK: - Feature group name

    func testBehaviorUnderCondition() throws { ... }
    func testBehaviorUnderOtherCondition() async throws { ... }
}
```

**Suite Organization (Rust):**
```rust
#[cfg(test)]
mod tests {
    use super::*;

    // --- Feature group name ---

    #[test]
    fn behavior_under_condition() {
        let links = extract_links("see [[Alpha]] for context");
        assert_eq!(links.len(), 1);
        assert_eq!(links[0].kind, LinkKind::Wikilink);
    }
}
```

**Patterns:**
- Setup: `setUpWithError` creates a `UUID`-suffixed temp directory (e.g., `"slate-appstate-test-\(UUID().uuidString)"`) for isolation
- Teardown: `tearDownWithError` removes temp directory with `try?` (non-fatal) then calls `super`
- UserDefaults isolation: tests create a named suite (`UserDefaults(suiteName: "slate.milestone-q.\(UUID().uuidString)")`) and track suite names in `leasedSuiteNames: [String]` for cleanup
- All assertions inline — no separate assertion helpers except factory methods
- `// MARK: - Section` divides related test groups within a single large `XCTestCase`

## Mocking

**Framework:** No mocking framework — dependency injection via constructor closures and protocol conformances only

**Closure injection pattern:**
```swift
// In tests: inject a recording closure to observe side-effects
let state = AppState(
    recentsStore: store,
    externalOpener: { url in
        capturedURL = url  // record for assertion
        return true
    }
)
// Or inject a failing stub
let state = AppState(
    recentsStore: store,
    externalOpener: { _ in false }  // simulate failure
)
```

**Stub conformances (used in CommandRegistry and CommandPalette tests):**
```swift
// Minimal CountingAction fixture
final class CountingAction: CommandAction, @unchecked Sendable {
    private let lock = NSLock()
    private var _invocationCount: Int = 0

    var invocationCount: Int {
        lock.lock(); defer { lock.unlock() }; return _invocationCount
    }

    func invoke() throws {
        lock.lock(); _invocationCount += 1; lock.unlock()
    }
}

// StubAction variant in MilestoneQ and CommandPaletteView tests
final class StubAction: CommandAction, @unchecked Sendable {
    var invoked = false
    let failWith: CommandError?
    func invoke() throws {
        invoked = true
        if let err = failWith { throw err }
    }
}
```

**Clock injection for deterministic timing:**
```swift
// Rate-guard tests pin the clock
var now = Date(timeIntervalSinceReferenceDate: 0)
state.scanClock = { now }
now = now.addingTimeInterval(0.050)  // advance simulated time
```

**What to Mock:**
- External side effects: URL opening (`externalOpener`), file system operations use real temp dirs
- Time-sensitive guards: `scanClock` closure for rate-limit tests
- FFI callback interfaces: `CommandAction` protocol conformances

**What NOT to Mock:**
- File system — tests use real `FileManager` against temp directories
- The Rust FFI layer — tests link the actual `slate_uniffi` dylib (`DYLD_LIBRARY_PATH` set in CI)
- `UserDefaults` — tests use real named suites, cleaned up in tearDown

## Fixtures and Factories

**Factory method pattern:**
```swift
// Each test class has one or more private `make` helpers
private func makeAppState(
    seedEntries: [RecentVault] = [],
    externalOpener: @escaping (URL) -> Bool = { _ in true }
) throws -> AppState {
    let store = RecentVaultsStore(fileURL: storeFile)
    if !seedEntries.isEmpty {
        try store.save(seedEntries)
    }
    return AppState(recentsStore: store, externalOpener: externalOpener)
}

// Integration tests use a compound factory that returns multiple handles
private func makeAppStateWithLoadedNote(
    body: String,
    notePath: String = "note.md"
) async throws -> (AppState, URL) { ... }
```

**Vault fixture pattern:**
```swift
// Inline vault creation — content as string literals
let vault = tempDir.appendingPathComponent("vault-\(UUID().uuidString)")
try FileManager.default.createDirectory(at: vault, withIntermediateDirectories: true)
try Data("# Alpha".utf8).write(to: vault.appendingPathComponent("alpha.md"))
try Data("# Beta\n## Beta two".utf8).write(to: vault.appendingPathComponent("beta.md"))
```

**Rust test helpers:**
```rust
fn target(link: &ParsedLink) -> &str {
    &link.target_raw
}
// Used inline: assert_eq!(target(&links[0]), "Alpha");
```

**Location:** All fixtures are inline — no separate fixture files or JSON seed data on disk

## Coverage

**Requirements:** No explicit numeric coverage target in CI (no `--coverage` flag in workflows)

**Enforcement instead:** Integration milestone tests (`Milestone{Letter}IntegrationTests`) provide end-to-end coverage checkpoints. Each milestone test file documents what IS and IS NOT covered (e.g., "What's NOT covered here (rides the Milestone Q integration suite): SwiftUI shortcut routing...")

**View Coverage:**
```bash
# Not configured in CI; run locally with:
cd apps/slate-mac
DYLD_LIBRARY_PATH="$REPO_ROOT/target/debug" swift test --enable-code-coverage
```

## Test Types

**Unit Tests:**
- Scope: single type in isolation (e.g., `EditorSyntaxSpansTests`, `HotkeySpokenTests`, `NoteSectionSlicerTests`, `CommandRegistryTests`)
- Pattern: one test per behavioral case; test method names describe the exact behavior being verified
- Synchronous when possible; `async throws` only when awaiting a task

**Integration Tests:**
- Scope: full feature path through `AppState` from model to persistence, using real FFI and real file system
- One integration suite per shipped milestone: `MilestoneIIntegrationTests` through `MilestoneQIntegrationTests`
- Single large test method (e.g., `testMilestoneIEndToEndRoundTrip`, `testMilestoneQEndToEndCommandPalette`) with inline phased assertions
- Wall-clock budget documented in class doc comment: "under 5 seconds on local + CI runners"

**Contrast / Accessibility Tests:**
- `EditorSyntaxPaletteTests` and `CommandPaletteViewTests` include APCA contrast measurement tests
- Shared helper `APCAContrast.swift` implements APCA-W3 v0.1.9 G-4g constants — single source of truth
- Project standard: `|Lc| > 75` (APCA "small body text" bucket)
- Both light (Aqua) and dark (DarkAqua) appearances tested

**E2E Tests:** Not present — XCUITest infra not yet set up; documented in integration test comments as a follow-up issue

**Rust Benchmarks:**
- Criterion framework in `crates/slate-core/benches/scan_bench.rs`
- Three benchmark cases: cold scan, warm scan (cache primed), paged file list
- Vault sizes: 1,000 / 10,000 / 50,000 files
- CI compiles benchmarks on every PR (`cargo bench --no-run`) but does not run them

## Common Patterns

**Async Testing — await known task handles:**
```swift
// AppState exposes task handles as @Published properties for test observability
state.openVault(at: vault)
await state.scanTask?.value           // wait for scan to complete

state.selectedFilePath = "note.md"
await state.noteLoadTask?.value       // wait for note load
await state.linksLoadTask?.value      // wait for links + embed resolution
```

**Async Testing — debouncer drain:**
```swift
// When a debouncer is involved (search, 150ms), sleep then await
private func awaitSearch(_ state: AppState) async {
    try? await Task.sleep(nanoseconds: 400_000_000)  // 400ms > 150ms debounce
    await state.searchTask?.value
}
```

**Error Testing:**
```swift
// Error state checked on the observable, not through throws
state.selectedFilePath = "bad.md"
await state.noteLoadTask?.value

XCTAssertNil(state.currentNoteText)
XCTAssertNotNil(state.noteLoadError)
XCTAssertTrue(
    state.noteLoadError?.contains("UTF-8") == true,
    "expected UTF-8 in error message, got \(String(describing: state.noteLoadError))"
)
```

**Failure messages on assertions:**
```swift
// Custom failure messages are standard for non-obvious assertions
XCTAssertEqual(
    state.scanAnnouncementCount, 0,
    "openVault must reset the per-vault announcement counter"
)
XCTAssertFalse(state.isVaultOpen, "missing entries must not open a session")
```

**Regression anchoring:**
```swift
// Tests that pin bug fixes reference the PR or issue explicitly
// "Reproduces the bug Codoki flagged on PR 36: closeVault fires mid-scan..."
// "Regression for #90 PropertiesPanel flicker..."
// "Regression for the Codoki callout on PR 79..."
```

**Exhaustiveness tests:**
```swift
// Lock enum coverage so future cases fail loudly
for kind in SyntaxKind.allCases {
    XCTAssertEqual(
        EditorSyntaxPalette.color(for: kind, increaseContrast: true),
        NSColor.labelColor
    )
}
```

---

*Testing analysis: 2026-05-28*
