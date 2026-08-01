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

    private RightPanePanelsViewModel MakePanels(
        List<A11yEvent> announced,
        List<Navigation>? navigations = null,
        List<string>? externalOpens = null,
        bool externalSucceeds = true,
        List<LinkAnchor>? anchors = null) =>
        new(
            _session,
            announced.Add,
            (path, target) => navigations?.Add(new Navigation(path, target)),
            target =>
            {
                externalOpens?.Add(target);
                return externalSucceeds;
            },
            anchor => anchors?.Add(anchor));

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
        List<LinkAnchor>? anchors = null)
    {
        RightPanePanelsViewModel panels = MakePanels(
            announced, navigations, externalOpens, externalSucceeds, anchors);
        panels.NoteChanged("host.md");
        WaitFor(
            () => !panels.IsLoadingLinks
                && !panels.IsLoadingOutline
                && !panels.IsResolvingEmbeds,
            "the host note never finished loading");
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
        var anchors = new List<LinkAnchor>();
        var panels = LoadHost([], anchors: anchors);

        panels.OpenHeading(panels.Outline[1]);
        LinkAnchor anchor = Assert.Single(anchors);
        Assert.Equal("heading", anchor.Kind);
        Assert.Equal("Beta", anchor.Text);
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
}
