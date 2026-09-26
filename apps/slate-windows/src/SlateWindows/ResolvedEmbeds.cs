// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// What a resolved embed card shows, as data (#1278; locked decision 05
/// §1.1, contract 38): the resolution's kind and identifying fields, never
/// a title. Core words the card title from it
/// (<see cref="SlateUniffiMethods.ResolvedEmbedTitle"/>) and the
/// <c>EmbedPreviewShown</c> announcement alike, so the Ctrl+E popover, its
/// nested cards, the embeds leaf and the reading view's embed headers all
/// carry core's title and no host spells the "Embedded note / section /
/// block / image / base" shapes. <see cref="ResolvedEmbed.Base"/> is the
/// reading view's `.base` summary card (Bases contract C10), built where
/// that card is: a resolution alone is a note.
/// </summary>
internal static class ResolvedEmbeds
{
    /// <summary>The card data for <paramref name="resolution"/>, or null
    /// for an unresolved embed, which is not a card.</summary>
    internal static ResolvedEmbed? Of(EmbedResolution resolution) =>
        resolution switch
        {
            EmbedResolution.FullNote full => new ResolvedEmbed.Note(full.TargetPath),
            EmbedResolution.Section section =>
                new ResolvedEmbed.Section(section.TargetPath, section.Heading),
            EmbedResolution.Block block => new ResolvedEmbed.Block(block.TargetPath),
            EmbedResolution.Image image => new ResolvedEmbed.Image(image.TargetPath, image.Alt),
            _ => null,
        };

    /// <summary>Core's card title for <paramref name="resolution"/>, or
    /// null for an unresolved embed.</summary>
    internal static string? TitleOf(EmbedResolution resolution) =>
        Of(resolution) is { } resolved
            ? SlateUniffiMethods.ResolvedEmbedTitle(resolved)
            : null;
}
