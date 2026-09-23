// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, OD-1, R-9): the main window's foreground rescan route.
/// With no live watcher, a window that comes back after being away rescans
/// — silently unless something changed — so a file made in another app is
/// there when the user returns.
/// </summary>
/// <remarks>
/// <para>
/// <c>MainWindow.Deactivated</c> records the time; <c>Activated</c>
/// forwards a <see cref="RescanReason.Foreground"/> request when the window
/// was away at least <see cref="MinimumAway"/> and no modal surface is
/// open. The very first activation never forwards: the open scan just ran.
/// </para>
/// <para>
/// Everything else is the lifecycle coalescer's
/// (<see cref="VaultLifecycleViewModel.RescanAsync"/>): only the initial
/// open scan, an import or a trash operation blocks the request; one that
/// arrives during a running rescan joins its pending-reason lattice — never
/// dropped, never subject to the cooldown — and the ≥ 5 s cooldown applies
/// only to starting a run. The route deliberately does not second-guess
/// that: a route that skipped while any scan was in flight would drop the
/// changes made after the running scan's snapshot.
/// </para>
/// </remarks>
internal sealed class ForegroundRescanRoute
{
    /// <summary>How long the window must have been away.</summary>
    internal static readonly TimeSpan MinimumAway = TimeSpan.FromSeconds(2);

    private readonly Func<RescanReason, Task> _rescan;
    private readonly Func<bool> _modalSurfaceOpen;
    private readonly Func<DateTimeOffset> _clock;
    private bool _activatedBefore;
    private DateTimeOffset? _deactivatedAt;

    public ForegroundRescanRoute(
        Func<RescanReason, Task> rescan,
        Func<bool> modalSurfaceOpen,
        Func<DateTimeOffset>? clock = null)
    {
        _rescan = rescan;
        _modalSurfaceOpen = modalSurfaceOpen;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The window lost activation: the away time starts.</summary>
    public void OnDeactivated() => _deactivatedAt = _clock();

    /// <summary>The window was activated. Returns the forwarded rescan, or
    /// null when the route did not forward one.</summary>
    public Task? OnActivated()
    {
        DateTimeOffset? deactivatedAt = _deactivatedAt;
        _deactivatedAt = null;
        if (!_activatedBefore)
        {
            _activatedBefore = true;
            return null;
        }

        if (deactivatedAt is not DateTimeOffset away
            || _clock() - away < MinimumAway
            || _modalSurfaceOpen())
        {
            return null;
        }

        return _rescan(RescanReason.Foreground);
    }
}
