// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W2EditorInteractionRedTeamTests
{
    [Fact]
    public void ExactCoreRanges_DisambiguateCommentsRightEdgesAndCheckboxPointerHits()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var navigation = new List<EditorNavigationRequest>();
        using var tab = OpenSourceTab(session, navigation.Add);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        string link = "[[target#Destination]]";
        int linkStart = tab.Text.IndexOf(link, StringComparison.Ordinal);
        int linkEnd = linkStart + link.Length;

        Assert.True(interactions.ActivateAt(
            linkStart + 2,
            EditorInteractionOrigin.Pointer));
        Assert.Single(navigation);

        Assert.True(interactions.ActivateAt(
            linkEnd,
            EditorInteractionOrigin.Keyboard));
        Assert.Equal(2, navigation.Count);

        Assert.False(interactions.ActivateAt(
            linkEnd,
            EditorInteractionOrigin.Pointer));
        Assert.Equal(2, navigation.Count);

        int prose = tab.Text.IndexOf("task prose", StringComparison.Ordinal) + 2;
        Assert.False(interactions.ActivateAt(
            prose,
            EditorInteractionOrigin.Pointer));
        Assert.Contains("- [ ] task prose", tab.Text, StringComparison.Ordinal);

        int checkbox = tab.Text.IndexOf("[ ]", StringComparison.Ordinal) + 1;
        Assert.True(interactions.ActivateAt(
            checkbox,
            EditorInteractionOrigin.Pointer));
        WaitForUi(() => tab.Text.Contains("- [x] task prose", StringComparison.Ordinal));
        Assert.Contains("- [x] task prose", tab.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbedPreview_ExpandsNestedContentAndExplainsCorruptImages()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var tab = OpenSourceTab(session);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        string sectionEmbed = "![[target#Destination]]";
        int rightEdge = tab.Text.IndexOf(sectionEmbed, StringComparison.Ordinal)
            + sectionEmbed.Length;

        Assert.True(interactions.PreviewEmbedAt(rightEdge));
        WaitForUi(() => !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        string rendered = EmbedText(interactions.PopoverEmbedRoot!);
        Assert.Contains("Section body", rendered, StringComparison.Ordinal);
        Assert.Contains("Nested leaf", rendered, StringComparison.Ordinal);
        interactions.ClosePopoverCommand.Execute(null);

        Assert.True(interactions.PreviewEmbedAt(Inside(tab.Text, "![[broken.png]]")));
        WaitForUi(() => !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        Assert.Null(interactions.PopoverImage);
        Assert.Contains(
            "Could not decode image",
            EmbedText(interactions.PopoverEmbedRoot!),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SvgPreview_UsesSecureStaticPolicyAndRejectsExternalResources()
    {
        foreach (string resource in new[]
        {
            "https://example.invalid/pixel.png",
            "file:///C:/private/secret.png",
            "relative.png",
        })
        {
            Assert.False(
                EditorInteractionCoordinator.SecureSvgAllowsResourceForTests(resource));
        }

        string[] rejected =
        [
            "<!DOCTYPE svg [<!ENTITY xxe SYSTEM 'file:///C:/private/secret'>]><svg xmlns='http://www.w3.org/2000/svg'>&xxe;</svg>",
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><image href='https://example.invalid/pixel.png'/></svg>",
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><image href='file:///C:/private/secret.png'/></svg>",
            "<?xml-stylesheet href='https://example.invalid/style.css'?><svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>",
        ];
        foreach (string source in rejected)
        {
            Assert.Null(EditorInteractionCoordinator.DecodeImage(
                Encoding.UTF8.GetBytes(source),
                "image/svg+xml"));
        }

        const string safe =
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='8' height='8' fill='red'/></svg>";
        Assert.True(EditorInteractionCoordinator.SecureSvgParsesForTests(safe));
    }
    [Fact]
    public void ExternallyReindexedSnapshot_FailsClosedUntilEditorReloads()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var navigation = new List<EditorNavigationRequest>();
        var announcements = new List<A11yEvent>();
        using var tab = OpenSourceTab(session, navigation.Add, announcements.Add);
        string before = File.ReadAllText(fixture.SourcePath);

        File.WriteAllText(fixture.SourcePath, before + "\nExternally changed.\n");
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        tab.EditorInteractions!.RefreshArtifactCacheForTests();

        Assert.True(tab.EditorInteractions.ActivateAt(
            Inside(tab.Text, "[[target#Destination]]")));
        Assert.Empty(navigation);
        Assert.Contains(
            announcements,
            item => item is A11yEvent.HostComposed composed
                && composed.Text.Contains("Reload source.md", StringComparison.Ordinal));

        int checkbox = tab.Text.IndexOf("[ ]", StringComparison.Ordinal) + 1;
        Assert.True(tab.EditorInteractions.ActivateAt(
            checkbox,
            EditorInteractionOrigin.Pointer));
        Assert.Equal(before + "\nExternally changed.\n", File.ReadAllText(fixture.SourcePath));
    }

    [Fact]
    public void TabAndGroupDeactivation_CloseTransientPopovers()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            _ => { });
        workspace.OpenPath("source.md");
        WorkspaceGroupViewModel originalGroup = workspace.ActiveGroup;
        WorkspaceTabViewModel source = originalGroup.ActiveTab!;
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();

        Assert.True(source.EditorInteractions.PreviewEmbedAt(
            Inside(source.Text, "![[target#Destination]]")));
        WaitForUi(() => !source.EditorInteractions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        workspace.OpenPath("target.md", WorkspaceOpenTarget.NewTab);
        Assert.False(source.EditorInteractions.IsPopoverOpen);

        originalGroup.ActiveTab = source;
        Assert.True(source.EditorInteractions.PreviewEmbedAt(
            Inside(source.Text, "![[target#Destination]]")));
        WaitForUi(() => !source.EditorInteractions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        workspace.OpenPath("target.md", WorkspaceOpenTarget.SplitRight);
        Assert.False(source.EditorInteractions.IsPopoverOpen);
    }

    [Fact]
    public void RepeatedHover_ReusesMathClassificationUntilTheWindowRevisionChanges()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var tab = OpenSourceTab(session);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        int citation = Inside(tab.Text, "[@doe]");

        long baseline = interactions.MathRangeRefreshCountForTests;
        interactions.HoverAt(citation);
        Assert.Equal(baseline, interactions.MathRangeRefreshCountForTests);
        interactions.HoverAt(citation);
        Assert.Equal(baseline, interactions.MathRangeRefreshCountForTests);

        tab.Text += "\nEdit invalidates the canonical window.\n";
        interactions.HoverAt(citation);
        Assert.Equal(baseline, interactions.MathRangeRefreshCountForTests);
        interactions.RefreshMathRangesForTests();
        Assert.True(interactions.MathRangeRefreshCountForTests >= baseline + 1);
    }

    [Fact]
    public void CitationHover_DoesNotRequestFocusAndMathClassificationKeepsWholeDocumentContext()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var tab = OpenSourceTab(session);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        int popoverFocusRequests = 0;
        int editorFocusRequests = 0;
        interactions.PopoverFocusRequested += (_, _) => popoverFocusRequests++;
        interactions.FocusRequested += (_, _) => editorFocusRequests++;

        interactions.HoverAt(Inside(tab.Text, "[@doe]"));
        Assert.True(interactions.IsPopoverOpen);
        Assert.Equal(0, popoverFocusRequests);

        interactions.HoverAt(0);
        Assert.True(interactions.IsPopoverOpen);
        interactions.IsPopoverOpen = false;
        Assert.Equal(0, editorFocusRequests);

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "[@doe]")));
        Assert.Equal(1, popoverFocusRequests);

        Assert.False(interactions.ActivateAt(Inside(tab.Text, "#not-a-tag-in-spaced-math")));
        Assert.False(interactions.ActivateAt(Inside(tab.Text, "[[target]] in spaced math")));
        Assert.True(interactions.MathRangeRefreshCountForTests >= 1);
    }

    [Fact]
    public void DuplicateTab_RetainsWorkspaceInteractionDependencies()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var announcements = new List<A11yEvent>();
        var tags = new List<string>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add);
        workspace.EditorTagActivated += (_, tag) => tags.Add(tag);
        workspace.OpenPath("source.md");

        workspace.DuplicateTabCommand.Execute(null);
        WorkspaceTabViewModel duplicate = workspace.ActiveGroup.ActiveTab!;
        duplicate.EditorInteractions!.RefreshMathRangesForTests();
        duplicate.EditorInteractions.RefreshArtifactCacheForTests();
        Assert.Same(workspace.EditorPreferences, duplicate.EditorPreferences);

        workspace.EditorPreferences.ToggleSpellCheckCommand.Execute(null);
        Assert.True(duplicate.EditorPreferences.IsSpellCheckEnabled);
        Assert.True(duplicate.EditorInteractions!.ActivateAt(Inside(duplicate.Text, "#project")));
        Assert.Equal(["project"], tags);

        Assert.True(duplicate.EditorInteractions.ActivateAt(
            Inside(duplicate.Text, "[[target#Destination]]")));
        Assert.Equal("target.md", workspace.ActiveGroup.ActiveTab!.Path);
        Assert.Contains(announcements, item => item is A11yEvent.InternalNavigated);
    }

    [Fact]
    public void DuplicateHeadingNavigation_PrefersTheExactCoreAnchorId()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            _ => { });
        workspace.OpenPath("source.md");
        WorkspaceTabViewModel source = workspace.ActiveGroup.ActiveTab!;
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();

        Assert.True(source.EditorInteractions.ActivateAt(
            Inside(source.Text, "[[target#destination-2]]")));

        WorkspaceTabViewModel target = workspace.ActiveGroup.ActiveTab!;
        Assert.Equal("target.md", target.Path);
        int expected = target.Text.LastIndexOf("## Destination", StringComparison.Ordinal);
        WaitForUi(() => target.EditorCaretOffset == expected);
    }

    [Fact]
    public void AsyncAnchorNavigation_DropsLateResultsAfterCaretMoveOrDeactivation()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var announcements = new List<A11yEvent>();
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "target.md")),
            announce: announcements.Add,
            startInteractionBackgroundWork: false,
            anchorResolver: (source, kind, anchor) =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
                return SlateUniffiMethods.LinkAnchorByteOffset(source, kind, anchor);
            });

        Assert.True(tab.NavigateToAnchor(
            new LinkAnchor("heading", "Destination"),
            null,
            announcements.Add));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        tab.EditorCaretOffset = 1;
        release.Set();
        WaitForUi(() => tab.AnchorNavigationPublishCountForTests == 1);
        Assert.Equal(1, tab.EditorCaretOffset);
        Assert.Empty(announcements);

        entered.Reset();
        release.Reset();
        bool isActive = true;
        Assert.True(tab.NavigateToAnchor(
            new LinkAnchor("heading", "Destination"),
            null,
            announcements.Add,
            () => isActive));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        isActive = false;
        release.Set();
        WaitForUi(() => tab.AnchorNavigationPublishCountForTests == 2);
        Assert.Equal(1, tab.EditorCaretOffset);
        Assert.Empty(announcements);
    }

    [Fact]
    public void MissingBlockNavigation_FocusesTargetAndUsesBlockSpecificCopy()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var announcements = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add,
            startInteractionBackgroundWork: false);
        int focusRequests = 0;
        workspace.EditorPaneFocusRequested += (_, _) => focusRequests++;
        workspace.OpenPath("source.md");
        WorkspaceTabViewModel source = workspace.ActiveGroup.ActiveTab!;
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();
        focusRequests = 0;

        Assert.True(source.EditorInteractions.ActivateAt(
            Inside(source.Text, "[[target^missing-block]]")));
        Assert.Equal("target.md", workspace.ActiveGroup.ActiveTab!.Path);
        Assert.Equal(1, focusRequests);
        WaitForUi(() => announcements.Any(item =>
            item is A11yEvent.HostComposed composed
                && composed.Text.Contains(
                    "Block missing-block was not found",
                    StringComparison.Ordinal)));
    }
    [Fact]
    public void SpellingPipeline_IsOptInBoundedAndClearsWhenDisabled()
    {
        RunOnSta(() =>
        {
            var service = new FakeEditorSpellingService();
            using var preferences = new EditorPreferencesViewModel(
                _ => { },
                service);
            var editor = new SlateTextEditor
            {
                Text = "mispel is deliberately wrong",
            };
            using var spelling = new AvalonSpellingCoordinator(editor, preferences);

            spelling.RefreshForTests();
            Assert.Equal(0, spelling.ErrorCountForTests);
            Assert.Null(service.LastCheckedText);

            preferences.ToggleSpellCheckCommand.Execute(null);
            spelling.RefreshForTests();
            Assert.Equal(1, spelling.ErrorCountForTests);
            Assert.Equal(editor.Text, service.LastCheckedText);

            preferences.ToggleSpellCheckCommand.Execute(null);
            spelling.RefreshForTests();
            Assert.Equal(0, spelling.ErrorCountForTests);
        });
    }

    [Fact]
    public void PointerHandler_SetsSourceCaretBeforeSynchronousNavigation()
    {
        string source = File.ReadAllText(RepoFile(
            "apps",
            "slate-windows",
            "src",
            "SlateWindows",
            "SlateTextEditor.cs"));
        int handler = source.IndexOf(
            "OnPreviewMouseLeftButtonDown",
            StringComparison.Ordinal);
        int caret = source.IndexOf("CaretOffset = offset;", handler, StringComparison.Ordinal);
        int activation = source.IndexOf(
            "InteractionSession?.ActivateAt(",
            handler,
            StringComparison.Ordinal);

        Assert.True(handler >= 0);
        Assert.True(caret > handler);
        Assert.True(activation > caret);
    }

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
    private static WorkspaceTabViewModel OpenSourceTab(
        VaultSession session,
        Action<EditorNavigationRequest>? navigate = null,
        Action<A11yEvent>? announce = null)
    {
        var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            navigate: navigate,
            announce: announce,
            startInteractionBackgroundWork: false);
        tab.EditorInteractions!.RefreshMathRangesForTests();
        tab.EditorInteractions.RefreshArtifactCacheForTests();
        return tab;
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static int Inside(string text, string needle)
    {
        int start = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Fixture token is missing: {needle}");
        return start + Math.Min(2, needle.Length - 1);
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

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA spelling test timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class RedTeamFixture : IDisposable
    {
        private RedTeamFixture(string root)
        {
            Root = root;
            SourcePath = Path.Combine(root, "source.md");
        }

        public string Root { get; }
        public string SourcePath { get; }

        public static RedTeamFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                $"slate-w2-red-team-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "nested.md"), "Nested leaf.\n");
            File.WriteAllText(
                Path.Combine(root, "target.md"),
                """
                # Lead

                ## Destination

                Section body.

                ![[nested]]

                ## Destination

                Duplicate section body.

                Block body ^block-id
                """);
            File.WriteAllBytes(
                Path.Combine(root, "broken.png"),
                "this is not a PNG"u8.ToArray());
            File.WriteAllText(
                Path.Combine(root, "source.md"),
                """
                # Source

                %% [[hidden]] %%
                [[target#Destination]]
                [[target#destination-2]]
                [[target^missing-block]]
                ![[target#Destination]]
                ![[broken.png]]
                [@doe]
                #project
                - [ ] task prose

                $$

                #not-a-tag-in-spaced-math
                [[target]] in spaced math

                $$
                """);
            return new RedTeamFixture(root);
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

internal sealed class FakeEditorSpellingService : IEditorSpellingService
{
    public bool IsAvailable => true;
    public string? LastCheckedText { get; private set; }

    public IReadOnlyList<EditorSpellingError> Check(string text)
    {
        LastCheckedText = text;
        int offset = text.IndexOf("mispel", StringComparison.Ordinal);
        return offset < 0 ? [] : [new EditorSpellingError(offset, "mispel".Length)];
    }

    public void Dispose()
    {
    }
}
