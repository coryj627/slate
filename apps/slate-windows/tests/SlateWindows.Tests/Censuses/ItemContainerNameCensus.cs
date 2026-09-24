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
// code; EVERY ItemsControl derivative: ListBox, ComboBox, TreeView,
// TabControl, DataGrid, a plain ItemsControl and the shell's own
// subclasses (the spec review, round 22) — is PINNED here, none silently
// exempt, to how it names its containers and why two siblings never read
// alike (R-4; the spec review, round 21). A host whose names CAN collide
// names through SiblingNames — the one collision-aware rule: the item's
// name, a distinguisher (a path, a folder) where namesakes meet, else an
// ordinal — declared on the host and read by its container style; every
// other host records, in the table, why its names cannot collide. Beneath
// that: an
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
// no longer finds. The C# the shell authors is inventoried as well as its
// XAML (the spec review, round 23): every object creation of an
// ItemsControl derivative and every host it fills in code (ItemsSource,
// Items.Add) is listed in CodeItemsHosts — pinned in the table, a tree's
// own container, or a menu with the reason its headers cannot collide —
// and a XAML host's INLINE items (a menu, a combo's fixed choices) are read
// and proved distinct. WPF gives two EQUAL items one automation peer, so a
// rule never reads a bare string that can repeat: such strings are
// wrapped (SiblingText), or the pin says why no two are equal. The runtime twins are the name census inside the FlaUI
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

/// <summary>The sibling rule a host declares (<see cref="SiblingNames"/>):
/// the property each item is read by ("" = the item itself), the one that
/// tells namesakes apart, and an ordinal's noun. WPF gives two EQUAL items
/// ONE automation peer, so a rule over the item itself says why no two
/// items are equal (<see cref="NoEqualItems"/>); a rule over
/// <see cref="SiblingText"/> rows (<see cref="Wrapped"/>) is given wrapped
/// strings wherever its host's ItemsSource is set.</summary>
internal sealed record SiblingRule(string NamePath, string? DistinguisherPath, string Noun)
{
    public string NoEqualItems { get; init; } = string.Empty;

    public bool Wrapped { get; init; }
}

/// <summary>How an items host names its containers — the R-4 pin.</summary>
internal abstract record ContainerNaming
{
    /// <summary>A LayoutItemsControl: containers leave the control view.
    /// <paramref name="Stops"/> says how the stops its items hold are told
    /// apart; with a <paramref name="Rule"/>, each container carries its
    /// item's name under the sibling rule and the stop inside binds it.</summary>
    internal sealed record Layout(string Stops, SiblingRule? Rule = null) : ContainerNaming;

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
    /// 2: a state's name is pinned as tightly as the resting one). Allowed
    /// only where names cannot collide: <see cref="Distinct"/> says
    /// why.</summary>
    internal sealed record Bound(Type ItemType, string Path, string? Converter = null) : ContainerNaming
    {
        public string Distinct { get; init; } = string.Empty;

        public IReadOnlyList<TriggerNaming> Triggers { get; init; } = [];
    }

    /// <summary>Names through the sibling rule: the host declares
    /// <paramref name="Rule"/> over <paramref name="ItemType"/>, and its
    /// container style's Name reads <see cref="SiblingNames.Converter"/>
    /// — in each of <see cref="Triggers"/> too.</summary>
    internal sealed record Sibling(Type ItemType, SiblingRule Rule) : ContainerNaming
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

/// <summary>How an items host the shell builds or fills in C# is accounted
/// for (the spec review, round 23).</summary>
internal abstract record CodeItemsHost
{
    /// <summary>An ItemsSource host pinned in the census table as
    /// <paramref name="Label"/>, whose container naming is checked
    /// there.</summary>
    internal sealed record Pinned(string Label) : CodeItemsHost;

    /// <summary>A tree's own item container, made for the tree pinned as
    /// <paramref name="Label"/>: it takes the tree's sibling rule and names
    /// its own children under it.</summary>
    internal sealed record Container(string Label) : CodeItemsHost;

    /// <summary>A menu the shell fills with MenuItems it builds: each is its
    /// own container, named by its Header, never an item's ToString; and
    /// <paramref name="Distinct"/> says why one menu's headers cannot
    /// collide.</summary>
    internal sealed record Menu(string Distinct) : CodeItemsHost;
}

[Trait("census", "item-container-names")]
public sealed class ItemContainerNameCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private const string ClrNamespacePrefix = "clr-namespace:";

    /// <summary>The converter a container style's Name reads the sibling
    /// rule through, as authored.</summary>
    internal const string SiblingConverter = "{x:Static local:SiblingNames.Converter}";

    /// <summary>The converter that wraps an authored ItemsSource's strings
    /// into SiblingText rows.</summary>
    private const string SiblingTextRows = "{x:Static local:SiblingText.Rows}";

    private static ContainerNaming.Bound Distinct(Type itemType, string path, string reason) =>
        new(itemType, path) { Distinct = reason };

    private static ContainerNaming.Sibling Sibling(
        Type itemType, string namePath, string? distinguisherPath, string noun) =>
        new(itemType, new SiblingRule(namePath, distinguisherPath, noun));

    /// <summary>A rule over <see cref="SiblingText"/> rows: strings that may
    /// repeat, each wrapped into its own item.</summary>
    private static SiblingRule Wrapped(string noun) =>
        new(nameof(SiblingText.Text), null, noun) { Wrapped = true };

    /// <summary>The sibling rule a pin declares, if any.</summary>
    internal static SiblingRule? RuleOf(ContainerNaming? naming) => naming switch
    {
        ContainerNaming.Sibling sibling => sibling.Rule,
        ContainerNaming.Layout layout => layout.Rule,
        _ => null,
    };

    /// <summary>
    /// Every items host, by label — its AutomationId, else x:Name, else its
    /// accessible name, else "{file}#{ItemsSource}" — and how it names its
    /// containers, with no host exempt (the spec review, rounds 21 and 22):
    /// each is on the sibling rule, or says why its names cannot collide.
    /// Paths come from <c>nameof</c>, so a renamed property breaks this
    /// build, and the binding must name exactly that property.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, ContainerNaming> ExpectedNaming =
        new Dictionary<string, ContainerNaming>(StringComparer.Ordinal)
        {
            // --- authored XAML: MainWindow.xaml ---
            ["Recent vaults"] = new ContainerNaming.Layout(
                "each button's name carries its vault's path when another recent vault shares its display name (RecentVault.SpokenName)"),
            ["SidebarSortOrder"] = new ContainerNaming.Bound(
                typeof(SidebarSortMode), string.Empty, "SidebarSortModeLabelConverter")
            {
                Distinct = "each sort mode has its own label (SidebarSortModeLabelConverter)",
            },
            ["SidebarTagTree"] = Distinct(
                typeof(SidebarTagViewModel), nameof(SidebarTagViewModel.AutomationName),
                "a tag tree's siblings are distinct tag segments, each with its file count"),
            ["SidebarShortcuts"] = Sibling(
                typeof(SidebarShortcutViewModel), nameof(SidebarShortcutViewModel.AutomationName),
                nameof(SidebarShortcutViewModel.Path), "shortcut"),
            ["FilesTree"] = Sibling(
                typeof(FileTreeNodeViewModel), nameof(FileTreeNodeViewModel.AutomationName),
                nameof(FileTreeNodeViewModel.Path), "item"),
            ["SidebarFilterResults"] = Sibling(
                typeof(FileTreeNodeViewModel), nameof(FileTreeNodeViewModel.AutomationName),
                nameof(FileTreeNodeViewModel.Path), "result"),
            ["SidebarDualPane"] = Sibling(
                typeof(FileTreeNodeViewModel), nameof(FileTreeNodeViewModel.AutomationName),
                nameof(FileTreeNodeViewModel.Path), "file"),
            ["PanelBacklinksList"] = Sibling(
                typeof(BacklinkRowViewModel), nameof(BacklinkRowViewModel.AutomationName),
                nameof(BacklinkRowViewModel.SourcePath), "backlink"),
            ["PanelOutgoingLinksList"] = Sibling(
                typeof(OutgoingLinkRowViewModel), nameof(OutgoingLinkRowViewModel.AutomationName), null, "link"),
            ["PanelOutlineList"] = Sibling(
                typeof(OutlineRowViewModel), nameof(OutlineRowViewModel.AutomationName), null, "heading"),
            ["PanelEmbedsList"] = new ContainerNaming.Layout(
                "each embed is ONE named group, named by its source among its siblings",
                new SiblingRule(nameof(EmbedRowViewModel.Title), null, "embed")),
            ["PanelTasksOpenList"] = Sibling(
                typeof(NoteTaskRowViewModel), nameof(NoteTaskRowViewModel.AutomationName), null, "task"),
            ["PanelTasksDoneList"] = Sibling(
                typeof(NoteTaskRowViewModel), nameof(NoteTaskRowViewModel.AutomationName), null, "task"),
            ["PanelReviewList"] = Sibling(
                typeof(ReviewTaskRowViewModel), nameof(ReviewTaskRowViewModel.AutomationName),
                nameof(ReviewTaskRowViewModel.Path), "task"),
            ["PanelCitationsList"] = Sibling(
                typeof(CitationRowViewModel), nameof(CitationRowViewModel.AutomationName), null, "citation"),
            ["BibliographyNotices"] = new ContainerNaming.Layout(
                "each notice's focusable text is named among its siblings", Wrapped("notice")),
            ["QueriesSavedList"] = Sibling(typeof(SavedQuerySummary), nameof(SavedQuerySummary.Name), null, "query"),
            ["QueriesBaseFilesList"] = Distinct(
                typeof(BaseFileSummary), nameof(BaseFileSummary.Path), "a vault path names one file"),
            ["QueriesDashboardsList"] = Sibling(typeof(DashboardSummary), nameof(DashboardSummary.Name), null, "dashboard"),
            ["RightPaneLeaves"] = Distinct(
                typeof(WorkspaceLeafOption), nameof(WorkspaceLeafOption.Title),
                "one row per leaf kind of WorkspaceViewModel.Leaves, each titled apart"),
            ["QuickSwitcherResults"] = Sibling(
                typeof(QuickSwitcherRowViewModel), nameof(QuickSwitcherRowViewModel.DisplayName),
                nameof(QuickSwitcherRowViewModel.Path), "result"),
            ["SearchOverlayResults"] = Sibling(
                typeof(SearchResultRowViewModel), nameof(SearchResultRowViewModel.AccessibleName),
                nameof(SearchResultRowViewModel.Path), "result"),
            ["MainWindow.xaml#{Binding SnippetSegments}"] = new ContainerNaming.Presentation(),
            ["Recent searches"] = new ContainerNaming.Layout(
                "each button is named among its siblings",
                new SiblingRule(string.Empty, null, "search")
                {
                    NoEqualItems = "the recents store keeps each query once (SearchRecentsStore: an ordinal de-duplication on add and on load)",
                }),
            ["CommandPaletteResults"] = Distinct(
                typeof(CommandPaletteRowViewModel), nameof(CommandPaletteRowViewModel.AccessibleName),
                "every command has its own label (chords.json)"),
            ["MainWindow.xaml#{Binding LabelSegments}"] = new ContainerNaming.Presentation(),
            ["AddPropertyType"] = Distinct(typeof(string), string.Empty, "the property kinds are fixed and distinct"),
            ["BulkRenameOldKeyType"] = Distinct(
                typeof(BulkRenameViewModel.KeyTypeChoice), nameof(BulkRenameViewModel.KeyTypeChoice.Label),
                "the key-type choices are fixed and distinct"),
            ["CitationDetailsFields"] = Distinct(
                typeof(CitationField), nameof(CitationField.AutomationName),
                "one row per citation field, each field labelled once"),
            ["FilesCitingList"] = Distinct(typeof(string), string.Empty, "a vault path names one file"),
            ["DashboardEditorQueryPicker"] = Sibling(typeof(SavedQuerySummary), nameof(SavedQuerySummary.Name), null, "query"),
            ["DashboardEditorSections"] = Distinct(
                typeof(DashboardEditorSection), nameof(DashboardEditorSection.AutomationName),
                "each section's name carries its place, renumbered on every change (DashboardEditorViewModel)"),
            ["BuilderConditions"] = new ContainerNaming.Layout(
                "each row's controls carry the row's place (BuilderConditionRow.Number)"),
            ["MainWindow.xaml#{Binding GroupMembers}"] = new ContainerNaming.Layout(
                "each member's box carries its group's and its own place (BuilderConditionRow.Number)"),
            ["TemplatePickerList"] = Sibling(
                typeof(TemplatePickerRowViewModel), nameof(TemplatePickerRowViewModel.AccessibleName), null, "template"),
            ["TemplateFlowPromptsList"] = new ContainerNaming.Layout(
                "each prompt's box is named by its label among its siblings",
                new SiblingRule("Label", null, "field")),
            ["MoveToList"] = Distinct(
                typeof(MoveToRowViewModel), nameof(MoveToRowViewModel.AccessibleName),
                "a nested folder's row carries its full path; top-level folders and the pinned rows are distinct"),
            ["CanvasCardPickerRows"] = Sibling(
                typeof(CanvasCardPickerRow), nameof(CanvasCardPickerRow.Label), null, "card"),
            ["CanvasPromptChoices"] = Sibling(typeof(CanvasPromptChoice), nameof(CanvasPromptChoice.Name), null, "choice"),

            // --- authored XAML: WorkspaceTemplates.xaml ---
            ["WorkspaceTemplates.xaml#{Binding Items}"] = new ContainerNaming.Layout(
                "each list item's controls carry its index (PropertyPhrase.ListItemLabel)"),
            ["PropertiesRows"] = new ContainerNaming.Layout(
                "each row's controls carry its property's key, unique in a note's frontmatter"),
            ["WorkspaceTabs"] = Sibling(
                typeof(WorkspaceTabViewModel), nameof(WorkspaceTabViewModel.Title),
                nameof(WorkspaceTabViewModel.Path), "tab") with
            {
                Triggers =
                [
                    new($"{nameof(WorkspaceTabViewModel.IsDirty)}=True",
                        string.Empty, SiblingConverter, "{}{0}, unsaved changes"),
                    new($"{nameof(WorkspaceTabViewModel.IsMissingFromDisk)}=True",
                        string.Empty, SiblingConverter, "{}{0}, missing from disk"),
                    new($"{nameof(WorkspaceTabViewModel.IsDirty)}=True & {nameof(WorkspaceTabViewModel.IsMissingFromDisk)}=True",
                        string.Empty, SiblingConverter, "{}{0}, missing from disk, unsaved changes"),
                ],
            },
            ["Editor panes"] = new ContainerNaming.Layout(
                "its panes are unnamed structural panes; each tab strip is its own sibling set"),

            // --- built in code (codex PR 3 round 1) ---
            ["BaseViewPicker"] = Sibling(typeof(BaseViewSummary), nameof(BaseViewSummary.Name), null, "view"),
            ["BaseWarningBanners"] = new ContainerNaming.Layout(
                "each warning's focusable text is named among its siblings", Wrapped("warning")),
            ["BaseTabList"] = Sibling(
                typeof(BaseListItemViewModel), nameof(BaseListItemViewModel.AccessibleName),
                nameof(BaseListItemViewModel.FilePath), "row"),
            ["CanvasWarningRows"] = new ContainerNaming.Sibling(typeof(SiblingText), Wrapped("warning")),
            ["ConnectionsDepth"] = Distinct(typeof(string), string.Empty, "the three depth tags are fixed and distinct"),
            ["GraphInspectorGroupRing:"] = Sibling(
                typeof(GraphRingStyleSpec), nameof(GraphRingStyleSpec.Title), null, "style"),
            ["GraphInspectorGroupColour:"] = Sibling(
                typeof(GraphColorTokenSpec), nameof(GraphColorTokenSpec.Title), null, "colour"),
            ["{idRoot}Section{index}List"] = Sibling(
                typeof(BasesRow), nameof(BasesRow.AudioDescription), nameof(BasesRow.FilePath), "row"),
            ["CanvasOutlineTree"] = Sibling(
                typeof(CanvasOutlineRowViewModel), nameof(CanvasOutlineRowViewModel.Name), null, "item"),
            ["ConnectionsTree"] = Sibling(
                typeof(ConnectionsRowViewModel), nameof(ConnectionsRowViewModel.Name), null, "item"),
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
            // No host silently exempt: a plain binding says why its names
            // cannot collide, and a layout host how its stops are told apart.
            if (naming is ContainerNaming.Bound { Distinct.Length: 0 })
            {
                offenders.Add($"{label}: a plain Name binding with no reason its names cannot collide — put it on SiblingNames");
            }
            if (naming is ContainerNaming.Layout { Stops.Length: 0 })
            {
                offenders.Add($"{label}: a layout host that does not say how its stops are told apart");
            }
            if (naming is ContainerNaming.Sibling sibling)
            {
                foreach (string? path in new[] { sibling.Rule.NamePath, sibling.Rule.DistinguisherPath })
                {
                    if (path is { Length: > 0 } && sibling.ItemType.GetProperty(path) is null)
                    {
                        offenders.Add($"{label}: {sibling.ItemType.Name} has no property {path}");
                    }
                }
                if (sibling.ItemType == typeof(SiblingText) && !sibling.Rule.Wrapped)
                {
                    offenders.Add($"{label}: reads SiblingText rows under a rule not marked Wrapped");
                }
            }
            // Two EQUAL items are one peer (WPF keys item peers by item):
            // a rule over the item itself says why none are equal.
            if (RuleOf(naming) is { } rule)
            {
                if (rule.NamePath.Length == 0 && rule.NoEqualItems.Length == 0)
                {
                    offenders.Add(
                        $"{label}: a sibling rule over the item itself with no reason two items cannot be equal — WPF "
                        + "gives equal items ONE peer, so the second of two reads as nothing: wrap them (SiblingText)");
                }
                if (rule.Wrapped && rule.NamePath != nameof(SiblingText.Text))
                {
                    offenders.Add($"{label}: a Wrapped rule must read {nameof(SiblingText)}.{nameof(SiblingText.Text)}");
                }
            }
        }

        Assert.True(seen.Count > 40, $"the census found only {seen.Count} items hosts — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "items hosts whose containers would not read their pinned name:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Every items host the shell builds or fills in C# (the spec review,
    /// round 23), by "{Type}.{field}" or "{Type}.{member}.{local}" — an
    /// object creation of any ItemsControl derivative (ListBox, ComboBox,
    /// TreeView, DataGrid, ItemsControl, a menu, the shell's own
    /// subclasses), and every host given items in code (ItemsSource, or
    /// Items.Add) — and how its item names are accounted for. A MenuItem
    /// handed to a menu's Items.Add and never filled itself is that menu's
    /// item, not a host.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, CodeItemsHost> CodeItemsHosts =
        new Dictionary<string, CodeItemsHost>(StringComparer.Ordinal)
        {
            ["BaseSurfaceView._viewPicker"] = new CodeItemsHost.Pinned("BaseViewPicker"),
            ["BaseSurfaceView._list"] = new CodeItemsHost.Pinned("BaseTabList"),
            ["BaseSurfaceView._warningBanners"] = new CodeItemsHost.Pinned("BaseWarningBanners"),
            ["DashboardSurfaceView.BuildSectionList.list"] = new CodeItemsHost.Pinned("{idRoot}Section{index}List"),
            ["CanvasSurfaceView._warningRows"] = new CodeItemsHost.Pinned("CanvasWarningRows"),
            ["CanvasOutlineView._tree"] = new CodeItemsHost.Pinned("CanvasOutlineTree"),
            ["ConnectionsLeafView._tree"] = new CodeItemsHost.Pinned("ConnectionsTree"),
            ["ConnectionsLeafView._depth"] = new CodeItemsHost.Pinned("ConnectionsDepth"),
            ["GraphInspectorView.BuildRow.ring"] = new CodeItemsHost.Pinned("GraphInspectorGroupRing:"),
            ["GraphInspectorView.BuildRow.colour"] = new CodeItemsHost.Pinned("GraphInspectorGroupColour:"),
            ["AccessibleDataGrid._grid"] = new CodeItemsHost.Pinned("AccessibleDataGrid"),
            ["CanvasOutlineTree.GetContainerForItemOverride"] = new CodeItemsHost.Container("CanvasOutlineTree"),
            ["CanvasOutlineItem.GetContainerForItemOverride"] = new CodeItemsHost.Container("CanvasOutlineTree"),
            ["ConnectionsTree.GetContainerForItemOverride"] = new CodeItemsHost.Container("ConnectionsTree"),
            ["ConnectionsTreeItem.GetContainerForItemOverride"] = new CodeItemsHost.Container("ConnectionsTree"),
            ["CanvasContextMenuBuilder.Build.menu"] = new CodeItemsHost.Menu(
                "one row per verb CanvasContextMenuPlan.RowsFor plans, and the plan names each verb once"),
            ["CanvasContextMenuBuilder.Refill.persistent"] = new CodeItemsHost.Menu(
                "Build's rows, moved across as they are"),
            ["CanvasRendererView._menu"] = new CodeItemsHost.Menu(
                "refilled from CanvasContextMenuBuilder: one row per planned verb"),
            ["CanvasOutlineItem.ContextMenu"] = new CodeItemsHost.Menu(
                "refilled from CanvasContextMenuBuilder: one row per planned verb"),
            ["ConnectionsLeafView._rowMenu"] = new CodeItemsHost.Menu(
                "BuildRowMenu's rows, moved across: one per action core lists for the row's kind"),
            ["ConnectionsLeafView.BuildRowMenu.menu"] = new CodeItemsHost.Menu(
                "one row per action core lists for the row's kind (GraphRowActionSpec), each titled once"),
            ["GraphDiagramView._menu"] = new CodeItemsHost.Menu(
                "one row per action core lists for the node's kind, each titled once, then the pin toggle"),
            ["AccessibleDataGrid._persistentMenu"] = new CodeItemsHost.Menu(
                "BuildRowActionsMenu's rows, moved across: one per row action its host declares"),
            ["AccessibleDataGrid.BuildRowActionsMenu.menu"] = new CodeItemsHost.Menu(
                "one row per row action its host declares (AccessibleGridRowAction), each named once"),
            ["GraphVerbosityMenu.Populate.submenu"] = new CodeItemsHost.Menu(
                "one check item per verbosity choice in core's vector, each titled once"),
        };

    /// <summary>The spec review, round 23: the inventory covers the C# the
    /// shell authors as well as its XAML. Every object creation of an
    /// ItemsControl derivative and every host filled in code — bound, never
    /// matched by spelling — is in <see cref="CodeItemsHosts"/>: pinned in
    /// the table (and the very host the ItemsSource scan labels so), a
    /// pinned tree's own container, or a menu with the reason its headers
    /// cannot collide. An unlisted host fails, and so does a stale
    /// entry.</summary>
    [Fact]
    public void EveryItemsHostTheShellBuildsInCodeIsInventoried()
    {
        var labels = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        foreach (string _ in CheckCodeBuiltHosts(KeyedStyles(), new HashSet<string>(StringComparer.Ordinal), labels))
        {
        }
        var offenders = new List<string>();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string file, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            var hosts = new List<(string Key, ISymbol? Symbol, ITypeSymbol? Type, string Site)>();
            var addedLocals = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var addedCreations = new HashSet<SyntaxNode>();
            foreach (InvocationExpressionSyntax add in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (ItemsAddReceiver(add, model) is not { } receiver)
                {
                    continue;
                }
                ExpressionSyntax item = add.ArgumentList.Arguments[^1].Expression;
                if (item is BaseObjectCreationExpressionSyntax created)
                {
                    _ = addedCreations.Add(created);
                }
                else if (model.GetSymbolInfo(item).Symbol is ILocalSymbol local)
                {
                    _ = addedLocals.Add(local);
                }
                ISymbol? symbol = model.GetSymbolInfo(receiver).Symbol;
                hosts.Add((HostKey(symbol, add), symbol, model.GetTypeInfo(receiver).Type, Site(file, add)));
            }
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                ITypeSymbol? type = model.GetTypeInfo(creation).Type;
                if (!InheritsFrom(type, "System.Windows.Controls.ItemsControl"))
                {
                    continue;
                }
                ISymbol? slot = SlotOf(creation, model);
                bool anItem = addedCreations.Contains(creation) || (slot is not null && addedLocals.Contains(slot));
                bool filled = slot is not null
                    && (labels.ContainsKey(slot) || hosts.Any(host => SymbolEqualityComparer.Default.Equals(host.Symbol, slot)));
                if (anItem && !filled)
                {
                    continue;
                }
                hosts.Add((HostKey(slot, creation), slot, type, Site(file, creation)));
            }
            foreach ((string key, ISymbol? symbol, ITypeSymbol? type, string site) in hosts)
            {
                if (!found.Add(key))
                {
                    continue;
                }
                if (!CodeItemsHosts.TryGetValue(key, out CodeItemsHost? disposition))
                {
                    offenders.Add(
                        $"{site}: `{key}` ({type?.Name ?? "unknown"}) is not inventoried — pin it in ExpectedNaming and "
                        + "list it in CodeItemsHosts, or say there why its item names cannot collide");
                    continue;
                }
                string? problem = disposition switch
                {
                    CodeItemsHost.Pinned pinned when !ExpectedNaming.ContainsKey(pinned.Label) =>
                        $"points at `{pinned.Label}`, which ExpectedNaming does not pin",
                    CodeItemsHost.Pinned pinned when symbol is null
                        || !labels.TryGetValue(symbol, out string? scanned)
                        || scanned != pinned.Label =>
                        $"is not the host the ItemsSource scan labels `{pinned.Label}`",
                    CodeItemsHost.Container container
                        when ExpectedNaming.GetValueOrDefault(container.Label) is not ContainerNaming.Sibling =>
                        $"is a container of `{container.Label}`, which is not a tree on the sibling rule",
                    CodeItemsHost.Container when !InheritsFrom(type, "System.Windows.Controls.TreeViewItem") =>
                        $"is a {type?.Name ?? "(unknown)"}, not a tree's item container",
                    CodeItemsHost.Menu { Distinct.Length: 0 } =>
                        "a menu with no reason its headers cannot collide",
                    CodeItemsHost.Menu when !InheritsFrom(type, "System.Windows.Controls.Primitives.MenuBase")
                        && !InheritsFrom(type, "System.Windows.Controls.MenuItem") =>
                        $"is a {type?.Name ?? "(unknown)"}, not a menu",
                    CodeItemsHost.Menu when symbol is not null && labels.ContainsKey(symbol) =>
                        "a menu given an ItemsSource: its containers would be named by ToString — pin it instead",
                    _ => null,
                };
                if (problem is not null)
                {
                    offenders.Add($"{site} `{key}`: {problem}");
                }
            }
        }
        foreach (string key in CodeItemsHosts.Keys.Where(key => !found.Contains(key)))
        {
            offenders.Add($"{key}: inventoried, but the shell no longer builds or fills it — a stale entry");
        }
        Assert.True(found.Count > 20, $"the inventory found only {found.Count} code-built hosts — the scan is broken");
        Assert.True(
            offenders.Count == 0,
            "items hosts built in code that the census does not account for:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>The host an <c>X.Items.Add(item)</c> or
    /// <c>X.Items.Insert(index, item)</c> fills — an ItemsControl's own
    /// Items, bound — or null for any other call.</summary>
    private static ExpressionSyntax? ItemsAddReceiver(InvocationExpressionSyntax invocation, SemanticModel model) =>
        invocation.Expression is MemberAccessExpressionSyntax
        {
            Name.Identifier.ValueText: "Add" or "Insert",
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Items" } items,
        }
        && invocation.ArgumentList.Arguments.Count > 0
        && model.GetSymbolInfo(items).Symbol is IPropertySymbol property
        && InheritsFrom(property.ContainingType, "System.Windows.Controls.ItemsControl")
            ? items.Expression
            : null;

    /// <summary>The field, property or local a creation is stored in.</summary>
    private static ISymbol? SlotOf(BaseObjectCreationExpressionSyntax creation, SemanticModel model) =>
        creation.Parent switch
        {
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => model.GetDeclaredSymbol(declarator),
            AssignmentExpressionSyntax assignment when assignment.Right == creation =>
                model.GetSymbolInfo(assignment.Left).Symbol,
            _ => null,
        };

    /// <summary>"{Type}.{field}" for a field or property; "{Type}.{member}.{name}"
    /// for a local or parameter; "{Type}.{member}" for a host held by
    /// neither.</summary>
    private static string HostKey(ISymbol? symbol, SyntaxNode site)
    {
        string owner = site.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "(file)";
        string member = site.Ancestors().Select(ancestor => ancestor switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            LocalFunctionStatementSyntax local => local.Identifier.ValueText,
            ConstructorDeclarationSyntax => "ctor",
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            _ => null,
        }).FirstOrDefault(name => name is not null) ?? "(member)";
        return symbol switch
        {
            IFieldSymbol or IPropertySymbol => $"{owner}.{symbol.Name}",
            ILocalSymbol or IParameterSymbol => $"{owner}.{member}.{symbol.Name}",
            _ => $"{owner}.{member}",
        };
    }

    private static string Site(string file, SyntaxNode node) =>
        $"{file}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    /// <summary>XAML hosts whose inline items do not all carry a literal
    /// name, and why those items cannot read alike.</summary>
    internal static readonly IReadOnlyDictionary<string, string> InlineItemsHosts =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["StatusBar"] =
                "three fixed regions of different kinds — the vault state, the status message and the progress bar",
        };

    /// <summary>The spec review, rounds 21-23: a XAML items host with INLINE
    /// items — a menu, a combo's fixed choices, the status bar; every
    /// ItemsControl derivative the shell fills without an ItemsSource —
    /// names each item as authored, so the census reads those names and
    /// proves no two siblings alike (access keys dropped, case ignored as
    /// speech ignores it). A host whose items carry no literal name says in
    /// <see cref="InlineItemsHosts"/> why they cannot read alike.</summary>
    [Fact]
    public void EveryAuthoredInlineItemsHostNamesItsItemsApart()
    {
        var offenders = new List<string>();
        var listed = new HashSet<string>(StringComparer.Ordinal);
        int hosts = 0;
        foreach (string path in ShellViewXaml())
        {
            string file = Path.GetFileName(path);
            foreach (XElement element in XDocument.Load(path, LoadOptions.SetLineInfo).Descendants())
            {
                if (element.Name.LocalName.Contains('.', StringComparison.Ordinal)
                    || HasItemsSource(element)
                    || ResolveType(element) is not { } type
                    || !typeof(ItemsControl).IsAssignableFrom(type))
                {
                    continue;
                }
                XElement[] items =
                [
                    .. element.Elements().Where(child =>
                        !child.Name.LocalName.Contains('.', StringComparison.Ordinal)
                        && child.Name.LocalName != "Separator"),
                ];
                if (items.Length == 0)
                {
                    continue;
                }
                hosts++;
                string label = Label(element, file);
                string site = $"{file}:{((IXmlLineInfo)element).LineNumber} <{element.Name.LocalName}> {label}";
                string?[] names = [.. items.Select(AuthoredItemName)];
                if (names.Any(name => name is null))
                {
                    if (InlineItemsHosts.TryGetValue(label, out string? reason) && reason.Length > 0)
                    {
                        _ = listed.Add(label);
                    }
                    else
                    {
                        offenders.Add($"{site}: an inline item with no literal name — name it, or say in InlineItemsHosts why its items cannot read alike");
                    }
                    continue;
                }
                foreach (IGrouping<string, string?> clash in names
                    .GroupBy(name => name!, StringComparer.CurrentCultureIgnoreCase)
                    .Where(group => group.Skip(1).Any()))
                {
                    offenders.Add($"{site}: {clash.Count()} items read \"{clash.Key}\"");
                }
            }
        }
        foreach (string label in InlineItemsHosts.Keys.Where(label => !listed.Contains(label)))
        {
            offenders.Add($"{label}: listed as a host of unnamed inline items, but no such host exists — a stale entry");
        }
        Assert.True(hosts > 10, $"the census found only {hosts} inline items hosts — the scan is broken");
        Assert.True(
            offenders.Count == 0,
            "inline items that could read alike:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>An inline item's authored name — AutomationProperties.Name,
    /// else its Header, else its Content — when that is literal text, its
    /// access-key markers dropped; null otherwise.</summary>
    internal static string? AuthoredItemName(XElement item)
    {
        foreach (string attribute in new[] { "AutomationProperties.Name", "Header", "Content" })
        {
            if ((string?)item.Attribute(attribute) is { } value)
            {
                return value.StartsWith('{') ? null : value.Replace("_", string.Empty, StringComparison.Ordinal);
            }
        }
        return null;
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
            ContainerNaming.Layout when !typeof(LayoutItemsControl).IsAssignableFrom(type) =>
                "must be a LayoutItemsControl",
            ContainerNaming.Layout { Rule: { } rule } =>
                XamlSiblingProblem(host, rule, ContainerStyle(host, keyedStyles), [], keyedStyles),
            ContainerNaming.Layout => null,
            ContainerNaming.Presentation => typeof(AutomationPresentationItemsControl).IsAssignableFrom(type)
                ? null : "must be an AutomationPresentationItemsControl",
            ContainerNaming.Bound or ContainerNaming.Sibling when !ContainersAreStops(type) => WrapperIsASecondStop,
            ContainerNaming.Bound bound => BoundProblem(ContainerStyle(host, keyedStyles), bound, keyedStyles),
            ContainerNaming.Sibling sibling => XamlSiblingProblem(
                host, sibling.Rule, ContainerStyle(host, keyedStyles), sibling.Triggers, keyedStyles),
            _ => $"pinned as {expected}, which a XAML host cannot be",
        };
        if (problem is not null)
        {
            offenders.Add($"{site}: {problem}");
        }
    }

    /// <summary>Why a XAML host does not name through the sibling rule
    /// <paramref name="rule"/>, or null: it declares the rule's name path,
    /// distinguisher and noun exactly, and its container style's Name — at
    /// rest and in each pinned trigger state — reads the rule's converter
    /// from the container itself.</summary>
    private static string? XamlSiblingProblem(
        XElement host,
        SiblingRule rule,
        XElement? style,
        IReadOnlyList<TriggerNaming> triggers,
        Dictionary<string, List<XElement>> keyedStyles)
    {
        foreach ((string property, string? expected) in new[]
        {
            ("SiblingNames.NamePath", (string?)rule.NamePath),
            ("SiblingNames.DistinguisherPath", rule.DistinguisherPath),
            ("SiblingNames.Noun", (string?)rule.Noun),
        })
        {
            string? declared = (string?)host.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == property);
            if (!string.Equals(declared, expected, StringComparison.Ordinal))
            {
                return $"declares {property}={(declared is null ? "(none)" : $"\"{declared}\"")} but R-4 pins "
                    + (expected is null ? "none" : $"\"{expected}\"");
            }
        }
        if (rule.Wrapped
            && ParseBindingMarkup((string?)host.Attribute("ItemsSource") ?? string.Empty)?.Converter != SiblingTextRows)
        {
            return $"reads SiblingText rows, but its ItemsSource does not wrap them (Converter={SiblingTextRows}): "
                + "two equal strings would be one peer";
        }
        if (style is null)
        {
            return "declares the sibling rule but has no container style to read it";
        }
        XElement? setter = TopLevelNameSetter(style, keyedStyles, depth: 0);
        if (setter is null || !IsSiblingBinding(setter, out string? format) || format is not null)
        {
            return $"its container style's Name (`{(setter is null ? "none" : SetterValueText(setter))}`) does not read "
                + $"{{Binding RelativeSource={{RelativeSource Self}}, Converter={SiblingConverter}}}";
        }
        var states = new List<TriggerNaming>();
        foreach ((string when, XElement triggered) in TriggerNameSetters(style))
        {
            if (!IsSiblingBinding(triggered, out string? stateFormat))
            {
                return $"the Name setter when {when} (`{SetterValueText(triggered)}`) does not read the sibling rule";
            }
            states.Add(new TriggerNaming(when, string.Empty, SiblingConverter, stateFormat));
        }
        return states.SequenceEqual(triggers)
            ? null
            : "its triggers name "
                + (states.Count == 0 ? "no state" : string.Join("; ", states.Select(Describe)))
                + " but R-4 pins "
                + (triggers.Count == 0 ? "none" : string.Join("; ", triggers.Select(Describe)));
    }

    /// <summary>Whether a Name setter is the sibling rule's binding —
    /// <c>{Binding RelativeSource={RelativeSource Self}, Converter={x:Static
    /// local:SiblingNames.Converter}}</c>, a StringFormat allowed — and its
    /// format.</summary>
    private static bool IsSiblingBinding(XElement setter, out string? format)
    {
        format = null;
        if (SetterBinding(setter) is not { } binding
            || binding.Path.Length != 0
            || binding.Converter != SiblingConverter
            || binding.Extras.Any(extra => extra is not ("RelativeSource" or "StringFormat"))
            || !SetterValueText(setter).Contains("RelativeSource={RelativeSource Self}", StringComparison.Ordinal))
        {
            return false;
        }
        format = binding.Format;
        return true;
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
        Dictionary<string, List<XElement>> keyedStyles,
        HashSet<string> seen,
        Dictionary<ISymbol, string>? labels = null)
    {
        var checkedHosts = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var hostLabels = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
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
                // A presenter handed the host (the palette's results list,
                // W7-7 PR 9): follow the field back to what its constructor
                // was passed.
                if (host is IFieldSymbol field && ForwardedHost(field) is { } forwarded)
                {
                    host = forwarded;
                    creation = null;
                }
                if (!checkedHosts.Add(host))
                {
                    if (hostLabels.TryGetValue(host, out string? known) && WrapProblem(known, assignment, model) is { } again)
                    {
                        yield return $"{site} `{known}`: {again}";
                    }
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
                hostLabels[host] = label;
                if (labels is not null)
                {
                    labels[host] = label;
                }
                if (!ExpectedNaming.TryGetValue(label, out ContainerNaming? expected))
                {
                    yield return $"{site}: `{label}` is not pinned — add it to ExpectedNaming";
                    continue;
                }
                if (WrapProblem(label, assignment, model) is { } wrap)
                {
                    yield return $"{site} `{label}`: {wrap}";
                }
                ITypeSymbol? type = creation is not null
                    ? model.GetTypeInfo(creation).Type
                    : (host as IFieldSymbol)?.Type ?? (host as ILocalSymbol)?.Type;
                string? problem2 = expected switch
                {
                    ContainerNaming.Layout when !InheritsFrom(type, "SlateWindows.LayoutItemsControl") =>
                        $"is a {type?.Name ?? "(unknown)"}, but must be a LayoutItemsControl",
                    ContainerNaming.Layout { Rule: { } rule } => CodeSiblingProblem(host, creation, source.Root, model, rule),
                    ContainerNaming.Layout => null,
                    ContainerNaming.Presentation => InheritsFrom(type, "SlateWindows.AutomationPresentationItemsControl")
                        ? null : $"is a {type?.Name ?? "(unknown)"}, but must be an AutomationPresentationItemsControl",
                    ContainerNaming.Grid => InheritsFrom(type, "System.Windows.Controls.DataGrid")
                        ? null : $"is a {type?.Name ?? "(unknown)"}, but is pinned as the grid substrate's DataGrid",
                    ContainerNaming.Bound or ContainerNaming.Sibling
                        when !InheritsFrom(type, "System.Windows.Controls.Primitives.Selector")
                        && !InheritsFrom(type, "System.Windows.Controls.TreeView") => WrapperIsASecondStop,
                    ContainerNaming.Bound bound => CodeBoundProblem(host, creation, source.Root, model, bound),
                    ContainerNaming.Sibling sibling => CodeSiblingProblem(host, creation, source.Root, model, sibling.Rule),
                    _ => $"pinned as {expected}",
                };
                if (problem2 is not null)
                {
                    yield return $"{site} `{label}`: {problem2}";
                }
            }
        }
    }

    /// <summary>Why an ItemsSource assignment to a host that reads
    /// SiblingText rows does not wrap its strings, or null.</summary>
    private static string? WrapProblem(string label, AssignmentExpressionSyntax assignment, SemanticModel model) =>
        RuleOf(ExpectedNaming.GetValueOrDefault(label)) is { Wrapped: true }
        && !(assignment.Right is InvocationExpressionSyntax call
            && model.GetSymbolInfo(call).Symbol is IMethodSymbol
            {
                Name: nameof(SiblingText.Wrap),
                ContainingType.Name: nameof(SiblingText),
            })
            ? $"reads SiblingText rows, but this ItemsSource (`{assignment.Right}`) is not SiblingText.Wrap(…): "
                + "two equal strings would be one peer"
            : null;

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

    /// <summary>Why a code-built host does not name through the sibling
    /// rule, or null: the view declares the rule's name path, distinguisher
    /// and noun on the host (<c>SiblingNames.SetNamePath</c> and its
    /// siblings, literal or nameof), and the host's container style is
    /// <c>SiblingNames.ContainerStyle</c> or a style-building method whose
    /// one Name setter is <c>SiblingNames.ContainerNameBinding()</c>.</summary>
    private static string? CodeSiblingProblem(
        ISymbol host,
        ObjectCreationExpressionSyntax? creation,
        CompilationUnitSyntax root,
        SemanticModel model,
        SiblingRule rule)
    {
        foreach ((string setter, string? expected) in new[]
        {
            ("SetNamePath", (string?)rule.NamePath),
            ("SetDistinguisherPath", rule.DistinguisherPath),
            ("SetNoun", (string?)rule.Noun),
        })
        {
            string?[] declared =
            [
                .. root.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax
                        {
                            Expression: IdentifierNameSyntax { Identifier.ValueText: "SiblingNames" },
                        } access
                        && access.Name.Identifier.ValueText == setter
                        && invocation.ArgumentList.Arguments.Count == 2
                        && SymbolEqualityComparer.Default.Equals(
                            model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression).Symbol, host))
                    .Select(invocation => ConstantText(invocation.ArgumentList.Arguments[1].Expression, model)),
            ];
            if (expected is null ? declared.Length != 0 : declared is not [{ } value] || value != expected)
            {
                return $"declares SiblingNames.{setter} as [{string.Join(", ", declared)}] but R-4 pins "
                    + (expected is null ? "none" : $"\"{expected}\"");
            }
        }
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
        if (styleExpression is not InvocationExpressionSyntax invocation
            || model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return $"its ItemContainerStyle (`{styleExpression}`) is not a style the census can read";
        }
        if (method.ContainingType.Name == "SiblingNames" && method.Name == "ContainerStyle")
        {
            return null;
        }
        if (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax declaration)
        {
            return $"its ItemContainerStyle (`{styleExpression}`) is not a style-building method the census can read";
        }
        SemanticModel methodModel = ShellCompilation.Compilation.GetSemanticModel(declaration.SyntaxTree);
        ExpressionSyntax[] values =
        [
            .. declaration.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Where(setter => setter.Type.ToString() == "Setter"
                    && setter.ArgumentList is { Arguments.Count: 2 } arguments
                    && methodModel.GetSymbolInfo(arguments.Arguments[0].Expression).Symbol is IFieldSymbol
                    {
                        Name: "NameProperty",
                        ContainingType.Name: "AutomationProperties",
                    })
                .Select(setter => setter.ArgumentList!.Arguments[1].Expression),
        ];
        return values is [InvocationExpressionSyntax nameBinding]
            && methodModel.GetSymbolInfo(nameBinding).Symbol is IMethodSymbol
            {
                Name: "ContainerNameBinding",
                ContainingType.Name: "SiblingNames",
            }
                ? null
                : $"{method.Name}'s Name setter does not read SiblingNames.ContainerNameBinding()";
    }

    /// <summary>A literal, nameof or <c>string.Empty</c> argument's text;
    /// null for anything else.</summary>
    private static string? ConstantText(ExpressionSyntax expression, SemanticModel model) =>
        model.GetConstantValue(expression) is { HasValue: true, Value: string text }
            ? text
            : model.GetSymbolInfo(expression).Symbol is IFieldSymbol
            {
                Name: "Empty",
                ContainingType.SpecialType: SpecialType.System_String,
            }
                ? string.Empty
                : null;

    /// <summary>When <paramref name="field"/> is only ever assigned a
    /// constructor parameter of its type, the one symbol every construction
    /// of that type passes for it — the host a presenter was handed — else
    /// null.</summary>
    private static ISymbol? ForwardedHost(IFieldSymbol field)
    {
        var parameters = new HashSet<IParameterSymbol>(SymbolEqualityComparer.Default);
        foreach ((_, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (AssignmentExpressionSyntax assignment in source.Root
                .DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(assignment.Left).Symbol, field)))
            {
                if (model.GetSymbolInfo(assignment.Right).Symbol is not IParameterSymbol
                    {
                        ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor },
                    } parameter)
                {
                    return null;
                }
                _ = parameters.Add(parameter);
            }
        }
        if (parameters.Count != 1)
        {
            return null;
        }
        IParameterSymbol forwarded = parameters.Single();
        var passed = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach ((_, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes()
                .OfType<BaseObjectCreationExpressionSyntax>()
                .Where(creation => SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(creation).Symbol, forwarded.ContainingSymbol)))
            {
                SeparatedSyntaxList<ArgumentSyntax> arguments = creation.ArgumentList?.Arguments ?? default;
                ArgumentSyntax? argument = arguments.FirstOrDefault(
                    candidate => candidate.NameColon?.Name.Identifier.ValueText == forwarded.Name)
                    ?? (forwarded.Ordinal < arguments.Count && arguments[forwarded.Ordinal].NameColon is null
                        ? arguments[forwarded.Ordinal]
                        : null);
                if (argument is null || model.GetSymbolInfo(argument.Expression).Symbol is not { } symbol)
                {
                    return null;
                }
                _ = passed.Add(symbol);
            }
        }
        return passed.Count == 1 ? passed.Single() : null;
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
            // An id composed per section ($"{idRoot}Section{index}List") is
            // labelled by its template, holes and all.
            if (id is InterpolatedStringExpressionSyntax interpolated)
            {
                return string.Concat(interpolated.Contents.Select(content => content switch
                {
                    InterpolatedStringTextSyntax text => text.TextToken.ValueText,
                    InterpolationSyntax hole => $"{{{hole.Expression}}}",
                    _ => string.Empty,
                }));
            }
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
