// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Canvas;
using SlateWindows.Templates;

namespace SlateWindows.Tests;

/// <summary>
/// #1866's facts for plan 47 CB-17b: each of the fourteen sheets is opened on the real
/// window by the product's own route, and nothing that holds the keys goes off before they
/// have left it. On main a sheet's landing is queued, so the menu and the empty pane's
/// launcher rows go off in the turn the sheet opens, with the keys still on them, and NVDA
/// says "unavailable" before the sheet's first stop. These facts are red until the product
/// change lands: a sheet takes the keys in the turn it opens, and the menu and the launcher
/// stay on while they hold the keys.
/// </summary>
public sealed class SheetOpeningTests
{
    private const string Note = "a.md";
    private const string Board = "board.canvas";

    /// <summary>The three surfaces that are not sheets. Each takes the keys in the turn it
    /// opens already (#1867); CommandPaletteTests holds them.</summary>
    private static readonly ModalSurface[] Overlays =
        [ModalSurface.QuickOpen, ModalSurface.SearchOverlay, ModalSurface.CommandPalette];

    /// <summary>The menu, with the keys on its bar and on an item of its open Slate menu, as
    /// each sheet opens over it: the menu goes off only once the keys have left it. The menu
    /// cases skip where the desktop refuses a menu the keys (#1680). The template flow opens
    /// only from the template picker, so its act opens the picker first.</summary>
    [Theory]
    [InlineData(nameof(WorkspaceViewModel.AddPropertySheet), false)]
    [InlineData(nameof(WorkspaceViewModel.BulkRenameSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.CitationDetails), false)]
    [InlineData(nameof(WorkspaceViewModel.CitationSummary), false)]
    [InlineData(nameof(WorkspaceViewModel.FilesCiting), false)]
    [InlineData(nameof(WorkspaceViewModel.DashboardEditorSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.BaseQueryBuilderSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.TemplatePickerSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.TemplateFlowSheet), false)]
    [InlineData(nameof(FilesSidebarViewModel.MoveToSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardEditorSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardPickerSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.CanvasPromptSheet), false)]
    [InlineData(nameof(VaultLifecycleViewModel.SettingsSheet), false)]
    [InlineData(nameof(WorkspaceViewModel.AddPropertySheet), true)]
    [InlineData(nameof(WorkspaceViewModel.BulkRenameSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.CitationDetails), true)]
    [InlineData(nameof(WorkspaceViewModel.CitationSummary), true)]
    [InlineData(nameof(WorkspaceViewModel.FilesCiting), true)]
    [InlineData(nameof(WorkspaceViewModel.DashboardEditorSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.BaseQueryBuilderSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.TemplatePickerSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.TemplateFlowSheet), true)]
    [InlineData(nameof(FilesSidebarViewModel.MoveToSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardEditorSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardPickerSheet), true)]
    [InlineData(nameof(WorkspaceViewModel.CanvasPromptSheet), true)]
    [InlineData(nameof(VaultLifecycleViewModel.SettingsSheet), true)]
    public void NothingHoldingTheKeysGoesOffAsASheetOpens(string sheetToken, bool inAnOpenMenu) => RunSta(() =>
    {
        Sheet sheet = SheetOf(sheetToken);
        using var bench = new Bench();
        bench.OpenTheTab(sheet.Tab);
        sheet.Prepare(bench);
        Menu menu = bench.Shell.MainMenu;
        MenuItem top = menu.Items.OfType<MenuItem>().First();
        MenuItem item = top;
        bench.Shell.Activate();
        bench.Host.Settle();
        if (inAnOpenMenu)
        {
            top.IsSubmenuOpen = true;
            bench.Host.Settle();
            item = top.Items.OfType<MenuItem>().FirstOrDefault(candidate => candidate.IsEnabled && candidate.IsVisible)
                ?? KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu("no item of the open menu is on screen");
        }

        KeysFirstRecorder.TakeTheMenuKeys(item);
        bench.Host.Settle();
        Assert.True(menu.IsEnabled, "premise: the menu is on with no sheet open");

        // The menu itself is the root: an open menu's item lives in its popup and does not
        // go off with the menu, but the recorder counts the keys on it as inside the menu.
        KeysFirstRecorder.AssertTheKeysLeaveFirst($"the menu, as {sheet.Token} opened,", menu, () => bench.OpenAndSettle(sheet));

        bench.AssertTheSheetHasTheKeys(sheet);
        Assert.False(menu.IsEnabled, $"the menu stayed on under {sheet.Token}");
        Assert.False(top.IsSubmenuOpen, $"the menu stayed open under {sheet.Token}");
    });

    /// <summary>A launcher row holding the keys as a sheet opens over the empty pane: the
    /// row goes off only once the keys have left it. Every sheet that can open with no tab
    /// open is here. The three canvas sheets are not: each opens only for the canvas of the
    /// active tab, and a pane left empty beside another is removed, so the launcher shows
    /// only when no tab is open at all.</summary>
    [Theory]
    [InlineData(nameof(WorkspaceViewModel.AddPropertySheet))]
    [InlineData(nameof(WorkspaceViewModel.BulkRenameSheet))]
    [InlineData(nameof(WorkspaceViewModel.CitationDetails))]
    [InlineData(nameof(WorkspaceViewModel.CitationSummary))]
    [InlineData(nameof(WorkspaceViewModel.FilesCiting))]
    [InlineData(nameof(WorkspaceViewModel.DashboardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.BaseQueryBuilderSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplatePickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplateFlowSheet))]
    [InlineData(nameof(FilesSidebarViewModel.MoveToSheet))]
    [InlineData(nameof(VaultLifecycleViewModel.SettingsSheet))]
    public void NoLauncherRowHoldingTheKeysGoesOffAsASheetOpens(string sheetToken) => RunSta(() =>
    {
        Sheet sheet = SheetOf(sheetToken);
        Assert.True(sheet.NotOverAnEmptyPane is null, $"{sheet.Token} cannot open over the empty pane: {sheet.NotOverAnEmptyPane}");
        using var bench = new Bench();
        sheet.Prepare(bench);
        Button row = LauncherRow(bench.Host, "EmptyPaneQuickOpen");
        Assert.Same(row.FindResource("Slate.LauncherRowStyle"), row.Style);
        Assert.True(row.IsVisible, "premise: no tab is open, so the empty pane shows its launcher");
        bench.Shell.Activate();
        bench.Host.Settle();
        TakeTheKeys(row);

        KeysFirstRecorder.AssertTheKeysLeaveFirst($"the launcher's row, as {sheet.Token} opened,", row, () => bench.OpenAndSettle(sheet));

        // The launcher style's sheet trigger reads this.
        Assert.True(bench.Shell.IsModalSheetOpen, $"premise: {sheet.Token} is a sheet to the launcher");
        bench.AssertTheSheetHasTheKeys(sheet);
        Assert.False(row.IsEnabled, $"the launcher's row stayed on under {sheet.Token}");
    });

    /// <summary>A sheet's first stop holds the keys by the end of the turn the sheet opens
    /// in, with no dispatcher turn in between, so the view under it is off before any key,
    /// text or click already queued is delivered. The keys start in the editor, or on the
    /// canvas for the three canvas sheets, or in the template picker for the template flow.
    /// The template picker is held only to the keys being inside it: it opens loading, and
    /// its list is filled later in the same call.</summary>
    [Theory]
    [InlineData(nameof(WorkspaceViewModel.AddPropertySheet))]
    [InlineData(nameof(WorkspaceViewModel.BulkRenameSheet))]
    [InlineData(nameof(WorkspaceViewModel.CitationDetails))]
    [InlineData(nameof(WorkspaceViewModel.CitationSummary))]
    [InlineData(nameof(WorkspaceViewModel.FilesCiting))]
    [InlineData(nameof(WorkspaceViewModel.DashboardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.BaseQueryBuilderSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplatePickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplateFlowSheet))]
    [InlineData(nameof(FilesSidebarViewModel.MoveToSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardPickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasPromptSheet))]
    [InlineData(nameof(VaultLifecycleViewModel.SettingsSheet))]
    public void ASheetTakesTheKeysInTheTurnItOpens(string sheetToken) => RunSta(() =>
    {
        Sheet sheet = SheetOf(sheetToken);
        using var bench = new Bench();
        bench.OpenTheTab(sheet.Tab);
        sheet.Prepare(bench);
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();
        sheet.Approach?.Invoke(bench);
        string before = Describe(Keyboard.FocusedElement);
        Assert.True(Keyboard.FocusedElement is UIElement { IsEnabled: true, IsVisible: true }, $"keyboard premise: before {sheet.Token} opened the keys were on {before}");

        IInputElement? inTheTurn = bench.OpenAndReadTheKeysInThatTurn(sheet);
        bench.Host.Settle();

        Assert.Equal(sheet.Surface, bench.Shell.OpenModalSurface);
        FrameworkElement overlay = bench.Overlay(sheet);
        if (sheet.Stop is null)
        {
            Assert.True(
                IsIn(overlay, inTheTurn),
                $"in the turn {sheet.Token} opened, the keys were on {Describe(inTheTurn)}, outside it; before it opened they were on {before}");
            return;
        }

        UIElement? stop = sheet.Stop(bench);
        Assert.True(stop is not null, $"{sheet.Token} has no first stop on screen once it has settled");
        Assert.True(
            ReferenceEquals(stop, inTheTurn),
            $"in the turn {sheet.Token} opened, the keys were on {Describe(inTheTurn)}, not on its first stop, {Describe(stop)}; "
            + $"before it opened they were on {before}");
    });

    /// <summary>The template picker opened from its own launcher row and closed by Escape
    /// gives the keys back to that row. On main the row goes off under the keys as the picker
    /// opens, WPF moves them to the pane's host, and the close leaves them there.</summary>
    [Theory]
    [InlineData("Enter")]
    [InlineData("Invoke")]
    public void ClosingAPickerOpenedFromTheLauncherReturnsTheKeysToItsRow(string press) => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"), ("Templates/Meeting.md", "# Meeting\n"));
        host.Settle();
        Button row = LauncherRow(host, "EmptyPaneNewFromTemplate");
        host.Shell.Activate();
        host.Settle();
        TakeTheKeys(row);

        if (press == "Enter")
        {
            Press(host, Key.Enter);
        }
        else
        {
            ((IInvokeProvider)new ButtonAutomationPeer(row).GetPattern(PatternInterface.Invoke)).Invoke();
        }

        host.Settle();
        host.Settle();
        TemplatePickerViewModel picker = Assert.IsType<TemplatePickerViewModel>(host.Workspace.TemplatePickerSheet);
        Assert.Equal(TemplatePickerState.Available, picker.State);
        _ = Assert.Single(picker.Rows);
        Assert.True(
            host.Shell.TemplatePickerOverlay.IsKeyboardFocusWithin,
            $"premise: with the picker open the keys are on {Describe(Keyboard.FocusedElement)}, outside it");

        Press(host, Key.Escape);
        host.Settle();
        host.Settle();

        Assert.Null(host.Shell.OpenModalSurface);
        Assert.True(row.IsEnabled, "the launcher's row stayed off once the picker had closed");
        Assert.True(
            ReferenceEquals(row, Keyboard.FocusedElement),
            $"once the picker opened by {press} on its launcher row had closed, the keys rested on {Describe(Keyboard.FocusedElement)}, not on that row");
    });

    /// <summary>A sheet whose first stop cannot take the keys in the turn it opens still
    /// takes them out of what held them: the sheet itself holds them, so a key or text
    /// delivered next reaches the sheet and nothing under it, and what held them is off. Once
    /// the stop can take them, the queued landing gives it the keys, and the sheet is no
    /// stop again. The keys start on the empty pane's Quick Open… row for each sheet that can
    /// open over it; on the canvas for the three canvas sheets; in the template picker for
    /// the template flow, which opens only from it.</summary>
    [Theory]
    [InlineData(nameof(WorkspaceViewModel.AddPropertySheet))]
    [InlineData(nameof(WorkspaceViewModel.BulkRenameSheet))]
    [InlineData(nameof(WorkspaceViewModel.CitationDetails))]
    [InlineData(nameof(WorkspaceViewModel.CitationSummary))]
    [InlineData(nameof(WorkspaceViewModel.FilesCiting))]
    [InlineData(nameof(WorkspaceViewModel.DashboardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.BaseQueryBuilderSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplatePickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplateFlowSheet))]
    [InlineData(nameof(FilesSidebarViewModel.MoveToSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardPickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasPromptSheet))]
    [InlineData(nameof(VaultLifecycleViewModel.SettingsSheet))]
    public void ALateLandingTakesTheKeysOutOfWhatIsUnderTheSheet(string sheetToken) => RunSta(() =>
    {
        Sheet sheet = SheetOf(sheetToken);
        using var bench = new Bench();
        UIElement holder = bench.TakeTheKeysUnder(sheet);
        FrameworkElement overlay = bench.Overlay(sheet);
        int reached = 0;
        void Reached(object sender, RoutedEventArgs e) => reached++;
        holder.PreviewKeyDown += Reached;
        holder.PreviewTextInput += Reached;

        IInputElement? inTheTurn;
        using (RefuseTheKeysInside(overlay))
        {
            inTheTurn = bench.OpenAndReadTheKeysInThatTurn(sheet);
            Assert.True(
                ReferenceEquals(overlay, inTheTurn),
                $"in the turn {sheet.Token} opened, with its stops refusing the keys, they were on {Describe(inTheTurn)}, not on the sheet itself");
            Assert.False(KeysFirstRecorder.TheKeysAreIn(holder), $"the keys stayed on {Describe(holder)} under {sheet.Token}");
            Assert.False(KeyboardNavigation.GetIsTabStop(overlay), $"{sheet.Token} holding the keys is a Tab stop");

            Press(bench.Host, Key.A);
            TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, Keyboard.FocusedElement, "a"));
            Assert.Equal(0, reached);
            if (holder is Button row)
            {
                Assert.False(row.IsEnabled, $"{Describe(row)} stayed on under {sheet.Token}");
                var invoke = (IInvokeProvider)new ButtonAutomationPeer(row).GetPattern(PatternInterface.Invoke);
                _ = Assert.Throws<ElementNotEnabledException>(invoke.Invoke);
            }
        }

        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(sheet.Surface, bench.Shell.OpenModalSurface);
        Assert.Equal(0, reached);
        Assert.False(overlay.Focusable, $"{sheet.Token} stayed a stop once its landing took the keys");
        Assert.True(
            Keyboard.FocusedElement is UIElement landed && !ReferenceEquals(landed, overlay) && KeysFirstRecorder.TheKeysAreIn(overlay),
            $"once its stops could take the keys, {sheet.Token}'s queued landing left them on {Describe(Keyboard.FocusedElement)}");
        IInputElement settled = Keyboard.FocusedElement;
        bench.Host.Settle();
        Assert.Same(settled, Keyboard.FocusedElement);
    });

    /// <summary>Escape closes a sheet whose stops could not take the keys in the turn it
    /// opened: the sheet itself holds them, so the key reaches the sheet's own route.</summary>
    [Theory]
    [InlineData(nameof(WorkspaceViewModel.AddPropertySheet))]
    [InlineData(nameof(WorkspaceViewModel.BulkRenameSheet))]
    [InlineData(nameof(WorkspaceViewModel.CitationDetails))]
    [InlineData(nameof(WorkspaceViewModel.CitationSummary))]
    [InlineData(nameof(WorkspaceViewModel.FilesCiting))]
    [InlineData(nameof(WorkspaceViewModel.DashboardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.BaseQueryBuilderSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplatePickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.TemplateFlowSheet))]
    [InlineData(nameof(FilesSidebarViewModel.MoveToSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardEditorSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasCardPickerSheet))]
    [InlineData(nameof(WorkspaceViewModel.CanvasPromptSheet))]
    [InlineData(nameof(VaultLifecycleViewModel.SettingsSheet))]
    public void EscapeBeforeALateLandingStillClosesTheSheet(string sheetToken) => RunSta(() =>
    {
        Sheet sheet = SheetOf(sheetToken);
        using var bench = new Bench();
        _ = bench.TakeTheKeysUnder(sheet);
        FrameworkElement overlay = bench.Overlay(sheet);
        using (RefuseTheKeysInside(overlay))
        {
            IInputElement? inTheTurn = bench.OpenAndReadTheKeysInThatTurn(sheet);
            Assert.True(
                ReferenceEquals(overlay, inTheTurn),
                $"in the turn {sheet.Token} opened, with its stops refusing the keys, they were on {Describe(inTheTurn)}, not on the sheet itself");

            Press(bench.Host, Key.Escape);
        }

        bench.Host.Settle();
        Assert.NotEqual(sheet.Surface, bench.Shell.OpenModalSurface);
        Assert.False(overlay.Focusable, $"{sheet.Token} stayed a stop once it closed");
    });

    /// <summary>A sheet that opens beneath another surface leaves the keys where the
    /// surface above it put them and is no stop: the card editor of a new card arriving
    /// after Settings opened (#1666). It takes them on its field once Settings closes.</summary>
    [Fact]
    public void ASheetThatOpensBeneathAnotherLeavesTheKeysAlone() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Board);
        bench.SelectTheCard("alpha");
        bench.Shell.Activate();
        bench.Host.Settle();
        bench.Host.Lifecycle.OpenSettingsCommand.Execute(null);
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.Settings, bench.Shell.OpenModalSurface);
        IInputElement inSettings = Keyboard.FocusedElement;
        Assert.True(KeysFirstRecorder.TheKeysAreIn(bench.Shell.SettingsOverlay), $"premise: Settings holds the keys (they are on {Describe(inSettings)})");
        FrameworkElement editor = bench.Shell.CanvasCardEditorOverlay;

        IInputElement? inTheTurn = bench.OpenAndReadTheKeysInThatTurn(SheetOf(nameof(WorkspaceViewModel.CanvasCardEditorSheet)), awaitedSurface: ModalSurface.Settings);
        Assert.NotNull(bench.Workspace.CanvasCardEditorSheet);
        Assert.Equal(ModalSurface.Settings, bench.Shell.OpenModalSurface);
        Assert.Same(inSettings, inTheTurn);
        Assert.False(editor.Focusable, "the card editor beneath Settings became a stop");
        bench.Host.Settle();
        Assert.Same(inSettings, Keyboard.FocusedElement);

        bench.Host.Lifecycle.CloseSettings();
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.CanvasCardEditor, bench.Shell.OpenModalSurface);
        Assert.Same(bench.Shell.CanvasCardEditorTextBox, Keyboard.FocusedElement);
    });

    /// <summary>The template picker opened by Enter on its launcher row opens loading and is
    /// filled later in the same call: its Cancel never holds the keys on the way, so a
    /// reader hears the list, not "Cancel, button" first.</summary>
    [Fact]
    public void TheTemplatePickerNeverLandsOnCancelWhileItFills() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"), ("Templates/Meeting.md", "# Meeting\n"));
        host.Settle();
        Button row = LauncherRow(host, "EmptyPaneNewFromTemplate");
        host.Shell.Activate();
        host.Settle();
        TakeTheKeys(row);
        Button cancel = host.Shell.TemplatePickerCancelButton;
        int onCancel = 0;
        void Counted(object sender, KeyboardFocusChangedEventArgs e) => onCancel++;
        cancel.GotKeyboardFocus += Counted;

        Press(host, Key.Enter);
        host.Settle();
        host.Settle();

        TemplatePickerViewModel picker = Assert.IsType<TemplatePickerViewModel>(host.Workspace.TemplatePickerSheet);
        Assert.Equal(TemplatePickerState.Available, picker.State);
        Assert.Equal(0, onCancel);
        Assert.True(
            Keyboard.FocusedElement is ListBoxItem item && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), host.Shell.TemplatePickerList),
            $"with the picker filled the keys are on {Describe(Keyboard.FocusedElement)}, not on a row of its list");
    });

    /// <summary>Every member of <see cref="ModalSurface"/> is one of the three overlays or
    /// has an opener, and every sheet has its rows in the theories above. The opener map has
    /// no wildcard arm, so a fifteenth sheet without an opener is a build warning (CS8509)
    /// and fails here; one with an opener and no rows fails here too.</summary>
    [Fact]
    public void EverySheetHasAnOpenerAndItsRows()
    {
        ModalSurface[] members = Enum.GetValues<ModalSurface>();
        Assert.Equal(17, members.Length);
        Assert.All(Overlays, overlay => Assert.Throws<ArgumentOutOfRangeException>(() => Opener(overlay)));
        Sheet[] sheets = Sheets();
        Assert.Equal(14, sheets.Length);
        Assert.Equal(sheets.Length, sheets.Select(sheet => sheet.Token).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(sheets.Length, sheets.Select(sheet => sheet.Overlay).Distinct(StringComparer.Ordinal).Count());
        string[] every = [.. sheets.Select(sheet => sheet.Token).Order(StringComparer.Ordinal)];

        Assert.Equal(every, RowsOf(nameof(NothingHoldingTheKeysGoesOffAsASheetOpens), row => row[1] is false));
        Assert.Equal(every, RowsOf(nameof(NothingHoldingTheKeysGoesOffAsASheetOpens), row => row[1] is true));
        Assert.Equal(every, RowsOf(nameof(ASheetTakesTheKeysInTheTurnItOpens), _ => true));
        Assert.Equal(every, RowsOf(nameof(ALateLandingTakesTheKeysOutOfWhatIsUnderTheSheet), _ => true));
        Assert.Equal(every, RowsOf(nameof(EscapeBeforeALateLandingStillClosesTheSheet), _ => true));
        Assert.Equal(
            sheets.Where(sheet => sheet.NotOverAnEmptyPane is null).Select(sheet => sheet.Token).Order(StringComparer.Ordinal),
            RowsOf(nameof(NoLauncherRowHoldingTheKeysGoesOffAsASheetOpens), _ => true));
    }

    private static string[] RowsOf(string theory, Func<object?[], bool> which) =>
    [
        .. typeof(SheetOpeningTests).GetMethod(theory)!.GetCustomAttributes<InlineDataAttribute>()
            .Select(row => row.Data)
            .Where(which)
            .Select(row => (string)row[0]!)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>One sheet: the surface it is, the token of the view model property that
    /// holds it (the menu's trigger reads that property), its overlay's name in the window,
    /// the tab it opens over, what its route needs before the keys are taken, the steps of
    /// its route before the call that opens it, that call, and its first stop.</summary>
    /// <param name="Stop">The stop its landing takes; null where the sheet has none yet in
    /// the turn it opens.</param>
    /// <param name="NotOverAnEmptyPane">Why it cannot open with no tab open; null when it
    /// can.</param>
    private sealed record Sheet(
        ModalSurface Surface,
        string Token,
        string Overlay,
        string Tab,
        Action<Bench> Prepare,
        Action<Bench>? Approach,
        Action<Bench> Open,
        Func<Bench, UIElement?>? Stop,
        string? NotOverAnEmptyPane = null);

    private const string CanvasNeedsItsTab =
        "it opens only for the canvas of the active tab, and a pane left empty beside another is removed, "
        + "so the launcher shows only with no tab open";

    private static Sheet SheetOf(string token) => Assert.Single(Sheets(), sheet => sheet.Token == token);

    private static Sheet[] Sheets() =>
        [.. Enum.GetValues<ModalSurface>().Where(surface => !Overlays.Contains(surface)).Select(Opener)];

    /// <summary>How each sheet is opened for real: the command or the view model call the
    /// app's own chord, row or button makes. No wildcard arm.</summary>
#pragma warning disable CS8524 // An unnamed value cast to the enum has no opener, and none would be right for it.
    private static Sheet Opener(ModalSurface surface) => surface switch
    {
        ModalSurface.QuickOpen or ModalSurface.SearchOverlay or ModalSurface.CommandPalette =>
            throw new ArgumentOutOfRangeException(nameof(surface), surface, "not a sheet: an overlay takes the keys in the turn it opens"),
        ModalSurface.AddProperty => new Sheet(
            surface,
            nameof(WorkspaceViewModel.AddPropertySheet),
            "AddPropertyOverlay",
            Note,
            Prepare: static _ => { },
            Approach: null,
            Open: static bench => bench.Workspace.OpenAddPropertySheetCommand.Execute(null),
            Stop: static bench => bench.Shell.AddPropertyKeyTextBox),
        ModalSurface.BulkRename => new Sheet(
            surface,
            nameof(WorkspaceViewModel.BulkRenameSheet),
            "BulkRenameOverlay",
            Note,
            Prepare: static _ => { },
            Approach: null,
            Open: static bench => bench.Workspace.OpenBulkRenameSheetCommand.Execute(null),
            Stop: static bench => bench.Shell.BulkRenameOldKeyTextBox),
        ModalSurface.CitationDetails => new Sheet(
            surface,
            nameof(WorkspaceViewModel.CitationDetails),
            "CitationDetailsOverlay",
            Note,
            Prepare: static bench => bench.LoadTheBibliography(),
            Approach: null,
            // The Bibliography leaf's Enter on an entry.
            Open: static bench => bench.Workspace.OpenEntryDetails(bench.Workspace.Bibliography.Entries[0].Entry),
            Stop: static bench => bench.Shell.CitationDetailsCloseButton),
        ModalSurface.CitationSummary => new Sheet(
            surface,
            nameof(WorkspaceViewModel.CitationSummary),
            "CitationSummaryOverlay",
            Note,
            Prepare: static bench => bench.LoadTheCitations(),
            Approach: null,
            Open: static bench => bench.Workspace.OpenCitationSummaryCommand.Execute(null),
            Stop: static bench => bench.Workspace.CitationSummary is { CanWalkThrough: true }
                ? bench.Shell.CitationSummaryWalkButton
                : bench.Shell.CitationSummaryDismissButton),
        ModalSurface.FilesCiting => new Sheet(
            surface,
            nameof(WorkspaceViewModel.FilesCiting),
            "FilesCitingOverlay",
            Note,
            Prepare: static bench => bench.LoadTheBibliography(),
            Approach: null,
            // The Bibliography leaf's Files Citing action. The sheet is shown once its
            // lookup has landed, in a later turn than this call.
            Open: static bench => bench.Workspace.OpenFilesCiting("knuth1984"),
            Stop: static bench => bench.Shell.FilesCitingCloseButton),
        ModalSurface.DashboardEditor => new Sheet(
            surface,
            nameof(WorkspaceViewModel.DashboardEditorSheet),
            "DashboardEditorOverlay",
            Note,
            Prepare: static _ => { },
            Approach: null,
            // The Queries leaf's New Dashboard button.
            Open: static bench => bench.Workspace.OpenDashboardEditor(null),
            Stop: static bench => bench.Shell.DashboardEditorNameBox),
        ModalSurface.BaseQueryBuilder => new Sheet(
            surface,
            nameof(WorkspaceViewModel.BaseQueryBuilderSheet),
            "BaseQueryBuilderOverlay",
            Note,
            Prepare: static _ => { },
            Approach: null,
            Open: static bench => bench.Workspace.BasesNewQueryCommand.Execute(null),
            Stop: static bench => bench.Shell.BuilderCombinatorBox),
        ModalSurface.TemplatePicker => new Sheet(
            surface,
            nameof(WorkspaceViewModel.TemplatePickerSheet),
            "TemplatePickerOverlay",
            Note,
            Prepare: static _ => { },
            Approach: null,
            Open: static bench => bench.Workspace.NewFromTemplateCommand.Execute(null),
            // It opens loading, with no row yet; its list is filled later in the same call.
            Stop: null),
        ModalSurface.TemplateFlow => new Sheet(
            surface,
            nameof(WorkspaceViewModel.TemplateFlowSheet),
            "TemplateFlowOverlay",
            Note,
            Prepare: static _ => { },
            // The only route to it: the picker, then Enter on a template's row.
            Approach: static bench =>
            {
                bench.Workspace.NewFromTemplateCommand.Execute(null);
                bench.Host.Settle();
                bench.Host.Settle();
            },
            Open: static bench =>
            {
                TemplatePickerViewModel picker = Assert.IsType<TemplatePickerViewModel>(bench.Workspace.TemplatePickerSheet);
                picker.ActivateCommand.Execute(Assert.Single(picker.Rows));
            },
            // The one template has no prompt, so the flow opens at its name.
            Stop: static bench => bench.Shell.TemplateFlowNameTextBox),
        ModalSurface.MoveTo => new Sheet(
            surface,
            nameof(FilesSidebarViewModel.MoveToSheet),
            "MoveToOverlay",
            Note,
            Prepare: static bench => bench.AttachASidebarAndSelect(Note),
            Approach: null,
            Open: static bench => bench.Sidebar!.MoveToCommand.Execute(null),
            Stop: static bench => bench.Shell.MoveToFilterTextBox),
        ModalSurface.CanvasCardEditor => new Sheet(
            surface,
            nameof(WorkspaceViewModel.CanvasCardEditorSheet),
            "CanvasCardEditorOverlay",
            Board,
            Prepare: static bench => bench.SelectTheCard("alpha"),
            Approach: null,
            Open: static bench => bench.Workspace.OpenCanvasCardEditor(),
            Stop: static bench => bench.Shell.CanvasCardEditorTextBox,
            NotOverAnEmptyPane: CanvasNeedsItsTab),
        ModalSurface.CanvasCardPicker => new Sheet(
            surface,
            nameof(WorkspaceViewModel.CanvasCardPickerSheet),
            "CanvasCardPickerOverlay",
            Board,
            Prepare: static bench => bench.SelectTheCard("alpha"),
            Approach: null,
            // The canvas menu's Connect To.
            Open: static bench => bench.Canvas!.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo),
            Stop: static bench => bench.Shell.CanvasCardPickerFilterBox,
            NotOverAnEmptyPane: CanvasNeedsItsTab),
        ModalSurface.CanvasPrompt => new Sheet(
            surface,
            nameof(WorkspaceViewModel.CanvasPromptSheet),
            "CanvasPromptOverlay",
            Board,
            Prepare: static bench => bench.SelectTheCard("alpha"),
            Approach: null,
            // The canvas menu's Set Color: a prompt whose first stop is a row of its choices.
            Open: static bench => bench.Canvas!.RequestSetColor(),
            Stop: static bench => RowOf(bench.Shell.CanvasPromptChoicesList),
            NotOverAnEmptyPane: CanvasNeedsItsTab),
        ModalSurface.Settings => new Sheet(
            surface,
            nameof(VaultLifecycleViewModel.SettingsSheet),
            "SettingsOverlay",
            Note,
            Prepare: static _ => { },
            Approach: null,
            Open: static bench => bench.Host.Lifecycle.OpenSettingsCommand.Execute(null),
            Stop: static bench => RowOf(bench.Shell.SettingsCategoriesList)),
    };
#pragma warning restore CS8524

    /// <summary>The real window over a vault that every sheet's route can run in: a note
    /// with a property and a citation, a bibliography with the cited entry, one template
    /// with no prompt, and a canvas with two text cards.</summary>
    private sealed class Bench : IDisposable
    {
        public ShownShell Host { get; } = new(
            (Note, "---\nstatus: draft\n---\n# A\n\nA citation [@knuth1984].\n"),
            ("library.bib", "@article{knuth1984,\n title={Literate Programming},\n author={Knuth, Donald E.},\n year={1984}\n}\n"),
            ("slate.json", "{\"citations\":{\"bibliography\":\"library.bib\"}}"),
            ("Templates/Meeting.md", "# Meeting\n"),
            (Board, "{\"nodes\":["
                + "{\"id\":\"alpha\",\"type\":\"text\",\"text\":\"Alpha\",\"x\":0,\"y\":0,\"width\":240,\"height\":120},"
                + "{\"id\":\"beta\",\"type\":\"text\",\"text\":\"Beta\",\"x\":320,\"y\":0,\"width\":240,\"height\":120}"
                + "],\"edges\":[]}"));

        public MainWindow Shell => Host.Shell;

        public WorkspaceViewModel Workspace => Host.Workspace;

        /// <summary>The Files sidebar, once a fact has attached one.</summary>
        public FilesSidebarViewModel? Sidebar { get; private set; }

        /// <summary>The canvas of the open board, once a fact has opened it.</summary>
        public CanvasDocumentViewModel? Canvas { get; private set; }

        public FrameworkElement Overlay(Sheet sheet) => Assert.IsAssignableFrom<FrameworkElement>(Shell.FindName(sheet.Overlay));

        public void OpenTheTab(string path)
        {
            Workspace.OpenPath(path);
            if (path == Board)
            {
                CanvasDocumentViewModel canvas = Assert.IsType<CanvasDocumentViewModel>(
                    Assert.IsType<WorkspaceTabViewModel>(Workspace.ActiveGroup.ActiveTab).Canvas);
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => canvas.State == CanvasLoadState.Ready, TimeSpan.FromSeconds(15)),
                    $"the canvas never loaded ({canvas.State})");
                Assert.False(canvas.IsReadOnly, "premise: the canvas is editable");
                Canvas = canvas;
            }

            Host.Settle();
            Host.Settle();
        }

        public void SelectTheCard(string id)
        {
            Assert.NotNull(Canvas);
            Canvas.SeatSelectionSilently(id);
            Host.Settle();
        }

        public void LoadTheBibliography()
        {
            PumpedDispatcher.PumpUntilDrained(Workspace.SeedWorkForTests);
            Workspace.Bibliography.EnsureLoaded();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Workspace.Bibliography.Entries.Count > 0, TimeSpan.FromSeconds(15)),
                "the bibliography never published its entry");
            Host.Settle();
        }

        public void LoadTheCitations()
        {
            PumpedDispatcher.PumpUntilDrained(Workspace.SeedWorkForTests);
            PumpedDispatcher.PumpUntilDrained(Workspace.Citations.WhenAllWorkDrained());
            Host.Settle();
        }

        /// <summary>The Files sidebar attached through the lifecycle's own setter, so the
        /// window wires it (SheetOpenListKeyTests' shape), with <paramref name="path"/>
        /// selected in its tree.</summary>
        public void AttachASidebarAndSelect(string path)
        {
            var sidebar = new FilesSidebarViewModel(
                Host.Session,
                _ => { },
                vaultRoot: Host.Root,
                localAppDataRoot: Path.Combine(Host.Root, "device-state"));
            PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
            SetTheSidebar(sidebar);
            Sidebar = sidebar;
            Host.Settle();
            sidebar.SelectedNode = sidebar.RootNodes.Single(node => node.Path == path);
            Host.Settle();
        }

        /// <summary>The sheet opened by its whole route, then the dispatcher drained, so its
        /// queued landing has run.</summary>
        public void OpenAndSettle(Sheet sheet)
        {
            sheet.Approach?.Invoke(this);
            sheet.Open(this);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Shell.OpenModalSurface == sheet.Surface, TimeSpan.FromSeconds(15)),
                $"{sheet.Token} never opened (the open surface is {Shell.OpenModalSurface?.ToString() ?? "none"})");
            Host.Settle();
            Host.Settle();
        }

        /// <summary>The opening call made, and what held the keys at the end of the turn the
        /// sheet opened in: when the call returns, for a sheet the call itself opens; when
        /// the dispatcher operation that opened it ends, for one shown in a later turn (Files
        /// Citing). No queued landing has run by then.</summary>
        public IInputElement? OpenAndReadTheKeysInThatTurn(Sheet sheet, ModalSurface? awaitedSurface = null)
        {
            IInputElement? inTheTurn = null;
            bool read = false;
            bool Opened() => awaitedSurface is null
                ? Shell.OpenModalSurface == sheet.Surface
                : Shell.OpenModalSurface == awaitedSurface && IsOpen(sheet);
            void TurnEnded(object? sender, DispatcherHookEventArgs e)
            {
                if (!read && Opened())
                {
                    read = true;
                    inTheTurn = Keyboard.FocusedElement;
                }
            }

            DispatcherHooks hooks = Dispatcher.CurrentDispatcher.Hooks;
            hooks.OperationCompleted += TurnEnded;
            try
            {
                sheet.Open(this);
                if (!read && Opened())
                {
                    read = true;
                    inTheTurn = Keyboard.FocusedElement;
                }

                Assert.True(
                    PumpedDispatcher.PumpUntil(() => read, TimeSpan.FromSeconds(15)),
                    $"{sheet.Token} never opened (the open surface is {Shell.OpenModalSurface?.ToString() ?? "none"})");
            }
            finally
            {
                hooks.OperationCompleted -= TurnEnded;
            }

            return inTheTurn;
        }

        /// <summary>Whether <paramref name="sheet"/>'s property is set, open or beneath
        /// another surface.</summary>
        public bool IsOpen(Sheet sheet) =>
            (typeof(WorkspaceViewModel).GetProperty(sheet.Token)?.GetValue(Workspace)
                ?? typeof(VaultLifecycleViewModel).GetProperty(sheet.Token)?.GetValue(Host.Lifecycle)
                ?? (Sidebar is null ? null : typeof(FilesSidebarViewModel).GetProperty(sheet.Token)?.GetValue(Sidebar))) is not null;

        /// <summary>The keys put where they are as <paramref name="sheet"/> opens, with its
        /// route run up to the call that opens it: on the empty pane's Quick Open… row for a
        /// sheet that can open over it, else in the editor of its tab (the canvas for the
        /// three canvas sheets) or, for the template flow, in the template picker its route
        /// opens. Returns what holds them.</summary>
        public UIElement TakeTheKeysUnder(Sheet sheet)
        {
            if (sheet.NotOverAnEmptyPane is null && sheet.Approach is null)
            {
                sheet.Prepare(this);
                Button row = LauncherRow(Host, "EmptyPaneQuickOpen");
                Assert.True(row.IsVisible, "premise: no tab is open, so the empty pane shows its launcher");
                Shell.Activate();
                Host.Settle();
                TakeTheKeys(row);
                return row;
            }

            OpenTheTab(sheet.Tab);
            sheet.Prepare(this);
            Shell.Activate();
            Host.Settle();
            Assert.True(Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
            Host.Settle();
            sheet.Approach?.Invoke(this);
            Assert.True(
                Keyboard.FocusedElement is UIElement { IsEnabled: true, IsVisible: true },
                $"keyboard premise: before {sheet.Token} opened the keys were on {Describe(Keyboard.FocusedElement)}");
            return (UIElement)Keyboard.FocusedElement;
        }

        public void AssertTheSheetHasTheKeys(Sheet sheet)
        {
            Assert.Equal(sheet.Surface, Shell.OpenModalSurface);
            Assert.True(
                KeysFirstRecorder.TheKeysAreIn(Overlay(sheet)),
                $"with {sheet.Token} open and settled the keys are on {Describe(Keyboard.FocusedElement)}, outside it");
        }

        public void Dispose()
        {
            try
            {
                if (Sidebar is { } sidebar)
                {
                    SetTheSidebar(null);
                    PumpedDispatcher.PumpUntilDrained(sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
                }
            }
            finally
            {
                Host.Dispose();
            }
        }

        private void SetTheSidebar(FilesSidebarViewModel? sidebar) =>
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.FileSidebar), BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("VaultLifecycleViewModel.FileSidebar is gone; the Move To facts attach their sidebar through its setter."))
            .SetValue(Host.Lifecycle, sidebar);
    }

    /// <summary>The row a list's landing takes: its selected row, or its first.</summary>
    private static UIElement? RowOf(ListBox list) =>
        list.Items.Count == 0
            ? null
            : list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem ?? list.Items[0]) as UIElement;

    private static Button LauncherRow(ShownShell host, string id) => Assert.Single(
        Assert.Single(ShownShell.Descendants(host.Shell).OfType<StackPanel>(), panel => panel.Name == "EmptyPaneLauncher")
            .Children.OfType<Button>(),
        button => AutomationProperties.GetAutomationId(button) == id);

    /// <summary>Every element inside <paramref name="overlay"/> refuses the keys until the
    /// result is disposed (its preview of the move is handled, which WPF reads as a refusal);
    /// the overlay itself may still take them.</summary>
    private static IDisposable RefuseTheKeysInside(FrameworkElement overlay)
    {
        void Refuse(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!ReferenceEquals(e.NewFocus, overlay))
            {
                e.Handled = true;
            }
        }

        overlay.PreviewGotKeyboardFocus += Refuse;
        return new Undo(() => overlay.PreviewGotKeyboardFocus -= Refuse);
    }

    private sealed class Undo(Action undo) : IDisposable
    {
        public void Dispose() => undo();
    }

    private static void TakeTheKeys(UIElement element) => Assert.True(
        element.Focus() && ReferenceEquals(element, Keyboard.FocusedElement),
        $"keyboard premise: {Describe(element)} could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

    /// <summary>A key pressed and let go where the keys are, through WPF's input manager,
    /// so it runs the window's own handlers as the keyboard's does.</summary>
    private static void Press(ShownShell host, Key key)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => Keyboard.Modifiers == ModifierKeys.None, TimeSpan.FromSeconds(5)),
            $"a real modifier key is held on this desktop ({Keyboard.Modifiers}); these facts press unmodified keys");
        PresentationSource source = PresentationSource.FromVisual(host.Shell)
            ?? throw new InvalidOperationException("the window is not shown");
        InputManager.Current.ProcessInput(
            new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        InputManager.Current.ProcessInput(
            new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key) { RoutedEvent = Keyboard.PreviewKeyUpEvent });
    }

    /// <summary>Whether <paramref name="element"/> is <paramref name="root"/> or inside it,
    /// by the logical tree and, where an element has no logical parent, the visual one.</summary>
    private static bool IsIn(UIElement root, IInputElement? element)
    {
        for (DependencyObject? at = element as DependencyObject; at is not null;
            at = LogicalTreeHelper.GetParent(at) ?? (at is Visual ? VisualTreeHelper.GetParent(at) : null))
        {
            if (ReferenceEquals(at, root))
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        FrameworkElement { Name.Length: > 0 } named => $"{named.GetType().Name} {named.Name}{(named.IsEnabled ? string.Empty : " (off)")}",
        FrameworkElement framework when AutomationProperties.GetAutomationId(framework) is { Length: > 0 } id
            => $"{framework.GetType().Name} {id}{(framework.IsEnabled ? string.Empty : " (off)")}",
        FrameworkElement framework when AutomationProperties.GetName(framework) is { Length: > 0 } name
            => $"{framework.GetType().Name} '{name}'{(framework.IsEnabled ? string.Empty : " (off)")}",
        FrameworkElement framework => $"{framework.GetType().Name}{(framework.IsEnabled ? string.Empty : " (off)")}",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "A sheet-opening fact timed out.");
}
