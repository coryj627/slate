// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-5 (#1239): the right pane stacks every leaf body in ONE grid cell
// and keeps them mutually exclusive by Visibility triggers alone — a
// body shows on `ActiveLeaf.Id == <id>`, and the placeholder
// (`RightPaneDockedPlaceholder`) collapses on the same id. The two halves
// live 60 lines apart, so a leaf that gains a body without the matching
// collapse trigger paints the placeholder's heading OVER its own content
// (the graph inspector shipped that way: the first human NVDA field pass
// saw "Graph inspector", "Open the graph to change these settings." and
// the placeholder sentence on top of each other, and the heading twice in
// the UIA tree). This census is the structural pin: for every leaf id,
// either a body shows on it AND the placeholder collapses on it, or
// neither — never one without the other.
//
// Plan 47 CB-23 (#1800, CD15): the placeholder is the body of a panel
// Windows has not built (Math, Code, Diagrams), and its line is core's
// (`empty_panel_line`): the leaves it shows for are exactly the host's
// `WorkspaceViewModel.PanelsWithoutBody`, its line binds the workspace's
// core-made line, and no shell source spells the developer note it
// replaced or core's words.

using System.Collections;
using System.Reflection;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "right-pane-leaf-bodies")]
public sealed class RightPaneLeafBodyCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void EveryLeafWithABodyRetiresThePlaceholder()
    {
        XDocument window = LoadWindow();

        // The placeholder's Style.Triggers are the collapse allow-list.
        XElement placeholder = Placeholder(window);
        var placeholderCollapses = LeafTriggers(placeholder, "Collapsed");

        // Every OTHER `ActiveLeaf.Id` trigger that flips Visibility to
        // Visible is a leaf body showing itself.
        var bodyShows = window.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger")
            .Where(element => !element.Ancestors().Contains(placeholder))
            .Where(element => IsLeafBinding(element))
            .Where(element => element.Elements().Any(setter =>
                setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "Visibility"
                && (string?)setter.Attribute("Value") == "Visible"))
            .Select(element => (string)element.Attribute("Value")!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(bodyShows);
        Assert.NotEmpty(placeholderCollapses);

        string[] known = WorkspaceViewModel.Leaves.Select(leaf => leaf.Id).ToArray();
        Assert.All(bodyShows, id => Assert.Contains(id, known));
        Assert.All(placeholderCollapses, id => Assert.Contains(id, known));

        var offenders = new List<string>();
        foreach (string id in known)
        {
            bool shows = bodyShows.Contains(id);
            bool collapses = placeholderCollapses.Contains(id);
            if (shows && !collapses)
            {
                offenders.Add($"{id}: a body shows on it but the placeholder still paints over it");
            }
            else if (collapses && !shows)
            {
                offenders.Add($"{id}: the placeholder collapses on it but no body shows — the pane goes blank");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Right-pane leaf bodies and the placeholder disagree:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>Plan 47 CB-23: the leaves the placeholder shows for are the
    /// host's panels without a body, each worded by core, and its one line
    /// binds the workspace's core-made line and its spoken name. The map is
    /// read by reflection, so a panel that gains a body (and so leaves the
    /// placeholder) and still sits in the map, or a fourth bodiless leaf the
    /// map lacks, fails here.</summary>
    [Fact]
    public void ThePlaceholderSaysCoresLineForEveryLeafItShowsFor()
    {
        XDocument window = LoadWindow();
        XElement placeholder = Placeholder(window);

        var collapses = LeafTriggers(placeholder, "Collapsed");
        string[] showsFor = [.. WorkspaceViewModel.Leaves.Select(leaf => leaf.Id).Where(id => !collapses.Contains(id)).Order(StringComparer.Ordinal)];
        object? map = typeof(WorkspaceViewModel)
            .GetProperty("PanelsWithoutBody", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(null);
        Assert.True(map is IDictionary, "WorkspaceViewModel has no PanelsWithoutBody map from leaf id to core's panel");
        string[] mapped = [.. ((IDictionary)map!).Keys.Cast<string>().Order(StringComparer.Ordinal)];
        Assert.Equal(showsFor, mapped);

        XElement line = Assert.Single(placeholder.Elements(), element => element.Name.LocalName == "TextBlock");
        Assert.Equal("{Binding ActiveLeafEmptyLine}", (string?)line.Attribute("Text"));
        Assert.Equal("{Binding ActiveLeafEmptyLineName}", (string?)line.Attribute("AutomationProperties.Name"));
        Assert.Equal("True", (string?)line.Attribute("Focusable"));
        Assert.Equal("{StaticResource PanelEmptyMessage}", (string?)line.Attribute("Style"));
    }

    /// <summary>Plan 47 CB-23: the words are core's alone. No shell source
    /// spells the developer note the placeholder used to show, or the words
    /// core's lines are made of.</summary>
    [Fact]
    public void NoShellSourceSpellsThePlaceholdersWords()
    {
        string[] phrases =
        [
            "feature surface",
            "listed here yet",
            "in the reading view, which is open now",
            "then the reading view",
        ];
        string root = SourceText.ShellSourceRoot();
        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal)))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            offenders.AddRange(phrases
                .Where(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                .Select(phrase => $"{relative} spells \"{phrase}\""));
        }

        Assert.True(offenders.Count == 0, "A panel without a body speaks core's line, never the shell's:\n  " + string.Join("\n  ", offenders));
    }

    private static XDocument LoadWindow() =>
        XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));

    /// <summary>The placeholder, by its name: its line is a binding, so it
    /// is no longer found by its text.</summary>
    private static XElement Placeholder(XDocument window) =>
        window.Descendants()
            .Single(element => (string?)element.Attribute(Xaml + "Name") == "RightPaneDockedPlaceholder");

    private static HashSet<string> LeafTriggers(XElement scope, string visibility) =>
        scope.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger")
            .Where(element => IsLeafBinding(element))
            .Where(element => element.Elements().Any(setter =>
                setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "Visibility"
                && (string?)setter.Attribute("Value") == visibility))
            .Select(element => (string)element.Attribute("Value")!)
            .ToHashSet(StringComparer.Ordinal);

    private static bool IsLeafBinding(XElement trigger) =>
        ((string?)trigger.Attribute("Binding"))?.Replace(" ", "", StringComparison.Ordinal)
            == "{BindingActiveLeaf.Id}";
}
