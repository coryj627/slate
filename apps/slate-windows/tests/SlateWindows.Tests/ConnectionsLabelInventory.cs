// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Commands;
using SlateWindows.Graph;

namespace SlateWindows.Tests;

/// <summary>
/// Contract 35 B-16 (as amended by the owner, #1274): the Connections leaf's
/// label inventory as DATA — the mac's strings T1–T17 byte for byte and the
/// one Windows-only composed form, T16a (the 0a-12 manifest's W1, W7-7
/// R-13): a file-backed row's hint, T16 then the new-tab clause whose chord
/// is the chord table's spoken column through <see cref="NavigationHelp"/>
/// (contract 39 N-1/N-2), never a literal. Every fact over the leaf's
/// strings reads its expectation here; the theory holds
/// <see cref="ConnectionsPhrase"/> to it in both directions.
/// </summary>
internal static class ConnectionsLabelInventory
{
    /// <summary>T16a's chord row: the tree's Ctrl+Enter (W7-7 R-13).</summary>
    internal const string NewTabChordRow = "windows.connections.openInNewTab";

    /// <summary>T16a with its chord spelled by the caller — the
    /// substitution the theory exercises.</summary>
    internal static string NoteRowHint(string spokenChord) =>
        "Opens the note. " + spokenChord + " opens it in a new tab.";

    /// <summary>T16a as a file-backed row carries it: the chord from its
    /// row, through <see cref="NavigationHelp"/>.</summary>
    internal static string NoteRowHint() => NoteRowHint(NavigationHelp.Spoken(NewTabChordRow));

    /// <summary>One entry per (member, case): the inventory's number, the
    /// <see cref="ConnectionsPhrase"/> member it pins, the expected text and
    /// the shipped value. A composing member has a case per substitution.</summary>
    internal static IReadOnlyList<(string Item, string Member, string Case, string Expected, Func<string> Actual)> Entries { get; } =
    [
        ("T1", nameof(ConnectionsPhrase.NoNote), "", "Select a note to see its connections.", () => ConnectionsPhrase.NoNote),
        ("T2", nameof(ConnectionsPhrase.LoadingVisible), "", "Loading connections…", () => ConnectionsPhrase.LoadingVisible),
        ("T3", nameof(ConnectionsPhrase.LoadingAccessible), "", "Loading connections.", () => ConnectionsPhrase.LoadingAccessible),
        ("T4", nameof(ConnectionsPhrase.Empty), "", "This note has no connections.", () => ConnectionsPhrase.Empty),
        ("T5", nameof(ConnectionsPhrase.Error), "boom", "Connections error: boom", () => ConnectionsPhrase.Error("boom")),
        ("T6", nameof(ConnectionsPhrase.IncomingTitle), "", "Linked from", () => ConnectionsPhrase.IncomingTitle),
        ("T7", nameof(ConnectionsPhrase.OutgoingTitle), "", "Links to", () => ConnectionsPhrase.OutgoingTitle),
        ("T8", nameof(ConnectionsPhrase.GroupHeader), "Linked from, 1", "Linked from, 1 note",
            () => ConnectionsPhrase.GroupHeader(ConnectionsPhrase.IncomingTitle, 1)),
        ("T9", nameof(ConnectionsPhrase.GroupHeader), "Links to, 0", "Links to, 0 notes",
            () => ConnectionsPhrase.GroupHeader(ConnectionsPhrase.OutgoingTitle, 0)),
        ("T9", nameof(ConnectionsPhrase.GroupHeader), "Links to, 12", "Links to, 12 notes",
            () => ConnectionsPhrase.GroupHeader(ConnectionsPhrase.OutgoingTitle, 12)),
        ("T10", nameof(ConnectionsPhrase.IncomingEmpty), "", "Nothing links here.", () => ConnectionsPhrase.IncomingEmpty),
        ("T11", nameof(ConnectionsPhrase.OutgoingEmpty), "", "This note links to nothing.", () => ConnectionsPhrase.OutgoingEmpty),
        ("T12", nameof(ConnectionsPhrase.BadgeUnresolved), "", "Unresolved", () => ConnectionsPhrase.BadgeUnresolved),
        ("T13", nameof(ConnectionsPhrase.BadgeEmbed), "", "Embed", () => ConnectionsPhrase.BadgeEmbed),
        ("T14", nameof(ConnectionsPhrase.BadgeAttachment), "", "Attachment", () => ConnectionsPhrase.BadgeAttachment),
        ("T15", nameof(ConnectionsPhrase.GhostHint), "", "Unresolved. Choose Create note to add it.", () => ConnectionsPhrase.GhostHint),
        ("T16", nameof(ConnectionsPhrase.NoteHint), "", "Opens the note.", () => ConnectionsPhrase.NoteHint),
        ("T16a", nameof(ConnectionsPhrase.NoteHintWithNewTab), "a substituted chord", NoteRowHint("Chord X"),
            () => ConnectionsPhrase.NoteHintWithNewTab("Chord X")),
        ("T16a", nameof(ConnectionsPhrase.NoteHintWithNewTab), NewTabChordRow, NoteRowHint(),
            () => ConnectionsPhrase.NoteHintWithNewTab(NavigationHelp.Spoken(NewTabChordRow))),
        ("T17", nameof(ConnectionsPhrase.DepthName), "", "Local graph depth", () => ConnectionsPhrase.DepthName),
        ("T17", nameof(ConnectionsPhrase.DepthHint), "", "How many links away from this note to include.", () => ConnectionsPhrase.DepthHint),
        ("T17", nameof(ConnectionsPhrase.DepthTags), "", "Links; 2 links away; 3 links away",
            () => string.Join("; ", ConnectionsPhrase.DepthTags)),
        ("title", nameof(ConnectionsPhrase.Title), "", "Connections", () => ConnectionsPhrase.Title),
    ];
}
