// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace SlateWindows.Reading;

/// <summary>
/// DS-1: the app's fonts ship inside it, so each family's source is a pack URI.
/// WPF hands that source on as the font's name where a reader meets it: UI
/// Automation's font name, which NVDA reads under Report Font Name, and a copy's
/// RTF font table, where Word shows it and the URI's semicolon ends the entry
/// early. This gives them the family's own name instead. A copy's XAML formats
/// carry it too, so <see cref="ReadingSurface"/> doesn't offer them.
/// </summary>
internal static partial class ShippedFontNames
{
    private static readonly Lazy<string[]> Sources = new(() =>
    [
        .. new ResourceDictionary { Source = new Uri("pack://application:,,,/SlateWindows;component/Themes/Slate.Text.xaml") }
            .Values.OfType<FontFamily>().Select(family => family.Source),
    ]);

    /// <summary>A family reference: the URI, the family's first name, and any
    /// fallbacks, up to whatever ends the entry.</summary>
    [GeneratedRegex(@"pack://[^#{}]*#([^,;{}]+)[^;{}]*", RegexOptions.CultureInvariant)]
    private static partial Regex PackFamily();

    /// <summary><paramref name="text"/> with each family reference replaced by
    /// its name.</summary>
    internal static string Named(string text) => PackFamily().Replace(text, "$1");

    /// <summary><paramref name="rtf"/> with the family references in its font table
    /// replaced by their names. Only there: the same characters in a note's text
    /// are the note's, and a brace in that text is always escaped, so the first
    /// unescaped <c>{\fonttbl</c> is the table.</summary>
    internal static string InRtfFontTable(string rtf)
    {
        int start = rtf.IndexOf(@"{\fonttbl", StringComparison.Ordinal);
        if (start < 0)
        {
            return rtf;
        }

        int depth = 0;
        for (int index = start; index < rtf.Length; index++)
        {
            switch (rtf[index])
            {
                case '\\':
                    index++;
                    break;
                case '{':
                    depth++;
                    break;
                case '}' when --depth == 0:
                    return string.Concat(rtf.AsSpan(0, start), Named(rtf[start..(index + 1)]), rtf.AsSpan(index + 1));
            }
        }

        return rtf;
    }

    /// <summary>The source of the shipped family a reader knows as
    /// <paramref name="name"/>, or the name itself for any other font, so a
    /// search for a name a range answered finds that range.</summary>
    internal static string SourceOf(string name) =>
        Sources.Value.FirstOrDefault(source => Named(source) == name) ?? name;
}
