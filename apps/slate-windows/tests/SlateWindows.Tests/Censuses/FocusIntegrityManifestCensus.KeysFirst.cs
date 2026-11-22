// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-17a (#1866; CD11 as the owner answered it on 2026-11-16: every
// self-disabling button moves the keys first, whatever the NVDA session hears).
// A control turned off while it holds the keys says "unavailable" in NVDA, and
// the keys fall to whatever is behind it. This part of the census:
//
//   - reads every Command= on an element that can take the keys itself
//     (xaml-command), and every C# write of a button's or a hyperlink's command
//     (command), an initializer's and a constructor's included, since the
//     command lives on with the control. A menu row is not one: WPF closes its
//     menu and gives the keys back before the row's command runs
//     (MenuRowCommandTests), unless it stays open on click;
//   - keys each IsEnabled trigger on its own, by its condition, and a style's
//     own IsEnabled setter that sets False or binds;
//   - holds every site that can turn a control off (an IsEnabled trigger,
//     binding or write, and every command site) to one of six witnesses:
//       keys-first: Class.Method   a fact whose code names the site (and, for
//                                  a trigger, its condition's token; for an
//                                  unnamed button, its command) and whose own
//                                  code calls KeysFirstRecorder.AssertTheKeysLeaveFirst
//       stays-on: Class.Method     the same, for a control that used to go off
//                                  under the keys and now stays on: its own code
//                                  calls KeysFirstRecorder.AssertItStaysOn with
//                                  what, root, act and reached (CB-17b)
//       exempt: not focusable      checked against the XAML
//       exempt: always on: T.P     checked by reading every construction of
//                                  every property of that name in the shell
//       exempt: its own press never turns it off: <why>
//       exempt: never fires: <why> (Class.Method)
//     guard: and a fact that only names the site no longer witness such a site.
//   - refuses a keys-first or stays-on witness whose fact can skip at run time
//     (FocusIntegrityManifestCensus.Skips.cs, CB-17a's merge judge, m1), except
//     on the menu's seventeen rows (WhereADesktopGivesAMenuTheKeys), each written
//     'keys-first, where a desktop gives a menu the keys (#1680): Class.Method';
//   - holds every row CB-17b ended (EndedByCb17b) to a measured or checked
//     witness: keys-first, stays-on, always on or not focusable, never a
//     free-text exemption (CB-17a's merge judge, m3), and keeps every key it
//     deleted with its trigger (GoneWithCb17b) gone.
//
// The press class's limit: a control bound to a RelayCommand, an
// AsyncRelayCommand or a ReasonedCommand has its keys moved first whoever
// turns it off (CommandKeysFirst, CB-17b). For an IsEnabled trigger, binding
// or write, and a button that runs a Click handler, another driver can still
// turn such a control off under the keys (a refresh's or a rescan's deselect,
// an in-place change of item, a find bar's mode switch). This census does not
// judge other drivers (#1955).

using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

public sealed partial class FocusIntegrityManifestCensus
{
    /// <summary>
    /// Plan 47 CB-17b (CB-17a's merge judge, m3): the 47 rows CB-17a dated on 2026-11-21,
    /// which CB-17b ended, frozen. Each is the key now, mapped to the key it was dated under
    /// (the Queries groups' keys changed with their condition, which now waits for the
    /// republish). Such a row takes only a measured or checked witness
    /// (<see cref="EndedWitnesses"/>), never a free-text exemption. Nothing is ever removed:
    /// a key that moves is rewritten here in the same PR, and a key deleted with its trigger
    /// moves to <see cref="GoneWithCb17b"/> with why.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> EndedByCb17b = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when FileSidebar.MoveToSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when FileSidebar.MoveToSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when SettingsSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when SettingsSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BaseQueryBuilderSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BaseQueryBuilderSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BulkRenameSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BulkRenameSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardEditorSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardEditorSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardPickerSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardPickerSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasPromptSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasPromptSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationDetails=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationDetails=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationSummary=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationSummary=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.DashboardEditorSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.DashboardEditorSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.FilesCiting=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.FilesCiting=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplateFlowSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplateFlowSheet=True",
        ["xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplatePickerSheet=True"] = "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplatePickerSheet=True",
        ["ui-state SlateWindows.Bases.BaseSurfaceView.RenderChrome _quickFilter IsEnabled"] = "ui-state SlateWindows.Bases.BaseSurfaceView.RenderChrome _quickFilter IsEnabled",
        ["ui-state SlateWindows.Bases.BaseSurfaceView.RenderChrome _viewPicker IsEnabled"] = "ui-state SlateWindows.Bases.BaseSurfaceView.RenderChrome _viewPicker IsEnabled",
        ["ui-state SlateWindows.Graph.ConnectionsLeafView.RenderCore _depth IsEnabled"] = "ui-state SlateWindows.Graph.ConnectionsLeafView.RenderCore _depth IsEnabled",
        ["ui-state SlateWindows.Graph.GraphInspectorView.RenderGate section IsEnabled"] = "ui-state SlateWindows.Graph.GraphInspectorView.RenderGate section IsEnabled",
        ["xaml-binding MainWindow.xaml BulkRenameApplyButton IsEnabled"] = "xaml-binding MainWindow.xaml BulkRenameApplyButton IsEnabled",
        ["xaml-binding MainWindow.xaml BulkRenamePreviewButton IsEnabled"] = "xaml-binding MainWindow.xaml BulkRenamePreviewButton IsEnabled",
        ["xaml-binding MainWindow.xaml CitationSummaryWalkButton IsEnabled"] = "xaml-binding MainWindow.xaml CitationSummaryWalkButton IsEnabled",
        ["xaml-command MainWindow.xaml CancelTrash CancelTrashCommand"] = "xaml-command MainWindow.xaml CancelTrash CancelTrashCommand",
        ["xaml-command MainWindow.xaml CanvasInspectionClose CloseCanvasInspectionCommand"] = "xaml-command MainWindow.xaml CanvasInspectionClose CloseCanvasInspectionCommand",
        ["xaml-command MainWindow.xaml CanvasPromptClearMarksButton CanvasPromptClearMarksCommand"] = "xaml-command MainWindow.xaml CanvasPromptClearMarksButton CanvasPromptClearMarksCommand",
        ["xaml-command MainWindow.xaml MoveToCancelButton CancelCommand"] = "xaml-command MainWindow.xaml MoveToCancelButton CancelCommand",
        ["xaml-command MainWindow.xaml MoveToRetry RetryCommand"] = "xaml-command MainWindow.xaml MoveToRetry RetryCommand",
        ["xaml-command MainWindow.xaml QuickSwitcherClose DismissCommand"] = "xaml-command MainWindow.xaml QuickSwitcherClose DismissCommand",
        ["xaml-command MainWindow.xaml SidebarBatchActions BatchTrashCommand"] = "xaml-command MainWindow.xaml SidebarBatchActions BatchTrashCommand",
        ["xaml-command MainWindow.xaml SidebarBatchActions MoveToCommand"] = "xaml-command MainWindow.xaml SidebarBatchActions MoveToCommand",
        ["xaml-command MainWindow.xaml SidebarExpandLoaded ExpandLoadedCommand"] = "xaml-command MainWindow.xaml SidebarExpandLoaded ExpandLoadedCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander CancelImportCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander CancelImportCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander CreateFolderNoteCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander CreateFolderNoteCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteFolderNoteCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander DeleteFolderNoteCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander ImportCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander ImportCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander MoveToCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander MoveToCommand",
        ["xaml-command MainWindow.xaml SidebarFileActionsExpander RenameCommand"] = "xaml-command MainWindow.xaml SidebarFileActionsExpander RenameCommand",
        ["xaml-command MainWindow.xaml SidebarSettingsRetry RetrySettingsCommand"] = "xaml-command MainWindow.xaml SidebarSettingsRetry RetrySettingsCommand",
        ["xaml-command MainWindow.xaml SidebarShortcutsActions RemoveShortcutCommand"] = "xaml-command MainWindow.xaml SidebarShortcutsActions RemoveShortcutCommand",
        ["xaml-command WorkspaceTemplates.xaml EditorPopoverClose ClosePopoverCommand"] = "xaml-command WorkspaceTemplates.xaml EditorPopoverClose ClosePopoverCommand",
        ["xaml-command WorkspaceTemplates.xaml EditorPopoverOpenSource OpenPopoverSourceCommand"] = "xaml-command WorkspaceTemplates.xaml EditorPopoverOpenSource OpenPopoverSourceCommand",
        ["xaml-trigger MainWindow.xaml WrapPanel(QueriesOpenBaseFile) IsEnabled when QueriesBaseFilesList.SelectedItem={x:Null} & IsQueriesRepublishing=False"] =
            "xaml-trigger MainWindow.xaml WrapPanel(QueriesOpenBaseFile) IsEnabled when QueriesBaseFilesList.SelectedItem={x:Null}",
        ["xaml-trigger MainWindow.xaml WrapPanel(QueriesOpenDashboard) IsEnabled when QueriesDashboardsList.SelectedItem={x:Null} & IsQueriesRepublishing=False"] =
            "xaml-trigger MainWindow.xaml WrapPanel(QueriesOpenDashboard) IsEnabled when QueriesDashboardsList.SelectedItem={x:Null}",
        ["xaml-trigger MainWindow.xaml WrapPanel(QueriesRun) IsEnabled when QueriesSavedList.SelectedItem={x:Null} & IsQueriesRepublishing=False"] =
            "xaml-trigger MainWindow.xaml WrapPanel(QueriesRun) IsEnabled when QueriesSavedList.SelectedItem={x:Null}",
        ["xaml-trigger WorkspaceTemplates.xaml PropertyRowTemplate CommitCommand IsEnabled when (style default)"] =
            "xaml-trigger WorkspaceTemplates.xaml PropertyRowTemplate CommitCommand IsEnabled when (style default)",
        ["xaml-trigger WorkspaceTemplates.xaml PropertyRowTemplate CommitCommand IsEnabled when WriteInFlight=True"] =
            "xaml-trigger WorkspaceTemplates.xaml PropertyRowTemplate CommitCommand IsEnabled when WriteInFlight=True",
    };

    /// <summary>Plan 47 CB-17b (red team F10): the keys CB-17b deleted with their trigger,
    /// each with why it is gone. A key here fails the census if the scrape finds it again or
    /// the manifest lists it.</summary>
    internal static readonly IReadOnlyDictionary<string, string> GoneWithCb17b = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["xaml-trigger WorkspaceTemplates.xaml Style:Slate.LauncherRowStyle IsEnabled when IsModalSheetOpen=True"] =
            "its trigger is deleted with IsModalSheetOpen (CB-17b D10): a launcher row goes off under a sheet only by the "
            + "modal-surface trigger, once the keys have left it",
    };

    /// <summary>Plan 47 CB-17b (CB-17a's merge judge, m1): how a row is written whose
    /// keys-first fact can skip at run time, where a desktop refuses a menu the keys (#1680).
    /// Only the rows in <see cref="WhereADesktopGivesAMenuTheKeys"/> are written so, and each
    /// of them is.</summary>
    internal const string MenuMarked = "keys-first, where a desktop gives a menu the keys (#1680): ";

    /// <summary>The menu's seventeen rows: the menu turned off under each overlay and each
    /// sheet. Their facts take the keys into the menu, which a desktop with no foreground
    /// window refuses, so they skip there (#1680); every other keys-first and stays-on
    /// witness runs on every lane.</summary>
    internal static readonly string[] WhereADesktopGivesAMenuTheKeys =
    [
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when FileSidebar.MoveToSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Palette.IsOpen=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when QuickSwitcher.IsOpen=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Search.IsOpen=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when SettingsSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BaseQueryBuilderSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.BulkRenameSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardEditorSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasCardPickerSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CanvasPromptSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationDetails=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.CitationSummary=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.DashboardEditorSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.FilesCiting=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplateFlowSheet=True",
        "xaml-trigger MainWindow.xaml MainMenu IsEnabled when Workspace.TemplatePickerSheet=True",
    ];

    /// <summary>The witnesses a row in <see cref="EndedByCb17b"/> may take: each is measured
    /// by a fact or checked by the census against the code.</summary>
    internal static readonly string[] EndedWitnesses = ["keys-first: ", MenuMarked, "stays-on: ", "exempt: always on: ", "exempt: not focusable"];

    /// <summary>A row CB-17b ended whose key is gone from the manifest, or whose witness is
    /// not measured or checked; and a key CB-17b deleted that the scrape finds again or the
    /// manifest lists (red team F10).</summary>
    internal static string[] EndedProblems(
        IReadOnlyDictionary<string, string> manifest,
        IReadOnlyDictionary<string, string> ended,
        IReadOnlyDictionary<string, string>? gone = null,
        IReadOnlyCollection<string>? scraped = null) =>
    [
        .. ended.Where(entry => !manifest.ContainsKey(entry.Key))
            .Select(entry => $"{entry.Key} is gone from the manifest (dated as {entry.Value}): a key that moved is rewritten in EndedByCb17b in the same PR, "
                + "and a key deleted with its trigger moves to GoneWithCb17b with why"),
        .. ended.Where(entry => manifest.TryGetValue(entry.Key, out string? witness)
                && !EndedWitnesses.Any(kind => witness.StartsWith(kind, StringComparison.Ordinal)))
            .Select(entry => $"{entry.Key} → '{manifest[entry.Key]}': a row CB-17b ended (EndedByCb17b) takes only "
                + $"'keys-first: Class.Method', '{MenuMarked}Class.Method', 'stays-on: Class.Method', 'exempt: always on: Type.Property' or 'exempt: not focusable'"),
        .. (gone ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Where(entry => manifest.ContainsKey(entry.Key) || scraped?.Contains(entry.Key, StringComparer.Ordinal) == true)
            .Select(entry => $"{entry.Key} is back, and CB-17b deleted it ({entry.Value}): a gone key is never scraped or witnessed again (GoneWithCb17b)"),
    ];

    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly string[] Untokened = ["IsOpen", "IsKeyboardFocusWithin", "SelectedItem"];

    /// <summary>A site that can turn a control off: an IsEnabled trigger,
    /// binding or write, and every command site.</summary>
    internal static bool IsDisableSite(string key) =>
        key.StartsWith("xaml-command ", StringComparison.Ordinal)
        || key.StartsWith("command ", StringComparison.Ordinal)
        || ((key.StartsWith("xaml-trigger ", StringComparison.Ordinal)
                || key.StartsWith("xaml-binding ", StringComparison.Ordinal)
                || key.StartsWith("ui-state ", StringComparison.Ordinal))
            && (key.EndsWith(" IsEnabled", StringComparison.Ordinal) || key.Contains(" IsEnabled when ", StringComparison.Ordinal)));

    /// <summary>The syntaxes the census does not read that it lets stand, each with why
    /// it cannot turn a control off under the keys (CB-17a review, m6).</summary>
    internal static readonly IReadOnlyDictionary<string, string> UnreadAllowed = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["code-setter SlateWindows.Bases.BaseSurfaceView.BuildListItemStyle IsEnabled=false when IsHeader=true"] =
            "a base list's group header row is off by IsHeader, which is fixed for the item it shows (BaseSurfaceView.cs:1346-1352, "
            + ":1428), so the trigger never turns a row off under the keys",
    };

    /// <summary>Whether xunit never runs what <paramref name="attribute"/> marks: skipped,
    /// explicit (run only on request), skipped by a condition or an exception, or a theory
    /// that skips when it has no data (TestEvidence.NeverRuns reads the same arguments from
    /// source).</summary>
    internal static bool NeverRuns(FactAttribute attribute) =>
        attribute.Skip is not null
        || attribute.Explicit
        || attribute.SkipUnless is not null
        || attribute.SkipWhen is not null
        || attribute.SkipType is not null
        || attribute.SkipExceptions is { Length: > 0 }
        || attribute is TheoryAttribute { SkipTestWithoutData: true };

    /// <summary>Each declared fact's source: the one method of its name that carries a
    /// Fact or Theory attribute xunit runs, never a helper of the same name.</summary>
    internal static Dictionary<string, string> FactSources(IEnumerable<CompilationUnitSyntax> roots, IReadOnlySet<string> declared)
    {
        var facts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (MethodDeclarationSyntax method in roots.SelectMany(root => root.DescendantNodes().OfType<MethodDeclarationSyntax>()))
        {
            if (method.Parent is TypeDeclarationSyntax type
                && declared.Contains($"{type.Identifier.ValueText}.{method.Identifier.ValueText}")
                && method.AttributeLists.SelectMany(list => list.Attributes)
                    .Where(attribute => LastName(attribute.Name) is "Fact" or "FactAttribute" or "Theory" or "TheoryAttribute")
                    .ToArray() is [var fact]
                && !NeverRunsBySource(fact))
            {
                facts[$"{type.Identifier.ValueText}.{method.Identifier.ValueText}"] = method.ToFullString();
            }
        }

        return facts;
    }

    private static string LastName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => name.ToString(),
    };

    private static bool NeverRunsBySource(AttributeSyntax attribute) =>
        attribute.ArgumentList?.Arguments.Any(argument => argument.NameEquals?.Name.Identifier.ValueText switch
        {
            "Skip" or "SkipUnless" or "SkipWhen" or "SkipType" or "SkipExceptions" => !argument.Expression.IsKind(SyntaxKind.NullLiteralExpression),
            "Explicit" or "SkipTestWithoutData" => !argument.Expression.IsKind(SyntaxKind.FalseLiteralExpression),
            _ => false,
        }) == true;

    /// <summary>What fails the main fact: an unlisted site, a stale row, a witness that does
    /// not hold, a row CB-17b ended or deleted out of its place, and a syntax the census does
    /// not read. An unlisted site and a stale row say how a renamed key keeps its
    /// witness.</summary>
    internal static string[] ManifestProblems(
        SortedDictionary<string, Site> scraped,
        IReadOnlyDictionary<string, string> manifest,
        WitnessJudge judge,
        IEnumerable<string> unread,
        IReadOnlyDictionary<string, string>? allowed = null,
        IReadOnlyDictionary<string, string>? ended = null,
        IReadOnlyDictionary<string, string>? gone = null)
    {
        const string Renamed = "A site an edit elsewhere renamed keeps its witness under its new key: rewrite with "
            + "SLATE_FOCUS_MANIFEST_UPDATE=1 and move the witness; a row CB-17b ended is rewritten in EndedByCb17b in the same PR.";
        allowed ??= UnreadAllowed;
        string[] read = [.. unread];
        string[] unlisted = [.. scraped.Keys.Where(site => !manifest.ContainsKey(site))];
        string[] stale = [.. manifest.Keys.Where(site => !scraped.ContainsKey(site))];
        string[] unwitnessed =
        [
            .. manifest
                .Where(entry => scraped.ContainsKey(entry.Key))
                .Select(entry => (entry.Key, Verdict: judge.Judge(scraped[entry.Key], entry.Value)))
                .Where(judged => judged.Verdict is not null)
                .Select(judged => $"{judged.Key} → {judged.Verdict}"),
        ];
        string[] endedRows = EndedProblems(manifest, ended ?? EndedByCb17b, gone ?? GoneWithCb17b, scraped.Keys);
        string[] unknown = [.. read.Where(syntax => !allowed.ContainsKey(syntax))];
        string[] goneSyntax = [.. allowed.Keys.Where(syntax => !read.Contains(syntax, StringComparer.Ordinal))];
        static IEnumerable<string> Section(string heading, string[] rows) =>
            rows.Length == 0 ? [] : [heading + "\n  " + string.Join("\n  ", rows)];
        return
        [
            .. Section($"Sites that can take the keys away with no witness in the manifest (S6). {Renamed}", unlisted),
            .. Section($"Manifest entries whose site is gone or was renamed. {Renamed}", stale),
            .. Section("Manifest witnesses that do not hold:", unwitnessed),
            .. Section("The rows CB-17b ended (EndedByCb17b) take only a measured or checked witness, and the keys it deleted stay gone:", endedRows),
            .. Section("Syntaxes that can turn a control off and that this census does not read yet (#1866): read them, "
                + "or allow one in UnreadAllowed with why it cannot:", unknown),
            .. Section("UnreadAllowed entries whose syntax is gone:", goneSyntax),
        ];
    }

    /// <summary>The syntaxes that can turn a control off and that the census does not read:
    /// a Command setter in a style (an item container style's included), a Command property
    /// element, an IsEnabled trigger whose condition binds through a property element (it
    /// gives no token), and a setter built in code for IsEnabled or a command.</summary>
    internal static string[] UnreadSyntax(
        IEnumerable<(CompilationUnitSyntax Root, SemanticModel Model)> sources, IEnumerable<(string File, XDocument Document)> xaml)
    {
        var unread = new SortedSet<string>(StringComparer.Ordinal);
        var setters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((CompilationUnitSyntax root, SemanticModel model) in sources)
        {
            // Only a creation written as a Setter, or an implicit new, is bound.
            foreach (BaseObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>()
                .Where(creation => creation is not ObjectCreationExpressionSyntax { Type: var written }
                    || written.ToString().EndsWith("Setter", StringComparison.Ordinal)))
            {
                if (model.GetTypeInfo(creation).Type?.ToDisplayString() != "System.Windows.Setter")
                {
                    continue;
                }

                IEnumerable<ExpressionSyntax> parts = (creation.ArgumentList?.Arguments.Select(argument => argument.Expression) ?? [])
                    .Concat(creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                        .Where(assignment => assignment.Left.ToString() == "Property").Select(assignment => assignment.Right) ?? []);
                foreach (IFieldSymbol field in parts.Select(part => model.GetSymbolInfo(part).Symbol).OfType<IFieldSymbol>())
                {
                    bool disables = field.Name == "IsEnabledProperty"
                        && (Derives(field.ContainingType, "System.Windows.UIElement") || Derives(field.ContainingType, "System.Windows.ContentElement"));
                    bool commands = field.Name == "CommandProperty" && TakesACommand(field.ContainingType);
                    ISymbol? member = model.GetEnclosingSymbol(creation.SpanStart);
                    while (member is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
                    {
                        member = member.ContainingSymbol;
                    }

                    if (disables || commands)
                    {
                        // Pinned by its target, its value and its trigger's condition, and
                        // counted, so an allowed entry covers that one setter only (Codex
                        // round 1, finding 4).
                        ExpressionSyntax? value = creation.ArgumentList?.Arguments.Skip(1).FirstOrDefault()?.Expression
                            ?? creation.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
                                .FirstOrDefault(assignment => assignment.Left.ToString() == "Value")?.Right;
                        string key = $"code-setter {member?.ContainingType?.ToDisplayString()}.{member?.Name} {field.Name[..^"Property".Length]}"
                            + $"={(value is null ? "(unset)" : Regex.Replace(value.ToString(), @"\s+", " "))} when {SetterCondition(creation, model)}";
                        int seen = setters[key] = setters.GetValueOrDefault(key) + 1;
                        _ = unread.Add(seen > 1 ? $"{key} #{seen}" : key);
                    }
                }
            }
        }

        foreach ((string file, XDocument document) in xaml)
        {
            foreach (XElement setter in document.Descendants().Where(element => element.Name.LocalName == "Setter"
                && PropertyName((string?)element.Attribute("Property")) == "Command"))
            {
                XElement owner = setter.Ancestors().First(ancestor => ancestor.Name.LocalName is "Style" or "ControlTemplate" or "DataTemplate");
                _ = unread.Add($"xaml-setter {file} {UnreadCarrier(owner)} Command");
            }

            // A property element the scrape does not read: a Command, or a state the census
            // keys (IsEnabled, Visibility, Focusable) given as <X.IsEnabled><Binding …/>
            // rather than an attribute (the owner's "Small fix, no new round", Codex round 3).
            foreach (XElement property in document.Descendants().Where(element =>
                element.Name.LocalName.EndsWith(".Command", StringComparison.Ordinal)
                || element.Name.LocalName.EndsWith(".IsEnabled", StringComparison.Ordinal)
                || element.Name.LocalName.EndsWith(".Visibility", StringComparison.Ordinal)
                || element.Name.LocalName.EndsWith(".Focusable", StringComparison.Ordinal)))
            {
                _ = unread.Add($"xaml-property {file} {Identity(property.Parent) ?? $"line {((IXmlLineInfo)property).LineNumber}"} {property.Name.LocalName}");
            }

            foreach (XElement setter in document.Descendants().Where(element => element.Name.LocalName == "Setter"
                && PropertyName((string?)element.Attribute("Property")) == "IsEnabled"
                && element.Ancestors().Any(ancestor => ancestor.Name.LocalName.EndsWith(".Triggers", StringComparison.Ordinal))))
            {
                XElement? trigger = setter.Ancestors().FirstOrDefault(ancestor =>
                    ancestor.Name.LocalName is "DataTrigger" or "Trigger" or "MultiDataTrigger" or "MultiTrigger");
                if (trigger?.DescendantsAndSelf().Any(element => element.Name.LocalName.EndsWith(".Binding", StringComparison.Ordinal)) == true)
                {
                    XElement owner = setter.Ancestors().First(ancestor => ancestor.Name.LocalName is "Style" or "ControlTemplate" or "DataTemplate");
                    _ = unread.Add($"xaml-trigger-binding {file} {UnreadCarrier(owner)} IsEnabled");
                }
            }
        }

        return [.. unread];
    }

    /// <summary>The condition of the trigger a code-built setter is added to (by
    /// X.Setters.Add or a Setters initializer): its binding's path or its property, then =
    /// and its value, as the trigger's initializer writes them; "(style)" for a style's own
    /// setter; "(unread)" for anything else.</summary>
    private static string SetterCondition(BaseObjectCreationExpressionSyntax creation, SemanticModel model)
    {
        ExpressionSyntax? owner = creation.Parent switch
        {
            ArgumentSyntax
            {
                Parent: ArgumentListSyntax
                {
                    Parent: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax
                        {
                            Name.Identifier.ValueText: "Add",
                            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Setters", Expression: var holder },
                        },
                    },
                },
            } => model.GetSymbolInfo(holder).Symbol?.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                .OfType<VariableDeclaratorSyntax>().Select(declarator => declarator.Initializer?.Value).FirstOrDefault(),
            InitializerExpressionSyntax
            {
                Parent: AssignmentExpressionSyntax
                {
                    Left: IdentifierNameSyntax { Identifier.ValueText: "Setters" },
                    Parent: InitializerExpressionSyntax { Parent: BaseObjectCreationExpressionSyntax made },
                },
            } => made,
            _ => null,
        };
        if (owner is not BaseObjectCreationExpressionSyntax trigger)
        {
            return "(unread)";
        }

        string? kind = model.GetTypeInfo(trigger).Type?.Name;
        if (kind == "Style")
        {
            return "(style)";
        }

        Dictionary<string, ExpressionSyntax> assigned = trigger.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
            .GroupBy(assignment => assignment.Left.ToString())
            .ToDictionary(group => group.Key, group => group.Last().Right, StringComparer.Ordinal) ?? [];
        string? path = kind switch
        {
            "DataTrigger" => assigned.GetValueOrDefault("Binding") is BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: [var first], Initializer: null }
                ? first.Expression switch
                {
                    LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
                    InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" }, ArgumentList.Arguments: [var named] }
                        => named.Expression.ToString().Split('.').Last(),
                    _ => null,
                }
                : null,
            "Trigger" => assigned.GetValueOrDefault("Property")?.ToString().Split('.').Last(),
            _ => null,
        };
        return path is null || assigned.GetValueOrDefault("Value") is not { } value ? "(unread)" : $"{path}={value}";
    }

    private static string UnreadCarrier(XElement owner) =>
        (string?)owner.Attribute(Xaml + "Key") is { } key
            ? $"{owner.Name.LocalName}:{key}"
            : Identity(owner.Parent?.Parent) ?? $"line {((IXmlLineInfo)owner).LineNumber}";

    [Fact]
    public void ThePlantedCommandSitesAreCaught()
    {
        const string Planted = """
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Controls.Primitives;
            using System.Windows.Data;
            using System.Windows.Input;

            namespace Planted;

            internal sealed class Commands
            {
                private readonly Button _button = new();
                private readonly Button _field;
                private readonly Button Unfocused = new();
                private readonly MenuItem _menuItem = new();
                private readonly MenuItem _stays = new() { StaysOpenOnClick = true };
                private readonly ICommand _command = new RoutedCommand();

                public Commands()
                {
                    _field = new Button { Command = _command };
                    _button.SetBinding(UIElement.IsEnabledProperty, new Binding("Ready"));
                }

                private void Assign() => _button.Command = _command;
                private void Clear(Button item) => item.Command = null;
                private void Row() => _menuItem.Command = _command;
                private void Stay() => _stays.Command = _command;
                private void Caption() => Unfocused.Command = _command;
                private void Set() => _button.SetValue(ButtonBase.CommandProperty, _command);
                private void Bind() => BindingOperations.SetBinding(_button, ButtonBase.CommandProperty, new Binding("Command"));
            }

            // CB-17a's merge judge, nit 4: a receiver named as a focusless XAML element but
            // of another type is not that element.
            internal sealed class Elsewhere
            {
                private readonly ToggleButton Unfocused = new();
                private readonly ICommand _command = new RoutedCommand();

                private void Caption() => Unfocused.Command = _command;
            }
            """;
        CSharpParseOptions options = (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Planted, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:SlateWindows">
              <Window.InputBindings>
                <KeyBinding Key="O" Modifiers="Control" Command="{Binding OpenCommand}" />
              </Window.InputBindings>
              <StackPanel>
                <Button x:Name="Pressed" Command="{Binding PressCommand}" />
                <Button x:Name="Unfocused" Focusable="False" Command="{Binding PressCommand}" />
                <TextBlock><Hyperlink x:Name="Linked" Command="{Binding FollowCommand}">Link</Hyperlink></TextBlock>
                <CheckBox x:Name="Checked" Command="{Binding Path=Model.CheckCommand}" />
                <Menu>
                  <MenuItem Header="Row" Command="{Binding RowCommand}" />
                  <local:CheckMenuItem Header="Check" Command="{Binding CheckRowCommand}" />
                  <MenuItem x:Name="StaysOpen" StaysOpenOnClick="True" Command="{Binding StayCommand}" />
                  <MenuItem x:Name="StyledOpen" Command="{Binding StyledCommand}">
                    <MenuItem.Style>
                      <Style TargetType="MenuItem">
                        <Setter Property="StaysOpenOnClick" Value="True" />
                      </Style>
                    </MenuItem.Style>
                  </MenuItem>
                </Menu>
                <Border>
                  <Border.ContextMenu>
                    <ContextMenu>
                      <MenuItem Header="Context" Command="{Binding ContextCommand}" />
                    </ContextMenu>
                  </Border.ContextMenu>
                </Border>
                <MenuItem x:Name="Loose" Command="{Binding LooseCommand}" />
                <WrapPanel x:Name="Group">
                  <Button Command="{Binding SaveCommand}" />
                  <Button Command="{Binding RevertCommand}" />
                  <Button Command="{Binding RevertCommand}" />
                </WrapPanel>
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape(
            [((CompilationUnitSyntax)tree.GetRoot(), compilation.GetSemanticModel(tree))], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "command Planted.Commands..ctor _field",
                "command Planted.Commands.Assign _button",
                "command Planted.Commands.Bind _button",
                "command Planted.Commands.Set _button",
                "command Planted.Commands.Stay _stays",
                "command Planted.Elsewhere.Caption Unfocused",
                "ui-state Planted.Commands..ctor _button IsEnabled",
                "xaml-command Planted.xaml Checked CheckCommand",
                "xaml-command Planted.xaml Group RevertCommand",
                "xaml-command Planted.xaml Group RevertCommand #2",
                "xaml-command Planted.xaml Group SaveCommand",
                "xaml-command Planted.xaml Linked FollowCommand",
                "xaml-command Planted.xaml Loose LooseCommand",
                "xaml-command Planted.xaml Pressed PressCommand",
                "xaml-command Planted.xaml StaysOpen StayCommand",
                "xaml-command Planted.xaml StyledOpen StyledCommand",
            ],
            sites.Keys.Where(key => key.StartsWith("command ", StringComparison.Ordinal)
                || key.StartsWith("ui-state ", StringComparison.Ordinal)
                || key.StartsWith("xaml-command ", StringComparison.Ordinal)));
        Assert.Equal(["Group"], sites["xaml-command Planted.xaml Group SaveCommand"].Names);
        Assert.Equal(["SaveCommand"], sites["xaml-command Planted.xaml Group SaveCommand"].Tokens);
        Assert.Equal("CheckCommand", sites["xaml-command Planted.xaml Checked CheckCommand"].Command);
    }

    [Fact]
    public void TheIsEnabledTriggersAreKeyedOneByOne()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <StackPanel>
                <Button x:Name="Styled">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding SelectedItem, ElementName=List}" Value="{x:Null}">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Path=Model.IsBusy}" Value="True">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <MultiDataTrigger>
                          <MultiDataTrigger.Conditions>
                            <Condition Binding="{Binding Sheet.IsOpen}" Value="True" />
                            <Condition Binding="{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource Self}}" Value="False" />
                          </MultiDataTrigger.Conditions>
                          <Setter Property="IsEnabled" Value="False" />
                        </MultiDataTrigger>
                        <DataTrigger Binding="{Binding Model.IsBusy, Converter={StaticResource Same}}" Value="True">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Ready}">
                          <DataTrigger.Value>
                            <x:Null />
                          </DataTrigger.Value>
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Ready}" Value="True">
                          <Setter Property="IsEnabled" Value="True" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Hidden}" Value="True">
                          <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding Gone}" Value="True">
                          <Setter Property="Visibility" Value="Collapsed" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
                <Button x:Name="Defaulted">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="IsEnabled" Value="False" />
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding IsDirty}" Value="True">
                          <Setter Property="IsEnabled" Value="True" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
                <Button x:Name="BoundStyle">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="IsEnabled" Value="{Binding Ready}" />
                    </Style>
                  </Button.Style>
                </Button>
                <ContentControl x:Name="Templated" Focusable="False">
                  <ContentControl.Template>
                    <ControlTemplate TargetType="ContentControl">
                      <StackPanel>
                        <Button x:Name="Part" />
                        <ContentPresenter />
                      </StackPanel>
                      <ControlTemplate.Triggers>
                        <Trigger SourceName="Part" Property="IsMouseOver" Value="True">
                          <Setter TargetName="Part" Property="IsEnabled" Value="False" />
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </ContentControl.Template>
                </ContentControl>
                <ListBox x:Name="List" />
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "xaml-trigger Planted.xaml BoundStyle IsEnabled when (style binding)",
                "xaml-trigger Planted.xaml Defaulted IsEnabled when (style default)",
                "xaml-trigger Planted.xaml Part IsEnabled when Part.IsMouseOver=True",
                "xaml-trigger Planted.xaml Styled IsEnabled when List.SelectedItem={x:Null}",
                "xaml-trigger Planted.xaml Styled IsEnabled when Model.IsBusy=True",
                "xaml-trigger Planted.xaml Styled IsEnabled when Model.IsBusy=True #2",
                "xaml-trigger Planted.xaml Styled IsEnabled when Ready=(element)",
                "xaml-trigger Planted.xaml Styled IsEnabled when Sheet.IsOpen=True & IsKeyboardFocusWithin=False",
                "xaml-trigger Planted.xaml Styled Visibility",
            ],
            sites.Keys.Where(key => key.StartsWith("xaml-trigger ", StringComparison.Ordinal)));
        Assert.Equal(["List"], sites["xaml-trigger Planted.xaml Styled IsEnabled when List.SelectedItem={x:Null}"].Tokens);
        Assert.Equal(["IsBusy"], sites["xaml-trigger Planted.xaml Styled IsEnabled when Model.IsBusy=True"].Tokens);
        Assert.Equal(["Sheet"], sites["xaml-trigger Planted.xaml Styled IsEnabled when Sheet.IsOpen=True & IsKeyboardFocusWithin=False"].Tokens);
        Assert.Equal(["Styled"], sites["xaml-trigger Planted.xaml Styled IsEnabled when Ready=(element)"].Names);
        Assert.Empty(sites["xaml-trigger Planted.xaml Defaulted IsEnabled when (style default)"].Tokens!);
    }

    /// <summary>A keys-first fact exists, its code names the site (and a trigger's
    /// condition token), and its own method calls the recorder, qualified: not in a
    /// string, a nameof, a method group, a look-alike of its own class, a helper it calls,
    /// or the list form. The sites are planted as the scrape keys them (fact 2 holds the
    /// scrape to its tokens).</summary>
    [Fact]
    public void AKeysFirstWitnessNamesTheSiteItsTokenAndCallsTheRecorder()
    {
        var pressed = new Site("xaml-command Planted.xaml Pressed PressCommand", null, null, null, null, ["Pressed"], null, "PressCommand");
        var sheet = new Site(
            "xaml-trigger Planted.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True", null, null, null, null, ["MainMenu"], ["AddPropertySheet"]);
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PlantedTests.Holds"] = "[Fact] void Holds() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); });",
                ["PlantedTests.TheSheet"] = "[Fact] void TheSheet() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"menu\", menu, () => host.Workspace.AddPropertySheet = new()); });",
                ["PlantedTests.NoCall"] = "[Fact] void NoCall() => RunSta(() => { var button = host.Pressed; button.Command.Execute(null); });",
                ["PlantedTests.NoName"] = "[Fact] void NoName() => RunSta(() => { var button = host.Other; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"button\", button, () => { }); });",
                ["PlantedTests.InAString"] = "[Fact] void InAString() => RunSta(() => { var button = host.Pressed; Log(\"KeysFirstRecorder.AssertTheKeysLeaveFirst(button)\"); });",
                ["PlantedTests.ByNameof"] = "[Fact] void ByNameof() => RunSta(() => { var button = host.Pressed; Log(nameof(KeysFirstRecorder.AssertTheKeysLeaveFirst)); });",
                ["PlantedTests.AsAGroup"] = "[Fact] void AsAGroup() => RunSta(() => { var button = host.Pressed; Run(KeysFirstRecorder.AssertTheKeysLeaveFirst); });",
                ["PlantedTests.Unqualified"] = "[Fact] void Unqualified() => RunSta(() => { var button = host.Pressed; AssertTheKeysLeaveFirst(\"Pressed\", button, () => { }); });",
                ["PlantedTests.InAHelper"] = "[Fact] void InAHelper() => RunSta(() => { var button = host.Pressed; PressUnderTheRecorder(button); });",
                ["PlantedTests.TheList"] = "[Fact] void TheList() => RunSta(() => { var button = host.Pressed; Assert.Empty(KeysFirstRecorder.DisabledWithTheKeysInside(button, () => { })); });",
                ["PlantedTests.NoToken"] = "[Fact] void NoToken() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"menu\", menu, () => host.Workspace.Open()); });",
                ["PlantedTests.InALocalFunction"] = "[Fact] void InALocalFunction() => RunSta(() => { void Measure() { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); } GC.KeepAlive((Action)Measure); });",
                ["PlantedTests.InAnUninvokedLambda"] = "[Fact] void InAnUninvokedLambda() => RunSta(() => { var button = host.Pressed; Action measure = () => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); GC.KeepAlive(measure); });",
                ["PlantedTests.CallInALocalFunction"] = "[Fact] void CallInALocalFunction() => RunSta(() => { var button = host.Pressed; void Measure() => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"button\", button, () => button.Command.Execute(null)); GC.KeepAlive((Action)Measure); });",
                ["PlantedTests.NameInALocalFunction"] = "[Fact] void NameInALocalFunction() => RunSta(() => { var button = host.Other; void Name() => _ = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"button\", button, () => button.Command.Execute(null)); });",
                ["PlantedTests.InANewAction"] = "[Fact] void InANewAction() => RunSta(() => { var button = host.Pressed; GC.KeepAlive(new Action(() => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)))); });",
                ["PlantedTests.InALazy"] = "[Fact] void InALazy() => RunSta(() => { var button = host.Pressed; GC.KeepAlive(new Lazy<bool>(() => { KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); return true; })); });",
                ["PlantedTests.InAThread"] = "[Fact] void InAThread() => RunSta(() => { var button = host.Pressed; GC.KeepAlive(new Thread(() => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)))); });",
                ["PlantedTests.InATask"] = "[Fact] void InATask() => RunSta(() => { var button = host.Pressed; GC.KeepAlive(Task.Run(() => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)))); });",
                ["PlantedTests.InAnUnknownRunner"] = "[Fact] void InAnUnknownRunner() => RunSta(() => { var button = host.Pressed; Later(() => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null))); });",
                ["PlantedTests.InADispatcherInvoke"] = "[Fact] void InADispatcherInvoke() => RunSta(() => { var button = host.Pressed; host.Shell.Dispatcher.Invoke(() => KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null))); });",
                ["PlantedTests.InADispatchedLambda"] = "[Fact] void InADispatchedLambda() => RunSta(() => PumpedDispatcher.Run(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); }));",
            },
            new HashSet<string>(StringComparer.Ordinal),
            []);

        Assert.Null(judge.Judge(pressed, "keys-first: PlantedTests.Holds"));
        Assert.Null(judge.Judge(sheet, "keys-first: PlantedTests.TheSheet"));
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.NoCall"), StringComparison.Ordinal);
        Assert.Contains("does not name the site", judge.Judge(pressed, "keys-first: PlantedTests.NoName"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.InAString"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.ByNameof"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.AsAGroup"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.Unqualified"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.InAHelper"), StringComparison.Ordinal);
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.TheList"), StringComparison.Ordinal);
        Assert.Contains("names no fact", judge.Judge(pressed, "keys-first: NoSuchTests.Method"), StringComparison.Ordinal);
        Assert.Contains("AddPropertySheet", judge.Judge(sheet, "keys-first: PlantedTests.NoToken"), StringComparison.Ordinal);
        // Codex round 1, finding 2: the call and the names in a local function, or in a
        // lambda nothing is handed, run nowhere; a lambda handed on (RunSta, a dispatcher) counts.
        Assert.NotNull(judge.Judge(pressed, "keys-first: PlantedTests.InALocalFunction"));
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.InAnUninvokedLambda"), StringComparison.Ordinal);
        Assert.Null(judge.Judge(pressed, "keys-first: PlantedTests.InADispatchedLambda"));
        // Codex round 2: only a named runner runs the lambda it is handed before it returns
        // (RunSta, PumpedDispatcher.Run, a dispatcher's Invoke); a delegate, a Lazy, a thread, a
        // task or any other consumer may never run it.
        Assert.Null(judge.Judge(pressed, "keys-first: PlantedTests.InADispatcherInvoke"));
        foreach (string kept in new[] { "InANewAction", "InALazy", "InAThread", "InATask", "InAnUnknownRunner" })
        {
            Assert.Contains("calls", judge.Judge(pressed, $"keys-first: PlantedTests.{kept}") ?? $"{kept} was taken as a witness", StringComparison.Ordinal);
        }
        Assert.Contains("calls", judge.Judge(pressed, "keys-first: PlantedTests.CallInALocalFunction"), StringComparison.Ordinal);
        Assert.Contains("does not name the site", judge.Judge(pressed, "keys-first: PlantedTests.NameInALocalFunction"), StringComparison.Ordinal);
    }

    /// <summary>Plan 47 CB-17b: a stays-on witness is a fact that exists, whose code names
    /// the site (and a trigger's condition token), and whose own method calls
    /// KeysFirstRecorder.AssertItStaysOn with its fourth argument, the predicate that says
    /// the driver which used to turn the control off ran. The call is read by the keys-first
    /// kind's rules (not in a string, a helper, a local function or a lambda nothing runs),
    /// neither kind is witnessed by the other's call, and only a site that can turn a
    /// control off takes it.</summary>
    [Fact]
    public void AStaysOnWitnessNamesTheSiteCallsTheRecorderAndSaysTheDriverRan()
    {
        var pressed = new Site("xaml-command Planted.xaml Pressed PressCommand", null, null, null, null, ["Pressed"], null, "PressCommand");
        var sheet = new Site(
            "xaml-trigger Planted.xaml MainMenu IsEnabled when Workspace.AddPropertySheet=True", null, null, null, null, ["MainMenu"], ["AddPropertySheet"]);
        var shown = new Site("xaml-binding Planted.xaml Pressed Visibility", null, null, null, null, ["Pressed"]);
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PlantedTests.Stays"] = "[Fact] void Stays() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => host.Ran); });",
                ["PlantedTests.TheSheet"] = "[Fact] void TheSheet() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertItStaysOn(\"menu\", menu, () => host.Workspace.AddPropertySheet = new(), () => host.Workspace.AddPropertySheet is not null); });",
                ["PlantedTests.Dispatched"] = "[Fact] void Dispatched() => RunSta(() => PumpedDispatcher.Run(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => host.Ran); }));",
                ["PlantedTests.NoReached"] = "[Fact] void NoReached() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null)); });",
                ["PlantedTests.Leaves"] = "[Fact] void Leaves() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst(\"Pressed\", button, () => button.Command.Execute(null)); });",
                ["PlantedTests.ConstantReached"] = "[Fact] void ConstantReached() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => true); });",
                ["PlantedTests.ReturnedConstant"] = "[Fact] void ReturnedConstant() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => { return true; }); });",
                ["PlantedTests.LiteralReached"] = "[Fact] void LiteralReached() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), static () => (true)); });",
                ["PlantedTests.NegatedConstant"] = "[Fact] void NegatedConstant() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => !false); });",
                ["PlantedTests.ComparedConstant"] = "[Fact] void ComparedConstant() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => 1 == 1); });",
                ["PlantedTests.ReturnedExpression"] = "[Fact] void ReturnedExpression() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => button.Command.Execute(null), () => { return !(false || false); }); });",
                ["PlantedTests.NoName"] = "[Fact] void NoName() => RunSta(() => { var button = host.Other; KeysFirstRecorder.AssertItStaysOn(\"button\", button, () => { }, () => true); });",
                ["PlantedTests.NoToken"] = "[Fact] void NoToken() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertItStaysOn(\"menu\", menu, () => host.Workspace.Open(), () => true); });",
                ["PlantedTests.InAString"] = "[Fact] void InAString() => RunSta(() => { var button = host.Pressed; Log(\"KeysFirstRecorder.AssertItStaysOn(button)\"); });",
                ["PlantedTests.Unqualified"] = "[Fact] void Unqualified() => RunSta(() => { var button = host.Pressed; AssertItStaysOn(\"Pressed\", button, () => { }, () => true); });",
                ["PlantedTests.InAHelper"] = "[Fact] void InAHelper() => RunSta(() => { var button = host.Pressed; StaysOnUnderTheRecorder(button); });",
                ["PlantedTests.InALocalFunction"] = "[Fact] void InALocalFunction() => RunSta(() => { var button = host.Pressed; void Measure() => KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => { }, () => true); GC.KeepAlive((Action)Measure); });",
                ["PlantedTests.InAnUninvokedLambda"] = "[Fact] void InAnUninvokedLambda() => RunSta(() => { var button = host.Pressed; Action measure = () => KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => { }, () => true); GC.KeepAlive(measure); });",
                ["PlantedTests.InATask"] = "[Fact] void InATask() => RunSta(() => { var button = host.Pressed; GC.KeepAlive(Task.Run(() => KeysFirstRecorder.AssertItStaysOn(\"Pressed\", button, () => { }, () => true))); });",
            },
            new HashSet<string>(StringComparer.Ordinal),
            []);

        Assert.Null(judge.Judge(pressed, "stays-on: PlantedTests.Stays"));
        Assert.Null(judge.Judge(sheet, "stays-on: PlantedTests.TheSheet"));
        Assert.Null(judge.Judge(pressed, "stays-on: PlantedTests.Dispatched"));
        Assert.Contains("reached", judge.Judge(pressed, "stays-on: PlantedTests.NoReached") ?? "a call with three arguments was taken", StringComparison.Ordinal);
        // Red team F7: a predicate that is a constant says nothing about the driver.
        // The merge judge's nit 5: so does any constant expression, not only a bare literal.
        foreach (string constant in new[] { "ConstantReached", "ReturnedConstant", "LiteralReached", "NegatedConstant", "ComparedConstant", "ReturnedExpression" })
        {
            Assert.Contains("reached", judge.Judge(pressed, $"stays-on: PlantedTests.{constant}") ?? $"{constant} was taken as a witness", StringComparison.Ordinal);
        }
        Assert.Contains("AssertItStaysOn", judge.Judge(pressed, "stays-on: PlantedTests.Leaves") ?? "a keys-first call witnessed stays-on", StringComparison.Ordinal);
        Assert.Contains("AssertTheKeysLeaveFirst", judge.Judge(pressed, "keys-first: PlantedTests.Stays") ?? "a stays-on call witnessed keys-first", StringComparison.Ordinal);
        Assert.Contains("does not name the site", judge.Judge(pressed, "stays-on: PlantedTests.NoName"), StringComparison.Ordinal);
        Assert.Contains("AddPropertySheet", judge.Judge(sheet, "stays-on: PlantedTests.NoToken"), StringComparison.Ordinal);
        Assert.Contains("names no fact", judge.Judge(pressed, "stays-on: NoSuchTests.Method"), StringComparison.Ordinal);
        foreach (string unread in new[] { "InAString", "Unqualified", "InAHelper", "InALocalFunction", "InAnUninvokedLambda", "InATask" })
        {
            Assert.Contains("AssertItStaysOn", judge.Judge(pressed, $"stays-on: PlantedTests.{unread}") ?? $"{unread} was taken as a witness", StringComparison.Ordinal);
        }

        Assert.Contains("only for a site", judge.Judge(shown, "stays-on: PlantedTests.Stays") ?? "a site that turns nothing off took stays-on", StringComparison.Ordinal);
    }

    /// <summary>Plan 47 CB-17b (CB-17a's merge judge, m3): a row CB-17b ended takes only a
    /// measured or checked witness. A free-text exemption, a date, or a fact that only names
    /// it is refused, and so is the row's key going missing: the frozen table names each
    /// key as it is now.</summary>
    [Fact]
    public void ARowCb17bEndedTakesOnlyAMeasuredOrCheckedWitness()
    {
        var ended = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["k-first"] = "k-first",
            ["k-stays"] = "k-stays",
            ["k-always"] = "k-always",
            ["k-unfocused"] = "k-unfocused",
            ["k-press"] = "k-press",
            ["k-fires"] = "k-fires",
            ["k-dated"] = "k-dated",
            ["k-named"] = "k-named",
            ["k-gone"] = "k-gone-was",
        };
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["k-first"] = "keys-first: PlantedTests.Holds",
            ["k-stays"] = "stays-on: PlantedTests.Stays",
            ["k-always"] = "exempt: always on: Planted.PressCommand",
            ["k-unfocused"] = "exempt: not focusable",
            ["k-press"] = "exempt: its own press never turns it off: it reads Ready, which no press of its changes",
            ["k-fires"] = "exempt: never fires: the model is never busy here (PlantedTests.NeverOpens)",
            ["k-dated"] = "dated: 2026-11-21; CB-17b (CD11): its press starts the work",
            ["k-named"] = "PlantedTests.Holds",
        };

        string[] problems = EndedProblems(manifest, ended);

        string[] named = [.. problems.Select(problem => problem.Split(' ')[0])];
        Assert.Equal(["k-dated", "k-fires", "k-gone", "k-named", "k-press"], named.Order(StringComparer.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("k-gone", StringComparison.Ordinal) && problem.Contains("k-gone-was", StringComparison.Ordinal));
    }

    /// <summary>Plan 47 CB-17b (red team F10): a key CB-17b deleted with its trigger stays
    /// gone. The census fails, naming why it went, when the scrape finds it again or the
    /// manifest lists it.</summary>
    [Fact]
    public void AKeyCb17bDeletedFailsTheCensusIfItComesBack()
    {
        var gone = new Dictionary<string, string>(StringComparer.Ordinal) { ["k-deleted"] = "its trigger is deleted" };
        var none = new Dictionary<string, string>(StringComparer.Ordinal);

        Assert.Empty(EndedProblems(none, none, gone, scraped: ["k-other"]));
        Assert.Contains(
            EndedProblems(none, none, gone, scraped: ["k-deleted"]),
            problem => problem.StartsWith("k-deleted", StringComparison.Ordinal) && problem.Contains("its trigger is deleted", StringComparison.Ordinal));
        Assert.Contains(
            EndedProblems(new Dictionary<string, string>(StringComparer.Ordinal) { ["k-deleted"] = "keys-first: PlantedTests.Holds" }, none, gone, scraped: []),
            problem => problem.StartsWith("k-deleted", StringComparison.Ordinal) && problem.Contains("its trigger is deleted", StringComparison.Ordinal));
    }

    /// <summary>Plan 47 CB-17b: the dated kind is gone, and so is every dated row.</summary>
    [Fact]
    public void TheManifestHasNoDatedRow() =>
        Assert.Empty(Manifest().Where(entry => entry.Value.StartsWith("dated:", StringComparison.Ordinal)).Select(entry => entry.Key));

    /// <summary>A site that can turn a control off takes keys-first, stays-on or one of the
    /// four exemptions. A guard, a fact that only names it, a reason a cited fact does not back
    /// and a dated row (a kind CB-17b deleted) do not hold.</summary>
    [Fact]
    public void ADisableSiteTakesOnlyKeysFirstOrAnExemption()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Window.Resources>
                <Style x:Key="Quiet" TargetType="Button">
                  <Setter Property="Focusable" Value="False" />
                  <Style.Triggers>
                    <DataTrigger Binding="{Binding IsModalSurfaceOpen}" Value="True">
                      <Setter Property="IsEnabled" Value="False" />
                    </DataTrigger>
                  </Style.Triggers>
                </Style>
                <Style x:Key="Loud" TargetType="Button">
                  <Setter Property="Focusable" Value="False" />
                  <Style.Triggers>
                    <DataTrigger Binding="{Binding IsModalSurfaceOpen}" Value="True">
                      <Setter Property="IsEnabled" Value="False" />
                    </DataTrigger>
                  </Style.Triggers>
                </Style>
              </Window.Resources>
              <Grid x:Name="Columns">
                <Border x:Name="FilesPaneBorder">
                  <StackPanel>
                    <Button x:Name="Bound" IsEnabled="{Binding Ready}" />
                    <Button x:Name="Triggered">
                      <Button.Style>
                        <Style TargetType="Button">
                          <Style.Triggers>
                            <DataTrigger Binding="{Binding Model.IsBusy}" Value="True">
                              <Setter Property="IsEnabled" Value="False" />
                            </DataTrigger>
                          </Style.Triggers>
                        </Style>
                      </Button.Style>
                    </Button>
                    <Button x:Name="Pressed" Command="{Binding PressCommand}" />
                    <Button x:Name="Shown" Visibility="{Binding Shown}" />
                    <Button x:Name="Unfocused" Focusable="False" />
                    <Button x:Name="TitleButton" Style="{StaticResource Quiet}" />
                    <Button x:Name="OpenButton" Style="{StaticResource Loud}" Focusable="True" />
                  </StackPanel>
                </Border>
              </Grid>
            </Window>
            """,
            LoadOptions.SetLineInfo);
        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);
        Site bound = sites["xaml-binding Planted.xaml Bound IsEnabled"];
        Site shown = sites["xaml-binding Planted.xaml Shown Visibility"];
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PlantedTests.NamesThem"] = "[Fact] void NamesThem() { host.Bound.IsEnabled = false; host.Pressed.Command = null; host.Triggered.IsEnabled = false; }",
                ["PlantedTests.NeverOpens"] = "[Fact] void NeverOpens() { Assert.False(host.Model.IsBusy); }",
            },
            new HashSet<string>(["Columns", "FilesPaneBorder"], StringComparer.Ordinal),
            [xaml]);

        // A guard lands the keys once they are lost; it does not keep a control from
        // going off while it holds them.
        Assert.Null(judge.Judge(shown, "guard: FilesPaneBorder"));
        Assert.NotNull(judge.Judge(bound, "guard: FilesPaneBorder"));
        Site pressed = sites["xaml-command Planted.xaml Pressed PressCommand"];
        Site triggered = sites["xaml-trigger Planted.xaml Triggered IsEnabled when Model.IsBusy=True"];
        Site quiet = sites["xaml-trigger Planted.xaml Style:Quiet IsEnabled when IsModalSurfaceOpen=True"];
        Site loud = sites["xaml-trigger Planted.xaml Style:Loud IsEnabled when IsModalSurfaceOpen=True"];
        var unfocused = new Site("ui-state Planted.View.Disable Unfocused IsEnabled", null, null, null, "Unfocused", ["Unfocused", "View"]);
        foreach (Site site in new[] { bound, triggered, pressed })
        {
            Assert.NotNull(judge.Judge(site, "guard: FilesPaneBorder"));
            Assert.NotNull(judge.Judge(site, "PlantedTests.NamesThem"));
            Assert.NotNull(judge.Judge(site, "exempt: covered by PlantedTests.NamesThem"));
            Assert.Null(judge.Judge(site, "exempt: its own press never turns it off: it reads Ready, which no press of its changes"));
            Assert.NotNull(judge.Judge(site, "exempt: its own press never turns it off: fine"));
            Assert.NotNull(judge.Judge(site, "exempt: its own press never turns it off: it reads Ready, as PlantedTests.NoSuchFact shows"));
            Assert.NotNull(judge.Judge(site, "exempt: not focusable"));
            Assert.NotNull(judge.Judge(site, "dated: 2026-11-21; CB-17b (CD11): its press starts the work"));
        }

        Assert.NotNull(judge.Judge(shown, "dated: 2026-11-21; CB-17b (CD11): it hides"));

        Assert.Null(judge.Judge(quiet, "exempt: not focusable"));
        Assert.Null(judge.Judge(unfocused, "exempt: not focusable"));
        Assert.NotNull(judge.Judge(loud, "exempt: not focusable"));
        Assert.Null(judge.Judge(triggered, "exempt: never fires: the model is never busy here (PlantedTests.NeverOpens)"));
        Assert.NotNull(judge.Judge(triggered, "exempt: never fires: the model is never busy here"));
        Assert.NotNull(judge.Judge(triggered, "exempt: never fires: the model is never busy here (PlantedTests.NoSuchFact)"));
        Assert.NotNull(judge.Judge(pressed, "exempt: never fires: a command is no trigger (PlantedTests.NeverOpens)"));
    }

    [Fact]
    public void AnAlwaysOnExemptionReadsTheCommand()
    {
        const string Planted = """
            using System.Threading.Tasks;
            using System.Windows.Input;
            using SlateWindows;

            namespace Planted;

            internal interface IPlantedHost
            {
                ICommand PlantedSharedCommand { get; }
            }

            internal sealed class AlwaysOn : IPlantedHost
            {
                private RelayCommand? _lazy;
                private readonly ICommand _admitted;

                public AlwaysOn()
                {
                    PlantedCtorCommand = new RelayCommand(_ => { }, _ => true);
                    _admitted = new AsyncRelayCommand(_ => Task.CompletedTask, () => true, admitWhileExecuting: true);
                    PlantedStateCommand = new RelayCommand(_ => { }, _ => IsOpen);
                    PlantedBusyCommand = new AsyncRelayCommand(_ => Task.CompletedTask, () => true);
                    PlantedSharedCommand = new RelayCommand(_ => { }, _ => true);
                    PlantedTwinCommand = new RelayCommand(_ => { }, _ => true);
                    PlantedRecordCommand = new RelayCommand(_ => { }, _ => true);
                }

                public bool IsOpen { get; set; }
                public ICommand PlantedCtorCommand { get; }
                public ICommand PlantedLazyCommand => _lazy ??= new RelayCommand(_ => { }, _ => true);
                public ICommand PlantedAdmittedCommand => _admitted;
                public ICommand PlantedStateCommand { get; }
                public ICommand PlantedBusyCommand { get; }
                public ICommand PlantedSharedCommand { get; }
                public ICommand PlantedTwinCommand { get; }
                public ICommand PlantedRecordCommand { get; }
                public ICommand PlantedOpenSetterCommand { get; set; } = new RelayCommand(_ => { }, _ => true);

                public ICommand PlantedLongGetterCommand
                {
                    get
                    {
                        var made = new RelayCommand(_ => { }, _ => true);
                        return made;
                    }
                }
            }

            internal sealed class Forwarder(AlwaysOn row)
            {
                public ICommand PlantedForwardCommand => row.PlantedCtorCommand;
            }

            internal sealed class Twin
            {
                public Twin() => PlantedTwinCommand = new RelayCommand(_ => { }, _ => Ready);

                public bool Ready { get; set; }
                public ICommand PlantedTwinCommand { get; }
            }

            internal sealed record PlantedRow(ICommand PlantedRecordCommand);
            """;
        CSharpParseOptions options = (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(Planted, options, path: "Planted.cs"));
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), [], commands: compilation);
        static Site Bound(string command) => new($"xaml-command Planted.xaml Thing {command}", null, null, null, null, ["Thing"], Command: command);

        Assert.Null(judge.Judge(Bound("PlantedCtorCommand"), "exempt: always on: AlwaysOn.PlantedCtorCommand"));
        Assert.Null(judge.Judge(Bound("PlantedLazyCommand"), "exempt: always on: AlwaysOn.PlantedLazyCommand"));
        Assert.Null(judge.Judge(Bound("PlantedAdmittedCommand"), "exempt: always on: AlwaysOn.PlantedAdmittedCommand"));
        Assert.Null(judge.Judge(Bound("PlantedForwardCommand"), "exempt: always on: Forwarder.PlantedForwardCommand"));
        Assert.Null(judge.Judge(Bound("PlantedSharedCommand"), "exempt: always on: AlwaysOn.PlantedSharedCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedStateCommand"), "exempt: always on: AlwaysOn.PlantedStateCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedBusyCommand"), "exempt: always on: AlwaysOn.PlantedBusyCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedCtorCommand"), "exempt: always on: Twin.PlantedCtorCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedOtherCommand"), "exempt: always on: AlwaysOn.PlantedCtorCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedTwinCommand"), "exempt: always on: AlwaysOn.PlantedTwinCommand"));
        Assert.NotNull(judge.Judge(Bound("PlantedRecordCommand"), "exempt: always on: AlwaysOn.PlantedRecordCommand"));
        // Another type can set it, or its getter is more than a return the reading follows.
        Assert.Contains("set by another type", judge.Judge(Bound("PlantedOpenSetterCommand"), "exempt: always on: AlwaysOn.PlantedOpenSetterCommand"), StringComparison.Ordinal);
        Assert.Contains("getter", judge.Judge(Bound("PlantedLongGetterCommand"), "exempt: always on: AlwaysOn.PlantedLongGetterCommand"), StringComparison.Ordinal);
    }

    /// <summary>The 47 rows CB-17a dated are the rows CB-17b ended, each once, by its key now,
    /// and the keys CB-17b deleted with their trigger. Only the three Queries groups' keys
    /// changed, with their condition. The menu's seventeen are its three overlay rows, never
    /// dated, and its fourteen sheet rows, which CB-17b ended.</summary>
    [Fact]
    public void TheRowsCb17bEndedAreTheFortySevenCb17aDated()
    {
        string[] then = [.. EndedByCb17b.Values, .. GoneWithCb17b.Keys];
        Assert.Equal(47, then.Length);
        Assert.Equal(47, then.Distinct(StringComparer.Ordinal).Count());

        // Red team on PR 2's diff, F8: the 47 are the keys CB-17a dated, not any 47 sites
        // that can turn a control off. Sorted and joined by line feeds, their text hashes to
        // what main ad40500d's frozen table and dated rows held, so a row swapped for
        // another site, which would free its key for a free-text exemption, is refused.
        Assert.Equal(
            "2749c3533c7494f9682ce7645a7ab5231dd7519cc87bbfaf69290bcc698de4fc",
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(string.Join('\n', then.Order(StringComparer.Ordinal))))));
        Assert.All(EndedByCb17b.Keys.Concat(GoneWithCb17b.Keys), key => Assert.True(IsDisableSite(key), $"{key} can turn no control off"));
        Assert.Equal(
            ["QueriesOpenBaseFile", "QueriesOpenDashboard", "QueriesRun"],
            EndedByCb17b.Where(entry => entry.Key != entry.Value)
                .Select(entry => Regex.Match(entry.Key, @"WrapPanel\((?<group>\w+)\)").Groups["group"].Value)
                .Order(StringComparer.Ordinal));
        Assert.All(GoneWithCb17b, entry => Assert.True(
            entry.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4, $"{entry.Key} is gone with no reason: '{entry.Value}'"));

        Assert.Equal(17, WhereADesktopGivesAMenuTheKeys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(WhereADesktopGivesAMenuTheKeys, key => Assert.StartsWith("xaml-trigger MainWindow.xaml MainMenu IsEnabled when ", key, StringComparison.Ordinal));
        Assert.Equal(
            ["Palette", "QuickSwitcher", "Search"],
            WhereADesktopGivesAMenuTheKeys.Where(key => !EndedByCb17b.ContainsKey(key))
                .Select(key => key.Split(" when ")[1].Split('.')[0])
                .Order(StringComparer.Ordinal));

        // Red team F10: the gone set is the launcher's sheet key alone, deleted with its trigger (D10).
        Assert.Equal(
            ["xaml-trigger WorkspaceTemplates.xaml Style:Slate.LauncherRowStyle IsEnabled when IsModalSheetOpen=True"],
            GoneWithCb17b.Keys);
    }

    /// <summary>A witness fact is one xunit runs, and its source is the method that carries
    /// the attribute: a skipped, explicit or conditionally skipped fact witnesses nothing,
    /// and a helper of the same name does not lend it its code (CB-17a review, m4).</summary>
    [Fact]
    public void AWitnessFactRunsAndIsTheMethodThatCarriesTheAttribute()
    {
        Assert.False(NeverRuns(new FactAttribute()));
        Assert.True(NeverRuns(new FactAttribute { Skip = "not run" }));
        Assert.True(NeverRuns(new FactAttribute { Explicit = true }));
        Assert.True(NeverRuns(new FactAttribute { SkipUnless = "On" }));
        Assert.True(NeverRuns(new FactAttribute { SkipWhen = "Off" }));
        Assert.True(NeverRuns(new TheoryAttribute { Skip = "not run" }));
        // CB-17a's merge judge, nit 3: a theory that skips when it has no data.
        Assert.True(NeverRuns(new TheoryAttribute { SkipTestWithoutData = true }));
        Assert.False(NeverRuns(new TheoryAttribute()));
        CompilationUnitSyntax planted = SyntaxFactory.ParseCompilationUnit(
            """
            public sealed class PlantedTests
            {
                [Fact]
                public void Holds() => Press(host.Pressed);

                private static void Holds(Button button) => KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => { });

                [Fact(Skip = "not run")]
                public void Skipped() => KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", host.Pressed, () => { });

                [Fact(Explicit = true)]
                public void ByRequest() => KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", host.Pressed, () => { });

                [Theory(SkipWhen = nameof(Off))]
                [InlineData(1)]
                public void WhenOff(int row) => KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", host.Pressed, () => { });

                [Fact(SkipUnless = nameof(On))]
                public void UnlessOn() => KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", host.Pressed, () => { });

                [Theory(SkipTestWithoutData = true)]
                [MemberData(nameof(Rows))]
                public void WithoutData(int row) => KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", host.Pressed, () => { });
            }
            """);

        Dictionary<string, string> facts = FactSources(
            [planted],
            new HashSet<string>(
                ["PlantedTests.Holds", "PlantedTests.Skipped", "PlantedTests.ByRequest", "PlantedTests.WhenOff", "PlantedTests.UnlessOn", "PlantedTests.WithoutData"],
                StringComparer.Ordinal));

        Assert.Equal(["PlantedTests.Holds"], facts.Keys);
        Assert.DoesNotContain("AssertTheKeysLeaveFirst", facts["PlantedTests.Holds"], StringComparison.Ordinal);
    }

    /// <summary>An unnamed button that carries a command and an IsEnabled trigger is keyed
    /// by its holder and its command, as its command site is, so a button inserted in front
    /// of it renames nothing (CB-17a review, m5).</summary>
    [Fact]
    public void AnUnnamedCommandButtonsTriggerIsKeyedByItsHolderAndCommand()
    {
        const string Toolbar = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <WrapPanel x:Name="Toolbar">
                INSERTED
                <Button Content="Save" Command="{Binding SaveCommand}">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="IsEnabled" Value="False" />
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding IsDirty}" Value="True">
                          <Setter Property="IsEnabled" Value="True" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding WriteInFlight}" Value="True">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
              </WrapPanel>
            </Window>
            """;
        static string[] Keys(string inserted) =>
        [
            .. Scrape([], [("Planted.xaml", XDocument.Parse(Toolbar.Replace("INSERTED", inserted, StringComparison.Ordinal), LoadOptions.SetLineInfo))])
                .Keys.Where(key => key.StartsWith("xaml-trigger ", StringComparison.Ordinal)),
        ];

        Assert.Equal(
            [
                "xaml-trigger Planted.xaml Toolbar SaveCommand IsEnabled when (style default)",
                "xaml-trigger Planted.xaml Toolbar SaveCommand IsEnabled when WriteInFlight=True",
            ],
            Keys(string.Empty));
        Assert.Equal(Keys(string.Empty), Keys("<Button Content=\"New\" Command=\"{Binding NewCommand}\" />"));
    }

    /// <summary>The main fact's problems, in one place so their wiring is held: an unlisted
    /// site and a stale row name the way a renamed key keeps its witness (and its place in
    /// EndedByCb17b), a witness that does not hold, a row CB-17b ended that is gone, a key it
    /// deleted that is back, and a syntax the census does not read (CB-17a review, m5, m6,
    /// low 7; red team F10).</summary>
    [Fact]
    public void TheManifestProblemsNameEachKind()
    {
        var apply = new Site("xaml-binding Planted.xaml Apply IsEnabled", null, null, null, null, ["Apply"]);
        var preview = new Site("xaml-binding Planted.xaml Preview IsEnabled", null, null, null, null, ["Preview"]);
        var back = new Site("xaml-binding Planted.xaml Deleted IsEnabled", null, null, null, null, ["Deleted"]);
        var scraped = new SortedDictionary<string, Site>(StringComparer.Ordinal) { [apply.Key] = apply, [preview.Key] = preview, [back.Key] = back };
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [apply.Key] = "dated: 2026-11-21; CB-17b (CD11): its press starts the apply",
            ["xaml-binding Planted.xaml Gone IsEnabled"] = "exempt: not focusable",
        };
        var judge = new WitnessJudge(new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), []);
        var ended = new Dictionary<string, string>(StringComparer.Ordinal) { ["xaml-binding Planted.xaml Ended IsEnabled"] = "xaml-binding Planted.xaml Ended IsEnabled" };
        var gone = new Dictionary<string, string>(StringComparer.Ordinal) { [back.Key] = "its binding is deleted with its button" };

        string[] problems = ManifestProblems(scraped, manifest, judge, unread: ["xaml-setter Planted.xaml Command (line 3)"], ended: ended, gone: gone);

        Assert.Contains(problems, problem => problem.Contains(preview.Key, StringComparison.Ordinal) && problem.Contains("EndedByCb17b", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("xaml-binding Planted.xaml Gone IsEnabled", StringComparison.Ordinal) && problem.Contains("renamed", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains($"{apply.Key} → ", StringComparison.Ordinal) && problem.Contains("keys-first", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("xaml-setter Planted.xaml Command (line 3)", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("EndedByCb17b", StringComparison.Ordinal) && problem.Contains("Planted.xaml Ended", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains($"{back.Key} is back", StringComparison.Ordinal) && problem.Contains("its binding is deleted", StringComparison.Ordinal));
    }

    /// <summary>The syntaxes that can turn a control off and that the census does not read
    /// yet fail it, rather than passing unseen: a Command setter in a style or an item
    /// container style, a Command property element, a trigger whose binding is a property
    /// element (it gives no token), and a trigger built in code that sets IsEnabled or a
    /// command (CB-17a review, m6).</summary>
    [Fact]
    public void TheSyntaxesTheCensusDoesNotReadAreNamed()
    {
        const string Planted = """
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Controls.Primitives;

            namespace Planted;

            internal static class Styles
            {
                private static Style Rows()
                {
                    var style = new Style(typeof(ListBoxItem));
                    var header = new DataTrigger();
                    header.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
                    style.Triggers.Add(header);
                    return style;
                }

                private static Style Buttons()
                {
                    var style = new Style(typeof(Button));
                    style.Setters.Add(new Setter { Property = ButtonBase.CommandProperty, Value = null });
                    return style;
                }

                private static Style Names()
                {
                    var style = new Style(typeof(Button));
                    style.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
                    return style;
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
              <Window.Resources>
                <Style x:Key="Commanded" TargetType="Button">
                  <Setter Property="Command" Value="{Binding SaveCommand}" />
                </Style>
              </Window.Resources>
              <StackPanel>
                <ListBox x:Name="Rows">
                  <ListBox.ItemContainerStyle>
                    <Style TargetType="ListBoxItem">
                      <Setter Property="Command" Value="{Binding OpenCommand}" />
                    </Style>
                  </ListBox.ItemContainerStyle>
                </ListBox>
                <Button x:Name="Propertied">
                  <Button.Command>
                    <Binding Path="SaveCommand" />
                  </Button.Command>
                </Button>
                <Button x:Name="Enabled">
                  <Button.IsEnabled>
                    <Binding Path="CanSave" />
                  </Button.IsEnabled>
                </Button>
                <Button x:Name="Shown">
                  <Button.Visibility>
                    <Binding Path="SaveVisibility" />
                  </Button.Visibility>
                </Button>
                <Button x:Name="Stoppable">
                  <UIElement.Focusable>
                    <Binding Path="CanFocus" />
                  </UIElement.Focusable>
                </Button>
                <Button x:Name="Elemented">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Style.Triggers>
                        <DataTrigger Value="True">
                          <DataTrigger.Binding>
                            <Binding Path="IsBusy" />
                          </DataTrigger.Binding>
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding IsHidden}" Value="True">
                          <Setter Property="IsEnabled" Value="False" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        string[] unread = UnreadSyntax([((CompilationUnitSyntax)tree.GetRoot(), compilation.GetSemanticModel(tree))], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "code-setter Planted.Styles.Buttons Command=null when (style)",
                "code-setter Planted.Styles.Rows IsEnabled=false when (unread)",
                "xaml-property Planted.xaml Enabled Button.IsEnabled",
                "xaml-property Planted.xaml Propertied Button.Command",
                "xaml-property Planted.xaml Shown Button.Visibility",
                "xaml-property Planted.xaml Stoppable UIElement.Focusable",
                "xaml-setter Planted.xaml Rows Command",
                "xaml-setter Planted.xaml Style:Commanded Command",
                "xaml-trigger-binding Planted.xaml Elemented IsEnabled",
            ],
            unread);
    }

    /// <summary>A menu row whose staying open the census cannot read stays a site: a bound
    /// StaysOpenOnClick, one set by a style it is based on or by its menu's item container
    /// style, and one whose based-on style is not found. A style's own IsEnabled False is
    /// read however it is cased (CB-17a review, low 6).</summary>
    [Fact]
    public void AMenuRowThatMayStayOpenStaysASite()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Window.Resources>
                <Style x:Key="StickyBase" TargetType="MenuItem">
                  <Setter Property="StaysOpenOnClick" Value="True" />
                </Style>
                <Style x:Key="Sticky" TargetType="MenuItem" BasedOn="{StaticResource StickyBase}" />
                <Style x:Key="PlainBase" TargetType="MenuItem">
                  <Setter Property="StaysOpenOnClick" Value="False" />
                </Style>
                <Style BasedOn="{StaticResource PlainBase}" TargetType="MenuItem" />
                <Style x:Key="Orphan" TargetType="MenuItem" BasedOn="{StaticResource Missing}" />
              </Window.Resources>
              <StackPanel>
                <Menu>
                  <MenuItem x:Name="Plain" Command="{Binding PlainCommand}" />
                  <MenuItem x:Name="Bound" StaysOpenOnClick="{Binding Sticky}" Command="{Binding BoundCommand}" />
                  <MenuItem x:Name="Lower" StaysOpenOnClick="true" Command="{Binding LowerCommand}" />
                  <MenuItem x:Name="Based" Style="{StaticResource Sticky}" Command="{Binding BasedCommand}" />
                  <MenuItem x:Name="Unresolved" Style="{StaticResource Orphan}" Command="{Binding OrphanCommand}" />
                </Menu>
                <Menu>
                  <Menu.ItemContainerStyle>
                    <Style TargetType="MenuItem">
                      <Setter Property="StaysOpenOnClick" Value="True" />
                    </Style>
                  </Menu.ItemContainerStyle>
                  <MenuItem x:Name="Contained" Command="{Binding ContainedCommand}" />
                </Menu>
                <Menu ItemContainerStyle="{StaticResource Elsewhere}">
                  <MenuItem x:Name="Referenced" Command="{Binding ReferencedCommand}" />
                </Menu>
                <Button x:Name="Lowered">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="IsEnabled" Value="false" />
                    </Style>
                  </Button.Style>
                </Button>
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);

        Assert.Equal(
            [
                "xaml-command Planted.xaml Based BasedCommand",
                "xaml-command Planted.xaml Bound BoundCommand",
                "xaml-command Planted.xaml Contained ContainedCommand",
                "xaml-command Planted.xaml Lower LowerCommand",
                "xaml-command Planted.xaml Referenced ReferencedCommand",
                "xaml-command Planted.xaml Unresolved OrphanCommand",
            ],
            sites.Keys.Where(key => key.StartsWith("xaml-command ", StringComparison.Ordinal)));
        Assert.Contains("xaml-trigger Planted.xaml Lowered IsEnabled when (style default)", sites.Keys);
    }

    /// <summary>A property written with its owner (UIElement.IsEnabled, (UIElement.IsEnabled),
    /// ButtonBase.Command, MenuItem.StaysOpenOnClick) is read as the property, in a trigger,
    /// a style's own setter, an element's attribute and the unread syntaxes alike (Codex
    /// round 1, finding 3).</summary>
    [Fact]
    public void OwnerQualifiedPropertiesAreReadAsTheirProperty()
    {
        XDocument xaml = XDocument.Parse(
            """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Window.Resources>
                <Style x:Key="Sticky" TargetType="MenuItem">
                  <Setter Property="MenuItem.StaysOpenOnClick" Value="True" />
                </Style>
                <Style x:Key="Commanded" TargetType="Button">
                  <Setter Property="ButtonBase.Command" Value="{Binding SaveCommand}" />
                </Style>
              </Window.Resources>
              <StackPanel>
                <Button x:Name="Qualified">
                  <Button.Style>
                    <Style TargetType="Button">
                      <Setter Property="FrameworkElement.IsEnabled" Value="False" />
                      <Style.Triggers>
                        <DataTrigger Binding="{Binding IsBusy}" Value="True">
                          <Setter Property="UIElement.IsEnabled" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding IsLocked}" Value="True">
                          <Setter Property="(UIElement.IsEnabled)" Value="False" />
                        </DataTrigger>
                        <DataTrigger Binding="{Binding IsHidden}" Value="True">
                          <Setter Property="UIElement.Visibility" Value="Collapsed" />
                        </DataTrigger>
                        <DataTrigger Value="True">
                          <DataTrigger.Binding>
                            <Binding Path="IsHeld" />
                          </DataTrigger.Binding>
                          <Setter Property="UIElement.IsEnabled" Value="False" />
                        </DataTrigger>
                      </Style.Triggers>
                    </Style>
                  </Button.Style>
                </Button>
                <Button x:Name="Attributed" ButtonBase.Command="{Binding PressCommand}" UIElement.IsEnabled="{Binding Ready}" />
                <Menu>
                  <MenuItem x:Name="StaysOpen" Style="{StaticResource Sticky}" Command="{Binding StayCommand}" />
                </Menu>
              </StackPanel>
            </Window>
            """,
            LoadOptions.SetLineInfo);

        SortedDictionary<string, Site> sites = Scrape([], [("Planted.xaml", xaml)]);

        Assert.Contains("xaml-trigger Planted.xaml Qualified IsEnabled when (style default)", sites.Keys);
        Assert.Contains("xaml-trigger Planted.xaml Qualified IsEnabled when IsBusy=True", sites.Keys);
        Assert.Contains("xaml-trigger Planted.xaml Qualified IsEnabled when IsLocked=True", sites.Keys);
        Assert.Contains("xaml-trigger Planted.xaml Qualified Visibility", sites.Keys);
        Assert.Contains("xaml-command Planted.xaml Attributed PressCommand", sites.Keys);
        Assert.Contains("xaml-binding Planted.xaml Attributed IsEnabled", sites.Keys);
        Assert.Contains("xaml-command Planted.xaml StaysOpen StayCommand", sites.Keys);
        Assert.Equal(
            ["xaml-setter Planted.xaml Style:Commanded Command", "xaml-trigger-binding Planted.xaml Qualified IsEnabled"],
            UnreadSyntax([], [("Planted.xaml", xaml)]));
    }

    /// <summary>A code-built setter the census lets stand is pinned by its target, its value
    /// and its trigger's condition: a second such setter, or a changed condition, is a new
    /// syntax that fails the census until it is read or allowed (Codex round 1, finding 4).</summary>
    [Fact]
    public void AnAllowedCodeSetterIsPinnedByItsConditionAndTarget()
    {
        const string Planted = """
            using System.Windows;
            using System.Windows.Controls;
            using System.Windows.Data;

            namespace Planted;

            internal sealed class Row
            {
                public bool IsHeader { get; }
            }

            internal static class Styles
            {
                private static Style Rows()
                {
                    var style = new Style(typeof(ListBoxItem));
                    var header = new DataTrigger { Binding = new Binding(nameof(Row.IsHeader)), Value = true };
                    header.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
                    style.Triggers.Add(header);
                    return style;
                }

                private static Style Twice()
                {
                    var style = new Style(typeof(ListBoxItem));
                    var header = new DataTrigger { Binding = new Binding(nameof(Row.IsHeader)), Value = true };
                    header.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
                    header.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
                    style.Triggers.Add(header);
                    return style;
                }

                private static Style Changed()
                {
                    var style = new Style(typeof(ListBoxItem));
                    var locked = new DataTrigger { Binding = new Binding("IsLocked"), Value = true };
                    locked.Setters.Add(new Setter(UIElement.IsEnabledProperty, false));
                    style.Triggers.Add(locked);
                    return style;
                }
            }
            """;
        CSharpParseOptions options = (CSharpParseOptions)ShellCompilation.Sources[0].Source.Root.SyntaxTree.Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Planted, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);

        string[] unread = UnreadSyntax([((CompilationUnitSyntax)tree.GetRoot(), compilation.GetSemanticModel(tree))], []);

        Assert.Equal(
            [
                "code-setter Planted.Styles.Changed IsEnabled=false when IsLocked=true",
                "code-setter Planted.Styles.Rows IsEnabled=false when IsHeader=true",
                "code-setter Planted.Styles.Twice IsEnabled=false when IsHeader=true",
                "code-setter Planted.Styles.Twice IsEnabled=false when IsHeader=true #2",
            ],
            unread);
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["code-setter Planted.Styles.Rows IsEnabled=false when IsHeader=true"] = "a header is fixed for its item",
        };
        var judge = new WitnessJudge(
            new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), []);
        var none = new SortedDictionary<string, Site>(StringComparer.Ordinal);
        var empty = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] problems = ManifestProblems(none, empty, judge, unread, allowed);
        Assert.Contains(problems, problem => problem.Contains("Twice IsEnabled=false when IsHeader=true #2", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("Changed IsEnabled=false when IsLocked=true", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, problem => problem.Contains("Styles.Rows IsEnabled", StringComparison.Ordinal));
        // The allowed setter's condition changed: the new one fails, and so does the entry.
        string[] changed = ManifestProblems(none, empty, judge, ["code-setter Planted.Styles.Rows IsEnabled=false when IsLocked=true"], allowed);
        Assert.Contains(changed, problem => problem.Contains("Rows IsEnabled=false when IsLocked=true", StringComparison.Ordinal));
        Assert.Contains(changed, problem => problem.Contains("whose syntax is gone", StringComparison.Ordinal) && problem.Contains("when IsHeader=true", StringComparison.Ordinal));
    }

    /// <summary>An IsEnabled setter's trigger, read for its own key: its conditions as
    /// written and the token a keys-first fact must name. One that sets True turns
    /// nothing off.</summary>
    internal sealed record TriggerCondition(string Text, IReadOnlyList<string> Tokens, XElement Trigger, bool SetsTrue);

    private static TriggerCondition ConditionOf(XElement setter)
    {
        bool setsTrue = IsTrue((string?)setter.Attribute("Value"));
        XElement? trigger = setter.Ancestors().FirstOrDefault(ancestor =>
            ancestor.Name.LocalName is "DataTrigger" or "Trigger" or "MultiDataTrigger" or "MultiTrigger");
        if (trigger is null)
        {
            return new TriggerCondition("(unknown)", [], setter, setsTrue);
        }

        IEnumerable<XElement> conditions = trigger.Name.LocalName.StartsWith("Multi", StringComparison.Ordinal)
            ? trigger.Elements().Where(element => element.Name.LocalName.EndsWith(".Conditions", StringComparison.Ordinal))
                .Elements().Where(element => element.Name.LocalName == "Condition")
            : [trigger];
        (string Text, string? Token)[] read = [.. conditions.Select(Describe)];
        return new TriggerCondition(string.Join(" & ", read.Select(part => part.Text)), [.. read.Select(part => part.Token).OfType<string>()], trigger, setsTrue);
    }

    /// <summary>A condition as written: its binding's path (Path= dropped) after its
    /// ElementName, or a property trigger's property after its SourceName, then = and its
    /// value. Its token is the path's last segment that says more than open, focused or
    /// selected; failing that, its ElementName or SourceName.</summary>
    private static (string Text, string? Token) Describe(XElement condition)
    {
        string path;
        string? named;
        if ((string?)condition.Attribute("Binding") is { } binding)
        {
            (path, named) = BindingParts(binding);
        }
        else if (condition.Elements().Any(element => element.Name.LocalName.EndsWith(".Binding", StringComparison.Ordinal)))
        {
            (path, named) = ("(binding)", null);
        }
        else
        {
            (path, named) = ((string?)condition.Attribute("Property") ?? string.Empty, (string?)condition.Attribute("SourceName"));
        }

        string value = (string?)condition.Attribute("Value")
            ?? (condition.Elements().Any(element => element.Name.LocalName.EndsWith(".Value", StringComparison.Ordinal)) ? "(element)" : string.Empty);
        string? token = path == "(binding)"
            ? named
            : path.Split('.').LastOrDefault(segment => segment.Length > 0 && !Untokened.Contains(segment)) ?? named;
        return ($"{(named is null ? string.Empty : named + ".")}{path}={value}", token);
    }

    /// <summary>A binding's path and ElementName, from its markup.</summary>
    private static (string Path, string? ElementName) BindingParts(string markup)
    {
        string inner = markup.Trim();
        inner = inner.StartsWith('{') && inner.EndsWith('}') ? inner[1..^1] : inner;
        int space = inner.IndexOf(' ', StringComparison.Ordinal);
        string path = string.Empty;
        string? element = null;
        foreach (string part in TopLevel(space < 0 ? string.Empty : inner[(space + 1)..], ','))
        {
            string[] named = [.. TopLevel(part, '=')];
            if (named.Length == 1)
            {
                path = part.Trim();
            }
            else if (named[0].Trim() == "Path")
            {
                path = named[1].Trim();
            }
            else if (named[0].Trim() == "ElementName")
            {
                element = named[1].Trim();
            }
        }

        return (path, element);
    }

    /// <summary>A markup string split at <paramref name="separator"/> outside braces.</summary>
    private static IEnumerable<string> TopLevel(string text, char separator)
    {
        int depth = 0;
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            depth += text[index] switch { '{' => 1, '}' => -1, _ => 0 };
            if (depth == 0 && text[index] == separator)
            {
                yield return text[start..index];
                start = index + 1;
            }
        }

        yield return text[start..];
    }

    /// <summary>A style's own IsEnabled setter: False turns the control off whenever no
    /// trigger setting True applies; a markup value can turn it off at any time.</summary>
    private static void ScrapeStyleSetters(SortedDictionary<string, Site> sites, string file, XDocument document, IReadOnlyList<XDocument> all)
    {
        foreach (XElement style in document.Descendants().Where(element => element.Name.LocalName == "Style"))
        {
            foreach (string? value in OwnSetters(style)
                .Where(setter => PropertyName((string?)setter.Attribute("Property")) == "IsEnabled")
                .Select(setter => (string?)setter.Attribute("Value")
                    ?? (setter.Elements().Any(element => element.Name.LocalName == "Setter.Value") ? "{" : null)))
            {
                string? when = IsFalse(value) ? "(style default)" : value?.StartsWith('{') == true ? "(style binding)" : null;
                if (when is not null && CarrierOf(style, null, all) is { } carried)
                {
                    string key = $"xaml-trigger {file} {carried.Identity} IsEnabled when {when}";
                    sites[key] = new Site(key, carried.Element, carried.ResourceKey, null, null, carried.Names, []);
                }
            }
        }
    }

    /// <summary>XAML reads a boolean however it is cased.</summary>
    private static bool IsFalse(string? value) => string.Equals(value?.Trim(), "False", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrue(string? value) => string.Equals(value?.Trim(), "True", StringComparison.OrdinalIgnoreCase);

    /// <summary>A property as XAML may write it, with its owner (UIElement.IsEnabled) or in
    /// parentheses ((UIElement.IsEnabled)), read as its own name (Codex round 1, finding 3).</summary>
    internal static string? PropertyName(string? written) =>
        written?.Trim().TrimStart('(').TrimEnd(')').Split('.').Last().Split(':').Last();

    /// <summary>An element's attribute for a property, however its owner is written.</summary>
    internal static XAttribute? PropertyAttribute(XElement element, string property) =>
        element.Attributes().FirstOrDefault(attribute =>
            attribute.Name.Namespace == XNamespace.None && PropertyName(attribute.Name.LocalName) == property);

    private static IEnumerable<XElement> OwnSetters(XElement style) =>
        style.Elements()
            .SelectMany(child => child.Name.LocalName == "Style.Setters" ? child.Elements() : [child])
            .Where(element => element.Name.LocalName == "Setter");

    private static string? StyleValue(XElement style, string property) =>
        OwnSetters(style).Where(setter => PropertyName((string?)setter.Attribute("Property")) == property)
            .Select(setter => (string?)setter.Attribute("Value")).LastOrDefault();

    /// <summary>Every Command= on an element that can take the keys itself, keyed by its
    /// own name, or by its nearest named holder and its command.</summary>
    private static void ScrapeXamlCommands(SortedDictionary<string, Site> sites, string file, XDocument document, IReadOnlyList<XDocument> all)
    {
        foreach (XElement element in document.Descendants().Where(element => IsACommandSite(element, all)))
        {
            string command = CommandName(PropertyAttribute(element, "Command")!.Value);
            string? own = Named(element);
            string? holder = own ?? element.Ancestors().Select(Named).FirstOrDefault(name => name is not null);
            string key = $"xaml-command {file} {(own is null ? CommandIdentity(element, all) : $"{own} {command}")}";
            sites[key] = new Site(key, element, null, null, null, holder is null ? [] : [holder], own is null ? [command] : null, command);
        }
    }

    private static bool IsACommandSite(XElement element, IReadOnlyList<XDocument> all) =>
        PropertyAttribute(element, "Command") is not null && TakesTheKeysItself(element) && !IsAMenuRow(element, all);

    /// <summary>An unnamed command element's identity: its nearest named holder and its
    /// command, with " #2" (and on) for a later one of the same holder and command.</summary>
    private static string CommandIdentity(XElement element, IReadOnlyList<XDocument> all)
    {
        string command = CommandName(PropertyAttribute(element, "Command")!.Value);
        string? holder = element.Ancestors().Select(Named).FirstOrDefault(name => name is not null);
        int ordinal = 1 + (element.Document?.Descendants() ?? [])
            .TakeWhile(other => !ReferenceEquals(other, element))
            .Count(other => PropertyAttribute(other, "Command") is not null
                && Named(other) is null
                && CommandName(PropertyAttribute(other, "Command")!.Value) == command
                && other.Ancestors().Select(Named).FirstOrDefault(name => name is not null) == holder
                && IsACommandSite(other, all));
        return $"{holder ?? "the window"} {command}{(ordinal > 1 ? $" #{ordinal}" : string.Empty)}";
    }

    private static bool TakesTheKeysItself(XElement element) =>
        element.Name == PresentationNamespace + "Hyperlink"
            ? !IsFalse(PropertyAttribute(element, "Focusable")?.Value)
            : RegionBoundaryCensus.IsFocusable(element);

    /// <summary>A menu row: a MenuItem in a Menu or a ContextMenu that does not stay
    /// open on click. WPF closes its menu and gives the keys back before its command runs
    /// (MenuRowCommandTests).</summary>
    private static bool IsAMenuRow(XElement element, IReadOnlyList<XDocument> all) =>
        RegionBoundaryCensus.Resolve(element) is { } type
        && typeof(MenuItem).IsAssignableFrom(type)
        && element.Ancestors().Any(ancestor => RegionBoundaryCensus.Resolve(ancestor) is { } menu && typeof(MenuBase).IsAssignableFrom(menu))
        && !StaysOpenOnClick(element, all);

    /// <summary>Whether a menu item stays open on click, or may: its own attribute, else the
    /// style it is given (inline or keyed), else its menu's item container style, else an
    /// implicit style of its type, each read through the styles it is based on. A bound
    /// value, a trigger that sets it, or a style or base that cannot be found is unknown, and
    /// an unknown row stays a site (CB-17a review, low 6).</summary>
    private static bool StaysOpenOnClick(XElement element, IReadOnlyList<XDocument> all)
    {
        if (PropertyAttribute(element, "StaysOpenOnClick")?.Value is { } own)
        {
            return !IsFalse(own);
        }

        XElement[] styles = [.. all.SelectMany(document => document.Descendants()).Where(candidate => candidate.Name.LocalName == "Style")];
        XElement[] inline = [.. element.Elements().Where(child => child.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)).Elements()];
        if (inline.Length > 0)
        {
            return inline.Any(style => StaysOpen(style, styles, 0) != false);
        }

        if ((string?)element.Attribute("Style") is { } reference)
        {
            return Keyed(reference, styles) is not { } style || StaysOpen(style, styles, 0) != false;
        }

        if (element.Parent is { } parent && !parent.Name.LocalName.Contains('.', StringComparison.Ordinal))
        {
            XElement[] contained = [.. parent.Elements().Where(child => child.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal)).Elements()];
            if (contained.Length > 0)
            {
                return contained.Any(style => StaysOpen(style, styles, 0) != false);
            }

            if ((string?)parent.Attribute("ItemContainerStyle") is { } containerReference)
            {
                return Keyed(containerReference, styles) is not { } style || StaysOpen(style, styles, 0) != false;
            }
        }

        return Implicit(element.Name.LocalName, styles).Any(style => StaysOpen(style, styles, 0) != false);
    }

    /// <summary>What a style says of StaysOpenOnClick: true, false, or null when it cannot be
    /// read (bound, set by a trigger, or based on a style that cannot be found).</summary>
    private static bool? StaysOpen(XElement style, XElement[] styles, int depth)
    {
        XElement[] set = [.. OwnSetters(style).Where(setter => PropertyName((string?)setter.Attribute("Property")) == "StaysOpenOnClick")];
        if (depth > 8
            || style.Descendants().Any(element => element.Name.LocalName == "Setter"
                && PropertyName((string?)element.Attribute("Property")) == "StaysOpenOnClick"
                && element.Ancestors().Any(ancestor => ancestor.Name.LocalName.EndsWith(".Triggers", StringComparison.Ordinal))))
        {
            return null;
        }

        if (set.Length > 0)
        {
            string? value = (string?)set[^1].Attribute("Value");
            return IsTrue(value) ? true : IsFalse(value) ? false : null;
        }

        if ((string?)style.Attribute("BasedOn") is not { } basedOn)
        {
            return false;
        }

        if (basedOn.Contains("x:Type", StringComparison.Ordinal))
        {
            string type = basedOn.Trim().TrimStart('{').TrimEnd('}').Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
            XElement[] bases = [.. Implicit(type.Split(':').Last(), styles).Where(candidate => !ReferenceEquals(candidate, style))];
            return bases.Length == 0 ? false : bases.Select(candidate => StaysOpen(candidate, styles, depth + 1)).Aggregate(Either);
        }

        return Keyed(basedOn, styles) is { } based ? StaysOpen(based, styles, depth + 1) : null;
    }

    private static bool? Either(bool? first, bool? second) => first == true || second == true ? true : first is null || second is null ? null : false;

    private static XElement? Keyed(string reference, XElement[] styles) =>
        ResourceKeyOf(reference) is { } key ? styles.FirstOrDefault(style => (string?)style.Attribute(Xaml + "Key") == key) : null;

    /// <summary>The implicit styles of an element type: unkeyed styles in a resource
    /// dictionary whose TargetType names the type.</summary>
    private static IEnumerable<XElement> Implicit(string type, XElement[] styles) =>
        styles.Where(style => style.Attribute(Xaml + "Key") is null
            && (style.Parent?.Name.LocalName == "ResourceDictionary" || style.Parent?.Name.LocalName.EndsWith(".Resources", StringComparison.Ordinal) == true)
            && (string?)style.Attribute("TargetType") is { } target
            && target.Trim().TrimStart('{').TrimEnd('}').Replace("x:Type", string.Empty, StringComparison.Ordinal).Trim().Split(':').Last() == type);

    private static string? ResourceKeyOf(string reference) =>
        reference.Trim().TrimStart('{').TrimEnd('}').Split(' ', StringSplitOptions.RemoveEmptyEntries) is [_, var key] ? key : null;

    /// <summary>A command's name: its binding path's last segment, or an x:Static's
    /// member.</summary>
    private static string CommandName(string markup)
    {
        string path = markup.StartsWith("{Binding", StringComparison.Ordinal) || markup.StartsWith("{TemplateBinding", StringComparison.Ordinal)
            ? BindingParts(markup).Path
            : markup.Trim().TrimStart('{').TrimEnd('}').Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
        string last = path.Split('.', ':').Last();
        return last.Length > 0 ? last : "(context)";
    }

    /// <summary>A C# write of a button's or a hyperlink's command, or a menu item's that
    /// its type keeps open on click: an assignment or an initializer, a constructor's too.
    /// A write of null, and a write to a named XAML element out of the keys' reach, are
    /// not sites.</summary>
    private static bool ScrapeACommandWrite(
        SortedDictionary<string, Site> sites, SemanticModel model, AssignmentExpressionSyntax assignment, IReadOnlySet<(string Name, string? Type)> focusless)
    {
        if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol { Name: "Command" } property
            || !TakesACommand(property.ContainingType))
        {
            return false;
        }

        (string receiver, ITypeSymbol? declared) = assignment.Left is MemberAccessExpressionSyntax access
            ? (access.Expression.ToString(), model.GetTypeInfo(access.Expression).Type)
            : assignment.Parent is InitializerExpressionSyntax { Parent: { } made } initializer
                ? (InitializedReceiver(initializer), model.GetTypeInfo(made).Type)
                : ("this", model.GetEnclosingSymbol(assignment.SpanStart)?.ContainingType);
        if (!IsNull(assignment.Right) && IsACommandSite(assignment, property.ContainingType, receiver, declared, focusless))
        {
            Add(sites, model, assignment, "command", receiver, property: null, constructorWrite: true);
        }

        return true;
    }

    private static bool ScrapeACommandCall(
        SortedDictionary<string, Site> sites, SemanticModel model, InvocationExpressionSyntax call, MemberAccessExpressionSyntax member, IReadOnlySet<(string Name, string? Type)> focusless)
    {
        bool operations = member.Expression.ToString() == "BindingOperations";
        int at = operations ? 1 : 0;
        SeparatedSyntaxList<ArgumentSyntax> arguments = call.ArgumentList.Arguments;
        if (member.Name.Identifier.ValueText is not ("SetValue" or "SetCurrentValue" or "SetBinding")
            || arguments.Count <= at
            || model.GetSymbolInfo(arguments[at].Expression).Symbol is not IFieldSymbol { Name: "CommandProperty" } field
            || !TakesACommand(field.ContainingType))
        {
            return false;
        }

        ExpressionSyntax written = operations ? arguments[0].Expression : member.Expression;
        string receiver = written.ToString();
        if (!(arguments.Count > at + 1 && IsNull(arguments[at + 1].Expression))
            && IsACommandSite(call, field.ContainingType, receiver, model.GetTypeInfo(written).Type, focusless))
        {
            Add(sites, model, call, "command", receiver, property: null, constructorWrite: true);
        }

        return true;
    }

    private static bool TakesACommand(ITypeSymbol? type) =>
        Derives(type, "System.Windows.Controls.Primitives.ButtonBase")
        || Derives(type, "System.Windows.Documents.Hyperlink")
        || Derives(type, "System.Windows.Controls.MenuItem");

    private static readonly SymbolDisplayFormat TypeName = new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <summary>A command write that is a site: not to a focusless XAML element (one of its
    /// name and its type), and not to a menu row unless its type keeps it open.</summary>
    private static bool IsACommandSite(
        SyntaxNode node, ITypeSymbol owner, string receiver, ITypeSymbol? declared, IReadOnlySet<(string Name, string? Type)> focusless) =>
        !(declared is not null && focusless.Contains((receiver.TrimEnd('!'), declared.ToDisplayString(TypeName))))
        && (!Derives(owner, "System.Windows.Controls.MenuItem") || StaysOpenIn(node, receiver));

    /// <summary>Whether the type writing a menu item's command also keeps that item open
    /// on click.</summary>
    private static bool StaysOpenIn(SyntaxNode node, string receiver) =>
        node.Ancestors().OfType<TypeDeclarationSyntax>().LastOrDefault() is { } type
        && type.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any(assignment =>
            assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
            && (assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "StaysOpenOnClick" } access
                    && access.Expression.ToString() == receiver
                || assignment.Left is IdentifierNameSyntax { Identifier.ValueText: "StaysOpenOnClick" }
                    && assignment.Parent is InitializerExpressionSyntax initializer
                    && InitializedReceiver(initializer) == receiver));

    /// <summary>The receiver an object initializer builds: what the new object is
    /// assigned to.</summary>
    private static string InitializedReceiver(InitializerExpressionSyntax initializer) =>
        initializer.Parent?.Parent switch
        {
            AssignmentExpressionSyntax outer => outer.Left.ToString(),
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } => declarator.Identifier.ValueText,
            EqualsValueClauseSyntax { Parent: PropertyDeclarationSyntax declared } => declared.Identifier.ValueText,
            _ => "new",
        };

    private static bool IsNull(ExpressionSyntax expression) =>
        expression is PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed
            ? IsNull(suppressed.Operand)
            : expression.IsKind(SyntaxKind.NullLiteralExpression) || expression.IsKind(SyntaxKind.DefaultLiteralExpression);

    /// <summary>A constructor's binding of IsEnabled lives on and can turn the control
    /// off later, unlike a constructor's plain write.</summary>
    private static bool BindsAState(string call, string property) => call == "SetBinding" && property == "IsEnabled";

    /// <summary>#1866 (CD11): the witnesses of a site that can turn a control off, and the
    /// keys-first and stays-on kinds wherever they are written. A keys-first or stays-on
    /// fact that can skip at run time witnesses only one of the menu's seventeen rows, written
    /// with <see cref="MenuMarked"/> (m1).</summary>
    internal sealed class KeysFirstRule(
        IReadOnlyDictionary<string, string> facts,
        IReadOnlyList<XDocument> documents,
        Compilation? commands,
        SkipReach? skips)
    {
        private const string KeysFirst = "keys-first: ";
        private const string StaysOn = "stays-on: ";
        private const string AlwaysOn = "exempt: always on: ";
        private const string OwnPress = "exempt: its own press never turns it off: ";
        private const string NeverFires = "exempt: never fires: ";
        private CommandConstancy? _constancy;

        /// <summary>True when this rule rules on the witness, with its verdict (null when
        /// it holds).</summary>
        public bool Judges(Site site, string witness, out string? verdict)
        {
            bool disables = IsDisableSite(site.Key);
            bool menu = WhereADesktopGivesAMenuTheKeys.Contains(site.Key, StringComparer.Ordinal);
            verdict = null;
            if (witness.StartsWith(MenuMarked, StringComparison.Ordinal))
            {
                verdict = menu
                    ? KeysFirstVerdict(site, witness[MenuMarked.Length..].Trim(), "AssertTheKeysLeaveFirst", arguments: null, maySkip: true)
                    : $"only the menu's seventeen rows ({nameof(WhereADesktopGivesAMenuTheKeys)}) are written '{MenuMarked}Class.Method': "
                        + "every other keys-first fact must run on every lane, so it cannot skip";
            }
            else if (menu)
            {
                verdict = $"one of the menu's seventeen rows ({nameof(WhereADesktopGivesAMenuTheKeys)}), whose facts skip where a desktop "
                    + $"refuses a menu the keys: it is written '{MenuMarked}Class.Method'";
            }
            else if (witness.StartsWith(KeysFirst, StringComparison.Ordinal))
            {
                verdict = KeysFirstVerdict(site, witness[KeysFirst.Length..].Trim(), "AssertTheKeysLeaveFirst", arguments: null, maySkip: false);
            }
            else if (witness.StartsWith(StaysOn, StringComparison.Ordinal))
            {
                verdict = disables
                    ? KeysFirstVerdict(site, witness[StaysOn.Length..].Trim(), "AssertItStaysOn", arguments: 4, maySkip: false)
                    : "stays-on is only for a site that can turn a control off with the keys on it";
            }
            else if (!disables)
            {
                return false;
            }
            else if (witness == "exempt: not focusable")
            {
                verdict = OutOfReach(site);
            }
            else if (witness.StartsWith(AlwaysOn, StringComparison.Ordinal))
            {
                verdict = commands is null
                    ? "no compilation to read the command in"
                    : (_constancy ??= new CommandConstancy(commands)).Verdict(site, witness[AlwaysOn.Length..].Trim());
            }
            else if (witness.StartsWith(OwnPress, StringComparison.Ordinal))
            {
                string why = witness[OwnPress.Length..].Trim();
                verdict = why.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 4
                    ? "say what its command or trigger reads, and that its own press does not change it"
                    : Uncited(why);
            }
            else if (witness.StartsWith(NeverFires, StringComparison.Ordinal))
            {
                Match cited = Regex.Match(witness[NeverFires.Length..], @"^(?<why>.*\S)\s+\((?<fact>[A-Z]\w*Tests\.[A-Z]\w*)\)$");
                verdict = !site.Key.StartsWith("xaml-trigger ", StringComparison.Ordinal) ? "only a trigger can never fire"
                    : !cited.Success ? "never fires says why, then cites the fact that shows it: (SomethingTests.Method)"
                    : Uncited(cited.Groups["fact"].Value);
            }
            else
            {
                verdict = "a site that can turn a control off with the keys on it (#1866, CD11) takes 'keys-first: Class.Method', "
                    + "'stays-on: Class.Method', 'exempt: not focusable', 'exempt: always on: Type.Property', 'exempt: its own press never turns it off: <why>' "
                    + "or 'exempt: never fires: <why> (Class.Method)'";
            }

            return true;
        }

        /// <summary>A keys-first or stays-on witness: the fact exists, its code names the
        /// site (and a trigger's condition token or an unnamed button's command), its own
        /// method calls <paramref name="recorder"/> on KeysFirstRecorder, with
        /// <paramref name="arguments"/> arguments when that is given (stays-on's fourth is
        /// the predicate that says the driver ran), it has no skipped or explicit data row,
        /// and, unless <paramref name="maySkip"/>, nothing it runs can skip it (m1).</summary>
        private string? KeysFirstVerdict(Site site, string fact, string recorder, int? arguments, bool maySkip)
        {
            if (!facts.TryGetValue(fact, out string? source))
            {
                return $"'{fact}' names no fact in this project";
            }

            HashSet<string> tokens = WitnessJudge.CodeTokens(WithoutLocalFunctions(source));
            return !site.Names.Any(name => name.Length > 2 && tokens.Contains(name))
                ? $"the fact's code does not name the site ({string.Join(", ", site.Names)})"
                : site.Tokens is { Count: > 0 } wanted && !wanted.Any(tokens.Contains)
                    ? $"the fact's code does not name the trigger's condition or the button's command ({string.Join(", ", wanted)})"
                    : !CallsTheRecorder(source, recorder, arguments)
                        ? arguments is { } count
                            ? $"the fact's own code never calls KeysFirstRecorder.{recorder} with its {count} arguments (what, root, act, and reached: the driver that used to turn the control off ran)"
                            : $"the fact's own code never calls KeysFirstRecorder.{recorder}"
                    : skips?.SkippedRow(fact) is { } row
                        ? $"the fact is a theory with a skipped or explicit row, {row}, which witnesses nothing where it does not run: refused outright (m1)"
                    : !maySkip && skips?.Skips(fact) is { } path
                        ? $"the fact can skip at run time ({path}), and where it skips the row is witnessed by nothing: only the menu's seventeen rows "
                            + $"({nameof(WhereADesktopGivesAMenuTheKeys)}) may lean on such a fact, written '{MenuMarked}Class.Method' (m1)"
                    : null;
        }

        /// <summary>An invocation of KeysFirstRecorder.<paramref name="recorder"/>, qualified so,
        /// in the fact's own method: not a nameof, a method group, a string, a look-alike of
        /// the fact's own class, or a call inside a helper. It runs when the fact does: not in
        /// a local function, which runs only if called, and every lambda around it is handed
        /// straight to a named runner that runs it before returning, not kept in a variable or
        /// handed to a delegate, a Lazy, a thread, a task or any other consumer (Codex rounds 1
        /// and 2). With <paramref name="arguments"/>, the call passes exactly that many.</summary>
        private static bool CallsTheRecorder(string source, string recorder, int? arguments) =>
            SyntaxFactory.ParseMemberDeclaration(source) is { } method
            && method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                call.Expression is MemberAccessExpressionSyntax access
                && access.Name.Identifier.ValueText == recorder
                && (arguments is not { } count
                    || (call.ArgumentList.Arguments.Count == count && !IsAConstant(call.ArgumentList.Arguments[count - 1].Expression)))
                && access.Expression switch
                {
                    IdentifierNameSyntax { Identifier.ValueText: "KeysFirstRecorder" } => true,
                    MemberAccessExpressionSyntax { Name.Identifier.ValueText: "KeysFirstRecorder" } => true,
                    _ => false,
                }
                && call.Ancestors().TakeWhile(ancestor => ancestor != method).All(ancestor => ancestor switch
                {
                    LocalFunctionStatementSyntax => false,
                    AnonymousFunctionExpressionSyntax lambda => lambda.Parent is ArgumentSyntax
                    {
                        Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax { Expression: var runner } },
                    } && IsARunner(runner),
                    _ => true,
                }));

        /// <summary>A predicate that answers a constant (<c>() => true</c>, <c>() => { return
        /// true; }</c>, <c>() => !false</c>, <c>() => 1 == 1</c>): its answer reads no name at
        /// all, so as stays-on's reached it says nothing about the driver having run (red team
        /// F7; the merge judge's nit 5).</summary>
        private static bool IsAConstant(ExpressionSyntax predicate)
        {
            static ExpressionSyntax Bare(ExpressionSyntax expression) =>
                expression is ParenthesizedExpressionSyntax parenthesized ? Bare(parenthesized.Expression) : expression;
            static bool ReadsNothing(ExpressionSyntax answer) =>
                !answer.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any();

            return Bare(predicate) is LambdaExpressionSyntax lambda
                && (lambda.ExpressionBody is { } body
                    ? ReadsNothing(body)
                    : lambda.Block?.Statements is [ReturnStatementSyntax { Expression: { } returned }] && ReadsNothing(returned));
        }

        /// <summary>The runners a keys-first fact may hand its body to, each of which runs the
        /// lambda before it returns: RunSta (every keys-first witness today: the four
        /// CommandPaletteTests overlay and launcher facts, EmptyPaneLauncherTests,
        /// SidebarTreeKeysTests and FindInNoteBarTests), PumpedDispatcher.Run and a
        /// dispatcher's Invoke. No other consumer counts.</summary>
        private static bool IsARunner(ExpressionSyntax runner) => runner switch
        {
            IdentifierNameSyntax { Identifier.ValueText: "RunSta" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "RunSta" } => true,
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Run", Expression: var owner } => LastName(owner) == "PumpedDispatcher",
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Invoke", Expression: var owner } => LastName(owner) == "Dispatcher",
            _ => false,
        };

        private static string? LastName(ExpressionSyntax expression) => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null,
        };

        /// <summary>A fact's source without its local functions, whose code runs only if
        /// called: they do not name a site for it.</summary>
        private static string WithoutLocalFunctions(string source) =>
            SyntaxFactory.ParseMemberDeclaration(source) is { } method
                ? method.RemoveNodes(method.DescendantNodes().OfType<LocalFunctionStatementSyntax>(), SyntaxRemoveOptions.KeepNoTrivia)?.ToFullString() ?? source
                : source;

        private string? Uncited(string text) =>
            Regex.Matches(text, @"\b[A-Z]\w*Tests\.[A-Z]\w*\b").Select(match => match.Value)
                .FirstOrDefault(cited => !facts.ContainsKey(cited)) is { } missing
                ? $"the exemption cites '{missing}', which names no fact in this project"
                : null;

        /// <summary>Whether the site's control is out of the keys' reach: declared or styled
        /// not focusable, and holding nothing that can take them.</summary>
        private string? OutOfReach(Site site)
        {
            XElement[] all = [.. documents.SelectMany(document => document.Descendants())];
            if (site.ResourceKey is { } key)
            {
                XElement[] styles = [.. all.Where(element => element.Name.LocalName == "Style" && (string?)element.Attribute(Xaml + "Key") == key)];
                XElement? open = all.FirstOrDefault(user => user.Attributes().Any(attribute => attribute.Value.Contains($"Resource {key}}}", StringComparison.Ordinal))
                    && (user.Name.LocalName == "Style" ? IsTrue(StyleValue(user, "Focusable")) : !Unreachable(user, all)));
                return styles.Length == 0 || !styles.All(style => IsFalse(StyleValue(style, "Focusable")))
                    ? $"the style {key} does not set Focusable False"
                    : open is not null ? $"{Identity(open)} uses {key} and can take the keys" : null;
            }

            XElement? element = site.Element
                ?? (site.Member is { } member ? all.FirstOrDefault(candidate => (string?)candidate.Attribute(Xaml + "Name") == member.TrimEnd('!')) : null);
            return element is null ? "nothing in the XAML shows the site's control out of the keys' reach"
                : Unreachable(element, all) ? null
                : $"{Identity(element)} can take the keys, or holds what can";
        }

        private static bool Unreachable(XElement element, XElement[] all)
        {
            string? keyed = (string?)element.Attribute("Style") is { } reference ? ResourceKeyOf(reference) : null;
            bool declared = PropertyAttribute(element, "Focusable")?.Value switch
            {
                { } own when IsFalse(own) => true,
                null => element.Elements().Where(child => child.Name.LocalName.EndsWith(".Style", StringComparison.Ordinal)).Elements()
                    .Concat(all.Where(style => keyed is not null && style.Name.LocalName == "Style" && (string?)style.Attribute(Xaml + "Key") == keyed))
                    .Any(style => IsFalse(StyleValue(style, "Focusable"))),
                _ => false,
            };
            Type? type = RegionBoundaryCensus.Resolve(element);
            return declared
                && !(type is not null && (typeof(ItemsControl).IsAssignableFrom(type) || type.Assembly == typeof(MainWindow).Assembly && typeof(Control).IsAssignableFrom(type)))
                && !(type is not null && typeof(ContentControl).IsAssignableFrom(type) && PropertyAttribute(element, "Content")?.Value is { } content && content.StartsWith('{'))
                && !element.Elements()
                    .SelectMany(child => !child.Name.LocalName.Contains('.', StringComparison.Ordinal) ? [child]
                        : child.Name.LocalName.EndsWith(".Content", StringComparison.Ordinal) || child.Name.LocalName.EndsWith(".Child", StringComparison.Ordinal) ? child.Elements()
                        : [])
                    .Any(RegionBoundaryCensus.HoldsAStop);
        }
    }

    /// <summary>#1866 (CD11): whether a command is always on, read from every construction
    /// that reaches every property of its name in the shell, since a binding's data context
    /// is not resolved. Always on is a RelayCommand whose canExecute is a lambda of the
    /// literal true, or an AsyncRelayCommand of the same that admits while executing.
    /// Interface members are skipped; a record's parameter, or anything the reading cannot
    /// follow, is not always on.</summary>
    internal sealed class CommandConstancy(Compilation compilation)
    {
        private readonly Dictionary<SyntaxTree, SemanticModel> _models = [];
        private readonly Dictionary<ISymbol, string?> _verdicts = new(SymbolEqualityComparer.Default);
        private readonly HashSet<ISymbol> _reading = new(SymbolEqualityComparer.Default);
        private INamedTypeSymbol[]? _types;
        private Dictionary<string, List<AssignmentExpressionSyntax>>? _assignments;

        public string? Verdict(Site site, string named)
        {
            int dot = named.LastIndexOf('.');
            string type = dot > 0 ? named[..dot] : string.Empty;
            string property = named[(dot + 1)..];
            INamedTypeSymbol[] declared = [.. Types().Where(candidate => candidate.Name == type && candidate.TypeKind != TypeKind.Interface)];
            if (site.Command != property)
            {
                return $"the site runs {site.Command ?? "no bound command"}, not {property}";
            }

            if (declared.Length != 1 || !declared[0].GetMembers(property).OfType<IPropertySymbol>().Any())
            {
                return $"'{named}' names no one shell class's property";
            }

            return Types().Where(candidate => candidate.TypeKind != TypeKind.Interface)
                .SelectMany(candidate => candidate.GetMembers(property).OfType<IPropertySymbol>())
                .Select(same => Unsteady(same) is { } why ? $"{same.ContainingType.Name}.{property} {why}" : null)
                .FirstOrDefault(why => why is not null);
        }

        private INamedTypeSymbol[] Types() =>
            _types ??= [.. compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>()];

        /// <summary>One semantic model per tree (a model per lookup made the main fact
        /// slow, CB-17a review m1).</summary>
        private SemanticModel Model(SyntaxNode node)
        {
            if (!_models.TryGetValue(node.SyntaxTree, out SemanticModel? model))
            {
                model = compilation.GetSemanticModel(node.SyntaxTree);
                _models[node.SyntaxTree] = model;
            }

            return model;
        }

        /// <summary>A property's verdict, read once.</summary>
        private string? Unsteady(IPropertySymbol property)
        {
            if (_verdicts.TryGetValue(property, out string? known))
            {
                return known;
            }

            if (!_reading.Add(property))
            {
                return "forwards to itself, which the reading cannot follow";
            }

            try
            {
                string? verdict = Read(property);
                _verdicts[property] = verdict;
                return verdict;
            }
            finally
            {
                _ = _reading.Remove(property);
            }
        }

        /// <summary>Null when every construction reaching <paramref name="property"/> is
        /// always on, else why not.</summary>
        private string? Read(IPropertySymbol property)
        {
            if (property.DeclaringSyntaxReferences.IsEmpty)
            {
                return "is not a property the reading can follow";
            }

            if (property.SetMethod is { DeclaredAccessibility: not Microsoft.CodeAnalysis.Accessibility.Private } && property.SetMethod.DeclaringSyntaxReferences.Length > 0)
            {
                return "can be set by another type";
            }

            var built = new List<ExpressionSyntax>();
            bool forwarded = false;
            foreach (SyntaxNode node in property.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()))
            {
                if (node is not PropertyDeclarationSyntax declaration)
                {
                    return "is not a property the reading can follow (a record's parameter)";
                }

                if (declaration.Initializer is { } initializer)
                {
                    built.Add(initializer.Value);
                }

                AccessorDeclarationSyntax? getter = declaration.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
                ExpressionSyntax? body = declaration.ExpressionBody?.Expression
                    ?? getter?.ExpressionBody?.Expression
                    ?? (getter?.Body?.Statements is [ReturnStatementSyntax { Expression: { } returned }] ? returned : null);
                if (body is null && getter?.Body is not null)
                {
                    return "has a getter the reading cannot follow";
                }

                if (body is not null && Follow(Unwrapped(body), built, ref forwarded) is { } why)
                {
                    return why;
                }
            }

            built.AddRange(Assigned(property));
            ExpressionSyntax[] made = [.. built.Select(Unwrapped).Where(expression => !IsNull(expression))];
            return made.Length == 0 && !forwarded ? "has no construction the reading can find"
                : made.FirstOrDefault(construction => !IsAlwaysOn(construction)) is { } off
                    ? $"is built by '{Regex.Replace(off.ToString(), @"\s+", " ")}', which can turn it off"
                : null;
        }

        /// <summary>A getter's expression: a construction, a lazy field (??=), a field, or
        /// another command property it forwards to.</summary>
        private string? Follow(ExpressionSyntax body, List<ExpressionSyntax> built, ref bool forwarded)
        {
            if (body is BaseObjectCreationExpressionSyntax)
            {
                built.Add(body);
                return null;
            }

            ExpressionSyntax read = body is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceAssignmentExpression } lazy ? lazy.Left : body;
            if (read != body)
            {
                built.Add(((AssignmentExpressionSyntax)body).Right);
            }

            switch (Model(read).GetSymbolInfo(read).Symbol)
            {
                case IFieldSymbol { DeclaredAccessibility: Microsoft.CodeAnalysis.Accessibility.Private } field:
                    built.AddRange(field.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                        .OfType<VariableDeclaratorSyntax>().Select(declarator => declarator.Initializer?.Value).OfType<ExpressionSyntax>());
                    built.AddRange(Assigned(field));
                    return null;
                case IPropertySymbol other when read == body:
                    forwarded = true;
                    return Unsteady(other);
                default:
                    return $"returns '{body}', which the reading cannot follow";
            }
        }

        /// <summary>Every value assigned to <paramref name="symbol"/> in its type. The shell's
        /// assignments are indexed once by the name they write, and only those of the
        /// symbol's name, inside its type, are bound.</summary>
        private IEnumerable<ExpressionSyntax> Assigned(ISymbol symbol)
        {
            _assignments ??= compilation.SyntaxTrees
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
                .GroupBy(assignment => assignment.Left switch
                {
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                    SimpleNameSyntax simple => simple.Identifier.ValueText,
                    _ => string.Empty,
                })
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            SyntaxReference[] owners = [.. symbol.ContainingType.DeclaringSyntaxReferences];
            return _assignments.GetValueOrDefault(symbol.Name, [])
                .Where(assignment => owners.Any(owner => owner.SyntaxTree == assignment.SyntaxTree && owner.Span.Contains(assignment.Span)))
                .Where(assignment => SymbolEqualityComparer.Default.Equals(Model(assignment).GetSymbolInfo(assignment.Left).Symbol, symbol))
                .Select(assignment => assignment.Right);
        }

        private bool IsAlwaysOn(ExpressionSyntax construction)
        {
            if (construction is not BaseObjectCreationExpressionSyntax created
                || Model(created).GetSymbolInfo(created).Symbol is not IMethodSymbol constructor)
            {
                return false;
            }

            string made = constructor.ContainingType.ToDisplayString();
            bool admits = made == "SlateWindows.RelayCommand"
                || (made == "SlateWindows.AsyncRelayCommand"
                    && Argument(created, constructor, "admitWhileExecuting") is { } admit
                    && admit.IsKind(SyntaxKind.TrueLiteralExpression));
            return admits
                && Argument(created, constructor, "canExecute") is LambdaExpressionSyntax { ExpressionBody: { } returns }
                && returns.IsKind(SyntaxKind.TrueLiteralExpression);
        }

        private static ExpressionSyntax? Argument(BaseObjectCreationExpressionSyntax created, IMethodSymbol constructor, string name)
        {
            SeparatedSyntaxList<ArgumentSyntax> arguments = created.ArgumentList?.Arguments ?? default;
            for (int index = 0; index < arguments.Count; index++)
            {
                string? parameter = arguments[index].NameColon?.Name.Identifier.ValueText
                    ?? (index < constructor.Parameters.Length ? constructor.Parameters[index].Name : null);
                if (parameter == name)
                {
                    return arguments[index].Expression;
                }
            }

            return null;
        }

        private static ExpressionSyntax Unwrapped(ExpressionSyntax expression) =>
            expression switch
            {
                ParenthesizedExpressionSyntax inner => Unwrapped(inner.Expression),
                PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } suppressed => Unwrapped(suppressed.Operand),
                _ => expression,
            };
    }
}
