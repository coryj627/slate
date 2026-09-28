// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
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
/// its windows destroyed by the system — every class carrying that name is
/// unregistered. A class that will not unregister fails the fact, naming
/// it. Nothing runs on the fact's thread after its body: tearing its
/// windows down while the thread lives (a dispatcher shutdown disposes
/// them) hands the foreground to another process's window, after which the
/// next fact's <c>Window.Activate</c> is refused and every focus-dependent
/// fact after it fails (measured: ReadingFocusTests, 6–9 of 21 facts),
/// whereas a thread that simply ends leaves the foreground for the next
/// fact to take. The fact's thread therefore ends exactly as it always has.
/// </para>
/// <para>
/// Bounded and fail-fast. The thread is a background thread, so a wedged
/// body can never hold the test host open past the run; on timeout its
/// dispatcher is shut down so a pump wedged in a frame unwinds through the
/// body's own disposals, and the fact fails here instead of at the job's
/// limit.
/// </para>
/// </remarks>
internal static class StaThread
{
    /// <summary>The bound a caller that names none gets.</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Every fact thread's name starts with this; the rest is a
    /// sequence number, so the thread's window classes are its alone.</summary>
    internal const string ThreadNamePrefix = "sta-fact-";

    private static int s_sequence;

    /// <summary>Run <paramref name="body"/> on a fresh STA thread, unregister
    /// the window classes it leaves behind, and rethrow whatever it threw.</summary>
    internal static void Run(Action body, TimeSpan? timeout = null, string? timeoutMessage = null) =>
        Run<object?>(
            () =>
            {
                body();
                return null;
            },
            timeout,
            timeoutMessage);

    /// <summary><see cref="Run(Action, TimeSpan?, string?)"/> under
    /// <see cref="PumpedDispatcher.Run"/>: the body's documents capture the
    /// thread's dispatcher as their owner context.</summary>
    internal static void RunPumped(Action body, TimeSpan? timeout = null, string? timeoutMessage = null) =>
        Run(() => PumpedDispatcher.Run(body), timeout, timeoutMessage);

    /// <summary>Run <paramref name="body"/> on a fresh STA thread and answer
    /// its result once the window classes it leaves behind are unregistered.</summary>
    internal static T Run<T>(Func<T> body, TimeSpan? timeout = null, string? timeoutMessage = null)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        Thread? owner = null;
        string name = ThreadNamePrefix + Interlocked.Increment(ref s_sequence).ToString(CultureInfo.InvariantCulture);
        var thread = new Thread(() =>
        {
            Volatile.Write(ref owner, Thread.CurrentThread);
            try
            {
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
        TimeSpan limit = timeout ?? DefaultTimeout;
        if (!thread.Join(limit))
        {
            if (Volatile.Read(ref owner) is { } started)
            {
                Dispatcher.FromThread(started)?.BeginInvokeShutdown(DispatcherPriority.Send);
            }
            bool unwound = thread.Join(TimeSpan.FromSeconds(10));
            throw new Xunit.Sdk.XunitException(
                $"{timeoutMessage ?? "The STA fact timed out."} (bound {limit.TotalSeconds:0} s; its dispatcher was shut down"
                + (unwound ? " and the body unwound.)" : ", but the body has not unwound — a background thread, so it cannot hold the test host open.)"));
        }
        string? residue = UnregisterWindowClasses(name);
        failure?.Throw();
        if (residue is not null)
        {
            throw new Xunit.Sdk.XunitException(residue);
        }
        return result;
    }

    /// <summary>Unregister every WPF window class the ended thread
    /// <paramref name="threadName"/> registered, answering a description of
    /// any that would not go (null when none remain). The class names live
    /// in the session's atom table, which <c>GetClipboardFormatName</c>
    /// reads; the classes are this process's, registered under its module.</summary>
    internal static string? UnregisterWindowClasses(string threadName)
    {
        IntPtr module = GetModuleHandleW(IntPtr.Zero);
        string marker = ";" + threadName + ";";
        var refused = new List<string>();
        var name = new StringBuilder(512);
        for (uint atom = 0xC000; atom <= 0xFFFF; atom++)
        {
            name.Clear();
            if (GetClipboardFormatNameW(atom, name, name.Capacity) <= 0)
            {
                continue;
            }
            string className = name.ToString();
            if (!className.StartsWith("HwndWrapper[", StringComparison.Ordinal)
                || !className.Contains(marker, StringComparison.Ordinal)
                || UnregisterClassW(className, module))
            {
                continue;
            }
            int error = Marshal.GetLastWin32Error();
            // ERROR_CLASS_DOES_NOT_EXIST: another test host's class of the
            // same name, or one already gone — nothing of this process's to free.
            if (error != 1411)
            {
                refused.Add($"{className} ({new Win32Exception(error).Message})");
            }
        }
        return refused.Count == 0
            ? null
            : $"{refused.Count} window class(es) the fact's thread registered would not unregister after it ended — each holds desktop heap for the rest of the run: "
                + string.Join("; ", refused);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClipboardFormatNameW(uint format, StringBuilder name, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(IntPtr moduleName);
}
