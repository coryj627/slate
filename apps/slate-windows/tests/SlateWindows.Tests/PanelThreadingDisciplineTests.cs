// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1129: the panel threading discipline. A PanelWorkScheduler's
/// publishes post to the SynchronizationContext captured at
/// construction, and only WPF's dispatcher context runs them on the
/// constructing thread — a null context runs them inline on the
/// worker and a test host's context (xunit's) hands them to the pool.
/// A workspace built headlessly with background work enabled therefore
/// raced its own panels against the test thread
/// (HistoryViewModel.PruneCompareSelection enumerated _loaded while a
/// tab switch's reset cleared it; intermittent on CI, in a test that
/// never touched the panel). The rule pinned here: when the caller
/// does not say, interaction work runs in the background ONLY under
/// the dispatcher and inline everywhere else; an explicit request is
/// honored either way (a fact that wants production scheduling
/// headlessly says so and owns the drain/seam discipline — the
/// *AsyncInterleavingTests shape).
/// </summary>
public sealed class PanelThreadingDisciplineTests
{
    [Fact]
    public void OnlyTheWpfDispatcherContextCountsAsUi()
    {
        // A test method's own context (xunit's) is not a UI context.
        Assert.False(PanelWorkScheduler.CurrentContextIsUiDispatcher());
        UnderContext(
            null,
            () => Assert.False(PanelWorkScheduler.CurrentContextIsUiDispatcher()));
        UnderContext(
            new PoolPostingContext(),
            () => Assert.False(PanelWorkScheduler.CurrentContextIsUiDispatcher()));
        UnderContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            () => Assert.True(PanelWorkScheduler.CurrentContextIsUiDispatcher()));
    }

    [Fact]
    public void AHeadlessWorkspaceRunsItsPanelsInline()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "panel-discipline-headless");
        using VaultSession session = OpenScanned(fixture.Root);
        // The DEFAULT flag — the shape of every pre-W4 workspace fact
        // and of the lifecycle's own InitializeWorkspace.
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { });

        Assert.False(workspace.StartsInteractionBackgroundWorkForTests);
        Assert.True(workspace.History.IsSynchronousForTests);
        Assert.True(workspace.Panels.IsSynchronousForTests);
        Assert.True(workspace.Citations.IsSynchronousForTests);
        Assert.True(workspace.Bibliography.IsSynchronousForTests);
        Assert.True(workspace.TasksReview.IsSynchronousForTests);

        // The #1129 schedule — back-to-back activations. Inline, each
        // history load publishes before the next activation's reset
        // can touch the rows it enumerates.
        workspace.OpenPath("note0.md");
        workspace.OpenPath("note1.md");
        Assert.Equal("note1.md", workspace.History.Path);
        Assert.False(workspace.History.IsLoading);
    }

    [Fact]
    public void ADispatcherHostedWorkspaceKeepsBackgroundWork()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-discipline-dispatcher");
        using VaultSession session = OpenScanned(fixture.Root);
        UnderContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            () =>
            {
                using var workspace = new WorkspaceViewModel(
                    session, fixture.Root, () => [], _ => { });
                Assert.True(workspace.StartsInteractionBackgroundWorkForTests);
                Assert.False(workspace.History.IsSynchronousForTests);
                Assert.False(workspace.Panels.IsSynchronousForTests);
            });
    }

    [Fact]
    public void AnExplicitBackgroundRequestIsHonoredHeadlessly()
    {
        // The citation interleaving suite's escape: a fact that asserts
        // production scheduling under the test host's context says so
        // and owns the drain/seam discipline that makes it safe.
        using FixtureVault fixture = FixtureVault.Create(0, "panel-discipline-explicit-bg");
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { },
            startInteractionBackgroundWork: true);
        Assert.True(workspace.StartsInteractionBackgroundWorkForTests);
        Assert.False(workspace.History.IsSynchronousForTests);
    }

    [Fact]
    public void AnExplicitInlineRequestIsHonoredUnderTheDispatcherToo()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-discipline-explicit");
        using VaultSession session = OpenScanned(fixture.Root);
        UnderContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher),
            () =>
            {
                using var workspace = new WorkspaceViewModel(
                    session, fixture.Root, () => [], _ => { },
                    startInteractionBackgroundWork: false);
                Assert.False(workspace.StartsInteractionBackgroundWorkForTests);
                Assert.True(workspace.History.IsSynchronousForTests);
            });
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static void UnderContext(SynchronizationContext? context, Action body)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            body();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>The xunit shape: a context that exists but runs its
    /// posts on the pool — the case the rule must NOT treat as UI.</summary>
    private sealed class PoolPostingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ => callback(state));
    }
}
