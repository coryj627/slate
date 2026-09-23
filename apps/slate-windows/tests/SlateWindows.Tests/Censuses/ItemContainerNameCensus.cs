// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 3 (#1246, contract R-4): WPF names an item container with the
// item's ToString() when nothing else names it, and a view model's
// ToString() is its .NET type name ("SlateWindows.FileTreeNodeViewModel")
// or, for a record, a dump of every field ("RecentVault { Path = …,
// LastOpenedMs = 1790112463550 }"). The agent-operated NVDA pass of
// 2026-09-22 heard both on ten surfaces (record F3). DisplayMemberPath
// does NOT help: it templates the visible text, but the container's name
// is still its content's ToString() (measured: a ListBox with
// DisplayMemberPath publishes "Namespace.Type+Row" as every ListItem's
// name).
//
// So every items host whose ItemsSource is set — in authored XAML or in
// code — is PINNED here to how it names its containers: an
// ItemContainerStyle whose AutomationProperties.Name setter binds the
// item's speakable property (path and converter exact; a literal or an
// unrelated binding fails — codex PR 3 round 1), or a layout host
// (LayoutItemsControl: containers out of the control view, the wrapped
// control is the stop; AutomationPresentationItemsControl: no container
// peers at all). Only a container that is itself a stop — a selector's or
// a tree's item — may be named: a plain ItemsControl's container WRAPS the
// item's own controls, and named it is a second stop beside them (R-4's
// one-stop rule), so such a host must be layout with its controls named.
// A host missing from the table fails, and so does a table entry the scan
// no longer finds. The runtime twins are the name census inside the FlaUI
// axe helper, ItemContainerNameBindingTests, which host each pinned style
// and read the container's name as it follows the item, and
// WrappedStopTests, which count the stops a wrapped item exposes.
//
// The grid substrate names its rows only when the host passes
// rowAutomationName (Grids/AccessibleDataGrid.cs OnLoadingRow): the grid
// fact pins every Bind call's identity expression through the bound
// syntax tree, never a regex over C#.

using System.Windows.Controls;
using System.Xaml.Schema;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using SlateWindows.FileManagement;
using SlateWindows.Graph;
using SlateWindows.Panels;
using SlateWindows.Search;
using SlateWindows.Templates;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

/// <summary>How an items host names its containers — the R-4 pin.</summary>
internal abstract record ContainerNaming
{
    /// <summary>A LayoutItemsControl: containers leave the control view.</summary>
    internal sealed record Layout : ContainerNaming;

    /// <summary>An AutomationPresentationItemsControl: no container peers.</summary>
    internal sealed record Presentation : ContainerNaming;

    /// <summary>The AccessibleDataGrid's own DataGrid: rows are named per
    /// Bind call (the grid fact), never by a container style.</summary>
    internal sealed record Grid : ContainerNaming;

    /// <summary>A container style whose Name setter binds
    /// <paramref name="Path"/> ("" = the item itself) of
    /// <paramref name="ItemType"/>, through <paramref name="Converter"/>
    /// (a resource key) when one is named — and, in each of
    /// <see cref="Triggers"/>, exactly the pinned variant (codex PR 3 round
    /// 2: a state's name is pinned as tightly as the resting one).</summary>
    internal sealed record Bound(Type ItemType, string Path, string? Converter = null) : ContainerNaming
    {
        public IReadOnlyList<TriggerNaming> Triggers { get; init; } = [];
    }
}

/// <summary>One trigger state's name: when <paramref name="When"/> holds
/// ("IsDirty=True", conditions joined by " &amp; " in document order), the
/// container's Name binds <paramref name="Path"/> through
/// <paramref name="Converter"/> with <paramref name="Format"/> as authored
/// (the XAML <c>{}</c> escape included).</summary>
internal sealed record TriggerNaming(string When, string Path, string? Converter, string? Format);

[Trait("census", "item-container-names")]
public sealed class ItemContainerNameCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private const string ClrNamespacePrefix = "clr-namespace:";

    private static ContainerNaming.Bound Named(Type itemType, string path) => new(itemType, path);

    private static ContainerNaming.Bound Self(Type itemType) => new(itemType, string.Empty);

    /// <summary>
    /// Every items host, by label — its AutomationId, else x:Name, else its
    /// accessible name, else "{file}#{ItemsSource}" — and how it names its
    /// containers. Paths come from <c>nameof</c>, so a renamed property
    /// breaks this build, and the binding must name exactly that property.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ContainerNaming> ExpectedNaming =
        new Dictionary<string, ContainerNaming>(StringComparer.Ordinal)
        {
            // --- authored XAML: MainWindow.xaml ---
            ["Recent vaults"] = new ContainerNaming.Layout(),
            ["SidebarSortOrder"] = new ContainerNaming.Bound(
                typeof(SidebarSortMode), string.Empty, "SidebarSortModeLabelConverter"),
            ["SidebarTagTree"] = Named(typeof(SidebarTagViewModel), nameof(SidebarTagViewModel.AutomationName)),
            ["SidebarShortcuts"] = Named(typeof(SidebarShortcutViewModel), nameof(SidebarShortcutViewModel.AutomationName)),
            ["FilesTree"] = Named(typeof(FileTreeNodeViewModel), nameof(FileTreeNodeViewModel.AutomationName)),
            ["SidebarFilterResults"] = Named(typeof(FileTreeNodeViewModel), nameof(FileTreeNodeViewModel.AutomationName)),
            ["SidebarDualPane"] = Named(typeof(FileTreeNodeViewModel), nameof(FileTreeNodeViewModel.AutomationName)),
            ["PanelBacklinksList"] = Named(typeof(BacklinkRowViewModel), nameof(BacklinkRowViewModel.AutomationName)),
            ["PanelOutgoingLinksList"] = Named(typeof(OutgoingLinkRowViewModel), nameof(OutgoingLinkRowViewModel.AutomationName)),
            ["PanelOutlineList"] = Named(typeof(OutlineRowViewModel), nameof(OutlineRowViewModel.AutomationName)),
            ["PanelEmbedsList"] = new ContainerNaming.Layout(),
            ["PanelTasksOpenList"] = Named(typeof(NoteTaskRowViewModel), nameof(NoteTaskRowViewModel.AutomationName)),
            ["PanelTasksDoneList"] = Named(typeof(NoteTaskRowViewModel), nameof(NoteTaskRowViewModel.AutomationName)),
            ["PanelReviewList"] = Named(typeof(ReviewTaskRowViewModel), nameof(ReviewTaskRowViewModel.AutomationName)),
            ["PanelCitationsList"] = Named(typeof(CitationRowViewModel), nameof(CitationRowViewModel.AutomationName)),
            ["BibliographyNotices"] = new ContainerNaming.Layout(),
            ["QueriesSavedList"] = Named(typeof(SavedQuerySummary), nameof(SavedQuerySummary.Name)),
            ["QueriesBaseFilesList"] = Named(typeof(BaseFileSummary), nameof(BaseFileSummary.Path)),
            ["QueriesDashboardsList"] = Named(typeof(DashboardSummary), nameof(DashboardSummary.Name)),
            ["RightPaneLeaves"] = Named(typeof(WorkspaceLeafOption), nameof(WorkspaceLeafOption.Title)),
            ["QuickSwitcherResults"] = Named(typeof(QuickSwitcherRowViewModel), nameof(QuickSwitcherRowViewModel.DisplayName)),
            ["SearchOverlayResults"] = Named(typeof(SearchResultRowViewModel), nameof(SearchResultRowViewModel.AccessibleName)),
            ["MainWindow.xaml#{Binding SnippetSegments}"] = new ContainerNaming.Presentation(),
            ["Recent searches"] = new ContainerNaming.Layout(),
            ["CommandPaletteResults"] = Named(typeof(CommandPaletteRowViewModel), nameof(CommandPaletteRowViewModel.AccessibleName)),
            ["MainWindow.xaml#{Binding LabelSegments}"] = new ContainerNaming.Presentation(),
            ["AddPropertyType"] = Self(typeof(string)),
            ["BulkRenameOldKeyType"] = Named(typeof(BulkRenameViewModel.KeyTypeChoice), nameof(BulkRenameViewModel.KeyTypeChoice.Label)),
            ["CitationDetailsFields"] = Named(typeof(CitationField), nameof(CitationField.AutomationName)),
            ["FilesCitingList"] = Self(typeof(string)),
            ["DashboardEditorQueryPicker"] = Named(typeof(SavedQuerySummary), nameof(SavedQuerySummary.Name)),
            ["DashboardEditorSections"] = Named(typeof(DashboardEditorSection), nameof(DashboardEditorSection.AutomationName)),
            ["BuilderConditions"] = new ContainerNaming.Layout(),
            ["MainWindow.xaml#{Binding GroupMembers}"] = new ContainerNaming.Layout(),
            ["TemplatePickerList"] = Named(typeof(TemplatePickerRowViewModel), nameof(TemplatePickerRowViewModel.AccessibleName)),
            ["TemplateFlowPromptsList"] = new ContainerNaming.Layout(),
            ["MoveToList"] = Named(typeof(MoveToRowViewModel), nameof(MoveToRowViewModel.AccessibleName)),
            ["CanvasCardPickerRows"] = Named(typeof(CanvasCardPickerRow), nameof(CanvasCardPickerRow.Label)),
            ["CanvasPromptChoices"] = Named(typeof(CanvasPromptChoice), nameof(CanvasPromptChoice.Name)),

            // --- authored XAML: WorkspaceTemplates.xaml ---
            ["WorkspaceTemplates.xaml#{Binding Items}"] = new ContainerNaming.Layout(),
            ["PropertiesRows"] = new ContainerNaming.Layout(),
            ["WorkspaceTabs"] = Named(typeof(WorkspaceTabViewModel), nameof(WorkspaceTabViewModel.Title)) with
            {
                Triggers =
                [
                    new($"{nameof(WorkspaceTabViewModel.IsDirty)}=True",
                        nameof(WorkspaceTabViewModel.Title), null, "{}{0}, unsaved changes"),
                    new($"{nameof(WorkspaceTabViewModel.IsMissingFromDisk)}=True",
                        nameof(WorkspaceTabViewModel.Title), null, "{}{0}, missing from disk"),
                    new($"{nameof(WorkspaceTabViewModel.IsDirty)}=True & {nameof(WorkspaceTabViewModel.IsMissingFromDisk)}=True",
                        nameof(WorkspaceTabViewModel.Title), null, "{}{0}, missing from disk, unsaved changes"),
                ],
            },
            ["Editor panes"] = new ContainerNaming.Layout(),

            // --- built in code (codex PR 3 round 1) ---
            ["BaseViewPicker"] = Named(typeof(BaseViewSummary), nameof(BaseViewSummary.Name)),
            ["BaseWarningBanners"] = new ContainerNaming.Layout(),
            ["BaseTabList"] = Named(typeof(BaseListItemViewModel), nameof(BaseListItemViewModel.AccessibleName)),
            ["CanvasWarningRows"] = Self(typeof(string)),
            ["ConnectionsDepth"] = Self(typeof(string)),
            ["GraphInspectorGroupRing:"] = Named(typeof(GraphRingStyleSpec), nameof(GraphRingStyleSpec.Title)),
            ["GraphInspectorGroupColour:"] = Named(typeof(GraphColorTokenSpec), nameof(GraphColorTokenSpec.Title)),
            ["CanvasOutlineTree"] = Named(typeof(CanvasOutlineRowViewModel), nameof(CanvasOutlineRowViewModel.Name)),
            ["ConnectionsTree"] = Named(typeof(ConnectionsRowViewModel), nameof(ConnectionsRowViewModel.Name)),
            ["AccessibleDataGrid"] = new ContainerNaming.Grid(),
        };

    /// <summary>R-4: every items host whose ItemsSource is set — in authored
    /// XAML, or in code (codex PR 3 round 1: the Bases warning banners were a
    /// plain ItemsControl built in C#, invisible to a XAML-only scan) — names
    /// its containers exactly as the table pins, or is the layout host it
    /// pins.</summary>
    [Fact]
    public void EveryItemsSourceHostNamesItsContainersOrMarksThemLayout()
    {
        Dictionary<string, List<XElement>> keyedStyles = KeyedStyles();
        var offenders = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach ((string file, XElement host) in XamlItemsSourceHosts())
        {
            CheckXamlHost(file, host, keyedStyles, seen, offenders);
        }
        foreach (string problem in CheckCodeBuiltHosts(keyedStyles, seen))
        {
            offenders.Add(problem);
        }

        foreach ((string label, ContainerNaming naming) in ExpectedNaming)
        {
            if (!seen.Contains(label))
            {
                offenders.Add($"{label}: pinned here, but no items host by that label exists — a stale entry");
            }
            if (naming is ContainerNaming.Bound { Path.Length: > 0 } bound
                && bound.ItemType.GetProperty(bound.Path) is null)
            {
                offenders.Add($"{label}: {bound.ItemType.Name} has no property {bound.Path}");
            }
        }

        Assert.True(seen.Count > 40, $"the census found only {seen.Count} items hosts — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "items hosts whose containers would not read their pinned name:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>R-4: every <c>AccessibleDataGrid.Bind</c> call passes
    /// <c>rowAutomationName</c>, and the delegate's body is the row identity
    /// pinned for its caller (codex PR 3 round 1: a present-but-wrong
    /// identity, a literal or the whole audio description, must fail as a
    /// missing one does). Calls are BOUND, not matched by spelling (three
    /// navigators declare a Bind of their own; the bibliography and
    /// bulk-rename grids are x:Name fields), and the GridConformanceHost
    /// fixture must model the rule too. No bind is exempt (codex PR 3 round
    /// 2): an empty bind was a teardown that skipped the rows' name cleanup,
    /// and a teardown is <c>AccessibleDataGrid.Clear</c> now.</summary>
    [Fact]
    public void EveryGridBindNamesItsRows()
    {
        var expected = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["Bases/BaseSurfaceView.cs"] = ["((BaseGridRowViewModel)row).FileName", "((BaseGridRowViewModel)row).FileName"],
            ["Bases/DashboardSurfaceView.cs"] = ["((BaseGridRowViewModel)row).FileName"],
            ["Canvas/CanvasTableView.cs"] = ["((CanvasTableRow)row).SpeakableName"],
            ["Graph/GraphTableView.cs"] = ["model.RowName((GraphTableRow)row)"],
            ["MainWindow.Citations.cs"] = ["((BibliographyRowViewModel)row).TitleLine", "((UnresolvedRowViewModel)row).Key"],
            ["MainWindow.Properties.cs"] = ["((BulkRenameViewModel.PreviewRow)row).Path"],
            ["Reading/ReadingTableGrid.cs"] = ["CellText(row, 0)"],
            ["tools/GridConformanceHost/Program.cs"] = ["((FixtureRow)row).Name"],
        };
        string hostPath = Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", "tools",
            "GridConformanceHost", "Program.cs");
        CSharpSource host = CSharpSource.LoadPath(hostPath);
        CSharpCompilation compilation = ShellCompilation.Compilation
            .AddSyntaxTrees(host.Root.SyntaxTree);
        IEnumerable<(string File, CSharpSource Source)> sources = ShellCompilation.Sources
            .Select(source => (source.Relative, source.Source))
            .Append(("tools/GridConformanceHost/Program.cs", host));

        var offenders = new List<string>();
        int named = 0;
        foreach ((string file, CSharpSource source) in sources)
        {
            SemanticModel model = compilation.GetSemanticModel(source.Root.SyntaxTree);
            foreach (InvocationExpressionSyntax invocation in source.Root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsBindCall))
            {
                int line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                SymbolInfo bound = model.GetSymbolInfo(invocation);
                IMethodSymbol? method = bound.Symbol as IMethodSymbol
                    ?? bound.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (method is null)
                {
                    // Fail-closed: an unbindable Bind could be the grid's.
                    offenders.Add($"{file}:{line}: `{invocation.Expression}` does not bind, so the census cannot tell whose Bind it is");
                    continue;
                }
                if (method.ContainingType.Name != "AccessibleDataGrid")
                {
                    continue;
                }
                ArgumentSyntax? name = ArgumentFor(invocation, method, "rowAutomationName");
                if (name is null || name.Expression.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    offenders.Add($"{file}:{line}: `{invocation.Expression}` binds rows without rowAutomationName");
                    continue;
                }
                if (name.Expression is not LambdaExpressionSyntax { ExpressionBody: { } body })
                {
                    offenders.Add($"{file}:{line}: rowAutomationName is not an expression lambda the census can read");
                    continue;
                }
                string identity = body.NormalizeWhitespace().ToFullString();
                if (!expected.TryGetValue(file, out List<string>? pinned) || !pinned.Remove(identity))
                {
                    offenders.Add($"{file}:{line}: rowAutomationName reads `{identity}`, which is not the identity pinned for this caller");
                    continue;
                }
                named++;
            }
        }
        foreach ((string file, List<string> unused) in expected.Where(pair => pair.Value.Count > 0))
        {
            offenders.Add($"{file}: pinned identities no Bind reads any more: {string.Join(", ", unused)}");
        }

        Assert.True(named + offenders.Count >= 10, $"only {named + offenders.Count} row-bearing grid binds found — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "grid binds whose rows would not read their identity:\n  "
            + string.Join("\n  ", offenders));
    }

    // ---------------------------------------------------------------- XAML

    /// <summary>Every ItemsControl-derived element in the shell's view XAML
    /// that binds an ItemsSource.</summary>
    private static IEnumerable<(string File, XElement Host)> XamlItemsSourceHosts()
    {
        foreach (string path in ShellViewXaml())
        {
            string file = Path.GetFileName(path);
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants().Where(HasItemsSource))
            {
                Type? type = ResolveType(element);
                // A HierarchicalDataTemplate's ItemsSource feeds a
                // TreeViewItem, whose container style comes from its
                // TreeView (ItemsControl.PrepareItemsControl). An
                // unresolvable type is kept: CheckXamlHost fails it.
                if (type is null || typeof(ItemsControl).IsAssignableFrom(type))
                {
                    yield return (file, element);
                }
            }
        }
    }

    private static void CheckXamlHost(
        string file,
        XElement host,
        Dictionary<string, List<XElement>> keyedStyles,
        HashSet<string> seen,
        List<string> offenders)
    {
        int line = ((IXmlLineInfo)host).LineNumber;
        string label = Label(host, file);
        string site = $"{file}:{line} <{host.Name.LocalName}> {label}";
        _ = seen.Add(label);
        Type? type = ResolveType(host);
        if (type is null)
        {
            // Fail-closed: a host whose type the census cannot see is a
            // blind spot indistinguishable from the bug.
            offenders.Add($"{site}: the census cannot resolve this element's type");
            return;
        }
        if (!ExpectedNaming.TryGetValue(label, out ContainerNaming? expected))
        {
            offenders.Add($"{site}: not pinned — add it to ExpectedNaming with the property its containers must read");
            return;
        }
        string? problem = expected switch
        {
            ContainerNaming.Layout => typeof(LayoutItemsControl).IsAssignableFrom(type)
                ? null : "must be a LayoutItemsControl",
            ContainerNaming.Presentation => typeof(AutomationPresentationItemsControl).IsAssignableFrom(type)
                ? null : "must be an AutomationPresentationItemsControl",
            ContainerNaming.Bound when !ContainersAreStops(type) => WrapperIsASecondStop,
            ContainerNaming.Bound bound => BoundProblem(ContainerStyle(host, keyedStyles), bound, keyedStyles),
            _ => $"pinned as {expected}, which a XAML host cannot be",
        };
        if (problem is not null)
        {
            offenders.Add($"{site}: {problem}");
        }
    }

    private const string WrapperIsASecondStop =
        "names its containers, but they only wrap the item's own controls: a named wrapper is a second "
        + "stop beside them (R-4's one-stop rule) — make it a LayoutItemsControl and name the controls";

    /// <summary>Whether a host's containers are stops themselves — a
    /// selector's items (ListBoxItem, ComboBoxItem, TabItem) or a tree's —
    /// and so may carry the item's name. A plain ItemsControl's container
    /// only wraps the item's content.</summary>
    internal static bool ContainersAreStops(Type hostType) =>
        typeof(System.Windows.Controls.Primitives.Selector).IsAssignableFrom(hostType)
        || typeof(TreeView).IsAssignableFrom(hostType);

    /// <summary>The host's item container style: inline, or a keyed
    /// resource declared exactly once (an ambiguous key names nothing the
    /// census can pin).</summary>
    internal static XElement? ContainerStyle(
        XElement host, Dictionary<string, List<XElement>> keyedStyles)
    {
        XElement? inline = host.Elements()
            .FirstOrDefault(child => child.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal))
            ?.Elements().FirstOrDefault(child => child.Name.LocalName == "Style");
        if (inline is not null)
        {
            return inline;
        }
        return ResourceKey((string?)host.Attribute("ItemContainerStyle")) is { } key
            && keyedStyles.TryGetValue(key, out List<XElement>? styles)
            && styles.Count == 1
                ? styles[0]
                : null;
    }

    /// <summary>Why <paramref name="style"/> does not name containers by
    /// <paramref name="expected"/>, or null. The top-level Name setter (on
    /// the style or the chain it is BasedOn) must be a binding of exactly
    /// the pinned path and converter, with nothing else; the triggers that
    /// set a Name must be exactly the pinned states, in order, each binding
    /// its pinned path, converter and format (codex PR 3 round 2: a format
    /// with no {0} names every dirty tab alike).</summary>
    private static string? BoundProblem(
        XElement? style, ContainerNaming.Bound expected, Dictionary<string, List<XElement>> keyedStyles)
    {
        if (style is null)
        {
            return "names no item container (no ItemContainerStyle, or a key that does not resolve to one "
                + "style; DisplayMemberPath is not enough) — WPF names each container with its item's ToString()";
        }
        XElement? setter = TopLevelNameSetter(style, keyedStyles, depth: 0);
        if (setter is null)
        {
            return "its ItemContainerStyle sets no AutomationProperties.Name";
        }
        BindingText? binding = SetterBinding(setter);
        if (binding is null)
        {
            return $"its Name setter is not a binding (`{SetterValueText(setter)}`) — a literal names every container alike";
        }
        if (!string.Equals(binding.Path, expected.Path, StringComparison.Ordinal)
            || !string.Equals(binding.Converter, expected.Converter, StringComparison.Ordinal)
            || binding.Extras.Count > 0)
        {
            return $"its Name setter binds {binding} but R-4 pins {Describe(expected)}";
        }
        var states = new List<TriggerNaming>();
        foreach ((string when, XElement triggered) in TriggerNameSetters(style))
        {
            BindingText? variant = SetterBinding(triggered);
            if (variant is null || variant.Extras.Any(extra => extra != "StringFormat"))
            {
                return $"the Name setter when {when} (`{SetterValueText(triggered)}`) is not a binding the census can pin";
            }
            states.Add(new TriggerNaming(when, variant.Path, variant.Converter, variant.Format));
        }
        if (!states.SequenceEqual(expected.Triggers))
        {
            return "its triggers name "
                + (states.Count == 0 ? "no state" : string.Join("; ", states.Select(Describe)))
                + " but R-4 pins "
                + (expected.Triggers.Count == 0 ? "none" : string.Join("; ", expected.Triggers.Select(Describe)));
        }
        return null;
    }

    private static string Describe(TriggerNaming state) =>
        $"{state.When} → {{Binding {state.Path}"
        + (state.Converter is null ? string.Empty : $", Converter={state.Converter}")
        + (state.Format is null ? string.Empty : $", StringFormat='{state.Format}'")
        + "}";

    private static string Describe(ContainerNaming.Bound bound) =>
        (bound.Path.Length == 0 ? "the item itself" : $"{bound.ItemType.Name}.{bound.Path}")
        + (bound.Converter is null ? string.Empty : $" through {bound.Converter}");

    private static XElement? TopLevelNameSetter(
        XElement style, Dictionary<string, List<XElement>> keyedStyles, int depth)
    {
        XElement? setter = style.Elements()
            .SelectMany(child => child.Name.LocalName == "Style.Setters" ? child.Elements() : [child])
            .FirstOrDefault(child => child.Name.LocalName == "Setter"
                && (string?)child.Attribute("Property") == "AutomationProperties.Name");
        if (setter is not null)
        {
            return setter;
        }
        return depth < 8
            && ResourceKey((string?)style.Attribute("BasedOn")) is { } basedOn
            && keyedStyles.TryGetValue(basedOn, out List<XElement>? bases)
            && bases.Count == 1
                ? TopLevelNameSetter(bases[0], keyedStyles, depth + 1)
                : null;
    }

    /// <summary>Every trigger's Name setter with the trigger's conditions:
    /// a DataTrigger's binding path and value, a property Trigger's
    /// property and value, and a Multi*Trigger's conditions joined by
    /// " &amp; " in document order.</summary>
    private static IEnumerable<(string When, XElement Setter)> TriggerNameSetters(XElement style) =>
        style.Elements()
            .Where(child => child.Name.LocalName == "Style.Triggers")
            .SelectMany(triggers => triggers.Elements())
            .SelectMany(trigger => trigger.Descendants()
                .Where(element => element.Name.LocalName == "Setter"
                    && (string?)element.Attribute("Property") == "AutomationProperties.Name")
                .Select(setter => (When(trigger), setter)));

    private static string When(XElement trigger)
    {
        IEnumerable<XElement> conditions = trigger.Name.LocalName is "MultiDataTrigger" or "MultiTrigger"
            ? trigger.Elements()
                .Where(child => child.Name.LocalName.EndsWith(".Conditions", StringComparison.Ordinal))
                .SelectMany(child => child.Elements())
            : [trigger];
        return string.Join(" & ", conditions.Select(condition =>
            ((string?)condition.Attribute("Binding") is { } binding
                ? ParseBindingMarkup(binding)?.Path ?? binding
                : (string?)condition.Attribute("Property") ?? "?")
            + "=" + ((string?)condition.Attribute("Value") ?? "?")));
    }

    /// <summary>A binding's path, converter key, StringFormat (unquoted,
    /// as authored) and the names of its other arguments.</summary>
    internal sealed record BindingText(string Path, string? Converter, IReadOnlyList<string> Extras, string? Format = null)
    {
        public override string ToString() =>
            $"{{Binding {(Path.Length == 0 ? "(the item)" : Path)}"
            + (Converter is null ? string.Empty : $", Converter={Converter}")
            + (Extras.Count == 0 ? string.Empty : $", {string.Join(", ", Extras)}")
            + "}";
    }

    /// <summary>The binding a Name setter applies — its <c>Value</c>
    /// attribute's <c>{Binding …}</c>, or a <c>Setter.Value</c> holding a
    /// Binding element — or null for anything else.</summary>
    internal static BindingText? SetterBinding(XElement setter)
    {
        if ((string?)setter.Attribute("Value") is { } value)
        {
            return ParseBindingMarkup(value);
        }
        XElement? element = setter.Elements()
            .FirstOrDefault(child => child.Name.LocalName == "Setter.Value")
            ?.Elements().SingleOrDefault();
        if (element is null || element.Name.LocalName != "Binding")
        {
            return null;
        }
        string path = (string?)element.Attribute("Path") ?? string.Empty;
        string? converter = ResourceKey((string?)element.Attribute("Converter"));
        string[] extras = element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration
                && attribute.Name.LocalName is not ("Path" or "Converter"))
            .Select(attribute => attribute.Name.LocalName)
            .ToArray();
        return new BindingText(path, converter, extras, (string?)element.Attribute("StringFormat"));
    }

    private static string SetterValueText(XElement setter) =>
        (string?)setter.Attribute("Value") ?? setter.Value;

    /// <summary>Parses <c>{Binding}</c>, <c>{Binding Path}</c>,
    /// <c>{Binding Path=X, Converter={StaticResource K}}</c>: top-level
    /// arguments split on commas outside nested braces and quoted text
    /// (<c>StringFormat='{}{0}, unsaved changes'</c>).</summary>
    internal static BindingText? ParseBindingMarkup(string value)
    {
        string text = value.Trim();
        if (!text.StartsWith("{Binding", StringComparison.Ordinal) || !text.EndsWith('}')
            || (text.Length > "{Binding".Length && !char.IsWhiteSpace(text["{Binding".Length]) && text["{Binding".Length] != '}'))
        {
            return null;
        }
        string body = text["{Binding".Length..^1].Trim();
        string path = string.Empty;
        string? converter = null;
        string? format = null;
        var extras = new List<string>();
        int depth = 0;
        int start = 0;
        bool quoted = false;
        var parts = new List<string>();
        for (int index = 0; index < body.Length; index++)
        {
            if (body[index] == '\'')
            {
                quoted = !quoted;
            }
            else if (quoted)
            {
                continue;
            }
            else if (body[index] == '{')
            {
                depth++;
            }
            else if (body[index] == '}')
            {
                depth--;
            }
            else if (body[index] == ',' && depth == 0)
            {
                parts.Add(body[start..index]);
                start = index + 1;
            }
        }
        parts.Add(body[start..]);
        foreach (string raw in parts)
        {
            string part = raw.Trim();
            if (part.Length == 0)
            {
                continue;
            }
            int equals = part.IndexOf('=', StringComparison.Ordinal);
            int brace = part.IndexOf('{', StringComparison.Ordinal);
            if (equals < 0 || (brace >= 0 && brace < equals))
            {
                path = part;
                continue;
            }
            string key = part[..equals].Trim();
            string argument = part[(equals + 1)..].Trim();
            switch (key)
            {
                case "Path":
                    path = argument;
                    break;
                case "Converter":
                    converter = ResourceKey(argument) ?? argument;
                    break;
                case "StringFormat":
                    format = argument.Length >= 2 && argument[0] == '\'' && argument[^1] == '\''
                        ? argument[1..^1]
                        : argument;
                    extras.Add(key);
                    break;
                default:
                    extras.Add(key);
                    break;
            }
        }
        return new BindingText(path, converter, extras, format);
    }

    // ---------------------------------------------------------------- code

    /// <summary>Every <c>ItemsSource</c> set in the shell's C# on an
    /// ItemsControl: the host is identified (its AutomationId literal, or
    /// its XAML element for an x:Name field), classified by its constructed
    /// type, and its container style's Name setter is read from the method
    /// that builds it.</summary>
    private static IEnumerable<string> CheckCodeBuiltHosts(
        Dictionary<string, List<XElement>> keyedStyles, HashSet<string> seen)
    {
        var checkedHosts = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach ((string file, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (AssignmentExpressionSyntax assignment in source.Root
                .DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => MemberName(assignment.Left) == "ItemsSource"))
            {
                int line = assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                string site = $"{file}:{line}";
                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property)
                {
                    yield return $"{site}: an ItemsSource assignment that does not bind — the census cannot tell whose it is";
                    continue;
                }
                if (!InheritsFrom(property.ContainingType, "System.Windows.Controls.ItemsControl"))
                {
                    // A HierarchicalDataTemplate's ItemsSource: its tree names
                    // the containers.
                    continue;
                }
                (ISymbol? host, ObjectCreationExpressionSyntax? creation) = HostOf(assignment, model);
                if (host is null)
                {
                    yield return $"{site}: an ItemsSource host the census cannot identify";
                    continue;
                }
                if (!checkedHosts.Add(host))
                {
                    continue;
                }
                if (host.DeclaringSyntaxReferences.All(reference => IsGenerated(reference.SyntaxTree.FilePath)))
                {
                    // An x:Name field: the host is authored XAML.
                    if (XamlHostByName(host.Name) is not { } xaml)
                    {
                        yield return $"{site}: `{host.Name}` is an x:Name field the census cannot find in the shell's XAML";
                        continue;
                    }
                    var problems = new List<string>();
                    CheckXamlHost(xaml.File, xaml.Host, keyedStyles, seen, problems);
                    foreach (string problem in problems)
                    {
                        yield return $"{site} (ItemsSource set in code): {problem}";
                    }
                    continue;
                }
                creation ??= CreationOf(host, source.Root, model);
                if (AutomationIdOf(host, source.Root, model) is not { } label)
                {
                    yield return $"{site}: `{host.Name}` has no AutomationId literal, so the census cannot pin it";
                    continue;
                }
                _ = seen.Add(label);
                if (!ExpectedNaming.TryGetValue(label, out ContainerNaming? expected))
                {
                    yield return $"{site}: `{label}` is not pinned — add it to ExpectedNaming";
                    continue;
                }
                ITypeSymbol? type = creation is not null
                    ? model.GetTypeInfo(creation).Type
                    : (host as IFieldSymbol)?.Type ?? (host as ILocalSymbol)?.Type;
                string? problem2 = expected switch
                {
                    ContainerNaming.Layout => InheritsFrom(type, "SlateWindows.LayoutItemsControl")
                        ? null : $"is a {type?.Name ?? "(unknown)"}, but must be a LayoutItemsControl",
                    ContainerNaming.Presentation => InheritsFrom(type, "SlateWindows.AutomationPresentationItemsControl")
                        ? null : $"is a {type?.Name ?? "(unknown)"}, but must be an AutomationPresentationItemsControl",
                    ContainerNaming.Grid => InheritsFrom(type, "System.Windows.Controls.DataGrid")
                        ? null : $"is a {type?.Name ?? "(unknown)"}, but is pinned as the grid substrate's DataGrid",
                    ContainerNaming.Bound when !InheritsFrom(type, "System.Windows.Controls.Primitives.Selector")
                        && !InheritsFrom(type, "System.Windows.Controls.TreeView") => WrapperIsASecondStop,
                    ContainerNaming.Bound bound => CodeBoundProblem(host, creation, source.Root, model, bound),
                    _ => $"pinned as {expected}",
                };
                if (problem2 is not null)
                {
                    yield return $"{site} `{label}`: {problem2}";
                }
            }
        }
    }

    private static string? CodeBoundProblem(
        ISymbol host,
        ObjectCreationExpressionSyntax? creation,
        CompilationUnitSyntax root,
        SemanticModel model,
        ContainerNaming.Bound expected)
    {
        ExpressionSyntax? styleExpression = creation?.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(assignment => MemberName(assignment.Left) == "ItemContainerStyle")
            ?.Right;
        styleExpression ??= root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ItemContainerStyle" } access
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(access.Expression).Symbol, host))
            .Select(assignment => assignment.Right)
            .FirstOrDefault();
        if (styleExpression is null)
        {
            return "names no item container (no ItemContainerStyle) — WPF names each container with its item's ToString()";
        }
        if (styleExpression is not InvocationExpressionSyntax invocation
            || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax declaration)
        {
            return $"its ItemContainerStyle (`{styleExpression}`) is not a style-building method the census can read";
        }
        SemanticModel methodModel = ShellCompilation.Compilation.GetSemanticModel(declaration.SyntaxTree);
        ObjectCreationExpressionSyntax[] nameSetters = declaration.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(setter => setter.Type.ToString() == "Setter"
                && setter.ArgumentList is { Arguments.Count: 2 } arguments
                && methodModel.GetSymbolInfo(arguments.Arguments[0].Expression).Symbol is IFieldSymbol
                {
                    Name: "NameProperty",
                    ContainingType.Name: "AutomationProperties",
                })
            .ToArray();
        if (nameSetters.Length != 1)
        {
            return $"{method.Name} sets AutomationProperties.Name {nameSetters.Length} times; R-4 pins exactly one binding";
        }
        ExpressionSyntax value = nameSetters[0].ArgumentList!.Arguments[1].Expression;
        if (value is not ObjectCreationExpressionSyntax binding
            || methodModel.GetTypeInfo(binding).Type?.ToDisplayString() != "System.Windows.Data.Binding"
            || binding.Initializer is not null)
        {
            return $"{method.Name}'s Name setter is not a plain Binding (`{value}`)";
        }
        string? path = binding.ArgumentList?.Arguments.Count switch
        {
            null or 0 => string.Empty,
            1 => binding.ArgumentList.Arguments[0].Expression switch
            {
                LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
                InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } nameOf
                    => nameOf.ArgumentList.Arguments[0].Expression switch
                    {
                        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                        _ => null,
                    },
                _ => null,
            },
            _ => null,
        };
        if (path is null)
        {
            return $"{method.Name}'s Name binding path (`{binding}`) is not a literal or nameof the census can read";
        }
        return string.Equals(path, expected.Path, StringComparison.Ordinal) && expected.Converter is null
            ? null
            : $"{method.Name} binds Name to `{(path.Length == 0 ? "(the item)" : path)}` but R-4 pins {Describe(expected)}";
    }

    /// <summary>The host an ItemsSource assignment targets, and the object
    /// creation that built it when the assignment is in its initializer.</summary>
    private static (ISymbol? Host, ObjectCreationExpressionSyntax? Creation) HostOf(
        AssignmentExpressionSyntax assignment, SemanticModel model)
    {
        if (assignment.Left is MemberAccessExpressionSyntax access)
        {
            return (model.GetSymbolInfo(access.Expression).Symbol, null);
        }
        if (assignment.Parent is InitializerExpressionSyntax { Parent: ObjectCreationExpressionSyntax creation })
        {
            return creation.Parent switch
            {
                AssignmentExpressionSyntax owner => (model.GetSymbolInfo(owner.Left).Symbol, creation),
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
                    => (model.GetDeclaredSymbol(declarator), creation),
                _ => (null, creation),
            };
        }
        return (null, null);
    }

    /// <summary>The object creation assigned to a field or local host in
    /// this file, if any.</summary>
    private static ObjectCreationExpressionSyntax? CreationOf(
        ISymbol host, CompilationUnitSyntax root, SemanticModel model) =>
        root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Right is ObjectCreationExpressionSyntax
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, host))
            .Select(assignment => (ObjectCreationExpressionSyntax)assignment.Right)
            .FirstOrDefault();

    /// <summary>The first <c>AutomationProperties.SetAutomationId(host,
    /// …)</c> literal — or its literal prefix, for an id composed per row.</summary>
    private static string? AutomationIdOf(ISymbol host, CompilationUnitSyntax root, SemanticModel model)
    {
        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "AutomationProperties" },
                    Name.Identifier.ValueText: "SetAutomationId",
                }
                || invocation.ArgumentList.Arguments.Count != 2
                || !SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression).Symbol, host))
            {
                continue;
            }
            ExpressionSyntax id = invocation.ArgumentList.Arguments[1].Expression;
            while (id is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } sum)
            {
                id = sum.Left;
            }
            if (id is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }
        return null;
    }

    private static (string File, XElement Host)? XamlHostByName(string name)
    {
        foreach (string path in ShellViewXaml())
        {
            XElement? element = XDocument.Load(path, LoadOptions.SetLineInfo).Descendants()
                .FirstOrDefault(candidate => (string?)candidate.Attribute(Xaml + "Name") == name);
            if (element is not null)
            {
                return (Path.GetFileName(path), element);
            }
        }
        return null;
    }

    private static string? MemberName(ExpressionSyntax expression) =>
        expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    private static bool InheritsFrom(ITypeSymbol? type, string fullName)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == fullName)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsGenerated(string path) =>
        path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase);

    private static bool IsBindCall(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "Bind",
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == "Bind",
            _ => false,
        };

    /// <summary>The argument an invocation passes for
    /// <paramref name="parameter"/>: named, else in that parameter's
    /// position (the name delegate is a required parameter now, which a
    /// caller may pass positionally).</summary>
    private static ArgumentSyntax? ArgumentFor(
        InvocationExpressionSyntax invocation, IMethodSymbol method, string parameter)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        ArgumentSyntax? named = arguments.FirstOrDefault(
            argument => argument.NameColon?.Name.Identifier.ValueText == parameter);
        if (named is not null)
        {
            return named;
        }
        int position = method.Parameters.IndexOf(
            method.Parameters.FirstOrDefault(candidate => candidate.Name == parameter)!);
        return position >= 0 && position < arguments.Count && arguments[position].NameColon is null
            ? arguments[position]
            : null;
    }

    // ---------------------------------------------------------------- files

    private static bool IsBuildOutput(string path, string root)
    {
        string relative = Path.GetRelativePath(root, path);
        string first = relative.Split(Path.DirectorySeparatorChar)[0];
        return first is "obj" or "bin";
    }

    /// <summary>The shell's view XAML: the FocusableLayoutHostCensus
    /// discovery — theme dictionaries and App.xaml declare styles and
    /// templates, not instances.</summary>
    internal static IEnumerable<string> ShellViewXaml() =>
        AllShellXaml()
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(
                Path.GetFileName(path), "App.xaml", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> AllShellXaml() =>
        Directory.EnumerateFiles(
                SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path, SourceText.ShellSourceRoot()))
            .OrderBy(path => path, StringComparer.Ordinal);

    /// <summary>Every keyed Style in any shell XAML: a StaticResource can
    /// resolve up to App.xaml. A key declared more than once is ambiguous,
    /// so a host that uses one names nothing the census can pin.</summary>
    internal static Dictionary<string, List<XElement>> KeyedStyles()
    {
        var styles = new Dictionary<string, List<XElement>>(StringComparer.Ordinal);
        foreach (string path in AllShellXaml())
        {
            foreach (XElement style in XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName == "Style"))
            {
                if ((string?)style.Attribute(Xaml + "Key") is { } key)
                {
                    if (!styles.TryGetValue(key, out List<XElement>? declared))
                    {
                        declared = [];
                        styles[key] = declared;
                    }
                    declared.Add(style);
                }
            }
        }
        return styles;
    }

    private static bool HasItemsSource(XElement element) =>
        element.Attribute("ItemsSource") is not null
        || element.Elements().Any(child => child.Name.LocalName.EndsWith(".ItemsSource", StringComparison.Ordinal));

    /// <summary>The key of a <c>{StaticResource K}</c> or
    /// <c>{DynamicResource K}</c> markup extension; null otherwise.</summary>
    internal static string? ResourceKey(string? value)
    {
        if (value is null || !value.StartsWith('{') || !value.EndsWith('}'))
        {
            return null;
        }
        string[] parts = value[1..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || parts[0] is not ("StaticResource" or "DynamicResource"))
        {
            return null;
        }
        return parts[1].StartsWith("ResourceKey=", StringComparison.Ordinal)
            ? parts[1]["ResourceKey=".Length..]
            : parts[1];
    }

    /// <summary>The element's CLR type: a <c>clr-namespace:</c> element
    /// from the shell assembly (or the one its mapping names); a WPF
    /// element through WPF's own XAML schema context.</summary>
    internal static Type? ResolveType(XElement element)
    {
        string xmlNamespace = element.Name.NamespaceName;
        string name = element.Name.LocalName;
        if (xmlNamespace.StartsWith(ClrNamespacePrefix, StringComparison.Ordinal))
        {
            string[] mapping = xmlNamespace[ClrNamespacePrefix.Length..].Split(';');
            string? assembly = mapping.Skip(1)
                .FirstOrDefault(part => part.StartsWith("assembly=", StringComparison.Ordinal))
                ?["assembly=".Length..];
            return assembly is null
                ? typeof(MainWindow).Assembly.GetType($"{mapping[0]}.{name}")
                : Type.GetType($"{mapping[0]}.{name}, {assembly}");
        }
        return System.Windows.Markup.XamlReader.GetWpfSchemaContext()
            .GetXamlType(new XamlTypeName(xmlNamespace, name))?.UnderlyingType;
    }

    /// <summary>How the census names a host: AutomationId, else x:Name,
    /// else its accessible name, else "{file}#{ItemsSource}".</summary>
    internal static string Label(XElement element, string file) =>
        (string?)element.Attribute("AutomationProperties.AutomationId")
        ?? (string?)element.Attribute(Xaml + "Name")
        ?? (string?)element.Attribute("AutomationProperties.Name")
        ?? $"{file}#{(string?)element.Attribute("ItemsSource")}";

    /// <summary>The authored XAML host a label names, with its file.</summary>
    internal static (string File, XElement Host) XamlHost(string label)
    {
        foreach (string path in ShellViewXaml())
        {
            string file = Path.GetFileName(path);
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            XElement? host = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName is not ("HierarchicalDataTemplate" or "DataTemplate" or "Style")
                    && (element.Attribute("ItemsSource") is not null || element.Attribute(Xaml + "Name") is not null)
                    && Label(element, file) == label
                    && ResolveType(element) is { } type
                    && typeof(ItemsControl).IsAssignableFrom(type));
            if (host is not null)
            {
                return (file, host);
            }
        }
        throw new InvalidOperationException($"no XAML items host is labelled {label}");
    }
}
