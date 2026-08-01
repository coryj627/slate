// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-2 (#734): the link/structure leaf contracts, over real core
/// output — the mac twins are LeafPortTests (empty-state sentences),
/// LeafContextMatrixTests (rebinding), and the panel row/AX label
/// pins. Labels here are the mac strings verbatim.
/// </summary>
public sealed class RightPanePanelsTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public RightPanePanelsTests()
    {
        _fixture = FixtureVault.Create(0, "right-pane-panels");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "host.md"),
            "# Alpha\n\n## Beta\n\nSee [[target]] and [[missing]] and "
                + "[link](https://example.com) here.\n\n![[target]]\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "target.md"),
            "# Target\n\nBody. Links back to [[host]].\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "bare.md"), "Plain text.\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "dup.md"),
            "# Beta\n\nIntro.\n\n## Beta\n\nDetail.\n");
        // The budget fixture: every distinct target within the cap,
        // then a REPEAT of the first (a dedupe cache hit, never a
        // budget slot), then one more distinct target (over budget).
        var many = new System.Text.StringBuilder("# Many\n\n");
        for (int i = 1;
            i <= RightPanePanelsViewModel.MaxResolvedEmbedTargets;
            i++)
        {
            many.Append($"![[e{i}]]\n");
        }
        many.Append("![[e1]]\n");
        many.Append(
            $"![[e{RightPanePanelsViewModel.MaxResolvedEmbedTargets + 1}]]\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "many.md"), many.ToString());
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private sealed record Navigation(string Path, WorkspaceOpenTarget Target);

    private sealed record AnchorRequest(LinkAnchor Anchor, string? ResolvedText);

    private RightPanePanelsViewModel MakePanels(
        List<A11yEvent> announced,
        List<Navigation>? navigations = null,
        List<string>? externalOpens = null,
        bool externalSucceeds = true,
        List<AnchorRequest>? anchors = null,
        bool navigationSucceeds = true) =>
        new(
            _session,
            announced.Add,
            (path, target) =>
            {
                navigations?.Add(new Navigation(path, target));
                return navigationSucceeds;
            },
            target =>
            {
                externalOpens?.Add(target);
                return externalSucceeds;
            },
            (anchor, resolvedText) =>
                anchors?.Add(new AnchorRequest(anchor, resolvedText)));

    private static void WaitFor(Func<bool> condition, string reason)
    {
        Assert.True(
            SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(20)),
            reason);
    }

    private RightPanePanelsViewModel LoadHost(
        List<A11yEvent> announced,
        List<Navigation>? navigations = null,
        List<string>? externalOpens = null,
        bool externalSucceeds = true,
        List<AnchorRequest>? anchors = null,
        bool navigationSucceeds = true,
        string path = "host.md")
    {
        RightPanePanelsViewModel panels = MakePanels(
            announced, navigations, externalOpens, externalSucceeds, anchors,
            navigationSucceeds);
        panels.NoteChanged(path);
        WaitFor(
            () => !panels.IsLoadingLinks
                && !panels.IsLoadingOutline
                && !panels.IsResolvingEmbeds,
            $"the {path} note never finished loading");
        return panels;
    }

    [Fact]
    public void BacklinkRowsCarryTheMacLabelContract()
    {
        var panels = LoadHost([]);

        BacklinkRowViewModel row = Assert.Single(panels.Backlinks);
        Assert.Equal("target.md", row.SourcePath);
        Assert.Equal("target.md", row.FileName);
        Assert.StartsWith("Backlink from target.md, context: ", row.AutomationName);
        Assert.Contains("host", row.AutomationName);
        Assert.Equal("Opens the source note.", row.AutomationHelpText);
        Assert.Equal("Backlinks, 1 entry", panels.BacklinksHeader);
        Assert.Null(panels.BacklinksEmptyMessage);
    }

    [Fact]
    public void OutgoingRowsCarryTheThreeStateContract()
    {
        var panels = LoadHost([]);

        // Document order: [[target]], [[missing]], external, ![[target]].
        Assert.Equal(4, panels.OutgoingLinks.Count);
        Assert.Equal("Outgoing links, 4 entries", panels.OutgoingLinksHeader);

        OutgoingLinkRowViewModel resolved = panels.OutgoingLinks[0];
        Assert.Equal("Link to target.md", resolved.AutomationName);
        Assert.Equal("Opens the linked note.", resolved.AutomationHelpText);
        Assert.Null(resolved.Badge);
        Assert.False(resolved.IsUnresolved);

        OutgoingLinkRowViewModel unresolved = panels.OutgoingLinks[1];
        Assert.Equal("Unresolved link: missing", unresolved.AutomationName);
        Assert.Equal(
            "Cannot open. Target file is not in the vault.",
            unresolved.AutomationHelpText);
        Assert.Equal("Unresolved", unresolved.Badge);
        Assert.True(unresolved.IsUnresolved);

        OutgoingLinkRowViewModel external = panels.OutgoingLinks[2];
        Assert.Equal(
            "External link: https://example.com", external.AutomationName);
        Assert.Equal("Opens in the default browser.", external.AutomationHelpText);
        Assert.Equal("External", external.Badge);

        OutgoingLinkRowViewModel embed = panels.OutgoingLinks[3];
        Assert.Equal("Embed", embed.Badge);
    }

    [Fact]
    public void OutlineRowsAreFlatWithLevelLabelsAndAnnounceOncePerFile()
    {
        var announced = new List<A11yEvent>();
        var panels = LoadHost(announced);

        Assert.Equal(2, panels.Outline.Count);
        Assert.Equal("Level 1 heading: Alpha", panels.Outline[0].AutomationName);
        Assert.Equal("Level 2 heading: Beta", panels.Outline[1].AutomationName);
        Assert.Equal(0, panels.Outline[0].Indent);
        Assert.Equal(14, panels.Outline[1].Indent);

        var count = Assert.Single(announced.OfType<A11yEvent.OutlineCount>());
        Assert.Equal(2u, count.Count);

        // A save-refresh re-reads headings but never re-announces.
        panels.NoteSaved("host.md");
        WaitFor(() => !panels.IsLoadingOutline, "the save refresh never settled");
        Assert.Single(announced.OfType<A11yEvent.OutlineCount>());
    }

    [Fact]
    public void EmbedRowsResolveThroughTheBoundedPreviewPath()
    {
        var panels = LoadHost([]);

        EmbedRowViewModel row = Assert.Single(panels.Embeds);
        Assert.True(row.Link.IsEmbed);
        Assert.Equal("Embedded note: target.md", row.Node.Title);
        Assert.Equal("Embeds, 1 entry", panels.EmbedsHeader);
        Assert.Null(panels.EmbedsEmptyMessage);
    }

    [Fact]
    public void EmptyStatesSpeakTheMacSentences()
    {
        var panels = MakePanels([]);

        // No selection.
        Assert.Equal(
            "Select a note to see its backlinks.", panels.BacklinksEmptyMessage);
        Assert.Equal(
            "Select a note to see its outgoing links.",
            panels.OutgoingLinksEmptyMessage);
        Assert.Equal(
            "Select a note to see its outline.", panels.OutlineEmptyMessage);
        Assert.Equal(
            "Select a note to see its embeds.", panels.EmbedsEmptyMessage);

        // A selected note with no content: the sentences must differ
        // from no-selection (the mac LeafPortTests distinction).
        panels.NoteChanged("bare.md");
        WaitFor(
            () => !panels.IsLoadingLinks
                && !panels.IsLoadingOutline
                && !panels.IsResolvingEmbeds,
            "the bare note never finished loading");
        Assert.Equal("No notes link here yet.", panels.BacklinksEmptyMessage);
        Assert.Equal(
            "This note has no outgoing links.", panels.OutgoingLinksEmptyMessage);
        Assert.Equal("This note has no headings.", panels.OutlineEmptyMessage);
        Assert.Equal("This note has no embeds.", panels.EmbedsEmptyMessage);
    }

    [Fact]
    public void SamePathNoteChangeIsARefetchFreeNoOp()
    {
        var announced = new List<A11yEvent>();
        var panels = LoadHost(announced);
        BacklinkRowViewModel row = Assert.Single(panels.Backlinks);

        // The retention contract: leaf switches re-push the same path.
        panels.NoteChanged("host.md");
        Assert.Same(row, Assert.Single(panels.Backlinks));
        Assert.Single(announced.OfType<A11yEvent.OutlineCount>());
    }

    [Fact]
    public void NullNoteEmptiesEveryCollection()
    {
        var panels = LoadHost([]);
        panels.NoteChanged(null);

        Assert.Empty(panels.Backlinks);
        Assert.Empty(panels.OutgoingLinks);
        Assert.Empty(panels.Outline);
        Assert.Empty(panels.Embeds);
        Assert.Null(panels.NotePath);
    }

    [Fact]
    public void BacklinkActivationNavigatesAndAnnouncesTheMacVerb()
    {
        var announced = new List<A11yEvent>();
        var navigations = new List<Navigation>();
        var panels = LoadHost(announced, navigations);

        panels.OpenBacklink(panels.Backlinks[0]);
        Navigation navigation = Assert.Single(navigations);
        Assert.Equal("target.md", navigation.Path);
        Assert.Equal(WorkspaceOpenTarget.CurrentTab, navigation.Target);
        var navigated = Assert.Single(
            announced.OfType<A11yEvent.InternalNavigated>());
        Assert.Equal("Opened backlink to", navigated.Kind);
        Assert.Equal("target.md", navigated.Filename);

        panels.OpenBacklink(panels.Backlinks[0], WorkspaceOpenTarget.NewTab);
        Assert.Equal(WorkspaceOpenTarget.NewTab, navigations[1].Target);
    }

    [Fact]
    public void OutgoingActivationRoutesByState()
    {
        var announced = new List<A11yEvent>();
        var navigations = new List<Navigation>();
        var externalOpens = new List<string>();
        var panels = LoadHost(announced, navigations, externalOpens);

        // Resolved: navigates + "Opened".
        panels.OpenOutgoingLink(panels.OutgoingLinks[0]);
        Assert.Equal("target.md", Assert.Single(navigations).Path);
        var navigated = Assert.Single(
            announced.OfType<A11yEvent.InternalNavigated>());
        Assert.Equal("Opened", navigated.Kind);

        // Unresolved: announce-only, never navigates.
        panels.OpenOutgoingLink(panels.OutgoingLinks[1]);
        Assert.Single(navigations);
        var unresolved = Assert.Single(
            announced.OfType<A11yEvent.LinkUnresolved>());
        Assert.Equal("missing", unresolved.Target);

        // External allowed scheme: routed to the opener, success spoken.
        panels.OpenOutgoingLink(panels.OutgoingLinks[2]);
        Assert.Equal("https://example.com", Assert.Single(externalOpens));
        Assert.Single(announced.OfType<A11yEvent.ExternalLinkOpened>());
        Assert.Single(navigations);
    }

    [Fact]
    public void ExternalSchemeAllowlistRefusesLoudly()
    {
        var announced = new List<A11yEvent>();
        var externalOpens = new List<string>();
        var panels = MakePanels(announced, null, externalOpens);
        static OutgoingLinkRowViewModel ExternalRow(string target) => new(
            new OutgoingLink(
                TargetPath: null, TargetRaw: target, TargetAnchor: null,
                Kind: "markdown", IsEmbed: false, IsExternal: true,
                IsUnresolved: false, Snippet: "", Ordinal: 0,
                SpanStart: 0, SpanEnd: 0, DisplayText: null));

        // The mac allowlist, verbatim: only http/https/mailto launch.
        panels.OpenOutgoingLink(ExternalRow("file:///C:/secret.txt"));
        panels.OpenOutgoingLink(ExternalRow("javascript:alert(1)"));
        panels.OpenOutgoingLink(ExternalRow("slate-custom://x"));
        Assert.Empty(externalOpens);
        Assert.Equal(
            3, announced.OfType<A11yEvent.ExternalLinkUnsupported>().Count());

        panels.OpenOutgoingLink(ExternalRow("mailto:cj@startingblind.com"));
        panels.OpenOutgoingLink(ExternalRow("https://example.com"));
        Assert.Equal(2, externalOpens.Count);
        Assert.Equal(
            2, announced.OfType<A11yEvent.ExternalLinkOpened>().Count());
    }

    [Fact]
    public void ExternalOpenerFailureIsSpoken()
    {
        var announced = new List<A11yEvent>();
        var panels = LoadHost(
            announced, externalOpens: [], externalSucceeds: false);

        panels.OpenOutgoingLink(panels.OutgoingLinks[2]);
        var failed = Assert.Single(
            announced.OfType<A11yEvent.ExternalLinkFailed>());
        Assert.Equal("https://example.com", failed.Target);
    }

    [Fact]
    public void OutlineActivationScrollsByHeadingAnchor()
    {
        var anchors = new List<AnchorRequest>();
        var panels = LoadHost([], anchors: anchors);

        panels.OpenHeading(panels.Outline[1]);
        AnchorRequest request = Assert.Single(anchors);
        // The anchor carries the UNIQUE slug (duplicate headings all
        // match the first occurrence by text); the display text rides
        // along so the landing announcement speaks prose.
        Assert.Equal("heading", request.Anchor.Kind);
        Assert.Equal("beta", request.Anchor.Text);
        Assert.Equal("Beta", request.ResolvedText);
    }

    [Fact]
    public void DuplicateHeadingsActivateByTheirUniqueSlugs()
    {
        var anchors = new List<AnchorRequest>();
        var panels = LoadHost([], anchors: anchors, path: "dup.md");

        Assert.Equal(2, panels.Outline.Count);
        Assert.Equal("beta", panels.Outline[0].AnchorId);
        Assert.Equal("beta-2", panels.Outline[1].AnchorId);

        // Activating the SECOND "Beta" must land on it — an anchor
        // sent by display text would scroll to the first.
        panels.OpenHeading(panels.Outline[1]);
        AnchorRequest request = Assert.Single(anchors);
        Assert.Equal("beta-2", request.Anchor.Text);
        Assert.Equal("Beta", request.ResolvedText);
    }

    [Fact]
    public void NavigationRefusalNeverAnnouncesSuccess()
    {
        var announced = new List<A11yEvent>();
        var navigations = new List<Navigation>();
        var panels = LoadHost(
            announced, navigations, navigationSucceeds: false);

        // A dirty-tab cancel (or failed save) refuses the navigation:
        // the attempt reaches the workspace, but no "Opened" may be
        // spoken while the editor stayed put.
        panels.OpenBacklink(panels.Backlinks[0]);
        panels.OpenOutgoingLink(panels.OutgoingLinks[0]);
        panels.OpenEmbedSource("target.md");
        Assert.Equal(3, navigations.Count);
        Assert.Empty(announced.OfType<A11yEvent.InternalNavigated>());
    }

    [Fact]
    public void EmbedJumpAnnouncesTheMacVerb()
    {
        var announced = new List<A11yEvent>();
        var navigations = new List<Navigation>();
        var panels = LoadHost(announced, navigations);

        panels.OpenEmbedSource("target.md");
        Assert.Equal("target.md", Assert.Single(navigations).Path);
        var navigated = Assert.Single(
            announced.OfType<A11yEvent.InternalNavigated>());
        Assert.Equal("Opened embed source", navigated.Kind);
    }

    [Fact]
    public void EmbedResolutionDedupesAndDegradesTheBudgetLoudly()
    {
        int cap = RightPanePanelsViewModel.MaxResolvedEmbedTargets;
        var panels = LoadHost([], path: "many.md");

        // cap distinct targets + one repeat + one over-budget target.
        Assert.Equal(cap + 2, panels.Embeds.Count);

        // Distinct targets within the cap resolve through core.
        Assert.NotEqual(
            EmbedRowViewModel.OverBudgetMessage, panels.Embeds[0].Node.Title);
        Assert.NotEqual(
            EmbedRowViewModel.OverBudgetMessage,
            panels.Embeds[cap - 1].Node.Title);

        // The repeat past the cap is a dedupe CACHE HIT sharing the
        // first row's resolution — never a budget casualty, never a
        // second core call.
        Assert.NotEqual(
            EmbedRowViewModel.OverBudgetMessage, panels.Embeds[cap].Node.Title);
        Assert.Same(
            panels.Embeds[0].Resolution, panels.Embeds[cap].Resolution);

        // The next DISTINCT target degrades loudly, not silently.
        Assert.Equal(
            EmbedRowViewModel.OverBudgetMessage,
            panels.Embeds[cap + 1].Node.Title);
        Assert.True(panels.Embeds[cap + 1].Node.IsWarning);
    }

    [Fact]
    public void OverBudgetRowKeepsJumpToSourceAlive()
    {
        var link = new OutgoingLink(
            TargetPath: "target.md", TargetRaw: "target", TargetAnchor: null,
            Kind: "wiki", IsEmbed: true, IsExternal: false,
            IsUnresolved: false, Snippet: "", Ordinal: 0,
            SpanStart: 0, SpanEnd: 0, DisplayText: null);

        EmbedRowViewModel row = EmbedRowViewModel.OverBudget(link);
        Assert.Equal(EmbedRowViewModel.OverBudgetMessage, row.Node.Title);
        Assert.True(row.Node.IsWarning);
        Assert.False(row.Node.IsDisclosure);
        Assert.Equal("target.md", row.Node.SourcePath);
    }

    [Fact]
    public void CountImageBytesSpansNestedTrees()
    {
        var nestedImage = new EmbedResolution.Image(
            "img.png", new byte[10], "image/png", null);
        var deeper = new EmbedResolution.Section(
            "b.md", "H", "text",
            [
                new NestedEmbed("![[img2.png]]", 0, 3,
                    new EmbedResolution.Image(
                        "img2.png", new byte[7], "image/png", null)),
            ]);
        var tree = new EmbedResolution.FullNote(
            "a.md", "text",
            [
                new NestedEmbed("![[img.png]]", 0, 5, nestedImage),
                new NestedEmbed("![[b]]", 6, 9, deeper),
            ]);

        Assert.Equal(
            17, RightPanePanelsViewModel.CountImageBytes(tree));
        Assert.Equal(
            10, RightPanePanelsViewModel.CountImageBytes(nestedImage));
        Assert.Equal(
            0,
            RightPanePanelsViewModel.CountImageBytes(
                new EmbedResolution.Block("b.md", "id", "text")));
    }

    [Fact]
    public void SamePaneTabSwitchRebindsThePanels()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("host.md");
        workspace.OpenPath("target.md", WorkspaceOpenTarget.NewTab);
        Assert.Equal("target.md", workspace.Panels.NotePath);

        WorkspaceGroupViewModel group = workspace.ActiveGroup;
        Assert.Equal(2, group.Tabs.Count);

        // The round-1 regression: a same-pane switch leaves group
        // identity unchanged, so the ActiveGroup setter's sync never
        // fires — Activate itself must re-derive the panels' note.
        group.ActiveTab = group.Tabs[0];
        Assert.Equal("host.md", workspace.Panels.NotePath);
        group.ActiveTab = group.Tabs[1];
        Assert.Equal("target.md", workspace.Panels.NotePath);
    }
}
