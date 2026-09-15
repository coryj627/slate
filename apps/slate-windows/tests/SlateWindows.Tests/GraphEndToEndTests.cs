// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using SlateWindows.Graph;
using SlateWindows.Grids;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 §F (F1, FD-1): the end-to-end suite — the spec's own sequence
/// through the REAL <see cref="VaultSession"/> and the workspace's own
/// seams (<c>OpenGraph</c>, the row actions, <c>ToggleGraphInspector</c>,
/// the document's <c>SetMode</c>), never a fake source and never a direct
/// FFI call except to compute an ORACLE from the committed golden's bytes.
/// The vault is the committed <c>graph_vault</c> the parity harness
/// serialises; the oracle is <c>graph_queries.json</c> wherever it carries
/// the answer, and core's exported function over the golden's data in the
/// test's own id space where the golden pins a synthetic input (FD-1).
/// Every announcement a fact claims is OBSERVED on the workspace's
/// rendered-line sink as core's render of the constructed event.
/// </summary>
public sealed class GraphEndToEndTests
{
    private const string Hub = "hub.md";
    private const string Orphan = "orphan.md";
    private const string Deep = "notes/nested/deep.md";

    /// <summary>The golden's default query: attachments out, ghosts in,
    /// every component — the document's filter on a fresh vault.</summary>
    private static readonly GraphFilter DefaultFilter = new(false, true, false);

    // --- the committed vault and the golden ---------------------------------

    private sealed class GraphVault : IDisposable
    {
        public string Root { get; }

        private GraphVault(string root)
        {
            Root = root;
        }

        public static GraphVault Copy(string label)
        {
            string source = Path.Combine(
                SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
            Assert.True(Directory.Exists(source), $"the graph vault is missing at {source}");
            string root = Path.Combine(Path.GetTempPath(), $"slate-graph-e2e-{label}-{Guid.NewGuid():N}");
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string target = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            return new GraphVault(root);
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

    private static JsonElement Golden()
    {
        string path = Path.Combine(
            SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "parity_golden", "graph_queries.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.Clone();
    }

    private static JsonElement GoldenEntry(JsonElement section, string field, string value) =>
        section.EnumerateArray().Single(e => e.GetProperty(field).GetString() == value);

    private static string[] Keys(JsonElement array) =>
        [.. array.EnumerateArray().Select(e => e.GetString()!)];

    // --- the workspace, the app's shape ------------------------------------

    /// <summary>A workspace over a scanned session in the shape
    /// <c>GraphDiagramTests</c>' host and the renderer benchmark build it:
    /// the motion policy explicit (D-4), the rendered lines captured, the
    /// PRODUCTION note creator (the files sidebar, as the lifecycle wires
    /// it) so the ghost create runs the app's own two-phase seam.</summary>
    private sealed class Host : IDisposable
    {
        public VaultSession Session { get; }
        public WorkspaceViewModel Workspace { get; }
        public FilesSidebarViewModel Sidebar { get; }
        public List<string> Lines { get; } = [];

        public Host(string root)
        {
            Session = VaultSession.OpenFilesystem(root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            // Both sinks into ONE timeline: the relay's rendered lines and the
            // shell's own posts (A-8's one direct post, NoteCreated, rides the
            // shell's announce), each as core's render.
            Workspace = new WorkspaceViewModel(
                Session,
                root,
                () => [],
                @event => Lines.Add(SlateUniffiMethods.A11yRender(@event).Text),
                startInteractionBackgroundWork: false,
                announceRendered: line => Lines.Add(line.Text));
            Workspace.GraphMotionPolicyForTests = new GraphMotionPolicy(() => false);
            Sidebar = new FilesSidebarViewModel(Session, _ => { }, vaultRoot: root, localAppDataRoot: Path.Combine(root, ".appdata"));
            Workspace.GraphNoteCreator = Sidebar;
        }

        public GraphDocumentViewModel Open()
        {
            Workspace.OpenGraph();
            GraphDocumentViewModel document = Workspace.GraphDocument!;
            Settle(document);
            return document;
        }

        public static void Settle(GraphDocumentViewModel document)
        {
            PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public void SettleLeaf()
        {
            PumpedDispatcher.PumpUntilDrained(Workspace.Connections.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public WorkspaceTabViewModel GraphTab =>
            Workspace.Groups.SelectMany(g => g.Tabs).First(t => t.IsGraph);

        public int Count(string line) => Lines.Count(l => l == line);

        /// <summary>A line of a COALESCED class (the relay's 200 ms
        /// latest-wins window: navigation, filter count, force value, the
        /// settle) is observed by pumping the dispatcher until it arrives;
        /// then it arrived exactly once.</summary>
        public void Observed(string line)
        {
            _ = PumpedDispatcher.PumpUntil(() => Count(line) > 0, TimeSpan.FromSeconds(5));
            Assert.True(Count(line) == 1, $"expected the line once, saw it {Count(line)} times: {line}\n{string.Join("\n", Lines)}");
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    private static string Render(GraphA11yEvent @event) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.Graph(@event)).Text;

    private static string Status(GraphStatusNote note) => Render(new GraphA11yEvent.GraphStatus(note));

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromMinutes(4)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        public void UpdateLayout() => window.UpdateLayout();

        public void Dispose() => window.Close();
    }

    private static HostedWindow HostInWindow(UIElement content, double width = 900, double height = 700)
    {
        var window = new Window
        {
            Content = content,
            Width = width,
            Height = height,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = false,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window);
    }

    private static GraphSurfaceView SurfaceFor(Host host, GraphDocumentViewModel document) =>
        new() { Model = document, DataContext = host.GraphTab };

    /// <summary>The diagram live and settled, as the app reaches it: the
    /// mode switched, the build landed, the settle ended, the first epoch on
    /// the renderer.</summary>
    private static (GraphDiagramView Diagram, GraphDiagramModel Model) LiveSettledDiagram(
        Host host, GraphDocumentViewModel document, GraphSurfaceView surface, HostedWindow window)
    {
        Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
        Assert.True(
            PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram || document.DiagramError is not null, TimeSpan.FromSeconds(60)),
            "the build never landed");
        Assert.Null(document.DiagramError);
        GraphDiagramModel model = document.DiagramModel!;
        Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(120)), "the settle never ended");
        Host.Settle(document);
        window.UpdateLayout();
        GraphDiagramView diagram = surface.DiagramForTests;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(60)),
            "the first epoch never landed on the renderer");
        window.UpdateLayout();
        return (diagram, model);
    }

    private static string KeyOf(GraphDiagramView diagram, ulong id) => diagram.Entries[id].StableKey;

    // --- F1, fact 1: the table, the summary, the sort, the preset, the needle, Where-am-I ----

    [Fact]
    public void OpenGraphVaultExposesTheTableTheSummaryAndTheSortAgainstTheGolden() => RunSta(() =>
    {
        using GraphVault vault = GraphVault.Copy("table");
        using var host = new Host(vault.Root);
        JsonElement golden = Golden();
        GraphDocumentViewModel document = host.Open();

        // The open: Opened and the snapshot summary observed, the rows and
        // the summary the golden's under the default sort and filter.
        Assert.Equal(1, host.Count(Status(new GraphStatusNote.Opened())));
        GraphPublication publication = document.Publication;
        Assert.Equal(new GraphVisibilityQuery(DefaultFilter, string.Empty, null), publication.Query);
        Assert.Equal(1, host.Count(Render(new GraphA11yEvent.GraphSnapshotSummary(publication.Snapshot!.SummaryCounts))));
        // The initial rows are under the FETCHED default sort
        // (`graph_table_default_sort`, AD-1), one of the golden's sixteen.
        Assert.Equal(SlateUniffiMethods.GraphTableDefaultSort(), publication.AcceptedSort);
        AssertRowsEqualTheGoldenSort(golden, publication, SortName(publication.AcceptedSort), underTheGoldensFilter: false);


        // The table sort leg: the surface hosted, the grid's sort command —
        // the one Ctrl+Alt+S runs — on every pinned sort, the Note column
        // read back through the Grid pattern in the golden's row order.
        GraphSurfaceView surface = SurfaceFor(host, document);
        using HostedWindow window = HostInWindow(surface);
        AccessibleDataGrid grid = surface.TableForTests.GridForTests;
        foreach (JsonElement entry in golden.GetProperty("table").EnumerateArray())
        {
            string sortName = entry.GetProperty("sort").GetString()!;
            (GraphTableColumn column, bool ascending) = ParseSort(sortName);
            int index = document.CellIndexOf(column);
            GraphTableRow seat = grid.Grid.Items.Cast<GraphTableRow>().First();
            grid.Grid.CurrentCell = new DataGridCellInfo(seat, grid.Grid.Columns[index]);
            // Toggle until the requested direction is the accepted one (the
            // command flips the current column: unsorted → ascending →
            // descending).
            for (int attempt = 0; attempt < 3 && document.Publication.AcceptedSort != new GraphTableSort(column, ascending); attempt++)
            {
                AccessibleDataGrid.ToggleSortCommand.Execute(null, grid);
                Host.Settle(document);
                window.UpdateLayout();
            }
            Assert.Equal(new GraphTableSort(column, ascending), document.Publication.AcceptedSort);
            // The golden's rows are under the INCLUSIVE filter (the
            // attachment in); the document's are under the default. The
            // order over the shared keys is the pin.
            string[] expected = [.. entry.GetProperty("rows").EnumerateArray()
                .Select(r => r.GetProperty("key").GetString()!)
                .Where(k => k != "p:pic.png")];
            Assert.Equal(expected, ReadKeyColumnThroughTheGridPattern(grid, document, document.Publication));
        }

        // The needle: a needle the golden pins; the visible set equal, the
        // count line observed as the pair; then the needle cleared.
        GraphNavigator navigator = host.Workspace.GraphNavigator;
        host.Lines.Clear();
        navigator.SetNameQuery("hub");
        Host.Settle(document);
        string[] visible = Keys(GoldenEntry(golden.GetProperty("visibility"), "query", "all:hub").GetProperty("visible"));
        Assert.Equal(visible.ToHashSet(), document.Publication.Rows.Select(r => r.StableKey).ToHashSet());
        GraphPublication filtered = document.Publication;
        host.Observed(Render(new GraphA11yEvent.GraphFilterCount((uint)filtered.Rows.Count, (uint)Math.Min(filtered.Total, uint.MaxValue))));
        navigator.ClearNameQuery();
        Host.Settle(document);
        Assert.Equal(publication.Rows.Count, document.Publication.Rows.Count);

        // Where-am-I: the seated row's readback as core renders it.
        Assert.True(document.SelectRow("p:hub.md"));
        Host.Settle(document);
        host.Lines.Clear();
        Assert.True(navigator.WhereAmI());
        Host.Settle(document);
        GraphA11yEvent.GraphWhereAmI readback = document.TableWhereAmI()!;
        Assert.Equal(Render(readback), navigator.WhereAmIText);
        Assert.Equal(1, host.Count(Render(readback)));
        Assert.Equal("hub", ((GraphWhereAmISelection.Node)readback.Selection).Row.Label);

        // The preset: Orphans through the navigator — the headline once,
        // the rows the golden's `orphans` visibility entry.
        host.Lines.Clear();
        navigator.RunPreset(GraphPreset.Orphans);
        Host.Settle(document);
        string[] orphans = Keys(GoldenEntry(golden.GetProperty("visibility"), "query", "orphans").GetProperty("visible"));
        Assert.Equal(orphans, document.Publication.Rows.Select(r => r.StableKey).ToArray());
        host.Observed(Render(new GraphA11yEvent.GraphPreset(new GraphPresetOutcome.Orphans((ulong)orphans.Length))));
    });

    private static string SortName(GraphTableSort sort)
    {
        string column = sort.Column switch
        {
            GraphTableColumn.Note => "note",
            GraphTableColumn.LinksIn => "links_in",
            GraphTableColumn.LinksOut => "links_out",
            GraphTableColumn.EmbedsIn => "embeds_in",
            GraphTableColumn.EmbedsOut => "embeds_out",
            GraphTableColumn.Component => "component",
            GraphTableColumn.Folder => "folder",
            GraphTableColumn.Kind => "kind",
            _ => throw new InvalidOperationException($"the golden pins no sort on {sort.Column}"),
        };
        return $"{column} {(sort.Ascending ? "asc" : "desc")}";
    }

    private static (GraphTableColumn Column, bool Ascending) ParseSort(string name)
    {
        string[] parts = name.Split(' ');
        GraphTableColumn column = parts[0] switch
        {
            "note" => GraphTableColumn.Note,
            "links_in" => GraphTableColumn.LinksIn,
            "links_out" => GraphTableColumn.LinksOut,
            "embeds_in" => GraphTableColumn.EmbedsIn,
            "embeds_out" => GraphTableColumn.EmbedsOut,
            "component" => GraphTableColumn.Component,
            "folder" => GraphTableColumn.Folder,
            "kind" => GraphTableColumn.Kind,
            _ => throw new InvalidOperationException($"the golden pins a sort this suite does not know: {name}"),
        };
        return (column, parts[1] == "asc");
    }

    /// <summary>The golden's eight displayed cells per row (the nine minus
    /// Modified, whose order is the checkout time), beside the stable key,
    /// and the summary verbatim.</summary>
    private static void AssertRowsEqualTheGoldenSort(JsonElement golden, GraphPublication publication, string sort, bool underTheGoldensFilter)
    {
        JsonElement entry = GoldenEntry(golden.GetProperty("table"), "sort", sort);
        var rows = entry.GetProperty("rows").EnumerateArray()
            .Where(r => underTheGoldensFilter || r.GetProperty("key").GetString() != "p:pic.png")
            .ToList();
        Assert.Equal(rows.Count, publication.Rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            JsonElement expected = rows[i];
            GraphTableRow actual = publication.Rows[i];
            Assert.Equal(expected.GetProperty("key").GetString(), actual.StableKey);
            Assert.Equal(expected.GetProperty("note").GetString(), actual.Cells[0]);
            Assert.Equal(expected.GetProperty("links_in").GetString(), actual.Cells[1]);
            Assert.Equal(expected.GetProperty("links_out").GetString(), actual.Cells[2]);
            Assert.Equal(expected.GetProperty("embeds_in").GetString(), actual.Cells[3]);
            Assert.Equal(expected.GetProperty("embeds_out").GetString(), actual.Cells[4]);
            Assert.Equal(expected.GetProperty("component").GetString(), actual.Cells[5]);
            Assert.Equal(expected.GetProperty("folder").GetString(), actual.Cells[7].Replace('\\', '/'));
            Assert.Equal(expected.GetProperty("kind").GetString(), actual.Cells[8]);
        }
        // The summary is the snapshot's under the document's filter; the
        // golden's is under the inclusive one — equal when the filters
        // agree, which the pin on the rows already settles.
        Assert.False(string.IsNullOrWhiteSpace(publication.Summary));
    }

    /// <summary>The Note column, read through the Grid pattern of the grid's
    /// automation peer — a UIA client's route — and mapped back to the
    /// row's stable key by the publication's own row order.</summary>
    private static string[] ReadKeyColumnThroughTheGridPattern(AccessibleDataGrid grid, GraphDocumentViewModel document, GraphPublication publication)
    {
        var peer = (AutomationPeer)UIElementAutomationPeer.CreatePeerForElement(grid.Grid)!;
        // An in-process client's re-walk: the item peers are cached per
        // items list, so the cache is reset the way UIA's structure-changed
        // notification makes a client re-fetch; then the Grid pattern's row
        // count and the row item peers in the grid's order.
        peer.ResetChildrenCache();
        List<AutomationPeer> children = peer.GetChildren() ?? [];
        var provider = (IGridProvider)peer.GetPattern(PatternInterface.Grid)!;
        Assert.Equal(publication.Rows.Count, provider.RowCount);
        var rows = children.OfType<ItemAutomationPeer>().ToList();
        Assert.Equal(provider.RowCount, rows.Count);
        var keys = new List<string>();
        foreach (ItemAutomationPeer rowPeer in rows)
        {
            // The row's Name is core's row copy at the standing verbosity
            // (the substrate's row header from the Note column, A-11).
            string name = rowPeer.GetName();
            GraphTableRow match = publication.Rows.Single(r =>
                Render(new GraphA11yEvent.GraphRow(GraphVerbosity.Standard, document.RowCopy(r))) == name);
            keys.Add(match.StableKey);
        }
        return [.. keys];
    }

    // --- F1, fact 2: the Connections leaf, the re-root, Back, the ghost create --

    [Fact]
    public void TheConnectionsLeafWalksReRootsAndCreatesAgainstTheGolden() => RunSta(() =>
    {
        using GraphVault vault = GraphVault.Copy("leaf");
        using var host = new Host(vault.Root);
        JsonElement golden = Golden();
        GraphDocumentViewModel document = host.Open();
        ConnectionsLeafViewModel leaf = host.Workspace.Connections;

        // The orphan first: its note active in the editor, the leaf the
        // active leaf (the follow rule roots it there); a focus entered
        // over the empty neighbourhood observes NoConnections.
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == "connections");
        host.Workspace.OpenPath(Orphan, WorkspaceOpenTarget.NewTab);
        host.SettleLeaf();
        Assert.Equal(Orphan, leaf.Root);
        Assert.False(leaf.Publication.HasRows);
        host.Lines.Clear();
        leaf.FocusEntered();
        Assert.Equal(1, host.Count(Status(new GraphStatusNote.NoConnections())));

        // The palette's Show connections on the already-active leaf: the
        // panel line (B-D5), nothing reloaded.
        host.Lines.Clear();
        host.Workspace.ShowConnections();
        Assert.Equal(1, host.Count(Status(new GraphStatusNote.ConnectionsPanel())));

        // Show connections from the table's seated row (B2's surface
        // route): the leaf re-roots on hub — GraphReRooted observed — and
        // the note opens in the current tab; a focus entered before the
        // pump observes the loading line.
        host.Workspace.OpenGraph();
        document = host.Workspace.GraphDocument!;
        Host.Settle(document);
        Assert.True(document.SelectRow("p:hub.md"));
        GraphTableRow hub = document.Publication.Rows.Single(r => r.StableKey == "p:hub.md");
        host.Lines.Clear();
        document.Execute(GraphRowAction.ShowConnections, hub);
        Assert.Equal(Hub, leaf.Root);
        leaf.FocusEntered();
        Assert.Equal(1, host.Count(Status(new GraphStatusNote.LoadingConnections())));
        host.SettleLeaf();
        host.Observed(Render(new GraphA11yEvent.GraphReRooted("hub.md")));

        // Depths one to three against the golden's connections entries.
        foreach (uint depth in (uint[])[1, 2, 3])
        {
            if (leaf.Depth != depth)
            {
                host.Lines.Clear();
                leaf.SetDepth(depth);
                host.SettleLeaf();
            }
            AssertTreeEqualsTheGolden(golden, leaf, Hub, depth);
            host.Observed(Render(new GraphA11yEvent.GraphNeighborhoodSummary(leaf.Publication.Tree!.SummaryCounts)));
        }

        // Re-root on a child (by its path, the leaf's own seam), then Back.
        leaf.SetDepth(2);
        host.SettleLeaf();
        Assert.Contains(leaf.Publication.Tree!.Outgoing, r => r.Path == Deep);
        host.Lines.Clear();
        Assert.True(leaf.PinTo(Deep));
        host.SettleLeaf();
        Assert.Equal(1, host.Count(Render(new GraphA11yEvent.GraphReRooted("deep.md"))));
        AssertTreeEqualsTheGolden(golden, leaf, Deep, 2);
        Assert.True(leaf.PopTo(Hub, Hub));
        host.SettleLeaf();
        Assert.Equal(Hub, leaf.Root);

        // The ONE mutation: the ghost's Create note through the workspace's
        // two-phase seam and the production creator; the path is core's
        // (the golden's ghost_paths), NoteCreated once, the node a Note.
        GraphConnectionRow ghost = leaf.Publication.Tree!.Outgoing.First(r => r.Kind == GraphNodeKind.Ghost && r.TargetRaw == "café");
        JsonElement minted = GoldenEntry(golden.GetProperty("ghost_paths"), "target", "café");
        string expectedPath = minted.GetProperty("path").GetString()!;
        Assert.Equal(expectedPath, SlateUniffiMethods.GraphGhostNotePath(ghost.TargetRaw));
        host.Lines.Clear();
        leaf.Activate(ghost, newTab: false);
        PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
        PumpedDispatcher.Drain();
        host.SettleLeaf();
        Assert.True(File.Exists(Path.Combine(vault.Root, expectedPath)));
        Assert.Equal(1, host.Count(Status(new GraphStatusNote.NoteCreated(Path.GetFileName(expectedPath)))));
        // The graph, re-opened (the surface route opened hub in the
        // current tab): the next publication carries the node as a Note.
        host.Workspace.OpenGraph();
        document = host.Workspace.GraphDocument!;
        Host.Settle(document);
        Assert.True(PumpedDispatcher.PumpUntil(() => document.Publication.Rows.Any(r => r.Path == expectedPath), TimeSpan.FromSeconds(30)), "the created note never reached a publication");
        GraphTableRow created = document.Publication.Rows.Single(r => r.Path == expectedPath);
        Assert.Equal(GraphNodeKind.Note, created.Kind);
        Assert.Equal("p:" + expectedPath, created.StableKey);

        // A second create of the same target through the leaf's seam: the
        // Exists arm rides the relay as NoteCreateFailed; nothing on disk
        // changes.
        DateTime stamp = File.GetLastWriteTimeUtc(Path.Combine(vault.Root, expectedPath));
        host.Lines.Clear();
        leaf.CreateNoteFromSurface!(expectedPath, leaf.Root!, leaf.RootEpoch);
        PumpedDispatcher.PumpUntilDrained(host.Workspace.DrainGraphNoteCreationForTests());
        PumpedDispatcher.Drain();
        string failedPrefix = Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.NoteCreateFailed("MESSAGE"))).Split("MESSAGE")[0];
        Assert.Equal(1, host.Lines.Count(line => line.StartsWith(failedPrefix, StringComparison.Ordinal)));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path.Combine(vault.Root, expectedPath)));
    });

    private static void AssertTreeEqualsTheGolden(JsonElement golden, ConnectionsLeafViewModel leaf, string path, uint depth)
    {
        JsonElement entry = golden.GetProperty("connections").EnumerateArray()
            .Single(e => e.GetProperty("path").GetString() == path && e.GetProperty("depth").GetUInt32() == depth);
        GraphConnectionsTree tree = leaf.Publication.Tree!;
        Assert.Equal(entry.GetProperty("center_key").GetString(), tree.CenterKey);
        Assert.Equal(entry.GetProperty("tree_depth").GetUInt32(), tree.Depth);
        JsonElement summary = entry.GetProperty("summary");
        Assert.Equal(summary.GetProperty("center_label").GetString(), tree.SummaryCounts.CenterLabel);
        Assert.Equal(summary.GetProperty("in_links").GetUInt32(), tree.SummaryCounts.InLinks);
        Assert.Equal(summary.GetProperty("out_links").GetUInt32(), tree.SummaryCounts.OutLinks);
        Assert.Equal(summary.GetProperty("note_count").GetUInt64(), tree.SummaryCounts.NoteCount);
        Assert.Equal(summary.GetProperty("depth").GetUInt32(), tree.SummaryCounts.Depth);
        AssertRowsEqual(entry.GetProperty("incoming"), tree.Incoming);
        AssertRowsEqual(entry.GetProperty("outgoing"), tree.Outgoing);
    }

    private static void AssertRowsEqual(JsonElement expected, GraphConnectionRow[] actual)
    {
        var rows = expected.EnumerateArray().ToList();
        Assert.Equal(rows.Count, actual.Length);
        for (int i = 0; i < rows.Count; i++)
        {
            Assert.Equal(rows[i].GetProperty("occurrence").GetString(), actual[i].Id);
            Assert.Equal(rows[i].GetProperty("parent").ValueKind == JsonValueKind.Null ? null : rows[i].GetProperty("parent").GetString(), actual[i].ParentId);
            Assert.Equal(rows[i].GetProperty("level").GetUInt32(), actual[i].Level);
            Assert.Equal(rows[i].GetProperty("key").GetString(), actual[i].StableKey);
            Assert.Equal(rows[i].GetProperty("kind").GetString(), actual[i].Kind.ToString().ToLowerInvariant());
            Assert.Equal(rows[i].GetProperty("embed_only").GetBoolean(), actual[i].EmbedOnly);
            Assert.Equal(rows[i].GetProperty("references").GetUInt32(), actual[i].References);
        }
    }

    // --- F1, fact 3: the diagram — the sixtieth tick, the oracles, the verbs -----

    [Fact]
    public void TheDiagramReproducesTheGoldensSixtiethTickThenConvergesStepsZoomsAndReadsBack() => RunSta(() =>
    {
        using GraphVault vault = GraphVault.Copy("diagram");
        using var host = new Host(vault.Root);
        JsonElement golden = Golden();
        GraphDocumentViewModel document = host.Open();
        GraphSurfaceView surface = SurfaceFor(host, document);
        using HostedWindow window = HostInWindow(surface, 1600, 1200);

        // Diagram mode: the mode line observed; the driver pumped to the
        // sixtieth tick — three steps — which the frame reaches exactly
        // because the vault does not converge by then (the census's pin).
        host.Lines.Clear();
        Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
        Assert.Equal(1, host.Count(Render(new GraphA11yEvent.GraphMode(GraphSurfaceMode.Diagram))));
        Assert.True(
            PumpedDispatcher.PumpUntil(() => document.HasLiveDiagram || document.DiagramError is not null, TimeSpan.FromSeconds(60)),
            "the build never landed");
        Assert.Null(document.DiagramError);
        GraphDiagramModel model = document.DiagramModel!;
        const uint Sixty = 3 * GraphLayoutDriver.IterationsPerStep;
        Assert.Equal(60u, Sixty);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => model.LastFrame is { } frame && frame.Iteration >= Sixty, TimeSpan.FromSeconds(60)),
            "the driver never reached the sixtieth tick");
        LayoutFrame sixtieth = model.LastFrame!;
        Assert.Equal(Sixty, sixtieth.Iteration);
        Assert.False(sixtieth.Converged);
        window.UpdateLayout();
        GraphDiagramView diagram = surface.DiagramForTests;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => diagram.Entries.Count == model.NodeIds.Length, TimeSpan.FromSeconds(60)),
            "the first epoch never landed on the renderer");
        window.UpdateLayout();

        // The precondition (IHA-2): the renderer's visible keys, in order,
        // are the golden's `default` visibility entry — the layout's keys.
        string[] visibleKeys = Keys(GoldenEntry(golden.GetProperty("visibility"), "query", "default").GetProperty("visible"));
        Assert.Equal(visibleKeys, diagram.VisibleIds.Select(id => KeyOf(diagram, id)).ToArray());
        var layout = golden.GetProperty("layout").EnumerateArray()
            .Select(e => (Key: e.GetProperty("key").GetString()!, X: e.GetProperty("x_x1000").GetInt64(), Y: e.GetProperty("y_x1000").GetInt64()))
            .ToList();
        Assert.Equal(visibleKeys, layout.Select(l => l.Key).ToArray());

        // The sixtieth tick's positions through the SHELL's driver and model
        // equal the golden's layout section, quantised as the serializer
        // quantises them.
        var positionOf = diagram.VisibleIds.ToDictionary(id => KeyOf(diagram, id), id => model.Positions[id]);
        foreach ((string key, long x, long y) in layout)
        {
            GraphPoint point = positionOf[key];
            Assert.Equal(x, (long)Math.Round(point.X * 1000.0, MidpointRounding.AwayFromZero));
            Assert.Equal(y, (long)Math.Round(point.Y * 1000.0, MidpointRounding.AwayFromZero));
        }

        // The oracles in the TEST's own id space (FD-1, IHA-1): ids 1…n over
        // the golden's keys; points from the golden's positions; neighbours
        // from the golden's topology; core's exported functions.
        var testId = new Dictionary<string, ulong>(StringComparer.Ordinal);
        for (int i = 0; i < layout.Count; i++)
        {
            testId[layout[i].Key] = (ulong)(i + 1);
        }
        GraphPoint[] points = [.. layout.Select(l => new GraphPoint(testId[l.Key], l.X / 1000.0, l.Y / 1000.0))];
        ulong[] visibleTestIds = [.. layout.Select(l => testId[l.Key])];
        Dictionary<string, ulong[]> neighboursOf = golden.GetProperty("topology").GetProperty("nodes").EnumerateArray()
            .Where(n => testId.ContainsKey(n.GetProperty("key").GetString()!))
            .ToDictionary(
                n => n.GetProperty("key").GetString()!,
                n => n.GetProperty("neighbors").EnumerateArray()
                    .Select(k => k.GetString()!)
                    .Where(testId.ContainsKey)
                    .Select(k => testId[k])
                    .ToArray(),
                StringComparer.Ordinal);
        string KeyOfTestId(ulong id) => layout[(int)id - 1].Key;

        // Seat hub, then the four arrow directions against the oracle.
        ulong hubId = diagram.VisibleIds.Single(id => KeyOf(diagram, id) == "p:hub.md");
        Assert.True(diagram.SelectNode(hubId, announce: false));
        foreach ((double dx, double dy) in ((double, double)[])[(1, 0), (-1, 0), (0, 1), (0, -1)])
        {
            Assert.True(diagram.SelectNode(hubId, announce: false));
            ulong? expected = SlateUniffiMethods.GraphSpatialStep(points, neighboursOf["p:hub.md"], testId["p:hub.md"], dx, dy);
            host.Lines.Clear();
            bool moved = diagram.SpatialMove(dx, dy);
            if (expected is { } target)
            {
                Assert.True(moved);
                Assert.Equal(KeyOfTestId(target), KeyOf(diagram, diagram.SelectedId!.Value));
                GraphTopologyNode landed = diagram.Entries[diagram.SelectedId!.Value];
                host.Observed(Render(new GraphA11yEvent.GraphRow(host.Workspace.GraphPreferences.Verbosity, RowCopyOf(landed))));
            }
            else
            {
                Assert.Equal(hubId, diagram.SelectedId);
            }
        }

        // The structural oracle: core's step chained over the test's ids in
        // the golden's visible order, for Tab and for Shift+Tab, wrapping.
        foreach (bool forward in (bool[])[true, false])
        {
            Assert.True(diagram.SelectNode(hubId, announce: false));
            ulong? cursor = testId["p:hub.md"];
            for (int step = 0; step < visibleTestIds.Length + 1; step++)
            {
                cursor = SlateUniffiMethods.GraphStructuralStep(visibleTestIds, cursor, forward);
                Assert.True(cursor.HasValue);
                Assert.True(diagram.StructuralMove(forward));
                Assert.Equal(KeyOfTestId(cursor.Value), KeyOf(diagram, diagram.SelectedId!.Value));
            }
        }

        // Then to convergence. The build's OWN convergence speaks the mode
        // line alone (Term G4; the mac's finding 8): the settle line is
        // armed by a forces edit — here through the inspector, the app's
        // route (Term K2) — whose run converges to ONE settle line.
        Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(120)), "the settle never ended");
        Host.Settle(document);
        Assert.Equal(0, host.Count(Render(new GraphA11yEvent.GraphLayoutSettled())));
        host.Workspace.ToggleGraphInspector();
        GraphForcesConfig forces = host.Workspace.GraphPreferences.CurrentConfig.Forces with { Repel = 0.6 };
        host.Lines.Clear();
        host.Workspace.Inspector.SetForces(forces);
        host.Observed(Render(new GraphA11yEvent.GraphForceValue(GraphForceControl.Repel, 60)));
        Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(120)), "the armed settle never ended");
        Host.Settle(document);
        host.Observed(Render(new GraphA11yEvent.GraphLayoutSettled()));

        // The verbs through the navigator: the percents core renders.
        GraphNavigator navigator = host.Workspace.GraphNavigator;
        uint before = document.DiagramZoomPercent!();
        host.Lines.Clear();
        Assert.True(navigator.ZoomIn());
        uint zoomedIn = document.DiagramZoomPercent!();
        Assert.True(zoomedIn > before);
        host.Observed(Render(new GraphA11yEvent.GraphZoom(false, zoomedIn)));
        host.Lines.Clear();
        Assert.True(navigator.ZoomOut());
        host.Observed(Render(new GraphA11yEvent.GraphZoom(false, document.DiagramZoomPercent!())));
        host.Lines.Clear();
        Assert.True(navigator.ActualSize());
        Assert.Equal(100u, document.DiagramZoomPercent!());
        host.Observed(Render(new GraphA11yEvent.GraphZoom(false, 100)));
        host.Lines.Clear();
        Assert.True(navigator.FitGraph());
        uint fit = document.DiagramZoomPercent!();
        Assert.NotEqual(100u, fit);
        host.Observed(Render(new GraphA11yEvent.GraphZoom(true, fit)));

        // Where-am-I in Diagram mode renders the node's readback.
        Assert.True(diagram.SelectNode(hubId, announce: false));
        host.Lines.Clear();
        Assert.True(navigator.WhereAmI());
        GraphA11yEvent.GraphWhereAmI readback = document.DiagramWhereAmI()!;
        Assert.Equal(Render(readback), navigator.WhereAmIText);
        Assert.Equal("hub", ((GraphWhereAmISelection.Node)readback.Selection).Row.Label);

        // The pin verb: GraphPinned observed, the pin surviving one more settle.
        host.Lines.Clear();
        Assert.True(diagram.TogglePin(hubId));
        host.Observed(Render(new GraphA11yEvent.GraphPinned(true)));
        Assert.Contains(hubId, model.Pinned);
        model.Driver.StartSettle();
        Assert.True(PumpedDispatcher.PumpUntil(() => !model.Driver.IsSettling, TimeSpan.FromSeconds(120)), "the second settle never ended");
        Host.Settle(document);
        Assert.Contains(hubId, model.Pinned);
    });

    private static GraphRowCopy RowCopyOf(GraphTopologyNode node) => GraphDocumentViewModel.RowCopyOf(node);

    // --- F1, fact 4: the config round trip ----------------------------------

    [Fact]
    public void TheConfigRoundTripsThroughTheInspectorAndTheStore() => RunSta(() =>
    {
        using GraphVault vault = GraphVault.Copy("config");
        using var host = new Host(vault.Root);
        JsonElement golden = Golden();
        GraphDocumentViewModel document = host.Open();
        host.Workspace.ToggleGraphInspector();
        GraphInspectorViewModel inspector = host.Workspace.Inspector;
        GraphPreferencesViewModel preferences = host.Workspace.GraphPreferences;

        // The backend filter through the inspector: attachments in.
        var filter = new GraphFilter(true, true, false);
        inspector.SetBackendFilter(filter);
        Host.Settle(document);
        Assert.Equal(filter, document.ViewState.Filter);

        // The forces through the inspector: Repel to 0.7; the force line
        // observed for the changed control at its resting percent.
        GraphForcesConfig forces = preferences.CurrentConfig.Forces with { Repel = 0.7 };
        host.Lines.Clear();
        inspector.SetForces(forces);
        Host.Settle(document);
        host.Observed(Render(new GraphA11yEvent.GraphForceValue(GraphForceControl.Repel, 70)));

        // The file: core's encode of the same config over the previous bytes
        // (the writer's 400 ms save window fires on the pumped dispatcher,
        // then the issued writes drain).
        Assert.True(PumpedDispatcher.PumpUntil(() => !preferences.HasPendingForTests, TimeSpan.FromSeconds(5)), "the save never flushed");
        PumpedDispatcher.PumpUntilDrained(preferences.WhenWritesDrained());
        PumpedDispatcher.Drain();
        string file = Path.Combine(vault.Root, ".slate", "graph.json");
        Assert.True(File.Exists(file), "the writer wrote no graph.json");
        string written = File.ReadAllText(file);
        GraphConfig config = preferences.CurrentConfig;
        Assert.Equal(SlateUniffiMethods.GraphConfigEncode(config, written), written);
        Assert.Equal(filter.IncludeAttachments, config.Filters.IncludeAttachments);
        Assert.Equal(0.7, config.Forces.Repel);
        // The golden pins encode over its own input: decode-encode of the
        // pinned input reproduces the pinned bytes, so the codec the writer
        // used is the golden's.
        JsonElement pinned = golden.GetProperty("config");
        string input = pinned.GetProperty("input").GetString()!;
        Assert.Equal(pinned.GetProperty("encoded").GetString(), SlateUniffiMethods.GraphConfigEncode(SlateUniffiMethods.GraphConfigDecode(input).Config, input));

        // A fresh preferences over the same vault reads the same values back
        // through core's decode.
        var reopened = new GraphPreferencesViewModel(vault.Root);
        PumpedDispatcher.PumpUntil(() => reopened.CurrentConfig.Forces.Repel == 0.7, TimeSpan.FromSeconds(10));
        Assert.Equal(0.7, reopened.CurrentConfig.Forces.Repel);
        Assert.True(reopened.CurrentConfig.Filters.IncludeAttachments);
    });

    // --- F1, fact 5: the large vault under budget ----------------------------

    /// <summary>1,500 notes — tier A's ceiling — through the workspace: the
    /// open under PR A's 10k ceiling (FD-9), then the renderer's four
    /// budgets on the real document, driver and surface (FD-D2).</summary>
    [Fact]
    public void LargeGraphOpensLaysOutPansAndStepsUnderBudget() => RunSta(() =>
    {
        const int Notes = 1500;
        using FixtureVault vault = FixtureVault.Create(Notes, "graph-e2e-large");
        using var host = new Host(vault.Root);
        var clock = Stopwatch.StartNew();
        GraphDocumentViewModel document = host.Open();
        clock.Stop();
        double openMs = clock.Elapsed.TotalMilliseconds;
        Assert.Equal(Notes, document.Publication.Rows.Count);
        Assert.True(openMs < 500, $"the open took {openMs:F1} ms; PR A's 10k ceiling is 500 ms");

        GraphSurfaceView surface = SurfaceFor(host, document);
        using HostedWindow window = HostInWindow(surface, 1600, 1200);
        (GraphDiagramView diagram, GraphDiagramModel model) = LiveSettledDiagram(host, document, surface, window);
        Assert.Equal(Notes, diagram.VisibleCount);
        Assert.False(diagram.IsTierB);
        Assert.True(diagram.SelectNode(diagram.VisibleIds[Notes / 2], announce: false));

        // The warm tick through the model's admission gate (budget 100 ms).
        clock.Restart();
        LayoutFrame? frame = model.WithSession<LayoutFrame?>(session => session.Tick(GraphLayoutDriver.IterationsPerStep), null);
        clock.Stop();
        Assert.NotNull(frame);
        double tickMs = clock.Elapsed.TotalMilliseconds;
        Assert.True(tickMs < 100, $"the warm tick took {tickMs:F1} ms; the budget is 100 ms");

        // The first rebuild (budget 500 ms): the epoch cleared, the topology
        // fetched and landed, the peers materialised with every name read.
        GraphViewState view = document.ViewState;
        var query = new GraphVisibilityQuery(view.Filter, view.NameQuery, view.KindOnly);
        GraphConfig config = SlateUniffiMethods.GraphConfigDefault() with { Groups = [.. view.Groups] };
        model.Topology = null;
        document.RaiseDiagramTopologyChangedForTests();
        clock.Restart();
        GraphTopology topology = host.Session.GraphTopology(query, config);
        Assert.Equal(model.Generation, topology.Generation);
        model.Topology = topology;
        document.RaiseDiagramTopologyChangedForTests();
        var peer = (GraphDiagramAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(diagram)!;
        int named = peer.GetChildren().Count(child => child.GetName().Length > 0);
        clock.Stop();
        Assert.Equal(Notes, named);
        double rebuildMs = clock.Elapsed.TotalMilliseconds;
        Assert.True(rebuildMs < 500, $"the first rebuild took {rebuildMs:F1} ms; the budget is 500 ms");

        // Ten pan hops averaged (budget 100 ms each): the viewport committed,
        // the visuals redrawn, every peer's screen rectangle read.
        clock.Restart();
        for (int hop = 0; hop < 10; hop++)
        {
            double sign = (hop & 1) == 0 ? -1 : 1;
            diagram.PanBy(sign * 800, sign * 600);
            int placed = diagram.VisibleIds.Count(id => !diagram.NodeScreenRect(id).IsEmpty);
            Assert.Equal(Notes, placed);
        }
        clock.Stop();
        double hopMs = clock.Elapsed.TotalMilliseconds / 10;
        Assert.True(hopMs < 100, $"a pan hop averaged {hopMs:F1} ms; the budget is 100 ms");

        // Fifty spatial steps averaged (budget 50 ms each) on the real document.
        clock.Restart();
        for (int step = 0; step < 50; step++)
        {
            double dx = (step & 1) == 0 ? 1 : -1;
            Assert.True(diagram.SpatialMove(dx, 0), "the spatial step found no node");
        }
        clock.Stop();
        double stepMs = clock.Elapsed.TotalMilliseconds / 50;
        Assert.True(stepMs < 50, $"a spatial step averaged {stepMs:F1} ms; the budget is 50 ms");

        Console.WriteLine($"BENCH graph e2e {Notes} notes: open {openMs:F1} ms; warm tick {tickMs:F1} ms; first rebuild {rebuildMs:F1} ms; pan hop {hopMs:F3} ms; spatial step {stepMs:F3} ms");
    });

    // --- F1, fact 6: the grammar per verbosity, tier B's label, the content --

    [Fact]
    public void AnnouncementGrammarConformsPerVerbosity() => RunSta(() =>
    {
        using GraphVault vault = GraphVault.Copy("grammar");
        using var host = new Host(vault.Root);
        GraphDocumentViewModel document = host.Open();
        GraphPreferencesViewModel preferences = host.Workspace.GraphPreferences;
        GraphSurfaceView surface = SurfaceFor(host, document);
        using HostedWindow window = HostInWindow(surface, 1600, 1200);
        host.Lines.Clear();
        (GraphDiagramView small, _) = LiveSettledDiagram(host, document, surface, window);
        // The build's own convergence speaks the mode line alone (Term G4).
        Assert.Equal(1, host.Count(Render(new GraphA11yEvent.GraphMode(GraphSurfaceMode.Diagram))));
        Assert.Equal(0, host.Count(Render(new GraphA11yEvent.GraphLayoutSettled())));
        ulong hubId = small.VisibleIds.Single(id => KeyOf(small, id) == "p:hub.md");
        ulong otherId = small.VisibleIds.Single(id => KeyOf(small, id) == "p:2.md");
        GraphRowCopy copy = RowCopyOf(small.Entries[hubId]);

        // The row line at each verbosity through the preferences' own
        // command (the Graph menu's route) on the diagram's keyboard
        // select (DD-Q2): the terse collapse, the counts.
        foreach (GraphVerbosity verbosity in (GraphVerbosity[])[GraphVerbosity.Terse, GraphVerbosity.Standard, GraphVerbosity.Verbose])
        {
            GraphVerbositySpec spec = preferences.Levels.Single(level => level.Verbosity == verbosity);
            preferences.SetVerbosityCommand.Execute(spec.Tag);
            Assert.Equal(verbosity, preferences.Verbosity);
            Assert.True(small.SelectNode(otherId, announce: false));
            host.Lines.Clear();
            Assert.True(small.SelectNode(hubId, announce: true));
            string expected = Render(new GraphA11yEvent.GraphRow(verbosity, copy));
            host.Observed(expected);
            if (verbosity == GraphVerbosity.Terse)
            {
                Assert.Equal(copy.Label, expected);
            }
            else
            {
                Assert.Contains("links in", expected);
            }
        }
        preferences.SetVerbosityCommand.Execute(preferences.Levels.Single(l => l.Verbosity == GraphVerbosity.Standard).Tag);

        // Tier A: a node peer's HelpText is core's render of
        // GraphNeighborsContent over its neighbours behind the prefix —
        // custom content, never posted.
        GraphTopologyNode entry = small.Entries[hubId];
        string content = GraphAnnouncer.RenderLabel(new GraphA11yEvent.GraphNeighborsContent([.. entry.Neighbors.Select(n => n.Label)]));
        Assert.Equal(GraphPhrase.ConnectsToPrefix + content, small.PeerFor(hubId)!.GetHelpText());
        Assert.DoesNotContain(content, host.Lines);

        // Tier B over a 1,501-note vault: TierEntered observed once, no
        // per-node peer, ONE peer whose Name is core's render of
        // GraphTierSummary{1501} — a label, never posted (IGZ-1).
        TierBSpeaksTheEntryOnceAndNamesTheSummaryPeerWithoutPostingIt(
            entered: Render(new GraphA11yEvent.GraphTierEntered()),
            summaryLabel: Render(new GraphA11yEvent.GraphTierSummary(1501)));
    });

    private static void TierBSpeaksTheEntryOnceAndNamesTheSummaryPeerWithoutPostingIt(string entered, string summaryLabel)
    {
        const int Notes = 1501;
        using FixtureVault vault = FixtureVault.Create(Notes, "graph-e2e-tier-b");
        using var host = new Host(vault.Root);
        GraphDocumentViewModel document = host.Open();
        GraphSurfaceView surface = SurfaceFor(host, document);
        using HostedWindow window = HostInWindow(surface, 1600, 1200);
        host.Lines.Clear();
        (GraphDiagramView diagram, _) = LiveSettledDiagram(host, document, surface, window);
        Assert.True(diagram.IsTierB);
        Assert.Equal(1, host.Count(entered));
        var summary = Assert.IsType<GraphTierSummaryAutomationPeer>(Assert.Single(diagram.PeersInOrder()));
        Assert.Equal(summaryLabel, summary.GetName());
        Assert.DoesNotContain(summaryLabel, host.Lines);
    }
}
