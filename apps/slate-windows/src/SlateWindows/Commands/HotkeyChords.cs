// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;

namespace SlateWindows.Commands;

/// <summary>
/// The spoken-hotkey producers and the declared mac-to-Windows modifier
/// mapping rule (contract P12).
/// </summary>
/// <remarks>
/// <para>
/// Two producers, deliberately separate. A Windows chord is <b>not</b> a
/// substitution into the mac glyph string: the token order inverts
/// (<c>⇧⌘O</c> speaks "Shift Command O" while <c>Ctrl+Shift+O</c> speaks
/// "Control Shift O"), so the Windows spoken string is walked over the
/// Windows chord and nothing else.
/// </para>
/// </remarks>
internal static class MacHotkeySpoken
{
    /// <summary>Mac modifier glyphs → spoken words. Mirror of
    /// <c>HotkeySpoken.glyphWord</c> (apps/slate-mac/Sources/SlateMac/HotkeySpoken.swift:70-75).</summary>
    private static readonly Dictionary<char, string> GlyphWord = new()
    {
        ['⌘'] = "Command",
        ['⇧'] = "Shift",
        ['⌥'] = "Option",
        ['⌃'] = "Control",
    };

    /// <summary>Punctuation and arrow keys → spoken names. Mirror of
    /// <c>HotkeySpoken.keyWord</c> (HotkeySpoken.swift:84-106).</summary>
    private static readonly Dictionary<char, string> KeyWord = new()
    {
        [','] = "Comma",
        ['.'] = "Period",
        ['/'] = "Slash",
        ['\\'] = "Backslash",
        [';'] = "Semicolon",
        ['\''] = "Quote",
        ['['] = "Left Bracket",
        [']'] = "Right Bracket",
        ['-'] = "Minus",
        ['='] = "Equals",
        ['`'] = "Backtick",
        [' '] = "Space",
        ['↑'] = "Up Arrow",
        ['↓'] = "Down Arrow",
        ['←'] = "Left Arrow",
        ['→'] = "Right Arrow",
        ['⌫'] = "Delete",
    };

    /// <summary>
    /// Walk a mac glyph chord in order, exactly as
    /// <c>HotkeySpoken.spoken(for:)</c> does. Returns <see langword="null"/>
    /// for a null chord so a chordless row projects a null spoken string.
    /// </summary>
    public static string? Spoken(string? macChord)
    {
        if (macChord is null)
        {
            return null;
        }

        return string.Join(
            " ",
            macChord.Select(character =>
                GlyphWord.TryGetValue(character, out string? modifier)
                    ? modifier
                    : KeyWord.TryGetValue(character, out string? key)
                        ? key
                        : character.ToString()));
    }
}

/// <summary>
/// The Windows spoken-hotkey producer (contract P12). Walks the
/// <b>Windows</b> chord string — the plus-delimited WPF gesture form —
/// and renders each token as a screen-reader-pronounceable word.
/// </summary>
/// <remarks>
/// <para>
/// Before W5-1 the <c>windowsSpoken</c> column in <c>chords.json</c> had
/// zero producers and zero consumers: 35 hand-authored literals. This is
/// the producer; <c>ChordTableTests</c> proves every stored row equals
/// what it generates.
/// </para>
/// <para>
/// The vocabulary mirrors mac's (<c>Ctrl</c> → "Control", <c>\</c> →
/// "Backslash", <c>[</c> → "Left Bracket") so a chord that exists on both
/// platforms speaks the same words in each platform's own token order.
/// The Windows table additionally needs the named non-modifier keys WPF
/// spells out (<c>Escape</c>, <c>Enter</c>, <c>Back</c>, <c>F2</c>) —
/// those are multi-character tokens, which is why this walk splits on
/// <c>+</c> rather than per-character like mac's.
/// </para>
/// </remarks>
internal static class WindowsHotkeySpoken
{
    /// <summary>Windows modifier tokens → spoken words.</summary>
    private static readonly Dictionary<string, string> ModifierWord =
        new(StringComparer.Ordinal)
        {
            ["Ctrl"] = "Control",
            ["Control"] = "Control",
            ["Alt"] = "Alt",
            ["Shift"] = "Shift",
            ["Win"] = "Windows",
        };

    /// <summary>
    /// Non-modifier key tokens → spoken names. Punctuation is spelled out
    /// for the same reason mac spells it out: at a screen reader's lowest
    /// punctuation level a bare <c>,</c> or <c>\</c> is elided entirely,
    /// leaving "Control" with no key indicator.
    /// </summary>
    private static readonly Dictionary<string, string> KeyWord =
        new(StringComparer.Ordinal)
        {
            [","] = "Comma",
            ["."] = "Period",
            ["/"] = "Slash",
            ["\\"] = "Backslash",
            [";"] = "Semicolon",
            ["'"] = "Quote",
            ["["] = "Left Bracket",
            ["]"] = "Right Bracket",
            ["-"] = "Minus",
            ["="] = "Equals",
            ["`"] = "Backtick",
            [" "] = "Space",
            ["Space"] = "Space",
            ["Up"] = "Up Arrow",
            ["Down"] = "Down Arrow",
            ["Left"] = "Left Arrow",
            ["Right"] = "Right Arrow",
            ["Back"] = "Backspace",
            ["Backspace"] = "Backspace",
            ["Return"] = "Enter",
            ["0"] = "0",
            ["1"] = "1",
            ["2"] = "2",
            ["3"] = "3",
            ["4"] = "4",
            ["5"] = "5",
            ["6"] = "6",
            ["7"] = "7",
            ["8"] = "8",
            ["9"] = "9",
        };

    /// <summary>
    /// Render <paramref name="windowsChord"/> as a spoken string. Returns
    /// <see langword="null"/> for a chordless row.
    /// </summary>
    /// <remarks>
    /// A lone <c>+</c> key (never used today, but a plausible future
    /// "Ctrl++") would split ambiguously, so the walk treats a trailing
    /// empty token as the literal plus key rather than silently dropping
    /// it.
    /// </remarks>
    public static string? Spoken(string? windowsChord)
    {
        if (windowsChord is null)
        {
            return null;
        }

        if (windowsChord.Length == 0)
        {
            // Mirrors mac's contract: an empty chord speaks nothing, and
            // guarding the caller against empty input stays the caller's
            // responsibility.
            return string.Empty;
        }

        string[] tokens = windowsChord.Split('+');
        var parts = new List<string>(tokens.Length);
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (token.Length == 0)
            {
                // "Ctrl++" splits to ["Ctrl", "", ""] — the empty pair is
                // one literal plus key.
                if (index == tokens.Length - 1 && parts.Count > 0)
                {
                    continue;
                }

                parts.Add("Plus");
                index++;
                continue;
            }

            parts.Add(
                ModifierWord.TryGetValue(token, out string? modifier)
                    ? modifier
                    : KeyWord.TryGetValue(token, out string? key)
                        ? key
                        : token);
        }

        return string.Join(" ", parts);
    }
}

/// <summary>
/// The declared mac-chord → Windows-chord mapping rule (contract P12).
/// </summary>
/// <remarks>
/// <para><b>The rule, stated.</b></para>
/// <list type="number">
///   <item><description><c>⌘</c> → <c>Ctrl</c> (decision 12).</description></item>
///   <item><description><c>⌥</c> → <c>Alt</c> (decision 12).</description></item>
///   <item><description><c>⇧</c> → <c>Shift</c>.</description></item>
///   <item><description><c>⌃</c> → <c>Alt</c>. This is the rule decision 12
///   left unstated. Windows has no fourth user-available modifier — Win is
///   reserved by the shell — and <c>⌘</c> already owns <c>Ctrl</c>, so the
///   only mapping that keeps the four shipped <c>⌃⌘</c> chords
///   (<c>⌃⌘[</c>, <c>⌃⌘]</c>, <c>⌃⌘←</c>, <c>⌃⌘→</c>) expressible is
///   <c>⌃</c> → <c>Alt</c>. It is also what the app already delivers for
///   three of those four.</description></item>
///   <item><description>Token order is canonical: <c>Ctrl</c>, <c>Alt</c>,
///   <c>Shift</c>, then the key — WPF's own <c>ModifierKeys</c> order and
///   the order every shipped <c>InputGestureText</c> already uses
///   (<c>Ctrl+Alt+Shift+Left</c>).</description></item>
/// </list>
/// <para>
/// The rule is a <b>predictor</b>, never a rebinder. W5-1's adjudication
/// clause is record-don't-rebind, so every row whose shipped Windows chord
/// differs from <see cref="Apply"/>'s answer carries a
/// <c>Divergence</c> reason, and <c>ChordTableTests</c> proves that
/// every such row has one and that every row without one matches.
/// </para>
/// </remarks>
internal static class MacToWindowsChordRule
{
    private static readonly Dictionary<char, string> KeyToken = new()
    {
        ['↑'] = "Up",
        ['↓'] = "Down",
        ['←'] = "Left",
        ['→'] = "Right",
        ['⌫'] = "Backspace",
    };

    /// <summary>
    /// The Windows chord the rule predicts for <paramref name="macChord"/>,
    /// or <see langword="null"/> when there is no mac chord to map.
    /// </summary>
    public static string? Apply(string? macChord)
    {
        if (macChord is null)
        {
            return null;
        }

        bool control = false;
        bool alt = false;
        bool shift = false;
        var key = new List<string>();
        foreach (char character in macChord)
        {
            switch (character)
            {
                case '⌘':
                    control = true;
                    break;
                case '⌥':
                case '⌃':
                    alt = true;
                    break;
                case '⇧':
                    shift = true;
                    break;
                default:
                    key.Add(
                        KeyToken.TryGetValue(character, out string? token)
                            ? token
                            : character.ToString());
                    break;
            }
        }

        var parts = new List<string>(4);
        if (control)
        {
            parts.Add("Ctrl");
        }

        if (alt)
        {
            parts.Add("Alt");
        }

        if (shift)
        {
            parts.Add("Shift");
        }

        parts.AddRange(key);
        return string.Join("+", parts);
    }
}
