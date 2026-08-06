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

    /// <summary>The terminal seed outcome both citation leaves wait on
    /// and BRANCH on. A bare "ready" gate said only when seeding
    /// finished; the leaves also need to know whether it succeeded,
    /// because a failed seed must stop them reading core rather than
    /// merely let them through late (see
    /// <see cref="BibliographySeedOutcome.MayReadEntries"/>).</summary>
    private readonly BibliographySeed _bibliographySeed = new();

    /// <summary>The seeding task itself, kept rather than discarded so
    /// teardown and tests can observe it. It was fire-and-forget, which
    /// left the application's only write with no handle at all.
    /// </summary>
    private Task? _seedWork;

    /// <summary>The shell launcher the panels VM already allowlists
    /// through; shared so citation links cannot drift from link rows.
    /// </summary>
    private readonly Func<string, bool> _externalOpener;

    internal Task SeedWorkForTests => _seedWork ?? Task.CompletedTask;

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
            BibliographySeedOutcome outcome = ReadAndSeedSources();
            // Settle BEFORE publishing: a leaf that reacts to the
            // publish must never observe an unsettled seed.
            _bibliographySeed.Complete(outcome);
            Bibliography.ApplySeedOutcome(outcome);
            return;
        }
        // Marshal the outcome back the way the panel schedulers do —
        // captured on the UI thread here, at construction time.
        SynchronizationContext? uiContext = SynchronizationContext.Current;
        _seedWork = Task.Run(() =>
        {
            // Teardown can settle the seed as Cancelled while this body
            // is still queued. SetBibliographySources is the ONLY write
            // this application performs (contract 6); starting it into
            // a vault that is closing leaves a completed write behind a
            // disposed workspace. The window between this check and the
            // call is closed by uniffi's call counter, which throws
            // rather than touching freed memory.
            if (_bibliographySeed.Outcome is { Status: BibliographySeedStatus.Cancelled })
            {
                return;
            }
            BibliographySeedOutcomeHolder holder = default;
            try
            {
                holder = new BibliographySeedOutcomeHolder(ReadAndSeedSources());
            }
            finally
            {
                // Settle AND publish on EVERY path. Settling alone left
                // a fatal-exception path where the seed said Failed but
                // ApplySeedOutcome never ran, because the exception
                // propagated past the publish below — the leaf then
                // showed "0 entries" with no notice, no error and no
                // "no sources" state: contract 5 satisfied on the
                // ordinary failure path and silently broken on this one.
                _bibliographySeed.Complete(
                    holder.Outcome
                        ?? new BibliographySeedOutcome(
                            BibliographySeedStatus.Failed, []));
                BibliographySeedOutcome settled = _bibliographySeed.Outcome!;
                if (uiContext is null)
                {
                    Bibliography.ApplySeedOutcome(settled);
                }
                else
                {
                    uiContext.Post(_ => Bibliography.ApplySeedOutcome(settled), null);
                }
            }
        });
    }

    /// <summary>Lets the finally block distinguish "the body produced
    /// an outcome" from "the body died before producing one" without
    /// catching fatal exceptions it has no business handling.</summary>
    private readonly struct BibliographySeedOutcomeHolder(BibliographySeedOutcome outcome)
    {
        public BibliographySeedOutcome? Outcome { get; } = outcome;
    }

    /// <summary>The seed body, shared by the sync and async paths so
    /// the test mode cannot drift from production.</summary>
    /// <summary>Fires inside the seed body, so a test can observe WHICH
    /// THREAD the vault's only write ran on. The same shape as the
    /// panels' InterleaveForTests hook.</summary>
    internal Action? SeedInterleaveForTests { get; set; }

    private BibliographySeedOutcome ReadAndSeedSources()
    {
        SeedInterleaveForTests?.Invoke();
        var notices = new List<string>();
        try
        {
            BibliographySource[] sources = _session.CitationsPrefs().Sources;
            if (sources.Length == 0)
            {
                return new BibliographySeedOutcome(
                    BibliographySeedStatus.NoSources, notices);
            }
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
            // Core's set_bibliography_sources is ALL-OR-NOTHING: it
            // returned before replacing anything, so the previous
            // session's entries and index are still live. Failed says
            // the leaves must not read them (D-13).
            return new BibliographySeedOutcome(
                BibliographySeedStatus.Failed, notices);
        }
        return new BibliographySeedOutcome(BibliographySeedStatus.Seeded, notices);
    }

    /// <summary>Ctrl+Shift+J (mac ⇧⌘J): the note's citation summary.
    /// Counts come from the leaf's already-published rows and
    /// references — no re-read, so the sheet can never disagree with
    /// the panel behind it (contract 12).
    ///
    /// Refused while the leaf is still loading: the sheet's own name is
    /// read on appear, so opening early announced "Citation Summary.
    /// This note has no citations." for a note that has eight, with the
    /// walk-through disabled and no re-read to correct it. Citation
    /// loads are gated on the seed, so the pre-publish window is a
    /// perfectly ordinary one at startup.</summary>
    internal void OpenCitationSummary()
    {
        if (Citations.IsLoading)
        {
            // DEFER, never drop: a keypress that produces nothing reads
            // as a dead key, which is the same failure the Ctrl+J
            // design forbids. One-shot — a second press while parked
            // replaces nothing because the handler detaches itself.
            // Safe after teardown: publishes are guarded by IsShutDown,
            // so a workspace that dies while parked simply never fires.
            void OpenWhenPublished(object? sender, EventArgs args)
            {
                Citations.RowsPublished -= OpenWhenPublished;
                OpenCitationSummary();
            }
            Citations.RowsPublished -= OpenWhenPublished;
            Citations.RowsPublished += OpenWhenPublished;
            return;
        }
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

    private System.Windows.Input.ICommand? _openCitationLinkCommand;

    /// <summary>
    /// Open a citation field's target — the DOI or URL of an expanded
    /// entry. `CitationField.LinkTarget` was populated for both and
    /// rendered as inert text, so a DOI could be read but never
    /// followed; mac renders both as real links
    /// (CitationPopover.swift:139-152). Routed through the same
    /// http/https/mailto allowlist the W4-2 panels use, so a hostile
    /// `.bib` cannot smuggle a `file:` or `javascript:` target in.
    /// </summary>
    public System.Windows.Input.ICommand OpenCitationLinkCommand =>
        _openCitationLinkCommand ??= new RelayCommand(
            parameter => OpenCitationLink(parameter as string),
            parameter => parameter is string { Length: > 0 });

    private void OpenCitationLink(string? target)
    {
        if (target is not { Length: > 0 })
        {
            return;
        }
        bool allowed = Uri.TryCreate(target, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https" or "mailto";
        if (!allowed)
        {
            _announce(new A11yEvent.ExternalLinkUnsupported(target));
            return;
        }
        _announce(_externalOpener(target)
            ? new A11yEvent.ExternalLinkOpened()
            : new A11yEvent.ExternalLinkFailed(target));
    }

    private System.Windows.Input.ICommand? _reloadBibliographyCommand;

    /// <summary>
    /// Re-seed the sources and reload both segments.
    ///
    /// Seeding was once-per-vault-open and <c>ForceReload</c> had no
    /// caller at all, so the whole recovery story was "close the vault
    /// and open it again": a user who saw "library.bib: no such file",
    /// fixed the path in slate.json, and came back had no way to retry.
    /// Re-seeding is what makes the retry real — reloading alone would
    /// re-read the same stale index.
    /// </summary>
    /// <summary>Refused while the initial seed is still in flight, and
    /// while a previous reload is running. Two concurrent
    /// SetBibliographySources calls commit their DB write and their
    /// index rebuild under SEPARATE locks, so they can land in opposite
    /// orders and leave core's table describing one attempt while its
    /// in-memory index describes the other — the two citation surfaces
    /// would then disagree about whether an entry exists.</summary>
    public System.Windows.Input.ICommand ReloadBibliographyCommand =>
        _reloadBibliographyCommand ??= new RelayCommand(
            _ => ReloadBibliography(),
            _ => _bibliographySeed.Outcome is not null && !_reloadInFlight);

    private bool _reloadInFlight;

    internal void ReloadBibliography()
    {
        if (_reloadInFlight)
        {
            return;
        }
        _reloadInFlight = true;

        void Apply(BibliographySeedOutcome outcome)
        {
            // Complete() is first-settle-wins, so on a retry the
            // ORIGINAL outcome is what the gate holds. The leaves need
            // the new one, which is what OverrideSeedOutcome carries.
            _bibliographySeed.Complete(outcome);
            Bibliography.ApplySeedOutcome(outcome);
            Bibliography.OverrideSeedOutcome(outcome);
            Citations.OverrideSeedOutcome(outcome);
            Bibliography.ForceReload();
            Citations.Refresh();
            _reloadInFlight = false;
        }

        if (!_startInteractionBackgroundWork)
        {
            Apply(ReadAndSeedSources());
            return;
        }
        // OFF the dispatcher. ReadAndSeedSources parses every .bib
        // source and rewrites the whole bibliography table under core's
        // connection mutex — core sizes that at "<10k entries
        // typically". Run inline from a menu command it froze the
        // window for the duration, which also freezes UIA, so the
        // screen reader goes silent too. Every other call of this in
        // the suite is deliberately scheduled; this one was not.
        SynchronizationContext? uiContext = SynchronizationContext.Current;
        _seedWork = Task.Run(() =>
        {
            BibliographySeedOutcome outcome = ReadAndSeedSources();
            if (uiContext is null)
            {
                Apply(outcome);
            }
            else
            {
                uiContext.Post(_ => Apply(outcome), null);
            }
        });
    }

    /// <summary>
    /// The ONE post-save funnel every note-scoped surface hangs off.
    ///
    /// There were ten call sites, all of them refreshing the link and
    /// task panels directly, and adding a surface meant remembering all
    /// ten. Citations was added and not remembered — so a save never
    /// updated it. Routing them through here means the next surface is
    /// wired once, not ten times.
    /// </summary>
    private void NotePersisted(string path)
    {
        Panels.NoteSaved(path);
        Citations.NoteSaved(path);
    }

    /// <summary>Bibliography row action: which notes cite this key.</summary>
    internal void OpenFilesCiting(string key, object? returnFocusToken = null)
    {
        FilesCiting?.Shutdown();
        // Derived from the workspace's own mode, not passed in. As a
        // parameter every caller had to remember it, and a test-mode
        // workspace that forgot got a sheet whose worker mutated an
        // ObservableCollection off-thread while the test read it.
        var sheet = new FilesCitingViewModel(
            _session, key, returnFocusToken,
            synchronousForTests: !_startInteractionBackgroundWork);
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

        // Mirror mac (AppState.swift:12307-12322), which does all of
        // this before announcing. Skipping any of it produced a
        // CONFIDENT LIE: the announcement said "Jumped to bibliography
        // entry" while focus never moved, because the leaf was hidden,
        // or showing the other segment, or filtered so the target was
        // not in the bound rows FocusRow scans.
        IsRightPaneVisible = true;
        Bibliography.Segment = BibliographySegment.Entries;
        // mac sets the search box to the key. That is not cosmetic: it
        // guarantees the target survives the filter, so the row the
        // announcement promises is the row that can be focused.
        Bibliography.SearchText = key;
        // mac clears expandedCitation. The sheet is IsDialog and
        // focus-trapped, so leaving it open would strand the user
        // typing into a grid behind a modal that still claims focus.
        details.SuppressFocusReturn();
        CitationDetails = null;

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
