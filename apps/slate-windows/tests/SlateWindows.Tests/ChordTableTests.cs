// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SlateWindows.Commands;

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741): the chord table is the single source of truth (contract
/// P12, invariant PINV-5), the Windows spoken-hotkey producer exists and
/// every stored row matches it, the declared modifier rule is applied or a
/// divergence is recorded, and the table agrees with the chord surface the
/// app actually delivers (contract P13c).
/// </summary>
public sealed class ChordTableTests
{
    /// <summary>Set to regenerate <c>chords.json</c> from the table rather
    /// than verify against it — the insta/trybuild pattern. Off in CI, so a
    /// hand edit to the JSON fails the build instead of silently becoming a
    /// second source of truth.</summary>
    private const string UpdateEnvironmentVariable = "SLATE_CHORDS_UPDATE";

    private static readonly JsonSerializerOptions ProjectionOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Fact]
    public void WindowsSpokenProducer_InvertsTokenOrderRelativeToMac()
    {
        // The failure this producer exists to prevent: substituting words
        // into the mac glyph string. `⇧⌘O` speaks "Shift Command O"; the
        // Windows chord for the same command speaks "Control Shift O".
        Assert.Equal("Shift Command O", MacHotkeySpoken.Spoken("⇧⌘O"));
        Assert.Equal("Control Shift O", WindowsHotkeySpoken.Spoken("Ctrl+Shift+O"));

        Assert.Equal("Control Backslash", WindowsHotkeySpoken.Spoken("Ctrl+\\"));
        Assert.Equal("Control Alt Left Bracket", WindowsHotkeySpoken.Spoken("Ctrl+Alt+["));
        Assert.Equal("Control Alt Right Bracket", WindowsHotkeySpoken.Spoken("Ctrl+Alt+]"));
        Assert.Equal("Control Alt Equals", WindowsHotkeySpoken.Spoken("Ctrl+Alt+="));
        Assert.Equal("Control Alt Minus", WindowsHotkeySpoken.Spoken("Ctrl+Alt+-"));
        Assert.Equal(
            "Control Alt Shift Left Arrow",
            WindowsHotkeySpoken.Spoken("Ctrl+Alt+Shift+Left"));
        Assert.Equal("Control Backspace", WindowsHotkeySpoken.Spoken("Ctrl+Backspace"));
        Assert.Equal("Control Enter", WindowsHotkeySpoken.Spoken("Ctrl+Enter"));
        Assert.Equal("Escape", WindowsHotkeySpoken.Spoken("Escape"));
        Assert.Equal("F2", WindowsHotkeySpoken.Spoken("F2"));
        Assert.Equal("Control 7", WindowsHotkeySpoken.Spoken("Ctrl+7"));
        Assert.Equal("Control Comma", WindowsHotkeySpoken.Spoken("Ctrl+,"));
        Assert.Equal("Control Period", WindowsHotkeySpoken.Spoken("Ctrl+."));
        Assert.Equal("Control Slash", WindowsHotkeySpoken.Spoken("Ctrl+/"));
        Assert.Equal("Control Semicolon", WindowsHotkeySpoken.Spoken("Ctrl+;"));
        Assert.Equal("Control Quote", WindowsHotkeySpoken.Spoken("Ctrl+'"));
        Assert.Equal("Control Backtick", WindowsHotkeySpoken.Spoken("Ctrl+`"));
        Assert.Equal("Control Space", WindowsHotkeySpoken.Spoken("Ctrl+Space"));
        Assert.Equal("Control Plus", WindowsHotkeySpoken.Spoken("Ctrl++"));

        // A chordless row projects null; an empty chord speaks nothing. The
        // two must not collapse into each other, or a row with no chord
        // would render an empty trailing fragment in its accessible name.
        Assert.Null(WindowsHotkeySpoken.Spoken(null));
        Assert.Equal(string.Empty, WindowsHotkeySpoken.Spoken(string.Empty));
        Assert.Null(MacHotkeySpoken.Spoken(null));
        Assert.Equal(string.Empty, MacHotkeySpoken.Spoken(string.Empty));
    }

    [Fact]
    public void EveryStoredSpokenString_EqualsWhatTheProducerGenerates()
    {
        // Contract P12: `windowsSpoken` had zero producers and zero
        // consumers before W5-1 — 35 hand-authored literals. This is the
        // fact that keeps the column honest.
        foreach (JsonNode? row in EveryStoredRow())
        {
            string id = row!["id"]!.GetValue<string>();
            string? windows = row["windows"]?.GetValue<string>();
            string? spoken = row["windowsSpoken"]?.GetValue<string>();
            Assert.Equal(WindowsHotkeySpoken.Spoken(windows), spoken);

            string? macChord = row["macChord"]?.GetValue<string>();
            Assert.Equal(MacHotkeySpoken.Spoken(macChord), row["mac"]?.GetValue<string>());
            Assert.False(
                windows is null && spoken is not null,
                $"{id} stores a spoken string with no Windows chord.");
        }
    }

    [Fact]
    public void MacSpokenColumn_PreservesEveryPreW51Literal()
    {
        // The mac column became a projection of the authored glyph chord in
        // W5-1. These are the literals the file carried before, so a wrong
        // glyph shows up here rather than as silent drift.
        var expected = new Dictionary<string, string>
        {
            ["slate.vault.open"] = "Shift Command O",
            ["slate.workspace.quickOpen"] = "Command O",
            ["slate.workspace.newTab"] = "Command T",
            ["slate.workspace.closeTab"] = "Command W",
            ["slate.workspace.reopenClosedTab"] = "Shift Command T",
            ["slate.workspace.nextTab"] = "Shift Command Right Bracket",
            ["slate.workspace.previousTab"] = "Shift Command Left Bracket",
            ["slate.workspace.splitRight"] = "Command Backslash",
            ["slate.workspace.splitDown"] = "Option Command Backslash",
            ["slate.workspace.focusPaneLeft"] = "Option Command Left Arrow",
            ["slate.workspace.focusPaneRight"] = "Option Command Right Arrow",
            ["slate.workspace.focusPaneAbove"] = "Option Command Up Arrow",
            ["slate.workspace.focusPaneBelow"] = "Option Command Down Arrow",
            ["slate.workspace.moveTabLeft"] = "Control Command Left Arrow",
            ["slate.workspace.moveTabRight"] = "Control Command Right Arrow",
            ["slate.workspace.growPane"] = "Option Command Equals",
            ["slate.workspace.shrinkPane"] = "Option Command Minus",
            ["slate.view.toggleRightPane"] = "Option Command I",
            ["slate.sidebar.focusFilter"] = "Option Command F",
            ["slate.sidebar.historyBack"] = "Control Command Left Bracket",
            ["slate.sidebar.historyForward"] = "Control Command Right Bracket",
            ["slate.file.cancelImport"] = "Command Period",
            ["slate.editor.toggleViewMode"] = "Shift Command E",
            ["slate.editor.bulkRenameProperties"] = "Shift Command R",
            ["slate.navigation.jumpToBibliography"] = "Command J",
            ["slate.editor.citationSummary"] = "Shift Command J",
        };

        foreach ((string id, string spoken) in expected)
        {
            ChordTableEntry row = RequireRow(id);
            Assert.Equal(spoken, row.MacSpoken);
        }
    }

    [Fact]
    public void EveryRow_EitherFollowsTheModifierRule_OrRecordsADivergence()
    {
        // Contract P12: the table states the rule it follows and marks every
        // deviation as a recorded divergence with a reason. Record, never
        // rebind — the app's shipped chords are not touched here.
        var divergent = new List<string>();
        foreach (ChordTableEntry row in ChordTable.Entries)
        {
            if (row.MacChord is null || row.WindowsChord is null)
            {
                Assert.True(
                    row.Divergence is null || row.MacChord is not null,
                    $"{row.Id} records a divergence with no mac chord to diverge from.");
                continue;
            }

            string predicted = MacToWindowsChordRule.Apply(row.MacChord)!;
            if (row.WindowsChord == predicted)
            {
                Assert.Null(row.Divergence);
                continue;
            }

            divergent.Add(row.Id);
            Assert.False(
                string.IsNullOrWhiteSpace(row.Divergence),
                $"{row.Id} ships {row.WindowsChord} but the declared rule predicts "
                + $"{predicted}, and it records no divergence reason.");
        }

        // The complete recorded-divergence set. A new one has to be added
        // here deliberately, which is the point.
        Assert.Equal(
            new[]
            {
                "slate.file.cancelImport",
                "slate.file.rename",
                "slate.sidebar.openShortcut1",
                "slate.sidebar.openShortcut2",
                "slate.sidebar.openShortcut3",
                "slate.sidebar.openShortcut4",
                "slate.sidebar.openShortcut5",
                "slate.sidebar.openShortcut6",
                "slate.sidebar.openShortcut7",
                "slate.sidebar.openShortcut8",
                "slate.sidebar.openShortcut9",
                "slate.workspace.moveTabLeft",
                "slate.workspace.moveTabRight",
            },
            divergent.OrderBy(id => id, System.StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ModifierRule_MapsControlToAltAndKeepsCanonicalTokenOrder()
    {
        Assert.Equal("Ctrl+Shift+O", MacToWindowsChordRule.Apply("⇧⌘O"));
        Assert.Equal("Ctrl+Alt+[", MacToWindowsChordRule.Apply("⌃⌘["));
        Assert.Equal("Ctrl+Alt+Left", MacToWindowsChordRule.Apply("⌥⌘←"));
        Assert.Equal("Ctrl+Alt+Left", MacToWindowsChordRule.Apply("⌃⌘←"));
        Assert.Equal("Ctrl+Backspace", MacToWindowsChordRule.Apply("⌘⌫"));
        Assert.Equal("Alt+1", MacToWindowsChordRule.Apply("⌃1"));

        // The collision the ⌃-to-Alt rule creates, and the reason
        // moveTabLeft/Right had to diverge.
        Assert.Equal(
            MacToWindowsChordRule.Apply("⌥⌘←"),
            MacToWindowsChordRule.Apply("⌃⌘←"));
    }

    [Fact]
    public void ChordsJson_IsExactlyTheTablesProjection()
    {
        string path = ChordsPath();
        string onDisk = File.ReadAllText(path);
        var root = (JsonObject)JsonNode.Parse(onDisk)!;

        // deliveryEvidence must survive the projection untouched:
        // scripts/generate-parity-matrix.py performs twelve fatal
        // validations on that object.
        JsonNode evidenceBefore = root["deliveryEvidence"]!.DeepClone();

        ChordTable.Project(root);

        // System.Text.Json's indented writer emits Environment.NewLine, and
        // .gitattributes is `* -text` — so normalise to LF explicitly rather
        // than letting the checked-in file flip to CRLF on a Windows run.
        string expected = root.ToJsonString(ProjectionOptions)
            .Replace("\r\n", "\n", System.StringComparison.Ordinal) + "\n";

        Assert.True(
            JsonNode.DeepEquals(evidenceBefore, root["deliveryEvidence"]),
            "the projection mutated deliveryEvidence");

        if (Environment.GetEnvironmentVariable(UpdateEnvironmentVariable) == "1")
        {
            File.WriteAllText(path, expected, new UTF8Encoding(false));
            return;
        }

        Assert.True(
            onDisk == expected,
            $"chords.json is not the chord table's projection. Re-run with "
            + $"{UpdateEnvironmentVariable}=1 to regenerate it, and never hand-edit "
            + "the file: the table is the single source of truth (PINV-5).");
    }

    [Fact]
    public void CommandRows_HaveNoActiveChordCollision()
    {
        // The W1 invariant, restated over the table rather than the file.
        // chordSurface rows are exempt because focus scopes make sharing a
        // chord string correct there — Ctrl+Enter in the editor and in Quick
        // Open are never live at the same moment.
        string[] active = ChordTable.Entries
            .Where(row => row.IsCommandId && row.WindowsChord is not null)
            .Select(row => row.WindowsChord!)
            .ToArray();
        Assert.Equal(
            active.Length,
            active.Distinct(System.StringComparer.OrdinalIgnoreCase).Count());

        // Two surface rows sharing a chord must be in different scopes.
        foreach (IGrouping<string, ChordTableEntry> group in ChordTable.Entries
            .Where(row => !row.IsCommandId && row.WindowsChord is not null)
            .GroupBy(row => row.WindowsChord!, System.StringComparer.OrdinalIgnoreCase))
        {
            Assert.Equal(
                group.Count(),
                group.Select(row => row.Scope).Distinct().Count());
        }
    }

    [Fact]
    public void TableCoversEveryDeclarativelyBoundChord_AndNothingStale()
    {
        HashSet<string> declared = ChordTable.Entries
            .Where(row => row.WindowsChord is not null)
            .Select(row => row.WindowsChord!)
            .ToHashSet(System.StringComparer.Ordinal);

        HashSet<string> windowBindings = KeyBindingChords("MainWindow.xaml");
        foreach (string chord in windowBindings
            .Union(KeyBindingChords("WorkspaceTemplates.xaml"))
            .Union(MenuGestureStrings())
            .Union(ReadingNavigatorChords())
            .Union(GridGestureChords()))
        {
            Assert.True(declared.Contains(chord), $"{chord} ships but the table omits it.");
        }

        // Scope-keyed, and BOTH sides derived from the shipping sources.
        // The previous form built its expectations from the same table it
        // was checking — Assert.Contains(("Up", PropertyRow), declared)
        // only restated that the table had a row it already had. Codex
        // found the consequence: deleting the real StepUpCommand binding,
        // or the splitter's arrow arm, left every scoped assertion green.
        foreach ((ChordScope scope, HashSet<string> shipped) in ScopedProductionChords())
        {
            HashSet<string> tabled = ChordTable.Entries
                .Where(row => row.Scope == scope && row.WindowsChord is not null)
                .Select(row => row.WindowsChord!)
                .ToHashSet(System.StringComparer.Ordinal);

            Assert.NotEmpty(shipped);
            foreach (string chord in shipped)
            {
                Assert.True(
                    tabled.Contains(chord),
                    $"{chord} ships in {scope} scope but the table has no row for "
                    + "it in that scope — another scope's row cannot stand in.");
            }

            foreach (string chord in tabled)
            {
                Assert.True(
                    shipped.Contains(chord),
                    $"the table claims {chord} in {scope} scope but no shipping "
                    + "source delivers it — the row is stale, or the chord moved.");
            }
        }

        // Reverse direction (contract P13c): every globally scoped row must
        // be delivered by a window KeyBinding, unless its row id is on the
        // imperative allow-list below.
        foreach (ChordTableEntry row in ChordTable.Entries
            .Where(row => row.Scope == ChordScope.Global))
        {
            if (ImperativeGlobalRows.ContainsKey(row.Id))
            {
                continue;
            }

            Assert.True(
                windowBindings.Contains(row.WindowsChord!),
                $"{row.Id} claims global scope for {row.WindowsChord} but no "
                + "MainWindow.xaml KeyBinding delivers it.");
        }

        // Allow-list staleness, checked in BOTH directions — mac's rule, and
        // what makes an allow-list non-bypassable. An entry whose row is gone
        // or whose chord became declarative fails here rather than lingering.
        foreach ((string id, ImperativeChord entry) in ImperativeGlobalRows)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(entry.Reason),
                $"allow-listed row {id} carries no reason.");
            ChordTableEntry row = RequireRow(id);
            Assert.Equal(ChordScope.Global, row.Scope);
            if (entry.SharesTheChordWithAnotherClaimant)
            {
                continue;
            }

            Assert.False(
                windowBindings.Contains(row.WindowsChord!),
                $"allow-list entry {id} now has a MainWindow.xaml KeyBinding for "
                + $"{row.WindowsChord} — the entry is stale and must be removed.");
        }
    }

    [Fact]
    public void EveryUnregisteredRow_RecordsAReason_AndEveryRegisteredRowIsDescribed()
    {
        // PR-4: dispositions, not silent registration.
        foreach (ChordTableEntry row in ChordTable.Entries)
        {
            Assert.Equal(row.WindowsChord is null, row.Scope == ChordScope.None);
            Assert.False(
                string.IsNullOrWhiteSpace(row.Id) || row.Id.EndsWith('.'),
                $"malformed row id '{row.Id}'.");

            if (row.IsRegistered)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Label));
                Assert.False(string.IsNullOrWhiteSpace(row.Hint));
                Assert.Null(row.Reason);
            }
            else
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(row.Reason),
                    $"{row.Id} is not registered and records no reason.");
            }
        }

        // The eleven orphans PR-4 names, each with its recorded decision.
        Assert.True(ChordTable.Find(ChordTable.Ids.FocusNextPane)!.IsRegistered);
        Assert.True(ChordTable.Find(ChordTable.Ids.FocusPreviousPane)!.IsRegistered);
        Assert.True(ChordTable.Find(ChordTable.Ids.SidebarToggleTags)!.IsRegistered);
        Assert.True(ChordTable.Find(ChordTable.Ids.ToggleLayout)!.IsRegistered);
        Assert.False(ChordTable.Find("windows.quickOpen.moveNext")!.IsRegistered);
        Assert.False(ChordTable.Find("windows.quickOpen.movePrevious")!.IsRegistered);
        Assert.False(ChordTable.Find("windows.quickOpen.openCurrentTab")!.IsRegistered);
        Assert.False(ChordTable.Find("windows.quickOpen.openNewTab")!.IsRegistered);
        Assert.False(ChordTable.Find("windows.quickOpen.openSplitRight")!.IsRegistered);
        Assert.False(ChordTable.Find("windows.quickOpen.openSplitDown")!.IsRegistered);
        Assert.False(ChordTable.Find("windows.propertyRow.editorVerbs")!.IsRegistered);
    }

    [Fact]
    public void EveryReadingNavigatorChordIsRecorded_IncludingTheTwoAliases()
    {
        // PR-3: w3_spec.md claims the reading chords are "registered in
        // chords.json". They were not, before W5-1.
        string[] letters = ["H", "K", "L", "U", "T", "E", "C", "M", "D", "G"];
        foreach (string letter in letters)
        {
            Assert.NotNull(ChordTable.Find($"windows.reading.next{letter}"));
            Assert.NotNull(ChordTable.Find($"windows.reading.previous{letter}"));
        }

        // U aliases L, G aliases D — same target, distinct chord strings, so
        // both must be recorded or the alias is an undocumented chord.
        Assert.Contains("list", ChordTable.Find("windows.reading.nextL")!.Label);
        Assert.Contains("alias for L", ChordTable.Find("windows.reading.nextU")!.Label);
        Assert.Contains("alias for D", ChordTable.Find("windows.reading.nextG")!.Label);
        Assert.Equal("Ctrl+Alt+U", ChordTable.Find("windows.reading.nextU")!.WindowsChord);
        Assert.Equal("Ctrl+Alt+G", ChordTable.Find("windows.reading.nextG")!.WindowsChord);

        Assert.Equal(
            32,
            ChordTable.Entries.Count(row => row.Scope == ChordScope.Reading));
    }

    [Fact]
    public void SidebarPinnedOrder_MirrorsMacCatalogOrder_AndOnlyNamesRealIds()
    {
        // Contract P16: data, not policy. Registry.List() sorts by
        // (section, id), so an empty list renders Sidebar alphabetically.
        Assert.Equal(
            ChordTable.SidebarPinnedOrder.Length,
            ChordTable.SidebarPinnedOrder.Distinct(System.StringComparer.Ordinal).Count());
        Assert.Equal(ChordTable.Ids.SidebarOpen, ChordTable.SidebarPinnedOrder[0]);
        Assert.Equal(ChordTable.Ids.NewNote, ChordTable.SidebarPinnedOrder[1]);
        Assert.Equal(ChordTable.Ids.NewFolder, ChordTable.SidebarPinnedOrder[2]);

        foreach (string id in ChordTable.SidebarPinnedOrder)
        {
            ChordTableEntry row = RequireRow(id);
            Assert.True(row.IsRegistered, $"{id} is pinned but never registered.");
            Assert.Equal(uniffi.slate_uniffi.CommandSection.Sidebar, row.Section);
        }

        // Both directions: every registered Sidebar-section row is pinned.
        foreach (ChordTableEntry row in ChordTable.RegisteredRows
            .Where(row => row.Section == uniffi.slate_uniffi.CommandSection.Sidebar))
        {
            Assert.Contains(row.Id, ChordTable.SidebarPinnedOrder);
        }
    }

    private sealed record ImperativeChord(
        string Reason,
        bool SharesTheChordWithAnotherClaimant = false);

    /// <summary>
    /// Rows the shell delivers imperatively rather than through a
    /// <c>KeyBinding</c>, each with the reason it cannot be scraped.
    /// </summary>
    private static readonly Dictionary<string, ImperativeChord> ImperativeGlobalRows =
        BuildImperativeGlobalRows();

    private static Dictionary<string, ImperativeChord> BuildImperativeGlobalRows()
    {
        var rows = new Dictionary<string, ImperativeChord>(System.StringComparer.Ordinal)
        {
            [ChordTable.Ids.FocusFilter] = new(
                "MainWindow.xaml.cs:435 focuses the sidebar filter TextBox directly; "
                + "the Files menu advertises it with InputGestureText only."),
            [ChordTable.Ids.RenameEntry] = new(
                "MainWindow.xaml.cs:443 opens the rename expander and moves focus, "
                + "gated on the file tree having focus."),
            [ChordTable.Ids.CancelImport] = new(
                "MainWindow.xaml.cs:417 cancels a running import, gated on IsImporting "
                + "and on Quick Open being closed. Escape also has a window KeyBinding "
                + "(MainWindow.xaml:55) for the editor popover — a different claimant in "
                + "the PR-2 chain, so the no-KeyBinding staleness check is waived here.",
                SharesTheChordWithAnotherClaimant: true),
            ["windows.view.showCommandPalette"] = new(
                "W5-1: Ctrl+Shift+P is delivered from Window_PreviewKeyDown, not a "
                + "KeyBinding, because the palette exposes methods rather than "
                + "ICommands (PR-4). The red team found the chord shipping with no "
                + "table row at all — the scrape reads only declarative sources, so "
                + "an imperative chord must be allow-listed here to be checked."),
            [ChordTable.Ids.ToggleSearch] = new(
                "W5-2: Ctrl+Shift+F is delivered from Window_PreviewKeyDown, not a "
                + "KeyBinding. The close-out registered ToggleSearchCommand, but a "
                + "KeyBinding to it would bypass the modal-surface gate "
                + "(TryClearTheWayForSearch) that only the chord path applies — "
                + "the registered command is deliberately unguarded because the "
                + "palette invokes before dismissing (P9). Recorded here so the "
                + "W5-1 red-team finding (an imperative chord shipping with no "
                + "table row) cannot recur for search."),
        };
        for (int slot = 1; slot <= 9; slot++)
        {
            rows[ChordTable.Ids.OpenShortcut(slot)] = new(ShortcutSlotReason);
        }

        return rows;
    }

    private const string ShortcutSlotReason =
        "MainWindow.xaml.cs:464 maps Ctrl plus a digit through ShortcutNumber (:594-606), "
        + "which also accepts the numeric keypad — nine KeyBindings could not express it.";

    private static ChordTableEntry RequireRow(string id) =>
        ChordTable.Find(id) ?? throw new Xunit.Sdk.XunitException($"no chord row for {id}");

    private static IEnumerable<JsonNode?> EveryStoredRow()
    {
        var root = (JsonObject)JsonNode.Parse(File.ReadAllText(ChordsPath()))!;
        return ((JsonArray)root["commands"]!).Concat((JsonArray)root["chordSurface"]!);
    }

    private static HashSet<string> KeyBindingChords(string xamlFileName)
    {
        XDocument document = XDocument.Load(
            Path.Combine(SourceRoot(), xamlFileName),
            LoadOptions.None);
        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (XElement binding in document.Descendants()
            .Where(element => element.Name.LocalName == "KeyBinding"))
        {
            string? key = binding.Attribute("Key")?.Value;
            if (key is null)
            {
                continue;
            }

            chords.Add(Canonical(binding.Attribute("Modifiers")?.Value, key));
        }

        return chords;
    }

    /// <summary>
    /// The chords the menu bar advertises, resolved through the table.
    /// </summary>
    /// <remarks>
    /// Menu accelerators are no longer authored in XAML — every
    /// <c>InputGestureText</c> is a <c>{cmd:ChordText &lt;id&gt;}</c>
    /// extension that reads the table, which is what PINV-5 always
    /// claimed and what codex found to be false. This scrape therefore
    /// reads the ids and asserts each names a real, chorded row: the
    /// markup extension throws at parse time on a bad id, and this makes
    /// the same failure visible without launching the app.
    /// </remarks>
    private static HashSet<string> MenuGestureStrings()
    {
        XDocument document = XDocument.Load(Path.Combine(SourceRoot(), "MainWindow.xaml"));
        string[] accelerators = document.Descendants()
            .Select(element => element.Attribute("InputGestureText")?.Value)
            .Where(value => !string.IsNullOrEmpty(value))
            .Select(value => value!)
            .ToArray();

        // Guard against a scrape that quietly matches nothing.
        Assert.NotEmpty(accelerators);

        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (string accelerator in accelerators)
        {
            Match reference = Regex.Match(
                accelerator, @"^\{cmd:ChordText\s+([A-Za-z0-9_.]+)\}$");
            Assert.True(
                reference.Success,
                $"menu accelerator '{accelerator}' is authored literally. "
                + "Accelerators resolve from the chord table (PINV-5) — use "
                + "InputGestureText=\"{cmd:ChordText <id>}\".");

            string id = reference.Groups[1].Value;
            ChordTableEntry row = ChordTable.Find(id)
                ?? throw new Xunit.Sdk.XunitException(
                    $"menu accelerator references '{id}', which has no chord-table row.");
            Assert.True(
                row.WindowsChord is not null,
                $"menu accelerator references '{id}', which has a row but no chord.");
            chords.Add(row.WindowsChord!);
        }

        return chords;
    }

    /// <summary>
    /// Chords the shipping sources deliver, keyed by the scope they are
    /// live in — the expected side of the scoped comparison, so it can
    /// disagree with the table.
    /// </summary>
    private static Dictionary<ChordScope, HashSet<string>> ScopedProductionChords() =>
        new()
        {
            [ChordScope.PropertyRow] = PropertyRowChords(),
            [ChordScope.Splitter] = SplitterChords(),
            [ChordScope.Reading] = ReadingNavigatorChords()
                .Union(ReadingHeadingLevelChords())
                .ToHashSet(System.StringComparer.Ordinal),
            [ChordScope.Grid] = GridGestureChords(),
            [ChordScope.Palette] = PaletteChords(),
            [ChordScope.QuickOpen] = QuickOpenChords(),
            [ChordScope.SearchOverlay] = SearchOverlayChords(),
            [ChordScope.Editor] = EditorChords(),
        };

    /// <summary>
    /// Scopes with no source scrape, and why. Every entry is reverse-
    /// direction coverage this suite does not provide.
    /// </summary>
    private static readonly Dictionary<ChordScope, string> ScopesWithoutAProductionScrape =
        new()
        {
            [ChordScope.None] = "no chord to deliver.",
            [ChordScope.Global] = "checked in both directions below, against "
                + "MainWindow.xaml's KeyBindings plus the imperative allow-list.",
        };

    /// <summary>
    /// Adding a scope must be a decision, not a silent exemption.
    /// </summary>
    [Fact]
    public void EveryScopeIsEitherScrapedFromProductionOrDispositioned()
    {
        foreach (ChordScope scope in System.Enum.GetValues<ChordScope>())
        {
            Assert.True(
                ScopedProductionChords().ContainsKey(scope)
                    ^ ScopesWithoutAProductionScrape.ContainsKey(scope),
                $"{scope} must have exactly one of a production scrape or a "
                + "recorded reason for lacking one. A new scope with neither "
                + "would be checked table-against-table, which proves nothing.");
        }

        foreach ((ChordScope scope, string reason) in ScopesWithoutAProductionScrape)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(reason),
                $"{scope} is exempt from the source scrape with no reason given.");
        }
    }

    /// <summary>
    /// The palette overlay's own key route.
    /// </summary>
    /// <remarks>
    /// The first version of this list exempted Palette on the grounds that
    /// its handler "carries selection logic rather than a scrapable gesture
    /// list". That was not true of the code —
    /// <c>HandleCommandPaletteKey</c> is a flat switch — and it would have
    /// left the one surface W5-1 actually adds as the only scope checked
    /// table-against-table.
    /// </remarks>
    private static HashSet<string> PaletteChords() =>
        UnmodifiedSwitchKeys("MainWindow.Palette.cs", "HandleCommandPaletteKey");

    /// <summary>
    /// The search overlay's own key route (W5-2) — a flat unmodified
    /// switch like the palette's, and scraped the same way so a key
    /// added to <c>HandleSearchOverlayKey</c> without a table row fails
    /// here in both directions.
    /// </summary>
    private static HashSet<string> SearchOverlayChords() =>
        UnmodifiedSwitchKeys("MainWindow.Search.cs", "HandleSearchOverlayKey");

    /// <summary>
    /// The editor's own key route: <c>SlateTextEditor.OnPreviewKeyDown</c>.
    /// </summary>
    /// <remarks>
    /// This scope was exempted with the reason "AvalonEdit's own key
    /// handling, which is a third-party control's and not ours to scrape".
    /// Codex found that false — Ctrl+E, Ctrl+Enter and Escape are all
    /// handled by Slate's own <c>OnPreviewKeyDown</c> override. That was
    /// the third untrue rationale in this list, so the list is now down to
    /// the two entries that are structural rather than prose: Global,
    /// which has its own both-direction check, and None, which has no
    /// chord.
    /// </remarks>
    private static HashSet<string> EditorChords()
    {
        MethodDeclarationSyntax route =
            CSharpSource.Load("SlateTextEditor.cs").Method("OnPreviewKeyDown");

        // The Ctrl+ prefix below rests entirely on these two locals meaning
        // what their names say. Read through their declarations, so an
        // inverted definition fails rather than quietly making the prefix
        // a lie.
        AssertLocalMeans(
            route,
            "controlGesture",
            "(modifiers&ModifierKeys.Control)==ModifierKeys.Control");
        AssertLocalMeans(
            route,
            "hasConflictingModifier",
            "(modifiers&(ModifierKeys.Alt|ModifierKeys.Shift|ModifierKeys.Windows))"
            + "!=ModifierKeys.None");

        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (IfStatementSyntax arm in route.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (CSharpSource.KeyNames(arm.Condition).FirstOrDefault() is not string key)
            {
                continue;
            }

            if (CSharpSource.References(arm.Condition, "controlGesture"))
            {
                chords.Add(Canonical("Control", key));
            }
            else if (CSharpSource.Normalize(arm.Condition)
                .Contains("modifiers==ModifierKeys.None", System.StringComparison.Ordinal))
            {
                chords.Add(Canonical(null, key));
            }
        }

        return chords;

    }

    /// <summary>
    /// Quick Open's key route: three bare arms written as <c>if</c>
    /// statements, plus a commit whose target comes from a modifier switch
    /// — every arm of which is a separately advertised chord.
    /// </summary>
    private static HashSet<string> QuickOpenChords()
    {
        MethodDeclarationSyntax route =
            CSharpSource.Load("MainWindow.xaml.cs").Method("HandleQuickSwitcherKey");
        var chords = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (IfStatementSyntax arm in route.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (CSharpSource.KeyNames(arm.Condition).FirstOrDefault() is string key
                && CSharpSource.Normalize(arm.Condition)
                    .Contains("modifiers==ModifierKeys.None", System.StringComparison.Ordinal))
            {
                chords.Add(Canonical(null, key));
            }
        }

        // Commit is one Enter arm whose destination comes from a switch over
        // the modifiers — every arm of that switch is a separately
        // advertised chord, including the discard, which is the bare Enter.
        SwitchExpressionSyntax[] targets = route.DescendantNodes()
            .OfType<SwitchExpressionSyntax>()
            .Where(switchExpression =>
                CSharpSource.Normalize(switchExpression.GoverningExpression) == "modifiers")
            .ToArray();
        Assert.True(
            targets.Length == 1,
            $"HandleQuickSwitcherKey has {targets.Length} switches over `modifiers`. "
            + "This reads one of them, so the rest go unchecked.");

        IfStatementSyntax commit = targets[0].Ancestors().OfType<IfStatementSyntax>().First();
        Assert.True(
            CSharpSource.KeyNames(commit.Condition).Contains("Enter"),
            "the switch that chooses Quick Open's destination is no longer "
            + "guarded on Enter, so attributing its arms to Enter is unfounded.");

        foreach (SwitchExpressionArmSyntax arm in targets[0].Arms)
        {
            chords.Add(arm.Pattern is DiscardPatternSyntax
                ? "Enter"
                : Canonical(
                    CSharpSource.Normalize(arm.Pattern)
                        .Replace("ModifierKeys.", string.Empty)
                        .Replace("|", "+"),
                    "Enter"));
        }

        return chords;

    }

    /// <summary>
    /// The <c>case Key.X:</c> arms of one method's unmodified switch.
    /// </summary>
    private static HashSet<string> UnmodifiedSwitchKeys(string fileName, string methodName)
    {
        MethodDeclarationSyntax route = CSharpSource.Load(fileName).Method(methodName);
        IfStatementSyntax[] guards = route.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Where(statement =>
                CSharpSource.Normalize(statement.Condition) == "modifiers==ModifierKeys.None")
            .ToArray();
        Assert.True(
            guards.Length == 1,
            $"{methodName} has {guards.Length} unmodified-chord guards; this "
            + "reads the keys inside exactly one, so any other is unchecked.");

        // Only the labels INSIDE the guard. Reading the whole method would
        // credit a modified arm elsewhere in it as a bare chord.
        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (CaseSwitchLabelSyntax label in guards[0]
            .DescendantNodes().OfType<CaseSwitchLabelSyntax>())
        {
            foreach (string key in CSharpSource.KeyNames(label.Value))
            {
                chords.Add(Canonical(null, key));
            }
        }

        return chords;

    }

    /// <summary>
    /// <c>ReadingNavigator.Bind</c>, where every reading chord is
    /// registered. The constructor only calls it.
    /// </summary>
    private static SyntaxNode NavigatorRegistration() =>
        CSharpSource.Load("Reading", "ReadingNavigator.cs").Method("Bind");

    /// <summary>Every <c>AddChord(...)</c> call under a node.</summary>
    private static IEnumerable<InvocationExpressionSyntax> AddChordCalls(SyntaxNode scope) =>
        scope.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is IdentifierNameSyntax name
                && name.Identifier.ValueText == "AddChord")
            .Where(call => call.ArgumentList.Arguments.Count >= 2);

    /// <summary>The value of an <c>AddChord</c> call's <c>shift:</c> argument.</summary>
    private static bool ShiftArgument(InvocationExpressionSyntax call)
    {
        ArgumentSyntax shift = call.ArgumentList.Arguments
            .FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "shift")
            ?? call.ArgumentList.Arguments[1];
        return CSharpSource.Normalize(shift.Expression) == "true";
    }

    /// <summary>
    /// A local must still be declared with the meaning a query depends on.
    /// </summary>
    private static void AssertLocalMeans(
        SyntaxNode scope, string local, string expected)
    {
        VariableDeclaratorSyntax[] declarations = scope.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Identifier.ValueText == local)
            .ToArray();
        Assert.True(
            declarations.Length == 1,
            $"{local} is declared {declarations.Length} times here; the query "
            + "that depends on its meaning reads one of them.");
        Assert.Equal(
            expected,
            CSharpSource.Normalize(declarations[0].Initializer!.Value));
    }

    /// <summary>
    /// The property-row templates' <c>TextBox.InputBindings</c> — read only
    /// from <c>DataTemplate</c>s bound to <c>PropertyRowViewModel</c>, so a
    /// neighbouring template's Up/Down cannot stand in for a stepper.
    /// </summary>
    private static HashSet<string> PropertyRowChords()
    {
        XDocument document = XDocument.Load(
            Path.Combine(SourceRoot(), "WorkspaceTemplates.xaml"));
        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (XElement template in document.Descendants()
            .Where(element => element.Name.LocalName == "DataTemplate"
                && element.Attribute("DataType")?.Value.Contains(
                    "PropertyRowViewModel", System.StringComparison.Ordinal) == true))
        {
            foreach (XElement binding in template.Descendants()
                .Where(element => element.Name.LocalName == "KeyBinding"))
            {
                if (binding.Attribute("Key")?.Value is { } key)
                {
                    chords.Add(Canonical(binding.Attribute("Modifiers")?.Value, key));
                }
            }
        }

        return chords;
    }

    /// <summary>
    /// <c>WeightedSplitPanel.Thumb_KeyDown</c>'s orientation switch — the
    /// only place a focused splitter thumb reads an arrow.
    /// </summary>
    private static HashSet<string> SplitterChords()
    {
        MethodDeclarationSyntax thumb =
            CSharpSource.Load("WeightedSplitPanel.cs").Method("Thumb_KeyDown");

        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (SwitchExpressionArmSyntax arm in thumb
            .DescendantNodes().OfType<SwitchExpressionArmSyntax>())
        {
            foreach (string key in CSharpSource.KeyNames(arm.Pattern))
            {
                chords.Add(Canonical(null, key));
            }
        }

        return chords;

    }

    /// <summary>
    /// The navigator's generated heading-level chords. The loop bound is
    /// read from the source rather than hard-coded at six, so narrowing the
    /// loop fails the table's rows instead of leaving twelve assertions
    /// green against a shorter reality.
    /// </summary>
    private static HashSet<string> ReadingHeadingLevelChords()
    {
        SyntaxNode register = NavigatorRegistration();
        ForStatementSyntax[] loops = register.DescendantNodes()
            .OfType<ForStatementSyntax>()
            .Where(loop => loop.Declaration is not null
                && loop.Declaration.Variables.Any(v => v.Identifier.ValueText == "level"))
            .ToArray();
        Assert.True(
            loops.Length == 1,
            $"the navigator has {loops.Length} loops over `level`; the heading "
            + "chords below are generated from exactly one.");

        // The bound is read from the loop, so narrowing it to four levels
        // fails the table's twelve rows instead of passing against a
        // shorter reality.
        Assert.True(
            loops[0].Condition is BinaryExpressionSyntax { Right: LiteralExpressionSyntax }
                condition
                && condition.OperatorToken.Text == "<=",
            "the heading-level loop no longer ends at a literal bound, so its "
            + "chords cannot be generated from it. Update this query.");
        int levels = int.Parse(
            ((LiteralExpressionSyntax)((BinaryExpressionSyntax)loops[0].Condition!).Right)
                .Token.ValueText,
            System.Globalization.CultureInfo.InvariantCulture);

        // Both registrations must still exist, and their key argument must
        // still be COMPUTED — a plain Key.X here would mean the loop no
        // longer generates the levels this projects.
        foreach (bool shifted in new[] { false, true })
        {
            Assert.True(
                AddChordCalls(register).Any(call =>
                    call.ArgumentList.Arguments[0].Expression is BinaryExpressionSyntax
                    && ShiftArgument(call) == shifted),
                $"the shift: {(shifted ? "true" : "false")} heading-level "
                + "registration is gone or rewritten; this would otherwise "
                + "invent chords the app no longer delivers.");
        }

        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        for (int level = 1; level <= levels; level++)
        {
            chords.Add($"Ctrl+Alt+{level}");
            chords.Add($"Ctrl+Alt+Shift+{level}");
        }

        return chords;

    }

    private static HashSet<string> ReadingNavigatorChords()
    {
        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (InvocationExpressionSyntax call in AddChordCalls(NavigatorRegistration()))
        {
            // Only the literal Key.X registrations; the computed
            // heading-level ones are projected from their loop instead.
            if (call.ArgumentList.Arguments[0].Expression
                    is not MemberAccessExpressionSyntax access
                || CSharpSource.Normalize(access.Expression) != "Key")
            {
                continue;
            }

            string shift = ShiftArgument(call) ? "Shift+" : string.Empty;
            chords.Add($"Ctrl+Alt+{shift}{access.Name.Identifier.ValueText}");
        }

        Assert.Equal(20, chords.Count);
        return chords;

    }

    private static HashSet<string> GridGestureChords()
    {
        CSharpSource grid = CSharpSource.Load("Grids", "AccessibleDataGrid.cs");
        var chords = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (ObjectCreationExpressionSyntax gesture in grid.Root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type is IdentifierNameSyntax type
                && type.Identifier.ValueText == "KeyGesture"))
        {
            ArgumentSyntax[] arguments = gesture.ArgumentList!.Arguments.ToArray();
            Assert.True(arguments.Length >= 2, $"KeyGesture {gesture} has no modifiers.");
            chords.Add(Canonical(
                CSharpSource.Normalize(arguments[1].Expression)
                    .Replace("ModifierKeys.", string.Empty)
                    .Replace("|", "+"),
                CSharpSource.KeyNames(arguments[0].Expression).Single()));
        }

        Assert.Equal(2, chords.Count);
        return chords;
    }

    /// <summary>WPF's <c>Key</c> / <c>Modifiers</c> spellings → the table's
    /// canonical chord string.</summary>
    private static string Canonical(string? modifiers, string key)
    {
        string[] tokens = (modifiers ?? string.Empty)
            .Split('+', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .ToArray();
        var parts = new List<string>(4);
        if (tokens.Contains("Control"))
        {
            parts.Add("Ctrl");
        }

        if (tokens.Contains("Alt"))
        {
            parts.Add("Alt");
        }

        if (tokens.Contains("Shift"))
        {
            parts.Add("Shift");
        }

        parts.Add(key switch
        {
            "Oem5" => "\\",
            "OemOpenBrackets" => "[",
            "OemCloseBrackets" => "]",
            "OemPlus" => "=",
            "OemMinus" => "-",
            "OemComma" => ",",
            "OemPeriod" => ".",
            "Return" => "Enter",
            "Back" => "Backspace",
            _ => key,
        });
        return string.Join("+", parts);
    }

    private static string ChordsPath() =>
        Path.Combine(RepoRoot(), "apps", "slate-windows", "chords.json");

    private static string SourceRoot() =>
        Path.Combine(RepoRoot(), "apps", "slate-windows", "src", "SlateWindows");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
