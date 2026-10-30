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
/// the tab rebinds the surface to the reloaded projection; WB-9 (#1312,
/// R-9/R-10) carries a held landing across that rebind, under the very token
/// the window's slot captured, to the reloaded projection's settle — and
/// refuses it once when no replacement projection of the tab is bound. The
/// surface is the production one, bound to the tab's projection as the
/// shell's template binds it, in a shown window so keyboard focus really
/// moves; the workspace runs on a pumped dispatcher with production
/// scheduling. No window slot is hosted: the slot is exercised through the two
/// delegates it holds, <c>ReferenceEquals(surface.HeldFocusLanding, token)</c>
/// (its isLive) and <c>surface.CancelFocusLanding(token)</c> (its withdraw).
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

    /// <summary>WB-9 (#1312, R-9/R-10): the landing held while the shown
    /// projection refreshes survives the rescan's in-place reload of the tab —
    /// the old projection's teardown and the rebind to the new one — under the
    /// token the window's slot captured, and the reloaded projection's settle
    /// seats it and speaks its line once. The reload itself says nothing.</summary>
    [Fact]
    public void ARescanReloadOfTheReadingTabCarriesItsHeldLandingToTheReloadedProjection() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var reloadParked = new ManualResetEventSlim(false);
        using var reloadRelease = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                model.FetchFaultForTests = ParkedFetch(reloadParked, reloadRelease);
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(PumpedDispatcher.PumpUntil(() => reloadParked.IsSet), "the reloaded projection's fetch parking");
            Assert.NotNull(reloaded);
            Assert.Same(reloaded, hosted.Surface.Model);
            // The slot's isLive: the landing it holds is still the one held.
            Assert.Same(held.Token, hosted.Surface.HeldFocusLanding);
            Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);

            reloadRelease.Set();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => documents.IsCompleted && !hosted.Surface.IsFocusLandingPending),
                "the reloaded projection settling the carried landing");
            Assert.Equal(0UL, documents.Result.Failed);
            Assert.Contains("Changed outside Slate.", hosted.Text, StringComparison.Ordinal);
            Assert.Same(hosted.Surface, Keyboard.FocusedElement);

            RunOutTheTornDownFetch(held.Before, release);
            Assert.Same(hosted.Surface, Keyboard.FocusedElement);
            Assert.Equal(1, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
            reloadRelease.Set();
        }
    }));

    /// <summary>WB-20 (#1317, WD11): a landing a rescan reload carries seats
    /// on the reloaded PARTIAL projection — its text applied, its diagram
    /// still pending — and the diagram stage that lands later neither
    /// re-seats, re-speaks nor refuses it, nor fails the rescan; a failing
    /// stage leaves the fence in the unmatched shape.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACarriedLandingSeatsOnTheReloadedPartialProjectionAndTheDiagramStageLeavesIt(bool stageTwoFails) =>
        RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-diagram-" + (stageTwoFails ? "fails" : "lands"));
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var gate = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            object token = held.Token;
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            int landed = 0;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                model.DiagramStageGateForTests = gate;
                model.DiagramsCompleted += (_, _) => landed++;
                if (stageTwoFails)
                {
                    model.DiagramFaultForTests = () => new uniffi.slate_uniffi.VaultException.Io("injected diagram fault");
                }
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(
                w,
                cancellation.Token,
                "# Host\n\n![[embedded]]\n\nChanged outside Slate.\n\n```mermaid\ngraph TD; A-->B\n```\n");
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => reloaded is not null && documents.IsCompleted && !hosted.Surface.IsFocusLandingPending),
                "the reloaded partial projection settling the carried landing");
            Assert.Equal(1, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Same(hosted.Surface, Keyboard.FocusedElement);
            Assert.Contains("Changed outside Slate.", hosted.Text, StringComparison.Ordinal);
            Assert.Contains("graph TD", hosted.Text, StringComparison.Ordinal);
            Assert.False(hosted.Surface.CancelFocusLanding(token), "the carried landing is seated: nothing is held");
            Assert.True(reloaded!.DiagramsPendingForTests, "premise: the reloaded projection's diagram stage is held");

            gate.Set();
            RunOutTheTornDownFetch(held.Before, release);
            Assert.True(PumpedDispatcher.PumpUntil(() => landed == 1), "the reloaded projection's diagram stage landing");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();

            Assert.Equal(1, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Empty(w.Announced);
            Assert.Same(hosted.Surface, Keyboard.FocusedElement);
            Assert.Equal(0UL, documents.Result.Failed);
            Assert.False(reloaded.LastRefreshFailed);
            Assert.False(reloaded.DiagramStageGateTimedOutForTests);
            if (stageTwoFails)
            {
                Assert.DoesNotContain(
                    hosted.Surface.Document.Blocks.OfType<Paragraph>().SelectMany(paragraph => paragraph.Inlines)
                        .OfType<InlineUIContainer>().Select(container => container.Child),
                    child => child is ReadingDiagramElement);
                Assert.Contains("graph TD", hosted.Text, StringComparison.Ordinal);
            }
        }
        finally
        {
            release.Set();
            gate.Set();
        }
    }));

    /// <summary>WB-9 (#1312): once the reload has ended, the slot's withdraw
    /// — its original token — still names the carried landing and lets go of
    /// it: the reloaded projection's settle seats nobody.</summary>
    [Fact]
    public void ACarriedLandingIsCancelledByItsOriginalTokenAfterTheReload() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-cancelled-after");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var reloadParked = new ManualResetEventSlim(false);
        using var reloadRelease = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                model.FetchFaultForTests = ParkedFetch(reloadParked, reloadRelease);
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(PumpedDispatcher.PumpUntil(() => reloadParked.IsSet), "the reloaded projection's fetch parking");
            Assert.True(
                hosted.Surface.CancelFocusLanding(held.Token),
                "the slot's original token no longer names the carried landing");
            Assert.Null(hosted.Surface.HeldFocusLanding);

            reloadRelease.Set();
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => documents.IsCompleted
                        && reloaded is { RefreshInFlight: false }
                        && hosted.Text.Contains("Changed outside Slate.", StringComparison.Ordinal)),
                "the reloaded projection's publish");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.Equal(0UL, documents.Result.Failed);

            RunOutTheTornDownFetch(held.Before, release);
            Assert.False(hosted.Surface.IsFocusLandingPending);
            Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
            reloadRelease.Set();
        }
    }));

    /// <summary>WB-9 (#1312): inside the reload — after the old projection's
    /// teardown, before the rebind — the slot's withdraw still names the
    /// carried landing and lets go of it: nothing resumes when the reload
    /// ends.</summary>
    [Fact]
    public void ACarriedLandingIsCancelledByItsOriginalTokenDuringTheReload() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-cancelled-during");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            bool? cancelledDuringTheReload = null;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                cancelledDuringTheReload = hosted.Surface.CancelFocusLanding(held.Token);
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => documents.IsCompleted
                        && reloaded is { RefreshInFlight: false }
                        && hosted.Text.Contains("Changed outside Slate.", StringComparison.Ordinal)),
                "the reloaded projection's publish");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.True(cancelledDuringTheReload, "the slot's original token did not name the carried landing during the reload");
            Assert.Equal(0UL, documents.Result.Failed);

            RunOutTheTornDownFetch(held.Before, release);
            Assert.False(hosted.Surface.IsFocusLandingPending);
            Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
        }
    }));

    /// <summary>WB-9 (#1312, R-10): a surface hidden inside the reload lets go
    /// of the carried landing, as it lets go of a held one: the slot's live
    /// check no longer names it, and nothing seats, speaks or falls through
    /// when the reload ends.</summary>
    [Fact]
    public void ASurfaceHiddenDuringTheReloadLetsGoOfTheCarriedLanding() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-hidden");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            object? heldOnceHidden = held.Token;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                hosted.Surface.Visibility = Visibility.Collapsed;
                heldOnceHidden = hosted.Surface.HeldFocusLanding;
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => documents.IsCompleted
                        && reloaded is { RefreshInFlight: false }
                        && hosted.Text.Contains("Changed outside Slate.", StringComparison.Ordinal)),
                "the reloaded projection's publish");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.False(hosted.Surface.IsVisible);
            Assert.Null(heldOnceHidden);
            Assert.Equal(0UL, documents.Result.Failed);

            RunOutTheTornDownFetch(held.Before, release);
            Assert.False(hosted.Surface.IsFocusLandingPending);
            Assert.Null(hosted.Surface.HeldFocusLanding);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
        }
    }));

    /// <summary>WB-9 (#1312, red team): a new landing asked for inside the
    /// reload supersedes the carried one, as it supersedes a held one: the
    /// reload's end resumes nothing, and the carried landing never seats,
    /// speaks or falls through.</summary>
    [Fact]
    public void ANewLandingInsideTheReloadSupersedesTheCarriedOne() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-superseded");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                _ = hosted.Surface.RequestFocusLanding();
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => documents.IsCompleted
                        && reloaded is { RefreshInFlight: false }
                        && hosted.Text.Contains("Changed outside Slate.", StringComparison.Ordinal)),
                "the reloaded projection's publish");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.Equal(0UL, documents.Result.Failed);
            Assert.False(ReferenceEquals(held.Token, hosted.Surface.HeldFocusLanding), "the superseded landing is still held");

            RunOutTheTornDownFetch(held.Before, release);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(0, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
        }
    }));

    /// <summary>WB-9 (#1312): the reloaded projection's fetch fails — the
    /// rescan counts it, and the carried landing is refused once, silently:
    /// the F6 ring moves on, Reading speaks nothing.</summary>
    [Fact]
    public void AFailedRescanReloadRefusesTheCarriedLandingSilently() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-fails");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            ReadingContentViewModel? reloaded = null;
            w.HostTab.ReadingCreatedForTests = model =>
            {
                reloaded = model;
                model.FetchFaultForTests = () => new InvalidOperationException("injected reading fetch failure");
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => documents.IsCompleted && w.HostTab.RescanReadingPublication.IsCompleted),
                "the failed reloaded projection settling");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.Equal(1UL, documents.Result.Failed);
            Assert.True(reloaded is { LastRefreshFailed: true }, "the reloaded projection's refresh did not fail");

            RunOutTheTornDownFetch(held.Before, release);
            Assert.False(hosted.Surface.IsFocusLandingPending);
            Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(1, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
        }
    }));

    /// <summary>WB-9 (#1312): the reload throws after the old projection's
    /// teardown and before the rebind, so the surface stays bound to the
    /// torn-down projection and no replacement ever settles the landing — the
    /// reload's end refuses it exactly once, and the rescan counts the
    /// faulted reload.</summary>
    [Fact]
    public void AReloadThatBindsNoReplacementModelRefusesTheCarriedLandingOnce() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-throws");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            w.HostTab.ReadingCreatedForTests = _ =>
            {
                w.HostTab.ReadingCreatedForTests = null;
                throw new InvalidOperationException("injected");
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(PumpedDispatcher.PumpUntil(() => documents.IsCompleted), "the faulted reload's re-sync");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.Equal(1UL, documents.Result.Failed);
            Assert.Same(held.Before, hosted.Surface.Model);
            Assert.Null(hosted.Surface.HeldFocusLanding);
            Assert.Equal(1, held.FellThrough());

            RunOutTheTornDownFetch(held.Before, release);
            Assert.Null(hosted.Surface.HeldFocusLanding);
            Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(1, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            w.HostTab.ReadingCreatedForTests = null;
            release.Set();
        }
    }));

    /// <summary>WB-9 (#1312): the surface lets go of the tab inside the reload
    /// (it unbinds, unsubscribing everything it subscribed on bind) — the
    /// reload's end still reaches the carried landing, through the
    /// transaction, and refuses it exactly once.</summary>
    [Fact]
    public void AReloadWhoseSurfaceLetsGoOfTheTabStillRefusesTheCarriedLandingOnce() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("landing-reload-unbound");
        using var hosted = new HostedReadingSurface(w.HostTab);
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            HeldReadingLanding held = hosted.HoldTheLandingOverARefresh(parked, release);
            w.Announced.Clear();
            object? modelOnceUnbound = held.Before;
            w.HostTab.ReadingCreatedForTests = _ =>
            {
                hosted.Surface.DataContext = null;
                modelOnceUnbound = hosted.Surface.Model;
            };

            Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = ReloadTheHostInPlace(w, cancellation.Token);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => documents.IsCompleted && w.HostTab.RescanReadingPublication.IsCompleted),
                "the reload's re-sync");
            PumpedDispatcher.Drain();
            PumpedDispatcher.Drain();
            Assert.Null(modelOnceUnbound);
            Assert.Null(hosted.Surface.Model);
            Assert.Null(hosted.Surface.HeldFocusLanding);
            Assert.Equal(1, held.FellThrough());

            RunOutTheTornDownFetch(held.Before, release);
            Assert.Null(hosted.Surface.HeldFocusLanding);
            Assert.Same(hosted.Sentinel, Keyboard.FocusedElement);
            Assert.Equal(0, held.Spoken());
            Assert.Equal(1, held.FellThrough());
            Assert.Empty(w.Announced);
        }
        finally
        {
            release.Set();
        }
    }));

    /// <summary>A landing held over a refresh of the shown projection, the
    /// counters of the line it would speak and the fall-through it would
    /// take, and its token as the window's slot captures it.</summary>
    private sealed record HeldReadingLanding(
        ReadingContentViewModel Before, Func<int> Spoken, Func<int> FellThrough, object Token);

    /// <summary>A fetch-fault seam that parks the fetch on its worker until
    /// <paramref name="release"/>, then lets it run; a release that never
    /// comes fails the fetch.</summary>
    private static Func<Exception?> ParkedFetch(ManualResetEventSlim parked, ManualResetEventSlim release) => () =>
    {
        parked.Set();
        return release.Wait(TimeSpan.FromSeconds(30))
            ? null
            : new TimeoutException("the parked reading fetch was never released");
    };

    /// <summary>Changed outside Slate and rescanned: the rescan's documents
    /// re-sync reloads the clean host tab in place, replacing its projection
    /// model in one dispatcher turn.</summary>
    private static Task<WorkspaceViewModel.RescanDocumentsOutcome> ReloadTheHostInPlace(
        BackgroundReadingWorkspace w,
        CancellationToken cancellation,
        string text = "# Host\n\n![[embedded]]\n\nChanged outside Slate.\n")
    {
        File.WriteAllText(Path.Combine(w.Root, "host.md"), text);
        using (var scan = new uniffi.slate_uniffi.CancelToken())
        {
            _ = w.Session.Rescan(scan);
        }

        return w.ReSyncHostAsync(cancellation);
    }

    /// <summary>The replaced projection's parked fetch runs out: torn down,
    /// it publishes nothing and settles nothing.</summary>
    private static void RunOutTheTornDownFetch(ReadingContentViewModel before, ManualResetEventSlim release)
    {
        release.Set();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => before.WhenRefreshWorkDrained().IsCompleted),
            "the torn-down projection's fetch running out");
        PumpedDispatcher.Drain();
    }

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

        /// <summary>The landing asked for while a refresh of the shown
        /// projection is in flight (the reader's return to reading mode after
        /// an edit starts one), its fetch parked on
        /// <paramref name="release"/> — so the landing is held — and the
        /// token the window's slot captures right after asking.</summary>
        public HeldReadingLanding HoldTheLandingOverARefresh(
            ManualResetEventSlim parked, ManualResetEventSlim release)
        {
            ReadingContentViewModel before = WaitForItsFirstMerge();
            before.FetchFaultForTests = ParkedFetch(parked, release);
            before.Refresh();
            Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the shown projection's refresh parking");
            (Func<int> spoken, Func<int> fellThrough) = RequestTheLanding();
            Assert.True(Surface.IsFocusLandingPending);
            object? token = Surface.HeldFocusLanding;
            Assert.NotNull(token);
            return new HeldReadingLanding(before, spoken, fellThrough, token);
        }

        public void Dispose()
        {
            _window.Close();
            PumpedDispatcher.Drain();
        }
    }
}
