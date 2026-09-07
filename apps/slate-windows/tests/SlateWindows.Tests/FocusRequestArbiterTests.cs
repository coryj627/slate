// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B2 (#746), IGL-3 — codex post-implementation pass 1, IPC-2: the
/// window's deferred focus requests land by GENERATION, so the last one
/// raised is the one that lands whatever the dispatcher priorities — the
/// editor's request (Input, the lower) raised inside a re-root's dialog by
/// a group change must not outlive the leaf's boundary request (Normal,
/// the higher) raised after it.
/// </summary>
public sealed class FocusRequestArbiterTests
{
    [Fact]
    public void TheLastRequestRaisedLandsWhateverThePriorities()
    {
        PumpedDispatcher.Run(() =>
        {
            var arbiter = new FocusRequestArbiter();
            var landed = new List<string>();
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            // The editor first at Input, the boundary after it at Normal:
            // Normal runs first, and the older Input callback then finds
            // itself superseded.
            arbiter.Post(dispatcher, DispatcherPriority.Input, () => landed.Add("editor"));
            arbiter.Post(dispatcher, DispatcherPriority.Normal, () => landed.Add("boundary"));
            PumpedDispatcher.Drain();
            Assert.Equal(["boundary"], landed);

            // The reverse: the boundary first, the editor after it — the
            // editor lands, the boundary is superseded although it would
            // have run first.
            landed.Clear();
            arbiter.Post(dispatcher, DispatcherPriority.Normal, () => landed.Add("boundary"));
            arbiter.Post(dispatcher, DispatcherPriority.Input, () => landed.Add("editor"));
            PumpedDispatcher.Drain();
            Assert.Equal(["editor"], landed);

            // A lone request lands.
            landed.Clear();
            arbiter.Post(dispatcher, DispatcherPriority.Input, () => landed.Add("editor"));
            PumpedDispatcher.Drain();
            Assert.Equal(["editor"], landed);
            Assert.Equal(5, arbiter.Generation);
        });
    }
}
