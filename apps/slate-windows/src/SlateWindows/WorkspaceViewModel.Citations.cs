// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-5 (#737): the citation suite's workspace seam — the sheets, the
/// two chorded commands, and the ONE place in the whole application
/// that calls <c>SetBibliographySources</c>.
///
/// That single call site is feature contract 6: no panel VM, row VM,
/// overlay, command, or context-menu action may write. The suite is
/// otherwise entirely read-only, and a test asserts note bytes are
/// unchanged across a full exercise of both leaves.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private CitationDetailsViewModel? _citationDetails;
    private CitationSummaryViewModel? _citationSummary;
    private FilesCitingViewModel? _filesCiting;
    private System.Windows.Input.ICommand? _openCitationSummaryCommand;
    private System.Windows.Input.ICommand? _jumpToBibliographyCommand;
    private System.Windows.Input.ICommand? _closeCitationDetailsCommand;
    private System.Windows.Input.ICommand? _closeCitationSummaryCommand;
    private System.Windows.Input.ICommand? _closeFilesCitingCommand;
    private bool _bibliographySeeded;

    /// <summary>Completed once SetBibliographySources has landed (or
    /// definitively failed). Both citation leaves gate their background
    /// loads on this, so no render can outrun the sources it needs.
    /// ALWAYS completes — a seeding failure that left this pending
    /// would hang every citation load for the life of the workspace.
    /// </summary>
    private readonly TaskCompletionSource _bibliographySourcesReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Non-null while the details overlay is open.</summary>
    public CitationDetailsViewModel? CitationDetails
    {
        get => _citationDetails;
        private set => SetField(ref _citationDetails, value);
    }

    /// <summary>Non-null while the summary sheet is open.</summary>
    public CitationSummaryViewModel? CitationSummary
    {
        get => _citationSummary;
        private set => SetField(ref _citationSummary, value);
    }

    /// <summary>Non-null while the files-citing sheet is open.</summary>
    public FilesCitingViewModel? FilesCiting
    {
        get => _filesCiting;
        private set => SetField(ref _filesCiting, value);
    }

    public System.Windows.Input.ICommand OpenCitationSummaryCommand =>
        _openCitationSummaryCommand ??= new RelayCommand(
            _ => OpenCitationSummary(), _ => true);

    /// <summary>Ctrl+J. Enabled only while a citation is expanded —
    /// mirroring mac, whose command is disabled when nothing is
    /// expanded (there is no key to jump to otherwise).</summary>
    public System.Windows.Input.ICommand JumpToBibliographyCommand =>
        _jumpToBibliographyCommand ??= new RelayCommand(
            _ => JumpToBibliography(), _ => CitationDetails is not null);

    public System.Windows.Input.ICommand CloseCitationDetailsCommand =>
        _closeCitationDetailsCommand ??= new RelayCommand(
            _ => CitationDetails = null, _ => true);

    public System.Windows.Input.ICommand CloseCitationSummaryCommand =>
        _closeCitationSummaryCommand ??= new RelayCommand(
            _ => CitationSummary = null, _ => true);

    public System.Windows.Input.ICommand CloseFilesCitingCommand =>
        _closeFilesCitingCommand ??= new RelayCommand(
            _ =>
            {
                FilesCiting?.Shutdown();
                FilesCiting = null;
            },
            _ => true);

    /// <summary>Seed the bibliography from the vault's own citation
    /// config, ONCE per vault open, off the UI thread.
    ///
    /// Core's <c>set_bibliography_sources</c> is ALL-OR-NOTHING: one
    /// unreadable source aborts the whole call, so nothing loads.
    /// That is a vault-health condition, not a fatal error — the vault
    /// still opens, the citations leaf still renders placeholders, and
    /// the bibliography leaf shows a notice naming the source
    /// (contract 5). Success and failure are BOTH silent: vault open
    /// must never speak unsolicited bibliography copy (§2.6).</summary>
    internal void SeedBibliographySources(bool synchronousForTests = false)
    {
        if (_bibliographySeeded)
        {
            return;
        }
        _bibliographySeeded = true;

        if (synchronousForTests)
        {
            try
            {
                (var notices, bool hasSources) = ReadAndSeedSources();
                Bibliography.ApplySeedOutcome(notices, hasSources);
            }
            finally
            {
                _ = _bibliographySourcesReady.TrySetResult();
            }
            return;
        }
        // Marshal the outcome back the way the panel schedulers do —
        // captured on the UI thread here, at construction time.
        SynchronizationContext? uiContext = SynchronizationContext.Current;
        _ = Task.Run(() =>
        {
            try
            {
                (var notices, bool hasSources) = ReadAndSeedSources();
                if (uiContext is null)
                {
                    Bibliography.ApplySeedOutcome(notices, hasSources);
                }
                else
                {
                    uiContext.Post(
                        _ => Bibliography.ApplySeedOutcome(notices, hasSources), null);
                }
            }
            finally
            {
                // Release the gate on EVERY path: the sources are as
                // seeded as they are ever going to get, and a pending
                // gate would deadlock both leaves permanently.
                _ = _bibliographySourcesReady.TrySetResult();
            }
        });
    }

    /// <summary>The seed body, shared by the sync and async paths so
    /// the test mode cannot drift from production.</summary>
    private (List<string> Notices, bool HasSources) ReadAndSeedSources()
    {
        var notices = new List<string>();
        bool hasSources = false;
        try
        {
            BibliographySource[] sources = _session.CitationsPrefs().Sources;
            hasSources = sources.Length > 0;
            foreach (BibLoadWarning warning in _session.SetBibliographySources(sources))
            {
                // Verbatim, naming the source — never swallowed.
                notices.Add($"{warning.SourcePath}: {warning.Message}");
            }
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            notices.Add(exception.Message);
        }
        return (notices, hasSources);
    }

    /// <summary>Ctrl+Shift+J (mac ⇧⌘J): the note's citation summary.
    /// Counts come from the leaf's already-published rows and
    /// references — no re-read, so the sheet can never disagree with
    /// the panel behind it (contract 12).</summary>
    internal void OpenCitationSummary()
    {
        CitationSummary = new CitationSummaryViewModel(
            Citations.Rows.Count,
            Citations.References,
            _announce,
            () => CitationSummary = null);
    }

    /// <summary>Open the details overlay for a rendered citation row.
    /// Silent by design (§2.6) — the overlay's name is the speech
    /// surface.</summary>
    internal void OpenCitationDetails(CitationRowViewModel row, object? returnFocusToken = null)
    {
        if (row.Rendered is not { } rendered)
        {
            // A placeholder row has no rendered citation to expand:
            // core never looked one up, so there is nothing honest to
            // show (contract 2).
            return;
        }
        CitationDetails = CitationDetailsViewModel.FromRendered(
            rendered, row.Reference, returnFocusToken);
    }

    /// <summary>Open the details overlay for a bibliography entry.</summary>
    internal void OpenEntryDetails(BibEntry entry, object? returnFocusToken = null) =>
        CitationDetails = CitationDetailsViewModel.FromEntry(entry, returnFocusToken);

    /// <summary>Bibliography row action: which notes cite this key.</summary>
    internal void OpenFilesCiting(
        string key, object? returnFocusToken = null, bool synchronousForTests = false)
    {
        FilesCiting?.Shutdown();
        var sheet = new FilesCitingViewModel(
            _session, key, returnFocusToken, synchronousForTests);
        FilesCiting = sheet;
        sheet.Load();
    }

    /// <summary>Bibliography row action: insert-citation is not
    /// buildable — core exports no citation mutator — so the product
    /// answer IS the announcement, exactly as on mac. The menu item
    /// stays ENABLED so the reason is discoverable rather than a
    /// greyed-out mystery.</summary>
    internal void AnnounceInsertCitationUnavailable() =>
        _announce(new A11yEvent.CitationInsertUnavailable());

    /// <summary>Ctrl+J (mac ⌘J): reveal the bibliography leaf and land
    /// on the expanded citation's key. Announces which of the two
    /// outcomes happened — the suite's ONLY host-composed texts
    /// (A5/A6).</summary>
    internal void JumpToBibliography()
    {
        if (CitationDetails is not { } details || details.EntryKey is not { Length: > 0 } key)
        {
            return;
        }

        ActiveLeaf = Leaves.First(
            leaf => string.Equals(leaf.Id, "bibliography", StringComparison.Ordinal));
        // The outcome depends on entries that may still be loading, so
        // the leaf decides it — immediately when they are already
        // published, at publish time otherwise.
        Bibliography.RequestKeyFocus(
            key,
            // W0.5-3 residue: bibliography-jump message builder, the
            // 1:1 twin of the mac AppState site.
            (jumpedKey, present) => _announce(new A11yEvent.HostComposed(
                present
                    ? CitationPhrase.JumpedToEntry(jumpedKey)
                    : CitationPhrase.SearchingBibliographyFor(jumpedKey),
                A11yPriority.Medium)));
    }
}
