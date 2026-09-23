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
    public void FrontmatterNote_LoadsWholeFileAndKeepsArtifactSpansAligned()
    {
        const string source =
            "---\ntags:\n  - accessibility\n---\n\n![[target#Destination]]\n";
        using InteractionFixture fixture = InteractionFixture.Create(source);
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

        Assert.Equal(source, tab.Text);
        Assert.True(interactions.PreviewEmbedAt(Inside(tab.Text, "![[target#Destination]]")));
        WaitForUi(() => !interactions.PopoverTitle.StartsWith(
            "Loading",
            StringComparison.Ordinal));
        Assert.True(interactions.IsPopoverOpen);
        Assert.Contains("Section body", EmbedText(interactions.PopoverEmbedRoot!));
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
    public void ActivatedCitation_DoesNotInheritHoverAutoClose()
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

        Assert.True(interactions.ActivateAt(citation));
        Assert.True(interactions.IsPopoverOpen);
        interactions.ClearCitationHover();
        PumpUiFor(TimeSpan.FromMilliseconds(1200));
        Assert.True(interactions.IsPopoverOpen);

        interactions.ClosePopoverCommand.Execute(null);
        DrainUi();
        interactions.HoverAt(citation);
        Assert.True(interactions.IsPopoverOpen);
        interactions.ClearCitationHover();
        WaitForUi(() => !interactions.IsPopoverOpen);
    }

    /// <summary>W7-7 (#1251, R-8): activating a citation announces it in
    /// core's sentence, built from the preview's speech exactly as it
    /// arrived, and the popover's UIA name is that same rendering. Two
    /// shapes: the unstyled placeholder, which already names itself, and a
    /// styled unresolved key's speech, which does not. A host that
    /// prefixes fails the second (its speech is no longer raw); a core
    /// that omits the prefix fails its name, and one that always prefixes
    /// fails the first ("Citation. Citation: doe").</summary>
    [Theory]
    [InlineData(false, "Citation: doe", "Citation: doe")]
    [InlineData(true, "Unresolved citation: doe", "Citation. Unresolved citation: doe")]
    public void ActivatingACitationAnnouncesCoresSentenceAndNamesThePopoverWithIt(
        bool styled, string rawSpeech, string spoken)
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        if (styled)
        {
            fixture.ConfigureCitationStyle();
        }
        using VaultSession session = ScannedSession(fixture);
        var announcements = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = OpenSourceTab(session, announcements);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "[@doe]")));

        Assert.True(interactions.IsPopoverOpen);
        var shown = Assert.IsType<A11yEvent.CitationPopoverShown>(Assert.Single(announcements));
        Assert.Equal(rawSpeech, shown.Speech);
        Assert.Equal(spoken, SlateUniffiMethods.A11yRender(shown).Text);
        Assert.Equal(spoken, interactions.PopoverAutomationName);
    }

    /// <summary>R-8 answers an activation. A pointer hover shows the same
    /// popover, named the same way, without an assertive announcement on
    /// every rest of the mouse — hover already keeps its unavailable
    /// states silent.</summary>
    [Fact]
    public void HoveringACitationShowsThePopoverWithoutAnnouncing()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = ScannedSession(fixture);
        var announcements = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = OpenSourceTab(session, announcements);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;

        interactions.HoverAt(Inside(tab.Text, "[@doe]"));

        Assert.True(interactions.IsPopoverOpen);
        Assert.Equal("Citation: doe", interactions.PopoverAutomationName);
        Assert.Empty(announcements);
    }

    /// <summary>R-8: an embed preview announces its outcome when the
    /// result lands, not at open, where the only outcome is
    /// "Loading".</summary>
    [Fact]
    public void AResolvedEmbedAnnouncesItsPreviewWhenTheResultLands()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = ScannedSession(fixture);
        var announcements = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = OpenSourceTab(session, announcements);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;

        Assert.True(interactions.PreviewEmbedAt(Inside(tab.Text, "![[target#Destination]]")));
        Assert.True(interactions.IsPopoverOpen);
        Assert.StartsWith("Loading", interactions.PopoverTitle, StringComparison.Ordinal);
        Assert.Empty(announcements);

        WaitForUi(() => !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));

        // The target as authored (the anchor lives in the card title), the
        // same pair the popover's name carries.
        var shown = Assert.IsType<A11yEvent.EmbedPreviewShown>(Assert.Single(announcements));
        Assert.Equal("target", shown.Target);
        Assert.Equal("Embedded section: Destination from target.md", shown.Title);
        Assert.Equal(
            "Embed preview for target. Embedded section: Destination from target.md.",
            SlateUniffiMethods.A11yRender(shown).Text);
        Assert.StartsWith("Embed preview for target, source line", interactions.PopoverAutomationName, StringComparison.Ordinal);
        Assert.EndsWith(shown.Title, interactions.PopoverAutomationName, StringComparison.Ordinal);
    }

    /// <summary>R-8: every outcome the resolver can give a top-level preview
    /// other than a card — each structured unresolved result, and a
    /// resolver that throws (a vault error, raw or structured, and any
    /// other exception) — is announced with ITS reason, through the real
    /// path: the resolve runs or throws, and the catch, the publish and
    /// the presenter run as shipped. The exact event and its fields come
    /// first, so a mapping that collapsed failures into one reason — and
    /// spoke false recovery information — fails every other row. Only
    /// then the surfaces: the popover's body, its name and the live tree's
    /// UIA name are that one core rendering, so the host's card wording
    /// (Describe) reaches none of them. (The depth limit is unreachable at
    /// the top level; the presenter fact below covers it.)</summary>
    [Theory]
    [InlineData("target-not-found")]
    [InlineData("heading-not-found")]
    [InlineData("block-not-found")]
    [InlineData("read-error")]
    [InlineData("thrown-vault-db")]
    [InlineData("thrown-vault-structured")]
    [InlineData("thrown-other")]
    [InlineData("thrown-empty-message")]
    public void AnUnresolvedEmbedIsWordedByCoreOnEverySurface(string outcome) =>
        RunOnSta(() =>
        {
            (string Embed, string Target, EmbedUnresolvedReason Reason, Exception? Thrown, string Expected) row =
                outcome switch
                {
                    "target-not-found" => (
                        "![[missing]]",
                        "missing",
                        new EmbedUnresolvedReason.TargetNotFound("missing"),
                        null,
                        "Embed preview for missing. Target not found: missing."),
                    "heading-not-found" => (
                        "![[target#Nope]]",
                        "target",
                        new EmbedUnresolvedReason.HeadingNotFound("target.md", "Nope"),
                        null,
                        "Embed preview for target. Heading not found: Nope in target.md."),
                    "block-not-found" => (
                        "![[target#^nope]]",
                        "target",
                        new EmbedUnresolvedReason.BlockNotFound("target.md", "nope"),
                        null,
                        "Embed preview for target. Block not found: nope in target.md."),
                    // Indexed while readable, then made invalid UTF-8 below:
                    // the link resolves and core's read fails.
                    "read-error" => (
                        "![[unreadable]]",
                        "unreadable",
                        new EmbedUnresolvedReason.ReadError("file at \"unreadable.md\" is not valid UTF-8"),
                        null,
                        "Embed preview for unreadable. Could not read embed: file at \"unreadable.md\" is not valid UTF-8."),
                    // A corrupt index fails core's first lookup; Db's detail is
                    // its own text.
                    "thrown-vault-db" => (
                        "![[target]]",
                        "target",
                        new EmbedUnresolvedReason.ReadError("sqlite error: database disk image is malformed"),
                        new VaultException.Db("sqlite error: database disk image is malformed"),
                        "Embed preview for target. Could not read embed: sqlite error: database disk image is malformed."),
                    // A structured error reads as core words it, never as the
                    // binding's "@path=…, @reason=…".
                    "thrown-vault-structured" => (
                        "![[target]]",
                        "target",
                        new EmbedUnresolvedReason.ReadError("Invalid path ../out.md: escapes the vault"),
                        new VaultException.InvalidPath("../out.md", "escapes the vault"),
                        "Embed preview for target. Could not read embed: Invalid path ../out.md: escapes the vault."),
                    "thrown-other" => (
                        "![[target]]",
                        "target",
                        new EmbedUnresolvedReason.ReadError("The resolver was torn down."),
                        new InvalidOperationException("The resolver was torn down."),
                        "Embed preview for target. Could not read embed: The resolver was torn down."),
                    // An exception with an empty message: the event carries
                    // what the error said (nothing), and core's sentence
                    // stands alone, never "Could not read embed:.".
                    "thrown-empty-message" => (
                        "![[target]]",
                        "target",
                        new EmbedUnresolvedReason.ReadError(string.Empty),
                        new InvalidOperationException(string.Empty),
                        "Embed preview for target. Could not read embed."),
                    _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
                };
            using InteractionFixture fixture = InteractionFixture.Create($"# Source\n\n{row.Embed}\n");
            File.WriteAllText(Path.Combine(fixture.Root, "unreadable.md"), "# Readable at scan time\n");
            using VaultSession session = ScannedSession(fixture);
            File.WriteAllBytes(Path.Combine(fixture.Root, "unreadable.md"), [0xff, 0xfe, 0xff]);
            var announcements = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
                announce: announcements.Add,
                startInteractionBackgroundWork: false,
                interactionBackgroundFaultForTests: worker =>
                    worker is EditorInteractionWorkerKind.EmbedPreview ? row.Thrown : null);
            EditorInteractionCoordinator interactions = tab.EditorInteractions!;
            interactions.RefreshMathRangesForTests();
            interactions.RefreshArtifactCacheForTests();

            Assert.True(interactions.PreviewEmbedAt(Inside(tab.Text, row.Embed)));
            Assert.Empty(announcements);
            WaitForUi(() => !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));

            // The event and its fields first: this reason variant with this
            // payload, not merely some unavailable line.
            var unavailable = Assert.IsType<A11yEvent.EmbedPreviewUnavailable>(Assert.Single(announcements));
            Assert.Equal(row.Target, unavailable.Target);
            Assert.Equal(row.Reason, unavailable.Reason);
            if (row.Thrown is VaultException error)
            {
                Assert.Equal(
                    SlateUniffiMethods.VaultErrorDetail(error),
                    Assert.IsType<EmbedUnresolvedReason.ReadError>(unavailable.Reason).Message);
            }

            // Then every surface: one core rendering.
            Assert.Equal(row.Expected, SlateUniffiMethods.A11yRender(unavailable).Text);
            Assert.Equal(row.Expected, interactions.PopoverBody);
            Assert.Equal(row.Expected, interactions.PopoverAutomationName);
            Assert.Null(interactions.PopoverEmbedRoot);
            AssertTheLiveTreeExposes(tab, row.Expected);
        });

    /// <summary>The shipped popover template, bound to the tab, exposes
    /// <paramref name="sentence"/> as the popover's UIA name and as the
    /// text of its body.</summary>
    private static void AssertTheLiveTreeExposes(WorkspaceTabViewModel tab, string sentence)
    {
        var resources = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
            new Uri("/SlateWindows;component/WorkspaceTemplates.xaml", UriKind.Relative));
        var template = (System.Windows.DataTemplate)resources["WorkspaceTabContentTemplate"];
        var root = (System.Windows.FrameworkElement)template.LoadContent();
        root.DataContext = tab;
        var window = new System.Windows.Window { Content = root };
        try
        {
            window.Show();
            root.UpdateLayout();
            root.Dispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            root.UpdateLayout();

            var popover = (System.Windows.UIElement)Assert.Single(
                LogicalDescendants(root),
                element => System.Windows.Automation.AutomationProperties.GetAutomationId(element)
                    == "EditorInteractionPopover");
            System.Windows.Automation.Peers.AutomationPeer peer =
                System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(popover);
            Assert.Equal(sentence, peer.GetName());
            System.Windows.Controls.TextBox body = Assert.Single(
                LogicalDescendants(popover).OfType<System.Windows.Controls.TextBox>(),
                box => System.Windows.Automation.AutomationProperties.GetName(box) == "Preview content");
            Assert.Equal(sentence, body.Text);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>R-8, every reason the presenter can be handed — including the
    /// depth limit, which a top-level preview cannot reach through the
    /// resolver: announcement, body and name are one core rendering.</summary>
    [Fact]
    public void EveryUnavailableReasonIsOneCoreRenderingOnEverySurface()
    {
        using InteractionFixture fixture = InteractionFixture.Create();
        using VaultSession session = ScannedSession(fixture);
        var announcements = new List<A11yEvent>();
        using WorkspaceTabViewModel tab = OpenSourceTab(session, announcements);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;

        foreach ((EmbedUnresolvedReason reason, string expected) in new (EmbedUnresolvedReason, string)[]
        {
            (new EmbedUnresolvedReason.TargetNotFound("X"), "Embed preview for X. Target not found: X."),
            (new EmbedUnresolvedReason.HeadingNotFound("x.md", "H"), "Embed preview for X. Heading not found: H in x.md."),
            (new EmbedUnresolvedReason.BlockNotFound("x.md", "b1"), "Embed preview for X. Block not found: b1 in x.md."),
            (new EmbedUnresolvedReason.DepthLimitReached(), "Embed preview for X. Nested embed depth limit reached."),
            (new EmbedUnresolvedReason.ReadError("disk on fire"), "Embed preview for X. Could not read embed: disk on fire."),
        })
        {
            announcements.Clear();
            interactions.PresentUnavailableEmbedForTests("X", 3, reason);

            var unavailable = Assert.IsType<A11yEvent.EmbedPreviewUnavailable>(Assert.Single(announcements));
            Assert.Equal(reason, unavailable.Reason);
            Assert.Equal(expected, SlateUniffiMethods.A11yRender(unavailable).Text);
            Assert.Equal(expected, interactions.PopoverBody);
            Assert.Equal(expected, interactions.PopoverAutomationName);
            Assert.Null(interactions.PopoverEmbedRoot);
        }
    }

    /// <summary>W7-7 (#1251, R-8): WPF disables caret navigation in a
    /// read-only TextBox that hides its caret, so Down scrolled the
    /// focusable host instead and the reader heard line 1 again. Both
    /// preview bodies keep a caret, and the popover's scroll host is not a
    /// focus stop of its own that would take the arrows.</summary>
    [Fact]
    public void PreviewTextKeepsACaretAndTheScrollHostIsNotAFocusStop() =>
        RunOnSta(() =>
        {
            // The card renderer the popover and the embeds leaf share.
            var view = new EditorEmbedPreviewView
            {
                Root = new EditorEmbedPreviewNode(
                    "Embedded note: a.md",
                    [new EditorEmbedPreviewPart("# A\nSecond line\n", null)],
                    null,
                    "a.md",
                    IsDisclosure: true,
                    InitiallyExpanded: true,
                    IsWarning: false),
            };
            System.Windows.Controls.TextBox embedded = Assert.Single(
                LogicalDescendants(view).OfType<System.Windows.Controls.TextBox>());
            Assert.True(embedded.IsReadOnly);
            Assert.True(embedded.IsReadOnlyCaretVisible);

            // The popover itself, from the shipped template.
            var resources = (System.Windows.ResourceDictionary)System.Windows.Application.LoadComponent(
                new Uri("/SlateWindows;component/WorkspaceTemplates.xaml", UriKind.Relative));
            var template = (System.Windows.DataTemplate)resources["WorkspaceTabContentTemplate"];
            var root = (System.Windows.DependencyObject)template.LoadContent();
            System.Windows.DependencyObject popover = Assert.Single(
                LogicalDescendants(root),
                element => System.Windows.Automation.AutomationProperties.GetAutomationId(element)
                    == "EditorInteractionPopover");
            System.Windows.Controls.ScrollViewer host = Assert.Single(
                LogicalDescendants(popover).OfType<System.Windows.Controls.ScrollViewer>(),
                scroller => System.Windows.Automation.AutomationProperties.GetName(scroller)
                    == "Scrollable embed preview");
            Assert.False(host.Focusable);
            System.Windows.Controls.TextBox body = Assert.Single(
                LogicalDescendants(popover).OfType<System.Windows.Controls.TextBox>(),
                box => System.Windows.Automation.AutomationProperties.GetName(box) == "Preview content");
            Assert.True(body.IsReadOnly);
            Assert.True(body.IsReadOnlyCaretVisible);
        });

    private static IEnumerable<System.Windows.DependencyObject> LogicalDescendants(
        System.Windows.DependencyObject root)
    {
        foreach (object child in System.Windows.LogicalTreeHelper.GetChildren(root))
        {
            if (child is System.Windows.DependencyObject element)
            {
                yield return element;
                foreach (System.Windows.DependencyObject nested in LogicalDescendants(element))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void RunOnSta(Action action)
    {
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA preview test timed out.");
        failure?.Throw();
    }

    private static VaultSession ScannedSession(InteractionFixture fixture)
    {
        VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static WorkspaceTabViewModel OpenSourceTab(
        VaultSession session,
        List<A11yEvent> announcements)
    {
        var tab = new WorkspaceTabViewModel(
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
        return tab;
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
        // The preview's own resolve is single-shot, not one of the retrying
        // workers this fact counts (W7-7 R-8).
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            interactionBackgroundFaultForTests: kind =>
                kind is not EditorInteractionWorkerKind.EmbedPreview
                && Interlocked.Increment(ref attempts[(int)kind]) <= 2
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
                // The preview's own resolve is single-shot, not one of the
                // retrying workers this fact counts (W7-7 R-8).
                if (kind is EditorInteractionWorkerKind.EmbedPreview)
                {
                    return null;
                }
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
                case EditorInteractionWorkerKind.EmbedPreview:
                    tab.EditorInteractions!.RefreshMathRangesForTests();
                    tab.EditorInteractions.RefreshArtifactCacheForTests();
                    Assert.True(tab.EditorInteractions.PreviewEmbedAt(0));
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
            // A preview whose resolver failed after disposal is retired, not
            // announced (W7-7 R-8).
            Assert.DoesNotContain(announcements, item => item is A11yEvent.EmbedPreviewUnavailable);
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

    [Theory]
    [InlineData("[site](https://example.org/a#part)", "https://example.org/a#part", true)]
    [InlineData("<https://example.org/>", "https://example.org/", true)]
    [InlineData("[site][ref]\n\n[ref]: https://example.org/ref", "https://example.org/ref", true)]
    [InlineData("![image](https://example.org/a.png)", "https://example.org/a.png", true)]
    [InlineData("[local](target.md#Destination)", "target.md#Destination", false)]
    [InlineData("[[target#Destination]]", "target#Destination", false)]
    public void HyperlinkMetadataAndActionsUseCurrentCanonicalRecords(string authored, string destination, bool external)
    {
        using InteractionFixture fixture = InteractionFixture.Create(authored);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var navigation = new List<EditorNavigationRequest>();
        var opened = new List<string>();
        var announcements = new List<A11yEvent>();
        using var tab = new WorkspaceTabViewModel(session, new WorkspaceTabState(Guid.NewGuid(),
            new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")), startInteractionBackgroundWork: false);
        using var interactions = new EditorInteractionCoordinator(session, tab, navigation.Add,
            announce: announcements.Add, startBackgroundWork: false,
            openExternalForTests: value => { opened.Add(value); return true; });
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();
        EditorSemanticSpan span = Assert.Single(tab.EditorSession!.InspectInRange(0, authored.Length).Spans
, EditorHyperlinkTree.IsLink);
        Assert.Equal(destination, interactions.DestinationFor(span));
        Assert.True(interactions.ActivateSpan(span));
        Assert.True(interactions.ActivateAt(span.StartUtf16 + 1));
        if (external) { Assert.Equal(new[] { destination, destination }, opened); }
        else { Assert.Equal(2, navigation.Count); Assert.All(navigation, request => Assert.Equal("target.md", request.Path)); }
        tab.EditorDocument!.Insert(0, "edited ");
        Assert.Null(interactions.DestinationFor(span));
        Assert.False(interactions.ActivateSpan(span));
        EditorSemanticSpan current = Assert.Single(tab.EditorSession.InspectInRange(0, tab.EditorDocument.TextLength).Spans, EditorHyperlinkTree.IsLink);
        Assert.True(interactions.ActivateSpan(current));
        Assert.Contains(announcements, announcement => announcement is A11yEvent.HostComposed message && message.Text.StartsWith("Save ", StringComparison.Ordinal));
        Assert.Equal(external ? 2 : 0, opened.Count);
    }

    /// <summary>The caret action follows the innermost link at the caret, as
    /// the UIA Hyperlink tree does (E-9: the wikilink nested in a Markdown
    /// link's label is the inner child, and its Invoke follows the wikilink).</summary>
    [Fact]
    public void ActivationAtTheCaretFollowsTheInnermostLinkAsTheHyperlinkTreeDoes()
    {
        const string authored = "[outer [[target#Destination]]](https://example.org/outer)";
        using InteractionFixture fixture = InteractionFixture.Create(authored);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        var navigation = new List<EditorNavigationRequest>();
        var opened = new List<string>();
        using var tab = new WorkspaceTabViewModel(session, new WorkspaceTabState(Guid.NewGuid(),
            new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")), startInteractionBackgroundWork: false);
        using var interactions = new EditorInteractionCoordinator(session, tab, navigation.Add,
            announce: _ => { }, startBackgroundWork: false,
            openExternalForTests: value => { opened.Add(value); return true; });
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();
        EditorSemanticSpan[] links = [.. tab.EditorSession!.InspectInRange(0, authored.Length).Spans.Where(EditorHyperlinkTree.IsLink)];
        Assert.Contains(links, span => span.Kind is EditorSpanKind.Link);
        Assert.Contains(links, span => span.Kind is EditorSpanKind.Wikilink);

        Assert.True(interactions.ActivateAt(Inside(authored, "[[target#Destination]]")));
        Assert.Equal("target.md", Assert.Single(navigation).Path);
        Assert.Empty(opened);

        Assert.True(interactions.ActivateAt(Inside(authored, "outer ")));
        Assert.Equal(["https://example.org/outer"], opened);
        Assert.Single(navigation);
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
    private static void PumpUiFor(TimeSpan duration)
    {
        DateTime deadline = DateTime.UtcNow + duration;
        while (DateTime.UtcNow < deadline)
        {
            DrainUi();
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
            File.WriteAllText(
                Path.Combine(root, "unrelated.md"),
                "- [ ] unrelated task\n");
            return new InteractionFixture(root);
        }

        /// <summary>W7-7: a configured citation style, so core renders the
        /// citation speech instead of the host's unstyled placeholder — and
        /// an unresolved key's rendered speech does not name itself a
        /// citation. Must run before the session opens.</summary>
        public void ConfigureCitationStyle()
        {
            File.Copy(
                RepoFile("demo-vault", "csl", "ieee.csl"),
                Path.Combine(Root, "ieee.csl"));
            File.WriteAllText(
                Path.Combine(Root, "slate.json"),
                "{\"citations\":{\"cite_style\":\"ieee\"}}");
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
