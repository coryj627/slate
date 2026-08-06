// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W4-5 (#737) feature contract 6, enforced rather than asserted.
//
// The contract says WorkspaceViewModel.Citations.cs holds the ONLY
// SetBibliographySources call site in the application: the citation
// suite is read-only, and no panel VM, row VM, overlay, command or
// context-menu action may write. The §W-C matrix claimed a test for
// this. There wasn't one — the suite proved the WEAKER property that
// note bytes are unchanged across a full exercise, which a second call
// site would sail straight through. A red-team pass found the gap in
// the evidence, not in the code.
//
// This scans source rather than behaviour on purpose: the risk is a
// future edit adding a second writer somewhere no behavioural test
// happens to exercise.

namespace SlateWindows.Tests.Censuses;

[Trait("census", "citation-write-surface")]
public class CitationWriteSurfaceCensus
{
    /// <summary>The one file allowed to call the mutator.</summary>
    private const string SanctionedCallSite = "WorkspaceViewModel.Citations.cs";

    [Fact]
    public void SetBibliographySourcesHasExactlyOneCallSiteInTheApp()
    {
        string source = SourceRoot();
        var offenders = new List<string>();
        int callSites = 0;

        foreach (string file in Directory.EnumerateFiles(
            source, "*.cs", SearchOption.AllDirectories))
        {
            // Skip build output and the generated FFI binding, which
            // necessarily DECLARES the method.
            string relative = Path.GetRelativePath(source, file);
            if (relative.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                || relative.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                || relative.Contains("generated", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int hits = CountOccurrences(File.ReadAllText(file), ".SetBibliographySources(");
            if (hits == 0)
            {
                continue;
            }
            callSites += hits;
            if (!string.Equals(
                Path.GetFileName(file), SanctionedCallSite, StringComparison.Ordinal))
            {
                offenders.Add($"{relative} ({hits})");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "contract 6: the citation suite is read-only, so "
                + $"{SanctionedCallSite} must hold the only "
                + "SetBibliographySources call site. Also found in: "
                + string.Join(", ", offenders));
        Assert.Equal(1, callSites);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }
        return count;
    }

    /// <summary>apps/slate-windows/src — the shipped application only.
    /// Tests legitimately call the mutator to build fixtures.</summary>
    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && !Directory.Exists(Path.Combine(dir.FullName, "src", "SlateWindows")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null, "could not locate apps/slate-windows");
        return Path.Combine(dir!.FullName, "src");
    }
}
