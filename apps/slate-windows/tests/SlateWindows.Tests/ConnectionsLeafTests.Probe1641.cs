// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;

namespace SlateWindows.Tests;

/// <summary>
/// THROWAWAY PROBE for #1641 (never merges): a real STA timeout on a CI
/// runner writes its full dump into the job's uploaded results folder, so
/// `dotnet-dump analyze` and `clrstack -all` can be run on a CI dump. The
/// name puts it under the model shards' filter, so it runs on each shard's
/// pool; <see cref="Probe1641AppJobTests"/> runs it in the app job.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    [Fact]
    public void TheModelOfProbe1641WritesARealDump() =>
        Probe1641.Run(Environment.GetEnvironmentVariable("SLATE_MODEL_REPORT_DIR"));

    internal static class Probe1641
    {
        internal static void Run(string? folder)
        {
            Assert.False(string.IsNullOrEmpty(folder), "this job names no dump folder");
            string? previous = StaThread.DumpDirectory;
            StaThread.DumpDirectory = folder;
            StaThread.ResetDumpsForTests();
            try
            {
                Xunit.Sdk.XunitException timedOut = Assert.Throws<Xunit.Sdk.XunitException>(() => StaThread.Run(
                    BlockedInProbe1641,
                    TimeSpan.FromSeconds(2),
                    "probe 1641",
                    unwindGrace: TimeSpan.FromSeconds(90)));
                Console.WriteLine("PROBE-1641 " + timedOut.Message);
                Assert.Contains("A full dump of the test process is at ", timedOut.Message, StringComparison.Ordinal);
            }
            finally
            {
                StaThread.DumpDirectory = previous;
                StaThread.ResetDumpsForTests();
            }
        }

        /// <summary>The frame clrstack -all must show on the body's thread.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BlockedInProbe1641()
        {
            using var never = new ManualResetEventSlim();
            _ = never.Wait(TimeSpan.FromSeconds(30));
        }
    }
}

/// <summary>THROWAWAY PROBE for #1641: the same, in the app job.</summary>
public sealed class Probe1641AppJobTests
{
    [Fact]
    public void Probe1641AppJobWritesARealDump() =>
        ConnectionsLeafTests.Probe1641.Run(Environment.GetEnvironmentVariable("SLATE_STA_DUMP_DIR"));
}
