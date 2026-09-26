// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;
using Verb = SlateWindows.Canvas.CanvasContextVerb;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-8 (IE-31), widened by §G2 TG2-7 (G2-12): the ONE
/// applicability plan over (surface, target), mac's order per
/// projection, the outline menu's bidirectional equality against it,
/// and no silent arm — never a dead click, never a hand-list. W7-7 R-12
/// (#1256, OD-3) adds the visual board as a consumer, and the keyboard
/// request that opens a fresh row's menu or the seated card's — never
/// an ancestor's.
/// </summary>
public sealed class CanvasContextMenuTests
{
    private static readonly CanvasNeighbor Captured =
        new("e1", "n2", "Other", default, null, null, true);

    /// <summary>Every target the censuses walk: the five node kinds in
    /// and out of a group, and a captured connection.</summary>
    private static IEnumerable<(string Name, CanvasContextTarget Target)> Targets()
    {
        foreach (string kind in CanvasContextMenuPlan.NodeKinds)
        {
            yield return (kind, new CanvasContextTarget.Node("n1", kind, false));
            yield return (kind + " in a group", new CanvasContextTarget.Node("n1", kind, true));
        }
        yield return ("connection", new CanvasContextTarget.Connection("n1", Captured));
    }

    private static IEnumerable<string> Names(CanvasContextSurface surface, CanvasContextTarget target) =>
        CanvasContextMenuPlan.RowsFor(surface, target).Select(row => row.Name);

    /// <summary>The plan's tables per (surface, target): mac's order,
    /// the kind-scoped rows present exactly where mac places them, Set
    /// Color on a group, Remove from Group only inside a group, a
    /// connection's three rows, and the grid's subset from the SAME
    /// plan.</summary>
    [Fact]
    public void ThePlanServesMacsOrderPerSurfaceAndTarget()
    {
        Assert.Equal(
            [
                CanvasPhrase.OpenRowAction,
                CanvasPhrase.EditCardRowAction,
                CanvasPhrase.ConvertToNoteRowAction,
                CanvasPhrase.CreateConnectedCardRowAction,
                CanvasPhrase.DuplicateRowAction,
                CanvasPhrase.ToggleMarkRowAction,
                CanvasPhrase.ConnectToRowAction,
                CanvasPhrase.SetColorRowAction,
                CanvasPhrase.MoveIntoGroupRowAction,
                CanvasPhrase.DeleteRowAction,
            ],
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("n1", "text", false)));

        Assert.Equal(
            [
                CanvasPhrase.OpenRowAction,
                CanvasPhrase.CreateConnectedCardRowAction,
                CanvasPhrase.DuplicateRowAction,
                CanvasPhrase.RenameGroupRowAction,
                CanvasPhrase.ToggleMarkRowAction,
                CanvasPhrase.ConnectToRowAction,
                CanvasPhrase.SetColorRowAction,
                CanvasPhrase.MoveIntoGroupRowAction,
                CanvasPhrase.RemoveFromGroupRowAction,
                CanvasPhrase.UngroupRowAction,
            ],
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("g1", "group", true)));

        // Locate File… on file and image cards, nowhere else; Edit Card
        // Text… and Convert to Note… on text cards only.
        foreach (string kind in (string[])["file", "image"])
        {
            IEnumerable<string> names =
                Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("n1", kind, false));
            Assert.Contains(CanvasPhrase.LocateFileRowAction, names);
            Assert.DoesNotContain(CanvasPhrase.EditCardRowAction, names);
            Assert.DoesNotContain(CanvasPhrase.ConvertToNoteRowAction, names);
        }
        IEnumerable<string> link =
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("n1", "link", false));
        Assert.DoesNotContain(CanvasPhrase.LocateFileRowAction, link);
        Assert.DoesNotContain(CanvasPhrase.EditCardRowAction, link);
        Assert.DoesNotContain(CanvasPhrase.RemoveFromGroupRowAction, link);

        Assert.Equal(
            [
                CanvasPhrase.JumpToCardRowAction,
                CanvasPhrase.EditConnectionRowAction,
                CanvasPhrase.DeleteConnectionRowAction,
            ],
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Connection("n1", Captured)));

        Assert.Equal(
            [CanvasPhrase.OpenRowAction, CanvasPhrase.ToggleMarkRowAction, CanvasPhrase.DeleteRowAction],
            Names(CanvasContextSurface.Grid, new CanvasContextTarget.Node("n1", "text", true)));
        Assert.Equal(
            [CanvasPhrase.OpenRowAction, CanvasPhrase.ToggleMarkRowAction, CanvasPhrase.UngroupRowAction],
            Names(CanvasContextSurface.Grid, new CanvasContextTarget.Node("g1", "group", false)));
        // The grid has no connection rows (G2-12).
        Assert.Empty(CanvasContextMenuPlan.RowsFor(
            CanvasContextSurface.Grid, new CanvasContextTarget.Connection("n1", Captured)));
    }

    /// <summary>No silent arm in any projection: a row is live, or it
    /// carries the WHY a reader can hear (the mac contract's
    /// visible-with-reason shape). Today every row is live — the last
    /// staged reason retired with the grid's Toggle Mark.</summary>
    [Fact]
    public void EveryRowIsLiveOrCarriesItsReason()
    {
        foreach (CanvasContextSurface surface in Enum.GetValues<CanvasContextSurface>())
        {
            foreach ((string name, CanvasContextTarget target) in Targets())
            {
                foreach (CanvasContextMenuRow row in CanvasContextMenuPlan.RowsFor(surface, target))
                {
                    Assert.True(
                        row.Enabled == (row.DisabledReason is null),
                        $"{surface}/{name}/{row.Name}: a staged row without a reason — or a "
                        + "live row carrying one — breaks the visible-why contract.");
                    Assert.True(row.Enabled, $"{surface}/{name}/{row.Name}: G2-12 retired the last staged row.");
                }
            }
        }
    }

    /// <summary>Every verb the enum can name is placed by SOME
    /// projection and carries mac's label through the one label table
    /// — no orphan verb, no row whose name is not its verb's.</summary>
    [Fact]
    public void EveryVerbIsPlacedSomewhereUnderItsOwnLabel()
    {
        var placed = new HashSet<CanvasContextVerb>();
        foreach (CanvasContextSurface surface in Enum.GetValues<CanvasContextSurface>())
        {
            foreach ((_, CanvasContextTarget target) in Targets())
            {
                foreach (CanvasContextMenuRow row in CanvasContextMenuPlan.RowsFor(surface, target))
                {
                    placed.Add(row.Verb);
                    Assert.Equal(CanvasContextMenuPlan.Label(row.Verb), row.Name);
                }
            }
        }
        foreach (CanvasContextVerb verb in Enum.GetValues<CanvasContextVerb>())
        {
            Assert.Contains(verb, placed);
            Assert.False(string.IsNullOrWhiteSpace(CanvasContextMenuPlan.Label(verb)));
        }
    }

    /// <summary>IE-31's census, per target (G2-12): the outline's built
    /// menu equals the plan's OUTLINE projection — headers, enabled
    /// flags and reasons verbatim, the same count both ways — so the
    /// surface can neither hand-list a subset nor carry a row the plan
    /// did not give it.</summary>
    [Fact]
    public void TheOutlineMenuEqualsThePlan() => RunSta(() =>
    {
        foreach ((string name, CanvasContextTarget target) in Targets())
        {
            // The census drives the ONE plan-to-menu mapping the
            // opening handler itself uses.
            AssertTheMenuIsThePlan(
                CanvasContextSurface.Outline,
                target,
                name,
                CanvasOutlineView.BuildMenuFromPlan(target, _ => { }));
        }
    });

    /// <summary>IE-31's census on the BOARD (R-12, OD-3; the census
    /// covers every consumer): the renderer's built menu equals the plan's
    /// RENDERER projection per target — headers, enabled flags and reasons
    /// verbatim, the same count both ways — through the one mapping the
    /// board's opening handler uses.</summary>
    [Fact]
    public void TheBoardMenuEqualsThePlan() => RunSta(() =>
    {
        foreach ((string name, CanvasContextTarget target) in Targets())
        {
            AssertTheMenuIsThePlan(
                CanvasContextSurface.Renderer,
                target,
                name,
                CanvasRendererView.BuildMenuFromPlan(target, _ => { }));
        }
    });

    /// <summary>
    /// R-12 (OD-3), contract 34 E17's applicable-verb inventory: the
    /// board's card menu per node kind, in and out of a group, pinned
    /// INDEPENDENTLY of the plan — every row written out, in mac's outline
    /// order (<c>CanvasOutlineView.swift</c>, IG2-29) — and nothing for a
    /// connection, which a board request never targets.
    /// </summary>
    /// <remarks>
    /// The plan DEFINES the renderer's projection, so the consumer census
    /// (<see cref="TheBoardMenuEqualsThePlan"/>) follows a projection that
    /// lost a verb straight down with it: a board menu of two rows would
    /// equal a two-row plan. This table is the inventory itself, spelled
    /// out rather than derived, and a missing or extra verb fails by name.
    /// </remarks>
    [Fact]
    public void TheBoardsCardMenuIsTheApplicableVerbInventoryPerKind()
    {
        var inventory = new (string Kind, bool InGroup, Verb[] Verbs)[]
        {
            ("text", false,
            [
                Verb.Open, Verb.EditCard, Verb.ConvertToNote, Verb.CreateConnectedCard, Verb.Duplicate,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.Delete,
            ]),
            ("text", true,
            [
                Verb.Open, Verb.EditCard, Verb.ConvertToNote, Verb.CreateConnectedCard, Verb.Duplicate,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.RemoveFromGroup,
                Verb.Delete,
            ]),
            ("file", false,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate, Verb.LocateFile,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.Delete,
            ]),
            ("file", true,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate, Verb.LocateFile,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.RemoveFromGroup,
                Verb.Delete,
            ]),
            ("image", false,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate, Verb.LocateFile,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.Delete,
            ]),
            ("image", true,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate, Verb.LocateFile,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.RemoveFromGroup,
                Verb.Delete,
            ]),
            ("link", false,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.Delete,
            ]),
            ("link", true,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.RemoveFromGroup,
                Verb.Delete,
            ]),
            ("group", false,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate, Verb.RenameGroup,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.Ungroup,
            ]),
            ("group", true,
            [
                Verb.Open, Verb.CreateConnectedCard, Verb.Duplicate, Verb.RenameGroup,
                Verb.ToggleMark, Verb.ConnectTo, Verb.SetColor, Verb.MoveIntoGroup, Verb.RemoveFromGroup,
                Verb.Ungroup,
            ]),
        };
        // The table covers every kind core names, in and out of a group.
        Assert.Equal(
            CanvasContextMenuPlan.NodeKinds.Order(StringComparer.Ordinal),
            inventory.Select(entry => entry.Kind).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(CanvasContextMenuPlan.NodeKinds.Length * 2, inventory.Length);

        var failures = new List<string>();
        foreach ((string kind, bool inGroup, Verb[] verbs) in inventory)
        {
            Verb[] board =
            [
                .. CanvasContextMenuPlan.RowsFor(
                    CanvasContextSurface.Renderer, new CanvasContextTarget.Node("n1", kind, inGroup))
                    .Select(row => row.Verb),
            ];
            if (!board.SequenceEqual(verbs))
            {
                failures.Add(
                    $"{kind}{(inGroup ? " in a group" : string.Empty)}: missing ["
                    + string.Join(", ", verbs.Except(board)) + "], extra ["
                    + string.Join(", ", board.Except(verbs)) + "], order ["
                    + string.Join(", ", board) + "]");
            }
        }
        Assert.True(
            CanvasContextMenuPlan.RowsFor(
                CanvasContextSurface.Renderer, new CanvasContextTarget.Connection("n1", Captured)).IsEmpty,
            "the board placed rows on a connection, which none of its requests can target.");
        Assert.True(
            failures.Count == 0,
            "the board's card menu is not the applicable-verb inventory (contract 34 E17):\n"
            + string.Join("\n", failures));
    }

    /// <summary>OD-3 (R-12; G2D-12 lifted, contract 34 E17's "renderer
    /// card"): the visual board carries the SAME derived card menu as the
    /// outline row — row for row, flags and reasons included, over every
    /// node kind in and out of a group — and no connection rows, because
    /// a board request targets a card: the seat, or the card under the
    /// pointer.</summary>
    [Fact]
    public void TheBoardCarriesTheOutlinesCardMenu()
    {
        foreach ((string name, CanvasContextTarget target) in Targets())
        {
            ImmutableArray<CanvasContextMenuRow> board =
                CanvasContextMenuPlan.RowsFor(CanvasContextSurface.Renderer, target);
            if (target is CanvasContextTarget.Connection)
            {
                Assert.True(board.IsEmpty, $"{name}: the board placed rows on a connection it cannot target.");
                continue;
            }
            Assert.True(
                board.SequenceEqual(CanvasContextMenuPlan.RowsFor(CanvasContextSurface.Outline, target)),
                $"{name}: the board's card menu is not the outline row's.");
        }
    }

    /// <summary>
    /// R-12 (#1256; the NVDA pass's F13), the
    /// <c>ConnectionsLeafViewTests</c> shape: a FRESH outline row — never
    /// asked for a menu, and not the first — carries its menu from the
    /// container's construction, so a keyboard request through WPF's own
    /// popup service opens THAT row's menu, built for that row, and never
    /// an ancestor's (here a stand-in for the workspace tab's). The menu
    /// acts on that row and no other, and a second request opens the same
    /// menu — mutated, never replaced.
    /// </summary>
    /// <remarks>
    /// The pass heard the tab's menu (Duplicate Tab … Close Pane) on the
    /// first Shift+F10 of a row and the card's on the next request,
    /// because the row's menu was first ASSIGNED inside the opening event
    /// and WPF opens the menu that exists when a request arrives — the
    /// first element up the route that carries one.
    /// </remarks>
    [Fact]
    public void AKeyboardRequestOnAFreshRowOpensThatRowsMenu() => RunSta(() =>
    {
        using var vault = new BoardVault();
        CanvasDocumentViewModel document = vault.Open();
        var surface = new CanvasSurfaceView { Model = document };
        ContextMenu tabMenu = TabMenuStandIn();
        using HostedWindow host = Host(new Border { ContextMenu = tabMenu, Child = surface });
        string first = document.Outline[0].NodeId;
        Assert.True(first != "evidence", "premise: the row under test must not be the first.");
        CanvasOutlineRowViewModel row = Assert.IsType<CanvasOutlineRowViewModel>(
            surface.OutlineForTests.DeliverFocus("evidence"));
        host.UpdateLayout();
        CanvasOutlineItem item = FocusedRowContainer(row);
        ContextMenu? persistent = item.ContextMenu;

        PressApplicationsKey(item);
        Assert.False(
            tabMenu.IsOpen,
            "a keyboard request on a FRESH row opened the ANCESTOR's menu — the workspace "
            + "tab's (F13): the row's menu did not exist when the request arrived.");
        Assert.True(persistent is { IsOpen: true }, "the fresh row's own menu did not open.");
        Assert.Same(persistent, item.ContextMenu);
        Assert.Equal(
            PlanNames(CanvasContextSurface.Outline, CanvasOutlineView.TargetOf(row)!),
            Headers(persistent!));

        Choose(persistent!, CanvasPhrase.ToggleMarkRowAction);
        Assert.True(
            document.Selection.IsMarked("evidence"),
            "Toggle Mark from the row's menu did not mark the row the menu was opened on.");
        Assert.False(document.Selection.IsMarked(first), "Toggle Mark from a non-first row's menu marked the first row.");
        Assert.Single(document.Selection.Marked);

        // The second request: the same menu, rebuilt in place.
        item = FocusedRowContainer(row);
        PressApplicationsKey(item);
        Assert.False(tabMenu.IsOpen, "the second request climbed to the ancestor's menu.");
        Assert.True(persistent!.IsOpen, "the second request did not reopen the row's menu.");
        Assert.Same(persistent, item.ContextMenu);
        persistent.IsOpen = false;
        Pump();
    });

    /// <summary>
    /// R-12 (#1256), OD-3: the Applications key on the visual board opens
    /// the board's OWN menu for the SEATED card — not the first card —
    /// from the plan's renderer projection, and never an ancestor's (the
    /// board carried no menu, so the pass's request climbed to the tab's:
    /// F13). The menu persists across requests and acts on the seated card
    /// and no other; with no seat there is no menu at all — the request is
    /// answered on the board, never handed up.
    /// </summary>
    [Fact]
    public void AKeyboardRequestOnTheBoardOpensTheSeatedCardsMenu() => RunSta(() =>
    {
        using var vault = new BoardVault();
        CanvasDocumentViewModel document = vault.Open();
        var surface = new CanvasSurfaceView { Model = document };
        ContextMenu tabMenu = TabMenuStandIn();
        using HostedWindow host = Host(new Border { ContextMenu = tabMenu, Child = surface });
        CanvasRendererView board = ShowBoard(document, surface, host);
        string first = document.Outline[0].NodeId;
        Assert.True(first != "evidence", "premise: the card under test must not be the first.");
        document.SeatSelectionSilently("evidence");
        ContextMenu? persistent = board.ContextMenu;

        PressApplicationsKey(board);
        Assert.False(
            tabMenu.IsOpen,
            "the Applications key on the board opened the ANCESTOR's menu — the workspace "
            + "tab's (F13): the board answered with no menu of its own.");
        Assert.True(persistent is { IsOpen: true }, "the board's own menu did not open for the seated card.");
        Assert.Same(persistent, board.ContextMenu);
        CanvasOutlineRow seat = document.RowFor("evidence")!;
        Assert.Equal(
            PlanNames(
                CanvasContextSurface.Renderer,
                new CanvasContextTarget.Node(seat.NodeId, seat.Kind, seat.GroupPath.Length > 0)),
            Headers(persistent!));

        Choose(persistent!, CanvasPhrase.ToggleMarkRowAction);
        Assert.True(
            document.Selection.IsMarked("evidence"),
            "Toggle Mark from the board's menu did not mark the seated card.");
        Assert.False(document.Selection.IsMarked(first), "Toggle Mark from the board's menu marked the first card.");
        Assert.Single(document.Selection.Marked);

        PressApplicationsKey(board);
        Assert.True(persistent!.IsOpen, "the second request did not reopen the board's menu.");
        Assert.Same(persistent, board.ContextMenu);
        persistent.IsOpen = false;
        Pump();

        // No seat: no menu — and still never the tab's — and the press SAYS
        // so (contract 34 C3/C4/E8a, #1283): the existing Nothing selected.
        // arm, the sentence the board's Right and Left speak for the same
        // seatless state, where the press used to be swallowed in silence.
        document.SeatSelectionSilently(null);
        document.AnnouncerForTests.FlushForTests();
        vault.Announced.Clear();
        PressApplicationsKey(board);
        Assert.False(tabMenu.IsOpen, "with no seat the request climbed to the ancestor's menu.");
        Assert.False(persistent.IsOpen, "with no seat the board opened a menu for nothing.");
        document.AnnouncerForTests.FlushForTests();
        string nothingSelected = CanvasAnnouncer.RenderLabel(
            new CanvasA11yEvent.CanvasStatus(new CanvasStatusNote.NothingSelected()));
        Assert.True(
            vault.Announced.SequenceEqual([nothingSelected]),
            $"with no seat the menu key said [{string.Join(" | ", vault.Announced)}], not "
            + $"\"{nothingSelected}\" alone: a keypress that does nothing must say so (C3, C4, #1283).");
    });

    /// <summary>
    /// #1283, contract 34 C3: a keyboard menu request on an outline with NO
    /// rows opens no menu and never the tab's — and it answers, with the
    /// sentence the tree's own arrows speak for the same empty tree: the
    /// empty canvas's on a canvas with no cards, "No cards match the
    /// filter." under a needle that keeps none.
    /// </summary>
    [Fact]
    public void AKeyboardRequestOnAnEmptyOutlineSaysWhyThereIsNoMenu() => RunSta(() =>
    {
        using var vault = new BoardVault();
        foreach ((string path, string? needle, CanvasStatusNote owed) in
            new (string, string?, CanvasStatusNote)[]
            {
                ("empty.canvas", null, new CanvasStatusNote.Empty()),
                ("board.canvas", "no card says this", new CanvasStatusNote.NoCardsMatchFilter()),
            })
        {
            string leg = needle is null ? "an empty canvas" : "a needle that keeps no card";
            CanvasDocumentViewModel document = vault.Open(path);
            var surface = new CanvasSurfaceView { Model = document };
            ContextMenu tabMenu = TabMenuStandIn();
            using HostedWindow host = Host(new Border { ContextMenu = tabMenu, Child = surface });
            host.UpdateLayout();
            if (needle is not null)
            {
                document.FilterText = needle;
                host.UpdateLayout();
            }
            CanvasOutlineView outline = surface.OutlineForTests;
            PumpUntil(() => outline.RootsForTests.Count == 0, $"premise ({leg}): the outline still shows rows.");
            Assert.True(outline.FocusTree(), $"premise ({leg}): the empty tree refused keyboard focus.");
            document.AnnouncerForTests.FlushForTests();
            vault.Announced.Clear();

            PressApplicationsKey(outline.TreeForTests);
            Assert.False(tabMenu.IsOpen, $"{leg}: the request climbed to the ANCESTOR's menu (F13's class).");
            document.AnnouncerForTests.FlushForTests();
            string sentence = CanvasAnnouncer.RenderLabel(new CanvasA11yEvent.CanvasStatus(owed));
            Assert.True(
                vault.Announced.SequenceEqual([sentence]),
                $"{leg}: the menu key said [{string.Join(" | ", vault.Announced)}], not \"{sentence}\" "
                + "alone — the empty tree swallowed the press (C3, #1283).");
        }
    });

    /// <summary>
    /// R-12 (#1256), OD-3's target rule — the diagram's: a POINTER request
    /// opens the board's menu on the card HIT at the view point, not the
    /// seat, and on nothing over empty space; a KEYBOARD request (−1, −1)
    /// opens on the seat. The keyboard fact above and the journey drive
    /// the route; this pins the rule the handler applies to both.
    /// </summary>
    [Fact]
    public void APointerRequestOnTheBoardTargetsTheHitCardAndAKeyboardRequestTheSeat() => RunSta(() =>
    {
        using var vault = new BoardVault();
        CanvasDocumentViewModel document = vault.Open();
        var surface = new CanvasSurfaceView { Model = document };
        using HostedWindow host = Host(surface);
        CanvasRendererView board = ShowBoard(document, surface, host);
        document.SeatSelectionSilently("question");

        Point loose = ViewCentre(board, "loose");
        Assert.Equal("loose", board.MenuTargetFor(pointerRequest: true, loose.X, loose.Y));
        Assert.Equal("question", board.MenuTargetFor(pointerRequest: false, -1, -1));
        Point[] corners =
        [
            new(1, 1),
            new(board.ActualWidth - 2, 1),
            new(1, board.ActualHeight - 2),
            new(board.ActualWidth - 2, board.ActualHeight - 2),
        ];
        Point empty = corners.First(point => board.HitTest(point) is null);
        Assert.Null(board.MenuTargetFor(pointerRequest: true, empty.X, empty.Y));
    });

    /// <summary>
    /// R-12 (#1256), OD-3's pointer arm through WPF's OWN right-click
    /// route: the popup service raises the menu request on the board with
    /// the cursor over card B's rendered bounds, the board's handler
    /// refills its menu for the HIT card and SEATS it — silently, contract
    /// 34 G2-12's rule for every context consumer, so the card the menu
    /// acts on is the selected card — WPF opens it, and its Toggle Mark
    /// changes card B alone, the previously seated card A untouched. The
    /// two cards take different rows (A sits in a group, B does not), so
    /// the opened menu's rows name the card it was built for.
    /// </summary>
    /// <remarks>
    /// Driven through <c>PopupControlService.RaiseContextMenuOpeningEvent</c>
    /// with the position a right-button release over B reports — the call
    /// the service's own mouse-up handler makes — because a hosted fact
    /// cannot move the shared desktop's real cursor, which is what the
    /// service reads the element and position from. Everything after that
    /// read is production: the request's routing, the handler's coordinate
    /// targeting, the menu WPF chooses to open, and the dispatch.
    /// </remarks>
    [Fact]
    public void ARightClickOnAnUnseatedCardOpensItsMenuAndActsOnItAlone() => RunSta(() =>
    {
        using var vault = new BoardVault();
        CanvasDocumentViewModel document = vault.Open();
        var surface = new CanvasSurfaceView { Model = document };
        ContextMenu tabMenu = TabMenuStandIn();
        using HostedWindow host = Host(new Border { ContextMenu = tabMenu, Child = surface });
        CanvasRendererView board = ShowBoard(document, surface, host);
        document.SeatSelectionSilently("question");
        Point overB = ViewCentre(board, "loose");
        Assert.True(board.HitTest(overB) == "loose", "premise: card B's centre does not hit card B.");
        ContextMenu? persistent = board.ContextMenu;
        document.AnnouncerForTests.FlushForTests();
        vault.Announced.Clear();

        Assert.True(RightClick(board, overB), "the right-click on card B was answered by nothing.");
        Assert.False(tabMenu.IsOpen, "a right-click on card B opened the ANCESTOR's menu (F13's class).");
        Assert.True(persistent is { IsOpen: true }, "a right-click on card B opened no card menu.");
        CanvasOutlineRow hit = document.RowFor("loose")!;
        Assert.Equal(
            PlanNames(
                CanvasContextSurface.Renderer,
                new CanvasContextTarget.Node(hit.NodeId, hit.Kind, hit.GroupPath.Length > 0)),
            Headers(persistent!));
        Assert.True(
            document.Selection.Selected == "loose",
            $"the right-click did not seat card B: the seat is \"{document.Selection.Selected}\", so the "
            + "menu's card and the selected card are two cards (G2-12: a consumer seats its row "
            + "silently before the verb).");
        document.AnnouncerForTests.FlushForTests();
        Assert.True(
            vault.Announced.Count == 0,
            "the right-click's seat spoke: [" + string.Join(" | ", vault.Announced) + "] — G2-12's seat "
            + "is silent; the menu is what the reader hears.");

        Choose(persistent!, CanvasPhrase.ToggleMarkRowAction);
        Assert.True(
            document.Selection.IsMarked("loose"),
            "Toggle Mark from the right-clicked card's menu did not mark the card under the pointer.");
        Assert.False(
            document.Selection.IsMarked("question"),
            "Toggle Mark from a right-click on card B marked the previously seated card A.");
        Assert.Single(document.Selection.Marked);
    });

    /// <summary>
    /// R-12 (#1256): a right-click on EMPTY board space opens no menu —
    /// neither the board's card menu (which still holds the previous card's
    /// rows, and would act on that card) nor the ancestor's (the workspace
    /// tab's, F13's class). The request is answered on the board with
    /// nothing, never handed up and never served stale.
    /// </summary>
    [Fact]
    public void ARightClickOnEmptyBoardOpensNoMenu() => RunSta(() =>
    {
        using var vault = new BoardVault();
        CanvasDocumentViewModel document = vault.Open();
        var surface = new CanvasSurfaceView { Model = document };
        ContextMenu tabMenu = TabMenuStandIn();
        using HostedWindow host = Host(new Border { ContextMenu = tabMenu, Child = surface });
        CanvasRendererView board = ShowBoard(document, surface, host);
        document.SeatSelectionSilently("question");
        ContextMenu persistent = board.ContextMenu!;

        // A card's menu first, so the persistent menu holds rows a
        // fall-through would serve.
        Assert.True(RightClick(board, ViewCentre(board, "loose")));
        Assert.True(persistent.IsOpen, "premise: the card's menu never opened, so its rows are not held.");
        persistent.IsOpen = false;
        Pump();
        Assert.NotEmpty(persistent.Items);

        Point[] corners =
        [
            new(1, 1),
            new(board.ActualWidth - 2, 1),
            new(1, board.ActualHeight - 2),
            new(board.ActualWidth - 2, board.ActualHeight - 2),
        ];
        Point empty = corners.First(point => board.HitTest(point) is null);
        _ = RightClick(board, empty);
        Assert.False(
            persistent.IsOpen,
            "a right-click on empty board opened the card menu — the previous card's rows, "
            + "which would act on that card.");
        Assert.False(tabMenu.IsOpen, "a right-click on empty board climbed to the ANCESTOR's menu.");
        Assert.Empty(document.Selection.Marked);
    });

    /// <summary>
    /// WPF's own right-click menu request: the popup service's mouse-up
    /// handler reads the element under the cursor and the cursor's position
    /// relative to it, then makes exactly this call — raise the request
    /// there, and open the menu its route found unless something handled
    /// it. The board hosts no child elements, so the element is the board
    /// and the position is in its view space. Answers what the service
    /// does: whether a menu opened or the request was handled.
    /// </summary>
    private static bool RightClick(CanvasRendererView board, Point at)
    {
        System.Reflection.PropertyInfo service = typeof(FrameworkElement).GetProperty(
            "PopupControlService",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException("WPF's popup service is not where this fact reaches for it.");
        object popups = service.GetValue(null)
            ?? throw new InvalidOperationException("WPF's popup service is not running.");
        System.Reflection.MethodInfo raise = popups.GetType().GetMethod(
            "RaiseContextMenuOpeningEvent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            [typeof(IInputElement), typeof(double), typeof(double), typeof(bool)])
            ?? throw new InvalidOperationException("WPF's popup service has no pointer request door to drive.");
        bool answered = (bool)raise.Invoke(popups, [board, at.X, at.Y, true])!;
        Pump();
        return answered;
    }

    private static void AssertTheMenuIsThePlan(
        CanvasContextSurface surface, CanvasContextTarget target, string name, ContextMenu menu)
    {
        ImmutableArray<CanvasContextMenuRow> planned = CanvasContextMenuPlan.RowsFor(surface, target);
        Assert.True(planned.Length == menu.Items.Count, $"{surface}/{name}: the menu and the plan differ in count.");
        foreach ((CanvasContextMenuRow expected, object actual) in planned.Zip(menu.Items.Cast<object>()))
        {
            var item = Assert.IsType<MenuItem>(actual);
            Assert.Equal(expected.Name, item.Header);
            Assert.Equal(expected.Enabled, item.IsEnabled);
            Assert.Equal(expected.DisabledReason, item.ToolTip);
        }
    }

    private static string[] PlanNames(CanvasContextSurface surface, CanvasContextTarget target) =>
        [.. CanvasContextMenuPlan.RowsFor(surface, target).Select(row => row.Name)];

    private static string[] Headers(ContextMenu menu) =>
        [.. menu.Items.OfType<MenuItem>().Select(item => (string)item.Header)];

    /// <summary>Close the menu, then run one of its rows the way a click
    /// does: the row's Click, which is where the one dispatch hangs.</summary>
    private static void Choose(ContextMenu menu, string header)
    {
        MenuItem row = menu.Items.OfType<MenuItem>().Single(item => (string)item.Header == header);
        menu.IsOpen = false;
        Pump();
        row.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Pump();
    }

    /// <summary>The keys' element must be the row's own container — the
    /// premise every keyboard-request leg stands on, named.</summary>
    private static CanvasOutlineItem FocusedRowContainer(CanvasOutlineRowViewModel row)
    {
        CanvasOutlineItem item = Assert.IsType<CanvasOutlineItem>(Keyboard.FocusedElement);
        Assert.True(
            ReferenceEquals(row, item.DataContext),
            "premise: the keys are on a row container, but not the row under test's.");
        return item;
    }

    /// <summary>
    /// The Applications key through WPF's OWN route: a key-up handed to the
    /// input manager, where the popup service's post-processing raises
    /// ContextMenuOpening on the focused element with the keyboard's −1, −1
    /// and then opens the menu the route found — the production decision
    /// itself, not a re-implementation of it. Shift+F10 is the same raise
    /// from the key-down arm, but needs a real Shift, which the journey
    /// presses.
    /// </summary>
    private static void PressApplicationsKey(System.Windows.Media.Visual within)
    {
        var args = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(within)
                ?? throw new InvalidOperationException("the element is not in a window."),
            0,
            Key.Apps)
        {
            RoutedEvent = Keyboard.KeyUpEvent,
        };
        InputManager.Current.ProcessInput(args);
    }

    /// <summary>The workspace tab's menu, stood in for: an ANCESTOR's
    /// context menu — where a request no row answered used to climb (the
    /// TabControl's, <c>WorkspaceTemplates.xaml</c>).</summary>
    private static ContextMenu TabMenuStandIn()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Duplicate Tab" });
        return menu;
    }

    /// <summary>The board shown, installed, and holding the keys — each a
    /// premise with its leg named.</summary>
    private static CanvasRendererView ShowBoard(
        CanvasDocumentViewModel document, CanvasSurfaceView surface, HostedWindow host)
    {
        document.ShowSurface(CanvasSurfaceKind.Visual);
        host.UpdateLayout();
        CanvasRendererView board = surface.VisualForTests;
        PumpUntil(
            () => board.Engine.Current is not null,
            "premise: the board never installed its first presentation state.");
        Assert.True(board.Focus(), "premise: the visual board refused keyboard focus.");
        host.UpdateLayout();
        Assert.True(surface.ProjectionHasFocus, "premise: the board holds the keys but not as the projection.");
        return board;
    }

    /// <summary>A card's centre in the board's view space, from the
    /// installed state — the space a pointer request's cursor is in.</summary>
    private static Point ViewCentre(CanvasRendererView board, string nodeId)
    {
        CanvasPresentationState state = board.Engine.Current!;
        CanvasSceneNode node = state.Source.Loaded!.Population.SceneByNode[nodeId];
        double zoom = state.Viewport.Zoom;
        return new Point(
            ((node.X + (node.Width / 2)) * zoom) + state.Viewport.PanX,
            ((node.Y + (node.Height / 2)) * zoom) + state.Viewport.PanY);
    }

    private static void PumpUntil(Func<bool> condition, string premise)
    {
        var budget = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && budget.Elapsed < TimeSpan.FromSeconds(10))
        {
            Pump();
            Thread.Yield();
        }
        Assert.True(condition(), premise);
    }

    private static void Pump()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

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

        public void Dispose() => window.Close();
    }

    /// <summary>The navigator suite's board, one canvas in a scanned vault,
    /// and the documents opened over it — the hosted facts' source, shut
    /// down with it.</summary>
    private sealed class BoardVault : IDisposable
    {
        private const string Board =
            """
            {
              "nodes": [
                {"id":"grp","type":"group","x":-40,"y":-40,"width":560,"height":400,"label":"Research"},
                {"id":"question","type":"text","text":"Core question","x":0,"y":0,"width":240,"height":140,"color":"1"},
                {"id":"evidence","type":"text","text":"Evidence zeta","x":260,"y":0,"width":220,"height":140},
                {"id":"note","type":"file","file":"note0.md","x":0,"y":180,"width":240,"height":140},
                {"id":"loose","type":"text","text":"Unfiled zeta thought","x":0,"y":460,"width":200,"height":100}
              ],
              "edges": [
                {"id":"e1","fromNode":"question","toNode":"evidence","label":"supports"},
                {"id":"e2","fromNode":"question","toNode":"note"},
                {"id":"e3","fromNode":"loose","toNode":"question","label":"revisit"}
              ]
            }
            """;

        private readonly FixtureVault _fixture;
        private readonly VaultSession _session;
        private readonly List<CanvasDocumentViewModel> _opened = [];

        /// <summary>Every rendered line the opened documents' funnels
        /// posted, in order.</summary>
        internal List<string> Announced { get; } = [];

        internal BoardVault()
        {
            _fixture = FixtureVault.Create(1, "canvas-context-menu");
            File.WriteAllText(Path.Combine(_fixture.Root, "board.canvas"), Board);
            File.WriteAllText(Path.Combine(_fixture.Root, "empty.canvas"), """{"nodes":[],"edges":[]}""");
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using var cancel = new CancelToken();
            _session.ScanInitial(cancel);
        }

        internal CanvasDocumentViewModel Open(string path = "board.canvas")
        {
            var document = new CanvasDocumentViewModel(
                _session,
                path,
                new CanvasAnnouncer(line => Announced.Add(line.Text), TimeSpan.FromMinutes(1)),
                synchronousForTests: true,
                verbosity: () => CanvasVerbosity.Standard);
            document.Load();
            _opened.Add(document);
            return document;
        }

        public void Dispose()
        {
            foreach (CanvasDocumentViewModel document in _opened)
            {
                document.Shutdown();
            }
            _session.Dispose();
            _fixture.Dispose();
        }
    }

    /// <summary>The outline row's target is DERIVED from the row: a
    /// node row's kind and group membership, a connection row's source
    /// and captured neighbor — no consumer decides its own target.</summary>
    [Fact]
    public void TheOutlineRowsTargetIsDerivedFromTheRow()
    {
        var inGroup = new CanvasOutlineRow("n1", 1, "file", "Paper", "Paper", ["Research"], 1, 2, 0, null);
        CanvasOutlineRowViewModel node = CanvasOutlineRowViewModel.ForNode(inGroup, marked: false, filtered: false);
        Assert.Equal(
            new CanvasContextTarget.Node("n1", "file", true),
            CanvasOutlineView.TargetOf(node));

        CanvasOutlineRowViewModel connection =
            CanvasOutlineRowViewModel.ForConnection(inGroup, Captured, "text", 1, 1);
        Assert.Equal(
            new CanvasContextTarget.Connection("n1", Captured),
            CanvasOutlineView.TargetOf(connection));
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
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
