// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-8 (#740): the sync-diagnostics label vocabulary — the mac
/// SyncDiagnosticsPanel.swift strings, host-duplicated by designation
/// (§W-C label goldens: each platform pins the identical verbatim
/// strings in its own unit tests; recorded in w_c_matrix.md). The
/// LOCK-STEP rule: a change here is a change in
/// apps/slate-mac/Sources/SlateMac/SyncDiagnosticsPanel.swift in the
/// same commit, or the platforms have silently diverged.
///
/// Contract SD10 is the complete inventory: nothing else in the leaf
/// is host-composed. Every provider display name, recommendation
/// sentence, evidence path, multi-sync warning, and summary sentence
/// is core's verbatim (SD1) and never passes through this class.
/// </summary>
internal static class SyncPhrase
{
    /// <summary>The risk word of the badge and of the composed
    /// provider-row name. The WORD is the non-color channel (SD3's
    /// "never color alone"): Windows has no symbol-font convention to
    /// mirror mac's SF Symbols, so the text carries the semantics and
    /// the brush is decoration on top of it.</summary>
    public static string RiskWord(RiskLevel risk) => risk switch
    {
        RiskLevel.High => "High risk",
        RiskLevel.Medium => "Medium risk",
        _ => "Low risk",
    };

    /// <summary>The populated header — mac's
    /// "Sync, \(CountCopy.counted(count, "system", "systems"))
    /// detected".</summary>
    public static string CountHeader(int count) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Sync, {count} {(count == 1 ? "system" : "systems")} detected");

    /// <summary>The refresh control's VISIBLE label — a contiguous
    /// prefix of <see cref="RefreshAccessibleName"/> (WCAG 2.5.3
    /// label-in-name, so "click Refresh" matches by voice).</summary>
    public const string Refresh = "Refresh";

    public const string RefreshAccessibleName = "Refresh sync diagnostics";

    public const string Loading = "Loading sync diagnostics";

    public const string Retry = "Retry";

    /// <summary>The error state's line: the host prefix plus core's
    /// relayed message (SD5).</summary>
    public static string LoadError(string message) =>
        "Could not load sync diagnostics: " + message;

    /// <summary>The multi-sync row's combined name: the host prefix
    /// plus core's warning sentence, verbatim (SD1/SD3).</summary>
    public static string Warning(string warning) => "Warning: " + warning;

    public const string Evidence = "Evidence";

    /// <summary>The evidence expander's ACCESSIBLE name: "Evidence"
    /// alone makes every provider's disclosure an identical focusable
    /// sibling (axe SiblingUniqueAndFocusable — caught by the W4-8
    /// journey's first live run). The visible header stays
    /// <see cref="Evidence"/>, a contiguous prefix of this name
    /// (WCAG 2.5.3, the Refresh-button shape). WINDOWS-ONLY golden
    /// for now: mac's bare DisclosureGroup("Evidence")
    /// (SyncDiagnosticsPanel.swift:180) carries the same defect —
    /// tracked for the mac twin; SDD-4 records the interim
    /// divergence.</summary>
    public static string EvidenceFor(string displayName) =>
        Evidence + ", " + displayName;

    public const string LiveSyncConfiguration = "LiveSync configuration";

    // The six config-row labels, in mac's render order.
    public const string ServerHost = "Server host";

    public const string Database = "Database";

    public const string LiveSyncEnabled = "Live sync";

    public const string SyncOnSave = "Sync on save";

    public const string SyncOnStart = "Sync on start";

    public const string EndToEndEncryption = "End-to-end encryption";

    /// <summary>An absent field (the plugin's schema drifts) reads
    /// "Unknown", never blank — the m_spec §M-3 rule.</summary>
    public const string Unknown = "Unknown";

    public const string On = "On";

    public const string Off = "Off";

    /// <summary>Optional booleans to their spoken words.</summary>
    public static string OnOff(bool? value) => value switch
    {
        true => On,
        false => Off,
        _ => Unknown,
    };

    /// <summary>A config row's combined accessible name AND its
    /// visible "{label}: {value}" pairing.</summary>
    public static string ConfigRow(string label, string value) =>
        label + ": " + value;

    /// <summary>Core's reason relayed behind the host prefix (SD3(4):
    /// everything but {reason} is a golden).</summary>
    public static string ConfigMalformed(string reason) =>
        "LiveSync config could not be read: " + reason;

    public const string ConfigAbsent = "LiveSync plugin present; no config found.";
}
