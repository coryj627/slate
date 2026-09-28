// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// The WPF window classes this test process has registered — what a hosted
/// fact leaks into the desktop heap when its thread ends without WPF
/// disposing its <c>HwndWrapper</c>s (see <see cref="StaThread"/>).
/// </summary>
/// <remarks>
/// WPF registers one class per <c>HwndWrapper</c>, named
/// <c>HwndWrapper[&lt;app&gt;;&lt;thread name&gt;;&lt;guid&gt;]</c>. Every
/// registered class name is an atom in the session's user atom table
/// (0xC000–0xFFFF), which <c>GetClipboardFormatName</c> reads; the table is
/// shared with every other process in the session — a concurrent test host
/// registers the same prefix — so a name counts only when
/// <c>GetClassInfoEx</c> finds it registered against this process's module,
/// the instance WPF registers under. Each name is asked about once (see
/// the cache below), so a scan stays about 4 ms however many classes other
/// processes have leaked into the table.
/// </remarks>
internal static class WindowClasses
{
    private const string Prefix = "HwndWrapper[";

    /// <summary>ERROR_CLASS_DOES_NOT_EXIST.</summary>
    private const int ClassDoesNotExist = 1411;

    // Every WPF class name carries a fresh GUID, so a name belongs to one
    // process for good: asked once, remembered. Another process's names — a
    // concurrent test host without this runner leaks thousands into the
    // shared table — are never asked about again (at ~12 µs a query they
    // made every scan tens of milliseconds); this process's stay registered
    // exactly as long as their atom stays in the table, since unregistering
    // a class releases its atom.
    private static readonly HashSet<string> s_ours = new(StringComparer.Ordinal);
    private static readonly HashSet<string> s_foreign = new(StringComparer.Ordinal);

    /// <summary>Every WPF window class this process has registered, by name.</summary>
    internal static HashSet<string> Registered()
    {
        IntPtr module = GetModuleHandleW(IntPtr.Zero);
        var registered = new HashSet<string>(StringComparer.Ordinal);
        lock (s_ours)
        {
            foreach (string name in WpfClassNames())
            {
                if (s_foreign.Contains(name))
                {
                    continue;
                }
                if (!s_ours.Contains(name))
                {
                    var info = new WindowClassInfo { Size = Marshal.SizeOf<WindowClassInfo>() };
                    _ = (GetClassInfoExW(module, name, ref info) != 0 ? s_ours : s_foreign).Add(name);
                }
                if (s_ours.Contains(name))
                {
                    registered.Add(name);
                }
            }
        }
        return registered;
    }

    /// <summary>The classes this process has registered for the thread
    /// named <paramref name="threadName"/>.</summary>
    internal static List<string> RegisteredFor(string threadName) =>
        [.. Registered().Where(name => ThreadOf(name) == threadName)];

    /// <summary>The name of the thread a WPF class was registered on ("" for
    /// an unnamed thread), or null for a name WPF did not form.</summary>
    internal static string? ThreadOf(string className)
    {
        if (!className.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }
        string[] parts = className[Prefix.Length..].Split(';');
        return parts.Length >= 3 ? parts[1] : null;
    }

    /// <summary>Unregister every class this process registered for the
    /// ENDED thread <paramref name="threadName"/> (the system destroyed its
    /// windows when it ended), answering a description of any that would not
    /// go, or null when none remain.</summary>
    internal static string? Unregister(string threadName)
    {
        IntPtr module = GetModuleHandleW(IntPtr.Zero);
        var refused = new List<string>();
        string[] candidates;
        lock (s_ours)
        {
            candidates = [.. WpfClassNames().Where(name => ThreadOf(name) == threadName && !s_foreign.Contains(name))];
        }
        foreach (string name in candidates)
        {
            if (UnregisterClassW(name, module))
            {
                continue;
            }
            int error = Marshal.GetLastWin32Error();
            // Another test host's class of the same name, or one already gone:
            // nothing of this process's to free.
            if (error != ClassDoesNotExist)
            {
                refused.Add($"{name} ({new Win32Exception(error).Message})");
            }
        }
        return refused.Count == 0
            ? null
            : $"{refused.Count} window class(es) thread \"{threadName}\" registered would not unregister after it ended — each holds desktop heap for the rest of the run: "
                + string.Join("; ", refused);
    }

    private static IEnumerable<string> WpfClassNames()
    {
        var names = new List<string>();
        var name = new StringBuilder(512);
        for (uint atom = 0xC000; atom <= 0xFFFF; atom++)
        {
            name.Clear();
            if (GetClipboardFormatNameW(atom, name, name.Capacity) > 0
                && name.ToString().StartsWith(Prefix, StringComparison.Ordinal))
            {
                names.Add(name.ToString());
            }
        }
        return names;
    }

    /// <summary>WNDCLASSEXW with its string members as raw pointers: the
    /// system writes them, and a marshalled string field would free memory
    /// it never allocated.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClassInfo
    {
        public int Size;
        public int Style;
        public IntPtr WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public IntPtr MenuName;
        public IntPtr ClassName;
        public IntPtr SmallIcon;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClipboardFormatNameW(uint format, StringBuilder name, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassInfoExW(IntPtr instance, string className, ref WindowClassInfo info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClassW(string className, IntPtr instance);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandleW(IntPtr moduleName);
}
