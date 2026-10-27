// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// WB-10 (#1293): SQL against a vault's shared cache from outside core, as
/// another process would run it — through Windows' own SQLite
/// (<c>winsqlite3.dll</c>, in System32 on every supported Windows), on its
/// own connection. The mac twin runs <c>/usr/bin/sqlite3</c>. Only for the
/// states core cannot be asked for in a fact's time: the feed trimmed past a
/// session's cursor, as its 100,000-row cap trims it.
/// </summary>
internal static class CacheSql
{
    private const int OpenReadWrite = 0x00000002;
    private const int Ok = 0;

    /// <summary>Run <paramref name="sql"/> on the cache of the vault at
    /// <paramref name="vaultRoot"/>, waiting up to ten seconds for its
    /// lock.</summary>
    internal static void Execute(string vaultRoot, string sql)
    {
        string cache = Path.Combine(vaultRoot, ".slate", "cache.sqlite");
        Assert.True(File.Exists(cache), $"premise: the cache at {cache}");
        int opened = sqlite3_open_v2(Utf8(cache), out IntPtr db, OpenReadWrite, IntPtr.Zero);
        try
        {
            Assert.True(opened == Ok, $"winsqlite3 could not open the cache: {Message(db)}");
            _ = sqlite3_busy_timeout(db, 10_000);
            int ran = sqlite3_exec(db, Utf8(sql), IntPtr.Zero, IntPtr.Zero, out IntPtr error);
            string? detail = error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(error);
            sqlite3_free(error);
            Assert.True(ran == Ok, $"winsqlite3: {detail ?? Message(db)}");
        }
        finally
        {
            _ = sqlite3_close_v2(db);
        }
    }

    /// <summary>The feed's rows trimmed past every cursor, as the
    /// 100,000-row cap trims them while a session lags: that session's next
    /// window is a gap (<c>Trimmed</c>).</summary>
    internal static void TrimTheFeedPastTheCursor(string vaultRoot) => Execute(
        vaultRoot,
        """
        DELETE FROM change_feed;
        UPDATE change_feed_meta
           SET trimmed_through = (SELECT seq FROM sqlite_sequence WHERE name = 'change_feed')
         WHERE id = 1;
        """);

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");

    private static string Message(IntPtr db) =>
        db == IntPtr.Zero ? "no connection" : Marshal.PtrToStringUTF8(sqlite3_errmsg(db)) ?? "unknown";

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    private static extern int sqlite3_busy_timeout(IntPtr db, int milliseconds);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    private static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr argument, out IntPtr error);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    private static extern void sqlite3_free(IntPtr memory);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    private static extern IntPtr sqlite3_errmsg(IntPtr db);

    [DllImport("winsqlite3.dll", ExactSpelling = true)]
    private static extern int sqlite3_close_v2(IntPtr db);
}
