// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// What a resolved embed card shows, as data (#1278; locked decision 05
/// §1.1, contract 38): the resolution's kind and identifying fields, never
/// a title. Core words the card title from it
/// (<see cref="SlateUniffiMethods.ResolvedEmbedTitle"/>) and the
/// <c>EmbedPreviewShown</c> announcement alike. Every title sink — the
/// Ctrl+E popover's header and cards, the embeds leaf and the reading view's
/// embed headers — calls core with this data directly, so no host code
/// composes a title (EmbedPreviewTitleCensus reads each sink). A whole
/// file core classifies as a base (<see cref="SlateUniffiMethods.EmbedTargetKind"/>,
/// #1288) is <see cref="ResolvedEmbed.Base"/> on every surface; the reading
/// view's summary card for it is Bases contract C10's.
/// </summary>
internal static class ResolvedEmbeds
{
    /// <summary>The card data for <paramref name="resolution"/>, or null
    /// for an unresolved embed, which is not a card.</summary>
    internal static ResolvedEmbed? Of(EmbedResolution resolution) =>
        resolution switch
        {
            EmbedResolution.FullNote full when SlateUniffiMethods.EmbedTargetKind(full.TargetPath) == EmbedTargetKind.Base =>
                new ResolvedEmbed.Base(full.TargetPath),
            EmbedResolution.FullNote full => new ResolvedEmbed.Note(full.TargetPath),
            EmbedResolution.Section section =>
                new ResolvedEmbed.Section(section.TargetPath, section.Heading),
            EmbedResolution.Block block => new ResolvedEmbed.Block(block.TargetPath),
            EmbedResolution.Image image => new ResolvedEmbed.Image(image.TargetPath, image.Alt),
            _ => null,
        };
}
