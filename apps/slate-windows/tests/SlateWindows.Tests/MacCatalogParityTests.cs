// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using SlateWindows.Commands;

namespace SlateWindows.Tests;

/// <summary>
/// Contract P3: for every capability that exists on both platforms, the
/// Windows catalog's id and label are byte-identical to mac's.
/// </summary>
/// <remarks>
/// <para>
/// Until now nothing read mac's source. The registration tests compare
/// the live registry back to <see cref="ChordTable"/> and then compare
/// each registered field back to that same table — so changing a shared
/// id or label on the Windows side left every test green while breaking
/// the cross-platform contract the whole catalog rests on. A reviewer
/// verified all 101 shared ids by hand in round 1 and codex flagged the
/// absent gate as high; hand verification is not a gate.
/// </para>
/// <para>
/// mac is the source of truth, read directly from
/// <c>SlateCommands.swift</c>. Windows-only ids are dispositioned
/// explicitly and the disposition list is checked for staleness in both
/// directions, so an entry cannot linger and shield a real divergence.
/// </para>
/// </remarks>
public sealed class MacCatalogParityTests
{
    [Fact]
    public void EverySharedCommandIdCarriesMacsLabel()
    {
        IReadOnlyDictionary<string, string> mac = MacLabelsById();
        Assert.NotEmpty(mac);
        IReadOnlySet<string> macIds = MacDeclaredIds();

        var divergences = new List<string>();
        foreach (ChordTableEntry row in ChordTable.Entries.Where(row => row.IsCommandId))
        {
            if (!mac.TryGetValue(row.Id, out string? macLabel))
            {
                // Not silently skipped: a shared id whose mac label could
                // not be parsed must say so, or an unparsed mac syntax
                // hides real divergence the way the generated slot calls
                // did.
                Assert.True(
                    !macIds.Contains(row.Id) || LabelNotMachineReadable.ContainsKey(row.Id),
                    $"{row.Id} is shared with mac but no mac label could be "
                    + "parsed, so its label is unchecked. Extend the parser or "
                    + "record it in " + nameof(LabelNotMachineReadable) + ".");
                continue;
            }

            if (!string.Equals(macLabel, row.Label, StringComparison.Ordinal))
            {
                divergences.Add(
                    $"{row.Id}: windows={row.Label} mac={macLabel}");
            }
        }

        Assert.True(
            divergences.Count == 0,
            "shared capabilities whose label diverges from mac (P3 requires "
            + "byte-identical): " + string.Join("; ", divergences));
    }

    /// <summary>
    /// Every Windows-only id is deliberate, and every recorded
    /// Windows-only id still exists.
    /// </summary>
    /// <remarks>
    /// Checked in both directions — mac's own drift tests are
    /// non-bypassable for exactly this reason. A stale entry would shield
    /// the next id that drifts away from mac's spelling.
    /// </remarks>
    [Fact]
    public void WindowsOnlyIdsAreDispositionedAndTheListIsNotStale()
    {
        IReadOnlyDictionary<string, string> mac = MacLabelsById();
        Assert.NotEmpty(mac);

        IReadOnlySet<string> macIds = MacDeclaredIds();
        Assert.NotEmpty(macIds);

        string[] windowsOnly = ChordTable.Entries
            .Where(row => row.IsCommandId && !macIds.Contains(row.Id))
            .Select(row => row.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] undeclared = windowsOnly
            .Where(id => !WindowsOnlyIds.ContainsKey(id))
            .ToArray();
        Assert.True(
            undeclared.Length == 0,
            "ids absent from mac's catalog with no recorded reason: "
            + string.Join(", ", undeclared)
            + ". Either the id drifted from mac's spelling — which P3 forbids "
            + "— or it is genuinely Windows-only and needs an entry.");

        string[] stale = WindowsOnlyIds.Keys
            .Where(id => !windowsOnly.Contains(id, StringComparer.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            stale.Length == 0,
            "recorded Windows-only ids that mac now defines, or that no "
            + "longer exist here: " + string.Join(", ", stale)
            + ". A stale entry shields the next real divergence.");

        foreach ((string id, string reason) in WindowsOnlyIds)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(reason),
                $"Windows-only id {id} carries no reason.");
        }
    }

    /// <summary>
    /// Ids the Windows catalog mints that mac does not define, each with
    /// why.
    /// </summary>
    private static readonly Dictionary<string, string> WindowsOnlyIds =
        new(StringComparer.Ordinal)
        {
            ["slate.workspace.focusNextPane"] =
                "Windows-only pane cycling; mac navigates panes directionally only.",
            ["slate.workspace.focusPreviousPane"] =
                "Windows-only pane cycling; mac navigates panes directionally only.",
            ["slate.editor.activateAtCaret"] =
                "The Windows editor's explicit activate verb; mac activates through "
                + "its own responder chain and registers no command.",
            ["slate.editor.previewEmbed"] =
                "The Windows editor's embed-preview verb; mac has no registered twin.",
            ["slate.editor.toggleReadingLinkTarget"] =
                "A Windows reading-view preference with no mac command id.",
            ["slate.editor.toggleHistoryChangesSinceOpen"] =
                "A Windows history preference with no mac command id.",
            ["slate.editor.reloadBibliography"] =
                "Windows exposes an explicit bibliography reload; mac reloads implicitly.",
            ["slate.sidebar.refresh"] =
                "Windows exposes an explicit sidebar refresh; mac refreshes implicitly.",
            ["slate.sidebar.clearFilter"] =
                "Windows exposes clearing the filter as its own verb.",
            ["slate.sidebar.toggleTags"] =
                "Windows-only sidebar tag-pane toggle.",
            ["slate.sidebar.trashSelected"] =
                "The single-node trash verb; mac's slate.file.delete is the batch form, "
                + "which Windows also registers.",
        };

    /// <summary>
    /// Shared ids whose mac label genuinely is not machine-readable, with
    /// why. Kept small and explicit — every entry is label coverage this
    /// gate does not provide.
    /// </summary>
    private static readonly Dictionary<string, string> LabelNotMachineReadable =
        new(StringComparer.Ordinal)
        {
            ["slate.file.cancelImport"] =
                "mac carries this label on CancelImportCommandContract rather than "
                + "in a register(...) call or the sidebar catalog.",
        };

    /// <summary>
    /// mac's generated shortcut-slot rows:
    /// <c>SlateCommandID.sidebarOpenShortcut(1), "Open Shortcut 1"</c>.
    /// </summary>
    private const string SidebarSlotLabelPattern =
        "SlateCommandID\\.sidebarOpenShortcut\\((\\d)\\),\\s*\"((?:[^\"\\\\]|\\\\.)*)\"";

    /// <summary>
    /// Every <c>slate.*</c> id mac declares, whether or not its label is
    /// machine-discoverable.
    /// </summary>
    /// <remarks>
    /// Shared-vs-Windows-only is decided here rather than by the label
    /// map, because two mac shapes declare an id whose label lives
    /// elsewhere: <c>slate.file.cancelImport</c> carries its label on a
    /// command-contract type, and the nine sidebar shortcut slots are
    /// SYNTHESISED from a marker rather than written out. Keying on the
    /// label map called all ten Windows-only, which would have been a
    /// false disposition recorded as fact.
    /// </remarks>
    private static IReadOnlySet<string> MacDeclaredIds()
    {
        string commands = SwiftSource.WithoutComments(File.ReadAllText(MacCommandsPath()));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            commands, @"static let \w+(?::\s*String)?\s*=\s*""(slate\.[A-Za-z0-9.]+)"""))
        {
            ids.Add(match.Groups[1].Value);
        }

        // The slot family, mirroring how the parity generator reads it.
        if (commands.Contains("sidebarOpenShortcutSlots", StringComparison.Ordinal))
        {
            for (int slot = 1; slot <= 9; slot++)
            {
                ids.Add($"slate.sidebar.openShortcut{slot}");
            }
        }

        return ids;
    }

    /// <summary>
    /// mac's declared id-to-label map, read from its own source.
    /// </summary>
    private static IReadOnlyDictionary<string, string> MacLabelsById()
    {
        string commands = SwiftSource.WithoutComments(File.ReadAllText(MacCommandsPath()));

        // static let <name> = "slate.…"
        var idByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            commands, @"static let (\w+)(?::\s*String)?\s*=\s*""(slate\.[A-Za-z0-9.]+)"""))
        {
            idByName[match.Groups[1].Value] = match.Groups[2].Value;
        }

        Assert.NotEmpty(idByName);

        // register(SlateCommandID.<name>, label: "…") and the contract-type
        // form, both of which mac uses.
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            commands,
            @"SlateCommandID\.(\w+)[\s\S]{0,400}?label:\s*""((?:[^""\\]|\\.)*)"""))
        {
            if (idByName.TryGetValue(match.Groups[1].Value, out string? id)
                && !labels.ContainsKey(id))
            {
                labels[id] = Unescape(match.Groups[2].Value);
            }
        }

        // The sidebar family declares its ids in SlateCommands.swift but its
        // LABELS in the action catalog, as positional factory arguments.
        // Reading only the first file left 43 shared ids looking
        // Windows-only — a scrape that under-matches rather than a real
        // divergence, which is the class this project keeps hitting.
        string catalog = SwiftSource.WithoutComments(File.ReadAllText(
            Path.Combine(MacSourceRoot(), "Sidebar", "SidebarActionCatalog.swift")));
        foreach (Match match in Regex.Matches(
            catalog,
            SidebarCatalogLabelPattern))
        {
            if (idByName.TryGetValue(match.Groups[1].Value, out string? id)
                && !labels.ContainsKey(id))
            {
                labels[id] = Unescape(match.Groups[2].Value);
            }
        }

        // The nine slots are written as a GENERATED call —
        // SlateCommandID.sidebarOpenShortcut(1), "Open Shortcut 1" — which
        // the identifier-comma pattern above cannot match. Codex caught
        // that all nine were bypassing label parity in silence.
        foreach (Match match in Regex.Matches(catalog, SidebarSlotLabelPattern))
        {
            string id = $"slate.sidebar.openShortcut{match.Groups[1].Value}";
            if (!labels.ContainsKey(id))
            {
                labels[id] = Unescape(match.Groups[2].Value);
            }
        }

        return labels;
    }

    /// <summary>
    /// mac's sidebar catalog passes the id and label positionally:
    /// <c>SlateCommandID.newNote, "New Note"</c>.
    /// </summary>
    private const string SidebarCatalogLabelPattern =
        "SlateCommandID\\.(\\w+),\\s*\"((?:[^\"\\\\]|\\\\.)*)\"";

    private static string Unescape(string value) =>
        value.Replace("\\\\", "\\", StringComparison.Ordinal)
            .Replace("\\\"", "\"", StringComparison.Ordinal);

    /// <summary>
    /// mac's command catalog. Exposed so the Swift stripper's
    /// over-stripping guard can measure itself against the real file
    /// rather than a sample.
    /// </summary>
    internal static string MacCommandsPath() =>
        Path.Combine(MacSourceRoot(), "SlateCommands.swift");

    private static string MacSourceRoot() =>
        Path.Combine(RepoRoot(), "apps", "slate-mac", "Sources", "SlateMac");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
