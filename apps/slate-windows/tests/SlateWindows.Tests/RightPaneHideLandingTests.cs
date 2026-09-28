// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b round 2 (codex r1 F1; R-5 (h), "never … the window"): hiding the
/// right pane — Ctrl+Alt+I, the View menu, the palette — with the keys in it
/// collapses every scope they were in, and WPF's re-evaluation found no
/// focusable ancestor short of the window: the keys went to the MainWindow.
/// The three workspace columns now land them in the editor region, inside
/// that re-evaluation — one focus change. The shipped window, shown, over a
/// real workspace with a markdown note open.
/// </summary>
public sealed class RightPaneHideLandingTests
{
    [Theory]
    [InlineData("RightPaneRail")]
    [InlineData("RightPaneContent")]
    public void HidingTheRightPaneUnderTheKeysLandsThemInTheEditorOnce(string origin) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Just a line of text.\n"));
        host.Workspace.OpenPath("a.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");
        host.Settle();
        var shell = (IShellRegionHost)host.Shell;
        Assert.True(shell.TryLand(Enum.Parse<ShellRegionKind>(origin)), $"premise: {origin} took no keys");
        Assert.True(host.Shell.RightPaneBorder.IsKeyboardFocusWithin, "premise: the keys are not in the right pane");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Workspace.ToggleRightPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.False(host.Workspace.IsRightPaneVisible);
        Assert.False(host.Shell.RightPaneBorder.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.Editor, shell.FocusedRegion());
        Assert.Single(changes);
    });

    /// <summary>The review's "Load more" owns a landing of its own — the row
    /// its page appended — which the hidden pane cannot take; the keys still
    /// land in the editor, once.</summary>
    [Fact]
    public void HidingTheRightPaneUnderLoadMoreLandsTheKeysInTheEditorOnce() => RunSta(() =>
    {
        string tasks = string.Concat(Enumerable.Range(0, (int)TasksReviewViewModel.PageSize + 5).Select(index => $"- [ ] task {index:D3}\n"));
        using var host = new ShownShell(("a.md", "Just a line of text.\n"), ("todo.md", tasks));
        host.Workspace.OpenPath("a.md");
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "tasksReview");
        TasksReviewViewModel review = host.Workspace.TasksReview;
        review.EnsureLoaded();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => review.HasMore && !review.IsLoading, TimeSpan.FromSeconds(30)),
            "premise: the review never published a page with more to load.");
        host.Settle();
        Button loadMore = host.Shell.PanelReviewLoadMore;
        Assert.True(loadMore.IsVisible && loadMore.Focus(), "premise: Load more refused the keys.");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Workspace.ToggleRightPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.False(host.Shell.RightPaneBorder.IsVisible);
        Assert.IsNotType<MainWindow>(Keyboard.FocusedElement);
        Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)host.Shell).FocusedRegion());
        Assert.Single(changes);
    });

    /// <summary>The shipped window, shown off-screen and never activated,
    /// over a real workspace attached through the lifecycle's own
    /// setters.</summary>
    private sealed class ShownShell : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(0, "right-pane-hide");
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly System.Reflection.PropertyInfo _slot =
            typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
            ?? throw new InvalidOperationException("Workspace is gone");
        private readonly System.Reflection.PropertyInfo _open =
            typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen))
            ?? throw new InvalidOperationException("IsVaultOpen is gone");

        public ShownShell(params (string Path, string Text)[] notes)
        {
            Assert.Null(Application.Current);
            foreach ((string path, string text) in notes)
            {
                File.WriteAllText(Path.Combine(_fixture.Root, path), text);
            }

            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Workspace = new WorkspaceViewModel(
                _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: true);
            Shell = new MainWindow
            {
                ShowActivated = false,
                ShowInTaskbar = false,
                Width = 1200,
                Height = 800,
            };
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            _slot.SetValue(_lifecycle, Workspace);
            _open.SetValue(_lifecycle, true);
            Shell.Show();
            Settle();
        }

        public MainWindow Shell { get; }

        public WorkspaceViewModel Workspace { get; }

        public void Settle()
        {
            Shell.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>Every keyboard focus change in the shell from here.</summary>
        public List<IInputElement> RecordFocusChanges()
        {
            var changes = new List<IInputElement>();
            Keyboard.AddGotKeyboardFocusHandler(Shell, (_, e) => changes.Add(e.NewFocus));
            return changes;
        }

        public void Dispose()
        {
            try
            {
                _open.SetValue(_lifecycle, false);
                _slot.SetValue(_lifecycle, null);
                Shell.Close();
                Workspace.Dispose();
            }
            finally
            {
                _session.Dispose();
                _fixture.Dispose();
            }
        }
    }

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "STA test body timed out.");
        CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
