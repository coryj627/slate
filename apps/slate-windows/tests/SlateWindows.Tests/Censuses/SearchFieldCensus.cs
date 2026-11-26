// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-35 (#1837): every text field in the shell is classified against the class the
// palette's DS-10 flush closed. A member is a field whose text filters or selects a list, or a
// field that holds the keys while that same list republishes in the background: TSF can be
// granted its lock between dispatcher operations, and a list publication that leaves layout
// pending then has its layout and automation events run inside the lock. A member names its
// list and the facts that hold its publications to the palette's flush, each a fact that
// exercises that member, or the issue that will (CB-35b, #2070, undated, so a slipped CB-35b
// does not turn main red). Every other field is exempt, with its reason.
//
// The census is keyed by type, not by tag or name. A site is:
// - every element of a TextBoxBase type in the shell's XAML, its type resolved through the
//   XAML namespace's mapping (a clr-namespace, or a URI the referenced assemblies map with
//   XmlnsDefinition), and every one built in its code;
// - every control whose template hosts one as a part: a DatePicker's DatePickerTextBox, and a
//   ComboBox's PART_EditableTextBox wherever IsEditable can be other than false (its attribute,
//   a style's setter, or code that sets or binds it);
// - every DataGridTextColumn whose editing box is WPF's own TextBox (one that does not build
//   its own in GenerateEditingElement, where the box it builds is the site instead).
// It reads sources only, binding them through ShellCompilation.

using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "search-fields")]
public sealed class SearchFieldCensus
{
    private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The XAML namespaces whose elements are not the UI's: the language's own, the
    /// markup-compatibility one and the designer's.</summary>
    private static readonly string[] LanguageNamespaces =
    [
        Xaml,
        "http://schemas.openxmlformats.org/markup-compatibility/2006",
        "http://schemas.microsoft.com/expression/blend/2008",
    ];

    /// <summary>CB-35b's issue: the grids and the canvas tree.</summary>
    private const string Cb35b = "#2070";

    private const string Publication = nameof(SearchFieldPublicationTests);

    /// <summary>One text field: its site key, and either the list it filters or selects with
    /// the facts that hold that list's publications (or the issue that will), or the reason it
    /// is no member. A held member names the word its facts are named by
    /// (<see cref="Exercises"/>).</summary>
    internal sealed record Row(
        string Site,
        string? List = null,
        string[]? Facts = null,
        string? Deferred = null,
        string? Exempt = null,
        string? Member = null);

    internal static readonly Row[] Table =
    [
        // The members this PR holds, and the palette they are held to.
        new("CommandPaletteSearch", "CommandPaletteResults",
        [
            $"{nameof(CommandPaletteTests)}.{nameof(CommandPaletteTests.TheRowsAreLaidOutInTheOperationThatPublishesThem)}",
            $"{nameof(CommandPaletteTests)}.{nameof(CommandPaletteTests.ALetterTypedInTheListMovesNothing)}",
        ], Member: "CommandPalette"),
        new("QuickSwitcherSearch", "QuickSwitcherResults",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenShowsTheRowTheArrowsSelect)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenKeepsAReRankedSelectionInView)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenKeepsTheViewThroughASilentReRank)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.AKeystrokeInTheFieldLeavesNothingPendingButTheField)}",
        ], Member: "QuickOpen"),
        new("SearchOverlaySearch", "SearchOverlayResults",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchsScopeChangeLeavesNothingPending)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchsClearRecentsLeavesNothingPending)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchShowsTheRowTheArrowsSelect)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchsFirstResultsLeaveNoScrollDeferred)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.AKeystrokeInTheFieldLeavesNothingPendingButTheField)}",
        ], Member: "Search"),
        new("SidebarFilter", "SidebarFilterResults",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.FilesFilterLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.FilesFiltersFailureLeavesNothingPending)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.AKeystrokeInTheFieldLeavesNothingPendingButTheField)}",
        ], Member: "FilesFilter"),
        new("MoveToFilter", "MoveToList",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToShowsTheRowTheArrowsSelect)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToKeepsTheViewWhileFoldersLoad)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToKeepsTheKeysOnARowWhileFoldersLoad)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToHoldsItsSheetStillAsItOpensAtTwiceTheTextSize)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.AKeystrokeInTheFieldLeavesNothingPendingButTheField)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.TheSheetHoldsStillWhileItsListShowsARowAtTwiceTheTextSize)}",
        ], Member: "MoveTo"),
        new("CanvasCardPickerFilter", "CanvasCardPickerRows",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.ThePickersLeaveNothingPendingWhenTheyPublish)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.APickerKeepsASelectionWhenItsFilterDropsTheRow)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.AKeystrokeInTheFieldLeavesNothingPendingButTheField)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.TheSheetHoldsStillWhileItsListShowsARowAtTwiceTheTextSize)}",
        ], Member: "Picker"),

        // CB-35b's members: the grids and the canvas tree.
        new("BibliographySearch", "BibliographyEntriesGrid", Deferred: Cb35b),
        new("Bases/BaseSurfaceView.cs#_quickFilter", "the base's table grid and its list", Deferred: Cb35b),
        new("Canvas/CanvasSurfaceView.cs#_filterField", "the canvas outline tree and its table grid", Deferred: Cb35b),
        new("Graph/GraphSurfaceView.cs#_filterField", "the graph table's grid", Deferred: Cb35b),
        new("Graph/GraphInspectorView.cs#_nameQuery", "the graph table's grid", Deferred: Cb35b),
        new("Graph/GraphInspectorView.cs#query", "the graph table's grid, through a new epoch", Deferred: Cb35b),
        new("BulkRenameOldKey", "the bulk rename preview grid its Enter rebinds", Deferred: Cb35b),
        new("BulkRenameNewKey", "the bulk rename preview grid its Enter rebinds", Deferred: Cb35b),

        // The rest: no list of their own, or a list that is not theirs.
        new("FindInNoteQuery", Exempt: "searches the open note's text; its matches are ranges in the editor, not a list"),
        new("FindInNoteReplaceText", Exempt: "the replacement text; it filters nothing"),
        new("MainWindow.xaml#SidebarBatchActions/TextBox#0", Exempt: "a tag name for the checked files; it filters nothing"),
        new("SidebarMutationName", Exempt: "names a new or renamed item; the Files tree publishing under it is the recorded residual (#1955), not its list"),
        new("QueriesRenameBox", Exempt: "a saved query's new name; it filters nothing"),
        new("AddPropertyKey", Exempt: "a new property's key; it filters nothing"),
        new("AddPropertyValue", Exempt: "a new property's initial value; it filters nothing"),
        new("DashboardEditorName", Exempt: "a dashboard's name; it filters nothing"),
        new("MainWindow.xaml#DashboardEditorSections/TextBox#0", Exempt: "a dashboard section's heading; it filters nothing"),
        new("MainWindow.xaml#BuilderConditions/TextBox#0", Exempt: "one condition's expression; its debounced preview is a line, not a list"),
        new("MainWindow.xaml#BuilderConditions/TextBox#1", Exempt: "one grouped condition's expression; its debounced preview is a line, not a list"),
        new("BuilderSavePathBox", Exempt: "the new .base file's path; it filters nothing"),
        new("BuilderSaveNameBox", Exempt: "the saved query's name; it filters nothing"),
        new("MainWindow.xaml#TemplateFlowPromptsList/TextBox#0", Exempt: "one template prompt's answer; it filters nothing"),
        new("TemplateFlowName", Exempt: "the new note's name; it filters nothing"),
        new("CanvasCardEditorText", Exempt: "a card's text; it filters nothing"),
        new("CanvasPromptDraft", Exempt: "a prompt's typed answer; the choices list beside it is fixed while it is typed in"),
        new("WorkspaceTemplates.xaml#PropertyTextEditorTemplate/TextBox#0", Exempt: "a property's value; it filters nothing"),
        new("WorkspaceTemplates.xaml#PropertyDatePickerTemplate/DatePicker#0", Exempt: "a date property's read-only text part; it filters nothing"),
        new("WorkspaceTemplates.xaml#PropertyNumberEditorTemplate/TextBox#0", Exempt: "a number property's value; it filters nothing"),
        new("WorkspaceTemplates.xaml#PropertyWikilinkEditorTemplate/TextBox#0", Exempt: "a link property's target; its picker is a sheet of its own"),
        new("WorkspaceTemplates.xaml#PropertyListEditorTemplate/TextBox#0", Exempt: "one list property item's value; it filters nothing"),
        new("WorkspaceTemplates.xaml#EditorInteractionPopover/TextBox#0", Exempt: "a link preview's read-only body; it filters nothing"),
        new("ReadingSurface", Exempt: "the reading view's document; its text is the note, not a filter"),
        new("EditorEmbedPreview.cs#text", Exempt: "an embed preview's read-only text; it filters nothing"),
        new("Canvas/CanvasSurfaceView.cs#_whereAmIReadback", Exempt: "a read-only readback of the canvas selection; it filters nothing"),
        new("Graph/GraphSurfaceView.cs#_whereAmIReadback", Exempt: "a read-only readback of the graph selection; it filters nothing"),
        new("Grids/AccessibleDataGrid.cs#editor", Exempt: "one cell's editor, the grid's text columns' own box; it edits that cell and filters nothing"),
        new("Panels/HistorySurfaceView.cs#box", Exempt: "a restore's destination path; it filters nothing"),
    ];

    /// <summary>One place the shell makes a text field.</summary>
    internal sealed record Site(string File, int Line, string Key);

    [Fact]
    public void EverySearchFieldIsClassified()
    {
        CSharpCompilation compilation = ShellCompilation.Compilation;
        IReadOnlyDictionary<string, string[]> mapped = XmlnsDefinitions(compilation);
        (string File, XDocument Document)[] documents = [.. ShellXaml()];
        List<Site> xaml = [.. documents.SelectMany(document => XamlSites(document.File, document.Document, compilation, mapped))];
        List<Site> code = [.. CodeSites(compilation, ShellCompilation.Sources.Select(source => (source.Relative, source.Source.Root.SyntaxTree)))];

        var problems = new List<string>();
        problems.AddRange(UnreadNamespaces(documents, compilation, mapped));
        if (xaml.Count < 30)
        {
            problems.Add($"only {xaml.Count} text fields were found in the shell's XAML; the scrape is broken");
        }

        if (code.Count < 10)
        {
            problems.Add($"only {code.Count} text fields were found in the shell's code; the scrape is broken");
        }

        problems.AddRange(Unlisted([.. xaml, .. code], Table));
        problems.AddRange(Stale([.. xaml, .. code], Table));
        problems.AddRange(Malformed(Table, typeof(SearchFieldCensus).Assembly, ListIds(documents)));

        Assert.True(
            problems.Count == 0,
            "Text fields the census does not hold to the search-field class (CB-35):\n  " + string.Join("\n  ", problems));
    }

    // ---- the table against the sites and the facts ----------------------------------

    internal static IEnumerable<string> Unlisted(IEnumerable<Site> sites, IReadOnlyList<Row> table)
    {
        foreach (Site site in sites)
        {
            if (!table.Any(row => row.Site == site.Key))
            {
                yield return $"{site.File}:{site.Line} a text field ({site.Key}) has no row in SearchFieldCensus.Table: "
                    + "add one with its list and the facts that hold its publications, or with the reason it is no member";
            }
        }
    }

    internal static IEnumerable<string> Stale(IEnumerable<Site> sites, IReadOnlyList<Row> table)
    {
        Site[] all = [.. sites];
        foreach (Row row in table)
        {
            int found = all.Count(site => site.Key == row.Site);
            if (found == 0)
            {
                yield return $"SearchFieldCensus.Table: the row '{row.Site}' names no text field in the shell (stale: remove it)";
            }
            else if (found > 1)
            {
                yield return $"SearchFieldCensus.Table: the row '{row.Site}' matches {found} text fields; give each its own key";
            }
        }

        foreach (IGrouping<string, Row> twice in table.GroupBy(row => row.Site).Where(group => group.Count() > 1))
        {
            yield return $"SearchFieldCensus.Table: the site '{twice.Key}' has {twice.Count()} rows";
        }
    }

    /// <summary>Each row is one of three things: a member held here (its list an AutomationId
    /// the shell declares, and at least one fact, each a fact of this test assembly that
    /// exercises the member), a member CB-35b will hold (its list, and the issue), or exempt (a
    /// one-line reason, nothing else).</summary>
    internal static IEnumerable<string> Malformed(IReadOnlyList<Row> table, Assembly tests, IReadOnlySet<string> listIds)
    {
        foreach (Row row in table)
        {
            bool exempt = row.Exempt is not null;
            bool deferred = row.Deferred is not null;
            if (exempt)
            {
                if (string.IsNullOrWhiteSpace(row.Exempt) || row.Exempt.Contains('\n', StringComparison.Ordinal))
                {
                    yield return $"SearchFieldCensus.Table: '{row.Site}' is exempt with no one-line reason";
                }

                if (row.List is not null || row.Facts is not null || deferred || row.Member is not null)
                {
                    yield return $"SearchFieldCensus.Table: '{row.Site}' is exempt and also names a list, facts, a member or an issue";
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(row.List))
            {
                yield return $"SearchFieldCensus.Table: the member '{row.Site}' names no list";
            }

            if (deferred)
            {
                if (row.Deferred != Cb35b)
                {
                    yield return $"SearchFieldCensus.Table: the member '{row.Site}' points at {row.Deferred}, not CB-35b's {Cb35b}";
                }

                if (row.Facts is not null)
                {
                    yield return $"SearchFieldCensus.Table: the member '{row.Site}' is CB-35b's and names facts; move it to the held members";
                }

                continue;
            }

            if (row.List is { } list && !listIds.Contains(list))
            {
                yield return $"SearchFieldCensus.Table: the member '{row.Site}' names the list '{list}', which no element of the shell's XAML carries as its AutomationId";
            }

            if (row.Facts is not { Length: > 0 } facts)
            {
                yield return $"SearchFieldCensus.Table: the member '{row.Site}' names no fact";
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Member))
            {
                yield return $"SearchFieldCensus.Table: the member '{row.Site}' names no member word for its facts";
                continue;
            }

            foreach (string fact in facts)
            {
                if (FactNamed(tests, fact) is not { } method)
                {
                    yield return $"SearchFieldCensus.Table: the member '{row.Site}' names '{fact}', which is no fact of the test assembly";
                }
                else if (!Exercises(method, row.Member))
                {
                    yield return $"SearchFieldCensus.Table: the member '{row.Site}' names '{fact}', which does not exercise {row.Member}: "
                        + $"neither its name nor a data row of it names {row.Member}, and its class is not {row.Member}Tests";
                }
            }
        }
    }

    private static MethodInfo? FactNamed(Assembly tests, string fact)
    {
        int dot = fact.LastIndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        string type = fact[..dot];
        string method = fact[(dot + 1)..];
        return tests.GetTypes()
            .Where(candidate => candidate.Name == type)
            .SelectMany(candidate => candidate.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .FirstOrDefault(candidate => candidate.Name == method && candidate.GetCustomAttributes<FactAttribute>(inherit: true).Any());
    }

    /// <summary>The rule a member's fact is held to: the member's word (its spaces taken out,
    /// any case) is in the fact's name, or in the first argument of one of its data rows, or
    /// the fact's class is the member's own (<c>{member}Tests</c>).</summary>
    internal static bool Exercises(MethodInfo fact, string member)
    {
        static bool Names(string? text, string member) =>
            text is not null && text.Replace(" ", string.Empty, StringComparison.Ordinal).Contains(member, StringComparison.OrdinalIgnoreCase);

        if (Names(fact.Name, member) || fact.DeclaringType?.Name == member + "Tests")
        {
            return true;
        }

        return fact.GetCustomAttributes(inherit: true)
            .Where(attribute => attribute.GetType().Name == "InlineDataAttribute")
            .Select(attribute => attribute.GetType().GetProperty("Data")?.GetValue(attribute) as object?[])
            .Any(data => data is { Length: > 0 } && Names(data[0] as string, member));
    }

    // ---- reading the XAML ---------------------------------------------------------------

    private static IEnumerable<(string File, XDocument Document)> ShellXaml()
    {
        string root = SourceText.ShellSourceRoot();
        foreach (string path in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            yield return (Path.GetRelativePath(root, path).Replace('\\', '/'), XDocument.Load(path, LoadOptions.SetLineInfo));
        }
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

    /// <summary>Every AutomationId the shell's XAML gives an element.</summary>
    private static HashSet<string> ListIds(IEnumerable<(string File, XDocument Document)> documents) =>
    [
        .. documents
            .SelectMany(document => document.Document.Descendants())
            .Select(element => Attribute(element, "AutomationProperties.AutomationId"))
            .OfType<string>(),
    ];

    /// <summary>Every URI XAML namespace the compilation's assemblies map with
    /// <c>XmlnsDefinition</c>, to the CLR namespaces it holds: WPF's presentation namespace
    /// among them, and any a referenced library or the shell itself declares.</summary>
    internal static IReadOnlyDictionary<string, string[]> XmlnsDefinitions(CSharpCompilation compilation)
    {
        var mapped = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        IEnumerable<IAssemblySymbol> assemblies = compilation.References
            .Select(compilation.GetAssemblyOrModuleSymbol)
            .OfType<IAssemblySymbol>()
            .Append(compilation.Assembly);
        foreach (AttributeData attribute in assemblies.SelectMany(assembly => assembly.GetAttributes()))
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.Windows.Markup.XmlnsDefinitionAttribute"
                && attribute.ConstructorArguments is [{ Value: string uri }, { Value: string clr }, ..])
            {
                if (!mapped.TryGetValue(uri, out HashSet<string>? spaces))
                {
                    mapped[uri] = spaces = new HashSet<string>(StringComparer.Ordinal);
                }

                _ = spaces.Add(clr);
            }
        }

        return mapped.ToDictionary(pair => pair.Key, pair => pair.Value.Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
    }

    /// <summary>Every URI XAML namespace the shell's elements are in, other than the language's
    /// own, whose elements the census resolves none of: an element there would go unseen.</summary>
    internal static IEnumerable<string> UnreadNamespaces(
        IEnumerable<(string File, XDocument Document)> documents, CSharpCompilation compilation, IReadOnlyDictionary<string, string[]> mapped)
    {
        foreach (IGrouping<string, XElement> space in documents
            .SelectMany(document => document.Document.Descendants())
            .Where(element => !element.Name.LocalName.Contains('.', StringComparison.Ordinal)
                && !element.Name.NamespaceName.StartsWith("clr-namespace:", StringComparison.Ordinal)
                && !LanguageNamespaces.Contains(element.Name.NamespaceName))
            .GroupBy(element => element.Name.NamespaceName))
        {
            if (!space.Any(element => XamlType(element, compilation, mapped) is not null))
            {
                yield return $"the census resolves no element of the XAML namespace '{space.Key}' ({space.Count()} elements): "
                    + "a text field there would go unseen; map it";
            }
        }
    }

    /// <summary>The text fields <paramref name="document"/> declares: each element whose type,
    /// bound in <paramref name="compilation"/>, is a <c>TextBoxBase</c> or a
    /// <c>DataGridTextColumn</c> with WPF's own editing box, each whose template hosts one as
    /// a part, and each style setter that can make a combo box editable.</summary>
    internal static IEnumerable<Site> XamlSites(
        string file, XDocument document, CSharpCompilation compilation, IReadOnlyDictionary<string, string[]> mapped)
    {
        Types types = Types.Of(compilation);
        foreach (XElement element in document.Descendants())
        {
            if (element.Name.LocalName == "Setter"
                && Attribute(element, "Property") is { } property
                && (property == "IsEditable" || property.EndsWith(".IsEditable", StringComparison.Ordinal))
                && !IsFalse(Attribute(element, "Value")))
            {
                yield return new Site(file, LineOf(element), XamlKey(file, element));
                continue;
            }

            if (XamlType(element, compilation, mapped) is not { } type)
            {
                continue;
            }

            if (types.IsTextField(type)
                || types.HostsAPart(type, Attribute(element, "IsEditable") is { } editable ? !IsFalse(editable) : false))
            {
                yield return new Site(file, LineOf(element), XamlKey(file, element));
            }
        }
    }

    private static bool IsFalse(string? value) => string.Equals(value?.Trim(), "False", StringComparison.OrdinalIgnoreCase);

    /// <summary>An element's type: a <c>clr-namespace:</c> mapping's, or a URI namespace's
    /// through the assemblies' <c>XmlnsDefinition</c>s. Property elements and the XAML
    /// language's own elements have none.</summary>
    private static INamedTypeSymbol? XamlType(XElement element, CSharpCompilation compilation, IReadOnlyDictionary<string, string[]> mapped)
    {
        string local = element.Name.LocalName;
        if (local.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        string space = element.Name.NamespaceName;
        const string ClrNamespace = "clr-namespace:";
        if (space.StartsWith(ClrNamespace, StringComparison.Ordinal))
        {
            return compilation.GetTypeByMetadataName($"{space[ClrNamespace.Length..].Split(';')[0]}.{local}");
        }

        return mapped.TryGetValue(space, out string[]? spaces)
            ? spaces.Select(candidate => compilation.GetTypeByMetadataName($"{candidate}.{local}")).FirstOrDefault(type => type is not null)
            : null;
    }

    /// <summary>The AutomationId, else the x:Name, else the file, the nearest holder with a
    /// key, a name or an AutomationId, and the element's place among that holder's elements of
    /// its type.</summary>
    private static string XamlKey(string file, XElement element)
    {
        if (Attribute(element, "AutomationProperties.AutomationId") is { Length: > 0 } id)
        {
            return id;
        }

        if (element.Attribute(XName.Get("Name", Xaml))?.Value is { Length: > 0 } name)
        {
            return name;
        }

        XElement? holder = element.Ancestors().FirstOrDefault(ancestor => Holder(ancestor) is not null);
        int ordinal = holder is null
            ? 0
            : holder.Descendants(element.Name).TakeWhile(candidate => !ReferenceEquals(candidate, element)).Count();
        return $"{Path.GetFileName(file)}#{(holder is null ? "the root" : Holder(holder))}/{element.Name.LocalName}#{ordinal}";
    }

    private static string? Holder(XElement element) =>
        element.Attribute(XName.Get("Key", Xaml))?.Value
            ?? element.Attribute(XName.Get("Name", Xaml))?.Value
            ?? Attribute(element, "AutomationProperties.AutomationId");

    private static string? Attribute(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static int LineOf(XObject node) => ((IXmlLineInfo)node).LineNumber;

    // ---- reading the code ---------------------------------------------------------------

    /// <summary>The text fields the shell's code builds: each object creation of a
    /// <c>TextBoxBase</c> type, of a <c>DataGridTextColumn</c> with WPF's own editing box, or
    /// of a control whose template hosts one as a part; each template factory made for one;
    /// and each place the code sets or binds a combo box's <c>IsEditable</c> to anything but
    /// false.</summary>
    internal static IEnumerable<Site> CodeSites(CSharpCompilation compilation, IEnumerable<(string Relative, SyntaxTree Tree)> trees)
    {
        Types types = Types.Of(compilation);
        INamedTypeSymbol factory = compilation.GetTypeByMetadataName("System.Windows.FrameworkElementFactory")
            ?? throw new Xunit.Sdk.XunitException("FrameworkElementFactory did not bind.");
        foreach ((string relative, SyntaxTree tree) in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            string file = relative.Replace('\\', '/');
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                switch (node)
                {
                    case BaseObjectCreationExpressionSyntax creation when model.GetTypeInfo(creation).Type is INamedTypeSymbol created:
                        if (types.IsTextField(created) || types.HostsAPart(created, EditableIn(creation)))
                        {
                            yield return new Site(file, line, $"{file}#{NameGiven(creation, line)}");
                        }
                        else if (SymbolEqualityComparer.Default.Equals(created, factory)
                            && creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax made
                            && model.GetTypeInfo(made.Type).Type is INamedTypeSymbol type
                            && (types.IsTextField(type) || types.HostsAPart(type, editable: true)))
                        {
                            yield return new Site(file, line, $"{file}#factory@{line}");
                        }

                        break;
                    case AssignmentExpressionSyntax assignment
                        when model.GetOperation(assignment) is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: "IsEditable" } target } set
                            && types.IsComboBox(target.Property.ContainingType)
                            && set.Value.ConstantValue is not { HasValue: true, Value: false }
                            && assignment.Parent is not InitializerExpressionSyntax:
                        yield return new Site(file, line, $"{file}#IsEditable@{line}");
                        break;
                    case IdentifierNameSyntax { Identifier.ValueText: "IsEditableProperty" } identifier
                        when model.GetSymbolInfo(identifier).Symbol is IFieldSymbol field && types.IsComboBox(field.ContainingType):
                        yield return new Site(file, line, $"{file}#IsEditableProperty@{line}");
                        break;
                }
            }
        }
    }

    /// <summary>Whether an object initializer sets IsEditable to anything but false.</summary>
    private static bool EditableIn(BaseObjectCreationExpressionSyntax creation) =>
        creation.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "IsEditable" }
                && !assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression)) == true;

    /// <summary>The name a code-built field is given: the variable it initialises, or the
    /// field or property it is assigned to.</summary>
    private static string NameGiven(SyntaxNode creation, int line) => creation.Parent switch
    {
        EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.Text,
        AssignmentExpressionSyntax { Left: IdentifierNameSyntax name } => name.Identifier.Text,
        AssignmentExpressionSyntax { Left: MemberAccessExpressionSyntax member } => member.Name.Identifier.Text,
        _ => "new@" + line,
    };

    // ---- types --------------------------------------------------------------------------

    /// <summary>The WPF types the census reads sites by, bound in one compilation.</summary>
    private sealed record Types(INamedTypeSymbol TextBoxBase, INamedTypeSymbol TextColumn, INamedTypeSymbol ComboBox)
    {
        internal static Types Of(CSharpCompilation compilation) => new(
            Bind(compilation, "System.Windows.Controls.Primitives.TextBoxBase"),
            Bind(compilation, "System.Windows.Controls.DataGridTextColumn"),
            Bind(compilation, "System.Windows.Controls.ComboBox"));

        private static INamedTypeSymbol Bind(CSharpCompilation compilation, string name) =>
            compilation.GetTypeByMetadataName(name) ?? throw new Xunit.Sdk.XunitException($"{name} did not bind.");

        /// <summary>A text field itself: a <c>TextBoxBase</c>, or a grid text column whose
        /// editing box is WPF's own TextBox. A column type that builds its own box in a
        /// <c>GenerateEditingElement</c> of the shell's source is not: the box it builds is
        /// the site, where the code creates it.</summary>
        internal bool IsTextField(INamedTypeSymbol type)
        {
            if (DerivesFrom(type, TextBoxBase))
            {
                return true;
            }

            if (!DerivesFrom(type, TextColumn))
            {
                return false;
            }

            for (INamedTypeSymbol? at = type; at is not null && !SymbolEqualityComparer.Default.Equals(at, TextColumn); at = at.BaseType)
            {
                if (at.GetMembers("GenerateEditingElement").OfType<IMethodSymbol>().Any(method => method.IsOverride && method.Locations.Any(location => location.IsInSource)))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool IsComboBox(ITypeSymbol? type) => type is not null && DerivesFrom(type, ComboBox);

        /// <summary>Whether the type's template hosts a text field as a part: a
        /// <c>TemplatePart</c> declared on it or a base whose type is a <c>TextBoxBase</c>. A
        /// combo box's editable text part lives only while it is editable.</summary>
        internal bool HostsAPart(INamedTypeSymbol type, bool editable)
        {
            bool hosts = false;
            for (INamedTypeSymbol? at = type; at is not null; at = at.BaseType)
            {
                hosts |= at.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.Name == "TemplatePartAttribute"
                    && attribute.NamedArguments.Any(argument => argument.Key == "Type"
                        && argument.Value.Value is INamedTypeSymbol part
                        && DerivesFrom(part, TextBoxBase)));
            }

            return hosts && (!IsComboBox(type) || editable);
        }

        private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol baseType)
        {
            for (ITypeSymbol? at = type; at is not null; at = at.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(at, baseType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
