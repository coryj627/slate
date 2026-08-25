// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 PR B (#745) facts: the canvas TABLE projection over a REAL
/// <see cref="VaultSession"/> and real <c>.canvas</c> bytes — contracts
/// B1–B11. Every fact drives the production composition
/// (<see cref="CanvasSurfaceView"/> → <see cref="CanvasTableView"/> →
/// the W4-1 substrate) and reads what the consumer reads: the rendered
/// row order, the cell labels a screen reader speaks, the summary
/// region's name, the row-action menu, and the announcements that come
/// out of the canvas funnel's post seam.
/// </summary>
public sealed class CanvasTableTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<RenderedAnnouncement> _announced = [];

    public CanvasTableTests()
    {
        _fixture = FixtureVault.Create(3, "canvas-table");
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

    /// <summary>
    /// One canvas that exercises every column: all five kinds, a group
    /// and four cards inside it, titles that only sort correctly under a
    /// case-INSENSITIVE compare (`Alpha` &lt; `beta` &lt; `Zeta`),
    /// connection counts of 2 and 10 (which an ordinal compare of the
    /// rendered digits gets backwards), and three colours — a preset, a
    /// second preset, and a hex whose nearest preset is the first, so
    /// the "customs sort beside their family" rule has something to
    /// prove.
    /// </summary>
    private void WriteCanvasFixtures()
    {
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "picture.png"),
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        File.WriteAllBytes(Path.Combine(_fixture.Root, "setup.exe"), [0x4D, 0x5A]);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "table.canvas"),
            """
            {
              "nodes": [
                {"id":"grp","type":"group","x":-40,"y":-40,"width":720,"height":420,"label":"Research"},
                {"id":"zeta","type":"text","text":"Zeta","x":0,"y":0,"width":200,"height":100,"color":"1"},
                {"id":"alpha","type":"text","text":"Alpha","x":220,"y":0,"width":200,"height":100,"color":"#f00"},
                {"id":"beta","type":"text","text":"beta","x":440,"y":0,"width":200,"height":100,"color":"4"},
                {"id":"note","type":"file","file":"note0.md","x":0,"y":160,"width":200,"height":100},
                {"id":"link","type":"link","url":"https://example.org/spec","x":800,"y":0,"width":200,"height":100},
                {"id":"pic","type":"file","file":"picture.png","x":800,"y":160,"width":200,"height":100},
                {"id":"exe","type":"file","file":"setup.exe","x":800,"y":320,"width":200,"height":100}
              ],
              "edges": [
                {"id":"e1","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e2","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e3","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e4","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e5","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e6","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e7","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e8","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e9","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"e10","fromNode":"zeta","fromSide":"right","toNode":"alpha","toSide":"left"},
                {"id":"f1","fromNode":"note","fromSide":"right","toNode":"link","toSide":"left"},
                {"id":"f2","fromNode":"note","fromSide":"right","toNode":"link","toSide":"left"}
              ]
            }
            """);
        // The pluralisation boundary: mac's sentence says "1 card,
        // 0 groups", and a formatter that pluralised on the wrong side
        // of one would only show it here.
        File.WriteAllText(
            Path.Combine(_fixture.Root, "one.canvas"),
            """
            {"nodes":[{"id":"only","type":"text","text":"Only","x":0,"y":0,"width":10,"height":10}],"edges":[]}
            """);
    }

    private CanvasDocumentViewModel NewDocument(string path) =>
        new(
            _session,
            path,
            new CanvasAnnouncer(_announced.Add, TimeSpan.FromMinutes(1)),
            synchronousForTests: true);

    private WorkspaceViewModel NewWorkspace() =>
        new(
            _session,
            _fixture.Root,
            () => [],
            _ => { },
            startInteractionBackgroundWork: false,
            announceRendered: _announced.Add);

    /// <summary>
    /// The production composition: the surface view, switched to the
    /// table, with its document loaded. Nothing here reaches around the
    /// surface — the projection under test is the one a user gets.
    /// </summary>
    private (CanvasDocumentViewModel Document, CanvasSurfaceView Surface, AccessibleDataGrid Grid)
        Table(string path = "table.canvas")
    {
        CanvasDocumentViewModel document = NewDocument(path);
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        document.ShowSurface(CanvasSurfaceKind.Table);
        return (document, surface, surface.TableForTests.GridForTests);
    }

    // --- B1/B2: core's rows, the mac column inventory --------------------

    /// <summary>
    /// Contract B2/B4: the grid renders CORE's table rows, in core's
    /// order, under the mac column inventory — and each cell carries the
    /// substrate's "Header: value" label, which is the string a screen
    /// reader actually speaks on that cell.
    /// </summary>
    [Fact]
    public void TheTableIsCoresRowsInCoresOrderUnderTheMacColumnInventory() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();

        Assert.Equal(
            new[] { "Type", "Title", "Group", "Target", "Connections", "Color" },
            grid.Grid.Columns.Select(column => (string)column.Header).ToArray());

        // Same rows, same order, same objects: the projection selects
        // nothing, drops nothing and re-derives nothing (R-D).
        Assert.Equal(
            document.TableRows.Select(row => row.NodeId).ToArray(),
            grid.Grid.Items.Cast<CanvasTableRow>().Select(row => row.NodeId).ToArray());
        Assert.Equal(8, document.TableRows.Count);

        // One row, read across: every value is core's — the target is
        // core's `target` (a file path here, the whole URL for a link),
        // never anything this host derived.
        CanvasTableRow beta = document.TableRows.Single(row => row.NodeId == "beta");
        Assert.Equal(
            new[]
            {
                "Type: Text", "Title: beta", "Group: Research", "Target: ",
                "Connections: 0", "Color: green",
            },
            CellsAcross(grid, beta));

        // IsRowHeader reached the substrate: the row header is the UIA
        // Table pattern's row identity (§8.7 "headers on entry").
        Assert.Equal(DataGridHeadersVisibility.All, grid.Grid.HeadersVisibility);
        document.Shutdown();
    });

    /// <summary>
    /// Contract B4: the Title column is core's `speakable_name`, the
    /// same field the outline row's name uses (CD-30) — so one card
    /// answers to one name on both projections, and the row-header
    /// identity the substrate restores the reader by is unique by
    /// construction.
    /// </summary>
    [Fact]
    public void TheTitleColumnIsCoresSpeakableNameJustAsTheOutlineIs() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        var titles = new List<string>();
        foreach (CanvasTableRow row in grid.Grid.Items.Cast<CanvasTableRow>())
        {
            titles.Add(CellText(grid, 1, row));
        }
        Assert.Equal(
            document.TableRows.Select(row => $"Title: {row.SpeakableName}").ToArray(),
            titles.ToArray());
        document.Shutdown();
    });

    // --- B3: the comparators ----------------------------------------------

    /// <summary>
    /// Contract B3: every column sorts, and each one sorts the way mac
    /// sorts it. The expected sequences are CELL VALUES, so a tie is
    /// spelled identically and the assertion pins the whole rendered
    /// column rather than an order that depends on how ties fell.
    /// </summary>
    [Fact]
    public void EveryColumnSortsTheWayMacSortsIt() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        (int Column, string[] Ascending)[] expectations =
        [
            (0, ["File", "File", "Group", "Image", "Link", "Text", "Text", "Text"]),
            // Case-INSENSITIVE, like mac's localizedCaseInsensitiveCompare:
            // an ordinal compare would put every capital first and read
            // "Alpha, Image: picture, Note 0, Research, Zeta, beta,
            // example.org, setup".
            (1, [
                "Alpha", "beta", "example.org", "Image: picture",
                "note0", "Research", "setup", "Zeta",
            ]),
            (2, ["", "", "", "", "Research", "Research", "Research", "Research"]),
            (3, [
                "", "", "", "", "https://example.org/spec", "note0.md",
                "picture.png", "setup.exe",
            ]),
            (4, ["0", "0", "0", "0", "2", "2", "10", "10"]),
            (5, ["", "", "", "", "", "green", "red", "red (custom)"]),
        ];

        foreach ((int column, string[] ascending) in expectations)
        {
            string header = (string)grid.Grid.Columns[column].Header;
            Assert.Equal(
                ascending.Select(value => $"{header}: {value}").ToArray(),
                SortedCells(grid, column, ascending: true));
            // Descending is the same comparator, negated — the
            // substrate's DirectionalComparer, inherited not rebuilt.
            Assert.Equal(
                ascending.Reverse().Select(value => $"{header}: {value}").ToArray(),
                SortedCells(grid, column, ascending: false));
        }
        document.Shutdown();
    });

    /// <summary>
    /// The Color comparator, called out because the spec's parenthetical
    /// describes it wrongly (§B's B3 records the correction): core's
    /// `color_name` never yields a hex — presets are words and a custom
    /// is "⟨nearest preset⟩ (custom)" — so mac's plain `&lt;` over the
    /// NAME is what puts a custom directly beside its family, which is
    /// the property core's own doc comment claims for this column.
    /// </summary>
    [Fact]
    public void TheColorColumnSortsCustomsBesideTheirFamily() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        string[] sorted = SortedCells(grid, 5, ascending: true);

        // The names are CORE's, not this host's: the cells are exactly
        // the ColorName field core served.
        Assert.Equal(
            document.TableRows.Select(row => row.ColorName ?? string.Empty)
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => $"Color: {name}")
                .ToArray(),
            sorted);
        // …and the custom lands next to the preset it is nearest, not in
        // a "custom" bucket of its own.
        Assert.Equal("Color: red", sorted[^2]);
        Assert.Equal("Color: red (custom)", sorted[^1]);
        document.Shutdown();
    });

    /// <summary>
    /// The Connections comparator is NUMERIC. Sorting the rendered
    /// digits would put 10 before 2, which is the defect a column of
    /// stringly-typed numbers invites and the reason mac's comparator
    /// takes the count rather than the cell.
    /// </summary>
    [Fact]
    public void TheConnectionsColumnSortsNumericallyNotAsText() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        string[] sorted = SortedCells(grid, 4, ascending: true);
        Assert.Equal("Connections: 2", sorted[4]);
        Assert.Equal("Connections: 10", sorted[^1]);
        document.Shutdown();
    });

    // --- B5: selection, both directions ------------------------------------

    /// <summary>
    /// Contract B5: the row the reader is on IS the canvas selection,
    /// and a selection made anywhere else re-seats the reader — with no
    /// echo in either direction and nothing spoken for the direction the
    /// user did not drive.
    /// </summary>
    [Fact]
    public void SelectionFlowsBothWaysWithoutAnEcho() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();

        // View → model: moving the reader onto a row selects that card
        // and narrates the canvas move (one line, not two).
        _announced.Clear();
        MoveReaderTo(grid, "beta");
        Assert.Equal("beta", document.Selection.Selected);
        document.Announcer.FlushForTests();
        Assert.Contains(_announced, line => line.Text == MovedToText(document, "beta"));

        // Model → view: another surface (or, from PR C, the navigator)
        // moves the shared selection, and the grid follows SILENTLY —
        // the user did not make this move, and the announcement they
        // need is the canvas one the document already posted.
        _announced.Clear();
        document.SelectNode("link");
        Assert.Equal(
            "link",
            Assert.IsType<CanvasTableRow>(grid.Grid.CurrentCell.Item).NodeId);
        document.Announcer.FlushForTests();
        // EXACTLY the canvas line: the grid's re-seat contributed
        // nothing, so the move is spoken once rather than twice.
        Assert.Equal(MovedToText(document, "link"), Assert.Single(_announced).Text);
        // …and the re-seat did not come back as a fresh user selection.
        Assert.Equal("link", document.Selection.Selected);
        document.Shutdown();
    });

    /// <summary>
    /// The model→view seat keeps the reader's COLUMN. Dropping them back
    /// to column 0 because another pane moved the selection is the same
    /// class of defect as moving their row.
    /// </summary>
    [Fact]
    public void AReSeatKeepsTheReaderInTheColumnTheyWereReading() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        grid.Grid.CurrentCell = new DataGridCellInfo(
            document.TableRows.Single(row => row.NodeId == "beta"), grid.Grid.Columns[5]);

        document.SelectNode("pic");
        Assert.Equal(
            "pic", Assert.IsType<CanvasTableRow>(grid.Grid.CurrentCell.Item).NodeId);
        Assert.Same(grid.Grid.Columns[5], grid.Grid.CurrentCell.Column);
        document.Shutdown();
    });

    // --- B7: the announce seam --------------------------------------------

    /// <summary>
    /// Contract B7 (DoD §H): the substrate's own canonical events ride
    /// the CANVAS announcer. The fact drives the production keyboard
    /// route (Ctrl+Alt+S) on the production surface and reads the
    /// canvas funnel's post seam — the sink the workspace wires in
    /// production — so nothing here supplies the mechanism it is
    /// checking.
    /// </summary>
    [Fact]
    public void TheGridsOwnAnnouncementsComeOutOfTheCanvasFunnel() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        MoveReaderTo(grid, "beta");
        _announced.Clear();

        AccessibleDataGrid.ToggleSortCommand.Execute(null, grid);

        RenderedAnnouncement sorted = SlateUniffiMethods.A11yRender(
            new A11yEvent.GridSorted("Type", true));
        RenderedAnnouncement posted = Assert.Single(
            _announced, line => line.Text == sorted.Text);
        // Core's PRIORITY too: `Relay` carries a non-canvas event
        // through unwrapped rather than re-classifying it as a canvas
        // status (contract A5).
        Assert.Equal(sorted.Priority, posted.Priority);
        document.Shutdown();
    });

    /// <summary>
    /// A row move speaks BOTH lines, exactly as mac does: the
    /// substrate's row-move event immediately, and the canvas move on
    /// the navigation class's 200 ms window. They say different things
    /// — the focused cell, and the card's position in the canvas.
    /// </summary>
    [Fact]
    public void ARowMoveSpeaksTheGridsLineAndTheCanvasLine() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        MoveReaderTo(grid, "beta");
        _announced.Clear();

        MoveReaderTo(grid, "zeta");
        // Immediate: the grid's own event, rendered by core with an
        // empty description (no engine-authored row context on a canvas
        // row) — i.e. the focused cell alone.
        Assert.Equal(
            SlateUniffiMethods.A11yRender(
                new A11yEvent.GridRowMoved(string.Empty, "Type: Text")).Text,
            _announced[^1].Text);
        // Coalesced: the canvas move.
        document.Announcer.FlushForTests();
        Assert.Equal(MovedToText(document, "zeta"), _announced[^1].Text);
        document.Shutdown();
    });

    // --- B6: activation and row actions ------------------------------------

    /// <summary>
    /// Contract B6: Enter on a row runs the document's ONE activation
    /// seam, so the table opens a card exactly as the outline does —
    /// including the media gate, which refuses a non-media file before
    /// any shell hand-off can happen.
    /// </summary>
    [Fact]
    public void ActivationRunsTheSameSeamTheOutlineDoesIncludingTheMediaGate() =>
        RunSta(() =>
        {
            (CanvasDocumentViewModel document, CanvasSurfaceView surface,
                AccessibleDataGrid grid) = Table();
            string? navigated = null;
            string? media = null;
            string? launched = null;
            document.OpenFileCardFromSurface = (target, _) =>
            {
                navigated = target;
                return true;
            };
            document.OpenMediaCardFromSurface = target =>
            {
                media = target;
                return true;
            };
            document.OpenExternalLinkFromSurface = target =>
            {
                launched = target;
                return true;
            };

            // Text ⇒ the interim read-only detail, and the surface is
            // told to move focus there (the outline's contract, one
            // projection over).
            PressEnterOn(grid, "zeta");
            Assert.Equal("Zeta", document.DetailText);
            Assert.Equal(document.DetailText, surface.DetailForTests.Text);

            // Markdown file ⇒ the note tab; image ⇒ the default app.
            PressEnterOn(grid, "note");
            Assert.Equal("note0.md", navigated);
            PressEnterOn(grid, "pic");
            Assert.Equal("picture.png", media);

            // Link ⇒ the shared allowlist and the injected opener.
            PressEnterOn(grid, "link");
            Assert.Equal("https://example.org/spec", launched);

            // The media gate, from this projection: an executable is
            // refused audibly and never reaches the shell.
            _announced.Clear();
            media = null;
            PressEnterOn(grid, "exe");
            Assert.Null(media);
            document.Announcer.FlushForTests();
            Assert.Equal(
                SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                    new CanvasA11yEvent.CanvasActionFailed(
                        CanvasFailedAction.CanvasAction, "setup.exe"))).Text,
                _announced[^1].Text);

            // A group has nothing to open on a flat projection, and mac
            // falls through the same way: no crash, no announcement, no
            // move.
            _announced.Clear();
            document.CloseDetail();
            PressEnterOn(grid, "grp");
            Assert.Null(document.DetailText);
            document.Announcer.FlushForTests();
            Assert.DoesNotContain(
                _announced,
                line => line.Text.Contains("failed", StringComparison.OrdinalIgnoreCase));
            document.Shutdown();
        });

    /// <summary>
    /// Contract B6: the row-actions menu lists mac's three verbs, and
    /// the two whose commands have not shipped stay LISTED and disabled
    /// with a reason on their HelpText — the mac RowAction contract the
    /// substrate implements, and the reason a screen-reader user can
    /// tell "not yet" from "not here".
    /// </summary>
    [Fact]
    public void TheUnshippedRowActionsAreListedDisabledWithTheirReason() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        MoveReaderTo(grid, "zeta");
        ContextMenu menu = grid.BuildRowActionsMenu()
            ?? throw new Xunit.Sdk.XunitException("the canvas table built no row menu");

        Assert.Equal(
            new[] { "Open", "Toggle Mark", "Delete" },
            menu.Items.Cast<MenuItem>().Select(item => (string)item.Header).ToArray());
        var open = Assert.IsType<MenuItem>(menu.Items[0]);
        Assert.True(open.IsEnabled);
        foreach ((int index, string reason) in new[]
        {
            (1, CanvasPhrase.MarkingArrivesLater),
            (2, CanvasPhrase.DeletingArrivesLater),
        })
        {
            var item = Assert.IsType<MenuItem>(menu.Items[index]);
            Assert.False(item.IsEnabled);
            Assert.Equal(reason, AutomationProperties.GetHelpText(item));
            Assert.Equal(reason, item.ToolTip);
        }

        // Open runs the same activation Enter runs.
        open.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal("Zeta", document.DetailText);
        document.Shutdown();
    });

    // --- B9: the summary region --------------------------------------------

    /// <summary>
    /// Contract B9: the summary is mac's sentence, in the substrate's
    /// separately-focusable region — a static label, never an
    /// announcement (0a-13), which is why no vocabulary event renders it.
    /// </summary>
    [Fact]
    public void TheSummaryIsMacsSentenceInTheFocusableRegion() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        Assert.Equal("Canvas table: 7 cards, 1 group.", grid.SummaryRegion.Text);
        Assert.Equal(
            "Summary: Canvas table: 7 cards, 1 group.",
            AutomationProperties.GetName(grid.SummaryRegion));
        Assert.True(grid.SummaryRegion.Focusable);
        document.Shutdown();

        // The pluralisation boundary, on both sides of one.
        (CanvasDocumentViewModel single, _, AccessibleDataGrid oneGrid) =
            Table("one.canvas");
        Assert.Equal("Canvas table: 1 card, 0 groups.", oneGrid.SummaryRegion.Text);
        single.Shutdown();
    });

    // --- B8: what the table deliberately does NOT wire ----------------------

    /// <summary>
    /// Contract B8: no export producer, because no core canvas export
    /// exists — the `ReadingTableGrid` precedent, and the reason the
    /// substrate's export commands answer CanExecute false rather than
    /// composing text host-side. Ctrl+F likewise keeps ROUTING until PR
    /// C subscribes the canvas filter, so the app-level find is never
    /// shadowed by a grid that cannot filter.
    /// </summary>
    [Fact]
    public void NoExportProducerAndTheFilterChordStillRoutes() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) = Table();
        Assert.False(AccessibleDataGrid.ExportCsvCommand.CanExecute(null, grid));
        Assert.False(AccessibleDataGrid.ExportMarkdownCommand.CanExecute(null, grid));
        Assert.False(AccessibleDataGrid.FilterCommand.CanExecute(null, grid));
        document.Shutdown();
    });

    // --- B10/B11: the projection and its command ----------------------------

    /// <summary>
    /// Contract B11: exactly one projection is in the UIA tree. The arm
    /// that is not showing is COLLAPSED, which keeps it out of the tree
    /// entirely rather than merely off screen, and a non-Ready document
    /// shows neither.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two levels, and the split is honest about which one proves what.
    /// HERE: the visibility gate itself, plus the positive half of the
    /// topology — the showing projection really is reachable as a peer,
    /// so "visible" is not just a property nobody reads.
    /// </para>
    /// <para>
    /// The ABSENCE of the other arm is the journey's
    /// (<c>CanvasSurfaces_TableGridSortSelectionAndActivation_AreClean</c>
    /// asserts the outline's element is gone from the live tree after
    /// the switch). It is not asserted in-process because WPF's own peer
    /// walk keeps an already-built peer for a collapsed element —
    /// observed while writing this fact — so an in-process "absent"
    /// assertion would be testing the walker, not the tree a UIA client
    /// reads. The cross-process bridge is where the claim is true and
    /// where it is checked.
    /// </para>
    /// </remarks>
    [Fact]
    public void ExactlyOneProjectionIsEverInTheTree() => RunSta(() =>
    {
        CanvasDocumentViewModel document = NewDocument("table.canvas");
        document.Load();
        var surface = new CanvasSurfaceView { Model = document };
        using var host = Host(surface);

        Assert.Equal(Visibility.Visible, surface.OutlineForTests.Visibility);
        Assert.Equal(Visibility.Collapsed, surface.TableForTests.Visibility);
        Assert.True(PeerTreeContains(surface, "CanvasOutlineTree"));

        document.ShowSurface(CanvasSurfaceKind.Table);
        host.UpdateLayout();
        Assert.Equal(Visibility.Collapsed, surface.OutlineForTests.Visibility);
        Assert.Equal(Visibility.Visible, surface.TableForTests.Visibility);
        Assert.True(surface.TableChoiceForTests.IsChecked);
        // The showing projection is REACHABLE, not merely marked
        // visible: its grid resolves as a peer with the id AT looks it
        // up by.
        Assert.True(PeerTreeContains(surface, "CanvasTableGrid"));

        document.ShowSurface(CanvasSurfaceKind.Outline);
        host.UpdateLayout();
        Assert.Equal(Visibility.Visible, surface.OutlineForTests.Visibility);
        Assert.Equal(Visibility.Collapsed, surface.TableForTests.Visibility);
        document.Shutdown();

        // A parse error is a message, never an empty grid.
        CanvasDocumentViewModel broken = NewDocument("no-such.canvas");
        broken.Load();
        surface.Model = broken;
        broken.Selection.ActiveSurface = CanvasSurfaceKind.Table;
        host.UpdateLayout();
        Assert.Equal(Visibility.Collapsed, surface.TableForTests.Visibility);
        Assert.Equal(Visibility.Collapsed, surface.OutlineForTests.Visibility);
        broken.Shutdown();
    });

    /// <summary>Whether a UIA client walking this surface's peers would
    /// reach an element with that automation id — the production
    /// topology, not the visual tree.</summary>
    private static bool PeerTreeContains(UIElement root, string automationId)
    {
        AutomationPeer? peer = UIElementAutomationPeer.CreatePeerForElement(root);
        return peer is not null && Walk(peer);

        bool Walk(AutomationPeer node)
        {
            node.ResetChildrenCache();
            if (string.Equals(
                node.GetAutomationId(), automationId, StringComparison.Ordinal))
            {
                return true;
            }
            foreach (AutomationPeer child in node.GetChildren() ?? [])
            {
                if (Walk(child))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Contract B10: `slate.canvas.showTable` is ENABLED now that the
    /// projection exists, resolves through the registrar, and drives the
    /// same one surface switch the header's radio does — so the state,
    /// the persisted token and the spoken sentence cannot disagree.
    /// </summary>
    [Fact]
    public void ShowTableIsEnabledAndDrivesTheOneSurfaceSwitch()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("table.canvas");
        // The registrar resolves through `Workspace`, so the host has to
        // be a live one — the PR A fact's own bridge, reused rather than
        // copied.
        var host = new CanvasDocumentTests.CanvasCommandHost(workspace);

        Assert.Null(Commands.SlateCommandRegistrar.DisabledReason(
            host, Commands.ChordTable.Ids.CanvasShowTable));
        // The one still unshipped stays disabled with the registrar's
        // canonical sentence (contract A18).
        Assert.Equal(
            Commands.SlateCommandRegistrar.UnavailableReason,
            Commands.SlateCommandRegistrar.DisabledReason(
                host, Commands.ChordTable.Ids.CanvasShowVisual));

        _announced.Clear();
        Commands.SlateCommandRegistrar
            .Resolve(host, Commands.ChordTable.Ids.CanvasShowTable)!
            .Execute(null);

        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.Equal(CanvasSurfaceKind.Table, document.Selection.ActiveSurface);
        // The persisted token followed (contract A15).
        Assert.Equal("table", tab.ActiveCanvasSurface);
        document.Announcer.FlushForTests();
        Assert.Equal(
            SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
                new CanvasA11yEvent.CanvasSurfaceShown(CanvasSurfaceKind.Table))).Text,
            _announced[^1].Text);
    }

    /// <summary>
    /// Contract A14's table arm: a focus request lands on a REALIZED
    /// cell of the row, and a row the grid does not have is not reported
    /// as delivered — the outline's tree-focus fallback is the defect
    /// that rule exists for, and the same shape is reachable here
    /// through the substrate's own grid-level fallback.
    /// </summary>
    [Fact]
    public void AFocusRequestLandsOnATableRowAndAnUnknownRowDoesNot() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("table.canvas");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        CanvasDocumentViewModel document =
            Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        document.ShowSurface(CanvasSurfaceKind.Table);
        var surface = new CanvasSurfaceView { DataContext = tab, Model = document };
        using var host = Host(surface);

        document.RequestFocusLanding(tab, "pic");
        host.UpdateLayout();

        Assert.Null(document.FocusRequest);
        Assert.Equal(
            "pic",
            Assert.IsType<CanvasTableRow>(
                (host.FocusedElement() as FrameworkElement)?.DataContext).NodeId);

        Assert.False(surface.TableForTests.DeliverFocus("no-such-node"));
    });

    // --- The substrate is INHERITED, not re-implemented ---------------------

    /// <summary>
    /// Contract B1: the projection IS the W4-1 substrate, so the §8.7
    /// conformance matrix — headers on entry, cell navigation, keyboard
    /// sort, type-ahead, the AT-safe virtualization mode, the
    /// separately-focusable summary — applies to it by construction.
    /// `GridConformanceTests` is that matrix and is CITED rather than
    /// re-run: re-running its 10,000-row probe here would prove the
    /// substrate again and this projection not at all.
    /// </summary>
    [Fact]
    public void TheProjectionIsTheSubstrateSoTheConformanceMatrixApplies() => RunSta(() =>
    {
        (CanvasDocumentViewModel document, CanvasSurfaceView surface,
            AccessibleDataGrid grid) = Table();
        Assert.IsType<AccessibleDataGrid>(surface.TableForTests.GridForTests);
        Assert.Equal(
            VirtualizationMode.Standard,
            VirtualizingPanel.GetVirtualizationMode(grid.Grid));
        Assert.True(grid.Grid.EnableRowVirtualization);
        // A distinct automation id per grid: the shell shows more than
        // one, and a hardcoded id makes them indistinguishable (D-12).
        Assert.Equal("CanvasTableGrid", grid.GridAutomationId);
        Assert.Equal(
            "CanvasTableGridSummary",
            AutomationProperties.GetAutomationId(grid.SummaryRegion));
        Assert.Equal(
            CanvasPhrase.TableName, AutomationProperties.GetName(grid.Grid));

        // Type-ahead is the substrate's, and it works on this
        // projection's first column because the columns are ordinary
        // substrate columns.
        Assert.True(grid.TypeAhead("gr"));
        document.Shutdown();
    });

    /// <summary>
    /// The 2,000-node fixture binds EVERY row: the substrate's row
    /// virtualization is what keeps that responsive (proven by
    /// `GridConformanceTests`, cited not re-run), and this fact pins the
    /// thing a projection can get wrong on its own — silently
    /// truncating, paging, or dropping rows core served.
    /// </summary>
    [Fact]
    public void TheLargeCanvasBindsEveryRowCoreServed() => RunSta(() =>
    {
        File.Copy(
            Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures",
                "canvas", "large_2000.canvas"),
            Path.Combine(_fixture.Root, "large.canvas"),
            overwrite: true);
        (CanvasDocumentViewModel document, _, AccessibleDataGrid grid) =
            Table("large.canvas");
        Assert.True(document.TableRows.Count >= 2_000);
        Assert.Equal(document.TableRows.Count, grid.Grid.Items.Count);
        document.Shutdown();
    });

    // --- helpers -------------------------------------------------------------

    /// <summary>The rendered text of the canvas move the document
    /// narrates for a row — core's render, never a local copy.</summary>
    private static string MovedToText(CanvasDocumentViewModel document, string nodeId)
    {
        CanvasOutlineRow row = Assert.IsType<CanvasOutlineRow>(document.RowFor(nodeId));
        return SlateUniffiMethods.A11yRender(new A11yEvent.Canvas(
            new CanvasA11yEvent.CanvasMovedTo(
                Verbosity: document.Verbosity,
                KindLabel: row.Kind,
                Title: row.Title,
                OrdinalN: row.OrdinalN,
                TotalM: row.TotalM,
                Container: row.GroupPath.Length > 0 ? row.GroupPath[^1] : null,
                ConnectionCount: row.ConnectionCount,
                ColorName: row.ColorName,
                Marked: false))).Text;
    }

    /// <summary>Move the reader onto a row the way an arrow key does:
    /// currency, which is what the substrate announces from and what
    /// the surface reads back.</summary>
    private static void MoveReaderTo(AccessibleDataGrid grid, string nodeId) =>
        grid.Grid.CurrentCell = new DataGridCellInfo(
            grid.Grid.Items.Cast<CanvasTableRow>().Single(row => row.NodeId == nodeId),
            grid.Grid.CurrentCell.Column ?? grid.Grid.Columns[0]);

    private static void PressEnterOn(AccessibleDataGrid grid, string nodeId)
    {
        MoveReaderTo(grid, nodeId);
        grid.Grid.RaiseEvent(new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(grid.Grid)
                ?? new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "t", IntPtr.Zero),
            0,
            Key.Enter)
        { RoutedEvent = UIElement.PreviewKeyDownEvent });
    }

    /// <summary>
    /// The label a screen reader speaks on one cell: the substrate's
    /// "Header: value" contract, read off the generated cell element
    /// rather than off the column configuration that fed it.
    /// </summary>
    private static string CellText(AccessibleDataGrid grid, int columnIndex, object row)
    {
        var column = Assert.IsType<AccessibleGridTextColumn>(grid.Grid.Columns[columnIndex]);
        var cell = new DataGridCell();
        _ = column.TestGenerateElement(cell, row);
        return AutomationProperties.GetName(cell);
    }

    private static string[] CellsAcross(AccessibleDataGrid grid, object row) =>
        Enumerable.Range(0, grid.Grid.Columns.Count)
            .Select(index => CellText(grid, index, row))
            .ToArray();

    /// <summary>Sort through the substrate's seam and read the rendered
    /// column top to bottom.</summary>
    private static string[] SortedCells(
        AccessibleDataGrid grid, int columnIndex, bool ascending)
    {
        _ = grid.ApplySort(columnIndex, ascending);
        return grid.Grid.Items.Cast<object>()
            .Select(row => CellText(grid, columnIndex, row))
            .ToArray();
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
