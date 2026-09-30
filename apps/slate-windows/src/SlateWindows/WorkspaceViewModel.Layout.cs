// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Owns workspace pane-tree state, tab placement, split mutation, pane focus
/// geometry, resize policy, focus announcements, and layout command refresh.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private readonly record struct PaneRect(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
    }

    private const int ClosedTabCapacity = 20;
    private readonly List<(WorkspaceItemState Item, Guid Group)> _closedTabs = [];
    private WorkspacePaneNodeViewModel _root;
    private WorkspaceGroupViewModel _activeGroup;

    public event EventHandler<WorkspaceFocusBoundary>? FocusBoundaryRequested;
    public event EventHandler<WorkspaceGroupViewModel>? EditorPaneFocusRequested;

    public WorkspacePaneNodeViewModel Root
    {
        get => _root;
        private set => SetField(ref _root, value);
    }

    public WorkspaceGroupViewModel ActiveGroup
    {
        get => _activeGroup;
        private set
        {
            if (ReferenceEquals(_activeGroup, value))
            {
                return;
            }

            _activeGroup?.ActiveTab?.Deactivate(clearBaseQuickFilter: false);
            _activeGroup = value;
            OnPropertyChanged();
            RaiseCommandStates();
            SyncPanels();
        }
    }

    public IReadOnlyList<WorkspaceGroupViewModel> Groups => EnumerateGroups(Root).ToArray();
    public bool HasDirtyTabs => Groups.SelectMany(group => group.Tabs).Any(tab => tab.IsDirty);

    public void SelectGroupFromKeyboardFocus(WorkspaceGroupViewModel group)
    {
        if (!Groups.Contains(group) || ReferenceEquals(ActiveGroup, group))
        {
            return;
        }

        ActiveGroup = group;
        AnnounceActivePane();
        Persist();
    }

    internal void Activate(WorkspaceGroupViewModel group, WorkspaceTabViewModel? tab)
    {
        if (_restoring)
        {
            return;
        }

        ActiveGroup = group;
        // W7-7 (R-2, codex PR 2 round 2): a Files selection's activation is
        // quiet — focus stayed on the row, so no tab took it, and the row's
        // own selection line is what the reader hears.
        if (tab is not null && !_selectionOpenInProgress)
        {
            int index = group.Tabs.IndexOf(tab) + 1;
            _announce(new A11yEvent.TabFocused(
                Prefix: string.Empty,
                Filename: group.SpokenNameOf(tab),
                Index: (uint)Math.Max(1, index),
                Count: (uint)group.Tabs.Count));
        }

        RaiseCommandStates();
        Persist();
        // EVERY tab activation re-derives the panels' note — the
        // ActiveGroup setter's sync alone misses same-pane tab
        // switches, whose group identity is unchanged (adversarial
        // round 1: the panels stayed bound to the previous note).
        SyncPanels();
    }

    private void OpenItem(WorkspaceItemState item, WorkspaceOpenTarget target)
    {
        if (TryOpenItem(item, target))
        {
            Persist();
        }
    }

    /// <summary>True while a Files selection's open runs (W7-7 R-2): the
    /// activations it causes post no <c>TabFocused</c>.</summary>
    private bool _selectionOpenInProgress;

    private bool TryOpenItem(
        WorkspaceItemState item,
        WorkspaceOpenTarget target,
        bool requestEditorFocus = true,
        bool fromSelection = false)
    {
        if (!fromSelection)
        {
            return TryOpenItemCore(item, target, requestEditorFocus);
        }

        bool outer = _selectionOpenInProgress;
        _selectionOpenInProgress = true;
        try
        {
            return ShowSelectionInTransientTab(item);
        }
        finally
        {
            _selectionOpenInProgress = outer;
        }
    }

    /// <summary>W7-7 (R-2, spec §3.2 item 2; codex PR 2 rounds 6–7): the tab
    /// of <paramref name="group"/> already showing <paramref name="item"/>
    /// — a PERMANENT one when there is one, the group's transient tab only
    /// as the fallback. Every open that reuses a tab (a Files selection,
    /// Enter's current-tab open, Ctrl+Enter's new-tab open) resolves it
    /// here, so a transient twin of a permanent tab (the tab a Duplicate
    /// Tab was made from, say) is never activated, promoted or replaced in
    /// the permanent tab's place.</summary>
    private static WorkspaceTabViewModel? FindOpenTab(WorkspaceGroupViewModel group, WorkspaceItemState item) =>
        group.Tabs.FirstOrDefault(tab => !tab.IsTransient && ItemsReferToSameTarget(tab.Item, item))
        ?? group.Tabs.FirstOrDefault(tab => ItemsReferToSameTarget(tab.Item, item));

    /// <summary>W7-7 (R-2, codex PR 2 round 4; spec review round 21): a
    /// Files selection shows its note in the group's ONE transient tab —
    /// VS Code's preview tab. A note already open in the group is simply
    /// activated (<see cref="FindOpenTab"/>: a permanent tab showing it
    /// first; the transient one stays as it is either way). Otherwise the
    /// transient tab takes the note in place, or, with none, a transient
    /// tab is created. A tab stops being transient for good on its first
    /// dirty transition (see <see cref="WorkspaceTabViewModel.IsTransient"/>),
    /// so an edited note — saved or undone since, or not — is never
    /// replaced, and no other tab is ever replaced by a selection: an
    /// explicitly opened tab survives arrowing. Focus stays on the row and
    /// the modal dirty-navigation gate never rises.</summary>
    private bool ShowSelectionInTransientTab(WorkspaceItemState item)
    {
        WorkspaceGroupViewModel group = ActiveGroup;
        WorkspaceTabViewModel? open = FindOpenTab(group, item);
        if (open is not null)
        {
            group.ActiveTab = open;
            return true;
        }

        WorkspaceTabViewModel? transient = group.Tabs.FirstOrDefault(tab => tab.IsTransient);
        if (transient is not null)
        {
            ReplaceTabItem(transient, item);
            group.ActiveTab = transient;
            RaiseCommandStates();
            return true;
        }

        WorkspaceTabViewModel created = AddTab(group, item, activate: true);
        // A tab that opens onto another pane's unsaved document is born
        // dirty, and a dirty tab is never transient.
        created.IsTransient = !created.IsDirty;
        return true;
    }

    private bool TryOpenItemCore(
        WorkspaceItemState item,
        WorkspaceOpenTarget target,
        bool requestEditorFocus)
    {
        if (item.Kind == WorkspaceItemKind.Graph && TryFocusGlobalGraph())
        {
            return true;
        }

        if (target is WorkspaceOpenTarget.SplitRight or WorkspaceOpenTarget.SplitDown)
        {
            return AddSplitWithItem(
                item,
                target == WorkspaceOpenTarget.SplitRight ? "horizontal" : "vertical");
        }

        if (target is WorkspaceOpenTarget.CurrentTab or WorkspaceOpenTarget.NewTab)
        {
            // A permanent tab showing the note first (codex PR 2 round 7):
            // with a transient twin beside it, the twin is left as it is.
            WorkspaceTabViewModel? existing = FindOpenTab(ActiveGroup, item);
            if (existing is not null)
            {
                if (target == WorkspaceOpenTarget.NewTab)
                {
                    // W7-7 (R-2, codex PR 2 round 4): "open in a new tab" gives
                    // the note a tab of its own that later selections never
                    // replace — the transient tab showing it becomes that tab
                    // when it is the only one; a permanent tab showing it is
                    // simply activated, a no-op here (DuplicateActiveTab is
                    // the deliberate route to a second tab of one note).
                    existing.IsTransient = false;
                }

                ActiveGroup.ActiveTab = existing;
                if (requestEditorFocus)
                {
                    RequestActiveEditorFocus();
                }
                return true;
            }

            if (target == WorkspaceOpenTarget.NewTab)
            {
                AddTab(ActiveGroup, item, activate: true);
                if (requestEditorFocus)
                {
                    RequestActiveEditorFocus();
                }
                return true;
            }
        }

        WorkspaceGroupViewModel group = ActiveGroup;
        WorkspaceTabViewModel? active = group.ActiveTab;
        if (active is null)
        {
            AddTab(ActiveGroup, item, activate: true);
        }
        // The plain comparison is right here: this is the in-place replace
        // of the ACTIVE tab, reached only once FindOpenTab found no tab of
        // the group showing the item, so transient-ness has no say in it.
        else if (!ItemsReferToSameTarget(active.Item, item))
        {
            // The gate is a MODAL dialog that pumps the dispatcher (W6-2
            // PR B2, IGL-6), and its Save pumps with the window ENABLED
            // (#1280): a group switch, a tab close, another navigation, an
            // edit or a teardown can land inside either frame, so the open
            // re-validates its address after every frame, before any
            // replacement. What the tab SHOWS is not part of the address:
            // an answer applies only to the document and edit it was asked
            // about (AdmitDirtyTab's pin), so a tab re-pointed inside a frame
            // is admitted afresh — asked again when dirty, taken when clean —
            // and the open then installs its own item over it (contract 35
            // B2-D10: a re-entrant open is an ordinary one; IGL-2: a rename
            // inside the dialog does not stop the open).
            if (!AdmitDirtyTab(
                    active,
                    () => _dirtyNavigationDecision(active, item),
                    () => !_workspaceDisposed
                        && !active.IsDisposed
                        && ReferenceEquals(ActiveGroup, group)
                        && Groups.Contains(group)
                        && group.Tabs.Contains(active)))
            {
                return false;
            }

            ReplaceTabItem(active, item);
            // An explicit open into the current tab makes it the note's own
            // tab: a transient tab it lands in is kept from here (W7-7 R-2).
            active.IsTransient = false;
            ActiveGroup.ActiveTab = active;
            RaiseCommandStates();
        }

        // Withheld for the re-root's and Back's opens (W6-2 PR B2, IGL-3):
        // the editor's request is queued at Input priority and would
        // otherwise land after the leaf's own boundary request.
        if (requestEditorFocus)
        {
            RequestActiveEditorFocus();
        }
        return true;
    }

    /// <summary>Put <paramref name="item"/> into <paramref name="tab"/> in
    /// place — the current tab's explicit replace and the transient tab's
    /// selection share it. The replace is a tab MUTATION site, not a
    /// construction site — it needs the same attach funnel as
    /// AddTab/restore/duplicate or a .base opened into the tab ships a dead
    /// pane (red team round 1 blocker). Attach before the release sweep so
    /// a shared document is never shut down between the two steps.
    /// <paramref name="load"/> false (W7-7 PR 7, codex PR 7 round 5 fix 3:
    /// the rescan's re-seat alone) constructs a missing board or base
    /// without loading it — the caller's own, awaited load is its one
    /// load. <paramref name="canvasSeed"/> (the re-seat's too) seeds a
    /// board constructed here with the retired board's selection, marks
    /// and projection, as a rename's retarget does (CD-32), and
    /// <paramref name="baseSeed"/> a base constructed here with the retired
    /// document's reader row (codex's merge-delta check, finding 2).</summary>
    private void ReplaceTabItem(
        WorkspaceTabViewModel tab,
        WorkspaceItemState item,
        bool load = true,
        SlateWindows.Canvas.CanvasSelection? canvasSeed = null,
        BasesRow? baseSeed = null)
    {
        WorkspaceTabViewModel? peer = FindSamePathTab(item, excluding: tab);
        tab.ReplaceItem(item);
        AttachTabDocumentsIfNeeded(tab, load, canvasSeed, baseSeed);
        ReleaseUnreferencedBaseDocuments();
        ReleaseUnreferencedDashboards();
        ReleaseUnreferencedCanvasDocuments();
        ReleaseGraphDocumentIfUnreferenced();
        if (peer is not null)
        {
            tab.MirrorDocumentStateFrom(peer);
        }
    }

    private bool TryFocusGlobalGraph()
    {
        WorkspaceTabViewModel? graph = Groups.SelectMany(group => group.Tabs)
            .FirstOrDefault(tab => tab.Item.Kind == WorkspaceItemKind.Graph);
        if (graph is null)
        {
            return false;
        }

        WorkspaceGroupViewModel owner = Groups.First(group => group.Tabs.Contains(graph));
        ActiveGroup = owner;
        owner.ActiveTab = graph;
        RequestActiveEditorFocus();
        return true;
    }

    private WorkspaceTabViewModel AddTab(
        WorkspaceGroupViewModel group,
        WorkspaceItemState item,
        bool activate)
    {
        WorkspaceTabViewModel? peer = FindSamePathTab(item);
        var tab = new WorkspaceTabViewModel(
            _session,
            new WorkspaceTabState(Guid.NewGuid(), item),
            MirrorSamePathDocumentState,
            OpenEditorNavigation,
            ActivateEditorTag,
            ActivateReadingTag,
            _announce,
            EditorPreferences,
            startInteractionBackgroundWork: _startInteractionBackgroundWork,
            interactionBackgroundFaultForTests: InteractionBackgroundFaultForTests);
        tab.TaskRepairs = _taskIndexRepairs;
        tab.SaveCoordinator = _saves;
        AttachTabDocumentsIfNeeded(tab);
        if (peer is not null)
        {
            tab.MirrorDocumentStateFrom(peer);
        }

        group.Tabs.Add(tab);
        if (activate)
        {
            group.ActiveTab = tab;
        }

        RaiseCommandStates();
        return tab;
    }

    private bool AddSplitWithItem(WorkspaceItemState item, string axis)
    {
        if (item.Kind == WorkspaceItemKind.Graph)
        {
            _announce(new A11yEvent.GraphOpensSinglePane());
            return false;
        }

        if (Groups.Count >= WorkspacePersistence.MaxGroups)
        {
            // W0.5-3 residue: Windows workspace-cap availability copy.
            _announce(new A11yEvent.HostComposed(
                "Pane limit reached. Slate supports up to six editor panes.",
                A11yPriority.Medium));
            return false;
        }

        var newGroup = new WorkspaceGroupViewModel(this, Guid.NewGuid());
        var newNode = new WorkspacePaneNodeViewModel(newGroup) { Weight = 0.5 };
        WorkspaceTabViewModel newTab = AddTab(newGroup, item, activate: false);

        WorkspacePaneNodeViewModel activeNode = FindNode(Root, ActiveGroup)
            ?? throw new InvalidOperationException("Active group is not in the workspace tree.");
        if (TryFindParent(Root, activeNode, out WorkspacePaneNodeViewModel? parent)
            && parent!.Axis == axis)
        {
            int index = parent.Children.IndexOf(activeNode);
            parent.Children.Insert(index + 1, newNode);
            NormalizeWeights(parent);
        }
        else
        {
            var split = new WorkspacePaneNodeViewModel(axis) { Weight = activeNode.Weight };
            activeNode.Weight = 0.5;
            split.Children.Add(activeNode);
            split.Children.Add(newNode);
            if (parent is null)
            {
                Root = split;
            }
            else
            {
                int index = parent.Children.IndexOf(activeNode);
                parent.Children[index] = split;
            }
        }

        newGroup.ActiveTab = newTab;
        ActiveGroup = newGroup;
        OnPropertyChanged(nameof(Groups));
        RaiseCommandStates();
        RequestActiveEditorFocus();
        return true;
    }

    private void SplitActive(string axis)
    {
        if (ActiveGroup.ActiveTab is WorkspaceTabViewModel tab)
        {
            if (AddSplitWithItem(tab.Item, axis))
            {
                Persist();
            }
        }
    }

    private bool CanSplitActive() =>
        Groups.Count < WorkspacePersistence.MaxGroups
        && ActiveGroup.ActiveTab is { Item.Kind: not WorkspaceItemKind.Graph };

    private void CloseTab(object? parameter)
    {
        if (parameter is not WorkspaceTabViewModel tab)
        {
            return;
        }

        // Admission may pump — the modal prompt, and a save-before-close
        // with the window enabled (#1280) — so the group is re-resolved by
        // membership after it and nothing captured before it is used: a
        // tab closed, moved or re-pointed meanwhile is never indexed from a
        // stale group, and one closed meanwhile is not closed twice.
        if (GroupOf(tab) is null
            || !AdmitDirtyTab(
                tab,
                () => _dirtyCloseDecision(tab),
                () => !_workspaceDisposed && !tab.IsDisposed && GroupOf(tab) is not null)
            || GroupOf(tab) is not WorkspaceGroupViewModel group)
        {
            return;
        }

        int index = group.Tabs.IndexOf(tab);
        // The closed tab is named among the tabs it leaves, BEFORE it leaves
        // them (codex PR 3 round 8); its successor, below, after.
        string closedName = group.SpokenNameOf(tab);
        _closedTabs.Add((tab.Item, group.Id));
        if (_closedTabs.Count > ClosedTabCapacity)
        {
            _closedTabs.RemoveAt(0);
        }

        group.Tabs.Remove(tab);
        tab.Dispose();
        ReleaseUnreferencedBaseDocuments();
        ReleaseUnreferencedDashboards();
        ReleaseUnreferencedCanvasDocuments();
        ReleaseGraphDocumentIfUnreferenced();
        WorkspaceTabViewModel? successor = group.Tabs.Count == 0
            ? null
            : group.Tabs[Math.Min(index, group.Tabs.Count - 1)];
        group.ActiveTab = successor;
        _announce(new A11yEvent.TabClosed(
            closedName, successor is null ? null : group.SpokenNameOf(successor)));
        if (group.Tabs.Count == 0 && Groups.Count > 1)
        {
            RemoveEmptyGroup(group);
        }

        RaiseCommandStates();
        Persist();
    }

    private void RemoveEmptyGroup(WorkspaceGroupViewModel group)
    {
        WorkspacePaneNodeViewModel? node = FindNode(Root, group);
        if (node is null || !TryFindParent(Root, node, out WorkspacePaneNodeViewModel? parent))
        {
            return;
        }

        parent!.Children.Remove(node);
        if (parent.Children.Count > 1)
        {
            NormalizeWeightRatios(parent);
        }
        else
        {
            WorkspacePaneNodeViewModel replacement = parent.Children[0];
            replacement.Weight = parent.Weight;
            if (TryFindParent(Root, parent, out WorkspacePaneNodeViewModel? grandparent))
            {
                int index = grandparent!.Children.IndexOf(parent);
                grandparent.Children[index] = replacement;
            }
            else
            {
                replacement.Weight = 1;
                Root = replacement;
            }
        }

        ActiveGroup = EnumerateGroups(Root).First();
        OnPropertyChanged(nameof(Groups));
    }

    private void CloseActivePane()
    {
        if (Groups.Count <= 1)
        {
            return;
        }

        WorkspaceGroupViewModel group = ActiveGroup;
        // #1280: each admission may pump — a tab opened into this pane, a
        // tab closed or moved, an edit, a teardown can land inside it — so
        // admission works in rounds over the pane's CURRENT tabs, and the
        // pane closes only after a round in which nothing pumped: every tab
        // it disposes has no save pending, and is clean or approved for
        // discard at exactly the document and edit it was asked about.
        var discarded = new Dictionary<WorkspaceTabViewModel, (int Identity, long Revision)>(
            ReferenceEqualityComparer.Instance);
        for (int round = 0; ; round++)
        {
            if (_workspaceDisposed || Groups.Count <= 1 || !Groups.Contains(group)
                || round >= MaxPumpedAdmissionRounds)
            {
                return;
            }
            // Codex round 2a: the pane's admitted saves settle before any of
            // its tabs is asked about or approved — a Ctrl+S still writing
            // never lands after the user's Discard. All of them in one round,
            // so a pane of saving tabs never runs out of rounds; the settle
            // pumped, so the next round reads everything again.
            if (group.Tabs.Any(tab => tab.HasPendingSaves))
            {
                foreach (WorkspaceTabViewModel tab in group.Tabs.ToArray())
                {
                    if (tab.HasPendingSaves && !tab.SettleSaves())
                    {
                        return;
                    }
                }
                continue;
            }
            TabSetStamp stamp = CaptureTabSet();
            bool pumped = false;
            foreach (WorkspaceTabViewModel tab in group.Tabs.ToArray())
            {
                if (!tab.IsDirty)
                {
                    // Codex round 4: a clean tab whose settled save faulted
                    // is not saved; the pane stays open.
                    if (tab.LastSaveFaulted)
                    {
                        return;
                    }
                    continue;
                }
                if (discarded.TryGetValue(tab, out (int Identity, long Revision) approved)
                    && approved == (tab.ItemIdentity, tab.EditRevision))
                {
                    continue;
                }
                pumped = true;
                // What the prompt asks about, read BEFORE it opens.
                (int Identity, long Revision) asked = (tab.ItemIdentity, tab.EditRevision);
                switch (_dirtyCloseDecision(tab))
                {
                    case WorkspaceDirtyNavigationDecision.Discard:
                        // Approved only for exactly what was asked about, with
                        // nothing pending; an edit or a save that landed while
                        // the prompt was up is asked about again.
                        if (!tab.HasPendingSaves && asked == (tab.ItemIdentity, tab.EditRevision))
                        {
                            discarded[tab] = asked;
                        }
                        break;
                    case WorkspaceDirtyNavigationDecision.Save:
                        // A save retired by a rename under it left the tab at
                        // a new path: the next round asks about it again.
                        WorkspaceItemState saving = tab.Item;
                        if (!tab.Save() && tab.Item == saving)
                        {
                            return;
                        }
                        break;
                    default:
                        return;
                }
                if (!stamp.StillHolds(this))
                {
                    break;
                }
            }
            if (!pumped)
            {
                break;
            }
        }

        foreach (WorkspaceTabViewModel tab in group.Tabs)
        {
            _closedTabs.Add((tab.Item, group.Id));
        }

        if (_closedTabs.Count > ClosedTabCapacity)
        {
            _closedTabs.RemoveRange(0, _closedTabs.Count - ClosedTabCapacity);
        }

        foreach (WorkspaceTabViewModel tab in group.Tabs)
        {
            tab.Dispose();
        }

        group.Tabs.Clear();
        ReleaseUnreferencedBaseDocuments();
        ReleaseUnreferencedDashboards();
        ReleaseUnreferencedCanvasDocuments();
        // W6-2 PR A (contract A-1; IPA-2): the pane close is the third
        // tab-set boundary — the last graph tab can leave with its pane.
        ReleaseGraphDocumentIfUnreferenced();
        RemoveEmptyGroup(group);
        AnnounceActivePane();
        RequestActiveEditorFocus();
        RaiseCommandStates();
        Persist();
    }

    /// <summary>#1280: the bound on admission rounds — a tab typed into
    /// during every one of its saves is refused rather than looped on.</summary>
    internal const int MaxPumpedAdmissionRounds = 8;

    private WorkspaceGroupViewModel? GroupOf(WorkspaceTabViewModel tab) =>
        Groups.FirstOrDefault(candidate => candidate.Tabs.Contains(tab));

    /// <summary>
    /// Admit replacing or closing a dirty tab (#1280, the pumped-wait
    /// invariant; codex rounds 1 and 2a). Every step that can pump — settling
    /// the tab's own saves, the modal prompt, a Save — is followed by a
    /// re-check of <paramref name="stillValid"/>, and admission restarts
    /// whenever what it read changed:
    /// <list type="bullet">
    /// <item>The tab's admitted saves settle BEFORE it is asked about, and
    /// again before Discard is accepted: a Ctrl+S still writing never lands
    /// after the user chose Discard, and a tab the settle made clean is not
    /// asked about at all.</item>
    /// <item>What the prompt asks about — the tab's document
    /// (<see cref="WorkspaceTabViewModel.ItemIdentity"/>, which a rename of
    /// its file keeps) and edit revision — is read BEFORE the prompt opens,
    /// and Discard is accepted only when that exact pair still holds
    /// afterwards: an edit that lands while the prompt is up is asked about
    /// again, never discarded unasked.</item>
    /// </list>
    /// True when the tab is clean, or its Discard was accepted, and it is
    /// still valid; false on Cancel, a failed save, an invalidated caller or
    /// too many rounds. A save that failed because the tab's file was
    /// renamed under it (the write was retired; the tab now shows the new
    /// path) is not a refusal: the tab is asked about again at its new
    /// path.
    /// </summary>
    private bool AdmitDirtyTab(
        WorkspaceTabViewModel tab,
        Func<WorkspaceDirtyNavigationDecision> ask,
        Func<bool> stillValid)
    {
        for (int round = 0; round < MaxPumpedAdmissionRounds; round++)
        {
            if (!stillValid())
            {
                return false;
            }
            if (tab.HasPendingSaves)
            {
                if (!tab.SettleSaves())
                {
                    return false;
                }
                continue;
            }
            if (!tab.IsDirty)
            {
                // Codex round 4: clean, but the save the admission settled —
                // or an earlier one of this item — faulted, a step after its
                // write was adopted included: not saved (D-10). Fail closed;
                // a later successful save of the item clears it.
                return !tab.LastSaveFaulted;
            }
            (int Identity, long Revision) asked = (tab.ItemIdentity, tab.EditRevision);
            WorkspaceDirtyNavigationDecision decision = ask();
            if (!stillValid())
            {
                return false;
            }
            switch (decision)
            {
                case WorkspaceDirtyNavigationDecision.Discard:
                    if (!tab.HasPendingSaves && asked == (tab.ItemIdentity, tab.EditRevision))
                    {
                        return true;
                    }
                    // A save admitted, or an edit made, while the prompt was
                    // up: settle it and ask again.
                    break;
                case WorkspaceDirtyNavigationDecision.Save:
                    WorkspaceItemState saving = tab.Item;
                    if (!tab.Save() && tab.Item == saving)
                    {
                        return false;
                    }
                    break;
                default:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// #1280: the tab set and every tab's identity at one instant — the
    /// ordered (group, tab, item) triples, empty groups included. A pumped
    /// caller compares it after its frame: structural, so no mutation site
    /// has to remember to bump a counter.
    /// </summary>
    private sealed class TabSetStamp
    {
        private readonly Entry[] _entries;

        internal TabSetStamp(WorkspaceViewModel workspace)
        {
            _entries = Capture(workspace);
            Tabs = [.. _entries.Where(entry => entry.Tab is not null).Select(entry => entry.Tab!)];
        }

        internal WorkspaceTabViewModel[] Tabs { get; }

        internal bool StillHolds(WorkspaceViewModel workspace)
        {
            if (workspace._workspaceDisposed)
            {
                return false;
            }
            Entry[] now = Capture(workspace);
            if (now.Length != _entries.Length)
            {
                return false;
            }
            for (int index = 0; index < now.Length; index++)
            {
                if (!ReferenceEquals(now[index].Group, _entries[index].Group)
                    || !ReferenceEquals(now[index].Tab, _entries[index].Tab)
                    || now[index].Item != _entries[index].Item)
                {
                    return false;
                }
            }
            return true;
        }

        private static Entry[] Capture(WorkspaceViewModel workspace) =>
            [.. workspace.Groups.SelectMany(group => group.Tabs.Count == 0
                ? [new Entry(group, null, null)]
                : group.Tabs.Select(tab => new Entry(group, tab, tab.Item)))];

        private readonly record struct Entry(
            WorkspaceGroupViewModel Group,
            WorkspaceTabViewModel? Tab,
            WorkspaceItemState? Item);
    }

    private TabSetStamp CaptureTabSet() => new(this);

    private void DuplicateActiveTab()
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel tab
            || tab.Item.Kind == WorkspaceItemKind.Graph)
        {
            return;
        }

        int index = ActiveGroup.Tabs.IndexOf(tab);
        WorkspaceTabViewModel duplicate = new(
            _session,
            new WorkspaceTabState(Guid.NewGuid(), tab.Item),
            MirrorSamePathDocumentState,
            OpenEditorNavigation,
            ActivateEditorTag,
            ActivateReadingTag,
            _announce,
            EditorPreferences,
            startInteractionBackgroundWork: _startInteractionBackgroundWork);
        // #1280: the duplicate's saves serialize with its source's.
        duplicate.SaveCoordinator = _saves;
        // The registry, not a fresh document: a duplicated tab shares
        // its source's ONE document (contract C3).
        AttachTabDocumentsIfNeeded(duplicate);
        duplicate.MirrorDocumentStateFrom(tab);
        ActiveGroup.Tabs.Insert(index + 1, duplicate);
        ActiveGroup.ActiveTab = duplicate;
        RequestActiveEditorFocus();
        Persist();
    }

    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            return;
        }

        (WorkspaceItemState item, Guid groupId) = _closedTabs[^1];
        _closedTabs.RemoveAt(_closedTabs.Count - 1);
        if (item.Kind == WorkspaceItemKind.Graph)
        {
            // W6-2 PR A (rule L, Term 6): a reopened graph speaks Opened,
            // then the shell's line below, then the summary when a load runs.
            SetGraphCause(GraphActivationCause.Reopen);
        }
        if (item.Kind == WorkspaceItemKind.Graph && TryFocusGlobalGraph())
        {
            // Rule L, Term 6 (IPA-3): a reopen of a tab that still exists
            // is an Open of that tab with the reopen line AFTER it. The two
            // assignments above reach the funnel only when a reference
            // changes; when the graph was already effective they change
            // nothing, so the follow method is called here, before the
            // shell's line — `Opened`, `ReopenedGraph`, and no load for an
            // effective READY tab.
            SyncPanels();
            _announce(new A11yEvent.ReopenedGraph());
            RaiseCommandStates();
            Persist();
            return;
        }

        WorkspaceGroupViewModel group = Groups.FirstOrDefault(candidate => candidate.Id == groupId)
            ?? ActiveGroup;
        WorkspaceTabViewModel tab = AddTab(group, item, activate: true);
        ActiveGroup = group;
        A11yEvent reopened = item.Kind switch
        {
            WorkspaceItemKind.Graph => new A11yEvent.ReopenedGraph(),
            WorkspaceItemKind.SavedQuery or WorkspaceItemKind.Dashboard =>
                new A11yEvent.ReopenedNamed(group.SpokenNameOf(tab)),
            _ => ReopenFileAnnouncement(group, tab),
        };
        // A missing target keeps the tab and its recovery UI, without
        // claiming success (D-11): the tab's state changes here, not
        // inside the function that composes the announcement.
        if (reopened is A11yEvent.ReopenTargetMissing)
        {
            tab.InvalidatePath();
        }
        _announce(reopened);
        RaiseCommandStates();
        Persist();
    }

    private A11yEvent ReopenFileAnnouncement(WorkspaceGroupViewModel group, WorkspaceTabViewModel tab)
    {
        // The reopened tab as its group's strip names it (codex PR 3 round
        // 8, OD-9): the file that failed or went missing is that tab's.
        string filename = group.SpokenNameOf(tab);
        try
        {
            // The index may still contain a deleted file. Ask the live core
            // provider (D-11) rather than trusting index metadata.
            if (_session.CanonicalPath(tab.Path) is null)
            {
                return new A11yEvent.ReopenTargetMissing(filename);
            }
        }
        catch (VaultException failure)
        {
            return new A11yEvent.FileReopenFailed(filename, failure.Message);
        }
        return tab.LoadFailure is { } detail
            ? new A11yEvent.FileReopenFailed(filename, detail)
            : new A11yEvent.ReopenedFile(filename);
    }

    private void MoveActiveTab(int delta)
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel tab)
        {
            return;
        }

        int oldIndex = ActiveGroup.Tabs.IndexOf(tab);
        int newIndex = oldIndex + delta;
        if (newIndex < 0 || newIndex >= ActiveGroup.Tabs.Count)
        {
            return;
        }

        ActiveGroup.Tabs.Move(oldIndex, newIndex);
        ActiveGroup.ActiveTab = tab;
        Persist();
    }

    private bool CanMoveActiveTab(int delta)
    {
        int index = ActiveGroup.ActiveTab is null ? -1 : ActiveGroup.Tabs.IndexOf(ActiveGroup.ActiveTab);
        return index >= 0 && index + delta >= 0 && index + delta < ActiveGroup.Tabs.Count;
    }

    private void CycleTab(int delta)
    {
        if (ActiveGroup.Tabs.Count == 0)
        {
            return;
        }

        int index = ActiveGroup.ActiveTab is null ? 0 : ActiveGroup.Tabs.IndexOf(ActiveGroup.ActiveTab);
        ActiveGroup.ActiveTab = ActiveGroup.Tabs[(index + delta + ActiveGroup.Tabs.Count) % ActiveGroup.Tabs.Count];
        // W7-7 PR 8 (R-10): a user's tab switch lands the new tab's editor
        // stop. Cycling into a reading tab used to rely on the reading
        // surface focusing itself when shown; the surface now takes focus
        // only through the landing this funnel asks for, and without the
        // request WPF's recovery off the collapsed editor seats the tab
        // control — record F10's "Workspace tabs, tab control".
        RequestActiveEditorFocus();
    }

    /// <summary>One F6 (<paramref name="direction"/> +1) or Shift+F6 (-1)
    /// press: spec §1 ring, §3 announcements, §4 no-op and fall-through.</summary>
    private void CycleShellRegion(int direction)
    {
        if (ShellRegionHost is not { } host || host.ModalSurfaceOpen)
        {
            return;
        }

        // A newer press CANCELS a held one (R-10): withdrawn — no line, no
        // fall-through, and the host lets go of it — so a late completion can
        // never finish a second traversal. The ring stands on the held region:
        // while the host still holds a press's landing, with the reader exactly
        // where the held press left them, the new press goes on from THAT ring
        // position in its own direction (F6 → the region after the editor,
        // Shift+F6 → the tab bar) and never asks it again. A landing the host
        // already let go of — it completed, the view changed, the reader moved,
        // even within one region — holds no position: the press starts from
        // where focus is. OD-12: whether a landing is held, and the position it
        // holds, are the host's one answer (the window's slot); the ring keeps
        // only the identity of its latest press.
        ShellRegionKind? from = host.FocusedRegion();
        _ringPress = null;
        if (host.HeldRingRegion is { } heldRegion && host.WithdrawHeldLanding())
        {
            from = heldRegion;
        }

        CycleShellRegionFrom(host, from, direction, attemptsTaken: 0);
    }

    /// <summary>W7-7 PR 8 (R-10): the ring's latest press — the only one whose
    /// completions act. Not "is a landing held" (OD-12: the host's slot alone
    /// answers that): a continuation of a press this one superseded, or one the
    /// funnel withdrew, is a no-op even from a host that broke its
    /// never-after-withdrawal contract.</summary>
    private object? _ringPress;

    /// <summary>W7-7 PR 8 (R-10, OD-12): the window holds a landing the F6 ring
    /// asked for — the host's slot answers.</summary>
    internal bool HoldsShellRegionLanding => ShellRegionHost?.HeldRingRegion is not null;

    /// <summary>W7-7 PR 8 (R-10): let go of the editor landing the window holds
    /// — silently, and synchronously, before anything else moves: another
    /// route is putting the reader somewhere (the editor-focus funnel behind
    /// every open, tab switch and pane move), so a late completion must neither
    /// seat focus nor speak. OD-12: the window holds one, whoever asked for it.</summary>
    internal void WithdrawHeldShellRegionLanding()
    {
        _ringPress = null;
        if (ShellRegionHost is { HoldsLanding: true } host)
        {
            _ = host.WithdrawHeldLanding();
        }
    }

    /// <summary>The press's traversal from <paramref name="current"/>, at most
    /// one full ring. W7-7 PR 8 (R-10): a held landing that is refused later
    /// resumes this same traversal from its own position, reading the ring
    /// from live state again — the next region receives focus once, and the
    /// held region is never spoken.</summary>
    private void CycleShellRegionFrom(
        IShellRegionHost host, ShellRegionKind? current, int direction, int attemptsTaken)
    {
        if (host.ModalSurfaceOpen)
        {
            return;
        }

        var layout = new ShellRegionLayout(
            HasTabs: ActiveGroup.Tabs.Count > 0,
            RightPaneVisible: IsRightPaneVisible,
            RightPaneHasContentStop: host.RightPaneHasContentStop);
        int attempts = ShellRegionRing.Ring(layout).Count;
        for (int attempt = attemptsTaken; attempt < attempts; attempt++)
        {
            ShellRegionKind target = ShellRegionRing.Next(layout, current, direction);
            int taken = attempt + 1;
            object press = new();
            _ringPress = press;
            ShellRegionLanding landing = host.TryLand(
                target,
                () =>
                {
                    if (!ReferenceEquals(_ringPress, press))
                    {
                        return;
                    }

                    _ringPress = null;
                    // W7-6 §4's modal rule: a modal surface owns the keys, so
                    // a success arriving under one is not spoken.
                    if (!host.ModalSurfaceOpen)
                    {
                        AnnounceShellRegion(target, host);
                    }
                },
                () =>
                {
                    if (ReferenceEquals(_ringPress, press))
                    {
                        _ringPress = null;
                        CycleShellRegionFrom(host, target, direction, taken);
                    }
                });
            if (landing == ShellRegionLanding.Pending)
            {
                // W7-7 PR 8 (R-10): the region holds the landing for its
                // content. The line is spoken when focus arrives, the ring
                // resumes here if the landing is refused, and a withdrawn
                // landing does neither; moving on now would land a second
                // region under the held one.
                return;
            }

            if (ReferenceEquals(_ringPress, press))
            {
                _ringPress = null;
            }

            if (landing == ShellRegionLanding.Landed)
            {
                AnnounceShellRegion(target, host);
                return;
            }

            current = target;
        }
    }

    private void AnnounceShellRegion(ShellRegionKind region, IShellRegionHost host)
    {
        switch (region)
        {
            case ShellRegionKind.MenuBar:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.MenuBar()));
                break;
            case ShellRegionKind.Files:
                _announce(new A11yEvent.FilesRegionFocused());
                break;
            case ShellRegionKind.TabBar:
                WorkspaceTabViewModel? tab = ActiveGroup.ActiveTab;
                _announce(new A11yEvent.TabFocused(
                    Prefix: "Tab bar. ",
                    Filename: tab is null ? string.Empty : ActiveGroup.SpokenNameOf(tab),
                    Index: (uint)Math.Max(1, tab is null ? 1 : ActiveGroup.Tabs.IndexOf(tab) + 1),
                    Count: (uint)ActiveGroup.Tabs.Count));
                break;
            case ShellRegionKind.Editor:
                AnnounceActivePane();
                break;
            case ShellRegionKind.EmptyEditor:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.EmptyEditor()));
                break;
            case ShellRegionKind.RightPaneContent:
                _announce(new A11yEvent.LeafPanelShown(ActiveLeaf.Title));
                break;
            case ShellRegionKind.RightPaneRail:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.RightPaneRail()));
                break;
            case ShellRegionKind.StatusBar:
                _announce(new A11yEvent.ShellRegionFocused(new ShellRegion.StatusBar(host.StatusText)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(region), region, "every shell region announces");
        }
    }

    public bool FocusDirectionalPane(string axis, int direction)
    {
        var rects = new Dictionary<WorkspaceGroupViewModel, PaneRect>();
        BuildPaneRects(Root, new PaneRect(0, 0, 1, 1), rects);
        if (rects.TryGetValue(ActiveGroup, out PaneRect origin))
        {
            WorkspaceGroupViewModel? bestGroup = null;
            double bestDistance = double.PositiveInfinity;
            double bestOverlap = double.NegativeInfinity;
            double bestCross = double.PositiveInfinity;
            foreach ((WorkspaceGroupViewModel group, PaneRect candidate) in rects)
            {
                if (ReferenceEquals(group, ActiveGroup)
                    || !TryDirectionalScore(
                        origin,
                        candidate,
                        axis,
                        direction,
                        out double distance,
                        out double overlap,
                        out double cross))
                {
                    continue;
                }

                bool isBetter = distance < bestDistance - 1e-9
                    || (Math.Abs(distance - bestDistance) <= 1e-9
                        && (overlap > bestOverlap + 1e-9
                            || (Math.Abs(overlap - bestOverlap) <= 1e-9
                                && cross < bestCross - 1e-9)));
                if (isBetter)
                {
                    bestGroup = group;
                    bestDistance = distance;
                    bestOverlap = overlap;
                    bestCross = cross;
                }
            }

            if (bestGroup is not null)
            {
                ActiveGroup = bestGroup;
                AnnounceActivePane();
                RequestActiveEditorFocus();
                Persist();
                return true;
            }
        }

        if (axis == "horizontal")
        {
            WorkspaceFocusBoundary boundary = direction < 0
                ? WorkspaceFocusBoundary.Files
                : WorkspaceFocusBoundary.RightPaneEdge;
            if (boundary == WorkspaceFocusBoundary.RightPaneEdge && !IsRightPaneVisible)
            {
                IsRightPaneVisible = true;
            }

            _announce(boundary == WorkspaceFocusBoundary.Files
                ? new A11yEvent.FilesRegionFocused()
                : new A11yEvent.LeafPanelShown(ActiveLeaf.Title));
            FocusBoundaryRequested?.Invoke(this, boundary);
            // W6-2 PR B (rule C, Term 3(a)): a directional reveal's end.
            ConsumePendingMount();
        }
        else
        {
            // W0.5-3 residue: Windows terminal vertical-focus availability copy.
            _announce(new A11yEvent.HostComposed(
                direction < 0 ? "No pane above." : "No pane below.",
                A11yPriority.Medium));
        }

        return false;
    }

    private static bool TryDirectionalScore(
        PaneRect origin,
        PaneRect candidate,
        string axis,
        int direction,
        out double distance,
        out double overlap,
        out double cross)
    {
        if (axis == "horizontal")
        {
            distance = direction < 0
                ? origin.MinX - candidate.MaxX
                : candidate.MinX - origin.MaxX;
            overlap = Math.Min(origin.MaxY, candidate.MaxY)
                - Math.Max(origin.MinY, candidate.MinY);
            cross = candidate.MinY;
        }
        else
        {
            distance = direction < 0
                ? origin.MinY - candidate.MaxY
                : candidate.MinY - origin.MaxY;
            overlap = Math.Min(origin.MaxX, candidate.MaxX)
                - Math.Max(origin.MinX, candidate.MinX);
            cross = candidate.MinX;
        }

        return distance >= -1e-9 && overlap > 1e-9;
    }

    private static void BuildPaneRects(
        WorkspacePaneNodeViewModel node,
        PaneRect rect,
        IDictionary<WorkspaceGroupViewModel, PaneRect> output)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            output[group] = rect;
            return;
        }

        double total = node.Children.Sum(child => child.Weight);
        if (!double.IsFinite(total) || total <= 0)
        {
            total = node.Children.Count;
        }

        double offset = 0;
        foreach (WorkspacePaneNodeViewModel child in node.Children)
        {
            double fraction = child.Weight / total;
            PaneRect childRect = node.Axis == "horizontal"
                ? new PaneRect(
                    rect.MinX + offset * rect.Width,
                    rect.MinY,
                    rect.MinX + (offset + fraction) * rect.Width,
                    rect.MaxY)
                : new PaneRect(
                    rect.MinX,
                    rect.MinY + offset * rect.Height,
                    rect.MaxX,
                    rect.MinY + (offset + fraction) * rect.Height);
            BuildPaneRects(child, childRect, output);
            offset += fraction;
        }
    }

    public void AnnounceActivePaneFocus() => AnnounceActivePane();

    /// <summary>Internal for the window's overlay-close focus
    /// fallback (the Bases overlays restore here when their captured
    /// element died in a republish).</summary>
    /// <summary>
    /// The ONE focus-request funnel: every user-initiated open calls it,
    /// and no background path does. W6-1 PR A hangs the canvas's focus
    /// landing here (contract A14) for exactly that property — a
    /// retarget, a session restore of a pane the user is not in, and a
    /// history reload all publish without asking, and must not steal
    /// focus; a second tab on an already-open path is a registry hit
    /// that never publishes, and must still land it. W7-7 PR 8 (R-10,
    /// OD-12's one entry): the funnel ASKS; it no longer raises a canvas or
    /// graph document's request itself. The shell's one landing entry
    /// creates every editor landing request — addressed to the tab that
    /// asked, under the window's slot before the request exists, and never
    /// under an open modal surface (<c>MainWindow.FocusEditorPane</c>).
    /// </summary>
    internal void RequestActiveEditorFocus()
    {
        // W7-7 PR 8 (R-10): this request supersedes a landing the window
        // holds — in this pane or in the one it leaves — so that landing is
        // withdrawn NOW, before its content can arrive ahead of this request's
        // own (queued) landing and seat focus there.
        WithdrawHeldShellRegionLanding();
        EditorPaneFocusRequested?.Invoke(this, ActiveGroup);
    }

    private void AnnounceActivePane()
    {
        IReadOnlyList<WorkspaceGroupViewModel> groups = Groups;
        uint ordinal = (uint)(Array.IndexOf(groups.ToArray(), ActiveGroup) + 1);
        _announce(new A11yEvent.EditorPaneFocused(
            ordinal,
            (uint)groups.Count,
            ActiveGroup.ActiveTab is { } active ? ActiveGroup.SpokenNameOf(active) : "Empty pane",
            string.Empty));
    }

    private void ResizeActivePane(double delta)
    {
        WorkspacePaneNodeViewModel? node = FindNode(Root, ActiveGroup);
        if (node is null || !TryFindParent(Root, node, out WorkspacePaneNodeViewModel? parent))
        {
            _announce(new A11yEvent.NoSplitPanesToResize());
            return;
        }

        int index = parent!.Children.IndexOf(node);
        int neighborIndex = index == parent.Children.Count - 1 ? index - 1 : index + 1;
        WorkspacePaneNodeViewModel neighbor = parent.Children[neighborIndex];
        double applied = Math.Clamp(
            delta,
            WorkspacePersistence.MinGroupWeight - node.Weight,
            neighbor.Weight - WorkspacePersistence.MinGroupWeight);
        node.Weight += applied;
        neighbor.Weight -= applied;
        _announce(new A11yEvent.PaneResized((uint)Math.Round(node.Weight * 100)));
        Persist();
    }

    internal void AnnouncePaneResize(double weight)
    {
        // W0.5-3 residue: Windows split-handle size feedback.
        _announce(new A11yEvent.HostComposed(
            $"Editor pane size {weight:P0}.",
            A11yPriority.Medium));
    }

    private static IEnumerable<WorkspaceGroupViewModel> EnumerateGroups(WorkspacePaneNodeViewModel node)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            yield return group;
            yield break;
        }

        foreach (WorkspacePaneNodeViewModel child in node.Children)
        {
            foreach (WorkspaceGroupViewModel descendant in EnumerateGroups(child))
            {
                yield return descendant;
            }
        }
    }

    private static WorkspacePaneNodeViewModel? FindNode(
        WorkspacePaneNodeViewModel node,
        WorkspaceGroupViewModel group)
    {
        if (node.Group == group)
        {
            return node;
        }

        return node.Children.Select(child => FindNode(child, group)).FirstOrDefault(found => found is not null);
    }

    private static bool TryFindParent(
        WorkspacePaneNodeViewModel current,
        WorkspacePaneNodeViewModel target,
        out WorkspacePaneNodeViewModel? parent)
    {
        if (current.Children.Contains(target))
        {
            parent = current;
            return true;
        }

        foreach (WorkspacePaneNodeViewModel child in current.Children)
        {
            if (TryFindParent(child, target, out parent))
            {
                return true;
            }
        }

        parent = null;
        return false;
    }

    private static void NormalizeWeights(WorkspacePaneNodeViewModel split)
    {
        double weight = 1d / split.Children.Count;
        foreach (WorkspacePaneNodeViewModel child in split.Children)
        {
            child.Weight = weight;
        }
    }

    private static void NormalizeWeightRatios(WorkspacePaneNodeViewModel split)
    {
        double total = split.Children.Sum(child => child.Weight);
        if (!double.IsFinite(total) || total <= 0)
        {
            NormalizeWeights(split);
            return;
        }

        foreach (WorkspacePaneNodeViewModel child in split.Children)
        {
            child.Weight /= total;
        }
    }

    /// <summary>
    /// Raised after this view model requeries its own hand-maintained list,
    /// so the shell can requery the REGISTERED catalog by enumeration
    /// (PINV-7).
    /// </summary>
    /// <remarks>
    /// The workspace refresh is the one that matters for the invariant's own
    /// example: <c>ToggleReadingModeCommand</c> gates on the active tab
    /// being Markdown, so it has to requery on a tab switch — which the
    /// vault-lifecycle refresh never sees.
    /// </remarks>
    internal Action? RegisteredCommandStatesChanged { get; set; }

    private void RaiseCommandStates()
    {
        RegisteredCommandStatesChanged?.Invoke();
        foreach (ICommand command in new[]
        {
            CloseActiveTabCommand,
            DuplicateTabCommand,
            ReopenClosedTabCommand,
            MoveTabLeftCommand,
            MoveTabRightCommand,
            NextTabCommand,
            PreviousTabCommand,
            SplitRightCommand,
            SplitDownCommand,
            ClosePaneCommand,
            FocusPaneLeftCommand,
            FocusPaneRightCommand,
            FocusPaneAboveCommand,
            FocusPaneBelowCommand,
            FocusNextPaneCommand,
            FocusPreviousPaneCommand,
            GrowPaneCommand,
            ShrinkPaneCommand,
            SaveActiveCommand,
        })
        {
            ((RelayCommand)command).RaiseCanExecuteChanged();
        }
        RaiseBasesCommandStates();
    }
}
