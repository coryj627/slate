// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-E handle lifetime under GC pressure (w0_spec §W0-3 item 2, #715):
// open/close/drop sessions and buffers through both the Dispose and the
// finalizer path, dispose-during-in-flight-call, use-after-Dispose as a
// managed failure. Seeded from the W0-1 probe's session-lifetime and
// stress-gc sections. Moderate sizes on the PR lane; SLATE_CENSUS_FULL=1
// runs the full tier (repo census convention).

using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "handle-lifetime")]
public class HandleLifetimeCensus
{
    [Fact]
    public void SessionLifecycle_DisposeReopenFinalizerAndUseAfterDispose()
    {
        using var vault = FixtureVault.Create(8);

        var s1 = VaultSession.OpenFilesystem(vault.Root);
        using (var cancel = new CancelToken())
        {
            Assert.Equal(8UL, s1.ScanInitial(cancel).FilesIndexed);
        }
        Assert.Equal(8, s1.ListFiles(FileFilter.MarkdownOnly, new Paging(null, 100)).Items.Length);
        s1.Dispose();
        s1.Dispose(); // double-Dispose must be a no-op

        // Use-after-Dispose must surface as a managed exception, never a
        // native fault.
        Assert.ThrowsAny<Exception>(() => s1.ListFiles(FileFilter.All, new Paging(null, 1)));

        // Reopen after close: the sqlite cache must have been released.
        using (var s2 = VaultSession.OpenFilesystem(vault.Root))
        {
            Assert.NotNull(s2.GetFileSummary("note0.md"));
        }

        // Finalizer path: drop without Dispose; collection must not crash
        // and the vault must remain openable.
        OpenAndDrop(vault.Root);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var s3 = VaultSession.OpenFilesystem(vault.Root);
        Assert.Equal(8UL, s3.ListFiles(FileFilter.All, new Paging(null, 1)).TotalFiltered);
    }

    private static void OpenAndDrop(string root)
    {
        _ = VaultSession.OpenFilesystem(root);
    }

    [Fact]
    public void GcPressure_ThousandsOfHandlesThroughDisposeAndFinalizerMix()
    {
        // Finalizer-path wrappers are tracked as WeakReferences (created in
        // a non-inlined helper so no JIT-held local pins them): the census
        // proves the finalizer path actually collects — a broken finalizer
        // would otherwise stay green until production resource exhaustion.
        int buffers = CensusTier.Scale(800, 4000);
        var undisposed = new List<WeakReference>();
        for (int i = 0; i < buffers; i++)
        {
            undisposed.AddRange(TouchBufferAndToken(i, disposeEagerly: (i & 1) == 0));
            if (i % 250 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        using var vault = FixtureVault.Create(2);
        int sessions = CensusTier.Scale(30, 120);
        for (int i = 0; i < sessions; i++)
        {
            var weak = OpenSession(vault.Root, dispose: i % 3 != 2);
            if (weak != null)
            {
                undisposed.Add(weak); // every third session finalizer-collected
            }
        }
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        int alive = undisposed.Count(w => w.IsAlive);
        Assert.True(
            alive <= 1,
            $"finalizer path leaks wrappers: {alive}/{undisposed.Count} finalizer-path handles still alive after GC");

        using var reopened = VaultSession.OpenFilesystem(vault.Root);
        using var census = new CancelToken();
        reopened.ScanInitial(census);
        Assert.Equal(2UL, reopened.ListFiles(FileFilter.All, new Paging(null, 1)).TotalFiltered);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static List<WeakReference> TouchBufferAndToken(int i, bool disposeEagerly)
    {
        var tracked = new List<WeakReference>();
        var buffer = new DocumentBuffer($"note {i} body with some text");
        buffer.ApplyEdit(0, 0, "x");
        var token = new CancelToken();
        if (disposeEagerly)
        {
            buffer.Dispose();
            token.Dispose();
        }
        else
        {
            tracked.Add(new WeakReference(buffer)); // rides the finalizer
            tracked.Add(new WeakReference(token));
        }
        return tracked;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference? OpenSession(string root, bool dispose)
    {
        var s = VaultSession.OpenFilesystem(root);
        if (dispose)
        {
            s.Dispose();
            return null;
        }
        return new WeakReference(s);
    }

    [Fact]
    public void DisposeDuringInFlightScan_CallStillTerminatesWithoutNativeFault()
    {
        using var vault = FixtureVault.Create(CensusTier.Scale(200, 400));
        var racing = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();

        // Synchronize on confirmed native entry: the first FileIndexed
        // callback (dispatched from inside the native scan) signals
        // `entered` and then blocks on `gate`, so Dispose provably races an
        // in-flight native call — no sleep-based guessing.
        using var entered = new ManualResetEventSlim(false);
        using var gate = new ManualResetEventSlim(false);
        var listener = new Support.ProgressRecorder
        {
            OnEvent = e =>
            {
                if (e is ScanProgress.FileIndexed && !entered.IsSet)
                {
                    entered.Set();
                    gate.Wait(TimeSpan.FromSeconds(10));
                }
            },
        };
        var scan = Task.Run(() =>
        {
            try
            {
                racing.ScanInitialWithProgress(token, listener);
                return "completed";
            }
            catch (VaultException.Cancelled)
            {
                return "cancelled";
            }
            catch (Exception ex)
            {
                return $"managed:{ex.GetType().Name}";
            }
        });

        Assert.True(entered.Wait(TimeSpan.FromSeconds(30)), "scan never entered native dispatch");
        racing.Dispose(); // while the native call is provably in flight
        token.Cancel();
        gate.Set();

        // uniffi's call counter must keep the native handle alive until the
        // in-flight call returns — any outcome is fine as long as it's a
        // managed one and the call terminates.
        Assert.True(scan.Wait(TimeSpan.FromSeconds(60)), "dispose-during-scan call did not terminate");
    }
}
