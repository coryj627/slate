// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W0-1 counter-candidate probe (#714): the same rule-1 surface and §W-E
// stress patterns as the uniffi probe (../), run through the hand-written
// C-ABI shim + csbindgen externs + the Interop.cs wrapper. Output format
// matches the uniffi probe so the two evidence blocks diff cleanly.

using System.Diagnostics;
using System.Text;
using SlateProbe; // FixtureVault (linked source)

namespace SlateShimProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? filter = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--filter" && i + 1 < args.Length) filter = args[i + 1];
        }

        var sections = new (string Name, Func<Probe, bool> Run)[]
        {
            ("session-lifetime", Sections.SessionLifetime),
            ("doc-buffer", Sections.DocBuffer),
            ("scan-progress", Sections.ScanProgress),
            ("vault-events", Sections.VaultEvents),
            ("cancellation", Sections.Cancellation),
            ("commands", Sections.Commands),
            ("error-mapping", Sections.ErrorMapping),
            ("stress-gc", Sections.GcPressure),
            ("stress-callback-concurrency", Sections.CallbackConcurrency),
            ("stress-listener-lifetime", Sections.ListenerLifetime),
        };

        Console.WriteLine($"slate csharp-probe (csbindgen shim) | {Environment.OSVersion} | " +
                          $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} | " +
                          $".NET {Environment.Version}");
        Console.WriteLine();

        int failed = 0;
        int ran = 0;
        foreach (var (name, run) in sections)
        {
            if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var probe = new Probe(name);
            var sw = Stopwatch.StartNew();
            bool pass;
            try
            {
                pass = run(probe);
            }
            catch (Exception ex)
            {
                probe.Note($"unhandled: {ex.GetType().Name}: {ex.Message}");
                pass = false;
            }
            sw.Stop();
            ran++;
            if (!pass) failed++;
            Console.WriteLine($"[{(pass ? "PASS" : "FAIL")}] {name} ({sw.Elapsed.TotalSeconds:F1}s)");
            foreach (var line in probe.Lines)
            {
                Console.WriteLine($"       {line}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("== findings (decision-record evidence) ==");
        foreach (var f in Probe.Findings)
        {
            Console.WriteLine($"  - {f}");
        }
        Console.WriteLine();
        Console.WriteLine(failed == 0 ? $"all {ran} sections passed" : $"{failed}/{ran} sections FAILED");
        return failed;
    }
}

internal sealed class Probe
{
    public static readonly List<string> Findings = new();
    public readonly List<string> Lines = new();
    private readonly string _section;

    public Probe(string section) => _section = section;

    public void Note(string line) => Lines.Add(line);

    public void Finding(string line)
    {
        Lines.Add(line);
        lock (Findings) Findings.Add($"[{_section}] {line}");
    }

    public bool Check(bool condition, string label)
    {
        Lines.Add($"{(condition ? "ok" : "MISS")}: {label}");
        return condition;
    }
}

internal static class Sections
{
    private const uint TagStarted = 1, TagFileIndexed = 2, TagFinished = 3, TagCancelled = 4;
    private const uint PhaseScanStarted = 1, PhaseReconcileStarted = 2,
        PhaseReconcileFinished = 3, PhaseScanFinished = 4;
    private const uint KindCreated = 1, KindModified = 2;

    public static bool SessionLifetime(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(8, "shim-lifetime");

        var s1 = ShimVault.Open(vault.Root);
        using (var cancel = new ShimCancelToken())
        {
            var recorder = new ShimScanRecorder();
            var (_, indexed) = s1.ScanWithProgress(cancel, recorder);
            ok &= p.Check(indexed == 8, $"first scan indexed 8 (got {indexed})");
        }
        s1.Dispose();
        s1.Dispose();
        p.Note("ok: double-Dispose tolerated");

        try
        {
            s1.ReadText("note0.md");
            p.Finding("use-after-Dispose silently succeeded (unexpected)");
            ok = false;
        }
        catch (ObjectDisposedException)
        {
            p.Finding("use-after-Dispose -> ObjectDisposedException (SafeHandle guard, hand-written)");
        }

        using (var s2 = ShimVault.Open(vault.Root))
        {
            ok &= p.Check(s2.ReadText("note0.md").Length > 0, "reopened session reads the vault");
        }

        void OpenAndDrop()
        {
            _ = ShimVault.Open(vault.Root);
        }
        OpenAndDrop();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using (var s3 = ShimVault.Open(vault.Root))
        {
            ok &= p.Check(s3.ReadText("note1.md").Length > 0,
                "reopen after finalizer-dropped session works (SafeHandle finalizer released it)");
        }
        return ok;
    }

    public static bool DocBuffer(Probe p)
    {
        bool ok = true;
        string reference = "# Title\n\nplain ascii paragraph\n";
        using var buffer = new ShimDocBuffer(reference);

        var edits = new (uint Start, uint OldLen, string NewText)[]
        {
            (9u, 0u, "inserted 🦀 rust crab, "),
            (0u, 1u, "##"),
            (12u, 5u, "写作"),
            ((uint)"# Title\n\n".Length, 0u, "combining é acute, "),
            (30u, 8u, ""),
            (0u, 0u, "\n"),
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

        byte[] utf8 = Encoding.UTF8.GetBytes(reference);
        bool mapOk = true;
        for (int b = 0; b <= utf8.Length; b += Math.Max(1, utf8.Length / 23))
        {
            int bb = b;
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

        var (appliedStart, appliedEnd, spans) = buffer.Highlight(0, Math.Min(10, buffer.LenUtf16()));
        uint lenBytes = (uint)utf8.Length;
        ok &= p.Check(appliedEnd <= lenBytes && appliedStart <= appliedEnd,
            $"highlight applied range sane [{appliedStart}, {appliedEnd}] of {lenBytes} bytes");
        foreach (var span in spans)
        {
            if (span.Start > span.End || span.End > lenBytes)
            {
                ok = p.Check(false, $"span out of bounds [{span.Start},{span.End}]");
                break;
            }
        }
        p.Finding($"highlight span array marshalled by hand ({spans.Length} spans; nested Code(TokenKind) " +
                  "detail collapsed to a bare discriminant — carrying it means flattening a second enum)");

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

    public static bool ScanProgress(Probe p)
    {
        bool ok = true;
        const int notes = 40;
        using var vault = FixtureVault.Create(notes, "shim-progress");
        using var session = ShimVault.Open(vault.Root);
        using var token = new ShimCancelToken();
        var recorder = new ShimScanRecorder();
        int mainThread = Environment.CurrentManagedThreadId;

        var (seen, indexed) = session.ScanWithProgress(token, recorder);
        var events = recorder.Snapshot();

        var started = events.Where(e => e.Tag == TagStarted).ToList();
        var files = events.Where(e => e.Tag == TagFileIndexed).ToList();
        var finished = events.Where(e => e.Tag == TagFinished).ToList();
        ok &= p.Check(started.Count == 1, $"exactly one Started (got {started.Count})");
        ok &= p.Check(files.Count == notes, $"one FileIndexed per file (got {files.Count}/{notes})");
        ok &= p.Check(finished.Count == 1 && events[^1].Tag == TagFinished, "exactly one terminal Finished");

        bool monotonic = files.Count > 0 && files[0].A == 1;
        for (int i = 1; i < files.Count && monotonic; i++)
        {
            monotonic = files[i].A == files[i - 1].A + 1;
        }
        ok &= p.Check(monotonic, "FileIndexed counter is 1..N monotonic");
        ok &= p.Check(finished.Count == 1 && finished[0].B == notes && indexed == notes,
            "Finished payload and returned report agree");
        ok &= p.Check(Trampolines.TakeFaults().Count == 0, "no trampoline faults");
        bool inline = recorder.ThreadIds.Count == 1 && recorder.ThreadIds.Contains(mainThread);
        p.Finding($"scan progress dispatch: {(inline ? "inline on the scanning (calling) thread" : $"threads {string.Join(",", recorder.ThreadIds)}")}");
        _ = seen;
        return ok;
    }

    public static bool VaultEvents(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(6, "shim-events");
        using var session = ShimVault.Open(vault.Root);
        using var token = new ShimCancelToken();
        var recorder = new ShimEventsRecorder();
        var subscription = session.RegisterEvents(recorder);

        var scanRec = new ShimScanRecorder();
        session.ScanWithProgress(token, scanRec);
        ok &= p.Check(
            WaitFor(() => recorder.Locked(r => r.IndexPhases.Count) >= 4, 5000),
            "scan delivered index-phase events");
        var phases = recorder.Locked(r => r.IndexPhases.Select(e => e.Phase).ToList());
        ok &= p.Check(
            phases.SequenceEqual(new[]
            {
                PhaseScanStarted, PhaseReconcileStarted, PhaseReconcileFinished, PhaseScanFinished,
            }),
            $"phase order correct (got {string.Join(",", phases)})");

        session.SaveText("created.md", "born\n", null);
        session.SaveText("created.md", "changed\n", null);
        ok &= p.Check(
            WaitFor(() => recorder.Locked(r => r.FileChanges.Count) >= 2, 5000),
            "saves delivered file-change events");
        var changes = recorder.Locked(r => r.FileChanges.ToList());
        ok &= p.Check(
            changes.Any(c => c.Kind == KindCreated && c.Path == "created.md")
                && changes.Any(c => c.Kind == KindModified && c.Path == "created.md"),
            "Created then Modified for the saved path");

        // Flag the log read-only within the same iteration that crosses the
        // 5 MiB threshold — a batch-then-flag sequence loses the race to the
        // compaction worker on slow shared runners (see the uniffi probe's
        // EventSections twin for the observed CI loss).
        const long compactionThresholdBytes = 5 * 1024 * 1024;
        string bigBody = new StringBuilder(270_000)
            .Insert(0, "compaction ballast paragraph without structure\n", 5_700)
            .ToString();
        var slateDir = new DirectoryInfo(Path.Combine(vault.Root, ".slate"));
        bool sawError = false;
        for (int round = 0; round < 4 && !sawError; round++)
        {
            FileInfo? flagged = null;
            for (int i = 0; i < 22 && flagged == null; i++)
            {
                session.SaveText("hot.md", bigBody + $"tail {round}/{i}\n", null);
                var log = slateDir.Exists
                    ? slateDir
                        .EnumerateFiles("*.oplog", SearchOption.AllDirectories)
                        .OrderByDescending(f => f.Length)
                        .FirstOrDefault()
                    : null;
                if (log != null && log.Length > compactionThresholdBytes)
                {
                    File.SetAttributes(log.FullName, FileAttributes.ReadOnly);
                    flagged = log;
                }
            }
            if (flagged == null)
            {
                p.Note($"round {round}: oplog never crossed the {compactionThresholdBytes / 1024 / 1024} MiB threshold");
                break;
            }
            try
            {
                sawError = WaitFor(() => recorder.Locked(r => r.Errors.Count) > 0, 10_000);
            }
            finally
            {
                File.SetAttributes(flagged.FullName, FileAttributes.Normal);
            }
            if (!sawError)
            {
                p.Note($"round {round}: compaction won the race (log {flagged.Length / 1024} KiB); regrowing");
            }
        }
        ok &= p.Check(sawError, "failed compaction dispatched on_error");
        if (sawError)
        {
            var (code, path, message) = recorder.Locked(r => r.Errors[0]);
            ok &= p.Check(code == 1, "code is CompactionFailed");
            ok &= p.Check(path == "hot.md", $"error names the hot path (got {path})");
            ok &= p.Check(message.Contains("hot.md"), "message carries user-facing copy");
        }
        p.Finding($"VaultEventListener: all three kinds delivered in one session; dispatch " +
                  $"threads {string.Join(",", recorder.ThreadIds)} (caller {Environment.CurrentManagedThreadId})");

        subscription.Dispose();
        int sealedCount = recorder.TotalCount;
        session.SaveText("post-unregister.md", "silent\n", null);
        Thread.Sleep(400);
        ok &= p.Check(recorder.TotalCount == sealedCount, "no events delivered after unregister");
        ok &= p.Check(Trampolines.TakeFaults().Count == 0, "no trampoline faults");
        return ok;
    }

    public static bool Cancellation(Probe p)
    {
        bool ok = true;
        var latencies = new List<double>();
        for (int attempt = 0; attempt < 3 && ok; attempt++)
        {
            using var vault = FixtureVault.Create(400, $"shim-cancel{attempt}");
            using var session = ShimVault.Open(vault.Root);
            using var token = new ShimCancelToken();
            var firstFile = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var recorder = new ShimScanRecorder
            {
                OnEvent = tag => { if (tag == TagFileIndexed) firstFile.TrySetResult(); },
            };
            var sw = new Stopwatch();
            var scan = Task.Run(() =>
            {
                try
                {
                    session.ScanWithProgress(token, recorder);
                    return "completed";
                }
                catch (ShimException ex) when (ex.Code == Codes.Cancelled)
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
                ok = p.Check(false, $"scan {scan.Result} before cancel took effect");
                break;
            }
            latencies.Add(sw.Elapsed.TotalMilliseconds);
            ok &= p.Check(recorder.Snapshot().Any(e => e.Tag == TagCancelled),
                "listener saw terminal Cancelled event");
        }
        if (ok)
        {
            latencies.Sort();
            p.Finding($"cancel latency over 3 mid-scan cancels: " +
                      $"min {latencies[0]:F0} ms / median {latencies[1]:F0} ms / max {latencies[2]:F0} ms");
            ok &= p.Check(latencies[^1] < 5000, "worst cancel latency bounded (<5s)");
        }

        using (var vault = FixtureVault.Create(3, "shim-precancel"))
        using (var session = ShimVault.Open(vault.Root))
        using (var token = new ShimCancelToken())
        {
            token.Cancel();
            ok &= p.Check(token.IsCancelled(), "is_cancelled round-trips");
            try
            {
                session.ScanWithProgress(token, new ShimScanRecorder());
                ok = p.Check(false, "pre-cancelled scan should throw");
            }
            catch (ShimException ex) when (ex.Code == Codes.Cancelled)
            {
                p.Note("ok: pre-cancelled token -> Cancelled status");
            }
        }
        return ok;
    }

    public static bool Commands(Probe p)
    {
        bool ok = true;
        using var registry = new ShimRegistry();

        var okAction = new ShimActionBox(() => (true, null));
        bool replaced = registry.Register("probe.ok", "Probe OK", okAction);
        ok &= p.Check(!replaced, "fresh registration returns false");
        registry.Invoke("probe.ok");
        ok &= p.Check(okAction.InvocationCount == 1, "success round-trip invoked the C# action");

        registry.Register("probe.fail", "Probe Fail", new ShimActionBox(() => (false, "boom from C#")));
        try
        {
            registry.Invoke("probe.fail");
            ok = p.Check(false, "failing action should raise");
        }
        catch (ShimException ex) when (ex.Code == Codes.CommandActionFailed)
        {
            ok &= p.Check(ex.Message.Contains("boom from C#"),
                $"error message round-trips (got {ex.Message})");
        }

        registry.Register("probe.long", "Probe Long", new ShimActionBox(() => (false, new string('x', 20_000))));
        try
        {
            registry.Invoke("probe.long");
            ok = p.Check(false, "long-message action should raise");
        }
        catch (ShimException ex) when (ex.Code == Codes.CommandActionFailed)
        {
            ok &= p.Check(ex.Message.Length <= 1100,
                $"20k message clipped by the shim's fixed 1 KiB buffer (len {ex.Message.Length})");
            p.Finding("foreign error messages ride a fixed 1 KiB buffer (hand-chosen contract; " +
                      "uniffi lifts arbitrary-length strings + core truncation marker)");
        }

        try
        {
            registry.Invoke("probe.nope");
            ok = p.Check(false, "unknown id should raise");
        }
        catch (ShimException ex) when (ex.Code == Codes.CommandUnknownId)
        {
            ok &= p.Check(ex.Message == "probe.nope", "UnknownId carries the id");
        }

        registry.Register("probe.throw", "Probe Throw",
            new ShimActionBox(() => throw new InvalidOperationException("untyped escape")));
        try
        {
            registry.Invoke("probe.throw");
            p.Finding("untyped C# exception in action was swallowed (unexpected)");
            ok = false;
        }
        catch (ShimException ex)
        {
            bool fenced = Trampolines.TakeFaults().Count > 0;
            ok &= p.Check(fenced, "trampoline fence caught the untyped exception");
            p.Finding($"untyped C# exception in action -> degraded to ActionFailed \"{ex.Message}\" " +
                      "(hand-written fence; forgetting it is UB, not an exception)");
        }

        int reentryCount = -1;
        registry.Register("probe.reenter", "Probe Reenter",
            new ShimActionBox(() => { reentryCount = registry.Count(); return (true, null); }));
        var reentry = Task.Run(() => registry.Invoke("probe.reenter"));
        if (reentry.Wait(TimeSpan.FromSeconds(5)))
        {
            ok &= p.Check(reentryCount >= 5, $"re-entrant Count() saw the registry ({reentryCount} commands)");
            p.Finding("action -> registry re-entry completes without deadlock");
        }
        else
        {
            p.Finding("action -> registry re-entry DEADLOCKED (watchdog hit at 5s)");
            ok = false;
        }

        ok &= p.Check(registry.Unregister("probe.ok"), "unregister returns true for live id");
        ok &= p.Check(!registry.Unregister("probe.ok"), "second unregister returns false");
        ok &= p.Check(registry.Register("probe.fail", "Probe Fail 2", okAction),
            "re-registration under a live id reports replacement");
        return ok;
    }

    public static bool ErrorMapping(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(4, "shim-errors");
        using var session = ShimVault.Open(vault.Root);
        using var token = new ShimCancelToken();
        session.ScanWithProgress(token, new ShimScanRecorder());

        string filePath = Path.Combine(Path.GetTempPath(), $"slate-probe-file-{Guid.NewGuid():N}");
        File.WriteAllText(filePath, "not a directory");
        try
        {
            using var bad = ShimVault.Open(Path.Combine(filePath, "sub"));
            ok = p.Check(false, "open under a file should fail");
        }
        catch (ShimException ex) when (ex.Code == Codes.InvalidPath)
        {
            ok &= p.Check(ex.Message.Length > 0, "root-under-file -> InvalidPath code + display message");
        }
        finally
        {
            File.Delete(filePath);
        }

        File.Delete(Path.Combine(vault.Root, "note3.md"));
        try
        {
            session.ReadText("note3.md");
            ok = p.Check(false, "read of deleted-behind-index file should fail");
        }
        catch (ShimException ex) when (ex.Code == Codes.Io)
        {
            ok &= p.Check(ex.Message.Length > 0, "delete-behind-index read -> Io code");
        }

        byte[] latin1 = { 0x68, 0xE9, 0x6C, 0x6C, 0x6F };
        File.WriteAllBytes(Path.Combine(vault.Root, "latin1.md"), latin1);
        try
        {
            session.ReadText("latin1.md");
            ok = p.Check(false, "non-UTF-8 read should fail");
        }
        catch (ShimException ex) when (ex.Code == Codes.InvalidUtf8)
        {
            ok &= p.Check(ex.Message.Contains("latin1.md"),
                "InvalidUtf8 path survives only inside the display string");
        }
        p.Finding("beyond WriteConflict's hand-plumbed out-params, structured error fields collapse " +
                  "to (code, display-string) — per-variant field plumbing is bespoke shim work each time");

        var (newHash, _) = session.SaveText("note0.md", "fresh content\n", null);
        try
        {
            session.SaveText("note0.md", "conflicting write\n", "0123456789abcdef");
            ok = p.Check(false, "stale-hash save should fail");
        }
        catch (ShimWriteConflictException ex)
        {
            ok &= p.Check(ex.CurrentContentHash == newHash && ex.CurrentMtimeMs > 0,
                "WriteConflict carries current hash + mtime through dedicated out-params");
        }
        return ok;
    }

    public static bool GcPressure(Probe p)
    {
        bool ok = true;
        long rssBefore = Process.GetCurrentProcess().WorkingSet64;

        for (int i = 0; i < 2000; i++)
        {
            var buffer = new ShimDocBuffer($"note {i} body with some text");
            buffer.ApplyEdit(0, 0, "x");
            if ((i & 1) == 0) buffer.Dispose();
            var token = new ShimCancelToken();
            if ((i & 1) == 0) token.Dispose();
            if (i % 500 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        p.Note("ok: 2000 buffers + 2000 tokens through Dispose/finalizer mix");

        using (var vault = FixtureVault.Create(2, "shim-gc"))
        {
            for (int i = 0; i < 60; i++)
            {
                var s = ShimVault.Open(vault.Root);
                if (i % 3 != 2) s.Dispose();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var reopened = ShimVault.Open(vault.Root);
            ok &= p.Check(reopened.ReadText("note0.md").Length > 0,
                "vault healthy after 60 session open/collect cycles");

            using var raceVault = FixtureVault.Create(300, "shim-gcrace");
            var racing = ShimVault.Open(raceVault.Root);
            using var token = new ShimCancelToken();
            var scan = Task.Run(() =>
            {
                try
                {
                    racing.ScanWithProgress(token, new ShimScanRecorder());
                    return "completed";
                }
                catch (ShimException ex) when (ex.Code == Codes.Cancelled) { return "cancelled"; }
                catch (ObjectDisposedException) { return "disposed-race"; }
                catch (Exception ex) { return $"managed:{ex.GetType().Name}"; }
            });
            Thread.Sleep(80);
            racing.Dispose();
            token.Cancel();
            bool finished = scan.Wait(TimeSpan.FromSeconds(30));
            ok &= p.Check(finished, "dispose-during-scan: call still terminates");
            if (finished)
            {
                p.Finding($"Dispose while a scan is in flight -> scan {scan.Result} " +
                          "(SafeHandle guard defers the close; correctness is on the wrapper author)");
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        long rssAfter = Process.GetCurrentProcess().WorkingSet64;
        p.Finding($"working set {rssBefore / 1_048_576} MiB -> {rssAfter / 1_048_576} MiB across GC-pressure loops");
        return ok;
    }

    public static bool CallbackConcurrency(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(500, "shim-concurrency");
        using var session = ShimVault.Open(vault.Root);
        var events = new ShimEventsRecorder();
        var subscription = session.RegisterEvents(events);
        var progress = new ShimScanRecorder();
        using var registry = new ShimRegistry();
        var spin = new ShimActionBox(() => (true, null));
        registry.Register("probe.spin", "Spin", spin);

        Exception? failure = null;
        using var scanToken = new ShimCancelToken();
        var scanTask = Task.Run(() =>
        {
            try { session.ScanWithProgress(scanToken, progress); }
            catch (Exception ex) { Volatile.Write(ref failure, ex); }
        });
        var stop = new CancellationTokenSource();
        var saveTask = Task.Run(() =>
        {
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    session.SaveText($"churn{n % 8}.md", $"save {n}\n", null);
                    n++;
                }
                return n;
            }
            catch (Exception ex) { Volatile.Write(ref failure, ex); return -1; }
        });
        var invokeTask = Task.Run(() =>
        {
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    registry.Invoke("probe.spin");
                    n++;
                }
                return n;
            }
            catch (Exception ex) { Volatile.Write(ref failure, ex); return -1; }
        });

        bool scanDone = scanTask.Wait(TimeSpan.FromSeconds(120));
        stop.Cancel();
        bool sideDone = Task.WaitAll(new Task[] { saveTask, invokeTask }, TimeSpan.FromSeconds(30));
        ok &= p.Check(scanDone && sideDone, "all three threads terminated (no deadlock)");
        ok &= p.Check(Volatile.Read(ref failure) == null,
            failure == null ? "no thread saw an exception" : $"thread failed: {failure!.GetType().Name}: {failure!.Message}");
        int indexedUnderLoad = progress.Snapshot().Count(e => e.Tag == TagFileIndexed);
        ok &= p.Check(indexedUnderLoad >= 500,
            $"scan progress complete under concurrent load ({indexedUnderLoad} files)");
        ok &= p.Check(saveTask.Result > 0 && invokeTask.Result > 0 && spin.InvocationCount == invokeTask.Result,
            $"saves ({saveTask.Result}) and command invokes ({invokeTask.Result}) both progressed");
        ok &= p.Check(events.TotalCount > 0, "vault events flowed during the storm");
        ok &= p.Check(Trampolines.TakeFaults().Count == 0, "no trampoline faults under the storm");
        p.Finding($"concurrency storm: {progress.Snapshot().Count} progress cbs, {events.TotalCount} vault events, " +
                  $"{spin.InvocationCount} command invokes");
        subscription.Dispose();
        return ok;
    }

    public static bool ListenerLifetime(Probe p)
    {
        bool ok = true;
        using var vault = FixtureVault.Create(4, "shim-listeners");
        using var session = ShimVault.Open(vault.Root);
        using var token = new ShimCancelToken();
        session.ScanWithProgress(token, new ShimScanRecorder());

        var stop = new CancellationTokenSource();
        Exception? churnFailure = null;
        var saveTask = Task.Run(() =>
        {
            try
            {
                int n = 0;
                while (!stop.IsCancellationRequested)
                {
                    session.SaveText("churn.md", $"rev {n++}\n", null);
                }
            }
            catch (Exception ex) { Volatile.Write(ref churnFailure, ex); }
        });

        var weakRefs = new List<WeakReference>();
        for (int i = 0; i < 400; i++)
        {
            var listener = new ShimEventsRecorder();
            var sub = session.RegisterEvents(listener);
            if ((i & 3) == 0) Thread.Sleep(1);
            sub.Dispose();
            weakRefs.Add(new WeakReference(listener));
        }
        stop.Cancel();
        ok &= p.Check(saveTask.Wait(TimeSpan.FromSeconds(30)) && churnFailure == null,
            "save churn survived 400 register/unregister cycles");

        // Contexts ride the reaper's grace window before their GCHandles
        // free — wait it out, sweep, then measure collectability.
        Thread.Sleep(700);
        ContextReaper.Sweep();
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        int alive = weakRefs.Count(w => w.IsAlive);
        p.Finding($"unregistered listeners still strongly held after grace+GC: {alive}/400 " +
                  "(handle release is the wrapper's delayed-free discipline, not the binding's)");
        ok &= p.Check(alive < 400, "unregistration (eventually) releases context handles");

        var survivor = new ShimEventsRecorder();
        var survivorSub = session.RegisterEvents(survivor);
        session.SaveText("final.md", "for the survivor\n", null);
        ok &= p.Check(WaitFor(() => survivor.Locked(r => r.FileChanges.Count) > 0, 5000),
            "freshly registered listener receives events after the churn");
        survivorSub.Dispose();
        ok &= p.Check(Trampolines.TakeFaults().Count == 0, "no trampoline faults across the churn");
        return ok;
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(25);
        }
        return condition();
    }
}
