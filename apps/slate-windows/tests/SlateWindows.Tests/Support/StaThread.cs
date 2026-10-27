// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// The one way a unit fact runs WPF work on a thread of its own: a fresh
/// STA thread, and once it has ended, the window classes it registered are
/// unregistered. Every per-class <c>RunSta</c>/<c>OnSta</c> helper forwards
/// here, and <c>StaThreadCensus</c> keeps it that way.
/// </summary>
/// <remarks>
/// <para>
/// Why. When a thread ends, Windows destroys its windows but NOT the window
/// classes registered for them. Every WPF <c>HwndWrapper</c> — a
/// Dispatcher's message window, its MediaContext's notification window,
/// each HwndSource, a hidden taskbar owner — registers a uniquely named
/// class that only a WPF-side dispose unregisters, and a fact's thread ends
/// without one: two classes leak per fact that only builds visuals, six per
/// fact that shows a window, for the life of the test process. Each holds
/// roughly 270 bytes of the DESKTOP HEAP. CI's session is non-interactive,
/// so its desktop heap is 768 KB rather than the interactive desktop's
/// 20 MB, and once a few thousand leaked classes fill it every later
/// <c>CreateWindowEx</c> fails — "The operation completed successfully" or
/// "Not enough memory resources are available" out of
/// <c>MediaContext..ctor</c>, <c>Window.SetTaskbarStatus</c> or
/// <c>Dispatcher..ctor</c> — and every later hosted fact fails with it.
/// </para>
/// <para>
/// How. WPF names each class <c>HwndWrapper[app;thread;guid]</c>, so every
/// fact's thread gets a name of its own, and after the thread has ended —
/// at the OS level, which is when the system destroys its windows, and
/// which a managed Join does not always wait for (<see cref="OsThreadExit"/>)
/// — every class carrying that name is unregistered. A class that will not
/// unregister fails the fact, naming it and any window that still has it. Nothing runs on the fact's thread after its body: tearing its
/// windows down while the thread lives (a dispatcher shutdown disposes
/// them) hands the foreground to another process's window, after which the
/// next fact's <c>Window.Activate</c> is refused and every focus-dependent
/// fact after it fails (measured: ReadingFocusTests, 6–9 of 21 facts),
/// whereas a thread that simply ends leaves the foreground for the next
/// fact to take. The fact's thread therefore ends exactly as it always has.
/// </para>
/// <para>
/// Bounded and fail-fast. The thread is a background thread, so a wedged
/// body can never hold the test host open past the run. On timeout its
/// token is cancelled (<see cref="TimedOut"/>), so a body that polls it
/// between phases stops there, and its dispatcher is told to shut down, so
/// a pump wedged in a frame unwinds through the body's own disposals; the
/// fact fails here instead of at the job's limit. A body that unwinds
/// within <see cref="UnwindGrace"/> still has its classes unregistered; one
/// that does not is running beside every later fact, so
/// <see cref="LeakedDispatcherGuardAttribute"/> fails each of them
/// (<see cref="Wedged"/>, which names the fact it came from, #1321) until
/// it ends, and then frees its classes.
/// </para>
/// </remarks>
internal static class StaThread
{
    /// <summary>The bound a caller that names none gets.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    /// <summary>How long a timed-out body gets to unwind once its dispatcher
    /// has been told to shut down.</summary>
    internal static readonly TimeSpan UnwindGrace = TimeSpan.FromSeconds(10);

    /// <summary>Every fact thread's name starts with this; the rest is a
    /// sequence number, so the thread's window classes are its alone.</summary>
    internal const string ThreadNamePrefix = "sta-fact-";

    /// <summary>How long a thread that has ended in managed code gets to
    /// finish its OS-level exit (which destroys its windows) before its
    /// classes are freed.</summary>
    internal static readonly TimeSpan OsExitBound = TimeSpan.FromSeconds(10);

    private static readonly List<(string Name, Thread Thread, Func<OsThreadExit?> Exit, string Fact, string What)> s_wedged = [];

    private static int s_sequence;

    /// <summary>The running body's timeout (#1321): cancelled once its bound
    /// has passed, so a body that polls it between phases stops there and
    /// its timeout is one failure, not every fact beside it. Outside a body
    /// it is <see cref="CancellationToken.None"/>.</summary>
    internal static CancellationToken Current => t_timeout;

    /// <summary>Whether the running body's bound has passed: see
    /// <see cref="Current"/>.</summary>
    internal static bool TimedOut => t_timeout.IsCancellationRequested;

    /// <summary>The body's own copy of its run's token. The joining caller
    /// owns the source and cancels it; the slot holds only the token, so the
    /// cancel reaches the body and never the caller's own empty slot.</summary>
    [ThreadStatic]
    private static CancellationToken t_timeout;

    /// <summary>Test seam: runs between the thread's start and the runner's
    /// Join, so a witness can make that Join late.</summary>
    internal static Action? AfterStartForTests { get; set; }

    /// <summary>Test seam: runs on the fact's thread before its first line,
    /// so a witness can make the body start after its joins have expired.</summary>
    internal static Action? BeforeBodyForTests { get; set; }

    /// <summary>Run <paramref name="body"/> on a fresh STA thread, unregister
    /// the window classes it leaves behind, and rethrow whatever it threw.</summary>
    internal static void Run(
        Action body, TimeSpan? timeout = null, string? timeoutMessage = null, TimeSpan? unwindGrace = null) =>
        Run<object?>(
            () =>
            {
                body();
                return null;
            },
            timeout,
            timeoutMessage,
            unwindGrace);

    /// <summary><see cref="Run(Action, TimeSpan?, string?, TimeSpan?)"/> under
    /// <see cref="PumpedDispatcher.Run"/>: the body's documents capture the
    /// thread's dispatcher as their owner context.</summary>
    internal static void RunPumped(Action body, TimeSpan? timeout = null, string? timeoutMessage = null) =>
        Run(() => PumpedDispatcher.Run(body), timeout, timeoutMessage);

    /// <summary>Run <paramref name="body"/> on a fresh STA thread and answer
    /// its result once the window classes it leaves behind are unregistered.
    /// <paramref name="unwindGrace"/> overrides <see cref="UnwindGrace"/>.</summary>
    internal static T Run<T>(
        Func<T> body, TimeSpan? timeout = null, string? timeoutMessage = null, TimeSpan? unwindGrace = null)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        Thread? owner = null;
        OsThreadExit? exit = null;
        string name = ThreadNamePrefix + Interlocked.Increment(ref s_sequence).ToString(CultureInfo.InvariantCulture);
        // Read here, on the caller's thread, where xUnit keeps the running
        // test: the helpers that forward to this runner would all be named
        // by a caller-member name.
        string fact = TestContext.Current.Test?.TestDisplayName ?? "a caller outside a test";
        using var cancellation = new CancellationTokenSource();
        // The token is taken here, before the thread starts: a thread that
        // first runs after a wedged run has disposed the source would throw
        // reading the source's token, outside the body, and end the process.
        CancellationToken token = cancellation.Token;
        Action? beforeBody = BeforeBodyForTests;
        var thread = new Thread(() =>
        {
            beforeBody?.Invoke();
            t_timeout = token;
            Volatile.Write(ref owner, Thread.CurrentThread);
            try
            {
                Volatile.Write(ref exit, OsThreadExit.OfCurrentThread());
                result = body();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = name,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        AfterStartForTests?.Invoke();
        TimeSpan limit = timeout ?? DefaultTimeout;
        if (!thread.Join(limit))
        {
            Dispatcher? dispatcher = Volatile.Read(ref owner) is { } started ? Dispatcher.FromThread(started) : null;
            (string stack, bool taken) = StackOf(thread, dispatcher);
            if (!taken)
            {
                stack += DumpOnTimeout(name);
            }
            // After the stack and the dump, so a body that stops on its token
            // has not left before either is taken.
            string cancelled = Cancel(cancellation);
            dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
            string what = $"{timeoutMessage ?? "The STA fact timed out."} (thread {name}, bound {limit.TotalSeconds:0.#} s; its timeout token was cancelled{cancelled} and its dispatcher was told to shut down";
            if (thread.Join(unwindGrace ?? UnwindGrace))
            {
                string? left = FreeClasses(name, Volatile.Read(ref exit));
                throw new Xunit.Sdk.XunitException(what + " and the body unwound" + (left is null ? ".) " : "; " + left + ") ") + stack);
            }
            lock (s_wedged)
            {
                // Its exit is read when asked: a thread that first runs after
                // its joins have expired has recorded none yet.
                s_wedged.Add((name, thread, () => Volatile.Read(ref exit), fact, timeoutMessage ?? "an STA fact that timed out"));
            }
            throw new Xunit.Sdk.XunitException(
                what + ", but the body has not unwound. It runs on beside the facts after this one, so each of them fails until it ends.) " + stack);
        }
        string? residue = FreeClasses(name, Volatile.Read(ref exit));
        failure?.Throw();
        if (residue is not null)
        {
            throw new Xunit.Sdk.XunitException(residue);
        }
        return result;
    }

    /// <summary>Cancel a timed-out body's token, answering what a callback on
    /// it threw (empty when none did): a callback that throws never keeps the
    /// dispatcher from being told to shut down.</summary>
    private static string Cancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
            return string.Empty;
        }
        catch (AggregateException thrown)
        {
            return $", though a callback on it threw {thrown.InnerException?.GetType().Name}: {thrown.InnerException?.Message},";
        }
    }

    /// <summary>The folder a timed-out body's dump goes to (#1603): CI's
    /// app job names its uploaded results folder through
    /// <c>SLATE_STA_DUMP_DIR</c>; null writes none.</summary>
    internal static string? DumpDirectory { get; set; } = Environment.GetEnvironmentVariable("SLATE_STA_DUMP_DIR");

    /// <summary>At most this many dumps per test process: one wedged body
    /// fails every fact beside it, and a full dump is several times the
    /// process's private memory.</summary>
    internal const int MaxDumps = 2;

    private static int s_dumps;

    internal static void ResetDumpsForTests() => Volatile.Write(ref s_dumps, 0);

    /// <summary>How long createdump gets to end (#1641). While it writes,
    /// every thread of this process is suspended, the one waiting here
    /// included, so the bound cannot stop a write midway: a wait that ends
    /// with a dump already on disk gets a second bound, and a write that
    /// hangs is stopped only by the job's blame-hang timeout.</summary>
    internal static readonly TimeSpan DumpBound = TimeSpan.FromMinutes(2);

    /// <summary>How long createdump's redirected output gets to be read to
    /// its end once it has exited.</summary>
    private static readonly TimeSpan DumpOutputBound = TimeSpan.FromSeconds(5);

    /// <summary>Test seam (#1641): runs in place of the dumper's process,
    /// answering its exit code (null when it ran past
    /// <see cref="DumpBound"/> and was stopped) and what it wrote.</summary>
    internal static Func<System.Diagnostics.ProcessStartInfo, (int? ExitCode, string Said)>? RunDumperForTests { get; set; }

    /// <summary>Test seam (#1641): the dump createdump writes, by its option's
    /// name (<c>normal</c>, <c>withheap</c>, <c>triage</c>, <c>full</c>);
    /// null is <c>full</c>. The real-createdump fact writes a normal one: a
    /// full dump of the test host mid-suite was 4.7 GB on CI.</summary>
    internal static string? DumpKindForTests { get; set; }

    /// <summary>Write a full dump of the test process into
    /// <see cref="DumpDirectory"/>, answering the sentence that names it
    /// (empty when no folder is named or the cap is reached). The dump keeps
    /// every thread, module and byte of memory a debugger needs to walk
    /// managed stacks (<c>dotnet-dump analyze</c>, then <c>clrstack -all</c>).
    /// A failure is a sentence too: a diagnostic never changes the timeout's
    /// path (the dispatcher's shutdown, the unwind join, the class cleanup).</summary>
    /// <remarks>#1641: the runtime's own createdump writes it, from outside
    /// this process. On Windows createdump dumps the process that started
    /// it and takes no process id (dotnet/runtime#68877, .NET 7), so it is
    /// started directly, never through a shell, which would become its
    /// parent. A process that dumped itself (MiniDumpWriteDump on its own
    /// handle) raced its own running threads: CI's dump came out partial,
    /// 2 MB with error 0x80070057. The dump is <c>--full</c>, not
    /// <c>--withheap</c>: a heap dump leans on the runtime's registration
    /// to include JIT code and on matching binaries for the modules, and
    /// CI's runtime is installed unregistered by dotnet-install. A full dump
    /// carries both, and writes faster.</remarks>
    private static string DumpOnTimeout(string name)
    {
        if (DumpDirectory is not { Length: > 0 } folder || Interlocked.Increment(ref s_dumps) > MaxDumps)
        {
            return string.Empty;
        }
        try
        {
            return WriteDump(folder, name);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $" No dump was written: {exception.GetType().Name}: {exception.Message}";
        }
    }

    private static string WriteDump(string folder, string name)
    {
        string path = Path.Combine(folder, $"{name}-{DateTime.UtcNow:yyyyMMddTHHmmss}.dmp");
        // createdump opens the file through an ANSI path and reads % as a
        // specifier (%p, %e, %h, %t): an ASCII path, with % doubled.
        if (!path.All(char.IsAscii))
        {
            return $" No dump was written: createdump needs an ASCII path, and {path} is not one.";
        }
        var start = new System.Diagnostics.ProcessStartInfo(
            Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "createdump.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string kind = DumpKindForTests ?? "full";
        start.ArgumentList.Add($"--{kind}");
        start.ArgumentList.Add("--name");
        start.ArgumentList.Add(path.Replace("%", "%%", StringComparison.Ordinal));
        long privateBytes;
        using (System.Diagnostics.Process self = System.Diagnostics.Process.GetCurrentProcess())
        {
            privateBytes = self.PrivateMemorySize64;
        }
        string memory = $"{privateBytes / (1024 * 1024)} MB private";
        Directory.CreateDirectory(folder);
        (int? exitCode, string said) = RunDumperForTests is { } fake ? fake(start) : RunDumper(start, path);
        long length = File.Exists(path) ? new FileInfo(path).Length : 0;
        if (exitCode is null)
        {
            return length > 0
                ? $" createdump ran past its {DumpBound.TotalSeconds:0} s bound and was stopped; what it wrote is at {path} ({length} bytes, {memory})."
                : $" No dump was written: createdump ran past its {DumpBound.TotalSeconds:0} s bound and was stopped.";
        }
        string outcome = said.Length > 0 ? $"createdump exited {exitCode}: {said}" : $"createdump exited {exitCode}";
        if (exitCode != 0)
        {
            // A dump createdump reports as failed is kept when it has
            // content, and named with the exit code and what it said (CI on
            // 3d4aa90d deleted one and said nothing).
            return length > 0
                ? $" A partial dump of the test process is at {path} ({length} bytes, {memory}; {outcome})."
                : $" No dump was written: {outcome}.";
        }
        if (length == 0)
        {
            return $" No dump was written: {outcome}, but nothing is at {path}.";
        }
        return said.Length > 0
            ? $" A {kind} dump of the test process is at {path} ({memory}; {said})."
            : $" A {kind} dump of the test process is at {path} ({memory}).";
    }

    /// <summary>Run createdump to its end, answering its exit code (null
    /// when it was stopped) and its output on one line.</summary>
    private static (int? ExitCode, string Said) RunDumper(System.Diagnostics.ProcessStartInfo start, string path)
    {
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("createdump did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(DumpBound)
            && !(File.Exists(path) && new FileInfo(path).Length > 0 && process.WaitForExit(DumpBound)))
        {
            // createdump has no children; Kill returns quietly if it has
            // just ended. Its reads end with its pipes: drained briefly, and
            // a read that faults is observed, never left unobserved.
            process.Kill();
            try
            {
                _ = Task.WaitAll([output, errors], DumpOutputBound);
            }
            catch (AggregateException)
            {
            }
            return (null, string.Empty);
        }
        // The reads finish once createdump has closed its end of the pipes;
        // bounded, so a stuck read cannot hold the timeout's report.
        try
        {
            if (!Task.WaitAll([output, errors], DumpOutputBound))
            {
                return (process.ExitCode, "(its output was not read to its end)");
            }
        }
        catch (AggregateException failure)
        {
            return (process.ExitCode, $"(its output could not be read: {failure.InnerException?.Message})");
        }
        string said = string.Join(
            " ",
            (output.Result + "\n" + errors.Result)
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return (process.ExitCode, said);
    }

    /// <summary>How long a timed-out body gets to pump the operation that
    /// takes its stack.</summary>
    internal static readonly TimeSpan StackBound = TimeSpan.FromSeconds(5);

    /// <summary>#1603: where a timed-out body is. The runtime cannot read
    /// another thread's managed stack, so the stack is taken ON the body's
    /// thread, by an operation at the highest priority on its dispatcher:
    /// a body still pumping — a wait loop that drains its dispatcher — runs
    /// it at its next pump. A body that does not pump within
    /// <see cref="StackBound"/>, or has no dispatcher, is reported as such.</summary>
    private static (string Text, bool Taken) StackOf(Thread thread, Dispatcher? dispatcher)
    {
        if (dispatcher is null)
        {
            return ("Its stack could not be taken: the body has no dispatcher to pump.", false);
        }
        string? stack = null;
        _ = dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            () => Volatile.Write(ref stack, BelowThePump(new System.Diagnostics.StackTrace(1, true).ToString())));
        _ = SpinWait.SpinUntil(() => Volatile.Read(ref stack) is not null || !thread.IsAlive, StackBound);
        return Volatile.Read(ref stack) is { } taken
            ? ("Its stack while pumping:" + Environment.NewLine + taken, true)
            : ($"Its stack could not be taken: the body did not pump its dispatcher within {StackBound.TotalSeconds:0} s.", false);
    }

    /// <summary>The stack without the dispatcher's own frames above the
    /// body's pump — WPF's queue, the message loop and the execution
    /// context — so the body's frames come first.</summary>
    private static string BelowThePump(string stack) =>
        string.Join(
            Environment.NewLine,
            stack.Split(Environment.NewLine)
                .SkipWhile(line => line.TrimStart() is var frame
                    && (frame.StartsWith("at System.Windows.Threading.", StringComparison.Ordinal)
                        || frame.StartsWith("at MS.Win32.", StringComparison.Ordinal)
                        || frame.StartsWith("at MS.Internal.", StringComparison.Ordinal)
                        || frame.StartsWith("at System.Threading.ExecutionContext.", StringComparison.Ordinal))));

    /// <summary>The timed-out fact threads still running (null when none
    /// are): a fact that runs beside one is not isolated, so the leak guard
    /// fails it. A wedged thread that has since ended is dropped here, and
    /// its window classes unregistered.</summary>
    internal static string? Wedged()
    {
        var running = new List<string>();
        var residue = new List<string>();
        lock (s_wedged)
        {
            for (int i = s_wedged.Count - 1; i >= 0; i--)
            {
                (string name, Thread thread, Func<OsThreadExit?> exitOf, string fact, string what) = s_wedged[i];
                OsThreadExit? exit = exitOf();
                // A thread that has recorded no exit yet is running while it
                // is alive; one that has, until its OS thread has exited.
                if (exit is null ? thread.IsAlive : !exit.WaitOne(0))
                {
                    running.Add($"`{fact}` (thread {name}) is still unwinding after its timeout: {what.TrimEnd('.')}");
                    continue;
                }
                s_wedged.RemoveAt(i);
                if (FreeClasses(name, exit) is { } left)
                {
                    residue.Add(left);
                }
            }
        }
        if (running.Count == 0 && residue.Count == 0)
        {
            return null;
        }
        return string.Join(
            " ",
            (running.Count == 0 ? [] : new[] { $"This fact failed because {string.Join("; ", running)}. A timed-out body still running beside this fact means it is not isolated." })
                .Concat(residue));
    }

    /// <summary>Once the thread named <paramref name="name"/> has exited at
    /// the OS level — not merely in managed code; see
    /// <see cref="OsThreadExit"/> — unregister its window classes, answering
    /// a description of what could not be freed (null when all were).</summary>
    private static string? FreeClasses(string name, OsThreadExit? exit)
    {
        using (exit)
        {
            if (exit is null)
            {
                return $"Thread {name} never recorded its OS exit, so its window classes were not freed.";
            }
            if (!exit.WaitOne(OsExitBound))
            {
                return $"Thread {name} ended in managed code, but its OS thread had not exited {OsExitBound.TotalSeconds:0} s later, so its window classes were not freed.";
            }
        }
        return WindowClasses.Unregister(name);
    }
}