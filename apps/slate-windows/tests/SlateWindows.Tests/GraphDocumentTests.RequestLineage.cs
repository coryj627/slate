// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), rule Q — the request lineage (Terms Q1–Q9), rule P's
/// token and headline (Term P4) and the document side of C-3 and C-6:
/// the four request arms through the one entry, the retained token, the
/// policies' lines, the pending sort's transitions, the replacing pairs'
/// inheritance, the token-gated count and the count region's text. Every
/// fact runs under the pumped dispatcher over a real session.
/// </summary>
public sealed partial class GraphDocumentTests
{
    /// <summary>A fetch parked in the worker after its crossings — the
    /// N-th fetch since arming — until released; the test thread pumps
    /// meanwhile (the gated superseding-pair fact's shape).</summary>
    private sealed class Park : IDisposable
    {
        private readonly ManualResetEventSlim _release = new(false);
        private readonly ManualResetEventSlim _reached = new(false);
        private int _fetches;

        public Park(GraphDocumentViewModel document, int which)
        {
            document.FetchGateForTests = () =>
            {
                if (Interlocked.Increment(ref _fetches) == which)
                {
                    _reached.Set();
                    _release.Wait(TimeSpan.FromSeconds(10));
                }
            };
        }

        public void WaitReached() =>
            Assert.True(_reached.Wait(TimeSpan.FromSeconds(10)), "the parked fetch never reached the gate");

        public void Release() => _release.Set();

        public void Dispose()
        {
            _release.Set();
            _release.Dispose();
            _reached.Dispose();
        }
    }

    private static readonly GraphTableSort ByNote = new(GraphTableColumn.Note, true);

    private static string Count(GraphPublication publication) =>
        Render(new GraphA11yEvent.GraphFilterCount((uint)publication.Rows.Count, (uint)publication.Total));

    private static string Headline(GraphPreset preset, GraphPublication publication) =>
        Render(new GraphA11yEvent.GraphPreset(SlateUniffiMethods.GraphPresetOutcome(
            preset, (ulong)publication.Rows.Count, publication.Rows.Count == 0 ? null : publication.Rows[0])));

    private static string GridSortedLine(GraphDocumentViewModel document, GraphTableSort sort) =>
        SlateUniffiMethods.A11yRender(new A11yEvent.GridSorted(
            document.ColumnSpecs.First(c => c.Column == sort.Column).Header, sort.Ascending)).Text;

    /// <summary>The surface's adoption line as <c>GraphTableView</c> relays
    /// it (A-5): GridSorted from PublicationInstalled when the install
    /// answered a sort request — raised synchronously, so it precedes the
    /// receiver's own line (Term Q5).</summary>
    private static void RelayGridSortedOnAdoption(GraphDocumentViewModel document) =>
        document.PublicationInstalled += install =>
        {
            if (install.AnsweredSortRequest)
            {
                document.RelayGridEvent(new A11yEvent.GridSorted(
                    document.ColumnSpecs.First(c => c.Column == install.Current.AcceptedSort.Column).Header,
                    install.Current.AcceptedSort.Ascending));
            }
        };

    private static void Flush(GraphDocumentViewModel document) => document.AnnouncerForTests.FlushForTests();

    private static void Drain(GraphDocumentViewModel document)
    {
        PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
    }

    /// <summary>A bare document over the host's session, quiescent over a
    /// silent first pair, its lines captured.</summary>
    private static GraphDocumentViewModel BareQuiescent(Host host, List<string> lines, bool load = true)
    {
        var document = new GraphDocumentViewModel(
            host.Session,
            new GraphAnnouncer(line => lines.Add(line.Text)),
            new GraphViewState(),
            isEffectiveActive: () => true,
            verbosity: () => GraphVerbosity.Standard);
        if (load)
        {
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            Drain(document);
            Assert.True(document.Publication.HoldsSnapshot);
        }
        return document;
    }

    private static GraphDocumentViewModel Quiescent(Host host)
    {
        host.Workspace.OpenGraph();
        host.Settle();
        host.GraphLines.Clear();
        host.Timeline.Clear();
        return host.Document;
    }

    // --- Terms Q1–Q3, Q8: the entry, the lineage, the kind, the crossings --

    [Fact]
    public void OneTokenPerKeystrokeAndTheBurstLandsTheLastOnce()
    {
        using GraphVault vault = GraphVault.Copy("burst");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            ulong seq = document.SeqForTests;
            int rowsCrossings = document.CrossingsForTests["graph_table_rows"];
            int snapshots = document.CrossingsForTests["graph_snapshot"];
            int installs = 0;
            document.PublicationInstalled += _ => installs++;
            foreach (string needle in new[] { "h", "hu", "hub" })
            {
                document.ViewState.NameQuery = needle;
                Assert.True(document.Request(new GraphRequest.Needle()));
                Assert.Equal(GraphLoadKind.RowsOnly, document.CurrentForTests!.Kind);
                Assert.Equal(GraphAnnouncePolicy.Silent, document.CurrentForTests!.Announce);
            }
            Assert.Equal(seq + 3, document.SeqForTests);
            Assert.True(document.IsRequestInFlight);
            host.Settle();
            Flush(document);
            Assert.False(document.IsRequestInFlight);
            Assert.Null(document.CurrentForTests);
            // One crossing per keystroke, no snapshot; only the last lands.
            Assert.Equal(rowsCrossings + 3, document.CrossingsForTests["graph_table_rows"]);
            Assert.Equal(snapshots, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(1, installs);
            Assert.Equal("hub", document.Publication.Query.NameQuery);
            Assert.Equal([Count(document.Publication)], host.GraphLines);
            Assert.Equal(Count(document.Publication), document.FilterCountText);
        });
    }

    [Fact]
    public void ANeedleDuringTheInitialPairIsAFilterCountPair()
    {
        using GraphVault vault = GraphVault.Copy("needle-initial");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines, load: false);
            using var park = new Park(document, 1);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
            park.WaitReached();
            document.ViewState.NameQuery = "hub";
            Assert.True(document.Request(new GraphRequest.Needle()));
            GraphLoadToken needle = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.Pair, needle.Kind);
            Assert.Equal(GraphAnnouncePolicy.FilterCount, needle.Announce);
            park.Release();
            Drain(document);
            Flush(document);
            // Two crossings for that needle; the count once, no summary (the mac's).
            Assert.Equal(2, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(2, document.CrossingsForTests["graph_table_rows"]);
            Assert.Equal("hub", document.Publication.Query.NameQuery);
            Assert.Equal([Count(document.Publication)], lines);
            document.Retire();
        });
    }

    [Fact]
    public void ABurstDuringAPairCostsTwoCrossingsPerKeystroke()
    {
        using GraphVault vault = GraphVault.Copy("burst-pair");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines, load: false);
            using var park = new Park(document, 1);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
            park.WaitReached();
            foreach (string needle in new[] { "h", "hu", "hub" })
            {
                document.ViewState.NameQuery = needle;
                Assert.True(document.Request(new GraphRequest.Needle()));
                Assert.Equal(GraphLoadKind.Pair, document.CurrentForTests!.Kind);
            }
            park.Release();
            Drain(document);
            Flush(document);
            // The held pair plus three pairs: 2N crossings until the first install (Term Q8, CR-2).
            Assert.Equal(4, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(4, document.CrossingsForTests["graph_table_rows"]);
            Assert.Equal("hub", document.Publication.Query.NameQuery);
            Assert.Equal([Count(document.Publication)], lines);
            document.Retire();
        });
    }

    [Fact]
    public void ARequestOnAnUnseatedOrRetiredDocumentIsRefusedWithoutMutation()
    {
        using GraphVault vault = GraphVault.Copy("refused");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            var lines = new List<string>();
            var beside = new GraphDocumentViewModel(
                host.Session,
                new GraphAnnouncer(line => lines.Add(line.Text)),
                document.ViewState,
                isEffectiveActive: () => true,
                verbosity: () => GraphVerbosity.Standard,
                isSeated: () => false);
            ulong seq = beside.SeqForTests;
            Assert.False(beside.Request(new GraphRequest.Needle()));
            Assert.False(beside.Request(new GraphRequest.Sort(ByNote)));
            Assert.False(beside.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            Assert.False(beside.Request(new GraphRequest.Filter(beside.ViewState.Filter)));
            Assert.Equal(seq, beside.SeqForTests);
            Assert.Null(beside.RequestedSortForTests);
            Assert.False(beside.IsRequestInFlight);
            beside.Retire();

            host.Workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(document.IsRetired);
            ulong retiredSeq = document.SeqForTests;
            Assert.False(document.Request(new GraphRequest.Needle()));
            Assert.Equal(retiredSeq, document.SeqForTests);
        });
    }

    [Fact]
    public void ARejectedCurrentEnvelopeEndsTheLineage()
    {
        using GraphVault vault = GraphVault.Copy("rejected");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            int installs = 0;
            document.PublicationInstalled += _ => installs++;
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            GraphLoadToken token = document.CurrentForTests!;
            Assert.Equal(ByNote, document.RequestedSortForTests);
            park.WaitReached();
            // An envelope for the CURRENT token whose query is not the request's.
            GraphVisibilityQuery foreign = token.Request.Query with { NameQuery = "not-the-request" };
            GraphTableRows rows = host.Session.GraphTableRows(foreign, token.Request.Sort);
            ulong seq = document.SeqForTests;
            document.ReceiveForTests(new GraphLoadEnvelope(token, foreign.Filter, foreign, token.Request.Sort, null, rows, null));
            // Terminal (Term Q2, IGO-6): the lineage ended, the pending sort rolled back, nothing re-fetched.
            Assert.False(document.IsRequestInFlight);
            Assert.Null(document.RequestedSortForTests);
            Assert.Equal(seq, document.SeqForTests);
            Assert.Equal(0, installs);
            park.Release();
            host.Settle();
            Assert.Equal(0, installs);
            Assert.Equal(document.DefaultSort, document.Publication.AcceptedSort);
        });
    }

    [Fact]
    public void AnUnchangedProbeSetsNoLineage()
    {
        using GraphVault vault = GraphVault.Copy("probe-unchanged");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            int snapshots = document.CrossingsForTests["graph_snapshot"];
            int probes = document.CrossingsForTests["graph_generation"];
            document.Probe();
            host.Settle();
            Assert.Equal(probes + 1, document.CrossingsForTests["graph_generation"]);
            Assert.Equal(snapshots, document.CrossingsForTests["graph_snapshot"]);
            Assert.False(document.IsRequestInFlight);
            Assert.Null(document.CurrentForTests);
            Assert.Empty(host.GraphLines);
        });
    }

    [Fact]
    public void TheLineageRecordIsTheTokenInFlightAndNothingElse()
    {
        // Term Q2: the token in flight is the ONLY memory of a policy, a
        // preset or a kind — no second field of those types, no in-flight flag.
        FieldInfo[] fields = typeof(GraphDocumentViewModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Single(fields, f => f.FieldType == typeof(GraphLoadToken));
        Assert.Equal("_current", fields.Single(f => f.FieldType == typeof(GraphLoadToken)).Name);
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(GraphAnnouncePolicy) || f.FieldType == typeof(GraphAnnouncePolicy?));
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(GraphPreset) || f.FieldType == typeof(GraphPreset?));
        Assert.DoesNotContain(fields, f => f.FieldType == typeof(GraphLoadKind) || f.FieldType == typeof(GraphLoadKind?));
        Assert.DoesNotContain(fields, f => f.Name.Contains("InFlight", StringComparison.OrdinalIgnoreCase));
    }

    // --- Terms Q4, Q5: the policies' lines and the pending sort ------------

    [Fact]
    public void AFilterRequestIsAFilterCountPairDroppingTheOverlayAndAPresetInFlight()
    {
        using GraphVault vault = GraphVault.Copy("filter-request");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Unresolved));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Unresolved)));
            park.WaitReached();
            // PR E's manual change: the caller clears the overlay through
            // ApplyQuery, then asks for the filter's pair.
            var attachmentsOn = new GraphFilter(IncludeAttachments: true, IncludeGhosts: true, OrphansOnly: false);
            document.ViewState.ApplyQuery(new GraphVisibilityQuery(attachmentsOn, string.Empty, null));
            Assert.True(document.Request(new GraphRequest.Filter(attachmentsOn)));
            GraphLoadToken filter = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.Pair, filter.Kind);
            Assert.Equal(GraphAnnouncePolicy.FilterCount, filter.Announce);
            Assert.Null(filter.Preset);
            park.Release();
            Drain(document);
            Flush(document);
            Assert.Null(document.Publication.Query.KindOnly);
            Assert.Equal(attachmentsOn, document.Publication.Filter);
            Assert.Equal([Count(document.Publication)], lines);
            Assert.Equal(0, document.CrossingsForTests["graph_preset_outcome"]);
            document.Retire();
        });
    }

    [Fact]
    public void ANeedleDuringAPresetPairSpeaksTheCount()
    {
        using GraphVault vault = GraphVault.Copy("needle-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.MostLinked)));
            park.WaitReached();
            document.ViewState.NameQuery = "hub";
            Assert.True(document.Request(new GraphRequest.Needle()));
            Assert.Equal(GraphAnnouncePolicy.FilterCount, document.CurrentForTests!.Announce);
            Assert.Null(document.CurrentForTests!.Preset);
            park.Release();
            Drain(document);
            Flush(document);
            // The reader's own needle replaced the headline with the count (Term Q4; the mac's).
            Assert.Equal([Count(document.Publication)], lines);
            Assert.Equal(0, document.CrossingsForTests["graph_preset_outcome"]);
            document.Retire();
        });
    }

    [Fact]
    public void ANeedleUnderErrorIsAPairSpeakingTheCount()
    {
        using GraphVault vault = GraphVault.Copy("needle-error");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines, load: false);
            bool armed = true;
            document.FetchGateForTests = () =>
            {
                if (armed)
                {
                    armed = false;
                    throw new InvalidOperationException("injected pair failure");
                }
            };
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
            Drain(document);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.Equal(string.Empty, document.FilterCountText);
            lines.Clear();
            document.ViewState.NameQuery = "hub";
            Assert.True(document.Request(new GraphRequest.Needle()));
            Assert.Equal(GraphLoadKind.Pair, document.CurrentForTests!.Kind);
            Assert.Equal(GraphAnnouncePolicy.FilterCount, document.CurrentForTests!.Announce);
            Drain(document);
            Flush(document);
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal([Count(document.Publication)], lines);
            document.Retire();
        });
    }

    [Fact]
    public void ANeedleKeepsThePendingSortAndGridSortedSpeaksOnAdoption()
    {
        using GraphVault vault = GraphVault.Copy("needle-keeps-sort");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            park.WaitReached();
            document.ViewState.NameQuery = "hub";
            Assert.True(document.Request(new GraphRequest.Needle()));
            GraphLoadToken needle = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.RowsOnly, needle.Kind);
            Assert.Equal(ByNote, needle.Request.Sort);
            Assert.True(needle.UserSort);
            park.Release();
            host.Settle();
            Flush(document);
            Assert.Equal(ByNote, document.Publication.AcceptedSort);
            Assert.Null(document.RequestedSortForTests);
            Assert.Equal([GridSortedLine(document, ByNote), Count(document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void ASortDuringAPairOverACompatibleSnapshotInstallsWithGridSortedThenTheCount()
    {
        using GraphVault vault = GraphVault.Copy("sort-compatible");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            using var park = new Park(document, 1);
            // MostLinked over the default filter: the held snapshot is compatible.
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.MostLinked)));
            park.WaitReached();
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            Assert.Equal(GraphLoadKind.RowsOnly, document.CurrentForTests!.Kind);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.Publication.AcceptedSort == ByNote),
                "the sort never installed over the compatible snapshot");
            park.Release();
            host.Settle();
            Flush(document);
            Assert.Equal([GridSortedLine(document, ByNote), Count(document.Publication)], host.GraphLines);
            Assert.Equal(0, document.CrossingsForTests["graph_preset_outcome"]);
            Assert.False(document.IsRequestInFlight);
        });
    }

    [Fact]
    public void ASortDuringAPairOverAStaleSnapshotRefetchesAndAdoptsWithGridSortedThenTheCount()
    {
        using GraphVault vault = GraphVault.Copy("sort-stale");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            int snapshots = document.CrossingsForTests["graph_snapshot"];
            using var park = new Park(document, 1);
            // Orphans changes the backend filter: the held snapshot is another filter's.
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            park.WaitReached();
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            GraphLoadToken sort = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.RowsOnly, sort.Kind);
            // The rows land over the wrong filter's snapshot: the receiver's
            // replacing pair carries the sort and speaks the count (Term Q9).
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.CurrentForTests is { Kind: GraphLoadKind.Pair } replacing && replacing.Seq > sort.Seq),
                "the receiver never issued the replacing pair");
            GraphLoadToken replacing = document.CurrentForTests!;
            Assert.Equal(ByNote, replacing.Request.Sort);
            Assert.True(replacing.UserSort);
            Assert.Equal(GraphAnnouncePolicy.FilterCount, replacing.Announce);
            park.Release();
            host.Settle();
            Flush(document);
            Assert.True(document.Publication.Filter.OrphansOnly);
            Assert.Equal(ByNote, document.Publication.AcceptedSort);
            Assert.Equal([GridSortedLine(document, ByNote), Count(document.Publication)], host.GraphLines);
            Assert.Equal(0, document.CrossingsForTests["graph_preset_outcome"]);
            Assert.Equal(snapshots + 2, document.CrossingsForTests["graph_snapshot"]);
        });
    }

    [Fact]
    public void TheActivationCarriesThePendingSortAndAdoptsItBeforeTheSummary()
    {
        using GraphVault vault = GraphVault.Copy("activation-sort");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            host.Settle();
            host.GraphLines.Clear();
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            park.WaitReached();
            // Return by tab: the activation's pair carries B (IGP-4).
            host.Workspace.ActiveGroup.ActiveTab = host.GraphTab;
            GraphLoadToken activation = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.Pair, activation.Kind);
            Assert.Equal(GraphAnnouncePolicy.Summary, activation.Announce);
            Assert.Equal(ByNote, activation.Request.Sort);
            Assert.True(activation.UserSort);
            park.Release();
            host.Settle();
            Assert.Equal(ByNote, document.Publication.AcceptedSort);
            Assert.Equal([GridSortedLine(document, ByNote), Summary(document)], host.GraphLines);
        });
    }

    [Fact]
    public void RequestingTheAcceptedSortCancelsThePendingOneSilently()
    {
        using GraphVault vault = GraphVault.Copy("cancel-sort");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            GraphTableSort accepted = document.Publication.AcceptedSort;
            // Nothing pending: the accepted sort asked for issues nothing.
            Assert.False(document.Request(new GraphRequest.Sort(accepted)));
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            park.WaitReached();
            Assert.True(document.Request(new GraphRequest.Sort(accepted)));
            Assert.Null(document.RequestedSortForTests);
            Assert.False(document.CurrentForTests!.UserSort);
            park.Release();
            host.Settle();
            Flush(document);
            Assert.Equal(accepted, document.Publication.AcceptedSort);
            Assert.Equal([Count(document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void APresetReplacesThePendingSortSilently()
    {
        using GraphVault vault = GraphVault.Copy("preset-sort");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            park.WaitReached();
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.MostLinked)));
            GraphLoadToken preset = document.CurrentForTests!;
            Assert.Null(document.RequestedSortForTests);
            Assert.Equal(document.DefaultSort, preset.Request.Sort);
            Assert.False(preset.UserSort);
            park.Release();
            host.Settle();
            Assert.Equal(document.DefaultSort, document.Publication.AcceptedSort);
            Assert.Equal([Headline(GraphPreset.MostLinked, document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void APresetOverAFolderSortResetsSilentlyWithNoGridSorted()
    {
        using GraphVault vault = GraphVault.Copy("preset-folder");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            var folder = new GraphTableSort(GraphTableColumn.Folder, true);
            Assert.True(document.Request(new GraphRequest.Sort(folder)));
            host.Settle();
            Flush(document);
            Assert.Equal(folder, document.Publication.AcceptedSort);
            host.GraphLines.Clear();
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.MostLinked)));
            host.Settle();
            Flush(document);
            Assert.Equal(document.DefaultSort, document.Publication.AcceptedSort);
            Assert.Equal([Headline(GraphPreset.MostLinked, document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void GridSortedPrecedesTheReceiversLineAtEveryAdoptingInstall()
    {
        using GraphVault vault = GraphVault.Copy("gridsorted-first");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            // A FILTER's pair carrying the pending sort: GridSorted, then the
            // count (the needle's and the activation's cells are their own facts).
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            park.WaitReached();
            var attachmentsOn = new GraphFilter(IncludeAttachments: true, IncludeGhosts: true, OrphansOnly: false);
            document.ViewState.ApplyQuery(new GraphVisibilityQuery(attachmentsOn, string.Empty, null));
            Assert.True(document.Request(new GraphRequest.Filter(attachmentsOn)));
            Assert.True(document.CurrentForTests!.UserSort);
            park.Release();
            host.Settle();
            Flush(document);
            Assert.Equal([GridSortedLine(document, ByNote), Count(document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void ASortSpeaksGridSortedThenTheCoalescedCountAndNothingElse()
    {
        using GraphVault vault = GraphVault.Copy("sort-lines");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            RelayGridSortedOnAdoption(document);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            host.Settle();
            Flush(document);
            Assert.Equal([GridSortedLine(document, ByNote), Count(document.Publication)], host.GraphLines);
        });
    }

    // --- Term Q6: the count's gate ----------------------------------------

    [Fact]
    public void AQueuedCountIsDroppedByANewerToken()
    {
        using GraphVault vault = GraphVault.Copy("count-newer");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            document.ViewState.NameQuery = "h";
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle();
            Assert.Empty(host.GraphLines);
            int dropped = document.AnnouncerForTests.DroppedAtFireForTests;
            using var park = new Park(document, 1);
            document.ViewState.NameQuery = "hu";
            Assert.True(document.Request(new GraphRequest.Needle()));
            park.WaitReached();
            // The first count fires while the newer token is in flight: dropped at fire.
            Flush(document);
            Assert.Equal(dropped + 1, document.AnnouncerForTests.DroppedAtFireForTests);
            Assert.Empty(host.GraphLines);
            park.Release();
            host.Settle();
            Flush(document);
            Assert.Equal([Count(document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void ACountQueuedBeforeAPresetIsDropped()
    {
        using GraphVault vault = GraphVault.Copy("count-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            document.ViewState.NameQuery = "h";
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle();
            int dropped = document.AnnouncerForTests.DroppedAtFireForTests;
            using var park = new Park(document, 1);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            park.WaitReached();
            Flush(document);
            Assert.Equal(dropped + 1, document.AnnouncerForTests.DroppedAtFireForTests);
            park.Release();
            host.Settle();
            Flush(document);
            Assert.Equal([Headline(GraphPreset.Orphans, document.Publication)], host.GraphLines);
        });
    }

    [Fact]
    public void ACountWhoseTabLeftEffectiveIsDroppedAtFire()
    {
        using GraphVault vault = GraphVault.Copy("count-left");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            document.ViewState.NameQuery = "h";
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle();
            int dropped = document.AnnouncerForTests.DroppedAtFireForTests;
            string note = document.Publication.Rows.First(r => r.Kind == GraphNodeKind.Note).Path!;
            host.Workspace.OpenPath(note, WorkspaceOpenTarget.NewTab);
            Assert.False(host.Workspace.GraphTabIsEffective());
            Flush(document);
            Assert.Equal(dropped + 1, document.AnnouncerForTests.DroppedAtFireForTests);
            Assert.Empty(host.GraphLines);
        });
    }

    // --- C-6: the needle's crossing, the region's text -----------------------

    [Fact]
    public void TheRawNeedleCrossesUntrimmedAndCoreDecides()
    {
        using GraphVault vault = GraphVault.Copy("raw-needle");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            const string raw = " Café ";
            document.ViewState.NameQuery = raw;
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle();
            Assert.Equal(raw, document.Publication.Query.NameQuery);
            Assert.NotEmpty(document.Publication.Rows);
            Assert.All(document.Publication.Rows, row => Assert.Contains("caf", row.Label, StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void TheRegionIsEmptyUnderLoadingAndError()
    {
        using GraphVault vault = GraphVault.Copy("region-text");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines, load: false);
            Assert.Equal(string.Empty, document.FilterCountText);
            bool armed = true;
            document.FetchGateForTests = () =>
            {
                if (armed)
                {
                    armed = false;
                    throw new InvalidOperationException("injected pair failure");
                }
            };
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            Drain(document);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.Equal(string.Empty, document.FilterCountText);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            Assert.Equal(GraphLoadState.Loading, document.Publication.State);
            Assert.Equal(string.Empty, document.FilterCountText);
            Drain(document);
            Assert.Equal(GraphLoadState.Ready, document.Publication.State);
            Assert.Equal(Count(document.Publication), document.FilterCountText);
            document.ViewState.NameQuery = "zzz-nothing-matches";
            Assert.True(document.Request(new GraphRequest.Needle()));
            Drain(document);
            Assert.Equal(GraphLoadState.Empty, document.Publication.State);
            Assert.Equal(Count(document.Publication), document.FilterCountText);
            Assert.StartsWith("0 of ", document.FilterCountText, StringComparison.Ordinal);
            document.Retire();
        });
    }

    [Fact]
    public void AClearedNeedleUnderTheGhostOverlaySpeaksTheSubsetCount()
    {
        using GraphVault vault = GraphVault.Copy("cleared-overlay");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            document.ViewState.ApplyQuery(new GraphVisibilityQuery(document.ViewState.Filter, "g", GraphNodeKind.Ghost));
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle();
            Flush(document);
            host.GraphLines.Clear();
            document.ViewState.NameQuery = string.Empty;
            Assert.True(document.Request(new GraphRequest.Needle()));
            host.Settle();
            Flush(document);
            Assert.All(document.Publication.Rows, row => Assert.Equal(GraphNodeKind.Ghost, row.Kind));
            Assert.True(document.Publication.Rows.Count < (int)document.Publication.Total);
            Assert.Equal([Count(document.Publication)], host.GraphLines);
        });
    }

    // --- Term Q9: the replacing pairs --------------------------------------

    [Fact]
    public void AProbeDuringThePresetsPairSpeaksTheHeadlineOverTheNewerGeneration()
    {
        using GraphVault vault = GraphVault.Copy("probe-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.MostLinked)));
            GraphLoadToken preset = document.CurrentForTests!;
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > preset.Seq),
                "the probe never superseded the preset's pair");
            GraphLoadToken replacing = document.CurrentForTests!;
            Assert.Equal(GraphAnnouncePolicy.Preset, replacing.Announce);
            Assert.Equal(GraphPreset.MostLinked, replacing.Preset);
            park.Release();
            Drain(document);
            Assert.Contains(document.Publication.Rows, row => row.Path == "iota.md");
            Assert.Equal([Headline(GraphPreset.MostLinked, document.Publication)], lines);
            Assert.Equal(1, document.CrossingsForTests["graph_preset_outcome"]);
            document.Retire();
        });
    }

    [Fact]
    public void AProbeDuringTheActivationsPairSpeaksTheSummary()
    {
        using GraphVault vault = GraphVault.Copy("probe-activation");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            GraphLoadToken activation = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > activation.Seq),
                "the probe never superseded the activation's pair");
            Assert.Equal(GraphAnnouncePolicy.Summary, document.CurrentForTests!.Announce);
            park.Release();
            Drain(document);
            Assert.Contains(document.Publication.Rows, row => row.Path == "iota.md");
            Assert.Equal([Summary(document)], lines);
            document.Retire();
        });
    }

    [Fact]
    public void AProbeDuringANeedlesPairSpeaksTheCount()
    {
        using GraphVault vault = GraphVault.Copy("probe-needle");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines, load: false);
            // The initial pair lands; the needle's FilterCount pair (the second fetch) parks.
            using var park = new Park(document, 2);
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Summary);
            Drain(document);
            lines.Clear();
            document.ViewState.NameQuery = "hub";
            Assert.True(document.Request(new GraphRequest.Needle()));
            GraphLoadToken needle = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.RowsOnly, needle.Kind);
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > needle.Seq),
                "the probe never superseded the needle's token");
            GraphLoadToken replacing = document.CurrentForTests!;
            Assert.Equal(GraphLoadKind.Pair, replacing.Kind);
            Assert.Equal(GraphAnnouncePolicy.FilterCount, replacing.Announce);
            park.Release();
            Drain(document);
            Flush(document);
            Assert.Equal("hub", document.Publication.Query.NameQuery);
            Assert.Equal([Count(document.Publication)], lines);
            document.Retire();
        });
    }

    [Fact]
    public void AProbesPairInheritsThePendingSortAndAdoptsItWithGridSortedThenTheCount()
    {
        using GraphVault vault = GraphVault.Copy("probe-sort");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            RelayGridSortedOnAdoption(document);
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            GraphLoadToken sort = document.CurrentForTests!;
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > sort.Seq),
                "the probe never superseded the sort's token");
            GraphLoadToken replacing = document.CurrentForTests!;
            Assert.Equal(ByNote, replacing.Request.Sort);
            Assert.True(replacing.UserSort);
            park.Release();
            Drain(document);
            Flush(document);
            Assert.Equal(ByNote, document.Publication.AcceptedSort);
            Assert.Null(document.RequestedSortForTests);
            Assert.Equal([GridSortedLine(document, ByNote), Count(document.Publication)], lines);
            document.Retire();
        });
    }

    [Fact]
    public void AProbeDuringASilentTokenIsSilent()
    {
        using GraphVault vault = GraphVault.Copy("probe-silent");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            GraphLoadToken silent = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Silent);
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > silent.Seq),
                "the probe never superseded the silent pair");
            Assert.Equal(GraphAnnouncePolicy.Silent, document.CurrentForTests!.Announce);
            park.Release();
            Drain(document);
            Flush(document);
            Assert.Contains(document.Publication.Rows, row => row.Path == "iota.md");
            Assert.Empty(lines);
            document.Retire();
        });
    }

    [Fact]
    public void AFailingSilentPairRollsThePendingSortBack()
    {
        using GraphVault vault = GraphVault.Copy("failing-replacing");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            Assert.True(document.Request(new GraphRequest.Sort(ByNote)));
            GraphLoadToken sort = document.CurrentForTests!;
            park.WaitReached();
            Assert.Equal(ByNote, document.RequestedSortForTests);
            // The replacing pair's fetch fails (the third fetch: the park's
            // first, the replacing pair's second).
            document.FetchGateForTests = () => throw new InvalidOperationException("injected pair failure");
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.SeqForTests > sort.Seq),
                "the probe never superseded the sort's token");
            park.Release();
            Drain(document);
            // Any token's terminal failure rolls the pending sort back (Term Q2).
            Assert.Null(document.RequestedSortForTests);
            Assert.False(document.IsRequestInFlight);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.NotEqual(ByNote, document.Publication.AcceptedSort);
            Assert.Equal([LoadFailed("injected pair failure")], lines);
            document.Retire();
        });
    }

    [Fact]
    public void TheHighWaterPairInheritsNothing()
    {
        using GraphVault vault = GraphVault.Copy("high-water-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines, load: false);
            using var park = new Park(document, 1);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
            _ = document.Load(GraphLoadKind.Pair, GraphAnnouncePolicy.Preset, preset: GraphPreset.MostLinked);
            park.WaitReached();
            _ = host.Session.CreateExclusive("iota.md", "# Iota\n\n[[hub]]\n");
            document.Probe();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => document.HighWaterForTests > 0),
                "the probe never kept the high-water mark");
            park.Release();
            Drain(document);
            // The preset's pair installed and spoke; the high-water pair
            // followed silently — no second headline, one outcome crossing.
            Assert.Equal(0UL, document.HighWaterForTests);
            Assert.Contains(document.Publication.Rows, row => row.Path == "iota.md");
            Assert.Equal(2, document.CrossingsForTests["graph_snapshot"]);
            Assert.Equal(1, document.CrossingsForTests["graph_preset_outcome"]);
            Assert.Single(lines);
            Assert.StartsWith("Most linked:", lines[0], StringComparison.Ordinal);
            document.Retire();
        });
    }

    [Fact]
    public void AStraddledPresetPairRefetchesAndSpeaksTheHeadline()
    {
        using GraphVault vault = GraphVault.Copy("straddled-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            using var park = new Park(document, 1);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            GraphLoadToken preset = document.CurrentForTests!;
            park.WaitReached();
            // The worker's envelope, straddled: the rows a generation ahead of the snapshot.
            GraphSnapshot snapshot = host.Session.GraphSnapshot(preset.Request.Query.Filter);
            GraphTableRows rows = host.Session.GraphTableRows(preset.Request.Query, preset.Request.Sort);
            GraphTableRows straddled = rows with { Generation = rows.Generation + 1 };
            document.ReceiveForTests(new GraphLoadEnvelope(
                preset, preset.Request.Query.Filter, preset.Request.Query, preset.Request.Sort, snapshot, straddled, null, document.ViewState.SelectionGeneration));
            GraphLoadToken replacing = document.CurrentForTests!;
            Assert.True(replacing.Seq > preset.Seq);
            Assert.Equal(GraphAnnouncePolicy.Preset, replacing.Announce);
            Assert.Equal(GraphPreset.Orphans, replacing.Preset);
            park.Release();
            Drain(document);
            Assert.True(document.Publication.Filter.OrphansOnly);
            Assert.Equal([Headline(GraphPreset.Orphans, document.Publication)], lines);
            Assert.Equal(1, document.CrossingsForTests["graph_preset_outcome"]);
            document.Retire();
        });
    }

    [Fact]
    public void AProbeAfterTheHeadlineReplaysNothing()
    {
        using GraphVault vault = GraphVault.Copy("probe-after");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            Drain(document);
            Assert.Equal([Headline(GraphPreset.Orphans, document.Publication)], lines);
            lines.Clear();
            _ = host.Session.CreateExclusive("lonely.md", "# Lonely\n");
            document.Probe();
            Drain(document);
            Assert.Contains(document.Publication.Rows, row => row.Path == "lonely.md");
            Assert.Empty(lines);
            Assert.Equal(1, document.CrossingsForTests["graph_preset_outcome"]);
            document.Retire();
        });
    }

    // --- C-3's document side: the outcomes, the failure, the selection -------

    [Fact]
    public void EachOutcomeAndNoNotesToRankOnAnEmptyVault()
    {
        using GraphVault vault = GraphVault.Copy("outcomes");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            foreach (GraphPreset preset in new[] { GraphPreset.Orphans, GraphPreset.Unresolved, GraphPreset.MostLinked })
            {
                host.GraphLines.Clear();
                GraphVisibilityQuery query = SlateUniffiMethods.GraphPresetQuery(preset);
                document.ViewState.ApplyQuery(query);
                Assert.True(document.Request(new GraphRequest.Preset(preset)));
                host.Settle();
                Flush(document);
                Assert.Equal(query, document.Publication.Query);
                Assert.Equal([Headline(preset, document.Publication)], host.GraphLines);
                if (preset == GraphPreset.Unresolved)
                {
                    Assert.All(document.Publication.Rows, row => Assert.Equal(GraphNodeKind.Ghost, row.Kind));
                }
            }
            Assert.Equal(3, document.CrossingsForTests["graph_preset_outcome"]);
        });
        string empty = Path.Combine(Path.GetTempPath(), $"slate-graph-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);
        try
        {
            PumpedDispatcher.Run(() =>
            {
                using var host = new Host(empty);
                GraphDocumentViewModel document = Quiescent(host);
                document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.MostLinked));
                Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.MostLinked)));
                host.Settle();
                Assert.Empty(document.Publication.Rows);
                Assert.Equal([Render(new GraphA11yEvent.GraphPreset(new GraphPresetOutcome.NoNotesToRank()))], host.GraphLines);
            });
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }
    }

    [Fact]
    public void AFailingPresetPairSpeaksTheBlockAndNoHeadline()
    {
        using GraphVault vault = GraphVault.Copy("failing-preset");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            var lines = new List<string>();
            GraphDocumentViewModel document = BareQuiescent(host, lines);
            document.FetchGateForTests = () => throw new InvalidOperationException("injected pair failure");
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            Drain(document);
            Assert.Equal(GraphLoadState.Error, document.Publication.State);
            Assert.Equal([LoadFailed("injected pair failure")], lines);
            Assert.Equal(0, document.CrossingsForTests["graph_preset_outcome"]);
            Assert.False(document.IsRequestInFlight);
            Assert.Null(document.CurrentForTests);
            document.Retire();
        });
    }

    [Fact]
    public void ThePresetsPublicationReseatsTheKeyAndSelectsNothingElse()
    {
        using GraphVault vault = GraphVault.Copy("preset-selection");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            string hub = document.Publication.Rows.First(r => r.Path == "hub.md").StableKey;
            Assert.True(document.SelectRow(hub));
            // Unresolved: the snapshot (ghosts on) keeps the note, so the key survives the overlay.
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Unresolved));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Unresolved)));
            host.Settle();
            Assert.Equal(hub, document.ViewState.SelectedKey);
            Assert.DoesNotContain(document.Publication.Rows, row => row.StableKey == hub);
            // Orphans: the snapshot drops the note, so A-7 clears the key — nothing else is selected.
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Orphans));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            host.Settle();
            Assert.Null(document.ViewState.SelectedKey);
        });
    }

    [Fact]
    public void ANeedleTypedAfterwardsKeepsTheOverlay()
    {
        using GraphVault vault = GraphVault.Copy("needle-overlay");
        PumpedDispatcher.Run(() =>
        {
            using var host = new Host(vault.Root);
            GraphDocumentViewModel document = Quiescent(host);
            document.ViewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(GraphPreset.Unresolved));
            Assert.True(document.Request(new GraphRequest.Preset(GraphPreset.Unresolved)));
            host.Settle();
            document.ViewState.NameQuery = "g";
            Assert.True(document.Request(new GraphRequest.Needle()));
            Assert.Equal(GraphLoadKind.RowsOnly, document.CurrentForTests!.Kind);
            host.Settle();
            Assert.Equal(GraphNodeKind.Ghost, document.Publication.Query.KindOnly);
            Assert.Equal("g", document.Publication.Query.NameQuery);
            Assert.NotEmpty(document.Publication.Rows);
            Assert.All(document.Publication.Rows, row => Assert.Equal(GraphNodeKind.Ghost, row.Kind));
        });
    }
}
