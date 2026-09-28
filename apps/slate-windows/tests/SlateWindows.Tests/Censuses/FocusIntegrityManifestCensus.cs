// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4b (#1247, contract R-5; the owner's S6): the site-to-witness
// manifest. Every place the shell can take the element holding the keys
// away under them is listed in docs/plans/18_windows_port/focus_integrity_manifest.json
// with what witnesses its landing. The census scrapes the sites and fails on
// a site the manifest does not list, on a listed site that is gone, and on a
// witness that does not hold — so the next instance of any of these classes
// is caught when it is written, not in the next review round.
//
// Codex PR 4b r1 F8 found the first scrape non-discriminating: it saw only
// syntactic shapes (an ItemsSource assignment, Children.Clear(), a XAML
// trigger setter), it exempted every binding as "the guard's" — though
// RightPaneBorder's binding sits outside every guarded region, holding them —
// and a witness was only a method name. The scrape now reads BOUND symbols:
//
//   ui-state      a write to UIElement.Visibility, IsEnabled or Focusable —
//                 an assignment or SetValue/SetCurrentValue/SetBinding
//   items-source  a write to ItemsControl.ItemsSource, the same ways
//   collection    a Clear/Remove/RemoveAt/Move call or an indexer write on an
//                 INotifyCollectionChanged, a UIElementCollection, an
//                 ItemCollection or a document's TextElementCollection (a
//                 FlowDocument's blocks, a paragraph's inlines) — an Add or an
//                 Insert takes nothing away
//   content       a write to ContentControl.Content, ContentPresenter.Content
//                 or Decorator.Child — a subtree swapped (a button's or a
//                 label's content is its label, not a subtree, and is not one)
//   xaml-trigger  a XAML trigger setting IsEnabled or Visibility
//   xaml-binding  a XAML Visibility, IsEnabled or Focusable given by a markup
//                 extension
//   xaml-items    a XAML ItemsSource given by a markup extension
//   xaml-content  a XAML ContentControl's or ContentPresenter's Content given
//                 by a markup extension
//
// A property set in an object creation's initializer, or anything a
// constructor writes, is a new element's initial state — it holds no keys
// yet — and is not a site; nor is a XAML element that neither is nor holds
// anything that can take the keys (RegionBoundaryCensus.HoldsAStop: a
// sheet's scrim, a status text). Landing CALLS are SelectorLandingCensus's,
// and region roots RegionGuardCensus's.
//
// A witness is one of:
//   Class.Method        a fact in this project whose CODE names the site —
//                       its element, its receiver or its type, as a whole
//                       identifier or string literal, case-sensitive (a
//                       comment or a longer name does not)
//   guard: A[, B]       the site sits strictly inside a guarded scope named A
//                       (or B) — an element the constructed window gives a
//                       RegionFocusGuard landing, or a view that registers its
//                       own — and neither is nor contains a region root: the
//                       guard lands the keys inside it. Where the site lives is
//                       read from the XAML: its element; for code, the named
//                       element it writes, the XAML instances of its view, or
//                       the items controls bound to its collection; and a
//                       template's element lives where the template is used.
//                       A site whose collapse takes a whole region away needs a
//                       fact of its own.
//   exempt: <reason>    a fact the reason cites (SomethingTests.Method) must
//                       exist
//
// SLATE_FOCUS_MANIFEST_UPDATE=1 rewrites the manifest with the scraped sites,
// keeping every assignment and leaving new sites unassigned, which fails
// until a witness is written.

using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "focus-integrity-manifest")]
public sealed class FocusIntegrityManifestCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] StateProperties = ["Visibility", "IsEnabled", "Focusable"];
    private static readonly string[] CollectionMutators = ["Clear", "Remove", "RemoveAt", "Move"];

    private static string ManifestPath() =>
        Path.Combine(SourceText.RepoRoot(), "docs", "plans", "18_windows_port", "focus_integrity_manifest.json");

    [Fact]
    public void EverySiteThatCanTakeTheKeysAwayNamesItsWitness()
    {
        SortedDictionary<string, Site> scraped = Sites();
        Assert.True(scraped.Count >= 150, $"only {scraped.Count} sites were scraped; the scrape is broken.");
        Dictionary<string, string> manifest = Manifest();

        if (Environment.GetEnvironmentVariable("SLATE_FOCUS_MANIFEST_UPDATE") == "1")
        {
            Write(scraped.Keys.ToDictionary(site => site, site => manifest.GetValueOrDefault(site, string.Empty)));
            manifest = Manifest();
        }

        string[] unlisted = [.. scraped.Keys.Where(site => !manifest.ContainsKey(site))];
        string[] stale = [.. manifest.Keys.Where(site => !scraped.ContainsKey(site))];
        var judge = new WitnessJudge(Facts(), GuardedScopes(), ShellXaml(), Creators());
        string[] unwitnessed =
        [
            .. manifest
                .Where(entry => scraped.ContainsKey(entry.Key))
                .Select(entry => (entry.Key, Verdict: judge.Judge(scraped[entry.Key], entry.Value)))
                .Where(judged => judged.Verdict is not null)
                .Select(judged => $"{judged.Key} → {judged.Verdict}"),
        ];
        Assert.True(unlisted.Length == 0, "Sites that can take the keys away with no witness in the manifest (S6):\n  " + string.Join("\n  ", unlisted));
        Assert.True(stale.Length == 0, "Manifest entries whose site is gone:\n  " + string.Join("\n  ", stale));
        Assert.True(unwitnessed.Length == 0, "Manifest witnesses that do not hold:\n  " + string.Join("\n  ", unwitnessed));
    }

    /// <summary>Codex PR 4b r1 F8's discriminating fact: the sites the first
    /// scrape could not see — a binding on a container of region roots, the
    /// review's "Load more" binding, a bound collection rebuilt in a view
    /// model, a condition removed from its collection, a view hiding its own
    /// list — are all scraped.</summary>
    [Fact]
    public void EveryClassOfKeyTakingSiteIsScraped()
    {
        SortedDictionary<string, Site> scraped = Sites();

        Assert.Contains("xaml-binding MainWindow.xaml RightPaneBorder Visibility", scraped.Keys);
        Assert.Contains("xaml-binding MainWindow.xaml PanelReviewLoadMore Visibility", scraped.Keys);
        Assert.Contains("collection SlateWindows.FilesSidebarViewModel.ApplyTags Tags", scraped.Keys);
        Assert.Contains("collection SlateWindows.Bases.BaseQueryBuilderViewModel.RemoveCondition ConditionRows", scraped.Keys);
        Assert.Contains(scraped.Keys, site => site.StartsWith("ui-state SlateWindows.Bases.BaseSurfaceView.", StringComparison.Ordinal) && site.EndsWith(" _list Visibility", StringComparison.Ordinal));
    }

    /// <summary>Each class of site is caught in planted source, and the
    /// shapes that are not sites — a new element's initializer, a
    /// constructor's writes, an Add, a view model's IsEnabled, a plain list —
    /// are not.</summary>
    [Fact]
    public void ThePlantedSitesAreCaught()
    {
        const string Planted = """
            using System.Collections.Generic;
            using System.Collections.ObjectModel;
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Data;
            using System.Windows.Documents;

            namespace Planted;

            internal sealed class Model
            {
                public bool IsEnabled { get; set; }
                public ObservableCollection<string> Rows { get; } = [];
                public List<string> Plain { get; } = [];
            }

            internal sealed class View
            {
                private readonly Button _button = new() { Visibility = Visibility.Collapsed };
                private readonly ListBox _list = new();
                private readonly StackPanel _panel = new();
                private readonly Model _model = new();
                private readonly ContentControl _host = new();
                private readonly Border _frame = new();
                private readonly FlowDocument _document = new();

                public View()
                {
                    _button.Visibility = Visibility.Visible;
                    _panel.Children.Clear();
                }

                private void Hide() => _button.Visibility = Visibility.Collapsed;
                private void Disable() => _button.SetCurrentValue(UIElement.IsEnabledProperty, false);
                private void Unfocus() => _button.Focusable = false;
                private void Bind() => BindingOperations.SetBinding(_button, UIElement.VisibilityProperty, new Binding());
                private void Feed() => _list.ItemsSource = _model.Rows;
                private void FeedAgain() => _list.SetValue(ItemsControl.ItemsSourceProperty, null);
                private void Rebuild() => _panel.Children.Clear();
                private void Publish() => _model.Rows.RemoveAt(0);
                private void Replace() => _model.Rows[0] = "row";
                private void Drop() => _list.Items.Remove("row");
                private void Swap() => _host.Content = new Button();
                private void Reframe() => _frame.Child = new Button();
                private void Present() => _host.SetValue(ContentControl.ContentProperty, null);
                private void Wipe() => _document.Blocks.Clear();
                private void NotASite()
                {
                    _model.IsEnabled = false;
                    _model.Plain.Clear();
                    _model.Rows.Add("row");
                    _list.Items.Insert(0, "row");
                    _button.Content = "Save";
                    _document.Blocks.Add(new Paragraph());
                    _ = new ContentControl { Content = "made" };
                }
            }
            """;
        CSharpParseOptions options = (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Planted, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <StackPanel>
                <Button x:Name="Bound" Visibility="{Binding Shown}" IsEnabled="True" />
                <ListBox x:Name="Fed" ItemsSource="{Binding Rows}" />
                <Border x:Name="Triggered">
                  <Border.Style>
                    <Style TargetType="Border">
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding Gone}" Value="True">
                          <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Border.Style>
                  <Button Content="Inside" />
                </Border>
                <Border x:Name="Scrim" Visibility="{Binding Shown}" />
                <ContentControl x:Name="Paged" Content="{Binding Page}" />
                <Button x:Name="Labelled" Content="{Binding Label}" />
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape(
            [((CompilationUnitSyntax)tree.GetRoot(), compilation.GetSemanticModel(tree))], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "collection Planted.View.Drop _list.Items",
                "collection Planted.View.Publish _model.Rows",
                "collection Planted.View.Rebuild _panel.Children",
                "collection Planted.View.Replace _model.Rows",
                "collection Planted.View.Wipe _document.Blocks",
                "content Planted.View.Present _host Content",
                "content Planted.View.Reframe _frame Child",
                "content Planted.View.Swap _host Content",
                "items-source Planted.View.Feed _list",
                "items-source Planted.View.FeedAgain _list",
                "ui-state Planted.View.Bind _button Visibility",
                "ui-state Planted.View.Disable _button IsEnabled",
                "ui-state Planted.View.Hide _button Visibility",
                "ui-state Planted.View.Unfocus _button Focusable",
                "xaml-binding Planted.xaml Bound Visibility",
                "xaml-content Planted.xaml Paged",
                "xaml-items Planted.xaml Fed",
                "xaml-trigger Planted.xaml Triggered Visibility",
            ],
            sites.Keys);
    }

    /// <summary>A guard witness holds only for a site strictly inside a
    /// guarded scope that neither is nor contains a region root; a fact
    /// witness only when the fact names the site.</summary>
    [Fact]
    public void AGuardWitnessOnlyCoversASiteInsideAGuardedRoot()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Grid x:Name="Columns">
                <Border x:Name="Pane" Visibility="{Binding PaneShown}">
                  <Border x:Name="FilesPaneBorder">
                    <Button x:Name="Inner" IsEnabled="{Binding Ready}" />
                  </Border>
                </Border>
                <Button x:Name="Loose" IsEnabled="{Binding Ready}" />
              </Grid>
            </Window>
            """,
            LoadOptions.SetLineInfo);
        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Facts.ThePaneHides"] = "shell.Pane.Visibility = Collapsed;" },
            new HashSet<string>(["Columns", "FilesPaneBorder"], StringComparer.Ordinal),
            [xaml]);

        Assert.Null(judge.Judge(sites["xaml-binding Planted.xaml Inner IsEnabled"], "guard: FilesPaneBorder"));
        Assert.Null(judge.Judge(sites["xaml-binding Planted.xaml Loose IsEnabled"], "guard: Columns"));
        Assert.NotNull(judge.Judge(sites["xaml-binding Planted.xaml Loose IsEnabled"], "guard: FilesPaneBorder"));
        // The pane holds a region root: no guard covers its collapse, a fact
        // naming it does.
        Assert.NotNull(judge.Judge(sites["xaml-binding Planted.xaml Pane Visibility"], "guard: Columns"));
        Assert.Null(judge.Judge(sites["xaml-binding Planted.xaml Pane Visibility"], "Facts.ThePaneHides"));
        Assert.NotNull(judge.Judge(sites["xaml-binding Planted.xaml Inner IsEnabled"], "Facts.ThePaneHides"));
        Assert.NotNull(judge.Judge(sites["xaml-binding Planted.xaml Inner IsEnabled"], "guard: Nowhere"));
        Assert.NotNull(judge.Judge(sites["xaml-binding Planted.xaml Inner IsEnabled"], "exempt: "));
    }

    /// <summary>A fact names a site in its CODE — an identifier or a string
    /// literal, whole and case-sensitive — and an exemption's cited fact
    /// exists (PR 4b codex r2 F6): a name in a comment, a name in another
    /// case, a longer identifier that contains it, or a missing cited fact
    /// does not hold.</summary>
    [Fact]
    public void AFactWitnessNamesTheSiteInItsCode()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Border x:Name="Pane" Visibility="{Binding PaneShown}">
                <Button Content="Inside" />
              </Border>
            </Window>
            """,
            LoadOptions.SetLineInfo);
        Site pane = Scrape([], [("Planted.xaml", xaml)])["xaml-binding Planted.xaml Pane Visibility"];
        var list = new Site("items-source Planted.View.Feed _list", null, null, null, "_list", ["list", "View"]);
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PlantedTests.ThePaneHides"] = "void ThePaneHides() { shell.Pane.Visibility = Visibility.Collapsed; }",
                ["PlantedTests.ThePaneByItsId"] = "[InlineData(\"Pane\")] void ThePaneByItsId(string id) { Hide(id); }",
                ["PlantedTests.OnlyAComment"] = "/// <summary>The Pane hides.</summary>\nvoid OnlyAComment() { // Pane\n shell.Other.Visibility = Visibility.Collapsed; }",
                ["PlantedTests.AnotherCase"] = "void AnotherCase() { shell.pane.Visibility = Visibility.Collapsed; }",
                ["PlantedTests.ALongerName"] = "void ALongerName() { shell.PaneBorder.Visibility = Visibility.Collapsed; }",
                ["PlantedTests.TheList"] = "void TheList() { ListBox list = host.List; list.ItemsSource = null; }",
                ["PlantedTests.AGenericList"] = "void AGenericList() { var rows = new List<int>(); rows.Clear(); }",
            },
            new HashSet<string>(StringComparer.Ordinal),
            [xaml]);

        Assert.Null(judge.Judge(pane, "PlantedTests.ThePaneHides"));
        Assert.Null(judge.Judge(pane, "PlantedTests.ThePaneByItsId"));
        Assert.NotNull(judge.Judge(pane, "PlantedTests.OnlyAComment"));
        Assert.NotNull(judge.Judge(pane, "PlantedTests.AnotherCase"));
        Assert.NotNull(judge.Judge(pane, "PlantedTests.ALongerName"));
        Assert.Null(judge.Judge(list, "PlantedTests.TheList"));
        Assert.NotNull(judge.Judge(list, "PlantedTests.AGenericList"));
        Assert.Null(judge.Judge(pane, "exempt: covered by PlantedTests.ThePaneHides"));
        Assert.NotNull(judge.Judge(pane, "exempt: covered by PlantedTests.NoSuchFact"));
    }

    /// <summary>A scraped site: its key, the XAML element that carries it
    /// (or the resource key its carrier is used by), the C# type declaring it
    /// and the receiver it writes, and the names a fact witnessing it must
    /// mention.</summary>
    internal sealed record Site(
        string Key,
        XElement? Element,
        string? ResourceKey,
        INamedTypeSymbol? DeclaringType,
        string? Member,
        IReadOnlyList<string> Names);

    /// <summary>The shell's scraped sites.</summary>
    internal static SortedDictionary<string, Site> Sites() =>
        Scrape(
            ShellCompilation.Sources.Select(entry => (entry.Source.Root, ShellCompilation.ModelFor(entry.Source))),
            ShellXamlFiles().Select(path => (Path.GetFileName(path), XDocument.Load(path, LoadOptions.SetLineInfo))));

    private static IEnumerable<string> ShellXamlFiles() =>
        Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);

    private static XDocument[] ShellXaml() =>
        [.. ShellXamlFiles().Select(path => XDocument.Load(path, LoadOptions.SetLineInfo))];

    private static SortedDictionary<string, Site> Scrape(
        IEnumerable<(CompilationUnitSyntax Root, SemanticModel Model)> sources,
        IEnumerable<(string File, XDocument Document)> xaml)
    {
        var sites = new SortedDictionary<string, Site>(StringComparer.Ordinal);
        foreach ((CompilationUnitSyntax root, SemanticModel model) in sources)
        {
            foreach (AssignmentExpressionSyntax assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)))
            {
                if (assignment.Parent is InitializerExpressionSyntax)
                {
                    // An object creation's initial state: the new element
                    // holds no keys yet.
                    continue;
                }

                if (assignment.Left is ElementAccessExpressionSyntax indexed)
                {
                    if (IsObservedCollection(model.GetTypeInfo(indexed.Expression).Type))
                    {
                        Add(sites, model, assignment, "collection", indexed.Expression.ToString(), property: null);
                    }

                    continue;
                }

                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property)
                {
                    continue;
                }

                string receiver = assignment.Left is MemberAccessExpressionSyntax access ? access.Expression.ToString() : "this";
                if (property.Name == "ItemsSource" && Derives(property.ContainingType, "System.Windows.Controls.ItemsControl"))
                {
                    Add(sites, model, assignment, "items-source", receiver, property: null);
                }
                else if (SwapsASubtree(property, assignment.Left is MemberAccessExpressionSyntax written
                    ? model.GetTypeInfo(written.Expression).Type
                    : model.GetEnclosingSymbol(assignment.SpanStart)?.ContainingType))
                {
                    Add(sites, model, assignment, "content", receiver, property.Name);
                }
                else if (StateProperties.Contains(property.Name) && Derives(property.ContainingType, "System.Windows.UIElement"))
                {
                    Add(sites, model, assignment, "ui-state", receiver, property.Name);
                }
            }

            foreach (InvocationExpressionSyntax call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is not MemberAccessExpressionSyntax member)
                {
                    continue;
                }

                string name = member.Name.Identifier.ValueText;
                SeparatedSyntaxList<ArgumentSyntax> arguments = call.ArgumentList.Arguments;
                if (name is "SetValue" or "SetCurrentValue" or "SetBinding")
                {
                    bool operations = member.Expression.ToString() == "BindingOperations";
                    int at = operations ? 1 : 0;
                    if (arguments.Count <= at
                        || model.GetSymbolInfo(arguments[at].Expression).Symbol is not IFieldSymbol { Name: var field } owned
                        || !(Derives(owned.ContainingType, "System.Windows.UIElement")
                            || Derives(owned.ContainingType, "System.Windows.Controls.ItemsControl")))
                    {
                        // An attached property of the shell's own (a fence's,
                        // a radio group's) is no element state.
                        continue;
                    }

                    string receiver = operations ? arguments[0].Expression.ToString() : member.Expression.ToString();
                    string target = field.EndsWith("Property", StringComparison.Ordinal) ? field[..^"Property".Length] : field;
                    if (target == "ItemsSource")
                    {
                        Add(sites, model, call, "items-source", receiver, property: null);
                    }
                    else if (target is "Content" or "Child"
                        && IsSubtreeHost(operations ? model.GetTypeInfo(arguments[0].Expression).Type : model.GetTypeInfo(member.Expression).Type))
                    {
                        Add(sites, model, call, "content", receiver, target);
                    }
                    else if (StateProperties.Contains(target))
                    {
                        Add(sites, model, call, "ui-state", receiver, target);
                    }
                }
                else if (CollectionMutators.Contains(name) && IsObservedCollection(model.GetTypeInfo(member.Expression).Type))
                {
                    Add(sites, model, call, "collection", member.Expression.ToString(), property: null);
                }
            }
        }

        foreach ((string file, XDocument document) in xaml)
        {
            foreach (XElement setter in document.Descendants().Where(element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") is "IsEnabled" or "Visibility"
                && element.Ancestors().Any(ancestor => ancestor.Name.LocalName.EndsWith(".Triggers", StringComparison.Ordinal))))
            {
                XElement owner = setter.Ancestors().First(ancestor =>
                    ancestor.Name.LocalName is "Style" or "ControlTemplate" or "DataTemplate");
                string property = (string)setter.Attribute("Property")!;
                if ((string?)owner.Attribute(Xaml + "Key") is { } key)
                {
                    string keyed = $"xaml-trigger {file} {owner.Name.LocalName}:{key} {property}";
                    sites[keyed] = new Site(keyed, null, key, null, null, [key]);
                    continue;
                }

                XElement? carrier = owner.Parent?.Parent;
                if (carrier is not null && !RegionBoundaryCensus.HoldsAStop(carrier))
                {
                    // It neither is nor holds anything that takes the keys.
                    continue;
                }

                string identity = Identity(carrier) ?? $"line {((IXmlLineInfo)owner).LineNumber}";
                string trigger = $"xaml-trigger {file} {identity} {property}";
                sites[trigger] = new Site(trigger, carrier, null, null, null, NamesOf(carrier));
            }

            foreach (XElement element in document.Descendants().Where(RegionBoundaryCensus.HoldsAStop))
            {
                foreach (XAttribute attribute in element.Attributes().Where(attribute => attribute.Value.StartsWith('{')))
                {
                    string local = attribute.Name.LocalName;
                    string? key = StateProperties.Contains(local)
                        ? $"xaml-binding {file} {Identity(element)} {local}"
                        : local == "ItemsSource" ? $"xaml-items {file} {Identity(element)}"
                        : local == "Content" && element.Name.LocalName is "ContentControl" or "ContentPresenter"
                            ? $"xaml-content {file} {Identity(element)}"
                            : null;
                    if (key is not null)
                    {
                        sites[key] = new Site(key, element, null, null, null, NamesOf(element));
                    }
                }
            }
        }

        return sites;
    }

    private static void Add(
        SortedDictionary<string, Site> sites, SemanticModel model, SyntaxNode node, string kind, string receiver, string? property)
    {
        ISymbol? member = model.GetEnclosingSymbol(node.SpanStart);
        while (member is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            member = member.ContainingSymbol;
        }

        if (member is IMethodSymbol { MethodKind: MethodKind.Constructor } || node.Ancestors().Any(ancestor => ancestor is ConstructorDeclarationSyntax))
        {
            // A constructor's writes: the element holds no keys yet.
            return;
        }

        INamedTypeSymbol? type = member?.ContainingType;
        string key = $"{kind} {type?.ToDisplayString()}.{member?.Name} {receiver}{(property is null ? string.Empty : " " + property)}";
        string[] segments = receiver.TrimEnd('!').Split('.');
        bool owned = segments.Length > 1 && segments[^1] is "Children" or "Items"
            && node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: var collection } }
            && (Derives(model.GetTypeInfo(collection).Type, "System.Windows.Controls.UIElementCollection")
                || Derives(model.GetTypeInfo(collection).Type, "System.Windows.Controls.ItemCollection"));
        string last = (owned ? segments[^2] : segments[^1]).TrimEnd('!');
        sites[key] = new Site(key, null, null, type, last, [last.TrimStart('_'), type?.Name ?? string.Empty]);
    }

    private static bool IsObservedCollection(ITypeSymbol? type) =>
        type is not null
        && (type.AllInterfaces.Any(candidate => candidate.ToDisplayString() == "System.Collections.Specialized.INotifyCollectionChanged")
            || Derives(type, "System.Windows.Controls.UIElementCollection")
            || Derives(type, "System.Windows.Controls.ItemCollection")
            || IsTextElementCollection(type));

    /// <summary>A document's blocks or inlines: a <c>TextElementCollection</c>
    /// (a FlowDocument's <c>Blocks</c>, a paragraph's <c>Inlines</c>), whose
    /// elements — a Hyperlink, an embedded control — can hold the keys.</summary>
    private static bool IsTextElementCollection(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.Name == "TextElementCollection" && current.ContainingNamespace?.ToDisplayString() == "System.Windows.Documents")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A write that swaps a subtree: <c>Content</c> on a content
    /// control or presenter, <c>Child</c> on a decorator.</summary>
    private static bool SwapsASubtree(IPropertySymbol property, ITypeSymbol? receiver) =>
        (property.Name == "Content"
            && (Derives(property.ContainingType, "System.Windows.Controls.ContentControl")
                || Derives(property.ContainingType, "System.Windows.Controls.ContentPresenter"))
            || property.Name == "Child" && Derives(property.ContainingType, "System.Windows.Controls.Decorator"))
        && IsSubtreeHost(receiver);

    /// <summary>A content control, presenter or decorator whose content is a
    /// subtree — not a button's or a label's, whose content is its
    /// label.</summary>
    private static bool IsSubtreeHost(ITypeSymbol? type) =>
        (Derives(type, "System.Windows.Controls.ContentControl")
            || Derives(type, "System.Windows.Controls.ContentPresenter")
            || Derives(type, "System.Windows.Controls.Decorator"))
        && !Derives(type, "System.Windows.Controls.Primitives.ButtonBase")
        && !Derives(type, "System.Windows.Controls.Label")
        && !Derives(type, "System.Windows.Controls.ToolTip");

    private static bool Derives(ITypeSymbol? type, string baseName)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>An element's identity that survives edits elsewhere in the
    /// file: its own name or automation id; else the first named element it
    /// holds; else its type, the nearest named element holding it, and its
    /// ordinal among that element's descendants of its type.</summary>
    private static string? Identity(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        if (Named(element) is { } own)
        {
            return own;
        }

        if (element.Descendants().Select(Named).FirstOrDefault(name => name is not null) is { } held)
        {
            return $"{element.Name.LocalName}({held})";
        }

        XElement? holder = element.Ancestors().FirstOrDefault(ancestor => Named(ancestor) is not null);
        int ordinal = holder is null
            ? 0
            : holder.Descendants(element.Name).TakeWhile(candidate => !ReferenceEquals(candidate, element)).Count();
        return $"{element.Name.LocalName} in {(holder is null ? "the window" : Named(holder))}#{ordinal}";
    }

    /// <summary>The names a fact witnessing a XAML site may mention: the
    /// element's own, else those of the elements it holds, else its
    /// holder's.</summary>
    private static string[] NamesOf(XElement? element)
    {
        if (element is null)
        {
            return [];
        }

        if (Named(element) is { } own)
        {
            return [own];
        }

        string[] held = [.. element.Descendants().Select(Named).OfType<string>().Take(3)];
        return held.Length > 0
            ? held
            : [.. element.Ancestors().Select(Named).OfType<string>().Take(1)];
    }

    internal static string? Named(XElement element) =>
        (string?)element.Attribute(Xaml + "Name")
            ?? (string?)element.Attribute("AutomationProperties.AutomationId")
            ?? (string?)element.Attribute(Xaml + "Key");

    /// <summary>Whether a site's witness holds; null when it does, else
    /// why not.</summary>
    internal sealed class WitnessJudge(
        IReadOnlyDictionary<string, string> facts,
        IReadOnlySet<string> guarded,
        IReadOnlyList<XDocument> documents,
        IReadOnlyDictionary<string, string[]>? creators = null)
    {
        private readonly Dictionary<XDocument, HashSet<XElement>> _regionRoots = [];

        public string? Judge(Site site, string witness)
        {
            if (witness.StartsWith("exempt: ", StringComparison.Ordinal))
            {
                if (witness.Length <= 12)
                {
                    return "an exemption with no reason";
                }

                // A fact an exemption leans on must exist (PR 4b codex r2 F6).
                return System.Text.RegularExpressions.Regex.Matches(witness, @"\b[A-Z]\w*Tests\.[A-Z]\w*\b")
                    .Select(match => match.Value)
                    .FirstOrDefault(cited => !facts.ContainsKey(cited)) is { } unresolved
                    ? $"the exemption cites '{unresolved}', which names no fact in this project"
                    : null;
            }

            if (witness.StartsWith("guard: ", StringComparison.Ordinal))
            {
                string[] scopes = [.. witness["guard: ".Length..].Split(',').Select(scope => scope.Trim())];
                if (scopes.FirstOrDefault(scope => !guarded.Contains(scope)) is { } unguarded)
                {
                    return $"'{unguarded}' is no guarded scope";
                }

                if (site.DeclaringType is { } type && scopes.Contains(type.Name))
                {
                    // A view that registers its own landing, writing its
                    // own elements.
                    return null;
                }

                return Hosts(site).SelectMany(host => Placed(host, [])).ToArray() is { Length: > 0 } hosts
                    ? hosts.Select(host => Inside(host, scopes)).FirstOrDefault(reason => reason is not null)
                    : "nothing in the XAML shows where the site's elements live; a fact must witness it";
            }

            if (!facts.TryGetValue(witness, out string? source))
            {
                return $"'{witness}' names no fact in this project";
            }

            // The fact's CODE names the site — an identifier or a string
            // literal, whole and case-sensitive; a comment, a doc comment or a
            // substring of a longer name does not (PR 4b codex r2 F6).
            HashSet<string> tokens = CodeTokens(source);
            return site.Names.Any(name => name.Length > 2 && tokens.Contains(name))
                ? null
                : $"the fact's code does not name the site ({string.Join(", ", site.Names)})";
        }

        /// <summary>The identifiers and string literals of a fact's source,
        /// trivia — comments, doc comments — left out.</summary>
        private static HashSet<string> CodeTokens(string source) =>
        [
            .. SyntaxFactory.ParseTokens(source)
                .Where(token => token.IsKind(SyntaxKind.IdentifierToken) || token.IsKind(SyntaxKind.StringLiteralToken))
                .Select(token => token.ValueText),
        ];

        /// <summary>The XAML elements a site's change lands on: its element;
        /// the users of its keyed resource; for code, the XAML instances of a
        /// view type, or the items controls bound to a model's
        /// collection.</summary>
        private IEnumerable<XElement> Hosts(Site site)
        {
            if (site.Element is not null)
            {
                yield return site.Element;
                yield break;
            }

            if (site.ResourceKey is { } key)
            {
                foreach (XElement user in Users(key))
                {
                    yield return user;
                }

                yield break;
            }

            if (site.DeclaringType is not { } type)
            {
                yield break;
            }

            string member = site.Member ?? string.Empty;
            XElement[] named = [.. All().Where(element => (string?)element.Attribute(Xaml + "Name") == member)];
            if (named.Length > 0)
            {
                // The window's own code writing one of its named elements.
                foreach (XElement element in named)
                {
                    yield return element;
                }

                yield break;
            }

            foreach (XElement view in Instances(type.Name, depth: 0))
            {
                yield return view;
            }

            foreach (XElement bound in All().Where(element =>
                (string?)element.Attribute("ItemsSource") is { } source
                && source.StartsWith("{Binding", StringComparison.Ordinal)
                && BindingPathEnd(source) == member))
            {
                yield return bound;
            }
        }

        private IEnumerable<XElement> All() => documents.SelectMany(document => document.Descendants());

        /// <summary>A view type's XAML instances; for a view the shell builds
        /// in code, the instances of the views that build it.</summary>
        private IEnumerable<XElement> Instances(string type, int depth)
        {
            XElement[] declared = [.. All().Where(element => element.Name.LocalName == type)];
            if (declared.Length > 0 || depth > 3)
            {
                return declared;
            }

            return (creators ?? new Dictionary<string, string[]>()).GetValueOrDefault(type, [])
                .SelectMany(creator => Instances(creator, depth + 1));
        }

        /// <summary>The elements that use the resource keyed
        /// <paramref name="key"/>.</summary>
        private IEnumerable<XElement> Users(string key) =>
            All().Where(element => element.Attributes().Any(attribute =>
                attribute.Value.Contains($"Resource {key}}}", StringComparison.Ordinal)));

        /// <summary>Where an element lives: itself, or — inside a keyed
        /// template or style — where that resource is used, as far as the
        /// window's own tree. Templates that nest each other (a split's node
        /// and child templates) are followed once.</summary>
        private IEnumerable<XElement> Placed(XElement host, ImmutableHashSet<XElement> through)
        {
            XElement? resource = host.AncestorsAndSelf().FirstOrDefault(ancestor => ancestor.Attribute(Xaml + "Key") is not null);
            if (resource is null)
            {
                yield return host;
                yield break;
            }

            if (through.Contains(resource))
            {
                yield break;
            }

            XElement[] users = [.. Users((string)resource.Attribute(Xaml + "Key")!)];
            if (users.Length == 0)
            {
                yield return host;
                yield break;
            }

            foreach (XElement user in users)
            {
                foreach (XElement placed in Placed(user, through.Add(resource)))
                {
                    yield return placed;
                }
            }
        }
        private static string? BindingPathEnd(string binding)
        {
            string path = binding["{Binding".Length..].TrimEnd('}').Split(',')[0].Trim();
            path = path.StartsWith("Path=", StringComparison.Ordinal) ? path["Path=".Length..] : path;
            return path.Length == 0 ? null : path.Split('.').Last();
        }

        /// <summary>The region roots of the document a host is in (a site's
        /// element comes from its own load of the XAML).</summary>
        private HashSet<XElement> RegionRootsOf(XDocument document)
        {
            if (!_regionRoots.TryGetValue(document, out HashSet<XElement>? roots))
            {
                roots = [.. RegionBoundaryCensus.RegionRoots(document).Select(root => root.Root)];
                _regionRoots[document] = roots;
            }

            return roots;
        }

        /// <summary>Null when <paramref name="host"/> sits strictly inside one
        /// of <paramref name="scopes"/> and neither is nor holds a region
        /// root.</summary>
        private string? Inside(XElement host, string[] scopes)
        {
            HashSet<XElement> roots = RegionRootsOf(host.Document ?? documents[0]);
            if (roots.Contains(host) || host.Descendants().Any(roots.Contains))
            {
                return $"{Identity(host)} is or holds a region root: its change takes a region away, and a fact must witness it";
            }

            return host.Ancestors().Any(ancestor => Named(ancestor) is { } name && scopes.Contains(name))
                ? null
                : $"{Identity(host)} is not inside {string.Join(" or ", scopes)}";
        }
    }

    /// <summary>The scopes the constructed window guards, by name: every
    /// named element with a RegionFocusGuard landing, and every view type
    /// that registers its own.</summary>
    private static HashSet<string> GuardedScopes()
    {
        var guarded = new HashSet<string>(StringComparer.Ordinal);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(() =>
                {
                    var shell = new MainWindow();
                    try
                    {
                        var pending = new Stack<object>([shell]);
                        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
                        while (pending.TryPop(out object? node))
                        {
                            if (!seen.Add(node) || node is not DependencyObject element)
                            {
                                continue;
                            }

                            if (element is FrameworkElement framework && RegionFocusGuard.HasLanding(framework))
                            {
                                foreach (string name in new[] { framework.Name, AutomationProperties.GetAutomationId(framework) }
                                    .Where(name => !string.IsNullOrEmpty(name)))
                                {
                                    _ = guarded.Add(name);
                                }
                            }

                            foreach (object child in LogicalTreeHelper.GetChildren(element))
                            {
                                pending.Push(child);
                            }
                        }
                    }
                    finally
                    {
                        shell.Close();
                    }
                });
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "the guard map timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(call => CSharpSource.Normalize(call.Expression) == "RegionFocusGuard.SetLanding"
                    && call.ArgumentList.Arguments.FirstOrDefault()?.Expression is ThisExpressionSyntax))
            {
                if (call.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault() is { } type)
                {
                    _ = guarded.Add(type.Identifier.ValueText);
                }
            }
        }

        return guarded;
    }

    /// <summary>The shell's view types each type is built by in code
    /// (<c>new T(...)</c> inside them), by simple name.</summary>
    private static Dictionary<string, string[]> Creators()
    {
        var creators = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(creation).Type is INamedTypeSymbol created
                    && creation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault() is { } creator
                    && creator.Identifier.ValueText != created.Name)
                {
                    if (!creators.TryGetValue(created.Name, out HashSet<string>? by))
                    {
                        by = new HashSet<string>(StringComparer.Ordinal);
                        creators[created.Name] = by;
                    }

                    _ = by.Add(creator.Identifier.ValueText);
                }
            }
        }

        return creators.ToDictionary(entry => entry.Key, entry => entry.Value.Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }
    private static Dictionary<string, string> Manifest()
    {
        if (!File.Exists(ManifestPath()))
        {
            return [];
        }

        JsonNode root = JsonNode.Parse(File.ReadAllText(ManifestPath()))!;
        return root["sites"]!.AsObject().ToDictionary(entry => entry.Key, entry => (string?)entry.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static void Write(Dictionary<string, string> sites)
    {
        var root = new JsonObject
        {
            ["_doc"] = "W7-7 PR 4b (#1247, R-5; the owner's S6): every site that can take the element holding the keys away under them, "
                + "and what witnesses its landing: a fact that names the site ('Class.Method'), 'guard: <scope>' for a site inside a "
                + "guarded scope that neither is nor holds a region root, or 'exempt: <reason>'. FocusIntegrityManifestCensus scrapes "
                + "the sites and judges each witness; SLATE_FOCUS_MANIFEST_UPDATE=1 rewrites this file, keeping every assignment.",
            ["sites"] = new JsonObject(sites.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => KeyValuePair.Create(entry.Key, (JsonNode?)JsonValue.Create(entry.Value)))),
        };
        File.WriteAllText(
            ManifestPath(),
            root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                NewLine = "\n",
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
    }

    /// <summary>Every fact in this project, as "Class.Method", with its
    /// source.</summary>
    private static Dictionary<string, string> Facts()
    {
        HashSet<string> declared =
        [
            .. typeof(FocusIntegrityManifestCensus).Assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
                    .Select(method => $"{type.Name}.{method.Name}")),
        ];
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        string tests = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.Tests");
        foreach (string file in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (MethodDeclarationSyntax method in CSharpSource.LoadPath(file).Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.Parent is TypeDeclarationSyntax type
                    && declared.Contains($"{type.Identifier.ValueText}.{method.Identifier.ValueText}"))
                {
                    facts[$"{type.Identifier.ValueText}.{method.Identifier.ValueText}"] = method.ToFullString();
                }
            }
        }

        return facts;
    }
}
