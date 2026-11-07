// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// DS-10 (#1811): Settings is a sheet with the design's layout, it works with no vault, and
/// its one setting so far sets the theme at once.
/// </summary>
public sealed class SettingsSheetTests
{
    [Theory]
    [InlineData(ThemeChoice.Light, false, false)]
    [InlineData(ThemeChoice.Light, true, false)]
    [InlineData(ThemeChoice.Dark, false, true)]
    [InlineData(ThemeChoice.Dark, true, true)]
    [InlineData(ThemeChoice.UseWindowsSetting, false, false)]
    [InlineData(ThemeChoice.UseWindowsSetting, true, true)]
    [InlineData("sepia", true, true)]
    public void TheChoiceResolves(string preference, bool systemIsDark, bool isDark) =>
        Assert.Equal(isDark ? SlateTheme.Dark : SlateTheme.Light, ThemeChoice.Resolve(preference, systemIsDark));

    [Fact]
    public void TheChoicesAreTheDesignsThreeInItsOrder() =>
        Assert.Equal(
            new[] { "Light", "Dark", "Use Windows setting" },
            ThemeChoice.Options.Select(option => option.Label));

    /// <summary>A second launch can open a vault with the sheet up. The welcome view takes
    /// the keys on the way, beneath a sheet that would then hold no focus and take no Esc.</summary>
    [Fact]
    public async Task AVaultOpeningOrClosingClosesTheSheet()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "settings-vault");
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(null),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(Path.Combine(fixture.Root, "device-state", "recent-vaults.json")));
        lifecycle.ThemePreference = new ThemePreference(
            new AppPreferencesStore(Path.Combine(fixture.Root, "device-state", "preferences.json")),
            _ => { });

        lifecycle.OpenSettings(null);
        Assert.NotNull(lifecycle.SettingsSheet);
        await lifecycle.OpenVaultAsync(fixture.Root);
        Assert.True(lifecycle.IsVaultOpen);
        Assert.Null(lifecycle.SettingsSheet);

        lifecycle.OpenSettings(null);
        Assert.NotNull(lifecycle.SettingsSheet);
        lifecycle.CloseVault();
        Assert.False(lifecycle.IsVaultOpen);
        Assert.Null(lifecycle.SettingsSheet);
    }

    /// <summary>NVDA is mostly silent on a closed combo box's change, so the sheet says it,
    /// once, and only when something changed.</summary>
    [Fact]
    public void AThemeChangeSaysItself()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"slate-ds10-{Guid.NewGuid():N}");
        var spoken = new List<A11yEvent>();
        var preference = new ThemePreference(new AppPreferencesStore(Path.Combine(directory, "preferences.json")), _ => { });
        var accent = new AccentPreference(new AppPreferencesStore(Path.Combine(directory, "preferences.json")), _ => { });
        var sheet = new SettingsSheetViewModel(preference, accent, spoken.Add);
        try
        {
            sheet.SelectedTheme = ThemeChoice.Options[2];
            Assert.Empty(spoken);

            sheet.SelectedTheme = ThemeChoice.Options[1];

            Assert.Equal("Dark", Assert.IsType<A11yEvent.ThemeChosen>(Assert.Single(spoken)).Label);
            Assert.Equal(ThemeChoice.Dark, preference.Current);

            spoken.Clear();
            sheet.SelectedAccent = SlateAccent.Options[0];
            Assert.Empty(spoken);

            sheet.SelectedAccent = SlateAccent.Options[1];

            Assert.Equal("Slate gold", Assert.IsType<A11yEvent.AccentChosen>(Assert.Single(spoken)).Label);
            Assert.Equal(SlateAccent.SlateGold, accent.Current);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void CtrlCommaOpensTheSheetAndEscGivesFocusBack() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor took no keys");
        IInputElement before = Keyboard.FocusedElement;

        Press(host, Key.OemComma, ModifierKeys.Control);

        Assert.NotNull(host.Lifecycle.SettingsSheet);
        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
        Assert.Equal(Visibility.Visible, host.Shell.SettingsOverlay.Visibility);
        Assert.Same(CategoryRow(host, 0), Keyboard.FocusedElement);
        Assert.False(ShownShell.Descendants(host.Shell).OfType<Menu>().Single().IsEnabled);

        Press(host, Key.Escape);

        Assert.Null(host.Lifecycle.SettingsSheet);
        Assert.Null(host.Shell.OpenModalSurface);
        Assert.Equal(Visibility.Collapsed, host.Shell.SettingsOverlay.Visibility);
        Assert.Same(before, Keyboard.FocusedElement);
    });

    /// <summary>Back where it was, which needn't be the editor.</summary>
    [Fact]
    public void EscGivesFocusBackToTheTree() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Files), "premise: the tree took no keys");
        IInputElement before = Keyboard.FocusedElement;

        Press(host, Key.OemComma, ModifierKeys.Control);
        Press(host, Key.Escape);

        Assert.Same(before, Keyboard.FocusedElement);
    });

    /// <summary>The menu and the chord are fenced off under a sheet, so this is the command
    /// itself: it doesn't open over one.</summary>
    [Fact]
    public void TheSheetDefersToAnOpenSheet() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nkey: value\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        host.Settle();
        Assert.Equal(ModalSurface.AddProperty, host.Shell.OpenModalSurface);

        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Assert.Null(host.Lifecycle.SettingsSheet);
        Assert.Equal(ModalSurface.AddProperty, host.Shell.OpenModalSurface);
    });

    /// <summary>A theme isn't a vault's, so its setting doesn't need one.</summary>
    [Fact]
    public void TheSheetOpensWithNoVault() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        Assert.False(host.Lifecycle.IsVaultOpen);
        Assert.Null(SlateCommandRegistrar.DisabledReason(host.Lifecycle, ChordTable.Ids.OpenSettings));

        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Assert.Equal(Visibility.Visible, host.Shell.SettingsOverlay.Visibility);
        Assert.Same(CategoryRow(host, 0), Keyboard.FocusedElement);
        ThemeCombo(host).SelectedItem = ThemeChoice.Options[1];
        Assert.Equal(new[] { ThemeChoice.Dark }, host.ThemesApplied);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    [Fact]
    public void TheCloseButtonCloses() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        host.Shell.SettingsCloseButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        host.Settle();

        Assert.Null(host.Lifecycle.SettingsSheet);
    });

    [Fact]
    public void TheCategoryListMovesWithArrowsAndHasOneStop() => RunSta(() =>
    {
        using var host = new ShownShell();
        var second = new SettingsCategory("Editor", "\uE70F");
        var third = new SettingsCategory("Math", "\uE8EF");
        host.Lifecycle.OpenSettings(new[] { SettingsSheetViewModel.Appearance, second, third });
        host.Settle();
        SettingsSheetViewModel sheet = host.Lifecycle.SettingsSheet!;
        Assert.Same(CategoryRow(host, 0), Keyboard.FocusedElement);

        Press(host, Key.Down);
        Assert.Same(second, sheet.SelectedCategory);
        Assert.Same(CategoryRow(host, 1), Keyboard.FocusedElement);

        Press(host, Key.End);
        Assert.Same(third, sheet.SelectedCategory);

        Press(host, Key.Up);
        Assert.Same(second, sheet.SelectedCategory);

        Press(host, Key.Home);
        Assert.Same(SettingsSheetViewModel.Appearance, sheet.SelectedCategory);

        // One stop, whichever is open, and Tab and Shift+Tab come back to it.
        Press(host, Key.Down);
        MoveFocus(FocusNavigationDirection.Next);
        Assert.Same(ThemeCombo(host), Keyboard.FocusedElement);
        MoveFocus(FocusNavigationDirection.Previous);
        Assert.Same(CategoryRow(host, 1), Keyboard.FocusedElement);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    /// <summary>Tab goes round the sheet and never leaves it, or the note behind it would
    /// take the next key.</summary>
    [Fact]
    public void TabGoesRoundTheSheet() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        var stops = new List<IInputElement>();
        for (int index = 0; index < 4; index++)
        {
            stops.Add(Keyboard.FocusedElement);
            MoveFocus(FocusNavigationDirection.Next);
        }

        Assert.Equal<IInputElement>(new IInputElement[] { CategoryRow(host, 0), ThemeCombo(host), host.Shell.SettingsAccentComboBox, host.Shell.SettingsCloseButton }, stops);
        Assert.Same(CategoryRow(host, 0), Keyboard.FocusedElement);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    [Fact]
    public void ASettingRowLabelsItsControl() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        ComboBox combo = ThemeCombo(host);

        TextBlock label = Assert.IsType<TextBlock>(AutomationProperties.GetLabeledBy(combo));
        Assert.Equal("Theme", label.Text);
        Assert.Equal("Theme", UIElementAutomationPeer.CreatePeerForElement(combo).GetName());
        Assert.Equal("Light, Dark, or what Windows uses.", AutomationProperties.GetHelpText(combo));
        Assert.True(label.IsVisible);
        ComboBox accent = host.Shell.SettingsAccentComboBox;
        Assert.Equal("Accent color", UIElementAutomationPeer.CreatePeerForElement(accent).GetName());
        Assert.Equal("Automatic uses Slate gold unless you’ve chosen an accent color in Windows.", AutomationProperties.GetHelpText(accent));

        // A row itself isn't one more thing to hear.
        SettingRow row = ShownShell.Descendants(host.Shell.SettingsOverlay).OfType<SettingRow>().First();
        AutomationPeer rowPeer = UIElementAutomationPeer.CreatePeerForElement(row);
        Assert.False(rowPeer.IsControlElement());
        Assert.False(rowPeer.IsContentElement());
        Assert.False(row.Focusable);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    /// <summary>The name is there before anything walks the tree. This row is in no window,
    /// so no client on this desktop can have made the label's peer for it: a runner with no
    /// client at all read the combo's name as empty.</summary>
    [Fact]
    public void ARowsControlHasItsNameBeforeAnyoneWalksTheTree() => RunSta(() =>
    {
        using var host = new ShownShell();
        var combo = new ComboBox();
        var row = new SettingRow
        {
            Label = "Theme",
            Content = combo,
            Template = (ControlTemplate)host.Shell.FindResource("Slate.SettingRowTemplate"),
        };

        Assert.True(row.ApplyTemplate(), "premise: the row took no template");

        Assert.Equal("Theme", UIElementAutomationPeer.CreatePeerForElement(combo).GetName());
    });

    /// <summary>A row's size is the design's, and a glyph doesn't name the category.</summary>
    [Fact]
    public void TheSheetHasTheDesignsLayout() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Assert.Equal(760, host.Shell.SettingsOverlay.ActualWidth);
        Assert.Equal(600, host.Shell.SettingsOverlay.ActualHeight);
        Assert.Equal(new CornerRadius(8), host.Shell.SettingsOverlay.CornerRadius);
        Assert.Equal(240, ((Grid)host.Shell.SettingsOverlay.Child).ColumnDefinitions[0].ActualWidth);
        SettingRow row = ShownShell.Descendants(host.Shell.SettingsOverlay).OfType<SettingRow>().First();
        SettingRow next = ShownShell.Descendants(host.Shell.SettingsOverlay).OfType<SettingRow>().Last();
        Assert.Equal(row.ActualHeight + 4, next.TranslatePoint(default, row).Y);
        Border card = ShownShell.Descendants(row).OfType<Border>().First();
        Assert.Equal(68, row.ActualHeight);
        Assert.Equal(new Thickness(16, 12, 16, 12), card.Padding);
        Assert.Equal(new CornerRadius(4), card.CornerRadius);
        Assert.Equal(180, ThemeCombo(host).ActualWidth);

        ListBoxItem category = CategoryRow(host, 0);
        Assert.Equal("Appearance", UIElementAutomationPeer.CreatePeerForElement(category).GetName());
        AutomationPresentationTextBlock glyph = ShownShell.Descendants(category).OfType<AutomationPresentationTextBlock>().First();
        Assert.Equal(20, glyph.FontSize);
        Assert.Equal((char)0xE790, Assert.Single(glyph.Text));
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    /// <summary>In the smallest window it scrolls rather than going off the screen, and its
    /// row keeps its control.</summary>
    [Fact]
    public void TheSheetFitsTheSmallestWindow() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Shell.Width = 400;
        host.Shell.Height = 300;
        host.Settle();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Point corner = host.Shell.SettingsOverlay.TranslatePoint(
            new Point(host.Shell.SettingsOverlay.ActualWidth, host.Shell.SettingsOverlay.ActualHeight),
            (UIElement)host.Shell.Content);
        Assert.InRange(corner.X, 0, ((FrameworkElement)host.Shell.Content).ActualWidth);
        Assert.InRange(corner.Y, 0, ((FrameworkElement)host.Shell.Content).ActualHeight);
        ComboBox combo = ThemeCombo(host);
        Point right = combo.TranslatePoint(new Point(combo.ActualWidth, 0), host.Shell.SettingsPage);
        Assert.InRange(right.X, 0, host.Shell.SettingsPage.ActualWidth);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    [Fact]
    public void ChoosingAThemeAppliesItAtOnceAndKeepsFocusOnTheCombo() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        ComboBox combo = ThemeCombo(host);
        Assert.Equal("Use Windows setting", Assert.IsType<ThemeOption>(combo.SelectedItem).Label);
        Assert.Equal("Use Windows setting", combo.Text);
        Assert.True(combo.Focus(), "premise: the combo took no keys");

        // What ThemeManager does to the app's resources, here to the window's.
        ThemeSwapOnApply(host);
        combo.SelectedItem = ThemeChoice.Options[0];
        host.Settle();

        Assert.Equal(new[] { ThemeChoice.Light }, host.ThemesApplied);
        Assert.Equal(ThemeChoice.Light, host.Lifecycle.ThemePreference.Current);
        Assert.Same(combo, Keyboard.FocusedElement);
        Assert.Equal("Light", combo.Text);
        Assert.NotNull(host.Lifecycle.SettingsSheet);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    /// <summary>The accent's row does what the theme's does, and the default is Automatic.</summary>
    [Fact]
    public void ChoosingAnAccentAppliesItAtOnceAndKeepsFocusOnTheCombo() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        ComboBox combo = host.Shell.SettingsAccentComboBox;
        Assert.Equal(new[] { "Automatic", "Slate gold", "Windows accent" }, SlateAccent.Options.Select(option => option.Label));
        Assert.Equal("Automatic", combo.Text);
        Assert.True(combo.Focus(), "premise: the combo took no keys");

        combo.SelectedItem = SlateAccent.Options[2];
        host.Settle();

        Assert.Equal(new[] { SlateAccent.WindowsAccent }, host.AccentsApplied);
        Assert.Equal(SlateAccent.WindowsAccent, host.Lifecycle.AccentPreference.Current);
        Assert.Same(combo, Keyboard.FocusedElement);
        host.Lifecycle.CloseSettings();
        host.Settle();

        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Assert.Equal("Windows accent", host.Shell.SettingsAccentComboBox.Text);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    [Fact]
    public void ReopeningShowsTheThemeChosenLastTime() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.ThemePreference.Choose(ThemeChoice.Dark);
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();

        Assert.Equal("Dark", ThemeCombo(host).Text);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    /// <summary>An open list takes the first Esc, and the sheet the next.</summary>
    [Fact]
    public void EscWithTheListOpenClosesOnlyTheList() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        ComboBox combo = ThemeCombo(host);
        Assert.True(combo.Focus(), "premise: the combo took no keys");
        combo.IsDropDownOpen = true;
        host.Settle();

        Press(host, Key.Escape);

        Assert.False(combo.IsDropDownOpen);
        Assert.NotNull(host.Lifecycle.SettingsSheet);

        Press(host, Key.Escape);

        Assert.Null(host.Lifecycle.SettingsSheet);
    });

    /// <summary>Nothing with Control acts beneath the sheet, and no second sheet opens over
    /// it. Alt+Down is the combo box's own, so it stays.</summary>
    [Theory]
    [InlineData(Key.OemComma, ModifierKeys.Control)]
    [InlineData(Key.W, ModifierKeys.Control)]
    [InlineData(Key.O, ModifierKeys.Control)]
    [InlineData(Key.O, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.P, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.F, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.N, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData(Key.F, ModifierKeys.Control)]
    [InlineData(Key.F6, ModifierKeys.None)]
    public void NothingActsBeneathTheSheet(Key key, ModifierKeys modifiers) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        SettingsSheetViewModel sheet = host.Lifecycle.SettingsSheet!;
        IInputElement focused = Keyboard.FocusedElement;

        Press(host, key, modifiers);

        Assert.Same(sheet, host.Lifecycle.SettingsSheet);
        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
        Assert.False(host.Lifecycle.Palette.IsOpen);
        Assert.Single(host.Workspace.ActiveGroup.Tabs);
        Assert.Same(focused, Keyboard.FocusedElement);
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    [Fact]
    public void AltDownStillOpensTheList() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        ComboBox combo = ThemeCombo(host);
        Assert.True(combo.Focus(), "premise: the combo took no keys");

        Press(host, Key.Down, ModifierKeys.Alt);

        Assert.True(combo.IsDropDownOpen);
        combo.IsDropDownOpen = false;
        host.Lifecycle.CloseSettings();
        host.Settle();
    });

    [Fact]
    public void TheCommandsAreInTheirMenusAndTheChordTable() => RunSta(() =>
    {
        using var host = new ShownShell();
        Assert.Same(host.Lifecycle.OpenSettingsCommand, MenuItem(host, "SettingsMenuItem").Command);
        Assert.Same(host.Lifecycle.SwitchThemeCommand, MenuItem(host, "SwitchThemeMenuItem").Command);
        Assert.Same(host.Lifecycle.OpenSettingsCommand, SlateCommandRegistrar.Resolve(host.Lifecycle, ChordTable.Ids.OpenSettings));
        Assert.Same(host.Lifecycle.SwitchThemeCommand, SlateCommandRegistrar.Resolve(host.Lifecycle, ChordTable.Ids.SwitchTheme));
        ChordTableEntry settings = Assert.Single(ChordTable.RegisteredRows, row => row.Id == ChordTable.Ids.OpenSettings);
        Assert.Equal("Settings…", settings.Label);
        Assert.Equal("Ctrl+,", settings.WindowsChord);
        Assert.Contains(ChordTable.RegisteredRows, row => row.Id == ChordTable.Ids.SwitchTheme && row.Label == "Switch Theme…");
    });

    [Fact]
    public void QuickOpenSwallowsTheChord() =>
        Assert.True(MainWindow.IsUnderlyingShellShortcutForTests(Key.OemComma, ModifierKeys.Control));

    private static void ThemeSwapOnApply(ShownShell host)
    {
        ResourceDictionary[] current = host.Shell.Resources.MergedDictionaries
            .Take(ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false).Length)
            .ToArray();
        host.Lifecycle.ThemePreference = new ThemePreference(
            new AppPreferencesStore(Path.Combine(Path.GetTempPath(), $"slate-ds10-{Guid.NewGuid():N}", "preferences.json")),
            preference =>
            {
                host.ThemesApplied.Add(preference);
                foreach (ResourceDictionary layer in current)
                {
                    host.Shell.Resources.MergedDictionaries.Remove(layer);
                }

                ResourceDictionary[] next = ThemeManager.DictionariesFor(
                    ThemeChoice.Resolve(preference, systemIsDark: true), highContrast: false);
                for (int index = 0; index < next.Length; index++)
                {
                    host.Shell.Resources.MergedDictionaries.Insert(index, next[index]);
                }
            });
        host.Lifecycle.CloseSettings();
        host.Settle();
        host.Lifecycle.OpenSettingsCommand.Execute(null);
        host.Settle();
        Assert.True(ThemeCombo(host).Focus(), "premise: the combo took no keys");
    }

    private static ListBoxItem CategoryRow(ShownShell host, int index) =>
        Assert.IsType<ListBoxItem>(host.Shell.SettingsCategoriesList.ItemContainerGenerator.ContainerFromIndex(index));

    private static ComboBox ThemeCombo(ShownShell host) => host.Shell.SettingsThemeComboBox;

    private static MenuItem MenuItem(ShownShell host, string id) =>
        ShownShell.Descendants(host.Shell).OfType<Menu>().Single().Items.OfType<MenuItem>()
            .SelectMany(top => top.Items.OfType<MenuItem>())
            .SelectMany(menu => menu.Items.OfType<MenuItem>().Prepend(menu))
            .Single(item => AutomationProperties.GetAutomationId(item) == id);

    private static void MoveFocus(FocusNavigationDirection direction)
    {
        Assert.True(
            Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement).MoveFocus(new TraversalRequest(direction)),
            $"focus didn't move {direction}");
        PumpedDispatcher.Drain();
    }

    private static void Press(ShownShell host, Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        if (modifiers != ModifierKeys.None)
        {
            CommandPaletteTests.ThreadModifiers.Hold(modifiers, () => Press(host, key));
            return;
        }

        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
        host.Settle();
    }

    private static void RunSta(Action body) => StaThread.RunPumped(body);
}
