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

    /// <summary>Text typed where the keys are, as the keyboard's text arrives.</summary>
    private static void Type(UIElement expected, string text)
    {
        Assert.True(
            ReferenceEquals(expected, Keyboard.FocusedElement),
            $"the keys are on {Describe(Keyboard.FocusedElement)}, not on {Describe(expected)}, as '{text}' is typed");
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, Keyboard.FocusedElement, text));
    }
}
