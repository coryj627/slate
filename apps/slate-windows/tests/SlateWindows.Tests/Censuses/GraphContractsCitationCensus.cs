// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-2 (#746): the graph contracts document, <c>35_graph_contracts.md</c>,
/// under the same two guards <see cref="ContractsCitationCensus"/> keeps
/// over the canvas's — every backticked identifier of fifteen or more
/// characters that begins with an uppercase letter is declared somewhere
/// in the Windows shell, its tests, the tools, the benchmarks or the
/// generated bindings; and every registered section is found and cites
/// at least its floor. One tuple per PR section, added with the
/// section's contracts commit; the floor sits below the population and
/// rises with the records. The declaration scan is the canvas census's
/// own (<see cref="ContractsCitationCensus.DeclaredNames"/>).
/// </summary>
[Trait("census", "graph-contracts-citation")]
public sealed class GraphContractsCitationCensus
{
    private const string ContractsDoc = "35_graph_contracts.md";

    private const int IdentifierFloor = 15;

    private static readonly (string Pr, string Start, string End, int Length, int Citations)[]
        PrSections =
        [
            (
                "0a",
                "## PR 0a — the graph announcer vocabulary moves to core",
                "## PR 0b — the graph structural queries move to core",
                // The vocabulary section: fifteen contracts, the event
                // enumeration, decisions, divergences, risks, the pins.
                6_000,
                // The frozen section (revision 6 + records + close-out) cites 100 existing identifiers (the canvas
                // precedent's tests, the Windows census and the mac twins the
                // census allow-lists, several more than once; the family's own
                // names are unbackticked until they exist); 99 sits one below
                // that population, and the floor rises with the records.
                99),
            (
                "0b",
                "## PR 0b — the graph structural queries move to core",
                "## PR A — the graph document, the tab wiring, the table projection",
                // The structural-queries section: fifteen contracts, the query
                // surface, decisions, divergences, risks, the register, the pins.
                6_000,
                // Revision 5 cites existing identifiers only through the mac
                // sources and tests it names; the family's own names are
                // unbackticked until they exist. The floor sits one below the
                // measured population and rises with the records.
                33),
            (
                "A",
                "## PR A — the graph document, the tab wiring, the table projection",
                "## PR B — the Connections leaf",
                // The document section: seventeen contracts, the three rules
                // on Windows, decisions, divergences, risks, the pins.
                6_000,
                // Revision 5 cites existing identifiers only through the
                // shell, its tests and the mac sources it names; the family's
                // own names are unbackticked until they exist. The floor sits
                // one below the measured population and rises with the records.
                165),
            (
                "B",
                "## PR B — the Connections leaf",
                "## PR B2 — re-root and Back, Show connections from three surfaces, the shared selection",
                // The leaf section: the contracts, the decisions, the
                // divergences, the pins.
                6_000,
                // Revision 8 cites existing identifiers only through the
                // shell, its tests and the core and mac sources it names; the
                // leaf's own names are unbackticked until they exist. The
                // floor sits one below the measured population and rises with
                // the records.
                82),
            (
                "B2",
                "## PR B2 — re-root and Back, Show connections from three surfaces, the shared selection",
                "<!-- end of the graph contracts document -->",
                // The re-root section: rule D, the contracts, the decisions,
                // the divergences, the pins.
                6_000,
                // Revision 2 cites existing identifiers only through the
                // shell, its tests and the core and mac sources it names; B2's
                // own names are unbackticked until they exist. The floor sits
                // one below the measured population and rises with the records.
                37),
        ];

    public static TheoryData<string, string, string> SectionRanges
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach ((string pr, string start, string end, _, _) in PrSections)
            {
                data.Add(pr, start, end);
            }
            return data;
        }
    }

    public static TheoryData<string, string, string, int, int> Sections
    {
        get
        {
            var data = new TheoryData<string, string, string, int, int>();
            foreach ((string pr, string start, string end, int length, int citations) in PrSections)
            {
                data.Add(pr, start, end, length, citations);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SectionRanges))]
    public void EveryIdentifierCitedInAPrSectionExists(string pr, string start, string end)
    {
        string section = Section(start, end, pr);
        HashSet<string> declared = ContractsCitationCensus.DeclaredNames();
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(section, @"`([A-Za-z_][A-Za-z0-9_]*)`"))
        {
            string cited = match.Groups[1].Value;
            if (cited.Length >= IdentifierFloor && char.IsUpper(cited[0]) && !declared.Contains(cited))
            {
                _ = missing.Add(cited);
            }
        }
        Assert.True(
            missing.Count == 0,
            $"§{pr} of the graph contracts document cites identifiers that do not exist "
            + "anywhere in the Windows shell or its tests:\n  " + string.Join("\n  ", missing));
    }

    [Theory]
    [MemberData(nameof(Sections))]
    public void EverySectionIsFoundAndCitesIdentifiers(
        string pr, string start, string end, int minimumLength, int minimumCitations)
    {
        string section = Section(start, end, pr);
        Assert.True(section.Length > minimumLength, $"§{pr} is implausibly short — did the marker move?");
        int citations = Regex.Matches(section, @"`([A-Z][A-Za-z0-9_]{14,})`").Count;
        Assert.True(
            citations >= minimumCitations,
            $"§{pr} cites only {citations} identifiers; the guard would be scanning almost nothing.");
    }

    private static string Section(string sectionStart, string sectionEnd, string pr)
    {
        string path = Path.Combine(SourceText.RepoRoot(), "docs", "plans", ContractsDoc);
        Assert.True(File.Exists(path), $"the graph contracts document is missing at {path}");
        string text = File.ReadAllText(path);
        int start = text.IndexOf(sectionStart, StringComparison.Ordinal);
        int end = text.IndexOf(sectionEnd, StringComparison.Ordinal);
        Assert.True(start >= 0, $"§{pr}'s heading is missing: {sectionStart}");
        Assert.True(end > start, $"§{pr}'s terminator is missing: {sectionEnd}");
        return text[start..end];
    }
}
