// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-8 (#740) facts: the sync-diagnostics document and coordinator
/// over a REAL session with REAL planted markers — contracts SD1 (core
/// copy relayed verbatim), SD2 (the five-state precedence), SD5
/// (one guarded hop for both FFI calls), SD6 (the once-per-vault
/// announce gate), SD7 (a refresh that never reveals), and SDINV-7
/// (the closed trigger set).
///
/// Marker choice: a <c>.git</c> DIRECTORY (Git, Low risk) and a
/// <c>.stfolder</c> directory (Syncthing, Medium) are deterministic,
/// cross-platform, provider-real markers; the LiveSync plugin
/// directory (High) drives the config section. Nothing here plants a
/// location-based marker (iCloud/Dropbox/OneDrive/Drive), so a
/// developer machine's own cloud folders cannot leak into a fact.
/// </summary>
public sealed class SyncDiagnosticsPanelTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public SyncDiagnosticsPanelTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note0.md"), "# Note 0\n\nBody.\n");
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // --- Marker planting (after the scan: detection reads the live
    // filesystem, never the index) ---

    private void PlantGit() =>
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

    private void PlantSyncthing() =>
        Directory.CreateDirectory(Path.Combine(_root, ".stfolder"));

    private string PlantLiveSyncPlugin()
    {
        string plugin = Path.Combine(
            _root, ".obsidian", "plugins", "obsidian-livesync");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(
            Path.Combine(plugin, "manifest.json"),
            "{\"id\":\"obsidian-livesync\",\"version\":\"0.24.0\"}\n");
        return plugin;
    }

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);

    private SyncDiagnosticsViewModel NewPanel() =>
        new(_session, synchronousForTests: true);

    private int HostComposedCount() =>
        _announced.OfType<A11yEvent.HostComposed>().Count();

    private static SyncDetectionReport Report(
        bool supported, string? warning, params DetectedSyncProvider[] providers) =>
        new(providers, warning, "summary", supported);

    private static DetectedSyncProvider Provider(RiskLevel risk) =>
        new(SyncProviderKind.Git, "Git", [".git"], risk, "recommendation");

    // --- SD2: the pure precedence selector ---

    [Fact]
    public void TheStateSelectorFollowsTheContractPrecedence()
    {
        // Unsupported wins over EVERYTHING — the report exists and
        // says detection isn't available for this vault type.
        Assert.Equal(
            SyncDiagnosticsState.Unsupported,
            SyncDiagnosticsViewModel.SelectState(
                Report(supported: false, "warning", Provider(RiskLevel.High)),
                "boom"));
        // Error beats loading (no report yet) …
        Assert.Equal(
            SyncDiagnosticsState.Error,
            SyncDiagnosticsViewModel.SelectState(null, "boom"));
        // … and beats a RETAINED report: a refresh failure shows the
        // error + Retry even with a stale report underneath (SD5).
        Assert.Equal(
            SyncDiagnosticsState.Error,
            SyncDiagnosticsViewModel.SelectState(
                Report(supported: true, null, Provider(RiskLevel.Low)), "boom"));
        // Loading is "no report yet", not a busy flag (mac parity).
        Assert.Equal(
            SyncDiagnosticsState.Loading,
            SyncDiagnosticsViewModel.SelectState(null, null));
        Assert.Equal(
            SyncDiagnosticsState.Empty,
            SyncDiagnosticsViewModel.SelectState(
                Report(supported: true, null), null));
        Assert.Equal(
            SyncDiagnosticsState.Populated,
            SyncDiagnosticsViewModel.SelectState(
                Report(supported: true, null, Provider(RiskLevel.Low)), null));
    }

    [Fact]
    public void AFreshDocumentIsLoadingUntilTheFirstProbeLands()
    {
        SyncDiagnosticsViewModel panel = NewPanel();

        Assert.Equal(SyncDiagnosticsState.Loading, panel.State);
        Assert.Null(panel.Report);
        Assert.False(panel.IsLoading);

        panel.Reload();

        Assert.Equal(SyncDiagnosticsState.Empty, panel.State);
        Assert.False(panel.IsLoading);
        panel.Shutdown();
    }

    // --- SD1/SD2: core's sentences, relayed ---

    [Fact]
    public void AnEmptyVaultRendersCoresOwnNoSyncSystemsSentence()
    {
        SyncDiagnosticsViewModel panel = NewPanel();

        panel.Reload();

        Assert.Equal(SyncDiagnosticsState.Empty, panel.State);
        Assert.Empty(panel.Report!.Providers);
        Assert.Null(panel.Report.MultiSyncWarning);
        // SDD-2: Windows renders the report's own pre-rendered
        // sentence instead of duplicating it in host code.
        Assert.Equal("No sync systems detected.", panel.Report.AudioSummary);
        panel.Shutdown();
    }

    [Fact]
    public void APlantedGitMarkerPublishesCoresRowDataUntouched()
    {
        PlantGit();
        SyncDiagnosticsViewModel panel = NewPanel();

        panel.Reload();

        Assert.Equal(SyncDiagnosticsState.Populated, panel.State);
        DetectedSyncProvider git = Assert.Single(panel.Report!.Providers);
        Assert.Equal(SyncProviderKind.Git, git.Kind);
        Assert.Equal("Git", git.DisplayName);
        Assert.Equal(RiskLevel.Low, git.RiskLevel);
        Assert.Contains(".git", git.EvidencePaths);
        Assert.NotEmpty(git.Recommendation);
        // Git alone is Low risk: no multi-sync warning (SD6's gate
        // reads this, so the fact pins it).
        Assert.Null(panel.Report.MultiSyncWarning);
        panel.Shutdown();
    }

    [Fact]
    public void BothFfiCallsRideTheSameLoad()
    {
        _ = PlantLiveSyncPlugin();
        SyncDiagnosticsViewModel panel = NewPanel();

        panel.Reload();

        // SDR-6: DetectSync and LivesyncConfig publish together, so a
        // populated LiveSync report always has its config status.
        Assert.Equal(SyncDiagnosticsState.Populated, panel.State);
        Assert.Contains(
            panel.Report!.Providers,
            provider => provider.Kind == SyncProviderKind.LiveSync);
        // manifest.json without data.json — the plugin is installed
        // but never configured (SD3(4)'s third arm).
        _ = Assert.IsType<LiveSyncConfigStatus.NotPresent>(panel.LiveSyncConfig);
        panel.Shutdown();
    }

    [Fact]
    public void ALiveSyncDataFileParsesIntoTheCredentialFreeSubset()
    {
        string plugin = PlantLiveSyncPlugin();
        File.WriteAllText(
            Path.Combine(plugin, "data.json"),
            "{\"couchDB_URI\":\"https://sync.example.com:5984/\","
            + "\"couchDB_DBNAME\":\"vaultdb\","
            + "\"couchDB_USER\":\"alice\",\"couchDB_PASSWORD\":\"hunter2\","
            + "\"liveSync\":true,\"syncOnSave\":false,\"encrypt\":true}\n");
        SyncDiagnosticsViewModel panel = NewPanel();

        panel.Reload();

        var parsed = Assert.IsType<LiveSyncConfigStatus.Parsed>(panel.LiveSyncConfig);
        Assert.Equal("sync.example.com:5984", parsed.Config.ServerHost);
        Assert.Equal("vaultdb", parsed.Config.Database);
        Assert.True(parsed.Config.LiveSyncEnabled);
        Assert.False(parsed.Config.SyncOnSave);
        // Absent in the file — schema drift renders "Unknown", never
        // a guessed default (SD10).
        Assert.Null(parsed.Config.SyncOnStart);
        Assert.True(parsed.Config.EndToEndEncryption);
        panel.Shutdown();
    }

    // --- SD6: the announcement gate ---

    [Fact]
    public void ALowRiskOnlyReportAnnouncesNothing()
    {
        PlantGit();
        WorkspaceViewModel workspace = NewWorkspace();
        int before = HostComposedCount();

        workspace.RefreshSyncDiagnostics();

        // Precondition: neither gate arm is armed by a Git-only vault.
        Assert.DoesNotContain(
            workspace.SyncDiagnostics.Report!.Providers,
            provider => provider.RiskLevel == RiskLevel.High);
        Assert.Null(workspace.SyncDiagnostics.Report.MultiSyncWarning);
        Assert.Equal(before, HostComposedCount());
        workspace.Dispose();
    }

    [Fact]
    public void AnEmptyReportAnnouncesNothing()
    {
        WorkspaceViewModel workspace = NewWorkspace();
        int before = HostComposedCount();

        workspace.RefreshSyncDiagnostics();

        Assert.Equal(SyncDiagnosticsState.Empty, workspace.SyncDiagnostics.State);
        Assert.Equal(before, HostComposedCount());
        workspace.Dispose();
    }

    [Fact]
    public void AHighRiskProviderAnnouncesCoresSummaryOncePerVault()
    {
        _ = PlantLiveSyncPlugin();
        WorkspaceViewModel workspace = NewWorkspace();
        int before = HostComposedCount();

        workspace.RefreshSyncDiagnostics();

        SyncDetectionReport report = workspace.SyncDiagnostics.Report!;
        // Gate arm (a) second half: a single High-risk provider with
        // no multi-sync warning still announces.
        Assert.Null(report.MultiSyncWarning);
        Assert.Contains(report.Providers, p => p.RiskLevel == RiskLevel.High);
        A11yEvent.HostComposed announced = Assert.Single(
            _announced.OfType<A11yEvent.HostComposed>().Skip(before));
        Assert.Equal(report.AudioSummary, announced.Text);
        Assert.Equal(A11yPriority.High, announced.Priority);

        // A watcher republish or a manual refresh is SILENT once the
        // vault has announced (SDR-5's in-session gate).
        workspace.RefreshSyncDiagnostics();
        Assert.Single(_announced.OfType<A11yEvent.HostComposed>().Skip(before));
        workspace.Dispose();
    }

    [Fact]
    public void AMultiSyncVaultAnnouncesCoresSummaryVerbatim()
    {
        PlantGit();
        PlantSyncthing();
        _ = PlantLiveSyncPlugin();
        WorkspaceViewModel workspace = NewWorkspace();
        int before = HostComposedCount();

        workspace.RefreshSyncDiagnostics();

        SyncDetectionReport report = workspace.SyncDiagnostics.Report!;
        // Two providers at risk >= Medium (Syncthing + LiveSync) —
        // core composes the warning; Git (Low) never triggers it.
        Assert.NotNull(report.MultiSyncWarning);
        A11yEvent.HostComposed announced = Assert.Single(
            _announced.OfType<A11yEvent.HostComposed>().Skip(before));
        // SD1/SD6: the announcement is core's pre-rendered summary,
        // never host prose.
        Assert.Equal(report.AudioSummary, announced.Text);
        Assert.Equal(A11yPriority.High, announced.Priority);
        workspace.Dispose();
    }

    // --- SD7 / SDINV-7: refresh refreshes; reveal does nothing ---

    [Fact]
    public void TheRefreshCommandRerunsDetectionWithoutRevealingTheLeaf()
    {
        PlantGit();
        WorkspaceViewModel workspace = NewWorkspace();
        WorkspaceLeafOption before = workspace.ActiveLeaf;
        workspace.RefreshSyncDiagnostics();
        int publishes = 0;
        workspace.SyncDiagnostics.Published += (_, _) => publishes++;
        int announcedBefore = _announced.Count;

        Assert.True(workspace.RefreshSyncDiagnosticsCommand.CanExecute(null));
        workspace.RefreshSyncDiagnosticsCommand.Execute(null);

        Assert.Equal(1, publishes);
        // SD7: the command refreshes ONLY — the mac command has no
        // reveal, and the refresh itself is silent.
        Assert.Same(before, workspace.ActiveLeaf);
        Assert.Equal(announcedBefore, _announced.Count);
        workspace.Dispose();
    }

    [Fact]
    public void RevealingTheLeafNeitherReloadsNorAnnouncesAnythingSyncSpecific()
    {
        PlantGit();
        WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshSyncDiagnostics();
        SyncDetectionReport before = workspace.SyncDiagnostics.Report!;
        int publishes = 0;
        workspace.SyncDiagnostics.Published += (_, _) => publishes++;
        int hostComposedBefore = HostComposedCount();

        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(
            leaf => leaf.Id == "syncDiagnostics");

        // SDINV-7: the trigger set is closed — no reveal hook.
        Assert.Equal(0, publishes);
        Assert.Same(before, workspace.SyncDiagnostics.Report);
        // SDINV-4: the ONLY event a reveal produces is the setter's
        // generic leaf announcement.
        Assert.Equal(hostComposedBefore, HostComposedCount());
        var shown = Assert.IsType<A11yEvent.LeafPanelShown>(_announced[^1]);
        Assert.Equal("Sync", shown.Title);
        workspace.Dispose();
    }

    [Fact]
    public void NothingPublishesOrAnnouncesAfterTheWorkspaceIsDisposed()
    {
        _ = PlantLiveSyncPlugin();
        WorkspaceViewModel workspace = NewWorkspace();
        SyncDiagnosticsViewModel panel = workspace.SyncDiagnostics;
        workspace.RefreshSyncDiagnostics();
        SyncDetectionReport before = panel.Report!;
        int hostComposedBefore = HostComposedCount();
        int publishes = 0;
        panel.Published += (_, _) => publishes++;

        workspace.Dispose();
        // The vault changes AFTER teardown: a surviving probe would
        // publish a different report, which is what makes this fact
        // falsifiable.
        PlantSyncthing();
        panel.Reload();
        workspace.RefreshSyncDiagnostics();

        // SDINV-8: the shut-down scheduler refuses new work and the
        // disposed workspace refuses to start any.
        Assert.Equal(0, publishes);
        Assert.Same(before, panel.Report);
        Assert.Equal(hostComposedBefore, HostComposedCount());
    }

    // --- SD4: the vault-open trigger (arm-then-probe) ---

    /// <summary>
    /// SD4 trigger (a): opening a vault runs the initial probe, and it
    /// comes from the LIFECYCLE — after the marker watcher arms — not
    /// from the workspace constructor.
    ///
    /// <c>Reload()</c> raises <c>IsLoading</c> synchronously on the
    /// caller's thread and every outcome of a probe clears it into a
    /// report or an error, so "the vault-open probe happened" is
    /// deterministic the moment <c>OpenVaultAsync</c> returns — no
    /// timing bet. The bare-workspace control at the end is what makes
    /// it falsifiable: nothing else in the closed SDINV-7 trigger set
    /// could have started it.
    ///
    /// The ORDERING half of arm-then-probe is structural (two adjacent
    /// statements in <c>StartSyncMarkerWatch</c>, with
    /// <c>SyncMarkerWatcher.Start</c> returning only once the handles
    /// are open); observing it would need a test-only seam on the
    /// lifecycle, so the watcher's own suite pins the arm and the W-C
    /// journey covers the pair end to end.
    /// </summary>
    [Fact]
    public async Task OpeningAVaultRunsTheInitialSyncProbe()
    {
        string root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-sync-open-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "note0.md"), "# Note 0\n\nBody.\n");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        try
        {
            using var lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(root),
                enqueueUi: action => action(),
                recentVaultsStore: new RecentVaultsStore(
                    Path.Combine(root, "device-state", "recent-vaults.json")),
                announce: _announced.Add,
                sessionLoadWorker: work => Task.FromResult(work()));

            await lifecycle.OpenVaultAsync(root);

            WorkspaceViewModel opened = Assert.IsType<WorkspaceViewModel>(
                lifecycle.Workspace);
            Assert.True(
                SpinWait.SpinUntil(
                    () => opened.SyncDiagnostics.IsLoading
                        || opened.SyncDiagnostics.Report is not null
                        || opened.SyncDiagnostics.LoadError is not null,
                    TimeSpan.FromSeconds(15)),
                "vault open must run the initial detection probe");

            // The control: a workspace built WITHOUT the lifecycle never
            // probes on its own, so the assertion above can only have
            // been satisfied by the vault-open trigger.
            WorkspaceViewModel bare = NewWorkspace();
            Assert.False(bare.SyncDiagnostics.IsLoading);
            Assert.Null(bare.SyncDiagnostics.Report);
            Assert.Null(bare.SyncDiagnostics.LoadError);
            bare.Dispose();
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

/// <summary>
/// W4-8 (#740): the leaf surface's render contract (SD3) over a real
/// report — the row/landmark peers, the composed names, the
/// SEPARATE evidence disclosure, and the LiveSync section. WPF
/// controls need an STA thread (the BaseSurfaceViewTests shape).
///
/// The error and unsupported states are covered by the pure selector
/// in <see cref="SyncDiagnosticsPanelTests"/>: neither is reachable
/// from a real filesystem session (detection degrades rather than
/// throwing, SDINV-2, and every Windows session has a root), and the
/// leaf deliberately exposes no publish seam to fake one.
/// </summary>
public sealed class SyncDiagnosticsSurfaceViewTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;

    public SyncDiagnosticsSurfaceViewTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-syncview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note0.md"), "# Note 0\n");
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void TheLoadingStateNamesItselfBeforeAnyProbeLands() => RunSta(() =>
    {
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };

        TextBlock loading = Find<TextBlock>(surface, "SyncDiagnosticsLoading");
        Assert.Equal(SyncPhrase.Loading, loading.Text);
        Assert.True(loading.Focusable);
        panel.Shutdown();
    });

    [Fact]
    public void TheEmptyStateRendersTheReportsOwnSentence() => RunSta(() =>
    {
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };

        panel.Reload();

        TextBlock empty = Find<TextBlock>(surface, "SyncDiagnosticsEmpty");
        Assert.Equal(panel.Report!.AudioSummary, empty.Text);
        panel.Shutdown();
    });

    [Fact]
    public void APopulatedReportRendersTheContractOrderAndCoresCopy() => RunSta(() =>
    {
        // Three real markers: LiveSync (High) + Syncthing (Medium)
        // are the two risk >= Medium providers core's multi-sync
        // warning needs; Git (Low) rides along without triggering it.
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        Directory.CreateDirectory(Path.Combine(_root, ".stfolder"));
        string plugin = Path.Combine(
            _root, ".obsidian", "plugins", "obsidian-livesync");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, "manifest.json"), "{}\n");
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };

        panel.Reload();

        SyncDetectionReport report = panel.Report!;
        // (1) The count header is the SD10 golden over core's count.
        TextBlock header = Find<TextBlock>(surface, "SyncDiagnosticsHeader");
        Assert.Equal(SyncPhrase.CountHeader(report.Providers.Length), header.Text);
        Assert.Equal(
            AutomationHeadingLevel.Level3,
            AutomationProperties.GetHeadingLevel(header));
        // The refresh control satisfies WCAG 2.5.3: the visible label
        // is a contiguous prefix of the accessible name.
        Button refresh = Find<Button>(surface, "SyncDiagnosticsRefresh");
        Assert.Equal(SyncPhrase.Refresh, (string)refresh.Content);
        Assert.Equal(
            SyncPhrase.RefreshAccessibleName, AutomationProperties.GetName(refresh));
        Assert.StartsWith(
            (string)refresh.Content, AutomationProperties.GetName(refresh));
        // (2) The warning row is FIRST, before any provider row, and
        // carries core's sentence behind the host prefix.
        Assert.NotNull(report.MultiSyncWarning);
        var warning = Find<AutomationNamedRowBorder>(surface, "SyncDiagnosticsWarning");
        Assert.Equal(
            SyncPhrase.Warning(report.MultiSyncWarning!),
            AutomationProperties.GetName(warning));
        Assert.True(warning.Focusable);
        int firstProviderIndex = report.Providers
            .Select(provider =>
                IndexOfId(surface, "SyncDiagnosticsProvider" + provider.Kind))
            .Min();
        Assert.True(
            IndexOfId(surface, "SyncDiagnosticsWarning") < firstProviderIndex,
            "SD3: the multi-sync warning renders before the provider rows.");
        // (3) One combined element per provider, named from core's own
        // fields (SD1 — the host adds only the risk word and the
        // punctuation), with a SEPARATE evidence disclosure.
        foreach (DetectedSyncProvider provider in report.Providers)
        {
            var row = Find<AutomationNamedRowBorder>(
                surface, "SyncDiagnosticsProvider" + provider.Kind);
            Assert.Equal(
                $"{provider.DisplayName}: {SyncPhrase.RiskWord(provider.RiskLevel)}. "
                + provider.Recommendation,
                AutomationProperties.GetName(row));
            Assert.True(row.Focusable);
            Expander evidence = Find<Expander>(
                surface, "SyncDiagnosticsEvidence" + provider.Kind);
            Assert.Equal(SyncPhrase.Evidence, (string)evidence.Header);
            // SDINV-6: every evidence path is its own focusable line,
            // verbatim — the host never edits them.
            for (int i = 0; i < provider.EvidencePaths.Length; i++)
            {
                TextBlock line = Find<TextBlock>(
                    surface, $"SyncDiagnosticsEvidence{provider.Kind}Path{i}");
                Assert.Equal(provider.EvidencePaths[i], line.Text);
                Assert.True(line.Focusable);
            }
        }
        // (4) The LiveSync section renders LAST, after every provider
        // row, because a LiveSync provider is in the report.
        Assert.True(
            IndexOfId(surface, "SyncDiagnosticsLiveSync") > firstProviderIndex,
            "SD3: the LiveSync section renders after the provider rows.");
        panel.Shutdown();
    });

    [Fact]
    public void WithoutALiveSyncProviderThereIsNoConfigSection() => RunSta(() =>
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };

        panel.Reload();

        Assert.Equal(
            SyncPhrase.CountHeader(1),
            Find<TextBlock>(surface, "SyncDiagnosticsHeader").Text);
        // No warning row for a single Low-risk provider …
        Assert.Null(FindOrNull(surface, "SyncDiagnosticsWarning"));
        // … and no config section: the read returned NotPresent, but
        // SD3(4) gates the section on the PROVIDER, not the status.
        _ = Assert.IsType<LiveSyncConfigStatus.NotPresent>(panel.LiveSyncConfig);
        Assert.Null(FindOrNull(surface, "SyncDiagnosticsLiveSync"));
        panel.Shutdown();
    });

    [Fact]
    public void TheLiveSyncSectionRendersSixCombinedConfigRows() => RunSta(() =>
    {
        string plugin = Path.Combine(
            _root, ".obsidian", "plugins", "obsidian-livesync");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, "manifest.json"), "{}\n");
        File.WriteAllText(
            Path.Combine(plugin, "data.json"),
            "{\"couchDB_URI\":\"https://sync.example.com:5984/db\","
            + "\"couchDB_DBNAME\":\"vaultdb\",\"liveSync\":true,"
            + "\"syncOnSave\":false,\"encrypt\":true}\n");
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };

        panel.Reload();

        var section = Find<AutomationLandmarkBorder>(surface, "SyncDiagnosticsLiveSync");
        Assert.Equal(
            SyncPhrase.LiveSyncConfiguration, AutomationProperties.GetName(section));
        (string Id, string Name)[] expected =
        [
            ("SyncDiagnosticsLiveSyncServerHost", "Server host: sync.example.com:5984"),
            ("SyncDiagnosticsLiveSyncDatabase", "Database: vaultdb"),
            ("SyncDiagnosticsLiveSyncLiveSyncEnabled", "Live sync: On"),
            ("SyncDiagnosticsLiveSyncSyncOnSave", "Sync on save: Off"),
            // Absent in the file: "Unknown", never a guessed default.
            ("SyncDiagnosticsLiveSyncSyncOnStart", "Sync on start: Unknown"),
            ("SyncDiagnosticsLiveSyncEndToEndEncryption",
                "End-to-end encryption: On"),
        ];
        foreach ((string id, string name) in expected)
        {
            var row = Find<AutomationNamedRowBorder>(surface, id);
            Assert.Equal(name, AutomationProperties.GetName(row));
            Assert.True(row.Focusable);
        }
        panel.Shutdown();
    });

    [Fact]
    public void AnInstalledButUnconfiguredPluginSaysSo() => RunSta(() =>
    {
        string plugin = Path.Combine(
            _root, ".obsidian", "plugins", "obsidian-livesync");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, "manifest.json"), "{}\n");
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };

        panel.Reload();

        TextBlock absent = Find<TextBlock>(surface, "SyncDiagnosticsLiveSyncAbsent");
        Assert.Equal(SyncPhrase.ConfigAbsent, absent.Text);
        Assert.True(absent.Focusable);
        Assert.Null(FindOrNull(surface, "SyncDiagnosticsLiveSyncMalformed"));
        panel.Shutdown();
    });

    [Fact]
    public void TheSurfaceRefreshRoutesThroughTheWorkspaceSeam() => RunSta(() =>
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var panel = new SyncDiagnosticsViewModel(_session, synchronousForTests: true);
        int seamCalls = 0;
        panel.RefreshFromSurface = () => seamCalls++;
        var surface = new SyncDiagnosticsSurfaceView { Model = panel };
        panel.Reload();

        Button refresh = Find<Button>(surface, "SyncDiagnosticsRefresh");
        refresh.RaiseEvent(new RoutedEventArgs(
            System.Windows.Controls.Primitives.ButtonBase.ClickEvent, refresh));

        // SD7: the button never calls Reload directly — the workspace
        // funnel owns the disposal guard for button, menu, and watcher
        // alike.
        Assert.Equal(1, seamCalls);
        panel.Shutdown();
    });

    // --- Tree helpers ---

    private static T Find<T>(DependencyObject root, string automationId)
        where T : FrameworkElement
    {
        FrameworkElement? found = FindOrNull(root, automationId);
        Assert.True(found is not null, $"No element with automation id {automationId}.");
        return Assert.IsAssignableFrom<T>(found);
    }

    private static FrameworkElement? FindOrNull(
        DependencyObject root, string automationId)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency)
            {
                continue;
            }
            if (dependency is FrameworkElement element
                && string.Equals(
                    AutomationProperties.GetAutomationId(element),
                    automationId,
                    StringComparison.Ordinal))
            {
                return element;
            }
            if (FindOrNull(dependency, automationId) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>The index of an automation id among the content
    /// host's children — how the SD3 ORDER facts are stated.</summary>
    private static int IndexOfId(SyncDiagnosticsSurfaceView surface, string id)
    {
        FrameworkElement? target = FindOrNull(surface, id);
        Assert.NotNull(target);
        var host = (StackPanel)((ScrollViewer)
            ((DockPanel)surface.Content).Children[0]).Content;
        for (int i = 0; i < host.Children.Count; i++)
        {
            if (ReferenceEquals(host.Children[i], target))
            {
                return i;
            }
        }
        return -1;
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

/// <summary>
/// W4-8 (#740): the SD10 label goldens, pinned verbatim. LOCK-STEP
/// TWIN: apps/slate-mac/Sources/SlateMac/SyncDiagnosticsPanel.swift —
/// both platforms carry the identical strings by designation (§W-C
/// label goldens, w_c_matrix.md), so a change to either without the
/// other is drift, and this file is the Windows half of the pin.
/// </summary>
public sealed class SyncPhraseTests
{
    [Fact]
    public void RiskWordsMatchTheMacRiskText()
    {
        Assert.Equal("Low risk", SyncPhrase.RiskWord(RiskLevel.Low));
        Assert.Equal("Medium risk", SyncPhrase.RiskWord(RiskLevel.Medium));
        Assert.Equal("High risk", SyncPhrase.RiskWord(RiskLevel.High));
    }

    [Fact]
    public void TheCountHeaderPluralizesLikeCountCopy()
    {
        Assert.Equal("Sync, 0 systems detected", SyncPhrase.CountHeader(0));
        Assert.Equal("Sync, 1 system detected", SyncPhrase.CountHeader(1));
        Assert.Equal("Sync, 2 systems detected", SyncPhrase.CountHeader(2));
    }

    [Fact]
    public void TheRefreshLabelIsAContiguousPrefixOfItsAccessibleName()
    {
        Assert.Equal("Refresh", SyncPhrase.Refresh);
        Assert.Equal("Refresh sync diagnostics", SyncPhrase.RefreshAccessibleName);
        // WCAG 2.5.3 label-in-name.
        Assert.StartsWith(SyncPhrase.Refresh, SyncPhrase.RefreshAccessibleName);
    }

    [Fact]
    public void TheStateLabelsAndPrefixesArePinned()
    {
        Assert.Equal("Loading sync diagnostics", SyncPhrase.Loading);
        Assert.Equal("Retry", SyncPhrase.Retry);
        Assert.Equal(
            "Could not load sync diagnostics: disk on fire",
            SyncPhrase.LoadError("disk on fire"));
        Assert.Equal(
            "Warning: Multiple sync systems are managing this vault.",
            SyncPhrase.Warning("Multiple sync systems are managing this vault."));
        Assert.Equal("Evidence", SyncPhrase.Evidence);
    }

    [Fact]
    public void TheLiveSyncSectionLabelsArePinned()
    {
        Assert.Equal("LiveSync configuration", SyncPhrase.LiveSyncConfiguration);
        Assert.Equal("Server host", SyncPhrase.ServerHost);
        Assert.Equal("Database", SyncPhrase.Database);
        Assert.Equal("Live sync", SyncPhrase.LiveSyncEnabled);
        Assert.Equal("Sync on save", SyncPhrase.SyncOnSave);
        Assert.Equal("Sync on start", SyncPhrase.SyncOnStart);
        Assert.Equal("End-to-end encryption", SyncPhrase.EndToEndEncryption);
        Assert.Equal(
            "LiveSync config could not be read: invalid JSON",
            SyncPhrase.ConfigMalformed("invalid JSON"));
        Assert.Equal(
            "LiveSync plugin present; no config found.", SyncPhrase.ConfigAbsent);
    }

    [Fact]
    public void OptionalBooleansRenderOnOffUnknown()
    {
        Assert.Equal("On", SyncPhrase.OnOff(true));
        Assert.Equal("Off", SyncPhrase.OnOff(false));
        Assert.Equal("Unknown", SyncPhrase.OnOff(null));
        Assert.Equal("On", SyncPhrase.On);
        Assert.Equal("Off", SyncPhrase.Off);
        Assert.Equal("Unknown", SyncPhrase.Unknown);
        Assert.Equal("Live sync: On", SyncPhrase.ConfigRow("Live sync", "On"));
    }
}
