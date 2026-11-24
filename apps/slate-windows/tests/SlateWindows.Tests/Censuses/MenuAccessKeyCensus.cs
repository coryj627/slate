// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-22 (#1661) and CB-22b: no two items of one menu level share an
// access key.
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
// keys compare without case). A menu built in code is outside it; none of
// those carries an access key (plan 47 CB-22b's survey).
//
// CB-22 gave the Editor menu's two shared keys letters of their own and
// found sixteen more, which it listed as dated exemptions. CB-22b gave each
// of those items the letter the owner ruled, in one table, and ended the
// exemptions: a shared key has no waiver here. The owner's letters are
// pinned by TheMenuLettersAreTheOwners, so a later edit that moves one fails
// by name.

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

        string[] offenders = [.. shared.Select(key => key.ToString())];
        Assert.True(
            offenders.Length == 0,
            "Items of one menu level share an access key. Give each a letter of its own: one that its header "
            + "holds and no sibling carries. A key the owner ruled is pinned in TheMenuLettersAreTheOwners; a "
            + "change to one goes back to the owner:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>The owner's letters for every item of a level that held a
    /// shared key on main 38f6edde (plan 47 CB-22b): each item that changed
    /// and each item that kept a once-shared letter. An item is found under
    /// its level by its automation id, or by its command binding where it
    /// has no id. A header with no underscore carries no key: in the
    /// Workspace menu, which has more items than its labels hold letters,
    /// the owner left four items to their chords.</summary>
    [Theory]
    [InlineData("FileMenu", "NewCanvasMenuItem", "_New Canvas")]
    [InlineData("FileMenu", "CloseVaultMenuItem", "_Close Vault")]
    [InlineData("FilesSidebarMenu", "FilesSidebarOpenInSplitMenuItem", "Open in _Split")]
    [InlineData("FilesSidebarMenu", "FilesSidebarCancelImportMenuItem", "Cancel I_mport")]
    [InlineData("FilesSidebarMenu", "FilesSidebarRefreshMenuItem", "_Refresh")]
    [InlineData("FilesSidebarMenu", "FilesSidebarCollapseAllMenuItem", "_Collapse All")]
    [InlineData("FilesSidebarMenu", "FilesSidebarFocusFilterMenuItem", "Focus _Filter")]
    [InlineData("FilesSidebarMenu", "FilesSidebarForwardMenuItem", "For_ward")]
    [InlineData("FilesSidebarMenu", "FilesSidebarAddToShortcutsMenuItem", "A_dd to Shortcuts")]
    [InlineData("FilesSidebarMenu", "FilesSidebarRemoveShortcutMenuItem", "Remove S_hortcut")]
    [InlineData("FilesSidebarMenu", "FilesSidebarUseVaultDefaultSortMenuItem", "Use _Vault Default Sort")]
    [InlineData("FilesSidebarMenu", "FilesSidebarClearRecentsMenuItem", "Clear Rece_nts")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.DuplicateTabCommand}", "_Duplicate Tab")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.CloseActiveTabCommand}", "_Close Tab")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.ReopenClosedTabCommand}", "Reopen Closed Tab")]
    [InlineData("WorkspaceMenu", "SplitRightMenuItem", "Split _Right")]
    [InlineData("WorkspaceMenu", "SplitDownMenuItem", "Split Down")]
    [InlineData("WorkspaceMenu", "ClosePaneMenuItem", "Cl_ose Pane")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.FocusPaneRightCommand}", "Foc_us Pane Right")]
    [InlineData("WorkspaceMenu", "WorkspaceFocusNextRegionMenuItem", "Focus Ne_xt Region")]
    [InlineData("WorkspaceMenu", "WorkspaceFocusPreviousRegionMenuItem", "Focus Previous Re_gion")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.GrowPaneCommand}", "Grow Pane")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.ShrinkPaneCommand}", "Shrink Pane")]
    [InlineData("WorkspaceMenu", "{Binding Workspace.ToggleRightPaneCommand}", "Toggle Right _Pane")]
    [InlineData("WorkspaceMenu", "SwitchThemeMenuItem", "Switch The_me…")]
    [InlineData("WorkspaceMenu", "ShowTasksReviewMenuItem", "Show Tas_ks Review")]
    [InlineData("WorkspaceMenu", "ShowHistoryPanelMenuItem", "Show _History Panel")]
    [InlineData("WorkspaceMenu", "HistoryChangesSinceOpenMenuItem", "Sho_w Changes Since Last Open")]
    [InlineData("WorkspaceMenu", "RefreshSyncDiagnosticsMenuItem", "Refresh _Sync Diagnostics")]
    [InlineData("BaseMenu", "BasesSortByColumnMenuItem", "Sort by _Column")]
    [InlineData("BaseMenu", "BasesCopyLinkMenuItem", "Copy Lin_k")]
    [InlineData("BaseMenu", "BasesExportCsvMenuItem", "E_xport View as CSV…")]
    [InlineData("BaseMenu", "BasesCopyMarkdownMenuItem", "Copy View as Mark_down")]
    [InlineData("CanvasMenu", "CanvasCreateConnectedCardMenuItem", "C_reate Connected Card")]
    [InlineData("CanvasMenu", "CanvasTracePathMenuItem", "_Trace Path from Selected Card")]
    [InlineData("GraphMenu", "GraphOpenTabMenuItem", "_Open Graph")]
    [InlineData("GraphMenu", "GraphZoomOutMenuItem", "Zoom Ou_t")]
    public void TheMenuLettersAreTheOwners(string level, string item, string header)
    {
        XDocument document = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);
        XElement[] levels = [.. document.Descendants().Where(element =>
            (IsMenu(element) || IsMenuItem(element)) && NameOf(element) == level)];
        XElement owner = Assert.Single(levels);
        XElement[] matches = [.. ItemsOf(owner).Where(element =>
            (string?)element.Attribute("AutomationProperties.AutomationId") == item
                || (string?)element.Attribute("Command") == item)];
        XElement found = Assert.Single(matches);
        Assert.Equal(header, (string?)found.Attribute("Header"));
    }

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

            MenuEntry[] items = ItemsOf(element)
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

    /// <summary>The menu items directly under a level, or under its own
    /// <c>.Items</c> property element.</summary>
    private static IEnumerable<XElement> ItemsOf(XElement level) =>
        level.Elements()
            .SelectMany(child => child.Name.LocalName == level.Name.LocalName + ".Items"
                ? child.Elements()
                : [child])
            .Where(IsMenuItem);

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

    /// <summary>What names a level in a failure line and in a pin: its
    /// automation id, else its x:Name, else its header; a context menu is
    /// named for the element it hangs on.</summary>
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
