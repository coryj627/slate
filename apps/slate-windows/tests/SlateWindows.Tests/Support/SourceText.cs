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
    /// <paramref name="start"/>, honouring verbatim and escaped forms.
    /// </summary>
    private static int EndOfLiteral(string source, int start)
    {
        char quote = source[start];
        bool verbatim = start > 0 && source[start - 1] == '@' && quote == '"';
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
}
