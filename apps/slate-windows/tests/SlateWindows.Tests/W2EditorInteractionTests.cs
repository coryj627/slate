// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using System.Xml.Linq;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W2EditorInteractionTests
{
    [Fact]
    public void DiscreteInspection_DoesNotReplacePaintedSemanticWindow()
    {
        using var session = new AvalonDocumentBufferSession(
            "# Heading\n\n[[target]] and #tag\n",
            _ => { });
        EditorHighlightWindow painted = session.HighlightInRange(0, 12);

        EditorHighlightWindow inspected = session.InspectInRange(
            0,
            session.Document.TextLength);

        Assert.NotSame(painted, inspected);
        Assert.Same(painted, session.LatestHighlightWindow);
        Assert.Contains(inspected.Spans, span => span.Kind is EditorSpanKind.Wikilink);
    }

    [Fact]
    public void CoreBackedActions_CoverLinksTagsCitationsEmbedsTasksAndProtectedRegions()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var navigation = new List<EditorNavigationRequest>();
        var tags = new List<string>();
        var announcements = new List<A11yEvent>();
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            navigate: navigation.Add,
            activateTag: tags.Add,
            announce: announcements.Add,
            startInteractionBackgroundWork: false);
        EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(
            tab.EditorInteractions);
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "[[target#Destination]]")));
        EditorNavigationRequest heading = Assert.Single(navigation);
        Assert.Equal("target.md", heading.Path);
        Assert.Equal("heading", heading.Anchor?.Kind);
        Assert.Null(heading.ResolvedAnchorText);

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "#project")));
        Assert.Equal(["project"], tags);

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "[@doe]")));
        Assert.True(interactions.IsPopoverOpen);
        Assert.StartsWith("Citation", interactions.PopoverAutomationName);
        Assert.Contains("doe", interactions.PopoverBody, StringComparison.OrdinalIgnoreCase);
        interactions.ClosePopoverCommand.Execute(null);

        Assert.True(interactions.PreviewEmbedAt(Inside(tab.Text, "![[target#Destination]]")));
        WaitForUi(() => !interactions.PopoverTitle.StartsWith(
            "Loading",
            StringComparison.Ordinal));
        Assert.True(interactions.IsPopoverOpen);
        Assert.Contains("Destination", interactions.PopoverTitle);
        Assert.Contains("Section body", EmbedText(interactions.PopoverEmbedRoot!));
        Assert.Equal("target.md", interactions.PopoverSourcePath);
        interactions.ClosePopoverCommand.Execute(null);

        int tagCount = tags.Count;
        Assert.False(interactions.ActivateAt(Inside(tab.Text, "#not-a-tag")));
        Assert.False(interactions.ActivateAt(Inside(tab.Text, "#not-math")));
        Assert.Equal(tagCount, tags.Count);

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "- [ ] task")));
        WaitForUi(() => tab.Text.Contains("- [x] task", StringComparison.Ordinal));
        Assert.Contains("- [x] task", tab.Text, StringComparison.Ordinal);
        Assert.Contains("- [x] task", File.ReadAllText(fixture.SourcePath), StringComparison.Ordinal);
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void ClosingPopover_DefersEditorFocusAndDropsStaleRequests()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            startInteractionBackgroundWork: false);
        EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(
            tab.EditorInteractions);
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();
        int citation = Inside(tab.Text, "[@doe]");
        int focusRequests = 0;
        interactions.FocusRequested += (_, _) => focusRequests++;

        Assert.True(interactions.ActivateAt(citation));
        Assert.True(interactions.IsPopoverOpen);
        interactions.ClosePopoverCommand.Execute(null);

        Assert.False(interactions.IsPopoverOpen);
        Assert.Equal(0, focusRequests);
        int focusRequestsDuringInput = -1;
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() => focusRequestsDuringInput = focusRequests));
        DrainUi();
        Assert.Equal(0, focusRequestsDuringInput);
        Assert.Equal(1, focusRequests);

        Assert.True(interactions.ActivateAt(citation));
        Assert.True(interactions.IsPopoverOpen);
        interactions.ClosePopoverCommand.Execute(null);
        Assert.True(interactions.ActivateAt(citation));
        Assert.True(interactions.IsPopoverOpen);
        DrainUi();
        Assert.Equal(1, focusRequests);

        interactions.ClosePopoverCommand.Execute(null);
        interactions.Dispose();
        DrainUi();
        Assert.Equal(1, focusRequests);
    }

    [Fact]
    public void DirtyTaskAndSavedRecordActions_FailClosedWithoutLosingEditorText()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announcements = new List<A11yEvent>();
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: announcements.Add,
            startInteractionBackgroundWork: false);
        EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(
            tab.EditorInteractions);
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();
        string diskBefore = File.ReadAllText(fixture.SourcePath);

        tab.Text += "\nUnsaved authority.\n";

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "- [ ] task")));
        Assert.Contains(announcements, item => item is A11yEvent.TaskToggleUnsaved);
        Assert.Equal(diskBefore, File.ReadAllText(fixture.SourcePath));
        Assert.EndsWith("Unsaved authority.\n", tab.Text, StringComparison.Ordinal);

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "[[target#Destination]]")));
        Assert.Contains(
            announcements,
            item => item is A11yEvent.HostComposed composed
                && composed.Text.Contains("Save source.md", StringComparison.Ordinal));
    }

    [Fact]
    public void EarlyEmbedPreview_ReplaysOnceAndDropsEditedOrDeactivatedRequests()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);

        using var active = OpenPendingPreviewTab(session);
        EditorInteractionCoordinator activeInteractions = active.EditorInteractions!;
        int activeFocusRequests = 0;
        activeInteractions.PopoverFocusRequested += (_, _) => activeFocusRequests++;

        activeInteractions.RefreshMathRangesForTests();
        Assert.True(activeInteractions.PreviewEmbedAt(0));
        Assert.False(activeInteractions.IsPopoverOpen);
        ulong generationBefore = session.InteractionGeneration();
        _ = session.ToggleTaskStatus("unrelated.md", 0, "x", null);
        Assert.NotEqual(generationBefore, session.InteractionGeneration());
        activeInteractions.InvalidateExternalState();
        activeInteractions.RefreshArtifactCacheForTests();
        Assert.True(activeInteractions.IsPopoverOpen);
        Assert.Equal(1, activeFocusRequests);
        activeInteractions.RefreshMathRangesForTests();
        activeInteractions.RefreshArtifactCacheForTests();
        Assert.Equal(1, activeFocusRequests);
        activeInteractions.ClosePopoverCommand.Execute(null);

        using var deactivated = OpenPendingPreviewTab(session);
        EditorInteractionCoordinator deactivatedInteractions = deactivated.EditorInteractions!;
        int deactivatedFocusRequests = 0;
        deactivatedInteractions.PopoverFocusRequested += (_, _) =>
            deactivatedFocusRequests++;
        Assert.True(deactivatedInteractions.PreviewEmbedAt(0));
        deactivated.Deactivate();
        deactivatedInteractions.RefreshMathRangesForTests();
        deactivatedInteractions.RefreshArtifactCacheForTests();
        Assert.False(deactivatedInteractions.IsPopoverOpen);
        Assert.Equal(0, deactivatedFocusRequests);

        using var edited = OpenPendingPreviewTab(session);
        EditorInteractionCoordinator editedInteractions = edited.EditorInteractions!;
        int editedFocusRequests = 0;
        editedInteractions.PopoverFocusRequested += (_, _) => editedFocusRequests++;
        Assert.True(editedInteractions.PreviewEmbedAt(0));
        edited.Text += "\nStale pending request.\n";
        editedInteractions.RefreshMathRangesForTests();
        editedInteractions.RefreshArtifactCacheForTests();
        Assert.False(editedInteractions.IsPopoverOpen);
        Assert.Equal(0, editedFocusRequests);
    }

    [Fact]
    public void ProductionBackgroundCache_ReplaysImmediateOffsetZeroEmbedPreview()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")));
        EditorInteractionCoordinator interactions = Assert.IsType<EditorInteractionCoordinator>(
            tab.EditorInteractions);
        int focusRequests = 0;
        interactions.PopoverFocusRequested += (_, _) => focusRequests++;

        Assert.True(interactions.PreviewEmbedAt(0));

        WaitForUi(() => interactions.IsPopoverOpen);
        Assert.Equal(1, focusRequests);
    }

    [Fact]
    public void BackgroundWorkers_RetryTransientFaultsAndRecover()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n[@doe]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var attempts = new int[3];
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            interactionBackgroundFaultForTests: kind =>
                Interlocked.Increment(ref attempts[(int)kind]) <= 2
                    ? new IOException("Injected transient worker fault.")
                    : null);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;

        Assert.True(interactions.PreviewEmbedAt(0));
        WaitForUi(() => interactions.IsPopoverOpen);
        interactions.ClosePopoverCommand.Execute(null);
        int citation = Inside(tab.Text, "[@doe]");
        WaitForUi(() =>
        {
            _ = interactions.ActivateAt(citation);
            return interactions.IsPopoverOpen;
        });

        Assert.StartsWith("Citation", interactions.PopoverAutomationName);
        Assert.All(attempts, count => Assert.InRange(count, 3, 6));
    }

    [Fact]
    public void BackgroundWorkers_BoundTerminalFaultsAndAnnounceFailures()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n[@doe]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);

        var mathAnnouncements = new List<A11yEvent>();
        int mathAttempts = 0;
        using (var mathTab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: mathAnnouncements.Add,
            startInteractionBackgroundWork: false,
            interactionBackgroundFaultForTests: kind =>
                kind is EditorInteractionWorkerKind.Math
                    ? InjectTerminalFault(ref mathAttempts)
                    : null))
        {
            mathTab.EditorInteractions!.RefreshArtifactCacheForTests();
            Assert.True(mathTab.EditorInteractions.PreviewEmbedAt(0));
            WaitForUi(() => ContainsAnnouncement(
                mathAnnouncements,
                "Editor interaction classification could not be refreshed; try again."));
            Assert.Equal(3, mathAttempts);
            Assert.False(mathTab.EditorInteractions.IsPopoverOpen);
        }

        var artifactAnnouncements = new List<A11yEvent>();
        int artifactAttempts = 0;
        using (var artifactTab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: artifactAnnouncements.Add,
            startInteractionBackgroundWork: false,
            interactionBackgroundFaultForTests: kind =>
                kind is EditorInteractionWorkerKind.Artifact
                    ? InjectTerminalFault(ref artifactAttempts)
                    : null))
        {
            artifactTab.EditorInteractions!.RefreshMathRangesForTests();
            Assert.True(artifactTab.EditorInteractions.PreviewEmbedAt(0));
            WaitForUi(() => ContainsAnnouncement(
                artifactAnnouncements,
                "Editor interaction data could not be refreshed; try again."));
            Assert.Equal(3, artifactAttempts);
            Assert.False(artifactTab.EditorInteractions.IsPopoverOpen);
        }

        var citationAnnouncements = new List<A11yEvent>();
        int citationAttempts = 0;
        using var citationTab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: citationAnnouncements.Add,
            startInteractionBackgroundWork: false,
            interactionBackgroundFaultForTests: kind =>
                kind is EditorInteractionWorkerKind.Citation
                    ? InjectTerminalFault(ref citationAttempts)
                    : null);
        citationTab.EditorInteractions!.RefreshMathRangesForTests();
        Assert.True(citationTab.EditorInteractions.ActivateAt(
            Inside(citationTab.Text, "[@doe]")));
        WaitForUi(() => ContainsAnnouncement(
            citationAnnouncements,
            "Citation data could not be refreshed; try again."));
        Assert.Equal(3, citationAttempts);
        Assert.False(citationTab.EditorInteractions.IsPopoverOpen);
    }
    [Fact]
    public void StaleMathFailure_DoesNotCancelNewerSameRevisionPreview()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announcements = new List<A11yEvent>();
        using var thirdFailure = new ManualResetEventSlim();
        int attempts = 0;
        int fail = 1;
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: announcements.Add,
            startInteractionBackgroundWork: false,
            interactionBackgroundFaultForTests: kind =>
            {
                if (kind is not EditorInteractionWorkerKind.Math
                    || Volatile.Read(ref fail) == 0)
                {
                    Interlocked.Increment(ref attempts);
                    return null;
                }

                int attempt = Interlocked.Increment(ref attempts);
                if (attempt == 3)
                {
                    thirdFailure.Set();
                }
                return new IOException("Injected stale math failure.");
            });
        tab.EditorInteractions!.RefreshArtifactCacheForTests();

        Assert.True(tab.EditorInteractions.PreviewEmbedAt(0));
        Assert.True(thirdFailure.Wait(TimeSpan.FromSeconds(5)));
        Volatile.Write(ref fail, 0);
        Assert.True(tab.EditorInteractions.PreviewEmbedAt(0));

        WaitForUi(() => tab.EditorInteractions.IsPopoverOpen);
        Assert.Equal(4, attempts);
        Assert.False(ContainsAnnouncement(
            announcements,
            "Editor interaction classification could not be refreshed; try again."));
    }

    [Fact]
    public void Deactivation_DropsDelayedCitationAndTerminalFailureState()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n[@doe]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);

        using var citationStarted = new ManualResetEventSlim();
        using var releaseCitation = new ManualResetEventSlim();
        var hoverAnnouncements = new List<A11yEvent>();
        using (var hoverTab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: hoverAnnouncements.Add,
            startInteractionBackgroundWork: false,
            interactionBackgroundFaultForTests: kind =>
            {
                if (kind is EditorInteractionWorkerKind.Citation)
                {
                    citationStarted.Set();
                    releaseCitation.Wait();
                }
                return null;
            }))
        {
            hoverTab.EditorInteractions!.HoverAt(Inside(hoverTab.Text, "[@doe]"));
            Assert.True(citationStarted.Wait(TimeSpan.FromSeconds(5)));
            WaitForUi(() =>
                hoverTab.EditorInteractions.HasPendingCitationInteractionForTests);
            hoverTab.Deactivate();
            Assert.False(
                hoverTab.EditorInteractions.HasPendingCitationInteractionForTests);
            releaseCitation.Set();
            WaitForUi(() =>
                hoverTab.EditorInteractions.CitationCacheLoadCountForTests == 1);
            Assert.False(hoverTab.EditorInteractions.IsPopoverOpen);
            Assert.Empty(hoverAnnouncements);
        }

        using var artifactStarted = new ManualResetEventSlim();
        using var releaseArtifact = new ManualResetEventSlim();
        var terminalAnnouncements = new List<A11yEvent>();
        int artifactAttempts = 0;
        using var terminalTab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            announce: terminalAnnouncements.Add,
            startInteractionBackgroundWork: false,
            interactionBackgroundFaultForTests: kind =>
            {
                if (kind is not EditorInteractionWorkerKind.Artifact)
                {
                    return null;
                }
                Interlocked.Increment(ref artifactAttempts);
                artifactStarted.Set();
                releaseArtifact.Wait();
                return new IOException("Injected delayed terminal artifact failure.");
            });
        terminalTab.EditorInteractions!.RefreshMathRangesForTests();
        Assert.True(terminalTab.EditorInteractions.PreviewEmbedAt(0));
        Assert.True(artifactStarted.Wait(TimeSpan.FromSeconds(5)));
        terminalTab.Deactivate();
        releaseArtifact.Set();
        WaitForUi(() =>
            artifactAttempts == 3
            && !terminalTab.EditorInteractions.ArtifactCacheLoadingForTests);
        Assert.False(terminalTab.EditorInteractions.IsPopoverOpen);
        Assert.False(ContainsAnnouncement(
            terminalAnnouncements,
            "Editor interaction data could not be refreshed; try again."));
    }

    [Fact]
    public void Disposal_StopsAllWorkerRetryLoopsWithoutTerminalAnnouncement()
    {
        using InteractionFixture fixture = InteractionFixture.Create(
            "![[target#Destination]]\n[@doe]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);

        foreach (EditorInteractionWorkerKind kind in
            Enum.GetValues<EditorInteractionWorkerKind>())
        {
            using var started = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int attempts = 0;
            var announcements = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
                announce: announcements.Add,
                startInteractionBackgroundWork: false,
                interactionBackgroundFaultForTests: candidate =>
                {
                    if (candidate != kind)
                    {
                        return null;
                    }
                    Interlocked.Increment(ref attempts);
                    started.Set();
                    release.Wait();
                    return new IOException("Injected disposal worker fault.");
                });
            switch (kind)
            {
                case EditorInteractionWorkerKind.Math:
                    tab.EditorInteractions!.RefreshArtifactCacheForTests();
                    Assert.True(tab.EditorInteractions.PreviewEmbedAt(0));
                    break;
                case EditorInteractionWorkerKind.Artifact:
                    tab.EditorInteractions!.RefreshMathRangesForTests();
                    Assert.True(tab.EditorInteractions.PreviewEmbedAt(0));
                    break;
                case EditorInteractionWorkerKind.Citation:
                    tab.EditorInteractions!.RefreshMathRangesForTests();
                    Assert.True(tab.EditorInteractions.ActivateAt(
                        Inside(tab.Text, "[@doe]")));
                    break;
            }

            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            tab.Dispose();
            release.Set();
            Thread.Sleep(350);
            Assert.Equal(1, attempts);
            Assert.DoesNotContain(
                announcements,
                item => item is A11yEvent.HostComposed composed
                    && composed.Priority is A11yPriority.High);
        }
    }
    [Fact]
    public void WorkspaceNavigation_UsesCoreHeadingAndBlockArtifactsToParkCaret()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var announcements = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("source.md");
        WorkspaceTabViewModel source = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();

        Assert.True(source.EditorInteractions!.ActivateAt(
            Inside(source.Text, "[[target^block-id]]")));

        WorkspaceTabViewModel target = Assert.IsType<WorkspaceTabViewModel>(
            workspace.ActiveGroup.ActiveTab);
        Assert.Equal("target.md", target.Path);
        int expected = target.Text.IndexOf("Block body", StringComparison.Ordinal);
        WaitForUi(() => target.EditorCaretOffset == expected);
        Assert.Contains(announcements, item => item is A11yEvent.InternalNavigated);
    }

    [Fact]
    public void EditorPreferences_ExposeAllFourMatrixCommandsWithBounds()
    {
        var announcements = new List<A11yEvent>();
        using var preferences = new EditorPreferencesViewModel(
            announcements.Add,
            new FakeEditorSpellingService());

        preferences.ZoomInCommand.Execute(null);
        Assert.Equal(EditorPreferencesViewModel.ActualFontSize + 1, preferences.FontSize);
        preferences.ActualSizeCommand.Execute(null);
        Assert.Equal(EditorPreferencesViewModel.ActualFontSize, preferences.FontSize);
        preferences.ZoomOutCommand.Execute(null);
        Assert.Equal(EditorPreferencesViewModel.ActualFontSize - 1, preferences.FontSize);
        Assert.False(preferences.IsSpellCheckEnabled);
        preferences.ToggleSpellCheckCommand.Execute(null);
        Assert.True(preferences.IsSpellCheckEnabled);
        Assert.Contains(
            announcements,
            item => item is A11yEvent.SpellCheckToggled toggled && toggled.Enabled);
    }

    [Fact]
    public void EditorXaml_PinsKeyboardContextMenuPopoverAndMatrixCommandHomes()
    {
        string templates = File.ReadAllText(RepoFile(
            "apps",
            "slate-windows",
            "src",
            "SlateWindows",
            "WorkspaceTemplates.xaml"));
        string main = File.ReadAllText(RepoFile(
            "apps",
            "slate-windows",
            "src",
            "SlateWindows",
            "MainWindow.xaml"));

        foreach (string required in new[]
        {
            "InteractionSession=\"{Binding EditorInteractions}\"",
            "AutomationProperties.AutomationId=\"MarkdownEditor\"",
            "EditorActivateAtCursor",
            "EditorPreviewEmbed",
            "EditorInteractionPopover",
            "EditorPopoverOpenSource",
            "EditorPopoverClose",
        })
        {
            Assert.Contains(required, templates, StringComparison.Ordinal);
        }

        XDocument templateDocument = XDocument.Parse(templates);
        XElement interactionPopover = Assert.Single(
            templateDocument.Descendants(),
            element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "AutomationProperties.AutomationId"
                && attribute.Value == "EditorInteractionPopover"));
        Assert.DoesNotContain(
            interactionPopover.Ancestors(),
            ancestor => ancestor.Name.LocalName == "Popup");
        Assert.Equal("AutomationLandmarkGrid", interactionPopover.Name.LocalName);
        Assert.Contains(
            interactionPopover.Attributes(),
            attribute => attribute.Name.LocalName == "Panel.ZIndex"
                && attribute.Value == "100");
        XElement popoverOpenSource = Assert.Single(
            interactionPopover.Descendants(),
            element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "AutomationProperties.AutomationId"
                && attribute.Value == "EditorPopoverOpenSource"));
        XElement popoverClose = Assert.Single(
            interactionPopover.Descendants(),
            element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "AutomationProperties.AutomationId"
                && attribute.Value == "EditorPopoverClose"));
        XElement popoverScroller = Assert.Single(
            interactionPopover.Descendants(),
            element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "AutomationProperties.Name"
                && attribute.Value == "Scrollable embed preview"));
        Assert.Contains(
            popoverClose.Attributes(),
            attribute => attribute.Name.LocalName == "KeyboardNavigation.TabIndex"
                && attribute.Value == "0");
        Assert.Contains(
            popoverOpenSource.Attributes(),
            attribute => attribute.Name.LocalName == "KeyboardNavigation.TabIndex"
                && attribute.Value == "1");
        Assert.Contains(
            popoverScroller.Attributes(),
            attribute => attribute.Name.LocalName == "KeyboardNavigation.TabIndex"
                && attribute.Value == "2");

        foreach (string required in new[]
        {
            "EditorActivateMenuItem",
            "EditorPreviewEmbedMenuItem",
            "EditorToggleSpellCheckMenuItem",
            "EditorZoomInMenuItem",
            "EditorZoomOutMenuItem",
            "EditorActualSizeMenuItem",
        })
        {
            Assert.Contains(required, main, StringComparison.Ordinal);
        }
    }

    private static int Inside(string text, string needle)
    {
        int start = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Fixture token is missing: {needle}");
        return start + Math.Min(2, needle.Length - 1);
    }

    private static WorkspaceTabViewModel OpenPendingPreviewTab(VaultSession session) =>
        new(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            startInteractionBackgroundWork: false);

    private static Exception InjectTerminalFault(ref int attempts)
    {
        Interlocked.Increment(ref attempts);
        return new IOException("Injected terminal worker fault.");
    }

    private static bool ContainsAnnouncement(
        IReadOnlyList<A11yEvent> announcements,
        string text) =>
        announcements.Any(item => item is A11yEvent.HostComposed composed
            && string.Equals(composed.Text, text, StringComparison.Ordinal));
    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous editor action timed out.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }
    private static void DrainUi()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    private static string EmbedText(EditorEmbedPreviewNode node) =>
        string.Concat(node.Parts.Select(part =>
            part.Text ?? (part.Nested is null ? string.Empty : EmbedText(part.Nested))));
    private static string RepoFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, .. parts]);
    }

    private sealed class InteractionFixture : IDisposable
    {
        private InteractionFixture(string root)
        {
            Root = root;
            SourcePath = Path.Combine(root, "source.md");
        }

        public string Root { get; }
        public string SourcePath { get; }

        public static InteractionFixture Create(string? sourceText = null)
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                $"slate-w2-interactions-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(
                Path.Combine(root, "target.md"),
                "# Lead\n\n## Destination\n\nSection body.\n\nBlock body ^block-id\n");
            File.WriteAllText(
                Path.Combine(root, "source.md"),
                sourceText
                    ?? """
                # Source

                [[target#Destination]]
                [[target^block-id]]
                #project
                [@doe]
                ![[target#Destination]]
                - [ ] task

                ```text
                #not-a-tag
                ```

                $$
                #not-math
                $$
                """);
            File.WriteAllText(
                Path.Combine(root, "unrelated.md"),
                "- [ ] unrelated task\n");
            return new InteractionFixture(root);
        }

        public void Dispose()
        {
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
}
