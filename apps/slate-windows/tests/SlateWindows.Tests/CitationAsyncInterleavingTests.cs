// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): the citation suite under PRODUCTION scheduling
/// (startInteractionBackgroundWork: true). Every other citation test
/// runs the synchronous test mode, which orders seeding, citation
/// loads and bibliography loads deterministically and therefore cannot
/// observe the interleavings the app actually ships.
/// </summary>
public sealed class CitationAsyncInterleavingTests : IDisposable
{
    private readonly FixtureVault _fixture;

    public CitationAsyncInterleavingTests()
    {
        _fixture = FixtureVault.Create(0, "citation-async");
        // A REALISTICALLY sized bibliography. The interleaving under
        // test is "did the key lookup run before the entries landed",
        // and with a toy 1-entry file the worker finishes inside the
        // two statements between EnsureLoaded and the lookup — the
        // race resolves the lucky way and hides the defect. Thousands
        // of entries put the load orders of magnitude beyond that gap.
        var bib = new System.Text.StringBuilder(
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984}\n}\n");
        for (int i = 0; i < 5000; i++)
        {
            _ = bib.Append($"@article{{filler{i},\n  title = {{Filler Study Number {i}}},\n")
                .Append($"  author = {{Author, Some {i}}},\n  year = {{20{i % 100:D2}}}\n}}\n");
        }
        File.WriteAllText(Path.Combine(_fixture.Root, "library.bib"), bib.ToString());
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cited.md"),
            "# Cited\n\nA citation [@knuth1984] and a ghost [@ghostkey].\n");
        File.Copy(
            Path.Combine(RepoRoot, "demo-vault", "csl", "ieee.csl"),
            Path.Combine(_fixture.Root, "ieee.csl"));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");
    }

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "demo-vault")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }
    }

    public void Dispose() => _fixture.Dispose();

    private VaultSession OpenScanned()
    {
        var session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    /// <summary>The shipping configuration: background work ON.</summary>
    private WorkspaceViewModel MakeAsyncWorkspace(
        VaultSession session, List<A11yEvent> announced) =>
        new(session, _fixture.Root, () => [], announced.Add,
            startInteractionBackgroundWork: true);

    /// <summary>Run the citation panels to a fixed point: drain tracked
    /// worker tasks, then let the publishes they POSTED actually run.
    /// Repeats because a drained worker can post a publish that queues
    /// further work. Task.Delay, not Task.Yield: a worker's publish is
    /// handed to the SynchronizationContext captured at construction,
    /// and yielding does not give that callback a chance to run — a
    /// drained worker is NOT the same as an applied publish.</summary>
    private static async Task QuiesceAsync(WorkspaceViewModel workspace)
    {
        for (int round = 0; round < 40; round++)
        {
            await Task.WhenAll(
                workspace.Citations.DrainForTests(),
                workspace.Bibliography.DrainForTests());
            await Task.Delay(2);
        }
    }

    /// <summary>The workspace constructor starts the first note's
    /// citation load (SyncPanels) and the vault's source seeding
    /// (SetBibliographySources) as two independent background
    /// operations. A citation render that wins that race sees no
    /// sources and marks every key unresolved — and because a
    /// same-path NoteChanged is a no-op and ApplySeedOutcome does not
    /// re-query, nothing ever repairs those rows.</summary>
    [Fact]
    public async Task CitationsResolveWhenSourceSeedingCompletesAfterTheFirstLoad()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);

        Assert.Equal(2, workspace.Citations.Rows.Count);
        Assert.Null(workspace.Citations.LoadError);
        // knuth1984 IS in the bibliography; only ghostkey is not.
        Assert.Single(workspace.Citations.Rows, row => !row.IsUnresolved);
    }

    /// <summary>Ctrl+J on the FIRST use, before the bibliography rail
    /// has ever been revealed: EnsureLoaded only STARTS the entries
    /// load, so the key lookup must not be answered from the empty
    /// pre-load list. The user pressed a key that means "take me to
    /// this entry" and the entry exists.</summary>
    [Fact]
    public async Task JumpToBibliographyLandsOnAKeyWhoseEntriesLoadAsynchronously()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);

        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));

        // The bibliography has never been revealed, so this is the
        // first EnsureLoaded: the entries load STARTS here.
        Assert.Empty(workspace.Bibliography.Entries);

        workspace.JumpToBibliography();
        // NOTE: no "the load has not published yet" assertion here.
        // That is a claim about SCHEDULING, and without a controllable
        // seam it is a coin flip — a cold run can stall the test thread
        // long enough for the worker to finish, which failed this suite
        // once in three. The interleaving assertion returns with the
        // manually-released scheduler seam; until then these tests pin
        // the END STATE only.
        await QuiesceAsync(workspace);

        Assert.Equal("knuth1984", workspace.Bibliography.ConsumeKeyFocusRequest());
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Jumped to bibliography entry: knuth1984.");
        Assert.DoesNotContain(
            announced.OfType<A11yEvent.HostComposed>(),
            e => e.Text == "Searching bibliography for: knuth1984.");
    }

    /// <summary>The focus request is now settled ASYNCHRONOUSLY on the
    /// first press, so a view cannot invoke the command and read a
    /// property straight afterwards — it has to be told. This is the
    /// contract the W4-5 XAML will bind against.</summary>
    [Fact]
    public async Task ADeferredJumpNotifiesTheViewAndIsConsumableExactlyOnce()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);
        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));

        int notifications = 0;
        workspace.Bibliography.KeyFocusRequested += (_, _) => notifications++;

        workspace.JumpToBibliography();
        // See the sibling test: no pre-quiescence scheduling assertion.
        await QuiesceAsync(workspace);

        Assert.Equal(1, notifications);
        Assert.Equal("knuth1984", workspace.Bibliography.ConsumeKeyFocusRequest());
        // Consumed exactly once: a stale value must never steal focus
        // from a later interaction.
        Assert.Null(workspace.Bibliography.ConsumeKeyFocusRequest());
    }

    /// <summary>A jump that never resolves must not leave a live focus
    /// request behind for whatever binds next.</summary>
    [Fact]
    public async Task ShutdownDropsAnUnconsumedFocusRequest()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);
        workspace.OpenCitationDetails(
            workspace.Citations.Rows.First(row => !row.IsUnresolved));

        workspace.JumpToBibliography();
        await QuiesceAsync(workspace);
        workspace.Bibliography.Shutdown();

        Assert.Null(workspace.Bibliography.ConsumeKeyFocusRequest());
    }

    /// <summary>
    /// Ctrl+J semantics (design decision 5): a parked press must never
    /// be discarded into SILENCE. The latest press wins and exactly one
    /// announcement is heard — a superseded press is quiet only because
    /// its successor speaks. A reload mid-jump has no successor, so it
    /// RE-TARGETS the parked request at the reloaded set instead of
    /// dropping it; a keypress that produces no speech at all reads as
    /// a dead key in a screen-reader-first app.
    ///
    /// The seed is the release seam: holding it unsettled keeps the
    /// entries load parked, so the jump genuinely parks too. The
    /// synchronous test mode CANNOT express this — it runs the load
    /// inline, so RequestKeyFocus always resolves immediately and the
    /// parked path is never entered. (A first version of this test
    /// lived there and passed against a deliberately broken
    /// implementation.)
    /// </summary>
    [Fact]
    public async Task AReloadMidJumpRetargetsTheParkedPressInsteadOfSilencingIt()
    {
        using VaultSession session = OpenScanned();
        var leaf = new BibliographyViewModel(
            session, _ => { }, synchronousForTests: false);
        var seed = new BibliographySeed();
        leaf.AttachSeed(seed);
        var answered = new List<(string Key, bool Present)>();

        // Parked: the gate is unsettled, so the entries load cannot
        // finish and the key is not yet answerable.
        leaf.EnsureLoaded();
        leaf.RequestKeyFocus("knuth1984", (key, present) => answered.Add((key, present)));
        Assert.Empty(answered);

        // A reload lands while the press is still parked.
        leaf.ForceReload();
        // Seed for real before releasing, exactly as the workspace
        // does — otherwise core answers empty and "present" would be
        // false for reasons that have nothing to do with the retarget.
        _ = session.SetBibliographySources(session.CitationsPrefs().Sources);
        seed.Complete(new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));

        for (int round = 0; round < 40; round++)
        {
            await leaf.DrainForTests();
            await Task.Delay(2);
        }

        (string Key, bool Present) only = Assert.Single(answered);
        Assert.Equal("knuth1984", only.Key);
        Assert.True(only.Present);
    }

    /// <summary>
    /// After a FAILED seed the two citation surfaces must AGREE.
    ///
    /// Core's set_bibliography_sources is all-or-nothing and returns
    /// before replacing anything, so the previous session's entries and
    /// BibIndex survive a failed load. The bibliography leaf refuses to
    /// show them (D-13). The citations leaf must not turn around and
    /// render the same keys as resolved from that same stale index —
    /// two surfaces in one window disagreeing about whether an entry
    /// exists is the dishonesty contract 5 exists to prevent.
    ///
    /// The fixture session IS seeded, so core really would answer:
    /// without the refusal this publishes a resolved row.
    /// </summary>
    [Fact]
    public async Task AFailedSeedStopsBothLeavesReadingCoreNotJustTheBibliography()
    {
        using VaultSession session = OpenScanned();
        _ = session.SetBibliographySources(session.CitationsPrefs().Sources);
        Assert.NotEmpty(session.GetBibliographyEntries());

        var seed = new BibliographySeed();
        var citations = new CitationsPanelViewModel(
            session, _ => { }, synchronousForTests: false);
        citations.AttachSeed(seed);
        seed.Complete(new BibliographySeedOutcome(
            BibliographySeedStatus.Failed, ["library.bib: permission denied"]));

        citations.NoteChanged("cited.md");
        for (int round = 0; round < 40; round++)
        {
            await citations.DrainForTests();
            await Task.Delay(2);
        }

        // The rows exist — the note really does cite two keys — but not
        // one of them claims to have been resolved.
        Assert.Equal(2, citations.Rows.Count);
        Assert.All(citations.Rows, row => Assert.Null(row.Rendered));
    }

    /// <summary>
    /// A reload must publish its CLEAR, not just its eventual result.
    ///
    /// Contract 3: "clears rows synchronously, so no stale row is ever
    /// actionable while a newer read is in flight." That held while the
    /// window rebound from CollectionChanged — `Entries.Clear()` itself
    /// rebound the grid to empty. Replacing that with an explicit
    /// publish event moved the rebind to the END of the load, so
    /// between the clear and the publish the grid keeps the PREVIOUS
    /// rows bound and live: Enter opens a details sheet for a discarded
    /// entry, and the row action queries a stale key.
    ///
    /// The gate is held so the window between clear and publish is
    /// observable at all; in synchronous mode the load completes inline
    /// and there is no gap to see.
    /// </summary>
    [Fact]
    public async Task AReloadPublishesTheClearBeforeTheNewReadLands()
    {
        using VaultSession session = OpenScanned();
        _ = session.SetBibliographySources(session.CitationsPrefs().Sources);
        var leaf = new BibliographyViewModel(
            session, _ => { }, synchronousForTests: false);
        var seed = new BibliographySeed();
        leaf.AttachSeed(seed);
        seed.Complete(new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));
        leaf.EnsureLoaded();
        for (int round = 0; round < 40; round++)
        {
            await leaf.DrainForTests();
            await Task.Delay(2);
        }
        Assert.NotEmpty(leaf.Entries);

        // Re-gate so the reload's read cannot land immediately.
        var held = new BibliographySeed();
        leaf.AttachSeed(held);
        int entriesPublished = 0;
        int unresolvedPublished = 0;
        leaf.EntriesPublished += (_, _) => entriesPublished++;
        leaf.UnresolvedPublished += (_, _) => unresolvedPublished++;

        leaf.ForceReload();

        // The clear is announced to the view immediately, so nothing
        // stale stays bound while the new read is in flight.
        Assert.Empty(leaf.Entries);
        Assert.Equal(1, entriesPublished);
        Assert.Equal(1, unresolvedPublished);
    }

    /// <summary>
    /// Gated bodies must never run on the caller's thread.
    ///
    /// The subtle case is an ALREADY-SETTLED gate: awaiting a completed
    /// task does not yield, so a naive `await gate; body();` runs the
    /// body straight through on the caller. In production the caller is
    /// always the UI thread and the seed is settled for the entire life
    /// of the workspace after startup, so that one line would put every
    /// citation FFI call — including the whole-vault unresolved query —
    /// on the dispatcher. Settle the seed FIRST here, which is exactly
    /// the steady state the app spends its life in.
    /// </summary>
    [Fact]
    public async Task AGatedBodyRunsOffTheCallersThreadEvenWhenTheSeedAlreadySettled()
    {
        using VaultSession session = OpenScanned();
        _ = session.SetBibliographySources(session.CitationsPrefs().Sources);
        var leaf = new BibliographyViewModel(
            session, _ => { }, synchronousForTests: false);
        var seed = new BibliographySeed();
        leaf.AttachSeed(seed);
        // Settled BEFORE any work is queued: the gate is complete, so
        // the await cannot suspend.
        seed.Complete(new BibliographySeedOutcome(BibliographySeedStatus.Seeded, []));

        int callerThread = Environment.CurrentManagedThreadId;
        int bodyThread = 0;
        leaf.InterleaveForTests =
            () => Volatile.Write(ref bodyThread, Environment.CurrentManagedThreadId);

        leaf.EnsureLoaded();

        for (int round = 0; round < 40; round++)
        {
            await leaf.DrainForTests();
            await Task.Delay(2);
        }

        Assert.NotEqual(0, Volatile.Read(ref bodyThread));
        Assert.NotEqual(callerThread, Volatile.Read(ref bodyThread));
    }

    /// <summary>
    /// Teardown must RELEASE parked bodies, and they must not run.
    ///
    /// Asserting "nothing published" would prove nothing here — the
    /// publish paths already check IsShutDown themselves, so that
    /// assertion passes with the scheduler's post-wait recheck deleted.
    /// What the recheck actually buys is that the BODY never executes:
    /// the vault lifecycle disposes the shared session immediately
    /// after the workspace, so a body waking up post-shutdown would
    /// call across FFI into a session that is going away.
    /// </summary>
    [Fact]
    public async Task ShutdownReleasesParkedBodiesWithoutRunningThem()
    {
        using VaultSession session = OpenScanned();
        _ = session.SetBibliographySources(session.CitationsPrefs().Sources);
        var leaf = new BibliographyViewModel(
            session, _ => { }, synchronousForTests: false);
        var seed = new BibliographySeed();
        leaf.AttachSeed(seed);
        int bodyRuns = 0;
        // Fires inside the body, after the core query — so a non-zero
        // count means a body ran against a session already torn down.
        leaf.InterleaveForTests = () => Interlocked.Increment(ref bodyRuns);
        leaf.EnsureLoaded();

        leaf.Shutdown();
        seed.Cancel();

        // The parked work completes rather than hanging forever...
        Task drained = leaf.DrainForTests();
        Assert.Same(drained, await Task.WhenAny(drained, Task.Delay(5_000)));
        await Task.Delay(20);
        // ...without ever touching the session.
        Assert.Equal(0, Volatile.Read(ref bodyRuns));
    }
}
