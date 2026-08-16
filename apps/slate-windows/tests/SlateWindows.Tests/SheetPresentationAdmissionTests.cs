// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Invariant 6's presentation-time admission (codex round 11, #742): a
/// sheet that PRESENTS — synchronously or from a deferred continuation
/// — closes any picker open at that moment. Admission at dispatch time
/// cannot hold the invariant alone: the files-citing lookup and the
/// bases edit-JSON fetch present from continuations, so the modal
/// decision taken at dispatch is stale by the time the sheet lands,
/// and a search overlay opened inside that window would otherwise sit
/// hidden-but-live beneath the sheet — the round-1/round-10 class
/// SD-5 retires. These facts run the REAL wiring — workspace property
/// change → lifecycle observer → picker close — over a real session,
/// the SD-4 suite's discipline.
/// </summary>
public sealed class SheetPresentationAdmissionTests : IDisposable
{
    private readonly List<string> _roots = [];
    private readonly object _announceGate = new();
    private readonly List<A11yEvent> _announced = [];

    public void Dispose()
    {
        foreach (string root in _roots)
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

    /// <summary>
    /// The synchronous shape: a citation summary presenting while the
    /// search overlay is open SUPERSEDES the overlay in the same
    /// notification — query preserved here, scope preservation pinned
    /// by the `Supersede*` facts in `SearchOverlayViewModelTests` — so
    /// Ctrl+Shift+F remains the way back (SD-5's restoration promise,
    /// not a reset).
    /// </summary>
    [Fact]
    public async Task ASheetPresentationClosesAnOpenSearchOverlay()
    {
        string root = NewVault("sheet-over-search");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);

        lifecycle.Search.Open();
        Assert.True(lifecycle.Search.IsOpen);
        lifecycle.Search.Query = "held";

        workspace.OpenCitationSummary();

        Assert.NotNull(workspace.CitationSummary);
        Assert.False(
            lifecycle.Search.IsOpen,
            "the citation summary presented over an OPEN search overlay — "
            + "the hidden overlay stays live in the UIA tree beneath the "
            + "sheet (invariant 6).");
        Assert.Equal("held", lifecycle.Search.Query);
    }

    /// <summary>
    /// The deferred shape — codex round 11's finding site: the
    /// files-citing sheet presents from its load continuation, after
    /// the dispatch-time modal decision has gone stale. The overlay
    /// open at presentation time must close regardless of when the
    /// lookup lands.
    /// </summary>
    [Fact]
    public async Task ADeferredSheetPresentationClosesTheSearchOverlayOpenAtLandingTime()
    {
        string root = NewVault("deferred-over-search");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);

        lifecycle.Search.Open();
        Assert.True(lifecycle.Search.IsOpen);

        // Dispatch with search already open: the load runs off the test
        // thread (the workspace's production background mode), so the
        // presentation lands from a continuation exactly as it does in
        // the app.
        workspace.OpenFilesCiting("anykey");

        Assert.True(
            SpinWait.SpinUntil(
                () => workspace.FilesCiting is not null,
                TimeSpan.FromSeconds(10)),
            "the files-citing lookup never landed");
        Assert.True(
            SpinWait.SpinUntil(
                () => !lifecycle.Search.IsOpen,
                TimeSpan.FromSeconds(10)),
            "the files-citing sheet landed over an OPEN search overlay — "
            + "the deferred presentation bypassed the modal invariant "
            + "(codex round 11).");
    }

    /// <summary>
    /// The same rule for Quick Open: a sheet presentation dismisses an
    /// open switcher rather than leaving it beneath the sheet.
    /// </summary>
    [Fact]
    public async Task ASheetPresentationDismissesAnOpenQuickOpenPicker()
    {
        string root = NewVault("sheet-over-quickopen");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);

        QuickSwitcherViewModel switcher = Assert.IsType<QuickSwitcherViewModel>(
            lifecycle.QuickSwitcher);
        switcher.Open();
        Assert.True(switcher.IsOpen);

        workspace.OpenCitationSummary();

        Assert.NotNull(workspace.CitationSummary);
        Assert.False(
            switcher.IsOpen,
            "the citation summary presented over an OPEN Quick Open — "
            + "the hidden picker keeps taking keys beneath the sheet.");
    }

    /// <summary>
    /// And for the palette. Outside a P9 invoke this is the deferred
    /// window closing; inside one it merely runs the dismissal P9
    /// itself performs on success — RecordInvocation reads only the
    /// row id, so the early dismissal is behaviour-preserving.
    /// </summary>
    [Fact]
    public async Task ASheetPresentationDismissesAnOpenPalette()
    {
        string root = NewVault("sheet-over-palette");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);

        lifecycle.Palette.Open();
        Assert.True(lifecycle.Palette.IsOpen);

        workspace.OpenCitationSummary();

        Assert.NotNull(workspace.CitationSummary);
        Assert.False(
            lifecycle.Palette.IsOpen,
            "the citation summary presented over an OPEN palette — the "
            + "hidden palette keeps owning the keyboard beneath the "
            + "sheet (the W5-1 round-1 class).");
    }

    /// <summary>
    /// Codex round 12: a scope preserved by a supersession must not
    /// survive a vault transition. The direct-switch path runs the
    /// transition reset through the CLOSED overlay — with a bare
    /// Close() (which early-returns when closed), vault A's tag scope
    /// re-armed silently inside vault B, stale chip and all.
    /// </summary>
    [Fact]
    public async Task ASupersededTagScopeDoesNotLeakIntoTheNextVault()
    {
        string rootA = NewVault("scope-leak-a");
        string rootB = NewVault("scope-leak-b");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(rootA);
        await lifecycle.OpenVaultAsync(rootA);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);

        lifecycle.Search.OpenTagScoped("project");
        workspace.OpenCitationSummary();
        Assert.False(lifecycle.Search.IsOpen);
        // The supersession preserved the scope — correct within one
        // vault, the hazard across two.
        Assert.Equal("project", lifecycle.Search.TagScopeName);

        await lifecycle.OpenVaultAsync(rootB);

        Assert.Null(lifecycle.Search.TagScopeName);
        Assert.IsType<SearchScope.Vault>(lifecycle.Search.Scope);
        Assert.Equal(string.Empty, lifecycle.Search.Query);
        lifecycle.Search.Open();
        Assert.True(lifecycle.Search.IsOpen);
        Assert.Equal(SearchOverlayState.Idle, lifecycle.Search.State);
    }

    // ---- Helpers --------------------------------------------------------

    private VaultLifecycleViewModel NewLifecycle(string root) =>
        new(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(root, "device-state", "recent-vaults.json")),
            announce: Record,
            sessionLoadWorker: work => Task.FromResult(work()));

    private void Record(A11yEvent announcement)
    {
        lock (_announceGate)
        {
            _announced.Add(announcement);
        }
    }

    private string NewVault(string label)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slate-windows-test-sheet-admission-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "note0.md"),
            "# Note 0\n\nBody text.\n");
        _roots.Add(root);
        return root;
    }
}
