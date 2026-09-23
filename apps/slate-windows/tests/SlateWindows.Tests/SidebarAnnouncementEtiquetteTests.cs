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

        // W7-7 (R-3, codex PR 2 round 2): the emptied field is the user's
        // clear, heard once as core's SidebarFilterCleared — and after the
        // listener heard the filter end, the same query is news again, so
        // its count is spoken. (Mac keeps the last key across its silent
        // clear; Windows speaks the clear, so the de-duplication starts
        // over.)
        sidebar.FilterText = string.Empty;
        Assert.IsType<A11yEvent.SidebarFilterCleared>(announcements[^1]);
        sidebar.FilterText = "not";
        await CompleteNext();
        Assert.Equal([2u, 2u, 3u, 3u], announcements.OfType<A11yEvent.FileListCount>().Select(e => e.Count));
        Assert.Single(announcements.OfType<A11yEvent.SidebarFilterCleared>());
    }

    /// <summary>W7-7 (R-3, codex round 4): a tag scope's count is
    /// de-duplicated on (query, scope, total), like a typed query's. The
    /// same scope refreshed with the same total stays quiet; another scope
    /// with an equal total speaks, naming its tag; and the same scope
    /// speaks again when a rescan changes its total.</summary>
    [Fact]
    public async Task ScopedPublicationDedupsQueryScopeAndTotalAcrossRefreshes()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "sidebar-scope-etiquette");
        WriteTagged(fixture, "a.md", "two words");
        WriteTagged(fixture, "b.md", "two words");
        WriteTagged(fixture, "c.md", "blue sky");
        WriteTagged(fixture, "d.md", "blue sky");
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

        async Task CompleteNext()
        {
            TaskCompletionSource delay = await delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            delay.SetResult();
            await context.PublishNext();
            await sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(5));
        }
        A11yEvent.FileListCount[] Counts() => [.. announcements.OfType<A11yEvent.FileListCount>()];

        sidebar.ActivateTag("two words");
        await CompleteNext();
        Assert.Equal([new A11yEvent.FileListCount(2, "two words")], Counts());

        // The same (query, scope, total) after a refresh is not news.
        sidebar.Refresh();
        await CompleteNext();
        Assert.Single(Counts());

        // An equal total under another scope is a distinct state.
        sidebar.ActivateTag("blue sky");
        await CompleteNext();
        Assert.Equal(
            [new A11yEvent.FileListCount(2, "two words"), new A11yEvent.FileListCount(2, "blue sky")],
            Counts());

        // The same scope speaks again when a rescan changes its total.
        WriteTagged(fixture, "e.md", "blue sky");
        using (var cancel = new CancelToken()) { session.ScanInitial(cancel); }
        sidebar.Refresh();
        await CompleteNext();
        Assert.Equal(
            [
                new A11yEvent.FileListCount(2, "two words"),
                new A11yEvent.FileListCount(2, "blue sky"),
                new A11yEvent.FileListCount(3, "blue sky"),
            ],
            Counts());
    }

    private static void WriteTagged(FixtureVault fixture, string name, string tag) =>
        File.WriteAllText(Path.Combine(fixture.Root, name), $"---\ntags: [\"{tag}\"]\n---\n\n# {name}\n");
}
