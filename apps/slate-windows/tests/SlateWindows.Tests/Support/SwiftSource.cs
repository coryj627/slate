// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// Swift source with its comments removed, for the mac parity gates
/// (#1108).
/// </summary>
/// <remarks>
/// <para>
/// The C# twins read a syntax tree, which settles the live-code question
/// by construction. There is no Swift parser available here, so the mac
/// gates get the next best thing: comments stripped before any pattern
/// runs, so a commented-out <c>static let</c> cannot declare an id and a
/// mention of <c>sidebarOpenShortcutSlots</c> in prose cannot synthesise
/// nine of them. Both were demonstrated against the previous version.
/// </para>
/// <para>
/// <b>String literals are deliberately preserved</b>, and must be: mac's
/// ids and labels ARE string literals, so the patterns read them. That
/// leaves one narrower hole this cannot close — an id written inside some
/// OTHER string, a doc blob quoting a registration, say, still reads as a
/// declaration. Closing that needs real Swift parsing or a catalog
/// exported from mac's own build, which is recorded on #1108 rather than
/// pretended away here.
/// </para>
/// <para>
/// Swift's rules differ from C#'s in three ways that matter, and all
/// three are handled: block comments NEST, raw strings use
/// <c>#"…"#</c> with any number of hashes, and there is no
/// <c>@"…"</c> verbatim form.
/// </para>
/// </remarks>
internal static class SwiftSource
{
    /// <summary>
    /// <paramref name="source"/> with comments removed and string
    /// literals left intact.
    /// </summary>
    internal static string WithoutComments(string source)
    {
        var kept = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            char current = source[index];

            if (current == '#' || current == '"')
            {
                int literalEnd = EndOfLiteral(source, index);
                if (literalEnd > index)
                {
                    kept.Append(source, index, literalEnd - index);
                    index = literalEnd - 1;
                    continue;
                }
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

                    // The newline itself stays, so line-anchored patterns
                    // still see the break.
                    index = lineEnd - 1;
                    continue;
                }

                if (source[index + 1] == '*')
                {
                    index = EndOfNestedBlockComment(source, index) - 1;
                    continue;
                }
            }

            kept.Append(current);
        }

        return kept.ToString();
    }

    /// <summary>
    /// The index one past a string literal starting at
    /// <paramref name="start"/>, or <paramref name="start"/> itself if one
    /// does not start there.
    /// </summary>
    private static int EndOfLiteral(string source, int start)
    {
        // Raw strings: any number of hashes, then the quote, closed by the
        // quote and the same number of hashes. Escapes inside are written
        // \# … and do not terminate it.
        var hashes = 0;
        while (start + hashes < source.Length && source[start + hashes] == '#')
        {
            hashes++;
        }

        int quote = start + hashes;
        if (quote >= source.Length || source[quote] != '"')
        {
            return start;
        }

        var fence = 0;
        while (quote + fence < source.Length && source[quote + fence] == '"')
        {
            fence++;
        }

        // Three or more quotes open a multiline literal; two are an empty
        // string, not a fence.
        fence = fence >= 3 ? 3 : 1;
        string closing = new string('"', fence) + new string('#', hashes);

        for (int index = quote + fence; index < source.Length; index++)
        {
            if (source[index] == '\\' && hashes == 0)
            {
                index++;
                continue;
            }

            if (source[index] == '"'
                && string.CompareOrdinal(source, index, closing, 0, closing.Length) == 0)
            {
                return index + closing.Length;
            }
        }

        return source.Length;
    }

    /// <summary>
    /// The index one past a block comment starting at
    /// <paramref name="start"/>, honouring Swift's nesting.
    /// </summary>
    private static int EndOfNestedBlockComment(string source, int start)
    {
        var depth = 0;
        for (int index = start; index < source.Length - 1; index++)
        {
            if (source[index] == '/' && source[index + 1] == '*')
            {
                depth++;
                index++;
                continue;
            }

            if (source[index] == '*' && source[index + 1] == '/')
            {
                depth--;
                index++;
                if (depth == 0)
                {
                    return index + 1;
                }
            }
        }

        return source.Length;
    }
}
