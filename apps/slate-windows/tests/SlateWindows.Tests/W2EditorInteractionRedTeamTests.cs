// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W2EditorInteractionRedTeamTests
{
    [Fact]
    public void ExactCoreRanges_DisambiguateCommentsRightEdgesAndCheckboxPointerHits()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var navigation = new List<EditorNavigationRequest>();
        using var tab = OpenSourceTab(session, navigation.Add);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        string link = "[[target#Destination]]";
        int linkStart = tab.Text.IndexOf(link, StringComparison.Ordinal);
        int linkEnd = linkStart + link.Length;

        Assert.True(interactions.ActivateAt(
            linkStart + 2,
            EditorInteractionOrigin.Pointer));
        Assert.Single(navigation);

        Assert.True(interactions.ActivateAt(
            linkEnd,
            EditorInteractionOrigin.Keyboard));
        Assert.Equal(2, navigation.Count);

        Assert.False(interactions.ActivateAt(
            linkEnd,
            EditorInteractionOrigin.Pointer));
        Assert.Equal(2, navigation.Count);

        int prose = tab.Text.IndexOf("task prose", StringComparison.Ordinal) + 2;
        Assert.False(interactions.ActivateAt(
            prose,
            EditorInteractionOrigin.Pointer));
        Assert.Contains("- [ ] task prose", tab.Text, StringComparison.Ordinal);

        int checkbox = tab.Text.IndexOf("[ ]", StringComparison.Ordinal) + 1;
        Assert.True(interactions.ActivateAt(
            checkbox,
            EditorInteractionOrigin.Pointer));
        WaitForUi(() => tab.Text.Contains("- [x] task prose", StringComparison.Ordinal));
        Assert.Contains("- [x] task prose", tab.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbedPreview_ExpandsNestedContentAndExplainsCorruptImages()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var tab = OpenSourceTab(session);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        string sectionEmbed = "![[target#Destination]]";
        int rightEdge = tab.Text.IndexOf(sectionEmbed, StringComparison.Ordinal)
            + sectionEmbed.Length;

        Assert.True(interactions.PreviewEmbedAt(rightEdge));
        WaitForUi(() => !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        string rendered = EmbedText(interactions.PopoverEmbedRoot!);
        Assert.Contains("Section body", rendered, StringComparison.Ordinal);
        Assert.Contains("Nested leaf", rendered, StringComparison.Ordinal);
        interactions.ClosePopoverCommand.Execute(null);

        Assert.True(interactions.PreviewEmbedAt(Inside(tab.Text, "![[broken.png]]")));
        WaitForUi(() => !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        Assert.Null(interactions.PopoverImage);
        Assert.Contains(
            "Could not decode image",
            EmbedText(interactions.PopoverEmbedRoot!),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SvgPreview_UsesSecureStaticPolicyAndRejectsExternalResources()
    {
        foreach (string resource in new[]
        {
            "https://example.invalid/pixel.png",
            "file:///C:/private/secret.png",
            "relative.png",
        })
        {
            Assert.False(
                EditorInteractionCoordinator.SecureSvgAllowsResourceForTests(resource));
        }

        string[] rejected =
        [
            "<!DOCTYPE svg [<!ENTITY xxe SYSTEM 'file:///C:/private/secret'>]><svg xmlns='http://www.w3.org/2000/svg'>&xxe;</svg>",
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><image href='https://example.invalid/pixel.png'/></svg>",
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><image href='file:///C:/private/secret.png'/></svg>",
            "<?xml-stylesheet href='https://example.invalid/style.css'?><svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'/>",
        ];
        foreach (string source in rejected)
        {
            Assert.Null(EditorInteractionCoordinator.DecodeImage(
                Encoding.UTF8.GetBytes(source),
                "image/svg+xml",
                out string? reason));
            Assert.Null(reason);
        }

        const string safe =
            "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='8' height='8' fill='red'/></svg>";
        Assert.True(EditorInteractionCoordinator.SecureSvgParsesForTests(safe));
    }

    /// <summary>The SVG decode runs on its own thread. If the wait for it
    /// ever failed, the caller would otherwise read the results while the
    /// decode still runs: the failure is logged, and the image is still the
    /// finished one.</summary>
    [Fact]
    public void SvgPreview_ReadsTheDecodeOnlyOnceItEnds_EvenIfTheWaitFails()
    {
        const uint waitFailed = 0xFFFFFFFF;
        var shapes = new StringBuilder();
        for (int i = 0; i < 2000; i++)
        {
            shapes.Append($"<rect x='{i % 64}' y='{i / 64}' width='1' height='1' fill='red'/>");
        }
        string svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'>{shapes}</svg>";
        var log = new StringWriter();
        TextWriter original = Console.Error;
        EditorInteractionCoordinator.SvgDecodeWaitForTests = _ => waitFailed;
        try
        {
            Console.SetError(log);
            Assert.NotNull(EditorInteractionCoordinator.DecodeImage(
                Encoding.UTF8.GetBytes(svg), "image/svg+xml"));
        }
        finally
        {
            EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
            Console.SetError(original);
        }
        Assert.Contains(
            $"SlateWindows.{HostDiagnosticEvent.SvgDecodeWaitFailed}",
            log.ToString(),
            StringComparison.Ordinal);
    }

    private const string SmallSvg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><rect width='8' height='8' fill='red'/></svg>";

    /// <summary>#1581 (plan 45 XB-10b): a decode past its budget is no
    /// longer waited on. The preview gets core's sentence at once, the decode
    /// runs on to its end, and its slots are released when it does.</summary>
    [Fact]
    public void SvgPreview_PastTheDecodeBudgetShowsCoresSentenceAndLeavesTheDecoderRunning()
    {
        using var release = new ManualResetEventSlim(false);
        using var lines = new DiagnosticLines();
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        int finished = 0;
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: 100,
            body: (_, _) =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                Interlocked.Increment(ref finished);
                return null;
            },
            () =>
            {
                var clock = Stopwatch.StartNew();
                Assert.Null(EditorInteractionCoordinator.DecodeImage(
                    Encoding.UTF8.GetBytes(SmallSvg), "image/svg+xml", out string? reason));
                Assert.True(
                    clock.ElapsedMilliseconds < 2_000,
                    $"the decode was waited on for {clock.ElapsedMilliseconds} ms");
                Assert.Equal("Image not shown: decoding took longer than 5 seconds.", reason);
                Assert.Equal(1, Volatile.Read(ref slots.Abandoned));
                Assert.Equal(1, Volatile.Read(ref slots.Live));
                Assert.Equal(0, Volatile.Read(ref finished));
            });
        Assert.Contains(
            lines.Snapshot,
            line => line.StartsWith(
                $"SlateWindows.{HostDiagnosticEvent.SvgDecodeAbandoned} (bytes=",
                StringComparison.Ordinal));
        release.Set();
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
        Assert.Equal(0, Volatile.Read(ref slots.Abandoned));
        Assert.Equal(1, Volatile.Read(ref finished));
    }

    /// <summary>#1581: the editor's embed preview shows core's sentence as a
    /// warning in place of an image, and an ordinary failure keeps the
    /// editor's own sentence.</summary>
    [Fact]
    public void SvgPreview_TheEditorEmbedShowsCoresSentenceAsAWarning()
    {
        using var release = new ManualResetEventSlim(false);
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: 100,
            body: (_, _) =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                return null;
            },
            () =>
            {
                EditorEmbedPreviewNode node = EditorInteractionCoordinator.BuildEmbedPreviewNode(
                    new EmbedResolution.Image(
                        "slow.svg", Encoding.UTF8.GetBytes(SmallSvg), "image/svg+xml", null));
                Assert.Null(node.Image);
                Assert.True(node.IsWarning);
                Assert.Equal(
                    EditorInteractionCoordinator.SvgDecodeTimeExceededMessage,
                    Assert.Single(node.Parts).Text);
            });
        release.Set();
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);

        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: null,
            body: null,
            () =>
            {
                EditorEmbedPreviewNode corrupt = EditorInteractionCoordinator.BuildEmbedPreviewNode(
                    new EmbedResolution.Image(
                        "broken.svg",
                        Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><unclosed"),
                        "image/svg+xml",
                        null));
                Assert.True(corrupt.IsWarning);
                Assert.Equal(
                    "Could not decode image. MIME: image/svg+xml. "
                    + "The file may be corrupt or use an unsupported codec.",
                    Assert.Single(corrupt.Parts).Text);
            });
    }

    /// <summary>#1581: while two abandoned decodes run, a new decode is
    /// refused with core's busy sentence (it never started, so it is not told
    /// it took too long) and starts no decoder; once they end, decoding works
    /// again.</summary>
    [Fact]
    public void SvgPreview_RefusesANewDecodeWhileTwoAreAbandoned()
    {
        using var release = new ManualResetEventSlim(false);
        using var lines = new DiagnosticLines();
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        int bodyRuns = 0;
        byte[] svg = Encoding.UTF8.GetBytes(SmallSvg);
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: 100,
            body: (_, _) =>
            {
                Interlocked.Increment(ref bodyRuns);
                release.Wait(TimeSpan.FromSeconds(10));
                return null;
            },
            () =>
            {
                Assert.Null(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out _));
                Assert.Null(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out _));
                WaitUntil(() => Volatile.Read(ref bodyRuns) == 2);
                Assert.Equal(
                    EditorInteractionCoordinator.MaxAbandonedSvgDecodes,
                    Volatile.Read(ref slots.Abandoned));

                Assert.Null(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out string? refused));
                Assert.Equal(
                    "Image not shown: earlier images are still decoding past their 5-second budget.",
                    refused);
                Assert.Equal(EditorInteractionCoordinator.SvgDecodeBusyMessage, refused);
                Assert.Equal(2, Volatile.Read(ref bodyRuns));
            });
        Assert.Contains(
            lines.Snapshot,
            line => line == $"SlateWindows.{HostDiagnosticEvent.SvgDecodeRefusedAtCap}");

        release.Set();
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: null,
            body: null,
            () => Assert.NotNull(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out _)));
    }

    /// <summary>#1581 (the plan 45 XB-10b review): decodes waited on from
    /// several threads at once count against a hard ceiling, so callers that
    /// all pass the abandoned cap before any is abandoned cannot pile up
    /// threads; a decode past the ceiling never starts.</summary>
    [Fact]
    public void SvgPreview_RefusesANewDecodeAtTheDecoderCeiling()
    {
        using var release = new ManualResetEventSlim(false);
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        int started = 0;
        byte[] svg = Encoding.UTF8.GetBytes(SmallSvg);
        Thread[] callers = Enumerable.Range(0, EditorInteractionCoordinator.MaxLiveSvgDecodes)
            .Select(index => new Thread(() => WithSvgDecodeSeams(
                slots,
                budgetMilliseconds: 10_000,
                body: (_, _) =>
                {
                    Interlocked.Increment(ref started);
                    release.Wait(TimeSpan.FromSeconds(10));
                    return null;
                },
                () => EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out _)))
            {
                IsBackground = true,
            })
            .ToArray();
        foreach (Thread caller in callers)
        {
            caller.Start();
        }
        try
        {
            WaitUntil(() => Volatile.Read(ref started) == EditorInteractionCoordinator.MaxLiveSvgDecodes);
            WithSvgDecodeSeams(
                slots,
                budgetMilliseconds: 100,
                body: (_, _) => throw new InvalidOperationException("no decode starts past the ceiling"),
                () =>
                {
                    Assert.Null(EditorInteractionCoordinator.DecodeImage(svg, "image/svg+xml", out string? refused));
                    Assert.Equal(EditorInteractionCoordinator.SvgDecodeBusyMessage, refused);
                });
            // None was abandoned: their callers are still waiting.
            Assert.Equal(0, Volatile.Read(ref slots.Abandoned));
        }
        finally
        {
            release.Set();
            foreach (Thread caller in callers)
            {
                Assert.True(caller.Join(TimeSpan.FromSeconds(10)));
            }
        }
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
    }

    /// <summary>#1581: an abandoned decode that throws still releases its
    /// slots, and nothing is rethrown on the thread that stopped waiting.</summary>
    [Fact]
    public void SvgPreview_AnAbandonedDecodeThatThrowsStillReleasesItsSlot()
    {
        using var release = new ManualResetEventSlim(false);
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: 100,
            body: (_, _) =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                throw new InvalidOperationException("the parked decode fails");
            },
            () =>
            {
                Assert.Null(EditorInteractionCoordinator.DecodeImage(
                    Encoding.UTF8.GetBytes(SmallSvg), "image/svg+xml", out string? reason));
                Assert.Equal(EditorInteractionCoordinator.SvgDecodeTimeExceededMessage, reason);
                Assert.Equal(1, Volatile.Read(ref slots.Abandoned));
            });
        release.Set();
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
        Assert.Equal(0, Volatile.Read(ref slots.Abandoned));
    }

    /// <summary>#1581 (the plan 45 XB-10b review): an abandoned decode cannot
    /// spend its caller's budget. The gate refuses once the caller has
    /// stopped waiting, and the caller's callback is never called.</summary>
    [Fact]
    public void SvgPreview_AnAbandonedDecodeCannotSpendItsCallersBudget()
    {
        using var release = new ManualResetEventSlim(false);
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        int charges = 0;
        int reservedAfterAbandon = -1;
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: 100,
            body: (_, reserve) =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                Volatile.Write(ref reservedAfterAbandon, reserve(64) ? 1 : 0);
                return null;
            },
            () =>
            {
                Assert.Null(EditorInteractionCoordinator.DecodeImage(
                    Encoding.UTF8.GetBytes(SmallSvg),
                    "image/svg+xml",
                    _ =>
                    {
                        Interlocked.Increment(ref charges);
                        return true;
                    },
                    out _,
                    out string? reason));
                Assert.Equal(EditorInteractionCoordinator.SvgDecodeTimeExceededMessage, reason);
            });
        release.Set();
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
        Assert.Equal(0, Volatile.Read(ref reservedAfterAbandon));
        Assert.Equal(0, Volatile.Read(ref charges));
    }

    /// <summary>#1581 (the plan 45 XB-10b review): a budget that ends while
    /// the decoder is inside the caller's callback waits that callback out
    /// before abandoning, so the callback never runs after the caller has
    /// returned.</summary>
    [Fact]
    public void SvgPreview_TheBudgetEndsOnlyAfterACallbackInProgressReturns()
    {
        using var inCallback = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        int callbackReturned = 0;
        // The wait ends its budget only once the decoder is in the callback.
        EditorInteractionCoordinator.SvgDecodeWaitForTests = _ =>
        {
            inCallback.Wait(TimeSpan.FromSeconds(10));
            return 0x102;
        };
        try
        {
            WithSvgDecodeSeams(
                slots,
                budgetMilliseconds: null,
                body: (_, reserve) =>
                {
                    reserve(64);
                    // Parked after the callback, so the caller's abandon,
                    // not the decode's end, follows the callback.
                    release.Wait(TimeSpan.FromSeconds(10));
                    return null;
                },
                () =>
                {
                    Assert.Null(EditorInteractionCoordinator.DecodeImage(
                        Encoding.UTF8.GetBytes(SmallSvg),
                        "image/svg+xml",
                        _ =>
                        {
                            inCallback.Set();
                            Thread.Sleep(300);
                            Volatile.Write(ref callbackReturned, 1);
                            return true;
                        },
                        out _,
                        out string? reason));
                    Assert.Equal(1, Volatile.Read(ref callbackReturned));
                    Assert.Equal(EditorInteractionCoordinator.SvgDecodeTimeExceededMessage, reason);
                });
        }
        finally
        {
            EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
            release.Set();
        }
        WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
    }

    /// <summary>#1581 (the plan 45 XB-10b review): a decode that finishes
    /// just as its budget ends is read as an ordinary end, its image
    /// returned and no slot kept.</summary>
    [Fact]
    public void SvgPreview_ADecodeThatFinishesAsTheBudgetEndsIsReadAsAnOrdinaryEnd()
    {
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        // The budget "ends" only after the decode has finished entirely.
        EditorInteractionCoordinator.SvgDecodeWaitForTests = handle =>
        {
            Assert.Equal(0u, WaitForSingleObject(handle, 10_000));
            WaitUntil(() => Volatile.Read(ref slots.Live) == 0);
            return 0x102;
        };
        try
        {
            WithSvgDecodeSeams(
                slots,
                budgetMilliseconds: null,
                body: null,
                () =>
                {
                    Assert.NotNull(EditorInteractionCoordinator.DecodeImage(
                        Encoding.UTF8.GetBytes(SmallSvg), "image/svg+xml", out string? reason));
                    Assert.Null(reason);
                });
        }
        finally
        {
            EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
        }
        Assert.Equal(0, Volatile.Read(ref slots.Abandoned));
    }

    /// <summary>#1581: an ordinary decode failure carries no reason, so the
    /// callers keep their own "Could not decode image" sentence.</summary>
    [Fact]
    public void SvgPreview_AnOrdinaryDecodeFailureKeepsTheCorruptImageSentence()
    {
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        WithSvgDecodeSeams(
            slots,
            budgetMilliseconds: null,
            body: null,
            () =>
            {
                Assert.Null(EditorInteractionCoordinator.DecodeImage(
                    Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><unclosed"),
                    "image/svg+xml",
                    out string? reason));
                Assert.Null(reason);
            });
        Assert.Equal(0, Volatile.Read(ref slots.Abandoned));
    }

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);

    /// <summary>Runs <paramref name="test"/> with this thread's decode seams
    /// set: its own decoder slots, a budget, a decode body.</summary>
    private static void WithSvgDecodeSeams(
        EditorInteractionCoordinator.SvgDecodeSlots slots,
        int? budgetMilliseconds,
        Func<byte[], Func<long, bool>, byte[]?>? body,
        Action test)
    {
        EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
        EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = budgetMilliseconds;
        EditorInteractionCoordinator.SvgDecodeBodyForTests = body;
        try
        {
            test();
        }
        finally
        {
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
            EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = null;
            EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
        }
    }

    private static void WaitUntil(Func<bool> condition) =>
        Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10)), "timed out");

    /// <summary>Every host diagnostic written while alive, from any thread,
    /// chained to whatever observer was installed before.</summary>
    private sealed class DiagnosticLines : IDisposable
    {
        private readonly Action<string>? _previous = HostLog.ObserverForTests;
        private readonly ConcurrentQueue<string> _lines = new();

        public DiagnosticLines() =>
            HostLog.ObserverForTests = line =>
            {
                _previous?.Invoke(line);
                _lines.Enqueue(line);
            };

        public string[] Snapshot => [.. _lines];

        public void Dispose() => HostLog.ObserverForTests = _previous;
    }

    [Fact]
    public void ExternallyReindexedSnapshot_FailsClosedUntilEditorReloads()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var navigation = new List<EditorNavigationRequest>();
        var announcements = new List<A11yEvent>();
        using var tab = OpenSourceTab(session, navigation.Add, announcements.Add);
        string before = File.ReadAllText(fixture.SourcePath);

        File.WriteAllText(fixture.SourcePath, before + "\nExternally changed.\n");
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        tab.EditorInteractions!.RefreshArtifactCacheForTests();

        Assert.True(tab.EditorInteractions.ActivateAt(
            Inside(tab.Text, "[[target#Destination]]")));
        Assert.Empty(navigation);
        Assert.Contains(
            announcements,
            item => item is A11yEvent.HostComposed composed
                && composed.Text.Contains("Reload source.md", StringComparison.Ordinal));

        int checkbox = tab.Text.IndexOf("[ ]", StringComparison.Ordinal) + 1;
        Assert.True(tab.EditorInteractions.ActivateAt(
            checkbox,
            EditorInteractionOrigin.Pointer));
        Assert.Equal(before + "\nExternally changed.\n", File.ReadAllText(fixture.SourcePath));
    }

    [Fact]
    public void TabAndGroupDeactivation_CloseTransientPopovers()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            _ => { });
        workspace.OpenPath("source.md");
        WorkspaceGroupViewModel originalGroup = workspace.ActiveGroup;
        WorkspaceTabViewModel source = originalGroup.ActiveTab!;
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();

        Assert.True(source.EditorInteractions.PreviewEmbedAt(
            Inside(source.Text, "![[target#Destination]]")));
        WaitForUi(() => !source.EditorInteractions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        workspace.OpenPath("target.md", WorkspaceOpenTarget.NewTab);
        Assert.False(source.EditorInteractions.IsPopoverOpen);

        originalGroup.ActiveTab = source;
        Assert.True(source.EditorInteractions.PreviewEmbedAt(
            Inside(source.Text, "![[target#Destination]]")));
        WaitForUi(() => !source.EditorInteractions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal));
        workspace.OpenPath("target.md", WorkspaceOpenTarget.SplitRight);
        Assert.False(source.EditorInteractions.IsPopoverOpen);
    }

    /// <summary>#1152: hover reuses the math classification while the
    /// window revision is unchanged, and an edit — which changes it — makes
    /// the NEXT hover refresh (asynchronously, through the worker) rather
    /// than reuse; once that refresh lands, hovering reuses again. Every
    /// sample waits for the worker to be idle AND published first: the
    /// earlier shape asserted the count immediately after a hover that had
    /// just queued a zero-delay refresh, i.e. it asserted the assertion
    /// would win a race against <c>Task.Run</c> — which it lost on a loaded
    /// CI runner.</summary>
    [Fact]
    public void RepeatedHover_ReusesMathClassificationUntilTheWindowRevisionChanges()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var tab = OpenSourceTab(session);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        int citation = Inside(tab.Text, "[@doe]");

        WaitForMathRefreshToSettle(interactions);
        long baseline = interactions.MathRangeRefreshCountForTests;
        interactions.HoverAt(citation);
        WaitForMathRefreshToSettle(interactions);
        Assert.Equal(baseline, interactions.MathRangeRefreshCountForTests);
        interactions.HoverAt(citation);
        WaitForMathRefreshToSettle(interactions);
        Assert.Equal(baseline, interactions.MathRangeRefreshCountForTests);

        // The edit changes the window revision: the next hover refreshes
        // (its zero-delay queue supersedes the edit's debounced one), and
        // only after that lands is the classification reused again.
        tab.Text += "\nEdit invalidates the canonical window.\n";
        interactions.HoverAt(citation);
        WaitForMathRefreshToSettle(interactions);
        long refreshed = interactions.MathRangeRefreshCountForTests;
        Assert.True(
            refreshed >= baseline + 1,
            $"a window-revision change must refresh: {refreshed} after baseline {baseline}");
        interactions.HoverAt(citation);
        WaitForMathRefreshToSettle(interactions);
        Assert.Equal(refreshed, interactions.MathRangeRefreshCountForTests);
    }

    /// <summary>Pumps the dispatcher until the math refresh worker is idle
    /// AND its publication has landed for the current revision — the
    /// premise every <c>MathRangeRefreshCountForTests</c> sample needs.</summary>
    private static void WaitForMathRefreshToSettle(EditorInteractionCoordinator interactions) =>
        WaitForUi(() => interactions.MathRefreshIdleForTests && interactions.MathRangesCurrentForTests);

    [Fact]
    public void CitationHover_DoesNotRequestFocusAndMathClassificationKeepsWholeDocumentContext()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var tab = OpenSourceTab(session);
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        int popoverFocusRequests = 0;
        int editorFocusRequests = 0;
        interactions.PopoverFocusRequested += (_, _) => popoverFocusRequests++;
        interactions.FocusRequested += (_, _) => editorFocusRequests++;

        interactions.HoverAt(Inside(tab.Text, "[@doe]"));
        Assert.True(interactions.IsPopoverOpen);
        Assert.Equal(0, popoverFocusRequests);

        interactions.HoverAt(0);
        Assert.True(interactions.IsPopoverOpen);
        interactions.IsPopoverOpen = false;
        Assert.Equal(0, editorFocusRequests);

        Assert.True(interactions.ActivateAt(Inside(tab.Text, "[@doe]")));
        Assert.Equal(1, popoverFocusRequests);

        Assert.False(interactions.ActivateAt(Inside(tab.Text, "#not-a-tag-in-spaced-math")));
        Assert.False(interactions.ActivateAt(Inside(tab.Text, "[[target]] in spaced math")));
        Assert.True(interactions.MathRangeRefreshCountForTests >= 1);
    }

    [Fact]
    public void DuplicateTab_RetainsWorkspaceInteractionDependencies()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var announcements = new List<A11yEvent>();
        var tags = new List<string>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add);
        workspace.EditorTagActivated += (_, tag) => tags.Add(tag);
        workspace.OpenPath("source.md");

        workspace.DuplicateTabCommand.Execute(null);
        WorkspaceTabViewModel duplicate = workspace.ActiveGroup.ActiveTab!;
        duplicate.EditorInteractions!.RefreshMathRangesForTests();
        duplicate.EditorInteractions.RefreshArtifactCacheForTests();
        Assert.Same(workspace.EditorPreferences, duplicate.EditorPreferences);

        workspace.EditorPreferences.ToggleSpellCheckCommand.Execute(null);
        Assert.True(duplicate.EditorPreferences.IsSpellCheckEnabled);
        Assert.True(duplicate.EditorInteractions!.ActivateAt(Inside(duplicate.Text, "#project")));
        Assert.Equal(["project"], tags);

        Assert.True(duplicate.EditorInteractions.ActivateAt(
            Inside(duplicate.Text, "[[target#Destination]]")));
        Assert.Equal("target.md", workspace.ActiveGroup.ActiveTab!.Path);
        Assert.Contains(announcements, item => item is A11yEvent.InternalNavigated);
    }

    [Fact]
    public void DuplicateHeadingNavigation_PrefersTheExactCoreAnchorId()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            _ => { });
        workspace.OpenPath("source.md");
        WorkspaceTabViewModel source = workspace.ActiveGroup.ActiveTab!;
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();

        Assert.True(source.EditorInteractions.ActivateAt(
            Inside(source.Text, "[[target#destination-2]]")));

        WorkspaceTabViewModel target = workspace.ActiveGroup.ActiveTab!;
        Assert.Equal("target.md", target.Path);
        int expected = target.Text.LastIndexOf("## Destination", StringComparison.Ordinal);
        WaitForUi(() => target.EditorCaretOffset == expected);
    }

    [Fact]
    public void AsyncAnchorNavigation_DropsLateResultsAfterCaretMoveOrDeactivation()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var announcements = new List<A11yEvent>();
        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "target.md")),
            announce: announcements.Add,
            startInteractionBackgroundWork: false,
            anchorResolver: (source, kind, anchor) =>
            {
                entered.Set();
                Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
                return SlateUniffiMethods.LinkAnchorByteOffset(source, kind, anchor);
            });

        Assert.True(tab.NavigateToAnchor(
            new LinkAnchor("heading", "Destination"),
            null,
            announcements.Add));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        tab.EditorCaretOffset = 1;
        release.Set();
        WaitForUi(() => tab.AnchorNavigationPublishCountForTests == 1);
        Assert.Equal(1, tab.EditorCaretOffset);
        Assert.Empty(announcements);

        entered.Reset();
        release.Reset();
        bool isActive = true;
        Assert.True(tab.NavigateToAnchor(
            new LinkAnchor("heading", "Destination"),
            null,
            announcements.Add,
            () => isActive));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
        isActive = false;
        release.Set();
        WaitForUi(() => tab.AnchorNavigationPublishCountForTests == 2);
        Assert.Equal(1, tab.EditorCaretOffset);
        Assert.Empty(announcements);
    }

    [Fact]
    public void MissingBlockNavigation_FocusesTargetAndUsesBlockSpecificCopy()
    {
        using RedTeamFixture fixture = RedTeamFixture.Create();
        using VaultSession session = OpenScanned(fixture.Root);
        var announcements = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session,
            fixture.Root,
            () => [],
            announcements.Add,
            startInteractionBackgroundWork: false);
        int focusRequests = 0;
        workspace.EditorPaneFocusRequested += (_, _) => focusRequests++;
        workspace.OpenPath("source.md");
        WorkspaceTabViewModel source = workspace.ActiveGroup.ActiveTab!;
        source.EditorInteractions!.RefreshMathRangesForTests();
        source.EditorInteractions.RefreshArtifactCacheForTests();
        focusRequests = 0;

        Assert.True(source.EditorInteractions.ActivateAt(
            Inside(source.Text, "[[target^missing-block]]")));
        Assert.Equal("target.md", workspace.ActiveGroup.ActiveTab!.Path);
        Assert.Equal(1, focusRequests);
        WaitForUi(() => announcements.Any(item =>
            item is A11yEvent.HostComposed composed
                && composed.Text.Contains(
                    "Block missing-block was not found",
                    StringComparison.Ordinal)));
    }
    [Fact]
    public void SpellingPipeline_IsOptInBoundedAndClearsWhenDisabled()
    {
        RunOnSta(() =>
        {
            var service = new FakeEditorSpellingService();
            using var preferences = new EditorPreferencesViewModel(
                _ => { },
                service);
            var editor = new SlateTextEditor
            {
                Text = "mispel is deliberately wrong",
            };
            using var spelling = new AvalonSpellingCoordinator(editor, preferences);

            spelling.RefreshForTests();
            Assert.Equal(0, spelling.ErrorCountForTests);
            Assert.Null(service.LastCheckedText);

            preferences.ToggleSpellCheckCommand.Execute(null);
            spelling.RefreshForTests();
            Assert.Equal(1, spelling.ErrorCountForTests);
            Assert.Equal(editor.Text, service.LastCheckedText);

            preferences.ToggleSpellCheckCommand.Execute(null);
            spelling.RefreshForTests();
            Assert.Equal(0, spelling.ErrorCountForTests);
        });
    }

    [Fact]
    public void PointerHandler_SetsSourceCaretBeforeSynchronousNavigation()
    {
        string source = File.ReadAllText(RepoFile(
            "apps",
            "slate-windows",
            "src",
            "SlateWindows",
            "SlateTextEditor.cs"));
        int handler = source.IndexOf(
            "OnPreviewMouseLeftButtonDown",
            StringComparison.Ordinal);
        int caret = source.IndexOf("CaretOffset = offset;", handler, StringComparison.Ordinal);
        int activation = source.IndexOf(
            "InteractionSession?.ActivateAt(",
            handler,
            StringComparison.Ordinal);

        Assert.True(handler >= 0);
        Assert.True(caret > handler);
        Assert.True(activation > caret);
    }

    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous editor action timed out.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }
    private static WorkspaceTabViewModel OpenSourceTab(
        VaultSession session,
        Action<EditorNavigationRequest>? navigate = null,
        Action<A11yEvent>? announce = null)
    {
        var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(
                Guid.NewGuid(),
                new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
            navigate: navigate,
            announce: announce,
            startInteractionBackgroundWork: false);
        tab.EditorInteractions!.RefreshMathRangesForTests();
        tab.EditorInteractions.RefreshArtifactCacheForTests();
        return tab;
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static int Inside(string text, string needle)
    {
        int start = text.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Fixture token is missing: {needle}");
        return start + Math.Min(2, needle.Length - 1);
    }

    private static string EmbedText(EditorEmbedPreviewNode node) =>
        string.Concat(node.Parts.Select(part =>
            part.Text ?? (part.Nested is null ? string.Empty : EmbedText(part.Nested))));
    private static string RepoFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, .. parts]);
    }

    private static void RunOnSta(Action action) =>
        StaThread.Run(action, TimeSpan.FromSeconds(20), "STA spelling test timed out.");

    private sealed class RedTeamFixture : IDisposable
    {
        private RedTeamFixture(string root)
        {
            Root = root;
            SourcePath = Path.Combine(root, "source.md");
        }

        public string Root { get; }
        public string SourcePath { get; }

        public static RedTeamFixture Create()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                $"slate-w2-red-team-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "nested.md"), "Nested leaf.\n");
            File.WriteAllText(
                Path.Combine(root, "target.md"),
                """
                # Lead

                ## Destination

                Section body.

                ![[nested]]

                ## Destination

                Duplicate section body.

                Block body ^block-id
                """);
            File.WriteAllBytes(
                Path.Combine(root, "broken.png"),
                "this is not a PNG"u8.ToArray());
            File.WriteAllText(
                Path.Combine(root, "source.md"),
                """
                # Source

                %% [[hidden]] %%
                [[target#Destination]]
                [[target#destination-2]]
                [[target^missing-block]]
                ![[target#Destination]]
                ![[broken.png]]
                [@doe]
                #project
                - [ ] task prose

                $$

                #not-a-tag-in-spaced-math
                [[target]] in spaced math

                $$
                """);
            return new RedTeamFixture(root);
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
}

internal sealed class FakeEditorSpellingService : IEditorSpellingService
{
    public bool IsAvailable => true;
    public string? LastCheckedText { get; private set; }

    public IReadOnlyList<EditorSpellingError> Check(string text)
    {
        LastCheckedText = text;
        int offset = text.IndexOf("mispel", StringComparison.Ordinal);
        return offset < 0 ? [] : [new EditorSpellingError(offset, "mispel".Length)];
    }

    public void Dispose()
    {
    }
}
