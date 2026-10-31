// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// WB-25 (#1290; locked decision 05 §9.3.1): no list crosses the FFI whole.
/// The Windows shell calls none of the unbounded vault-list entry points
/// mac still uses until its follow-up (WD17): the tag tree comes in pages
/// (<c>TagTreePage</c>), and Quick Open ranks against the index
/// (<c>SwitcherRankTopIndexed</c>) instead of handing core the whole file
/// list (<c>SwitcherRankTop</c>, stage 2).
/// </summary>
[Trait("census", "bounded-ffi-list")]
public sealed partial class BoundedFfiListCensus
{
    /// <summary>The unbounded entry points, as a call site or a method group
    /// spells them (<c>TagTreePage</c> and <c>SwitcherRankTopIndexed</c> are
    /// different words).</summary>
    private static readonly string[] UnboundedCalls = [@"\.TagTree\b", @"\bSwitcherRankTop\b"];

    [Fact]
    public void EveryVaultListOperationTheWindowsHostCallsIsPagedOrBounded()
    {
        string source = SourceText.ShellSourceRoot();
        List<string> calls = [];
        foreach (string file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            calls.AddRange(UnboundedCallSites(Path.GetRelativePath(source, file), File.ReadAllText(file)));
        }

        Assert.True(
            calls.Count == 0,
            "the shell calls an unbounded vault-list entry point (page it):\n  " + string.Join("\n  ", calls));
    }

    /// <summary>The scrape finds a planted call with its file and line, so the
    /// census can never pass by matching nothing.</summary>
    [Fact]
    public void TheScrapeNamesAPlantedCallsFileAndLine()
    {
        string planted = "class X\n{\n    void M(VaultSession s) => _ = s.TagTree();\n}\n";

        Assert.Equal(["Planted.cs:3"], UnboundedCallSites("Planted.cs", planted));
        Assert.Equal(
            ["Group.cs:1"],
            UnboundedCallSites("Group.cs", "Func<TagTree> load = _session.TagTree;\n"));
        Assert.Empty(UnboundedCallSites("Paged.cs", "class Y { void M(VaultSession s) => _ = s.TagTreePage(p, c); }"));
        Assert.Equal(
            ["Ranked.cs:1"],
            UnboundedCallSites("Ranked.cs", "_ = SlateUniffiMethods.SwitcherRankTop(files, query, recents, 50);\n"));
        Assert.Empty(UnboundedCallSites("Indexed.cs", "_ = session.SwitcherRankTopIndexed(query, recents, 50, cancel);\n"));
    }

    private static IEnumerable<string> UnboundedCallSites(string relativePath, string text)
    {
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            foreach (string call in UnboundedCalls)
            {
                if (Regex.IsMatch(lines[index], call))
                {
                    yield return $"{relativePath}:{index + 1}";
                }
            }
        }
    }
}
