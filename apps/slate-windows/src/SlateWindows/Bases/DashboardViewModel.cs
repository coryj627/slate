// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>One dashboard section's projection (the mac
/// DashboardSectionDocument twin, read-only): its own ephemeral
/// saved-query handle, executed once per load/refresh and closed with
/// the dashboard (INV-2).</summary>
internal enum DashboardSectionState
{
    Loading,
    Ready,
    Missing,
    Degraded,
    Failed,
}

internal sealed class DashboardSectionViewModel : BindableBase
{
    private DashboardSectionState _state = DashboardSectionState.Loading;
    private string? _message;
    private BasesResultSet? _result;

    public DashboardSectionViewModel(DashboardSectionStatus status)
    {
        Status = status;
    }

    public DashboardSectionStatus Status { get; }

    /// <summary>headingOverride → savedQueryName → the mac missing
    /// placeholder.</summary>
    public string Title =>
        Status.HeadingOverride is { Length: > 0 } heading ? heading
        : Status.SavedQueryName is { Length: > 0 } name ? name
        : "Missing saved query";

    public DashboardSectionState State
    {
        get => _state;
        internal set => SetField(ref _state, value);
    }

    public string? Message
    {
        get => _message;
        internal set => SetField(ref _message, value);
    }

    public BasesResultSet? Result
    {
        get => _result;
        internal set => SetField(ref _result, value);
    }
}

/// <summary>
/// W4-6 (#738, contract C12): a dashboard tab's document — sections
/// over ephemeral saved-query handles, all read-only. Failures are
/// per-section (one broken saved query never blanks its siblings);
/// a missing section says so with the mac wording and points at the
/// dashboard editor.
/// </summary>
internal sealed class DashboardViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private int _generation;
    private string _name = string.Empty;

    public DashboardViewModel(
        VaultSession session,
        string id,
        string name,
        Action<A11yEvent> announce,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        Id = id;
        _name = name;
        _announce = announce;
    }

    public string Id { get; }

    public string Name
    {
        get => _name;
        private set => SetField(ref _name, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<DashboardSectionViewModel>
        Sections
    { get; } = [];

    public event EventHandler? SectionsPublished;

    public void Load()
    {
        if (IsShutDown)
        {
            return;
        }
        int generation = Interlocked.Increment(ref _generation);
        StartWork(() => LoadBody(generation));
    }

    private void LoadBody(int generation)
    {
        Dashboard dashboard;
        try
        {
            dashboard = _session.GetDashboard(Id);
        }
        catch (VaultException failure)
        {
            Post(() =>
            {
                if (Volatile.Read(ref _generation) != generation)
                {
                    return;
                }
                Sections.Clear();
                var failed = new DashboardSectionViewModel(
                    new DashboardSectionStatus(string.Empty, null, null, null, false))
                {
                    State = DashboardSectionState.Failed,
                    Message = $"Dashboard could not be loaded: {failure.Message}",
                };
                Sections.Add(failed);
                SectionsPublished?.Invoke(this, EventArgs.Empty);
            });
            return;
        }
        var projected = new List<DashboardSectionViewModel>();
        foreach (DashboardSectionStatus status in dashboard.Sections)
        {
            var section = new DashboardSectionViewModel(status);
            if (status.Missing || status.SavedQueryId.Length == 0)
            {
                section.State = DashboardSectionState.Missing;
                section.Message =
                    "Missing saved query. Remove this section or pick a replacement.";
                projected.Add(section);
                continue;
            }
            ulong? handle = null;
            try
            {
                handle = _session.OpenSavedQuery(status.SavedQueryId);
                using var cancel = new CancelToken();
                BasesResultSet result = _session.BaseExecute(
                    handle.Value, view: 0, thisPath: null, quickFilter: null, cancel);
                section.Result = result;
                if (result.ViewError is { Length: > 0 } viewError)
                {
                    section.State = DashboardSectionState.Degraded;
                    section.Message = viewError;
                }
                else
                {
                    section.State = DashboardSectionState.Ready;
                }
            }
            catch (VaultException failure)
            {
                section.State = DashboardSectionState.Failed;
                section.Message = failure.Message;
            }
            finally
            {
                if (handle is { } opened)
                {
                    try
                    {
                        _session.CloseBase(opened);
                    }
                    catch (Exception closeFailure) when (closeFailure
                        is VaultException or ObjectDisposedException)
                    {
                        // Teardown race: the session died first — a
                        // tracked task must never fault (the
                        // scheduler contract).
                    }
                }
            }
            projected.Add(section);
        }
        Post(() =>
        {
            if (Volatile.Read(ref _generation) != generation)
            {
                return;
            }
            Name = dashboard.Name;
            Sections.Clear();
            foreach (DashboardSectionViewModel section in projected)
            {
                Sections.Add(section);
            }
            SectionsPublished?.Invoke(this, EventArgs.Empty);
        });
    }

    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _generation);
    }
}

/// <summary>
/// The dashboard editor's draft (the mac DashboardEditorDraft twin) —
/// an in-window overlay (W4-5 D-1), name + ordered sections with
/// saved-query picker and heading/view overrides. Save routes through
/// SaveDashboard/UpdateDashboard with the canonical announcements.
/// </summary>
internal sealed class DashboardEditorViewModel : BindableBase
{
    private string _name;

    public DashboardEditorViewModel(string? dashboardId, string name)
    {
        DashboardId = dashboardId;
        _name = name;
    }

    /// <summary>Null for a NEW dashboard.</summary>
    public string? DashboardId { get; }

    /// <summary>The registry timestamp captured at editor open — the
    /// C12 stale-section guard's baseline: a save re-reads and
    /// refuses with BasesDashboardSectionStale when the dashboard
    /// changed underneath the editor.</summary>
    internal long OpenedModifiedAtMs { get; set; }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<DashboardEditorSection>
        Sections
    { get; } = [];

    public bool CanSave => Name.Trim().Length > 0;

    public string Title => DashboardId is null ? "New dashboard" : "Edit dashboard";

    public DashboardSection[] DraftSections() =>
        Sections
            .Select(section => new DashboardSection(
                section.SavedQueryId,
                NullIfBlank(section.HeadingOverride),
                NullIfBlank(section.ViewOverride)))
            .ToArray();

    private static string? NullIfBlank(string value) =>
        value.Trim().Length == 0 ? null : value.Trim();
}

internal sealed class DashboardEditorSection : BindableBase
{
    private string _headingOverride = string.Empty;
    private string _viewOverride = string.Empty;

    public DashboardEditorSection(string savedQueryId, string savedQueryName)
    {
        SavedQueryId = savedQueryId;
        SavedQueryName = savedQueryName;
    }

    public string SavedQueryId { get; set; }

    public string SavedQueryName { get; set; }

    public string HeadingOverride
    {
        get => _headingOverride;
        set => SetField(ref _headingOverride, value);
    }

    /// <summary>Blank = Default; "table"/"list" per the mac picker.</summary>
    public string ViewOverride
    {
        get => _viewOverride;
        set => SetField(ref _viewOverride, value);
    }
}
