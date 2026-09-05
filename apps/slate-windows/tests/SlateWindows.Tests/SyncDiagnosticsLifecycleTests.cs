// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-8 (#740) facts about the sync-diagnostics LIFECYCLE seam — the
/// half that the panel suite deliberately cannot see because it builds
/// workspaces directly: SDR-5's once-per-vault-PATH announce gate
/// across vault close and reopen, and SD4/SDINV-5/SDINV-8's arm
/// discipline (off the dispatcher, generation-checked, and unable to
/// leave a handle behind when a teardown wins the race).
///
/// Everything here runs against a real <c>VaultLifecycleViewModel</c>
/// over a real session and real planted markers, because the whole
/// point of these facts is the wiring BETWEEN the objects. The
/// LiveSync plugin directory is the marker of choice: it is the only
/// deterministic, plantable High-risk provider, and High risk is what
/// arms the SD6 announcement.
///
/// Waiting discipline: the vault-open probe is now asynchronous by
/// design (SD4's arm-then-probe rides one background hop), so positive
/// waits poll with <c>await</c> rather than blocking the test thread —
/// a blocked thread cannot pump the synchronization context the panel
/// VM publishes through.
/// </summary>
public sealed class SyncDiagnosticsLifecycleTests : IDisposable
{
    /// <summary>Upper bound on any positive wait — never the expected
    /// duration.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

    private readonly List<string> _roots = [];
    private readonly object _announceGate = new();
    private readonly List<A11yEvent> _announced = [];

    public void Dispose()
    {
        foreach (string root in _roots)
        {
            DeleteTree(root);
        }
    }

    // --- SDR-5: the announce gate is keyed by vault path, not by
    // workspace instance ---

    /// <summary>
    /// The defect this fact exists for: the gate used to be an instance
    /// bool on <c>WorkspaceViewModel</c>, and a workspace dies at vault
    /// close. Closing a risky vault and reopening it from Recents
    /// therefore re-fired an assertive High-priority summary at a
    /// reader whose situation had not changed — the mac is silent, and
    /// says so in as many words at
    /// <c>AppState.syncAnnouncedVaultPath</c>.
    /// </summary>
    [Fact]
    public async Task ReopeningTheSameVaultDoesNotReAnnounceItsSyncFindings()
    {
        string root = NewVault("announce-reopen");
        PlantLiveSyncPlugin(root);
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);

        SyncDetectionReport report = await OpenAndProbeAsync(lifecycle, root);

        // The first open still announces — the gate must be a gate, not
        // a mute switch.
        A11yEvent.HostComposed announced = Assert.Single(SummaryAnnouncements());
        Assert.Equal(report.AudioSummary, announced.Text);
        Assert.Equal(A11yPriority.High, announced.Priority);
        Assert.Contains(report.Providers, provider => provider.RiskLevel == RiskLevel.High);

        await SettleSidebarAsync(lifecycle);
        lifecycle.CloseVault();
        Assert.Null(lifecycle.Workspace);

        _ = await OpenAndProbeAsync(lifecycle, root);

        // SDR-5: an in-session set keyed by vault path. The risk story
        // did not change just because the vault was closed and
        // reopened, so the reader is not interrupted a second time.
        Assert.Single(SummaryAnnouncements());
    }

    /// <summary>The other half of SDR-5, and the reason the gate is a
    /// SET rather than a latch: a different vault is a different risk
    /// story and re-arms, while the vault already announced stays
    /// silent even after switching away and back.</summary>
    [Fact]
    public async Task ADifferentVaultPathReArmsTheSyncAnnounceGate()
    {
        string first = NewVault("announce-first");
        string second = NewVault("announce-second");
        PlantLiveSyncPlugin(first);
        PlantLiveSyncPlugin(second);
        using VaultLifecycleViewModel lifecycle = NewLifecycle(first);

        _ = await OpenAndProbeAsync(lifecycle, first);
        Assert.Single(SummaryAnnouncements());

        await SettleSidebarAsync(lifecycle);
        _ = await OpenAndProbeAsync(lifecycle, second);
        Assert.Equal(2, SummaryAnnouncements().Count);

        await SettleSidebarAsync(lifecycle);
        _ = await OpenAndProbeAsync(lifecycle, first);
        // Back to a vault that already spoke: still silent. This is
        // where the SET parts company with mac's single-slot latch,
        // which would have re-armed on the switch away — divergence
        // SDD-6, recorded deliberately as the quieter reading.
        Assert.Equal(2, SummaryAnnouncements().Count);
    }

    /// <summary>
    /// The gate's KEY, not its scope. The comment on
    /// <c>_announcedSyncVaultPaths</c> promises that "C:\Vault" and
    /// "c:\vault\" are one vault, which is exactly the case a real
    /// user hits: a CLI/ArgumentList spelling versus the picker's
    /// canonical casing, or a trailing separator from a shell
    /// completion. Without BOTH the case-insensitive comparer and the
    /// trailing-separator trim the round-1 HIGH defect comes straight
    /// back — a second High-priority interruption on what is really
    /// the same reopen. Round 2 caught that neither half was gated:
    /// swapping to Ordinal and dropping the trim left all 80 sync
    /// facts green.
    /// </summary>
    [Fact]
    public async Task TheAnnounceGateTreatsCasingAndATrailingSeparatorAsOneVault()
    {
        string root = NewVault("announce-key");
        PlantLiveSyncPlugin(root);
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);

        _ = await OpenAndProbeAsync(lifecycle, root);
        Assert.Single(SummaryAnnouncements());

        // The same directory, spelled two ways a caller legitimately
        // produces. Neither is a new vault, so neither may re-announce.
        await SettleSidebarAsync(lifecycle);
        lifecycle.CloseVault();
        _ = await OpenAndProbeAsync(lifecycle, root.ToUpperInvariant());
        Assert.Single(SummaryAnnouncements());

        await SettleSidebarAsync(lifecycle);
        lifecycle.CloseVault();
        _ = await OpenAndProbeAsync(
            lifecycle, root + Path.DirectorySeparatorChar);
        Assert.Single(SummaryAnnouncements());
    }

    /// <summary>
    /// The gate's ORDER. A Low/Medium-only report must not consume the
    /// vault's one admission: the code checks risk FIRST and admits
    /// second, and round 2 found that inverting those two — the exact
    /// ordering the comment says must not happen — left every sync
    /// fact green. The scenario is reachable and silences a real
    /// finding: open a vault whose only marker is Low risk, then a
    /// sync client starts mid-session and the watcher republishes a
    /// High-risk report that must still be announced.
    /// </summary>
    [Fact]
    public async Task ALowRiskFirstProbeDoesNotConsumeTheVaultsAnnouncement()
    {
        string root = NewVault("announce-order");
        // Git alone is Low risk: no warning, no High provider, so the
        // SD6 gate refuses — WITHOUT spending the admission.
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);

        SyncDetectionReport low = await OpenAndProbeAsync(lifecycle, root);
        Assert.Contains(low.Providers, provider => provider.Kind == SyncProviderKind.Git);
        Assert.DoesNotContain(
            low.Providers, provider => provider.RiskLevel == RiskLevel.High);
        Assert.Empty(SummaryAnnouncements());

        // The risk story changes mid-session — the case the watcher
        // exists for. The admission must still be available.
        PlantLiveSyncPlugin(root);
        WorkspaceViewModel workspace =
            Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        workspace.RefreshSyncDiagnostics();
        Assert.True(
            await SpinAsync(
                () => SummaryAnnouncements().Count == 1, Bound),
            "a High-risk report that arrived after a Low-risk probe was "
            + "never announced — the Low probe consumed the admission");
    }

    // --- SD4 / SDINV-5 / SDINV-8: the arm ---

    /// <summary>
    /// SD4/SDR-2: arming is synchronous filesystem I/O — three
    /// <c>Directory.Exists</c> probes and up to three
    /// <c>CreateFileW</c> calls against the vault root — and on an
    /// unresponsive share or a files-on-demand root each can block for
    /// seconds. Run on the dispatcher it freezes the window at every
    /// vault open, so the arm and the probe chained after it ride ONE
    /// background hop. Both halves are pinned here: the open returns
    /// with the arm still parked, and releasing it produces the arm
    /// FIRST and the probe second.
    /// </summary>
    [Fact]
    public async Task TheMarkerArmAndTheOpenProbeRunOffTheOpeningThread()
    {
        string root = NewVault("arm-hop");
        using var armEntered = new ManualResetEventSlim();
        using var releaseArm = new ManualResetEventSlim();
        using VaultLifecycleViewModel lifecycle = NewLifecycle(
            root,
            syncArmWorker: work => Task.Run(() =>
            {
                armEntered.Set();
                _ = releaseArm.Wait(Bound);
                work();
            }));

        await lifecycle.OpenVaultAsync(root);

        Assert.True(
            armEntered.Wait(Bound),
            "the arm must be handed to a worker, not run on the opening thread");
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        // The open completed with the arm still parked: nothing about
        // the vault-open path waits on filesystem handles.
        Assert.Equal(0, lifecycle.SyncMarkerWatchForTests?.ArmedWatchCountForTests);
        Assert.Null(workspace.SyncDiagnostics.Report);
        Assert.False(workspace.SyncDiagnostics.IsLoading);

        releaseArm.Set();

        // SD4's ordering survives the move: the handles are open BEFORE
        // the probe runs, so a marker landing between them still emits
        // an event instead of hiding until the next manual refresh.
        Assert.True(
            await SpinAsync(
                () => lifecycle.SyncMarkerWatchForTests?.ArmedWatchCountForTests > 0,
                Bound),
            "the released hop must arm the watch");
        Assert.True(
            await SpinAsync(() => HasProbed(workspace), Bound),
            "the released hop must then run the vault-open probe");
    }

    /// <summary>
    /// SDINV-5/SDINV-8, the concurrency defect: a teardown that lands
    /// while the open continuation is still building the workspace
    /// reads a still-null watcher field and tears everything down, and
    /// the opening thread then arms three <see cref="FileSystemWatcher"/>
    /// instances with no owner left to stop them. They would live for
    /// the rest of the PROCESS and hold the vault directory
    /// undeletable. <c>WorkspaceReady</c> is the last statement of
    /// <c>InitializeWorkspace</c>, so disposing from a handler on it
    /// reproduces exactly that interleave with no timing bet at all.
    /// </summary>
    [Fact]
    public async Task ATeardownDuringVaultOpenLeavesNoArmedMarkerWatch()
    {
        string root = NewVault("arm-race");
        // A whole chain to arm, so a lost race would leak three
        // handles rather than one.
        Directory.CreateDirectory(Path.Combine(root, ".obsidian", "plugins"));
        using var armCompleted = new ManualResetEventSlim();
        VaultLifecycleViewModel? lifecycle = null;
        lifecycle = NewLifecycle(
            root,
            syncArmWorker: work => Task.Run(() =>
            {
                work();
                armCompleted.Set();
            }));
        lifecycle.WorkspaceReady += (_, _) => lifecycle!.Dispose();

        await lifecycle.OpenVaultAsync(root);

        // The teardown really did land mid-open.
        Assert.Null(lifecycle.Workspace);
        // Either the entry re-check refused to build a watcher at all,
        // or the hop found the stale generation and disposed the one it
        // was handed. Both are "no handles"; neither is "three".
        Assert.True(
            await SpinAsync(
                () => armCompleted.IsSet || lifecycle.SyncMarkerWatchForTests is null,
                Bound),
            "the arm hop must settle one way or the other");
        Assert.Equal(0, lifecycle.SyncMarkerWatchForTests?.ArmedWatchCountForTests ?? 0);
    }

    /// <summary>
    /// SD8's callback contract, enforced rather than documented.
    /// <c>SyncMarkerWatcher</c> invokes the owner's callback INSIDE its
    /// own lock — that is what makes "never fires after Stop returns"
    /// structural — so a callback that blocks blocks <c>Stop</c>, and
    /// <c>Stop</c> is the first thing vault close does. The lifecycle
    /// is handed an arbitrary enqueue delegate, and a blocking one (a
    /// dispatcher <c>Invoke</c>, or a test's inline
    /// <c>action =&gt; action()</c> in front of slow work) must not be
    /// able to turn a marker fire into a frozen vault close.
    /// </summary>
    [Fact]
    public async Task ABlockingUiHopNeverStallsTheWatcherTeardown()
    {
        string root = NewVault("blocking-hop");
        int blockUi = 0;
        using var uiEntered = new ManualResetEventSlim();
        using var releaseUi = new ManualResetEventSlim();
        VaultLifecycleViewModel lifecycle = NewLifecycle(
            root,
            enqueueUi: action =>
            {
                if (Volatile.Read(ref blockUi) == 1)
                {
                    uiEntered.Set();
                    _ = releaseUi.Wait(TimeSpan.FromSeconds(30));
                }

                action();
            },
            // Short enough that the fire lands inside the fact rather
            // than production's 2.5 s.
            syncMarkerDebounce: TimeSpan.FromMilliseconds(150));
        try
        {
            _ = await OpenAndProbeAsync(lifecycle, root);
            await SettleSidebarAsync(lifecycle);

            // Only now does the UI hop become hostile, so the open
            // itself is unaffected.
            Volatile.Write(ref blockUi, 1);
            File.WriteAllText(Path.Combine(root, ".stignore"), string.Empty);
            Assert.True(
                uiEntered.Wait(Bound),
                "a root marker must reach the lifecycle's UI hop");
            // Comfortably past the injected debounce, so the marker
            // fire itself is demonstrably in flight — not just some
            // other hop that happened to arrive first.
            Thread.Sleep(TimeSpan.FromSeconds(1));

            var teardown = Task.Run(lifecycle.Dispose);

            Assert.True(
                teardown.Wait(TimeSpan.FromSeconds(5)),
                "vault teardown must not wait on a blocked UI hop");
        }
        finally
        {
            releaseUi.Set();
            lifecycle.Dispose();
        }
    }

    // --- Helpers ---

    private VaultLifecycleViewModel NewLifecycle(
        string root,
        Action<Action>? enqueueUi = null,
        Func<Action, Task>? syncArmWorker = null,
        TimeSpan? syncMarkerDebounce = null) =>
        new(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: enqueueUi ?? (action => action()),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(root, "device-state", "recent-vaults.json")),
            announce: Record,
            sessionLoadWorker: work => Task.FromResult(work()),
            syncArmWorker: syncArmWorker,
            syncMarkerDebounce: syncMarkerDebounce);

    private void Record(A11yEvent announcement)
    {
        // Publishes land on whichever thread the panel scheduler's
        // context runs them on, so the list needs a gate.
        lock (_announceGate)
        {
            _announced.Add(announcement);
        }
    }

    /// <summary>Every HIGH-priority host-composed announcement. On a
    /// healthy open/close path that is the SD6 family and nothing
    /// else: the scan gate speaks at Medium, and the lifecycle's own
    /// High copy is terminal-error copy — which these facts SHOULD
    /// fail on rather than filter away.</summary>
    private List<A11yEvent.HostComposed> SummaryAnnouncements()
    {
        lock (_announceGate)
        {
            return [.. _announced
                .OfType<A11yEvent.HostComposed>()
                .Where(item => item.Priority == A11yPriority.High)];
        }
    }

    private static bool HasProbed(WorkspaceViewModel workspace) =>
        workspace.SyncDiagnostics.Report is not null
            || workspace.SyncDiagnostics.LoadError is not null;

    private static async Task<SyncDetectionReport> OpenAndProbeAsync(
        VaultLifecycleViewModel lifecycle, string root)
    {
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        Assert.True(
            await SpinAsync(() => HasProbed(workspace), Bound),
            "the vault-open probe must publish a report or an error");
        Assert.Null(workspace.SyncDiagnostics.LoadError);
        return workspace.SyncDiagnostics.Report!;
    }

    /// <summary>A close or a switch is REFUSED while the file tree is
    /// still reading (the W1 close barrier), which has nothing to do
    /// with sync. Settling it first keeps these facts about the thing
    /// they are about.</summary>
    private static async Task SettleSidebarAsync(VaultLifecycleViewModel lifecycle)
    {
        if (lifecycle.FileSidebar is FilesSidebarViewModel sidebar)
        {
            await sidebar.TreeRefreshCompletion.WaitAsync(Bound);
        }
    }

    /// <summary>Poll with <c>await</c>, never
    /// <see cref="SpinWait.SpinUntil(Func{bool}, TimeSpan)"/>: the work
    /// being waited for publishes through a synchronization context
    /// that a blocked test thread can starve.</summary>
    private static async Task<bool> SpinAsync(Func<bool> condition, TimeSpan timeout)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return condition();
    }

    private string NewVault(string label)
    {
        string root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-sync-lifecycle-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "note0.md"), "# Note 0\n\nBody.\n");
        _roots.Add(root);
        return root;
    }

    /// <summary>The one deterministic, plantable HIGH-risk provider —
    /// and High risk is what arms the SD6 gate. Core's arm needs the
    /// plugin directory AND a manifest.json inside it. Nothing here
    /// plants a location-based marker, so a developer machine's own
    /// cloud folders cannot leak into a fact.</summary>
    private static void PlantLiveSyncPlugin(string root)
    {
        string plugin = Path.Combine(root, ".obsidian", "plugins", "obsidian-livesync");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(
            Path.Combine(plugin, "manifest.json"),
            "{\"id\":\"obsidian-livesync\",\"version\":\"0.24.0\"}\n");
    }

    private static void DeleteTree(string root)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
