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

    /// <summary>These facts time out on purpose: none of them writes CI's
    /// minidump unless it names a folder of its own.</summary>
    public StaThreadTests()
    {
        StaThread.DumpDirectory = null;
        StaThread.ResetDumpsForTests();
    }

    public void Dispose()
    {
        StaThread.DumpDirectory = _dumpDirectory;
        StaThread.ResetDumpsForTests();
    }

    /// <summary>#1603: a body that does not pump gives no stack, so with a
    /// dump folder named (CI's app job names its results folder) the timeout
    /// writes a dump of the process there and says where. #1641: the dump is
    /// the runtime's createdump's, taken from outside the process, and it is
    /// full: it lists the timed-out body's thread with its stack, and it
    /// holds the bytes of an object on the managed heap. A dump the process
    /// took of itself came out partial on CI (2 MB, error 0x80070057).</summary>
    [Fact]
    public void ATimedOutBodyThatDoesNotPumpLeavesAFullDumpWhereTheFolderSays()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"sta-dump-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        StaThread.DumpDirectory = folder;
        byte[] marker = System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
        GCHandle pinned = GCHandle.Alloc(marker, GCHandleType.Pinned);
        uint bodyThread = 0;
        try
        {
            Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
                () =>
                {
                    Volatile.Write(ref bodyThread, GetCurrentThreadId());
                    Thread.Sleep(TimeSpan.FromMilliseconds(600));
                },
                TimeSpan.FromMilliseconds(200),
                "the body only slept",
                unwindGrace: TimeSpan.FromSeconds(30)));

            // The message first: a write that fails says why (CI on 3d4aa90d
            // failed here with an empty folder and no message to read).
            Assert.Contains("A full dump of the test process is at ", timedOut.Message, StringComparison.Ordinal);
            string dump = Assert.Single(Directory.GetFiles(folder, "*.dmp"));
            Assert.Contains($"A full dump of the test process is at {dump} (", timedOut.Message, StringComparison.Ordinal);
            Assert.Contains(" MB private", timedOut.Message, StringComparison.Ordinal);
            Minidump read = Minidump.Read(dump, pinned.AddrOfPinnedObject(), marker.Length);
            Assert.True(
                read.ThreadStacks.TryGetValue(Volatile.Read(ref bodyThread), out uint stack) && stack > 0,
                $"the dump lists the body's thread {bodyThread} with its stack");
            Assert.Equal(marker, read.Marker);
        }
        finally
        {
            pinned.Free();
            Directory.Delete(folder, recursive: true);
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
            return (1, "[createdump] Writing minidump FAILED 8007012B");
        };
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            string dump = Assert.Single(Directory.GetFiles(folder, "*.dmp"));
            Assert.Contains($"A partial dump of the test process is at {dump} (4 bytes, ", timedOut.Message, StringComparison.Ordinal);
            Assert.Contains(
                " MB private; createdump exited 1: [createdump] Writing minidump FAILED 8007012B).",
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
        StaThread.RunDumperForTests = _ => (2, "[createdump] The pid argument is no longer supported");
        try
        {
            Xunit.Sdk.XunitException timedOut = TimeOutASleepingBody();

            Assert.Empty(Directory.GetFiles(folder, "*.dmp"));
            Assert.Contains(
                "No dump was written: createdump exited 2: [createdump] The pid argument is no longer supported.",
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
    /// MINIDUMP_DIRECTORY per stream): each listed thread's stack size
    /// (ThreadListStream, 3), and the bytes at one address, found in the
    /// memory a heap dump lists (MemoryListStream, 5) or a full one does
    /// (Memory64ListStream, 9). Null when no range holds them whole.</summary>
    private sealed record Minidump(IReadOnlyDictionary<uint, uint> ThreadStacks, byte[]? Marker)
    {
        public static Minidump Read(string path, IntPtr address, int length)
        {
            using FileStream stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            Assert.Equal("MDMP", System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4)));
            _ = reader.ReadUInt32();
            uint streams = reader.ReadUInt32();
            uint directory = reader.ReadUInt32();
            ulong wanted = (ulong)address.ToInt64();
            var threads = new Dictionary<uint, uint>();
            byte[]? marker = null;
            for (uint index = 0; index < streams; index++)
            {
                stream.Position = directory + (index * 12L);
                uint type = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                uint rva = reader.ReadUInt32();
                stream.Position = rva;
                if (type == 3)
                {
                    // MINIDUMP_THREAD: id, suspend count, priority class,
                    // priority, TEB, then the stack's descriptor.
                    uint count = reader.ReadUInt32();
                    for (uint thread = 0; thread < count; thread++)
                    {
                        long entry = rva + 4 + (thread * 48L);
                        stream.Position = entry;
                        uint id = reader.ReadUInt32();
                        stream.Position = entry + 24 + 8;
                        threads[id] = reader.ReadUInt32();
                    }
                }
                else if (type == 5)
                {
                    uint count = reader.ReadUInt32();
                    for (uint range = 0; range < count && marker is null; range++)
                    {
                        stream.Position = rva + 4 + (range * 16L);
                        ulong start = reader.ReadUInt64();
                        uint size = reader.ReadUInt32();
                        uint data = reader.ReadUInt32();
                        marker = Within(stream, reader, start, size, data, wanted, length);
                    }
                }
                else if (type == 9)
                {
                    ulong count = reader.ReadUInt64();
                    long data = (long)reader.ReadUInt64();
                    for (ulong range = 0; range < count && marker is null; range++)
                    {
                        stream.Position = rva + 16 + ((long)range * 16L);
                        ulong start = reader.ReadUInt64();
                        ulong size = reader.ReadUInt64();
                        marker = Within(stream, reader, start, size, data, wanted, length);
                        data += (long)size;
                    }
                }
            }
            return new Minidump(threads, marker);
        }

        private static byte[]? Within(
            FileStream stream, BinaryReader reader, ulong start, ulong size, long data, ulong wanted, int length)
        {
            if (wanted < start || wanted + (ulong)length > start + size)
            {
                return null;
            }
            stream.Position = data + (long)(wanted - start);
            return reader.ReadBytes(length);
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