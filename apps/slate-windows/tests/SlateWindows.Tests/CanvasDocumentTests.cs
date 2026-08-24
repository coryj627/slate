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
        document.Announcer.FlushForTests();
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
        document.Announcer.FlushForTests();
        Assert.Empty(_announced);

        // Invoke IS the follow path, and it does move.
        connection.RaiseActivate();
        document.Announcer.FlushForTests();
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

    /// <summary>
    /// Contract A8's UIA surface, pinned locally as well as in the
    /// journey: the item is a Tree/TreeItem pair with ExpandCollapse
    /// and SelectionItem from WPF, and Invoke from
    /// <c>CanvasOutlineItemAutomationPeer</c> — which exists because
    /// <c>TreeViewItemAutomationPeer</c> implements the first three and
    /// not the fourth. The journey is CI-arbitrated; this is not.
    /// </summary>
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

        CanvasOutlineRowViewModel group =
            Assert.Single(view.RootsForTests, row => row.Id == "grp");
        var container = Assert.IsType<CanvasOutlineItem>(
            view.TreeForTests.ItemContainerGenerator.ContainerFromItem(group));
        AutomationPeer peer = Assert.IsAssignableFrom<AutomationPeer>(
            UIElementAutomationPeer.CreatePeerForElement(container));
        Assert.Equal(AutomationControlType.TreeItem, peer.GetAutomationControlType());
        Assert.Equal(group.Name, peer.GetName());
        Assert.Equal(group.Status, peer.GetItemStatus());
        Assert.Equal(group.Hint, peer.GetHelpText());
        Assert.NotNull(peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.NotNull(peer.GetPattern(PatternInterface.SelectionItem));
        // The one WPF does not give a TreeViewItem.
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke));

        // And it really activates: Invoke on a group expands it.
        group.IsExpanded = false;
        invoke.Invoke();
        Assert.True(group.IsExpanded);
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

    // --- A18: the three surface commands ------------------------------------

    /// <summary>
    /// All three register so the palette lists the whole switcher from
    /// this slice; the two whose projections have not shipped resolve
    /// to a command whose CanExecute is false, so the registrar answers
    /// its canonical unavailable sentence rather than a per-PR string
    /// (contract A18).
    /// </summary>
    [Fact]
    public void ShowTableAndShowVisualRegisterAndStayDisabledUntilTheirProjectionsShip()
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
        foreach (string unshipped in new[]
        {
            Commands.ChordTable.Ids.CanvasShowTable,
            Commands.ChordTable.Ids.CanvasShowVisual,
        })
        {
            Assert.Equal(
                Commands.SlateCommandRegistrar.UnavailableReason,
                Commands.SlateCommandRegistrar.DisabledReason(host, unshipped));
        }

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
        document.Announcer.FlushForTests();
        Assert.Empty(_announced);

        document.ShowSurface(CanvasSurfaceKind.Table);
        document.Announcer.FlushForTests();
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
    private sealed class CanvasCommandHost(WorkspaceViewModel workspace)
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
        document.Announcer.FlushForTests();
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

    // --- M2: focus lands when the user asks, and only then -----------------

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
        var surface = new CanvasSurfaceView { Model = tab.Canvas };
        var elsewhere = new TextBox();
        var panel = new StackPanel();
        panel.Children.Add(elsewhere);
        panel.Children.Add(surface);
        using var host = Host(panel);
        Assert.True(elsewhere.Focus());
        Assert.Same(elsewhere, host.FocusedElement());

        File.Move(
            Path.Combine(_fixture.Root, "board.canvas"),
            Path.Combine(_fixture.Root, "renamed.canvas"));
        workspace.RetargetPath("board.canvas", "renamed.canvas");
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
        var surface = new CanvasSurfaceView { Model = first.Canvas };
        using var host = Host(surface);

        // The document is already loaded and published; the surface
        // mounts onto a registry HIT.
        Assert.Equal(CanvasLoadState.Ready, first.Canvas!.State);
        Assert.Null(FocusedRow(host));

        // The open funnel every user-initiated open already calls.
        workspace.RequestActiveEditorFocus();
        host.UpdateLayout();

        CanvasOutlineRowViewModel landed = Assert.IsType<CanvasOutlineRowViewModel>(
            FocusedRow(host));
        Assert.Equal(first.Canvas.Outline[0].NodeId, landed.Id);
    });

    /// <summary>An empty canvas has no row to land on; focus goes to the
    /// onboarding region rather than nowhere (m4).</summary>
    [Fact]
    public void AnEmptyCanvasLandsFocusOnTheOnboardingRegion() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("blank.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        document.RequestFocusLanding();
        host.UpdateLayout();
        Assert.Same(surface.OnboardingForTests, host.FocusedElement());
        document.Shutdown();
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
