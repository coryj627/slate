// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.IO;

namespace SlateWindows;

/// <summary>
/// WB-12 (#1505, WD6): the Windows twin of mac's <c>pruneStaleSidebarPins</c>.
/// A pin whose entry is gone is pruned lazily, on a complete level fetch of
/// its folder, once per folder per session, never while the settings file is
/// read-only or behind memory. A worker proves absence on disk (error-aware,
/// <see cref="VaultAbsence"/>); the dispatcher then commits in one turn — the
/// pin-generation check, the store's locked re-read and re-probe, and the
/// memory removal — so a pin edit before the commit writes nothing, one
/// queued during it runs after the publication, and the file's pins and
/// memory's agree after every prune. Nothing is spoken.
/// </summary>
/// <remarks>Why the commit is not on the worker: <see cref="PersistPins"/>
/// writes the whole pin set, so a worker's removal that a moved generation
/// then discarded from memory would be written back by the next pin edit;
/// and a worker's write would overlap the dispatcher's own on the
/// settings file's <c>FileShare.None</c> lock.</remarks>
internal sealed partial class FilesSidebarViewModel
{
    private readonly Func<Action, CancellationToken, Task> _runPinPruneWorker;

    /// <summary>Moves on every write to <see cref="_pinned"/>; a prune
    /// commits only when it has not moved since the prune started.</summary>
    private int _pinGeneration;

    /// <summary>Folders a prune removed something from this session.</summary>
    private readonly HashSet<string> _prunedFolders = new(StringComparer.Ordinal);

    /// <summary>Folders whose prune is between its start and its commit.</summary>
    private readonly HashSet<string> _pruningFolders = new(StringComparer.Ordinal);

    /// <summary>Stale pins a complete level fetch found, by folder, waiting
    /// for the dispatcher's next publication. Level fetches run on the tree
    /// worker against the dispatcher's copy of the pins.</summary>
    private readonly ConcurrentQueue<(string Folder, string[] Stale)> _stalePinReports = new();

    /// <summary>Test seam: runs inside the commit's turn, after the store's
    /// locked mutation and before memory's removal.</summary>
    internal Action? StalePinCommitForTests { get; set; }

    /// <summary>A copy of the pins memory holds.</summary>
    internal IReadOnlySet<string> PinnedForTests => _pinned.ToHashSet(StringComparer.Ordinal);

    /// <summary>On the tree worker: a complete level's pins (from the
    /// dispatcher's copy) that are in this folder but not listed.</summary>
    private void ReportStalePins(string folder, IReadOnlySet<string> pinned, IEnumerable<string> listed)
    {
        if (pinned.Count == 0)
        {
            return;
        }

        var present = new HashSet<string>(listed, StringComparer.Ordinal);
        string[] stale = pinned
            .Where(pin => string.Equals(ParentPath(pin), folder, StringComparison.Ordinal) && !present.Contains(pin))
            .ToArray();
        if (stale.Length > 0)
        {
            _stalePinReports.Enqueue((folder, stale));
        }
    }

    /// <summary>On the dispatcher, after a level publication: starts a prune
    /// for every folder a fetch reported.</summary>
    private void StartReportedPinPrunes()
    {
        while (_stalePinReports.TryDequeue(out (string Folder, string[] Stale) report))
        {
            PruneStalePins(report.Folder, report.Stale);
        }
    }

    private void PruneStalePins(string folder, IReadOnlyCollection<string> stale)
    {
        if (_settingsStore is null
            || _vaultRoot is not { } vaultRoot
            || _settingsStore.ReadOnlyReason is not null
            || _pinsBehindFile
            || SessionShutdownStarted
            || _prunedFolders.Contains(folder)
            || _pruningFolders.Contains(folder))
        {
            return;
        }

        string[] candidates = stale
            .Where(_pinned.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(SidebarSettingsStore.MaxPinsPerFolder)
            .ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        int generation = _pinGeneration;
        _pruningFolders.Add(folder);
        SynchronizationContext? context = _treeUiContext;
        if (context is null)
        {
            // Headless: no dispatcher to come back to, so the probe and the
            // commit run here, in order.
            CommitStalePinPrune(folder, generation, Probe(vaultRoot, folder, candidates));
            return;
        }

        try
        {
            _ = _runPinPruneWorker(
                () =>
                {
                    IReadOnlySet<string> proven = Probe(vaultRoot, folder, candidates);
                    context.Post(_ => CommitStalePinPrune(folder, generation, proven), null);
                },
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarPinsPersistFailed, exception);
            _pruningFolders.Remove(folder);
        }
    }

    /// <summary>The worker's proof: the disk only, never memory or the store.</summary>
    private static IReadOnlySet<string> Probe(string vaultRoot, string folder, string[] candidates)
    {
        try
        {
            return VaultAbsence.AbsentAmong(vaultRoot, folder, candidates);
        }
        catch (Exception exception)
        {
            // Proves nothing; the commit still runs, so the folder is not
            // left marked in flight.
            HostLog.Write(HostDiagnosticEvent.SidebarPinsPersistFailed, exception);
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    /// <summary>On the dispatcher, in one synchronous turn: nothing is
    /// written unless no pin edit has landed since the prune started, the
    /// store is writable and holds memory's last pins; then the store removes,
    /// under its lock, only candidates it still holds that are still absent,
    /// and memory drops exactly those.</summary>
    private void CommitStalePinPrune(string folder, int generation, IReadOnlySet<string> proven)
    {
        _pruningFolders.Remove(folder);
        if (proven.Count == 0
            || generation != _pinGeneration
            || _settingsStore is null
            || _vaultRoot is not { } vaultRoot
            || _settingsStore.ReadOnlyReason is not null
            || _pinsBehindFile
            || SessionShutdownStarted)
        {
            return;
        }

        IReadOnlySet<string> removed;
        try
        {
            removed = _settingsStore.PruneStalePins(
                folder,
                [.. proven.Where(_pinned.Contains)],
                names => VaultAbsence.AbsentAmong(vaultRoot, folder, names));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            // Silent; memory and the file are as they were, and the folder
            // stays eligible.
            HostLog.Write(HostDiagnosticEvent.SidebarPinsPersistFailed, exception);
            return;
        }

        StalePinCommitForTests?.Invoke();
        if (removed.Count == 0)
        {
            return;
        }

        _pinned.ExceptWith(removed);
        _pinGeneration++;
        _prunedFolders.Add(folder);
    }
}
