// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// DS-10 (#1811): a command can replace the palette's rows with a short list of choices, for
/// Switch Theme…. The rows run through the palette's one ranking path.
/// </summary>
public sealed partial class CommandPaletteTests
{
    private const string SwitchThemeId = "slate.view.switchTheme";

    private sealed class ChoicesHarness
    {
        public ChoicesHarness(string current = ThemeChoice.Dark)
        {
            Harness = new PaletteHarness(
                Cmd("slate.tasks.review", "Show Tasks Review", CommandSection.View),
                Cmd(SwitchThemeId, "Switch Theme…", CommandSection.View));
            Choices = new PaletteChoices(
                "Select a theme",
                "Current theme",
                ThemeChoice.Options.Select(option => new PaletteChoice(option.Value, option.Label)).ToArray(),
                current,
                Chosen.Add);
            Harness.Source.OnInvoke = id =>
            {
                if (id == SwitchThemeId)
                {
                    Harness.Palette.OpenChoices(Choices);
                }
            };
        }

        public PaletteHarness Harness { get; }

        public PaletteChoices Choices { get; }

        public List<PaletteChoice> Chosen { get; } = [];

        public CommandPaletteViewModel Palette => Harness.Palette;

        public void InvokeSwitchTheme()
        {
            Palette.Open();
            Palette.Select(Assert.Single(Palette.Rows, row => row.Id == SwitchThemeId));
            Harness.Announcements.Clear();
            Palette.InvokeSelected();
        }
    }

    [Fact]
    public void SwitchThemeListsTheThreeChoicesWithTheCurrentOneMarked()
    {
        var host = new ChoicesHarness();

        host.InvokeSwitchTheme();

        CommandPaletteViewModel palette = host.Palette;
        Assert.True(palette.IsOpen);
        Assert.True(palette.IsChoosing);
        Assert.Equal(new[] { ThemeChoice.Light, ThemeChoice.Dark, ThemeChoice.UseWindowsSetting }, palette.Rows.Select(row => row.Id));
        Assert.Equal(new[] { "Light", "Dark", "Use Windows setting" }, palette.Rows.Select(row => row.Label));
        Assert.Equal(ThemeChoice.Dark, palette.SelectedId);
        Assert.Equal(new string?[] { null, "Current theme", null }, palette.Rows.Select(row => row.ItemStatus));
        Assert.Equal(new[] { string.Empty, ((char)0xE73E).ToString(), string.Empty }, palette.Rows.Select(row => row.ChoiceMark));
        Assert.All(palette.Rows, row => Assert.True(row.IsChoice));
        Assert.Equal("Select a theme", palette.SearchFieldName);
        Assert.Contains("goes back", palette.SearchFieldHelp, StringComparison.Ordinal);
        Assert.Equal("Enter choose · Esc back", palette.KeysHint);
        Assert.Equal(string.Empty, palette.Query);
        A11yEvent.PaletteChoicesOpened opened = Assert.IsType<A11yEvent.PaletteChoicesOpened>(Assert.Single(host.Harness.Announcements));
        Assert.Equal("Select a theme", opened.Prompt);
        Assert.Equal("Dark", opened.Current);
        Assert.DoesNotContain("dismiss", host.Harness.Log);
        Assert.Contains(SwitchThemeId, host.Harness.Source.Recorded);
    }

    /// <summary>A command row has none of a choice's state.</summary>
    [Fact]
    public void ACommandRowIsNoChoice()
    {
        var host = new ChoicesHarness();

        host.Palette.Open();

        Assert.False(host.Palette.IsChoosing);
        Assert.Equal(CommandPaletteViewModel.CommandsFieldName, host.Palette.SearchFieldName);
        Assert.Equal("Enter run · Esc close", host.Palette.KeysHint);
        Assert.All(host.Palette.Rows, row =>
        {
            Assert.False(row.IsChoice);
            Assert.Null(row.ItemStatus);
            Assert.Equal(string.Empty, row.ChoiceMark);
        });
    }

    [Fact]
    public void MovingThroughTheChoicesSaysWhichIsCurrent()
    {
        var host = new ChoicesHarness();
        host.InvokeSwitchTheme();
        host.Harness.Announcements.Clear();

        host.Palette.MoveSelection(1);
        host.Palette.MoveSelection(-1);

        Assert.Collection(
            host.Harness.Announcements,
            first =>
            {
                A11yEvent.PaletteChoiceSelected selected = Assert.IsType<A11yEvent.PaletteChoiceSelected>(first);
                Assert.Equal("Use Windows setting", selected.Label);
                Assert.False(selected.IsCurrent);
            },
            second =>
            {
                A11yEvent.PaletteChoiceSelected selected = Assert.IsType<A11yEvent.PaletteChoiceSelected>(second);
                Assert.Equal("Dark", selected.Label);
                Assert.True(selected.IsCurrent);
            });
    }

    [Fact]
    public void ChoosingAppliesAndCloses()
    {
        var host = new ChoicesHarness();
        host.InvokeSwitchTheme();
        host.Palette.SelectFirst();
        host.Harness.Log.Clear();
        host.Harness.Source.Recorded.Clear();

        host.Palette.InvokeSelected();

        Assert.Equal(ThemeChoice.Light, Assert.Single(host.Chosen).Id);
        Assert.False(host.Palette.IsOpen);
        Assert.False(host.Palette.IsChoosing);
        Assert.Contains("dismiss", host.Harness.Log);
        // A choice isn't a command: it doesn't reach the registry or the recents.
        Assert.Equal(new[] { SwitchThemeId }, host.Harness.Source.Invoked);
        Assert.Empty(host.Harness.Source.Recorded);
    }

    [Fact]
    public void EscFromTheChoicesGoesBack()
    {
        var host = new ChoicesHarness();
        host.Palette.Open();
        host.Palette.Query = "theme";
        host.Palette.Select(Assert.Single(host.Palette.Rows, row => row.Id == SwitchThemeId));
        host.Palette.InvokeSelected();
        Assert.True(host.Palette.IsChoosing);
        host.Harness.Announcements.Clear();

        host.Palette.BackOrDismiss();

        Assert.True(host.Palette.IsOpen);
        Assert.False(host.Palette.IsChoosing);
        Assert.Equal("theme", host.Palette.Query);
        Assert.Contains(host.Palette.Rows, row => row.Id == SwitchThemeId);
        Assert.Equal(SwitchThemeId, host.Palette.SelectedId);
        Assert.Equal(CommandPaletteViewModel.CommandsFieldName, host.Palette.SearchFieldName);
        A11yEvent.PaletteCommandSelected back = Assert.IsType<A11yEvent.PaletteCommandSelected>(host.Harness.Announcements[0]);
        Assert.Equal("Switch Theme…", back.Label);
        Assert.Empty(host.Chosen);
        Assert.Equal(1, host.Harness.Source.ListCommandsCalls);

        // And the next Esc closes, as today.
        host.Palette.BackOrDismiss();

        Assert.False(host.Palette.IsOpen);
    }

    /// <summary>The menu's route has no commands to go back to.</summary>
    [Fact]
    public void EscFromChoicesOpenedElsewhereCloses()
    {
        var host = new ChoicesHarness();

        host.Palette.OpenChoices(host.Choices);

        Assert.True(host.Palette.IsOpen);
        Assert.Equal(3, host.Palette.Rows.Count);
        Assert.Contains("closes", host.Palette.SearchFieldHelp, StringComparison.Ordinal);
        Assert.Equal("Enter choose · Esc close", host.Palette.KeysHint);

        host.Palette.BackOrDismiss();

        Assert.False(host.Palette.IsOpen);
        Assert.Empty(host.Harness.Source.Invoked);
    }

    /// <summary>A theme isn't a vault's, so its list doesn't need one, though the commands do.</summary>
    [Fact]
    public void TheChoicesOpenWithNoVault()
    {
        var host = new ChoicesHarness();
        host.Harness.Source.IsVaultOpen = false;

        host.Palette.OpenChoices(host.Choices);

        Assert.True(host.Palette.IsOpen);
        Assert.Equal(3, host.Palette.Rows.Count);
        Assert.Empty(host.Harness.Announcements.OfType<A11yEvent.CommandPaletteNeedsVault>());
    }

    [Fact]
    public void TypingFiltersTheChoicesAndCountsChoices()
    {
        var host = new ChoicesHarness();
        host.InvokeSwitchTheme();
        host.Harness.Announcements.Clear();

        host.Palette.Query = "li";
        PumpedDispatcher.PumpUntilDrained(host.Palette.FilterCountCompletion);

        CommandPaletteRowViewModel only = Assert.Single(host.Palette.Rows);
        Assert.Equal(ThemeChoice.Light, only.Id);
        Assert.Equal(ThemeChoice.Light, host.Palette.SelectedId);
        A11yEvent.PaletteChoiceFilterCount count = Assert.Single(host.Harness.Announcements.OfType<A11yEvent.PaletteChoiceFilterCount>());
        Assert.Equal(1u, count.Count);
        Assert.Equal("li", count.Query);
        Assert.Empty(host.Harness.Announcements.OfType<A11yEvent.PaletteFilterCount>());

        host.Palette.Query = "zzz";

        Assert.Empty(host.Palette.Rows);
        Assert.True(host.Palette.ShowsNoMatches);
        Assert.Equal(CommandPaletteViewModel.NoChoiceMatchesDetail, host.Palette.EmptyStateDetail);
        Assert.Equal($"No matches. {CommandPaletteViewModel.NoChoiceMatchesDetail}", host.Palette.EmptyStateAccessibleName);
    }

    /// <summary>The palette's chord re-opens (PD-2), which is the commands, not the level.</summary>
    [Fact]
    public void AReOpenFromTheChoicesIsTheCommands()
    {
        var host = new ChoicesHarness();
        host.InvokeSwitchTheme();

        host.Palette.Open();

        Assert.False(host.Palette.IsChoosing);
        Assert.Contains(host.Palette.Rows, row => row.Id == SwitchThemeId);
        Assert.Equal(2, host.Harness.Source.ListCommandsCalls);
    }

    [Fact]
    public void ClosingLeavesNoLevelBehind()
    {
        var host = new ChoicesHarness();
        host.InvokeSwitchTheme();

        host.Palette.Dismiss();

        Assert.False(host.Palette.IsChoosing);
        host.Palette.Open();
        Assert.False(host.Palette.IsChoosing);
        Assert.Contains(host.Palette.Rows, row => row.Id == SwitchThemeId);
        host.Palette.BackOrDismiss();
        Assert.False(host.Palette.IsOpen);
    }

    /// <summary>A prompt over the shell seals the palette, and a list can't open under it.</summary>
    [Fact]
    public void ASealedPaletteOpensNoChoices()
    {
        var host = new ChoicesHarness();
        host.Palette.SetModalLoop(true);

        host.Palette.OpenChoices(host.Choices);

        Assert.False(host.Palette.IsOpen);
        host.Palette.SetModalLoop(false);
    }

    /// <summary>Choices wait for their command to succeed. One that fails afterward leaves
    /// the commands as they were, and nothing behind for the next command to trip on.</summary>
    [Fact]
    public void ACommandThatFailsAfterOpeningChoicesLeavesTheCommands()
    {
        var host = new ChoicesHarness();
        host.Harness.Source.InvokeFailures[SwitchThemeId] = new CommandException.ActionFailed("no");

        host.InvokeSwitchTheme();

        Assert.True(host.Palette.IsOpen);
        Assert.False(host.Palette.IsChoosing);
        Assert.Contains(host.Palette.Rows, row => row.Id == SwitchThemeId);
        Assert.IsType<A11yEvent.PaletteCommandFailed>(Assert.Single(host.Harness.Announcements));
        Assert.Empty(host.Harness.Source.Recorded);

        host.Harness.Source.InvokeFailures.Clear();
        host.Harness.Source.OnInvoke = null;
        host.Palette.Select(Assert.Single(host.Palette.Rows, row => row.Id == "slate.tasks.review"));
        host.Palette.InvokeSelected();

        Assert.False(host.Palette.IsOpen);
        Assert.False(host.Palette.IsChoosing);
    }

    /// <summary>Esc acts under the seal (T9). It's the reader's word over the command's.</summary>
    [Fact]
    public void EscDuringTheCommandLeavesNoChoicesBehind()
    {
        var host = new ChoicesHarness();
        host.Harness.Source.OnInvoke = _ =>
        {
            host.Palette.OpenChoices(host.Choices);
            host.Palette.BackOrDismiss();
        };

        host.InvokeSwitchTheme();

        Assert.False(host.Palette.IsOpen);
        Assert.False(host.Palette.IsChoosing);
        host.Harness.Source.OnInvoke = null;
        host.Palette.Open();
        Assert.False(host.Palette.IsChoosing);
        host.Palette.Select(Assert.Single(host.Palette.Rows, row => row.Id == "slate.tasks.review"));
        host.Palette.InvokeSelected();
        Assert.False(host.Palette.IsOpen);
    }

    /// <summary>The seal would drop the rows, so a loop still up when the command returns
    /// keeps to P9's dismissal.</summary>
    [Fact]
    public void AModalLoopStillUpAfterTheCommandClosesThePalette()
    {
        var host = new ChoicesHarness();
        host.Harness.Source.OnInvoke = _ =>
        {
            host.Palette.OpenChoices(host.Choices);
            host.Palette.SetModalLoop(true);
        };

        host.InvokeSwitchTheme();

        Assert.False(host.Palette.IsOpen);
        Assert.False(host.Palette.IsChoosing);
        host.Palette.SetModalLoop(false);
    }

    /// <summary>The shipped shell: Settings… retires the palette, and focus comes back to
    /// what had it before the palette opened.</summary>
    [Fact]
    public void ThePalettesSettingsRowOpensTheSheetAlone() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        host.Lifecycle.ThemePreference = new ThemePreference(new AppPreferencesStore(host.FileInFixture("preferences.json")), _ => { });
        var spoken = new List<A11yEvent>();
        (typeof(VaultLifecycleViewModel).GetField("_announce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("VaultLifecycleViewModel._announce is gone"))
            .SetValue(host.Lifecycle, (Action<A11yEvent>)spoken.Add);
        host.OpenThePalette();
        host.Palette.Select(Assert.Single(host.Palette.Rows, row => row.Id == ChordTable.Ids.OpenSettings));

        host.PressEnterInThePalette();

        Assert.False(host.Palette.IsOpen);
        Assert.NotNull(host.Lifecycle.SettingsSheet);
        Assert.IsType<A11yEvent.SettingsSheetShown>(Assert.Single(spoken));
        Assert.Equal(ModalSurface.Settings, host.Shell.OpenModalSurface);
        host.Settle();
        Assert.Same(
            host.Shell.SettingsCategoriesList.ItemContainerGenerator.ContainerFromIndex(0),
            System.Windows.Input.Keyboard.FocusedElement);

        host.Lifecycle.CloseSettings();
        host.Settle();

        Assert.Same(host.Sentinel, System.Windows.Input.Keyboard.FocusedElement);
    });

    /// <summary>The shipped shell, through the registry: the row's level, a real choice, and
    /// focus back where it started.</summary>
    [Fact]
    public void SwitchThemeRunsInTheShell() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        var applied = new List<string>();
        host.Lifecycle.ThemePreference = new ThemePreference(new AppPreferencesStore(host.FileInFixture("preferences.json")), applied.Add);
        var spoken = new List<A11yEvent>();
        (typeof(VaultLifecycleViewModel).GetField("_announce", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("VaultLifecycleViewModel._announce is gone"))
            .SetValue(host.Lifecycle, (Action<A11yEvent>)spoken.Add);
        host.OpenThePalette();
        host.Palette.Select(Assert.Single(host.Palette.Rows, row => row.Id == ChordTable.Ids.SwitchTheme));

        host.PressEnterInThePalette();

        Assert.True(PumpedDispatcher.PumpUntil(() => host.Palette.Rows.Count == 3, TimeSpan.FromSeconds(10)), "the themes never published");
        host.Settle();
        Assert.Equal("Select a theme", System.Windows.Automation.AutomationProperties.GetName(host.Shell.CommandPaletteSearchTextBox));
        Assert.Same(host.Shell.CommandPaletteSearchTextBox, System.Windows.Input.Keyboard.FocusedElement);
        Assert.Equal(ThemeChoice.UseWindowsSetting, host.Palette.SelectedId);
        Assert.Same(host.Palette.SelectedRow, host.Shell.CommandPaletteResultsList.SelectedItem);
        // One list, with no section to head it.
        Assert.True(host.Shell.CommandPaletteResultsList.Items.Groups is null or { Count: 0 });
        Assert.Contains(host.Heard, heard => heard is A11yEvent.PaletteChoicesOpened);

        host.Palette.SelectFirst();
        host.PressEnterInThePalette();
        host.Settle();

        Assert.False(host.Palette.IsOpen);
        Assert.Equal(new[] { ThemeChoice.Light }, applied);
        Assert.Equal("Light", Assert.IsType<A11yEvent.ThemeChosen>(Assert.Single(spoken)).Label);
        Assert.Same(host.Sentinel, System.Windows.Input.Keyboard.FocusedElement);
        Assert.Contains(ChordTable.Ids.SwitchTheme, host.PersistedRecents());
    });

    /// <summary>The menu's route, with no vault and no commands to go back to.</summary>
    [Fact]
    public void SwitchThemeWorksAtTheWelcomeView() => RunSta(() =>
    {
        using var host = new ShownShell();
        host.ShowWelcomeWithoutAWorkspace();
        Assert.Null(SlateCommandRegistrar.DisabledReason(host.Lifecycle, ChordTable.Ids.SwitchTheme));

        host.Lifecycle.SwitchThemeCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => host.Lifecycle.Palette.Rows.Count == 3, TimeSpan.FromSeconds(10)), "the themes never published");
        host.Settle();

        Assert.Equal(System.Windows.Visibility.Visible, host.Shell.CommandPaletteOverlay.Visibility);
        Assert.Same(host.Shell.CommandPaletteSearchTextBox, System.Windows.Input.Keyboard.FocusedElement);

        host.Lifecycle.Palette.BackOrDismiss();
        host.Settle();

        Assert.False(host.Lifecycle.Palette.IsOpen);
    });
}
