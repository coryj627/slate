// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Channels;

namespace SlateWindows.Tests;

/// <summary>A synchronization context that channels every Post so a test
/// decides when each owner-context publication runs: <see cref="Next"/>
/// hands the callback back to be invoked (or retired) deliberately,
/// <see cref="PublishNext"/> runs it. One copy for the etiquette and
/// recovery facts; a timeout bounds a hung publication.</summary>
internal sealed class PublicationContext : SynchronizationContext
{
    private readonly Channel<Action> _posts = Channel.CreateUnbounded<Action>();

    public override void Post(SendOrPostCallback callback, object? state) =>
        Assert.True(_posts.Writer.TryWrite(() => callback(state)));

    internal Task<Action> Next() => _posts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

    internal bool HasPending => _posts.Reader.TryPeek(out _);

    internal async Task PublishNext() => (await Next())();
}
