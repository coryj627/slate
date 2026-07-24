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

        Assert.True(activeInteractions.PreviewEmbedAt(0));
        Assert.False(activeInteractions.IsPopoverOpen);
        activeInteractions.RefreshMathRangesForTests();
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
        Assert.Contains(
            interactionPopover.Ancestors(),
            ancestor => ancestor.Name.LocalName == "Grid"
                && ancestor.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Panel.ZIndex"
                    && attribute.Value == "100"));

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
