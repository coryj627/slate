// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.Tests;

/// <summary>
/// W4-8 (#740) contract SD8 facts: the bounded sync-marker watch, the
/// Windows twin of the mac <c>SyncMarkerWatcherTests</c> (#638). The
/// watcher is pure filesystem and UI-free, so every fact runs against a
/// real temp directory with no session, workspace, or dispatcher.
///
/// Timing discipline: the intervals are INJECTED short (100 ms debounce
/// / 400 ms ceiling unless a fact needs otherwise) and every positive
/// wait is <see cref="SpinWait.SpinUntil(Func{bool}, TimeSpan)"/> on the
/// fire counter with a generous upper BOUND — green runs finish early
/// and a loaded runner does not turn a real behaviour into a flake. The
/// only sleeps are negative-fire settle windows, where waiting IS the
/// assertion.
/// </summary>
public sealed class SyncMarkerWatcherTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Ceiling = TimeSpan.FromMilliseconds(400);

    /// <summary>Upper bound on any positive wait — never the expected
    /// duration.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(15);

    /// <summary>Negative-fire settle window: comfortably past 3x the
    /// ceiling, so "nothing fired" means the deadline really passed.
    /// </summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1500);

    // --- Debounce ---

    [Fact]
    public void ARootMarkerCreationFiresOnceAfterTheQuietPeriod()
    {
        string root = NewVault("root-marker");
        int fires = 0;
        // A DELIBERATELY wide quiet period, not the shared 100 ms. One
        // File.WriteAllText emits Created AND Changed, and on a hosted
        // runner Defender scans the file on close and can emit a
        // further LastWrite a couple of hundred milliseconds after
        // that — at 100 ms the window can close mid-write and the
        // "one detection" assertion below becomes a coin toss. The
        // property pinned here is "a root marker reaches the debounced
        // callback, once", never "the debounce is short"; the ceiling
        // fact owns the interval arithmetic.
        var watcher = new SyncMarkerWatcher(
            root,
            () => Interlocked.Increment(ref fires),
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(4));
        try
        {
            // Start returns with the handles open (SD4 arm-then-probe),
            // so a marker written on the next line MUST emit an event —
            // no arming sleep, no setup-race window.
            watcher.Start();
            // Written with CONTENT on purpose. An empty write emits
            // Created and nothing else, which makes the "one detection"
            // assertion below unfalsifiable — it would hold even with
            // the coalescing removed. Real bytes give the OS a Changed
            // to report as well, so the assertion is about the debounce
            // rather than about how few events Windows happened to
            // send.
            File.WriteAllText(Path.Combine(root, ".stignore"), "*.tmp\n");

            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                "a root marker must reach the debounced callback");
            // Three times the injected quiet period: a second burst
            // would have landed well inside this window.
            Thread.Sleep(TimeSpan.FromMilliseconds(1500));
            // Create plus the write's own Changed are ONE entry-churn
            // burst, not two detections.
            Assert.Equal(1, Volatile.Read(ref fires));
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    [Fact]
    public void ABurstOfEntryChurnCoalescesIntoOneFire()
    {
        string root = NewVault("burst");
        int fires = 0;
        // A far wider window than the other facts on purpose: the
        // assertion is "eight writes inside ONE quiet period produce
        // one fire", so BOTH intervals have to outlast the write loop
        // on a loaded runner. Defender routinely adds 50-200 ms per
        // create, which puts eight writes within reach of a 2 s
        // ceiling — and a ceiling that lands mid-burst legitimately
        // yields two fires and fails a fact that is not about the
        // ceiling at all. 1 s trailing / 10 s ceiling clears both.
        var watcher = new SyncMarkerWatcher(
            root,
            () => Interlocked.Increment(ref fires),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10));
        try
        {
            watcher.Start();
            // The note-save pattern: a burst of root entry-list churn.
            for (int index = 0; index < 8; index++)
            {
                File.WriteAllText(Path.Combine(root, $"note-{index}.md"), "x");
            }

            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                "the burst must settle into a fire");
            // Past the 1 s quiet period with room to spare: a burst
            // that had really split in two would have shown its second
            // fire by now.
            Thread.Sleep(TimeSpan.FromSeconds(3));
            Assert.Equal(1, Volatile.Read(ref fires));
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    /// <summary>The #638 anti-starvation fact. Churn at half the debounce
    /// resets the trailing deadline every time, so a pure trailing
    /// debounce would never fire at all — the max-latency ceiling,
    /// anchored on the burst's FIRST event, is the only thing that can
    /// land a detection here.</summary>
    [Fact]
    public void ContinuousSubIntervalChurnStillFiresByTheCeiling()
    {
        string root = NewVault("starve");
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            var elapsed = Stopwatch.StartNew();
            // The churn only stops when this scope exits, so it is
            // demonstrably still running when the assertions return.
            using (new Churn(root, periodMs: 50))
            {
                Assert.True(
                    SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                    "continuous sub-interval churn must not starve detection");
                Assert.True(
                    elapsed.Elapsed < TimeSpan.FromSeconds(5),
                    $"the ceiling is {Ceiling}; the fire took {elapsed.Elapsed}");
            }
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    // --- Re-arm through the parent watch ---

    [Fact]
    public void AnObsidianTreeCreatedAfterStartIsArmedByItsParent()
    {
        string root = NewVault("chain");
        string obsidian = Path.Combine(root, ".obsidian");
        string plugins = Path.Combine(obsidian, "plugins");
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();

            // Hop 1: the root watch sees `.obsidian` appear and re-arms.
            Directory.CreateDirectory(obsidian);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                "the root watch must see .obsidian appear");

            // Hop 2: `plugins` appears one level down.
            Directory.CreateDirectory(plugins);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 2, Bound),
                "the re-armed .obsidian watch must see plugins appear");

            Thread.Sleep(Settle);
            int baseline = Volatile.Read(ref fires);
            // Hop 3, the airtight one: a FILE two levels below the root
            // is out of reach of both the root and `.obsidian` watches
            // (SD8 is non-recursive), so only a live `plugins` watch —
            // armed by a live `.obsidian` watch, itself armed by the
            // root — can report it.
            File.WriteAllText(Path.Combine(plugins, "manifest.json"), "{}");
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) > baseline, Bound),
                "the re-armed plugins watch must be live");
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    /// <summary>Identity safety (the ABA rule): a watched subdirectory
    /// deleted and recreated must end with a LIVE watch on the NEW
    /// directory. Without generations, a late handler from the dead watch
    /// could retire whatever now sits at that key and kill live detection
    /// silently.</summary>
    [Fact]
    public void DeletingAndRecreatingObsidianKeepsItsWatchLive()
    {
        string root = NewVault("recreate");
        string obsidian = Path.Combine(root, ".obsidian");
        string plugins = Path.Combine(obsidian, "plugins");
        Directory.CreateDirectory(obsidian);
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();

            DeleteDirectoryWithRetry(obsidian);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                ".obsidian going away must reach the callback");

            CreateDirectoryWithRetry(obsidian);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 2, Bound),
                "the root watch must see .obsidian come back");

            // Walk the chain one level deeper through the RECREATED dir:
            // only a fresh, live `.obsidian` watch can arm `plugins`.
            Directory.CreateDirectory(plugins);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 3, Bound),
                "the re-armed .obsidian watch must see plugins appear");

            Thread.Sleep(Settle);
            int baseline = Volatile.Read(ref fires);
            File.WriteAllText(Path.Combine(plugins, "manifest.json"), "{}");
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) > baseline, Bound),
                "the whole re-armed chain must be live after a recreate");
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    /// <summary>SD8's RENAME arm, a genuinely different code path from
    /// the delete one above: <c>Renamed</c> is the only event that
    /// carries <c>OldFullPath</c>, and only that path retires the watch
    /// sitting at the directory that vanished. It is also the shape
    /// real sync clients produce — a conflicted <c>.obsidian</c> gets
    /// moved aside and replaced, never deleted in place — and a watch
    /// left behind would follow the RENAMED directory by handle while
    /// the vault's real <c>.obsidian</c> went unwatched.</summary>
    [Fact]
    public void RenamingObsidianAwayAndRecreatingItKeepsTheChainLive()
    {
        string root = NewVault("rename");
        string obsidian = Path.Combine(root, ".obsidian");
        string movedAside = Path.Combine(root, ".obsidian-conflict");
        string plugins = Path.Combine(obsidian, "plugins");
        Directory.CreateDirectory(obsidian);
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            Assert.NotEqual(-1, watcher.ArmedGenerationForTests(".obsidian"));

            MoveDirectoryWithRetry(obsidian, movedAside);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                ".obsidian being renamed away must reach the callback");
            // The watch at the vanished path is GONE, not quietly
            // aliasing `.obsidian-conflict`.
            Assert.True(
                SpinWait.SpinUntil(
                    () => watcher.ArmedGenerationForTests(".obsidian") == -1, Bound),
                "the renamed-away directory must lose its watch");

            CreateDirectoryWithRetry(obsidian);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 2, Bound),
                "the root watch must see a replacement .obsidian appear");
            Directory.CreateDirectory(plugins);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 3, Bound),
                "the re-armed .obsidian watch must see plugins appear");

            Thread.Sleep(Settle);
            int baseline = Volatile.Read(ref fires);
            // Two levels down: only a live `plugins` watch, armed by a
            // live replacement `.obsidian` watch, can report this.
            File.WriteAllText(Path.Combine(plugins, "manifest.json"), "{}");
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) > baseline, Bound),
                "the chain must re-arm through a RENAME, not only a delete");
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    // --- SDR-1: the OS error channel ---

    /// <summary>SDR-1: <c>InternalBufferOverflow</c> means the OS
    /// DROPPED events. What was dropped is unknowable, so the only
    /// correct response is to rebuild the lossy watch and re-detect —
    /// a fire, never an error surface the reader has to dismiss. Driven
    /// through the watcher's error seam because a real overflow needs
    /// thousands of events inside one buffer window and could never be
    /// made deterministic.</summary>
    [Fact]
    public void ADroppedEventBurstRebuildsTheWatchAndFires()
    {
        string root = NewVault("overflow");
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            long armed = watcher.ArmedGenerationForTests("");
            Assert.NotEqual(-1, armed);

            Assert.True(
                watcher.RaiseWatchErrorForTests(""),
                "the root watch must be armed before its error channel is driven");

            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                "a dropped-event burst must re-detect, not surface an error");
            // Retired AND reconciled: same key, still exactly one live
            // watch, but a NEW generation — the lossy handle was
            // disposed and replaced rather than left in place.
            Assert.Equal(1, watcher.ArmedWatchCountForTests);
            Assert.NotEqual(armed, watcher.ArmedGenerationForTests(""));

            Thread.Sleep(Settle);
            int baseline = Volatile.Read(ref fires);
            File.WriteAllText(Path.Combine(root, ".stignore"), string.Empty);
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fires) > baseline, Bound),
                "the rebuilt watch must be live, not merely present");
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    // --- Teardown ---

    [Fact]
    public void StopIsIdempotentAndSuppressesEveryLaterFire()
    {
        string root = NewVault("stopped");
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            watcher.Stop();
            watcher.Stop();
            // Dispose IS Stop; a third pass must be just as quiet.
            watcher.Dispose();

            Directory.CreateDirectory(Path.Combine(root, ".git"));
            File.WriteAllText(Path.Combine(root, ".stignore"), string.Empty);
            Thread.Sleep(Settle);

            Assert.Equal(0, Volatile.Read(ref fires));
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    /// <summary>SDINV-5's hard edge: events landing WHILE Stop runs must
    /// not produce a callback after it returns. The stopped check and the
    /// invocation share one acquisition of the watcher's lock, so the
    /// count observed the instant Stop returns is final.</summary>
    [Fact]
    public void EventsLandingDuringStopNeverFireAfterItReturns()
    {
        string root = NewVault("stop-race");
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            using (new Churn(root, periodMs: 5))
            {
                // Stop must land mid-burst, with raw events queued behind
                // the watcher's lock rather than against an idle watcher.
                Assert.True(
                    SpinWait.SpinUntil(() => Volatile.Read(ref fires) >= 1, Bound),
                    "the churn must be reaching the callback before Stop");

                watcher.Stop();
                int frozen = Volatile.Read(ref fires);

                // The churn keeps running right through the settle
                // window; the count must not move.
                Thread.Sleep(Settle);
                Assert.Equal(frozen, Volatile.Read(ref fires));
            }
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    // --- Bounded scope ---

    [Fact]
    public void ChurnBelowTheWatchedScopeNeverFires()
    {
        string root = NewVault("scope");
        string nested = Path.Combine(root, "notes", "sub");
        Directory.CreateDirectory(nested);
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            root, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            // SD8's bound: the watched set is {root, .obsidian,
            // .obsidian/plugins} and every watch is non-recursive, so the
            // root sees entry churn for its DIRECT children only. Files
            // two levels down under `notes` are outside the scope
            // entirely — no fire, so ordinary note writing in subfolders
            // costs no re-detection.
            for (int index = 0; index < 5; index++)
            {
                File.WriteAllText(Path.Combine(nested, $"note-{index}.md"), "x");
            }

            Thread.Sleep(Settle);
            Assert.Equal(0, Volatile.Read(ref fires));
        }
        finally
        {
            watcher.Dispose();
            DeleteTree(root);
        }
    }

    /// <summary>SDR-2: an arm that cannot happen is non-fatal. Nothing
    /// throws, nothing fires, and detection still runs at vault open and
    /// on manual refresh. Two distinct SDR-2 shapes exist and this fact
    /// covers both of the ones that are deterministic on a hosted
    /// runner: a directory that is merely ABSENT (skipped, not failed),
    /// and a root STRING the platform cannot turn into a path (recorded
    /// at the constructor's normalization guard). The third — a
    /// directory that exists but refuses a change-notification handle
    /// (a virtualized root, a hostile share), which lands in
    /// <c>Arm()</c>'s own catch — has no deterministic reproduction
    /// here and stays covered by the recorded-degradation path.
    /// </summary>
    [Fact]
    public void AVaultRootThatCannotBeArmedIsNotAFault()
    {
        string absent = Path.Combine(
            Path.GetTempPath(), $"slate-marker-watch-absent-{Guid.NewGuid():N}");
        int fires = 0;
        var watcher = new SyncMarkerWatcher(
            absent, () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            watcher.Start();
            watcher.Stop();

            Assert.Equal(0, Volatile.Read(ref fires));
            Assert.False(Directory.Exists(absent));
            // A directory that is merely ABSENT is skipped, not failed —
            // that is the ordinary "no .obsidian yet" path, and the
            // parent watch arms it when it appears.
            Assert.Null(watcher.ArmFailureForTests);
        }
        finally
        {
            watcher.Dispose();
        }

        // The failure CHANNEL itself, exercised at the CONSTRUCTOR's
        // path-normalization guard and not at Arm()'s catch:
        // Path.GetFullPath throws on an embedded NUL, so the root never
        // becomes a path and no arm is ever attempted. Naming the exact
        // recorded value is what keeps that claim honest — if the throw
        // ever moved to the arm, the prefix would change with it.
        var unusable = new SyncMarkerWatcher(
            "\0not-a-path", () => Interlocked.Increment(ref fires), Debounce, Ceiling);
        try
        {
            unusable.Start();
            Assert.Equal("root: ArgumentException", unusable.ArmFailureForTests);
            Assert.Equal(0, unusable.ArmedWatchCountForTests);
            Assert.Equal(0, Volatile.Read(ref fires));
        }
        finally
        {
            unusable.Dispose();
        }
    }

    // --- Helpers ---

    private static string NewVault(string label)
    {
        string root = Path.Combine(
            Path.GetTempPath(), $"slate-marker-watch-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTree(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A live change-notification handle holds a directory in a
    /// delete-pending state until the watcher learns it is gone and
    /// closes it, so setup churn on a watched directory needs a few
    /// retries. The retries are filesystem PLUMBING — no assertion
    /// depends on them.</summary>
    private static void DeleteDirectoryWithRetry(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (
                attempt < 40 && exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>Same PLUMBING as the delete retry: a live
    /// change-notification handle can briefly refuse a rename of the
    /// directory it watches. No assertion depends on the retries.
    /// </summary>
    private static void MoveDirectoryWithRetry(string source, string destination)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (Exception exception) when (
                attempt < 40 && exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private static void CreateDirectoryWithRetry(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.CreateDirectory(path);
                return;
            }
            catch (Exception exception) when (
                attempt < 40 && exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>Background root entry-list churn for the ceiling and
    /// stop-race facts. Disposal stops the thread and joins it, so the
    /// churn is provably alive for the whole scope it wraps.</summary>
    private sealed class Churn : IDisposable
    {
        private readonly Thread _thread;
        private int _running = 1;

        public Churn(string root, int periodMs)
        {
            _thread = new Thread(() => Loop(root, periodMs)) { IsBackground = true };
            _thread.Start();
        }

        public void Dispose()
        {
            Volatile.Write(ref _running, 0);
            _thread.Join(TimeSpan.FromSeconds(10));
        }

        private void Loop(string root, int periodMs)
        {
            for (int index = 0; Volatile.Read(ref _running) == 1; index++)
            {
                try
                {
                    File.WriteAllText(Path.Combine(root, $"churn-{index}.md"), "x");
                }
                catch (DirectoryNotFoundException)
                {
                    return;
                }
                catch (IOException)
                {
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }

                Thread.Sleep(periodMs);
            }
        }
    }
}
