// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-9a (#1889; owner decision CD5, 2026-11-16). A route that rewrites
// a note, or reloads a tab on it, waits for every save and operation admitted
// for that note before it, and a save asked for after it waits for its
// publication (contract 38 D-10; WB-2, #1885, #1888). This census holds every
// such route in the Windows shell to the note's save chain, and names each
// route that still writes outside it with a reason, a date and what ends it,
// so no new one arrives unseen.
//
// It BINDS (ShellCompilation); it never reads spellings. Each rule is a pure
// function of a reading of bound trees, so the planted theory feeds it source.
//
// How a call is shown admitted. The shell's calls between members are read
// once. A node is a method, a constructor, an accessor, or the initialiser of
// a field or a property. A lambda or a local function belongs to the
// innermost admission argument or member that holds it, and an admission
// argument is a node of its own, admitted whatever holds it. Nodes and edges
// are named by the symbol's original definition, so a generic method is one
// node however it is constructed, and a reduced extension call is taken back
// to the method it calls. An
// admission call is a bound call to WorkspaceSaveCoordinator.Admit or Enqueue,
// to INoteSaveChain.Admit, or to WorkspaceViewModel.AdmitPropertyWrite, and
// its admission argument is the argument for `operation` (`start` for
// Enqueue), never `abandoned`. An edge from A to B is a call of B, a creation
// through B, or a reference to B as a method group, in A. The admitted set is
// the least set that holds every admission argument and every node that has a
// caller and whose callers are all in the set. A call is admitted when either
// holds:
//   (A) its node is in the admitted set. A node that implements an interface
//       member (implicitly or explicitly), that overrides, or that is virtual
//       or abstract is never admitted this way: a call through the interface
//       or the base binds to another symbol, so the graph does not hold all
//       of its callers. Nor is a method shaped like an event handler (a
//       sender and arguments derived from EventArgs): XAML wires such a
//       method in generated code this census does not read; or
//   (B) it stands in a lambda assigned to a local, and every read of that
//       local in the member stands in an admission argument (the property
//       write's shape: the write is built first and handed to the admitted
//       operation).
//
// What the census does not prove. That the path admitted is the path
// written. That the worker is handed to TrackWorker. That the ticket settles
// on every path. That a route re-checks its gates at its turn. The facts in
// SaveChainAdmissionTests answer those four for each route. The rest have no
// fact and are for review. A call through a delegate kept in a field or a
// property, or through any interface other than INoteSaveChain. That a lambda
// or a method group created inside an admitted node runs before the operation
// ends: one kept and run later counts as admitted and is not. For rule 3,
// that the answer of HasAdmittedSaveFor gates the reload, or that it was
// asked about the path that is reloaded: the rule sees only a call standing
// before the reload. That a method does what its name says: every table
// classifies by name. A write made through reflection or a dynamic call, and
// a direct System.IO write to a vault note, are not seen. A method XAML
// wires that has no event handler's shape is not seen as wired. A call
// inside an
// accessor or an operator has no caller here, so it is never admitted, and
// the rule names it.

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "save-chain-admission")]
public sealed class SaveChainAdmissionCensus
{
    // ---- The tables -------------------------------------------------------

    /// <summary>The session methods that rewrite a note's bytes, each with a
    /// word of what it writes. A call to one is a site of rule 2.</summary>
    private static readonly (string Name, string Writes)[] NoteWriters =
    [
        ("SaveText", "a file's whole text"),
        ("SaveTextChecked", "a file's whole text, checked against its identity"),
        ("SaveComposed", "a note's body under its frontmatter"),
        ("SetFrontmatterSource", "a note's frontmatter"),
        ("RestoreVersion", "a note's text from its history"),
        ("ToggleTaskStatus", "one task's checkbox"),
        ("SetProperty", "one property"),
        ("DeleteProperty", "one property"),
        ("SetPropertyByIdentity", "one property"),
        ("DeletePropertyByIdentity", "one property"),
        ("RenamePropertyAcrossVault", "a property's key in every note that has it"),
        ("RenamePropertyByIdentityAcrossVault", "a property's key in every note that has it"),
        ("AddTagToFiles", "a tag in each named note"),
        ("RemoveTagFromFiles", "a tag in each named note"),
        ("ExportSavedQueryAsBase", "a whole file at a path the reader names"),
        ("RenameFile", "the links of every note that links to the file"),
        ("RenameFileIfIdentity", "the links of every note that links to the file"),
        ("RenameFolder", "the links of every note that links into the folder"),
        ("RenameFolderWithNote", "the links of every note that links into the folder"),
        ("RenameFolderWithNoteIfIdentity", "the links of every note that links into the folder"),
        ("MoveFile", "the links of every note that links to the file"),
        ("MoveFileIfIdentity", "the links of every note that links to the file"),
        ("MoveFolder", "the links of every note that links into the folder"),
        ("MoveFolderIfIdentity", "the links of every note that links into the folder"),
        ("BatchMove", "the links of every note that links to a moved item"),
        ("UndoStructural", "the links a rename or a move rewrote"),
        ("UndoBatchMove", "the links a batch move rewrote"),
        ("UndoBatchMoveIfIdentities", "the links a batch move rewrote"),
    ];

    /// <summary>The session methods that create a file only where none is:
    /// core refuses an occupied path, so the create is its own check.</summary>
    /// <remarks><c>Admitted</c> marks the creators held to the chain all the
    /// same (CD5(c)): a deleted file's recovery to its own path. A call to
    /// one is a site of rule 2. It is a flag on these rows and no table of
    /// its own, so the admitted creators are creators by construction.</remarks>
    private static readonly (string Name, bool Admitted)[] ExclusiveCreatorRows =
    [
        ("CreateExclusive", false),
        ("CreateExclusiveReporting", false),
        ("CreateExclusiveBytes", false),
        ("RecoverDeletedFile", true),
        ("RecoverDeletedFileAs", false),
    ];

    private static readonly string[] ExclusiveCreators = [.. ExclusiveCreatorRows.Select(row => row.Name)];

    /// <summary>The rows of <see cref="ExclusiveCreatorRows"/> that carry the
    /// flag.</summary>
    private static readonly string[] AdmittedCreators =
        [.. ExclusiveCreatorRows.Where(row => row.Admitted).Select(row => row.Name)];

    /// <summary>Every other session method the shell calls, in groups with
    /// one reason a group: none rewrites the bytes of a note an editor can
    /// hold.</summary>
    private static readonly (string Reason, string[] Names)[] NotNoteBytes =
    [
        (
            "reads and queries: each reads the vault, the index or a handle and writes no file",
            [
                "BaseDefinitionHash", "BaseExecute", "BaseExport", "BaseViewEditQueryJson", "BaseViewQueryJson", "BaseViews",
                "BasesList", "CanonicalPath", "CanvasBounds", "CanvasCheckOverlap", "CanvasChildrenOf", "CanvasCurrentText",
                "CanvasDescribeRelative", "CanvasEditorSeed", "CanvasFilter", "CanvasGroupRectAround", "CanvasNeighbors",
                "CanvasNodeText", "CanvasOrderNodes", "CanvasOutline", "CanvasParentOf", "CanvasPlaceInsideGroup",
                "CanvasPlaceNew", "CanvasPlaceSet", "CanvasProximityOrder", "CanvasScene", "CanvasTableRows", "CanvasTracePath",
                "CanvasWhereAmI", "CaptureBatchMoveIdentities", "CaptureStructuralIdentity", "ChangeFeedHead", "ChangeFeedPage",
                "ChangeFeedWindow", "ChangesSinceLastOpen", "CitationsPrefs", "ClassifyHotExitEntries", "DetectSync",
                "DiffVersions", "FilterFiles", "FullTextSearch", "GetBibliographyEntries", "GetDashboard", "GetDiagramBlocks",
                "GetFileMetadata", "GetMathBlocks", "GetSavedQuery", "GetSyntaxTokens", "GraphConnectionsTreeCancellable",
                "GraphGeneration", "GraphSnapshotCancellable", "GraphTableRowsCancellable", "GraphTopologyCancellable",
                "HotExitKey", "IndexedContentHashes", "InteractionGeneration", "ListCitationsInFile", "ListDashboards",
                "ListDeletedFiles", "ListDirChildrenPage", "ListDirFilesPage", "ListFiles", "ListFilesCiting",
                "ListSavedQueries", "ListTemplates", "ListUnresolvedCitations", "ListVersions", "LivesyncConfig",
                "NoteLinkPanels", "NoteLoadBundle", "NoteOutline", "NoteTasks", "OutgoingLinks", "ReadNoteParts", "ReadText",
                "ReadTextIdentified", "RenderCitation", "RenderTemplate", "ResolveEmbedPreview", "ResolveEmbedPreviewPooled",
                "ResolveEmbedReadingCard", "RootDisplayPath", "RootIdentity", "SidebarFilterDateRequirements",
                "StructuralIdentitiesMatch", "SwitcherRankTopIndexed", "TagTreePage", "TasksForFile", "TasksInVault",
                "TasksIndexRevision", "ValidateBaseExpression", "VersionContent", "WikilinkForPath",
            ]),
        (
            "the index, the scan, the change feed and repairs: each writes the index or its records, never a file's bytes",
            [
                "AcknowledgeChangeFeed", "MarkOpened", "RegisterChangeFeed", "RepairOrContainPath", "Rescan",
                "RescanWithProgress", "ScanInitialWithProgress",
            ]),
        (
            "deletes, trash and folders: structural, and no note is rewritten",
            [
                "BatchTrashStagedCancellable", "CreateFolder", "CreateFolderExclusive", "DeleteFile",
                "DeleteFileStagedCancellable", "DeleteFolderStagedCancellable", "DiscardStagedTrash", "StageTrashCancellable",
            ]),
        (
            "a base or a canvas through its handle, and a base saved from a query: neither kind has an editor save "
            + "(a tab's save takes a path key for Markdown only), so no admitted save can stand beside the write",
            [
                "BaseApplyEdit", "BaseApplyEdits", "BaseSetTransientSort", "CanvasApply", "CloseBase", "CloseCanvas",
                "OpenBaseCancellable", "OpenCanvas", "OpenDql", "OpenQuery", "OpenSavedQuery", "SaveQueryAsBase",
            ]),
        (
            "saved queries, dashboards, preferences and settings: the app's own stores",
            [
                "DeleteDashboard", "DeleteSavedQuery", "RenameSavedQuery", "SaveDashboard", "SaveQuery",
                "SetBibliographySources", "SetMathPrefs", "UpdateDashboard", "UpdateSavedQuery",
            ]),
        (
            "handles, listeners and the session's lifecycle",
            [
                "DuplicateRootHandle", "RegisterEventListener", "StartGraphLayout", "StartWatching", "StopWatching",
                "UnregisterEventListener",
            ]),
    ];

    /// <summary>The tab methods that replace a tab's buffer from disk. A call
    /// to one from outside the tab is a site of rule 3.</summary>
    private static readonly string[] ReloadMethods =
    [
        "ReplaceItem",
        "ReplaceItemWithReadText",
        "ReloadKeepingCaretLine",
        "ReloadKeepingQueuedSave",
        "RebaselineAfterPropertyWrite",
    ];

    /// <summary>One route that writes a note, or reloads a tab, outside the
    /// chain: the member, the calls found in it (sorted, so a new unjoined
    /// call in a listed member still fails), why it stands, when that was
    /// written, and what ends it.</summary>
    /// <remarks><c>Calls</c> names each callee once for each site that calls
    /// it in the member, over every overload of that name in the file, so a
    /// second site of a listed callee fails as a new callee does.
    /// <c>ExcusedCallers</c>, for a row that stands only because some caller
    /// reaches the member outside the chain, lists those callers as
    /// <c>File (Member)</c>: a new one fails.</remarks>
    internal sealed record Row(
        string File, string Member, string[] Calls, string Reason, string Dated, string Ends, string[]? ExcusedCallers = null);

    private const string LinkRewriteReason =
        "core rewrites the notes that link to the item and reports them, each under its own compare-and-swap, "
        + "and the set is not known before the call, so there is no path to admit";

    private const string AcceptedLinkRewrite = "accepted (plan 47 CD5(b)); put to the owner again in CB-46's table";

    private static readonly Row[] OutsideTheChain =
    [
        new(
            "Panels/BulkRenameViewModel.cs",
            "Run",
            ["RenamePropertyAcrossVault", "RenamePropertyByIdentityAcrossVault"],
            "the bulk rename's apply rewrites every note that has the key on its own worker",
            "2026-11-16",
            "CB-9b"),
        new(
            "FilesSidebarViewModel.cs",
            "EditTag",
            ["AddTagToFiles", "RemoveTagFromFiles"],
            "a tag added to or removed from the checked files rewrites each of them on the sidebar's worker",
            "2026-11-16",
            "CB-9b"),
        new(
            "WorkspaceViewModel.Bases.cs",
            "ExportSavedQueryAsBase",
            ["ExportSavedQueryAsBase"],
            "a saved query exported as a base overwrites the file at the path the reader names, with no "
            + "compare-and-swap",
            "2026-11-16",
            "CB-9b (plan 47 CD5(d))"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "ReconcileTabsAfterPropertyWrite",
            ["RebaselineAfterPropertyWrite"],
            "the bulk rename's reconcile reaches this fan-out outside the chain; every other caller is admitted",
            "2026-11-16",
            "CB-9b",
            ExcusedCallers: ["WorkspaceViewModel.Properties.cs (ReconcileTabsAfterBulkRename)"]),
        new(
            "FilesSidebarViewModel.FileManagement.cs",
            "TryRenameSelected",
            ["RenameFile", "RenameFolderWithNote"],
            LinkRewriteReason,
            "2026-11-16",
            AcceptedLinkRewrite),
        new(
            "FilesSidebarViewModel.FileManagement.cs",
            "ExecuteUndoStep",
            ["MoveFileIfIdentity", "MoveFolderIfIdentity", "RenameFileIfIdentity", "RenameFolderWithNoteIfIdentity", "UndoBatchMoveIfIdentities"],
            LinkRewriteReason,
            "2026-11-16",
            AcceptedLinkRewrite),
        new(
            "FilesSidebarViewModel.FileManagement.cs",
            "ExecuteMoveTo",
            ["MoveFile", "MoveFolder"],
            LinkRewriteReason,
            "2026-11-16",
            AcceptedLinkRewrite),
        new(
            "FilesSidebarViewModel.cs",
            "BatchMove",
            ["BatchMove"],
            LinkRewriteReason,
            "2026-11-16",
            AcceptedLinkRewrite),
        new(
            "WorkspaceViewModel.Bases.cs",
            "PublishBasesExport",
            ["SaveText"],
            "a Bases view exported onto an existing vault file writes on the hash seen before the reader confirmed; "
            + "the export and a save of that file are each guarded by a compare-and-swap, and both refusals are true",
            "2026-11-16",
            "accepted until the owner answers in CB-46's table"),
    ];

    /// <summary>A reload that is right as it stands and is no reload of the
    /// note a tab already shows.</summary>
    private static readonly Row[] NotThisClass =
    [
        new(
            "WorkspaceViewModel.Layout.cs",
            "ReplaceTabItem",
            ["ReplaceItem"],
            "navigation re-points a tab at another item behind the dirty-tab settle; its one same-item caller, the "
            + "rescan's re-seat, asks KeepsBufferOnReseat first",
            "2026-11-16",
            "standing"),
    ];

    // ---- The reading ------------------------------------------------------

    internal enum SiteKind
    {
        /// <summary>A call to a session method.</summary>
        Session,

        /// <summary>A call from outside the tab to a method that replaces a
        /// tab's buffer from disk.</summary>
        Reload,
    }

    /// <summary>One call the rules weigh: where it stands, the member and
    /// the node it belongs to, what it calls, and what the member shows for
    /// it.</summary>
    internal sealed record Site(
        SiteKind Kind,
        string File,
        int Line,
        string Member,
        string Node,
        string Name,
        bool HandedToAnAdmission,
        bool HasTicketParameter,
        bool ChainAskedBefore);

    internal sealed record Violation(string File, int Line, string Member, string Name, string Text)
    {
        /// <summary>The node the call belongs to, for a rule's site.</summary>
        public string Node { get; init; } = string.Empty;

        public override string ToString() => $"{File}:{Line} ({Member}): {Text}";
    }

    /// <summary>What binding a set of trees found. Nodes are named by text
    /// that is the same in every compilation of the same source, so the
    /// reading of a planted tree joins the reading of the shell.</summary>
    internal sealed class Reading
    {
        public List<Site> Sites { get; } = [];

        public List<(string File, int Line)> AdmissionCalls { get; } = [];

        public HashSet<string> AdmissionNodes { get; } = new(StringComparer.Ordinal);

        public List<(string Caller, string Callee)> Edges { get; } = [];

        /// <summary>A calling node, as <c>File (Member)</c>.</summary>
        public Dictionary<string, string> NodeNames { get; } = new(StringComparer.Ordinal);

        /// <summary>The methods a call can reach through another symbol: one
        /// that implements an interface member, overrides, or is virtual or
        /// abstract. Rule (A) never admits them.</summary>
        public HashSet<string> Open { get; } = new(StringComparer.Ordinal);

        public List<Violation> Unbound { get; } = [];

        public List<string> ChainImplementers { get; } = [];

        public Reading With(Reading other)
        {
            var joined = new Reading();
            foreach (Reading part in (Reading[])[this, other])
            {
                joined.Sites.AddRange(part.Sites);
                joined.AdmissionCalls.AddRange(part.AdmissionCalls);
                joined.AdmissionNodes.UnionWith(part.AdmissionNodes);
                joined.Edges.AddRange(part.Edges);
                joined.Unbound.AddRange(part.Unbound);
                joined.ChainImplementers.AddRange(part.ChainImplementers);
                joined.Open.UnionWith(part.Open);
                foreach ((string node, string name) in part.NodeNames)
                {
                    joined.NodeNames[node] = name;
                }
            }
            return joined;
        }
    }

    private const string ShellNamespace = "SlateWindows";

    /// <summary>The binding's session methods, by reflection.</summary>
    private static readonly Lazy<HashSet<string>> SessionMethods = new(() =>
    [
        .. typeof(VaultSession)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.Name != "Dispose")
            .Select(method => method.Name),
    ]);

    private static readonly Lazy<(IReadOnlyList<(string File, SyntaxTree Tree)> Trees, HashSet<string> MethodNames, Reading Reading)> Shell = new(() =>
    {
        (string File, SyntaxTree Tree)[] trees =
            [.. ShellCompilation.Sources.Select(source => (source.Relative, source.Source.Root.SyntaxTree))];
        HashSet<string> names = DeclaredMethodNames(trees.Select(tree => tree.Tree));
        return (trees, names, Read(ShellCompilation.Compilation, trees, SessionMethods.Value, names));
    });

    private static HashSet<string> DeclaredMethodNames(IEnumerable<SyntaxTree> trees) =>
        new(
            trees.SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                .Select(method => method.Identifier.ValueText),
            StringComparer.Ordinal);

    /// <summary>Bind <paramref name="trees"/> and read every call the rules
    /// weigh. <paramref name="methodNames"/> is every method name a method
    /// group in these trees could name.</summary>
    internal static Reading Read(
        CSharpCompilation compilation,
        IReadOnlyList<(string File, SyntaxTree Tree)> trees,
        IReadOnlySet<string> sessionMethods,
        IReadOnlySet<string> methodNames)
    {
        var reading = new Reading();
        var typesRead = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string file, SyntaxTree tree) in trees)
        {
            new TreeReader(compilation.GetSemanticModel(tree), file, sessionMethods, methodNames, reading, typesRead).Read(tree.GetRoot());
        }
        return reading;
    }

    private sealed class TreeReader(
        SemanticModel model,
        string file,
        IReadOnlySet<string> sessionMethods,
        IReadOnlySet<string> methodNames,
        Reading reading,
        HashSet<string> typesRead)
    {
        private readonly Dictionary<SyntaxNode, string> _admissionArguments = [];
        private readonly HashSet<ITypeSymbol> _sessionTypes = SessionTypes(model.Compilation);

        public void Read(SyntaxNode root)
        {
            // The admission arguments first: every node below is placed
            // against them.
            foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                foreach (IMethodSymbol method in Bound(invocation))
                {
                    if (AdmissionParameter(method) is not { } parameter)
                    {
                        continue;
                    }
                    reading.AdmissionCalls.Add((file, LineOf(invocation)));
                    if (ArgumentFor(invocation, method, parameter) is not { } argument)
                    {
                        reading.Unbound.Add(new Violation(
                            file, LineOf(invocation), MemberOf(invocation).Name, method.Name,
                            $"the census cannot find the argument this admission call hands to `{parameter}`"));
                        continue;
                    }
                    if (!_admissionArguments.ContainsKey(argument))
                    {
                        FileLinePositionSpan at = argument.GetLocation().GetLineSpan();
                        string node = $"admission:{file}:{at.StartLinePosition.Line + 1}:{at.StartLinePosition.Character + 1}";
                        _admissionArguments[argument] = node;
                        _ = reading.AdmissionNodes.Add(node);
                    }
                }
            }

            foreach (SyntaxNode node in root.DescendantNodes())
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation:
                        Weigh(invocation, Bound(invocation), CalleeName(invocation));
                        break;
                    case BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax:
                        Weigh(node, Bound(node), null);
                        break;
                    case SimpleNameSyntax name when IsMethodGroupCandidate(name):
                        Weigh(name, Bound(name), name.Identifier.ValueText);
                        break;
                    case BaseTypeDeclarationSyntax type:
                        ReadType(type);
                        break;
                }
            }
        }

        private IMethodSymbol[] Bound(SyntaxNode node)
        {
            SymbolInfo info = model.GetSymbolInfo(node);
            return info.Symbol is IMethodSymbol method
                ? [method]
                : [.. info.CandidateSymbols.OfType<IMethodSymbol>()];
        }

        private static string? CalleeName(InvocationExpressionSyntax invocation) => invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };

        /// <summary>A name that could be a method handed on as a group: it
        /// is no call's own callee, it stands in no <c>nameof</c>, and it
        /// names no argument or member being set.</summary>
        private bool IsMethodGroupCandidate(SimpleNameSyntax name)
        {
            string text = name.Identifier.ValueText;
            if (!methodNames.Contains(text) && !sessionMethods.Contains(text))
            {
                return false;
            }
            if (name.Parent is NameColonSyntax or NameEqualsSyntax)
            {
                return false;
            }
            ExpressionSyntax written = name.Parent switch
            {
                MemberAccessExpressionSyntax access when access.Name == name => access,
                MemberBindingExpressionSyntax binding when binding.Name == name => binding,
                _ => name,
            };
            if (written.Parent is InvocationExpressionSyntax invocation && invocation.Expression == written)
            {
                return false;
            }
            return !name.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });
        }

        private void Weigh(SyntaxNode node, IMethodSymbol[] methods, string? spelled)
        {
            if (methods.Length == 0)
            {
                // Fail closed: a call spelled as a session method that binds
                // to nothing could be one.
                if (spelled is not null
                    && sessionMethods.Contains(spelled)
                    && model.GetSymbolInfo(node) is { Symbol: null, CandidateSymbols.IsEmpty: true })
                {
                    reading.Unbound.Add(new Violation(
                        file, LineOf(node), MemberOf(node).Name, spelled,
                        $"`{spelled}` is the name of a session method and does not bind here, so the census cannot "
                        + "say what it calls"));
                }
                return;
            }
            foreach (IMethodSymbol bound in methods)
            {
                IMethodSymbol method = (bound.ReducedFrom ?? bound).OriginalDefinition;
                if (method.MethodKind is MethodKind.LocalFunction or MethodKind.DelegateInvoke or MethodKind.AnonymousFunction)
                {
                    continue;
                }
                if (!method.DeclaringSyntaxReferences.IsDefaultOrEmpty)
                {
                    string caller = NodeOf(node);
                    reading.Edges.Add((caller, Id(method)));
                    reading.NodeNames[caller] = $"{file} ({MemberOf(node).Name})";
                }
                if (method.ContainingType is { } owner
                    && _sessionTypes.Contains(owner.OriginalDefinition)
                    && sessionMethods.Contains(method.Name))
                {
                    reading.Sites.Add(SiteAt(SiteKind.Session, node, method.Name));
                }
                else if (IsShellType(method.ContainingType, "WorkspaceTabViewModel")
                    && ReloadMethods.Contains(method.Name)
                    && !InsideTheTab(node))
                {
                    reading.Sites.Add(SiteAt(SiteKind.Reload, node, method.Name));
                }
            }
        }

        private Site SiteAt(SiteKind kind, SyntaxNode node, string name)
        {
            (string member, SyntaxNode? declaration) = MemberOf(node);
            return new Site(
                kind,
                file,
                LineOf(node),
                member,
                NodeOf(node),
                name,
                declaration is not null && HandedToAnAdmission(node, declaration),
                declaration is BaseMethodDeclarationSyntax method && method.ParameterList.Parameters.Any(parameter =>
                    parameter.Type is { } type && model.GetTypeInfo(type).Type is { Name: "SaveTicket" }),
                declaration is not null && declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                    call.SpanStart < node.SpanStart && Bound(call).Any(AsksTheChain)));
        }

        private static bool AsksTheChain(IMethodSymbol method) =>
            (method.Name == "HasAdmittedSaveFor" && IsShellType(method.ContainingType, "WorkspaceSaveCoordinator"))
            || (method.Name == "KeepsBufferOnReseat" && IsShellType(method.ContainingType, "WorkspaceViewModel"));

        /// <summary>Shape (B): the call stands in a lambda assigned to a
        /// local, and every read of that local in the member stands in an
        /// admission argument.</summary>
        private bool HandedToAnAdmission(SyntaxNode node, SyntaxNode declaration)
        {
            foreach (AnonymousFunctionExpressionSyntax function in node.Ancestors()
                .TakeWhile(ancestor => ancestor != declaration)
                .OfType<AnonymousFunctionExpressionSyntax>())
            {
                if (LocalAssigned(function) is not { } local)
                {
                    continue;
                }
                IdentifierNameSyntax[] reads = [.. declaration.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Where(name => name.Identifier.ValueText == local.Name
                        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, local)
                        && !IsAssignedTo(name))];
                if (reads.Length > 0 && reads.All(read => read.AncestorsAndSelf().Any(_admissionArguments.ContainsKey)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAssignedTo(IdentifierNameSyntax name) =>
            name.Parent is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
            && assignment.Left == name;

        private ILocalSymbol? LocalAssigned(AnonymousFunctionExpressionSyntax function)
        {
            ExpressionSyntax written = function;
            while (written.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            {
                written = (ExpressionSyntax)written.Parent;
            }
            return written.Parent switch
            {
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } =>
                    model.GetDeclaredSymbol(declarator) as ILocalSymbol,
                AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
                    when assignment.Right == written =>
                    model.GetSymbolInfo(assignment.Left).Symbol as ILocalSymbol,
                _ => null,
            };
        }

        private bool InsideTheTab(SyntaxNode node) => node.Ancestors().OfType<BaseTypeDeclarationSyntax>()
            .Any(type => model.GetDeclaredSymbol(type) is INamedTypeSymbol symbol && IsShellType(symbol, "WorkspaceTabViewModel"));

        /// <summary>The node a syntax node belongs to: the admission argument
        /// it stands in, or else the member that holds it.</summary>
        private string NodeOf(SyntaxNode node)
        {
            for (SyntaxNode? current = node; current is not null; current = current.Parent)
            {
                if (_admissionArguments.TryGetValue(current, out string? admission))
                {
                    return admission;
                }
                if (Declared(current) is { } symbol)
                {
                    return Id(symbol);
                }
            }
            return "(no member)";
        }

        private (string Name, SyntaxNode? Declaration) MemberOf(SyntaxNode node)
        {
            for (SyntaxNode? current = node; current is not null; current = current.Parent)
            {
                if (Declared(current) is { } symbol)
                {
                    return (symbol switch
                    {
                        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor =>
                            constructor.ContainingType.Name,
                        IMethodSymbol { AssociatedSymbol: { } owner } => owner.Name,
                        _ => symbol.Name,
                    }, current);
                }
            }
            return ("(no member)", null);
        }

        private ISymbol? Declared(SyntaxNode node) => node switch
        {
            BaseMethodDeclarationSyntax or AccessorDeclarationSyntax => model.GetDeclaredSymbol(node),
            ArrowExpressionClauseSyntax { Parent: BasePropertyDeclarationSyntax property } =>
                (model.GetDeclaredSymbol(property) as IPropertySymbol)?.GetMethod,
            VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax } => model.GetDeclaredSymbol(node),
            BasePropertyDeclarationSyntax or BaseTypeDeclarationSyntax => model.GetDeclaredSymbol(node),
            _ => null,
        };

        /// <summary>Which methods of a type a call can reach through another
        /// symbol, and whether the type is a save chain.</summary>
        private void ReadType(BaseTypeDeclarationSyntax declaration)
        {
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type || !typesRead.Add(Id(type)))
            {
                return;
            }
            if (type.TypeKind != TypeKind.Interface && type.AllInterfaces.Any(candidate => IsShellType(candidate, "INoteSaveChain")))
            {
                reading.ChainImplementers.Add(type.Name);
            }
            foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.IsVirtual
                    || method.IsAbstract
                    || method.IsOverride
                    || !method.ExplicitInterfaceImplementations.IsDefaultOrEmpty
                    || IsShapedLikeAnEventHandler(method)
                    || type.AllInterfaces.Any(contract => contract.GetMembers(method.Name).OfType<IMethodSymbol>().Any(member =>
                        SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), method))))
                {
                    _ = reading.Open.Add(Id(method));
                }
            }
        }

        /// <summary>A method with an event handler's parameters, a sender
        /// and arguments derived from <c>EventArgs</c>. XAML wires such a
        /// method in generated code this census does not read, so the
        /// graph may not hold all of its callers.</summary>
        private static bool IsShapedLikeAnEventHandler(IMethodSymbol method)
        {
            if (method.Parameters.Length != 2 || method.Parameters[0].Type.SpecialType != SpecialType.System_Object)
            {
                return false;
            }
            for (ITypeSymbol? type = method.Parameters[1].Type; type is not null; type = type.BaseType)
            {
                if (type.Name == "EventArgs" && type.ContainingNamespace?.ToDisplayString() == "System")
                {
                    return true;
                }
            }
            return false;
        }

        private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    }

    /// <summary>The parameter an admission call takes its operation in, or
    /// null for any other method.</summary>
    private static string? AdmissionParameter(IMethodSymbol bound)
    {
        IMethodSymbol method = (bound.ReducedFrom ?? bound).OriginalDefinition;
        return (method.ContainingType?.Name, method.Name) switch
        {
            ("WorkspaceSaveCoordinator", "Admit") or ("INoteSaveChain", "Admit") or ("WorkspaceViewModel", "AdmitPropertyWrite")
                when IsShellType(method.ContainingType, method.ContainingType.Name) => "operation",
            ("WorkspaceSaveCoordinator", "Enqueue") when IsShellType(method.ContainingType, "WorkspaceSaveCoordinator") => "start",
            _ => null,
        };
    }

    private static ExpressionSyntax? ArgumentFor(InvocationExpressionSyntax invocation, IMethodSymbol method, string parameter)
    {
        int ordinal = method.Parameters.FirstOrDefault(candidate => candidate.Name == parameter)?.Ordinal ?? -1;
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        for (int index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].NameColon is { } named
                ? named.Name.Identifier.ValueText == parameter
                : index == ordinal)
            {
                return arguments[index].Expression;
            }
        }
        return null;
    }

    private static bool IsShellType(ITypeSymbol? type, string name) =>
        type is not null && type.Name == name && type.ContainingNamespace?.ToDisplayString() == ShellNamespace;

    /// <summary>The binding's <c>VaultSession</c> and every interface it
    /// implements, as <paramref name="compilation"/> binds them: a call
    /// through one of the interfaces is the same call.</summary>
    private static HashSet<ITypeSymbol> SessionTypes(Compilation compilation)
    {
        INamedTypeSymbol session = compilation.GetTypeByMetadataName(typeof(VaultSession).FullName!)
            ?? throw new InvalidOperationException("The binding's VaultSession does not bind in the shell compilation.");
        return new HashSet<ITypeSymbol>([session, .. session.AllInterfaces], SymbolEqualityComparer.Default);
    }

    /// <summary>A symbol's name as every compilation of the same source
    /// spells it.</summary>
    private static string Id(ISymbol symbol)
    {
        ISymbol original = symbol.OriginalDefinition;
        if (original is IMethodSymbol { PartialDefinitionPart: { } definition })
        {
            original = definition;
        }
        return original.GetDocumentationCommentId() ?? original.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>The least set that holds every admission argument and every
    /// node that has a caller, whose callers are all in the set, and that no
    /// call can reach through another symbol (<see cref="Reading.Open"/>). A
    /// call a member makes to itself is no caller.</summary>
    internal static HashSet<string> AdmittedNodes(Reading reading)
    {
        var direct = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        HashSet<string> CallersOf(string node)
        {
            if (!direct.TryGetValue(node, out HashSet<string>? callers))
            {
                callers = new HashSet<string>(StringComparer.Ordinal);
                direct[node] = callers;
            }
            return callers;
        }
        foreach ((string caller, string callee) in reading.Edges)
        {
            if (caller != callee)
            {
                _ = CallersOf(callee).Add(caller);
            }
        }
        var admitted = new HashSet<string>(reading.AdmissionNodes, StringComparer.Ordinal);
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach ((string node, HashSet<string> callers) in direct)
            {
                if (callers.Count > 0
                    && !reading.Open.Contains(node)
                    && !admitted.Contains(node)
                    && callers.All(admitted.Contains))
                {
                    _ = admitted.Add(node);
                    grew = true;
                }
            }
        }
        return admitted;
    }

    private static bool IsAdmitted(Site site, HashSet<string> admitted) =>
        admitted.Contains(site.Node) || site.HandedToAnAdmission;

    // ---- Rule 1: every session method the shell calls is classified -------

    /// <summary>The classification as rule 1 reads it.</summary>
    internal sealed record Tables(
        IReadOnlyList<string> NoteWriters,
        IReadOnlyList<string> ExclusiveCreators,
        IReadOnlyList<string> NotNoteBytes);

    private static Tables TheTables() => new(
        [.. NoteWriters.Select(writer => writer.Name)],
        ExclusiveCreators,
        [.. NotNoteBytes.SelectMany(group => group.Names)]);

    /// <summary>Rule 1: a call to a session method that is named in no
    /// table, or in more than one; a table name the binding does not have;
    /// and a call that could be a session method and does not bind.</summary>
    internal static IReadOnlyList<string> Unclassified(Reading reading, IReadOnlySet<string> sessionMethods, Tables tables)
    {
        List<string> found = [.. reading.Unbound.Select(unbound => unbound.ToString())];
        (string Table, IReadOnlyList<string> Names)[] all =
        [
            (nameof(NoteWriters), tables.NoteWriters),
            (nameof(ExclusiveCreators), tables.ExclusiveCreators),
            (nameof(NotNoteBytes), tables.NotNoteBytes),
        ];
        foreach ((string table, IReadOnlyList<string> names) in all)
        {
            found.AddRange(names.Where(name => !sessionMethods.Contains(name))
                .Select(name => $"{table} names {name}, which the binding's VaultSession does not have"));
            found.AddRange(names.GroupBy(name => name, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => $"{table} names {group.Key} {group.Count()} times"));
        }
        foreach (IGrouping<string, Site> called in reading.Sites.Where(site => site.Kind == SiteKind.Session)
            .GroupBy(site => site.Name, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            string[] named = [.. all.Where(table => table.Names.Contains(called.Key)).Select(table => table.Table)];
            if (named.Length == 1)
            {
                continue;
            }
            Site first = called.OrderBy(site => site.File, StringComparer.Ordinal).ThenBy(site => site.Line).First();
            found.Add(named.Length == 0
                ? $"{first.File}:{first.Line} ({first.Member}): VaultSession.{called.Key} is in no table ({called.Count()} "
                    + $"call(s)). Name it in {nameof(NoteWriters)} if it rewrites the bytes of a note, in "
                    + $"{nameof(ExclusiveCreators)} if it creates a file only where none is, and in a group of "
                    + $"{nameof(NotNoteBytes)} otherwise."
                : $"{first.File}:{first.Line} ({first.Member}): VaultSession.{called.Key} is in {named.Length} tables "
                    + $"({string.Join(", ", named)}). It belongs in one.");
        }
        return found;
    }

    [Fact]
    public void EveryVaultSessionMethodTheShellCallsIsClassified()
    {
        IReadOnlyList<string> found = Unclassified(Shell.Value.Reading, SessionMethods.Value, TheTables());
        Assert.True(
            found.Count == 0,
            "A route that writes a note is held to the note's save chain by name, so every session method the shell "
            + "calls is classified once:\n" + string.Join("\n", found));
    }

    // ---- Rule 2: every note writer is admitted into its note's chain ------

    /// <summary>Rule 2: every call to a note writer, or to an admitted
    /// creator, that is not admitted.</summary>
    internal static IReadOnlyList<Violation> UnadmittedWriters(Reading reading, IReadOnlySet<string> writers)
    {
        HashSet<string> admitted = AdmittedNodes(reading);
        return [.. reading.Sites
            .Where(site => site.Kind == SiteKind.Session && writers.Contains(site.Name) && !IsAdmitted(site, admitted))
            .Select(site => new Violation(
                site.File, site.Line, site.Member, site.Name,
                $"VaultSession.{site.Name} writes outside the note's save chain")
            { Node = site.Node })];
    }

    private static HashSet<string> Writers() =>
        new(NoteWriters.Select(writer => writer.Name).Concat(AdmittedCreators), StringComparer.Ordinal);

    [Fact]
    public void EveryNoteWriterCallIsAdmittedIntoItsNotesChain() =>
        AssertOnlyTheListed(
            Shell.Value.Reading,
            UnadmittedWriters(Shell.Value.Reading, Writers()),
            [.. OutsideTheChain.Where(row => !IsAReloadRow(row))],
            "These write a note outside its save chain, so they can write beside a save admitted for the note and "
            + "not yet published. Admit the operation (WorkspaceSaveCoordinator.Admit, INoteSaveChain.Admit or "
            + "AdmitPropertyWrite), hand its worker to TrackWorker, and end its ticket on every path:");

    // ---- Rule 3: every reload of a tab from disk is admitted or asks ------

    /// <summary>Rule 3: every call from outside the tab to a method that
    /// replaces a tab's buffer from disk, unless it is admitted, its member
    /// holds a save ticket, or its member asks the chain before it.</summary>
    internal static IReadOnlyList<Violation> UnguardedReloads(Reading reading)
    {
        HashSet<string> admitted = AdmittedNodes(reading);
        return [.. reading.Sites
            .Where(site => site.Kind == SiteKind.Reload
                && !IsAdmitted(site, admitted)
                && !site.HasTicketParameter
                && !site.ChainAskedBefore)
            .Select(site => new Violation(
                site.File, site.Line, site.Member, site.Name,
                $"WorkspaceTabViewModel.{site.Name} reloads a tab from disk outside the note's save chain and "
                + "without asking it")
            { Node = site.Node })];
    }

    [Fact]
    public void EveryReloadOfATabFromDiskIsAdmittedOrAsksTheChain() =>
        AssertOnlyTheListed(
            Shell.Value.Reading,
            UnguardedReloads(Shell.Value.Reading),
            [.. OutsideTheChain.Where(IsAReloadRow), .. NotThisClass],
            "These replace a tab's buffer from disk under whatever is admitted for its note. Run the reload inside "
            + "the admitted operation, or ask HasAdmittedSaveFor (or KeepsBufferOnReseat) first and keep the tab "
            + "while it holds:");

    private static bool IsAReloadRow(Row row) => row.Calls.All(ReloadMethods.Contains);

    private static void AssertOnlyTheListed(Reading reading, IReadOnlyList<Violation> found, IReadOnlyList<Row> rows, string remedy)
    {
        IReadOnlyList<string> problems = Problems(reading, found, rows);
        Assert.True(problems.Count == 0, remedy + "\n" + string.Join("\n", problems));
    }

    /// <summary>What the rows do not answer. A violation in a member no row
    /// lists. A callee whose count of sites in the member, over every
    /// overload of that name in the file, differs from the count the row
    /// lists, so a second site of a listed callee fails. For a row with
    /// excused callers, a direct caller outside the chain that the row does
    /// not excuse, and an excused caller that is no longer one. A row with
    /// no reason, date or end. A row that matches no violation.</summary>
    internal static IReadOnlyList<string> Problems(Reading reading, IReadOnlyList<Violation> found, IReadOnlyList<Row> rows)
    {
        List<string> problems = [.. rows
            .Where(row => string.IsNullOrWhiteSpace(row.Reason) || string.IsNullOrWhiteSpace(row.Dated) || string.IsNullOrWhiteSpace(row.Ends))
            .Select(row => $"{Named(row)}: a row with no reason, no date or nothing that ends it.")];
        HashSet<string> admitted = AdmittedNodes(reading);
        foreach (IGrouping<(string File, string Member), Violation> member in found
            .GroupBy(violation => (violation.File, violation.Member)))
        {
            string where = $"{member.Key.File} ({member.Key.Member})";
            Row[] listed = [.. rows.Where(row => row.File == member.Key.File && row.Member == member.Key.Member)];
            if (listed.Length != 1)
            {
                problems.AddRange(member.OrderBy(violation => violation.Line).Select(violation => violation.ToString()));
                problems.Add($"  {where} holds [{Calls(member.Select(violation => violation.Name))}]; "
                    + (listed.Length == 0 ? "no row lists it." : "more than one row lists it."));
                continue;
            }
            Row row = listed[0];
            foreach (string callee in member.Select(violation => violation.Name).Concat(row.Calls)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal))
            {
                int sites = member.Count(violation => violation.Name == callee);
                int counted = row.Calls.Count(name => name == callee);
                if (sites != counted)
                {
                    problems.Add($"{where}: {callee} is called at {sites} site(s) and the row lists {counted} (lines "
                        + string.Join(", ", member.Where(violation => violation.Name == callee).Select(violation => violation.Line).Order())
                        + "). Join the new call to the chain, or list it in the row once for each site.");
                }
            }
            if (row.ExcusedCallers is { } excused)
            {
                HashSet<string> nodes = new(member.Select(violation => violation.Node), StringComparer.Ordinal);
                string[] callers = [.. reading.Edges
                    .Where(edge => nodes.Contains(edge.Callee) && !nodes.Contains(edge.Caller) && !admitted.Contains(edge.Caller))
                    .Select(edge => reading.NodeNames.GetValueOrDefault(edge.Caller, edge.Caller))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)];
                problems.AddRange(callers.Except(excused, StringComparer.Ordinal).Select(caller =>
                    $"{where}: {caller} calls it outside the chain, and the row excuses only [{string.Join("; ", excused)}]. "
                    + "Admit the new caller, or excuse it in the row with its reason."));
                problems.AddRange(excused.Except(callers, StringComparer.Ordinal).Select(caller =>
                    $"{where}: the row excuses {caller}, which no longer calls it outside the chain (take it out of the row)."));
            }
        }
        problems.AddRange(rows
            .Where(row => !found.Any(violation => violation.File == row.File && violation.Member == row.Member))
            .Select(row => $"{Named(row)}: a row that matches no violation (delete it)."));
        return problems;
    }

    private static string Calls(IEnumerable<string> names) =>
        string.Join(", ", names.OrderBy(name => name, StringComparer.Ordinal));

    private static string Named(Row row) => $"{row.File} ({row.Member}) [{Calls(row.Calls)}]";

    // ---- The chain has one implementer ------------------------------------

    [Fact]
    public void OnlyTheCoordinatorIsANotesSaveChain() =>
        Assert.Equal(["WorkspaceSaveCoordinator"], Shell.Value.Reading.ChainImplementers);

    // ---- The create class is tied to its fact -----------------------------

    private const string CreateClassFact = "AnExclusiveCreateAndAGhostTabsSaveNeverOverwriteEachOther";

    /// <summary>The exclusive creators the shell calls outside the chain are
    /// exempt as a class (CD5(c)) on the strength of one theory, so its rows
    /// are exactly those creators: a new creator called outside the chain,
    /// or a row for one no longer called, fails here.</summary>
    [Fact]
    public void EveryUnadmittedCreatorIsInTheCreateClassFact()
    {
        Reading reading = Shell.Value.Reading;
        HashSet<string> admitted = AdmittedNodes(reading);
        string[] outside = [.. reading.Sites
            .Where(site => site.Kind == SiteKind.Session
                && ExclusiveCreators.Contains(site.Name)
                && !AdmittedCreators.Contains(site.Name)
                && !IsAdmitted(site, admitted))
            .Select(site => site.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        MethodInfo? fact = typeof(SaveChainAdmissionTests).GetMethod(CreateClassFact);
        Assert.True(fact is not null, $"SaveChainAdmissionTests.{CreateClassFact} does not exist.");
        string[] proved = [.. fact.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(InlineDataAttribute))
            .Select(attribute => ((IReadOnlyCollection<CustomAttributeTypedArgument>)attribute.ConstructorArguments[0].Value!).First().Value as string ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        Assert.True(
            outside.SequenceEqual(proved),
            $"The shell calls these exclusive creators outside the save chain: [{string.Join(", ", outside)}]. "
            + $"{CreateClassFact} has rows for: [{string.Join(", ", proved)}]. Add a row for a new creator (the create "
            + "first and the tab's save first), or admit it and flag its row in ExclusiveCreatorRows.");
    }

    // ---- Fail closed, and the floors --------------------------------------

    /// <summary>Sources the rules read whether or not they hold a site
    /// today.</summary>
    private static readonly string[] AlwaysBound =
    [
        "WorkspaceViewModel.cs",
        "WorkspaceViewModel.Bases.cs",
        "WorkspaceViewModel.History.cs",
        "WorkspaceViewModel.Properties.cs",
        "WorkspaceViewModel.Rescan.cs",
        "WorkspaceViewModel.Templates.cs",
        "WorkspaceViewModel.Layout.cs",
        "WorkspaceSaveCoordinator.cs",
        "WorkspaceTabViewModel.Properties.cs",
        "Panels/TasksReviewViewModel.cs",
        "Panels/BulkRenameViewModel.cs",
        "FilesSidebarViewModel.cs",
        "FilesSidebarViewModel.FileManagement.cs",
        "FilesSidebarViewModel.Import.cs",
        "CreateOutcomes.cs",
    ];

    /// <summary>Fail closed: a source that does not bind could hide a call's
    /// target from the rules.</summary>
    [Fact]
    public void TheSaveChainSourcesBind()
    {
        Reading reading = Shell.Value.Reading;
        HashSet<string> files = new(AlwaysBound, StringComparer.Ordinal);
        files.UnionWith(reading.Sites
            .Where(site => site.Kind == SiteKind.Reload || IsWeighed(site.Name))
            .Select(site => site.File));
        files.UnionWith(reading.AdmissionCalls.Select(call => call.File));
        ILookup<string, SyntaxTree> trees = Shell.Value.Trees.ToLookup(tree => tree.File, tree => tree.Tree, StringComparer.Ordinal);
        foreach (string required in AlwaysBound)
        {
            Assert.True(trees.Contains(required), $"premise: {required} is among the shell's sources.");
        }
        string[] errors = [.. files.OrderBy(file => file, StringComparer.Ordinal).SelectMany(file => trees[file].SelectMany(tree =>
            ShellCompilation.Compilation.GetSemanticModel(tree).GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => $"{file}: {diagnostic}")))];
        Assert.True(errors.Length == 0, string.Join("\n", errors.Take(20)));
    }

    private static bool IsWeighed(string sessionMethod) =>
        NoteWriters.Any(writer => writer.Name == sessionMethod) || ExclusiveCreators.Contains(sessionMethod);

    /// <summary>The walk as it stood on 2026-11-16: 308 sources, 31 calls to
    /// a note writer, 6 to an exclusive creator, 9 reloads of a tab from
    /// outside it and, once the recover, the tabless cell write and the
    /// tabless review toggle were admitted, 15 admission calls. Fewer means
    /// the walk missed files or calls, not that the shell writes less.</summary>
    [Fact]
    public void TheWalkReadsTheShell()
    {
        Reading reading = Shell.Value.Reading;
        (string What, int Found, int AtLeast)[] floors =
        [
            ("sources", Shell.Value.Trees.Count, 290),
            ("calls to a note writer", reading.Sites.Count(site => site.Kind == SiteKind.Session && NoteWriters.Any(writer => writer.Name == site.Name)), 28),
            ("calls to an exclusive creator", reading.Sites.Count(site => site.Kind == SiteKind.Session && ExclusiveCreators.Contains(site.Name)), 5),
            ("reloads of a tab from outside it", reading.Sites.Count(site => site.Kind == SiteKind.Reload), 8),
            ("admission calls", reading.AdmissionCalls.Count, 14),
        ];
        string[] missing = [.. floors.Where(floor => floor.Found < floor.AtLeast)
            .Select(floor => $"{floor.What}: the walk found {floor.Found}; it held at least {floor.AtLeast} when the census was written")];
        Assert.True(missing.Length == 0, "The walk is partial.\n" + string.Join("\n", missing));
    }

    // ---- The rules on planted source --------------------------------------

    private const string PlantedPrelude = "using uniffi.slate_uniffi;\nnamespace SlateWindows;\n";

    private const string AWrite = "_ = session.SaveText(\"a.md\", \"text\", null);";

    /// <summary>Members of a class of its own, with a session, the
    /// coordinator and the chain to hand.</summary>
    private static string InAClass(string members) =>
        PlantedPrelude
        + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves, INoteSaveChain chain)\n{\n"
        + members + "\n}\n";

    /// <summary>Members of one more part of the workspace, which can call
    /// its private admission and its private guard.</summary>
    private static string InTheWorkspace(string members) =>
        PlantedPrelude + "partial class WorkspaceViewModel\n{\n" + members + "\n}\n";

    private static string InTheTab(string members) =>
        PlantedPrelude + "partial class WorkspaceTabViewModel\n{\n" + members + "\n}\n";

    /// <summary>Bind <paramref name="source"/> as one more shell tree and
    /// read it. The planted source must bind, or the case proves nothing.</summary>
    private static Reading Planted(string source, bool mustBind = true, string file = "Planted.cs")
    {
        var options = (CSharpParseOptions)ShellCompilation.Compilation.SyntaxTrees.First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, options, path: "Planted.cs");
        Assert.DoesNotContain(tree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        if (mustBind)
        {
            Diagnostic[] errors = [.. compilation.GetSemanticModel(tree).GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
            Assert.True(errors.Length == 0, "premise: the planted source binds.\n" + string.Join("\n", errors.Select(error => error.ToString())));
        }
        HashSet<string> names = new(Shell.Value.MethodNames, StringComparer.Ordinal);
        names.UnionWith(DeclaredMethodNames([tree]));
        return Read(compilation, [(file, tree)], SessionMethods.Value, names);
    }

    /// <summary>Each unjoined form is named and each joined form is left
    /// alone, by both rules.</summary>
    [Theory]
    [MemberData(nameof(PlantedRoutes))]
    public void TheRulesSeeAPlantedRoute(string form, string planted, string expected)
    {
        Reading read = Planted(planted);
        Reading all = Shell.Value.Reading.With(read);
        HashSet<string> writers = Writers();
        var seen = new List<string>();
        if (read.Sites.Any(site => site.Kind == SiteKind.Session && writers.Contains(site.Name)))
        {
            seen.Add(UnadmittedWriters(all, writers).Any(violation => violation.File == "Planted.cs") ? "unjoined" : "joined");
        }
        if (read.Sites.Any(site => site.Kind == SiteKind.Reload))
        {
            seen.Add(UnguardedReloads(all).Any(violation => violation.File == "Planted.cs") ? "unguarded" : "guarded");
        }
        if (read.ChainImplementers.Count > 0)
        {
            seen.Add("a second chain");
        }
        string answer = seen.Count == 0 ? "clean" : string.Join(", ", seen);
        Assert.True(expected == answer, $"{form}: expected {expected}, the rules answered {answer}");
    }

    public static TheoryData<string, string, string> PlantedRoutes() => new()
    {
        // Unjoined.
        {
            "a writer in a plain method another plain method calls",
            InAClass("void PlantedWrite() { " + AWrite + " }\nvoid PlantedCaller() { PlantedWrite(); }"),
            "unjoined"
        },
        {
            "a method called from an admission lambda and from an unadmitted method",
            InAClass(
                "void PlantedWrite() { " + AWrite + " }\n"
                + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { PlantedWrite(); return Task.CompletedTask; }); }\n"
                + "void PlantedBeside() { PlantedWrite(); }"),
            "unjoined"
        },
        {
            "a method with no caller",
            InAClass("void PlantedWrite() { " + AWrite + " }"),
            "unjoined"
        },
        {
            "a method handed out as a method group by an unadmitted method",
            InAClass(
                "void PlantedWrite() { " + AWrite + " }\n"
                + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { PlantedWrite(); return Task.CompletedTask; }); }\n"
                + "Action PlantedHandsOut() => PlantedWrite;"),
            "unjoined"
        },
        {
            "a writer lambda in a local no admission lambda reads",
            InAClass("void PlantedM() { Action write = () => { " + AWrite + " }; write(); }"),
            "unjoined"
        },
        {
            "a writer lambda in a local an admission lambda reads and the member calls too",
            InAClass(
                "void PlantedM() { Func<Task> write = () => { " + AWrite + " return Task.CompletedTask; }; "
                + "_ = saves.Admit(\"a.md\", _ => write()); _ = write(); }"),
            "unjoined"
        },
        {
            "a writer in the abandoned argument",
            InTheWorkspace(
                "void PlantedM() { AdmitPropertyWrite(\"a.md\", () => Task.CompletedTask, "
                + "() => { _ = _session.SaveText(\"a.md\", \"text\", null); }); }"),
            "unjoined"
        },
        {
            "RecoverDeletedFile in a plain method",
            InAClass("void PlantedM() { _ = session.RecoverDeletedFile(\"a.md\"); }"),
            "unjoined"
        },
        {
            "a method an admission lambda calls, reached through its interface from an unadmitted method",
            PlantedPrelude
            + "internal interface IPlantedWriter { void Write(); }\n"
            + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves) : IPlantedWriter\n{\n"
            + "public void Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Write(); return Task.CompletedTask; }); }\n}\n"
            + "internal sealed class PlantedUser { void M(IPlantedWriter writer) => writer.Write(); }\n",
            "unjoined"
        },
        {
            "a method an admission lambda calls that also answers a framework interface",
            PlantedPrelude
            + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves) : IDisposable\n{\n"
            + "public void Dispose() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Dispose(); return Task.CompletedTask; }); }\n}\n",
            "unjoined"
        },
        {
            "a writer in a property's getter an admission lambda reads",
            InAClass(
                "int PlantedCount { get { " + AWrite + " return 1; } }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", ticket => { int count = PlantedCount; return Task.FromResult(count); }); }"),
            "unjoined"
        },

        {
            "a writer in a method that implements an interface member, with every caller admitted",
            PlantedPrelude
            + "internal interface IPlantedWriter { void Write(); }\n"
            + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves) : IPlantedWriter\n{\n"
            + "public void Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Write(); return Task.CompletedTask; }); }\n}\n",
            "unjoined"
        },
        {
            "a writer in a method that implements an interface member explicitly, with every caller admitted",
            PlantedPrelude
            + "internal interface IPlantedWriter { void Write(); }\n"
            + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves) : IPlantedWriter\n{\n"
            + "void IPlantedWriter.Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { ((IPlantedWriter)this).Write(); return Task.CompletedTask; }); }\n}\n",
            "unjoined"
        },
        {
            "a writer in an override, with every caller admitted",
            PlantedPrelude
            + "internal abstract class PlantedBase { public abstract void Write(); }\n"
            + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves) : PlantedBase\n{\n"
            + "public override void Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Write(); return Task.CompletedTask; }); }\n}\n",
            "unjoined"
        },
        {
            "a writer in a virtual method, with every caller admitted",
            PlantedPrelude
            + "internal class Planted(VaultSession session, WorkspaceSaveCoordinator saves)\n{\n"
            + "public virtual void Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Write(); return Task.CompletedTask; }); }\n}\n",
            "unjoined"
        },
        {
            "a writer in a method shaped like an event handler, with every caller admitted",
            InAClass(
                "void PlantedClick(object sender, EventArgs e) { " + AWrite + " }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", ticket => { PlantedClick(this, EventArgs.Empty); "
                + "return Task.CompletedTask; }); }"),
            "unjoined"
        },
        {
            "a writer called through IVaultSession in a plain method",
            InAClass("void PlantedM(IVaultSession through) { _ = through.SaveText(\"a.md\", \"text\", null); }"),
            "unjoined"
        },

        // Joined.
        {
            "a writer in a lambda nested inside an admission lambda",
            InAClass(
                "void PlantedM() { _ = saves.Admit(\"a.md\", ticket => { Action inner = () => { " + AWrite + " }; inner(); "
                + "return Task.CompletedTask; }); }"),
            "joined"
        },
        {
            "a writer in an admission lambda that an unadmitted lambda holds",
            InAClass(
                "void PlantedM() { Action outer = () => { _ = saves.Admit(\"a.md\", ticket => { " + AWrite
                + " return Task.CompletedTask; }); }; outer(); }"),
            "joined"
        },
        {
            "a writer in an Admit lambda",
            InAClass("void PlantedM() { _ = saves.Admit(\"a.md\", ticket => { " + AWrite + " return Task.CompletedTask; }); }"),
            "joined"
        },
        {
            "a writer in an Enqueue lambda",
            InAClass("void PlantedM() { _ = saves.Enqueue([\"path:a.md\"], ticket => { " + AWrite + " ticket.Complete(true); }); }"),
            "joined"
        },
        {
            "a method reached only through Task.Run inside an admission lambda",
            InAClass(
                "void PlantedWrite() { " + AWrite + " }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => Task.Run(() => PlantedWrite())); }"),
            "joined"
        },
        {
            "two calls deep with every caller admitted",
            InAClass(
                "void PlantedWrite() { " + AWrite + " }\n"
                + "void PlantedMiddle() { PlantedWrite(); }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => { PlantedMiddle(); return Task.CompletedTask; }); }"),
            "joined"
        },
        {
            "through AdmitPropertyWrite",
            InTheWorkspace(
                "void PlantedM() { AdmitPropertyWrite(\"a.md\", "
                + "() => { _ = _session.SaveText(\"a.md\", \"text\", null); return Task.CompletedTask; }, () => { }); }"),
            "joined"
        },
        {
            "through INoteSaveChain.Admit",
            InAClass("void PlantedM() { _ = chain.Admit(\"a.md\", () => { " + AWrite + " return Task.CompletedTask; }); }"),
            "joined"
        },
        {
            "a writer lambda in a local only an admission lambda reads",
            InAClass(
                "void PlantedM() { Func<Task> write = () => { " + AWrite + " return Task.CompletedTask; }; "
                + "_ = saves.Admit(\"a.md\", _ => write()); }"),
            "joined"
        },
        {
            "a method handed to the chain as its operation",
            InAClass(
                "Task PlantedWrite() { " + AWrite + " return Task.CompletedTask; }\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedWrite); }"),
            "joined"
        },

        // No site of rule 2: an exclusive create outside the admitted creators.
        {
            "CreateExclusiveBytes in a plain method",
            InAClass("void PlantedM() { _ = session.CreateExclusiveBytes(\"a.png\", []); }"),
            "clean"
        },

        // Reloads.
        {
            "a reload in a plain method",
            InAClass("void PlantedM(WorkspaceTabViewModel tab) { tab.ReplaceItem(tab.Item); }"),
            "unguarded"
        },
        {
            "the chain asked after the reload",
            InAClass("void PlantedM(WorkspaceTabViewModel tab) { tab.ReplaceItem(tab.Item); _ = saves.HasAdmittedSaveFor(\"a.md\"); }"),
            "unguarded"
        },
        {
            "HasAdmittedSaveFor before the reload",
            InAClass("void PlantedM(WorkspaceTabViewModel tab) { if (!saves.HasAdmittedSaveFor(\"a.md\")) { tab.ReplaceItem(tab.Item); } }"),
            "guarded"
        },
        {
            "KeepsBufferOnReseat before the reload",
            InTheWorkspace("void PlantedM(WorkspaceTabViewModel tab) { if (!KeepsBufferOnReseat(tab, \"a.md\")) { tab.ReplaceItem(tab.Item); } }"),
            "guarded"
        },
        {
            "a SaveTicket parameter",
            InAClass("void PlantedM(WorkspaceTabViewModel tab, WorkspaceSaveCoordinator.SaveTicket admittedBy) { tab.ReplaceItem(tab.Item); }"),
            "guarded"
        },
        {
            "a reload inside an admission lambda",
            InAClass(
                "void PlantedM(WorkspaceTabViewModel tab) { _ = saves.Admit(\"a.md\", _ => { tab.ReplaceItem(tab.Item); return Task.CompletedTask; }); }"),
            "guarded"
        },
        {
            "ReplaceItem(Item) inside the tab",
            InTheTab("void PlantedM() { ReplaceItem(Item); }"),
            "clean"
        },

        // The chain's one implementer.
        {
            "a second INoteSaveChain",
            PlantedPrelude
            + "internal sealed class PlantedChain : INoteSaveChain\n{\n"
            + "public Task<bool> Admit(string path, Func<Task> operation) => Task.FromResult(false);\n"
            + "public void TrackWorker(Task worker) { }\n}\n",
            "a second chain"
        },
    };

    /// <summary>The rows on planted source read under a real file's name,
    /// joined with the shell's reading: a second site of a callee a row
    /// lists, in another overload of the listed member (the count runs over
    /// every overload, so a second call in the same body counts the same
    /// way), and a new caller that reaches an excused member outside the
    /// chain.</summary>
    [Theory]
    [InlineData(
        "a second SaveText in another overload of PublishBasesExport",
        "WorkspaceViewModel.Bases.cs",
        "partial class WorkspaceViewModel { void PublishBasesExport(int planted) { _ = _session.SaveText(\"a.md\", \"text\", null); } }",
        "WorkspaceViewModel.Bases.cs (PublishBasesExport): SaveText is called at 2 site(s) and the row lists 1")]
    [InlineData(
        "a second AddTagToFiles in another overload of EditTag",
        "FilesSidebarViewModel.cs",
        "partial class FilesSidebarViewModel { void EditTag(int planted) { Delegate add = _session.AddTagToFiles; _ = add; } }",
        "FilesSidebarViewModel.cs (EditTag): AddTagToFiles is called at 2 site(s) and the row lists 1")]
    [InlineData(
        "a new caller of ReconcileTabsAfterPropertyWrite outside the chain",
        "WorkspaceViewModel.Properties.cs",
        "partial class WorkspaceViewModel { void PlantedCaller() { _ = ReconcileTabsAfterPropertyWrite(\"a.md\", \"hash\"); } }",
        "WorkspaceViewModel.Properties.cs (ReconcileTabsAfterPropertyWrite): WorkspaceViewModel.Properties.cs (PlantedCaller) calls it outside the chain")]
    public void ARowAnswersOnlyTheSitesAndTheCallersItLists(string form, string file, string members, string expected)
    {
        Reading all = Shell.Value.Reading.With(Planted(PlantedPrelude + members + "\n", file: file));
        string[] problems =
        [
            .. Problems(all, UnadmittedWriters(all, Writers()), [.. OutsideTheChain.Where(row => !IsAReloadRow(row))]),
            .. Problems(all, UnguardedReloads(all), [.. OutsideTheChain.Where(IsAReloadRow), .. NotThisClass]),
        ];
        Assert.True(
            problems.Any(problem => problem.StartsWith(expected, StringComparison.Ordinal)),
            $"{form}: the rows answered it. Problems found:\n" + string.Join("\n", problems));
    }

    private const string PlantedWriter =
        PlantedPrelude
        + "internal sealed class Planted(VaultSession session)\n"
        + "{\n"
        + "    private void PlantedWrite()\n"
        + "    {\n"
        + "        " + AWrite + "\n"
        + "    }\n"
        + "}\n";

    /// <summary>The rule finds a planted call with its file, line and
    /// member, so it can never pass by matching nothing.</summary>
    [Fact]
    public void ARuleNamesAPlantedCallsFileAndLine()
    {
        Reading all = Shell.Value.Reading.With(Planted(PlantedWriter));
        Assert.Equal(
            ["Planted.cs:7 (PlantedWrite): VaultSession.SaveText writes outside the note's save chain"],
            UnadmittedWriters(all, Writers()).Where(violation => violation.File == "Planted.cs").Select(violation => violation.ToString()));
    }

    /// <summary>Rule 1 on a planted call: a name in no table, a name in two,
    /// a table name the binding does not have, and a call spelled as a
    /// session method that does not bind each fail; a name in one table
    /// passes.</summary>
    [Fact]
    public void RuleOneSeesANameInNoTableAndANameInTwo()
    {
        Reading read = Planted(PlantedWriter);
        HashSet<string> session = SessionMethods.Value;

        Assert.Empty(Unclassified(read, session, new Tables(["SaveText"], [], [])));
        Assert.StartsWith(
            "Planted.cs:7 (PlantedWrite): VaultSession.SaveText is in no table",
            Assert.Single(Unclassified(read, session, new Tables([], [], []))),
            StringComparison.Ordinal);
        Assert.Contains(
            "VaultSession.SaveText is in 2 tables (NoteWriters, NotNoteBytes)",
            Assert.Single(Unclassified(read, session, new Tables(["SaveText"], [], ["SaveText"]))),
            StringComparison.Ordinal);
        Assert.Equal(
            ["NoteWriters names NoSuchMethod, which the binding's VaultSession does not have"],
            Unclassified(read, session, new Tables(["SaveText", "NoSuchMethod"], [], [])));

        Reading unbound = Planted(
            PlantedPrelude + "internal sealed class Planted\n{\n    private void PlantedWrite()\n    {\n        missing.SaveText(\"a.md\", \"text\", null);\n    }\n}\n",
            mustBind: false);
        Assert.StartsWith(
            "Planted.cs:7 (PlantedWrite): `SaveText` is the name of a session method and does not bind here",
            Assert.Single(Unclassified(unbound, session, new Tables(["SaveText"], [], []))),
            StringComparison.Ordinal);
    }
}
