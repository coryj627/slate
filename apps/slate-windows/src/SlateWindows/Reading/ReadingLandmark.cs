// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Documents;

namespace SlateWindows.Reading;

/// <summary>
/// The structure kinds the chorded navigation commands move between
/// (W3-1, gap_analysis G21). One-to-one with the core announcement
/// vocabulary's <c>ReadingNavTarget</c> — the host never invents a kind
/// core cannot announce a miss for.
/// </summary>
internal enum ReadingLandmarkKind
{
    Heading,
    Link,
    List,
    Table,
    Embed,
    CodeBlock,
    Math,
    Diagram,
}

/// <summary>
/// One navigable position in the built reading document, in document
/// order. <see cref="Position"/> is a live <see cref="TextPointer"/>
/// into the document's text container, so navigation is a caret move,
/// paired with an explicit landing announcement — measured 2026-07-27:
/// NVDA does not echo programmatic caret moves.
/// </summary>
internal sealed class ReadingLandmark
{
    public ReadingLandmark(
        ReadingLandmarkKind kind,
        TextPointer position,
        byte headingLevel = 0,
        string text = "",
        TextElement? element = null)
    {
        Kind = kind;
        Position = position;
        HeadingLevel = headingLevel;
        Text = text;
        Element = element;
    }

    /// <summary>
    /// The landmark's own element where activation needs it (the
    /// Hyperlink for Link landmarks) — caret containment against its
    /// content range is how Enter-at-caret finds its target, instead of
    /// pointer-parent walking, which misses at normalized boundaries.
    /// </summary>
    public TextElement? Element { get; }

    public ReadingLandmarkKind Kind { get; }

    public TextPointer Position { get; }

    /// <summary>1–6 for headings; 0 otherwise.</summary>
    public byte HeadingLevel { get; }

    /// <summary>
    /// The target's own document text, captured at collection time and
    /// bounded — the payload of the landing announcement. Measured
    /// (2026-07-27, NVDA 2026.1.1): a programmatic caret move produces
    /// no speech, so a silent landing is an unusable one. Content, not
    /// composition: core owns the announcement phrasing.
    /// </summary>
    public string Text { get; }
}
