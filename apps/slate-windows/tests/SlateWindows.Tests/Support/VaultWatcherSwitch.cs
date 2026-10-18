// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 43 PR 20b (#1267; design §4.7, the test switch): every vault this
/// test process opens keeps core's vault watcher off. The lifecycle arms the
/// watcher on every open, so a fact that changes a vault from outside and
/// then asserts what a Refresh counted would otherwise race a Watcher run,
/// which could index the change first and leave the Refresh saying "No
/// changes". Core's <c>start_watching</c> reads
/// <c>SLATE_TEST_DISABLE_VAULT_WATCHER</c> at every call; the watcher facts
/// lift it for their own body (<see cref="Lift"/>).
/// </summary>
internal static class VaultWatcherSwitch
{
    internal const string Variable = "SLATE_TEST_DISABLE_VAULT_WATCHER";

    /// <summary>Runs once, as the test assembly loads, before xUnit runs any
    /// fact: the process environment is the one core reads.</summary>
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "The switch must precede every vault the suite opens.")]
    internal static void KeepTheWatcherOffInThisProcess() =>
        Environment.SetEnvironmentVariable(Variable, "1");

    /// <summary>Lift the switch for one fact: the vaults it opens until the
    /// returned handle is disposed arm the real watcher. Serialized with the
    /// other process-global switches (<see cref="EnvFaultLock"/>).</summary>
    internal static IDisposable Lift()
    {
        IDisposable gate = EnvFaultLock.Acquire();
        Environment.SetEnvironmentVariable(Variable, null);
        return new Restore(gate);
    }

    private sealed class Restore(IDisposable gate) : IDisposable
    {
        public void Dispose()
        {
            KeepTheWatcherOffInThisProcess();
            gate.Dispose();
        }
    }
}
