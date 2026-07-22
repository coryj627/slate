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
            if (SetField(ref _activeGroup, value))
            {
                RaiseCommandStates();
            }
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
        if (tab is not null)
        {
            int index = group.Tabs.IndexOf(tab) + 1;
            _announce(new A11yEvent.TabFocused(
                Prefix: string.Empty,
                Filename: tab.Title,
                Index: (uint)Math.Max(1, index),
                Count: (uint)group.Tabs.Count));
        }

        RaiseCommandStates();
        Persist();
    }

    private void OpenItem(WorkspaceItemState item, WorkspaceOpenTarget target)
    {
        if (TryOpenItem(item, target))
        {
            Persist();
        }
    }

    private bool TryOpenItem(WorkspaceItemState item, WorkspaceOpenTarget target)
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
            WorkspaceTabViewModel? existing = ActiveGroup.Tabs.FirstOrDefault(
                tab => ItemsReferToSameTarget(tab.Item, item));
            if (existing is not null)
            {
                ActiveGroup.ActiveTab = existing;
                RequestActiveEditorFocus();
                return true;
            }

            if (target == WorkspaceOpenTarget.NewTab)
            {
                AddTab(ActiveGroup, item, activate: true);
                RequestActiveEditorFocus();
                return true;
            }
        }

        WorkspaceTabViewModel? active = ActiveGroup.ActiveTab;
        if (active is null)
        {
            AddTab(ActiveGroup, item, activate: true);
        }
        else if (!ItemsReferToSameTarget(active.Item, item))
        {
            if (active.IsDirty)
            {
                WorkspaceDirtyNavigationDecision decision = _dirtyNavigationDecision(active, item);
                if (decision == WorkspaceDirtyNavigationDecision.Cancel
                    || (decision == WorkspaceDirtyNavigationDecision.Save && !active.Save()))
                {
                    return false;
                }
            }

            WorkspaceTabViewModel? peer = FindSamePathTab(item, excluding: active);
            active.ReplaceItem(item);
            if (peer is not null)
            {
                active.MirrorDocumentStateFrom(peer);
            }

            ActiveGroup.ActiveTab = active;
            RaiseCommandStates();
        }

        RequestActiveEditorFocus();
        return true;
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
            MirrorSamePathDocumentState);
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

        WorkspaceGroupViewModel? group = Groups.FirstOrDefault(candidate => candidate.Tabs.Contains(tab));
        if (group is null || !CanCloseTab(tab))
        {
            return;
        }

        int index = group.Tabs.IndexOf(tab);
        _closedTabs.Add((tab.Item, group.Id));
        if (_closedTabs.Count > ClosedTabCapacity)
        {
            _closedTabs.RemoveAt(0);
        }

        group.Tabs.Remove(tab);
        WorkspaceTabViewModel? successor = group.Tabs.Count == 0
            ? null
            : group.Tabs[Math.Min(index, group.Tabs.Count - 1)];
        group.ActiveTab = successor;
        _announce(new A11yEvent.TabClosed(tab.Title, successor?.Title));
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
        foreach (WorkspaceTabViewModel tab in group.Tabs.ToArray())
        {
            if (!CanCloseTab(tab))
            {
                return;
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

        group.Tabs.Clear();
        RemoveEmptyGroup(group);
        AnnounceActivePane();
        RequestActiveEditorFocus();
        RaiseCommandStates();
        Persist();
    }

    private bool CanCloseTab(WorkspaceTabViewModel tab)
    {
        if (!tab.IsDirty)
        {
            return true;
        }

        WorkspaceDirtyNavigationDecision decision = _dirtyCloseDecision(tab);
        return decision == WorkspaceDirtyNavigationDecision.Discard
            || (decision == WorkspaceDirtyNavigationDecision.Save && tab.Save());
    }

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
            MirrorSamePathDocumentState);
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
        if (item.Kind == WorkspaceItemKind.Graph && TryFocusGlobalGraph())
        {
            _announce(new A11yEvent.ReopenedGraph());
            RaiseCommandStates();
            Persist();
            return;
        }

        WorkspaceGroupViewModel group = Groups.FirstOrDefault(candidate => candidate.Id == groupId)
            ?? ActiveGroup;
        WorkspaceTabViewModel tab = AddTab(group, item, activate: true);
        ActiveGroup = group;
        _announce(item.Kind switch
        {
            WorkspaceItemKind.Graph => new A11yEvent.ReopenedGraph(),
            WorkspaceItemKind.SavedQuery or WorkspaceItemKind.Dashboard =>
                new A11yEvent.ReopenedNamed(tab.Title),
            _ => new A11yEvent.ReopenedFile(tab.Title),
        });
        RaiseCommandStates();
        Persist();
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
    }

    private void FocusPane(int delta)
    {
        IReadOnlyList<WorkspaceGroupViewModel> groups = Groups;
        int index = Array.IndexOf(groups.ToArray(), ActiveGroup);
        ActiveGroup = groups[(index + delta + groups.Count) % groups.Count];
        AnnounceActivePane();
        RequestActiveEditorFocus();
        Persist();
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
                : WorkspaceFocusBoundary.RightPane;
            if (boundary == WorkspaceFocusBoundary.RightPane && !IsRightPaneVisible)
            {
                IsRightPaneVisible = true;
            }

            _announce(boundary == WorkspaceFocusBoundary.Files
                ? new A11yEvent.FilesRegionFocused()
                : new A11yEvent.LeafPanelShown(ActiveLeaf.Title));
            FocusBoundaryRequested?.Invoke(this, boundary);
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

    private void RequestActiveEditorFocus() =>
        EditorPaneFocusRequested?.Invoke(this, ActiveGroup);

    private void AnnounceActivePane()
    {
        IReadOnlyList<WorkspaceGroupViewModel> groups = Groups;
        uint ordinal = (uint)(Array.IndexOf(groups.ToArray(), ActiveGroup) + 1);
        _announce(new A11yEvent.EditorPaneFocused(
            ordinal,
            (uint)groups.Count,
            ActiveGroup.ActiveTab?.Title ?? "Empty pane",
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

    private void RaiseCommandStates()
    {
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
    }
}
