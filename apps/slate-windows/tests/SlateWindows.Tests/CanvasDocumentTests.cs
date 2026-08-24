// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Windows;
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
        document.Announcer.FlushForTests();

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

        document.Announcer.FlushForTests();
        RenderedAnnouncement spoken = Assert.Single(_announced);
        Assert.Equal(document.DegradedBannerText, spoken.Text);
    }

    [Fact]
    public void AReloadIsAnOpenAndReArmsTheAnnouncement()
    {
        CanvasDocumentViewModel document = NewDocument("skipped.canvas");
        document.Load();
        document.Announcer.FlushForTests();
        Assert.Single(_announced);

        document.Load();
        document.Announcer.FlushForTests();
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
        // 0a-13 LABEL class: core composes it, the host supplies only
        // the display chord it owns (contract 0a-9).
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasEmptyOnboarding(
                    CanvasDocumentViewModel.PaletteChord,
                    CanvasDocumentViewModel.PaletteChord))).Text,
            document.EmptyOnboardingText);
        // The t2 rule: until PR E ships New Card, the copy never
        // advertises a chord that does nothing.
        Assert.Contains(CanvasDocumentViewModel.PaletteChord, document.EmptyOnboardingText);
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
        document.Announcer.FlushForTests();

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
        document.Announcer.FlushForTests();
        _announced.Clear();

        document.SelectNode("evidence");
        document.Announcer.FlushForTests();
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
        document.Announcer.FlushForTests();

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
        document.Announcer.FlushForTests();
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
        document.Announcer.FlushForTests();
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
        document.Announcer.FlushForTests();
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
        document.Announcer.FlushForTests();
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
        document.Announcer.FlushForTests();
        int afterModelMove = _announced.Count;
        Assert.True(afterModelMove > 0);

        // View → model: one move announced, not two — the model's
        // re-seat must not echo back through the tree (contract A12).
        loose.IsSelected = true;
        host.UpdateLayout();
        document.Announcer.FlushForTests();
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

    [Fact]
    public void OpeningLandsFocusOnTheFirstRowAndReturningRestoresIt() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        // A rendered visual tree is what makes focus real.
        using var host = Host(surface);
        CanvasOutlineRowViewModel landed = Assert.IsType<CanvasOutlineRowViewModel>(
            surface.OutlineForTests.FocusLandingRow());
        Assert.Equal(document.Outline[0].NodeId, landed.Id);
        Assert.Equal(document.Outline[0].NodeId, document.Selection.Selected);
        Assert.Same(landed, FocusedRow(host));

        // WCAG 2.4.3: after activating a card, coming back lands on
        // THAT row, not the top.
        document.OpenFileCardFromSurface = (_, _) => true;
        _ = document.Activate(Row(document, "note"));
        CanvasOutlineRowViewModel restored = Assert.IsType<CanvasOutlineRowViewModel>(
            surface.OutlineForTests.FocusLandingRow());
        Assert.Equal("note", restored.Id);
        Assert.Equal("note", document.Selection.Selected);
        Assert.Same(restored, FocusedRow(host));
        document.Shutdown();
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
            // t0 §5's focusable detail rows, one per preserved entry.
            Assert.Equal(Visibility.Visible, surface.WarningRowsForTests.Visibility);
            Assert.Equal(
                skipped.PreservedItemCount,
                surface.WarningRowsForTests.ItemsSource.Cast<object>().Count());
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
    public void TheSurfaceSwitcherIsNamedAndTheUnshippedArmsAreDisabled() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("board.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };

        Assert.False(surface.TableChoiceForTests.IsEnabled);
        Assert.Equal(
            CanvasPhrase.TableShipsLater,
            System.Windows.Automation.AutomationProperties.GetHelpText(
                surface.TableChoiceForTests));
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
        // The mac core path is 5.62 ms at this scale; the C# delta is
        // marshalling. 500 ms is the §K interactive budget the renderer
        // suite also asserts — a regression that makes opening a
        // 2,000-node canvas non-interactive fails here.
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
