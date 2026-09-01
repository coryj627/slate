// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1118: the two W4-era sheet-opening chords (Ctrl+Shift+R bulk
/// rename, Ctrl+Shift+J citation summary) rode bare Window KeyBindings
/// with no modal admission, so they presented their sheet BENEATH a
/// higher sheet and pulled keyboard focus into an invisible surface;
/// under Quick Open they (and Ctrl+J, Ctrl+R, Ctrl+Shift+E) fell
/// through the deny-list and fired beneath the picker. These facts pin
/// the fix: a decision table per opener (the template flow's arms), the
/// admission wired and consulted inside the workspace's open so every
/// opener — chord, menu, palette row — passes one gate, and a deny-list
/// that answers every Window KeyBinding chord.
/// </summary>
public sealed class SheetChordAdmissionTests
{
    // ---- the decision tables --------------------------------------------

    [Theory]
    [InlineData(null, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.DismissQuickOpenThenOpen)]
    [InlineData(ModalSurface.SearchOverlay, PaletteOpenDecision.DismissSearchThenOpen)]
    [InlineData(ModalSurface.CommandPalette, PaletteOpenDecision.DismissPaletteThenOpen)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplatePicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CanvasCardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplateFlow, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.MoveTo, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CanvasCardPicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CanvasPrompt, PaletteOpenDecision.Refuse)]
    public void BulkRenameSupersedesTheOverlaysAndDefersToEverySheet(
        object? topmost, object expected)
    {
        ModalSurface? surface = topmost is null ? null : (ModalSurface)topmost;
        Assert.Equal(
            (PaletteOpenDecision)expected,
            ModalSurfaces.DecideBulkRenameOpen(surface));
    }

    /// <summary>The citation summary keeps W4-5's one deliberate stacking
    /// (it paints above, and opens over, the details sheet) and treats
    /// re-opening over itself as a refresh; every other sheet refuses.</summary>
    [Theory]
    [InlineData(null, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.DismissQuickOpenThenOpen)]
    [InlineData(ModalSurface.SearchOverlay, PaletteOpenDecision.DismissSearchThenOpen)]
    [InlineData(ModalSurface.CommandPalette, PaletteOpenDecision.DismissPaletteThenOpen)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplatePicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CanvasCardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplateFlow, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.MoveTo, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CanvasCardPicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CanvasPrompt, PaletteOpenDecision.Refuse)]
    public void CitationSummarySupersedesTheOverlaysStacksOnDetailsAndDefersToOtherSheets(
        object? topmost, object expected)
    {
        ModalSurface? surface = topmost is null ? null : (ModalSurface)topmost;
        Assert.Equal(
            (PaletteOpenDecision)expected,
            ModalSurfaces.DecideCitationSummaryOpen(surface));
    }

    /// <summary>Both tables cover every surface — a member added without a
    /// row falls into the wildcard Refuse arm and looks correct while
    /// nothing asserts it (the round-3 shape).</summary>
    [Fact]
    public void BothTablesAnswerEveryModalSurface()
    {
        int members = Enum.GetValues<ModalSurface>().Length;
        foreach (string theory in new[]
        {
            nameof(BulkRenameSupersedesTheOverlaysAndDefersToEverySheet),
            nameof(CitationSummarySupersedesTheOverlaysStacksOnDetailsAndDefersToOtherSheets),
        })
        {
            // One row per member plus the null (nothing open) row — the
            // theory must grow with the enum.
            int rows = typeof(SheetChordAdmissionTests)
                .GetMethod(theory)!
                .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false)
                .Length;
            Assert.True(
                rows == members + 1,
                $"{theory} lists {rows} rows for {members} ModalSurface members (+ null)");
        }
    }

    // ---- the admission is wired and consulted ----------------------------

    /// <summary>
    /// The one-gate design is WIRED, not just designed (the W5-3
    /// template pin's four legs, for both openers): the window hands the
    /// workspace each admission, the workspace consults it BEFORE any
    /// presentation, and each admission consults its decision and can
    /// perform every dismissal arm plus the refusal announcement.
    /// </summary>
    [Fact]
    public void TheSheetOpenersShareOneWiredAdmission()
    {
        // (a) The window wires both seams.
        MethodDeclarationSyntax wireProperties =
            CSharpSource.Load("MainWindow.Properties.cs").Method("WireWorkspaceProperties");
        Assert.Contains(
            "workspace.BulkRenameOpenAdmission=TryClearTheWayForBulkRename",
            CSharpSource.Normalize(wireProperties),
            StringComparison.Ordinal);
        MethodDeclarationSyntax wireCitations =
            CSharpSource.Load("MainWindow.Citations.cs").Method("WireWorkspaceCitations");
        Assert.Contains(
            "workspace.CitationSummaryOpenAdmission=TryClearTheWayForCitationSummary",
            CSharpSource.Normalize(wireCitations),
            StringComparison.Ordinal);

        // (b) The workspace consults each seam BEFORE presenting.
        MethodDeclarationSyntax openRename =
            CSharpSource.Load("WorkspaceViewModel.Properties.cs").Method("OpenBulkRenameSheet");
        string renameText = CSharpSource.Normalize(openRename);
        int renameAdmission = renameText.IndexOf(
            "BulkRenameOpenAdmission?.Invoke()==false", StringComparison.Ordinal);
        int renamePresent = renameText.IndexOf("BulkRenameSheet=sheet", StringComparison.Ordinal);
        Assert.True(
            renameAdmission >= 0 && renamePresent > renameAdmission,
            "OpenBulkRenameSheet no longer consults BulkRenameOpenAdmission before presenting.");

        MethodDeclarationSyntax openSummary =
            CSharpSource.Load("WorkspaceViewModel.Citations.cs").Method("OpenCitationSummary");
        string summaryText = CSharpSource.Normalize(openSummary);
        int summaryAdmission = summaryText.IndexOf(
            "CitationSummaryOpenAdmission?.Invoke()==false", StringComparison.Ordinal);
        int summaryPresent = summaryText.IndexOf(
            "CitationSummary=newCitationSummaryViewModel", StringComparison.Ordinal);
        int summaryPark = summaryText.IndexOf("_summaryParked=true", StringComparison.Ordinal);
        Assert.True(
            summaryAdmission >= 0
                && summaryPresent > summaryAdmission
                && summaryPark > summaryAdmission,
            "OpenCitationSummary no longer consults CitationSummaryOpenAdmission "
            + "before presenting or parking.");

        // (c) Each admission consults its decision and can perform every
        // dismissal arm plus the refusal announcement.
        foreach ((string file, string method, string decision) in new[]
        {
            ("MainWindow.Properties.cs", "TryClearTheWayForBulkRename",
                "ModalSurfaces.DecideBulkRenameOpen"),
            ("MainWindow.Citations.cs", "TryClearTheWayForCitationSummary",
                "ModalSurfaces.DecideCitationSummaryOpen"),
        })
        {
            MethodDeclarationSyntax admission = CSharpSource.Load(file).Method(method);
            Assert.True(
                CSharpSource.Invokes(admission, decision),
                $"{method} no longer consults {decision}.");
            string admissionText = CSharpSource.Normalize(admission);
            foreach (string arm in new[]
            {
                "_viewModel.QuickSwitcher!.Dismiss",
                "_viewModel.Search.Supersede",
                "_viewModel.Palette.Dismiss",
                "_announcer.Post",
            })
            {
                Assert.Contains(arm, admissionText, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>A refused admission presents nothing; an admitted one (and
    /// a headless null seam) presents — the chord, the menu, and a
    /// palette row all arrive here.</summary>
    [Fact]
    public void TheWorkspaceHonorsTheBulkRenameAdmission()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sheet-admission-rename");
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        workspace.OpenPath("note0.md");

        int consulted = 0;
        workspace.BulkRenameOpenAdmission = () =>
        {
            consulted++;
            return false;
        };
        workspace.OpenBulkRenameSheetCommand.Execute(null);
        Assert.Equal(1, consulted);
        Assert.Null(workspace.BulkRenameSheet);

        workspace.BulkRenameOpenAdmission = () =>
        {
            consulted++;
            return true;
        };
        workspace.OpenBulkRenameSheetCommand.Execute(null);
        Assert.Equal(2, consulted);
        Assert.NotNull(workspace.BulkRenameSheet);
        workspace.CloseBulkRenameSheetCommand.Execute(null);

        workspace.BulkRenameOpenAdmission = null;
        workspace.OpenBulkRenameSheet(synchronousForTests: true);
        Assert.NotNull(workspace.BulkRenameSheet);
    }

    [Fact]
    public void TheWorkspaceHonorsTheCitationSummaryAdmission()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "sheet-admission-summary");
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        workspace.OpenPath("note0.md");

        int consulted = 0;
        workspace.CitationSummaryOpenAdmission = () =>
        {
            consulted++;
            return false;
        };
        workspace.OpenCitationSummaryCommand.Execute(null);
        Assert.Equal(1, consulted);
        Assert.Null(workspace.CitationSummary);

        workspace.CitationSummaryOpenAdmission = () =>
        {
            consulted++;
            return true;
        };
        workspace.OpenCitationSummaryCommand.Execute(null);
        Assert.Equal(2, consulted);
        Assert.NotNull(workspace.CitationSummary);
    }

    // ---- the Quick Open deny-list -----------------------------------------

    /// <summary>The five chords the issue named: under Quick Open they fell
    /// through to the Window KeyBindings (the palette and search swallow
    /// them through the text-editing allow-list; Quick Open relies on the
    /// deny-list alone).</summary>
    [Theory]
    [InlineData(Key.J, ModifierKeys.Control)]
    [InlineData(Key.J, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.R, ModifierKeys.Control)]
    [InlineData(Key.R, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.E, ModifierKeys.Control | ModifierKeys.Shift)]
    public void TheDenyListAnswersTheW4ChordsItNeverTracked(Key key, ModifierKeys modifiers) =>
        Assert.True(MainWindow.IsUnderlyingShellShortcutForTests(key, modifiers));

    /// <summary>
    /// The general rule the five were instances of: every chord the
    /// shell delivers through <c>Window.InputBindings</c> must be
    /// answered by the deny-list, or it fires beneath Quick Open. Read
    /// from the XAML so a new KeyBinding cannot ship untracked; the
    /// modifier-less Escape binding is the picker's own key and is
    /// handled before the list is consulted.
    /// </summary>
    [Fact]
    public void TheDenyListAnswersEveryWindowKeyBinding()
    {
        string xaml = Path.Combine(ShellSourceDirectory, "MainWindow.xaml");
        Assert.True(File.Exists(xaml), $"shell XAML missing at {xaml}");
        XDocument document = XDocument.Load(xaml);
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XElement bindings = document.Root!
            .Elements(presentation + "Window.InputBindings")
            .Single();
        var unanswered = new List<string>();
        int checkedBindings = 0;
        foreach (XElement binding in bindings.Elements(presentation + "KeyBinding"))
        {
            string keyName = binding.Attribute("Key")!.Value;
            string? modifierNames = binding.Attribute("Modifiers")?.Value;
            if (modifierNames is null)
            {
                continue;
            }

            Key key = Enum.Parse<Key>(keyName);
            ModifierKeys modifiers = ModifierKeys.None;
            foreach (string token in modifierNames.Split('+', StringSplitOptions.RemoveEmptyEntries))
            {
                modifiers |= Enum.Parse<ModifierKeys>(token);
            }

            checkedBindings++;
            if (!MainWindow.IsUnderlyingShellShortcutForTests(key, modifiers))
            {
                unanswered.Add($"{modifierNames}+{keyName}");
            }
        }

        Assert.True(checkedBindings > 10, "the KeyBinding scrape found too few bindings");
        Assert.True(
            unanswered.Count == 0,
            "Window KeyBindings the Quick Open deny-list does not answer (they fire "
            + "beneath the picker): " + string.Join(", ", unanswered));
    }

    private static string ShellSourceDirectory
    {
        get
        {
            // tests/.../bin/<cfg>/net10.0-windows/ -> apps/slate-windows is six
            // hops above (the FocusableLayoutHostCensus walk).
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir)!;
            }
            return Path.Combine(dir, "src", "SlateWindows");
        }
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
