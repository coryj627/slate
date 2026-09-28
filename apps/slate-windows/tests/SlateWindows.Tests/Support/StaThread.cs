// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// The one way a unit fact runs WPF work on a thread of its own: a fresh
/// STA thread whose WPF resources are torn down before it ends. Every
/// per-class <c>RunSta</c>/<c>OnSta</c> helper forwards here, and
/// <c>StaThreadCensus</c> keeps it that way.
/// </summary>
/// <remarks>
/// <para>
/// Why a teardown. When a thread ends, Windows destroys its windows but
/// NOT the window classes registered for them. Every WPF
/// <c>HwndWrapper</c> — a Dispatcher's message window, its MediaContext's
/// notification window, each HwndSource, a hidden taskbar owner — registers
/// a uniquely named class that only a WPF-side dispose unregisters. A
/// thread that ends without shutting its Dispatcher down leaks two classes
/// (a Dispatcher and one visual) to six (a shown Window never closed) for
/// the life of the test process, and each class holds roughly 270 bytes of
/// the DESKTOP HEAP. CI's session is non-interactive: its desktop heap is
/// 768 KB, not the interactive desktop's 20 MB. Once the leaked classes
/// fill it, every later <c>CreateWindowEx</c> fails — "The operation
/// completed successfully" or "Not enough memory resources are available"
/// from <c>MediaContext..ctor</c>, <c>Window.SetTaskbarStatus</c> or
/// <c>Dispatcher..ctor</c> — and every later hosted fact fails with it.
/// </para>
/// <para>
/// The teardown runs on the fact's own thread whatever the body did: each
/// WPF window still open has its HwndSource disposed, the dispatcher
/// drains so the class unregistrations those disposals post can run, and
/// the dispatcher shuts down, which disposes its own wrappers. (Shutting
/// down alone is not enough: a window the shutdown destroys posts its
/// class unregistration to a dispatcher that no longer runs anything.)
/// Then the guard: a WPF window that outlives the teardown, or a
/// dispatcher that did not finish shutting down, fails the fact.
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

    /// <summary>Run <paramref name="body"/> on a fresh STA thread, tear its
    /// WPF resources down, and rethrow whatever it threw.</summary>
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
    /// its result once its WPF resources are torn down.</summary>
    internal static T Run<T>(Func<T> body, TimeSpan? timeout = null, string? timeoutMessage = null)
    {
        T result = default!;
        ExceptionDispatchInfo? failure = null;
        string? residue = null;
        Thread? owner = null;
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
            finally
            {
                residue = TearDown();
            }
        })
        {
            IsBackground = true,
            Name = "sta-fact",
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
        failure?.Throw();
        if (residue is not null)
        {
            throw new Xunit.Sdk.XunitException(residue);
        }
        return result;
    }

    /// <summary>On the fact's thread: close what the body left open, let
    /// the posted class unregistrations run, shut the dispatcher down, and
    /// answer a description of anything that survived (null when clean).</summary>
    private static string? TearDown()
    {
        var problems = new List<string>();
        Dispatcher? dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
        if (dispatcher is { HasShutdownStarted: false })
        {
            // Disposed, not Closed: a Window's Closing and Closed handlers
            // are application logic (MainWindow's saves its placement to
            // the user's profile and may prompt), and a teardown must not
            // run what the fact never asked for. Destroying the source
            // raises neither.
            foreach (IntPtr hwnd in WpfWindows())
            {
                try
                {
                    if (HwndSource.FromHwnd(hwnd) is { IsDisposed: false } source)
                    {
                        source.Dispose();
                    }
                }
                catch (Exception exception)
                {
                    // The shutdown below still disposes it; the survivor
                    // check reports it if even that fails.
                    problems.Add($"disposing {Describe(hwnd)} threw {exception.GetType().Name}: {exception.Message}");
                }
            }
            try
            {
                PumpedDispatcher.Drain();
            }
            catch (Exception exception)
            {
                problems.Add($"the teardown drain failed: {exception.GetType().Name}: {exception.Message}");
            }
            try
            {
                dispatcher.InvokeShutdown();
            }
            catch (Exception exception)
            {
                problems.Add($"the dispatcher's shutdown threw {exception.GetType().Name}: {exception.Message}");
            }
        }
        if (dispatcher is { HasShutdownFinished: false })
        {
            problems.Add("the dispatcher did not finish shutting down");
        }
        problems.AddRange(WpfWindows().Select(hwnd => $"{Describe(hwnd)} is still open"));
        return problems.Count == 0
            ? null
            : "The STA fact's teardown left WPF resources alive — each one leaks desktop heap for the rest of the run: "
                + string.Join("; ", problems);
    }

    /// <summary>This thread's top-level WPF windows (every HwndWrapper
    /// class is named <c>HwndWrapper[…]</c>); system windows such as the
    /// thread's IME window are not the fact's to close.</summary>
    private static List<IntPtr> WpfWindows()
    {
        var windows = new List<IntPtr>();
        _ = EnumThreadWindows(GetCurrentThreadId(), (hwnd, _) =>
        {
            var name = new StringBuilder(256);
            if (GetClassNameW(hwnd, name, name.Capacity) > 0
                && name.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal))
            {
                windows.Add(hwnd);
            }
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    private static string Describe(IntPtr hwnd)
    {
        string root = HwndSource.FromHwnd(hwnd)?.RootVisual?.GetType().Name ?? "no root visual";
        var title = new StringBuilder(256);
        _ = GetWindowTextW(hwnd, title, title.Capacity);
        return $"window 0x{hwnd:X} ({root}, \"{title}\")";
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
