// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// REFERENCE PROBES for #1285 — compiled by no project. To run them, copy
// this file into apps/slate-windows/tests/SlateWindows.Tests/, rebuild the
// WineTestRunner (or the unit project on Windows) and run the class
// SlateWindows.Tests.CitationSummaryInterleavingProbe. Never commit it into
// the unit project: two facts fail BY DESIGN, because each demonstrates a
// failure.
//
// Results recorded under Wine on 2026-09-29 (see ../README.md):
//   OriginalAssertionsWhenCitedPublishesBeforeTheMove   FAIL — the reported
//     #1285 failure, reproduced on demand
//   ProductAnswersForTheAskedNoteWhenItPublishesBeforeTheMove   PASS
//   ProductDropsThePressWhenTheMoveLandsFirst   PASS
//   OriginalTestVerbatim   passes under plain Wine; fails about half the
//     time with slow-persist.patch applied
//   TimingOfTheSeedAgainstTheMove   always passes; prints a TIMING line
//   ParkedPressAfterTheNoteClosesDoesNotAnswerAReopen   FAIL — the separate
//     product bug (a parked Ctrl+Shift+J survives closing its note)

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class CitationSummaryInterleavingProbe : IDisposable
{
    private readonly FixtureVault _fixture;

    /// <summary>The same fixture as CitationAsyncInterleavingTests.</summary>
    public CitationSummaryInterleavingProbe()
    {
        _fixture = FixtureVault.Create(0, "citation-probe");
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
        File.WriteAllText(
            Path.Combine(_fixture.Root, "other.md"),
            "# Other\n\nOnly one citation [@filler7] here.\n");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "demo-vault")))
        {
            dir = dir.Parent;
        }
        File.Copy(
            Path.Combine(dir!.FullName, "demo-vault", "csl", "ieee.csl"),
            Path.Combine(_fixture.Root, "ieee.csl"));
        File.WriteAllText(
            Path.Combine(_fixture.Root, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");
    }

    public void Dispose() => _fixture.Dispose();

    private VaultSession OpenScanned()
    {
        var session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private WorkspaceViewModel MakeAsyncWorkspace(
        VaultSession session, List<A11yEvent> announced) =>
        new(session, _fixture.Root, () => [], announced.Add,
            startInteractionBackgroundWork: true);

    private static async Task QuiesceAsync(WorkspaceViewModel workspace)
    {
        for (int round = 0; round < 40; round++)
        {
            await workspace.SeedWorkForTests;
            await Task.WhenAll(
                workspace.Citations.DrainForTests(),
                workspace.Bibliography.DrainForTests());
            await Task.Delay(2);
        }
    }

    /// <summary>The ORIGINAL test's assertions, with cited.md's answer
    /// allowed to land before the move (a quiesce between the park and the
    /// move). Expected to FAIL exactly as #1285 reports.</summary>
    [Fact]
    public async Task OriginalAssertionsWhenCitedPublishesBeforeTheMove()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);

        workspace.OpenPath("cited.md");
        Assert.True(workspace.Citations.IsLoading);
        workspace.OpenCitationSummary();
        Assert.Null(workspace.CitationSummary);

        await QuiesceAsync(workspace);
        workspace.OpenPath("other.md");
        await QuiesceAsync(workspace);

        Assert.Null(workspace.CitationSummary);
        Assert.Equal("other.md", workspace.Citations.Path);
    }

    /// <summary>What the product does in that ordering: the parked press
    /// is answered for the note it was asked about, while that note is
    /// still current, with cited.md's counts.</summary>
    [Fact]
    public async Task ProductAnswersForTheAskedNoteWhenItPublishesBeforeTheMove()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        string? pathWhenOpened = null;
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.CitationSummary)
                && workspace.CitationSummary is not null)
            {
                pathWhenOpened = workspace.Citations.Path;
            }
        };

        workspace.OpenPath("cited.md");
        workspace.OpenCitationSummary();
        await QuiesceAsync(workspace);
        workspace.OpenPath("other.md");
        await QuiesceAsync(workspace);

        Assert.NotNull(workspace.CitationSummary);
        Assert.Equal("cited.md", pathWhenOpened);
        Assert.Equal(
            "This note has 2 citations referencing 2 unique sources.",
            workspace.CitationSummary!.Body);
    }

    /// <summary>The opposite ordering, forced with the leaf's own seam:
    /// cited.md's answer is held until after the move. The product drops
    /// the parked press.</summary>
    [Fact]
    public async Task ProductDropsThePressWhenTheMoveLandsFirst()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        using var held = new ManualResetEventSlim(false);
        workspace.Citations.InterleaveForTests = () => held.Wait(TimeSpan.FromSeconds(10));

        workspace.OpenPath("cited.md");
        Assert.True(workspace.Citations.IsLoading);
        workspace.OpenCitationSummary();
        Assert.Null(workspace.CitationSummary);
        workspace.OpenPath("other.md");
        held.Set();
        workspace.Citations.InterleaveForTests = null;
        await QuiesceAsync(workspace);

        Assert.Null(workspace.CitationSummary);
        Assert.Equal("other.md", workspace.Citations.Path);
    }

    /// <summary>The pre-fix ADeferredSummaryDoesNotAnswerForADifferentNote
    /// body, verbatim, for the before/after under slow-persist.patch.</summary>
    [Fact]
    public async Task OriginalTestVerbatim()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);

        workspace.OpenPath("cited.md");
        // Deterministic: Refresh sets IsLoading synchronously before
        // queueing the gated body.
        Assert.True(workspace.Citations.IsLoading);

        workspace.OpenCitationSummary();
        Assert.Null(workspace.CitationSummary);

        // The user moves on before the answer arrives.
        workspace.OpenPath("other.md");
        await QuiesceAsync(workspace);

        // No sheet, because the question was about cited.md and the
        // only answer available is about other.md.
        Assert.Null(workspace.CitationSummary);
        Assert.Equal("other.md", workspace.Citations.Path);
    }

    /// <summary>Timing only (always passes): where the seed settles and
    /// cited.md's body runs relative to the park and the move, in
    /// milliseconds from the fact's start.</summary>
    [Fact]
    public async Task TimingOfTheSeedAgainstTheMove()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        long tStart = clock.ElapsedMilliseconds;
        using var workspace = MakeAsyncWorkspace(session, announced);
        long tCtor = clock.ElapsedMilliseconds;
        long tSeed = -1, tBody = -1, tPublish = -1;
        _ = workspace.SeedWorkForTests.ContinueWith(
            _ => Volatile.Write(ref tSeed, clock.ElapsedMilliseconds), TaskScheduler.Default);
        workspace.Citations.InterleaveForTests = () =>
            Interlocked.CompareExchange(ref tBody, clock.ElapsedMilliseconds, -1);
        workspace.Citations.RowsPublished += (_, _) =>
            Interlocked.CompareExchange(ref tPublish, clock.ElapsedMilliseconds, -1);

        workspace.OpenPath("cited.md");
        long tOpened = clock.ElapsedMilliseconds;
        workspace.OpenCitationSummary();
        long tParked = clock.ElapsedMilliseconds;
        workspace.OpenPath("other.md");
        long tMoved = clock.ElapsedMilliseconds;
        await QuiesceAsync(workspace);

        Console.WriteLine(
            $"TIMING start={tStart} ctor={tCtor} opened={tOpened} parked={tParked} moved={tMoved} "
            + $"seedSettled={Volatile.Read(ref tSeed)} firstBodyRead={Volatile.Read(ref tBody)} "
            + $"firstPublish={Volatile.Read(ref tPublish)}");
    }

    /// <summary>The separate product bug: a note change to NO note never
    /// publishes, so the parked press stays armed and answers when the
    /// same note is opened again later. Fails today.</summary>
    [Fact]
    public async Task ParkedPressAfterTheNoteClosesDoesNotAnswerAReopen()
    {
        var announced = new List<A11yEvent>();
        using VaultSession session = OpenScanned();
        using var workspace = MakeAsyncWorkspace(session, announced);
        using var held = new ManualResetEventSlim(false);
        workspace.Citations.InterleaveForTests = () => held.Wait(TimeSpan.FromSeconds(10));

        workspace.OpenPath("cited.md");
        workspace.OpenCitationSummary();
        workspace.CloseActiveTabCommand.Execute(null);
        held.Set();
        workspace.Citations.InterleaveForTests = null;
        await QuiesceAsync(workspace);
        Assert.Null(workspace.Citations.Path);
        Assert.Null(workspace.CitationSummary);

        // Later, the user opens the note again — nobody pressed anything.
        workspace.OpenPath("cited.md");
        await QuiesceAsync(workspace);

        Assert.Null(workspace.CitationSummary);
    }
}
