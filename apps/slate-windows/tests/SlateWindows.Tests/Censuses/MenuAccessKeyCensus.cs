// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-22 (#1661): no two items of one menu level share an access key.
//
// WPF resolves an access key pressed in an open menu among the items of that
// level that carry it and are enabled. With two enabled items the key moves
// between them and runs neither. With one, it runs that one at once. So a
// key shared by two items runs the wrong command the moment the item the
// reader learned it for is disabled: in the Editor menu "_Activate at Cursor"
// and "_Actual Size" shared A, and with Activate at Cursor unavailable in
// reading mode A reset the reader's text size.
//
// The census reads every menu the shell declares in XAML: each Menu, each
// ContextMenu and each MenuItem that holds items is one level, and the items
// directly under it are compared by access key (the character after the first
// single underscore of the header; a doubled underscore is a literal one; the
// keys compare without case). A menu built in code is outside it.
//
// CB-22 gave the Editor menu's two shared keys letters of their own. The
// sixteen other shared keys it found stand in the dated table below, each
// with its items and what the key does today, until CB-22b puts their letters
// to the owner as one table and ends every row.

using System.Xml;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "menu-access-keys")]
public sealed class MenuAccessKeyCensus
{
    /// <summary>One access key carried by two or more items of one level,
    /// in the order the menu shows them.</summary>
    internal sealed record SharedKey(
        string File,
        string Level,
        int LevelLine,
        char Key,
        IReadOnlyList<(string Header, int Line)> Items)
    {
        public override string ToString() =>
            $"{File}: level {Level} (line {LevelLine}): '{Key}' is carried by "
            + string.Join(" and ", Items.Select(item => $"\"{item.Header}\" (line {item.Line})"));
    }

    /// <summary>A shared key that stays for now: where it is, the items that
    /// carry it, whether one of them can be enabled alone (the state in which
    /// the key runs that one at once), when the row was written and why it
    /// stays. The lines are as read on the row's date.</summary>
    private sealed record Exemption(
        string File,
        string Level,
        char Key,
        string[] Items,
        string Alone,
        string Dated,
        string Reason);

    private const string Sweep = "ended by CB-22b";

    /// <summary>The shared keys CB-22 found beside the Editor menu's own two
    /// and did not move. A row that matches no shared key is an error, and so
    /// is a shared key whose items are not exactly the row's: an exemption
    /// covers the items it names and no item that joins them later.</summary>
    private static readonly Exemption[] Exemptions =
    [
        new("MainWindow.xaml", "FileMenu", 'C',
            ["New _Canvas", "_Close Vault"],
            "Lines 301 and 305. Alone: yes. During an import or a trash New Canvas is off, and C runs Close Vault at once.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "FilesSidebarMenu", 'S',
            ["Open in _Split", "Add to _Shortcuts", "Use Vault Default _Sort"],
            "Lines 325, 364 and 370. Alone: yes. With no row selected and a changed sort, S runs Use Vault Default Sort at once.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "FilesSidebarMenu", 'C',
            ["_Cancel Import", "_Collapse All"],
            "Lines 332 and 339. Alone: yes, the usual state. C runs Collapse All; during an import it only moves.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "FilesSidebarMenu", 'R',
            ["_Refresh", "_Remove Shortcut", "Clear _Recents"],
            "Lines 336, 367 and 373. Alone: yes. Whichever of the three is on alone runs at once.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "FilesSidebarMenu", 'F',
            ["Focus _Filter", "_Forward"],
            "Lines 345 and 351. Alone: yes, the usual state. F runs Focus Filter; with forward history it only moves.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'D',
            ["_Duplicate Tab", "Split _Down"],
            "Lines 387 and 392. Alone: yes. Whichever is on alone runs at once.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'C',
            ["_Close Tab", "Show _Changes Since Last Open"],
            "Lines 388 and 455. Alone: yes. With no tab open, C toggles Show Changes Since Last Open.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'R',
            ["_Reopen Closed Tab", "Split _Right", "Focus Pane _Right", "Focus Next _Region", "Show Tasks _Review"],
            "Lines 389, 391, 396, 399 and 443. Alone: no. Three of the five are always on, so R only moves.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'P',
            ["Close _Pane", "Toggle Right _Pane"],
            "Lines 393 and 428. Alone: yes, the usual state. With one pane, P toggles the right pane.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'G',
            ["Focus Previous Re_gion", "_Grow Pane"],
            "Lines 403 and 422. Alone: yes, the usual state. With one pane, G runs Focus Previous Region.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'S',
            ["_Shrink Pane", "Refresh _Sync Diagnostics"],
            "Lines 423 and 465. Alone: yes, the usual state. With one pane, S runs Refresh Sync Diagnostics.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "WorkspaceMenu", 'H',
            ["Switch T_heme…", "Show _History Panel"],
            "Lines 429 and 450. Alone: no. Both are always on, so H only moves.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "BaseMenu", 'C',
            ["Sort by _Column", "Export View as _CSV…"],
            "Lines 506 and 527. Alone: not settled. Both go through one command gate, by reading.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "BaseMenu", 'K',
            ["Copy Lin_k", "Copy View as Mar_kdown"],
            "Lines 517 and 533. Alone: yes. With no row, K runs Copy View as Markdown.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "CanvasMenu", 'T',
            ["Create Connec_ted Card", "_Trace Path from Selected Card"],
            "Lines 704 and 731. Alone: not settled. The two go through different command gates.",
            "2026-11-21", Sweep),
        new("MainWindow.xaml", "GraphMenu", 'O',
            ["_Open Graph", "Zoom _Out"],
            "Lines 792 and 814. Alone: yes, the usual state. With no graph showing, O runs Open Graph; on a graph tab it only moves.",
            "2026-11-21", Sweep),
    ];

    [Fact]
    public void NoTwoItemsOfOneMenuLevelShareAnAccessKey()
    {
        string root = SourceText.ShellSourceRoot();
        string[] files = Directory
            .EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(file => !InBuildOutput(root, file))
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(files);

        var shared = new List<SharedKey>();
        int levels = 0;
        int keyed = 0;
        foreach (string file in files)
        {
            string name = Path.GetRelativePath(root, file).Replace('\\', '/');
            XDocument document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (MenuLevel level in LevelsOf(document))
            {
                levels++;
                keyed += level.Items.Count(item => item.Key is not null);
                shared.AddRange(SharedKeysIn(name, level));
            }
        }

        // The scrape's own tripwire: the shell's two menu homes are both seen,
        // items and keys included, so an under-matching scrape fails here by
        // name and never passes as "no shared key".
        Assert.True(levels >= 10, $"the census saw {levels} menu levels; the shell's menus hold more than ten.");
        Assert.True(keyed >= 100, $"the census read {keyed} access keys; the shell's menus hold more than a hundred.");

        string[] unexplained = [.. Exemptions
            .Where(row => string.IsNullOrWhiteSpace(row.Alone)
                || string.IsNullOrWhiteSpace(row.Dated)
                || string.IsNullOrWhiteSpace(row.Reason))
            .Select(Named)];
        Assert.True(
            unexplained.Length == 0,
            "Exemption rows with no alone state, no date or no reason: " + string.Join(", ", unexplained));

        string[] offenders = [.. shared
            .Where(key => !Exemptions.Any(row => Covers(row, key)))
            .Select(key => key.ToString())];
        Assert.True(
            offenders.Length == 0,
            "Items of one menu level share an access key. Give each a letter of its own (one that its header "
            + "holds and no sibling carries), or record the key in " + nameof(Exemptions)
            + " with exactly its items, its date and its reason:\n  " + string.Join("\n  ", offenders));

        string[] stale = [.. Exemptions
            .Where(row => !shared.Any(key => Covers(row, key)))
            .Select(Named)];
        Assert.True(
            stale.Length == 0,
            "An exemption row matches no shared key as it stands (the key is no longer shared, or its items "
            + "changed); remove or correct it:\n  " + string.Join("\n  ", stale));
    }

    private static string Named(Exemption row) => $"{row.File}: level {row.Level}: '{row.Key}'";

    private static bool Covers(Exemption row, SharedKey key) =>
        row.File == key.File
        && row.Level == key.Level
        && row.Key == key.Key
        && row.Items.SequenceEqual(key.Items.Select(item => item.Header), StringComparer.Ordinal);

    /// <summary>The census sees a planted pair, and reads a header as WPF
    /// does: the first single underscore marks the key, a doubled one is a
    /// literal underscore, a header with none has no key, the keys compare
    /// without case, and two levels never collide with each other.</summary>
    [Fact]
    public void TheCensusSeesAPlantedPairAndReadsHeadersAsWpfDoes()
    {
        const string planted = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:local="clr-namespace:SlateWindows">
              <Menu x:Name="Bar">
                <MenuItem Header="_Editor" AutomationProperties.AutomationId="EditorMenu">
                  <MenuItem Header="_Activate at Cursor" />
                  <MenuItem Header="Preview _Embed" />
                  <Separator />
                  <local:CheckMenuItem Header="Zoom _actual" />
                  <MenuItem Header="Snake__case _Name" />
                  <MenuItem Header="No key here" />
                  <MenuItem Header="{Binding Title}" />
                  <MenuItem Header="_Math">
                    <MenuItem Header="_Activate" />
                    <MenuItem Header="_Never" />
                  </MenuItem>
                  <MenuItem Header="_Actual Size" />
                </MenuItem>
                <MenuItem Header="_Workspace" />
              </Menu>
              <TextBox>
                <TextBox.ContextMenu>
                  <ContextMenu>
                    <MenuItem Header="_Copy" />
                    <MenuItem Header="Re_cord" />
                  </ContextMenu>
                </TextBox.ContextMenu>
              </TextBox>
            </Grid>
            """;

        XDocument document = XDocument.Parse(planted, LoadOptions.SetLineInfo);
        SharedKey[] found = [.. LevelsOf(document).SelectMany(level => SharedKeysIn("planted.xaml", level))];

        Assert.Equal(2, found.Length);
        SharedKey editor = Assert.Single(found, key => key.Key == 'A');
        Assert.Equal("EditorMenu", editor.Level);
        Assert.Equal(
            ["_Activate at Cursor", "Zoom _actual", "_Actual Size"],
            editor.Items.Select(item => item.Header));
        Assert.True(editor.Items[2].Line > editor.Items[0].Line);
        SharedKey context = Assert.Single(found, key => key.Key == 'C');
        Assert.Equal("ContextMenu of TextBox", context.Level);
        Assert.Equal(["_Copy", "Re_cord"], context.Items.Select(item => item.Header));

        // An exemption covers the items it names, in their order, and no others.
        var row = new Exemption(
            "planted.xaml", "EditorMenu", 'A', ["_Activate at Cursor", "Zoom _actual"], "yes", "2026-11-21", "planted");
        Assert.False(Covers(row, editor), "an exemption for two items covered a third that joined their key");
        Assert.True(Covers(row with { Items = ["_Activate at Cursor", "Zoom _actual", "_Actual Size"] }, editor));
        Assert.False(Covers(row with { Level = "Bar" }, editor));

        Assert.Equal('N', AccessKeyOf("Snake__case _Name"));
        Assert.Null(AccessKeyOf("No key here"));
        Assert.Null(AccessKeyOf("Ends with _"));
        Assert.Null(AccessKeyOf("{Binding Title}"));
        Assert.Equal('Z', AccessKeyOf("Actual Si_ze"));
        Assert.Equal('T', AccessKeyOf("Zoom Ou_t"));
        Assert.Equal('É', AccessKeyOf("_édition"));
    }

    private sealed record MenuLevel(string Name, int Line, IReadOnlyList<MenuEntry> Items);

    private sealed record MenuEntry(string Header, char? Key, int Line);

    private static bool InBuildOutput(string root, string file)
    {
        string[] segments = Path.GetRelativePath(root, file).Split('\\', '/');
        return segments.Contains("bin", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("obj", StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every menu level of a document: a Menu, a ContextMenu, or a
    /// menu item, with the items directly under it (or under its own
    /// <c>.Items</c> property element).</summary>
    private static IEnumerable<MenuLevel> LevelsOf(XDocument document)
    {
        foreach (XElement element in document.Descendants())
        {
            if (!IsMenu(element) && !IsMenuItem(element))
            {
                continue;
            }

            MenuEntry[] items = element.Elements()
                .SelectMany(child => child.Name.LocalName == element.Name.LocalName + ".Items"
                    ? child.Elements()
                    : [child])
                .Where(IsMenuItem)
                .Select(item =>
                {
                    string header = (string?)item.Attribute("Header") ?? string.Empty;
                    return new MenuEntry(header, AccessKeyOf(header), LineOf(item));
                })
                .ToArray();
            if (items.Length > 0)
            {
                yield return new MenuLevel(NameOf(element), LineOf(element), items);
            }
        }
    }

    private static IEnumerable<SharedKey> SharedKeysIn(string file, MenuLevel level) =>
        level.Items
            .Where(item => item.Key is not null)
            .GroupBy(item => item.Key!.Value)
            .Where(group => group.Count() > 1)
            .Select(group => new SharedKey(
                file,
                level.Name,
                level.Line,
                group.Key,
                [.. group.Select(item => (item.Header, item.Line))]));

    /// <summary>The access key WPF reads from a header: the character after
    /// the first underscore that is not doubled, upper-cased. A markup
    /// extension has no key the source can show.</summary>
    internal static char? AccessKeyOf(string header)
    {
        if (header.StartsWith('{') && !header.StartsWith("{}", StringComparison.Ordinal))
        {
            return null;
        }

        for (int index = 0; index < header.Length; index++)
        {
            if (header[index] != '_')
            {
                continue;
            }

            if (index + 1 >= header.Length)
            {
                return null;
            }

            if (header[index + 1] == '_')
            {
                index++;
                continue;
            }

            return char.ToUpperInvariant(header[index + 1]);
        }

        return null;
    }

    private static bool IsMenu(XElement element) =>
        element.Name.LocalName is "Menu" or "ContextMenu";

    private static bool IsMenuItem(XElement element) =>
        element.Name.LocalName.EndsWith("MenuItem", StringComparison.Ordinal);

    /// <summary>What names a level in a failure line and in an exemption:
    /// its automation id, else its x:Name, else its header; a context menu
    /// is named for the element it hangs on.</summary>
    private static string NameOf(XElement element)
    {
        XName xName = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
        if (element.Name.LocalName == "ContextMenu")
        {
            XElement? owner = element.Parent?.Name.LocalName.EndsWith(".ContextMenu", StringComparison.Ordinal) == true
                ? element.Parent.Parent
                : element.Parent;
            string ownerName = owner is null
                ? "?"
                : (string?)owner.Attribute("AutomationProperties.AutomationId")
                    ?? (string?)owner.Attribute(xName)
                    ?? owner.Name.LocalName;
            return "ContextMenu of " + ownerName;
        }

        return (string?)element.Attribute("AutomationProperties.AutomationId")
            ?? (string?)element.Attribute(xName)
            ?? (string?)element.Attribute("Header")
            ?? element.Name.LocalName;
    }

    private static int LineOf(XElement element) =>
        element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
}
