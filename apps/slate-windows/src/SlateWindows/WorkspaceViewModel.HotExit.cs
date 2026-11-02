// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>Plan 44 WB-29 §2: what the open read of a vault's store found.
/// Without a key, after a failed read, or with the vault held elsewhere the
/// writer is inert; only a damaged read keeps the key, for Discard All's
/// repair (§6).</summary>
internal sealed record HotExitOpen(
    HotExitVaultKey? Key,
    ulong Epoch,
    IReadOnlyList<HotExitEntry> Entries,
    IReadOnlyList<HotExitRestore> Decisions,
    IReadOnlyList<HotExitCurrentClaim> Claims,
    string? Reason,
    bool Damaged)
{
    /// <summary>A host never installed has a null reason: it keeps today's
    /// prompt silently (§6).</summary>
    internal static HotExitOpen None(string? reason) =>
        new(null, 0, [], [], [], reason, Damaged: false);

    internal static HotExitOpen DamagedVault(HotExitVaultKey key, string reason) =>
        new(key, 0, [], [], [], reason, Damaged: true);

    internal bool IsInert => Key is null || Damaged;
}

/// <summary>§2, §5: how the store's entries map to the layout's tabs, decided
/// before any tab is built. A new note's text is preloaded so nothing is read
/// for it, and the layout it restores from names the ids the entries keep.
/// </summary>
internal sealed class HotExitRestorePlan
{
    internal sealed record Planned(
        HotExitEntry Entry,
        HotExitRestore Decision,
        string Path,
        string? ObservedClaim,
        string? Text);

    private readonly Dictionary<Guid, Planned> _byLayoutTab = [];

    private HotExitRestorePlan(WorkspaceSnapshot? layout) => Layout = layout;

    /// <summary>The layout to restore: the persisted one, with any tab that
    /// lost its id to a recovered peer holding a fresh one.</summary>
    internal WorkspaceSnapshot? Layout { get; private set; }

    /// <summary>Entries with no layout tab, in written order.</summary>
    internal List<(Guid Id, string EntryId, Planned Plan)> NewTabs { get; } = [];

    /// <summary>Current claims with no payload (§2).</summary>
    internal List<HotExitCurrentClaim> OrphanClaims { get; } = [];

    internal IReadOnlyDictionary<Guid, Planned> ByLayoutTab => _byLayoutTab;

    /// <summary>What a kept note's tab loads instead of reading disk: nothing
    /// for a note still at its baseline, empty for a new note.</summary>
    internal string? PreloadFor(Guid tabId) =>
        _byLayoutTab.TryGetValue(tabId, out Planned? planned) && planned.Decision
            is HotExitRestore.ExistsNow or HotExitRestore.Gone or HotExitRestore.Recovered
            ? string.Empty
            : null;

    internal static HotExitRestorePlan For(HotExitOpen open, WorkspaceSnapshot? layout)
    {
        var plan = new HotExitRestorePlan(layout);
        if (open.IsInert)
        {
            return plan;
        }
        List<(HotExitEntry Entry, HotExitRestore Decision, string Path)> kept = [];
        for (int index = 0; index < open.Entries.Count && index < open.Decisions.Count; index++)
        {
            kept.Add((open.Entries[index], open.Decisions[index], PathOf(open.Decisions[index])));
        }
        var entryIds = new HashSet<string>(kept.Select(item => item.Entry.EntryId), StringComparer.Ordinal);
        plan.OrphanClaims.AddRange(open.Claims.Where(claim => !entryIds.Contains(claim.EntryId)));
        string? ObservedClaim(HotExitEntry entry) =>
            open.Claims.FirstOrDefault(claim =>
                string.Equals(claim.EntryId, entry.EntryId, StringComparison.Ordinal))?.Incarnation;
        var layoutTabs = new Dictionary<string, WorkspaceTabState>(StringComparer.Ordinal);
        if (layout is not null)
        {
            foreach (WorkspaceTabState tab in TabsOf(layout.Root))
            {
                layoutTabs[tab.Id.ToString("D")] = tab;
            }
        }
        // §5, WB-29a §3: peers whose texts differ. The latest goes to every
        // tab on the path; each other distinct text becomes a recovered note
        // keeping its entry's id, and the tab that held that id takes a fresh
        // one, since its own text is the latest's.
        var taken = new HashSet<string>(kept.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);
        var displaced = new Dictionary<Guid, Guid>();
        var recovered = new HashSet<string>(StringComparer.Ordinal);
        foreach (IGrouping<string, (HotExitEntry Entry, HotExitRestore Decision, string Path)> peers in kept
            .Where(item => item.Decision is HotExitRestore.Restore or HotExitRestore.RestoreStale)
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            string latest = peers
                .OrderBy(peer => peer.Entry.WrittenAt, StringComparer.Ordinal)
                .Last().Entry.Text;
            foreach ((HotExitEntry Entry, HotExitRestore Decision, string Path) peer in peers)
            {
                if (string.Equals(peer.Entry.Text, latest, StringComparison.Ordinal))
                {
                    if (layoutTabs.TryGetValue(peer.Entry.EntryId, out WorkspaceTabState? state))
                    {
                        plan._byLayoutTab[state.Id] = new Planned(
                            peer.Entry, peer.Decision, peer.Path, ObservedClaim(peer.Entry), peer.Entry.Text);
                    }
                    else
                    {
                        plan.AddNewTab(peer.Entry, peer.Decision, peer.Path, ObservedClaim(peer.Entry));
                    }
                    continue;
                }
                string name = FreeRecoveredName(peer.Path, taken);
                taken.Add(name);
                recovered.Add(peer.Entry.EntryId);
                if (layoutTabs.TryGetValue(peer.Entry.EntryId, out WorkspaceTabState? holder))
                {
                    Guid fresh = Guid.NewGuid();
                    displaced[holder.Id] = fresh;
                    // The displaced tab shows the latest text, as a peer does.
                    plan._byLayoutTab[fresh] = new Planned(
                        peers.OrderBy(other => other.Entry.WrittenAt, StringComparer.Ordinal).Last().Entry,
                        peer.Decision,
                        peer.Path,
                        null,
                        latest);
                    layoutTabs.Remove(peer.Entry.EntryId);
                }
                plan.AddNewTab(
                    peer.Entry,
                    new HotExitRestore.Recovered(name),
                    name,
                    ObservedClaim(peer.Entry));
            }
        }
        foreach ((HotExitEntry Entry, HotExitRestore Decision, string Path) item in kept)
        {
            if (item.Decision is HotExitRestore.Restore or HotExitRestore.RestoreStale)
            {
                continue;
            }
            if (layoutTabs.TryGetValue(item.Entry.EntryId, out WorkspaceTabState? state))
            {
                plan._byLayoutTab[state.Id] = new Planned(
                    item.Entry, item.Decision, item.Path, ObservedClaim(item.Entry), item.Entry.Text);
            }
            else
            {
                plan.AddNewTab(item.Entry, item.Decision, item.Path, ObservedClaim(item.Entry));
            }
        }
        if (layout is not null)
        {
            plan.Layout = layout with { Root = Rewrite(layout.Root, plan, displaced) };
        }
        return plan;
    }

    private void AddNewTab(HotExitEntry entry, HotExitRestore decision, string path, string? observedClaim)
    {
        // A canonical id becomes the tab's Id, so the next layout names it and
        // a kept close reopens one tab, not two. Anything else (no id this
        // host writes) keeps the entry's id beside a fresh Id (§2).
        bool canonical = Guid.TryParse(entry.EntryId, out Guid parsed)
            && string.Equals(parsed.ToString("D"), entry.EntryId, StringComparison.Ordinal);
        NewTabs.Add((
            canonical ? parsed : Guid.NewGuid(),
            entry.EntryId,
            new Planned(entry, decision, path, observedClaim, entry.Text)));
    }

    private static string PathOf(HotExitRestore decision) => decision switch
    {
        HotExitRestore.Restore restore => restore.Path,
        HotExitRestore.RestoreStale stale => stale.Path,
        HotExitRestore.ExistsNow exists => exists.Path,
        HotExitRestore.Gone gone => gone.Path,
        HotExitRestore.Recovered recovered => recovered.Path,
        _ => throw new InvalidOperationException("unknown restore decision"),
    };

    /// <summary>{stem} (recovered).md, numbered when taken.</summary>
    private static string FreeRecoveredName(string path, HashSet<string> taken)
    {
        string directory = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
        string stem = System.IO.Path.GetFileNameWithoutExtension(path);
        string prefix = directory.Length == 0 ? string.Empty : directory + "/";
        string candidate = $"{prefix}{stem} (recovered).md";
        for (int number = 2; taken.Contains(candidate); number++)
        {
            candidate = $"{prefix}{stem} (recovered {number}).md";
        }
        return candidate;
    }

    private static IEnumerable<WorkspaceTabState> TabsOf(WorkspaceNodeState node) => node switch
    {
        WorkspaceGroupState group => group.Tabs,
        WorkspaceSplitState split => split.Children.SelectMany(TabsOf),
        _ => [],
    };

    /// <summary>Re-points each kept tab to its classified path, writes a
    /// displaced tab's fresh id and remaps its group's active tab with it.</summary>
    private static WorkspaceNodeState Rewrite(
        WorkspaceNodeState node,
        HotExitRestorePlan plan,
        Dictionary<Guid, Guid> displaced) => node switch
        {
            WorkspaceGroupState group => group with
            {
                ActiveTab = group.ActiveTab is { } active && displaced.TryGetValue(active, out Guid remapped)
                    ? remapped
                    : group.ActiveTab,
                Tabs =
                [
                    .. group.Tabs.Select(tab =>
                    {
                        Guid id = displaced.TryGetValue(tab.Id, out Guid fresh) ? fresh : tab.Id;
                        return plan._byLayoutTab.TryGetValue(id, out Planned? planned)
                            ? tab with
                            {
                                Id = id,
                                Item = tab.Item with { Kind = WorkspaceItemKind.Markdown, Path = planned.Path },
                            }
                            : tab with { Id = id };
                    }),
                ],
            },
            WorkspaceSplitState split => split with
            {
                Children = [.. split.Children.Select(child => Rewrite(child, plan, displaced))],
            },
            _ => node,
        };
}

/// <summary>§4: which obligations a settle works on.</summary>
internal enum HotExitSettleMode
{
    /// <summary>Issues every dirty tab's latest snapshot and waits for all.</summary>
    Normal,

    /// <summary>After a committed Discard All: no claim, no write.</summary>
    RetireOnly,
}

/// <summary>§4: what a settle found. A null reason with
/// <see cref="Kept"/> false is an uninstalled host: the prompt, silently.</summary>
internal readonly record struct HotExitSettleResult(bool Kept, string? Reason);

/// <summary>
/// Plan 44 WB-29 §3: one workspace's recovery writes. Every tab owns its entry
/// for one dirty episode, ended by the reader (retired) or abandoned by
/// disposal (kept, as a crash keeps it). The writer never decides inside a
/// mutation (O7): a change schedules one coalesced re-evaluation that reads the
/// finished state. It holds no reference the store host needs, and nothing
/// joins it. All its state is the dispatcher's.
/// </summary>
internal sealed class HotExitWriter
{
    /// <summary>A changed snapshot is written this long after the last
    /// change (§3).</summary>
    internal static TimeSpan DebounceDelay { get; set; } = TimeSpan.FromSeconds(1);

    private readonly HotExitStoreHost _host;
    private readonly HotExitOpen _open;
    private readonly Dispatcher _dispatcher;
    private readonly Func<IEnumerable<WorkspaceTabViewModel>> _tabs;
    private readonly Dictionary<WorkspaceTabViewModel, TabState> _states = [];
    private readonly HashSet<WorkspaceTabViewModel> _pending = [];
    private ulong _epoch;
    private bool _postPending;
    private bool _detached;
    private bool _fenced;

    internal HotExitWriter(
        HotExitStoreHost host,
        HotExitOpen open,
        Dispatcher dispatcher,
        Func<IEnumerable<WorkspaceTabViewModel>> tabs)
    {
        _host = host;
        _open = open;
        _dispatcher = dispatcher;
        _tabs = tabs;
        _epoch = open.Epoch;
    }

    private HotExitVaultKey? ActiveKey => _open.IsInert ? null : _open.Key;

    internal bool IsInertForTests => _open.IsInert;

    /// <summary>The vault's key, kept for a damaged vault's repair too.</summary>
    internal HotExitVaultKey? Key => _open.Key;

    /// <summary>Starts watching a tab (§3, O7).</summary>
    internal void Attach(WorkspaceTabViewModel tab)
    {
        if (ActiveKey is null || _detached)
        {
            return;
        }
        tab.HotExitChanged = Observe;
        Observe(tab);
    }

    /// <summary>§5: a restored tab starts its episode at install. It adopts
    /// the claim the snapshot observed and carries every inherited
    /// incarnation until the reader ends it.</summary>
    internal void InstallRestored(
        WorkspaceTabViewModel tab,
        string? observedClaim,
        IEnumerable<string> inherited)
    {
        if (ActiveKey is null || _detached)
        {
            return;
        }
        TabState state = StateFor(tab);
        state.ExpectedClaim = observedClaim;
        state.Inherited.AddRange(inherited.Distinct(StringComparer.Ordinal));
        tab.HotExitChanged = Observe;
        Observe(tab);
    }

    /// <summary>§2: a current claim with no payload. The tab with that id
    /// carries it and claims expecting it; it holds no text.</summary>
    internal void InheritOrphanClaim(WorkspaceTabViewModel tab, string incarnation)
    {
        if (ActiveKey is null || _detached)
        {
            return;
        }
        TabState state = StateFor(tab);
        state.ExpectedClaim = incarnation;
        state.Inherited.Add(incarnation);
    }

    /// <summary>§2: a current claim with no payload that no tab holds.</summary>
    internal void RetireOrphanClaim(string entryId, string incarnation)
    {
        if (ActiveKey is { } key && !_detached)
        {
            _ = _host.RetireAsync(key, entryId, [incarnation], _epoch);
        }
    }

    // ---- O7: read after the mutation ---------------------------------------

    private void Observe(WorkspaceTabViewModel tab)
    {
        if (_detached)
        {
            return;
        }
        _pending.Add(tab);
        if (_postPending)
        {
            return;
        }
        _postPending = true;
        _ = _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DrainPending));
    }

    private void DrainPending()
    {
        _postPending = false;
        if (_detached)
        {
            _pending.Clear();
            return;
        }
        WorkspaceTabViewModel[] tabs = [.. _pending];
        _pending.Clear();
        foreach (WorkspaceTabViewModel tab in tabs)
        {
            Evaluate(tab);
        }
    }

    private void Evaluate(WorkspaceTabViewModel tab)
    {
        if (ActiveKey is null || _detached || tab.IsDisposed || !tab.IsMarkdown)
        {
            return;
        }
        TabState state = StateFor(tab);
        if (!tab.IsDirty)
        {
            state.DiscardedUnkept = false;
            if (state.EpisodeLive || state.HeldIncarnations.Count > 0)
            {
                EndByReader(state);
            }
            return;
        }
        if (state.DiscardedUnkept)
        {
            if (tab.ItemIdentity == state.CoveredIdentity && tab.EditRevision == state.CoveredRevision)
            {
                return;
            }
            state.DiscardedUnkept = false;
            state.NotKeptReason = null;
        }
        if (_fenced)
        {
            return;
        }
        if (!state.EpisodeLive)
        {
            state.EpisodeLive = true;
            state.Token++;
            state.ClaimLost = false;
            state.NotKeptReason = null;
            state.LastIssued = null;
            state.LastKept = null;
            state.NextGeneration = 1;
        }
        if (state.ClaimLost)
        {
            return;
        }
        if (state.LiveClaim is null)
        {
            IssueClaim(state);
            return;
        }
        Snapshot now = SnapshotOf(tab);
        if (now == state.LastIssued)
        {
            return;
        }
        // A baseline change is written at once (§3); an edit after the
        // debounce.
        if (state.LastIssued is { } issued && issued.SameBaseline(now))
        {
            ScheduleDebounce(state);
        }
        else
        {
            IssueWrite(state);
        }
    }

    private static Snapshot SnapshotOf(WorkspaceTabViewModel tab) => new(
        tab.Path,
        tab.SavedContentHash,
        tab.FileIdentity,
        tab.ItemIdentity,
        tab.EditRevision);

    // ---- Claims ------------------------------------------------------------

    private void IssueClaim(TabState state)
    {
        if (state.ClaimInFlight || ActiveKey is not { } key)
        {
            return;
        }
        // An errored claim retries with its own incarnation (O2).
        state.ClaimIncarnation ??= SlateUniffiMethods.NewHotExitIncarnation();
        state.HeldIncarnations.Add(state.ClaimIncarnation);
        state.ClaimInFlight = true;
        int token = state.Token;
        ulong epoch = _epoch;
        string incarnation = state.ClaimIncarnation;
        Task<HotExitResult<HotExitClaimOutcome>> call = _host.ClaimAsync(
            key,
            epoch,
            state.Tab.RecoveryEntryId,
            state.ExpectedClaim,
            incarnation);
        PostResult(call, result => OnClaimResolved(state, token, incarnation, result));
    }

    private void OnClaimResolved(
        TabState state,
        int token,
        string incarnation,
        HotExitResult<HotExitClaimOutcome> result)
    {
        // O3: an outcome counts only for the current token. An older
        // episode's claim is already named by that episode's retirement.
        bool current = token == state.Token && state.EpisodeLive;
        if (token == state.Token)
        {
            state.ClaimInFlight = false;
        }
        if (result.Moot)
        {
            return;
        }
        if (result.ErrorReason is { } reason)
        {
            if (current)
            {
                state.NotKeptReason = reason;
            }
            return;
        }
        switch (result.Value)
        {
            case HotExitClaimOutcome.Claimed claimed when current:
                state.LiveClaim = claimed.Claim;
                state.ClaimIncarnation = null;
                state.ExpectedClaim = null;
                state.NotKeptReason = null;
                Evaluate(state.Tab);
                break;
            case HotExitClaimOutcome.Superseded superseded:
                state.HeldIncarnations.Remove(incarnation);
                state.ClaimIncarnation = null;
                if (current)
                {
                    OnSuperseded(state, superseded.By, isClaim: true);
                }
                break;
            case HotExitClaimOutcome.Refused refused:
                state.HeldIncarnations.Remove(incarnation);
                state.ClaimIncarnation = null;
                if (current)
                {
                    state.NotKeptReason = SlateUniffiMethods.HotExitRefusalReason(refused.Refusal);
                }
                break;
        }
    }

    private void OnSuperseded(TabState state, HotExitSupersession by, bool isClaim)
    {
        if (by is HotExitSupersession.Epoch epoch)
        {
            OnEpochMoved(epoch.Current);
            return;
        }
        // Another holder owns the entry: this tab no longer carries anything
        // that holder inherited, and never takes a claim back (§3, §5).
        state.ClaimLost = true;
        state.LiveClaim = null;
        state.NotKeptReason = SlateUniffiMethods.HotExitReasonTakenOver();
        if (isClaim)
        {
            state.Inherited.Clear();
            state.ExpectedClaim = null;
        }
    }

    /// <summary>O6: an epoch this writer's own fence does not explain. No
    /// episode re-claims by itself: what the epoch retired stays unkept and
    /// the next close asks; an edit after it starts a fresh episode.</summary>
    private void OnEpochMoved(ulong current)
    {
        if (current <= _epoch)
        {
            return;
        }
        _epoch = current;
        if (_fenced)
        {
            return;
        }
        foreach (TabState state in _states.Values)
        {
            if (state.Tab.IsDirty && !state.DiscardedUnkept)
            {
                MarkUnkept(state, SlateUniffiMethods.HotExitReasonTakenOver());
            }
        }
    }

    private static void MarkUnkept(TabState state, string reason)
    {
        state.CancelDebounce();
        state.Token++;
        state.ClaimInFlight = false;
        state.WriteInFlight = false;
        state.EpisodeLive = false;
        state.LiveClaim = null;
        state.ClaimIncarnation = null;
        state.ExpectedClaim = null;
        state.LastIssued = null;
        state.LastKept = null;
        state.HeldIncarnations.Clear();
        state.Inherited.Clear();
        state.DiscardedUnkept = true;
        state.CoveredIdentity = state.Tab.ItemIdentity;
        state.CoveredRevision = state.Tab.EditRevision;
        state.NotKeptReason = reason;
    }

    // ---- Writes ------------------------------------------------------------

    private void ScheduleDebounce(TabState state)
    {
        if (state.Debounce is null)
        {
            state.Debounce = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = DebounceDelay,
            };
            state.Debounce.Tick += (_, _) =>
            {
                state.CancelDebounce();
                if (!_detached)
                {
                    IssueWrite(state);
                }
            };
        }
        state.Debounce.Stop();
        state.Debounce.Start();
    }

    private void IssueWrite(TabState state)
    {
        if (ActiveKey is not { } key || state.LiveClaim is not { } claim || state.ClaimLost || _fenced)
        {
            return;
        }
        if (state.WriteInFlight)
        {
            state.WriteDue = true;
            return;
        }
        WorkspaceTabViewModel tab = state.Tab;
        Snapshot snapshot = SnapshotOf(tab);
        ulong generation = state.NextGeneration++;
        state.WriteInFlight = true;
        state.WriteDue = false;
        state.LastIssued = snapshot;
        int token = state.Token;
        var entry = new HotExitEntryWrite(
            tab.Path,
            tab.Text,
            tab.SavedContentHash,
            tab.FileIdentity);
        Task<HotExitResult<HotExitWriteOutcome>> call = _host.WriteAsync(key, claim, generation, entry);
        PostResult(call, result => OnWriteResolved(state, token, claim, snapshot, generation, result));
    }

    private void OnWriteResolved(
        TabState state,
        int token,
        HotExitClaim claim,
        Snapshot snapshot,
        ulong generation,
        HotExitResult<HotExitWriteOutcome> result)
    {
        bool current = token == state.Token
            && state.LiveClaim is { } live
            && string.Equals(live.Incarnation, claim.Incarnation, StringComparison.Ordinal);
        if (token == state.Token)
        {
            state.WriteInFlight = false;
        }
        if (!current || result.Moot)
        {
            return;
        }
        if (result.ErrorReason is { } reason)
        {
            state.NotKeptReason = reason;
        }
        else
        {
            switch (result.Value)
            {
                case HotExitWriteOutcome.Durable:
                    state.LastKept = snapshot;
                    state.NotKeptReason = null;
                    break;
                case HotExitWriteOutcome.Superseded { By: HotExitSupersession.Newer newer }
                    when newer.StoredGeneration >= generation:
                    state.LastKept = snapshot;
                    state.NotKeptReason = null;
                    break;
                case HotExitWriteOutcome.Superseded superseded:
                    OnSuperseded(state, superseded.By, isClaim: false);
                    break;
                case HotExitWriteOutcome.Refused refused:
                    state.NotKeptReason = SlateUniffiMethods.HotExitRefusalReason(refused.Refusal);
                    break;
            }
        }
        if (state.WriteDue)
        {
            IssueWrite(state);
        }
        else
        {
            Evaluate(state.Tab);
        }
    }

    private void PostResult<T>(Task<T> task, Action<T> onResolved)
    {
        _ = task.ContinueWith(
            finished =>
            {
                // Late outcomes of a detached writer publish nothing.
                if (!_detached && !_dispatcher.HasShutdownStarted)
                {
                    _ = _dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new Action(() =>
                        {
                            if (!_detached)
                            {
                                onResolved(finished.Result);
                            }
                        }));
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    // ---- Ending an episode (O4) --------------------------------------------

    /// <summary>§4: the reader ended this tab's episode (Discard on its close,
    /// its pane's close, a dirty navigation), before it is disposed or
    /// re-pointed. Whatever it holds or inherited is retired.</summary>
    internal void EndEpisodeByReader(WorkspaceTabViewModel tab)
    {
        if (ActiveKey is null || _detached)
        {
            return;
        }
        // A pending re-evaluation of this tab is overtaken by the end.
        _pending.Remove(tab);
        EndByReader(StateFor(tab));
    }

    private void EndByReader(TabState state)
    {
        if (ActiveKey is not { } key)
        {
            return;
        }
        state.CancelDebounce();
        List<string> incarnations = [.. state.HeldIncarnations, .. state.Inherited];
        if (state.LiveClaim is { } live)
        {
            incarnations.Add(live.Incarnation);
            incarnations.AddRange(live.Adopted);
        }
        if (state.ClaimIncarnation is { } pending)
        {
            incarnations.Add(pending);
        }
        state.Token++;
        // In-flight flags belong to the episode: a held call of this one
        // never stops the next one's, and the store orders them (O2, O3).
        state.ClaimInFlight = false;
        state.WriteInFlight = false;
        state.EpisodeLive = false;
        state.LiveClaim = null;
        state.ClaimIncarnation = null;
        state.ExpectedClaim = null;
        state.ClaimLost = false;
        state.LastIssued = null;
        state.LastKept = null;
        state.NotKeptReason = null;
        state.WriteDue = false;
        state.HeldIncarnations.Clear();
        state.Inherited.Clear();
        List<string> distinct = [.. incarnations.Distinct(StringComparer.Ordinal)];
        if (distinct.Count > 0)
        {
            _ = _host.RetireAsync(key, state.Tab.RecoveryEntryId, distinct, _epoch);
        }
    }

    // ---- Settling (§4, O5) -------------------------------------------------

    /// <summary>Waits pumped, within <paramref name="budget"/>, for every
    /// obligation of the vault's key and every dirty tab to be kept, re-reading
    /// the tabs after each frame. Always runs at least one dispatcher frame.</summary>
    internal HotExitSettleResult Settle(TimeSpan budget, HotExitSettleMode mode)
    {
        if (_open.IsInert)
        {
            return SettleInert(mode);
        }
        HotExitVaultKey key = ActiveKey!;
        DateTime deadline = DateTime.UtcNow + budget;
        // The one guaranteed frame: what was queued before the settle runs
        // inside it, and the teardown's re-check has that frame to guard.
        _ = PumpedWait.Until(
            _dispatcher,
            _dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background).Task);
        bool epochChecked = false;
        int attempt = ++_settleAttempt;
        while (true)
        {
            DrainPending();
            _host.RetryParkedRetirements(key);
            bool issued = mode == HotExitSettleMode.Normal && IssueLatest(attempt);
            // An outcome is posted after its obligation resolves: the writer
            // decides only once it has read every one.
            if (!issued && !_host.HasUnresolved(key) && !AnyCallInFlight())
            {
                if (mode == HotExitSettleMode.RetireOnly)
                {
                    return new HotExitSettleResult(true, null);
                }
                if (!epochChecked)
                {
                    epochChecked = true;
                    Task<HotExitResult<ulong>> read = _host.EpochAsync(key);
                    PumpBounded(read, deadline);
                    if (read.IsCompleted && read.Result is { Succeeded: true } answered)
                    {
                        OnEpochMoved(answered.Value);
                    }
                    continue;
                }
                // Each failed call had its one try: nothing further can
                // change, so the settle says so now.
                return AllDirtyKept()
                    ? new HotExitSettleResult(true, null)
                    : new HotExitSettleResult(false, FirstReason(key));
            }
            if (DateTime.UtcNow >= deadline)
            {
                return mode == HotExitSettleMode.Normal && !_host.HasUnresolved(key) && AllDirtyKept()
                    ? new HotExitSettleResult(true, null)
                    : new HotExitSettleResult(false, FirstReason(key));
            }
            // Always a real frame: a posted outcome runs in it.
            PumpBounded(
                _host.HasUnresolved(key)
                    ? Task.WhenAny(_host.WhenSettled(key), Task.Delay(TimeSpan.FromMilliseconds(50)))
                    : Task.Delay(TimeSpan.FromMilliseconds(5)),
                deadline);
        }
    }

    private int _settleAttempt;

    private bool AnyCallInFlight() =>
        _states.Values.Any(state => state.ClaimInFlight || state.WriteInFlight);

    private HotExitSettleResult SettleInert(HotExitSettleMode mode)
    {
        if (_open.Key is { } key && _host.HasUnresolved(key))
        {
            return new HotExitSettleResult(false, SlateUniffiMethods.HotExitReasonDiscardPending());
        }
        return mode == HotExitSettleMode.Normal && _tabs().Any(tab => tab.IsDirty)
            ? new HotExitSettleResult(false, _open.Reason)
            : new HotExitSettleResult(true, null);
    }

    private void PumpBounded(Task task, DateTime deadline)
    {
        TimeSpan remaining = deadline - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }
        _ = PumpedWait.Until(_dispatcher, Task.WhenAny(task, Task.Delay(remaining)));
    }

    /// <summary>Issues what the debounce was holding, and retries an errored
    /// claim or write once per settle, for every dirty tab not kept at its
    /// latest. True when it issued anything.</summary>
    private bool IssueLatest(int attempt)
    {
        if (_fenced)
        {
            return false;
        }
        bool issued = false;
        foreach (WorkspaceTabViewModel tab in _tabs())
        {
            if (tab.IsDisposed || !tab.IsDirty || !tab.IsMarkdown)
            {
                continue;
            }
            TabState state = StateFor(tab);
            state.CancelDebounce();
            if (state.DiscardedUnkept || state.ClaimLost || state.ClaimInFlight || state.WriteInFlight)
            {
                continue;
            }
            Snapshot now = SnapshotOf(tab);
            if (state.LastKept == now)
            {
                continue;
            }
            // New text is always tried; a failure of this same snapshot once.
            if (state.SettleAttempt == attempt && state.LastIssued == now)
            {
                continue;
            }
            state.SettleAttempt = attempt;
            if (state.EpisodeLive && state.LiveClaim is not null)
            {
                IssueWrite(state);
            }
            else if (state.SettleClaimAttempt != attempt)
            {
                state.SettleClaimAttempt = attempt;
                Evaluate(tab);
            }
            issued |= state.ClaimInFlight || state.WriteInFlight;
        }
        return issued;
    }

    private bool AllDirtyKept() =>
        _tabs()
            .Where(tab => !tab.IsDisposed && tab.IsDirty && tab.IsMarkdown)
            .All(tab => StateFor(tab) is { DiscardedUnkept: false, ClaimLost: false, LiveClaim: not null } state
                && state.LastKept == SnapshotOf(tab));

    /// <summary>A reason no further waiting can change: a lost claim or a
    /// discarded-unkept buffer.</summary>
    private string? HopelessReason() =>
        _tabs()
            .Where(tab => !tab.IsDisposed && tab.IsDirty && tab.IsMarkdown)
            .Select(StateFor)
            .Where(state => state.DiscardedUnkept || state.ClaimLost)
            .Select(state => state.NotKeptReason)
            .FirstOrDefault(reason => reason is not null);

    private string FirstReason(HotExitVaultKey key)
    {
        if (_host.HasUnresolvedDiscard(key))
        {
            return SlateUniffiMethods.HotExitReasonDiscardPending();
        }
        return _tabs()
                .Where(tab => !tab.IsDisposed && tab.IsDirty && tab.IsMarkdown)
                .Select(tab => StateFor(tab).NotKeptReason)
                .FirstOrDefault(reason => reason is not null)
            ?? SlateUniffiMethods.HotExitReasonTimedOut();
    }

    // ---- Discard All (§4, O6) ----------------------------------------------

    /// <summary>What a Discard All's wait found.</summary>
    internal enum DiscardWait
    {
        Committed,
        NotCommitted,
    }

    /// <summary>Records and retires the covered buffers, fences the writer,
    /// issues the discard (or joins the one in flight), and waits at most
    /// <paramref name="budget"/> WITHOUT pumping: §1 makes that deadlock-free,
    /// and 4 s stays under Windows' hung-window threshold.</summary>
    internal DiscardWait DiscardAll(TimeSpan budget)
    {
        // A damaged vault has no episodes: Discard All is its repair.
        if (_open.Key is not { } key)
        {
            return DiscardWait.Committed;
        }
        if (!_open.IsInert)
        {
            CoverDirtyTabs();
        }
        _fenced = true;
        _discardTask = _host.HasUnresolvedDiscard(key) && _discardTask is not null
            ? _discardTask
            : IssueDiscard(key);
        return WaitForDiscard(budget);
    }

    /// <summary>Try again: the same discard while it runs, a new one once
    /// none is in flight, under the same fence (§4).</summary>
    internal DiscardWait RetryDiscard(TimeSpan budget)
    {
        if (_open.Key is not { } key)
        {
            return DiscardWait.Committed;
        }
        _discardTask = _host.HasUnresolvedDiscard(key) && _discardTask is not null
            ? _discardTask
            : IssueDiscard(key);
        return WaitForDiscard(budget);
    }

    private Task<HotExitResult<ulong>>? _discardTask;

    private DiscardWait WaitForDiscard(TimeSpan budget)
    {
        if (_discardTask is null || !_discardTask.Wait(budget))
        {
            return DiscardWait.NotCommitted;
        }
        return _discardTask.Result.Succeeded ? DiscardWait.Committed : DiscardWait.NotCommitted;
    }

    private Task<HotExitResult<ulong>> IssueDiscard(HotExitVaultKey key)
    {
        Task<HotExitResult<ulong>> call = _host.DiscardAsync(key);
        PostResult(call, OnDiscardResolved);
        return call;
    }

    private void CoverDirtyTabs()
    {
        foreach (WorkspaceTabViewModel tab in _tabs())
        {
            if (tab.IsDisposed || !tab.IsDirty || !tab.IsMarkdown)
            {
                continue;
            }
            TabState state = StateFor(tab);
            state.CoveredIdentity = tab.ItemIdentity;
            state.CoveredRevision = tab.EditRevision;
            state.Covered = true;
            EndByReader(state);
        }
    }

    /// <summary>O6: when the last unresolved discard resolves, the fence lifts,
    /// a covered tab still at its covered revision is discarded-unkept, and any
    /// other dirty tab starts a fresh episode at the current epoch.</summary>
    private void OnDiscardResolved(HotExitResult<ulong> result)
    {
        if (result is { Succeeded: true } committed && committed.Value > _epoch)
        {
            _epoch = committed.Value;
        }
        if (_open.Key is not { } key || _host.HasUnresolvedDiscard(key))
        {
            return;
        }
        _fenced = false;
        // An uncommitted discard may still have advanced the visible epoch.
        PostResult(_host.EpochAsync(key), epoch =>
        {
            if (epoch is { Succeeded: true } read && read.Value > _epoch)
            {
                _epoch = read.Value;
            }
            ReevaluateAfterFence();
        });
    }

    private void ReevaluateAfterFence()
    {
        foreach (WorkspaceTabViewModel tab in _tabs().ToList())
        {
            if (tab.IsDisposed || !tab.IsMarkdown)
            {
                continue;
            }
            TabState state = StateFor(tab);
            if (state.Covered)
            {
                state.Covered = false;
                if (tab.IsDirty
                    && tab.ItemIdentity == state.CoveredIdentity
                    && tab.EditRevision == state.CoveredRevision)
                {
                    state.DiscardedUnkept = true;
                    state.NotKeptReason = SlateUniffiMethods.HotExitReasonDiscarded();
                }
            }
            Evaluate(tab);
        }
    }

    // ---- Session end and disposal ------------------------------------------

    /// <summary>§4: Windows is ending the session. Captures every dirty tab's
    /// latest snapshot on the dispatcher and hands its claim-then-write chain
    /// to the store host, off the dispatcher; waits at most
    /// <paramref name="budget"/> without pumping; opens nothing.</summary>
    internal void FlushForSessionEnd(TimeSpan budget)
    {
        if (ActiveKey is not { } key || _detached || _fenced)
        {
            return;
        }
        DrainPending();
        List<Task> chains = [];
        foreach (WorkspaceTabViewModel tab in _tabs())
        {
            if (tab.IsDisposed || !tab.IsDirty || !tab.IsMarkdown)
            {
                continue;
            }
            TabState state = StateFor(tab);
            state.CancelDebounce();
            if (state.DiscardedUnkept || state.ClaimLost)
            {
                continue;
            }
            var entry = new HotExitEntryWrite(tab.Path, tab.Text, tab.SavedContentHash, tab.FileIdentity);
            ulong generation = state.NextGeneration++;
            if (state.LiveClaim is { } claim)
            {
                chains.Add(_host.WriteAsync(key, claim, generation, entry));
            }
            else
            {
                string incarnation = state.ClaimIncarnation ?? SlateUniffiMethods.NewHotExitIncarnation();
                chains.Add(_host
                    .ClaimAsync(key, _epoch, tab.RecoveryEntryId, state.ExpectedClaim, incarnation)
                    .ContinueWith(
                        claimed => claimed.Result.Value is HotExitClaimOutcome.Claimed granted
                            ? _host.WriteAsync(key, granted.Claim, generation, entry)
                            : Task.CompletedTask,
                        TaskScheduler.Default)
                    .Unwrap());
            }
        }
        if (chains.Count > 0)
        {
            _ = Task.WaitAll([.. chains], budget);
        }
    }

    /// <summary>§4: disposal abandons. Timers stop, late outcomes publish
    /// nothing, nothing is retired, and nothing is waited for.</summary>
    internal void Detach()
    {
        _detached = true;
        _pending.Clear();
        foreach (TabState state in _states.Values)
        {
            state.CancelDebounce();
            state.Tab.HotExitChanged = null;
        }
    }

    private TabState StateFor(WorkspaceTabViewModel tab)
    {
        if (!_states.TryGetValue(tab, out TabState? state))
        {
            state = new TabState(tab);
            _states[tab] = state;
        }
        return state;
    }

    /// <summary>The tuple a kept entry must match (§0); the revision stands in
    /// for the text, which only changes with it.</summary>
    private sealed record Snapshot(
        string Path,
        string? BaselineHash,
        string? FileIdentity,
        int ItemIdentity,
        long Revision)
    {
        internal bool SameBaseline(Snapshot other) =>
            string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(BaselineHash, other.BaselineHash, StringComparison.Ordinal)
            && string.Equals(FileIdentity, other.FileIdentity, StringComparison.Ordinal)
            && ItemIdentity == other.ItemIdentity;
    }

    private sealed class TabState(WorkspaceTabViewModel tab)
    {
        internal WorkspaceTabViewModel Tab { get; } = tab;

        /// <summary>O3's token: bumped when an episode starts or ends.</summary>
        internal int Token { get; set; }

        internal bool EpisodeLive { get; set; }

        internal HotExitClaim? LiveClaim { get; set; }

        internal bool ClaimInFlight { get; set; }

        /// <summary>The incarnation of the claim being issued, kept across an
        /// error so its retry is idempotent (O2).</summary>
        internal string? ClaimIncarnation { get; set; }

        /// <summary>What the next claim expects to replace (O2).</summary>
        internal string? ExpectedClaim { get; set; }

        internal bool ClaimLost { get; set; }

        /// <summary>Every incarnation issued not answered Superseded or
        /// Refused: a claim that errored may have landed (O4).</summary>
        internal HashSet<string> HeldIncarnations { get; } = new(StringComparer.Ordinal);

        internal List<string> Inherited { get; } = [];

        internal ulong NextGeneration { get; set; } = 1;

        internal Snapshot? LastIssued { get; set; }

        internal Snapshot? LastKept { get; set; }

        internal bool WriteInFlight { get; set; }

        internal bool WriteDue { get; set; }

        internal string? NotKeptReason { get; set; }

        /// <summary>O6: a Discard All covered this tab and has not resolved.</summary>
        internal bool Covered { get; set; }

        internal bool DiscardedUnkept { get; set; }

        internal int CoveredIdentity { get; set; }

        internal long CoveredRevision { get; set; }

        internal int SettleAttempt { get; set; }

        internal int SettleClaimAttempt { get; set; }

        internal DispatcherTimer? Debounce { get; set; }

        internal void CancelDebounce() => Debounce?.Stop();
    }
}

internal sealed partial class WorkspaceViewModel
{
    private readonly HotExitStoreHost _hotExitHost;
    private readonly HotExitOpen _hotExitOpen;
    private HotExitWriter? _hotExit;

    /// <summary>Read by <c>RestoreNode</c> while the layout is built.</summary>
    private HotExitRestorePlan? _hotExitPlan;

    /// <summary>WB-29 §3: this workspace's writer, null for a workspace built
    /// with no way to reach a dispatcher.</summary>
    internal HotExitWriter? HotExit => _hotExit;

    /// <summary>§5: core's sentences for what the open restored, for the
    /// lifecycle to post once after <c>WorkspaceReady</c>.</summary>
    internal IReadOnlyList<A11yEvent> HotExitRestoreEvents { get; private set; } = [];

    /// <summary>§5: entries land after the tabs exist and before anything is
    /// published; each restored tab starts its episode here.</summary>
    private void InstallHotExit(HotExitRestorePlan plan)
    {
        _hotExit = new HotExitWriter(
            _hotExitHost,
            _hotExitOpen,
            System.Windows.Threading.Dispatcher.CurrentDispatcher,
            () => Groups.SelectMany(group => group.Tabs));
        if (_hotExitOpen.IsInert)
        {
            return;
        }
        var byId = Groups.SelectMany(group => group.Tabs).ToDictionary(tab => tab.Id);
        List<A11yEvent> notices = [];
        int restored = 0;
        foreach ((Guid id, HotExitRestorePlan.Planned planned) in plan.ByLayoutTab)
        {
            if (byId.TryGetValue(id, out WorkspaceTabViewModel? tab))
            {
                LandHotExitEntry(tab, planned, notices);
                restored++;
            }
        }
        foreach ((Guid id, string entryId, HotExitRestorePlan.Planned planned) in plan.NewTabs)
        {
            WorkspaceTabViewModel tab = AddTab(
                ActiveGroup,
                new WorkspaceItemState(WorkspaceItemKind.Markdown, planned.Path),
                activate: false,
                restoredId: id,
                preloadedTextForRestore: planned.Decision
                    is HotExitRestore.ExistsNow or HotExitRestore.Gone or HotExitRestore.Recovered
                    ? string.Empty
                    : null);
            tab.RecoveryEntryId = entryId;
            LandHotExitEntry(tab, planned, notices);
            restored++;
        }
        foreach (HotExitCurrentClaim orphan in plan.OrphanClaims)
        {
            WorkspaceTabViewModel? holder = Groups.SelectMany(group => group.Tabs)
                .FirstOrDefault(tab => string.Equals(tab.RecoveryEntryId, orphan.EntryId, StringComparison.Ordinal));
            if (holder is not null)
            {
                _hotExit.InheritOrphanClaim(holder, orphan.Incarnation);
            }
            else
            {
                _hotExit.RetireOrphanClaim(orphan.EntryId, orphan.Incarnation);
            }
        }
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs).ToList())
        {
            _hotExit.Attach(tab);
        }
        if (restored > 0)
        {
            HotExitRestoreEvents = [new A11yEvent.UnsavedNotesRestored((uint)restored), .. notices];
        }
    }

    private void LandHotExitEntry(
        WorkspaceTabViewModel tab,
        HotExitRestorePlan.Planned planned,
        List<A11yEvent> notices)
    {
        string name = System.IO.Path.GetFileName(planned.Path);
        HotExitEntry entry = planned.Entry;
        string text = planned.Text ?? entry.Text;
        switch (planned.Decision)
        {
            case HotExitRestore.Restore:
                tab.AdoptHotExitEntry(text, entry.BaselineHash, entry.BaselineFileId, externallyStale: false);
                break;
            case HotExitRestore.RestoreStale:
                tab.AdoptHotExitEntry(text, entry.BaselineHash, entry.BaselineFileId, externallyStale: true);
                notices.Add(new A11yEvent.RestoredNoteChangedOnDisk(name));
                break;
            case HotExitRestore.ExistsNow:
                tab.AdoptHotExitEntry(text, null, null, externallyStale: false);
                notices.Add(new A11yEvent.RestoredNoteExistsOnDisk(name));
                break;
            case HotExitRestore.Gone:
                tab.AdoptHotExitEntry(text, null, null, externallyStale: false);
                notices.Add(new A11yEvent.RestoredNoteMissingOnDisk(name));
                break;
            case HotExitRestore.Recovered:
                tab.AdoptHotExitEntry(text, null, null, externallyStale: false);
                notices.Add(new A11yEvent.RestoredNoteRecovered(name));
                break;
        }
        // §5: it adopts the observed claim and carries both the claim's and
        // the payload's incarnations until the reader ends it.
        _hotExit!.InstallRestored(
            tab,
            planned.ObservedClaim,
            planned.ObservedClaim is { } claim
                ? [claim, entry.Incarnation]
                : [entry.Incarnation]);
    }
}
