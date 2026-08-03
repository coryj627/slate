// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>Serializes tests that mutate the process-global core
/// fault-seam environment variables (SLATE_TEST_FAULT_AFTER_WRITE
/// and friends): xunit runs test classes in parallel, and two tests
/// setting different trigger values overwrite each other — the
/// loser's fault never fires and its assertions flake. Acquire at
/// the top of any test that touches the fault env vars.</summary>
internal static class EnvFaultLock
{
    private static readonly object Gate = new();

    public static IDisposable Acquire()
    {
        Monitor.Enter(Gate);
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose() => Monitor.Exit(Gate);
    }
}
