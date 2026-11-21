// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace SlateWindows.Tests;

/// <summary>#1690: an overlay disables the view under it, but only once the keys have left
/// that view. Disabled with the keys still inside, the focused control went unavailable, and
/// NVDA said "unavailable" before the overlay's field. Each overlay is named by the token of
/// its view model (QuickSwitcher, Search, Palette), which the focus manifest's per-trigger
/// rows of the view and the menu read (#1866, CD11).</summary>
public sealed partial class CommandPaletteTests
{
    [Theory]
    [InlineData("QuickSwitcher")]
    [InlineData("Search")]
    [InlineData("Palette")]
    public void TheWorkspaceIsDisabledOnlyAfterTheKeysLeaveIt(string overlay) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        _ = host.AttachDirtyWorkspace();
        host.Settle();
        FrameworkElement root = host.Shell.WorkspaceRoot;
        UIElement editor = ShownShell.Descendants(root).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        Assert.True(
            editor.Focus() && ReferenceEquals(editor, Keyboard.FocusedElement),
            $"keyboard premise: the editor could not take the keys (they are on {Keyboard.FocusedElement})");
        host.Settle();
        Assert.True(root.IsEnabled, "premise: the workspace is enabled with no overlay open");

        TextBox? field = null;
        IInputElement? atOnce = null;
        bool offAtOnce = true;
        KeysFirstRecorder.AssertTheKeysLeaveFirst("the workspace", root, () =>
        {
            field = OpenTheOverlay(host, overlay);
            atOnce = Keyboard.FocusedElement;
            offAtOnce = !root.IsEnabled;
            host.Settle();
        });

        // In the turn it opens, so no key, text or click already queued reaches the editor.
        Assert.True(ReferenceEquals(field, atOnce), $"in the turn the overlay opened, the keys were on {atOnce}");
        Assert.True(offAtOnce, "in the turn the overlay opened, the workspace was still on");
        host.AssertFocusIsOn(field!);
        Assert.False(root.IsEnabled, "the workspace stayed enabled under the open overlay");
    });

    /// <summary>Text the reader types once the field has the keys, before the dispatcher
    /// drains, stays as typed: no second, queued landing selects it for the next key to
    /// replace.</summary>
    [Theory]
    [InlineData("QuickSwitcher")]
    [InlineData("Search")]
    [InlineData("Palette")]
    public void TextTypedAfterTheLandingIsNotSelectedByASecondOne(string overlay) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        _ = host.AttachDirtyWorkspace();
        host.Settle();
        UIElement editor = ShownShell.Descendants(host.Shell.WorkspaceRoot).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        TakeTheKeys(editor);
        host.Settle();

        TextBox field = OpenTheOverlay(host, overlay);
        Assert.Same(field, Keyboard.FocusedElement);
        field.Text = "q";
        field.CaretIndex = 1;
        host.Settle();

        Assert.Equal("q", field.Text);
        Assert.Equal(0, field.SelectionLength);
        Assert.Equal(1, field.CaretIndex);
    });

    /// <summary>The palette's field takes the keys in the turn the palette opens, so what it
    /// is named and helped by then is what a reader hears first. Opened for Switch Theme,
    /// that's the choice's prompt and help, published after IsOpen; not the commands'
    /// "Search commands".</summary>
    [Fact]
    public void ThePaletteFieldSaysItsChoicesAsItTakesTheKeys() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        host.Shell.Activate();
        host.Settle();
        TextBox field = host.Shell.CommandPaletteSearchTextBox;
        var heard = new List<string>();
        KeyboardFocusChangedEventHandler took = (_, _) => heard.Add(
            $"{System.Windows.Automation.AutomationProperties.GetName(field)} | {System.Windows.Automation.AutomationProperties.GetHelpText(field)}");
        field.GotKeyboardFocus += took;
        try
        {
            host.Lifecycle.SwitchThemeCommand.Execute(null);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.Lifecycle.Palette.Rows.Count == 3, TimeSpan.FromSeconds(10)),
                "the themes never published");
            host.Settle();
        }
        finally
        {
            field.GotKeyboardFocus -= took;
        }

        Assert.NotEmpty(heard);
        Assert.Equal($"Select a theme | {host.Lifecycle.Palette.SearchFieldHelp}", heard[0]);
    });

    /// <summary>#1690 as the field pass heard it: the keys on a canvas outline row, then
    /// Ctrl+Shift+P. The row's canvas goes off only once the palette's field has the keys,
    /// in the turn the palette opens.</summary>
    [Fact]
    public void ACanvasOutlineRowIsDisabledOnlyAfterThePaletteTakesTheKeys() => RunSta(() =>
    {
        using var delete = new PaletteCanvasDelete();
        IInputElement row = Keyboard.FocusedElement;
        Assert.Equal("beta", RowIdOf(row));
        FrameworkElement root = delete.Host.Shell.WorkspaceRoot;

        IInputElement? atOnce = null;
        KeysFirstRecorder.AssertTheKeysLeaveFirst("the canvas", root, () =>
        {
            delete.Host.Palette.Open();
            atOnce = Keyboard.FocusedElement;
            delete.Host.Settle();
        });

        Assert.Same(delete.Host.Shell.CommandPaletteSearchTextBox, atOnce);
        delete.Host.AssertFocusIsOn(delete.Host.Shell.CommandPaletteSearchTextBox);
        Assert.False(((UIElement)row).IsEnabled, "Beta's row stayed on under the palette");
    });

    /// <summary>The empty pane's action that opens an overlay, activated as a reader's
    /// activate command does, through its peer, goes off only once the overlay's field has
    /// taken the keys. Quick Open's action runs the switcher's own command, which goes off as
    /// Quick Open opens, after its field has the keys.</summary>
    [Theory]
    [InlineData("EmptyPaneQuickOpen")]
    [InlineData("EmptyPaneSearchVault")]
    public void ALauncherActionThatOpensAnOverlayIsDisabledOnlyAfterTheKeysLeaveIt(string id) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        _ = host.AttachEmptyWorkspace();
        host.Settle();
        Button action = LauncherAction(host, id);
        TakeTheKeys(action);
        host.Settle();
        bool quickOpen = id == "EmptyPaneQuickOpen";

        KeysFirstRecorder.AssertTheKeysLeaveFirst(id, action, () =>
        {
            var invoke = (System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(action)
                .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);
            invoke.Invoke();
            host.Settle();
        });

        host.AssertTheOneOpenModalIs(quickOpen ? ModalSurface.QuickOpen : ModalSurface.SearchOverlay);
        host.AssertFocusIsOn(quickOpen ? host.Shell.QuickSwitcherSearchTextBox : host.Shell.SearchOverlaySearchTextBox);
        Assert.False(action.IsEnabled, $"{id} stayed on under the open overlay");
    });

    /// <summary>The menu is off under every modal surface. A chord pressed with the keys in
    /// the menu, on its bar or on an item of an open menu, opens an overlay over it: the menu
    /// stays on until the overlay's field takes the keys out of it.</summary>
    [Theory]
    [InlineData("QuickSwitcher", false)]
    [InlineData("Search", false)]
    [InlineData("Palette", false)]
    [InlineData("QuickSwitcher", true)]
    [InlineData("Search", true)]
    [InlineData("Palette", true)]
    public void TheMenuIsDisabledOnlyAfterTheKeysLeaveIt(string overlay, bool inAnOpenMenu) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        _ = host.AttachDirtyWorkspace();
        host.Settle();
        Menu menu = host.Shell.MainMenu;
        MenuItem top = menu.Items.OfType<MenuItem>().First();
        MenuItem item = top;
        if (inAnOpenMenu)
        {
            top.IsSubmenuOpen = true;
            host.Settle();
            item = top.Items.OfType<MenuItem>().FirstOrDefault(candidate => candidate.IsEnabled && candidate.IsVisible)
                ?? KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu("no item of the open menu is on screen");
        }

        KeysFirstRecorder.TakeTheMenuKeys(item);
        host.Settle();
        Assert.True(menu.IsEnabled, "premise: the menu is on with no overlay open");

        TextBox? field = null;
        // The menu itself is the root: an open menu's item lives in its popup and does not
        // go off with the menu, but the recorder counts the keys on it as inside the menu.
        KeysFirstRecorder.AssertTheKeysLeaveFirst("the menu", menu, () =>
        {
            field = OpenTheOverlay(host, overlay);
            host.Settle();
        });

        host.AssertFocusIsOn(field!);
        Assert.False(menu.IsEnabled, "the menu stayed on under the open overlay");
        Assert.False(top.IsSubmenuOpen, "the menu stayed open under the overlay");
    });

    /// <summary>The palette's field takes the keys in the turn the palette opens, from the
    /// editor and from the menu, so the view under it is off by the time any key, text or
    /// click already queued is delivered. This host is the real window.</summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("menu")]
    public void ThePaletteTakesTheKeysInTheTurnItOpens(string under) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        var editor = ShownShell.Descendants(host.Shell.WorkspaceRoot).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        MenuItem top = host.Shell.MainMenu.Items.OfType<MenuItem>().First();
        UIElement holder = under == "editor" ? editor : top;
        host.Shell.Activate();
        host.Settle();
        if (under == "menu")
        {
            KeysFirstRecorder.TakeTheMenuKeys(holder);
        }
        else
        {
            TakeTheKeys(holder);
        }

        host.Settle();

        host.Lifecycle.Palette.Open();

        TextBox field = host.Shell.CommandPaletteSearchTextBox;
        Assert.True(
            ReferenceEquals(field, Keyboard.FocusedElement),
            $"in the turn the palette opened, the keys were on {Keyboard.FocusedElement}; the field was "
            + $"visible {field.IsVisible}, enabled {field.IsEnabled}, focusable {field.Focusable}, "
            + $"overlay {host.Shell.CommandPaletteOverlay.Visibility}");
        Assert.False(holder.IsEnabled, $"the {under} was still on in the turn the palette opened");
    });

    /// <summary>When the field can't take the keys in that turn, they're taken out of the
    /// view under the palette rather than left in it until the queued landing runs, so no
    /// key, text or composition reaches the note, or the menu, under the palette meanwhile.
    /// The view goes off with nothing in it holding the keys, so NVDA hears no
    /// "unavailable". The field refuses the keys here until the fact lets it.</summary>
    [Theory]
    [InlineData("editor")]
    [InlineData("menu")]
    public void ALateLandingTakesTheKeysOutOfTheViewUnderThePalette(string under) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        var editor = ShownShell.Descendants(host.Shell.WorkspaceRoot).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        MenuItem top = host.Shell.MainMenu.Items.OfType<MenuItem>().First();
        UIElement holder = under == "editor" ? editor : top;
        host.Shell.Activate();
        host.Settle();
        if (under == "menu")
        {
            KeysFirstRecorder.TakeTheMenuKeys(holder);
        }
        else
        {
            TakeTheKeys(holder);
        }

        host.Settle();
        TextBox field = host.Shell.CommandPaletteSearchTextBox;

        field.Focusable = false;
        KeysFirstRecorder.AssertTheKeysLeaveFirst($"the {under}", holder, () => host.Lifecycle.Palette.Open());

        Assert.False(KeysFirstRecorder.TheKeysAreIn(holder), $"the keys stayed on the {under} under the palette: {Keyboard.FocusedElement}");
        Assert.False(holder.IsEnabled, $"the {under} stayed on under the palette");

        field.Focusable = true;
        host.Settle();
        Assert.True(host.Lifecycle.Palette.IsOpen);
        Assert.Same(field, Keyboard.FocusedElement);
        Assert.False(host.Shell.CommandPaletteOverlay.Focusable, "the palette stayed a stop once its field took the keys");
    });

    /// <summary>Escape still closes the palette while its landing waits, and if the
    /// fallback fails too: the keys are on the palette itself, so WPF delivers it, through
    /// the input pipeline, to the window's route. With no element holding them it delivers
    /// no key.</summary>
    [Fact]
    public void EscapeBeforeALateLandingStillClosesThePalette() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        var editor = ShownShell.Descendants(host.Shell.WorkspaceRoot).OfType<ICSharpCode.AvalonEdit.Editing.TextArea>().First(area => area.IsVisible);
        host.Shell.Activate();
        host.Settle();
        TakeTheKeys(editor);
        host.Settle();

        FrameworkElement overlay = host.Shell.CommandPaletteOverlay;
        Assert.False(overlay.Focusable, "premise: the palette is a stop before it opens");
        host.Shell.CommandPaletteSearchTextBox.Focusable = false;
        host.Lifecycle.Palette.Open();
        host.Settle();
        Assert.True(host.Lifecycle.Palette.IsOpen);
        Assert.True(
            ReferenceEquals(overlay, Keyboard.FocusedElement),
            $"while the landing waits, the keys are on {Keyboard.FocusedElement?.ToString() ?? "nothing"}, not the palette");
        Assert.False(KeyboardNavigation.GetIsTabStop(overlay), "the palette holding the keys is a Tab stop");
        InputManager.Current.ProcessInput(
            new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
        host.Settle();

        Assert.False(host.Lifecycle.Palette.IsOpen, "Escape before the landing left the palette open");
        Assert.False(overlay.Focusable, "the palette stayed a stop once it let the keys go");
    });

    private static Button LauncherAction(PaletteShellHost host, string id) => Assert.Single(
        ShownShell.Descendants(host.Shell.WorkspaceRoot).OfType<Button>(),
        button => System.Windows.Automation.AutomationProperties.GetAutomationId(button) == id && button.IsVisible);

    private static void TakeTheKeys(UIElement element) => Assert.True(
        element.Focus() && ReferenceEquals(element, Keyboard.FocusedElement),
        $"keyboard premise: {element} could not take the keys (they are on {Keyboard.FocusedElement})");

    /// <summary>The welcome view does the same under the palette, which opens over it for
    /// Switch Theme.</summary>
    [Fact]
    public void TheWelcomeViewIsDisabledOnlyAfterTheKeysLeaveIt() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        FrameworkElement root = host.Shell.WelcomeRoot;
        host.Shell.Activate();
        host.Settle();
        Assert.True(
            host.Shell.OpenVaultButton.Focus() && ReferenceEquals(host.Shell.OpenVaultButton, Keyboard.FocusedElement),
            $"keyboard premise: Open Vault could not take the keys (they are on {Keyboard.FocusedElement})");
        host.Settle();

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the welcome view", root, () =>
        {
            host.Lifecycle.SwitchThemeCommand.Execute(null);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.Lifecycle.Palette.Rows.Count == 3, TimeSpan.FromSeconds(10)),
                "the themes never published");
            host.Settle();
        });

        Assert.Same(host.Shell.CommandPaletteSearchTextBox, Keyboard.FocusedElement);
        Assert.False(root.IsEnabled, "the welcome view stayed enabled under the open palette");
    });

    /// <summary>#1866 (CD11): the welcome view's Quick Open and Search triggers never fire.
    /// The welcome view shows only without a vault, and without one Quick Open has no
    /// switcher to open and Search refuses to open, so neither can turn the welcome view off
    /// under the keys. The focus manifest's never-fires exemption of those two rows cites
    /// this fact.</summary>
    [Fact]
    public void QuickOpenAndSearchStayShutOverTheWelcomeView() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        FrameworkElement root = host.Shell.WelcomeRoot;
        host.Shell.Activate();
        host.Settle();
        Assert.True(root.IsVisible, "premise: the welcome view shows");
        Assert.False(host.Lifecycle.IsVaultOpen, "premise: no vault is open");
        Assert.True(
            host.Shell.OpenVaultButton.Focus() && ReferenceEquals(host.Shell.OpenVaultButton, Keyboard.FocusedElement),
            $"keyboard premise: Open Vault could not take the keys (they are on {Keyboard.FocusedElement})");
        host.Settle();

        // Quick Open's chord is bound to the switcher's own command, and there is no
        // switcher without a vault.
        KeyBinding quickOpen = Assert.Single(
            host.Shell.InputBindings.OfType<KeyBinding>(),
            binding => binding.Key == Key.O && binding.Modifiers == ModifierKeys.Control);
        Assert.Null(host.Lifecycle.QuickSwitcher);
        Assert.Null(quickOpen.Command);
        host.Lifecycle.ToggleSearchCommand.Execute(null);
        host.Lifecycle.Search.Open();
        host.Settle();

        Assert.Null(host.Lifecycle.QuickSwitcher);
        Assert.False(host.Lifecycle.Search.IsOpen, "Search opened over the welcome view without a vault");
        Assert.False(host.Shell.QuickSwitcherOverlay.IsVisible, "Quick Open showed over the welcome view");
        Assert.False(host.Shell.SearchOverlay.IsVisible, "Search showed over the welcome view");
        Assert.True(root.IsEnabled, "the welcome view went off with no overlay open");
        Assert.Same(host.Shell.OpenVaultButton, Keyboard.FocusedElement);
    });

    private static TextBox OpenTheOverlay(PaletteShellHost host, string overlay)
    {
        switch (overlay)
        {
            case "QuickSwitcher":
                host.Lifecycle.QuickSwitcher!.Open();
                return host.Shell.QuickSwitcherSearchTextBox;
            case "Search":
                host.Lifecycle.Search.Open();
                return host.Shell.SearchOverlaySearchTextBox;
            default:
                host.Palette.Open();
                return host.Shell.CommandPaletteSearchTextBox;
        }
    }
}
