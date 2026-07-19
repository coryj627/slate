// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-E cancellation latency (w0_spec §W0-3 item 2, #715): cancel mid-scan
// of a large fixture vault and require a bounded stop plus the terminal
// Cancelled progress event; pre-cancelled tokens short-circuit. Seeded
// from the W0-1 probe's cancellation section.

using System.Diagnostics;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "cancellation")]
public class CancellationCensus
{
    [Fact]
    public void MidScanCancel_BoundedLatencyAndTerminalCancelledEvent()
    {
        int attempts = CensusTier.Scale(2, 3);
        int notes = CensusTier.Scale(300, 500);
        var latencies = new List<double>();
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            using var vault = FixtureVault.Create(notes, $"cancel{attempt}");
            using var session = VaultSession.OpenFilesystem(vault.Root);
            using var token = new CancelToken();
            var firstFile = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var recorder = new ProgressRecorder
            {
                OnEvent = e =>
                {
                    if (e is ScanProgress.FileIndexed)
                    {
                        firstFile.TrySetResult();
                    }
                },
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

            Assert.True(firstFile.Task.Wait(TimeSpan.FromSeconds(30)), "scan produced no FileIndexed within 30s");
            sw.Start();
            token.Cancel();
            Assert.True(scan.Wait(TimeSpan.FromSeconds(10)), "scan did not return within 10s of cancel");
            // A debug-build index of hundreds of files takes far longer than
            // one event dispatch; completion before cancel means the fixture
            // was too small to measure.
            Assert.Equal("cancelled", scan.Result);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
            Assert.Contains(recorder.Snapshot(), e => e is ScanProgress.Cancelled);
        }

        Assert.True(latencies.Max() < 5000, $"worst cancel latency unbounded ({latencies.Max():F0} ms)");
    }

    [Fact]
    public void PreCancelledToken_ShortCircuitsAsTypedCancelled()
    {
        using var vault = FixtureVault.Create(3);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        token.Cancel();
        Assert.True(token.IsCancelled());
        Assert.Throws<VaultException.Cancelled>(() => session.ScanInitial(token));
    }
}
