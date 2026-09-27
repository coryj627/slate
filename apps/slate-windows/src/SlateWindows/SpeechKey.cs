// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Text;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 3 (#1246, R-4; codex PR 3 round 6, owner decision OD-9): what a
/// name SOUNDS like — the one input every sibling-name collision check
/// reads (<see cref="SiblingNames.ReadAlike"/> compares two names by their
/// keys and nothing else). A screen reader speaks the letters and digits of
/// a name; it does not speak case, the string's encoding, most punctuation
/// at its default level, or the whitespace between words. So the key is the
/// name's letter runs and digit runs, after NFKC normalization (composed and
/// decomposed "café", a fullwidth or superscript digit, a ligature) and the
/// invariant culture's case folding, joined by single spaces:
/// <list type="bullet">
/// <item>a letter or digit starts or extends its run; a change between
/// letters and digits starts a new one ("Note1" reads like "Note 1"), and a
/// decimal digit of any script is its value ("١" is "1");</item>
/// <item>a combining mark stays with the letter it sits on (a Devanagari
/// vowel sign is part of its word), and is silent with nothing to sit
/// on;</item>
/// <item>a format character — a zero-width space or joiner, a soft hyphen —
/// is silent and does not split a word;</item>
/// <item>everything else — whitespace of any kind, punctuation, symbols —
/// only separates runs: "Open tasks.", " Open  tasks" and "Open tasks"
/// share a key, and so do "note-a" and "note a".</item>
/// </list>
/// The key is deliberately COARSE: two names that share a key but would be
/// spoken apart only cost one needless distinguisher, while two names spoken
/// alike under different keys would leave a reader unable to tell two
/// siblings apart. What it cannot see — two scripts whose letters look or
/// sound alike (Latin "A" and Greek "Α") — is accepted risk AR-34.
/// </summary>
internal readonly record struct SpeechKey
{
    private SpeechKey(string value) => Value = value;

    /// <summary>The runs, joined by single spaces; "" for a name with no
    /// letter or digit.</summary>
    public string Value { get; }

    public static SpeechKey Of(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new SpeechKey(string.Empty);
        }
        string folded = Normalized(Normalized(text).ToLowerInvariant());
        var key = new StringBuilder(folded.Length);
        Run previous = Run.None;
        foreach (Rune rune in folded.EnumerateRunes())
        {
            Kind kind = Classify(rune);
            if (kind is Kind.Silent)
            {
                continue;
            }
            if (kind is Kind.Separator)
            {
                previous = Run.None;
                continue;
            }
            if (kind is Kind.Mark)
            {
                if (previous is not Run.None)
                {
                    _ = key.Append(rune.ToString());
                }
                continue;
            }
            Run run = kind is Kind.Letter ? Run.Letters : Run.Digits;
            if (run != previous && key.Length > 0)
            {
                _ = key.Append(' ');
            }
            // A decimal digit of any script is its value: "١" reads "1".
            _ = Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber
                ? key.Append((char)('0' + (int)Rune.GetNumericValue(rune)))
                : key.Append(rune.ToString());
            previous = run;
        }
        return new SpeechKey(key.ToString());
    }

    /// <summary>Whether <paramref name="text"/> says nothing at all — only
    /// whitespace, control and format characters: such a name is no
    /// name.</summary>
    public static bool IsSilent(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune)
                && Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format))
            {
                return false;
            }
        }
        return true;
    }

    public override string ToString() => Value;

    private static string Normalized(string text) =>
        text.IsNormalized(NormalizationForm.FormKC) ? text : text.Normalize(NormalizationForm.FormKC);

    private static Kind Classify(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.UppercaseLetter
            or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter => Kind.Letter,
        UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.OtherNumber => Kind.Digit,
        UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark => Kind.Mark,
        UnicodeCategory.Format => Kind.Silent,
        _ => Kind.Separator,
    };

    private enum Kind
    {
        Letter,
        Digit,
        Mark,
        Silent,
        Separator,
    }

    private enum Run
    {
        None,
        Letters,
        Digits,
    }
}
