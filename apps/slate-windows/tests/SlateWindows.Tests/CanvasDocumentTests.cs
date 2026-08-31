// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR A (#745) facts: the canvas document VM over a REAL
/// <see cref="VaultSession"/> and real <c>.canvas</c> bytes — contracts
/// A1 (one document per path, released on the last close), A3 (the five
/// load states), A4 (the degraded announcement once per document open),
/// A8–A14 (the outline projection, selection, activation and focus),
/// A15 (the persisted surface token), A17 (the scheduler conventions
/// and the §K budget in BOTH scheduling modes) and A19 (the close-gate
/// bypass).
/// </summary>
public sealed class CanvasDocumentTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];

    public CanvasDocumentTests()
    {
        _fixture = FixtureVault.Create(3, "canvas-document");
        WriteCanvasFixtures();
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private void WriteCanvasFixtures()
    {
        // One group holding two text cards and a file card that points
        // at a real note with a real heading, plus a link card and an
        // ungrouped card — enough for every activation arm and for a
        // group boundary in both directions.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            """
            {
              "nodes": [
                {"id":"grp","type":"group","x":-40,"y":-40,"width":560,"height":400,"label":"Research"},
                {"id":"question","type":"text","text":"# Core question\nCan it be accessible?","x":0,"y":0,"width":240,"height":140,"color":"1"},
                {"id":"evidence","type":"text","text":"Evidence so far","x":260,"y":0,"width":220,"height":140},
                {"id":"note","type":"file","file":"note0.md","subpath":"#Note 0","x":0,"y":180,"width":240,"height":140},
                {"id":"link","type":"link","url":"https://example.org/spec","x":640,"y":0,"width":240,"height":140},
                {"id":"loose","type":"text","text":"Unfiled thought","x":0,"y":460,"width":200,"height":100}
              ],
              "edges": [
                {"id":"e1","fromNode":"question","fromSide":"right","toNode":"evidence","toSide":"left","label":"supports"}
              ]
            }
            """);
        // Entries core preserves but cannot show — the t0 §5 banner's
        // subject. Nothing here is a ParseFailed, so the load is READY.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "skipped.canvas"),
            """
            {
              "nodes": [
                {"id":"kept","type":"text","text":"kept","x":0,"y":0,"width":100,"height":50},
                {"id":"no-x","type":"text","text":"no x","y":0,"width":100,"height":50},
                42,
                {"id":"kept","type":"text","text":"duplicate id","x":300,"y":0,"width":100,"height":50}
              ],
              "edges": []
            }
            """);
        // Not JSON at all: CanvasOpenInfo.degraded, which is the
        // PARSE-ERROR state and carries no skipped entries (CD-28).
        File.WriteAllText(
            Path.Combine(_fixture.Root, "broken.canvas"), "{ this is not json");
        File.WriteAllText(Path.Combine(_fixture.Root, "blank.canvas"), "{}");
    }

    private CanvasDocumentViewModel NewDocument(
        string path, bool synchronousForTests = true) =>
        new(
            _session,
            path,
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests);

    private WorkspaceViewModel NewWorkspace() =>
        new(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: _announced.Add);

    private static CanvasOutlineRow Row(CanvasDocumentViewModel document, string nodeId) =>
        Assert.IsType<CanvasOutlineRow>(document.RowFor(nodeId));

    // --- A1: the registry ------------------------------------------------

    /// <summary>
    /// Closing the pane a request was addressed to cancels it, even
    /// though the document lives on in another pane.
    /// </summary>
    /// <remarks>
    /// Retirement does not cover this: the document is not being
    /// retired. No peer may take the request (the address gate is doing
    /// its job), nothing supersedes it, and the closed tab's object
    /// graph stays reachable through it — a stranded request with no
    /// expiry, which is half of what made the un-addressed version
    /// wrong. Taken where the TAB SET changes, not in `Unloaded`, which
    /// also fires when a pane is merely hidden and whose request must
    /// survive.
    /// </remarks>
    [Fact]
    public void ClosingTheAddressedPaneCancelsItsPendingRequests() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(first.Canvas);

        // A second pane on the SAME path keeps the document alive.
        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel second =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.Same(document, second.Canvas);

        // Both requests pending, addressed to the tab about to close.
        document.RequestFocusLanding(second);
        document.RequestFilterFocus(second);
        Assert.NotNull(document.FocusRequest);
        Assert.NotNull(document.FilterFocusRequest);

        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);

        Assert.Same(document, first.Canvas);
        Assert.Null(document.FocusRequest);
        Assert.Null(document.FilterFocusRequest);
        Assert.False(
            document.HoldsPendingRequestsForTests,
            "a surviving document must not hold the closed pane's tab through "
            + "a request nobody can ever deliver.");
    });

    /// <summary>
    /// A pane that still EXISTS but now shows a different canvas is not a
    /// live address for the one it left.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sweep's first shape asked one question for the whole window —
    /// "is this tab still SOME canvas owner" — and a tab pointed at
    /// another canvas answers yes. Opening a second canvas into the
    /// current tab is the reachable route (`TryOpenItem`'s replace arm,
    /// which attaches and then sweeps), and the canvas the tab left
    /// survives whenever a second pane still shows it. It then went on
    /// holding a request addressed to a tab whose surface renders
    /// something else entirely: undeliverable, and holding the tab's
    /// object graph with it — the same defect the close case fixed, one
    /// step sideways.
    /// </para>
    /// <para>
    /// Ownership is the PAIRING of a tab with a document, so that is what
    /// the predicate asks. The re-raise arms below are what distinguish a
    /// predicate from a one-shot: the same wrong address is dropped again
    /// by the next sweep, and the right one survives it.
    /// </para>
    /// </remarks>
    [Fact]
    public void APaneThatMovedToAnotherCanvasStopsBeingAnAddress() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel moving =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel left =
            Assert.IsType<CanvasDocumentViewModel>(moving.Canvas);

        // A second pane on the SAME canvas, so the one the tab leaves
        // SURVIVES the sweep — otherwise retirement would clear the
        // requests and this fact would be about `Shutdown`.
        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel staying =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.Same(left, staying.Canvas);
        workspace.ActiveGroup.ActiveTab = moving;

        left.RequestFocusLanding(moving);
        left.RequestFilterFocus(moving);
        CanvasFocusRequest stale =
            Assert.IsType<CanvasFocusRequest>(left.FocusRequest);
        Assert.NotNull(left.FilterFocusRequest);

        // The pane is pointed at a DIFFERENT canvas. The tab is still
        // open, still a canvas tab, still in the group.
        workspace.OpenPath("connected.canvas");
        Assert.NotSame(left, moving.Canvas);
        Assert.IsType<CanvasDocumentViewModel>(moving.Canvas);
        Assert.Same(
            left,
            staying.Canvas);
        Assert.Null(left.FocusRequest);
        Assert.Null(left.FilterFocusRequest);
        Assert.False(
            left.HoldsPendingRequestsForTests,
            "the canvas the pane left is still holding that pane's request — "
            + "the surface showing it now renders a different document, so "
            + "nothing will ever deliver it.");

        // A PREDICATE, not a one-shot: the same wrong address raised
        // again is dropped again by the next sweep.
        left.RequestFocusLanding(moving);
        left.RequestFilterFocus(moving);
        workspace.OpenPath("note0.md", WorkspaceOpenTarget.NewTab);
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        Assert.Null(left.FocusRequest);
        Assert.Null(left.FilterFocusRequest);

        // …and the pane that DID stay keeps its own, through the same
        // sweep that dropped the other two.
        left.RequestFocusLanding(staying);
        left.RequestFilterFocus(staying);
        CanvasFocusRequest live =
            Assert.IsType<CanvasFocusRequest>(left.FocusRequest);
        workspace.OpenPath("note0.md", WorkspaceOpenTarget.NewTab);
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        Assert.Same(live, left.FocusRequest);
        Assert.NotNull(left.FilterFocusRequest);

        // The dropped record arriving late clears NOTHING: completion is
        // by reference identity, and a swept record is not the live one.
        left.CompleteFocusLanding(stale);
        Assert.Same(
            live,
            left.FocusRequest);
    });

    [Fact]
    public void OneDocumentIsSharedByEveryTabOnThePath()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.True(first.IsCanvas);
        Assert.True(first.IsCanvasVisible);
        // Contract A15's placeholder exclusion: a canvas tab has a real
        // surface, so the "ships in its owning milestone" body is gone.
        Assert.False(first.IsPlaceholder);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(first.Canvas);
        Assert.Equal(CanvasLoadState.Ready, document.State);

        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel second =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.Same(document, second.Canvas);
        // R-B: one selection object, therefore one selection.
        Assert.Same(document.Selection, second.Canvas!.Selection);
    }

    [Fact]
    public void TheLastTabClosingReleasesTheDocumentAndItsMarks()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(first.Canvas);
        _ = document.Selection.ToggleMark("question");
        Assert.True(document.Selection.IsMarked("question"));

        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel second =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        // One tab left: the shared document survives with its marks.
        Assert.Same(document, first.Canvas);
        Assert.True(document.Selection.IsMarked("question"));

        workspace.ActiveGroup.ActiveTab = first;
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        // Shut down: a released document refuses Load, and reopening
        // the path builds a NEW document with no marks.
        document.Load();
        Assert.Equal(CanvasLoadState.Ready, document.State);
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel reopened = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.NotSame(document, reopened);
        Assert.Empty(reopened.Selection.Marked);
        _ = second;
    }

    [Fact]
    public void RenamingRekeysTheRegistryAndCarriesTheSelectionAcross()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel before =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        before.SelectNode("evidence");
        _ = before.Selection.ToggleMark("evidence");

        File.Move(
            Path.Combine(_fixture.Root, "board.canvas"),
            Path.Combine(_fixture.Root, "renamed.canvas"));
        workspace.RetargetPath("board.canvas", "renamed.canvas");

        CanvasDocumentViewModel after =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.NotSame(before, after);
        Assert.Equal("renamed.canvas", after.Path);
        Assert.Equal(CanvasLoadState.Ready, after.State);
        // CD-32: a rename is not a close.
        Assert.Equal("evidence", after.Selection.Selected);
        Assert.True(after.Selection.IsMarked("evidence"));
    }

    [Fact]
    public void ARetargetThatCannotReopenLandsInRetargetAbsentNotFailed()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);

        // The rename is published, but nothing exists at the new path.
        workspace.RetargetPath("board.canvas", "gone.canvas");
        CanvasDocumentViewModel after =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.Equal(CanvasLoadState.RetargetAbsent, after.State);
        Assert.Contains("board.canvas", after.StateMessage);
        Assert.Contains("gone.canvas", after.StateMessage);
        Assert.True(after.IsReadOnly);
    }

    // --- A3/A4: the load states ------------------------------------------

    [Fact]
    public void ReadyPublishesCoreRowsAndSeatsTheFirstRowSilently()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();

        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Null(document.StateMessage);
        Assert.False(document.IsReadOnly);
        Assert.NotEmpty(document.Outline);
        // Core's reading order, untransformed (R-D).
        Assert.Equal(document.Outline[0].NodeId, document.Selection.Selected);
        // Contract A12: the landing seat says nothing — the focus event
        // reads the row it lands on (t0 §1.5 no-doubling).
        Assert.Empty(_announced);
        document.Shutdown();
    }

    [Fact]
    public void SkippedEntriesStayReadyAndDriveTheBannerNotTheState()
    {
        CanvasDocumentViewModel document = NewDocument("skipped.canvas");
        document.Load();

        // CD-28: entries core preserved but cannot show are WARNINGS.
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.False(document.IsReadOnly);
        Assert.True(document.PreservedItemCount > 0);
        Assert.NotEmpty(document.Outline);
        document.Shutdown();
    }

    [Fact]
    public void TheDegradedBannerIsTheSameRenderTheAnnouncementSpeaks()
    {
        CanvasDocumentViewModel document = NewDocument("skipped.canvas");
        document.Load();
        document.AnnouncerForTests.FlushForTests();

        RenderedAnnouncement spoken = Assert.Single(_announced);
        // Contract A4/CD-3: banner and speech are ONE render, so they
        // cannot drift.
        Assert.Equal(spoken.Text, document.DegradedBannerText);
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasLoadedDegraded(
                    (uint)document.PreservedItemCount))).Text,
            spoken.Text);
        document.Shutdown();
    }

    [Fact]
    public void TwoPanesOnOneDocumentAnnounceTheDegradedLoadOnce()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("skipped.canvas");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(first.Canvas);
        // A second pane on the same path: the registry hits, so the
        // 0→1 transition happened once (contract A4, CD-29).
        ((System.Windows.Input.ICommand)workspace.SplitRightCommand).Execute(null);
        workspace.OpenPath("skipped.canvas");
        Assert.Same(
            document,
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab).Canvas);

        document.AnnouncerForTests.FlushForTests();
        RenderedAnnouncement spoken = Assert.Single(_announced);
        Assert.Equal(document.DegradedBannerText, spoken.Text);
    }

    [Fact]
    public void AReloadIsAnOpenAndReArmsTheAnnouncement()
    {
        CanvasDocumentViewModel document = NewDocument("skipped.canvas");
        document.Load();
        document.AnnouncerForTests.FlushForTests();
        Assert.Single(_announced);

        document.Load();
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(2, _announced.Count);
        document.Shutdown();
    }

    [Fact]
    public void AParseFailureIsTheReadOnlyErrorStateWithCoresDetail()
    {
        CanvasDocumentViewModel document = NewDocument("broken.canvas");
        document.Load();

        Assert.Equal(CanvasLoadState.ParseError, document.State);
        Assert.True(document.IsReadOnly);
        // Never a blank pane: the message is core's own ParseFailed
        // detail, and there are no rows to pretend otherwise with.
        Assert.NotNull(document.StateMessage);
        Assert.Empty(document.Outline);
        Assert.Equal(0, document.PreservedItemCount);
        Assert.Null(document.DegradedBannerText);
        // Read-only BY CONSTRUCTION (contract A3): the handle is gone,
        // so a per-node read cannot answer.
        Assert.Empty(document.NeighborsOf("anything"));
        // Nothing is spoken for a parse failure — a "0 unsupported
        // items" sentence would be a lie about a file that produced no
        // rows at all (CD-28).
        Assert.Empty(_announced);
        document.Shutdown();
    }

    [Fact]
    public void AMissingFileIsFailedNotParseError()
    {
        CanvasDocumentViewModel document = NewDocument("no-such.canvas");
        document.Load();

        Assert.Equal(CanvasLoadState.Failed, document.State);
        Assert.NotNull(document.StateMessage);
        Assert.Empty(document.Outline);
        document.Shutdown();
    }

    [Fact]
    public void AnEmptyCanvasCarriesTheOnboardingCopyFromCore()
    {
        CanvasDocumentViewModel document = NewDocument("blank.canvas");
        document.Load();

        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Empty(document.Outline);
        // 0a-13 LABEL class, core-rendered. NOT CanvasEmptyOnboarding:
        // its template says "Press ⟨chord⟩ to create your first card"
        // unconditionally, and PR A has no create command — so any chord
        // in that slot tells a screen-reader user to press a key that
        // creates nothing. The t2 rule the spec cites in the same
        // sentence forbids exactly that; PR E swaps the event in with
        // the real chord (CD-37).
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasStatus(
                    new CanvasStatusNote.Empty()))).Text,
            document.EmptyOnboardingText);
        Assert.DoesNotContain(
            "create your first card",
            document.EmptyOnboardingText,
            StringComparison.Ordinal);
        document.Shutdown();
    }

    // --- A12: selection --------------------------------------------------

    [Fact]
    public void MovingSelectionAnnouncesTheCoreRenderedMoveAtTheVerbosity()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        _announced.Clear();

        document.SelectNode("evidence");
        document.AnnouncerForTests.FlushForTests();

        CanvasOutlineRow row = Row(document, "evidence");
        RenderedAnnouncement spoken = _announced[^1];
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasMovedTo(
                    CanvasVerbosity.Standard,
                    row.Kind,
                    row.Title,
                    row.OrdinalN,
                    row.TotalM,
                    row.GroupPath.Length > 0 ? row.GroupPath[^1] : null,
                    row.ConnectionCount,
                    row.ColorName,
                    Marked: false))).Text,
            spoken.Text);
        document.Shutdown();
    }

    [Fact]
    public void ReSelectingTheSameNodeIsSilent()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        document.SelectNode("evidence");
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        document.SelectNode("evidence");
        document.AnnouncerForTests.FlushForTests();
        Assert.Empty(_announced);
        document.Shutdown();
    }

    /// <summary>
    /// CD-4's count rule, pinned on the PURE decision so the coalescer
    /// is not in the way: the entered group's own card count is the
    /// arrived-at row's container size, never the sibling count.
    /// </summary>
    [Fact]
    public void CrossingAGroupBoundaryBuildsTheEntryEventWithTheGroupsOwnCount()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        CanvasOutlineRow inside = Row(document, "question");
        CanvasOutlineRow outside = Row(document, "loose");
        Assert.NotEmpty(inside.GroupPath);
        Assert.Empty(outside.GroupPath);

        Assert.Equal(
            new CanvasA11yEvent.CanvasGroupEntered(inside.GroupPath[^1], inside.TotalM),
            CanvasDocumentViewModel.GroupBoundaryEvent(outside.GroupPath, inside));
        Assert.Equal(
            new CanvasA11yEvent.CanvasGroupLeft(inside.GroupPath[^1]),
            CanvasDocumentViewModel.GroupBoundaryEvent(inside.GroupPath, outside));
        // Same container both times: no boundary crossed, nothing said.
        Assert.Null(
            CanvasDocumentViewModel.GroupBoundaryEvent(
                inside.GroupPath, Row(document, "evidence")));
        document.Shutdown();
    }

    /// <summary>
    /// And what the user actually HEARS when the two are announced
    /// back to back: the boundary and the move share the `navigation`
    /// coalescing class (0a-8), so inside the window the move wins.
    /// The membership list is core's and is not this host's to change,
    /// so both hosts behave identically — the property §W-D protects.
    /// </summary>
    [Fact]
    public void TheMoveSupersedesTheBoundaryInsideTheCoalescingWindow()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        document.SelectNode("loose", announce: false);
        _announced.Clear();

        document.SelectNode("question");
        document.AnnouncerForTests.FlushForTests();

        CanvasOutlineRow row = Row(document, "question");
        RenderedAnnouncement spoken = Assert.Single(_announced);
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasMovedTo(
                    CanvasVerbosity.Standard,
                    row.Kind,
                    row.Title,
                    row.OrdinalN,
                    row.TotalM,
                    row.GroupPath[^1],
                    row.ConnectionCount,
                    row.ColorName,
                    Marked: false))).Text,
            spoken.Text);
        document.Shutdown();
    }

    // --- A13: activation --------------------------------------------------

    [Fact]
    public void ActivatingATextCardPublishesCoresCardText()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();

        Assert.Equal(
            CanvasActivation.DetailShown, document.Activate(Row(document, "question")));
        Assert.Equal("# Core question\nCan it be accessible?", document.DetailText);
        Assert.Equal(Row(document, "question").Title, document.DetailTitle);
        Assert.Equal("question", document.LastActivatedNode);

        document.CloseDetail();
        Assert.Null(document.DetailText);
        document.Shutdown();
    }

    [Fact]
    public void ActivatingAGroupTellsTheViewToExpand()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        Assert.Equal(
            CanvasActivation.ExpandGroup, document.Activate(Row(document, "grp")));
        document.Shutdown();
    }

    [Fact]
    public void ActivatingAFileCardOpensTheNoteAtTheSubpathAnchor()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        (string Path, LinkAnchor? Anchor)? opened = null;
        document.OpenFileCardFromSurface = (path, anchor) =>
        {
            opened = (path, anchor);
            return true;
        };

        Assert.Equal(
            CanvasActivation.Navigated, document.Activate(Row(document, "note")));
        Assert.Equal("note0.md", opened!.Value.Path);
        // The W3-5 anchor resolution: `#Heading` lands at the heading,
        // not the note top (contract A13).
        Assert.Equal("heading", opened.Value.Anchor!.Kind);
        Assert.Equal("Note 0", opened.Value.Anchor.Text);
        Assert.Equal("note", document.LastActivatedNode);
        document.Shutdown();
    }

    [Fact]
    public void ActivatingALinkCardGoesThroughTheAllowlistAndSpeaksTheVocabulary()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        _announced.Clear();
        string? launched = null;
        document.OpenExternalLinkFromSurface = target =>
        {
            launched = target;
            return true;
        };

        Assert.Equal(CanvasActivation.Opened, document.Activate(Row(document, "link")));
        Assert.Equal("https://example.org/spec", launched);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasOpened(
                    Row(document, "link").Title, CanvasOpenTarget.Browser))).Text,
            _announced[^1].Text);
        document.Shutdown();
    }

    [Fact]
    public void ALinkOutsideTheAllowlistIsRefusedWithTheVocabularysReason()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "hostile.canvas"),
            """
            {"nodes":[{"id":"js","type":"link","url":"javascript:alert(1)","x":0,"y":0,"width":10,"height":10}],"edges":[]}
            """);
        CanvasDocumentViewModel document = NewDocument("hostile.canvas");
        document.Load();
        _announced.Clear();
        bool launched = false;
        document.OpenExternalLinkFromSurface = _ =>
        {
            launched = true;
            return true;
        };

        Assert.Equal(CanvasActivation.Refused, document.Activate(Row(document, "js")));
        Assert.False(launched);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasBlocked(
                    new CanvasBlockedReason.NotAUrl()))).Text,
            _announced[^1].Text);
        document.Shutdown();
    }

    [Fact]
    public void AFileCardWithNoVaultTargetSaysSoAndStaysNavigable()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "absent.canvas"),
            """
            {"nodes":[{"id":"gone","type":"file","file":"nowhere/missing.md","x":0,"y":0,"width":10,"height":10}],"edges":[]}
            """);
        CanvasDocumentViewModel document = NewDocument("absent.canvas");
        document.Load();
        _announced.Clear();
        bool navigated = false;
        document.OpenFileCardFromSurface = (_, _) =>
        {
            navigated = true;
            return true;
        };

        Assert.Equal(CanvasActivation.Refused, document.Activate(Row(document, "gone")));
        Assert.False(navigated);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasFileNotFound("nowhere/missing.md"))).Text,
            _announced[^1].Text);
        // t0 §5: the row is still there to select.
        document.SelectNode("gone");
        Assert.Equal("gone", document.Selection.Selected);
        document.Shutdown();
    }

    // --- A11: connections --------------------------------------------------

    [Fact]
    public void FollowingAConnectionSelectsTheOtherCardAndNarratesTheMove()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        document.SelectNode("question", announce: false);
        CanvasNeighbor neighbor = Assert.Single(document.NeighborsOf("question"));
        _announced.Clear();

        document.FollowConnection(neighbor);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal("evidence", document.Selection.Selected);
        Assert.NotEmpty(_announced);
        document.Shutdown();
    }

    // --- A8–A14: the outline projection ------------------------------------

    [Fact]
    public void TheOutlineNestsCoresDepthAndNamesRowsFromCoresParts() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var view = new CanvasOutlineView { Model = document };

        // The group is a root; its members are its children (0b-8's
        // tree, projected — no host containment math).
        CanvasOutlineRowViewModel group =
            Assert.Single(view.RootsForTests, row => row.Id == "grp");
        Assert.True(group.IsGroup);
        Assert.Contains(group.Children, child => child.Id == "question");
        Assert.DoesNotContain(view.RootsForTests, row => row.Id == "question");
        Assert.Contains(view.RootsForTests, row => row.Id == "loose");

        CanvasOutlineRow row = Row(document, "question");
        // Contract A9: core's kind word + core's speakable_name.
        Assert.Equal(
            CanvasPhrase.CardReference(row.Kind, row.SpeakableName),
            Assert.Single(group.Children, child => child.Id == "question").Name);
        // Contract A10: the t0 §3 inspectability slot.
        Assert.Equal(
            CanvasPhrase.RowStatus(
                row.OrdinalN, row.TotalM, row.GroupPath[^1], row.ColorName, marked: false),
            Assert.Single(group.Children, child => child.Id == "question").Status);
        Assert.Equal(
            CanvasPhrase.ActivationHint("text"),
            Assert.Single(group.Children, child => child.Id == "question").Hint);
        document.Shutdown();
    });

    [Fact]
    public void TheSelectedCardsConnectionRowsAreCoreRenderedAndComeFirst() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var view = new CanvasOutlineView { Model = document };
        document.SelectNode("question");

        CanvasOutlineRowViewModel group =
            Assert.Single(view.RootsForTests, row => row.Id == "grp");
        CanvasOutlineRowViewModel question =
            Assert.Single(group.Children, child => child.Id == "question");
        CanvasOutlineRowViewModel connection = question.Children[0];
        Assert.True(connection.IsConnection);
        Assert.True(question.IsExpanded);
        CanvasNeighbor neighbor = Assert.Single(document.NeighborsOf("question"));
        // CD-14: the row reads the SAME traversal event the navigator
        // speaks — one render, no second composition.
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasConnectionTraversed(
                    neighbor.Direction,
                    Row(document, neighbor.OtherNode).Kind,
                    neighbor.OtherTitle,
                    neighbor.Label))).Text,
            connection.Name);

        // Moving selection away takes the rows with it.
        document.SelectNode("evidence");
        Assert.DoesNotContain(question.Children, child => child.IsConnection);
        document.Shutdown();
    });

    /// <summary>
    /// Contract A11: a connection row is a READING position, not canvas
    /// selection state. Arrowing onto one must leave the model alone —
    /// following there rebuilt the selected card's children out from
    /// under the cursor, so the direction phrase a screen reader was
    /// about to speak was gone before it spoke it, and the row could
    /// never be read at all. Invoke and Enter are the follow path
    /// (mac's `returnOpensRow` split).
    /// </summary>
    [Fact]
    public void ArrowingOntoAConnectionRowLeavesItReadable() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        CanvasOutlineView view = surface.OutlineForTests;

        document.SelectNode("question");
        host.UpdateLayout();
        CanvasOutlineRowViewModel group =
            Assert.Single(view.RootsForTests, row => row.Id == "grp");
        CanvasOutlineRowViewModel question =
            Assert.Single(group.Children, child => child.Id == "question");
        CanvasOutlineRowViewModel connection = question.Children[0];
        Assert.True(connection.IsConnection);
        string name = connection.Name;
        Assert.NotEmpty(name);
        // Drain the move that seated the selection BEFORE clearing:
        // clearing the recorder does not empty the coalescer, and its
        // pending navigation line would land in the middle of the
        // assertion below and read as an arrow-key announcement.
        document.AnnouncerForTests.FlushForTests();
        _announced.Clear();

        // The arrow key's effect: the tree selects the row.
        connection.IsSelected = true;
        host.UpdateLayout();

        // The row is still there, still under the same card, still
        // carrying the direction phrase a reader is about to speak.
        Assert.Same(connection, question.Children[0]);
        Assert.Equal(name, connection.Name);
        Assert.Equal(
            CanvasPhrase.ConnectionStatus(1, document.NeighborsOf("question").Count),
            connection.Status);
        // The canvas selection did not move, and nothing was said.
        Assert.Equal("question", document.Selection.Selected);
        document.AnnouncerForTests.FlushForTests();
        Assert.Empty(_announced);

        // Invoke IS the follow path, and it does move.
        connection.RaiseActivate();
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal("evidence", document.Selection.Selected);
        Assert.NotEmpty(_announced);
        document.Shutdown();
    });

    [Fact]
    public void SelectionFlowsBothWaysWithoutAnEcho() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        // A LAID-OUT tree, so the containers exist and the real
        // binding → container → SelectedItemChanged path runs.
        using var host = Host(surface);
        CanvasOutlineView view = surface.OutlineForTests;
        CanvasOutlineRowViewModel group =
            Assert.Single(view.RootsForTests, row => row.Id == "grp");
        CanvasOutlineRowViewModel evidence =
            Assert.Single(group.Children, child => child.Id == "evidence");
        CanvasOutlineRowViewModel loose =
            Assert.Single(view.RootsForTests, row => row.Id == "loose");

        // Model → view.
        _announced.Clear();
        document.SelectNode("evidence");
        Assert.True(evidence.IsSelected);
        document.AnnouncerForTests.FlushForTests();
        int afterModelMove = _announced.Count;
        Assert.True(afterModelMove > 0);

        // View → model: one move announced, not two — the model's
        // re-seat must not echo back through the tree (contract A12).
        loose.IsSelected = true;
        host.UpdateLayout();
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal("loose", document.Selection.Selected);
        Assert.Equal(afterModelMove + 1, _announced.Count);
        document.Shutdown();
    });

    [Fact]
    public void TheOutlineTreeIsVirtualizedTheUiaSafeWay() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var view = new CanvasOutlineView { Model = document };

        Assert.True(VirtualizingStackPanel.GetIsVirtualizing(view.TreeForTests));
        // W4-1's UIA-safe setting: Recycling re-uses one automation peer
        // for different rows (contract A8).
        Assert.Equal(
            VirtualizationMode.Standard,
            VirtualizingStackPanel.GetVirtualizationMode(view.TreeForTests));
        document.Shutdown();
    });

    /// <summary>
    /// Contract A8's UIA surface, read the way ASSISTIVE TECHNOLOGY reads
    /// it: down <c>treePeer.GetChildren()</c> to the row peers that are
    /// actually projected into the UIA tree, asserting every pattern on
    /// those — Tree/TreeItem, ExpandCollapse and SelectionItem from WPF,
    /// and Invoke from <c>CanvasOutlineRowDataPeer</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The previous version of this fact interrogated the CONTAINER peer
    /// (<c>CreatePeerForElement(container)</c>) and passed for three
    /// rounds while the shipped surface was broken. WPF projects each row
    /// as a <c>TreeViewDataItemAutomationPeer</c>, which implements
    /// SelectionItem/ExpandCollapse itself and does NOT forward a custom
    /// Invoke, so a screen reader saw <c>invoke=NULL</c> on every row
    /// while this test read a peer no client ever sees.
    /// </para>
    /// <para>
    /// That is the supplies-its-own-mechanism false-green class, FOURTH
    /// instance in this PR, and the reason it survived codex round 6's
    /// verdict is that the journey which would have caught it had never
    /// once run past its fixture lookup. The rule this restates: assert on
    /// the surface the consumer reads, not the object you constructed.
    /// Mutation-verified — reverting either <c>CreateItemAutomationPeer</c>
    /// override fails this on <c>invoke=NULL</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheTreeItemsCarryTreeSelectionItemExpandCollapseAndInvoke() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        CanvasOutlineView view = surface.OutlineForTests;

        AutomationPeer treePeer = Assert.IsAssignableFrom<AutomationPeer>(
            UIElementAutomationPeer.CreatePeerForElement(view.TreeForTests));
        Assert.Equal(AutomationControlType.Tree, treePeer.GetAutomationControlType());
        Assert.Equal(CanvasPhrase.OutlineName, treePeer.GetName());

        // The PRODUCTION topology: what a UIA client walks.
        List<AutomationPeer> rowPeers = treePeer.GetChildren() ?? [];
        Assert.NotEmpty(rowPeers);

        CanvasOutlineRowViewModel group =
            Assert.Single(view.RootsForTests, row => row.Id == "grp");
        AutomationPeer groupPeer = Assert.Single(
            rowPeers, peer => peer.GetName() == group.Name);

        // Every row peer a client sees is the data-item peer, not the
        // container peer — pinning the topology itself, so a refactor
        // that reverts to container peers is caught here and not by a
        // screen-reader user.
        Assert.All(
            rowPeers,
            peer => Assert.IsType<CanvasOutlineRowDataPeer>(peer));

        Assert.Equal(AutomationControlType.TreeItem, groupPeer.GetAutomationControlType());
        Assert.Equal(group.Status, groupPeer.GetItemStatus());
        Assert.Equal(group.Hint, groupPeer.GetHelpText());
        Assert.NotNull(groupPeer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.NotNull(groupPeer.GetPattern(PatternInterface.SelectionItem));
        // The one WPF gives neither the container nor the data peer.
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(
            groupPeer.GetPattern(PatternInterface.Invoke));

        // And it really activates: Invoke on a group expands it.
        group.IsExpanded = false;
        invoke.Invoke();
        Assert.True(group.IsExpanded);

        // NESTED rows are projected by the ITEM peer, so they need the
        // same override one level down — the half of the fix a top-level
        // assertion alone would miss. The group's peer children also
        // include its template's expander ToggleButton, so the ROW peers
        // are selected by type rather than by position.
        group.IsExpanded = true;
        surface.UpdateLayout();
        List<CanvasOutlineRowDataPeer> nestedRows =
            (groupPeer.GetChildren() ?? [])
                .OfType<CanvasOutlineRowDataPeer>()
                .ToList();
        Assert.NotEmpty(nestedRows);
        Assert.All(
            nestedRows,
            row => Assert.IsAssignableFrom<IInvokeProvider>(
                row.GetPattern(PatternInterface.Invoke)));

        document.Shutdown();
    });

    /// <summary>
    /// Contract A14 end to end, through the PRODUCTION trigger: opening
    /// a canvas from the workspace lands keyboard focus on a realized
    /// outline row, and coming back from an activated card lands on
    /// THAT row (WCAG 2.4.3).
    /// </summary>
    /// <remarks>
    /// This fact used to call <c>FocusLandingRow()</c> itself, which is
    /// manufacturing the delivery it claims to observe: it would have
    /// passed with every open site's focus call deleted. It drives
    /// <c>OpenPath</c> now — the real user route — with MainWindow's own
    /// subscriber attached, so the two focus authorities actually
    /// compete. Mutation-verified: removing the funnel's canvas request
    /// fails it.
    /// </remarks>
    [Fact]
    public void OpeningACanvasFromTheWorkspaceLandsFocusOnARealizedRow() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        using var host = new FocusHarness(workspace);
        workspace.OpenPath("board.canvas");
        host.Pump();

        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        // Delivered: the request is consumed and focus is on a row.
        Assert.Null(document.FocusRequest);
        CanvasOutlineRowViewModel landed = Assert.IsType<CanvasOutlineRowViewModel>(
            host.FocusedRow());
        Assert.Equal(document.Outline[0].NodeId, landed.Id);
        Assert.Equal(document.Outline[0].NodeId, document.Selection.Selected);
        // The pane-focus event DID fire, so the canvas keeping focus is
        // not an artefact of the competing authority never being invoked.
        Assert.NotEmpty(host.PaneFocusRequests);

        // WCAG 2.4.3: activating a card records the row; coming back
        // through the same funnel lands on it, not the top.
        document.OpenFileCardFromSurface = (_, _) => true;
        _ = document.Activate(Row(document, "note"));
        workspace.RequestActiveEditorFocus();
        host.Pump();
        Assert.Null(document.FocusRequest);
        CanvasOutlineRowViewModel restored = Assert.IsType<CanvasOutlineRowViewModel>(
            host.FocusedRow());
        Assert.Equal("note", restored.Id);
    });

    /// <summary>
    /// The request is STATE, so a surface that mounts LATE still gets
    /// it: the edge-triggered version delivered to whoever happened to
    /// be subscribed at the instant, and a view created afterwards never
    /// heard it at all.
    /// </summary>
    [Fact]
    public void AFocusRequestSurvivesUntilASurfaceCanActuallyDeliverIt() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);

        // Asked for before any surface exists at all.
        document.RequestFocusLanding(tab);
        Assert.NotNull(document.FocusRequest);

        var surface = new CanvasSurfaceView { DataContext = tab, Model = document };
        using var host = Host(surface);

        // Mounting delivered it.
        Assert.Null(document.FocusRequest);
        Assert.NotNull(FocusedRow(host));
    });

    /// <summary>
    /// A row whose container is virtualized away is REALIZED before
    /// focus goes to it, and an unrealizable one does not consume the
    /// request. <c>_tree.Focus()</c> used to stand in for both,
    /// reporting success while the row was never reached.
    /// </summary>
    [Fact]
    public void FocusRealizesADeeplyVirtualizedRowRatherThanFakingIt() => RunSta(() =>
    {
        File.Copy(
            Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures",
                "canvas", "large_2000.canvas"),
            Path.Combine(_fixture.Root, "large.canvas"),
            overwrite: true);
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("large.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        var surface = new CanvasSurfaceView { DataContext = tab, Model = document };
        using var host = Host(surface);

        // A row far past the first viewport — its container cannot
        // already exist under virtualization.
        string deep = document.Outline[^1].NodeId;
        document.RequestFocusLanding(tab, deep);
        host.UpdateLayout();

        Assert.Null(document.FocusRequest);
        CanvasOutlineRowViewModel landed = Assert.IsType<CanvasOutlineRowViewModel>(
            FocusedRow(host));
        Assert.Equal(deep, landed.Id);
    });

    /// <summary>
    /// A request naming a row this document does not have falls back to
    /// the document's own answer rather than being consumed by nothing.
    /// </summary>
    [Fact]
    public void AFocusRequestForAnUnknownRowFallsBackToTheFirst() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        var surface = new CanvasSurfaceView { DataContext = tab, Model = document };
        using var host = Host(surface);

        document.RequestFocusLanding(tab, "no-such-node");
        host.UpdateLayout();
        Assert.Null(document.FocusRequest);
        Assert.Equal(
            document.Outline[0].NodeId,
            Assert.IsType<CanvasOutlineRowViewModel>(FocusedRow(host)).Id);
    });

    /// <summary>
    /// A window hosting the active tab's canvas surface AND MainWindow's
    /// own focus subscriber, so the two authorities compete exactly as
    /// they do in the app (contract A14).
    /// </summary>
    private sealed class FocusHarness : IDisposable
    {
        private readonly Window _window;
        private readonly ContentControl _content;
        private readonly WorkspaceViewModel _workspace;
        private readonly List<WorkspaceGroupViewModel> _paneFocusRequests = [];

        internal FocusHarness(WorkspaceViewModel workspace)
        {
            _workspace = workspace;
            _content = new ContentControl();
            _window = new Window
            {
                Content = _content,
                Width = 900,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            // MainWindow's subscriber. Recording the call proves only
            // that the workspace RAISED it — that the canvas keeping
            // focus is not an artefact of the event never firing. What
            // the production handler then does with a canvas tab is a
            // source fact
            // (`TheEditorPaneFocusFallbackStandsAsideForACanvasTab`),
            // because MainWindow is not reachable from this project.
            workspace.EditorPaneFocusRequested += OnPaneFocusRequested;
        }

        internal IReadOnlyList<WorkspaceGroupViewModel> PaneFocusRequests =>
            _paneFocusRequests;

        private void OnPaneFocusRequested(object? sender, WorkspaceGroupViewModel group) =>
            _paneFocusRequests.Add(group);

        /// <summary>Mount the active tab's canvas surface the way the
        /// tab template does — DataContext is the tab.</summary>
        internal void Pump()
        {
            WorkspaceTabViewModel? tab = _workspace.ActiveGroup.ActiveTab;
            if (_content.Content is not CanvasSurfaceView surface
                || !ReferenceEquals(surface.DataContext, tab))
            {
                surface = new CanvasSurfaceView { DataContext = tab, Model = tab?.Canvas };
                _content.Content = surface;
            }
            else
            {
                surface.Model = tab?.Canvas;
            }
            _window.UpdateLayout();
            // Drain Loaded/Render-priority work: a surface added to a
            // shown window is Loaded asynchronously, and the delivery
            // retries on that. The app's dispatcher does this by
            // running; a test has to ask.
            DrainDispatcher();
        }

        /// <summary>Run everything queued at Loaded priority or
        /// above — the pass the app gets for free.</summary>
        private static void DrainDispatcher()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => frame.Continue = false);
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        internal CanvasOutlineRowViewModel? FocusedRow() =>
            (System.Windows.Input.FocusManager.GetFocusedElement(_window)
                as FrameworkElement)?.DataContext as CanvasOutlineRowViewModel;

        public void Dispose()
        {
            _workspace.EditorPaneFocusRequested -= OnPaneFocusRequested;
            _window.Close();
        }
    }

    /// <summary>
    /// An unrealizable container does NOT consume the request. This is
    /// the property the old <c>_tree.Focus()</c> fallback destroyed: it
    /// reported success, the request was cleared, focus sat on the tree,
    /// and the row was never read — with nothing left to retry.
    /// </summary>
    /// <remarks>
    /// Driven on a view that has a model and rows but no visual tree, so
    /// no container can exist. Mutation-verified: restoring the fallback
    /// makes this return the row.
    /// </remarks>
    [Fact]
    public void AnUnrealizableRowIsNotReportedAsDelivered() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        // Never hosted: the generator has produced nothing.
        var view = new CanvasOutlineView { Model = document };
        Assert.NotEmpty(view.RootsForTests);

        Assert.Null(view.DeliverFocus(document.Outline[0].NodeId));
        document.Shutdown();
    });

    /// <summary>
    /// And the surface leaves the request PENDING when delivery fails,
    /// so the next realization can still deliver it.
    /// </summary>
    [Fact]
    public void AFailedDeliveryLeavesTheRequestPendingForTheNextTry() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);

        // A surface that is not in any window: IsVisible is false, so
        // nothing can be delivered.
        var offscreen = new CanvasSurfaceView { DataContext = tab, Model = document };
        document.RequestFocusLanding(tab);
        Assert.NotNull(document.FocusRequest);
        _ = offscreen;

        // A surface that IS hosted delivers the same still-pending
        // request.
        var hosted = new CanvasSurfaceView { DataContext = tab, Model = document };
        using var host = Host(hosted);
        Assert.Null(document.FocusRequest);
        Assert.NotNull(FocusedRow(host));
    });

    /// <summary>The LOGICAL focus target's row — keyboard focus needs
    /// an activated window, which a headless unit lane cannot promise;
    /// logical focus is what <c>UIElement.Focus</c> sets either
    /// way.</summary>
    private static CanvasOutlineRowViewModel? FocusedRow(HostedWindow host) =>
        (host.FocusedElement() as FrameworkElement)?.DataContext
            as CanvasOutlineRowViewModel;

    [Fact]
    public void TheSurfaceRendersTheStateRegionsAndHidesTheTreeWhenThereIsNoTree() =>
        RunSta(() =>
        {
            CanvasDocumentViewModel broken = NewDocument("broken.canvas");
            broken.Load();
            var surface = new CanvasSurfaceView { Model = broken };
            Assert.Equal(Visibility.Collapsed, surface.OutlineForTests.Visibility);
            Assert.Equal(Visibility.Collapsed, surface.WarningRowsForTests.Visibility);
            broken.Shutdown();

            CanvasDocumentViewModel skipped = NewDocument("skipped.canvas");
            skipped.Load();
            surface.Model = skipped;
            Assert.Equal(Visibility.Visible, surface.OutlineForTests.Visibility);
            Assert.Equal(Visibility.Visible, surface.DegradedBannerForTests.Visibility);
            Assert.Equal(skipped.DegradedBannerText, surface.DegradedBannerForTests.Text);
            // t0 §5's focusable detail rows: EVERY warning, which is
            // wider than the banner's skipped-entry count on purpose.
            Assert.Equal(Visibility.Visible, surface.WarningRowsForTests.Visibility);
            Assert.Equal(
                skipped.Warnings.Count,
                surface.WarningRowsForTests.ItemsSource.Cast<object>().Count());
            Assert.True(skipped.Warnings.Count >= skipped.PreservedItemCount);
            skipped.Shutdown();

            CanvasDocumentViewModel blank = NewDocument("blank.canvas");
            blank.Load();
            surface.Model = blank;
            Assert.Equal(Visibility.Visible, surface.OnboardingForTests.Visibility);
            Assert.Equal(blank.EmptyOnboardingText, surface.OnboardingForTests.Text);
            // The onboarding region is reachable by keyboard, not decor.
            Assert.True(surface.OnboardingForTests.Focusable);
            blank.Shutdown();
        });

    [Fact]
    public void TheSurfaceSwitcherIsNamedAndTheUnshippedArmIsDisabled() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };

        // W6-1 PR B shipped the table, so its arm is live now; the
        // visual arm is PR D's and stays disabled with its reason.
        Assert.True(surface.TableChoiceForTests.IsEnabled);
        Assert.False(surface.VisualChoiceForTests.IsEnabled);
        Assert.Equal(
            CanvasPhrase.VisualShipsLater,
            System.Windows.Automation.AutomationProperties.GetHelpText(
                surface.VisualChoiceForTests));
        document.Shutdown();
    });

    [Fact]
    public void EscapeClosesTheInterimDetailSoItIsNeverAKeyboardTrap() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);
        _ = document.Activate(Row(document, "question"));
        Assert.Equal(document.DetailText, surface.DetailForTests.Text);

        PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
            PresentationSource.FromVisual(surface.DetailForTests));
        surface.DetailForTests.RaiseEvent(new System.Windows.Input.KeyEventArgs(
            System.Windows.Input.Keyboard.PrimaryDevice,
            source,
            0,
            System.Windows.Input.Key.Escape)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        });
        Assert.Null(document.DetailText);
        document.Shutdown();
    });

    // --- A15: persistence ---------------------------------------------------

    [Fact]
    public void TheActiveSurfaceTokenRoundTripsAndOutlineStaysAbsent()
    {
        using (WorkspaceViewModel workspace = NewWorkspace())
        {
            workspace.OpenPath("board.canvas");
            WorkspaceTabViewModel tab =
                Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
            CanvasDocumentViewModel document =
                Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
            // Outline is the ABSENT default (the mac sparse-map shape).
            Assert.Null(tab.ActiveCanvasSurface);

            document.ShowSurface(CanvasSurfaceKind.Table);
            Assert.Equal("table", tab.ActiveCanvasSurface);
        }

        string persisted = File.ReadAllText(
            Path.Combine(_fixture.Root, ".slate", "workspace.json"));
        Assert.Contains("\"activeCanvasSurface\": \"table\"", persisted);

        using WorkspaceViewModel restored = NewWorkspace();
        WorkspaceTabViewModel restoredTab = Assert.Single(
            restored.Groups.SelectMany(group => group.Tabs), tab => tab.IsCanvas);
        Assert.Equal("table", restoredTab.ActiveCanvasSurface);
        Assert.Equal(
            CanvasSurfaceKind.Table, restoredTab.Canvas!.Selection.ActiveSurface);

        // Back to outline: the key leaves the file entirely.
        restoredTab.Canvas.ShowSurface(CanvasSurfaceKind.Outline);
        Assert.Null(restoredTab.ActiveCanvasSurface);
        Assert.DoesNotContain(
            "activeCanvasSurface",
            File.ReadAllText(Path.Combine(_fixture.Root, ".slate", "workspace.json")));
    }

    [Fact]
    public void AnUnrecognizedSurfaceTokenStillCollapsesToOutline()
    {
        // The forward-compat drop the spec asks PR A to keep passing:
        // the writer only ever emits "table"/"visual", so anything else
        // a future build wrote reads back as the outline default.
        Directory.CreateDirectory(Path.Combine(_fixture.Root, ".slate"));
        var id = Guid.NewGuid();
        var group = Guid.NewGuid();
        File.WriteAllText(
            Path.Combine(_fixture.Root, ".slate", "workspace.json"),
            "{\"version\":1,\"activeGroup\":\"" + group + "\","
            + "\"activeLeaf\":\"outline\","
            + "\"root\":{\"kind\":\"group\",\"id\":\"" + group + "\","
            + "\"activeTab\":\"" + id + "\",\"tabs\":["
            + "{\"id\":\"" + id + "\","
            + "\"item\":{\"kind\":\"canvas\",\"path\":\"board.canvas\"},"
            + "\"activeCanvasSurface\":\"hologram\"}]}}");

        using WorkspaceViewModel workspace = NewWorkspace();
        WorkspaceTabViewModel tab = Assert.Single(
            workspace.Groups.SelectMany(candidate => candidate.Tabs));
        Assert.True(tab.IsCanvas);
        Assert.Null(tab.ActiveCanvasSurface);
        Assert.Equal(CanvasSurfaceKind.Outline, tab.Canvas!.Selection.ActiveSurface);
    }

    // --- A19: the close gate ------------------------------------------------

    [Fact]
    public void ClosingACanvasTabNeverConsultsTheDirtyCloseGate()
    {
        bool consulted = false;
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            dirtyCloseDecision: _ =>
            {
                consulted = true;
                return WorkspaceDirtyNavigationDecision.Cancel;
            },
            startInteractionBackgroundWork: false,
            announceRendered: _announced.Add);
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.False(tab.IsDirty);

        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);
        Assert.False(consulted);
        Assert.Empty(workspace.ActiveGroup.Tabs);
    }

    // --- A18: the three surface commands ------------------------------------

    /// <summary>
    /// All three register so the palette lists the whole switcher from
    /// this slice; the one whose projection has not shipped resolves to
    /// a command whose CanExecute is false, so the registrar answers its
    /// canonical unavailable sentence rather than a per-PR string
    /// (contract A18).
    /// </summary>
    /// <remarks>
    /// Renamed in W6-1 PR B: `showTable` shipped its projection there
    /// and is enabled from that slice on (contract B10), so the shape
    /// this fact pins is now one unshipped arm, not two.
    /// `ShowTableIsEnabledAndDrivesTheOneSurfaceSwitch` in
    /// <c>CanvasTableTests</c> owns the other half.
    /// </remarks>
    [Fact]
    public void ShowVisualRegistersAndStaysDisabledUntilItsProjectionShips()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        var host = new CanvasCommandHost(workspace);

        foreach (string id in new[]
        {
            Commands.ChordTable.Ids.CanvasShowOutline,
            Commands.ChordTable.Ids.CanvasShowTable,
            Commands.ChordTable.Ids.CanvasShowVisual,
        })
        {
            Commands.ChordTableEntry row = Assert.IsType<Commands.ChordTableEntry>(
                Commands.ChordTable.Find(id));
            Assert.True(row.IsRegistered, $"{id} must be a registered row");
            Assert.Equal(CommandSection.Canvas, row.Section);
            // No chord in PR A: the switcher is a visible control and
            // the palette is always a path (rule R1), so Reg's own rule
            // gives the row ChordScope.None.
            Assert.Null(row.WindowsChord);
            Assert.Equal(Commands.ChordScope.None, row.Scope);
            Assert.Contains(id, Commands.SlateCommandRegistrar.ResolvableIds);
        }

        Assert.Null(Commands.SlateCommandRegistrar.DisabledReason(
            host, Commands.ChordTable.Ids.CanvasShowOutline));
        Assert.Equal(
            Commands.SlateCommandRegistrar.UnavailableReason,
            Commands.SlateCommandRegistrar.DisabledReason(
                host, Commands.ChordTable.Ids.CanvasShowVisual));

        // The one that IS shipped switches the shared surface and
        // speaks core's sentence.
        _announced.Clear();
        Commands.SlateCommandRegistrar
            .Resolve(host, Commands.ChordTable.Ids.CanvasShowOutline)!
            .Execute(null);
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Equal(CanvasSurfaceKind.Outline, document.Selection.ActiveSurface);
        // Already on the outline, so the switch is a no-op and silent.
        document.AnnouncerForTests.FlushForTests();
        Assert.Empty(_announced);

        document.ShowSurface(CanvasSurfaceKind.Table);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasSurfaceShown(CanvasSurfaceKind.Table))).Text,
            _announced[^1].Text);
    }

    /// <summary>Every canvas command dies with the vault: the tab and
    /// the surface are gone, so a resolver that still answered would
    /// hand the palette a command over a disposed session.</summary>
    [Fact]
    public void CanvasCommandsAreUnavailableWithNoCanvasTab()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("note0.md");
        var host = new CanvasCommandHost(workspace);
        Assert.Equal(
            Commands.SlateCommandRegistrar.UnavailableReason,
            Commands.SlateCommandRegistrar.DisabledReason(
                host, Commands.ChordTable.Ids.CanvasShowOutline));
    }

    // --- A1/A17: vault-close teardown ----------------------------------------

    /// <summary>
    /// Vault close tears every canvas document down (spec behavior 1):
    /// each holds the shared session and a native handle, and the
    /// session is disposed right after this returns.
    /// </summary>
    [Fact]
    public void DisposingTheWorkspaceShutsDownEveryCanvasDocument()
    {
        var workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel first = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        ((System.Windows.Input.ICommand)workspace.SplitRightCommand).Execute(null);
        workspace.OpenPath("skipped.canvas");
        CanvasDocumentViewModel second = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.NotSame(first, second);

        workspace.Dispose();

        // A shut-down scheduler refuses every body, so a post-teardown
        // Load cannot reopen a handle over the dying session.
        foreach (CanvasDocumentViewModel document in new[] { first, second })
        {
            CanvasLoadState before = document.State;
            document.Load();
            Assert.Equal(before, document.State);
            Assert.True(document.WhenHandleClosed().IsCompleted);
        }
    }

    /// <summary>The command bridge's host over a live workspace — the
    /// registrar resolves through <c>Workspace</c>, so a null-workspace
    /// stub could not see these rows at all.</summary>
    internal sealed class CanvasCommandHost(WorkspaceViewModel workspace)
        : Commands.ISlateCommandHost
    {
        public WorkspaceViewModel? Workspace => workspace;

        public FilesSidebarViewModel? FileSidebar => null;

        public QuickSwitcherViewModel? QuickSwitcher => null;

        public bool IsVaultOpen => true;

        public System.Windows.Input.ICommand OpenVaultCommand =>
            throw new NotSupportedException();

        public System.Windows.Input.ICommand CloseVaultCommand =>
            throw new NotSupportedException();

        public System.Windows.Input.ICommand ToggleSearchCommand =>
            throw new NotSupportedException();
    }

    // --- B2: a disk change the shell made must reach the surface -----------

    /// <summary>
    /// The site named "reload open tab from disk" reloads a canvas from
    /// disk. Attach is a registry HIT for an already-open path and a hit
    /// returns the document exactly as it stands, so the outline used to
    /// keep rendering the PRE-restore rows right after this shell
    /// announced the restore.
    /// </summary>
    /// <remarks>
    /// Driven at the reload site rather than through the whole History
    /// restore: W4-7's restore carries its own preconditions for a
    /// non-markdown tab (the CAS basis is the history head hash), and a
    /// `.canvas` tab does not satisfy them end to end in this harness —
    /// an observation about W4-7's path, recorded rather than worked
    /// around, and orthogonal to whether THIS site reloads.
    /// </remarks>
    [Fact]
    public void RestoringAVersionReloadsAnOpenCanvasTab()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Contains(document.Outline, row => row.NodeId == "question");
        document.SelectNode("evidence", announce: false);

        // What a restore does: the bytes on disk change under an open
        // tab, and the shell then routes its reload site.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas"),
            """
            {
              "nodes": [
                {"id":"evidence","type":"text","text":"Evidence so far","x":0,"y":0,"width":220,"height":140},
                {"id":"after","type":"text","text":"Restored body","x":0,"y":200,"width":240,"height":140}
              ],
              "edges": []
            }
            """);
        workspace.ReloadOpenTabFromDiskForTests("board.canvas");

        // Same shared document object — and it now agrees with disk.
        Assert.Same(document, workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Contains(document.Outline, row => row.NodeId == "after");
        Assert.DoesNotContain(document.Outline, row => row.NodeId == "question");
        // Selection survives where the node did (the reload keeps it).
        Assert.Equal("evidence", document.Selection.Selected);
    }

    /// <summary>The registry hit is what made the stale render
    /// possible, and it is still a hit: the reload must work THROUGH
    /// the shared document, not by replacing it.</summary>
    [Fact]
    public void TheReloadKeepsTheSharedDocumentObject()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        ((System.Windows.Input.ICommand)workspace.DuplicateTabCommand).Execute(null);
        WorkspaceTabViewModel second =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel shared =
            Assert.IsType<CanvasDocumentViewModel>(first.Canvas);
        Assert.Same(shared, second.Canvas);

        workspace.ReloadOpenTabFromDiskForTests("board.canvas");

        Assert.Same(shared, first.Canvas);
        Assert.Same(shared, second.Canvas);
        Assert.Equal(CanvasLoadState.Ready, shared.State);
    }

    // --- M1: activation routes on the TARGET, not the kind -----------------

    /// <summary>
    /// An image card opens in its default app, never in a Markdown
    /// editor tab. `ItemForPath` calls every extension that is not
    /// `.canvas`/`.base` Markdown, so routing image cards through the
    /// note-open seam replaced the canvas tab with an editor over the
    /// PNG's bytes. The mac reference routes on the target's extension
    /// (`CanvasContainerView.swift:168–187`), and the vocabulary's
    /// `CanvasOpenTarget.DefaultApp` arm exists for exactly this.
    /// </summary>
    [Fact]
    public void ActivatingAnImageCardOpensItInTheDefaultAppNotAnEditorTab()
    {
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "diagram.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "media.canvas"),
            """
            {"nodes":[
              {"id":"pic","type":"file","file":"diagram.png","x":0,"y":0,"width":10,"height":10},
              {"id":"doc","type":"file","file":"note0.md","x":0,"y":40,"width":10,"height":10}
            ],"edges":[]}
            """);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);

        CanvasDocumentViewModel document = NewDocument("media.canvas");
        document.Load();
        _announced.Clear();
        string? media = null;
        string? navigated = null;
        document.OpenMediaCardFromSurface = target =>
        {
            media = target;
            return true;
        };
        document.OpenFileCardFromSurface = (target, _) =>
        {
            navigated = target;
            return true;
        };

        // The image: the shell's default app, announced as such.
        Assert.Equal(CanvasActivation.Opened, document.Activate(Row(document, "pic")));
        Assert.Equal("diagram.png", media);
        Assert.Null(navigated);
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasOpened(
                    Row(document, "pic").Title, CanvasOpenTarget.DefaultApp))).Text,
            _announced[^1].Text);

        // A Markdown target still opens the note tab.
        media = null;
        Assert.Equal(CanvasActivation.Navigated, document.Activate(Row(document, "doc")));
        Assert.Equal("note0.md", navigated);
        Assert.Null(media);
        document.Shutdown();
    }

    /// <summary>The routing predicate itself — mac's `hasSuffix` test,
    /// including the case-insensitivity that makes `.MD` a note.</summary>
    [Theory]
    [InlineData("note.md", true)]
    [InlineData("NOTE.MD", true)]
    [InlineData("deep/path/note.markdown", true)]
    [InlineData("diagram.png", false)]
    [InlineData("clip.mp4", false)]
    [InlineData("no-extension", false)]
    public void MarkdownTargetsAreTheOnesThatOpenAsNotes(string target, bool expected) =>
        Assert.Equal(expected, CanvasDocumentViewModel.IsMarkdownTarget(target));

    /// <summary>A media target that escapes the vault is refused rather
    /// than handed to the shell: a `.canvas` file is untrusted input and
    /// `../../` in a file node would otherwise open anything on the
    /// disk.</summary>
    [Fact]
    public void AMediaTargetOutsideTheVaultIsNeverHandedToTheShell()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        Func<string, bool> open = Assert.IsType<Func<string, bool>>(
            document.OpenMediaCardFromSurface);

        Assert.False(open("../outside.png"));
        Assert.False(open("../../etc/passwd"));
        Assert.False(open("nowhere.png"));
    }

    /// <summary>
    /// The shell-execution gate (CD-38), through the PRODUCTION closure
    /// the workspace installs — not a stub. A benign in-vault media file
    /// reaches the opener seam with the path it should; a non-media
    /// extension never reaches it at all.
    /// </summary>
    /// <remarks>
    /// `Process.Start(UseShellExecute: true)` is `ShellExecute`, which
    /// EXECUTES what it is handed. A canvas is untrusted input — it
    /// arrives over sync, from a shared vault, from Obsidian — so a
    /// `{"type":"file","file":"setup.exe"}` node would have launched on
    /// one Enter. The stubbed activation facts above cannot see this:
    /// they replace the very closure that carries the gate.
    /// </remarks>
    [Fact]
    public void TheProductionMediaSeamOpensMediaAndRefusesEverythingElse()
    {
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "diagram.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "setup.exe"), [0x4D, 0x5A, 0x90, 0x00]);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "shell.canvas"),
            """
            {"nodes":[
              {"id":"pic","type":"file","file":"diagram.png","x":0,"y":0,"width":10,"height":10},
              {"id":"exe","type":"file","file":"setup.exe","x":0,"y":40,"width":10,"height":10}
            ],"edges":[]}
            """);

        var launched = new List<string>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: _announced.Add,
            externalOpener: target =>
            {
                launched.Add(target);
                return true;
            });
        workspace.OpenPath("shell.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        _announced.Clear();

        // Media: the real closure resolves it under the vault root and
        // hands the shell the file BY IDENTITY — the extended \\?\ form
        // that names exactly the in-vault file, compared here on OS
        // identity rather than string, which is the round-3 point.
        Assert.Equal(CanvasActivation.Opened, document.Activate(Row(document, "pic")));
        string handed = Assert.Single(launched);
        Assert.Equal(
            CanvasMediaPolicy.IdentityForTests(
                Path.Combine(_fixture.Root, "diagram.png")),
            CanvasMediaPolicy.IdentityForTests(handed));
        document.AnnouncerForTests.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasOpened(
                    Row(document, "pic").Title, CanvasOpenTarget.DefaultApp))).Text,
            _announced[^1].Text);

        // The executable: refused before the closure is consulted, and
        // audibly — never silent, never launched.
        launched.Clear();
        _announced.Clear();
        Assert.Equal(CanvasActivation.Refused, document.Activate(Row(document, "exe")));
        Assert.Empty(launched);
        document.AnnouncerForTests.FlushForTests();
        RenderedAnnouncement refusal = Assert.Single(_announced);
        Assert.Equal(A11yPriority.High, refusal.Priority);
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasActionFailed(
                    CanvasFailedAction.CanvasAction, "setup.exe"))).Text,
            refusal.Text);
    }

    /// <summary>
    /// A media file that EXISTS but cannot be opened announces a
    /// REFUSAL, never "missing from the vault" (Codoki round 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every failure of the media open below the existence check is a
    /// refusal of a present file: the containment gate rejecting a
    /// junction that escapes the vault, the TOCTOU revalidation catching
    /// a swap, ShellExecute finding no association, or — the arm driven
    /// here — the identity query failing on a volume whose filesystem
    /// does not answer <c>FileIdInfo</c>, which is CD-38's recorded
    /// NTFS/ReFS limitation and therefore a SHIPPING configuration, not a
    /// hypothetical.
    /// </para>
    /// <para>
    /// Announcing <c>CanvasFileNotFound</c> for these rendered "…is
    /// missing from the vault. Use Locate File to repoint this card." —
    /// a false statement AND a recovery that would repoint a card whose
    /// target is fine. On a FAT32/exFAT vault it fired for every media
    /// file in the canvas. Mutation-verified: swapping the arm back to
    /// <c>CanvasFileNotFound</c> fails this fact.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMediaFileThatExistsButCannotBeOpenedRefusesRatherThanClaimingItIsMissing()
    {
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "present.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "unopenable.canvas"),
            """
            {"nodes":[
              {"id":"pic","type":"file","file":"present.png","x":0,"y":0,"width":10,"height":10}
            ],"edges":[]}
            """);

        var launched = new List<string>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: _announced.Add,
            externalOpener: target =>
            {
                launched.Add(target);
                return true;
            });
        workspace.OpenPath("unopenable.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);

        // The premise: the file is genuinely THERE, so a "missing" claim
        // could only ever be false.
        Assert.True(File.Exists(Path.Combine(_fixture.Root, "present.png")));

        _announced.Clear();
        try
        {
            // The volume refuses to answer the identity query — the whole
            // production gate runs, and fails closed.
            CanvasMediaPolicy.FailIdentityQueryForTests = true;
            Assert.Equal(
                CanvasActivation.Refused, document.Activate(Row(document, "pic")));
        }
        finally
        {
            CanvasMediaPolicy.FailIdentityQueryForTests = false;
        }

        Assert.Empty(launched);
        document.AnnouncerForTests.FlushForTests();
        RenderedAnnouncement refusal = Assert.Single(_announced);
        Assert.Equal(A11yPriority.High, refusal.Priority);
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasActionFailed(
                    CanvasFailedAction.CanvasAction, "present.png"))).Text,
            refusal.Text);
        // And explicitly NOT the absence sentence, which is the wrong
        // answer this fact exists to keep out.
        Assert.NotEqual(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasFileNotFound("present.png"))).Text,
            refusal.Text);
    }

    /// <summary>
    /// The gate's set is core's `media_class`, transliterated because it
    /// is not exported — including both of core's edge rules: the
    /// BASENAME's real extension, and a dotfile like `.mov` being a
    /// hidden file rather than a video.
    /// </summary>
    [Theory]
    [InlineData("diagram.png", true)]
    [InlineData("photo.JPEG", true)]
    [InlineData("clip.mp4", true)]
    [InlineData("theme.mov", true)]
    [InlineData("song.flac", true)]
    [InlineData("deep/path/art.webp", true)]
    [InlineData("deep\\path\\art.avif", true)]
    [InlineData("setup.exe", false)]
    [InlineData("run.bat", false)]
    [InlineData("script.ps1", false)]
    [InlineData("payload.lnk", false)]
    [InlineData("archive.zip", false)]
    [InlineData("doc.pdf", false)]
    // Core: "a file with no `.` in its basename (even one literally
    // named `mov`) is not media".
    [InlineData("mov", false)]
    [InlineData("deep.dir/plainfile", false)]
    // Core: a dotfile like `.mov` is hidden, not media.
    [InlineData(".mov", false)]
    [InlineData("deep/.png", false)]
    [InlineData("", false)]
    public void TheMediaGateIsCoresClassification(string target, bool expected) =>
        Assert.Equal(expected, CanvasMediaPolicy.IsOpenableMedia(target));

    /// <summary>
    /// Half the transliteration, pinned against CORE — for free, and
    /// without waiting for PR E to export `media_class`.
    /// </summary>
    /// <remarks>
    /// Core does not export the classification, but it does export one
    /// of its ANSWERS: `kind_label` returns `"image"` exactly when
    /// `media_class` says Image (`model.rs:646`), and that reaches the
    /// host as `CanvasOutlineRow.Kind`. So for every image extension the
    /// host set claims, core's own row must agree — and for a
    /// non-media extension core must say `"file"`, which catches the
    /// gate widening in either direction. The audio and video thirds
    /// have no exported answer and stay unpinned until PR E; CD-38
    /// records that.
    /// </remarks>
    [Fact]
    public void TheImageThirdOfTheGateAgreesWithCoresOwnKindLabel()
    {
        string[] imageExtensions =
        [
            "png", "jpg", "jpeg", "gif", "svg", "webp", "bmp", "heic", "avif", "tiff",
        ];
        string[] nonMediaExtensions = ["exe", "bat", "ps1", "zip", "pdf", "txt"];

        var nodes = new List<string>();
        foreach (string extension in imageExtensions.Concat(nonMediaExtensions))
        {
            File.WriteAllBytes(
                Path.Combine(_fixture.Root, $"asset.{extension}"), [0x00]);
            nodes.Add(
                $"{{\"id\":\"{extension}\",\"type\":\"file\",\"file\":\"asset.{extension}\","
                + "\"x\":0,\"y\":0,\"width\":10,\"height\":10}");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "kinds.canvas"),
            "{\"nodes\":[" + string.Join(",", nodes) + "],\"edges\":[]}");
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);

        CanvasDocumentViewModel document = NewDocument("kinds.canvas");
        document.Load();

        foreach (string extension in imageExtensions)
        {
            // Core says image ⇒ the host gate must admit it.
            Assert.Equal("image", Row(document, extension).Kind);
            Assert.True(
                CanvasMediaPolicy.IsOpenableMedia($"asset.{extension}"),
                $"core classifies .{extension} as an image; the gate refuses it");
        }
        foreach (string extension in nonMediaExtensions)
        {
            // Core says plain file ⇒ the host gate must not admit it as
            // an IMAGE. (Audio and video are also "file" to kind_label,
            // which is why this direction only pins the image third.)
            Assert.Equal("file", Row(document, extension).Kind);
            Assert.False(
                CanvasMediaPolicy.IsOpenableMedia($"asset.{extension}"),
                $"the gate admits .{extension}, which core does not classify as media");
        }
        document.Shutdown();
    }

    /// <summary>
    /// M2's last hole: one document serves every pane on the path, so an
    /// unaddressed focus request landed in all of them. Opening in pane
    /// B must not pull focus out of pane A.
    /// </summary>
    [Fact]
    public void OpeningACanvasInOnePaneNeverLandsFocusInAnother() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel paneA =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        ((System.Windows.Input.ICommand)workspace.SplitRightCommand).Execute(null);
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel paneB =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.NotSame(paneA, paneB);
        // The registry shares ONE document across the two panes — which
        // is what made a broadcast reach both surfaces.
        Assert.Same(paneA.Canvas, paneB.Canvas);

        // Two mounted surfaces, each carrying its own tab as DataContext
        // exactly as the tab template gives it.
        var surfaceA = new CanvasSurfaceView { DataContext = paneA, Model = paneA.Canvas };
        var surfaceB = new CanvasSurfaceView { DataContext = paneB, Model = paneB.Canvas };
        var panel = new StackPanel();
        panel.Children.Add(surfaceA);
        panel.Children.Add(surfaceB);
        using var host = Host(panel);

        // Addressed to pane A — the surface mounted FIRST, and therefore
        // the one a broadcast would lose to. With the guard, only A
        // lands. Without it both land and B, subscribed second, wins the
        // last word: asserting on A is what makes the deleted guard
        // fail this fact rather than pass it.
        paneA.Canvas!.RequestFocusLanding(paneA);
        host.UpdateLayout();

        CanvasOutlineRowViewModel? focused = FocusedRow(host);
        Assert.NotNull(focused);
        Assert.Contains(focused, AllRows(surfaceA.OutlineForTests.RootsForTests));
        Assert.DoesNotContain(focused, AllRows(surfaceB.OutlineForTests.RootsForTests));
    });

    private static IEnumerable<CanvasOutlineRowViewModel> AllRows(
        IEnumerable<CanvasOutlineRowViewModel> rows) =>
        rows.SelectMany(row => new[] { row }.Concat(AllRows(row.Children)));

    // --- M2: focus lands when the user asks, and only then -----------------

    /// <summary>
    /// A retarget replaces the document under a reader who never moved,
    /// and the movement verbs still seat them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presenter attachment needs a false→true keyboard-focus edge or a
    /// canvas chord, and an external rename produces neither: the tab
    /// survives, the reader's keys never leave the filter field, and
    /// `OnModelChanged` detached the surface from X's navigator without
    /// ever introducing it to Y's. Every palette movement verb afterwards
    /// moved the selection and SPOKE the motion while `FocusRow` reached
    /// nobody — the reader hears "card 2 of 5" and the keys are still in
    /// the filter field. That is CD-40's contract (the reader and the
    /// selection agree) broken by an ordinary rename plus one palette
    /// command.
    /// </para>
    /// <para>
    /// The premise below is the same verb BEFORE the rename, so a failure
    /// here is the replacement and not the verb.
    /// </para>
    /// <para>
    /// TWO arms, because the rebind has two clauses and one of them was
    /// unpinned for a wave. With the keys still in the surface,
    /// `IsKeyboardFocusWithin` alone satisfies the rebind and the
    /// AFFINITY clause does nothing — so the second arm hands the keys to
    /// something else first (a palette, a menu, an overlay: the reader's
    /// pane is the one they were LAST in, not the one they are in), which
    /// is the only arrangement in which `wasTheAttachedPane` is the
    /// clause doing the work. Nothing detaches on focus loss, which is
    /// what makes that answer available at all.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARetargetKeepsThePaneTheReaderIsWorkingIn(
        bool keysHeldElsewhere) => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel before =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        var surface = new CanvasSurfaceView { DataContext = tab, Model = tab.Canvas };
        var palette = new TextBox();
        var root = new StackPanel();
        root.Children.Add(surface);
        root.Children.Add(palette);
        using var host = Host(root);

        // The reader is in the filter field.
        Assert.True(
            surface.FilterFieldForTests.Focus(),
            "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();
        // PREMISE: the verb seats them today, so the attachment exists
        // and this fact is about what the replacement does to it.
        before.Navigator.NextCard();
        host.UpdateLayout();
        Assert.True(
            surface.ProjectionHasFocus,
            "the movement verb did not seat the reader even before the "
            + "rename, so this fact would be about the verb.");
        Assert.True(
            surface.FilterFieldForTests.Focus(),
            "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();

        if (keysHeldElsewhere)
        {
            // Something transient takes the keys — and the retarget lands
            // while it holds them.
            Assert.True(
                palette.Focus(),
                "premise: palette refused keyboard focus, so this arrangement never established.");
            host.UpdateLayout();
            Assert.False(surface.IsKeyboardFocusWithin);
        }

        File.Move(
            Path.Combine(_fixture.Root, "board.canvas"),
            Path.Combine(_fixture.Root, "renamed.canvas"));
        workspace.RetargetPath("board.canvas", "renamed.canvas");
        // What the tab template's binding does; the reader did nothing.
        surface.Model = tab.Canvas;
        host.UpdateLayout();
        CanvasDocumentViewModel after =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.NotSame(before, after);
        Assert.Equal(CanvasLoadState.Ready, after.State);
        Assert.True(
            keysHeldElsewhere
                ? palette.IsKeyboardFocused
                : surface.FilterFieldForTests.IsKeyboardFocused,
            "the retarget moved the reader, which is a different defect.");

        after.Navigator.NextCard();
        host.UpdateLayout();

        Assert.True(
            surface.ProjectionHasFocus,
            "the verb moved the selection and spoke the motion while the "
            + "keys stayed in the filter field — the reader and the "
            + "selection disagree (CD-40), because the replacement "
            + "document's navigator never met this surface.");
        var seated = Assert.IsType<CanvasOutlineItem>(
            System.Windows.Input.Keyboard.FocusedElement);
        CanvasOutlineRowViewModel row =
            Assert.IsType<CanvasOutlineRowViewModel>(seated.DataContext);
        Assert.Equal(after.Selection.Selected, row.Id);
    });

    /// <summary>
    /// A surface that took the keys BEFORE its model arrived still hosts
    /// the verbs.
    /// </summary>
    /// <remarks>
    /// The other clause of the rebind, and the ordering that earns it.
    /// The focus edge attaches through <c>Model?.Navigator</c>, so a
    /// surface that gains keyboard focus while its `Model` is still null
    /// attaches NOTHING — the null-conditional swallows it. The edge will
    /// not come again, because focus is already inside. So the model
    /// arriving has to ask whether the reader is here, and that is what
    /// `IsKeyboardFocusWithin` answers in `OnModelChanged`. Reachable
    /// during tab construction, where the template applies and the
    /// binding resolves in that order.
    /// </remarks>
    [Fact]
    public void ASurfaceThatTookTheKeysBeforeItsModelStillHostsTheVerbs() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { DataContext = new object() };
        using var host = Host(surface);
        Assert.Null(surface.Model);

        // The keys arrive FIRST. Nothing is attached: there is no
        // navigator to attach to yet.
        Assert.True(
            surface.FilterFieldForTests.Focus(),
            "premise: surface.FilterField refused keyboard focus, so this arrangement never established.");
        host.UpdateLayout();

        surface.Model = document;
        host.UpdateLayout();
        Assert.Equal(CanvasLoadState.Ready, document.State);

        document.Navigator.NextCard();
        host.UpdateLayout();

        Assert.True(
            surface.ProjectionHasFocus,
            "the verb moved the selection and spoke it while the keys sat in "
            + "a filter field the navigator had never been told about — the "
            + "focus edge that would have told it happened before there was "
            + "a navigator to tell.");
        document.Shutdown();
    });

    /// <summary>
    /// A retarget publishes without anyone asking, and must not pull
    /// focus out of whatever the user was doing — the invariant the
    /// old code's own comment stated and broke, because focus was a
    /// side effect of the first publish while visible.
    /// </summary>
    [Fact]
    public void ARetargetPublishNeverStealsFocus() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        // DataContext is the tab, as the tab template gives it — this
        // fact does not raise a request, but a surface that cannot tell
        // whose it is is not the production shape.
        var surface = new CanvasSurfaceView { DataContext = tab, Model = tab.Canvas };
        var elsewhere = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(elsewhere);
        panel.Children.Add(surface);
        using var host = Host(panel);
        Assert.True(
            elsewhere.Focus(),
            "premise: elsewhere refused keyboard focus, so this arrangement never established.");
        Assert.Same(elsewhere, host.FocusedElement());

        File.Move(
            Path.Combine(_fixture.Root, "board.canvas"),
            Path.Combine(_fixture.Root, "renamed.canvas"));
        workspace.RetargetPath("board.canvas", "renamed.canvas");
        surface.DataContext = tab;
        surface.Model = tab.Canvas;
        host.UpdateLayout();

        // A fresh document published rows; focus stayed where the user
        // put it.
        Assert.Equal(CanvasLoadState.Ready, tab.Canvas!.State);
        Assert.NotEmpty(tab.Canvas.Outline);
        Assert.Same(elsewhere, host.FocusedElement());
    });

    /// <summary>
    /// The inverse hole: a second tab on an ALREADY-open path is a
    /// registry hit, so no publish will ever come — and focus landing
    /// keyed on "first publish" never happened for it. It is keyed on
    /// the workspace's user-initiated open funnel instead.
    /// </summary>
    [Fact]
    public void ASecondTabOnAnOpenPathStillLandsFocus() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel first =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        // DataContext is the tab, exactly as the tab template gives it —
        // the focus request is ADDRESSED, so a view that does not know
        // which tab it belongs to is not the production shape.
        var surface = new CanvasSurfaceView { DataContext = first, Model = first.Canvas };
        using var host = Host(surface);

        // The document is already loaded and published; the surface
        // mounts onto a registry HIT — no publish will ever come for it,
        // which is what the old publish-triggered design missed
        // entirely. The request is durable state, so mounting delivers
        // the one OpenPath already raised.
        Assert.Equal(CanvasLoadState.Ready, first.Canvas!.State);
        Assert.Null(first.Canvas.FocusRequest);
        CanvasOutlineRowViewModel landed = Assert.IsType<CanvasOutlineRowViewModel>(
            FocusedRow(host));
        Assert.Equal(first.Canvas.Outline[0].NodeId, landed.Id);

        // And a fresh request on the same already-open document still
        // lands, with no publish anywhere in sight.
        first.Canvas.SelectNode("loose", announce: false);
        workspace.RequestActiveEditorFocus();
        host.UpdateLayout();
        Assert.Null(first.Canvas.FocusRequest);
        Assert.NotNull(FocusedRow(host));
    });

    /// <summary>An empty canvas has no row to land on; focus goes to the
    /// onboarding region rather than nowhere (m4).</summary>
    [Fact]
    public void AnEmptyCanvasLandsFocusOnTheOnboardingRegion() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("blank.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.Empty(document.Outline);
        var surface = new CanvasSurfaceView { DataContext = tab, Model = document };
        using var host = Host(surface);

        // The production route: the workspace's open funnel, addressed.
        workspace.RequestActiveEditorFocus();
        host.UpdateLayout();
        Assert.Same(surface.OnboardingForTests, host.FocusedElement());
    });

    /// <summary>t0 §3: a failure a keyboard user cannot reach is a
    /// failure nobody reported (m7).</summary>
    [Fact]
    public void TheFailureBannerIsAFocusableRegion() => RunSta(() =>
    {
        CanvasDocumentViewModel broken = NewDocument("broken.canvas");
        broken.Load();
        var surface = new CanvasSurfaceView { Model = broken };
        Assert.True(surface.StateBannerForTests.Focusable);
        Assert.True(System.Windows.Input.KeyboardNavigation.GetIsTabStop(
            surface.StateBannerForTests));

        CanvasDocumentViewModel ready = NewDocument("board.canvas");
        ready.Load();
        surface.Model = ready;
        // A transient "Opening canvas…" is not a tab stop that vanishes
        // under the cursor.
        Assert.False(surface.StateBannerForTests.Focusable);
        broken.Shutdown();
        ready.Shutdown();
    });

    /// <summary>
    /// A failure does not erase the durable selection intent: the
    /// failed load clears the SEAT — there are no rows — but the next
    /// successful load's rebase resolves the intent and the selection
    /// comes back (task T3; the design's "a node that comes back on the
    /// next load should come back selected").
    /// </summary>
    [Fact]
    public void AFailedLoadKeepsTheSelectionIntentForTheNextOne()
    {
        string board = Path.Combine(_fixture.Root, "board.canvas");
        string moved = Path.Combine(_fixture.Root, "board.canvas.away");
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        document.SelectNode("evidence", announce: false);
        Assert.Equal("evidence", document.Selection.Selected);

        File.Move(board, moved);
        try
        {
            document.Load();
            Assert.Equal(CanvasLoadState.Failed, document.State);
            Assert.Null(document.Selection.Selected);
        }
        finally
        {
            File.Move(moved, board);
        }

        document.Load();
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Equal("evidence", document.Selection.Selected);
        document.Shutdown();
    }

    /// <summary>
    /// The clear APPLIES its widen (task T6, after its review): with the
    /// keystroke's publication left unapplied, the next needle's
    /// in-flight window showed the cleared filter's rows and count — a
    /// retired needle's answer under the new one.
    /// </summary>
    [Fact]
    public void TheClearAppliesItsWidenSoTheNextKeystrokeCannotResurrectOldRows()
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        document.FilterText = "question";
        Assert.Equal(CanvasAnswerState.Answered, document.AppliedFilterAnswerForTests);
        _ = Assert.Single(document.FilteredOutline);

        document.FilterText = " ";

        Assert.Equal(CanvasAnswerState.Unfiltered, document.AppliedFilterAnswerForTests);
        Assert.False(document.Filter.Narrowed, "the widen is applied, not just published.");
        Assert.Equal(document.Outline.Count, document.FilteredOutline.Count);

        document.FilterText = "evidence";
        Assert.Equal(CanvasAnswerState.Answered, document.AppliedFilterAnswerForTests);
        _ = Assert.Single(document.FilteredOutline);
        document.Shutdown();
    }

    /// <summary>
    /// SEAM 3's ordering, pinned at the source: the retired publication
    /// precedes the base shutdown in <c>Shutdown</c>, so a load issued
    /// between the two is refused by the model rather than silently
    /// dropped by the scheduler (task T3). Reversing the writes returns
    /// the silent drop — a request published onto a document whose
    /// worker will never run — and the window between two adjacent
    /// writes is not one a barrier can be put into, so the order is
    /// asserted where it lives, the way the render predicate is.
    /// </summary>
    [Fact]
    public void TheRetiredPublicationPrecedesTheBaseShutdown()
    {
        string source = File.ReadAllText(Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateWindows",
            "Canvas", "CanvasDocumentViewModel.cs"));
        int shutdown = source.IndexOf(
            "internal override void Shutdown()", StringComparison.Ordinal);
        Assert.True(shutdown >= 0, "premise: the teardown method moved.");
        int retired = source.IndexOf(
            "_slot.Publish(s => s.WithRetired())", shutdown, StringComparison.Ordinal);
        int baseShutdown = source.IndexOf("base.Shutdown();", shutdown, StringComparison.Ordinal);
        Assert.True(retired >= 0 && baseShutdown >= 0, "premise: a write moved out of the teardown.");
        Assert.True(
            retired < baseShutdown,
            "the base shutdown precedes the retired publication: a load issued in "
            + "between is accepted by the model and dropped by the scheduler — the "
            + "silent drop SEAM 3 exists to make impossible.");
    }

    // --- M3: the production scheduling mode's own interleavings -------------

    /// <summary>
    /// The W4-5 lesson, applied to teardown: a shutdown landing while a
    /// load body is in flight must publish nothing and still close the
    /// handle exactly once. Synchronous mode orders the body before the
    /// shutdown can interleave at all, so this fact only exists in the
    /// production mode.
    /// </summary>
    [Fact]
    public async Task AShutdownDuringAnInFlightLoadNeverPublishesAndClosesTheHandle()
    {
        CanvasDocumentViewModel document = NewAsyncDocument("board.canvas");
        int published = 0;
        document.OutlinePublished += (_, _) => published++;

        document.Load();
        document.Shutdown();
        await QuiesceAsync(document);
        await document.WhenHandleClosed();

        // Either the body bailed at its generation check or its publish
        // did; what must never happen is a Ready surface after teardown.
        Assert.NotEqual(CanvasLoadState.Ready, document.State);
        Assert.Equal(0, published);
        Assert.Empty(document.Outline);
        // Refused afterwards, forever.
        document.Load();
        await QuiesceAsync(document);
        Assert.NotEqual(CanvasLoadState.Ready, document.State);
    }

    /// <summary>
    /// Two loads in flight: the generation guard drops the first body's
    /// publish, so the surface is published exactly once and from the
    /// LATER open. Without the guard both would publish and the second's
    /// rows could be overwritten by the first's.
    /// </summary>
    [Fact]
    public async Task ASecondLoadSupersedesTheFirstPublish()
    {
        CanvasDocumentViewModel document = NewAsyncDocument("board.canvas");
        int published = 0;
        document.OutlinePublished += (_, _) => published++;

        document.Load();
        document.Load();
        await QuiesceAsync(document);

        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Equal(1, published);
        Assert.NotEmpty(document.Outline);
        document.Shutdown();
        await document.WhenHandleClosed();
    }

    /// <summary>
    /// A retarget while the old document is still loading: the old one
    /// is shut down mid-flight and the new one at the new path is the
    /// only thing that publishes.
    /// </summary>
    [Fact]
    public async Task ARetargetDuringAnInFlightLoadPublishesOnlyTheNewDocument()
    {
        CanvasDocumentViewModel stale = NewAsyncDocument("board.canvas");
        int stalePublished = 0;
        stale.OutlinePublished += (_, _) => stalePublished++;
        stale.Load();
        stale.Shutdown();

        CanvasDocumentViewModel fresh = NewAsyncDocument("skipped.canvas");
        int freshPublished = 0;
        fresh.OutlinePublished += (_, _) => freshPublished++;
        fresh.Load();

        await QuiesceAsync(stale);
        await QuiesceAsync(fresh);
        await stale.WhenHandleClosed();

        Assert.Equal(0, stalePublished);
        Assert.NotEqual(CanvasLoadState.Ready, stale.State);
        Assert.Equal(1, freshPublished);
        Assert.Equal(CanvasLoadState.Ready, fresh.State);
        fresh.Shutdown();
        await fresh.WhenHandleClosed();
    }

    /// <summary>Production scheduling, a NULL SynchronizationContext so
    /// publishes run inline on the worker: after a drain every publish
    /// has been applied (with xunit's context they would still be
    /// queued) — the history async-suite pattern.</summary>
    private CanvasDocumentViewModel NewAsyncDocument(string path)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return new CanvasDocumentViewModel(
                _session,
                path,
                new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
                synchronousForTests: false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Drain repeatedly: a drained body can have queued
    /// follow-up work the drain's snapshot missed.</summary>
    private static async Task QuiesceAsync(CanvasDocumentViewModel document)
    {
        for (int round = 0; round < 20; round++)
        {
            await document.DrainForTests();
            await Task.Delay(2);
        }
    }

    // --- B3: a rename lands new bytes at the DESTINATION -------------------

    /// <summary>
    /// The atomic-save shape: write a temp file, rename it onto the open
    /// canvas. The SOURCE was never open, so the registry re-key loop
    /// does nothing — and the destination document went on rendering the
    /// pre-rename rows.
    /// </summary>
    [Fact]
    public void ARenameOntoAnOpenCanvasReloadsTheDestination()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Contains(document.Outline, row => row.NodeId == "question");

        File.WriteAllText(
            Path.Combine(_fixture.Root, "board.canvas.tmp"),
            """
            {"nodes":[
              {"id":"after","type":"text","text":"Renamed in","x":0,"y":0,"width":10,"height":10}
            ],"edges":[]}
            """);
        File.Move(
            Path.Combine(_fixture.Root, "board.canvas.tmp"),
            Path.Combine(_fixture.Root, "board.canvas"),
            overwrite: true);
        workspace.RetargetPath("board.canvas.tmp", "board.canvas");

        Assert.Same(document, workspace.ActiveGroup.ActiveTab!.Canvas);
        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Contains(document.Outline, row => row.NodeId == "after");
        Assert.DoesNotContain(document.Outline, row => row.NodeId == "question");
    }

    /// <summary>
    /// Both open: the destination reloads and the source's tabs retarget
    /// onto it, so the two panes end up on ONE document showing the
    /// bytes that are actually on disk.
    /// </summary>
    [Fact]
    public void ARenameWithBothPathsOpenReloadsTheDestinationAndRetargetsTheSource()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "source.canvas"),
            """
            {"nodes":[{"id":"src","type":"text","text":"Source","x":0,"y":0,"width":10,"height":10}],"edges":[]}
            """);
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel destinationTab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel destination = Assert.IsType<CanvasDocumentViewModel>(
            destinationTab.Canvas);
        ((System.Windows.Input.ICommand)workspace.SplitRightCommand).Execute(null);
        workspace.OpenPath("source.canvas");
        WorkspaceTabViewModel sourceTab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.NotSame(destination, sourceTab.Canvas);

        File.Move(
            Path.Combine(_fixture.Root, "source.canvas"),
            Path.Combine(_fixture.Root, "board.canvas"),
            overwrite: true);
        workspace.RetargetPath("source.canvas", "board.canvas");

        // One document for the one path, and it agrees with disk.
        CanvasDocumentViewModel survivor = Assert.IsType<CanvasDocumentViewModel>(
            destinationTab.Canvas);
        Assert.Same(survivor, sourceTab.Canvas);
        Assert.Equal(CanvasLoadState.Ready, survivor.State);
        Assert.Contains(survivor.Outline, row => row.NodeId == "src");
        Assert.DoesNotContain(survivor.Outline, row => row.NodeId == "question");
    }

    // --- B4: the gate is physical and fails closed -------------------------

    /// <summary>
    /// Containment is checked against the target's PHYSICAL identity. A
    /// symlink inside the vault whose terminal target is outside it has
    /// a path that starts with the vault root, so a textual prefix check
    /// admits it and the shell opens a file the vault never contained.
    /// </summary>
    [Fact]
    public void AnInVaultSymlinkPointingOutsideTheVaultIsRefused()
    {
        string outside = Path.Combine(Path.GetTempPath(), $"slate-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            string realFile = Path.Combine(outside, "elsewhere.png");
            File.WriteAllBytes(realFile, [0x89, 0x50, 0x4E, 0x47]);
            string link = Path.Combine(_fixture.Root, "innocent.png");
            try
            {
                File.CreateSymbolicLink(link, realFile);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                // Symlink creation needs Developer Mode or elevation.
                // Skipping silently would make this fact meaningless, so
                // it says so — and the fail-closed fact below covers the
                // policy's other half unconditionally.
                Assert.Fail(
                    "this box cannot create symlinks, so the physical-containment "
                    + "arm of CD-38 went unchecked: enable Developer Mode or run "
                    + $"elevated. ({exception.GetType().Name})");
                return;
            }

            // A plain in-vault media file still opens...
            File.WriteAllBytes(
                Path.Combine(_fixture.Root, "real.png"), [0x89, 0x50, 0x4E, 0x47]);
            Assert.NotNull(
                CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "real.png"));
            // ...and the link out of the vault does not.
            Assert.Null(
                CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "innocent.png"));
        }
        finally
        {
            try
            {
                Directory.Delete(outside, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// Fail CLOSED: every malformed target the framework throws on is a
    /// refusal, never an exception escaping into the activation and
    /// never a launch. An escaping exception would abort the activation
    /// without saying anything, which is the one outcome t0 forbids.
    /// </summary>
    [Theory]
    [InlineData("has\0nul.png")]
    [InlineData("CON.png")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("C:\\Windows\\System32\\calc.png")]
    [InlineData("\\\\server\\share\\thing.png")]
    public void AMalformedMediaTargetIsRefusedRatherThanThrown(string target)
    {
        string? resolved = CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, target);
        Assert.Null(resolved);
    }

    /// <summary>And the refusal is AUDIBLE at the activation, not a
    /// silent false: t0's never-silent rule.</summary>
    [Fact]
    public void AMalformedMediaTargetRefusesAudibly()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "hostile-media.canvas"),
            "{\"nodes\":[{\"id\":\"bad\",\"type\":\"file\","
            + "\"file\":\"..\\\\..\\\\outside.png\","
            + "\"x\":0,\"y\":0,\"width\":10,\"height\":10}],\"edges\":[]}");
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("hostile-media.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        _announced.Clear();

        Assert.Equal(CanvasActivation.Refused, document.Activate(Row(document, "bad")));
        document.AnnouncerForTests.FlushForTests();
        Assert.NotEmpty(_announced);
        Assert.Equal(A11yPriority.High, _announced[^1].Priority);
    }

    /// <summary>
    /// The bypass leaf-only resolution left open: a DIRECTORY junction
    /// inside the vault pointing outside it, holding an ordinary
    /// <c>.png</c>. The leaf is not a link, so <c>ResolveLinkTarget</c>
    /// answers null for it and the lexical path still begins with the
    /// vault root — the file opens.
    /// </summary>
    /// <remarks>
    /// Junctions need neither elevation nor Developer Mode, which is why
    /// the symlink fact's "this box may not be able to" caveat does not
    /// apply here and this one has no escape hatch. Mutation-verified
    /// against the ancestor walk.
    /// </remarks>
    [Fact]
    public void AJunctionInsideTheVaultPointingOutsideIsRefused()
    {
        string outside = Path.Combine(
            Path.GetTempPath(), $"slate-junction-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            // An ordinary media file, outside the vault.
            File.WriteAllBytes(
                Path.Combine(outside, "leaf.png"), [0x89, 0x50, 0x4E, 0x47]);

            // A junction inside the vault pointing at it. No elevation,
            // no Developer Mode — this is the whole point.
            string junction = Path.Combine(_fixture.Root, "assets");
            RunCommand($"mklink /J \"{junction}\" \"{outside}\"");
            Assert.True(
                Directory.Exists(junction) && File.Exists(Path.Combine(junction, "leaf.png")),
                "the junction was not created, so the bypass went unchecked");

            // In-vault by every lexical measure; outside it physically.
            Assert.Null(
                CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "assets/leaf.png"));

            // ...while a real in-vault file under a real directory still
            // opens, so the check is containment and not a blanket no.
            string real = Path.Combine(_fixture.Root, "media");
            Directory.CreateDirectory(real);
            File.WriteAllBytes(Path.Combine(real, "ok.png"), [0x89, 0x50, 0x4E, 0x47]);
            Assert.NotNull(
                CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "media/ok.png"));
        }
        finally
        {
            try
            {
                // Remove the junction itself, never its target's contents.
                string junction = Path.Combine(_fixture.Root, "assets");
                if (Directory.Exists(junction))
                {
                    Directory.Delete(junction);
                }
                Directory.Delete(outside, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>A nested junction — the target of a junction itself
    /// under another — still resolves, because the walk substitutes the
    /// deepest linked ancestor and starts again.</summary>
    [Fact]
    public void ANestedJunctionChainStillResolvesOutsideTheVault()
    {
        string outside = Path.Combine(
            Path.GetTempPath(), $"slate-junction-nested-{Guid.NewGuid():N}");
        string inner = Path.Combine(outside, "inner");
        Directory.CreateDirectory(inner);
        try
        {
            File.WriteAllBytes(
                Path.Combine(inner, "leaf.png"), [0x89, 0x50, 0x4E, 0x47]);
            string hop = Path.Combine(_fixture.Root, "hop");
            RunCommand($"mklink /J \"{hop}\" \"{outside}\"");
            Assert.True(Directory.Exists(hop), "the junction was not created");

            Assert.Null(
                CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "hop/inner/leaf.png"));
        }
        finally
        {
            try
            {
                string hop = Path.Combine(_fixture.Root, "hop");
                if (Directory.Exists(hop))
                {
                    Directory.Delete(hop);
                }
                Directory.Delete(outside, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void RunCommand(string command)
    {
        using var process = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });
        Assert.NotNull(process);
        Assert.True(process!.WaitForExit(15_000), $"`{command}` did not finish");
    }

    /// <summary>
    /// B1 — the revalidation catches a namespace swap in the TOCTOU
    /// window. The gate resolves the target, then an attacker (the test
    /// seam, standing in for a hostile in-vault sync peer) swaps the
    /// checked directory for an outward junction; the immediate
    /// re-resolution before launch sees the change and refuses.
    /// </summary>
    /// <remarks>
    /// Mutation-verified: removing the revalidation launches the swapped
    /// target. This does not close the residual — the sub-instruction
    /// gap between the final check and ShellExecute's own resolution is
    /// irreducible with a path-taking launcher (CD-38) — it shrinks the
    /// window to near-zero and proves the shrink.
    /// </remarks>
    [Fact]
    public void ASwapInTheTocTouWindowIsCaughtByRevalidation()
    {
        string outside = Path.Combine(
            Path.GetTempPath(), $"slate-toctou-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "evil.png"), [0x89, 0x50, 0x4E, 0x47]);
        string safe = Path.Combine(_fixture.Root, "safe");
        Directory.CreateDirectory(safe);
        File.WriteAllBytes(Path.Combine(safe, "ok.png"), [0x89, 0x50, 0x4E, 0x47]);
        // The click names a plain in-vault directory: `real/ok.png`.
        string real = Path.Combine(_fixture.Root, "real");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "ok.png"), [0x89, 0x50, 0x4E, 0x47]);

        var launched = new List<string>();
        try
        {
            // In the window, swap `real` (a real directory, checked and
            // valid) for a junction pointing outside the vault.
            CanvasMediaPolicy.BetweenCheckAndLaunchForTests = () =>
            {
                Directory.Delete(real, recursive: true);
                RunCommand($"mklink /J \"{real}\" \"{outside}\"");
            };
            bool opened = CanvasMediaPolicy.OpenMediaInVault(
                _fixture.Root, "real/ok.png", target =>
                {
                    launched.Add(target);
                    return true;
                });

            Assert.False(opened, "the swapped target was launched");
            Assert.Empty(launched);
        }
        finally
        {
            CanvasMediaPolicy.BetweenCheckAndLaunchForTests = null;
            try
            {
                if (Directory.Exists(real)
                    && new DirectoryInfo(real).LinkTarget is not null)
                {
                    Directory.Delete(real);
                }
                Directory.Delete(outside, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// And without a swap, the gate launches the FULLY-RESOLVED terminal
    /// path — not the vault-relative one the click named — so
    /// ShellExecute's own re-resolution has no reparse point left to
    /// redirect.
    /// </summary>
    [Fact]
    public void TheLaunchedPathIsTheFullyResolvedTerminalIdentity()
    {
        string real = Path.Combine(_fixture.Root, "pics");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "photo.png"), [0x89, 0x50, 0x4E, 0x47]);

        string? handed = null;
        bool opened = CanvasMediaPolicy.OpenMediaInVault(
            _fixture.Root, "pics/photo.png", target =>
            {
                handed = target;
                return true;
            });

        Assert.True(opened);
        Assert.NotNull(handed);
        // A fully-qualified path, and one that actually points at the
        // file (GetFinalPathNameByHandle, prefix stripped).
        Assert.True(Path.IsPathFullyQualified(handed!));
        Assert.True(File.Exists(handed));
        Assert.EndsWith("photo.png", handed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The identity primitive itself: two spellings that reach the SAME
    /// file object compare EQUAL, and two different objects compare
    /// UNEQUAL. This is the substrate every containment and revalidation
    /// decision now stands on, so it is pinned directly — the OS-identity
    /// checks that follow are only as good as this.
    /// </summary>
    [Fact]
    public void FileIdentityIsStableAcrossSpellingsAndDistinctAcrossObjects()
    {
        string dir = Path.Combine(_fixture.Root, "id");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "a.png"), [0x89]);
        File.WriteAllBytes(Path.Combine(dir, "b.png"), [0x89]);

        CanvasMediaPolicy.FileIdentity? a1 =
            CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, "a.png"));
        // A different spelling of the SAME file: mixed case (Windows is
        // case-insensitive by default) and a redundant `.\` segment.
        CanvasMediaPolicy.FileIdentity? a2 =
            CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, ".", "A.PNG"));
        CanvasMediaPolicy.FileIdentity? b =
            CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, "b.png"));
        CanvasMediaPolicy.FileIdentity? parent =
            CanvasMediaPolicy.IdentityForTests(dir);

        Assert.NotNull(a1);
        Assert.Equal(a1, a2);          // same object, different spelling
        Assert.NotEqual(a1, b);        // sibling file
        Assert.NotEqual(a1, parent);   // a file is never its directory
        // A path that does not exist has no identity.
        Assert.Null(CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, "gone.png")));
    }

    /// <summary>
    /// Round 4 #2 — the identity primitive is the 128-bit
    /// <c>FILE_ID_INFO</c> (volume serial + 128-bit file id), NOT the
    /// 64-bit <c>nFileIndex</c>, which is documented as NON-UNIQUE on ReFS
    /// and would let two different files compare equal — a fail-OPEN in
    /// the containment gate. This pins that the ReFS-safe class is the one
    /// actually taken on a live handle here.
    /// </summary>
    /// <remarks>
    /// A ReFS volume cannot be created unprivileged, so the ReFS collision
    /// itself is recorded as manual in CD-38. What is pinned is the
    /// PRIMITIVE: <c>GetFileInformationByHandleEx(FileIdInfo=18)</c> with
    /// the 24-byte <c>FILE_ID_INFO</c> layout succeeds on this box (so the
    /// class value, the struct size and the P/Invoke are all correct and
    /// the 64-bit fallback is NOT what runs), and the 128-bit id it
    /// returns distinguishes two genuinely different files. Mutation:
    /// pointing the primitive at the 64-bit path (or a wrong class value)
    /// makes <c>UsesFileIdInfoForTests</c> return false and trips this.
    /// </remarks>
    [Fact]
    public void IdentityIsThe128BitFileIdInfoNotThe64BitIndex()
    {
        string dir = Path.Combine(_fixture.Root, "id128");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "a.png"), [0x89]);
        File.WriteAllBytes(Path.Combine(dir, "b.png"), [0x89]);

        // The 128-bit class is available and succeeds on a real handle
        // here. Since round 5 there is no other identity method, so this
        // failing would mean the gate refuses ALL media on this box —
        // which is the safe direction, but still a defect worth catching.
        Assert.True(
            CanvasMediaPolicy.UsesFileIdInfoForTests(Path.Combine(dir, "a.png")),
            "the 128-bit FILE_ID_INFO primitive did not run — with no fallback "
            + "by design, the gate would now refuse every media file on this host.");

        // And the 128-bit identity distinguishes two different objects.
        CanvasMediaPolicy.FileIdentity? a =
            CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, "a.png"));
        CanvasMediaPolicy.FileIdentity? b =
            CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, "b.png"));
        Assert.NotNull(a);
        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Round 5 — a failure of the PRIMARY identity query refuses the whole
    /// resolution; it never downgrades to a weaker identity. There is no
    /// legacy 64-bit path left to fall back to, and this proves the
    /// absence behaviourally rather than by reading the source.
    /// </summary>
    /// <remarks>
    /// The round-4 fallback was per-CALL, not the per-host capability
    /// selection its record claimed: ANY transient failure of
    /// <c>FileIdInfo</c> silently downgraded that one read to the
    /// non-unique <c>nFileIndex</c> — on ReFS a fail-open, arriving
    /// exactly when something was already wrong. The fallback is deleted:
    /// the constraint is the FILESYSTEM, not the OS version, so supported
    /// vault volumes are NTFS/ReFS and one that does not answer
    /// <c>FileIdInfo</c> refuses every media open — a recorded fail-CLOSED
    /// limitation, deliberately preferred to a weaker identity.
    /// Mutation-verified: reintroducing ANY fallback arm makes the injected
    /// failure resolve successfully again and fails this fact.
    /// </remarks>
    [Fact]
    public void IdentityQueryFailureRefusesRatherThanDowngrading()
    {
        string dir = Path.Combine(_fixture.Root, "idfail");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "shot.png"), [0x89, 0x50, 0x4E, 0x47]);

        // The premise: without injection this media resolves and opens.
        // Without this the fact could pass because the path was never
        // openable in the first place.
        Assert.NotNull(CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "idfail/shot.png"));

        var launched = new List<string>();
        try
        {
            CanvasMediaPolicy.FailIdentityQueryForTests = true;

            // The identity primitive itself yields NOTHING — not a
            // weaker id.
            Assert.Null(
                CanvasMediaPolicy.IdentityForTests(Path.Combine(dir, "shot.png")));
            // The containment flow refuses outright...
            Assert.Null(
                CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "idfail/shot.png"));
            // ...and nothing is ever handed to the shell.
            Assert.False(CanvasMediaPolicy.OpenMediaInVault(
                _fixture.Root, "idfail/shot.png", target =>
                {
                    launched.Add(target);
                    return true;
                }));
            Assert.Empty(launched);
        }
        finally
        {
            CanvasMediaPolicy.FailIdentityQueryForTests = false;
        }

        // And the injection is reversible — the gate still works after,
        // so the refusal above was the injection and not a broken fixture.
        Assert.NotNull(CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, "idfail/shot.png"));
    }

    /// <summary>
    /// Round 4 #3 — a valid in-vault media file 70 directories deep OPENS.
    /// The reparse-cycle bound (<c>ResolveRounds=64</c>) was wrongly
    /// applied to the LEXICAL parent walk, refusing legitimate media more
    /// than 64 dirs deep — a fail-CLOSED availability bug. The lexical
    /// walk shortens strictly and terminates at the volume root, so a
    /// fixed-point guard suffices and no depth cap belongs on it.
    /// </summary>
    /// <remarks>
    /// Mutation-verified: reinstating a 64-iteration cap on the ancestor
    /// walk makes this file (70 levels down) resolve null and refuse.
    /// </remarks>
    [Fact]
    public void MediaSeventyDirectoriesDeepStillOpens()
    {
        string deep = _fixture.Root;
        for (var level = 0; level < 70; level++)
        {
            deep = Path.Combine(deep, $"d{level}");
        }
        Directory.CreateDirectory(deep);
        File.WriteAllBytes(Path.Combine(deep, "buried.png"), [0x89, 0x50, 0x4E, 0x47]);

        string relative = Path.GetRelativePath(_fixture.Root, Path.Combine(deep, "buried.png"));
        string? resolved = CanvasMediaPolicy.ResolveInsideVault(_fixture.Root, relative);

        Assert.NotNull(resolved);
        Assert.Equal(
            CanvasMediaPolicy.IdentityForTests(Path.Combine(deep, "buried.png")),
            CanvasMediaPolicy.IdentityForTests(resolved!));

        // And it launches by the identity gate all the way down.
        string? handed = null;
        Assert.True(CanvasMediaPolicy.OpenMediaInVault(
            _fixture.Root, relative, target => { handed = target; return true; }));
        Assert.NotNull(handed);
    }

    /// <summary>
    /// Containment is decided by IDENTITY, so two adjacent directories
    /// that differ only in case — which a text prefix over an
    /// OrdinalIgnoreCase relative path falsely accepted (codex round-3
    /// defect 3, reachable when per-directory case sensitivity is on) —
    /// are DIFFERENT file objects and do not contain each other's files.
    /// </summary>
    /// <remarks>
    /// The exploit needs per-directory case sensitivity, which is
    /// non-default and needs admin (`fsutil`) to enable, so this pins the
    /// IDENTITY DISTINCTION directly on two genuinely different
    /// directories rather than manufacturing the case-sensitive variant.
    /// The escape a text prefix allowed was to a same-parent adjacent
    /// directory; identity refuses it because the adjacent directory is a
    /// different object. The `fsutil` end-to-end is recorded as manual in
    /// CD-38.
    /// </remarks>
    [Fact]
    public void ContainmentUsesIdentityNotAPrefixOverAdjacentDirectories()
    {
        // Two sibling dirs; a file lives under `vault`. Its resolution
        // must be contained by `vault` and NOT by `other`.
        string vault = Path.Combine(_fixture.Root, "vault");
        string other = Path.Combine(_fixture.Root, "other");
        Directory.CreateDirectory(vault);
        Directory.CreateDirectory(other);
        File.WriteAllBytes(Path.Combine(vault, "cover.png"), [0x89, 0x50, 0x4E, 0x47]);

        // Named relative to its real root: resolves and is contained.
        Assert.NotNull(CanvasMediaPolicy.ResolveInsideVault(vault, "cover.png"));
        // The same physical file, but the vault root passed is the
        // SIBLING: a text prefix that only compared strings would still
        // see `...\vault\cover.png` under `...\other`? No — but the
        // reverse, a file under `other` claimed for `vault`, is the
        // adjacency escape. Pin both directions on identity.
        File.WriteAllBytes(Path.Combine(other, "sneak.png"), [0x89, 0x50, 0x4E, 0x47]);
        // `other/sneak.png` addressed against the `vault` root by walking
        // out and back in — the escape shape.
        Assert.Null(CanvasMediaPolicy.ResolveInsideVault(vault, @"..\other\sneak.png"));
        // And the identities of the two roots genuinely differ, which is
        // what makes the refusal an identity fact and not a text one.
        Assert.NotEqual(
            CanvasMediaPolicy.IdentityForTests(vault),
            CanvasMediaPolicy.IdentityForTests(other));
    }

    /// <summary>
    /// Major-4: the volume-GUID fallback. `GetFinalPathNameByHandle`
    /// with the default DOS-name flag returns ERROR_PATH_NOT_FOUND for a
    /// volume with no drive letter (a folder-mounted volume), so every
    /// target under such a vault resolved null and NO media opened. The
    /// fallback resolves by volume-GUID name instead.
    /// </summary>
    /// <remarks>
    /// A driveless/folder-mounted volume cannot be created unprivileged,
    /// so the end-to-end scenario is recorded as manual in CD-38. What is
    /// pinned here is the PRIMITIVE the fallback depends on: the GUID
    /// resolution returns a well-formed volume-GUID path for an ordinary
    /// file, proving the flag and the P/Invoke work, so the fallback is
    /// not dead code.
    /// </remarks>
    [Fact]
    public void TheVolumeGuidResolutionReturnsAWellFormedPath()
    {
        File.WriteAllBytes(Path.Combine(_fixture.Root, "guid.png"), [0x89, 0x50, 0x4E, 0x47]);
        string? guid = CanvasMediaPolicy.GuidNameForTests(
            Path.Combine(_fixture.Root, "guid.png"));

        Assert.NotNull(guid);
        // \\?\Volume{xxxxxxxx-....}\...  — the device-GUID form.
        Assert.StartsWith(@"\\?\Volume{", guid, StringComparison.Ordinal);
        Assert.EndsWith("guid.png", guid, StringComparison.OrdinalIgnoreCase);
        // It names the same file as the DOS resolution — identity agrees.
        Assert.Equal(
            CanvasMediaPolicy.IdentityForTests(Path.Combine(_fixture.Root, "guid.png")),
            CanvasMediaPolicy.IdentityForTests(guid!));
    }

    /// <summary>
    /// Identity containment accepts a file whose vault ROOT is itself a
    /// junction — which a TEXT prefix rejects, because the resolved
    /// terminal path (through the junction) no longer starts with the
    /// root's own spelling. This is the identity-vs-text discriminator
    /// that needs no privileged filesystem feature: a junction is
    /// unprivileged, and it forces the two answers apart.
    /// </summary>
    /// <remarks>
    /// Mutation-verified: a text-prefix `ReachesRootByIdentity` refuses
    /// this file. The case-sensitive-directory sibling (codex defect 3)
    /// needs per-directory case sensitivity, which is non-default and
    /// needs admin; it is recorded as a manual/bounded residual in CD-38,
    /// and this fact pins the same underlying rule — containment is
    /// identity, not text — through a reproducible construction.
    /// </remarks>
    [Fact]
    public void IdentityContainmentAcceptsAJunctionRootedVaultAtextPrefixWouldReject()
    {
        string real = Path.Combine(_fixture.Root, "real-root");
        Directory.CreateDirectory(real);
        File.WriteAllBytes(Path.Combine(real, "cover.png"), [0x89, 0x50, 0x4E, 0x47]);
        string junctionRoot = Path.Combine(_fixture.Root, "link-root");
        RunCommand($"mklink /J \"{junctionRoot}\" \"{real}\"");
        try
        {
            Assert.True(Directory.Exists(junctionRoot), "the junction root was not created");

            // The vault is opened AT the junction. The file resolves
            // through it to real-root\cover.png, whose path does NOT
            // start with ...\link-root — a text prefix refuses it,
            // identity (both sides resolve to the real dir) accepts it.
            string? resolved = CanvasMediaPolicy.ResolveInsideVault(junctionRoot, "cover.png");
            Assert.NotNull(resolved);
            Assert.Equal(
                CanvasMediaPolicy.IdentityForTests(Path.Combine(real, "cover.png")),
                CanvasMediaPolicy.IdentityForTests(resolved!));

            // And it still launches by the identity gate.
            string? handed = null;
            Assert.True(CanvasMediaPolicy.OpenMediaInVault(
                junctionRoot, "cover.png", target => { handed = target; return true; }));
            Assert.NotNull(handed);
        }
        finally
        {
            try
            {
                if (Directory.Exists(junctionRoot))
                {
                    Directory.Delete(junctionRoot);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// The extended (\\?\) form is kept end to end: a vault-root
    /// component with a TRAILING DOT verifies and launches the identity
    /// it checked, not the sibling ShellExecute would renormalize a bare
    /// path to (`vault.` → `vault`).
    /// </summary>
    /// <remarks>
    /// The trailing-dot directory can only be CREATED through the \\?\
    /// prefix (Win32 strips trailing dots), which needs no privilege.
    /// This is the launch-integrity bug codex flagged: verify one string,
    /// launch another.
    /// </remarks>
    [Fact]
    public void ATrailingDotVaultComponentLaunchesTheVerifiedIdentity()
    {
        // Two sibling dirs: `dotdir.` (trailing dot, made via \\?\) and a
        // plain `dotdir` that ShellExecute would renormalize TO. Give
        // them DIFFERENT media so a renormalization is detectable.
        string root = Path.GetFullPath(_fixture.Root);
        string withDot = $@"\\?\{root}\dotdir.";
        string without = Path.Combine(root, "dotdir");
        Directory.CreateDirectory(withDot);
        Directory.CreateDirectory(without);
        File.WriteAllBytes($@"{withDot}\pic.png", [0x89, 0x50, 0x4E, 0x47]);
        File.WriteAllBytes(Path.Combine(without, "pic.png"), [0x89, 0x50, 0x4E, 0x47]);

        // A vault ROOTED at the trailing-dot directory, opening its own
        // media. The resolved+launched path must retain the dot.
        string? handed = null;
        bool opened = CanvasMediaPolicy.OpenMediaInVault(
            withDot, "pic.png", target => { handed = target; return true; });

        Assert.True(opened, "the trailing-dot vault refused its own media");
        Assert.NotNull(handed);
        // The launched string is the EXTENDED form — that is what stops
        // ShellExecute renormalizing the dot away. Stripping the prefix
        // (the bug) would hand a bare path ShellExecute collapses to the
        // sibling; this is the deterministic discriminator.
        Assert.StartsWith(@"\\?\", handed, StringComparison.Ordinal);
        Assert.Contains("dotdir.", handed, StringComparison.Ordinal);
        // And it names the dot directory's file, not the sibling — both
        // by identity through the extended spelling.
        Assert.Equal(
            CanvasMediaPolicy.IdentityForTests($@"{withDot}\pic.png"),
            CanvasMediaPolicy.IdentityForTests(handed!));
    }

    /// <summary>
    /// B2 — a request for owner B strands when the presenter rebinds
    /// A→B while the Model is IDENTICAL (both panes share one document),
    /// because OnModelChanged does not fire. The DataContextChanged
    /// trigger delivers it.
    /// </summary>
    [Fact]
    public void ARequestDeliversWhenThePresenterRebindsToItsOwner() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel paneA =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        ((System.Windows.Input.ICommand)workspace.SplitRightCommand).Execute(null);
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel paneB =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        // One document, two owners — the churn shape.
        Assert.Same(paneA.Canvas, paneB.Canvas);

        // A presenter mounted on A, then rebound to B: the Model
        // reference never changes, only the DataContext.
        var surface = new CanvasSurfaceView { DataContext = paneA, Model = paneA.Canvas };
        using var host = Host(surface);

        paneB.Canvas!.RequestFocusLanding(paneB);
        Assert.NotNull(paneB.Canvas.FocusRequest);

        surface.DataContext = paneB;
        host.UpdateLayout();

        // The rebind delivered B's request.
        Assert.Null(paneB.Canvas.FocusRequest);
        Assert.NotNull(FocusedRow(host));
    });

    /// <summary>
    /// The DELIVERY half of the dismissal-fallback story: a bare focus
    /// request — no publish, no open, no other trigger, exactly what
    /// MainWindow's canvas arm raises — lands on the outline row.
    /// </summary>
    /// <remarks>
    /// This does NOT exercise MainWindow's arm (that is not reachable
    /// in-process); it proves the thing the arm relies on. That the arm
    /// actually RAISES the request — and does not merely return bare — is
    /// the two-sided source census
    /// `TheEditorPaneFocusFallbackStandsAsideForACanvasTab`, which
    /// mutation-fails when the raise is removed. Split this way on
    /// purpose: an earlier single fact called `RequestFocusLanding`
    /// itself and so proved nothing about the arm at all (Major-2).
    /// </remarks>
    [Fact]
    public void ABareFocusRequestDeliversWithoutAnyOtherTrigger() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("board.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        var surface = new CanvasSurfaceView { DataContext = tab, Model = document };
        var elsewhere = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(elsewhere);
        panel.Children.Add(surface);
        using var host = Host(panel);

        // A sheet had focus and is dismissing; focus is nowhere useful.
        Assert.True(
            elsewhere.Focus(),
            "premise: elsewhere refused keyboard focus, so this arrangement never established.");
        Assert.Null(FocusedRow(host));

        // What MainWindow's canvas arm does.
        document.RequestFocusLanding(tab);
        host.UpdateLayout();

        Assert.Null(document.FocusRequest);
        Assert.NotNull(FocusedRow(host));
    });

    /// <summary>
    /// A rename opens the destination ONCE. The re-key loop's
    /// <c>CanvasDocumentFor</c> loads on a miss, so an unconditional
    /// reload after it read the file twice and spoke the degraded-load
    /// sentence twice.
    /// </summary>
    [Fact]
    public void ARenameOfADegradedCanvasAnnouncesTheLoadOnce()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "renameme.canvas"),
            File.ReadAllText(Path.Combine(_fixture.Root, "skipped.canvas")));
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("renameme.canvas");
        CanvasDocumentViewModel before = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        before.AnnouncerForTests.FlushForTests();
        Assert.Single(_announced);
        _announced.Clear();

        File.Move(
            Path.Combine(_fixture.Root, "renameme.canvas"),
            Path.Combine(_fixture.Root, "renamed-degraded.canvas"));
        workspace.RetargetPath("renameme.canvas", "renamed-degraded.canvas");

        CanvasDocumentViewModel after = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);
        after.AnnouncerForTests.FlushForTests();
        Assert.Equal(CanvasLoadState.Ready, after.State);
        // ONE open, one sentence.
        Assert.Single(_announced);
    }

    // --- B5: the announcer is retired with its document --------------------

    /// <summary>
    /// A coalesced line is a timer holding a rendered string. Closing the
    /// last tab on a canvas the user had just moved around in used to
    /// leave one queued on a retired document, firing ~200 ms later —
    /// the shell speaking about a surface that is gone.
    /// </summary>
    [Fact]
    public void NothingSpeaksAfterTheLastTabClosed() => RunSta(() =>
    {
        var posted = new List<RenderedAnnouncement>();
        using var workspace = new WorkspaceViewModel(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: posted.Add);
        workspace.OpenPath("board.canvas");
        CanvasDocumentViewModel document = Assert.IsType<CanvasDocumentViewModel>(
            workspace.ActiveGroup.ActiveTab!.Canvas);

        // A move, still inside the coalescing window.
        document.SelectNode("evidence");
        Assert.Empty(posted);

        // The last tab closes: the document is retired mid-window.
        ((System.Windows.Input.ICommand)workspace.CloseActiveTabCommand).Execute(null);

        // The queued line is DROPPED, not flushed: the reason to say it
        // (the user is reading that canvas) stopped being true.
        document.AnnouncerForTests.FlushForTests();
        Assert.Empty(posted);

        // And a LATE caller is refused, not merely un-queued. This is
        // the other half of retirement: a path that outlived its
        // document must not reach the dispatcher at all.
        using (DebugAsserts.Suppressed())
        {
            document.AnnouncerForTests.Announce(
                new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NoMarks()));
        }
        document.AnnouncerForTests.FlushForTests();
        Assert.Empty(posted);
    });

    // --- A17: the §K budget, in BOTH scheduling modes ------------------------

    /// <summary>
    /// The W4-5 lesson — test the mode users run. Synchronous mode
    /// orders the load body deterministically and makes every
    /// generation guard dead code, so the production arm is the one
    /// that proves the publish path.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LargeCanvasOutlineBuildsUnderBudget(bool synchronousForTests)
    {
        string large = Path.Combine(
            SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures",
            "canvas", "large_2000.canvas");
        Assert.True(File.Exists(large), $"the §K fixture is missing at {large}");
        File.Copy(large, Path.Combine(_fixture.Root, "large.canvas"), overwrite: true);

        CanvasDocumentViewModel document = NewDocument("large.canvas", synchronousForTests);
        var clock = Stopwatch.StartNew();
        document.Load();
        if (!synchronousForTests)
        {
            for (int round = 0; round < 20 && document.State == CanvasLoadState.Loading;
                round++)
            {
                await document.DrainForTests();
                await Task.Delay(5);
            }
        }
        clock.Stop();

        Assert.Equal(CanvasLoadState.Ready, document.State);
        Assert.Equal(2000, document.Outline.Count);
        // 500 ms is the §K interactive budget the mac renderer suite
        // also asserts — a regression that makes opening a 2,000-node
        // canvas non-interactive fails here. The measured figures live
        // in BENCHMARKS.md, taken by CanvasOpenBenchmarks, not by this
        // clock.
        //
        // In the ASYNCHRONOUS arm the number is a CEILING, not a
        // measurement: the drain loop polls at 5 ms, so a load that
        // finished at t=1 ms can be observed as late as t=6 ms and the
        // reading is quantized to that granularity. That is fine for
        // what this asserts — an order of magnitude of headroom — and
        // it is why the elapsed value is never recorded anywhere as a
        // benchmark.
        Assert.True(
            clock.ElapsedMilliseconds < 500,
            $"opening a 2,000-node canvas took {clock.ElapsedMilliseconds} ms "
            + $"(synchronousForTests: {synchronousForTests})");
        document.Shutdown();
        await document.WhenHandleClosed();
    }

    [Fact]
    public void TheOutlineTreeBuildsEveryRowOfTheLargeFixture() => RunSta(() =>
    {
        File.Copy(
            Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures",
                "canvas", "large_2000.canvas"),
            Path.Combine(_fixture.Root, "large.canvas"),
            overwrite: true);
        CanvasDocumentViewModel document = NewDocument("large.canvas");
        document.Load();
        var clock = Stopwatch.StartNew();
        var view = new CanvasOutlineView { Model = document };
        clock.Stop();

        Assert.Equal(document.Outline.Count, CountLines(view.RootsForTests));
        Assert.True(
            clock.ElapsedMilliseconds < 500,
            $"projecting 2,000 rows took {clock.ElapsedMilliseconds} ms");
        document.Shutdown();
    });

    private static int CountLines(IEnumerable<CanvasOutlineRowViewModel> rows) =>
        rows.Sum(row => 1 + CountLines(row.Children));

    /// <summary>A shown, laid-out window: containers exist, focus is
    /// real, and a raised key event has a live PresentationSource.</summary>
    private static HostedWindow Host(UIElement content)
    {
        var window = new Window
        {
            Content = content,
            Width = 900,
            Height = 700,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window);
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        internal void UpdateLayout() => window.UpdateLayout();

        internal IInputElement? FocusedElement() =>
            System.Windows.Input.FocusManager.GetFocusedElement(window);

        public void Dispose() => window.Close();
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
