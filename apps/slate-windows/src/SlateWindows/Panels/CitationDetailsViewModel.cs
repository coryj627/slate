// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>One labelled row in the details overlay. Absent values
/// never produce a row at all (feature contract 10).</summary>
internal sealed record CitationField(string Label, string Value, string? LinkTarget = null)
{
    /// <summary>The spoken form; the visible caption is
    /// presentation-only.</summary>
    public string AutomationName => CitationPhrase.FieldSpoken(Label, Value);

    /// <summary>DOI and URL are followable; every other field is
    /// plain text.</summary>
    public bool IsLink => LinkTarget is { Length: > 0 };
}

/// <summary>
/// W4-5 (#737): the shared citation details overlay — the mac
/// CitationPopover twin, used from BOTH the citations leaf (opened
/// from a rendered citation) and the bibliography leaf (opened from
/// an entry), exactly as mac uses one view twice.
///
/// Field order is mac's verbatim (CitationPopover.swift:74-134):
/// Title → Authors → Year → Journal → Publisher → DOI → URL →
/// Abstract. Every field is CONDITIONAL: an absent year, journal,
/// publisher, DOI, URL, or abstract omits its row entirely rather
/// than rendering "0", an empty value, or a hollow disclosure
/// (contract 10).
///
/// Nothing here re-derives citation speech: values come from
/// <see cref="BibEntry"/> members, never from parsing
/// <c>VisualText</c> or <c>RawCslJson</c> (contract 1).
/// </summary>
internal sealed class CitationDetailsViewModel : BindableBase
{
    private bool _abstractExpanded;

    private CitationDetailsViewModel(
        IReadOnlyList<CitationField> fields,
        string? abstractText,
        bool isUnresolved,
        string unresolvedKey,
        string entryKey,
        string automationName,
        object? returnFocusToken)
    {
        Fields = fields;
        AbstractText = abstractText;
        IsUnresolved = isUnresolved;
        UnresolvedKey = unresolvedKey;
        EntryKey = entryKey;
        AutomationName = automationName;
        ReturnFocusToken = returnFocusToken;
    }

    /// <summary>The citation KEY this expansion is about — the
    /// bibliography's row identity, so Jump to Bibliography has
    /// something exact to land on. Resolved expansions carry the
    /// matched entry's key; unresolved ones carry the key core
    /// parsed. Never derived from display text.</summary>
    public string EntryKey { get; }

    public IReadOnlyList<CitationField> Fields { get; }

    /// <summary>Null when the entry carries no abstract — which is the
    /// NORMAL case on the DB read path, where core hardcodes it
    /// absent. An absent abstract renders as absence, never as an
    /// empty expander (contract 10).</summary>
    public string? AbstractText { get; }

    public bool HasAbstract => AbstractText is { Length: > 0 };

    public bool IsUnresolved { get; }

    public string UnresolvedKey { get; }

    /// <summary>The overlay's container name — the speech surface.
    /// Opening the overlay announces NOTHING; this label is what AT
    /// reads on focus (§2.6).</summary>
    public string AutomationName { get; }

    /// <summary>Identity of the row that opened this overlay, so
    /// Escape can return focus exactly there (contract 11).</summary>
    /// <summary>
    /// The element Escape must return focus to (contract 11). This is
    /// PER-SHEET on purpose: the window previously restored from a
    /// single shared field, so closing an inner sheet put focus behind
    /// a still-open outer one and cleared the slot, leaving the outer
    /// sheet with nothing to restore.
    /// </summary>
    public object? ReturnFocusToken { get; private set; }

    /// <summary>Give up the focus return — used when the sheet is
    /// closed as part of a jump that is itself moving focus somewhere
    /// else, so the two do not fight over it.</summary>
    internal void SuppressFocusReturn() => ReturnFocusToken = null;

    public string UnresolvedHeading => CitationPhrase.DetailsUnresolvedHeading;

    public string UnresolvedBody => CitationPhrase.DetailsUnresolvedBody(UnresolvedKey);

    public string CloseText => CitationPhrase.DetailsClose;

    public string CloseHelp => CitationPhrase.DetailsCloseHelp;

    public bool AbstractExpanded
    {
        get => _abstractExpanded;
        set
        {
            if (SetField(ref _abstractExpanded, value))
            {
                OnPropertyChanged(nameof(AbstractAutomationName));
            }
        }
    }

    public string AbstractAutomationName =>
        _abstractExpanded && AbstractText is { } text
            ? CitationPhrase.AbstractSpoken(text)
            : CitationPhrase.AbstractCollapsedSpoken;

    /// <summary>Open from a RENDERED citation. An unresolved citation
    /// shows the not-found panel; the key comes from the reference's
    /// cited items — core's own parse — rather than re-parsing the
    /// raw text (mac scrapes `raw` because its popover has no
    /// reference to hand; Windows does, so it uses it).</summary>
    public static CitationDetailsViewModel FromRendered(
        RenderedCitation rendered,
        CitationReference reference,
        object? returnFocusToken = null)
    {
        if (rendered.BibEntry is { } entry)
        {
            return new CitationDetailsViewModel(
                BuildFields(entry),
                entry.AbstractText,
                isUnresolved: false,
                unresolvedKey: "",
                entryKey: entry.Key,
                automationName: CitationPhrase.DetailsSummary(entry.Title),
                returnFocusToken);
        }
        string key = reference.Citations.Length > 0
            ? reference.Citations[0].Key
            : reference.Raw;
        return new CitationDetailsViewModel(
            [],
            abstractText: null,
            isUnresolved: true,
            unresolvedKey: key,
            entryKey: key,
            automationName: CitationPhrase.DetailsUnresolvedSpoken(key),
            returnFocusToken);
    }

    /// <summary>Open from a bibliography ENTRY (mac
    /// CitationPopover.init(entry:)). Always resolved by
    /// construction — the entry is the bibliography record.</summary>
    public static CitationDetailsViewModel FromEntry(
        BibEntry entry, object? returnFocusToken = null) =>
        new(
            BuildFields(entry),
            entry.AbstractText,
            isUnresolved: false,
            unresolvedKey: "",
            entryKey: entry.Key,
            automationName: CitationPhrase.DetailsSummary(entry.Title),
            returnFocusToken);

    /// <summary>Mac's field order, with every optional row omitted
    /// when absent.</summary>
    private static List<CitationField> BuildFields(BibEntry entry)
    {
        var fields = new List<CitationField>(7);
        if (!string.IsNullOrEmpty(entry.Title))
        {
            fields.Add(new CitationField(CitationPhrase.FieldTitle, entry.Title));
        }
        if (entry.Authors.Length > 0)
        {
            fields.Add(new CitationField(
                CitationPhrase.FieldAuthors, CitationPhrase.AuthorList(entry.Authors)));
        }
        if (CitationPhrase.YearText(entry.Year) is { } year)
        {
            fields.Add(new CitationField(CitationPhrase.FieldYear, year));
        }
        if (entry.Journal is { Length: > 0 } journal)
        {
            fields.Add(new CitationField(CitationPhrase.FieldJournal, journal));
        }
        if (entry.Publisher is { Length: > 0 } publisher)
        {
            fields.Add(new CitationField(CitationPhrase.FieldPublisher, publisher));
        }
        if (entry.Doi is { Length: > 0 } doi)
        {
            fields.Add(new CitationField(
                CitationPhrase.FieldDoi, doi, CitationPhrase.DoiTarget(doi)));
        }
        if (entry.Url is { Length: > 0 } url)
        {
            fields.Add(new CitationField(CitationPhrase.FieldUrl, url, url));
        }
        return fields;
    }
}
