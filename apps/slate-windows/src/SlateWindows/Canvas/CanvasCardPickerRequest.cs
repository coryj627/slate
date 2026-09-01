// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>§F TF-7 (F5/F6): what a card picker is FOR.</summary>
internal enum CanvasCardPickerPurpose
{
    PlaceBelow,
    PlaceRightOf,
    PlaceAbove,
    PlaceLeftOf,
    AlignWith,
}

/// <summary>
/// §F TF-7 (IF-19): the picker's IMMUTABLE operation context,
/// captured whole at open — the reading-ordered moving ids, their
/// rects, and the loaded identity the capture ran against. A confirm
/// after the world moved re-validates against THIS, never against
/// live state: a reload between open and confirm refuses
/// PickDifferentTarget rather than moving the wrong set.
/// </summary>
internal sealed record CanvasCardPickerRequest(
    CanvasCardPickerPurpose Purpose,
    ImmutableArray<string> Moving,
    ImmutableDictionary<string, CanvasRect> Rects,
    CanvasLoaded Identity);
