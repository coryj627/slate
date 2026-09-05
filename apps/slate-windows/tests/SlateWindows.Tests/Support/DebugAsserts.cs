// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;

namespace SlateWindows.Tests;

/// <summary>
/// Scope a DELIBERATE trip of a <c>Debug.Fail</c> guard, so the fact can
/// assert the guard's BEHAVIOUR without the diagnostic aborting it.
/// </summary>
/// <remarks>
/// <para>
/// A handful of facts exist to prove what happens when production code
/// is misused — "a caller that outlived its document reaches the
/// announcer and NOTHING is spoken" is the canvas's (contract A5). The
/// production guard marks that misuse with <c>Debug.Fail</c>, which is
/// correct and valuable: a path that outlived its document should be
/// loud in a developer's build. The two are in direct conflict, and the
/// conflict is only visible in DEBUG, where the test host converts the
/// assert into an exception.
/// </para>
/// <para>
/// This is the reconciliation, and its narrowness is the point. It is
/// wrapped around the ONE deliberate call, never around a whole test
/// body and never around a production teardown — so a `Debug.Fail` that
/// fires because the SHIPPED code posted after retirement still fails
/// the run. That distinction is what makes the Debug suite worth
/// running: it was red for three canvas facts, and a red suite is where
/// a real one hides.
/// </para>
/// <para>
/// The listener collection is what the test host installs its
/// assert-to-exception listener into, so clearing and restoring it is
/// the whole mechanism.
/// </para>
/// </remarks>
internal static class DebugAsserts
{
    /// <summary>Suppress assert dialogs/exceptions until disposed.</summary>
    internal static IDisposable Suppressed() => new Scope();

    private sealed class Scope : IDisposable
    {
        private readonly TraceListener[] _saved;

        internal Scope()
        {
            _saved = [.. Trace.Listeners.Cast<TraceListener>()];
            Trace.Listeners.Clear();
        }

        public void Dispose()
        {
            Trace.Listeners.Clear();
            Trace.Listeners.AddRange(_saved);
        }
    }
}
