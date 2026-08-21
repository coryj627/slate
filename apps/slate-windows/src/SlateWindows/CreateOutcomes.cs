// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// The host side of core's typed create outcome (#1123). Every create
/// funnel — the sidebar's untitled note and folder note, duplicate, the
/// history Restore As…, the template flow — calls
/// <c>CreateExclusiveReporting</c> and finishes its flow against the
/// LANDED file on a post-publish failure: the bytes are real and readable
/// (core serves reads from disk), the index catches up on the next scan,
/// and the one thing a host must never do is retry under another name.
/// </summary>
internal static class CreateOutcomes
{
    /// <summary>W0.5-3 residue: the post-publish caveat — spoken after
    /// the ordinary "created" sentence, never instead of it, because the
    /// create DID happen.</summary>
    internal static string PublishedUnindexedCaveat(string name, string reason) =>
        $"{name} was written but not indexed: {reason} "
        + "It will appear in listings after the next scan; do not recreate it.";

    /// <summary>Runs a create and separates the two landed arms from the
    /// refusal: returns the landed path's caveat (null when committed);
    /// a refusal throws its <see cref="VaultException"/> as before, so
    /// the typed <c>DestinationExists</c> advance signal keeps working.</summary>
    internal static string? CreateReporting(
        VaultSession session, string path, string content, string displayName)
    {
        CreateExclusiveOutcome outcome = session.CreateExclusiveReporting(path, content);
        return outcome switch
        {
            CreateExclusiveOutcome.Committed => null,
            CreateExclusiveOutcome.PublishedUnindexed published =>
                PublishedUnindexedCaveat(displayName, published.ErrorMessage),
            _ => throw new InvalidOperationException(
                $"unknown create outcome {outcome.GetType().Name}"),
        };
    }
}
