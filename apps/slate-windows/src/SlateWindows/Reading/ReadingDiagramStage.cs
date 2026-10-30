// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Media;
using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// What one diagram fence became (WB-20, #1317): the outcome the reading
/// projection's diagram stage resolves off the dispatcher, and the shape the
/// builder and the in-place completion both build from it
/// (<see cref="ReadingDocumentBuilder.ApplyRenderedShape"/>,
/// <see cref="ReadingDocumentBuilder.ApplyFailureShape"/>), so a completed
/// paragraph and a fresh complete build cannot drift.
/// </summary>
internal abstract record DiagramOutcome
{
    private DiagramOutcome()
    {
    }

    /// <summary>Core rendered it and the host decoded the SVG: the visual is
    /// frozen, so it crosses from the stage's worker to the dispatcher.</summary>
    internal sealed record Rendered(string Description, string Landing, ImageSource Visual)
        : DiagramOutcome;

    /// <summary>An honest failure: the header, the reason and the source go
    /// in the paragraph's text. <paramref name="Transient"/> marks a refusal
    /// that a retry may not repeat — core's "busy" render or the host's
    /// "image-busy" decode — which is shown but never remembered, so the
    /// next refresh retries it (the owner's call of 2026-10-26).</summary>
    internal sealed record Failed(
        string Description, string Landing, string Header, string Reason, bool Transient)
        : DiagramOutcome;

    /// <summary>No artifact matches the fence: the live buffer drifted from
    /// the saved file, or the stage's fetch degraded. The fence keeps its
    /// source, and the next refresh tries it again.</summary>
    internal sealed record Unmatched : DiagramOutcome;
}

/// <summary>A diagram the last completed stage landed for this path, kept
/// so the next projection builds its fence complete with no decode on the
/// dispatcher. Only <see cref="DiagramOutcome.Rendered"/> and a
/// non-transient <see cref="DiagramOutcome.Failed"/> are kept.</summary>
internal sealed record LandedDiagram(DiagramBlock Artifact, DiagramOutcome Outcome);

/// <summary>A fence a stage-1 build left pending: <paramref name="Key"/> is
/// its block's byte start in decimal (unique within one projection), the key
/// the pending paragraph is marked with.</summary>
internal sealed record PendingFence(string Key, ReadingBlock Block, string FallbackSource);
