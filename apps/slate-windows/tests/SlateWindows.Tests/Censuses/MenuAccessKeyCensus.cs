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

using System.Xml;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "menu-access-keys")]
public sealed class MenuAccessKeyCensus
{
    /// <summary>One access key carried by two items of one level.</summary>
    internal sealed record Collision(
        string File,
        string Level,
        int LevelLine,
        char Key,
        string First,
        int FirstLine,
        string Second,
        int SecondLine)
    {
        public override string ToString() =>
            $"{File}: level {Level} (line {LevelLine}): '{Key}' is carried by \"{First}\" (line {FirstLine}) and \"{Second}\" (line {SecondLine})";
    }

    /// <summary>A collision that stays, by file, level and key, with its
    /// reason. An entry no collision matches is an error too: an exemption
    /// outlives its reason otherwise.</summary>
    private static readonly Dictionary<(string File, string Level, char Key), string> Exemptions = [];

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

        var collisions = new List<Collision>();
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
                collisions.AddRange(CollisionsIn(name, level));
            }
        }

        // The scrape's own tripwire: the shell's two menu homes are both seen,
        // items and keys included, so an under-matching scrape fails here by
        // name and never passes as "no collisions".
        Assert.True(levels >= 10, $"the census saw {levels} menu levels; the shell's menus hold more than ten.");
        Assert.True(keyed >= 100, $"the census read {keyed} access keys; the shell's menus hold more than a hundred.");

        string[] unexempted = collisions
            .Where(collision => !Exemptions.ContainsKey((collision.File, collision.Level, collision.Key)))
            .Select(collision => collision.ToString())
            .ToArray();
        Assert.True(
            unexempted.Length == 0,
            "Two items of one menu level share an access key. Give one of them a letter of its own "
            + "(one that its header holds and no sibling carries), or record the pair in "
            + nameof(Exemptions) + " with its reason:\n  " + string.Join("\n  ", unexempted));

        string[] stale = Exemptions.Keys
            .Where(exemption => !collisions.Any(collision =>
                (collision.File, collision.Level, collision.Key) == exemption))
            .Select(exemption => $"{exemption.File}: level {exemption.Level}: '{exemption.Key}'")
            .ToArray();
        Assert.True(
            stale.Length == 0,
            "An exemption matches no collision any more; remove it:\n  " + string.Join("\n  ", stale));
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
        Collision[] found = LevelsOf(document)
            .SelectMany(level => CollisionsIn("planted.xaml", level))
            .ToArray();

        Assert.Equal(2, found.Length);
        Collision editor = Assert.Single(found, collision => collision.Key == 'A');
        Assert.Equal("EditorMenu", editor.Level);
        Assert.Equal("_Activate at Cursor", editor.First);
        Assert.Equal("Zoom _actual", editor.Second);
        Assert.True(editor.SecondLine > editor.FirstLine);
        Collision context = Assert.Single(found, collision => collision.Key == 'C');
        Assert.Equal("ContextMenu of TextBox", context.Level);
        Assert.Equal("_Copy", context.First);
        Assert.Equal("Re_cord", context.Second);

        Assert.Equal('N', AccessKeyOf("Snake__case _Name"));
        Assert.Null(AccessKeyOf("No key here"));
        Assert.Null(AccessKeyOf("Ends with _"));
        Assert.Null(AccessKeyOf("{Binding Title}"));
        Assert.Equal('Z', AccessKeyOf("Actual Si_ze"));
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

    private static IEnumerable<Collision> CollisionsIn(string file, MenuLevel level)
    {
        foreach (IGrouping<char, MenuEntry> group in level.Items
                     .Where(item => item.Key is not null)
                     .GroupBy(item => item.Key!.Value)
                     .Where(group => group.Count() > 1))
        {
            MenuEntry[] sharing = [.. group];
            for (int index = 1; index < sharing.Length; index++)
            {
                yield return new Collision(
                    file,
                    level.Name,
                    level.Line,
                    group.Key,
                    sharing[0].Header,
                    sharing[0].Line,
                    sharing[index].Header,
                    sharing[index].Line);
            }
        }
    }

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
