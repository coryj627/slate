// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-5 (#737): the citation summary sheet (mac
/// CitationSummarySheet twin, ⇧⌘J / Ctrl+Shift+J).
///
/// Counting is REFERENCE-derived (feature contract 12):
/// <c>Total</c> is the rendered row count, and <c>Unique</c> counts
/// ordinal-distinct keys across every reference's cited items — so
/// <c>[@a; @b]</c> contributes two and <c>[@a, p. 1]</c> plus
/// <c>[@a, p. 9]</c> contributes one. It never reads
/// <c>BibEntry.Key</c> and never parses raw text.
///
/// Windows has no <c>extractCitationKey</c> fallback (D-8): mac needs
/// one because its refs and rendered lists load independently and can
/// disagree; the Windows leaf publishes both from the same worker in
/// the same publish, so they are structurally 1:1.
/// </summary>
internal sealed class CitationSummaryViewModel
{
    private readonly Action<A11yEvent> _announce;
    private readonly Action _close;

    public CitationSummaryViewModel(
        int total,
        IReadOnlyList<CitationReference> references,
        Action<A11yEvent> announce,
        Action close)
    {
        _announce = announce;
        _close = close;
        Total = total;
        Unique = references
            .SelectMany(reference => reference.Citations)
            .Select(item => item.Key)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    public int Total { get; }

    public int Unique { get; }

    public bool IsEmpty => Total == 0;

    public string Heading => CitationPhrase.SummaryHeading;

    public string Body =>
        IsEmpty ? CitationPhrase.SummaryEmptyBody : CitationPhrase.SummaryLine(Total, Unique);

    /// <summary>The sheet's container name — read on appear, so the
    /// user hears the statistic without navigating to the body. The
    /// sheet itself announces nothing (§2.6).</summary>
    public string AutomationName => CitationPhrase.SummarySheetName(Body);

    public string WalkActionText => CitationPhrase.SummaryWalkAction;

    public string WalkActionHelp => CitationPhrase.SummaryWalkHelp;

    /// <summary>"OK" when there is nothing to walk, "Done" otherwise
    /// — mac's wording split.</summary>
    public string DismissText =>
        IsEmpty ? CitationPhrase.SummaryDismissEmpty : CitationPhrase.SummaryDismiss;

    public string DismissHelp => CitationPhrase.SummaryDismissHelp;

    public bool CanWalkThrough => !IsEmpty;

    /// <summary>Closes the sheet and announces the canonical
    /// walk-through event exactly once (contract 4). Core owns the
    /// wording, including its "sidebar tab" phrasing — forking it
    /// would break §W-D (recorded as D-5).</summary>
    public void WalkThrough()
    {
        if (!CanWalkThrough)
        {
            return;
        }
        _close();
        _announce(new A11yEvent.CitationWalkThrough());
    }
}
