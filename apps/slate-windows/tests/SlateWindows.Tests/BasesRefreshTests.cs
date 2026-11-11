// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls.Primitives;
using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1692: the explicit refresh (the palette's Bases: Refresh and the surface
/// header's Refresh button) says "Base refreshed." only once its reload has
/// published a result. A reload that fails says nothing of its own; its
/// failed state speaks for itself, as Retry's does.
/// </summary>
public sealed class BasesRefreshTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<(A11yEvent Event, BaseLoadState? State)> _announced = [];
    private BaseDocumentViewModel? _watched;

    public BasesRefreshTests()
    {
        _fixture = FixtureVault.Create(1, "bases-refresh");
        File.WriteAllText(Path.Combine(_fixture.Root, "Notes.base"), OneView);
        File.WriteAllText(Path.Combine(_fixture.Root, "a.md"), "# A\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    private const string OneView =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n";

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    /// <summary>Each sentence, with the watched document's state when it
    /// was spoken: "Base refreshed." must follow the result it reports.</summary>
    private void Announce(A11yEvent spoken)
    {
        lock (_announced)
        {
            _announced.Add((spoken, _watched?.State));
        }
    }

    private (A11yEvent Event, BaseLoadState? State)[] Refreshed()
    {
        lock (_announced)
        {
            return [.. _announced.Where(entry => entry.Event is A11yEvent.BaseRefreshed)];
        }
    }

    /// <summary>The issue's report: the open base's file is deleted, then
    /// Refresh runs from the palette or the header. The reload fails, and
    /// "Base refreshed." is never spoken.</summary>
    [Theory]
    [InlineData("palette")]
    [InlineData("header")]
    public void ARefreshWhoseReloadFailsSaysNothingOfItsOwn(string from) => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        (WorkspaceTabViewModel tab, BaseDocumentViewModel document) = OpenNotes(workspace);
        File.Delete(Path.Combine(_fixture.Root, "Notes.base"));
        Clear();

        Refresh(from, workspace, tab, document);

        Assert.Equal(BaseLoadState.Failed, document.State);
        Assert.Empty(Refreshed());
    });

    /// <summary>The open succeeds but the execute fails (Codex round 2's
    /// coverage note): the base is published Degraded, an operation failure
    /// (the rows it showed stay), so "Base refreshed." is not said.</summary>
    [Theory]
    [InlineData("palette")]
    [InlineData("header")]
    public void ARefreshWhoseExecuteFailsSaysNothingOfItsOwn(string from) => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        (WorkspaceTabViewModel tab, BaseDocumentViewModel document) = OpenNotes(workspace);
        document.ExecuteFaultForTests = () => new VaultException.Db("database is locked");
        Clear();

        Refresh(from, workspace, tab, document);

        Assert.Equal(BaseLoadState.Degraded, document.State);
        Assert.Empty(Refreshed());
    });

    /// <summary>A reload that publishes a result is spoken once, after the
    /// result: the sentence never reports a base still loading.</summary>
    [Theory]
    [InlineData("palette")]
    [InlineData("header")]
    public void ARefreshThatPublishesAResultSaysBaseRefreshedOnce(string from) => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        (WorkspaceTabViewModel tab, BaseDocumentViewModel document) = OpenNotes(workspace);
        Clear();

        Refresh(from, workspace, tab, document);

        Assert.Equal(BaseLoadState.Ready, document.State);
        (_, BaseLoadState? state) = Assert.Single(Refreshed());
        Assert.Equal(BaseLoadState.Ready, state);
    });

    /// <summary>The app's own shape: the reload runs on the document's
    /// worker and publishes on the dispatcher after the refresh has
    /// returned. A failure that lands then still says nothing of its own;
    /// a result that lands then is spoken after it, once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAsynchronousRefreshSpeaksOnlyForTheResultItsReloadPublished(bool fileDeleted)
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump);
        _watched = document;
        document.Load();
        await Settle(document, pump, () => document.State != BaseLoadState.Loading);
        Assert.Equal(BaseLoadState.Ready, document.State);
        if (fileDeleted)
        {
            File.Delete(Path.Combine(_fixture.Root, "Notes.base"));
        }
        Clear();

        Task spoken = document.RefreshByUserAsync();
        await Settle(document, pump, () => spoken.IsCompleted);

        if (fileDeleted)
        {
            Assert.Equal(BaseLoadState.Failed, document.State);
            Assert.Empty(Refreshed());
        }
        else
        {
            Assert.Equal(BaseLoadState.Ready, document.State);
            (_, BaseLoadState? state) = Assert.Single(Refreshed());
            Assert.Equal(BaseLoadState.Ready, state);
        }
        document.Shutdown();
    }

    /// <summary>Codex round 1: two refreshes started before either has
    /// published share the one surviving result, the second's, so
    /// "Base refreshed." is spoken once, for it.</summary>
    [Fact]
    public async Task TwoOverlappingRefreshesSpeakOnceForTheSurvivingResult()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump);
        _watched = document;
        document.Load();
        await Settle(document, pump, () => document.State != BaseLoadState.Loading);
        Clear();

        Task first = document.RefreshByUserAsync();
        Task second = document.RefreshByUserAsync();
        await Settle(document, pump, () => first.IsCompleted && second.IsCompleted);

        Assert.Equal(BaseLoadState.Ready, document.State);
        (_, BaseLoadState? state) = Assert.Single(Refreshed());
        Assert.Equal(BaseLoadState.Ready, state);
        document.Shutdown();
    }

    /// <summary>Codex round 1: a refresh superseded by a load that is then
    /// cancelled (a rescan's) published nothing, and neither did the load:
    /// the base is still loading, so "Base refreshed." is not said.</summary>
    [Fact]
    public async Task ARefreshSupersededByACancelledLoadSaysNothing()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump);
        _watched = document;
        document.Load();
        await Settle(document, pump, () => document.State != BaseLoadState.Loading);
        Clear();

        Task refresh = document.RefreshByUserAsync();
        using var rescan = new CancellationTokenSource();
        Task superseding = document.LoadAsync(rescan.Token);
        rescan.Cancel();
        await Settle(document, pump, () => refresh.IsCompleted && superseding.IsCompleted);

        Assert.True(superseding.IsCanceled, "premise: the superseding load was cancelled");
        Assert.Equal(BaseLoadState.Loading, document.State);
        Assert.Empty(Refreshed());
        document.Shutdown();
    }

    /// <summary>A document closed while its refresh reloads speaks nothing
    /// for it.</summary>
    [Fact]
    public async Task ARefreshOfADocumentShutDownMeanwhileSaysNothing()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump);
        _watched = document;
        document.Load();
        await Settle(document, pump, () => document.State != BaseLoadState.Loading);
        Clear();

        Task spoken = document.RefreshByUserAsync();
        document.Shutdown();
        await Settle(document, pump, () => spoken.IsCompleted);

        Assert.Empty(Refreshed());
    }

    private void Clear()
    {
        lock (_announced)
        {
            _announced.Clear();
        }
    }

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _fixture.Root, () => [], Announce, startInteractionBackgroundWork: false);

    private (WorkspaceTabViewModel Tab, BaseDocumentViewModel Document) OpenNotes(WorkspaceViewModel workspace)
    {
        workspace.OpenPath("Notes.base", WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Equal(BaseLoadState.Ready, document.State);
        _watched = document;
        return (tab, document);
    }

    private static void Refresh(string from, WorkspaceViewModel workspace, WorkspaceTabViewModel tab, BaseDocumentViewModel document)
    {
        if (from == "palette")
        {
            Assert.True(workspace.BasesRefreshCommand.CanExecute(null));
            workspace.BasesRefreshCommand.Execute(null);
        }
        else
        {
            var surface = new BaseSurfaceView { Tab = tab, Model = document };
            surface.RefreshForTests.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
    }

    private BaseDocumentViewModel NewAsyncDocument(SynchronizationContext context)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return new BaseDocumentViewModel(_session, "Notes.base", Announce, synchronousForTests: false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Drains the document's worker and the pump until
    /// <paramref name="done"/> holds, within a bound.</summary>
    private static async Task Settle(BaseDocumentViewModel document, PumpSynchronizationContext pump, Func<bool> done)
    {
        var bound = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            await document.DrainForTests();
            pump.Drain();
            if (done())
            {
                return;
            }
            Assert.True(bound.Elapsed < TimeSpan.FromSeconds(30), "the refresh did not settle within 30 s");
            await Task.Delay(10);
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Enqueue((callback, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
