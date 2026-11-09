// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace SlateWindows.Tests;

/// <summary>#1690: an overlay disables the view under it, but only once the keys have left
/// that view. Disabled with the keys still inside, the focused control went unavailable, and
/// NVDA said "unavailable" before the overlay's field.</summary>
public sealed partial class CommandPaletteTests
{
    [Theory]
    [InlineData("QuickOpen")]
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
        List<string> disabledUnder = DisabledWithTheKeysInside(root, () =>
        {
            field = OpenTheOverlay(host, overlay);
            atOnce = Keyboard.FocusedElement;
            offAtOnce = !root.IsEnabled;
            host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"the workspace was disabled with the keys on {string.Join(", ", disabledUnder)}");
        // In the turn it opens, so no key, text or click already queued reaches the editor.
        Assert.True(ReferenceEquals(field, atOnce), $"in the turn the overlay opened, the keys were on {atOnce}");
        Assert.True(offAtOnce, "in the turn the overlay opened, the workspace was still on");
        host.AssertFocusIsOn(field!);
        Assert.False(root.IsEnabled, "the workspace stayed enabled under the open overlay");
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
        List<string> disabledUnder = DisabledWithTheKeysInside(root, () =>
        {
            delete.Host.Palette.Open();
            atOnce = Keyboard.FocusedElement;
            delete.Host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"the canvas went off with the keys on {string.Join(", ", disabledUnder)}");
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

        List<string> disabledUnder = DisabledWithTheKeysInside(action, () =>
        {
            var invoke = (System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(action)
                .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);
            invoke.Invoke();
            host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"{id} went off with the keys on {string.Join(", ", disabledUnder)}");
        host.AssertTheOneOpenModalIs(quickOpen ? ModalSurface.QuickOpen : ModalSurface.SearchOverlay);
        host.AssertFocusIsOn(quickOpen ? host.Shell.QuickSwitcherSearchTextBox : host.Shell.SearchOverlaySearchTextBox);
        Assert.False(action.IsEnabled, $"{id} stayed on under the open overlay");
    });

    /// <summary>The menu is off under every modal surface. A chord pressed with the keys in
    /// the menu, on its bar or on an item of an open menu, opens an overlay over it: the menu
    /// stays on until the overlay's field takes the keys out of it.</summary>
    [Theory]
    [InlineData("QuickOpen", false)]
    [InlineData("Search", false)]
    [InlineData("Palette", false)]
    [InlineData("QuickOpen", true)]
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
            item = top.Items.OfType<MenuItem>().First(candidate => candidate.IsEnabled && candidate.IsVisible);
        }

        TakeTheKeys(item);
        host.Settle();
        Assert.True(menu.IsEnabled, "premise: the menu is on with no overlay open");

        TextBox? field = null;
        List<string> disabledUnder = DisabledWithTheKeysInside(item, () =>
        {
            field = OpenTheOverlay(host, overlay);
            host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"the menu went off with the keys on {string.Join(", ", disabledUnder)}");
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
        TakeTheKeys(holder);
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
        TakeTheKeys(holder);
        host.Settle();
        TextBox field = host.Shell.CommandPaletteSearchTextBox;

        field.Focusable = false;
        List<string> offUnder = DisabledWithTheKeysInside(holder, () => host.Lifecycle.Palette.Open());

        Assert.True(offUnder.Count == 0, $"the {under} went off with the keys on {string.Join(", ", offUnder)}");
        Assert.False(TheKeysAreIn(holder), $"the keys stayed on the {under} under the palette: {Keyboard.FocusedElement}");
        Assert.False(holder.IsEnabled, $"the {under} stayed on under the palette");

        field.Focusable = true;
        host.Settle();
        Assert.True(host.Lifecycle.Palette.IsOpen);
        Assert.Same(field, Keyboard.FocusedElement);
    });

    /// <summary>Escape still closes the palette while its landing waits: with no element
    /// holding the keys, it reaches the window's route.</summary>
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

        host.Shell.CommandPaletteSearchTextBox.Focusable = false;
        host.Lifecycle.Palette.Open();
        Assert.Null(Keyboard.FocusedElement);
        _ = Press(host.Shell, Key.Escape);
        host.Settle();

        Assert.False(host.Lifecycle.Palette.IsOpen, "Escape before the landing left the palette open");
    });

    /// <summary>Whether the keys are on <paramref name="element"/> or inside it, by where
    /// they are rather than by its IsKeyboardFocusWithin: WPF moves the keys first and
    /// updates that flag along each chain after, so an ancestor's trigger can run while an
    /// element the keys just left still reads true.</summary>
    internal static bool TheKeysAreIn(UIElement element) =>
        Keyboard.FocusedElement is DependencyObject keys
        && (ReferenceEquals(keys, element) || (keys is System.Windows.Media.Visual && element.IsAncestorOf(keys)));

    /// <summary>A key as WPF delivers it to the element holding the keys: the preview through
    /// the window's route, then, if nothing took it, the key itself. Whether the preview was
    /// taken.</summary>
    internal static bool Press(UIElement holder, Key key)
    {
        PresentationSource source = PresentationSource.FromVisual(holder)
            ?? throw new InvalidOperationException("the element holding the keys has no presentation source");
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        holder.RaiseEvent(preview);
        if (!preview.Handled)
        {
            holder.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.KeyDownEvent,
            });
        }

        return preview.Handled;
    }

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

        List<string> disabledUnder = DisabledWithTheKeysInside(root, () =>
        {
            host.Lifecycle.SwitchThemeCommand.Execute(null);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.Lifecycle.Palette.Rows.Count == 3, TimeSpan.FromSeconds(10)),
                "the themes never published");
            host.Settle();
        });

        Assert.True(disabledUnder.Count == 0, $"the welcome view was disabled with the keys on {string.Join(", ", disabledUnder)}");
        Assert.Same(host.Shell.CommandPaletteSearchTextBox, Keyboard.FocusedElement);
        Assert.False(root.IsEnabled, "the welcome view stayed enabled under the open palette");
    });

    /// <summary>Runs <paramref name="open"/> and returns the element that held the keys each
    /// time <paramref name="root"/> was disabled with the keys still inside it.</summary>
    private static List<string> DisabledWithTheKeysInside(UIElement root, Action open)
    {
        var disabledUnder = new List<string>();
        DependencyPropertyChangedEventHandler record = (_, e) =>
        {
            if (e.NewValue is false && TheKeysAreIn(root))
            {
                disabledUnder.Add(Keyboard.FocusedElement?.ToString() ?? "nothing");
            }
        };
        root.IsEnabledChanged += record;
        try
        {
            open();
        }
        finally
        {
            root.IsEnabledChanged -= record;
        }

        return disabledUnder;
    }

    private static TextBox OpenTheOverlay(PaletteShellHost host, string overlay)
    {
        switch (overlay)
        {
            case "QuickOpen":
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
