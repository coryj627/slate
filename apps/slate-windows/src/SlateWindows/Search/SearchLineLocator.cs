// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Search;

/// <summary>
/// Derives the line an activated search result should land on — the
/// Windows twin of mac's <c>firstTokenLineNumber</c>
/// (<c>AppState.swift:23773-23830</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>QueryHit</c> deliberately carries no line number: producing it
/// core-side meant pulling <c>body_text</c> through SQLite for every hit
/// (#92 item 1), so the host derives the line from the note it just
/// loaded. The heuristic is mac's, verbatim: lowercase both sides, strip
/// the <c>body_text:</c> FTS5 column-filter prefix, tokenize on
/// letters-and-digits, drop FTS5 keywords and pure-numeric tokens, take
/// the earliest occurrence of any surviving token, and count newlines up
/// to it. No match — including an empty or all-filtered query — is line 1.
/// </para>
/// <para>
/// Pure and static so the derivation is pinned by unit facts rather than
/// only observable through a live editor.
/// </para>
/// </remarks>
internal static class SearchLineLocator
{
    /// <summary>FTS5 keywords a bare prose word must not anchor the scan
    /// on (mac's set, from #93 item 5).</summary>
    private static readonly string[] Fts5Keywords = ["and", "or", "not", "near"];

    /// <summary>
    /// The 1-based line of the earliest occurrence of any query token in
    /// <paramref name="body"/>, or 1 when nothing matches.
    /// </summary>
    /// <remarks>
    /// The newline count runs over the LOWERCASED body's prefix, exactly
    /// as mac counts it — Unicode lowercasing can change string length
    /// (<c>İ</c> → <c>i</c> + U+0307), so indexing the original string
    /// with a lowered index would be wrong on both platforms.
    /// </remarks>
    public static int FirstTokenLine(string body, string query)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(query);

        string bodyLower = body.ToLowerInvariant();
        // Strip FTS5 column-filter prefixes before tokenizing. Today the
        // only indexed column is body_text; a user typing body_text:foo
        // means "find foo inside body_text", so the prefix must not seed
        // tokens for the line scan (the mac comment, transcribed).
        string preprocessed = query.ToLowerInvariant()
            .Replace("body_text:", " ", StringComparison.Ordinal);

        int earliest = -1;
        foreach (string token in Tokenize(preprocessed))
        {
            int at = bodyLower.IndexOf(token, StringComparison.Ordinal);
            if (at >= 0 && (earliest < 0 || at < earliest))
            {
                earliest = at;
            }
        }

        if (earliest < 0)
        {
            return 1;
        }

        int line = 1;
        for (int index = 0; index < earliest; index++)
        {
            if (bodyLower[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// The UTF-16 offset of a 1-based line's first character in
    /// <paramref name="text"/> — where the caret parks after activation.
    /// A line past the end of the text lands on the start of the last
    /// line actually present.
    /// </summary>
    public static int LineStartOffset(string text, int line)
    {
        ArgumentNullException.ThrowIfNull(text);
        int offset = 0;
        for (int remaining = line - 1; remaining > 0; remaining--)
        {
            int newline = text.IndexOf('\n', offset);
            if (newline < 0)
            {
                break;
            }

            offset = newline + 1;
        }

        return offset;
    }

    /// <summary>Split on anything that is not a letter or digit, then
    /// drop FTS5 keywords and pure-numeric tokens — numbers inside
    /// composite FTS5 constructs (<c>NEAR(a b, 5)</c>, <c>LIMIT 10</c>)
    /// are not semantically meaningful to a body-line scan.</summary>
    private static IEnumerable<string> Tokenize(string preprocessed)
    {
        int start = -1;
        for (int index = 0; index <= preprocessed.Length; index++)
        {
            bool isTokenChar = index < preprocessed.Length
                && char.IsLetterOrDigit(preprocessed[index]);
            if (isTokenChar)
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start >= 0)
            {
                string token = preprocessed[start..index];
                start = -1;
                if (!Fts5Keywords.Contains(token, StringComparer.Ordinal)
                    && !token.All(char.IsDigit))
                {
                    yield return token;
                }
            }
        }
    }
}
