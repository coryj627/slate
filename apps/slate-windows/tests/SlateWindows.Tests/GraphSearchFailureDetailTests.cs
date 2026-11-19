// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using SlateWindows.Graph;
using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-15b (#1890, CD9): the search overlay's, the graph's and the
/// Connections leaf's failure sentences say core's detail, never the
/// binding's field dump (<c>@message=…</c>, <c>@path=…</c>), and the search
/// and graph error arms that worded four variants apart from core are gone:
/// an invalid search query no longer adds "Search query is invalid: ". The
/// graph config's read-only notice reads its error's typed fields, in the
/// words the FFI crate's own Display gives them, never "@version=" or
/// "@reason=".
/// </summary>
public sealed class GraphSearchFailureDetailTests
{
    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    private static string Render(A11yEvent @event) => SlateUniffiMethods.A11yRender(@event).Text;

    private static string RenderGraph(GraphA11yEvent @event) => Render(new A11yEvent.Graph(@event));

    // ---- Search -------------------------------------------------------------

    /// <summary>A query core's full-text index cannot parse, typed into the
    /// overlay over a real vault: the summary and the spoken line are
    /// "Search error: " and core's detail, with no host lead of their
    /// own.</summary>
    [Theory]
    [InlineData("AND")]
    [InlineData("unbalanced\"")]
    public void AnInvalidQuerySaysCoresDetailWithNoLeadOfItsOwn(string query)
    {
        using FixtureVault vault = FixtureVault.Create(3, "search-failure-detail");
        using VaultSession session = VaultSession.OpenFilesystem(vault.Root);
        using (var scan = new CancelToken())
        {
            session.ScanInitial(scan);
        }
        VaultException.InvalidQuery refused;
        using (var cancel = new CancelToken())
        {
            refused = Assert.Throws<VaultException.InvalidQuery>(
                () => session.FullTextSearch(query, new SearchScope.Vault(), cancel));
        }
        var announcements = new List<A11yEvent>();
        var overlay = new SearchOverlayViewModel(
            new VaultSearchSource(() => session, () => vault.Root), announcements.Add, debounceSearches: false);
        overlay.Open();

        overlay.Query = query;

        Assert.Equal(SearchOverlayState.Error, overlay.State);
        string expected = "Search error: " + SlateUniffiMethods.VaultErrorDetail(refused);
        Assert.Equal(expected, overlay.Summary);
        Assert.Equal(expected, Render(Assert.IsType<A11yEvent.SearchFailed>(announcements.Last())));
        Assert.StartsWith("Search error: FTS5 could not parse ", overlay.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Search query is invalid", overlay.Summary, StringComparison.Ordinal);
        Assert.DoesNotMatch(FieldToken, overlay.Summary);
    }

    /// <summary>Every other variant says core's detail: a vault error the
    /// removed arms never named (core's rendering, not the dump), and the
    /// three the arms named, which said core's words already.</summary>
    [Theory]
    [InlineData("InvalidPath", "Search error: Invalid path a/b.md: no such folder")]
    [InlineData("DestinationExists", "Search error: Something named a.md already exists there.")]
    [InlineData("Db", "Search error: database disk image is malformed")]
    [InlineData("Io", "Search error: The device is not ready.")]
    [InlineData("Unsupported", "Search error: search scope: File is not implemented yet.")]
    public void EveryOtherVariantSaysCoresDetail(string variant, string expected)
    {
        Exception failure = variant switch
        {
            "InvalidPath" => new VaultException.InvalidPath("a/b.md", "no such folder"),
            "DestinationExists" => new VaultException.DestinationExists("a.md"),
            "Db" => new VaultException.Db("database disk image is malformed"),
            "Io" => new VaultException.Io("The device is not ready."),
            "Unsupported" => new VaultException.Unsupported("search scope: File"),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "no such case"),
        };
        var announcements = new List<A11yEvent>();
        var overlay = new SearchOverlayViewModel(new ThrowingSource(failure), announcements.Add, debounceSearches: false);
        overlay.Open();

        overlay.Query = "anything";

        Assert.Equal(SearchOverlayState.Error, overlay.State);
        Assert.Equal(expected, overlay.Summary);
        Assert.Equal(expected, Render(Assert.IsType<A11yEvent.SearchFailed>(Assert.Single(announcements))));
    }

    /// <summary>A search source that refuses every query with one
    /// failure.</summary>
    private sealed class ThrowingSource(Exception failure) : ISearchSource
    {
        public bool IsVaultOpen => true;

        public object? SessionIdentity { get; } = new();

        public QueryResultSet Search(string query, SearchScope scope, CancelToken cancel) => throw failure;

        public IReadOnlyList<string> LoadRecents() => [];

        public void RecordRecent(string query)
        {
        }

        public void ClearRecents()
        {
        }
    }

    // ---- The graph -----------------------------------------------------------

    /// <summary>A workspace over a copy of core's graph fixture vault.</summary>
    private sealed class GraphHost : IDisposable
    {
        public GraphHost(string root, bool scan)
        {
            Session = VaultSession.OpenFilesystem(root);
            if (scan)
            {
                using var cancel = new CancelToken();
                Session.ScanInitial(cancel);
            }
            Workspace = new WorkspaceViewModel(
                Session,
                root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => Lines.Add(line.Text));
        }

        public VaultSession Session { get; }

        public WorkspaceViewModel Workspace { get; }

        public List<string> Lines { get; } = [];

        public GraphDocumentViewModel Document => Workspace.GraphDocument!;

        public ConnectionsLeafViewModel Leaf => Workspace.Connections;

        public void SettleGraph()
        {
            PumpedDispatcher.PumpUntilDrained(Document.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public void SettleLeaf()
        {
            PumpedDispatcher.PumpUntilDrained(Leaf.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
        }
    }

    private static string CopyGraphVault(string label)
    {
        string source = Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
        string root = Path.Combine(Path.GetTempPath(), $"slate-failure-detail-{label}-{Guid.NewGuid():N}");
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(root, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        return root;
    }

    private static void TryDelete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A build that fails says core's detail as the diagram's
    /// error: an <c>InvalidQuery</c> its bare message (the removed arm added
    /// "Search query is invalid: "), and a variant the arms never named its
    /// rendering, not the dump. No input reaches either through core's graph
    /// build, which fails only with <c>Db</c>; the build's test gate throws
    /// them (CB-15b's Call 1, as the lead ruled).</summary>
    [Theory]
    [InlineData("InvalidQuery", "a query core refused")]
    [InlineData("InvalidPath", "Invalid path hub.md: no such note in the graph under the active filter")]
    public void AFailedDiagramBuildSaysCoresDetail(string variant, string expected)
    {
        string root = CopyGraphVault("diagram");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new GraphHost(root, scan: true);
                host.Workspace.OpenGraph();
                host.SettleGraph();
                GraphDocumentViewModel document = host.Document;
                document.FetchGateForTests = variant == "InvalidQuery"
                    ? () => throw new VaultException.InvalidQuery("a query core refused")
                    : () => throw new VaultException.InvalidPath("hub.md", "no such note in the graph under the active filter");

                Assert.True(document.SetMode(GraphSurfaceMode.Diagram));
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => document.DiagramError is not null, TimeSpan.FromSeconds(30)),
                    "the failure never landed");
                document.FetchGateForTests = null;
                host.SettleGraph();

                Assert.Equal(expected, document.DiagramError);
                Assert.DoesNotMatch(FieldToken, document.DiagramError!);
            });
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>The <c>links</c> table's root page zeroed in a closed vault's
    /// index, which a session opened with no scan then reads: core refuses the
    /// graph's table rows with <c>Db</c>. The open's own scan refuses the same
    /// damage first, so this state is constructed.</summary>
    private static void DamageTheLinksTable(string root)
    {
        string db = Path.Combine(root, ".slate", "cache.sqlite");
        var wal = new FileInfo(db + "-wal");
        long walBytes = wal.Exists ? wal.Length : 0;
        Assert.True(
            walBytes == 0,
            $"premise: the closed session checkpointed its WAL ({walBytes} bytes left), so the page patched is the live one");
        byte[] bytes = File.ReadAllBytes(db);
        int pageSize = (bytes[16] << 8) | bytes[17];
        if (pageSize == 1)
        {
            pageSize = 65536;
        }
        byte[] name = "tablelinkslinks"u8.ToArray();
        int at = bytes.AsSpan().IndexOf(name);
        Assert.True(at >= 0, "premise: the schema names the links table");
        int create = bytes.AsSpan(at + name.Length).IndexOf("CREATE TABLE"u8);
        Assert.InRange(create, 1, 4);
        int page = 0;
        for (int i = 0; i < create; i++)
        {
            page = (page << 8) | bytes[at + name.Length + i];
        }
        Assert.True(page > 1, $"premise: a root page past the schema ({page})");
        long offset = (long)(page - 1) * pageSize;
        Assert.True(
            bytes[offset] is 0x02 or 0x05 or 0x0A or 0x0D,
            $"premise: page {page} starts with a b-tree page type, not 0x{bytes[offset]:X2}");
        using var stream = new FileStream(db, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        stream.Position = offset;
        stream.WriteByte(0x00);
    }

    /// <summary>The graph table's load over a damaged index: the spoken line
    /// and the error state say core's detail.</summary>
    [Fact]
    public void AFailedGraphLoadSaysCoresDetail()
    {
        string root = CopyGraphVault("table");
        try
        {
            using (VaultSession first = VaultSession.OpenFilesystem(root))
            {
                using var cancel = new CancelToken();
                first.ScanInitial(cancel);
            }
            DamageTheLinksTable(root);
            string detail;
            using (VaultSession direct = VaultSession.OpenFilesystem(root))
            {
                using var cancel = new CancelToken();
                GraphFilterConfig filters = SlateUniffiMethods.GraphConfigDefault().Filters;
                detail = SlateUniffiMethods.VaultErrorDetail(Assert.Throws<VaultException.Db>(() => direct.GraphTableRowsCancellable(
                    new GraphVisibilityQuery(
                        new GraphFilter(filters.IncludeAttachments, filters.IncludeGhosts, filters.OrphansOnly), string.Empty, null),
                    SlateUniffiMethods.GraphTableDefaultSort(),
                    cancel)));
            }

            PumpedDispatcher.Run(() =>
            {
                using var host = new GraphHost(root, scan: false);

                host.Workspace.OpenGraph();
                host.SettleGraph();

                Assert.Equal(GraphLoadState.Error, host.Document.Publication.State);
                Assert.Equal(detail, host.Document.Publication.Error);
                Assert.Contains(
                    RenderGraph(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.LoadFailed(detail))), host.Lines);
                Assert.DoesNotContain(host.Lines, line => System.Text.RegularExpressions.Regex.IsMatch(line, FieldToken));
            });
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>A note the graph does not hold, opened in a tab with the
    /// Connections leaf showing: the spoken line and the leaf's error state
    /// say core's detail.</summary>
    [Fact]
    public void AFailedConnectionsLoadSaysCoresDetail()
    {
        string root = CopyGraphVault("connections");
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new GraphHost(root, scan: true);
                host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "connections");
                host.Workspace.OpenPath("hub.md", WorkspaceOpenTarget.CurrentTab);
                host.SettleLeaf();
                host.Lines.Clear();

                host.Workspace.OpenPath("missing-note.md", WorkspaceOpenTarget.NewTab);
                host.SettleLeaf();

                const string detail = "Invalid path missing-note.md: no such note in the graph under the active filter";
                Assert.Equal(ConnectionsLoadState.Error, host.Leaf.Publication.State);
                Assert.Equal(detail, host.Leaf.Publication.Failure);
                Assert.Contains(
                    RenderGraph(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.ConnectionsLoadFailed(detail))),
                    host.Lines);
                Assert.Equal("Connections error: " + detail, ConnectionsPhrase.Error(host.Leaf.Publication.Failure!));
            });
        }
        finally
        {
            TryDelete(root);
        }
    }

    // ---- The graph config ------------------------------------------------------

    /// <summary>The graph inspector's read-only notice for a file from a
    /// newer Slate: the FFI crate's words for the variant, the version in
    /// parentheses before "not downgrading", never the binding's "@version="
    /// (the lead's ruling on Call 3). The notice is the preferences' load
    /// failure under the inspector's prefix.</summary>
    [Fact]
    public void AGraphConfigFromANewerSlateSaysTheCratesWords()
    {
        string root = NewConfigRoot();
        try
        {
            WriteConfig(root, "{\"version\": 999}");

            string? failure = LoadFailureOf(root);

            Assert.Equal("graph.json is a newer version (999); not downgrading", failure);
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>A file core's codec cannot parse: the FFI crate's lead
    /// before the parser's own reason, never the binding's "@reason=". The
    /// reason comes from core's codec called directly, so a parser
    /// wording change upstream does not fail this fact.</summary>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"version\": \"x\"}")]
    [InlineData("[1, 2]")]
    public void AnUnparseableGraphConfigSaysTheCratesWords(string text)
    {
        string root = NewConfigRoot();
        try
        {
            WriteConfig(root, text);
            string reason = Assert.Throws<GraphConfigException.Unparseable>(() => SlateUniffiMethods.GraphConfigDecode(text)).@reason;

            string? failure = LoadFailureOf(root);

            Assert.Equal("graph.json is unparseable: " + reason, failure);
            Assert.DoesNotMatch(FieldToken, failure!);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void WriteConfig(string root, string text) =>
        File.WriteAllText(
            Path.Combine(root, ".slate", GraphConfigStore.FileName), text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    /// <summary>A file the store cannot read keeps its own message: the read
    /// arm's ordinary exceptions are not uniffi errors.</summary>
    [Fact]
    public void AnUnreadableGraphConfigKeepsItsOwnMessage()
    {
        string root = NewConfigRoot();
        try
        {
            File.WriteAllBytes(Path.Combine(root, ".slate", GraphConfigStore.FileName), GraphConfigs.InvalidUtf8(withBom: false));

            string? failure = LoadFailureOf(root);

            Assert.StartsWith("graph.json is unreadable: ", failure, StringComparison.Ordinal);
            Assert.DoesNotMatch(FieldToken, failure!);
        }
        finally
        {
            TryDelete(root);
        }
    }

    /// <summary>The reason's last arm cannot run while the binding's
    /// <c>GraphConfigException</c> has exactly these two variants; a third
    /// one core adds fails here before a reader meets that arm.</summary>
    [Fact]
    public void TheGraphConfigErrorHasTwoVariants() =>
        Assert.Equal(
            ["NewerVersion", "Unparseable"],
            typeof(GraphConfigException).GetNestedTypes()
                .Where(type => type.IsSubclassOf(typeof(GraphConfigException)))
                .Select(type => type.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

    private static string NewConfigRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), $"slate-failure-detail-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, ".slate"));
        return root;
    }

    /// <summary>The preferences' load failure, built as the workspace builds
    /// it, after checking the store says the same.</summary>
    private static string? LoadFailureOf(string root)
    {
        GraphConfigLoad load = new GraphConfigStore(root).Read();
        Assert.False(load.Writable);
        string? failure = null;
        PumpedDispatcher.Run(() =>
        {
            var preferences = new GraphPreferencesViewModel(root, new GraphConfigWriter(), new GraphConfigStore(root));
            failure = preferences.LoadFailure;
            Assert.False(preferences.IsWritable);
            preferences.Shutdown();
        });
        Assert.Equal(load.Failure, failure);
        return failure;
    }
}
