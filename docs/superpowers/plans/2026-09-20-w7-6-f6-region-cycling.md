# W7-6 F6 / Shift+F6 Shell Region Cycling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** F6 and Shift+F6 move keyboard focus through the Windows shell's seven regions (menu bar, Files, tab bar, editor, right-pane content, right-pane rail, status bar) with wrap, each landing announced through a typed core event.

**Architecture:** A pure ring function (`ShellRegionRing`) computes the next region from a small layout record; `WorkspaceViewModel` orchestrates a press through an `IShellRegionHost` the window implements (origin classification, landing, modal state), and announces through the existing `_announce` sink. The two existing chordless verbs `slate.workspace.focusNextPane` / `focusPreviousPane` are rebound to F6 / Shift+F6 and widened from editor splits to shell regions. New announcement copy lives in core (`A11yEvent::ShellRegionFocused`) and is mirrored in uniffi, Swift and C# per the W7-2 four-place rule.

**Tech Stack:** Rust (`slate-core`, `slate-uniffi`), uniffi-bindgen-cs (`generate-bindings.ps1`), C# / WPF net10.0-windows, xunit, FlaUI.UIA3 + Axe.Windows, PowerShell 7.

**Spec:** `docs/plans/18_windows_port/specs/w7_6_f6_region_cycling_spec.md` (read it first; every task below cites its section).

## Global Constraints

- Work in the worktree `C:\dev\slate\.claude\worktrees\nvda-accessibility-testing-f5696a`, on branch `w7-6-f6-region-cycling` (stacked on `w7-5-nvda-field-pass-fixes`, PR #1241). Windows app root: `apps/slate-windows` (all `dotnet` commands below run from there).
- `.gitattributes` is `* -text`: never let a tool flatten line endings. After ANY edit to a `.cs` file run `dotnet format SlateWindows.slnx --include <file>`; before each commit run `dotnet format SlateWindows.slnx --verify-no-changes` (must print nothing and exit 0). Do not truncate its output.
- Scripted edits: Python `read_bytes`/`write_bytes` or `read_text(encoding="utf-8")` with the same newline; bytes literals must be ASCII; avoid backslashes in heredoc Python regexes (`chr(92)`), or use the Edit tool then `dotnet format --include`.
- `chords.json` is a PROJECTION of `ChordTable.cs`; never hand-edit it. Regenerate with `SLATE_CHORDS_UPDATE=1 dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter FullyQualifiedName~ChordsJson_IsExactlyTheTablesProjection`, then re-run without the variable.
- Core announcement copy: four places — `crates/slate-core/src/a11y.rs` (variant, `render`, `corpus()`), `crates/slate-core/tests/fixtures/a11y/corpus.json` (regenerate with `SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y`, re-run clean), the `slate-uniffi` mirror (`crates/slate-uniffi/src/lib.rs`), the Swift mirror `apps/slate-mac/Tests/SlateMacTests/A11yCorpusCensusTests.swift` (one entry per line, starting with `.`) and the C# mirror `apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs`. Never `HostComposed` for this copy.
- Every new `AutomationProperties.AutomationId` in XAML must be registered in `docs/plans/18_windows_port/w_c_automation_ids.json` (`Source`, `Expression`, `Surface` = an existing `w_c_matrix.md` row title, here `"Main window and menu bar"`), sorted by (Source, Expression); `WcMatrixEvidenceCensus.EveryAutomationIdConstructionHasExactlyOneSurface` gates it.
- Commit messages: `W7-6: <what>` subject, body explaining why, and end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Mac keeps no F6 (spec §0); `macChord`/`mac` stay `null`.
- FlaUI journeys need an interactive desktop with no screen reader running; on this box NVDA is often up and three keystroke journeys fail identically on `main` (see `docs/plans/18_windows_port/reports/w7_5_nvda_field_pass_2026-09-20.md` context in memory). Run the new journey, record the outcome honestly, and let CI arbitrate.

---

### Task 1: Core event family `ShellRegionFocused` (Rust + three mirrors)

Spec §3. Adds the four-region announcement family; regions 2–5 reuse existing events.

**Files:**
- Modify: `crates/slate-core/src/a11y.rs` (enum near line 576, `render` near line 1715, `corpus()` near line 3465)
- Modify: `crates/slate-core/tests/fixtures/a11y/corpus.json` (regenerated, not hand-edited)
- Modify: `crates/slate-uniffi/src/lib.rs` (enum near line 8260, `From` mapping near line 10112)
- Modify: `apps/slate-mac/Tests/SlateMacTests/A11yCorpusCensusTests.swift` (corpus list, line ~52)
- Modify: `apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs` (`Corpus` array, line ~59)
- Regenerate: `apps/slate-windows/src/SlateUniffi/generated/slate_uniffi.cs` via `./generate-bindings.ps1`

**Interfaces:**
- Produces (Rust): `A11yEvent::ShellRegionFocused { region: ShellRegion }`, `pub enum ShellRegion { MenuBar, EmptyEditor, RightPaneRail, StatusBar { text: String } }`.
- Produces (C#, generated): `A11yEvent.ShellRegionFocused(ShellRegion Region)`; `ShellRegion.MenuBar()`, `ShellRegion.EmptyEditor()`, `ShellRegion.RightPaneRail()`, `ShellRegion.StatusBar(string Text)`.
- Rendered text: `"Menu bar."`, `"Editor pane. Empty."`, `"Right pane panels."`, `"Status bar. {text}."` and `"Status bar."` when text is empty. Priority: Medium (the `_ => A11yPriority::Medium` arm already covers it).

- [ ] **Step 1: Write the failing Rust golden rows**

In `crates/slate-core/src/a11y.rs`, inside `pub fn corpus()`, insert after the `LeafPanelShown { title: "Outline".into() }` entry:

```rust
        ShellRegionFocused {
            region: ShellRegion::MenuBar,
        },
        ShellRegionFocused {
            region: ShellRegion::EmptyEditor,
        },
        ShellRegionFocused {
            region: ShellRegion::RightPaneRail,
        },
        ShellRegionFocused {
            region: ShellRegion::StatusBar {
                text: "Scan finished: 90 files indexed.".into(),
            },
        },
        ShellRegionFocused {
            region: ShellRegion::StatusBar {
                text: String::new(),
            },
        },
```

- [ ] **Step 2: Run the core a11y tests to see them fail to compile**

Run (repo root): `cargo test -p slate-core a11y 2>&1 | tail -20`
Expected: compile error `no variant named ShellRegionFocused`.

- [ ] **Step 3: Add the variants and the render arms**

In the `A11yEvent` enum, directly after the `LeafPanelShown { title: String, },` variant:

```rust
    /// F6 / Shift+F6 landed on a shell region that has no event of its
    /// own (W7-6, #1240). Files, tab bar, editor and right-pane leaf
    /// landings reuse `FilesRegionFocused`, `TabFocused`,
    /// `EditorPaneFocused` and `LeafPanelShown`.
    ShellRegionFocused {
        region: ShellRegion,
    },
```

Above the `pub enum A11yEvent` declaration add:

```rust
/// The shell regions F6 cycling names that carry no other event.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ShellRegion {
    MenuBar,
    EmptyEditor,
    RightPaneRail,
    /// The status bar, with its current status text (may be empty).
    StatusBar { text: String },
}
```

In `pub fn render(&self)`, directly after the `LeafPanelShown { title } => format!("{title} panel."),` arm:

```rust
            ShellRegionFocused { region } => match region {
                ShellRegion::MenuBar => "Menu bar.".to_owned(),
                ShellRegion::EmptyEditor => "Editor pane. Empty.".to_owned(),
                ShellRegion::RightPaneRail => "Right pane panels.".to_owned(),
                ShellRegion::StatusBar { text } if text.trim().is_empty() => "Status bar.".to_owned(),
                ShellRegion::StatusBar { text } => format!("Status bar. {}.", text.trim().trim_end_matches('.')),
            },
```

- [ ] **Step 4: Regenerate the fixture, then prove the pin**

Run: `SLATE_REGENERATE_FIXTURES=1 cargo test -p slate-core a11y` (fails by design after rewriting), then `cargo test -p slate-core a11y`.
Expected: second run passes; `git diff crates/slate-core/tests/fixtures/a11y/corpus.json` shows exactly five added entries with the texts in Interfaces.

- [ ] **Step 5: Mirror in uniffi**

In `crates/slate-uniffi/src/lib.rs`, in `pub enum A11yEvent` (uniffi mirror) after `LeafPanelShown { title: String, },` add `ShellRegionFocused { region: ShellRegion },`, and above that enum add:

```rust
/// 1:1 mirror of `slate_core::a11y::ShellRegion`.
#[derive(Debug, Clone, PartialEq, Eq, uniffi::Enum)]
pub enum ShellRegion {
    MenuBar,
    EmptyEditor,
    RightPaneRail,
    StatusBar { text: String },
}

impl From<ShellRegion> for core::a11y::ShellRegion {
    fn from(r: ShellRegion) -> Self {
        match r {
            ShellRegion::MenuBar => Self::MenuBar,
            ShellRegion::EmptyEditor => Self::EmptyEditor,
            ShellRegion::RightPaneRail => Self::RightPaneRail,
            ShellRegion::StatusBar { text } => Self::StatusBar { text },
        }
    }
}
```

In `impl From<A11yEvent> for core::a11y::A11yEvent`, after the `F::LeafPanelShown { title } => C::LeafPanelShown { title },` arm add `F::ShellRegionFocused { region } => C::ShellRegionFocused { region: region.into() },`. Grep `LeafPanelShown` in the file; if a second mapping direction exists, mirror it the same way.

Run: `cargo test -p slate-uniffi 2>&1 | tail -15`
Expected: `the_mac_corpus_mirror_lists_every_event_in_order` and `the_windows_corpus_mirror_lists_every_event_in_order` FAIL naming the missing entries (the Rust side compiles).

- [ ] **Step 6: Mirror in Swift and C#**

Swift, `A11yCorpusCensusTests.swift`, after `.leafPanelShown(title: "Outline"),` add one entry per line:

```swift
            .shellRegionFocused(region: .menuBar),
            .shellRegionFocused(region: .emptyEditor),
            .shellRegionFocused(region: .rightPaneRail),
            .shellRegionFocused(region: .statusBar(text: "Scan finished: 90 files indexed.")),
            .shellRegionFocused(region: .statusBar(text: "")),
```

C#, `A11yCorpusCensus.cs`, after `new A11yEvent.LeafPanelShown(Title: "Outline"),` add:

```csharp
        new A11yEvent.ShellRegionFocused(Region: new ShellRegion.MenuBar()),
        new A11yEvent.ShellRegionFocused(Region: new ShellRegion.EmptyEditor()),
        new A11yEvent.ShellRegionFocused(Region: new ShellRegion.RightPaneRail()),
        new A11yEvent.ShellRegionFocused(Region: new ShellRegion.StatusBar(Text: "Scan finished: 90 files indexed.")),
        new A11yEvent.ShellRegionFocused(Region: new ShellRegion.StatusBar(Text: "")),
```

Run: `cargo test -p slate-uniffi 2>&1 | tail -5` → both tripwires pass.
Regenerate bindings (from `apps/slate-windows`): `pwsh -NoProfile -Command ./generate-bindings.ps1`.
Run: `dotnet format SlateWindows.slnx --include tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs` then `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter FullyQualifiedName~A11yCorpusCensus`
Expected: PASS (the C# census renders through the FFI and matches the regenerated fixture).

- [ ] **Step 7: Commit**

```bash
git add crates/slate-core/src/a11y.rs crates/slate-core/tests/fixtures/a11y/corpus.json crates/slate-uniffi/src/lib.rs apps/slate-mac/Tests/SlateMacTests/A11yCorpusCensusTests.swift apps/slate-windows/tests/SlateWindows.Tests/Censuses/A11yCorpusCensus.cs
git commit -m "W7-6: ShellRegionFocused announcement family in core and its three mirrors (#1240)" -m "F6 cycling lands on four regions that had no event of their own: menu bar, empty editor pane, right-pane rail, status bar. Four places per W7-2 B2; the other regions reuse FilesRegionFocused, TabFocused, EditorPaneFocused and LeafPanelShown." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

(`generated/slate_uniffi.cs` is build output; check `git status` — if it is tracked, include it; if ignored, leave it.)

---

### Task 2: The pure ring (`ShellRegionRing`)

Spec §1 (order, presence rules, wrap, origin → "before 1").

**Files:**
- Create: `apps/slate-windows/src/SlateWindows/ShellRegionRing.cs`
- Test: `apps/slate-windows/tests/SlateWindows.Tests/ShellRegionRingTests.cs`

**Interfaces:**
- Produces:
  - `internal enum ShellRegionKind { MenuBar, Files, TabBar, Editor, EmptyEditor, RightPaneContent, RightPaneRail, StatusBar }`
  - `internal readonly record struct ShellRegionLayout(bool HasTabs, bool RightPaneVisible, bool RightPaneHasContentStop);`
  - `internal static class ShellRegionRing { public static IReadOnlyList<ShellRegionKind> Ring(ShellRegionLayout layout); public static ShellRegionKind Next(ShellRegionLayout layout, ShellRegionKind? origin, int direction); }` — `direction` is `+1` (F6) or `-1` (Shift+F6); a null origin means "before the first region" so `Next(_, null, +1)` is the ring's first element and `Next(_, null, -1)` its last.

- [ ] **Step 1: Write the failing tests**

`tests/SlateWindows.Tests/ShellRegionRingTests.cs`:

```csharp
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>W7-6 (#1240) spec §1: the F6 ring is a pure function of the
/// shell's layout, so every shape is pinned here without a window.</summary>
public sealed class ShellRegionRingTests
{
    private static readonly ShellRegionLayout Full = new(HasTabs: true, RightPaneVisible: true, RightPaneHasContentStop: true);

    [Fact]
    public void TheFullRingHasSevenRegionsInSpecOrder() =>
        Assert.Equal(
            [
                ShellRegionKind.MenuBar,
                ShellRegionKind.Files,
                ShellRegionKind.TabBar,
                ShellRegionKind.Editor,
                ShellRegionKind.RightPaneContent,
                ShellRegionKind.RightPaneRail,
                ShellRegionKind.StatusBar,
            ],
            ShellRegionRing.Ring(Full));

    [Fact]
    public void NoTabsReplacesTabBarAndEditorWithTheEmptyPane() =>
        Assert.Equal(
            [
                ShellRegionKind.MenuBar,
                ShellRegionKind.Files,
                ShellRegionKind.EmptyEditor,
                ShellRegionKind.RightPaneContent,
                ShellRegionKind.RightPaneRail,
                ShellRegionKind.StatusBar,
            ],
            ShellRegionRing.Ring(Full with { HasTabs = false }));

    [Fact]
    public void AHiddenRightPaneDropsBothRightStops() =>
        Assert.Equal(
            [ShellRegionKind.MenuBar, ShellRegionKind.Files, ShellRegionKind.TabBar, ShellRegionKind.Editor, ShellRegionKind.StatusBar],
            ShellRegionRing.Ring(Full with { RightPaneVisible = false }));

    [Fact]
    public void ALeafWithoutAStopDropsOnlyTheContentRegion() =>
        Assert.Equal(
            [ShellRegionKind.MenuBar, ShellRegionKind.Files, ShellRegionKind.TabBar, ShellRegionKind.Editor, ShellRegionKind.RightPaneRail, ShellRegionKind.StatusBar],
            ShellRegionRing.Ring(Full with { RightPaneHasContentStop = false }));

    [Fact]
    public void NextWrapsInBothDirections()
    {
        Assert.Equal(ShellRegionKind.MenuBar, ShellRegionRing.Next(Full, ShellRegionKind.StatusBar, +1));
        Assert.Equal(ShellRegionKind.StatusBar, ShellRegionRing.Next(Full, ShellRegionKind.MenuBar, -1));
        Assert.Equal(ShellRegionKind.TabBar, ShellRegionRing.Next(Full, ShellRegionKind.Files, +1));
        Assert.Equal(ShellRegionKind.Files, ShellRegionRing.Next(Full, ShellRegionKind.TabBar, -1));
    }

    [Fact]
    public void AnUnknownOriginIsBeforeTheFirstRegion()
    {
        Assert.Equal(ShellRegionKind.MenuBar, ShellRegionRing.Next(Full, null, +1));
        Assert.Equal(ShellRegionKind.StatusBar, ShellRegionRing.Next(Full, null, -1));
    }

    [Fact]
    public void AnOriginAbsentFromTheRingResolvesToItsNeighbour()
    {
        // Focus was in the right-pane content when the leaf lost its stop:
        // forward goes to the rail, backward to the editor.
        ShellRegionLayout layout = Full with { RightPaneHasContentStop = false };
        Assert.Equal(ShellRegionKind.RightPaneRail, ShellRegionRing.Next(layout, ShellRegionKind.RightPaneContent, +1));
        Assert.Equal(ShellRegionKind.Editor, ShellRegionRing.Next(layout, ShellRegionKind.RightPaneContent, -1));
        // Focus was on the tab bar when the last tab closed.
        ShellRegionLayout noTabs = Full with { HasTabs = false };
        Assert.Equal(ShellRegionKind.EmptyEditor, ShellRegionRing.Next(noTabs, ShellRegionKind.TabBar, +1));
        Assert.Equal(ShellRegionKind.Files, ShellRegionRing.Next(noTabs, ShellRegionKind.Editor, -1));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter FullyQualifiedName~ShellRegionRingTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: compile errors (`ShellRegionKind` not found).

- [ ] **Step 3: Implement the ring**

`src/SlateWindows/ShellRegionRing.cs`:

```csharp
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>The shell regions F6 / Shift+F6 cycle through (W7-6, #1240,
/// spec §1), in ring order. <see cref="EmptyEditor"/> stands in for
/// <see cref="TabBar"/> + <see cref="Editor"/> when no tab is open.</summary>
internal enum ShellRegionKind
{
    MenuBar,
    Files,
    TabBar,
    Editor,
    EmptyEditor,
    RightPaneContent,
    RightPaneRail,
    StatusBar,
}

/// <summary>What the ring depends on, read live on every press.</summary>
internal readonly record struct ShellRegionLayout(
    bool HasTabs,
    bool RightPaneVisible,
    bool RightPaneHasContentStop);

/// <summary>The F6 ring as a pure function: no window, no focus, no
/// timing — <c>ShellRegionRingTests</c> pins every layout shape.</summary>
internal static class ShellRegionRing
{
    /// <summary>The spec's full order; presence rules remove entries.</summary>
    private static readonly ShellRegionKind[] Order =
    [
        ShellRegionKind.MenuBar,
        ShellRegionKind.Files,
        ShellRegionKind.TabBar,
        ShellRegionKind.Editor,
        ShellRegionKind.EmptyEditor,
        ShellRegionKind.RightPaneContent,
        ShellRegionKind.RightPaneRail,
        ShellRegionKind.StatusBar,
    ];

    public static IReadOnlyList<ShellRegionKind> Ring(ShellRegionLayout layout) =>
        Order.Where(region => IsPresent(region, layout)).ToArray();

    /// <summary>The region after (<paramref name="direction"/> = +1) or
    /// before (-1) <paramref name="origin"/>, wrapping. A null origin, or
    /// one no longer in the ring, is placed by its slot in the full order
    /// so a press from a vanished region still lands on its neighbour.</summary>
    public static ShellRegionKind Next(ShellRegionLayout layout, ShellRegionKind? origin, int direction)
    {
        IReadOnlyList<ShellRegionKind> ring = Ring(layout);
        int step = direction < 0 ? -1 : 1;
        if (origin is null)
        {
            return step > 0 ? ring[0] : ring[^1];
        }

        int index = IndexOf(ring, origin.Value);
        if (index >= 0)
        {
            return ring[(index + step + ring.Count) % ring.Count];
        }

        // Absent origin: walk the full order from its slot in the pressed
        // direction until a present region is met (wrapping).
        int slot = Array.IndexOf(Order, origin.Value);
        for (int probe = 1; probe <= Order.Length; probe++)
        {
            ShellRegionKind candidate = Order[(slot + probe * step + Order.Length * probe) % Order.Length];
            if (IsPresent(candidate, layout))
            {
                return candidate;
            }
        }

        return step > 0 ? ring[0] : ring[^1];
    }

    private static bool IsPresent(ShellRegionKind region, ShellRegionLayout layout) => region switch
    {
        ShellRegionKind.TabBar or ShellRegionKind.Editor => layout.HasTabs,
        ShellRegionKind.EmptyEditor => !layout.HasTabs,
        ShellRegionKind.RightPaneContent => layout.RightPaneVisible && layout.RightPaneHasContentStop,
        ShellRegionKind.RightPaneRail => layout.RightPaneVisible,
        _ => true,
    };

    private static int IndexOf(IReadOnlyList<ShellRegionKind> ring, ShellRegionKind region)
    {
        for (int index = 0; index < ring.Count; index++)
        {
            if (ring[index] == region)
            {
                return index;
            }
        }

        return -1;
    }
}
```

- [ ] **Step 4: Format and run**

Run: `dotnet format SlateWindows.slnx --include src/SlateWindows/ShellRegionRing.cs tests/SlateWindows.Tests/ShellRegionRingTests.cs` then the Step 2 command.
Expected: 7 passed.

- [ ] **Step 5: Commit**

```bash
git add apps/slate-windows/src/SlateWindows/ShellRegionRing.cs apps/slate-windows/tests/SlateWindows.Tests/ShellRegionRingTests.cs
git commit -m "W7-6: the F6 shell-region ring as a pure function (#1240)" -m "Spec §1: seven regions in order, the empty-pane stand-in, hidden right pane and stop-less leaf removals, wrap both ways, and a vanished origin resolving to its neighbour." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: `IShellRegionHost` and the view-model orchestration

Spec §2 (`CanExecute` true), §3 (which event per region), §4 (modal no-op, fall-through at most one ring).

**Files:**
- Create: `apps/slate-windows/src/SlateWindows/IShellRegionHost.cs`
- Modify: `apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs:1734-1735` (the two command constructions)
- Modify: `apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Layout.cs` (replace `FocusPane(int)` near line 594 with `CycleShellRegion(int)`)
- Test: `apps/slate-windows/tests/SlateWindows.Tests/ShellRegionCycleTests.cs`

**Interfaces:**
- Consumes: `ShellRegionKind`, `ShellRegionLayout`, `ShellRegionRing` (Task 2); `A11yEvent.ShellRegionFocused`, `ShellRegion.*` (Task 1).
- Produces:
  ```csharp
  internal interface IShellRegionHost
  {
      bool ModalSurfaceOpen { get; }
      bool RightPaneHasContentStop { get; }
      string StatusText { get; }
      ShellRegionKind? FocusedRegion();
      bool TryLand(ShellRegionKind region);
  }
  ```
  and on `WorkspaceViewModel`: `internal IShellRegionHost? ShellRegionHost { get; set; }`. `FocusNextPaneCommand` / `FocusPreviousPaneCommand` keep their names (the XAML, palette and chord table bind to them) but now cycle regions.

- [ ] **Step 1: Write the failing tests**

`tests/SlateWindows.Tests/ShellRegionCycleTests.cs` (the fixture pattern is `W1WorkspaceModelTests`):

```csharp
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>W7-6 (#1240) spec §2–§4: a press asks the host where focus is,
/// lands the ring's next region through the host, and announces it —
/// the modal no-op and the fall-through are the view model's, not the
/// window's.</summary>
public sealed class ShellRegionCycleTests
{
    private sealed class FakeHost : IShellRegionHost
    {
        public bool ModalSurfaceOpen { get; set; }
        public bool RightPaneHasContentStop { get; set; } = true;
        public string StatusText { get; set; } = "Scan finished: 2 files indexed.";
        public ShellRegionKind? Focused { get; set; }
        public HashSet<ShellRegionKind> Refuses { get; } = [];
        public List<ShellRegionKind> Landed { get; } = [];

        public ShellRegionKind? FocusedRegion() => Focused;

        public bool TryLand(ShellRegionKind region)
        {
            Landed.Add(region);
            if (Refuses.Contains(region))
            {
                return false;
            }

            Focused = region;
            return true;
        }
    }

    private static (WorkspaceViewModel Workspace, FakeHost Host, List<A11yEvent> Announced, FixtureVault Fixture, VaultSession Session) Open()
    {
        FixtureVault fixture = FixtureVault.Create(2, "shell-region-cycle");
        VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announced = new List<A11yEvent>();
        var workspace = new WorkspaceViewModel(session, fixture.Root, () => [], announced.Add);
        var host = new FakeHost();
        workspace.ShellRegionHost = host;
        return (workspace, host, announced, fixture, session);
    }

    [Fact]
    public void ForwardFromFilesWithATabLandsOnTheTabBarAndAnnouncesIt()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.Files;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.TabBar], host.Landed);
            A11yEvent.TabFocused tab = Assert.IsType<A11yEvent.TabFocused>(Assert.Single(announced));
            Assert.Equal("Tab bar. ", tab.Prefix);
            Assert.Equal("note0.md", tab.Filename);
        }
    }

    [Fact]
    public void BackwardFromTheMenuBarWrapsToTheStatusBarWithItsText()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.MenuBar;

            workspace.FocusPreviousPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.StatusBar], host.Landed);
            A11yEvent.ShellRegionFocused region = Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            ShellRegion.StatusBar status = Assert.IsType<ShellRegion.StatusBar>(region.Region);
            Assert.Equal("Scan finished: 2 files indexed.", status.Text);
        }
    }

    [Fact]
    public void NoTabsLandsOnTheEmptyEditorPane()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.Files;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.EmptyEditor], host.Landed);
            A11yEvent.ShellRegionFocused region = Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            Assert.IsType<ShellRegion.EmptyEditor>(region.Region);
        }
    }

    [Fact]
    public void AHiddenRightPaneIsSkippedNotRevealed()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            workspace.IsRightPaneVisible = false;
            announced.Clear();
            host.Focused = ShellRegionKind.Editor;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.StatusBar], host.Landed);
            Assert.False(workspace.IsRightPaneVisible);
        }
    }

    [Fact]
    public void ARefusedLandingFallsThroughToTheNextRegion()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            workspace.OpenPath("note0.md");
            announced.Clear();
            host.Focused = ShellRegionKind.Editor;
            host.Refuses.Add(ShellRegionKind.RightPaneContent);

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal([ShellRegionKind.RightPaneContent, ShellRegionKind.RightPaneRail], host.Landed);
            A11yEvent.ShellRegionFocused region = Assert.IsType<A11yEvent.ShellRegionFocused>(Assert.Single(announced));
            Assert.IsType<ShellRegion.RightPaneRail>(region.Region);
        }
    }

    [Fact]
    public void EveryLandingRefusedLeavesFocusAndSaysNothing()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.Focused = ShellRegionKind.Files;
            foreach (ShellRegionKind region in Enum.GetValues<ShellRegionKind>())
            {
                host.Refuses.Add(region);
            }

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Equal(ShellRegionRing.Ring(new ShellRegionLayout(false, true, true)).Count, host.Landed.Count);
            Assert.Empty(announced);
        }
    }

    [Fact]
    public void AModalSurfaceMakesThePressANoOp()
    {
        (WorkspaceViewModel workspace, FakeHost host, List<A11yEvent> announced, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            host.ModalSurfaceOpen = true;
            host.Focused = ShellRegionKind.Files;

            workspace.FocusNextPaneCommand.Execute(null);

            Assert.Empty(host.Landed);
            Assert.Empty(announced);
        }
    }

    [Fact]
    public void TheVerbsAreAlwaysExecutable()
    {
        (WorkspaceViewModel workspace, FakeHost _, List<A11yEvent> _, FixtureVault fixture, VaultSession session) = Open();
        using (fixture)
        using (session)
        using (workspace)
        {
            Assert.True(workspace.FocusNextPaneCommand.CanExecute(null));
            Assert.True(workspace.FocusPreviousPaneCommand.CanExecute(null));
        }
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter FullyQualifiedName~ShellRegionCycleTests 2>&1 | grep -E "error CS|Passed!|Failed!"`
Expected: compile errors (`IShellRegionHost`, `ShellRegionHost` not found).

- [ ] **Step 3: Add the host interface**

`src/SlateWindows/IShellRegionHost.cs`:

```csharp
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows;

/// <summary>What the window answers for an F6 press (W7-6, #1240): where
/// focus is, whether a modal surface owns the keys, and whether a region
/// can take focus. The view model owns the ring and the announcement;
/// the window owns elements. <c>ShellRegionCycleTests</c> drives this
/// through a fake.</summary>
internal interface IShellRegionHost
{
    /// <summary>A sheet, overlay or palette is up: F6 is a no-op (spec §4).</summary>
    bool ModalSurfaceOpen { get; }

    /// <summary>The shown right-pane leaf has a focusable first stop.</summary>
    bool RightPaneHasContentStop { get; }

    /// <summary>The status bar's current text, spoken on that landing.</summary>
    string StatusText { get; }

    /// <summary>The region containing keyboard focus, or null when focus
    /// is on the window root, an overlay, or nowhere.</summary>
    ShellRegionKind? FocusedRegion();

    /// <summary>Put keyboard focus in the region; false when it cannot
    /// (the view model then tries the next region).</summary>
    bool TryLand(ShellRegionKind region);
}
```

- [ ] **Step 4: Rewire the commands and replace `FocusPane`**

In `WorkspaceViewModel.cs` replace the two lines

```csharp
        FocusNextPaneCommand = new RelayCommand(_ => FocusPane(1), _ => Groups.Count > 1);
        FocusPreviousPaneCommand = new RelayCommand(_ => FocusPane(-1), _ => Groups.Count > 1);
```

with

```csharp
        // W7-6 (#1240): F6 / Shift+F6 cycle SHELL REGIONS, not editor
        // splits (Ctrl+Alt+Arrows keep the splits). Always executable: the
        // ring exists whenever the workspace does; the modal no-op is
        // decided per press, not by CanExecute.
        FocusNextPaneCommand = new RelayCommand(_ => CycleShellRegion(1), _ => true);
        FocusPreviousPaneCommand = new RelayCommand(_ => CycleShellRegion(-1), _ => true);
```

and add, next to `public ICommand FocusNextPaneCommand { get; }`:

```csharp
    /// <summary>The window's answers for F6 (W7-6); null until the window
    /// attaches, in which case a press does nothing.</summary>
    internal IShellRegionHost? ShellRegionHost { get; set; }
```

In `WorkspaceViewModel.Layout.cs` replace the whole `private void FocusPane(int delta)` method with:

```csharp
    /// <summary>One F6 (<paramref name="direction"/> +1) or Shift+F6 (-1)
    /// press: spec §1 ring, §3 announcements, §4 no-op and fall-through.</summary>
    private void CycleShellRegion(int direction)
    {
        if (ShellRegionHost is not { } host || host.ModalSurfaceOpen)
        {
            return;
        }

        var layout = new ShellRegionLayout(
            HasTabs: ActiveGroup.Tabs.Count > 0,
            RightPaneVisible: IsRightPaneVisible,
            RightPaneHasContentStop: host.RightPaneHasContentStop);
        ShellRegionKind? current = host.FocusedRegion();
        int attempts = ShellRegionRing.Ring(layout).Count;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            ShellRegionKind target = ShellRegionRing.Next(layout, current, direction);
            if (host.TryLand(target))
            {
                AnnounceShellRegion(target, host);
                return;
            }

            current = target;
        }
    }

    private void AnnounceShellRegion(ShellRegionKind region, IShellRegionHost host)
    {
        switch (region)
        {
            case ShellRegionKind.MenuBar:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.MenuBar()));
                break;
            case ShellRegionKind.Files:
                _announce(new A11yEvent.FilesRegionFocused());
                break;
            case ShellRegionKind.TabBar:
                WorkspaceTabViewModel? tab = ActiveGroup.ActiveTab;
                _announce(new A11yEvent.TabFocused(
                    Prefix: "Tab bar. ",
                    Filename: tab?.Title ?? string.Empty,
                    Index: (uint)Math.Max(1, tab is null ? 1 : ActiveGroup.Tabs.IndexOf(tab) + 1),
                    Count: (uint)ActiveGroup.Tabs.Count));
                break;
            case ShellRegionKind.Editor:
                AnnounceActivePane();
                break;
            case ShellRegionKind.EmptyEditor:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.EmptyEditor()));
                break;
            case ShellRegionKind.RightPaneContent:
                _announce(new A11yEvent.LeafPanelShown(ActiveLeaf.Title));
                break;
            case ShellRegionKind.RightPaneRail:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.RightPaneRail()));
                break;
            case ShellRegionKind.StatusBar:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.StatusBar(host.StatusText)));
                break;
        }
    }
```

If `WorkspaceViewModel.Layout.cs` lacks `using uniffi.slate_uniffi;`, the existing `A11yEvent` references show which alias is in scope; use the same.

- [ ] **Step 5: Format, run the new tests and the neighbours**

Run: `dotnet format SlateWindows.slnx --include src/SlateWindows/IShellRegionHost.cs src/SlateWindows/WorkspaceViewModel.cs src/SlateWindows/WorkspaceViewModel.Layout.cs tests/SlateWindows.Tests/ShellRegionCycleTests.cs`
Run: `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter "FullyQualifiedName~ShellRegionCycleTests|FullyQualifiedName~W1WorkspaceModelTests|FullyQualifiedName~ChordTableTests" 2>&1 | grep -E "\[FAIL\]|Passed!|Failed!"`
Expected: all pass. `W1WorkspaceModelTests` still calls `FocusPreviousPaneCommand.Execute(null)` with no host attached: that is now a no-op, and its following assertions only read `ActiveGroup.ActiveTab`, which is unchanged. If it fails, read the failure before touching the test.

- [ ] **Step 6: Commit**

```bash
git add apps/slate-windows/src/SlateWindows/IShellRegionHost.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Layout.cs apps/slate-windows/tests/SlateWindows.Tests/ShellRegionCycleTests.cs
git commit -m "W7-6: focusNextPane/focusPreviousPane cycle shell regions through IShellRegionHost (#1240)" -m "The view model owns the ring and the announcement; the window answers origin, landing and modal state. Spec §2 CanExecute, §3 events per region, §4 modal no-op and one-ring fall-through, all pinned by ShellRegionCycleTests." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: The window host, the chords, the menu, the help and the status bar

Spec §1 (landing elements), §2 (chords, KeyBindings, menu, help), §4 (menu-mode F6).

**Files:**
- Create: `apps/slate-windows/src/SlateWindows/MainWindow.ShellRegions.cs`
- Modify: `apps/slate-windows/src/SlateWindows/MainWindow.xaml` (InputBindings ~line 29–58; `MainMenu` element; Workspace menu after `Focus Pane _Below` line 264; the FilesPane landmark; `ContentPaneBorder` line 1062; the right-pane `<Grid>` under `RightPaneBorder`; the `<StatusBar>` at line 668)
- Modify: `apps/slate-windows/src/SlateWindows/MainWindow.xaml.cs:239-242` (`ViewModel_WorkspaceReady`)
- Modify: `apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs:768-773`
- Modify: `apps/slate-windows/src/SlateWindows/Commands/NavigationHelp.cs`
- Modify: `apps/slate-windows/tests/SlateWindows.Tests/MacCatalogParityTests.cs:255-258`
- Modify: `apps/slate-windows/chords.json` (regenerated)
- Modify: `docs/plans/18_windows_port/w_c_automation_ids.json`
- Test: `apps/slate-windows/tests/SlateWindows.Tests/Censuses/MenuBarCensus.cs` (add one fact), `ChordTableTests`, `CommandDriftTests`, `WcMatrixEvidenceCensus`

**Interfaces:**
- Consumes: `IShellRegionHost`, `ShellRegionKind` (Task 3); `WorkspaceViewModel.ShellRegionHost`.
- Produces: XAML names `FilesPaneBorder`, `RightPaneLeafHost`, `ShellStatusBar`; AutomationIds `StatusBar`, `WorkspaceFocusNextRegionMenuItem`, `WorkspaceFocusPreviousRegionMenuItem`; `NavigationHelp.Shell`; chord rows `F6` / `Shift+F6` with labels "Focus Next Region" / "Focus Previous Region".

- [ ] **Step 1: Write the failing source census fact**

Append to `MenuBarCensus`:

```csharp
    /// <summary>W7-6 (#1240): F6 and Shift+F6 are delivered by window
    /// KeyBindings bound to the two region verbs, and the menu bar hands
    /// F6 on while in menu mode (WPF's menu mode would otherwise keep it).</summary>
    [Fact]
    public void F6AndShiftF6AreBoundToTheRegionVerbs()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        var bindings = window.Descendants()
            .Where(element => element.Name.LocalName == "KeyBinding" && (string?)element.Attribute("Key") == "F6")
            .ToDictionary(
                element => (string?)element.Attribute("Modifiers") ?? "",
                element => (string?)element.Attribute("Command"));
        Assert.Equal("{Binding Workspace.FocusNextPaneCommand}", bindings[""]);
        Assert.Equal("{Binding Workspace.FocusPreviousPaneCommand}", bindings["Shift"]);
        Assert.Equal("MainMenu_PreviewKeyDown", (string?)MainMenu().Attribute("PreviewKeyDown"));
    }
```

Run: `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter FullyQualifiedName~MenuBarCensus 2>&1 | grep -E "\[FAIL\]|Passed!|Failed!"` → the new fact FAILS (no F6 binding yet).

- [ ] **Step 2: XAML — bindings, menu bar handler, menu items, landmarks, status bar**

In `Window.InputBindings` add after the `Key="Left" Modifiers="Control+Alt"` binding:

```xml
        <!-- W7-6 (#1240): F6 / Shift+F6 cycle the shell regions; the verbs
             were the PR-4 orphans focusNextPane / focusPreviousPane. -->
        <KeyBinding Key="F6" Command="{Binding Workspace.FocusNextPaneCommand}" />
        <KeyBinding Key="F6" Modifiers="Shift" Command="{Binding Workspace.FocusPreviousPaneCommand}" />
```

On the `<Menu x:Name="MainMenu" …>` element add the attribute `PreviewKeyDown="MainMenu_PreviewKeyDown"`.

In the Workspace menu, after the `Focus Pane _Below` item add:

```xml
                <MenuItem Header="Focus Next _Region"
                          AutomationProperties.AutomationId="WorkspaceFocusNextRegionMenuItem"
                          InputGestureText="{cmd:ChordText slate.workspace.focusNextPane}" AutomationProperties.AcceleratorKey="{Binding InputGestureText, RelativeSource={RelativeSource Self}}"
                          Command="{Binding Workspace.FocusNextPaneCommand}" />
                <MenuItem Header="Focus Previous Re_gion"
                          AutomationProperties.AutomationId="WorkspaceFocusPreviousRegionMenuItem"
                          InputGestureText="{cmd:ChordText slate.workspace.focusPreviousPane}" AutomationProperties.AcceleratorKey="{Binding InputGestureText, RelativeSource={RelativeSource Self}}"
                          Command="{Binding Workspace.FocusPreviousPaneCommand}" />
```

Find the `local:AutomationLandmarkBorder` carrying `AutomationProperties.AutomationId="FilesPane"` (grep the id); if it has no `x:Name`, add `x:Name="FilesPaneBorder"`. On `ContentPaneBorder` add `Focusable="True"` (an `AutomationLandmarkBorder` is a `Border`, whose default is not focusable; `TabControl`s inside remain the normal stops — the border is only focused by F6's empty-pane landing). On the `<Grid>` directly inside `RightPaneBorder` (the one with `ColumnDefinition Width="92"`) add `x:Name="RightPaneLeafHost"`.

Replace `<StatusBar DockPanel.Dock="Bottom">` with:

```xml
        <!-- W7-6 (#1240): the status bar is the ring's last region — a
             named, focusable stop whose landing speaks the status text;
             its HelpText is the shell's F6 help. -->
        <StatusBar x:Name="ShellStatusBar"
                   DockPanel.Dock="Bottom"
                   Focusable="True"
                   AutomationProperties.AutomationId="StatusBar"
                   AutomationProperties.Name="Status bar"
                   AutomationProperties.HelpText="{x:Static cmd:NavigationHelp.Shell}">
```

- [ ] **Step 3: The window host**

`src/SlateWindows/MainWindow.ShellRegions.cs`:

```csharp
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>The window's half of F6 cycling (W7-6, #1240): which region
/// holds focus, and how each region takes it. The ring and the speech
/// are the view model's (<see cref="WorkspaceViewModel.ShellRegionHost"/>).</summary>
public partial class MainWindow : IShellRegionHost
{
    bool IShellRegionHost.ModalSurfaceOpen =>
        ModalSurfaces.TopmostOpen(CurrentModalSurfaceState) is not null;

    string IShellRegionHost.StatusText => _viewModel.StatusText ?? string.Empty;

    bool IShellRegionHost.RightPaneHasContentStop =>
        _viewModel.Workspace is WorkspaceViewModel workspace
        && (workspace.ConnectionsLeafIsActive()
            || (workspace.IsGraphInspectorShown && GraphInspectorSurface.IsVisible)
            || VisibleLeafBody() is { } body && FirstFocusable(body) is not null);

    ShellRegionKind? IShellRegionHost.FocusedRegion()
    {
        if (Keyboard.FocusedElement is not DependencyObject focused
            || !ReferenceEquals(Window.GetWindow(focused), this))
        {
            return null;
        }

        if (IsWithin(focused, MainMenu))
        {
            return ShellRegionKind.MenuBar;
        }

        if (IsWithin(focused, FilesPaneBorder))
        {
            return ShellRegionKind.Files;
        }

        if (IsWithin(focused, ContentPaneBorder))
        {
            if (ReferenceEquals(focused, ContentPaneBorder))
            {
                return ShellRegionKind.EmptyEditor;
            }

            return HasAncestor<TabItem>(focused) || HasAncestor<TabPanel>(focused)
                ? ShellRegionKind.TabBar
                : ShellRegionKind.Editor;
        }

        if (IsWithin(focused, RightPaneBorder))
        {
            return IsWithin(focused, RightPaneLeavesList)
                ? ShellRegionKind.RightPaneRail
                : ShellRegionKind.RightPaneContent;
        }

        if (IsWithin(focused, ShellStatusBar))
        {
            return ShellRegionKind.StatusBar;
        }

        return null;
    }

    bool IShellRegionHost.TryLand(ShellRegionKind region)
    {
        if (_viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return false;
        }

        switch (region)
        {
            case ShellRegionKind.MenuBar:
                return MainMenu.Items.Count > 0
                    && MainMenu.ItemContainerGenerator.ContainerFromIndex(0) is MenuItem first
                    && first.Focus();
            case ShellRegionKind.Files:
                return FilterResultsList.IsVisible ? FilterResultsList.Focus() : FilesTree.Focus();
            case ShellRegionKind.TabBar:
                {
                    WorkspaceGroupViewModel group = workspace.ActiveGroup;
                    if (group.ActiveTab is not { } activeTab)
                    {
                        return false;
                    }

                    TabControl? tabs = FindVisualDescendants<TabControl>(ContentPaneBorder)
                        .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, group));
                    tabs?.UpdateLayout();
                    return tabs?.ItemContainerGenerator.ContainerFromItem(activeTab) is TabItem item && item.Focus();
                }
            case ShellRegionKind.Editor:
                if (workspace.ActiveGroup.ActiveTab is null)
                {
                    return false;
                }

                FocusEditorPane(workspace.ActiveGroup);
                return true;
            case ShellRegionKind.EmptyEditor:
                return workspace.ActiveGroup.ActiveTab is null && ContentPaneBorder.Focus();
            case ShellRegionKind.RightPaneContent:
                if (!workspace.IsRightPaneVisible)
                {
                    return false;
                }

                if (workspace.ConnectionsLeafIsActive() && ConnectionsLeafSurface.FocusAnchor())
                {
                    return true;
                }

                if (workspace.IsGraphInspectorShown && GraphInspectorSurface.FocusFirstStop())
                {
                    return true;
                }

                return VisibleLeafBody() is { } body && FirstFocusable(body) is { } stop && stop.Focus();
            case ShellRegionKind.RightPaneRail:
                if (!workspace.IsRightPaneVisible)
                {
                    return false;
                }

                return (RightPaneLeavesList.SelectedItem is { } selected
                        && RightPaneLeavesList.ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem row
                        && row.Focus())
                    || RightPaneLeavesList.Focus();
            case ShellRegionKind.StatusBar:
                return ShellStatusBar.Focus();
            default:
                return false;
        }
    }

    /// <summary>WPF's menu mode routes keys to the menu; F6 is handed to
    /// the ring so a press from the menu-bar region moves on (spec §4).</summary>
    private void MainMenu_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F6 || _viewModel.Workspace is not WorkspaceViewModel workspace)
        {
            return;
        }

        bool back = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        (back ? workspace.FocusPreviousPaneCommand : workspace.FocusNextPaneCommand).Execute(null);
        e.Handled = true;
    }

    /// <summary>The leaf body currently shown in the right pane's content
    /// column: the visible child of <c>RightPaneLeafHost</c> in column 0
    /// that is not the docked placeholder.</summary>
    private FrameworkElement? VisibleLeafBody() =>
        RightPaneLeafHost.Children.OfType<FrameworkElement>()
            .Where(child => Grid.GetColumn(child) == 0 && child.IsVisible)
            .FirstOrDefault(child => child is not StackPanel);

    private static UIElement? FirstFocusable(DependencyObject root)
    {
        foreach (DependencyObject candidate in FindVisualDescendants<DependencyObject>(root))
        {
            if (candidate is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element
                && candidate is not Border)
            {
                return element;
            }
        }

        return null;
    }

    private static bool IsWithin(DependencyObject element, DependencyObject scope)
    {
        for (DependencyObject? current = element; current is not null; current = Parent(current))
        {
            if (ReferenceEquals(current, scope))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAncestor<T>(DependencyObject element) where T : DependencyObject
    {
        for (DependencyObject? current = element; current is not null; current = Parent(current))
        {
            if (current is T)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? Parent(DependencyObject current) =>
        current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
```

Notes for the implementer: `FindVisualDescendants<T>` already exists in `MainWindow.xaml.cs` (used by `FocusEditorPane`); `CurrentModalSurfaceState` lives in `MainWindow.Palette.cs`; `ConnectionsLeafIsActive()` and `IsGraphInspectorShown` are on `WorkspaceViewModel`; `FocusAnchor()` is on `ConnectionsLeafSurface`, `FocusFirstStop()` (internal) on `GraphInspectorSurface`. If `MainWindow` is declared `internal`, match its accessibility on the partial.

- [ ] **Step 4: Attach the host on workspace ready**

Replace `ViewModel_WorkspaceReady` in `MainWindow.xaml.cs` with:

```csharp
    private void ViewModel_WorkspaceReady(object? sender, EventArgs e)
    {
        // W7-6 (#1240): the window answers F6 for THIS workspace.
        if (_viewModel.Workspace is WorkspaceViewModel workspace)
        {
            workspace.ShellRegionHost = this;
        }

        _ = Dispatcher.InvokeAsync(FocusActiveEditorPane, DispatcherPriority.Loaded);
    }
```

- [ ] **Step 5: Chord table, help and parity reasons**

In `ChordTable.cs` replace the two orphan `Reg(...)` calls (keep the comment above them, but reword it):

```csharp
        // PR-4 orphans, chorded by W7-6 (#1240): F6 / Shift+F6 cycle the
        // shell regions (menu bar, files, tab bar, editor, right pane,
        // status bar). Windows-only — mac navigates panes directionally.
        Reg(Ids.FocusNextPane, "Focus Next Region", CommandSection.View,
            "Move focus to the next shell region: menu bar, files, tab bar, editor, right pane, status bar.",
            null, "F6"),
        Reg(Ids.FocusPreviousPane, "Focus Previous Region", CommandSection.View,
            "Move focus to the previous shell region, wrapping from the menu bar to the status bar.",
            null, "Shift+F6"),
```

In `NavigationHelp.cs` add after `VerticalSplitter`:

```csharp
    public static string Shell =>
        $"{Spoken("slate.workspace.focusNextPane")} moves to the next region: menu bar, files, tab bar, editor, right pane, status bar. "
        + $"{Spoken("slate.workspace.focusPreviousPane")} moves back.";
```

In `MacCatalogParityTests.cs` change both reasons to `"Windows-only shell region cycling (F6 / Shift+F6, W7-6); mac navigates panes directionally only."`.

Regenerate the projection: `SLATE_CHORDS_UPDATE=1 dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter FullyQualifiedName~ChordsJson_IsExactlyTheTablesProjection` then the same without the variable. `git diff chords.json` must show only the two rows' `label`, `hint`, `windows`, `windowsSpoken`, `scope` fields changing (`windowsSpoken` is derived: expect `"F6"` and `"Shift F6"`; if the derivation spells differently, keep the derived value and use it in the spec's §2 wording when documenting).

- [ ] **Step 6: Register the automation ids**

Add to `docs/plans/18_windows_port/w_c_automation_ids.json`, keeping (Source, Expression) order, three entries with `"Source": "MainWindow.xaml"` and `"Surface": "Main window and menu bar"`: `"StatusBar"`, `"WorkspaceFocusNextRegionMenuItem"`, `"WorkspaceFocusPreviousRegionMenuItem"`. Use Python: load, append, sort by `(Source, Expression)`, dump with `indent=2`, preserving the file's newline style; `git diff --stat` must be insertions only.

- [ ] **Step 7: Format, build, run the gates**

Run: `dotnet format SlateWindows.slnx --include src/SlateWindows/MainWindow.ShellRegions.cs src/SlateWindows/MainWindow.xaml.cs src/SlateWindows/Commands/ChordTable.cs src/SlateWindows/Commands/NavigationHelp.cs tests/SlateWindows.Tests/MacCatalogParityTests.cs tests/SlateWindows.Tests/Censuses/MenuBarCensus.cs`
Run: `dotnet build src/SlateWindows/SlateWindows.csproj -c Release -nologo -v q 2>&1 | grep -E "error|Build succeeded"` → succeeded, 0 errors.
Run: `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter "FullyQualifiedName~MenuBarCensus|FullyQualifiedName~ChordTableTests|FullyQualifiedName~CommandDriftTests|FullyQualifiedName~MacCatalogParityTests|FullyQualifiedName~WcMatrixEvidenceCensus|FullyQualifiedName~ShellRegionCycleTests|FullyQualifiedName~FocusableLayoutHostCensus|FullyQualifiedName~ModalSurfaceTests" 2>&1 | grep -E "\[FAIL\]|Passed!|Failed!"` → all pass. `FocusableLayoutHostCensus` may object to `ContentPaneBorder` being focusable only if it is a `ScrollViewer`/`ItemsControl`/`ContentControl`; it is a `Border`, so it should not, but read any failure.
Run: `dotnet format SlateWindows.slnx --verify-no-changes` → clean.

- [ ] **Step 8: Commit**

```bash
git add apps/slate-windows/src/SlateWindows/MainWindow.ShellRegions.cs apps/slate-windows/src/SlateWindows/MainWindow.xaml apps/slate-windows/src/SlateWindows/MainWindow.xaml.cs apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs apps/slate-windows/src/SlateWindows/Commands/NavigationHelp.cs apps/slate-windows/chords.json apps/slate-windows/tests/SlateWindows.Tests/MacCatalogParityTests.cs apps/slate-windows/tests/SlateWindows.Tests/Censuses/MenuBarCensus.cs docs/plans/18_windows_port/w_c_automation_ids.json
git commit -m "W7-6: F6 / Shift+F6 land the seven shell regions from the window (#1240)" -m "KeyBindings, the menu-mode hand-off, Workspace menu rows, the chord rows (Windows-only), NavigationHelp.Shell on the now-focusable named status bar, and MainWindow's IShellRegionHost: origin by landmark, landing per region, modal state from ModalSurfaces." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: The FlaUI journey

Spec §5 (the twin the checklist row names).

**Files:**
- Create: `apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.ShellRegions.cs` (a partial of `ShellAccessibilityTests`; the helpers `SlateWindowsExe`, `WaitForMainWindow`, `WaitForElement`, `WaitForEditor`, `AssertEventuallyFocused`, `PressKey`, `PressChord`, `HasInteractiveDesktop`, `AssertAxeClean`, `FocusDiagnosis` all live in `ShellAccessibilityTests.cs`; copy the process-start shape from `SpokenChords_MenusPaletteAndOverlays_MatchTheTable`)

**Interfaces:**
- Consumes: AutomationIds `FilesTree`, `WorkspaceTabs`, `MarkdownEditor`, `RightPaneLeaves`, `StatusBar`, `FileMenu`, `ContentPane`; `Workspace ▸ Toggle Right Pane` chord `Ctrl+Alt+I`.
- Produces: test name `ShellRegions_F6CyclesForwardAndShiftF6Back` (the checklist twin).

- [ ] **Step 1: Write the journey**

```csharp
// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>W7-6 (#1240) spec §5: F6 walks the ring forward from the
    /// Files tree and Shift+F6 walks it back; hiding the right pane drops
    /// both right stops; a zero-tab vault stops on the empty pane.</summary>
    [Fact]
    public void ShellRegions_F6CyclesForwardAndShiftF6Back()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-shell-regions-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\nBody.\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "shell-regions-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "shell-regions")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // Zero tabs: Files → empty pane → leaf/rail → status bar → menu bar → Files.
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            tree.Focus();
            AssertEventuallyFocused(tree, "The Files tree did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "ContentPane", TimeSpan.FromSeconds(10)), "F6 from Files with no tab did not land on the empty editor pane.");
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "F6 from the empty pane did not land in the right pane.");
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneRailOrStatus(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "F6 did not reach the status bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10)), "F6 from the status bar did not wrap to the menu bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(tree, "F6 from the menu bar did not land on Files.");

            // Open a note: Files → tab bar → editor → … ; then Shift+F6 back.
            AutomationElement note = FindDescendantWithin(tree,
                automation.ConditionFactory.ByControlType(ControlType.TreeItem).And(automation.ConditionFactory.ByName("alpha.md")),
                TimeSpan.FromSeconds(10)) ?? throw new Xunit.Sdk.XunitException("alpha.md is not in the tree.");
            note.Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            tree.Focus();
            AssertEventuallyFocused(tree, "The Files tree did not take focus back.");
            PressKey(VirtualKeyShort.F6);
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = FindDescendantWithin(tabs, automation.ConditionFactory.ByControlType(ControlType.TabItem), TimeSpan.FromSeconds(10))
                ?? throw new Xunit.Sdk.XunitException("No tab item.");
            AssertEventuallyFocused(tab, "F6 from Files did not land on the tab bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(editor, "F6 from the tab bar did not land in the editor.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(tab, "Shift+F6 from the editor did not return to the tab bar.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(tree, "Shift+F6 from the tab bar did not return to Files.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10)), "Shift+F6 from Files did not land on the menu bar.");
            PressKey(VirtualKeyShort.ESCAPE);

            // Hidden right pane: editor → status bar directly.
            editor.Focus();
            AssertEventuallyFocused(editor, "The editor did not take focus.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
            AssertElementDisappears(window, automation, "RightPaneLeaves");
            editor.Focus();
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "With the right pane hidden, F6 from the editor did not skip to the status bar.");

            AssertAxeClean(process, "shell-regions");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The right-pane content stop for the default Outline leaf
    /// with no note open is the rail itself when the leaf has no
    /// focusable stop; either is a right-pane landing.</summary>
    private static void AssertRightPaneStop(Window window, UIA3Automation automation, string message)
    {
        AutomationElement pane = WaitForElement(window, "InspectorPane", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(() => automation.FocusedElement() is { } focused && IsDescendantOf(focused, pane), TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    private static void AssertRightPaneRailOrStatus(Window window, UIA3Automation automation)
    {
        // With the Outline leaf empty, the content stop is skipped and this
        // press already reached the rail; one more press reaches the status
        // bar. When a stop exists, this press lands on the rail.
        AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
        if (rail.Properties.HasKeyboardFocus.ValueOrDefault)
        {
            PressKey(VirtualKeyShort.F6);
        }
    }

    private static bool IsDescendantOf(AutomationElement element, AutomationElement ancestor)
    {
        for (AutomationElement? current = element; current is not null; current = current.Parent)
        {
            if (current.Properties.AutomationId.ValueOrDefault == ancestor.Properties.AutomationId.ValueOrDefault
                && current.Properties.AutomationId.ValueOrDefault is not null)
            {
                return true;
            }
        }

        return false;
    }
}
```

If `FindDescendantWithin`, `AssertElementDisappears` or `PressChord` with three keys are absent or differently named in `ShellAccessibilityTests.cs`, read that file's helpers (grep `private static`) and use the existing ones; `Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I)` is the FlaUI primitive behind them.

- [ ] **Step 2: Build and run it**

Run: `dotnet format SlateWindows.slnx --include tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.ShellRegions.cs`
Run: `dotnet test tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj -c Release --filter FullyQualifiedName~ShellRegions_F6CyclesForwardAndShiftF6Back --logger "console;verbosity=normal" > $env:TEMP/shell-regions.txt 2>&1; grep -E "Passed:|Failed:|Error Message" -A 2 $env:TEMP/shell-regions.txt`
Expected: Passed: 1. If it fails at a `PressKey` step while NVDA is running, check `Get-Process nvda`; if NVDA is up, note it, do not kill it, and record the failure honestly in the PR body; the keyboard-free assertions (the first F6 from a `.Focus()`ed tree) are still meaningful. If it fails with the window's own focus not moving (F6 delivered but `FocusedRegion()` returned null), the cause is in Task 4 — debug with `FocusDiagnosis()` output before changing the test.

- [ ] **Step 3: Commit**

```bash
git add apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.ShellRegions.cs
git commit -m "W7-6: FlaUI journey — F6 forward, Shift+F6 back, hidden pane skipped, empty pane stop (#1240)" -m "The executable twin of w1_shell_at_checklist.md#8." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Documentation rows and the PR

Spec §5 (human row, navigation map, matrix), §0 (recorded divergence).

**Files:**
- Modify: `docs/plans/18_windows_port/reports/w1_shell_at_checklist.md` (append row 8)
- Modify: `docs/plans/18_windows_port/at_navigation_map.md:37` (Landmarks row)
- Modify: `docs/plans/18_windows_port/w_c_matrix.md:7` ("Main window and menu bar" row, focus-route cell = 5th cell)
- Modify: `docs/plans/18_windows_port/specs/w7_6_f6_region_cycling_spec.md` (only if `windowsSpoken` derived differently from "Shift F6" in Task 4)

- [ ] **Step 1: The checklist row**

Append after row 7 of the table in `w1_shell_at_checklist.md` (nine cells; the twin name must be a real test):

```markdown
| 8 | W7-6 | Region cycling | From the Files tree press F6 repeatedly through a full loop, then Shift+F6 back; hide the right pane (Ctrl+Alt+I) and repeat; repeat once on a vault with no tab open. | Each landing is spoken (Files, tab bar with the tab, editor, leaf panel, "Right pane panels", "Status bar" with its text, "Menu bar", "Editor pane. Empty"); hidden right-pane stops are skipped; the ring wraps both ways. | `ShellRegions_F6CyclesForwardAndShiftF6Back` | Pending | Pending | Pending |
```

- [ ] **Step 2: The navigation map and the matrix**

In `at_navigation_map.md` line 37 (the `| native | Landmarks | …` row) change the chord cell (the 6th cell, currently `-`) to `` `F6` / `Shift+F6` (`slate.workspace.focusNextPane` / `focusPreviousPane`) `` and append `` `ShellRegionRingTests`, `ShellRegionCycleTests`, `ShellRegions_F6CyclesForwardAndShiftF6Back` `` to the evidence cell (the 10th), keeping the existing entries. Do this with a Python split on unescaped pipes (see W7-5's approach: replace the two-character sequence backslash-pipe with a sentinel before splitting).

In `w_c_matrix.md` row `| Main window and menu bar |` change the focus-route cell (5th data cell, currently `Menu access keys; declarative named chords`) to `Menu access keys; declarative named chords; F6 / Shift+F6 shell region ring (W7-6)`.

- [ ] **Step 3: Run the documentation censuses**

Run: `dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter "FullyQualifiedName~WcMatrixEvidenceCensus|FullyQualifiedName~AtNavigationMap|FullyQualifiedName~A11yTrigger" 2>&1 | grep -E "\[FAIL\]|Passed!|Failed!"`
Expected: pass (the checklist twin resolves because Task 5's test exists; the navigation-map census, if one exists under a different name, is found by `grep -rln at_navigation_map tests/SlateWindows.Tests` — run whatever it names).

- [ ] **Step 4: Commit, push, open the PR**

```bash
git add docs/plans/18_windows_port/reports/w1_shell_at_checklist.md docs/plans/18_windows_port/at_navigation_map.md docs/plans/18_windows_port/w_c_matrix.md docs/plans/18_windows_port/specs/w7_6_f6_region_cycling_spec.md
git commit -m "W7-6: checklist row 8, Landmarks chord row and matrix focus route for F6 cycling (#1240)" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push -u origin w7-6-f6-region-cycling
```

Then run the whole unit suite once (`dotnet test tests/SlateWindows.Tests/SlateWindows.Tests.csproj -c Release --filter "FullyQualifiedName!~ConnectionsLeafTests.TheModelOf"`), `cargo test -p slate-core a11y`, `cargo test -p slate-uniffi`, and `dotnet format SlateWindows.slnx --verify-no-changes`; record the results. Open the PR with base `w7-5-nvda-field-pass-fixes` (stacked; retarget to `main` after #1241 merges): `gh pr create --base w7-5-nvda-field-pass-fixes --head w7-6-f6-region-cycling --title "feat(windows): W7-6 F6 / Shift+F6 shell region cycling (#1240)" --body-file <body>`, where the body lists the ring, the spec path, each twin, the verification results, and ends with `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
