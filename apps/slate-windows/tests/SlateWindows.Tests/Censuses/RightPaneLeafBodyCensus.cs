// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-5 (#1239): the right pane stacks every leaf body in ONE grid cell
// and keeps them mutually exclusive by Visibility triggers alone — a
// body shows on `ActiveLeaf.Id == <id>`, and the generic "This panel is
// docked and ready for its feature surface." placeholder collapses on
// the same id. The two halves live 60 lines apart, so a leaf that gains
// a body without the matching collapse trigger paints the placeholder's
// heading OVER its own content (the graph inspector shipped that way:
// the first human NVDA field pass saw "Graph inspector", "Open the
// graph to change these settings." and the placeholder sentence on top
// of each other, and the heading twice in the UIA tree). This census is
// the structural pin: for every leaf id, either a body shows on it AND
// the placeholder collapses on it, or neither — never one without the
// other.

using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "right-pane-leaf-bodies")]
public sealed class RightPaneLeafBodyCensus
{
    private const string PlaceholderSentence =
        "This panel is docked and ready for its feature surface.";

    [Fact]
    public void EveryLeafWithABodyRetiresThePlaceholder()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));

        // The placeholder is the StackPanel that renders the sentence; its
        // Style.Triggers are the collapse allow-list.
        XElement placeholder = window.Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Single(element => (string?)element.Attribute("Text") == PlaceholderSentence)
            .Parent!;
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
