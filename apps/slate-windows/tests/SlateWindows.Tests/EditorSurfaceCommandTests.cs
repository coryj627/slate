// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-22 (#1661, owner decision CD14). Activate at Cursor and Preview
/// Embed act at the editor's caret, so they are unavailable while the note is
/// in reading mode and its editor is hidden: the menu items dim, the palette
/// rows say their own unavailable sentence, and no entry of the coordinator
/// acts, speaks or queues work for a hidden caret. Leaving the editor closes
/// its popover and drops a preview still pending. And a Ctrl+click on prose
/// says nothing while the note's links and tasks load; the key still says why
/// nothing happened, and a note changed on disk still says to reload it.
/// </summary>
/// <remarks>
/// Every fact that enters reading mode runs on an STA thread: on the test
/// thread the reading view's own refresh speaks a failure line into the
/// fact's announcement list, which would defeat a "says nothing" assertion.
/// </remarks>
public sealed class EditorSurfaceCommandTests
{
    private const string SourceText =
        "# Source\n\nPlain prose with nothing to act on.\n\n- [ ] buy milk\n\n[[target#Destination]]\n\n![[target#Destination]]\n";

    private const string TargetText = "# Lead\n\n## Destination\n\nSection body.\n";
    private const string OtherText = "# Other\n\nOther body.\n";

    private const string Loading = "Editor interactions are still loading; try again.";
    private const string RefreshFailed = "Editor interaction data could not be refreshed; try again.";
    private const string Reload = "Reload source.md before using this interaction.";
    private const string Unavailable = "This command is not available right now.";

    // ---- The predicates (Design 1) ------------------------------------------

    [Fact]
    public void ActivateAtCursorIsUnavailableInReadingMode() => RunSta(() =>
    {
        using var vault = new Vault();
        using WorkspaceTabViewModel tab = vault.Tab();
        EditorInteractionCoordinator interactions = Current(tab);
        Assert.True(interactions.ActivateAtCaretCommand.CanExecute(null), "premise: available in the editor");

        tab.ToggleViewMode();
        Assert.True(tab.IsReadingMode);
        Assert.False(interactions.ActivateAtCaretCommand.CanExecute(null), "Activate at Cursor is available with the editor hidden");

        tab.ToggleViewMode();
        Assert.True(interactions.ActivateAtCaretCommand.CanExecute(null), "Activate at Cursor stayed unavailable back in the editor");
    });

    [Fact]
    public void PreviewEmbedIsUnavailableInReadingMode() => RunSta(() =>
    {
        using var vault = new Vault();
        using WorkspaceTabViewModel tab = vault.Tab();
        EditorInteractionCoordinator interactions = Current(tab);
        Assert.True(interactions.PreviewEmbedCommand.CanExecute(null), "premise: available in the editor");

        tab.ToggleViewMode();
        Assert.True(tab.IsReadingMode);
        Assert.False(interactions.PreviewEmbedCommand.CanExecute(null), "Preview Embed is available with the editor hidden");

        tab.ToggleViewMode();
        Assert.True(interactions.PreviewEmbedCommand.CanExecute(null), "Preview Embed stayed unavailable back in the editor");
    });

    // ---- The requery (Design 2) ---------------------------------------------

    /// <summary>A menu item asks its command again only when the command says
    /// its answer changed. Each route that switches the mode raises both
    /// commands, in both directions, and the answer read inside the handler is
    /// already the new one.</summary>
    [Theory]
    [InlineData("the tab's own toggle")]
    [InlineData("the shell's SetViewMode")]
    [InlineData("Ctrl+Shift+E's command")]
    public void BothEditorCommandsRequeryWhenTheModeSwitches(string route) => RunSta(() =>
    {
        using var vault = new Vault();
        using WorkspaceTabViewModel? bare = route == "the tab's own toggle" ? vault.Tab() : null;
        using LifecycleHost? host = bare is null ? new LifecycleHost() : null;
        WorkspaceTabViewModel tab = bare ?? host!.OpenSource();
        EditorInteractionCoordinator interactions = Current(tab, refresh: bare is not null);
        ICommand activate = interactions.ActivateAtCaretCommand;
        ICommand preview = interactions.PreviewEmbedCommand;
        var activateAnswers = new List<bool>();
        var previewAnswers = new List<bool>();
        activate.CanExecuteChanged += (_, _) => activateAnswers.Add(activate.CanExecute(null));
        preview.CanExecuteChanged += (_, _) => previewAnswers.Add(preview.CanExecute(null));

        void Switch(bool reading)
        {
            switch (route)
            {
                case "the tab's own toggle":
                    tab.ToggleViewMode();
                    break;
                case "the shell's SetViewMode":
                    host!.Workspace.SetViewMode(tab, reading, landFocus: true);
                    break;
                default:
                    host!.Workspace.ToggleReadingModeCommand.Execute(null);
                    break;
            }

            PumpedDispatcher.Drain();
            Assert.Equal(reading, tab.IsReadingMode);
        }

        Switch(reading: true);
        Assert.True(activateAnswers.Count > 0, $"{route}: Activate at Cursor raised nothing on the way into reading mode");
        Assert.True(previewAnswers.Count > 0, $"{route}: Preview Embed raised nothing on the way into reading mode");
        Assert.All(activateAnswers, answer => Assert.False(answer, $"{route}: Activate at Cursor raised before its answer changed"));
        Assert.All(previewAnswers, answer => Assert.False(answer, $"{route}: Preview Embed raised before its answer changed"));

        activateAnswers.Clear();
        previewAnswers.Clear();
        Switch(reading: false);
        Assert.True(activateAnswers.Count > 0, $"{route}: Activate at Cursor raised nothing on the way back to the editor");
        Assert.True(previewAnswers.Count > 0, $"{route}: Preview Embed raised nothing on the way back to the editor");
        Assert.All(activateAnswers, answer => Assert.True(answer, $"{route}: Activate at Cursor raised before its answer changed"));
        Assert.All(previewAnswers, answer => Assert.True(answer, $"{route}: Preview Embed raised before its answer changed"));
    });

    // ---- The palette (CD14: its own unavailable sentence) --------------------

    [Fact]
    public void TheRegistrarRefusesBothCommandsInReadingMode() => RunSta(() =>
    {
        using var host = new LifecycleHost();
        WorkspaceTabViewModel tab = host.OpenSource();
        string[] ids = [ChordTable.Ids.ActivateAtCaret, ChordTable.Ids.PreviewEmbed];
        Assert.All(ids, id => Assert.Null(SlateCommandRegistrar.DisabledReason(host.Lifecycle, id)));

        host.Workspace.SetViewMode(tab, reading: true, landFocus: true);
        Assert.All(ids, id => Assert.Equal(
            SlateCommandRegistrar.UnavailableReason, SlateCommandRegistrar.DisabledReason(host.Lifecycle, id)));

        host.Workspace.SetViewMode(tab, reading: false, landFocus: true);
        Assert.All(ids, id => Assert.Null(SlateCommandRegistrar.DisabledReason(host.Lifecycle, id)));
    });

    [Theory]
    [InlineData(ChordTable.Ids.ActivateAtCaret, "Selected: Activate at Cursor. Unavailable: This command is not available right now.")]
    [InlineData(ChordTable.Ids.PreviewEmbed, "Selected: Preview Embed. Unavailable: This command is not available right now.")]
    public void ThePaletteSpeaksItsUnavailableSentenceForBothRowsInReadingMode(string id, string selected) => RunSta(() =>
    {
        using var host = new LifecycleHost();
        WorkspaceTabViewModel tab = host.OpenSource();
        EditorInteractionCoordinator interactions = Current(tab, refresh: false);
        host.Workspace.SetViewMode(tab, reading: true, landFocus: true);
        // The hidden caret sits where each command would act: a task for
        // Activate at Cursor, an embed for Preview Embed.
        tab.EditorCaretOffset = id == ChordTable.Ids.PreviewEmbed ? Offset("embed") : Offset("task");
        string path = Path.Combine(host.Root, "source.md");
        string before = File.ReadAllText(path);
        CommandPaletteViewModel palette = host.Lifecycle.Palette;
        palette.Open();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending && palette.Rows.Count > 0, TimeSpan.FromSeconds(20)),
            "the palette's rows never published");
        CommandPaletteRowViewModel row = Assert.Single(palette.Rows, candidate => candidate.Id == id);
        // The first selection change after the open is not spoken (P10).
        palette.Select(palette.Rows.First(candidate => candidate.Id != id));
        PumpedDispatcher.Drain();
        host.Announced.Clear();

        palette.Select(row);
        PumpedDispatcher.Drain();
        A11yEvent.PaletteCommandSelected selection =
            Assert.IsType<A11yEvent.PaletteCommandSelected>(Assert.Single(host.Announced));
        Assert.Equal(SlateCommandRegistrar.UnavailableReason, selection.DisabledReason);
        Assert.Equal(selected, SlateUniffiMethods.A11yRender(selection).Text);
        Assert.Equal(SlateCommandRegistrar.UnavailableReason, row.DisabledReason);

        host.Announced.Clear();
        palette.Invoke(row);
        Quiesce(tab, interactions);
        A11yEvent.PaletteCommandUnavailable refused =
            Assert.IsType<A11yEvent.PaletteCommandUnavailable>(Assert.Single(host.Announced));
        Assert.Equal(Unavailable, SlateUniffiMethods.A11yRender(refused).Text);
        Assert.True(palette.IsOpen, "the palette closed on a refused row");
        Assert.False(interactions.IsPopoverOpen, "a preview opened from the palette in reading mode");
        Assert.Equal(before, StableText(path));
    });

    // ---- The guard (Design 3) ------------------------------------------------

    public static TheoryData<string, string> EntriesAndTargets()
    {
        var rows = new TheoryData<string, string>();
        foreach (string entry in new[]
                 {
                     "ActivateAtCaretCommand", "PreviewEmbedCommand", "ActivateAt by the key",
                     "ActivateAt by the pointer", "PreviewEmbedAt",
                 })
        {
            foreach (string target in new[] { "task", "wikilink", "embed" })
            {
                rows.Add(entry, target);
            }
        }

        // The UIA link peer's entry; a task is no span.
        rows.Add("ActivateSpan", "wikilink");
        rows.Add("ActivateSpan", "embed");
        return rows;
    }

    /// <summary>With the editor hidden, no entry acts at its caret, whoever
    /// calls it: the two commands, the editor's own handlers and the UIA link
    /// peer. Nothing is toggled, followed, opened or said.</summary>
    [Theory]
    [MemberData(nameof(EntriesAndTargets))]
    public void NothingActsAtTheHiddenCaret(string entry, string target) => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        var navigated = new List<EditorNavigationRequest>();
        using WorkspaceTabViewModel tab = vault.Tab(announced, navigated);
        EditorInteractionCoordinator interactions = Current(tab);
        tab.ToggleViewMode();
        Settle();
        announced.Clear();
        string before = File.ReadAllText(vault.SourcePath);

        bool? answered = Call(entry, target, tab, interactions);
        Quiesce(tab, interactions);

        Assert.Equal(before, StableText(vault.SourcePath));
        Assert.Empty(navigated);
        Assert.False(interactions.IsPopoverOpen, $"{entry} on the {target} opened a popover over the reading view");
        Assert.Empty(announced.Select(Render));
        Assert.Equal(ExpectedAnswer(entry), answered);
    });

    /// <summary>And none queues work: with the note's links and tasks never
    /// loaded, an entry called for a hidden editor starts no load, so nothing
    /// is said then or later and no preview opens when a load ends.</summary>
    [Theory]
    [InlineData("ActivateAt by the key", "prose")]
    [InlineData("ActivateAt by the pointer", "prose")]
    [InlineData("ActivateAtCaretCommand", "prose")]
    [InlineData("PreviewEmbedAt", "embed")]
    public void NothingIsQueuedOrSaidForAHiddenEditorWhoseCacheNeverLoaded(string entry, string target) => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = vault.Tab(announced, artifactCacheCurrent: false);
        EditorInteractionCoordinator interactions = Current(tab);
        tab.ToggleViewMode();
        Settle();
        announced.Clear();
        Assert.Equal(0, interactions.ArtifactCacheLoadCountForTests);

        bool? answered = Call(entry, target, tab, interactions);
        Quiesce(tab, interactions);

        Assert.Equal(0, interactions.ArtifactCacheLoadCountForTests);
        Assert.False(interactions.IsPopoverOpen, $"{entry} opened a preview in reading mode once a load ended");
        Assert.Empty(announced.Select(Render));
        Assert.Equal(ExpectedAnswer(entry), answered);
    });

    // ---- Leaving the editor closes its transient UI (Design 4) -----------------

    [Fact]
    public void AnOpenPopoverClosesWhenTheNoteLeavesTheEditor() => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = vault.Tab(announced);
        EditorInteractionCoordinator interactions = Current(tab);
        Assert.True(interactions.PreviewEmbedAt(Offset("embed")));
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => interactions.IsPopoverOpen && !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal),
                TimeSpan.FromSeconds(20)),
            "premise: the preview never opened in the editor");
        Quiesce(tab, interactions);
        int focusRequests = 0;
        interactions.FocusRequested += (_, _) => focusRequests++;
        announced.Clear();

        tab.ToggleViewMode();
        Settle();

        Assert.False(interactions.IsPopoverOpen, "the editor's popover stayed open over the reading view");
        Assert.Empty(announced.Select(Render));
        Assert.Equal(0, focusRequests);

        tab.ToggleViewMode();
        Settle();
        Assert.False(interactions.IsPopoverOpen, "the popover came back with the editor");
        Assert.Empty(announced.Select(Render));
        Assert.Equal(0, focusRequests);
    });

    [Fact]
    public void APreviewStillPendingDoesNotOpenAfterTheSwitchToReading() => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = vault.Tab(announced, artifactCacheCurrent: false);
        EditorInteractionCoordinator interactions = Current(tab);
        Assert.True(interactions.PreviewEmbedAt(Offset("embed")));
        Assert.False(interactions.IsPopoverOpen, "premise: the preview waits for the note's links");
        Assert.Empty(announced);

        tab.ToggleViewMode();
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => interactions.ArtifactCacheSourceCurrentForTests && Idle(tab, interactions),
                TimeSpan.FromSeconds(20)),
            "the load the preview asked for never published");
        Quiesce(tab, interactions);

        Assert.False(interactions.IsPopoverOpen, "the pending preview opened in reading mode");
        Assert.Empty(announced.Select(Render));
    });

    [Fact]
    public void ALoadTheKeyAskedForSaysNothingOnceTheReaderHasLeftTheEditor() => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        int attempts = 0;
        using WorkspaceTabViewModel tab = vault.Tab(
            announced, artifactCacheCurrent: false, fault: TerminalArtifactFault(() => Interlocked.Increment(ref attempts)));
        EditorInteractionCoordinator interactions = Current(tab);
        Assert.True(interactions.ActivateAt(Offset("prose"), EditorInteractionOrigin.Keyboard));
        Assert.Equal(Loading, Assert.IsType<A11yEvent.HostComposed>(Assert.Single(announced)).Text);
        announced.Clear();

        // The switch stands before the load's end: its publication is queued
        // to this thread, which has not pumped since the key.
        tab.ToggleViewMode();
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => Volatile.Read(ref attempts) >= 3 && Idle(tab, interactions), TimeSpan.FromSeconds(20)),
            "the faulted load never ended");
        Settle();

        Assert.Empty(announced.Select(Render));
    });

    /// <summary>In the shipped window, with the keys inside the popover: the
    /// switch closes it and the keys land on the reading surface, never in the
    /// collapsed editor, on both of the switch's routes (Ctrl+Shift+E lands
    /// focus itself; the note header's switch does not when the keys are
    /// outside the editor).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheKeysLandOnTheReadingSurfaceWhenThePopoverClosesAtTheSwitch(bool landFocus) => RunSta(() =>
    {
        using var host = new ShownShell(("source.md", SourceText), ("target.md", TargetText));
        host.Workspace.OpenPath("source.md");
        host.Settle();
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        EditorInteractionCoordinator interactions = Current(tab, refresh: false);
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => interactions.MathRangesCurrentForTests
                    && interactions.ArtifactCacheSourceCurrentForTests
                    && interactions.IsBackgroundIdle,
                TimeSpan.FromSeconds(30)),
            "premise: the note's interaction caches never loaded");
        host.Shell.Activate();
        host.Settle();
        FrameworkElement popover = ShownShell.Descendants(host.Shell).OfType<FrameworkElement>()
            .First(element => AutomationProperties.GetAutomationId(element) == "EditorInteractionPopover");
        SlateTextEditor editor = ShownShell.Descendants(host.Shell).OfType<SlateTextEditor>().First();
        Assert.True(interactions.PreviewEmbedAt(Offset("embed")));
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => interactions.IsPopoverOpen
                    && !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal)
                    && popover.IsKeyboardFocusWithin,
                TimeSpan.FromSeconds(20)),
            $"keyboard premise: the keys never went into the open popover (they are on {Describe(Keyboard.FocusedElement)})");
        host.Settle();
        host.Announced.Clear();

        host.Workspace.SetViewMode(tab, reading: true, landFocus: landFocus);
        bool landed = PumpedDispatcher.PumpUntil(
            () => Keyboard.FocusedElement is SlateWindows.Reading.ReadingSurface, TimeSpan.FromSeconds(10));
        host.Settle();

        Assert.False(interactions.IsPopoverOpen, "the popover stayed open over the reading view");
        Assert.False(popover.IsVisible, "the popover is still drawn over the reading view");
        Assert.True(landed, $"the keys are on {Describe(Keyboard.FocusedElement)}, not on the reading surface");
        Assert.False(
            CommandPaletteTests.TheKeysAreIn(editor), $"the keys are in the collapsed editor: {Describe(Keyboard.FocusedElement)}");
        Assert.Empty(host.Announced.Select(Render));
    });

    // ---- The pointer while the cache loads (Design 5, ruling 4) ---------------

    /// <summary>A Ctrl+click in a note whose links and tasks have never loaded
    /// says nothing: not at the click, not when the load ends, and not when it
    /// faults. No task list is published yet, so a click on a checkbox cannot
    /// be told from a click on prose, and its rows are silent as prose is. The
    /// click is still taken.</summary>
    [Theory]
    [InlineData("prose", false)]
    [InlineData("prose", true)]
    [InlineData("checkbox", false)]
    [InlineData("checkbox", true)]
    public void ACtrlClickStaysSilentWhileTheNotesFirstTaskListLoads(string target, bool theLoadFaults) => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        int attempts = 0;
        using WorkspaceTabViewModel tab = vault.Tab(
            announced,
            artifactCacheCurrent: false,
            fault: theLoadFaults ? TerminalArtifactFault(() => Interlocked.Increment(ref attempts)) : null);
        EditorInteractionCoordinator interactions = Current(tab);
        string before = File.ReadAllText(vault.SourcePath);

        Assert.True(
            interactions.ActivateAt(Offset(target), EditorInteractionOrigin.Pointer),
            "the click was passed on while the cache loads");
        Assert.Empty(announced.Select(Render));

        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => (theLoadFaults
                        ? Volatile.Read(ref attempts) >= 3
                        : interactions.ArtifactCacheSourceCurrentForTests)
                    && Idle(tab, interactions),
                TimeSpan.FromSeconds(20)),
            "the load the click started never ended");
        Settle();

        Assert.Empty(announced.Select(Render));
        Assert.Equal(before, StableText(vault.SourcePath));
    });

    /// <summary>Any scan or core write moves the session on, which is the
    /// loading state for every other open note; the task list each last
    /// published stays in place and still describes its saved text. There a
    /// Ctrl+click on a checkbox that list names is on a real target, so it
    /// hears why nothing happened, as a Ctrl+click on a link does. On prose,
    /// in that same state, it stays silent. Either way the click is taken, a
    /// load starts, its end says nothing and nothing is written.</summary>
    [Theory]
    [InlineData("checkbox")]
    [InlineData("prose")]
    public void ACtrlClickOnACheckboxTheCacheStillNamesSaysWhyNothingHappened(string target) => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = vault.Tab(announced);
        EditorInteractionCoordinator interactions = Current(tab);
        vault.ChangeAnotherNote();
        long loads = interactions.ArtifactCacheLoadCountForTests;
        string before = File.ReadAllText(vault.SourcePath);

        Assert.True(
            interactions.ActivateAt(Offset(target), EditorInteractionOrigin.Pointer),
            "the click was passed on while the cache loads");

        if (target == "checkbox")
        {
            A11yEvent.HostComposed spoken = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(announced));
            Assert.Equal(Loading, spoken.Text);
            Assert.Equal(A11yPriority.Medium, spoken.Priority);
        }
        else
        {
            Assert.Empty(announced.Select(Render));
        }

        announced.Clear();
        Quiesce(tab, interactions);

        Assert.Empty(announced.Select(Render));
        Assert.True(interactions.ArtifactCacheLoadCountForTests > loads, "the click started no load");
        Assert.Equal(before, StableText(vault.SourcePath));
    });

    /// <summary>A list from an older save of this note cannot vouch for a
    /// click: its positions describe text the note no longer holds. Here a
    /// paragraph typed above the task and saved leaves the old list naming a
    /// checkbox where the note now holds prose, and a Ctrl+click there stays
    /// silent, as on any prose while the cache loads.</summary>
    [Fact]
    public void ACtrlClickWhereAnOlderSavesListNamedACheckboxStaysSilent() => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = vault.Tab(announced);
        EditorInteractionCoordinator interactions = Current(tab);
        int published = interactions.ArtifactCacheGenerationForTests;
        long loads = interactions.ArtifactCacheLoadCountForTests;
        const string typed = "A paragraph typed above the task.\n\n";
        tab.EditorDocument!.Insert(At("- [ ] buy milk", 0), typed);
        Assert.True(tab.Save(), "premise: the note saved");
        interactions.RefreshMathRangesForTests();
        string saved = StableText(vault.SourcePath);
        Assert.Contains(typed + "- [ ] buy milk", saved, StringComparison.Ordinal);
        Assert.False(tab.IsDirty, "premise: the tab is clean after its save");
        Assert.True(
            interactions.ArtifactCacheGenerationForTests == published
                && interactions.ArtifactCacheLoadCountForTests == loads,
            "premise: the list published before the save is still the one in place");
        announced.Clear();

        // Where the checkbox was before the save: prose now.
        Assert.True(
            interactions.ActivateAt(Offset("checkbox"), EditorInteractionOrigin.Pointer),
            "the click was passed on while the cache loads");
        Assert.Empty(announced.Select(Render));

        Quiesce(tab, interactions);

        Assert.Empty(announced.Select(Render));
        Assert.True(interactions.ArtifactCacheLoadCountForTests > loads, "the click started no load");
        Assert.Equal(saved, StableText(vault.SourcePath));
    });

    /// <summary>Ruling 4: a note changed on disk is another state. "Reload x.md
    /// before using this interaction." is true and says what to do, so a
    /// Ctrl+click hears it too, on prose as on a checkbox, and nothing is
    /// written or loaded.</summary>
    [Theory]
    [InlineData("prose")]
    [InlineData("checkbox")]
    public void ACtrlClickInANoteChangedOnDiskStillSaysToReloadIt(string target) => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = vault.Tab(announced, artifactCacheCurrent: false);
        EditorInteractionCoordinator interactions = Current(tab);
        string outside = vault.ChangeTheNoteOnDisk(interactions);
        long loads = interactions.ArtifactCacheLoadCountForTests;

        Assert.True(interactions.ActivateAt(Offset(target), EditorInteractionOrigin.Pointer));
        Quiesce(tab, interactions);

        A11yEvent.HostComposed spoken = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(announced));
        Assert.Equal(Reload, spoken.Text);
        Assert.Equal(A11yPriority.High, spoken.Priority);
        Assert.Equal(outside, StableText(vault.SourcePath));
        Assert.Equal(loads, interactions.ArtifactCacheLoadCountForTests);
    });

    /// <summary>The key is unchanged: Ctrl+Enter says why nothing happened in
    /// each state, and a load it asked for still says so when it faults.</summary>
    [Theory]
    [InlineData("loading")]
    [InlineData("loading, and the load faults")]
    [InlineData("changed on disk")]
    public void CtrlEnterStillSaysWhyNothingHappened(string state) => RunSta(() =>
    {
        using var vault = new Vault();
        var announced = new List<A11yEvent>();
        int attempts = 0;
        bool faults = state == "loading, and the load faults";
        using WorkspaceTabViewModel tab = vault.Tab(
            announced,
            artifactCacheCurrent: false,
            fault: faults ? TerminalArtifactFault(() => Interlocked.Increment(ref attempts)) : null);
        EditorInteractionCoordinator interactions = Current(tab);
        bool changedOnDisk = state == "changed on disk";
        if (changedOnDisk)
        {
            _ = vault.ChangeTheNoteOnDisk(interactions);
        }

        Assert.True(interactions.ActivateAt(Offset("prose"), EditorInteractionOrigin.Keyboard));

        A11yEvent.HostComposed spoken = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(announced));
        Assert.Equal(changedOnDisk ? Reload : Loading, spoken.Text);
        Assert.Equal(changedOnDisk ? A11yPriority.High : A11yPriority.Medium, spoken.Priority);
        announced.Clear();

        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => (faults
                        ? Volatile.Read(ref attempts) >= 3
                        : changedOnDisk || interactions.ArtifactCacheSourceCurrentForTests)
                    && Idle(tab, interactions),
                TimeSpan.FromSeconds(20)),
            "the load never ended");
        Settle();

        if (faults)
        {
            A11yEvent.HostComposed followUp = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(announced));
            Assert.Equal(RefreshFailed, followUp.Text);
            Assert.Equal(A11yPriority.High, followUp.Priority);
        }
        else
        {
            Assert.Empty(announced.Select(Render));
        }
    });

    // ---- The shipped Editor menu (hosted) -------------------------------------

    /// <summary>The Editor menu's two items follow the mode at the menu's next
    /// open, on the way into reading mode and on the way back, and when the
    /// mode is switched by the menu's own Toggle Reading Mode item. A child of
    /// a closed submenu reads enabled whatever its command says, so the items
    /// are read with the menu open.</summary>
    [Fact]
    public void TheEditorMenuItemsFollowTheModeAcrossOpens() => RunSta(() =>
    {
        using var host = new ShownShell(("source.md", SourceText), ("target.md", TargetText));
        host.Workspace.OpenPath("source.md");
        host.Settle();
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        var menu = new EditorMenu(host);

        menu.Open();
        Assert.True(menu.Activate.IsEnabled && menu.Preview.IsEnabled, "premise: both items are available in the editor");
        menu.Close();

        host.Workspace.SetViewMode(tab, reading: true, landFocus: false);
        host.Settle();
        menu.Open();
        Assert.False(menu.Activate.IsEnabled, "Activate at Cursor reads available in reading mode");
        Assert.False(menu.Preview.IsEnabled, "Preview Embed reads available in reading mode");
        menu.Close();

        host.Workspace.SetViewMode(tab, reading: false, landFocus: false);
        host.Settle();
        menu.Open();
        Assert.True(menu.Activate.IsEnabled, "Activate at Cursor stayed unavailable back in the editor");
        Assert.True(menu.Preview.IsEnabled, "Preview Embed stayed unavailable back in the editor");

        // The mode switched by the menu's own item, with the menu open.
        var invoke = (System.Windows.Automation.Provider.IInvokeProvider)
            new System.Windows.Automation.Peers.MenuItemAutomationPeer(menu.Item("EditorToggleReadingModeMenuItem"))
                .GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke);
        invoke.Invoke();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => tab.IsReadingMode, TimeSpan.FromSeconds(10)),
            "the menu's Toggle Reading Mode item did not switch the mode");
        host.Settle();
        menu.Open();
        Assert.False(menu.Activate.IsEnabled, "Activate at Cursor reads available after the menu's own switch");
        Assert.False(menu.Preview.IsEnabled, "Preview Embed reads available after the menu's own switch");
        menu.Close();
    });

    /// <summary>WPF runs an item at once on its access key when it is the only
    /// enabled item of its level that carries the key. With Activate at Cursor
    /// unavailable, its key A must reach no other command: before Actual Size
    /// had a key of its own, A reset the reader's text size.</summary>
    [Fact]
    public void TheAccessKeyARunsNothingElseInReadingMode() => RunSta(() =>
    {
        using var host = new ShownShell(("source.md", SourceText), ("target.md", TargetText));
        host.Workspace.OpenPath("source.md");
        host.Settle();
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        var menu = new EditorMenu(host);
        // A text size Actual Size would change, so its run could not be missed.
        host.Workspace.EditorPreferences.ZoomInCommand.Execute(null);
        double size = host.Workspace.EditorPreferences.FontSize;
        Assert.NotEqual(EditorPreferencesViewModel.ActualFontSize, size);
        host.Workspace.SetViewMode(tab, reading: true, landFocus: false);
        host.Settle();
        int ran = 0;
        menu.Item("EditorActualSizeMenuItem").Click += (_, _) => ran++;
        menu.OpenWithTheKeysOnFind();
        host.Announced.Clear();

        menu.PressAccessKey("a");

        Assert.Equal(0, ran);
        Assert.Equal(size, host.Workspace.EditorPreferences.FontSize);
        Assert.Empty(host.Announced.Select(Render));

        // The fact's own premise: a letter typed this way, in this same open
        // menu, does reach the menu's access keys.
        menu.PressAccessKey("z");

        Assert.Equal(1, ran);
        Assert.Equal(EditorPreferencesViewModel.ActualFontSize, host.Workspace.EditorPreferences.FontSize);
    });

    /// <summary>Actual Size answers to a key of its own, Z, in the editor as
    /// in the reading view.</summary>
    [Fact]
    public void ActualSizeAnswersToItsOwnAccessKey() => RunSta(() =>
    {
        using var host = new ShownShell(("source.md", SourceText), ("target.md", TargetText));
        host.Workspace.OpenPath("source.md");
        host.Settle();
        var menu = new EditorMenu(host);
        host.Workspace.EditorPreferences.ZoomInCommand.Execute(null);
        Assert.NotEqual(EditorPreferencesViewModel.ActualFontSize, host.Workspace.EditorPreferences.FontSize);
        int ran = 0;
        menu.Item("EditorActualSizeMenuItem").Click += (_, _) => ran++;
        menu.OpenWithTheKeysOnFind();

        menu.PressAccessKey("z");

        Assert.Equal(1, ran);
        Assert.Equal(EditorPreferencesViewModel.ActualFontSize, host.Workspace.EditorPreferences.FontSize);
    });

    // ---- Support ----------------------------------------------------------------

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "the editor surface command fact timed out.");

    /// <summary>The tab's coordinator, with its math ranges current: every
    /// fact here starts past the math check, at the entry it is about.</summary>
    private static EditorInteractionCoordinator Current(WorkspaceTabViewModel tab, bool refresh = true)
    {
        EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(tab.EditorInteractions);
        if (refresh)
        {
            interactions.RefreshMathRangesForTests();
        }

        return interactions;
    }

    /// <summary>One entry called at one target. The answer is null for a
    /// command, which returns none.</summary>
    private static bool? Call(string entry, string target, WorkspaceTabViewModel tab, EditorInteractionCoordinator interactions)
    {
        int offset = Offset(entry == "ActivateAt by the pointer" && target == "task" ? "checkbox" : target);
        tab.EditorCaretOffset = offset;
        switch (entry)
        {
            case "ActivateAtCaretCommand":
                interactions.ActivateAtCaretCommand.Execute(null);
                return null;
            case "PreviewEmbedCommand":
                interactions.PreviewEmbedCommand.Execute(null);
                return null;
            case "ActivateAt by the key":
                return interactions.ActivateAt(offset, EditorInteractionOrigin.Keyboard);
            case "ActivateAt by the pointer":
                return interactions.ActivateAt(offset, EditorInteractionOrigin.Pointer);
            case "PreviewEmbedAt":
                return interactions.PreviewEmbedAt(offset);
            case "ActivateSpan":
                EditorSemanticSpan span = tab.EditorSession!
                    .InspectInRange(0, tab.EditorDocument!.TextLength)
                    .Spans.First(candidate => target == "embed"
                        ? candidate.Kind is EditorSpanKind.Embed
                        : candidate.Kind is EditorSpanKind.Wikilink);
                return interactions.ActivateSpan(span);
            default:
                throw new ArgumentOutOfRangeException(nameof(entry), entry, null);
        }
    }

    /// <summary>What a guarded entry answers. ActivateAt and PreviewEmbedAt
    /// take the press, so a key that raced the switch is not handed on to the
    /// hidden editor as a line break; ActivateSpan answers as it does for a
    /// span that is gone.</summary>
    private static bool? ExpectedAnswer(string entry) => entry switch
    {
        "ActivateAtCaretCommand" or "PreviewEmbedCommand" => null,
        "ActivateSpan" => false,
        _ => true,
    };

    private static int Offset(string target) => target switch
    {
        "prose" => At("Plain prose", 2),
        "task" => At("- [ ] buy milk", 8),
        // Inside the brackets: a pointer's task is the one whose checkbox holds the click.
        "checkbox" => At("- [ ] buy milk", 3),
        "wikilink" => At("\n[[target#Destination]]", 4),
        "embed" => At("![[target#Destination]]", 4),
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
    };

    private static int At(string needle, int into)
    {
        int start = SourceText.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0, $"the fixture's note lacks: {needle}");
        return start + into;
    }

    /// <summary>Nothing of the tab's is running: no task toggle in flight, no
    /// worker of the coordinator, no load between its start and its
    /// publication.</summary>
    private static bool Idle(WorkspaceTabViewModel tab, EditorInteractionCoordinator interactions) =>
        !tab.IsTaskToggleInFlight && interactions.LiveWorkersForTests == 0 && interactions.IsBackgroundIdle;

    /// <summary>Wait for everything an entry may have started to end, and run
    /// what it queued to this thread.</summary>
    private static void Quiesce(WorkspaceTabViewModel tab, EditorInteractionCoordinator interactions)
    {
        do
        {
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Idle(tab, interactions), TimeSpan.FromSeconds(20)),
                "the tab's background work never ended");
            Settle();
        }
        while (!Idle(tab, interactions));
    }

    private static void Settle()
    {
        for (int turn = 0; turn < 5; turn++)
        {
            PumpedDispatcher.Drain();
        }
    }

    private static Func<EditorInteractionWorkerKind, Exception?> TerminalArtifactFault(Action attempted) =>
        kind =>
        {
            if (kind is not EditorInteractionWorkerKind.Artifact)
            {
                return null;
            }

            attempted();
            return new IOException("Injected terminal artifact fault.");
        };

    /// <summary>The file's text once no writer holds it.</summary>
    private static string StableText(string path)
    {
        string? text = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () =>
                {
                    try
                    {
                        text = File.ReadAllText(path);
                        return true;
                    }
                    catch (IOException)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            "the note stayed held by a writer");
        return text!;
    }

    private static string Render(A11yEvent item)
    {
        RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(item);
        return $"{item.GetType().Name} ({rendered.Priority}): {rendered.Text}";
    }

    private static string Describe(IInputElement? element) => element switch
    {
        null => "nothing",
        FrameworkElement framework => $"{framework.GetType().Name} '{AutomationProperties.GetAutomationId(framework)}'",
        _ => element.GetType().Name,
    };

    /// <summary>A vault on disk and a bare session over it, with a Markdown
    /// tab on its one note that records what it announces and where it asks
    /// to navigate, and starts no background work of its own.</summary>
    private sealed class Vault : IDisposable
    {
        private readonly CancelToken _cancel = new();

        public Vault()
        {
            Root = Path.Combine(Path.GetTempPath(), $"slate-editor-surface-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(SourcePath, SourceText);
            File.WriteAllText(Path.Combine(Root, "target.md"), TargetText);
            Session = VaultSession.OpenFilesystem(Root);
            Session.ScanInitial(_cancel);
        }

        public string Root { get; }
        public string SourcePath => Path.Combine(Root, "source.md");
        public VaultSession Session { get; }

        /// <param name="artifactCacheCurrent">False leaves the note's links
        /// and tasks never loaded, as they are just after a note opens.</param>
        public WorkspaceTabViewModel Tab(
            List<A11yEvent>? announced = null,
            List<EditorNavigationRequest>? navigated = null,
            bool artifactCacheCurrent = true,
            Func<EditorInteractionWorkerKind, Exception?>? fault = null)
        {
            var tab = new WorkspaceTabViewModel(
                Session,
                new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
                navigate: request => navigated?.Add(request),
                activateTag: _ => { },
                announce: item => announced?.Add(item),
                startInteractionBackgroundWork: false,
                interactionBackgroundFaultForTests: fault);
            if (artifactCacheCurrent)
            {
                tab.EditorInteractions!.RefreshMathRangesForTests();
                tab.EditorInteractions.RefreshArtifactCacheForTests();
            }

            return tab;
        }

        /// <summary>The note written by another program and seen by the index,
        /// with the tab not reloaded: its cache matches the tab and its source
        /// is no longer current. Answers the text now on disk.</summary>
        public string ChangeTheNoteOnDisk(EditorInteractionCoordinator interactions)
        {
            string outside = SourceText + "\nA line written outside Slate.\n";
            File.WriteAllText(SourcePath, outside);
            Session.ReindexPath("source.md");
            interactions.RefreshArtifactCacheForTests();
            Assert.False(interactions.ArtifactCacheSourceCurrentForTests, "premise: the cache's source is not current");
            return outside;
        }

        /// <summary>Another note written and seen by the index: the session
        /// moves on, and nothing tells the tab on this note, whose published
        /// links and tasks stay as they were.</summary>
        public void ChangeAnotherNote()
        {
            ulong before = Session.InteractionGeneration();
            File.WriteAllText(Path.Combine(Root, "target.md"), TargetText + "\nA line added to the other note.\n");
            Session.ReindexPath("target.md");
            Assert.NotEqual(before, Session.InteractionGeneration());
        }

        public void Dispose()
        {
            Session.Dispose();
            _cancel.Dispose();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>A real vault lifecycle and its workspace over a vault on disk,
    /// under the dispatcher's context, so its command table, its palette and
    /// its tabs are the app's.</summary>
    private sealed class LifecycleHost : IDisposable
    {
        private readonly SynchronizationContext? _previous;
        private readonly string _state;

        public LifecycleHost()
        {
            _previous = SynchronizationContext.Current;
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            Root = Path.Combine(Path.GetTempPath(), $"slate-editor-surface-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "source.md"), SourceText);
            File.WriteAllText(Path.Combine(Root, "target.md"), TargetText);
            File.WriteAllText(Path.Combine(Root, "other.md"), OtherText);
            _state = Path.Combine(Path.GetTempPath(), $"slate-editor-surface-state-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_state);
            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => dispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(_state, "recent-vaults.json")),
                announce: Announced.Add,
                paletteRecentsStore: new CommandPaletteRecentsStore(Path.Combine(_state, "command-palette-recents.json")));
            Task open = Lifecycle.OpenVaultAsync(Root);
            Assert.True(PumpedDispatcher.PumpUntil(() => open.IsCompleted, TimeSpan.FromSeconds(60)), "the vault never opened");
            open.GetAwaiter().GetResult();
            Workspace = Assert.IsType<WorkspaceViewModel>(Lifecycle.Workspace);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => Lifecycle.FileSidebar is { } sidebar
                        && sidebar.TreeRefreshCompletion.IsCompleted
                        && !sidebar.IsLoadingChildren
                        && !sidebar.IsExpandingLoaded
                        && !Lifecycle.IsBusy,
                    TimeSpan.FromSeconds(30)),
                "the sidebar never settled");
            PumpedDispatcher.Drain();
            Announced.Clear();
        }

        public string Root { get; }
        public VaultLifecycleViewModel Lifecycle { get; }
        public WorkspaceViewModel Workspace { get; }
        public List<A11yEvent> Announced { get; } = [];

        /// <summary>source.md opened in the editor, its interaction caches loaded.</summary>
        public WorkspaceTabViewModel OpenSource()
        {
            Workspace.OpenPath("source.md");
            WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(Workspace.ActiveGroup.ActiveTab);
            EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(tab.EditorInteractions);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => interactions.MathRangesCurrentForTests
                        && interactions.ArtifactCacheSourceCurrentForTests
                        && interactions.IsBackgroundIdle,
                    TimeSpan.FromSeconds(30)),
                "the note's interaction caches never loaded");
            PumpedDispatcher.Drain();
            Announced.Clear();
            return tab;
        }

        public void Dispose()
        {
            try
            {
                Lifecycle.Dispose();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(_previous);
                foreach (string folder in new[] { Root, _state })
                {
                    try
                    {
                        Directory.Delete(folder, recursive: true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }
    }

    /// <summary>The shipped window's Editor menu, a submenu of the logo's app
    /// menu since DS-13: opened in that order, and read only while its items
    /// are on screen. A desktop whose session has no foreground window (the
    /// hosted runners, #1680) opens no menu popup; there the fact says it
    /// measured nothing.</summary>
    private sealed class EditorMenu
    {
        private readonly ShownShell _host;
        private readonly MenuItem _app;
        private readonly MenuItem _editor;

        public EditorMenu(ShownShell host)
        {
            _host = host;
            _app = host.Shell.MainMenu.Items.OfType<MenuItem>()
                .Single(item => AutomationProperties.GetAutomationId(item) == "AppMenu");
            _editor = _app.Items.OfType<MenuItem>()
                .Single(item => AutomationProperties.GetAutomationId(item) == "EditorMenu");
            Activate = Item("EditorActivateMenuItem");
            Preview = Item("EditorPreviewEmbedMenuItem");
        }

        public MenuItem Activate { get; }
        public MenuItem Preview { get; }

        public MenuItem Item(string automationId) => _editor.Items.OfType<MenuItem>()
            .Single(item => AutomationProperties.GetAutomationId(item) == automationId);

        public void Open()
        {
            _app.IsSubmenuOpen = true;
            _host.Settle();
            _editor.IsSubmenuOpen = true;
            _host.Settle();
            if (!_editor.IsSubmenuOpen || !Activate.IsVisible || !Preview.IsVisible)
            {
                SkipWhereTheDesktopRefusesTheMenu("the Editor menu's items are not on screen");
            }
        }

        public void Close()
        {
            _editor.IsSubmenuOpen = false;
            _app.IsSubmenuOpen = false;
            _host.Settle();
        }

        /// <summary>The menu open with the keys on its first item, as Alt+E
        /// leaves them.</summary>
        public void OpenWithTheKeysOnFind()
        {
            _host.Shell.Activate();
            _host.Settle();
            Open();
            MenuItem find = Item("EditorFindInNoteMenuItem");
            if (!find.Focus() || !ReferenceEquals(find, Keyboard.FocusedElement))
            {
                SkipWhereTheDesktopRefusesTheMenu($"the keys stayed on {Describe(Keyboard.FocusedElement)}");
            }

            _host.Settle();
        }

        /// <summary>A letter typed with the menu open, as the text the keyboard
        /// would deliver: the route WPF's access keys listen on.</summary>
        public void PressAccessKey(string letter)
        {
            _ = TextCompositionManager.StartComposition(
                new TextComposition(InputManager.Current, Keyboard.FocusedElement, letter));
            _host.Settle();
        }

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void SkipWhereTheDesktopRefusesTheMenu(string what)
        {
            Assert.Skip($"this desktop refused the menu (#1680): {what}, so the Editor menu was not measured on it");
            throw new System.Diagnostics.UnreachableException("Assert.Skip returned instead of ending the fact");
        }
    }
}
