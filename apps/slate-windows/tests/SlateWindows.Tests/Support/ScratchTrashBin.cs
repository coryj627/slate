// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1455: this test process's scratch Trash bin. Every vault the suite opens
/// trashes into it, whether through the shell's own open
/// (<c>VaultLifecycleViewModel</c> calls the shipping
/// <c>VaultSession.OpenFilesystem</c>) or a session a test opens itself, so
/// no fact moves its fixtures into the Recycle Bin of whoever runs the suite.
/// </summary>
/// <remarks>
/// Core routes every session of one process into one bin, once something
/// asks it before the first session opens. The module initializer below asks
/// as the test assembly loads, before xUnit runs any test.
/// <c>ScratchTrashCensus</c> keeps it so.
/// </remarks>
internal static class ScratchTrashBin
{
    /// <summary>
    /// The bin's folder: in this process's temp directory, where
    /// <see cref="FixtureVault"/> makes the vaults, and removed at exit.
    /// </summary>
    internal static string Folder { get; private set; } = string.Empty;

    /// <summary>
    /// Runs once, as the test assembly loads: makes the bin and routes the
    /// process's Trash into it through core. If core refuses, the load fails
    /// rather than let a fact trash into the Recycle Bin.
    /// </summary>
#pragma warning disable CA2255 // The test assembly's one load hook: the routing must precede every fact.
    [ModuleInitializer]
    internal static void RouteThisProcessIntoAScratchBin()
#pragma warning restore CA2255
    {
        string bin = Path.Combine(
            Path.GetTempPath(),
            $"slate-scratch-trash-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(bin);
        SlateUniffiMethods.RouteTrashToScratchBinForTests(bin);
        Folder = bin;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(bin, recursive: true);
            }
            catch (IOException)
            {
                // Best effort, like FixtureVault: a leaked temp folder is harmless.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
    }
}
