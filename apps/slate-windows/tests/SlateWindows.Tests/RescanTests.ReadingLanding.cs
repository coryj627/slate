// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 beside PR 8 (#1253, contract R-10): a rescan's silent
/// re-projection and a reading surface's held focus landing. The rescan's
/// refresh is a refresh in flight like any other — a landing asked for during
/// it holds until it settles, and it settles the landing when it ends:
/// delivered on what it published (nothing spoken by Reading), refused when
/// it failed (silently — the rescan counts the failure), delivered on the
/// content it kept when the run is cancelled. A rescan's in-place reload of
/// the tab rebinds the surface to the reloaded projection, which withdraws a
/// held landing silently (PR 8's rebind rule). The surface is the production
/// one, bound to the tab's projection as the shell's template binds it, in a
/// shown window so keyboard focus really moves; the workspace runs on a pumped
/// dispatcher with production scheduling.
/// </summary>
public sealed partial class RescanTests
{
    [Fact]
    public void AHeldReadingLandingSettlesWithTheRescansSilentReprojection() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reprojection");
        using var hosted = new HostedReadingSurface(w.HostTab);
        ReadingContentViewModel reading = hosted.WaitForItsFirstMerge();
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        reading.FetchFaultForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            return null;
        };
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the rescan's re-projection parking");
        (Func<int> spoken, Func<int> fellThrough) = hosted.RequestTheLanding();

        Assert.True(
            hosted.Surface.IsFocusLandingPending,
            "the landing was seated on a projection the rescan's refresh is about to replace");
        Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);

        release.Set();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => dependents.IsCompleted && !hosted.Surface.IsFocusLandingPending),
            "the rescan's re-projection settling the held landing");
        Assert.Equal(0UL, dependents.Result);
        Assert.Same(hosted.Surface, Keyboard.FocusedElement);
        Assert.Equal(1, spoken());
        Assert.Equal(0, fellThrough());
        Assert.Empty(w.Announced);
    }));

    [Fact]
    public void AFailedRescanReprojectionRefusesTheHeldLandingSilently() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reprojection-fails");
        using var hosted = new HostedReadingSurface(w.HostTab);
        ReadingContentViewModel reading = hosted.WaitForItsFirstMerge();
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        reading.FetchFaultForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            return new InvalidOperationException("injected reading fetch failure");
        };
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the rescan's re-projection parking");
        (Func<int> spoken, Func<int> fellThrough) = hosted.RequestTheLanding();
        Assert.True(hosted.Surface.IsFocusLandingPending);

        release.Set();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => dependents.IsCompleted && !hosted.Surface.IsFocusLandingPending),
            "the failed re-projection settling the held landing");

        // Counted by the rescan, refused by the surface, spoken by neither
        // Reading nor the landing.
        Assert.Equal(1UL, dependents.Result);
        Assert.True(reading.LastRefreshFailed);
        Assert.Equal(0, spoken());
        Assert.Equal(1, fellThrough());
        Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
        Assert.Empty(w.Announced);
    }));

    [Fact]
    public void ACancelledRescanReprojectionStillSettlesTheHeldLanding() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reprojection-cancelled");
        using var hosted = new HostedReadingSurface(w.HostTab);
        ReadingContentViewModel reading = hosted.WaitForItsFirstMerge();
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        reading.FetchFaultForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            return null;
        };
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<ulong> dependents = w.ReSyncDependentsAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the rescan's re-projection parking");
        (Func<int> spoken, Func<int> fellThrough) = hosted.RequestTheLanding();
        Assert.True(hosted.Surface.IsFocusLandingPending);

        cancellation.Cancel();
        Assert.True(PumpedDispatcher.PumpUntil(() => dependents.IsCompleted), "the cancelled re-sync ending");
        Assert.True(dependents.IsCanceled);
        release.Set();

        // The cancelled fetch publishes nothing, and its refresh is over: the
        // landing settles on the content it kept — never left waiting for a
        // publication that is not coming.
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !hosted.Surface.IsFocusLandingPending),
            "the cancelled re-projection never settled the held landing");
        Assert.False(reading.RefreshInFlight);
        Assert.False(reading.LastRefreshFailed);
        Assert.Contains("Host", hosted.Text, StringComparison.Ordinal);
        Assert.Same(hosted.Surface, Keyboard.FocusedElement);
        Assert.Equal(1, spoken());
        Assert.Equal(0, fellThrough());
        Assert.Empty(w.Announced);
    }));

    [Fact]
    public void ARescanReloadOfTheReadingTabWithdrawsItsHeldLandingSilently() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload");
        using var hosted = new HostedReadingSurface(w.HostTab);
        ReadingContentViewModel before = hosted.WaitForItsFirstMerge();
        // A refresh of the shown projection in flight (the reader's return to
        // reading mode after an edit starts one), its fetch parked: a landing
        // asked for now is held.
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        before.FetchFaultForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            return null;
        };
        before.Refresh();
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the shown projection's refresh parking");
        (Func<int> spoken, Func<int> fellThrough) = hosted.RequestTheLanding();
        Assert.True(hosted.Surface.IsFocusLandingPending);
        w.Announced.Clear();

        // Changed outside Slate: the rescan's documents re-sync reloads the
        // clean tab in place, replacing its projection model.
        File.WriteAllText(Path.Combine(w.Root, "host.md"), "# Host\n\n![[embedded]]\n\nChanged outside Slate.\n");
        using (var scan = new uniffi.slate_uniffi.CancelToken())
        {
            _ = w.Session.Rescan(scan);
        }

        using var cancellation = new CancellationTokenSource();
        Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = w.ReSyncHostAsync(cancellation.Token);
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => documents.IsCompleted && hosted.Text.Contains("Changed outside Slate.", StringComparison.Ordinal)),
            "the reloaded note's projection");
        Assert.Equal(0UL, documents.Result.Failed);
        Assert.NotSame(before, hosted.Surface.Model);
        Assert.Same(w.HostTab.Reading, hosted.Surface.Model);

        // The old projection's parked fetch runs out: torn down, it publishes
        // nothing — and its teardown's refusal is stale.
        release.Set();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => before.WhenRefreshWorkDrained().IsCompleted),
            "the torn-down projection's fetch running out");
        PumpedDispatcher.Drain();

        Assert.False(hosted.Surface.IsFocusLandingPending);
        Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
        Assert.Equal(0, spoken());
        Assert.Equal(0, fellThrough());
        Assert.Empty(w.Announced);
    }));

    /// <summary>The production reading surface, bound to the tab's projection
    /// as the shell's template binds it, in a shown window beside a text box
    /// where the reader waits.</summary>
    private sealed class HostedReadingSurface : IDisposable
    {
        private readonly Window _window;

        public HostedReadingSurface(WorkspaceTabViewModel tab)
        {
            Sentinel = new TextBox { Text = "Where the reader waits" };
            Surface = new ReadingSurface { DataContext = tab };
            _ = Surface.SetBinding(ReadingSurface.ModelProperty, new Binding(nameof(WorkspaceTabViewModel.Reading)));
            var content = new DockPanel();
            DockPanel.SetDock(Sentinel, Dock.Top);
            _ = content.Children.Add(Sentinel);
            _ = content.Children.Add(Surface);
            _window = new Window
            {
                Content = content,
                Width = 600,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -2000,
                Top = -2000,
            };
            _window.Show();
            _ = _window.Activate();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public TextBox Sentinel { get; }

        public ReadingSurface Surface { get; }

        public string Text => new TextRange(Surface.Document.ContentStart, Surface.Document.ContentEnd).Text;

        /// <summary>The bound projection once the surface shows it, no refresh
        /// in flight — and the reader waiting on the text box.</summary>
        public ReadingContentViewModel WaitForItsFirstMerge()
        {
            ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(Surface.Model);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => !reading.RefreshInFlight && Text.Contains("Host", StringComparison.Ordinal)),
                "the surface's first merge");
            Assert.True(Sentinel.Focus());
            PumpedDispatcher.Drain();
            return reading;
        }

        /// <summary>The reading arm's landing, as the shell's editor landing
        /// asks for it: counting the line it would speak on arrival and the
        /// fall-through it would take when refused.</summary>
        public (Func<int> Spoken, Func<int> FellThrough) RequestTheLanding()
        {
            int spoken = 0;
            int fellThrough = 0;
            Assert.True(Surface.RequestFocusLanding(() => spoken++, () => fellThrough++));
            PumpedDispatcher.Drain();
            return (() => spoken, () => fellThrough);
        }

        public void Dispose()
        {
            _window.Close();
            PumpedDispatcher.Drain();
        }
    }
}
