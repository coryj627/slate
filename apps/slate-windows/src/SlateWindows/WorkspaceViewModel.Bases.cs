// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-6 (#738) phase C: the Bases cell-write coordinator — the mac
/// basesApplyProperty funnel's twin, with the Windows route split
/// (contract C8, divergence D-14):
///
/// - target file open in a CLEAN markdown tab → the W4-4 tab write
///   seam (mutation lease bracket, CAS on the tab's saved hash,
///   rebaseline) — the tab's promise that its buffer is never
///   clobbered extends to Bases writes;
/// - open in a DIRTY tab → refuse with the W4-4 sentence (mac writes
///   regardless; recorded divergence D-14);
/// - no open tab → direct session write with NO expected hash (the
///   W4-3 NoOpenTab precedent, matching mac's nil hash).
///
/// One post-write funnel (contract C9): every registered Bases
/// document re-executes; membership changes announce deduped
/// BasesRefreshUpdated; the WRITING document's publish carries the
/// terminal cell outcome (Saved/Cleared/RowNoLongerMatches), exactly
/// one sentence per write.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private int _basesWriteFunnelId;
    private readonly HashSet<string> _basesFunnelAnnouncedSummaries = new(StringComparer.Ordinal);

    private void InstallBaseDocumentSeams(BaseDocumentViewModel document)
    {
        document.ApplyPropertyEdit = (row, column, value) =>
            BasesApplyPropertyEdit(document, row, column, value);
        document.MembershipChanged += OnBaseMembershipChanged;
    }

    private void OnBaseMembershipChanged(int funnelId, string audioSummary)
    {
        // Deduped on the SUMMARY text within one funnel pass (the mac
        // rendered-text dedup): two surfaces over the same source must
        // not speak the same update twice.
        if (funnelId != _basesWriteFunnelId
            || !_basesFunnelAnnouncedSummaries.Add(audioSummary))
        {
            return;
        }
        _announce(new A11yEvent.BasesRefreshUpdated(audioSummary));
    }

    private void BasesApplyPropertyEdit(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        PropertyValue? value)
    {
        // Defence in depth (the mac funnel re-checks too): the surface
        // already refused read-only cells, but every route into a
        // write re-verifies at dispatch (contract C13's backstop).
        if (BaseCellEditPolicy.PropertyKey(column) is not { } key)
        {
            _announce(BaseCellEditPolicy.ReadOnlyEvent(column));
            return;
        }
        WorkspaceTabViewModel? tab = Groups
            .SelectMany(group => group.Tabs)
            .FirstOrDefault(candidate =>
                candidate.IsMarkdown
                && string.Equals(candidate.Path, row.FilePath, StringComparison.Ordinal));
        if (tab is { IsDirty: true })
        {
            // D-14: the Windows tab model promises the unsaved buffer
            // is never clobbered — one sentence, the W4-4 site.
            AnnounceDirtyTabPropertyRefusal(tab);
            return;
        }
        if (tab is not null)
        {
            // The W4-4 seam: lease bracket + CAS + rebaseline live in
            // the tab; the completion routes into the one funnel.
            tab.WriteProperty(
                tab.SavedContentHash,
                expectedHash => value is null
                    ? _session.DeleteProperty(row.FilePath, key, expectedHash)
                    : _session.SetProperty(row.FilePath, key, value, expectedHash),
                (report, failure, _postFailureDiskHash) =>
                {
                    if (failure is not null)
                    {
                        _announce(new A11yEvent.BasesCellEditFailed(failure.Message));
                        return;
                    }
                    if (report is not null)
                    {
                        _ = tab.RebaselineAfterPropertyWrite(
                            report.NewContentHash, _announce);
                    }
                    CompleteBasesWrite(document, row, column, value);
                });
            return;
        }
        // Tabless: direct write, no expected hash (mac parity). Off
        // the dispatcher in production; inline in synchronous tests.
        if (!_startInteractionBackgroundWork)
        {
            BasesTablessWriteBody(document, row, column, key, value);
            return;
        }
        _ = Task.Run(() => BasesTablessWriteBody(document, row, column, key, value));
    }

    private void BasesTablessWriteBody(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        string key,
        PropertyValue? value)
    {
        try
        {
            if (value is null)
            {
                _ = _session.DeleteProperty(row.FilePath, key, expectedContentHash: null);
            }
            else
            {
                _ = _session.SetProperty(row.FilePath, key, value, expectedContentHash: null);
            }
        }
        catch (VaultException failure)
        {
            RunOnDispatcher(() =>
                _announce(new A11yEvent.BasesCellEditFailed(failure.Message)));
            return;
        }
        RunOnDispatcher(() => CompleteBasesWrite(document, row, column, value));
    }

    /// <summary>The one post-write funnel (contract C9): bump the
    /// funnel generation, refresh every registered document, and let
    /// the WRITING document's publish announce the terminal outcome
    /// from its refreshed rows.</summary>
    private void CompleteBasesWrite(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        PropertyValue? value)
    {
        int funnelId = ++_basesWriteFunnelId;
        _basesFunnelAnnouncedSummaries.Clear();
        foreach (BaseDocumentViewModel other in _baseDocuments.Values)
        {
            if (!ReferenceEquals(other, document))
            {
                other.RefreshForFunnel(funnelId);
            }
        }
        document.RefreshForFunnel(
            funnelId,
            onPublished: () => AnnounceBasesCellOutcome(document, row, column, value));
    }

    private void AnnounceBasesCellOutcome(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        PropertyValue? value)
    {
        bool stillPresent = document.Result is { } result
            && result.Rows.Any(candidate =>
                string.Equals(candidate.FilePath, row.FilePath, StringComparison.Ordinal)
                && candidate.TaskOrdinal == row.TaskOrdinal);
        if (!stillPresent)
        {
            _announce(new A11yEvent.BasesCellRowNoLongerMatches());
            return;
        }
        _announce(value is null
            ? new A11yEvent.BasesCellCleared(column.Label)
            : new A11yEvent.BasesCellSaved(
                column.Label, BaseCellEditPolicy.DisplayValue(value)));
    }

    private void RunOnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(action);
            return;
        }
        action();
    }
}
