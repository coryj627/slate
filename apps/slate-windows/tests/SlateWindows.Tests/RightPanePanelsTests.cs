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
            "# Target\n\nBody. Links back to [[host]].\n\n"
                + "## Alpha\n\nFirst section.\n\n"
                + "## Beta\n\nSecond section. ^b1\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "anchored.md"),
            "![[target#Alpha]]\n\n![[target#Beta]]\n\n![[target^b1]]\n");
        // A link-dense note (round 7): outgoing display caps with a
        // loud notice; the embed BEYOND the display cap must still
        // reach the embeds leaf.
        var dense = new System.Text.StringBuilder("# Dense\n\n");
        for (int i = 1; i <= 600; i++)
        {
            dense.Append($"[[d{i}]]\n");
        }
        dense.Append("![[target]]\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "dense.md"), dense.ToString());
        // Outline fan-out (round 8): heading extraction is
        // count-unbounded in core.
        var heads = new System.Text.StringBuilder();
        for (int i = 1; i <= 1200; i++)
        {
            heads.Append($"# Heading {i}\n\n");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "headings.md"), heads.ToString());
        // All-embed density (round 8): the candidate array and the
        // dispatcher publish must both stay bounded.
        var embDense = new System.Text.StringBuilder("# EmbDense\n\n");
        for (int i = 1; i <= 2000; i++)
        {
            embDense.Append($"![[e{i}]]\n");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "embdense.md"), embDense.ToString());
        // A megabyte heading that RESOLVES (round 16): the section
        // card must bound its title while the anchored resolution
        // stays exact.
        string longHeading = new('h', 1024 * 1024);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "longhead.md"),
            $"# {longHeading}\n\nSection body.\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "bigsec.md"),
            $"![[longhead#{longHeading}]]\n");
        // Backlink density (round 11): the page request is bounded
        // at 200 — the header and notice must carry the true total.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "hub.md"), "# Hub\n");
        for (int i = 1; i <= 201; i++)
        {
            File.WriteAllText(
                Path.Combine(_fixture.Root, $"in{i}.md"), "See [[hub]].\n");
        }
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
        // Per-occurrence alt text: the same image twice with
        // different alts must resolve per occurrence, never share.
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "photo.png"), new byte[16]);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "alts.md"),
            "![Front](photo.png)\n\n![Back](photo.png)\n");
        // The materialized-row cap: one target repeated far past
        // MaxEmbedRows (all cache hits — the cap is on rows, not
        // resolutions).
        var repeats = new System.Text.StringBuilder("# Rowcap\n\n");
        for (int i = 0; i < RightPanePanelsViewModel.MaxEmbedRows + 44; i++)
        {
            repeats.Append("![[e1]]\n");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "rowcap.md"), repeats.ToString());
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
                anchors?.Add(new AnchorRequest(anchor, resolvedText)),
            _ => true,
            _ => { });

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
        // first row's resolution AND its built card (round 2: a fresh
        // card per occurrence re-decoded the same image) — never a
        // budget casualty, never a second core call.
        Assert.NotEqual(
            EmbedRowViewModel.OverBudgetMessage, panels.Embeds[cap].Node.Title);
        Assert.Same(
            panels.Embeds[0].Resolution, panels.Embeds[cap].Resolution);
        Assert.Same(panels.Embeds[0].Node, panels.Embeds[cap].Node);

        // The next DISTINCT target degrades loudly, not silently.
        Assert.Equal(
            EmbedRowViewModel.OverBudgetMessage,
            panels.Embeds[cap + 1].Node.Title);
        Assert.True(panels.Embeds[cap + 1].Node.IsWarning);
    }

    [Fact]
    public void LinkDenseNotesCapDisplayedRowsLoudly()
    {
        var panels = LoadHost([], path: "dense.md");

        // 601 links, display capped: the header keeps the TRUE
        // count, the notice says what is hidden, and the embed
        // beyond the cap still reaches its leaf.
        Assert.Equal(
            RightPanePanelsViewModel.MaxOutgoingRows,
            panels.OutgoingLinks.Count);
        Assert.Equal("Outgoing links, 601 entries", panels.OutgoingLinksHeader);
        Assert.Equal(
            $"Showing {RightPanePanelsViewModel.MaxOutgoingRows} of 601 "
                + "outgoing links.",
            panels.OutgoingLinksTruncationNotice);
        EmbedRowViewModel embed = Assert.Single(panels.Embeds);
        Assert.Equal("Embedded note: target.md", embed.Node.Title);
    }

    [Fact]
    public void BacklinkDenseNotesKeepTheTrueTotalLoudly()
    {
        var panels = LoadHost([], path: "hub.md");

        // 201 inbound links, page bounded at 200: the header speaks
        // the TRUE count and the truncation is never silent.
        Assert.Equal(200, panels.Backlinks.Count);
        Assert.Equal("Backlinks, 201 entries", panels.BacklinksHeader);
        Assert.Equal(
            "Showing 200 of 201 backlinks.",
            panels.BacklinksTruncationNotice);
    }

    [Fact]
    public void HeadingDenseNotesCapTheOutlineLoudly()
    {
        var announced = new List<A11yEvent>();
        var panels = LoadHost(announced, path: "headings.md");

        Assert.Equal(
            RightPanePanelsViewModel.MaxOutlineRows, panels.Outline.Count);
        Assert.Equal(
            $"Showing {RightPanePanelsViewModel.MaxOutlineRows} of 1200 "
                + "headings.",
            panels.OutlineTruncationNotice);
        // The announcement speaks the TRUE total, not the display cap.
        var count = Assert.Single(announced.OfType<A11yEvent.OutlineCount>());
        Assert.Equal(1200u, count.Count);

        // The save-refresh path publishes through the same cap.
        panels.NoteSaved("headings.md");
        WaitFor(() => !panels.IsLoadingOutline, "the save refresh never settled");
        Assert.Equal(
            RightPanePanelsViewModel.MaxOutlineRows, panels.Outline.Count);
        Assert.NotNull(panels.OutlineTruncationNotice);
    }

    [Fact]
    public void AllEmbedDenseNotesBoundEveryPublication()
    {
        var panels = LoadHost([], path: "embdense.md");

        // 2000 embed links: outgoing display caps, and the embeds
        // leaf holds cap + one summary row whose tail count comes
        // from the TRUE total (the candidate array is itself capped).
        Assert.Equal(
            RightPanePanelsViewModel.MaxOutgoingRows,
            panels.OutgoingLinks.Count);
        Assert.Equal(
            "Outgoing links, 2000 entries", panels.OutgoingLinksHeader);
        Assert.Equal(
            RightPanePanelsViewModel.MaxEmbedRows + 1, panels.Embeds.Count);
        // The header speaks the TRUE embed count, not the capped
        // collection with its synthetic summary row (round 17).
        Assert.Equal("Embeds, 2000 entries", panels.EmbedsHeader);
        EmbedRowViewModel summary = panels.Embeds[^1];
        Assert.True(summary.Node.IsWarning);
        Assert.Equal(
            $"Embed limit reached for this note. "
                + $"{2000 - RightPanePanelsViewModel.MaxEmbedRows} more "
                + "embeds are not shown.",
            summary.Node.Title);
    }

    [Fact]
    public void SectionEmbedCardsBoundTheirTitlesWhileResolvingExactly()
    {
        var panels = LoadHost([], path: "bigsec.md");

        // The megabyte heading RESOLVED (exact anchor through the
        // whole pipeline) — and the card's title, which becomes the
        // Expander header and UIA name, stays display-bounded.
        EmbedRowViewModel row = Assert.Single(panels.Embeds);
        Assert.IsType<EmbedResolution.Section>(row.Resolution);
        Assert.StartsWith("Embedded section: ", row.Node.Title);
        Assert.True(row.Node.Title.Length <= 4200);
    }

    [Fact]
    public void OutlineRowsBoundDisplayWhileAnchorsStayExact()
    {
        // Round 15: the outline's rendered text and UIA name clip at
        // the display ceiling, while the anchor — what activation
        // resolves by — stays verbatim.
        string giant = new('h', 1024 * 1024);
        var anchors = new List<AnchorRequest>();
        var panels = MakePanels([], anchors: anchors);
        var row = new OutlineRowViewModel(new Heading(
            Level: 2, Text: giant, Ordinal: 0,
            AnchorId: giant, ByteOffset: 0));

        Assert.True(row.Text.Length <= 4097);
        Assert.True(row.AutomationName.Length <= 4200);
        Assert.Equal(giant, row.AnchorId);

        panels.OpenHeading(row);
        AnchorRequest request = Assert.Single(anchors);
        Assert.Equal(giant, request.Anchor.Text);
        Assert.True(request.ResolvedText!.Length <= 4097);
    }

    [Fact]
    public void DisplayStringsAreBoundedWhileActivationDataStaysExact()
    {
        // Round 14: a megabyte target must not become a megabyte UIA
        // name — but truncating the DATA would activate a different
        // URL, so the record keeps it verbatim.
        string giant = new('a', 1024 * 1024);
        var row = new OutgoingLinkRowViewModel(
            new OutgoingLink(
                TargetPath: null, TargetRaw: $"https://x.example/{giant}",
                TargetAnchor: null, Kind: "markdown", IsEmbed: false,
                IsExternal: true, IsUnresolved: false, Snippet: "",
                Ordinal: 0, SpanStart: 0, SpanEnd: 0, DisplayText: null));

        Assert.True(row.DisplayTarget.Length <= 4097);
        Assert.True(row.AutomationName.Length <= 4200);
        Assert.EndsWith("…", row.DisplayTarget);
        Assert.Equal(1024 * 1024 + 18, row.Link.TargetRaw.Length);
    }

    [Fact]
    public void AnchoredEmbedsResolveTheirSectionsNotTheWholeNote()
    {
        var panels = LoadHost([], path: "anchored.md");

        // TargetRaw is anchor-stripped: resolving by it rendered
        // every anchored embed as the full note, and the cache
        // collapsed distinct anchors of one note into one card
        // (round 6).
        Assert.Equal(3, panels.Embeds.Count);
        Assert.Equal(
            "Embedded section: Alpha from target.md",
            panels.Embeds[0].Node.Title);
        Assert.Equal(
            "Embedded section: Beta from target.md",
            panels.Embeds[1].Node.Title);
        Assert.Equal(
            "Embedded block from target.md",
            panels.Embeds[2].Node.Title);
        Assert.NotSame(panels.Embeds[0].Node, panels.Embeds[1].Node);
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
    public void DuplicateAltTextsResolvePerOccurrence()
    {
        var panels = LoadHost([], path: "alts.md");

        // Same image, two alts: the cache is keyed per occurrence
        // (round 2: keying by raw target alone reused the FIRST
        // occurrence's alt for every later one).
        Assert.Equal(2, panels.Embeds.Count);
        var front = Assert.IsType<EmbedResolution.Image>(
            panels.Embeds[0].Resolution);
        var back = Assert.IsType<EmbedResolution.Image>(
            panels.Embeds[1].Resolution);
        Assert.Equal("Front", front.Alt);
        Assert.Equal("Back", back.Alt);
        Assert.NotSame(panels.Embeds[0].Node, panels.Embeds[1].Node);
    }

    [Fact]
    public void RowCapSummarizesTheTailLoudly()
    {
        int cap = RightPanePanelsViewModel.MaxEmbedRows;
        var panels = LoadHost([], path: "rowcap.md");

        // cap rendered rows + ONE summary row covering the tail.
        Assert.Equal(cap + 1, panels.Embeds.Count);
        Assert.Same(panels.Embeds[0].Node, panels.Embeds[cap - 1].Node);
        EmbedRowViewModel summary = panels.Embeds[cap];
        Assert.True(summary.Node.IsWarning);
        Assert.Equal(
            "Embed limit reached for this note. 44 more embeds are "
                + "not shown.",
            summary.Node.Title);
        Assert.Null(summary.Node.SourcePath);
    }

    [Fact]
    public void StaleOutlinePublishesAreDiscarded()
    {
        var panels = LoadHost([]);
        Assert.Equal(2, panels.Outline.Count);
        OutlineRowViewModel first = panels.Outline[0];

        // A slower request from BEFORE the newest one completing
        // late must not overwrite (round 2: save-refreshes reuse the
        // note generation, so ordering needs its own token).
        var stale = new Heading(
            Level: 1, Text: "Stale", Ordinal: 0,
            AnchorId: "stale", ByteOffset: 0);
        panels.PublishOutline(
            "host.md",
            panels.LoadGenerationForTests,
            panels.OutlineRequestIdForTests - 1,
            [stale],
            total: 1,
            announceCount: false);
        Assert.Equal(2, panels.Outline.Count);
        Assert.Same(first, panels.Outline[0]);

        // The CURRENT request id still publishes.
        panels.PublishOutline(
            "host.md",
            panels.LoadGenerationForTests,
            panels.OutlineRequestIdForTests,
            [stale],
            total: 1,
            announceCount: false);
        OutlineRowViewModel row = Assert.Single(panels.Outline);
        Assert.Equal("Stale", row.Text);
    }

    [Fact]
    public void OutlineNotificationsAlwaysReadFinalState()
    {
        var panels = LoadHost([]);
        var observed = new List<string?>();
        panels.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(panels.OutlineEmptyMessage))
            {
                observed.Add(panels.OutlineEmptyMessage);
            }
        };

        // Populated → empty refresh: every notification must already
        // see the mutated collection (round 3: the loading flag was
        // notified first, freezing the previous state's sentence).
        panels.PublishOutline(
            "host.md",
            panels.LoadGenerationForTests,
            panels.OutlineRequestIdForTests,
            [],
            total: 0,
            announceCount: false);
        Assert.NotEmpty(observed);
        Assert.All(
            observed,
            value => Assert.Equal("This note has no headings.", value));

        // Empty → populated refresh: the message must clear.
        observed.Clear();
        var heading = new Heading(
            Level: 1, Text: "Back", Ordinal: 0,
            AnchorId: "back", ByteOffset: 0);
        panels.PublishOutline(
            "host.md",
            panels.LoadGenerationForTests,
            panels.OutlineRequestIdForTests,
            [heading],
            total: 1,
            announceCount: false);
        Assert.NotEmpty(observed);
        Assert.All(observed, Assert.Null);
    }

    [Fact]
    public async Task CoreReadFailuresNeverMasqueradeAsEmptyNotes()
    {
        using var fixture = FixtureVault.Create(0, "right-pane-failure");
        File.WriteAllText(Path.Combine(fixture.Root, "note.md"), "# N\n");
        var session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var panels = new RightPanePanelsViewModel(
            session, _ => { }, (_, _) => true, _ => true, (_, _) => { },
            _ => true, _ => { });
        session.Dispose();

        // Every core call now fails: the leaves must SAY so — a read
        // fault rendered as "no links here yet" is a lie (round 3).
        panels.NoteChanged("note.md");
        await panels.DrainForTests();

        Assert.StartsWith(
            "Could not load links: ", panels.BacklinksEmptyMessage);
        Assert.StartsWith(
            "Could not load links: ", panels.OutgoingLinksEmptyMessage);
        Assert.StartsWith(
            "Could not resolve embeds: ", panels.EmbedsEmptyMessage);
        Assert.StartsWith(
            "Could not load outline: ", panels.OutlineEmptyMessage);
        Assert.False(panels.IsLoadingLinks);
        Assert.False(panels.IsLoadingOutline);
        Assert.False(panels.IsResolvingEmbeds);
    }

    [Fact]
    public async Task ShutdownDuringResolutionNeverFaultsAndRefusesNewWork()
    {
        using var fixture = FixtureVault.Create(0, "right-pane-shutdown");
        var body = new System.Text.StringBuilder("# S\n\n");
        for (int i = 0; i < 60; i++)
        {
            body.Append($"![[m{i}]]\n");
        }
        File.WriteAllText(
            Path.Combine(fixture.Root, "s.md"), body.ToString());
        var session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var panels = new RightPanePanelsViewModel(
            session, _ => { }, (_, _) => true, _ => true, (_, _) => { },
            _ => true, _ => { });

        // Close the vault mid-batch: workers must degrade through
        // their catches — a drain that faults fails this test.
        panels.NoteChanged("s.md");
        panels.Shutdown();
        session.Dispose();
        await panels.DrainForTests();

        // Post-shutdown the panels refuse new work.
        panels.NoteChanged("other.md");
        Assert.Equal("s.md", panels.NotePath);
    }

    [Fact]
    public void ActiveNoteRenameRebindsThePanels()
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            announced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("host.md");
        Assert.Equal("host.md", workspace.Panels.NotePath);

        // The rename funnel changes the active tab's Path IN PLACE —
        // the panels must follow it, or every save on the new path
        // is ignored until a tab switch (round 2).
        workspace.RetargetPath("host.md", "renamed.md");
        Assert.Equal("renamed.md", workspace.Panels.NotePath);
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
