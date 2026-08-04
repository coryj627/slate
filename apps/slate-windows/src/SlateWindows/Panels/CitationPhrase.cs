// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-5 (#737): the citation label vocabulary — the mac
/// CitationsPanel / BibliographyPanel / CitationPopover strings,
/// host-duplicated by designation (§W-C label goldens: each platform
/// pins the identical verbatim strings in its own unit tests;
/// recorded in w_c_matrix.md).
///
/// What is NOT here matters as much as what is: a rendered
/// citation's SPOKEN text is core's <c>RenderedCitation.SpeechText</c>
/// verbatim (feature contract 1). This class never composes speech
/// for a rendered citation — only the surrounding chrome (counts,
/// field labels, empty states) and the PLACEHOLDER used when no
/// style is configured and core rendered nothing.
///
/// The <c>CitationMode</c> set is OPEN: every switch carries a
/// pass-through arm so a new core mode degrades to an honest label
/// instead of throwing. Nothing here throws — these run at row
/// publish time on the UI thread.
/// </summary>
internal static class CitationPhrase
{
    // --- Counting (mac CountCopy.counted twin) ---

    /// <summary>"1 citation" / "3 citations" — the singular/plural
    /// pair is supplied so callers read at the call site.</summary>
    public static string Counted(int n, string singular, string plural) =>
        string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? singular : plural)}");

    // --- Citations leaf (mac CitationsPanel.swift) ---

    public const string CitationsHeading = "Citations";
    public const string CitationsNoFile = "Select a file to see its citations.";

    /// <summary>Visible loading copy keeps the ellipsis; the spoken
    /// form uses a period. The divergence is mac's and is preserved
    /// deliberately.</summary>
    public const string CitationsLoadingVisible = "Loading citations…";
    public const string CitationsLoadingSpoken = "Loading citations.";

    public const string CitationsErrorHeading = "Citations couldn't be loaded";
    public const string CitationsEmpty = "This note has no citations.";
    public const string UnresolvedBadge = "Unresolved";
    public const string CitationRowHelp = "Activate to expand citation fields.";

    public static string CitationsErrorSpoken(string message) =>
        $"{CitationsErrorHeading}. {message}";

    /// <summary>An unresolved row's spoken name: the badge is spoken
    /// as a PREFIX so the state precedes the content, then core's
    /// speech verbatim (contract 1's single sanctioned prefix).</summary>
    public static string UnresolvedRowSpeech(string speechText) =>
        $"Unresolved citation key. {speechText}";

    /// <summary>The row text shown when core rendered nothing — no
    /// style configured, or the render failed. Mirrors mac
    /// `placeholderRendered`: a single in-text item speaks the bare
    /// key, any other single item is prefixed, and multiple items are
    /// joined. Never invents a style or a bibliography match.</summary>
    public static string PlaceholderSpeech(CitationReference reference)
    {
        var items = reference.Citations;
        if (items.Length == 0)
        {
            return reference.Raw;
        }
        if (items.Length == 1)
        {
            return items[0].Mode == CitationMode.InText
                ? items[0].Key
                : $"Citation: {items[0].Key}";
        }
        return $"Citation: {string.Join(", ", items.Select(item => item.Key))}";
    }

    // --- Bibliography leaf (mac BibliographyPanel.swift) ---

    public const string BibliographyHeading = "Bibliography";
    public const string BibliographySegmentName = "Bibliography view";
    public const string SegmentEntries = "Entries";
    public const string SegmentUnresolved = "Unresolved";

    public const string BibliographyNoVault = "Open a vault to see its bibliography.";
    public const string BibliographyLoading = "Loading bibliography…";
    public const string BibliographyErrorHeading = "Bibliography couldn't be loaded";

    public static string BibliographyErrorSpoken(string message) =>
        $"{BibliographyErrorHeading}. {message}";

    /// <summary>RECORDED DIVERGENCE D-2: mac says "Open Settings →
    /// Bibliography to add one." Windows has no bibliography settings
    /// pane until W8-1, and pointing a screen-reader user at a menu
    /// that does not exist is an accessibility lie. Flip to the mac
    /// string when W8-1 ships the pane.</summary>
    public const string BibliographyNoSources =
        "No bibliography sources configured. "
        + "Add a \"citations\" section to the vault's slate.json.";

    public static string BibliographyNoFilterHits(string query) =>
        $"No entries match '{query}'.";

    public const string BibliographySearchPlaceholder = "Search title, author, key…";
    public const string BibliographySearchName = "Search bibliography";
    public const string BibliographySearchHelp =
        "Filters entries by title, author family name, or citation key.";

    public const string UnresolvedNoVault = "Open a vault to see unresolved citations.";
    public const string UnresolvedLoading = "Loading unresolved citations…";
    public const string UnresolvedEmpty =
        "No unresolved citations. Every key in your notes has a bibliography entry.";

    public const string ShowFilesCitingAction = "Show files citing this entry";

    /// <summary>Stays ENABLED and announces its unavailability — the
    /// product answer is the announcement, exactly as on mac (O1).</summary>
    public const string InsertCitationAction = "Insert citation in current note (V1.x)";

    /// <summary>WINDOWS ADDITION D-3: the entry grid is capped, and the
    /// cap is SPOKEN in the grid summary so nothing is silently
    /// hidden (contract 9).</summary>
    public static string TruncationNotice(int shown, int total) =>
        string.Create(
            CultureInfo.InvariantCulture, $"Showing the first {shown} of {total} entries.");

    // --- Entry presentation ---

    /// <summary>"{title} ({year})" / "{title}", falling back to the
    /// key when the entry has no title.</summary>
    public static string EntryTitleLine(BibEntry entry)
    {
        string title = string.IsNullOrEmpty(entry.Title) ? entry.Key : entry.Title;
        return entry.Year is int year
            ? string.Create(CultureInfo.InvariantCulture, $"{title} ({year})")
            : title;
    }

    /// <summary>Up to three author families, ", et al." beyond that,
    /// with the journal appended after an em dash. Journal alone when
    /// there are no authors; the key when there is neither.</summary>
    public static string EntrySubtitle(BibEntry entry)
    {
        string authors = entry.Authors.Length switch
        {
            0 => "",
            <= 3 => string.Join(", ", entry.Authors.Select(a => a.Family)),
            _ => string.Join(", ", entry.Authors.Take(3).Select(a => a.Family)) + ", et al.",
        };
        string journal = entry.Journal ?? "";
        if (authors.Length > 0 && journal.Length > 0)
        {
            return $"{authors} — {journal}";
        }
        if (authors.Length > 0)
        {
            return authors;
        }
        return journal.Length > 0 ? journal : entry.Key;
    }

    /// <summary>"{family}, {given}" joined with "; "; family alone
    /// when the given name is absent.</summary>
    public static string AuthorList(IReadOnlyList<Author> authors) =>
        string.Join(
            "; ",
            authors.Select(a => a.Given is { Length: > 0 } given ? $"{a.Family}, {given}" : a.Family));

    /// <summary>The grid row's audio description — present parts
    /// only, single-space joined. Absent fields are OMITTED, never
    /// rendered as an empty or zero value (contract 10).</summary>
    public static string EntryRowDescription(BibEntry entry)
    {
        var parts = new List<string>(5);
        // Title is ALWAYS present, falling back to the key — mac
        // BibliographyPanel.swift:243-244. Only authors/year/journal
        // are conditional.
        parts.Add($"Title: {(string.IsNullOrEmpty(entry.Title) ? entry.Key : entry.Title)}.");
        if (entry.Authors.Length > 0)
        {
            parts.Add($"Authors: {AuthorList(entry.Authors)}.");
        }
        if (YearText(entry.Year) is { } year)
        {
            parts.Add($"Year: {year}.");
        }
        if (!string.IsNullOrEmpty(entry.Journal))
        {
            parts.Add($"Journal: {entry.Journal}.");
        }
        parts.Add($"Key: {entry.Key}.");
        return string.Join(" ", parts);
    }

    /// <summary>Null for an absent year — never "0", never a throw
    /// (the TaskStatusPhrase degradation posture).</summary>
    public static string? YearText(int? year) =>
        year is int value ? value.ToString(CultureInfo.InvariantCulture) : null;

    public static string UnresolvedRowLabel(string key, string path) =>
        $"Unresolved key: {key} in {Slash(path)}.";

    // --- Citation details overlay (mac CitationPopover.swift) ---

    public const string FieldTitle = "Title";
    public const string FieldAuthors = "Authors";
    public const string FieldYear = "Year";
    public const string FieldJournal = "Journal";
    public const string FieldPublisher = "Publisher";
    public const string FieldDoi = "DOI";
    public const string FieldUrl = "URL";
    public const string FieldAbstract = "Abstract";

    /// <summary>The spoken form of a field row; the visible caption is
    /// presentation-only.</summary>
    public static string FieldSpoken(string label, string value) => $"{label}: {value}";

    public static string DoiTarget(string doi) => $"https://doi.org/{doi}";

    public static string AbstractSpoken(string text) => $"Abstract: {text}";
    public const string AbstractCollapsedSpoken = "Abstract, collapsed.";

    public const string DetailsUnresolvedHeading = "Citation key not found";

    public static string DetailsUnresolvedBody(string key) =>
        $"'{key}' isn't in any bibliography source.";

    /// <summary>Visible body and spoken body differ deliberately (mac
    /// parity): the spoken form names the state first and uses "This
    /// key" where the visible text quotes it.</summary>
    public static string DetailsUnresolvedSpoken(string key) =>
        $"Unresolved citation: {key}. This key isn't in any bibliography source.";

    public const string DetailsClose = "Close";
    public const string DetailsCloseHelp = "Close the expanded citation.";

    /// <summary>Container name for a RESOLVED expansion.</summary>
    public static string DetailsSummary(string? title) =>
        string.IsNullOrEmpty(title) ? "Citation expanded." : $"Citation expanded. Title: {title}.";

    /// <summary>Speech synthesized for details opened from a
    /// bibliography ENTRY rather than a rendered citation (mac
    /// CitationPopover.init(entry:)) — the only place this class
    /// composes citation-shaped speech, and it never touches a
    /// rendered citation.</summary>
    public static string EntryDetailsSpeech(BibEntry entry) =>
        entry.Year is int year
            ? string.Create(CultureInfo.InvariantCulture, $"Citation: {entry.Title} {year}")
            : $"Citation: {entry.Title}";

    // --- Citation summary sheet (mac CitationSummarySheet.swift) ---

    public const string SummaryHeading = "Citation Summary";
    public const string SummaryEmptyBody = "This note has no citations.";

    public static string SummaryLine(int total, int unique) =>
        $"This note has {Counted(total, "citation", "citations")} referencing "
        + $"{Counted(unique, "unique source", "unique sources")}.";

    public const string SummaryWalkAction = "Walk through citations";
    public const string SummaryWalkHelp = "Closes this sheet and starts a walk-through.";
    public const string SummaryDismissEmpty = "OK";
    public const string SummaryDismiss = "Done";
    public const string SummaryDismissHelp = "Close the citation summary.";

    public static string SummarySheetName(string body) => $"{SummaryHeading}. {body}";

    // --- Files-citing sheet ---

    public const string FilesCitingHeading = "Files citing this entry";
    public const string FilesCitingEmpty = "No files in this vault cite this entry.";

    public static string FilesCitingContainerLabel(int n) =>
        $"{FilesCitingHeading}. {Counted(n, "file", "files")}.";

    // --- Residue: the bibliography-jump messages (A5/A6) ---

    // W0.5-3 residue: bibliography-jump message builder, 1:1 twin of
    // the mac AppState site. These are the ONLY two host-composed
    // citation texts in the suite.
    public static string JumpedToEntry(string key) => $"Jumped to bibliography entry: {key}.";

    public static string SearchingBibliographyFor(string key) =>
        $"Searching bibliography for: {key}.";

    private static string Slash(string path) => path.Replace('\\', '/');
}
