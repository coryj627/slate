// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Owns workspace restore, mutation batching, and persisted snapshot
/// serialization. Layout behavior and command policy remain in other partials.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private readonly WorkspacePersistence _persistence;
    private readonly Func<IReadOnlyList<string>> _expandedDirectoryPaths;
    private bool _restoring;
    private int _persistenceBatchDepth;
    private bool _persistencePending;

    private (WorkspacePaneNodeViewModel Root, WorkspaceGroupViewModel Active) Restore(
        WorkspaceSnapshot? snapshot)
    {
        _restoring = true;
        try
        {
            if (snapshot is null)
            {
                var group = new WorkspaceGroupViewModel(this, Guid.NewGuid());
                return (new WorkspacePaneNodeViewModel(group), group);
            }

            bool graphRestored = false;
            WorkspacePaneNodeViewModel restored = RestoreNode(snapshot.Root, ref graphRestored);
            WorkspacePaneNodeViewModel root = PruneEmptyRestoreGroups(restored, allowEmptyRoot: true)
                ?? new WorkspacePaneNodeViewModel(
                    new WorkspaceGroupViewModel(this, Guid.NewGuid()));
            root.Weight = 1;
            WorkspaceGroupViewModel active = EnumerateGroups(root)
                .FirstOrDefault(group =>
                    group.Id == snapshot.ActiveGroup
                    && (group.ActiveTab is not null || GroupsHaveNoTabs(root)))
                ?? EnumerateGroups(root).First();
            _activeLeaf = Leaves.FirstOrDefault(leaf => leaf.Id == snapshot.ActiveLeaf) ?? Leaves[0];
            return (root, active);
        }
        finally
        {
            _restoring = false;
        }
    }

    private WorkspacePaneNodeViewModel RestoreNode(WorkspaceNodeState state, ref bool graphRestored)
    {
        if (state is WorkspaceGroupState groupState)
        {
            var group = new WorkspaceGroupViewModel(this, groupState.Id);
            foreach (WorkspaceTabState tabState in groupState.Tabs)
            {
                if (tabState.Item.Kind == WorkspaceItemKind.Graph)
                {
                    if (graphRestored)
                    {
                        continue;
                    }

                    graphRestored = true;
                }

                group.Tabs.Add(new WorkspaceTabViewModel(
                    _session,
                    tabState,
                    MirrorSamePathDocumentState));
            }

            WorkspaceTabViewModel? active = group.Tabs.FirstOrDefault(tab => tab.Id == groupState.ActiveTab)
                ?? group.Tabs.FirstOrDefault();
            group.RestoreActive(active);
            return new WorkspacePaneNodeViewModel(group);
        }

        var splitState = (WorkspaceSplitState)state;
        var split = new WorkspacePaneNodeViewModel(splitState.Axis);
        for (int index = 0; index < splitState.Children.Count; index++)
        {
            WorkspacePaneNodeViewModel child = RestoreNode(
                splitState.Children[index],
                ref graphRestored);
            child.Weight = splitState.Weights[index];
            split.Children.Add(child);
        }

        return split;
    }

    private static WorkspacePaneNodeViewModel? PruneEmptyRestoreGroups(
        WorkspacePaneNodeViewModel node,
        bool allowEmptyRoot)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            return group.Tabs.Count > 0 || allowEmptyRoot ? node : null;
        }

        WorkspacePaneNodeViewModel[] children = node.Children.ToArray();
        node.Children.Clear();
        foreach (WorkspacePaneNodeViewModel child in children)
        {
            WorkspacePaneNodeViewModel? retained = PruneEmptyRestoreGroups(
                child,
                allowEmptyRoot: false);
            if (retained is not null)
            {
                node.Children.Add(retained);
            }
        }

        if (node.Children.Count == 0)
        {
            return null;
        }

        if (node.Children.Count == 1)
        {
            WorkspacePaneNodeViewModel only = node.Children[0];
            only.Weight = node.Weight;
            return only;
        }

        NormalizeWeightRatios(node);
        return node;
    }

    private static bool GroupsHaveNoTabs(WorkspacePaneNodeViewModel root) =>
        !EnumerateGroups(root).SelectMany(group => group.Tabs).Any();

    private void RunWorkspaceMutation(Action action)
    {
        _persistenceBatchDepth++;
        try
        {
            action();
        }
        finally
        {
            _persistenceBatchDepth--;
            if (_persistenceBatchDepth == 0 && _persistencePending)
            {
                _persistencePending = false;
                PersistCore();
            }
        }
    }

    private void Persist()
    {
        if (_restoring)
        {
            return;
        }

        if (_persistenceBatchDepth > 0)
        {
            _persistencePending = true;
            return;
        }

        PersistCore();
    }

    internal void PersistLayoutWeights() => Persist();

    private void PersistCore()
    {
        if (_restoring)
        {
            return;
        }

        try
        {
            _persistence.Save(new WorkspaceSnapshot(
                WorkspacePersistence.SchemaVersion,
                ActiveGroup.Id,
                SnapshotNode(Root),
                ActiveLeaf.Id,
                _expandedDirectoryPaths()));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            HostLog.Write(HostDiagnosticEvent.WorkspacePersistFailed, exception);
        }
    }

    private static WorkspaceNodeState SnapshotNode(WorkspacePaneNodeViewModel node)
    {
        if (node.Group is WorkspaceGroupViewModel group)
        {
            return new WorkspaceGroupState(
                group.Id,
                group.ActiveTab?.Id,
                group.Tabs.Select(tab => tab.Snapshot()).ToArray());
        }

        double total = node.Children.Sum(child => child.Weight);
        return new WorkspaceSplitState(
            node.Axis!,
            node.Children.Select(child => child.Weight / total).ToArray(),
            node.Children.Select(SnapshotNode).ToArray());
    }
}
