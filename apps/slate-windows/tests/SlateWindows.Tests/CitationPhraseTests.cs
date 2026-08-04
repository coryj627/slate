// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737) §W-C label goldens: every mac citation string pinned
/// verbatim. The mac twin pins the identical strings in its own
/// tests — that is what "host-duplicated by designation" means, and
/// this file is the Windows half of the contract.
/// </summary>
public class CitationPhraseTests
{
    private static BibEntry Entry(
        string key = "knuth1984",
        string title = "Literate Programming",
        Author[]? authors = null,
        int? year = 1984,
        string? journal = "The Computer Journal",
        string? doi = null,
        string? url = null,
        string? publisher = null,
        string? abstractText = null) =>
        new(key, "article", title, authors ?? [new Author("Knuth", "Donald E.")],
            year, journal, doi, url, publisher, abstractText, "{}");

    private static CitationReference Reference(params CitedItem[] items) =>
        new("[@knuth1984]", items, 0, 1);

    private static CitedItem Item(string key, CitationMode mode = CitationMode.Bracketed) =>
        new(key, null, null, null, mode);

    [Fact]
    public void CountedPluralizesOnTheCallerSuppliedPair()
    {
        Assert.Equal("1 citation", CitationPhrase.Counted(1, "citation", "citations"));
        Assert.Equal("3 citations", CitationPhrase.Counted(3, "citation", "citations"));
        Assert.Equal("0 citations", CitationPhrase.Counted(0, "citation", "citations"));
        Assert.Equal("1 file", CitationPhrase.Counted(1, "file", "files"));
        Assert.Equal("2 files", CitationPhrase.Counted(2, "file", "files"));
    }

    [Fact]
    public void CitationsLeafStatesAreVerbatim()
    {
        Assert.Equal("Citations", CitationPhrase.CitationsHeading);
        Assert.Equal(
            "Select a file to see its citations.", CitationPhrase.CitationsNoFile);
        // The ellipsis/period split between visible and spoken loading
        // copy is mac's and is deliberately preserved.
        Assert.Equal("Loading citations…", CitationPhrase.CitationsLoadingVisible);
        Assert.Equal("Loading citations.", CitationPhrase.CitationsLoadingSpoken);
        Assert.Equal(
            "Citations couldn't be loaded", CitationPhrase.CitationsErrorHeading);
        Assert.Equal(
            "Citations couldn't be loaded. disk on fire",
            CitationPhrase.CitationsErrorSpoken("disk on fire"));
        Assert.Equal("This note has no citations.", CitationPhrase.CitationsEmpty);
        Assert.Equal("Unresolved", CitationPhrase.UnresolvedBadge);
        Assert.Equal(
            "Activate to expand citation fields.", CitationPhrase.CitationRowHelp);
    }

    [Fact]
    public void UnresolvedRowSpeechPrefixesCoreSpeechAndNeverRewritesIt()
    {
        // Contract 1: the ONLY sanctioned modification of core speech.
        Assert.Equal(
            "Unresolved citation key. Unresolved citation: nosuchkey",
            CitationPhrase.UnresolvedRowSpeech("Unresolved citation: nosuchkey"));
    }

    [Fact]
    public void PlaceholderSpeechFollowsTheThreeMacModes()
    {
        // Single in-text item speaks the bare key…
        Assert.Equal(
            "lamport1994",
            CitationPhrase.PlaceholderSpeech(
                Reference(Item("lamport1994", CitationMode.InText))));
        // …any other single item is prefixed…
        Assert.Equal(
            "Citation: knuth1984",
            CitationPhrase.PlaceholderSpeech(Reference(Item("knuth1984"))));
        Assert.Equal(
            "Citation: knuth1984",
            CitationPhrase.PlaceholderSpeech(
                Reference(Item("knuth1984", CitationMode.SuppressAuthor))));
        // …and multiple items join with ", ".
        Assert.Equal(
            "Citation: knuth1984, lamport1994",
            CitationPhrase.PlaceholderSpeech(
                Reference(Item("knuth1984"), Item("lamport1994"))));
        // Zero items degrades to the raw text rather than inventing.
        Assert.Equal("[@knuth1984]", CitationPhrase.PlaceholderSpeech(Reference()));
    }

    [Fact]
    public void BibliographyStatesAreVerbatimIncludingTheWindowsDivergence()
    {
        Assert.Equal("Bibliography", CitationPhrase.BibliographyHeading);
        Assert.Equal("Bibliography view", CitationPhrase.BibliographySegmentName);
        Assert.Equal("Entries", CitationPhrase.SegmentEntries);
        Assert.Equal("Unresolved", CitationPhrase.SegmentUnresolved);
        Assert.Equal(
            "Open a vault to see its bibliography.", CitationPhrase.BibliographyNoVault);
        Assert.Equal("Loading bibliography…", CitationPhrase.BibliographyLoading);
        Assert.Equal(
            "Bibliography couldn't be loaded", CitationPhrase.BibliographyErrorHeading);
        Assert.Equal(
            "Bibliography couldn't be loaded. no such file",
            CitationPhrase.BibliographyErrorSpoken("no such file"));

        // RECORDED DIVERGENCE D-2: Windows cannot point at a Settings
        // pane that does not exist until W8-1.
        Assert.Equal(
            "No bibliography sources configured. "
                + "Add a \"citations\" section to the vault's slate.json.",
            CitationPhrase.BibliographyNoSources);
        Assert.DoesNotContain("Settings", CitationPhrase.BibliographyNoSources);

        Assert.Equal(
            "No entries match 'knuth'.", CitationPhrase.BibliographyNoFilterHits("knuth"));
        Assert.Equal(
            "Search title, author, key…", CitationPhrase.BibliographySearchPlaceholder);
        Assert.Equal("Search bibliography", CitationPhrase.BibliographySearchName);
        Assert.Equal(
            "Filters entries by title, author family name, or citation key.",
            CitationPhrase.BibliographySearchHelp);
        Assert.Equal(
            "Open a vault to see unresolved citations.", CitationPhrase.UnresolvedNoVault);
        Assert.Equal("Loading unresolved citations…", CitationPhrase.UnresolvedLoading);
        Assert.Equal(
            "No unresolved citations. Every key in your notes has a bibliography entry.",
            CitationPhrase.UnresolvedEmpty);
        Assert.Equal(
            "Show files citing this entry", CitationPhrase.ShowFilesCitingAction);
        Assert.Equal(
            "Insert citation in current note (V1.x)", CitationPhrase.InsertCitationAction);
        Assert.Equal(
            "Showing the first 5000 of 12000 entries.",
            CitationPhrase.TruncationNotice(5000, 12000));
    }

    [Fact]
    public void EntryTitleAndSubtitleDegradeInsteadOfInventing()
    {
        Assert.Equal(
            "Literate Programming (1984)", CitationPhrase.EntryTitleLine(Entry()));
        Assert.Equal(
            "Literate Programming", CitationPhrase.EntryTitleLine(Entry(year: null)));
        // No title falls back to the key — never an empty line.
        Assert.Equal("knuth1984", CitationPhrase.EntryTitleLine(Entry(title: "", year: null)));

        Assert.Equal(
            "Knuth — The Computer Journal", CitationPhrase.EntrySubtitle(Entry()));
        Assert.Equal("Knuth", CitationPhrase.EntrySubtitle(Entry(journal: null)));
        Assert.Equal(
            "The Computer Journal",
            CitationPhrase.EntrySubtitle(Entry(authors: [], journal: "The Computer Journal")));
        Assert.Equal(
            "knuth1984", CitationPhrase.EntrySubtitle(Entry(authors: [], journal: null)));

        Author[] four =
        [
            new("Adams", "A"), new("Brown", "B"), new("Clark", "C"), new("Davis", "D"),
        ];
        Assert.Equal(
            "Adams, Brown, Clark, et al.",
            CitationPhrase.EntrySubtitle(Entry(authors: four, journal: null)));
        Assert.Equal(
            "Adams, Brown, Clark",
            CitationPhrase.EntrySubtitle(Entry(authors: four[..3], journal: null)));
    }

    [Fact]
    public void AuthorListUsesFamilyGivenPairsJoinedBySemicolons()
    {
        Assert.Equal(
            "Knuth, Donald E.",
            CitationPhrase.AuthorList([new Author("Knuth", "Donald E.")]));
        Assert.Equal(
            "Lamport, Leslie; Bibby, Duane",
            CitationPhrase.AuthorList(
                [new Author("Lamport", "Leslie"), new Author("Bibby", "Duane")]));
        // A missing given name degrades to the family alone.
        Assert.Equal("Anon", CitationPhrase.AuthorList([new Author("Anon", null)]));
        Assert.Equal("Anon", CitationPhrase.AuthorList([new Author("Anon", "")]));
    }

    [Fact]
    public void EntryRowDescriptionOmitsAbsentPartsEntirely()
    {
        // The doubled period after "Donald E." is MAC-CORRECT: mac
        // appends "." to the joined author list regardless of how a
        // name ends (BibliographyPanel.swift:252). Do not "fix" it.
        Assert.Equal(
            "Title: Literate Programming. Authors: Knuth, Donald E.. Year: 1984. "
                + "Journal: The Computer Journal. Key: knuth1984.",
            CitationPhrase.EntryRowDescription(Entry()));

        // Contract 10: absent AUTHORS/YEAR/JOURNAL are omitted — never
        // "Year: 0.", never an empty "Journal: ." fragment. Title is
        // always present and falls back to the key (mac :243-244).
        string sparse = CitationPhrase.EntryRowDescription(
            Entry(title: "", authors: [], year: null, journal: null));
        Assert.Equal("Title: knuth1984. Key: knuth1984.", sparse);
        Assert.DoesNotContain("Year", sparse);
        Assert.DoesNotContain("Journal", sparse);
        Assert.DoesNotContain("Authors", sparse);
    }

    [Fact]
    public void YearTextIsNullForAnAbsentYear()
    {
        Assert.Equal("1984", CitationPhrase.YearText(1984));
        Assert.Null(CitationPhrase.YearText(null));
        // A zero year is a real value, not an absence.
        Assert.Equal("0", CitationPhrase.YearText(0));
    }

    [Fact]
    public void UnresolvedRowLabelForwardSlashesThePath()
    {
        Assert.Equal(
            "Unresolved key: smith2020 in notes/paper.md.",
            CitationPhrase.UnresolvedRowLabel("smith2020", "notes\\paper.md"));
    }

    [Fact]
    public void DetailsOverlayStringsAreVerbatim()
    {
        Assert.Equal("Title", CitationPhrase.FieldTitle);
        Assert.Equal("Authors", CitationPhrase.FieldAuthors);
        Assert.Equal("Year", CitationPhrase.FieldYear);
        Assert.Equal("Journal", CitationPhrase.FieldJournal);
        Assert.Equal("Publisher", CitationPhrase.FieldPublisher);
        Assert.Equal("DOI", CitationPhrase.FieldDoi);
        Assert.Equal("URL", CitationPhrase.FieldUrl);
        Assert.Equal("Abstract", CitationPhrase.FieldAbstract);

        Assert.Equal("Year: 1984", CitationPhrase.FieldSpoken("Year", "1984"));
        Assert.Equal(
            "https://doi.org/10.1093/comjnl/27.2.97",
            CitationPhrase.DoiTarget("10.1093/comjnl/27.2.97"));
        Assert.Equal("Abstract: A method.", CitationPhrase.AbstractSpoken("A method."));
        Assert.Equal("Abstract, collapsed.", CitationPhrase.AbstractCollapsedSpoken);

        Assert.Equal("Citation expanded.", CitationPhrase.DetailsSummary(null));
        Assert.Equal("Citation expanded.", CitationPhrase.DetailsSummary(""));
        Assert.Equal(
            "Citation expanded. Title: Literate Programming.",
            CitationPhrase.DetailsSummary("Literate Programming"));

        Assert.Equal("Citation key not found", CitationPhrase.DetailsUnresolvedHeading);
        // Visible and spoken forms differ deliberately (mac parity).
        Assert.Equal(
            "'nosuchkey' isn't in any bibliography source.",
            CitationPhrase.DetailsUnresolvedBody("nosuchkey"));
        Assert.Equal(
            "Unresolved citation: nosuchkey. This key isn't in any bibliography source.",
            CitationPhrase.DetailsUnresolvedSpoken("nosuchkey"));

        Assert.Equal("Close", CitationPhrase.DetailsClose);
        Assert.Equal("Close the expanded citation.", CitationPhrase.DetailsCloseHelp);

        Assert.Equal(
            "Citation: Literate Programming 1984",
            CitationPhrase.EntryDetailsSpeech(Entry()));
        Assert.Equal(
            "Citation: Literate Programming",
            CitationPhrase.EntryDetailsSpeech(Entry(year: null)));
    }

    [Fact]
    public void SummarySheetStringsAreVerbatim()
    {
        Assert.Equal("Citation Summary", CitationPhrase.SummaryHeading);
        Assert.Equal("This note has no citations.", CitationPhrase.SummaryEmptyBody);
        Assert.Equal(
            "This note has 3 citations referencing 2 unique sources.",
            CitationPhrase.SummaryLine(3, 2));
        Assert.Equal(
            "This note has 1 citation referencing 1 unique source.",
            CitationPhrase.SummaryLine(1, 1));
        Assert.Equal("Walk through citations", CitationPhrase.SummaryWalkAction);
        Assert.Equal(
            "Closes this sheet and starts a walk-through.", CitationPhrase.SummaryWalkHelp);
        Assert.Equal("OK", CitationPhrase.SummaryDismissEmpty);
        Assert.Equal("Done", CitationPhrase.SummaryDismiss);
        Assert.Equal("Close the citation summary.", CitationPhrase.SummaryDismissHelp);
        Assert.Equal(
            "Citation Summary. This note has no citations.",
            CitationPhrase.SummarySheetName(CitationPhrase.SummaryEmptyBody));
    }

    [Fact]
    public void FilesCitingStringsAreVerbatim()
    {
        Assert.Equal("Files citing this entry", CitationPhrase.FilesCitingHeading);
        Assert.Equal(
            "No files in this vault cite this entry.", CitationPhrase.FilesCitingEmpty);
        Assert.Equal(
            "Files citing this entry. 1 file.",
            CitationPhrase.FilesCitingContainerLabel(1));
        Assert.Equal(
            "Files citing this entry. 3 files.",
            CitationPhrase.FilesCitingContainerLabel(3));
    }

    [Fact]
    public void TheTwoResidueMessagesAreVerbatim()
    {
        // The ONLY host-composed citation texts in the suite (A5/A6).
        Assert.Equal(
            "Jumped to bibliography entry: knuth1984.",
            CitationPhrase.JumpedToEntry("knuth1984"));
        Assert.Equal(
            "Searching bibliography for: nosuchkey.",
            CitationPhrase.SearchingBibliographyFor("nosuchkey"));
    }
}
