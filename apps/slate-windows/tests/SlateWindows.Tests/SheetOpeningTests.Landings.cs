// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Templates;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b PR 2 (#1866): the red team's finds on the sheets' same-turn landing. A
/// sheet takes the keys from wherever they are in the window, not only from the workspace,
/// the welcome view and the menu; a sheet that opens beneath another waits until it is the
/// topmost; a sheet that opens or holds the keys while the window is away has them when the
/// window comes back; a sheet holding the keys itself is never forwarded to the element its
/// scope remembers from an earlier opening; and the fallback landing never selects again what
/// the reader typed after the first.
/// </summary>
public sealed partial class SheetOpeningTests
{
    /// <summary>The keys in the palette's field, which the template route dismisses before
    /// the picker opens (Quick Open and search take the same route); on the status bar; on
    /// the menu bar. With every stop in the sheet refusing them in the turn it opens, the
    /// sheet itself holds them by the end of that turn, so nothing they were on, or that WPF
    /// would hand them up to, takes a key meanwhile; then its landing gives a stop the
    /// keys.</summary>
    [Theory]
    [InlineData("PaletteField")]
    [InlineData("StatusBar")]
    [InlineData("MenuBar")]
    [InlineData("OpenMenuItem")]
    public void ASheetTakesTheKeysFromWhereverTheyAreInTheWindow(string holder) => RunSta(() =>
    {
        Sheet sheet = SheetOf(holder is "PaletteField"
            ? nameof(WorkspaceViewModel.TemplatePickerSheet)
            : nameof(VaultLifecycleViewModel.SettingsSheet));
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.Shell.Activate();
        bench.Host.Settle();
        switch (holder)
        {
            case "PaletteField":
                bench.Host.Lifecycle.Palette.Open();
                bench.Host.Settle();
                Assert.Same(bench.Shell.CommandPaletteSearchTextBox, Keyboard.FocusedElement);
                break;
            case "StatusBar":
                Assert.True(bench.Host.Land(ShellRegionKind.StatusBar), $"keyboard premise: the status bar refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
                bench.Host.Settle();
                break;
            case "OpenMenuItem":
                // The red team on PR 2's diff, F10: an item of the open Slate menu, in its popup.
                MenuItem top = bench.Shell.MainMenu.Items.OfType<MenuItem>().First();
                top.IsSubmenuOpen = true;
                bench.Host.Settle();
                MenuItem item = top.Items.OfType<MenuItem>().FirstOrDefault(candidate => candidate.IsEnabled && candidate.IsVisible)
                    ?? KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu("no item of the open menu is on screen");
                KeysFirstRecorder.TakeTheMenuKeys(item);
                bench.Host.Settle();
                break;
            default:
                KeysFirstRecorder.TakeTheMenuKeys(bench.Shell.MainMenu.Items.OfType<MenuItem>().First());
                bench.Host.Settle();
                break;
        }

        IInputElement before = Keyboard.FocusedElement;
        FrameworkElement overlay = bench.Overlay(sheet);
        IInputElement? inTheTurn = bench.OpenAndReadTheKeysInThatTurn(sheet, endAtTheRead: RefuseTheKeysInside(overlay));

        Assert.True(
            ReferenceEquals(overlay, inTheTurn),
            $"in the turn {sheet.Token} opened from {holder}, with its stops refusing the keys, they were on {Describe(inTheTurn)}, not on the sheet itself; before it opened they were on {Describe(before)}");
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(sheet.Surface, bench.Shell.OpenModalSurface);
        Assert.True(
            Keyboard.FocusedElement is UIElement landed && !ReferenceEquals(landed, overlay) && KeysFirstRecorder.TheKeysAreIn(overlay),
            $"once its stops could take the keys, {sheet.Token}'s landing left them on {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>Files Citing presents when its lookup lands, with no admission, so it can
    /// arrive while Settings is open above it: it leaves the keys in Settings and takes them
    /// on its Close only once Settings has closed.</summary>
    [Fact]
    public void ASheetPresentedBeneathAnotherWaitsUntilItIsTopmost() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.LoadTheBibliography();
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();

        using var held = new HeldWork(hook => bench.Workspace.FilesCitingInterleaveForTests = hook);
        bench.Workspace.OpenFilesCiting("knuth1984");
        held.WaitUntilEntered();
        bench.Host.Lifecycle.OpenSettingsCommand.Execute(null);
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.Settings, bench.Shell.OpenModalSurface);
        IInputElement inSettings = Keyboard.FocusedElement;
        Assert.True(KeysFirstRecorder.TheKeysAreIn(bench.Shell.SettingsOverlay), $"premise: Settings holds the keys (they are on {Describe(inSettings)})");

        held.Release();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => bench.Workspace.FilesCiting is not null, TimeSpan.FromSeconds(15)),
            "Files Citing never presented");
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.Settings, bench.Shell.OpenModalSurface);
        Assert.True(
            ReferenceEquals(inSettings, Keyboard.FocusedElement),
            $"Files Citing, presented beneath Settings, moved the keys from {Describe(inSettings)} to {Describe(Keyboard.FocusedElement)}");

        bench.Host.Lifecycle.CloseSettings();
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.FilesCiting, bench.Shell.OpenModalSurface);
        Assert.True(
            ReferenceEquals(bench.Shell.FilesCitingCloseButton, Keyboard.FocusedElement),
            $"once Settings closed over Files Citing the keys were on {Describe(Keyboard.FocusedElement)}, not on its Close");
    });

    /// <summary>Files Citing presents while the window is away (the reader switched to
    /// another window while the lookup ran): when the window comes back the keys are in the
    /// sheet, not on what held them under it before.</summary>
    [Fact]
    public void ASheetPresentedWhileTheWindowIsAwayHasTheKeysWhenItComesBack() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.LoadTheBibliography();
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();

        using var held = new HeldWork(hook => bench.Workspace.FilesCitingInterleaveForTests = hook);
        bench.Workspace.OpenFilesCiting("knuth1984");
        held.WaitUntilEntered();
        WindowActivation.GoAway(bench.Shell);
        held.Release();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => bench.Workspace.FilesCiting is not null, TimeSpan.FromSeconds(15)),
            "Files Citing never presented");
        bench.Host.Settle();
        WindowActivation.ComeBack(bench.Shell);
        bench.Host.Settle();
        bench.Host.Settle();

        Assert.Equal(ModalSurface.FilesCiting, bench.Shell.OpenModalSurface);
        Assert.True(
            KeysFirstRecorder.TheKeysAreIn(bench.Shell.FilesCitingOverlay),
            $"Files Citing presented while the window was away; on its return the keys are on {Describe(Keyboard.FocusedElement)}, outside it");
    });

    /// <summary>Settings holding the keys itself, its stops refusing them, when the window
    /// goes away: when it comes back, and its stops can take the keys again, they are in
    /// Settings.</summary>
    [Fact]
    public void ASheetHoldingTheKeysItselfHasThemWhenTheWindowComesBack() => RunSta(() =>
    {
        Sheet sheet = SheetOf(nameof(VaultLifecycleViewModel.SettingsSheet));
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();
        FrameworkElement overlay = bench.Overlay(sheet);
        using (RefuseTheKeysInside(overlay))
        {
            IInputElement? inTheTurn = bench.OpenAndReadTheKeysInThatTurn(sheet);
            Assert.True(ReferenceEquals(overlay, inTheTurn), $"premise: in the turn Settings opened the keys were on {Describe(inTheTurn)}, not on Settings itself");
            WindowActivation.GoAway(bench.Shell);
            bench.Host.Settle();
        }

        WindowActivation.ComeBack(bench.Shell);
        bench.Host.Settle();
        bench.Host.Settle();

        Assert.Equal(ModalSurface.Settings, bench.Shell.OpenModalSurface);
        Assert.True(
            Keyboard.FocusedElement is UIElement landed && !ReferenceEquals(landed, overlay) && KeysFirstRecorder.TheKeysAreIn(overlay),
            $"Settings held the keys itself as the window went away; on its return they are on {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>The template picker remembers Cancel when it is left from there. Opened
    /// again by Enter on its launcher row it holds the keys while it loads, and its scope
    /// would hand a request for the sheet on to Cancel: Cancel never holds the keys on the
    /// way to the list.</summary>
    [Fact]
    public void ThePickerLeftFromItsCancelDoesNotLandThereWhenItOpensAgain() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"), ("Templates/Meeting.md", "# Meeting\n"));
        host.Settle();
        Button row = LauncherRow(host, "EmptyPaneNewFromTemplate");
        host.Shell.Activate();
        host.Settle();
        TakeTheKeys(row);
        Press(host, Key.Enter);
        host.Settle();
        host.Settle();
        Button cancel = host.Shell.TemplatePickerCancelButton;
        TakeTheKeys(cancel);
        Press(host, Key.Escape);
        host.Settle();
        host.Settle();
        Assert.Null(host.Workspace.TemplatePickerSheet);
        Assert.Same(cancel, FocusManager.GetFocusedElement(host.Shell.TemplatePickerOverlay));
        TakeTheKeys(row);
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
            $"the picker opened again holds the keys on {Describe(Keyboard.FocusedElement)}, not on a row of its list");
    });

    /// <summary>The NVDA spot on PR 2's head (A1): Enter on the launcher's New Note from
    /// Template… row still said "unavailable". The picker opens loading, so the sheet held
    /// the keys for an instant before its list, filled in the same call, took them; NVDA
    /// ignores a focus event for an element that no longer has the keys when it reads it,
    /// kept the row as its focus, and spoke the row going off. The keys go from the row
    /// straight to the picker's row, with nothing in between.</summary>
    [Fact]
    public void ThePickerTakesTheKeysStraightFromItsLauncherRowToItsList() => RunSta(() =>
    {
        using var host = new ShownShell(("note.md", "# Note\n"), ("Templates/Meeting.md", "# Meeting\n"));
        host.Settle();
        Button row = LauncherRow(host, "EmptyPaneNewFromTemplate");
        host.Shell.Activate();
        host.Settle();
        TakeTheKeys(row);
        List<string> landings = [];
        void Landed(object sender, KeyboardFocusChangedEventArgs e) => landings.Add(Describe(e.NewFocus));
        var handler = new KeyboardFocusChangedEventHandler(Landed);
        host.Shell.AddHandler(Keyboard.GotKeyboardFocusEvent, handler, handledEventsToo: true);

        Press(host, Key.Enter);
        host.Settle();
        host.Settle();
        host.Shell.RemoveHandler(Keyboard.GotKeyboardFocusEvent, handler);

        TemplatePickerViewModel picker = Assert.IsType<TemplatePickerViewModel>(host.Workspace.TemplatePickerSheet);
        Assert.Equal(TemplatePickerState.Available, picker.State);
        Assert.True(
            Keyboard.FocusedElement is ListBoxItem item && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(item), host.Shell.TemplatePickerList),
            $"the picker holds the keys on {Describe(Keyboard.FocusedElement)}, not on a row of its list");
        Assert.True(
            landings.Count == 1,
            $"from the launcher row the keys went to {string.Join(", then ", landings)}, not straight to the picker's row");
    });

    /// <summary>The template flow's name box takes the keys with its seed selected in the
    /// turn the flow opens, so what the reader types first replaces the seed; the fallback
    /// landing then finds the keys on the box and selects nothing again, so the next letter
    /// is added, not typed over what came before (#1867's round 6, for a sheet).</summary>
    [Fact]
    public void TextTypedAfterTheFlowsLandingIsNotSelectedByASecondOne() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.Shell.Activate();
        bench.Host.Settle();
        bench.Workspace.NewFromTemplateCommand.Execute(null);
        bench.Host.Settle();
        bench.Host.Settle();
        TemplatePickerViewModel picker = Assert.IsType<TemplatePickerViewModel>(bench.Workspace.TemplatePickerSheet);
        picker.ActivateCommand.Execute(Assert.Single(picker.Rows));
        TextBox name = bench.Shell.TemplateFlowNameTextBox;

        Type(name, "x");
        bench.Host.Settle();
        bench.Host.Settle();
        Type(name, "y");

        Assert.Equal(ModalSurface.TemplateFlow, bench.Shell.OpenModalSurface);
        Assert.Equal("xy", name.Text);
    });

    /// <summary>The red team on PR 2's diff, F1: Files Citing presented beneath the dashboard
    /// editor waits; when the editor closes, Files Citing lands its Close, and the editor's
    /// own restore, whose token is the editor's text behind both, gives way to it: the keys
    /// rest in Files Citing, and Escape closes it.</summary>
    [Fact]
    public void ASheetClosedOverAWaitingSheetNeverRestoresBeneathIt() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.LoadTheBibliography();
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();

        using var held = new HeldWork(hook => bench.Workspace.FilesCitingInterleaveForTests = hook);
        bench.Workspace.OpenFilesCiting("knuth1984");
        held.WaitUntilEntered();
        bench.Workspace.OpenDashboardEditor(null);
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.DashboardEditor, bench.Shell.OpenModalSurface);
        held.Release();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => bench.Workspace.FilesCiting is not null, TimeSpan.FromSeconds(15)),
            "Files Citing never presented");
        bench.Host.Settle();
        Assert.Equal(ModalSurface.DashboardEditor, bench.Shell.OpenModalSurface);
        Assert.True(KeysFirstRecorder.TheKeysAreIn(bench.Shell.DashboardEditorOverlay), $"premise: the dashboard editor holds the keys (they are on {Describe(Keyboard.FocusedElement)})");

        List<string> outside = LandingsOutside(bench.Shell, bench.Shell.FilesCitingOverlay, out Action stop);
        bench.Workspace.CloseDashboardEditor();
        bench.Host.Settle();
        bench.Host.Settle();
        stop();

        Assert.Equal(ModalSurface.FilesCiting, bench.Shell.OpenModalSurface);
        Assert.True(outside.Count == 0, $"as the dashboard editor closed over Files Citing the keys went to {string.Join(", ", outside)}, behind it");
        Assert.True(
            ReferenceEquals(bench.Shell.FilesCitingCloseButton, Keyboard.FocusedElement),
            $"once the dashboard editor closed over Files Citing the keys rested on {Describe(Keyboard.FocusedElement)}, not on its Close");
        Press(bench.Host, Key.Escape);
        bench.Host.Settle();
        Assert.Null(bench.Workspace.FilesCiting);
    });

    /// <summary>The red team on PR 2's diff, F1: Files Citing presented over the citation
    /// details sheet takes the keys; closed, its restore, whose token is outside the details
    /// sheet, gives way to the details sheet, which is still open.</summary>
    [Fact]
    public void ACitationSheetClosedOverTheDetailsSheetLandsBackInIt() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.LoadTheBibliography();
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();

        using var held = new HeldWork(hook => bench.Workspace.FilesCitingInterleaveForTests = hook);
        bench.Workspace.OpenFilesCiting("knuth1984");
        held.WaitUntilEntered();
        bench.Workspace.OpenEntryDetails(bench.Workspace.Bibliography.Entries[0].Entry);
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.CitationDetails, bench.Shell.OpenModalSurface);
        held.Release();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => bench.Workspace.FilesCiting is not null, TimeSpan.FromSeconds(15)),
            "Files Citing never presented");
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.FilesCiting, bench.Shell.OpenModalSurface);
        Assert.True(KeysFirstRecorder.TheKeysAreIn(bench.Shell.FilesCitingOverlay), $"premise: Files Citing holds the keys (they are on {Describe(Keyboard.FocusedElement)})");

        List<string> outside = LandingsOutside(bench.Shell, bench.Shell.CitationDetailsOverlay, out Action stop);
        Press(bench.Host, Key.Escape);
        bench.Host.Settle();
        bench.Host.Settle();
        stop();

        Assert.Equal(ModalSurface.CitationDetails, bench.Shell.OpenModalSurface);
        Assert.True(outside.Count == 0, $"as Files Citing closed over the details sheet the keys went to {string.Join(", ", outside)}, behind it");
        Assert.True(
            KeysFirstRecorder.TheKeysAreIn(bench.Shell.CitationDetailsOverlay),
            $"once Files Citing closed over the details sheet the keys rested on {Describe(Keyboard.FocusedElement)}, outside it");
    });

    /// <summary>The red team on PR 2's diff, F1's mirror: the card editor has no restore of
    /// its own. Presented over Move To (a card's editor arriving while Move To is open) and
    /// closed, it leaves the keys to WPF, which hands them to the window's own element
    /// beneath Move To; the close's check lands Move To again.</summary>
    [Fact]
    public void ASheetWithNoRestoreClosedOverAnotherLandsTheOneLeftOnTop() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Board);
        bench.SelectTheCard("alpha");
        bench.AttachASidebarAndSelect(Note);
        bench.Shell.Activate();
        bench.Host.Settle();
        bench.Sidebar!.MoveToCommand.Execute(null);
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.MoveTo, bench.Shell.OpenModalSurface);
        Assert.True(KeysFirstRecorder.TheKeysAreIn(bench.Shell.MoveToOverlay), $"premise: Move To holds the keys (they are on {Describe(Keyboard.FocusedElement)})");

        bench.Workspace.OpenCanvasCardEditor();
        bench.Host.Settle();
        bench.Host.Settle();
        Assert.Equal(ModalSurface.CanvasCardEditor, bench.Shell.OpenModalSurface);
        Assert.True(KeysFirstRecorder.TheKeysAreIn(bench.Shell.CanvasCardEditorOverlay), $"premise: the card editor holds the keys (they are on {Describe(Keyboard.FocusedElement)})");

        bench.Workspace.CloseCanvasCardEditor();
        bench.Host.Settle();
        bench.Host.Settle();

        Assert.Equal(ModalSurface.MoveTo, bench.Shell.OpenModalSurface);
        Assert.True(
            KeysFirstRecorder.TheKeysAreIn(bench.Shell.MoveToOverlay),
            $"once the card editor closed over Move To the keys rested on {Describe(Keyboard.FocusedElement)}, outside it");
    });

    /// <summary>The red team on PR 2's diff, F2 and F4: when the window comes back with the
    /// keys outside the open sheet (WPF gives them to the window's own focused element, under
    /// the sheet), they go back to the stop the reader left in the sheet, not to its first
    /// stop.</summary>
    [Fact]
    public void TheWindowsReturnPutsTheReaderBackOnTheStopTheyLeftInTheSheet() => RunSta(() =>
    {
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();
        var editor = ShownShell.Descendants(bench.Shell.WorkspaceRoot).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        bench.Host.Lifecycle.OpenSettingsCommand.Execute(null);
        bench.Host.Settle();
        bench.Host.Settle();
        Button close = bench.Shell.SettingsCloseButton;
        TakeTheKeys(close);
        TakeTheKeys(editor);
        bench.Host.Settle();

        WindowActivation.GoAway(bench.Shell);
        WindowActivation.ComeBack(bench.Shell);
        bench.Host.Settle();
        bench.Host.Settle();

        Assert.Equal(ModalSurface.Settings, bench.Shell.OpenModalSurface);
        Assert.True(
            ReferenceEquals(close, Keyboard.FocusedElement),
            $"the window came back with Settings open; the keys are on {Describe(Keyboard.FocusedElement)}, not on the Close the reader left");
    });

    /// <summary>A sheet whose stops refused the keys in its opening turn holds them; a stop
    /// the reader moves them to before the fallback runs keeps them: the fallback runs only
    /// while no stop inside the sheet has the keys.</summary>
    [Fact]
    public void AFallbackLandingLeavesTheKeysOnAStopTheReaderMovedTo() => RunSta(() =>
    {
        Sheet sheet = SheetOf(nameof(VaultLifecycleViewModel.SettingsSheet));
        using var bench = new Bench();
        bench.OpenTheTab(Note);
        bench.Shell.Activate();
        bench.Host.Settle();
        Assert.True(bench.Host.Land(ShellRegionKind.Editor), $"keyboard premise: the editor refused the keys (they are on {Describe(Keyboard.FocusedElement)})");
        bench.Host.Settle();
        FrameworkElement overlay = bench.Overlay(sheet);

        IInputElement? inTheTurn = bench.OpenAndReadTheKeysInThatTurn(sheet, endAtTheRead: RefuseTheKeysInside(overlay));
        Assert.True(ReferenceEquals(overlay, inTheTurn), $"premise: in the turn Settings opened the keys were on {Describe(inTheTurn)}, not on Settings itself");
        Button close = bench.Shell.SettingsCloseButton;
        TakeTheKeys(close);
        bench.Host.Settle();
        bench.Host.Settle();

        Assert.True(
            ReferenceEquals(close, Keyboard.FocusedElement),
            $"the fallback landing moved the keys from the Close the reader chose to {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>The menu stays on while it holds the keys, by one trigger, last in its style,
    /// whose condition reads no surface (D10). Every surface lands in the turn it opens, so
    /// in the orders these facts can arrange the landing runs before the menu's triggers hear
    /// the surface open, and the menu goes off only once the keys have left it. WPF orders a
    /// binding's listener against the window's handler by when each subscribed, which no fact
    /// chooses, and #1690 measured the menu's trigger first; this pins the trigger that holds
    /// it on in that order: there, last, setting IsEnabled True.</summary>
    [Fact]
    public void TheMenusLastTriggerKeepsItOnWhileItHoldsTheKeys()
    {
        System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Load(
            System.IO.Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        System.Xml.Linq.XElement menu = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Menu");
        System.Xml.Linq.XElement triggers = menu.Descendants().First(element => element.Name.LocalName == "Style.Triggers");
        System.Xml.Linq.XElement last = triggers.Elements().Last();

        Assert.Equal("DataTrigger", last.Name.LocalName);
        Assert.Equal("{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource Self}}", (string?)last.Attribute("Binding"));
        Assert.Equal("True", (string?)last.Attribute("Value"));
        System.Xml.Linq.XElement setter = Assert.Single(last.Elements(), element => element.Name.LocalName == "Setter");
        Assert.Equal("IsEnabled", (string?)setter.Attribute("Property"));
        Assert.Equal("True", (string?)setter.Attribute("Value"));
    }

    /// <summary>Records each element outside <paramref name="surface"/> that takes the
    /// keys in <paramref name="window"/> until <paramref name="stop"/> is called.</summary>
    private static List<string> LandingsOutside(Window window, FrameworkElement surface, out Action stop)
    {
        List<string> outside = [];
        void Landed(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is DependencyObject element && !ReferenceEquals(element, window) && !IsIn(surface, e.NewFocus))
            {
                outside.Add(Describe(element));
            }
        }

        var handler = new KeyboardFocusChangedEventHandler(Landed);
        window.AddHandler(Keyboard.GotKeyboardFocusEvent, handler, handledEventsToo: true);
        stop = () => window.RemoveHandler(Keyboard.GotKeyboardFocusEvent, handler);
        return outside;
    }

    /// <summary>Text typed where the keys are, as the keyboard's text arrives.</summary>
    private static void Type(UIElement expected, string text)
    {
        Assert.True(
            ReferenceEquals(expected, Keyboard.FocusedElement),
            $"the keys are on {Describe(Keyboard.FocusedElement)}, not on {Describe(expected)}, as '{text}' is typed");
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, Keyboard.FocusedElement, text));
    }
}
