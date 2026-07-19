// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Windows app-log adapter for slate-core's host_logging stderr sink
// (w0_spec §W0-3 item 4, #715). The core sink writes native fd 2 by
// design (#507 defers per-platform bridges); a WinExe has no console, so
// without redirection every non-fatal diagnostic would be discarded.
// This thin host adapter points the process stderr handle at a durable
// log file before the sink is installed — platform I/O plumbing only,
// no logging policy (ADR 13 "C# may contain" list).

using System.IO;
using System.Runtime.InteropServices;

namespace SlateWindows;

internal static class HostLog
{
    private const int StdErrorHandle = -12; // STD_ERROR_HANDLE

    // DllImport rather than LibraryImport: the app project compiles
    // without AllowUnsafeBlocks, and one bool/IntPtr P/Invoke needs no
    // generated marshalling.
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    // Keeps the sink stream alive for the process lifetime; the OS handle
    // backs the redirected fd 2.
    private static FileStream? _sink;

    /// <summary>
    /// The app-log directory: SLATE_LOG_DIR when set (censuses point it
    /// at a temp dir), else %LOCALAPPDATA%\Slate\logs.
    /// </summary>
    public static string LogDirectory =>
        Environment.GetEnvironmentVariable("SLATE_LOG_DIR")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Slate", "logs");

    /// <summary>
    /// Redirect native stderr (fd 2) into the durable app log so
    /// slate-core diagnostics survive in a windowed process. Call before
    /// InitHostLogging. Failure to redirect must not stop the app —
    /// diagnostics then behave as before (lost without a console), which
    /// is the pre-existing floor, not a new failure mode.
    /// </summary>
    public static void RedirectNativeStderrToAppLog()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var stream = new FileStream(
                Path.Combine(LogDirectory, "slate-windows.log"),
                FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            if (!SetStdHandle(StdErrorHandle, stream.SafeFileHandle.DangerousGetHandle()))
            {
                stream.Dispose();
                return;
            }
            _sink = stream;
            // Managed Console.Error joins the same sink for symmetry.
            Console.SetError(new StreamWriter(stream) { AutoFlush = true });
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
