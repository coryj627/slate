// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// Source-scraping helpers shared by the W5-1 drift twins.
/// </summary>
/// <remarks>
/// Every drift test in this suite is a regex over source, and a regex
/// cannot tell code from prose. Codex found the consequence: comment out
/// the splitter's <c>Key.Up</c> arm and leave its text behind, and the
/// chord is gone from the product while the scrape still reads it — a
/// dead-source match that passes both comparison directions and the
/// non-empty guard. The same hazard applies to every scrape here, which
/// is why the stripper lives in one place rather than in the one test
/// that was caught by it.
/// </remarks>
internal static class SourceText
{
    /// <summary>The shell's source directory.</summary>
    /// <remarks>
    /// Three test classes each carry a private copy of this walk. This is
    /// the shared one; consolidating the rest is tracked separately rather
    /// than folded into a review-response commit.
    /// </remarks>
    internal static string ShellSourceRoot() =>
        System.IO.Path.Combine(
            RepoRoot(), "apps", "slate-windows", "src", "SlateWindows");

    private static string RepoRoot()
    {
        var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
        while (directory is not null
            && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new System.InvalidOperationException("repository root not found");
    }

    /// <summary>
    /// <paramref name="source"/> with its comments removed. String and
    /// character literals are preserved, so a <c>"//"</c> inside one
    /// cannot swallow the rest of a line.
    /// </summary>
    internal static string WithoutComments(string source)
    {
        var kept = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            char current = source[index];
            if (current is '"' or '\'')
            {
                int literalEnd = EndOfLiteral(source, index);
                kept.Append(source, index, literalEnd - index);
                index = literalEnd - 1;
                continue;
            }

            if (current == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    int lineEnd = source.IndexOf('\n', index);
                    if (lineEnd < 0)
                    {
                        break;
                    }

                    // The newline itself is kept: patterns anchored on
                    // line starts must still see the line break.
                    index = lineEnd - 1;
                    continue;
                }

                if (source[index + 1] == '*')
                {
                    int blockEnd = source.IndexOf("*/", index + 2, System.StringComparison.Ordinal);
                    index = blockEnd < 0 ? source.Length : blockEnd + 1;
                    continue;
                }
            }

            kept.Append(current);
        }

        return kept.ToString();
    }

    /// <summary>
    /// The index one past a string or character literal beginning at
    /// <paramref name="start"/>, honouring raw, verbatim and escaped forms.
    /// </summary>
    /// <remarks>
    /// The raw and <c>@$</c> arms are here because this stripper is
    /// load-bearing for every drift scrape in the suite. None of the files
    /// currently scraped uses either form, but both are ordinary C#, and a
    /// stripper that mis-parses one would end a literal early and then
    /// treat live code as string content — a silent under-scrape, which is
    /// the failure class this whole review has been about.
    /// </remarks>
    private static int EndOfLiteral(string source, int start)
    {
        char quote = source[start];
        if (quote == '"')
        {
            var fence = 0;
            while (start + fence < source.Length && source[start + fence] == '"')
            {
                fence++;
            }

            if (fence >= 3)
            {
                return EndOfRawLiteral(source, start, fence);
            }
        }

        // $@"…" and @$"…" are both legal, so look back past either prefix.
        var beforeQuote = start - 1;
        while (beforeQuote >= 0 && source[beforeQuote] is '$' or '@')
        {
            beforeQuote--;
        }

        bool verbatim = quote == '"'
            && source[(beforeQuote + 1)..start].Contains('@', System.StringComparison.Ordinal);
        for (int index = start + 1; index < source.Length; index++)
        {
            char current = source[index];
            if (verbatim)
            {
                if (current != quote)
                {
                    continue;
                }

                // "" inside a verbatim literal is an escaped quote.
                if (index + 1 < source.Length && source[index + 1] == quote)
                {
                    index++;
                    continue;
                }

                return index + 1;
            }

            if (current == '\\')
            {
                index++;
                continue;
            }

            if (current == quote || current == '\n')
            {
                return index + 1;
            }
        }

        return source.Length;
    }

    /// <summary>
    /// The index one past a raw string literal — <c>"""…"""</c> — whose
    /// opening fence of <paramref name="fence"/> quotes begins at
    /// <paramref name="start"/>. A raw literal ends at the first run of at
    /// least that many quotes, and nothing inside it escapes.
    /// </summary>
    private static int EndOfRawLiteral(string source, int start, int fence)
    {
        for (int index = start + fence; index < source.Length; index++)
        {
            if (source[index] != '"')
            {
                continue;
            }

            var run = 0;
            while (index + run < source.Length && source[index + run] == '"')
            {
                run++;
            }

            if (run >= fence)
            {
                return index + run;
            }

            index += run - 1;
        }

        return source.Length;
    }
}
