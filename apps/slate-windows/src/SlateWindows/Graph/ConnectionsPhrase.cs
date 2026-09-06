// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR B, slice B1 (#746), contract B-16: the leaf's label inventory
/// T1–T17, the mac's strings byte for byte (`ConnectionsPanel.swift`),
/// plus the one Windows-only B1 string (B-D6). Labels, never announced —
/// every spoken line is core's through the relay.
/// </summary>
internal static class ConnectionsPhrase
{
    /// <summary>T1 — no note in view (`:23`).</summary>
    public const string NoNote = "Select a note to see its connections.";

    /// <summary>T2 — the loading row's visible text (`:141`).</summary>
    public const string LoadingVisible = "Loading connections…";

    /// <summary>T3 — the loading row's accessible name (`:148`).</summary>
    public const string LoadingAccessible = "Loading connections.";

    /// <summary>T4 — the empty neighbourhood (`:129`, `:134`).</summary>
    public const string Empty = "This note has no connections.";

    /// <summary>T5 — the error label (`:125`).</summary>
    public static string Error(string message) => "Connections error: " + message;

    /// <summary>T6, T7 — the group titles (`:153–154`).</summary>
    public const string IncomingTitle = "Linked from";
    public const string OutgoingTitle = "Links to";

    /// <summary>T8, T9 — the group headers with their counts (`:192`).</summary>
    public static string GroupHeader(string title, int count) =>
        title + ", " + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + " " + (count == 1 ? "note" : "notes");

    /// <summary>T10, T11 — the group empties (`:153–154`).</summary>
    public const string IncomingEmpty = "Nothing links here.";
    public const string OutgoingEmpty = "This note links to nothing.";

    /// <summary>T12–T14 — the badges, in order (`:311`, `:313`, `:315`).</summary>
    public const string BadgeUnresolved = "Unresolved";
    public const string BadgeEmbed = "Embed";
    public const string BadgeAttachment = "Attachment";

    /// <summary>T15, T16 — the row hints (`:250`).</summary>
    public const string GhostHint = "Unresolved. Choose Create note to add it.";
    public const string NoteHint = "Opens the note.";

    /// <summary>T17 — the depth control (`:97`, `:108`) and its tags (`:102–104`).</summary>
    public const string DepthName = "Local graph depth";
    public const string DepthHint = "How many links away from this note to include.";
    public static readonly string[] DepthTags = ["Links", "2 links away", "3 links away"];

    /// <summary>B-D6 — Show connections, listed and disabled until B2, on
    /// the menu action's HelpText alone (B-9). Windows-only; no mac twin.</summary>
    public const string ShowConnectionsUnavailable = "Show connections is not available yet.";

    /// <summary>The leaf heading's name with no note (the mac's
    /// `navigationTitle`, `:29`); with a note, the note's file name (`:77–79`).</summary>
    public const string Title = "Connections";
}
