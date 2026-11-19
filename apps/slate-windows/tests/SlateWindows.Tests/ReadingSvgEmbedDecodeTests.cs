// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Documents;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1634: an SVG embed is decoded with its resolution, on the reading
/// refresh's worker, never while the document is built on the dispatcher.
/// The decode can run up to its 5 s budget (#1581); a dispatcher blocked
/// that long when the reading view takes focus leaves NVDA on its
/// IAccessible fallback, and Down says nothing until focus moves.
/// </summary>
public sealed class ReadingSvgEmbedDecodeTests
{
    private const string Svg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'>"
        + "<rect width='8' height='8' fill='red'/></svg>";

    [Fact]
    public void AnSvgEmbedIsDecodedOffTheDispatcherBeforeTheDocumentIsBuilt()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-thread");
        File.WriteAllText(Path.Combine(fixture.Root, "pic.svg"), Svg);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "Before.\n\n![[pic.svg]]\n\nAfter.\n");
        var fetched = new List<int>();
        var built = new List<string>();
        ReadingContentViewModel.EmbedSvgDecodeProbeForTests = _ =>
        {
            lock (fetched)
            {
                fetched.Add(Environment.CurrentManagedThreadId);
            }
        };
        ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = built.Add;
        try
        {
            StaThread.RunPumped(() =>
            {
                int dispatcher = Environment.CurrentManagedThreadId;
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: true);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                // A slow decode is waited out too (Codex on #1949).
                Assert.True(
                    PumpedDispatcher.PumpUntil(
                        () => !reading.IsLoading && reading.Document is not null,
                        UnhurriedSvgDecodes.LandingWait()),
                    "the projection never landed");

                Assert.Empty(built);
                lock (fetched)
                {
                    Assert.NotEmpty(fetched);
                    Assert.DoesNotContain(dispatcher, fetched);
                }

                FlowDocument document = reading.Document!;
                Assert.True(
                    BlocksOf(document.Blocks).Any(
                        block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } }),
                    "the decoded image was not placed: "
                    + new TextRange(document.ContentStart, document.ContentEnd).Text
                    + " / " + reading.LastTerminalFailureForTests);
            });
        }
        finally
        {
            ReadingContentViewModel.EmbedSvgDecodeProbeForTests = null;
            ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = null;
        }
    }

    private const string TookLonger = "Image not shown: decoding took longer than 5 seconds.";

    private const string ProjectionSpent =
        "Image not shown: this note's images used their 20-second decoding budget.";

    private const string Busy =
        "Image not shown: earlier images are still decoding past their 5-second budget.";

    /// <summary>The builder's notice for an image past the projection's
    /// decoded-pixel pool, as much of it as names it.</summary>
    private const string PoolNotice = "over this note's embedded-image budget";

    /// <summary>#1635 (Codex round 2): once the projection's pixel pool is
    /// spent a later SVG never enters the decoder, but one its decoder slots
    /// remember as timed out still says so, not the pool notice. The real
    /// <c>a.svg</c> spends the pool; <c>b.svg</c>, remembered from an earlier
    /// open, meets it spent.</summary>
    [Fact]
    public void ARememberedSvgPastTheSpentPixelPoolStillSaysItTimedOut()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-remembered-past-pool");
        string b = FilledSvg("maroon");
        File.WriteAllText(Path.Combine(fixture.Root, "a.svg"), FilledSvg("navy"));
        File.WriteAllText(Path.Combine(fixture.Root, "b.svg"), b);
        File.WriteAllText(Path.Combine(fixture.Root, "first.md"), "![[b.svg]]\n");
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.svg]]\n");
        var probed = new List<string>();
        try
        {
            StaThread.Run(() =>
            {
                using VaultSession session = Scanned(fixture.Root);
                var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
                var decodes = new ParkedSvgDecodes();
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
                try
                {
                    // The first open times b.svg out at its whole budget, so
                    // these slots remember it.
                    EditorInteractionCoordinator.SvgDecodeBodyForTests = decodes.Parked;
                    EditorInteractionCoordinator.SvgDecodeWaitForTests = decodes.TimeoutOnceEntered;
                    try
                    {
                        using WorkspaceTabViewModel first = ReadingTab(session, "first.md");
                        Assert.Equal(
                            new[] { TookLonger },
                            CardSentences(Assert.IsType<ReadingContentViewModel>(first.Reading).Document!, "b.svg"));
                    }
                    finally
                    {
                        EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
                        EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                        decodes.Release();
                        Assert.True(
                            SpinWait.SpinUntil(() => Volatile.Read(ref slots.Live) == 0, TimeSpan.FromSeconds(10)),
                            "the parked decode never ended");
                    }

                    // An 8x8 SVG rasterises to 64 pixels: the real a.svg spends
                    // the whole pool.
                    ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 64;
                    ReadingContentViewModel.EmbedSvgDecodeProbeForTests = probed.Add;
                    using WorkspaceTabViewModel tab = ReadingTab(session, "host.md");
                    FlowDocument document = Assert.IsType<ReadingContentViewModel>(tab.Reading).Document!;

                    ParkedSvgDecodes.AssertSaid(CardSentences(document, "a.svg", "b.svg"), string.Empty, TookLonger);
                    Assert.Equal(["a.svg"], probed);
                    Assert.Equal(1, decodes.RunsOf(b));
                    Assert.Single(
                        BlocksOf(document.Blocks),
                        block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });
                }
                finally
                {
                    EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
                }
            });
        }
        finally
        {
            ReadingContentViewModel.EmbedSvgDecodeProbeForTests = null;
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = null;
        }
    }

    /// <summary>#1635 (Codex round 2): an SVG met once the projection's pixel
    /// pool and its decode deadline are both spent says the deadline's
    /// sentence, not the pool notice, and never enters the decoder. The first
    /// decode spends both: it reserves the whole pool, then runs out a 100 ms
    /// budget that is the whole 100 ms deadline.</summary>
    [Fact]
    public void AnSvgPastTheSpentPixelPoolAndDeadlineSaysTheDeadline()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-deadline-past-pool");
        string[] svgs = [FilledSvg("olive"), FilledSvg("orange")];
        File.WriteAllText(Path.Combine(fixture.Root, "a.svg"), svgs[0]);
        File.WriteAllText(Path.Combine(fixture.Root, "b.svg"), svgs[1]);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.svg]]\n");
        try
        {
            StaThread.Run(() =>
            {
                using VaultSession session = Scanned(fixture.Root);
                var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
                var decodes = new ParkedSvgDecodes();
                ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 64;
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
                EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = 100;
                EditorInteractionCoordinator.SvgDecodeBodyForTests = decodes.ParkedAfterReserving(64 * 4);
                EditorInteractionCoordinator.SvgDecodeWaitForTests = decodes.TimeoutOnceEntered;
                EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = 100;
                try
                {
                    using WorkspaceTabViewModel tab = ReadingTab(session, "host.md");
                    FlowDocument document = Assert.IsType<ReadingContentViewModel>(tab.Reading).Document!;

                    ParkedSvgDecodes.AssertSaid(
                        CardSentences(document, "a.svg", "b.svg"), TookLonger, ProjectionSpent);
                    Assert.Equal(new[] { 1, 0 }, svgs.Select(svg => decodes.RunsOf(svg)));
                    Assert.Equal(new uint[] { 100 }, decodes.Waits);
                }
                finally
                {
                    EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = null;
                    EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
                    EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                    EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = null;
                    EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
                    decodes.Release();
                    Assert.True(
                        SpinWait.SpinUntil(() => Volatile.Read(ref slots.Live) == 0, TimeSpan.FromSeconds(10)),
                        "the parked decode never ended");
                }
            });
        }
        finally
        {
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = null;
        }
    }

    /// <summary>A tab on <paramref name="path"/>, switched to reading, its
    /// projection built on this thread.</summary>
    private static WorkspaceTabViewModel ReadingTab(VaultSession session, string path)
    {
        var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, path)),
            startInteractionBackgroundWork: false);
        tab.ToggleViewMode();
        return tab;
    }

    /// <summary>#1635 (CD19): a refresh never decodes again an SVG whose
    /// decode ran out its budget. It says the sentence it said the first time,
    /// and the projection is a memo hit, so its card does not flip to the busy
    /// sentence while the abandoned decode runs on. A decode that reserved
    /// pixels before its budget ended rebuilds once, with the same sentence:
    /// the remembered refusal reserves none.</summary>
    [Fact]
    public void ASecondRefreshNeverReDecodesAnSvgThatRanPastItsBudget()
    {
        // The field document's shape (step 0's P6): its reservation comes
        // after 5 seconds, so a decode abandoned at its budget reserved none.
        RefreshTwiceOverATimedOutDecode("reading-svg-embed-remembered", reservesFirst: false);
        RefreshTwiceOverATimedOutDecode("reading-svg-embed-remembered-reserving", reservesFirst: true);
    }

    private static void RefreshTwiceOverATimedOutDecode(string name, bool reservesFirst)
    {
        using FixtureVault fixture = FixtureVault.Create(0, name);
        File.WriteAllText(Path.Combine(fixture.Root, "pic.svg"), Svg);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[pic.svg]]\n");
        StaThread.Run(() =>
        {
            using VaultSession session = Scanned(fixture.Root);
            var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
            var decodes = new ParkedSvgDecodes();
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
            EditorInteractionCoordinator.SvgDecodeBodyForTests =
                reservesFirst ? decodes.ParkedAfterReserving(256) : decodes.Parked;
            EditorInteractionCoordinator.SvgDecodeWaitForTests = decodes.TimeoutOnceEntered;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                FlowDocument opened = reading.Document!;
                Assert.Equal(1, decodes.Runs);
                Assert.Equal(new[] { TookLonger }, CardSentences(opened, "pic.svg"));

                reading.Refresh();
                Assert.Equal(1, decodes.Runs);
                Assert.Equal(new[] { TookLonger }, CardSentences(reading.Document!, "pic.svg"));
                FlowDocument settled = reading.Document!;
                if (!reservesFirst)
                {
                    Assert.Same(opened, settled);
                }

                reading.Refresh();
                Assert.Equal(1, decodes.Runs);
                Assert.Same(settled, reading.Document);
                Assert.Equal(new[] { TookLonger }, CardSentences(reading.Document!, "pic.svg"));
            }
            finally
            {
                EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
                EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
                decodes.Release();
                Assert.True(
                    SpinWait.SpinUntil(() => Volatile.Read(ref slots.Live) == 0, TimeSpan.FromSeconds(10)),
                    "the parked decodes never ended");
            }
        });
    }

    /// <summary>What #1908's fact pinned that still holds: a decode refused
    /// as busy never started, so the next refresh decodes it, and the
    /// decode's outcome is part of the projection's identity, so that refresh
    /// rebuilds the card with the image where an unchanged note alone would
    /// keep the busy sentence.</summary>
    [Fact]
    public void ARefreshAfterABusyRefusalShowsTheImage()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-busy-retry");
        File.WriteAllText(Path.Combine(fixture.Root, "pic.svg"), Svg);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[pic.svg]]\n");
        StaThread.Run(() =>
        {
            using VaultSession session = Scanned(fixture.Root);
            var slots = new EditorInteractionCoordinator.SvgDecodeSlots
            {
                Abandoned = EditorInteractionCoordinator.MaxAbandonedSvgDecodes,
            };
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                FlowDocument opened = reading.Document!;
                Assert.Equal(new[] { Busy }, CardSentences(opened, "pic.svg"));

                // The body reserves nothing, so the two projections differ in
                // the decode's outcome alone.
                Volatile.Write(ref slots.Abandoned, 0);
                EditorInteractionCoordinator.SvgDecodeBodyForTests = (_, _) => TinyPng;
                reading.Refresh();

                FlowDocument document = reading.Document!;
                Assert.NotSame(opened, document);
                Assert.Equal(new[] { string.Empty }, CardSentences(document, "pic.svg"));
                Assert.Single(
                    BlocksOf(document.Blocks),
                    block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });
            }
            finally
            {
                EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
            }
        });
    }

    /// <summary>#1635: a projection whose refresh runs on a worker reads the
    /// process's own deadline there, where no fact's per-thread seam
    /// reaches: with it spent, the card says the projection's sentence and
    /// the SVG never enters the decoder.</summary>
    [Fact]
    public void ABackgroundRefreshReadsTheProcessDeadline()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-process-deadline");
        File.WriteAllText(Path.Combine(fixture.Root, "pic.svg"), Svg);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[pic.svg]]\n");
        EditorInteractionCoordinator.SvgProjectionDecodeDeadlineDefaultMillisecondsForTests = 0;
        try
        {
            StaThread.RunPumped(() =>
            {
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: true);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                Assert.True(
                    PumpedDispatcher.PumpUntil(
                        () => !reading.IsLoading && reading.Document is not null,
                        UnhurriedSvgDecodes.LandingWait()),
                    "the projection never landed");

                Assert.Equal(new[] { ProjectionSpent }, CardSentences(reading.Document!, "pic.svg"));
            });
        }
        finally
        {
            UnhurriedSvgDecodes.LetEveryRealDecodeFinish();
        }
    }

    /// <summary>#1635 (CD19): a reading projection's SVG decodes share a
    /// deadline. Each waits for its own budget or what is left of the
    /// deadline, whichever is shorter; once the deadline is spent the rest
    /// say the projection's sentence without entering the decoder. The next
    /// refresh refuses the remembered one at once and charges it nothing, so
    /// the next image gets its turn.</summary>
    [Fact]
    public void SvgsPastTheProjectionsDecodeDeadlineNeverEnterTheDecoder()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-deadline");
        string[] files = ["a.svg", "b.svg", "c.svg"];
        string[] svgs = [FilledSvg("navy"), FilledSvg("maroon"), FilledSvg("purple")];
        for (int index = 0; index < files.Length; index++)
        {
            File.WriteAllText(Path.Combine(fixture.Root, files[index]), svgs[index]);
        }
        File.WriteAllText(
            Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.svg]]\n\n![[c.svg]]\n");
        StaThread.Run(() =>
        {
            using VaultSession session = Scanned(fixture.Root);
            var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
            var decodes = new ParkedSvgDecodes();
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
            EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = 100;
            EditorInteractionCoordinator.SvgDecodeBodyForTests = decodes.Parked;
            EditorInteractionCoordinator.SvgDecodeWaitForTests = decodes.TimeoutOnceEntered;
            EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = 150;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                FlowDocument opened = reading.Document!;
                ParkedSvgDecodes.AssertSaid(
                    CardSentences(opened, files), TookLonger, ProjectionSpent, ProjectionSpent);
                Assert.Equal(new[] { 1, 1, 0 }, svgs.Select(svg => decodes.RunsOf(svg)));
                Assert.Equal(new uint[] { 100, 50 }, decodes.Waits);

                decodes.Repark();
                Assert.True(
                    SpinWait.SpinUntil(() => Volatile.Read(ref slots.Live) == 0, TimeSpan.FromSeconds(10)),
                    "the parked decodes never ended");
                reading.Refresh();

                ParkedSvgDecodes.AssertSaid(
                    CardSentences(reading.Document!, files), TookLonger, TookLonger, ProjectionSpent);
                Assert.Equal(new[] { 1, 2, 1 }, svgs.Select(svg => decodes.RunsOf(svg)));
                Assert.Equal(new uint[] { 100, 50, 100, 50 }, decodes.Waits);
                Assert.NotSame(opened, reading.Document);
            }
            finally
            {
                EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = null;
                EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
                EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = null;
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
                decodes.Release();
                Assert.True(
                    SpinWait.SpinUntil(() => Volatile.Read(ref slots.Live) == 0, TimeSpan.FromSeconds(10)),
                    "the parked decodes never ended");
            }
        });
    }

    /// <summary>The 8x8 test SVG in a fill of its own, so its bytes are its
    /// own (#1635 remembers a decode by its bytes).</summary>
    private static string FilledSvg(string fill) =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'>"
        + $"<rect width='8' height='8' fill='{fill}'/></svg>";

    /// <summary>What each card says of the decode sentences, in the order of
    /// <paramref name="files"/>, which is the cards' order: a card runs from
    /// its file's name to the next card's.</summary>
    private static string[] CardSentences(FlowDocument document, params string[] files)
    {
        string text = TextOf(document);
        string[] sentences = [TookLonger, ProjectionSpent, Busy, PoolNotice];
        var said = new string[files.Length];
        int start = 0;
        for (int index = 0; index < files.Length; index++)
        {
            start = text.IndexOf(files[index], start, StringComparison.Ordinal);
            Assert.True(start >= 0, $"no card names {files[index]}: {text}");
            int end = index + 1 < files.Length
                ? text.IndexOf(files[index + 1], start, StringComparison.Ordinal)
                : text.Length;
            Assert.True(end > start, $"the cards are out of order: {text}");
            string card = text[start..end];
            said[index] = string.Join(
                " | ", sentences.Where(sentence => card.Contains(sentence, StringComparison.Ordinal)));
        }
        return said;
    }

    /// <summary>The decoded-pixel pool bounds the worker's SVG decodes, as it
    /// bounded the builder's (W3-5 round 5): each decode reserves its pixels
    /// before its raster is allocated, and once the pool is spent a later
    /// distinct SVG never enters the decoder and keeps its header with the
    /// budget notice (Codex on #1634: decoding every SVG up front let a note
    /// of many small SVGs hold ten times the pool).</summary>
    [Fact]
    public void DistinctSvgsPastThePixelPoolNeverEnterTheDecoder()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-pool");
        foreach (string name in (string[])["a", "b", "c"])
        {
            File.WriteAllText(Path.Combine(fixture.Root, $"{name}.svg"), Svg);
        }
        File.WriteAllText(
            Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.svg]]\n\n![[c.svg]]\n");
        var decoded = new List<string>();
        ReadingContentViewModel.EmbedSvgDecodeProbeForTests = decoded.Add;
        // An 8x8 SVG rasterises to 64 pixels: the pool admits the first only.
        ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 64;
        try
        {
            StaThread.Run(() =>
            {
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                FlowDocument document = reading.Document!;

                Assert.Equal(["a.svg"], decoded);
                Assert.Single(
                    BlocksOf(document.Blocks),
                    block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });
                Assert.Equal(
                    2,
                    System.Text.RegularExpressions.Regex.Matches(
                        TextOf(document), "over this note's embedded-image budget").Count);
            });
        }
        finally
        {
            ReadingContentViewModel.EmbedSvgDecodeProbeForTests = null;
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = null;
        }
    }

    /// <summary>SVGs and rasters share one decoded-pixel budget (Codex round
    /// 2 on #1634): what the worker's SVG decodes reserved is spent before
    /// the build decodes a raster, so a raster past the shared pool never
    /// enters the decoder, where two pools let a mixed note hold twice the
    /// budget.</summary>
    [Fact]
    public void SvgsAndRastersShareOneDecodedPixelPool()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-shared-pool");
        File.WriteAllText(Path.Combine(fixture.Root, "a.svg"), Svg);
        File.WriteAllBytes(Path.Combine(fixture.Root, "b.png"), TinyPng);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.png]]\n");
        var rasters = new List<string>();
        ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = rasters.Add;
        // The 8x8 SVG spends the whole 64-pixel pool on the worker.
        ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 64;
        try
        {
            StaThread.Run(() =>
            {
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                FlowDocument document = Assert.IsType<ReadingContentViewModel>(tab.Reading).Document!;

                Assert.Empty(rasters);
                Assert.Single(
                    BlocksOf(document.Blocks),
                    block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });
                Assert.Contains("over this note's embedded-image budget", TextOf(document), StringComparison.Ordinal);
            });
        }
        finally
        {
            ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = null;
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = null;
        }
    }

    /// <summary>Only an SVG the publish will render decodes on the worker
    /// (Codex round 3 on #1634): one past the block ceiling neither decodes
    /// nor spends the pool, so a shown raster before it keeps its image.</summary>
    [Fact]
    public void AnSvgPastTheRenderCeilingNeitherDecodesNorSpendsThePool()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-ceiling");
        File.WriteAllText(Path.Combine(fixture.Root, "a.svg"), Svg);
        File.WriteAllBytes(Path.Combine(fixture.Root, "b.png"), TinyPng);
        var note = new System.Text.StringBuilder("![[b.png]]\n\n");
        for (int index = 0; index < ReadingContentViewModel.MaximumRenderedBlocks + 50; index++)
        {
            note.Append("Line ").Append(index).Append(".\n\n");
        }
        note.Append("![[a.svg]]\n");
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), note.ToString());
        var decoded = new List<string>();
        ReadingContentViewModel.EmbedSvgDecodeProbeForTests = decoded.Add;
        // The SVG would spend this whole pool; the 1x1 PNG needs one pixel.
        ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 64;
        try
        {
            StaThread.Run(() =>
            {
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                FlowDocument document = Assert.IsType<ReadingContentViewModel>(tab.Reading).Document!;

                Assert.Empty(decoded);
                Assert.Single(
                    BlocksOf(document.Blocks),
                    block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });
            });
        }
        finally
        {
            ReadingContentViewModel.EmbedSvgDecodeProbeForTests = null;
            ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = null;
        }
    }

    /// <summary>What an SVG decode reserved is part of the projection's
    /// identity (Codex round 3 on #1634): the same failed decode, once after
    /// its reservation and once before, leaves the rasters different pools,
    /// so the refresh rebuilds and the raster the second pool admits shows.</summary>
    [Fact]
    public void ARefreshWhoseSvgReservedLessShowsTheRasterItNowAdmits()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-reserved");
        File.WriteAllText(Path.Combine(fixture.Root, "a.svg"), Svg);
        File.WriteAllBytes(Path.Combine(fixture.Root, "b.png"), TinyPng);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.png]]\n");
        ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = 64;
        bool reserveFirst = true;
        StaThread.Run(() =>
        {
            // Both decodes fail; the first after reserving the whole pool.
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = new EditorInteractionCoordinator.SvgDecodeSlots();
            EditorInteractionCoordinator.SvgDecodeBodyForTests = (bytes, reserve) =>
            {
                if (reserveFirst)
                {
                    bool admitted = reserve(64 * 4);
                    Assert.True(admitted, "the first decode's reservation was refused");
                }
                return null;
            };
            try
            {
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                Assert.DoesNotContain(
                    BlocksOf(reading.Document!.Blocks),
                    block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });

                reserveFirst = false;
                reading.Refresh();

                Assert.Single(
                    BlocksOf(reading.Document!.Blocks),
                    block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } });
            }
            finally
            {
                EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
                ReadingDocumentBuilder.ProjectionEmbedDecodedPixelBudgetOverrideForTests = null;
            }
        });
    }

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8"
        + "z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static string TextOf(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text;

    private static IEnumerable<Block> BlocksOf(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            yield return block;
            if (block is Section section)
            {
                foreach (Block inner in BlocksOf(section.Blocks))
                {
                    yield return inner;
                }
            }
        }
    }

    private static VaultSession Scanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
