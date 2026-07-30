// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// One block-embed card's resolved content (W3-5), fetched on the
/// background task alongside the other reading artifacts. A null
/// <see cref="Resolution"/> is the PER-KEY degraded state — an FFI
/// failure or a note-wide budget refusal — rendered as a header-only
/// card that still activates through <c>ReadingMatchLink</c>.
/// <see cref="Alt"/> is the embed link record's authored display
/// text, threaded so image cards title from the author's alt (the
/// mac batch precedent, minus its fallback path's alt loss).
/// </summary>
/// <summary><see cref="ImageBudgetRefused"/> marks a SUCCESSFULLY
/// resolved image whose payload the note-wide byte pool refused: the
/// card keeps its true identity and destination, only the bytes are
/// absent, and the builder says exactly that (round 1 [medium]: a
/// budget refusal must never claim decode failure or turn an image
/// into a "note").</summary>
internal sealed record ReadingEmbedArtifact(
    string Key,
    string? Alt,
    EmbedPreviewResolution? Resolution,
    bool ImageBudgetRefused = false);
