// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using uniffi.slate_uniffi;

namespace SlateProbe;

internal static class CoreSections
{
    /// <summary>
    /// Handle lifetime: open → use → Dispose → reopen the same vault
    /// (cache reuse), double-Dispose, use-after-Dispose (must fail as a
    /// managed exception, never a native fault), finalizer-path drop.
    /// </summary>
    public static bool SessionLifetime(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(8, "lifetime");

        var s1 = VaultSession.OpenFilesystem(vault.Root);
        using (var cancel = new CancelToken())
        {
            var report = s1.ScanInitial(cancel);
            ok &= p.Check(report.FilesIndexed == 8, $"first scan indexed 8 (got {report.FilesIndexed})");
        }
        var page = s1.ListFiles(FileFilter.MarkdownOnly, new Paging(null, 100));
        ok &= p.Check(page.Items.Length == 8, $"list_files returned 8 (got {page.Items.Length})");
        s1.Dispose();
        s1.Dispose(); // double-Dispose must be a no-op
        p.Note("ok: double-Dispose tolerated");

        try
        {
            s1.ListFiles(FileFilter.All, new Paging(null, 1));
            p.Finding("use-after-Dispose silently succeeded (unexpected)");
            ok = false;
        }
        catch (Exception ex)
        {
            p.Finding($"use-after-Dispose -> managed {ex.GetType().Name} (no native fault)");
        }

        // Reopen after close: the sqlite cache must have been released.
        using (var s2 = VaultSession.OpenFilesystem(vault.Root))
        {
            var summary = s2.GetFileSummary("note0.md");
            ok &= p.Check(summary != null, "reopened session reads the persisted cache");
        }

        // Finalizer path: drop without Dispose; collection must not crash,
        // and the vault must remain openable afterwards.
        void OpenAndDrop()
        {
            _ = VaultSession.OpenFilesystem(vault.Root);
        }
        OpenAndDrop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using (var s3 = VaultSession.OpenFilesystem(vault.Root))
        {
            ok &= p.Check(s3.ListFiles(FileFilter.All, new Paging(null, 1)).TotalFiltered == 8,
                "reopen after finalizer-dropped session works");
        }
        return ok;
    }

    /// <summary>
    /// The keystroke hot path: apply_edit deltas mirrored against a C#
    /// reference string (UTF-16 semantics), len/byte-offset agreement at
    /// every step incl. astral + CJK content, windowed highlight sanity,
    /// reset, and a bulk-edit timing figure.
    /// </summary>
    public static bool DocBuffer(Probe p)
    {
        bool ok = true;
        string reference = "# Title\n\nplain ascii paragraph\n";
        using var buffer = new DocumentBuffer(reference);

        var edits = new (uint Start, uint OldLen, string NewText)[]
        {
            (9u, 0u, "inserted 🦀 rust crab, ")
            , (0u, 1u, "##")
            , (12u, 5u, "写作")
            , ((uint)"# Title\n\n".Length, 0u, "combining é acute, ")
            , (30u, 8u, "")
            , (0u, 0u, "\n")
        };
        foreach (var (start, oldLen, newText) in edits)
        {
            buffer.ApplyEdit(start, oldLen, newText);
            reference = reference.Remove((int)start, (int)oldLen).Insert((int)start, newText);
            if (buffer.LenUtf16() != (uint)reference.Length)
            {
                ok = p.Check(false, $"len_utf16 {buffer.LenUtf16()} != reference {reference.Length} after edit @{start}");
                break;
            }
        }
        ok &= p.Check(buffer.LenUtf16() == (uint)reference.Length,
            $"scripted edits: len_utf16 agrees ({reference.Length} units)");

        // byte→utf16 agreement on a spread of offsets (reference mapping
        // computed from the C# string's UTF-8 encoding).
        byte[] utf8 = Encoding.UTF8.GetBytes(reference);
        bool mapOk = true;
        for (int b = 0; b <= utf8.Length; b += Math.Max(1, utf8.Length / 23))
        {
            int bb = b;
            // Snap to a UTF-8 boundary (continuation bytes are 10xxxxxx).
            while (bb < utf8.Length && (utf8[bb] & 0xC0) == 0x80) bb++;
            uint got = buffer.ByteToUtf16((uint)bb);
            int expected = Encoding.UTF8.GetString(utf8, 0, bb).Length;
            if (got != (uint)expected)
            {
                mapOk = false;
                p.Note($"MISS: byte_to_utf16({bb}) = {got}, expected {expected}");
                break;
            }
        }
        ok &= p.Check(mapOk, "byte_to_utf16 agrees with reference mapping across the doc");

        var ranged = buffer.HighlightInRange(0, Math.Min(10, buffer.LenUtf16()));
        uint lenBytes = (uint)utf8.Length;
        ok &= p.Check(ranged.AppliedEnd <= lenBytes && ranged.AppliedStart <= ranged.AppliedEnd,
            $"highlight applied range sane [{ranged.AppliedStart}, {ranged.AppliedEnd}] of {lenBytes} bytes");
        foreach (var span in ranged.Spans)
        {
            if (span.StartByte > span.EndByte || span.EndByte > lenBytes)
            {
                ok = p.Check(false, $"span out of bounds [{span.StartByte},{span.EndByte}]");
                break;
            }
        }

        buffer.Reset("fresh");
        ok &= p.Check(buffer.LenUtf16() == 5, "reset replaces content");

        // Hot-path timing: 2k single-char inserts (debug-build figure —
        // a relative marshalling indicator, not a perf budget).
        buffer.Reset(string.Empty);
        var sw = Stopwatch.StartNew();
        for (uint i = 0; i < 2000; i++)
        {
            buffer.ApplyEdit(i, 0, "x");
        }
        sw.Stop();
        ok &= p.Check(buffer.LenUtf16() == 2000, "2k sequential inserts land");
        p.Finding($"apply_edit x2000 (debug build): {sw.Elapsed.TotalMilliseconds:F0} ms " +
                  $"({sw.Elapsed.TotalMilliseconds * 1000 / 2000:F0} us/edit incl. marshalling)");
        return ok;
    }

    /// <summary>
    /// Mid-scan cancellation: latency from CancelToken.Cancel() to the
    /// blocked scan call surfacing VaultException.Cancelled, plus the
    /// pre-cancelled-token fast path.
    /// </summary>
    public static bool Cancellation(Probe p)
    {
        bool ok = true;
        var latencies = new List<double>();
        for (int attempt = 0; attempt < 3 && ok; attempt++)
        {
            using var vault = FixtureVault.Create(400, $"cancel{attempt}");
            using var session = VaultSession.OpenFilesystem(vault.Root);
            using var token = new CancelToken();
            var firstFile = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var recorder = new ProgressRecorder
            {
                OnEvent = e => { if (e is ScanProgress.FileIndexed) firstFile.TrySetResult(); },
            };
            var sw = new Stopwatch();
            var scan = Task.Run(() =>
            {
                try
                {
                    session.ScanInitialWithProgress(token, recorder);
                    return "completed";
                }
                catch (VaultException.Cancelled)
                {
                    sw.Stop();
                    return "cancelled";
                }
            });
            if (!firstFile.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                ok = p.Check(false, "scan produced no FileIndexed within 30s");
                break;
            }
            sw.Start();
            token.Cancel();
            if (!scan.Wait(TimeSpan.FromSeconds(10)))
            {
                ok = p.Check(false, "scan did not return within 10s of cancel");
                break;
            }
            if (scan.Result != "cancelled")
            {
                // 400 debug-build indexes should take far longer than one
                // event dispatch; completion before cancel means the fixture
                // is too small to measure — treat as inconclusive failure.
                ok = p.Check(false, $"scan {scan.Result} before cancel took effect");
                break;
            }
            latencies.Add(sw.Elapsed.TotalMilliseconds);
            bool sawTerminalCancelled = recorder.Snapshot().Any(e => e is ScanProgress.Cancelled);
            ok &= p.Check(sawTerminalCancelled, "listener saw terminal Cancelled event");
        }
        if (ok)
        {
            latencies.Sort();
            p.Finding($"cancel latency over 3 mid-scan cancels: " +
                      $"min {latencies[0]:F0} ms / median {latencies[1]:F0} ms / max {latencies[2]:F0} ms");
            ok &= p.Check(latencies[^1] < 5000, "worst cancel latency bounded (<5s)");
        }

        // Pre-cancelled token short-circuits without touching the cache.
        using (var vault = FixtureVault.Create(3, "precancel"))
        using (var session = VaultSession.OpenFilesystem(vault.Root))
        using (var token = new CancelToken())
        {
            token.Cancel();
            ok &= p.Check(token.IsCancelled(), "is_cancelled round-trips");
            try
            {
                session.ScanInitial(token);
                ok = p.Check(false, "pre-cancelled scan should throw");
            }
            catch (VaultException.Cancelled)
            {
                p.Note("ok: pre-cancelled token -> VaultException.Cancelled");
            }
        }
        return ok;
    }

    /// <summary>
    /// CommandRegistry + foreign CommandAction: success and typed-error
    /// round-trips, unknown id, unregister semantics, foreign-message
    /// truncation at the trust boundary, non-CommandException escape
    /// behavior, and re-entry (action calling back into the registry).
    /// </summary>
    public static bool Commands(Probe p)
    {
        bool ok = true;
        using var registry = new CommandRegistry();

        var okAction = new ProbeAction(() => { });
        bool replaced = registry.Register(
            new Command("probe.ok", "Probe OK", null, null, CommandSection.File), okAction);
        ok &= p.Check(!replaced, "fresh registration returns false");
        registry.InvokeById("probe.ok");
        ok &= p.Check(okAction.InvocationCount == 1, "success round-trip invoked the C# action");

        // Typed failure: C# throws CommandException.ActionFailed; Rust maps
        // it back across invoke_by_id as the same typed error.
        _ = registry.Register(
            new Command("probe.fail", "Probe Fail", null, null, CommandSection.File),
            new ProbeAction(() => throw new CommandException.ActionFailed("boom from C#")));
        try
        {
            registry.InvokeById("probe.fail");
            ok = p.Check(false, "failing action should raise");
        }
        catch (CommandException.ActionFailed ex)
        {
            ok &= p.Check(ex.message == "boom from C#", $"error message round-trips (got {ex.message})");
        }

        // Foreign-controlled message truncation (the Rust trust boundary).
        _ = registry.Register(
            new Command("probe.long", "Probe Long", null, null, CommandSection.File),
            new ProbeAction(() => throw new CommandException.ActionFailed(new string('x', 20_000))));
        try
        {
            registry.InvokeById("probe.long");
            ok = p.Check(false, "long-message action should raise");
        }
        catch (CommandException.ActionFailed ex)
        {
            bool truncated = ex.message.Length < 20_000 && ex.message.Contains("truncated");
            ok &= p.Check(truncated, $"20k message truncated at boundary (len {ex.message.Length})");
        }

        try
        {
            registry.InvokeById("probe.nope");
            ok = p.Check(false, "unknown id should raise");
        }
        catch (CommandException.UnknownId ex)
        {
            ok &= p.Check(ex.id == "probe.nope", "UnknownId carries the id");
        }

        // A non-CommandException escaping the C# action must surface as a
        // managed error on the caller, never a native abort.
        _ = registry.Register(
            new Command("probe.throw", "Probe Throw", null, null, CommandSection.File),
            new ProbeAction(() => throw new InvalidOperationException("untyped escape")));
        try
        {
            registry.InvokeById("probe.throw");
            p.Finding("untyped C# exception in action was swallowed (invoke succeeded)");
        }
        catch (Exception ex)
        {
            p.Finding($"untyped C# exception in action -> caller sees {ex.GetType().Name} (no abort)");
        }

        // Re-entry: an action that calls back into the registry (the W5
        // menu-dispatch shape). Watchdogged so a deadlock is a recorded
        // finding, not a hung probe.
        int reentryListCount = -1;
        _ = registry.Register(
            new Command("probe.reenter", "Probe Reenter", null, null, CommandSection.File),
            new ProbeAction(() => reentryListCount = registry.List().Length));
        var reentry = Task.Run(() => registry.InvokeById("probe.reenter"));
        if (reentry.Wait(TimeSpan.FromSeconds(5)))
        {
            ok &= p.Check(reentryListCount >= 5, $"re-entrant List() saw the registry ({reentryListCount} commands)");
            p.Finding("action -> registry re-entry (List during invoke) completes without deadlock");
        }
        else
        {
            p.Finding("action -> registry re-entry DEADLOCKED (watchdog hit at 5s)");
            ok = false;
        }

        ok &= p.Check(registry.Unregister("probe.ok"), "unregister returns true for live id");
        ok &= p.Check(!registry.Unregister("probe.ok"), "second unregister returns false");
        var replacedNow = registry.Register(
            new Command("probe.fail", "Probe Fail 2", null, null, CommandSection.File), okAction);
        ok &= p.Check(replacedNow, "re-registration under a live id reports replacement");
        return ok;
    }

    /// <summary>
    /// Typed VaultError mapping: a spread of arms with structured fields
    /// (not the W0-3 totality census — representative shapes only).
    /// </summary>
    public static bool ErrorMapping(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(4, "errors");
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        session.ScanInitial(token);

        // A root under an existing *file* is rejected up front as
        // InvalidPath (validated before any IO — observed behavior).
        string filePath = Path.Combine(Path.GetTempPath(), $"slate-probe-file-{Guid.NewGuid():N}");
        File.WriteAllText(filePath, "not a directory");
        try
        {
            using var bad = VaultSession.OpenFilesystem(Path.Combine(filePath, "sub"));
            ok = p.Check(false, "open under a file should fail");
        }
        catch (VaultException.InvalidPath ex)
        {
            ok &= p.Check(ex.reason.Length > 0, "root-under-file -> InvalidPath with reason");
        }
        finally
        {
            File.Delete(filePath);
        }

        // Io: the index knows the file but the bytes are gone from disk.
        File.Delete(Path.Combine(vault.Root, "note3.md"));
        try
        {
            session.ReadText("note3.md");
            ok = p.Check(false, "read of deleted-behind-index file should fail");
        }
        catch (VaultException.Io ex)
        {
            ok &= p.Check(ex.message.Length > 0, "delete-behind-index read -> Io with message");
        }

        try
        {
            session.ReadText("../outside.md");
            ok = p.Check(false, "path escape should fail");
        }
        catch (VaultException.InvalidPath ex)
        {
            ok &= p.Check(ex.path == "../outside.md" && ex.reason.Length > 0,
                $"InvalidPath fields populated (reason: {ex.reason})");
        }

        try
        {
            session.ReadText("missing.md");
            ok = p.Check(false, "missing file should fail");
        }
        catch (VaultException ex)
        {
            p.Finding($"read_text on a missing file maps to VaultException.{ex.GetType().Name}");
        }

        byte[] latin1 = { 0x68, 0xE9, 0x6C, 0x6C, 0x6F }; // "héllo" in Latin-1, invalid UTF-8
        File.WriteAllBytes(Path.Combine(vault.Root, "latin1.md"), latin1);
        try
        {
            session.ReadText("latin1.md");
            ok = p.Check(false, "non-UTF-8 read should fail");
        }
        catch (VaultException.InvalidUtf8 ex)
        {
            ok &= p.Check(ex.path == "latin1.md", "InvalidUtf8 carries the path");
        }

        try
        {
            session.FullTextSearch("\"unterminated", new SearchScope.Vault(), token);
            ok = p.Check(false, "bad FTS5 query should fail");
        }
        catch (VaultException.InvalidQuery ex)
        {
            ok &= p.Check(ex.message.Length > 0, "InvalidQuery carries the parser message");
        }

        try
        {
            session.FullTextSearch("probe", new SearchScope.File("note0.md"), token);
            ok = p.Check(false, "reserved File scope should fail");
        }
        catch (VaultException.Unsupported ex)
        {
            ok &= p.Check(ex.feature.Length > 0, $"Unsupported names the feature ({ex.feature})");
        }

        var saved = session.SaveText("note0.md", "fresh content\n", null);
        try
        {
            session.SaveText("note0.md", "conflicting write\n", "0123456789abcdef");
            ok = p.Check(false, "stale-hash save should fail");
        }
        catch (VaultException.WriteConflict ex)
        {
            ok &= p.Check(
                ex.currentContentHash == saved.NewContentHash
                    && ex.expectedContentHash == "0123456789abcdef"
                    && ex.currentMtimeMs > 0,
                "WriteConflict carries current/expected hashes + mtime");
        }
        return ok;
    }
}
