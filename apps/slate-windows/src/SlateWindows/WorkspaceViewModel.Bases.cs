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
    /// <summary>The active tab's Bases document, or null — every
    /// slate.bases.* command gates on this (contract C15); menu items
    /// disable through the shared CanExecute.</summary>
    internal BaseDocumentViewModel? ActiveBaseDocument =>
        ActiveGroup.ActiveTab is { IsBase: true } tab ? tab.Base : null;

    private RelayCommand? _basesRefreshCommand;
    private RelayCommand? _basesWhereAmICommand;
    private RelayCommand? _basesResultsCommand;
    private RelayCommand? _basesNextViewCommand;
    private RelayCommand? _basesPreviousViewCommand;
    private RelayCommand? _basesViewSwitcherCommand;
    private RelayCommand? _basesViewAsTableCommand;
    private RelayCommand? _basesViewAsListCommand;
    private RelayCommand? _basesQuickFilterCommand;
    private RelayCommand? _basesSaveSortToViewCommand;
    private RelayCommand? _basesSortByColumnCommand;
    private RelayCommand? _basesOpenRowCommand;
    private RelayCommand? _basesCopyLinkCommand;
    private RelayCommand? _basesShowBacklinksCommand;
    private RelayCommand? _basesEditPropertyCommand;
    private RelayCommand? _basesExportCsvCommand;
    private RelayCommand? _basesExportMarkdownCommand;
    private RelayCommand? _basesCopyMarkdownCommand;

    public System.Windows.Input.ICommand BasesRefreshCommand =>
        _basesRefreshCommand ??= BasesCommand(document =>
        {
            bool wasFailed = document.State == BaseLoadState.Failed;
            document.Load();
            if (!wasFailed)
            {
                document.AnnounceRefreshed();
            }
        });

    public System.Windows.Input.ICommand BasesWhereAmICommand =>
        _basesWhereAmICommand ??= BasesCommand(document =>
            document.AnnounceEvent(document.WhereAmIEvent()));

    public System.Windows.Input.ICommand BasesResultsCommand =>
        _basesResultsCommand ??= BasesCommand(document =>
        {
            if (document.ResultsPopoverEvent() is { } popover)
            {
                document.AnnounceEvent(popover);
            }
        });

    public System.Windows.Input.ICommand BasesNextViewCommand =>
        _basesNextViewCommand ??= BasesCommand(document =>
            BasesStepView(document, +1));

    public System.Windows.Input.ICommand BasesPreviousViewCommand =>
        _basesPreviousViewCommand ??= BasesCommand(document =>
            BasesStepView(document, -1));

    public System.Windows.Input.ICommand BasesViewSwitcherCommand =>
        _basesViewSwitcherCommand ??= BasesCommand(document =>
            document.AnnounceEvent(
                new A11yEvent.BaseViewSwitcher((uint)document.Views.Count)));

    public System.Windows.Input.ICommand BasesViewAsTableCommand =>
        _basesViewAsTableCommand ??= BasesCommand(document =>
        {
            document.RequestRendererOverride(BaseRendererOverride.Table);
            document.AnnounceEvent(new A11yEvent.BaseViewMode("table"));
        });

    public System.Windows.Input.ICommand BasesViewAsListCommand =>
        _basesViewAsListCommand ??= BasesCommand(document =>
        {
            document.RequestRendererOverride(BaseRendererOverride.List);
            document.AnnounceEvent(new A11yEvent.BaseViewMode("list"));
        });

    public System.Windows.Input.ICommand BasesQuickFilterCommand =>
        _basesQuickFilterCommand ??= BasesCommand(document =>
            document.RequestQuickFilterFocus());

    public System.Windows.Input.ICommand BasesSaveSortToViewCommand =>
        _basesSaveSortToViewCommand ??= BasesCommand(document =>
            document.SaveSortToView());

    public System.Windows.Input.ICommand BasesSortByColumnCommand =>
        _basesSortByColumnCommand ??= BasesCommand(document =>
            document.RequestSortCurrentColumn());

    public System.Windows.Input.ICommand BasesOpenRowCommand =>
        _basesOpenRowCommand ??= BasesRowCommand(BasesOpenRow);

    public System.Windows.Input.ICommand BasesCopyLinkCommand =>
        _basesCopyLinkCommand ??= BasesRowCommand((document, row) =>
        {
            string link = BaseWikilink(row.FilePath);
            try
            {
                System.Windows.Clipboard.SetText(link);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is a shared OS resource another
                // process can hold; a failed copy speaks nothing
                // rather than lying that it copied.
                return;
            }
            document.AnnounceEvent(new A11yEvent.BasesLinkCopied(
                DisplayNameWithoutExtension(row.FilePath)));
        });

    public System.Windows.Input.ICommand BasesShowBacklinksCommand =>
        _basesShowBacklinksCommand ??= BasesRowCommand((document, row) =>
        {
            OpenPath(row.FilePath);
            ActiveLeaf = Leaves.Single(leaf =>
                string.Equals(leaf.Id, "backlinks", StringComparison.Ordinal));
            IsRightPaneVisible = true;
            document.AnnounceEvent(new A11yEvent.BasesBacklinksFor(
                DisplayNameWithoutExtension(row.FilePath)));
        });

    public System.Windows.Input.ICommand BasesEditPropertyCommand =>
        _basesEditPropertyCommand ??= BasesCommand(document =>
            document.RequestEditSelectedProperty());

    public System.Windows.Input.ICommand BasesExportCsvCommand =>
        _basesExportCsvCommand ??= BasesCommand(document =>
            BasesDeliverExport(document, ExportFormat.Csv));

    public System.Windows.Input.ICommand BasesExportMarkdownCommand =>
        _basesExportMarkdownCommand ??= BasesCommand(document =>
            BasesDeliverExport(document, ExportFormat.Markdown));

    public System.Windows.Input.ICommand BasesCopyMarkdownCommand =>
        _basesCopyMarkdownCommand ??= BasesCommand(document =>
        {
            if (document.ExportText(ExportFormat.Markdown) is not { } text)
            {
                return;
            }
            try
            {
                System.Windows.Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.ExternalException failure)
            {
                document.AnnounceEvent(
                    new A11yEvent.BasesViewCopyFailed(failure.Message));
                return;
            }
            document.AnnounceEvent(new A11yEvent.BasesViewCopiedAsMarkdown());
        });

    private RelayCommand BasesCommand(Action<BaseDocumentViewModel> body) =>
        new(
            _ =>
            {
                if (ActiveBaseDocument is { } document)
                {
                    body(document);
                }
            },
            _ => ActiveBaseDocument is not null);

    private RelayCommand BasesRowCommand(
        Action<BaseDocumentViewModel, BasesRow> body) =>
        new(
            _ =>
            {
                if (ActiveBaseDocument is not { } document)
                {
                    return;
                }
                if (document.SelectedRow is not { } row)
                {
                    document.AnnounceEvent(new A11yEvent.BasesRowSelectionNeeded());
                    return;
                }
                body(document, row);
            },
            _ => ActiveBaseDocument is not null);

    private void BasesStepView(BaseDocumentViewModel document, int delta)
    {
        int target = Math.Clamp(
            document.ActiveViewIndex + delta,
            0,
            Math.Max(0, document.Views.Count - 1));
        if (target == document.ActiveViewIndex)
        {
            return;
        }
        document.SelectView(target);
        if (document.ActiveViewName is { } name)
        {
            document.AnnounceViewSelected(name);
        }
    }

    internal void BasesOpenRow(BaseDocumentViewModel document, BasesRow row)
    {
        OpenPath(row.FilePath);
        // Task rows open their FILE here; mac lands on the task's line
        // via its task artifact. The announcement stays honest about
        // what happened (OpenedFile, not OpenedAtLine).
        document.AnnounceEvent(new A11yEvent.OpenedFile(
            System.IO.Path.GetFileName(row.FilePath)));
    }

    /// <summary>Mac's baseWikilink verbatim: the path without its
    /// extension, wrapped.</summary>
    internal static string BaseWikilink(string path)
    {
        int dot = path.LastIndexOf('.');
        int slash = path.LastIndexOf('/');
        string target = dot > slash ? path[..dot] : path;
        return "[[" + target + "]]";
    }

    private static string DisplayNameWithoutExtension(string path) =>
        System.IO.Path.GetFileNameWithoutExtension(path);

    /// <summary>Export delivery (contract C14): core's bytes to a save
    /// panel; success/failure announce the canonical events.</summary>
    private void BasesDeliverExport(
        BaseDocumentViewModel document, ExportFormat format)
    {
        if (document.ExportText(format) is not { } text)
        {
            return;
        }
        string extension = format == ExportFormat.Csv ? "csv" : "md";
        string viewName = document.ActiveViewName ?? "View";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = document.DisplayName + " - " + viewName + "." + extension,
            DefaultExt = extension,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            System.IO.File.WriteAllText(dialog.FileName, text);
        }
        catch (Exception failure) when (failure is System.IO.IOException
            or UnauthorizedAccessException)
        {
            document.AnnounceEvent(
                new A11yEvent.BasesViewExportFailed(failure.Message));
            return;
        }
        document.AnnounceEvent(new A11yEvent.BasesViewExported());
    }

    internal void RaiseBasesCommandStates()
    {
        foreach (RelayCommand? command in new[]
        {
            _basesRefreshCommand, _basesWhereAmICommand, _basesResultsCommand,
            _basesNextViewCommand, _basesPreviousViewCommand,
            _basesViewSwitcherCommand, _basesViewAsTableCommand,
            _basesViewAsListCommand, _basesQuickFilterCommand,
            _basesSaveSortToViewCommand, _basesSortByColumnCommand,
            _basesOpenRowCommand, _basesCopyLinkCommand,
            _basesShowBacklinksCommand, _basesEditPropertyCommand,
            _basesExportCsvCommand, _basesExportMarkdownCommand,
            _basesCopyMarkdownCommand,
        })
        {
            command?.RaiseCanExecuteChanged();
        }
    }

    private int _basesWriteFunnelId;
    private readonly HashSet<string> _basesFunnelAnnouncedSummaries = new(StringComparer.Ordinal);

    private void InstallBaseDocumentSeams(BaseDocumentViewModel document)
    {
        document.ApplyPropertyEdit = (row, column, value) =>
            BasesApplyPropertyEdit(document, row, column, value);
        document.MembershipChanged += OnBaseMembershipChanged;
        document.OpenRowFromSurface = row => BasesOpenRow(document, row);
        document.CopyLinkFromSurface = row =>
            ((RelayCommand)BasesCopyLinkCommand).Execute(RowOverride(document, row));
        document.ShowBacklinksFromSurface = row =>
        {
            OpenPath(row.FilePath);
            ActiveLeaf = Leaves.Single(leaf =>
                string.Equals(leaf.Id, "backlinks", StringComparison.Ordinal));
            IsRightPaneVisible = true;
            document.AnnounceEvent(new A11yEvent.BasesBacklinksFor(
                DisplayNameWithoutExtension(row.FilePath)));
        };
    }

    private static object RowOverride(BaseDocumentViewModel document, BasesRow row)
    {
        document.SelectedRow = row;
        return row;
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
