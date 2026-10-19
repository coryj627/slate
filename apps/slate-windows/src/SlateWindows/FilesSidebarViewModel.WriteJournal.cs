// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// #1435 (the Windows twin of mac's #944, plan 43 PR 23, structural half):
/// the sidebar's side of <see cref="SidebarWriteJournal"/>. A rename or move
/// journals the renames it anticipates, durably, before core is called;
/// records what committed when core reports, or settles when nothing did;
/// and settles once the pins and shortcuts writes that carry its renames
/// have succeeded. A failed pins or shortcuts write leaves the entry
/// unsettled, so the divergence survives a quit. The next open of the same
/// vault replays every unsettled entry before the settings file's pins and
/// shortcuts are adopted, and so does the sidebar-settings retry when the
/// file is readable again (#1514: the outage's own renames and moves
/// included); an entry recorded before core was known to have committed
/// replays only the renames whose source is definitely gone.
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    // W0.5-3 residue: mac's sidebar write-journal copy, host-composed there
    // too (AppState.sidebarUnreadableJournalReason,
    // announceSidebarWriteDiscardedForReplacedVault, sidebarAgedOutChangesReason).
    internal const string UnreadableJournalReason =
        "Some sidebar changes saved before Slate last quit could not be read, "
        + "so they were not applied.";

    // W0.5-3 residue: mac's replaced-vault discard sentence (see above).
    internal const string ReplacedVaultJournalReason =
        "The vault at this location changed, so a queued sidebar settings write was discarded.";

    private readonly SidebarWriteJournal? _writeJournal;
    private readonly SidebarJournalIdentity? _journalIdentity;

    /// <summary>Journal entries whose renames are in memory but whose pins
    /// write has not landed since — settled by the next pins write that
    /// succeeds (and its shortcuts twin below).</summary>
    private readonly HashSet<Guid> _journalAwaitingPins = [];
    private readonly HashSet<Guid> _journalAwaitingShortcuts = [];

    /// <summary>What a replay has to say, held until the first publication
    /// so the status shows it.</summary>
    private readonly List<string> _pendingJournalNotices = [];
    private readonly List<string> _pendingJournalFailures = [];

    /// <summary>The last pins (shortcuts) write failed: memory holds what
    /// <c>.slate/sidebar.json</c> does not.</summary>
    private bool _pinsBehindFile;
    private bool _shortcutsBehindFile;

    /// <summary>Test seam: runs once a rename's or move's pending line is on
    /// disk, before core is called.</summary>
    internal Action? BeforeStructuralCommitForTesting { get; set; }

    /// <summary>Test seam: runs after core reported and the journal recorded
    /// what committed, before any stored path follows. A fact throws from it
    /// to end the process there.</summary>
    internal Action? StructuralCommitLandedForTesting { get; set; }

    /// <summary>mac's <c>sidebarAgedOutChangesReason</c> for changes that
    /// carry no acknowledgement (a rename's).</summary>
    internal static string AgedOutJournalReason(int count, int days)
    {
        // W0.5-3 residue: mac's aged-out journal sentence (see above).
        return count == 1
            ? $"A sidebar change waiting more than {days} days to be saved was not applied."
            : $"{count} sidebar changes waiting more than {days} days to be saved were not applied.";
    }

    /// <summary>Journal the renames a structural operation anticipates and
    /// wait, bounded, for the line to reach the disk before core may commit
    /// them. Null when nothing is journaled: no vault root, a volume that
    /// cannot say the root's identity, or nothing to rename.</summary>
    /// <remarks>The wait is on the UI thread, as the rename it guards is, but
    /// it waits on nothing that needs that thread: the journal's lane runs
    /// only file work. A line that cannot land in time lets the operation
    /// proceed without its durable record (mac's rule for a line that could
    /// not be written); the line still lands, in order.</remarks>
    private Guid? JournalBeforeStructuralCommit(IReadOnlyList<SidebarJournalRename> planned)
    {
        if (_writeJournal is not SidebarWriteJournal journal
            || _journalIdentity is not SidebarJournalIdentity identity
            || _vaultRoot is not string vaultRoot
            || planned.Count == 0)
        {
            return null;
        }

        SidebarJournalAppend? append = journal.AppendPending(identity, vaultRoot, planned);
        if (append is null)
        {
            return null;
        }

        if (!append.Durable.Wait(SidebarWriteJournal.DurableWaitBudget) || !append.Durable.Result)
        {
            HostLog.Write(HostDiagnosticEvent.SidebarWriteJournalFailed);
        }

        BeforeStructuralCommitForTesting?.Invoke();
        return append.Id;
    }

    /// <summary>Core reported: the entry records what committed, or is
    /// settled when nothing did — before anything else can return
    /// early.</summary>
    private void JournalStructuralCommitted(Guid? entry, IReadOnlyList<SidebarJournalRename> committed)
    {
        if (entry is Guid id
            && _writeJournal is SidebarWriteJournal journal
            && _journalIdentity is SidebarJournalIdentity identity)
        {
            journal.ResolveCommitted(identity, id, committed);
        }

        StructuralCommitLandedForTesting?.Invoke();
    }

    /// <summary>A single rename or move refused: nothing committed, so its
    /// entry is settled — unless core says the mutation stopped incomplete,
    /// when what committed is unknown.</summary>
    private void JournalStructuralFailed(Guid? entry, VaultException exception)
    {
        if (exception is VaultException.StructuralMutationIncomplete)
        {
            JournalStructuralOutcomeUnknown(entry);
            return;
        }

        if (entry is Guid id
            && _writeJournal is SidebarWriteJournal journal
            && _journalIdentity is SidebarJournalIdentity identity)
        {
            journal.Settle(identity, [id]);
        }
    }

    /// <summary>Core ended without saying what committed: the entry stays as
    /// anticipated, and the next open replays only the renames whose source
    /// is definitely gone.</summary>
    private void JournalStructuralOutcomeUnknown(Guid? entry)
    {
        if (entry is Guid id
            && _writeJournal is SidebarWriteJournal journal
            && _journalIdentity is SidebarJournalIdentity identity)
        {
            journal.ReleaseForVerification(identity, id);
        }
    }

    /// <summary>Every stored-path rewrite the operation implies is in memory
    /// now. Its journal entry, when it has one, is settled when the pins and
    /// shortcuts writes that carry it have landed, and otherwise waits for
    /// the next writes that do — or for the retry's replay (#1514). A
    /// rewrite no entry carries — a delete's, or a rename's or move's the
    /// journal could not record — whose write did not land is #1230's
    /// divergence memory: nothing can replay it, so the retry's adoption
    /// speaks the stale-references outcome. A rewrite that lands both
    /// sections writes the whole current sets, so it clears the
    /// memory.</summary>
    private void StoredPathsFollowed(Guid? journalEntry)
    {
        if (!_pinsBehindFile && !_shortcutsBehindFile)
        {
            _storedPathsDivergedFromFile = false;
        }
        else if (journalEntry is null)
        {
            _storedPathsDivergedFromFile = true;
        }

        if (journalEntry is Guid id)
        {
            CarryJournalEntries([id]);
        }
    }

    private void CarryJournalEntries(IReadOnlyCollection<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        if (_pinsBehindFile)
        {
            _journalAwaitingPins.UnionWith(ids);
        }

        if (_shortcutsBehindFile)
        {
            _journalAwaitingShortcuts.UnionWith(ids);
        }

        SettleCarriedJournalEntries(ids);
    }

    /// <summary>A pins write succeeded: it wrote every pin in memory, so it
    /// carries every rename memory holds.</summary>
    private void PinsLandedInFile()
    {
        _pinsBehindFile = false;
        if (_journalAwaitingPins.Count > 0)
        {
            Guid[] carried = [.. _journalAwaitingPins];
            _journalAwaitingPins.Clear();
            SettleCarriedJournalEntries(carried);
        }
    }

    private void ShortcutsLandedInFile()
    {
        _shortcutsBehindFile = false;
        if (_journalAwaitingShortcuts.Count > 0)
        {
            Guid[] carried = [.. _journalAwaitingShortcuts];
            _journalAwaitingShortcuts.Clear();
            SettleCarriedJournalEntries(carried);
        }
    }

    private void SettleCarriedJournalEntries(IEnumerable<Guid> ids)
    {
        if (_writeJournal is not SidebarWriteJournal journal
            || _journalIdentity is not SidebarJournalIdentity identity)
        {
            return;
        }

        Guid[] settled =
        [
            .. ids.Where(id => !_journalAwaitingPins.Contains(id) && !_journalAwaitingShortcuts.Contains(id)),
        ];
        if (settled.Length > 0)
        {
            journal.Settle(identity, settled);
        }
    }

    /// <summary>The retry adopts the file: memory's pins and shortcuts become
    /// the file's with the journal replayed over them, so no write of the
    /// outage is behind the file any more, and the entries that waited on
    /// those writes are the replay's to settle once its own writes land
    /// (#1514) — they are among the entries the replay adopted, and a
    /// replay write that fails puts them back to waiting.</summary>
    private void ForgetWritesTheFileReplaces()
    {
        _pinsBehindFile = false;
        _shortcutsBehindFile = false;
        _journalAwaitingPins.Clear();
        _journalAwaitingShortcuts.Clear();
    }

    /// <summary>What the settings file holds, with every unsettled journal
    /// entry replayed onto it, in the order they were recorded — the pins
    /// and shortcuts an open adopts. A read-only file adopts nothing from the
    /// journal: its entries wait for an open, or a retry, that can write
    /// them.</summary>
    private JournalReplay ReplayWriteJournal(
        SidebarSettingsSnapshot settings,
        IReadOnlyList<string> recents)
    {
        if (settings.ReadOnlyReason is not null
            || _writeJournal is not SidebarWriteJournal journal
            || _journalIdentity is not SidebarJournalIdentity identity
            || _vaultRoot is not string vaultRoot)
        {
            return new JournalReplay(settings.Pins, false, settings.Shortcuts, false, recents, false, [], []);
        }

        SidebarJournalAdoption adoption = journal.Adopt(identity, vaultRoot);
        var notices = new List<string>();
        foreach (SidebarJournalNotice notice in adoption.Notices)
        {
            notices.Add(notice == SidebarJournalNotice.ReplacedVault
                ? ReplacedVaultJournalReason
                : UnreadableJournalReason);
        }

        if (adoption.AgedOut.Count > 0)
        {
            notices.Add(AgedOutJournalReason(
                adoption.AgedOut.Count,
                (int)SidebarWriteJournal.MaxEntryAge.TotalDays));
        }

        HashSet<string> missing = DefinitelyMissingPaths(
            vaultRoot,
            identity,
            adoption.Entries
                .Where(entry => !entry.Committed)
                .SelectMany(entry => entry.Renames)
                .Select(rename => rename.OldPath));
        List<string> pins = [.. settings.Pins];
        List<SidebarShortcutState> shortcuts = [.. settings.Shortcuts];
        List<string> recentPaths = [.. recents];
        foreach (SidebarJournalEntry entry in adoption.Entries)
        {
            foreach (SidebarJournalRename rename in entry.Renames)
            {
                if (!entry.Committed && !missing.Contains(rename.OldPath))
                {
                    // Recorded before core was known to have committed, and
                    // the source is still there: this rename never happened.
                    continue;
                }

                string Follow(string path) => TransformedStoredPath(
                    path, rename.OldPath, rename.NewPath, rename.IsDirectory, deleted: false) ?? path;
                pins = [.. pins.Select(Follow).Distinct(StringComparer.Ordinal)];
                shortcuts = [.. shortcuts.Select(slot => slot with { Path = Follow(slot.Path) })];
                recentPaths = [.. recentPaths.Select(Follow).Distinct(StringComparer.Ordinal)];
            }
        }

        HashSet<string> replayedPins = pins.ToHashSet(StringComparer.Ordinal);
        return new JournalReplay(
            replayedPins,
            !replayedPins.SetEquals(settings.Pins),
            shortcuts,
            !shortcuts.SequenceEqual(settings.Shortcuts),
            recentPaths,
            !recentPaths.SequenceEqual(recents, StringComparer.Ordinal),
            [.. adoption.Entries.Select(entry => entry.Id)],
            notices);
    }

    /// <summary>Write what a replay changed, then settle its entries — or,
    /// when a write fails, keep them for the next writes that land, or for
    /// the next retry's replay. Runs once the commands exist: a write that
    /// finds the file blocked raises the notice. True when every changed
    /// section reached the file.</summary>
    private bool LandWriteJournalReplay(JournalReplay replay)
    {
        bool pinsSaved = !replay.PinsChanged || PersistPins(_pendingJournalFailures.Add);
        bool shortcutsSaved = !replay.ShortcutsChanged || PersistShortcuts(_pendingJournalFailures.Add);
        if (replay.RecentsChanged)
        {
            _recentsStore?.Replace(_recents);
        }

        _pendingJournalNotices.AddRange(replay.Notices);
        CarryJournalEntries(replay.Entries);
        return pinsSaved && shortcutsSaved;
    }

    /// <summary>Say what the journal's replay found, after the first
    /// publication so the status line keeps it: the notices first, then a
    /// replayed write that failed.</summary>
    private void AnnounceWriteJournalOutcome()
    {
        string[] notices = [.. _pendingJournalNotices];
        string[] failures = [.. _pendingJournalFailures];
        _pendingJournalNotices.Clear();
        _pendingJournalFailures.Clear();
        foreach (string notice in notices)
        {
            AnnounceWriteJournalNotice(notice);
        }

        foreach (string failure in failures)
        {
            ReportFailure(failure);
        }
    }

    private void AnnounceWriteJournalNotice(string notice)
    {
        Status = notice;
        HoldStatusForPendingPublication();

        // W0.5-3 residue: mac's sidebar write-journal notices, posted at
        // medium priority as mac's are (surfaceSidebarActionBackgroundFailure,
        // announceSidebarWriteDiscardedForReplacedVault).
        _announce(new A11yEvent.HostComposed(notice, A11yPriority.Medium));
    }

    /// <summary>A rename of <paramref name="oldPath"/> to
    /// <paramref name="newPath"/>: the entry and, for a folder, its folder
    /// note under the new name (core renames the note with its folder; the
    /// folder's own rewrite leaves it at the old leaf).</summary>
    private static List<SidebarJournalRename> PlannedRename(string oldPath, string newPath, bool isDirectory)
    {
        List<SidebarJournalRename> planned = [new(oldPath, newPath, isDirectory)];
        string oldLeaf = System.IO.Path.GetFileName(oldPath);
        string newLeaf = System.IO.Path.GetFileName(newPath);
        if (isDirectory && !string.Equals(oldLeaf, newLeaf, StringComparison.Ordinal))
        {
            planned.Add(new(
                CombineVaultPath(newPath, $"{oldLeaf}.md"),
                CombineVaultPath(newPath, $"{newLeaf}.md"),
                IsDirectory: false));
        }

        return planned;
    }

    /// <summary>What a single rename or move committed: the entry, and every
    /// path the report moved beyond prefix math (the folder note).</summary>
    private static List<SidebarJournalRename> CommittedMove(
        StructuralReport report,
        string oldPath,
        string newPath,
        bool isDirectory) =>
        [new(oldPath, newPath, isDirectory), .. StoredPathFixUps(report, oldPath, newPath)];

    /// <summary>Every pair the report moved whose real destination differs
    /// from its prefix image — the compound folder rename's note
    /// (<c>docs/docs.md → guide/guide.md</c>). Plain renames and moves yield
    /// nothing.</summary>
    private static IEnumerable<SidebarJournalRename> StoredPathFixUps(
        StructuralReport report,
        string oldPath,
        string newPath)
    {
        foreach (MovedPath moved in report.Moved)
        {
            string prefixImage;
            if (moved.OldPath == oldPath)
            {
                prefixImage = newPath;
            }
            else if (moved.OldPath.StartsWith(
                oldPath + "/", StringComparison.Ordinal))
            {
                prefixImage = newPath + moved.OldPath[oldPath.Length..];
            }
            else
            {
                continue;
            }

            if (prefixImage != moved.NewPath)
            {
                yield return new SidebarJournalRename(prefixImage, moved.NewPath, IsDirectory: false);
            }
        }
    }

    /// <summary>A batch move's renames: every item to the destination by its
    /// own name, one rename per item.</summary>
    private static List<SidebarJournalRename> PlannedBatchMove(
        IEnumerable<StructuralBatchItem> items,
        string destination)
    {
        List<SidebarJournalRename> planned = [];
        foreach (StructuralBatchItem item in items)
        {
            planned.Add(new SidebarJournalRename(
                item.Path,
                CombineVaultPath(destination, System.IO.Path.GetFileName(item.Path)),
                item.IsDirectory));
        }

        return planned;
    }

    /// <summary>The renames a batch report says stand.</summary>
    private static List<SidebarJournalRename> BatchStanding(IEnumerable<BatchPathChange> standing)
    {
        List<SidebarJournalRename> committed = [];
        foreach (BatchPathChange change in standing)
        {
            committed.Add(new SidebarJournalRename(change.OldPath, change.NewPath, change.IsDirectory));
        }

        return committed;
    }

    /// <summary>A recorded batch edge moves every change back from its new
    /// path.</summary>
    private static List<SidebarJournalRename> BatchInverse(IEnumerable<BatchPathChange>? changes)
    {
        List<SidebarJournalRename> planned = [];
        foreach (BatchPathChange change in changes ?? [])
        {
            planned.Add(new SidebarJournalRename(change.NewPath, change.OldPath, change.IsDirectory));
        }

        return planned;
    }

    /// <summary>
    /// The candidates whose path names nothing in the vault — mac's
    /// <c>sidebarDefinitelyMissingPaths</c>. Each parent is walked from the
    /// root by exact (ordinal) name, so a case-only rename on this
    /// case-insensitive filesystem reads correctly; a component that is a
    /// link, an unreadable directory, or a root that no longer has the
    /// journal's identity is uncertainty, and keeps the rename from
    /// replaying. A path that could never be a vault entry (rooted, or
    /// traversing) is missing without touching the disk.
    /// </summary>
    internal static HashSet<string> DefinitelyMissingPaths(
        string vaultRoot,
        SidebarJournalIdentity identity,
        IEnumerable<string> candidates)
    {
        var missing = new HashSet<string>(StringComparer.Ordinal);
        var byParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string candidate in candidates)
        {
            if (!IsAdmissibleVaultPath(candidate))
            {
                missing.Add(candidate);
                continue;
            }

            string parent = ParentPath(candidate);
            if (!byParent.TryGetValue(parent, out List<string>? children))
            {
                children = [];
                byParent[parent] = children;
            }

            children.Add(candidate);
        }

        if (byParent.Count == 0 || SidebarJournalIdentity.Observe(vaultRoot) != identity)
        {
            return missing;
        }

        foreach ((string parent, List<string> children) in byParent)
        {
            HashSet<string>? names = ExactChildNames(vaultRoot, parent, out bool parentAbsent);
            if (parentAbsent)
            {
                missing.UnionWith(children);
                continue;
            }

            if (names is null)
            {
                continue;
            }

            foreach (string child in children)
            {
                if (!names.Contains(System.IO.Path.GetFileName(child)))
                {
                    missing.Add(child);
                }
            }
        }

        return missing;
    }

    /// <summary>The exact names in vault folder <paramref name="parent"/>, or
    /// null when they cannot be known; <paramref name="parentAbsent"/> when
    /// the folder itself is not there.</summary>
    private static HashSet<string>? ExactChildNames(string vaultRoot, string parent, out bool parentAbsent)
    {
        parentAbsent = false;
        try
        {
            var directory = new DirectoryInfo(System.IO.Path.GetFullPath(vaultRoot));
            string[] components = parent.Length == 0 ? [] : parent.Split('/');
            foreach (string component in components)
            {
                FileSystemInfo? next = directory.EnumerateFileSystemInfos()
                    .FirstOrDefault(entry => string.Equals(entry.Name, component, StringComparison.Ordinal));
                if (next is null)
                {
                    parentAbsent = true;
                    return null;
                }

                if ((next.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return null;
                }

                if (next is not DirectoryInfo child)
                {
                    parentAbsent = true;
                    return null;
                }

                directory = child;
            }

            return [.. directory.EnumerateFileSystemInfos().Select(entry => entry.Name)];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static bool IsAdmissibleVaultPath(string path) =>
        path.Length > 0
        && path.IndexOfAny(['\\', ':', '\0']) < 0
        && path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));

    private sealed record JournalReplay(
        IReadOnlySet<string> Pins,
        bool PinsChanged,
        IReadOnlyList<SidebarShortcutState> Shortcuts,
        bool ShortcutsChanged,
        IReadOnlyList<string> Recents,
        bool RecentsChanged,
        IReadOnlyList<Guid> Entries,
        IReadOnlyList<string> Notices);
}
