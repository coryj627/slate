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
}

/// <summary>
/// One navigable position in the built reading document, in document
/// order. <see cref="Position"/> is a live <see cref="TextPointer"/>
/// into the document's text container, so navigation is a caret move —
/// the AT speaks the landing line itself (the reason landings are not
/// announcement events).
/// </summary>
internal sealed class ReadingLandmark
{
    public ReadingLandmark(
        ReadingLandmarkKind kind, TextPointer position, byte headingLevel = 0)
    {
        Kind = kind;
        Position = position;
        HeadingLevel = headingLevel;
    }

    public ReadingLandmarkKind Kind { get; }

    public TextPointer Position { get; }

    /// <summary>1–6 for headings; 0 otherwise.</summary>
    public byte HeadingLevel { get; }
}
