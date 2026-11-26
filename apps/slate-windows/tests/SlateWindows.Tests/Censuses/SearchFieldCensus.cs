// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-35 (#1837): every text field in the shell is classified against the class the
// palette's DS-10 flush closed. A member is a field whose text filters or selects a list, or a
// field that holds the keys while that same list republishes in the background: TSF can be
// granted its lock between dispatcher operations, and a list publication that leaves layout
// pending then has its layout and automation events run inside the lock. A member names its
// list and the facts that hold its publications to the palette's flush, or the issue that will
// (CB-35b, #2070, undated, so a slipped CB-35b does not turn main red). Every other field is
// exempt, with its reason. The census is keyed by type, not by tag or name: every element of a
// TextBoxBase type in the shell's XAML, every one built in its code, and every control whose
// template hosts one as a part (a DatePicker's DatePickerTextBox, an editable ComboBox's
// PART_EditableTextBox). It reads sources only, binding them through ShellCompilation.

using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "search-fields")]
public sealed class SearchFieldCensus
{
    private const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>CB-35b's issue: the grids and the canvas tree.</summary>
    private const string Cb35b = "#2070";

    private const string Publication = nameof(SearchFieldPublicationTests);

    /// <summary>One text field: its site key, and either the list it filters or selects with
    /// the facts that hold that list's publications (or the issue that will), or the reason it
    /// is no member.</summary>
    internal sealed record Row(string Site, string? List = null, string[]? Facts = null, string? Deferred = null, string? Exempt = null);

    internal static readonly Row[] Table =
    [
        // The members this PR holds, and the palette they are held to.
        new("CommandPaletteSearch", "CommandPaletteResults",
        [
            $"{nameof(CommandPaletteTests)}.{nameof(CommandPaletteTests.TheRowsAreLaidOutInTheOperationThatPublishesThem)}",
            $"{nameof(CommandPaletteTests)}.{nameof(CommandPaletteTests.ALetterTypedInTheListMovesNothing)}",
        ]),
        new("QuickSwitcherSearch", "QuickSwitcherResults",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenShowsTheRowTheArrowsSelect)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.QuickOpenKeepsAReRankedSelectionInView)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
        ]),
        new("SearchOverlaySearch", "SearchOverlayResults",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchShowsTheRowTheArrowsSelect)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.SearchsFirstResultsLeaveNoScrollDeferred)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
        ]),
        new("SidebarFilter", "SidebarFilterResults",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.FilesFilterLeavesNothingPendingWhenItPublishes)}",
        ]),
        new("MoveToFilter", "MoveToList",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToLeavesNothingPendingWhenItPublishes)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToShowsTheRowTheArrowsSelect)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.MoveToKeepsTheViewWhileFoldersLoad)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
        ]),
        new("CanvasCardPickerFilter", "CanvasCardPickerRows",
        [
            $"{Publication}.{nameof(SearchFieldPublicationTests.ThePickersLeaveNothingPendingWhenTheyPublish)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.APickerKeepsASelectionWhenItsFilterDropsTheRow)}",
            $"{Publication}.{nameof(SearchFieldPublicationTests.ALetterOnASearchListMovesNothing)}",
        ]),

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
        new("Grids/AccessibleDataGrid.cs#editor", Exempt: "one cell's editor; it edits that cell and filters nothing"),
        new("Panels/HistorySurfaceView.cs#box", Exempt: "a restore's destination path; it filters nothing"),
    ];

    /// <summary>One place the shell makes a text field.</summary>
    internal sealed record Site(string File, int Line, string Key);

    [Fact]
    public void EverySearchFieldIsClassified()
    {
        CSharpCompilation compilation = ShellCompilation.Compilation;
        List<Site> xaml = [.. ShellXaml().SelectMany(document => XamlSites(document.File, document.Document, compilation))];
        List<Site> code = [.. CodeSites(compilation, ShellCompilation.Sources.Select(source => (source.Relative, source.Source.Root.SyntaxTree)))];
        Assert.True(xaml.Count >= 30, $"only {xaml.Count} text fields were found in the shell's XAML; the scrape is broken.");
        Assert.True(code.Count >= 10, $"only {code.Count} text fields were found in the shell's code; the scrape is broken.");

        var problems = new List<string>();
        problems.AddRange(Unlisted([.. xaml, .. code], Table));
        problems.AddRange(Stale([.. xaml, .. code], Table));
        problems.AddRange(Malformed(Table, typeof(SearchFieldCensus).Assembly, ListIds()));

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
    /// the shell declares, and at least one fact, each a fact of this test assembly), a member
    /// CB-35b will hold (its list, and the issue), or exempt (a one-line reason, nothing else).</summary>
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

                if (row.List is not null || row.Facts is not null || deferred)
                {
                    yield return $"SearchFieldCensus.Table: '{row.Site}' is exempt and also names a list, facts or an issue";
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

            foreach (string fact in facts)
            {
                if (!IsFact(tests, fact))
                {
                    yield return $"SearchFieldCensus.Table: the member '{row.Site}' names '{fact}', which is no fact of the test assembly";
                }
            }
        }
    }

    private static bool IsFact(Assembly tests, string fact)
    {
        int dot = fact.LastIndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        string type = fact[..dot];
        string method = fact[(dot + 1)..];
        return tests.GetTypes()
            .Where(candidate => candidate.Name == type)
            .SelectMany(candidate => candidate.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Any(candidate => candidate.Name == method && candidate.GetCustomAttributes<FactAttribute>(inherit: true).Any());
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
    private static HashSet<string> ListIds() =>
    [
        .. ShellXaml()
            .SelectMany(document => document.Document.Descendants())
            .Select(element => Attribute(element, "AutomationProperties.AutomationId"))
            .OfType<string>(),
    ];

    /// <summary>The text fields <paramref name="document"/> declares: each element whose type,
    /// bound in <paramref name="compilation"/>, is a <c>TextBoxBase</c>, and each whose
    /// template hosts one as a part.</summary>
    internal static IEnumerable<Site> XamlSites(string file, XDocument document, CSharpCompilation compilation)
    {
        INamedTypeSymbol textBoxBase = compilation.GetTypeByMetadataName("System.Windows.Controls.Primitives.TextBoxBase")
            ?? throw new Xunit.Sdk.XunitException("TextBoxBase did not bind.");
        foreach (XElement element in document.Descendants())
        {
            if (XamlType(element, compilation) is not { } type)
            {
                continue;
            }

            if (DerivesFrom(type, textBoxBase) || HostsAPart(type, textBoxBase, Attribute(element, "IsEditable")))
            {
                yield return new Site(file, LineOf(element), XamlKey(file, element));
            }
        }
    }

    private static readonly string[] PresentationNamespaces =
    [
        "System.Windows.Controls",
        "System.Windows.Controls.Primitives",
        "System.Windows.Documents",
        "System.Windows",
    ];

    /// <summary>An element's type: the presentation namespace's WPF types, or a
    /// <c>clr-namespace:</c> mapping's. Property elements and the XAML language's own
    /// elements have none.</summary>
    private static INamedTypeSymbol? XamlType(XElement element, CSharpCompilation compilation)
    {
        string local = element.Name.LocalName;
        if (local.Contains('.', StringComparison.Ordinal))
        {
            return null;
        }

        string space = element.Name.NamespaceName;
        if (space == Presentation)
        {
            return PresentationNamespaces
                .Select(candidate => compilation.GetTypeByMetadataName($"{candidate}.{local}"))
                .FirstOrDefault(type => type is not null);
        }

        const string ClrNamespace = "clr-namespace:";
        return space.StartsWith(ClrNamespace, StringComparison.Ordinal)
            ? compilation.GetTypeByMetadataName($"{space[ClrNamespace.Length..].Split(';')[0]}.{local}")
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
    /// <c>TextBoxBase</c> type or of a control whose template hosts one as a part, and each
    /// template factory made for one.</summary>
    internal static IEnumerable<Site> CodeSites(CSharpCompilation compilation, IEnumerable<(string Relative, SyntaxTree Tree)> trees)
    {
        INamedTypeSymbol textBoxBase = compilation.GetTypeByMetadataName("System.Windows.Controls.Primitives.TextBoxBase")
            ?? throw new Xunit.Sdk.XunitException("TextBoxBase did not bind.");
        INamedTypeSymbol factory = compilation.GetTypeByMetadataName("System.Windows.FrameworkElementFactory")
            ?? throw new Xunit.Sdk.XunitException("FrameworkElementFactory did not bind.");
        foreach ((string relative, SyntaxTree tree) in trees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            string file = relative.Replace('\\', '/');
            foreach (BaseObjectCreationExpressionSyntax creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol created)
                {
                    continue;
                }

                int line = creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                if (DerivesFrom(created, textBoxBase) || HostsAPart(created, textBoxBase, EditableIn(creation)))
                {
                    yield return new Site(file, line, $"{file}#{NameGiven(creation, line)}");
                }
                else if (SymbolEqualityComparer.Default.Equals(created, factory)
                    && creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax made
                    && model.GetTypeInfo(made.Type).Type is INamedTypeSymbol type
                    && (DerivesFrom(type, textBoxBase) || HostsAPart(type, textBoxBase, "True")))
                {
                    yield return new Site(file, line, $"{file}#factory@{line}");
                }
            }
        }
    }

    /// <summary>"True" when an object initializer sets IsEditable to true.</summary>
    private static string? EditableIn(BaseObjectCreationExpressionSyntax creation) =>
        creation.Initializer?.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "IsEditable" }
                && assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)) == true
            ? "True"
            : null;

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

    /// <summary>Whether the type's template hosts a text field as a part: a
    /// <c>TemplatePart</c> declared on it or a base whose type is a <c>TextBoxBase</c>. A
    /// combo box's editable text part lives only while it is editable.</summary>
    private static bool HostsAPart(INamedTypeSymbol type, INamedTypeSymbol textBoxBase, string? isEditable)
    {
        bool combo = false;
        bool hosts = false;
        for (INamedTypeSymbol? at = type; at is not null; at = at.BaseType)
        {
            combo |= at.ToDisplayString() == "System.Windows.Controls.ComboBox";
            hosts |= at.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "TemplatePartAttribute"
                && attribute.NamedArguments.Any(argument => argument.Key == "Type"
                    && argument.Value.Value is INamedTypeSymbol part
                    && DerivesFrom(part, textBoxBase)));
        }

        return hosts && (!combo || string.Equals(isEditable, "True", StringComparison.OrdinalIgnoreCase));
    }
}
