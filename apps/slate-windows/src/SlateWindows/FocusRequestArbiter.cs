// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// The window's focus requests, arbitrated by GENERATION: the editor's
/// request is deferred at Input priority and a boundary's at Normal, so a
/// boundary request raised AFTER an editor request would run first and the
/// older editor callback last — landing focus in the editor after the leaf
/// asked for it (W6-2 PR B2, IGL-3; codex post-implementation pass 1,
/// IPC-2: a group change pumped inside a re-root's dirty dialog). Every
/// request takes the next generation; a deferred callback runs only while
/// its generation is still the latest, so the LAST request raised is the
/// one that lands, whatever the priorities.
/// </summary>
internal sealed class FocusRequestArbiter
{
    private int _generation;

    /// <summary>The generation of the last request.</summary>
    public int Generation => _generation;

    /// <summary>Defer <paramref name="land"/> at <paramref name="priority"/>
    /// as the latest request; it runs only if no later request has been
    /// posted by then.</summary>
    public DispatcherOperation Post(Dispatcher dispatcher, DispatcherPriority priority, Action land)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(land);
        int mine = ++_generation;
        return dispatcher.InvokeAsync(
            () =>
            {
                if (mine == _generation)
                {
                    land();
                }
            },
            priority);
    }
}
