// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Channels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class SidebarAnnouncementEtiquetteTests
{
    [Fact]
    public async Task DebouncedPublicationDedupsQueryAndTotalAcrossRefreshes()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "sidebar-etiquette");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken()) { session.ScanInitial(cancel); }
        var delays = Channel.CreateUnbounded<TaskCompletionSource>();
        var context = new PublicationContext();
        var announcements = new List<A11yEvent>();
        Task Delay(CancellationToken token)
        {
            var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.True(delays.Writer.TryWrite(delay));
            return delay.Task.WaitAsync(token);
        }
        var sidebar = new FilesSidebarViewModel(session, announcements.Add,
            filterUiContext: context, filterDelay: Delay,
            filterWorker: (work, token) => { token.ThrowIfCancellationRequested(); work(); return Task.CompletedTask; });
        announcements.Clear();

        TaskCompletionSource? latest = null;
        var pending = new List<Task>();
        foreach (string query in new[] { "n", "no", "not", "note" })
        {
            sidebar.FilterText = query;
            pending.Add(sidebar.FilterCompletion);
            latest = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Empty(announcements);
        Assert.Empty(sidebar.FilterResults);
        latest!.SetResult();
        await context.PublishNext();
        await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2u, Assert.Single(announcements.OfType<A11yEvent.FileListCount>()).Count);
        Assert.Equal(2, sidebar.FilterResults.Count);

        async Task CompleteNext()
        {
            TaskCompletionSource delay = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            delay.SetResult();
            await context.PublishNext();
            await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        sidebar.Refresh();
        await CompleteNext();
        Assert.Single(announcements.OfType<A11yEvent.FileListCount>());

        // Equal totals for a different query are distinct states.
        sidebar.FilterText = "not";
        await CompleteNext();
        Assert.Equal([2u, 2u], announcements.OfType<A11yEvent.FileListCount>().Select(e => e.Count));

        File.WriteAllText(Path.Combine(fixture.Root, "note-new.md"), "# New\n");
        using (var cancel = new CancelToken()) { session.ScanInitial(cancel); }
        sidebar.Refresh();
        await CompleteNext();
        Assert.Equal([2u, 2u, 3u], announcements.OfType<A11yEvent.FileListCount>().Select(e => e.Count));

        // Mac keeps the last successful key when the field is cleared.
        sidebar.FilterText = string.Empty;
        sidebar.FilterText = "not";
        await CompleteNext();
        Assert.Equal(3, announcements.OfType<A11yEvent.FileListCount>().Count());
    }

    private sealed class PublicationContext : SynchronizationContext
    {
        private readonly Channel<Action> _posts = Channel.CreateUnbounded<Action>();
        public override void Post(SendOrPostCallback callback, object? state) =>
            Assert.True(_posts.Writer.TryWrite(() => callback(state)));
        internal async Task PublishNext() =>
            (await _posts.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)))();
    }
}
