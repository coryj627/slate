// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// <see cref="StaThread"/>'s timeout path: a body that outlives its bound
/// still leaves no window class behind when it unwinds within the grace,
/// and one that never unwinds fails every fact that runs beside it until it
/// ends — then its classes are freed.
/// </summary>
public sealed class StaThreadTests : IDisposable
{
    private readonly string? _dumpDirectory = StaThread.DumpDirectory;
    private readonly ITestOutputHelper _output;

    /// <summary>These facts time out on purpose: none of them writes CI's
    /// dump unless it names a folder of its own.</summary>
    public StaThreadTests(ITestOutputHelper output)
    {
        _output = output;
        StaThread.DumpDirectory = null;
        StaThread.ResetDumpsForTests();
    }

    public void Dispose()
    {
        StaThread.DumpDirectory = _dumpDirectory;
        StaThread.DumpKindForTests = null;
        StaThread.ResetDumpsForTests();
    }

    /// <summary>#1603: a body that does not pump gives no stack, so with a
    /// dump folder named (CI's app job names its results folder) the timeout
    /// writes a dump of the process there and says where. #1641: the dump is
    /// the runtime's createdump's, taken from outside the process, of THIS
    /// process: it lists the timed-out body's thread with its stack. A dump
    /// the process took of itself came out partial on CI (2 MB, error
    /// 0x80070057). The real createdump writes a normal dump here: a full
    /// one of the test host mid-suite was 4.7 GB and 12.5 s on CI, on every
    /// run. Production's <c>--full</c> is pinned by the command fact below,
    /// and #1644's probe read CI's full dumps with <c>clrstack -all</c>.</summary>
    [Fact]
    public void ATimedOutBodyThatDoesNotPumpLeavesADumpOfThisProcessWhereTheFolderSays()
    {
        // Under CI's own dump folder when it names one (the runner's work
        // folder, so a failing fact's dump is uploaded as its evidence).
        string folder = Path.Combine(
            Environment.GetEnvironmentVariable("SLATE_STA_DUMP_DIR") ?? Path.GetTempPath(), $"sta-dump-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        StaThread.DumpKindForTests = "normal";
        uint bodyThread = 0;
        bool passed = false;
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
                () =>
                {
                    Volatile.Write(ref bodyThread, GetCurrentThreadId());
                    // Alive until the dump is on disk: createdump suspends
                    // this thread for the whole write, so a length is seen
                    // only once the write has ended.
                    var alive = System.Diagnostics.Stopwatch.StartNew();
                    while (alive.Elapsed < TimeSpan.FromSeconds(60)
                        && !Directory.EnumerateFiles(folder, "*.dmp").Any(file => new FileInfo(file).Length > 0))
                    {
                        Thread.Sleep(20);
                    }
                },
                TimeSpan.FromMilliseconds(200),
                "the body waited for its dump",
                unwindGrace: TimeSpan.FromSeconds(90)));

            // The message first: a write that fails says why (CI on 3d4aa90d
            // failed here with an empty folder and no message to read).
            Assert.Contains("A normal dump of the test process is at ", timedOut.Message, StringComparison.Ordinal);
            string dump = Assert.Single(Directory.GetFiles(folder, "*.dmp"));
            Assert.Contains($"A normal dump of the test process is at {dump} (", timedOut.Message, StringComparison.Ordinal);
            Assert.Contains(" MB private", timedOut.Message, StringComparison.Ordinal);
            _output.WriteLine($"{new FileInfo(dump).Length} bytes in {clock.Elapsed.TotalSeconds:0.0} s:{timedOut.Message}");
            Minidump read = Minidump.Read(dump);
            Assert.True(
                read.ThreadStacks.TryGetValue(Volatile.Read(ref bodyThread), out uint stack) && stack > 0,
                $"the dump lists the body's thread {bodyThread} with its stack: it is this process's");
            passed = true;
        }
        finally
        {
            StaThread.DumpKindForTests = null;
            if (passed)
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    /// <summary>#1641: the dumper is a process of its own, the runtime's
    /// createdump, started directly (it dumps the process that started it,
    /// so a shell between them would be dumped instead) with a full dump
    /// into the folder.</summary>
    [Fact]
    public void TheDumpIsTheRuntimesCreatedumpStartedByThisProcess()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        System.Diagnostics.ProcessStartInfo? started = null;
        StaThread.RunDumperForTests = start =>
        {
            started = start;
            File.WriteAllBytes(start.ArgumentList[2], "MDMP"u8.ToArray());
            return (0, string.Empty);
        };
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            Assert.NotNull(started);
            Assert.Equal(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "createdump.exe"), started.FileName);
            Assert.True(File.Exists(started.FileName), "createdump ships with the runtime");
            Assert.False(started.UseShellExecute);
            Assert.True(started.CreateNoWindow);
            string dump = Assert.Single(Directory.GetFiles(folder, "*.dmp"));
            Assert.Equal(new[] { "--full", "--name", dump }, started.ArgumentList);
            Assert.Contains($"A full dump of the test process is at {dump} (", timedOut.Message, StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>createdump reads % as a specifier (%p, %e, %h, %t): a folder
    /// with one in its name reaches it doubled.</summary>
    [Fact]
    public void APercentInTheDumpFolderReachesCreatedumpDoubled()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-100%-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        string? named = null;
        StaThread.RunDumperForTests = start =>
        {
            named = start.ArgumentList[2];
            return (1, "[createdump] not run");
        };
        try
        {
            _ = TimeOutASleepingBody();

            Assert.NotNull(named);
            Assert.StartsWith(Path.Combine(Path.GetTempPath(), "sta-dump-100%%-"), named, StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>createdump opens the dump through an ANSI path: a folder that
    /// is not ASCII is refused in words, and createdump is never started.</summary>
    [Fact]
    public void ANonAsciiDumpFolderIsRefusedInWords()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-é-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        bool started = false;
        StaThread.RunDumperForTests = _ =>
        {
            started = true;
            return (0, string.Empty);
        };
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            Assert.False(started, "createdump was started on a path it cannot open");
            Assert.Contains("No dump was written: createdump needs an ASCII path, and ", timedOut.Message, StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A createdump that fails after writing part of the dump: the
    /// file is kept and named, with its exit code and what it said.</summary>
    [Fact]
    public void ADumpCreatedumpReportsAsFailedIsKeptAndNamed()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        StaThread.RunDumperForTests = start =>
        {
            File.WriteAllBytes(start.ArgumentList[2], "MDMP"u8.ToArray());
            return (-1, "[createdump] MiniDumpWriteDump - Only part of a ReadProcessMemory request was completed. (0x8007012b)");
        };
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            string dump = Assert.Single(Directory.GetFiles(folder, "*.dmp"));
            Assert.Contains($"A partial dump of the test process is at {dump} (4 bytes, ", timedOut.Message, StringComparison.Ordinal);
            Assert.Contains(
                " MB private; createdump exited -1: [createdump] MiniDumpWriteDump - Only part of a ReadProcessMemory request was completed. (0x8007012b)).",
                timedOut.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A createdump that fails without writing says so, with its
    /// exit code and what it said, and leaves no file.</summary>
    [Fact]
    public void ACreatedumpThatWritesNothingIsNamedWithWhatItSaid()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        StaThread.RunDumperForTests = _ => (-1, "[createdump] The pid argument is no longer supported");
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            Assert.Empty(Directory.GetFiles(folder, "*.dmp"));
            Assert.Contains(
                "No dump was written: createdump exited -1: [createdump] The pid argument is no longer supported.",
                timedOut.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>A createdump that runs past its bound is stopped, and the
    /// timeout says so rather than waiting on it.</summary>
    [Fact]
    public void ACreatedumpPastItsBoundIsStoppedAndNamed()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        StaThread.RunDumperForTests = _ => (null, string.Empty);
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            Assert.Contains(
                $"No dump was written: createdump ran past its {StaThread.DumpBound.TotalSeconds:0} s bound and was stopped.",
                timedOut.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            StaThread.RunDumperForTests = null;
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Xunit.Sdk.XunitException TimeOutASleepingBody() =>
        Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () => Thread.Sleep(TimeSpan.FromMilliseconds(600)),
            TimeSpan.FromMilliseconds(200),
            "the body only slept",
            unwindGrace: TimeSpan.FromSeconds(10)));

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>What a fact reads from a minidump (MINIDUMP_HEADER, then one
    /// MINIDUMP_DIRECTORY per stream): each listed thread's stack size, by
    /// its OS id (ThreadListStream, 3).</summary>
    private sealed record Minidump(IReadOnlyDictionary<uint, uint> ThreadStacks)
    {
        public static Minidump Read(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            Assert.Equal("MDMP", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)));
            _ = reader.ReadUInt32();
            uint streams = reader.ReadUInt32();
            uint directory = reader.ReadUInt32();
            var threads = new Dictionary<uint, uint>();
            for (uint index = 0; index < streams; index++)
            {
                stream.Position = directory + (index * 12L);
                uint type = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                uint rva = reader.ReadUInt32();
                if (type != 3)
                {
                    continue;
                }
                // MINIDUMP_THREAD, 48 bytes: id, suspend count, priority
                // class, priority, TEB, then the stack's descriptor (start,
                // then its size at +32).
                stream.Position = rva;
                uint count = reader.ReadUInt32();
                for (uint thread = 0; thread < count; thread++)
                {
                    long entry = rva + 4 + (thread * 48L);
                    stream.Position = entry;
                    uint id = reader.ReadUInt32();
                    stream.Position = entry + 32;
                    threads[id] = reader.ReadUInt32();
                }
            }
            return new Minidump(threads);
        }
    }

    /// <summary>With no folder named, nothing is written and the message
    /// says nothing of a dump.</summary>
    [Fact]
    public void WithNoDumpFolderATimeoutWritesNoDump()
    {
        Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

        Assert.DoesNotContain("dump", timedOut.Message, StringComparison.Ordinal);
    }

    /// <summary>The body neither pumps nor returns before its bound, so the
    /// shutdown the runner asks for never runs; it returns inside the grace,
    /// and its thread's classes — a dispatcher's and a visual's at least —
    /// are unregistered before the timeout is reported.</summary>
    [Fact]
    public void ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind()
    {
        string? name = null;
        int registered = 0;
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () =>
            {
                name = Thread.CurrentThread.Name;
                _ = Dispatcher.CurrentDispatcher;
                _ = new Border { Visibility = Visibility.Collapsed };
                registered = WindowClasses.RegisteredFor(name!).Count;
                Thread.Sleep(TimeSpan.FromMilliseconds(800));
            },
            TimeSpan.FromMilliseconds(200),
            "the body outlived its bound",
            unwindGrace: TimeSpan.FromSeconds(10)));

        Assert.Contains("the body unwound", timedOut.Message, StringComparison.Ordinal);
        Assert.Contains("did not pump its dispatcher", timedOut.Message, StringComparison.Ordinal);
        Assert.NotNull(name);
        Assert.True(registered >= 2, $"premise: the body's thread registered {registered} WPF classes, expected a dispatcher's and a visual's");
        Assert.Empty(WindowClasses.RegisteredFor(name));
    }

    /// <summary>#1603: a body that is still pumping when its bound passes —
    /// a rescan's wait loop, say — has its stack taken on its own dispatcher,
    /// so the timeout names where it waits.</summary>
    [Fact]
    public void ATimedOutBodyThatStillPumpsReportsWhereItWaits()
    {
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            WaitHereWhilePumping,
            TimeSpan.FromMilliseconds(200),
            "the body kept pumping",
            unwindGrace: TimeSpan.FromSeconds(10)));

        Assert.Contains("Its stack while pumping:", timedOut.Message, StringComparison.Ordinal);
        Assert.Contains($"{nameof(StaThreadTests)}.{nameof(WaitHereWhilePumping)}", timedOut.Message, StringComparison.Ordinal);
    }

    /// <summary>A body that never made a dispatcher has none to pump: the
    /// timeout says so instead of waiting for a stack.</summary>
    [Fact]
    public void ATimedOutBodyWithNoDispatcherSaysWhyItHasNoStack()
    {
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () => Thread.Sleep(TimeSpan.FromMilliseconds(600)),
            TimeSpan.FromMilliseconds(200),
            "the body only slept",
            unwindGrace: TimeSpan.FromSeconds(10)));

        Assert.Contains("the body has no dispatcher to pump", timedOut.Message, StringComparison.Ordinal);
    }

    /// <summary>A body that pumps its dispatcher until the runner shuts it
    /// down, or three seconds pass.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void WaitHereWhilePumping()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        while (!dispatcher.HasShutdownStarted && clock.Elapsed < TimeSpan.FromSeconds(3))
        {
            var frame = new DispatcherFrame();
            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            try
            {
                Dispatcher.PushFrame(frame);
            }
            catch (InvalidOperationException) when (dispatcher.HasShutdownStarted)
            {
                return;
            }
            Thread.Sleep(2);
        }
    }

    /// <summary>A body that never unwinds runs on beside the facts after its
    /// own: <see cref="StaThread.Wedged"/> names it — and the leak guard's
    /// <c>Before</c> fails the next fact on it — until it ends, when its
    /// classes are unregistered and the run is clean again.</summary>
    [Fact]
    public void ABodyThatNeverUnwindsFailsTheFactsBesideItUntilItEnds()
    {
        using var release = new ManualResetEventSlim(false);
        string? name = null;
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () =>
            {
                name = Thread.CurrentThread.Name;
                _ = new Border { Visibility = Visibility.Collapsed };
                _ = release.Wait(TimeSpan.FromSeconds(60));
            },
            TimeSpan.FromMilliseconds(200),
            "the body never returned",
            unwindGrace: TimeSpan.FromMilliseconds(200)));
        try
        {
            Assert.Contains("has not unwound", timedOut.Message, StringComparison.Ordinal);
            Assert.NotNull(name);
            string? wedged = StaThread.Wedged();
            Assert.NotNull(wedged);
            Assert.Contains(name, wedged, StringComparison.Ordinal);
            MethodInfo next = typeof(StaThreadTests).GetMethod(nameof(ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind))!;
            Xunit.Sdk.XunitException refused = Assert.Throws<Xunit.Sdk.XunitException>(
                () => new LeakedDispatcherGuardAttribute().Before(next, (Xunit.v3.IXunitTest)TestContext.Current.Test!));
            Assert.Contains(name, refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            release.Set();
            Assert.True(
                SpinWait.SpinUntil(() => StaThread.Wedged() is null, TimeSpan.FromSeconds(10)),
                "the released body's thread never ended");
        }
        Assert.Empty(WindowClasses.RegisteredFor(name));
    }

    /// <summary>#1321: a timed-out body that polls
    /// <see cref="StaThread.TimedOut"/> between its phases stops there,
    /// inside the grace, so its timeout is one failure and the facts after it
    /// run clean — not one slow walk failing every fact beside it.</summary>
    [Fact]
    public void ABodyThatPollsTimedOutUnwindsWithinTheGraceAndLaterFactsPass()
    {
        bool stoppedOnItsToken = false;
        Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
            () =>
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (clock.Elapsed < TimeSpan.FromSeconds(30))
                {
                    if (StaThread.TimedOut)
                    {
                        Volatile.Write(ref stoppedOnItsToken, true);
                        return;
                    }
                    Thread.Sleep(5);
                }
            },
            TimeSpan.FromMilliseconds(200),
            "the body polled its token",
            unwindGrace: TimeSpan.FromSeconds(10)));

        Assert.Contains("the body unwound", timedOut.Message, StringComparison.Ordinal);
        Assert.True(Volatile.Read(ref stoppedOnItsToken), "the body ran out its own 30 s instead of stopping on its token");
        Assert.Null(StaThread.Wedged());
        MethodInfo next = typeof(StaThreadTests).GetMethod(nameof(ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind))!;
        new LeakedDispatcherGuardAttribute().Before(next, (Xunit.v3.IXunitTest)TestContext.Current.Test!);
    }

    /// <summary>The token a body reads is the one its own run cancels: the
    /// joining caller owns the source, made before the thread starts, and only
    /// its token sits in the thread's slot. A source in the slot, cancelled
    /// from the caller, would be the caller's own empty one.</summary>
    [Fact]
    public void TheTokenABodyReadsIsTheOneItsRunCancels()
    {
        Assert.False(StaThread.Current.CanBeCanceled, "premise: outside a run there is no timeout to read");
        CancellationToken read = default;
        bool cancelledAtTheStart = true;
        using var readIt = new ManualResetEventSlim(false);
        // The bound starts once the body has read its token: a thread that
        // first ran late would otherwise read one already cancelled.
        StaThread.AfterStartForTests = () => Assert.True(readIt.Wait(TimeSpan.FromSeconds(30)), "the body never started");
        try
        {
            _ = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
                () =>
                {
                    read = StaThread.Current;
                    cancelledAtTheStart = read.IsCancellationRequested;
                    readIt.Set();
                    Thread.Sleep(TimeSpan.FromMilliseconds(600));
                },
                TimeSpan.FromMilliseconds(200),
                "the body only slept",
                unwindGrace: TimeSpan.FromSeconds(10)));
        }
        finally
        {
            StaThread.AfterStartForTests = null;
        }

        Assert.True(read.CanBeCanceled, "the body read no run's token");
        Assert.False(cancelledAtTheStart, "the body's token was cancelled before its bound passed");
        Assert.True(read.IsCancellationRequested, "the run timed out, but the token its body read was never cancelled");
    }

    /// <summary>The token is a request, not a stop: a body that never reads
    /// it still wedges, and still fails every fact beside it until it ends.</summary>
    [Fact]
    public void ABodyThatIgnoresTimedOutStillWedgesAndFailsLaterFacts()
    {
        using var release = new ManualResetEventSlim(false);
        try
        {
            (Xunit.Sdk.XunitException timedOut, string name, CancellationToken read) = WedgeABodyThatIgnoresItsToken(release);
            Assert.Contains("has not unwound", timedOut.Message, StringComparison.Ordinal);
            Assert.True(read.IsCancellationRequested, "premise: the body's token was cancelled");
            string? wedged = StaThread.Wedged();
            Assert.NotNull(wedged);
            Assert.Contains(name, wedged, StringComparison.Ordinal);
            MethodInfo next = typeof(StaThreadTests).GetMethod(nameof(ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind))!;
            Xunit.Sdk.XunitException refused = Assert.Throws<Xunit.Sdk.XunitException>(
                () => new LeakedDispatcherGuardAttribute().Before(next, (Xunit.v3.IXunitTest)TestContext.Current.Test!));
            Assert.Contains(name, refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            Release(release);
        }
    }

    /// <summary>#1321: the facts a wedged body fails read which fact it was,
    /// so the one root failure is found among the hundreds after it.</summary>
    [Fact]
    public void TheCascadeMessageNamesTheRootFact()
    {
        string root = TestContext.Current.Test!.TestDisplayName;
        using var release = new ManualResetEventSlim(false);
        try
        {
            (_, string name, _) = WedgeABodyThatIgnoresItsToken(release);
            string? wedged = StaThread.Wedged();
            Assert.NotNull(wedged);
            Assert.Contains($"`{root}` (thread {name}) is still unwinding after its timeout: the body ignored its token", wedged, StringComparison.Ordinal);
            MethodInfo next = typeof(StaThreadTests).GetMethod(nameof(ABodyThatTimesOutAndThenUnwindsLeavesNoWindowClassBehind))!;
            Xunit.Sdk.XunitException refused = Assert.Throws<Xunit.Sdk.XunitException>(
                () => new LeakedDispatcherGuardAttribute().Before(next, (Xunit.v3.IXunitTest)TestContext.Current.Test!));
            Assert.Contains($"`{root}`", refused.Message, StringComparison.Ordinal);
        }
        finally
        {
            Release(release);
        }
    }

    /// <summary>A fact's thread that first runs after both joins have
    /// expired has recorded no exit yet: it is still a body running beside
    /// the facts after it — tracked, and named, until it ends — never
    /// residue that is reported once and then forgotten.</summary>
    [Fact]
    public void AWedgedBodyWhoseThreadStartedLateIsStillTracked()
    {
        string root = TestContext.Current.Test!.TestDisplayName;
        using var start = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        StaThread.BeforeBodyForTests = () => start.Wait(TimeSpan.FromSeconds(30));
        try
        {
            Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
                () =>
                {
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    while (!release.IsSet && clock.Elapsed < TimeSpan.FromSeconds(60))
                    {
                        Thread.Sleep(20);
                    }
                },
                TimeSpan.FromMilliseconds(200),
                "the body started late",
                unwindGrace: TimeSpan.FromMilliseconds(200)));
            StaThread.BeforeBodyForTests = null;
            Assert.Contains("has not unwound", timedOut.Message, StringComparison.Ordinal);

            string? beforeItRan = StaThread.Wedged();
            Assert.NotNull(beforeItRan);
            Assert.Contains($"`{root}` (thread ", beforeItRan, StringComparison.Ordinal);
            start.Set();
            Assert.True(
                SpinWait.SpinUntil(() => StaThread.Wedged() is { } wedged && wedged.Contains($"`{root}`", StringComparison.Ordinal), TimeSpan.FromSeconds(1)),
                "a body that started after its joins expired was dropped from the wedged list");
        }
        finally
        {
            StaThread.BeforeBodyForTests = null;
            start.Set();
            Release(release);
        }
    }

    /// <summary>A body that sleeps until <paramref name="release"/> is set,
    /// never reading its token, past a 200 ms bound and a 200 ms grace. The
    /// bound starts once the body runs, so a late thread start cannot leave
    /// it untracked.</summary>
    private static (Xunit.Sdk.XunitException TimedOut, string Name, CancellationToken Read) WedgeABodyThatIgnoresItsToken(
        ManualResetEventSlim release)
    {
        string? name = null;
        CancellationToken read = default;
        using var started = new ManualResetEventSlim(false);
        StaThread.AfterStartForTests = () => Assert.True(started.Wait(TimeSpan.FromSeconds(30)), "the body never started");
        Xunit.Sdk.XunitException timedOut;
        try
        {
            timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
                () =>
                {
                    name = Thread.CurrentThread.Name;
                    read = StaThread.Current;
                    started.Set();
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    while (!release.IsSet && clock.Elapsed < TimeSpan.FromSeconds(60))
                    {
                        Thread.Sleep(20);
                    }
                },
                TimeSpan.FromMilliseconds(200),
                "the body ignored its token",
                unwindGrace: TimeSpan.FromMilliseconds(200)));
        }
        finally
        {
            StaThread.AfterStartForTests = null;
        }
        Assert.NotNull(name);
        return (timedOut, name, read);
    }

    /// <summary>Let a wedged body end, and wait until its thread has.</summary>
    private static void Release(ManualResetEventSlim release)
    {
        release.Set();
        Assert.True(
            SpinWait.SpinUntil(() => StaThread.Wedged() is null, TimeSpan.FromSeconds(10)),
            "the released body's thread never ended");
    }

    /// <summary>The CI failure of #1310, reproduced: the runner's Join begins
    /// only after the runtime has marked the fact's thread dead, while the OS
    /// thread's exit — which is when the system destroys its windows — is
    /// held 300 ms (an FLS callback the system runs as the thread exits:
    /// kernel32!Sleep). A managed Join then answers at once, with the windows
    /// still alive; the runner must wait for the OS exit before it frees the
    /// thread's classes, and then leave none.</summary>
    [Fact]
    public void AJoinThatBeginsAfterTheThreadDiedStillWaitsForItsWindowsToGo()
    {
        Thread? fact = null;
        OsThreadExit? observed = null;
        bool osExitedBeforeTheJoin = true;
        string? name = null;
        int registered = 0;
        StaThread.AfterStartForTests = () =>
        {
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref fact) is { IsAlive: false }, TimeSpan.FromSeconds(10)),
                "the fact's thread never ended in managed code");
            osExitedBeforeTheJoin = Volatile.Read(ref observed)!.WaitOne(0);
        };
        try
        {
            StaThread.Run(() =>
            {
                name = Thread.CurrentThread.Name;
                Volatile.Write(ref observed, OsThreadExit.OfCurrentThread());
                _ = Dispatcher.CurrentDispatcher;
                _ = new Border { Visibility = Visibility.Collapsed };
                registered = WindowClasses.RegisteredFor(name!).Count;
                HoldTheOsExit(TimeSpan.FromMilliseconds(300));
                Volatile.Write(ref fact, Thread.CurrentThread);
            });
        }
        finally
        {
            StaThread.AfterStartForTests = null;
            Volatile.Read(ref observed)?.Dispose();
        }

        Assert.NotNull(name);
        Assert.True(registered >= 2, $"premise: the fact's thread registered {registered} WPF classes, expected a dispatcher's and a visual's");
        Assert.False(osExitedBeforeTheJoin, "premise: the OS thread had already exited when the runner joined, so the race was not set up");
        Assert.Empty(WindowClasses.RegisteredFor(name));
    }

    private static readonly Lazy<uint> HoldExitSlot = new(() =>
    {
        uint slot = FlsAlloc(GetProcAddress(GetModuleHandleW("kernel32.dll"), "Sleep"));
        Assert.True(slot != uint.MaxValue, "no FLS slot for the exit hold");
        return slot;
    });

    /// <summary>Make the calling thread's OS exit take <paramref name="delay"/>
    /// longer: the system passes the slot's value to the slot's callback,
    /// kernel32!Sleep, as the thread exits — after its managed code has ended.</summary>
    private static void HoldTheOsExit(TimeSpan delay) =>
        Assert.True(FlsSetValue(HoldExitSlot.Value, (IntPtr)(long)delay.TotalMilliseconds), "the exit hold was not set");

    [DllImport("kernel32.dll")]
    private static extern uint FlsAlloc(IntPtr callback);

    [DllImport("kernel32.dll")]
    private static extern bool FlsSetValue(uint slot, IntPtr value);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string moduleName);
}