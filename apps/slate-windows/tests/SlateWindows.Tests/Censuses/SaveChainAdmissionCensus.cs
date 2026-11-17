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
// innermost admission node or member that holds it. Nodes and edges
// are named by the symbol's original definition, so a generic method is one
// node however it is constructed, and a reduced extension call is taken back
// to the method it calls. An
// admission call is a bound call to WorkspaceSaveCoordinator.Admit or Enqueue,
// to INoteSaveChain.Admit, or to WorkspaceViewModel.AdmitPropertyWrite, and
// its operation argument is the argument for `operation` (`start` for
// Enqueue), never `abandoned`. An admission node is a node of its own,
// admitted whatever holds it, and only two things make one: a lambda that
// stands as the operation argument (its body is the operation), and a method
// group that does (its target is the node's one callee). What stands as the
// operation is read from the bound value of the argument that fills the
// parameter (IArgumentOperation.Value), never from how it is written. Between
// the parameter and a lambda (IAnonymousFunctionOperation) or a method group
// (IMethodReferenceOperation) only a form that hands a value on and runs no
// code may stand: a delegate creation, and a conversion that is an identity,
// an implicit reference conversion, a lambda's or a method group's conversion
// to a delegate, or a conditional's own, with no operator method. Each result
// of a conditional is read the same way. Parentheses and a null-forgiving
// mark are no operations. Anything else between them (a user-defined
// conversion, which can run the delegate it is given; a call; a creation of a
// wrapper; an await; a switch expression; a coalesce) admits nothing, and a
// lambda under it is an ordinary lambda of the node that holds the call.
// Everything else in or around an admission call runs, or may run, where the
// call stands, before any ticket exists, and belongs to that node too: a
// conditional's condition, a method group's receiver, a property read for a
// delegate, the receiver of the admission call, and its other arguments (the
// path, `abandoned`), none of which is unwrapped or read at all.
//
// The edges. They are drawn from the bound operation tree of every body,
// initialiser and attribute (SemanticModel.GetOperation), never from syntax
// kinds, so a call the language makes with no invocation expression is an
// edge all the same. An edge from A to B stands for each of these in A:
//   - a call of B: IInvocationOperation, written or implicit (a collection
//     initialiser's Add, a query clause's method, an interpolated string
//     handler's appends, a `this(...)` or `base(...)` initialiser, a reduced
//     extension call);
//   - a creation through B: IObjectCreationOperation, and a collection
//     expression's ICollectionExpressionOperation.ConstructMethod;
//   - a reference to B as a method group: IMethodReferenceOperation;
//   - a use of a property, an indexer or an event whose accessor B has code:
//     IPropertyReferenceOperation and IEventReferenceOperation, by the
//     accessor used (get, set, add or remove), and both where the tree does
//     not say which;
//   - a user-defined operator or conversion B: IBinaryOperation,
//     IUnaryOperation, IIncrementOrDecrementOperation,
//     ICompoundAssignmentOperation, IConversionOperation, ICoalesceOperation,
//     IArgumentOperation and ISpreadOperation;
//   - a member a pattern form uses: IImplicitIndexerReferenceOperation (an
//     index or a range), IListPatternOperation, ISlicePatternOperation,
//     IRecursivePatternOperation (Deconstruct), IWithOperation and
//     IRangeOperation;
//   - and, asked of the semantic model because the operation carries no
//     symbol: the GetEnumerator, MoveNext, Current and Dispose of a
//     `foreach`, the GetAwaiter, IsCompleted and GetResult of an `await`,
//     and the Deconstruct of a deconstruction.
// The sites (a call to a session method, a reload of a tab) are found from
// the same operations, so one reached as a method group is a site too.
// Syntax is read for these and nothing else. To find the invocation
// expressions that are admission calls; what each is handed is then read from
// its operation, and one with no operation fails rule 1. For the tests that
// are about a place in the source: which lambda or member holds a call; which
// names in a member read a local (shape (B): every name bound to the local
// that is not the left of a plain assignment is a read, so any other use
// counts against the shape); and rule 3's "asked before" (the asking call is
// a bound IInvocationOperation, stands earlier in the member, and stands in a
// body that holds the reload). And as a fallback where no operation stands,
// which is source that does not compile: such a call is weighed by its
// candidates, and one spelled as a session method that binds to nothing fails
// rule 1. A fact holds the shell to no fallback read
// (TheShellsCallsAreAllReadFromOperations). Rule 3's other test, a save
// ticket among the member's parameters, is read from the declared symbol: the
// coordinator's own SaveTicket type, not a type of that name.
//
// The admitted set. Rule (A) refuses by default. The set holds every
// admission node, and a node besides only when all three hold: it is NAMED
// in AdmittedByItsCallers; it has a caller, and every caller the graph holds
// is in the set; and the open rule below does not exclude it. A row of the
// table is the first line. It asserts, on a reader's check, that every way
// to reach the member is inside a note's ticket. The graph and the open rule
// are second lines behind it: they can refute a row and can never stand in
// for one, so a gap in either admits nothing by itself. A fact holds the
// table to the shell (EveryMemberAdmittedByItsCallersIsNamedAndEveryRowHolds).
// A helper between an admission and a site that no row names fails. So does
// a row whose member is gone, cannot be named, has a caller outside the
// chain, leads to no site any more, or stands for another count of members
// than it says. A row names a member by file and name, so overloads, and
// members of one name in two types of one file, are told apart by that
// count. A call is admitted when either holds:
//   (A) its node is in the admitted set. Some nodes are never admitted this
//       way, and cannot be named, because the graph may not hold all of
//       their callers:
//       - one that implements an interface member (implicitly or
//         explicitly), that overrides, or that is virtual or abstract: a
//         call through the interface or the base binds to another symbol;
//       - a method shaped like an event handler (a sender and arguments
//         derived from EventArgs): XAML wires such a method in generated
//         code this census does not read;
//       - a member the language, the runtime or the binding engine can call
//         with no operation that names it. By kind: every constructor (a
//         derived type's constructor chains to it unwritten; XAML and a
//         `new()` constraint construct with no call here), a static
//         constructor, a finaliser, a user-defined operator or conversion (a
//         tuple comparison and `&&` on a user type name none), and every
//         accessor (a binding reads and sets a property, generated code
//         subscribes an event, and `foreach`, `await`, a range and a pattern
//         read Current, IsCompleted, Length, Count and an indexer). By
//         attribute: a module initialiser. By name alone, whatever the
//         parameters, where no kind exists (the language calls a pattern
//         member with optional and params parameters unwritten): Dispose
//         and DisposeAsync (`using`); GetEnumerator, GetAsyncEnumerator,
//         MoveNext, MoveNextAsync and Current (`foreach`); GetAwaiter,
//         GetResult, IsCompleted, OnCompleted and UnsafeOnCompleted
//         (`await`); Deconstruct; GetPinnableReference (`fixed`); Slice,
//         Length and Count (a range or an index); and Add (a collection
//         initialiser or expression). The query pattern's names (Select,
//         Where and the rest) stay closed: a query clause is an implicit
//         invocation, so its edge is drawn. The edges above are drawn for
//         the open members all the same; this is the floor under them; or
//   (B) it stands in a lambda assigned to a local of a delegate type, through
//       those same forms only, and every read of that local in the member
//       is handed to an admission call as its operation
//       or stands in an admission lambda (the property write's shape: the
//       write is built first and handed to the admitted operation). The
//       lambda is assigned in the local's declaration or in a statement of
//       its own, never in an expression whose value is used.
// A delegate that reaches an admission call any other way is not followed
// back to the code it was made from: one that arrives as a parameter, or from
// a field, a property, a call or an element of a collection, a local of a
// type that is no delegate, and a local function passed by name. A write
// there counts as its maker's, so the rule names it even where it does run
// inside the ticket.
// The same cost falls on the members (A) never admits: a write in a
// constructor, an accessor, an operator or a pattern member is named even
// where its every call stands inside a ticket.
//
// What the census does not prove. That the path admitted is the path
// written. That the worker is handed to TrackWorker. That the ticket settles
// on every path. That a route re-checks its gates at its turn. The facts in
// SaveChainAdmissionTests answer those four for each route. The rest have no
// fact and are for review. A call through a delegate kept in a field or a
// property, or through any interface other than INoteSaveChain. That a lambda
// or a method group created inside an admitted node runs before the operation
// ends: one kept and run later counts as admitted and is not, and so does the
// rest of an iterator or an async method after it first yields or awaits. For
// rule 3, that the answer of HasAdmittedSaveFor gates the reload, or that it
// was asked about the path that is reloaded: the rule sees only a call
// standing before the reload. That a method does what its name says: every
// table classifies by name. Not seen at all: a write made through reflection
// or a dynamic call, and a direct System.IO write to a vault note. Not seen
// as a caller: generated code (XAML's code-behind, a source generator), so an
// ordinary method that such code calls, that has no event handler's shape and
// that an admitted node also calls counts as admitted. Not an edge, and held
// by the open rule alone: the Dispose of a `using`, the Add calls of a
// collection expression, the operators of a tuple comparison, the `true` and
// `false` operators of `&&` and `||`, a construction through a type
// parameter, and the awaiter of an `await foreach` or an `await using`. A
// static field's initialiser and a module initialiser are nodes with no
// caller, so what they call has a caller outside the chain.

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
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

    /// <summary>One member rule (A) may admit by its callers: a helper
    /// between an admission and a call the rules weigh. The row asserts
    /// that every way to reach the member is inside a note's ticket. The
    /// graph and the open rule can only refute that, never show it, so the
    /// member is named here by a reader who checked. <c>Overloads</c> is
    /// how many members of that name in the file the row stands for:
    /// overloads, and members of one name in two types of one file.</summary>
    internal sealed record NamedMember(string File, string Member, string Reason, int Overloads = 1);

    private static readonly NamedMember[] AdmittedByItsCallers =
    [
        new(
            "WorkspaceViewModel.cs",
            "StartSave",
            "a tab's save at its turn: run only by the lambda SaveAsync hands to Enqueue"),
        new(
            "WorkspaceViewModel.cs",
            "WriteForSave",
            "a tab's save, its write on the worker: called only by StartSave"),
        new(
            "WorkspaceViewModel.cs",
            "StartAdmittedTaskToggle",
            "the tab toggle's admitted operation: run only by the lambda ToggleTask hands to Admit"),
        new(
            "WorkspaceViewModel.cs",
            "PerformTaskToggle",
            "the tab toggle's worker: started only by StartAdmittedTaskToggle, whose ticket ends when it has published"),
        new(
            "WorkspaceViewModel.History.cs",
            "RestoreAdmitted",
            "the restore's admitted operation: run only by the lambda PerformRestore hands to Admit"),
        new(
            "WorkspaceViewModel.History.cs",
            "FinishRestore",
            "the restore's publication: run only by RestoreAdmitted, inside its ticket"),
        new(
            "WorkspaceViewModel.History.cs",
            "ReloadOpenTabFromDiskAdmitted",
            "the restore's reload of its tab: called only by FinishRestore"),
        new(
            "WorkspaceViewModel.History.cs",
            "RecoverAdmitted",
            "the recover's admitted operation: run only by the lambda RecoverDeletedFile hands to AdmitPropertyWrite"),
        new(
            "WorkspaceViewModel.History.cs",
            "FinishRecover",
            "the recover's publication: run only by RecoverAdmitted, inside its ticket"),
        new(
            "WorkspaceViewModel.Bases.cs",
            "BasesCellWriteAdmitted",
            "the open-note cell write's admitted operation: run by the lambda BasesApplyPropertyEdit hands to "
            + "AdmitPropertyWrite, and by BasesTablessWriteAdmitted when a tab opened while it waited"),
        new(
            "WorkspaceViewModel.Bases.cs",
            "BasesTablessWriteAdmitted",
            "the tabless cell write's admitted operation: run only by the lambda BasesApplyPropertyEdit hands to "
            + "AdmitPropertyWrite"),
        new(
            "WorkspaceViewModel.Bases.cs",
            "BasesTablessWriteBody",
            "the tabless cell write's worker and publication: started only by BasesTablessWriteAdmitted, whose "
            + "ticket ends when it has published"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "AddAdmitted",
            "the property add's admitted operation: run only by the lambdas CommitAddProperty and "
            + "RetryAddWithFreshHash hand to AdmitPropertyWrite"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "AddWriteCompletion",
            "the property add's completion, which reconciles the note's tabs: built only by AddAdmitted and run "
            + "before its operation ends"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "RowWriteAdmitted",
            "the property row write's admitted operation: run only by the lambdas DispatchRowWrite and "
            + "RetryPropertyEditWithFreshHash hand to AdmitPropertyWrite"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "RowWriteAfterHeaderWait",
            "the row write once the header has re-read the saved bytes: called only by RowWriteAdmitted"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "StartRowWrite",
            "the row write's worker start: called only by RowWriteAdmitted and RowWriteAfterHeaderWait"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "PropertyWriteCompletion",
            "the row write's completion, which reconciles the note's tabs: built only by StartRowWrite and run "
            + "before its operation ends"),
        new(
            "WorkspaceViewModel.Properties.cs",
            "ReconcileAfterReadBack",
            "a failed property write's reconcile after its read-back: called only by the two completions"),
        new(
            "Panels/TasksReviewViewModel.cs",
            "DirectToggleAtItsTurn",
            "the review's direct toggle, its admitted operation: run only by the lambda ToggleTask hands to "
            + "INoteSaveChain.Admit"),
    ];

    private static string Label(NamedMember row) => $"{row.File} ({row.Member})";

    private static HashSet<string> Labels(IEnumerable<NamedMember> rows) => new(rows.Select(Label), StringComparer.Ordinal);

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

        /// <summary>The members whose callers the graph may not all hold: one
        /// a call can reach through another symbol (it implements an
        /// interface member, overrides, or is virtual or abstract), one
        /// shaped like an event handler, and one the language, the runtime
        /// or the binding engine can call with no operation that names it.
        /// Rule (A) never admits them.</summary>
        public HashSet<string> Open { get; } = new(StringComparer.Ordinal);

        public List<Violation> Unbound { get; } = [];

        public List<string> ChainImplementers { get; } = [];

        /// <summary>How many bound operation trees were walked.</summary>
        public int TreesWalked { get; set; }

        /// <summary>Each call the syntax fallback read because no operation
        /// stood for it. A source that compiles has none.</summary>
        public List<string> ReadFromSyntax { get; } = [];

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
                joined.TreesWalked += part.TreesWalked;
                joined.ReadFromSyntax.AddRange(part.ReadFromSyntax);
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
        /// <summary>The admission nodes: each lambda that stands as an
        /// operation, and the name of each method group that does.</summary>
        private readonly Dictionary<SyntaxNode, string> _admissionArguments = [];

        /// <summary>Each read of a local handed to an admission call as its
        /// operation, for shape (B).</summary>
        private readonly HashSet<SyntaxNode> _localsHandedOver = [];
        private readonly HashSet<ITypeSymbol> _sessionTypes = SessionTypes(model.Compilation);

        /// <summary>The top of every operation tree walked, by its syntax.</summary>
        private readonly HashSet<SyntaxNode> _walked = [];

        /// <summary>The syntax of every call, creation and method group an
        /// operation showed, so the fallback reads only what none did.</summary>
        private readonly HashSet<SyntaxNode> _seen = [];

        /// <summary>Where a site was counted, so no call is counted twice.</summary>
        private readonly HashSet<(int Start, string Name)> _sited = [];

        private readonly Dictionary<ISymbol, string> _ids = new(SymbolEqualityComparer.Default);

        public void Read(SyntaxNode root)
        {
            // The admission arguments first: every node below is placed
            // against them.
            foreach (InvocationExpressionSyntax written in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                IMethodSymbol[] bound = Bound(written);
                if (!bound.Any(method => AdmissionParameter(method) is not null))
                {
                    continue;
                }
                reading.AdmissionCalls.Add((file, LineOf(written)));
                // What the call is handed is read from its bound operation:
                // the value of the argument that fills the parameter. An
                // admission call with no such operation (it does not
                // compile) admits nothing, and fails rule 1.
                if (model.GetOperation(written) is IInvocationOperation invocation
                    && AdmissionParameter(invocation.TargetMethod) is { } parameter
                    && invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == parameter) is { } handed)
                {
                    AdmitWhatStandsIn(handed.Value);
                    continue;
                }
                reading.Unbound.Add(new Violation(
                    file, LineOf(written), MemberOf(written).Name, bound[0].Name,
                    "the census cannot read the operation this admission call is handed"));
            }

            // The calls, from the bound operation tree of every body,
            // initialiser and attribute: a call the language makes with no
            // invocation expression is an operation there all the same.
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (node is BaseTypeDeclarationSyntax type)
                {
                    ReadType(type);
                }
                else if (HoldsAnOperationTree(node))
                {
                    WalkTheTreeOf(node);
                }
            }

            // The syntax, only where no operation stood: a call or a creation
            // in a place the pass above did not start from is walked from its
            // own tree, and one that binds to no operation (it does not
            // compile) is weighed by its candidates, as is a name that could
            // be a method group. Fail closed.
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (_seen.Contains(node))
                {
                    continue;
                }
                switch (node)
                {
                    case InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax:
                        WalkTheTreeOf(node);
                        if (!_seen.Contains(node)
                            && Weigh(node, Bound(node), node is InvocationExpressionSyntax called ? CalleeName(called) : null))
                        {
                            reading.ReadFromSyntax.Add($"{file}:{LineOf(node)}: {node}");
                        }
                        break;
                    case SimpleNameSyntax name when IsMethodGroupCandidate(name):
                        if (Weigh(name, Bound(name), name.Identifier.ValueText))
                        {
                            reading.ReadFromSyntax.Add($"{file}:{LineOf(name)}: {name.Parent}");
                        }
                        break;
                }
            }
        }

        /// <summary>The syntax a bound operation tree hangs from: a body, an
        /// expression body, an initialiser, an attribute.</summary>
        private static bool HoldsAnOperationTree(SyntaxNode node) => node is
            BaseMethodDeclarationSyntax
            or AccessorDeclarationSyntax
            or AttributeSyntax
            or GlobalStatementSyntax
            or PrimaryConstructorBaseTypeSyntax
            or ArrowExpressionClauseSyntax { Parent: BasePropertyDeclarationSyntax }
            or EqualsValueClauseSyntax
            {
                Parent: VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax }
                    or PropertyDeclarationSyntax
                    or ParameterSyntax
                    or EnumMemberDeclarationSyntax,
            };

        /// <summary>Walk the whole operation tree <paramref name="node"/>
        /// stands in, once, from its top.</summary>
        private void WalkTheTreeOf(SyntaxNode node)
        {
            IOperation? top = model.GetOperation(node);
            if (top is null && node is ArrowExpressionClauseSyntax arrow)
            {
                top = model.GetOperation(arrow.Expression);
            }
            if (top is null)
            {
                return;
            }
            while (top.Parent is { } parent)
            {
                top = parent;
            }
            if (!_walked.Add(top.Syntax))
            {
                return;
            }
            reading.TreesWalked++;
            var pending = new Stack<IOperation>();
            pending.Push(top);
            while (pending.Count > 0)
            {
                IOperation operation = pending.Pop();
                ReadOperation(operation);
                if (operation is INameOfOperation)
                {
                    // A name, not a call: nothing under it runs.
                    continue;
                }
                foreach (IOperation child in operation.ChildOperations)
                {
                    pending.Push(child);
                }
            }
        }

        /// <summary>The members one operation calls. Every kind of operation
        /// that carries a member the shell could declare is here; the
        /// header lists them.</summary>
        private void ReadOperation(IOperation operation)
        {
            SyntaxNode at = operation.Syntax;
            switch (operation)
            {
                case INameOfOperation:
                    _ = _seen.Add(at);
                    break;
                case IInvocationOperation invocation:
                    _ = _seen.Add(at);
                    Weigh(at, [invocation.TargetMethod], null);
                    break;
                case IObjectCreationOperation creation:
                    _ = _seen.Add(at);
                    Call(at, creation.Constructor);
                    break;
                case IMethodReferenceOperation reference:
                    SyntaxNode name = NamePart(at);
                    _ = _seen.Add(name);
                    Weigh(name, [reference.Method], null);
                    break;
                case IPropertyReferenceOperation property:
                    (bool reads, bool writes) = UseOf(property);
                    if (reads)
                    {
                        Accessor(at, property.Property.GetMethod);
                    }
                    if (writes)
                    {
                        Accessor(at, property.Property.SetMethod);
                    }
                    break;
                case IEventReferenceOperation raised:
                    // Subscribed or unsubscribed; anything else, both.
                    bool? adds = raised.Parent is IEventAssignmentOperation assignment && assignment.EventReference == raised
                        ? assignment.Adds
                        : null;
                    if (adds != false)
                    {
                        Accessor(at, raised.Event.AddMethod);
                    }
                    if (adds != true)
                    {
                        Accessor(at, raised.Event.RemoveMethod);
                    }
                    break;
                case IImplicitIndexerReferenceOperation indexed:
                    Member(at, indexed.IndexerSymbol);
                    Member(at, indexed.LengthSymbol);
                    break;
                case IListPatternOperation list:
                    Member(at, list.IndexerSymbol);
                    Member(at, list.LengthSymbol);
                    break;
                case ISlicePatternOperation slice:
                    Member(at, slice.SliceSymbol);
                    break;
                case IRecursivePatternOperation recursive:
                    Member(at, recursive.DeconstructSymbol);
                    break;
                case IBinaryOperation binary:
                    Call(at, binary.OperatorMethod);
                    break;
                case IUnaryOperation unary:
                    Call(at, unary.OperatorMethod);
                    break;
                case IIncrementOrDecrementOperation step:
                    Call(at, step.OperatorMethod);
                    break;
                case ICompoundAssignmentOperation compound:
                    Call(at, compound.OperatorMethod);
                    Call(at, compound.InConversion.MethodSymbol);
                    Call(at, compound.OutConversion.MethodSymbol);
                    break;
                case IConversionOperation conversion:
                    Call(at, conversion.OperatorMethod);
                    break;
                case ICoalesceOperation coalesce:
                    Call(at, coalesce.ValueConversion.MethodSymbol);
                    break;
                case IArgumentOperation argument:
                    Call(at, argument.InConversion.MethodSymbol);
                    Call(at, argument.OutConversion.MethodSymbol);
                    break;
                case ICollectionExpressionOperation collection:
                    Call(at, collection.ConstructMethod);
                    break;
                case ISpreadOperation spread:
                    Call(at, spread.ElementConversion.MethodSymbol);
                    break;
                case IWithOperation with:
                    Call(at, with.CloneMethod);
                    break;
                case IRangeOperation range:
                    Call(at, range.Method);
                    break;
                case IForEachLoopOperation when at is CommonForEachStatementSyntax loop:
                    ForEachStatementInfo each = model.GetForEachStatementInfo(loop);
                    Call(at, each.GetEnumeratorMethod);
                    Call(at, each.MoveNextMethod);
                    Accessor(at, each.CurrentProperty?.GetMethod);
                    Call(at, each.DisposeMethod);
                    Call(at, each.ElementConversion.MethodSymbol);
                    Call(at, each.CurrentConversion.MethodSymbol);
                    if (loop is ForEachVariableStatementSyntax variables)
                    {
                        Deconstruction(at, model.GetDeconstructionInfo(variables));
                    }
                    break;
                case IAwaitOperation when at is AwaitExpressionSyntax awaited:
                    AwaitExpressionInfo awaiter = model.GetAwaitExpressionInfo(awaited);
                    Call(at, awaiter.GetAwaiterMethod);
                    Accessor(at, awaiter.IsCompletedProperty?.GetMethod);
                    Call(at, awaiter.GetResultMethod);
                    break;
                case IDeconstructionAssignmentOperation when at is AssignmentExpressionSyntax assigned:
                    Deconstruction(at, model.GetDeconstructionInfo(assigned));
                    break;
            }
        }

        private void Call(SyntaxNode at, IMethodSymbol? method)
        {
            if (method is not null)
            {
                Weigh(at, [method], null);
            }
        }

        /// <summary>An accessor, when it has code: an auto-property's holds
        /// no call and no site.</summary>
        private void Accessor(SyntaxNode at, IMethodSymbol? accessor)
        {
            if (accessor is not null
                && accessor.OriginalDefinition.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is
                    AccessorDeclarationSyntax { Body: not null }
                    or AccessorDeclarationSyntax { ExpressionBody: not null }
                    or ArrowExpressionClauseSyntax))
            {
                Weigh(at, [accessor], null);
            }
        }

        private void Member(SyntaxNode at, ISymbol? member)
        {
            switch (member)
            {
                case IMethodSymbol method:
                    Call(at, method);
                    break;
                case IPropertySymbol property:
                    Accessor(at, property.GetMethod);
                    break;
            }
        }

        private void Deconstruction(SyntaxNode at, DeconstructionInfo info)
        {
            Call(at, info.Method);
            Call(at, info.Conversion?.MethodSymbol);
            foreach (DeconstructionInfo nested in info.Nested)
            {
                Deconstruction(at, nested);
            }
        }

        /// <summary>Whether a reference to a property reads it, writes it or
        /// both. Where the tree does not say, both.</summary>
        private static (bool Reads, bool Writes) UseOf(IOperation reference) => reference.Parent switch
        {
            ISimpleAssignmentOperation assignment when assignment.Target == reference => (false, true),
            ICompoundAssignmentOperation or ICoalesceAssignmentOperation or IIncrementOrDecrementOperation
                or IDeconstructionAssignmentOperation or ITupleOperation => (true, true),
            IArgumentOperation { Parameter.RefKind: not RefKind.None } => (true, true),
            _ => (true, false),
        };

        /// <summary>The name a method group is written with: the node an
        /// admission is registered on, and the one the fallback would
        /// otherwise read again.</summary>
        private static SyntaxNode NamePart(SyntaxNode written) => written switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            MemberBindingExpressionSyntax binding => binding.Name,
            AliasQualifiedNameSyntax alias => alias.Name,
            QualifiedNameSyntax qualified => qualified.Right,
            _ => written,
        };

        /// <summary>What an operation argument admits, read from its bound
        /// value and never from how it is written. A lambda: its body is the
        /// operation. A method group: its target is, and the name is the
        /// node that calls it. A local of a delegate type: the lambda
        /// assigned to it is weighed by shape (B). Between the parameter and
        /// any of those, only a form that hands its value on and runs no
        /// code may stand (<see cref="HandsItsValueOn"/>), and each result
        /// of a conditional is read this way. Anything else (a user-defined
        /// conversion, a call, a creation of a wrapper, an await, a switch
        /// expression, a coalesce) runs, or may run, where the admission
        /// call stands, before any ticket exists, and admits nothing: a
        /// lambda under it is an ordinary lambda of the node that holds the
        /// call.</summary>
        private void AdmitWhatStandsIn(IOperation value)
        {
            switch (value)
            {
                case IDelegateCreationOperation creation:
                    AdmitWhatStandsIn(creation.Target);
                    return;
                case IConversionOperation conversion when RunsNoCode(conversion):
                    AdmitWhatStandsIn(conversion.Operand);
                    return;
                case IConditionalOperation { WhenFalse: { } otherwise } conditional:
                    AdmitWhatStandsIn(conditional.WhenTrue);
                    AdmitWhatStandsIn(otherwise);
                    return;
                case IAnonymousFunctionOperation function:
                    Admit(function.Syntax);
                    return;
                case IMethodReferenceOperation reference:
                    Admit(NamePart(Bare(reference.Syntax)));
                    return;
                case ILocalReferenceOperation { Local.Type.TypeKind: TypeKind.Delegate } local:
                    _ = _localsHandedOver.Add(Bare(local.Syntax));
                    return;
            }
        }

        /// <summary>A conversion that runs no code: an identity, an implicit
        /// reference conversion, a lambda's or a method group's conversion
        /// to a delegate, or the conversion of a conditional to the type it
        /// is handed to (each of its results carries its own), with no
        /// operator method. A user-defined conversion can run the delegate
        /// it is given.</summary>
        private static bool RunsNoCode(IConversionOperation conversion)
        {
            if (conversion.OperatorMethod is not null)
            {
                return false;
            }
            Conversion kind = conversion.GetConversion();
            return !kind.IsUserDefined
                && (kind.IsIdentity
                    || (kind.IsImplicit && kind.IsReference)
                    || kind.IsMethodGroup
                    || kind.IsAnonymousFunction
                    || kind.IsConditionalExpression);
        }

        /// <summary>True when <paramref name="parent"/> does nothing with
        /// <paramref name="child"/> but hand its value on: a delegate
        /// creation over it, a conversion of it that runs no code, a
        /// conditional it is a result of.</summary>
        private static bool HandsItsValueOn(IOperation parent, IOperation child) => parent switch
        {
            IDelegateCreationOperation creation => creation.Target == child,
            IConversionOperation conversion => conversion.Operand == child && RunsNoCode(conversion),
            IConditionalOperation conditional => conditional.Condition != child,
            _ => false,
        };

        /// <summary>The expression under parentheses and a null-forgiving
        /// mark, neither of which is an operation.</summary>
        private static SyntaxNode Bare(SyntaxNode written)
        {
            while (true)
            {
                switch (written)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        written = parenthesized.Expression;
                        break;
                    case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } forgiven:
                        written = forgiven.Operand;
                        break;
                    default:
                        return written;
                }
            }
        }

        private void Admit(SyntaxNode operation)
        {
            if (_admissionArguments.ContainsKey(operation))
            {
                return;
            }
            FileLinePositionSpan at = operation.GetLocation().GetLineSpan();
            string node = $"admission:{file}:{at.StartLinePosition.Line + 1}:{at.StartLinePosition.Character + 1}";
            _admissionArguments[operation] = node;
            _ = reading.AdmissionNodes.Add(node);
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

        /// <summary>Add the edge and the site a call makes; true when it
        /// made either.</summary>
        private bool Weigh(SyntaxNode node, IMethodSymbol[] methods, string? spelled)
        {
            bool read = false;
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
                return read;
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
                    reading.Edges.Add((caller, IdOf(method)));
                    if (!reading.NodeNames.ContainsKey(caller))
                    {
                        reading.NodeNames[caller] = $"{file} ({MemberOf(node).Name})";
                    }
                    read = true;
                }
                SiteKind? kind = method.ContainingType is { } owner
                    && _sessionTypes.Contains(owner.OriginalDefinition)
                    && sessionMethods.Contains(method.Name)
                        ? SiteKind.Session
                        : IsShellType(method.ContainingType, "WorkspaceTabViewModel")
                            && ReloadMethods.Contains(method.Name)
                            && !InsideTheTab(node)
                                ? SiteKind.Reload
                                : null;
                if (kind is { } site && _sited.Add((node.SpanStart, method.Name)))
                {
                    reading.Sites.Add(SiteAt(site, node, method.Name));
                    read = true;
                }
            }
            return read;
        }

        private Site SiteAt(SiteKind kind, SyntaxNode node, string name)
        {
            (string member, SyntaxNode? declaration) = MemberOf(node);
            // The node that holds a site has a name even when it calls
            // nothing the shell declares.
            _ = reading.NodeNames.TryAdd(NodeOf(node), $"{file} ({member})");
            return new Site(
                kind,
                file,
                LineOf(node),
                member,
                NodeOf(node),
                name,
                declaration is not null && HandedToAnAdmission(node, declaration),
                declaration is BaseMethodDeclarationSyntax method
                    && model.GetDeclaredSymbol(method) is IMethodSymbol holder
                    && holder.Parameters.Any(parameter => IsTheSaveTicket(parameter.Type)),
                declaration is not null && declaration.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                    call.SpanStart < node.SpanStart
                    && model.GetOperation(call) is IInvocationOperation asked
                    && AsksTheChain(asked.TargetMethod)
                    && StandsInABodyThatHolds(call, node, declaration)));
        }

        /// <summary>The coordinator's own ticket type, by its symbol: a type
        /// that only shares the name holds nothing.</summary>
        private static bool IsTheSaveTicket(ITypeSymbol type) =>
            type is INamedTypeSymbol { Name: "SaveTicket" } ticket && IsShellType(ticket.ContainingType, "WorkspaceSaveCoordinator");

        /// <summary>The call that asks the chain, by its bound target: one
        /// that binds to no single method asks nothing.</summary>
        private static bool AsksTheChain(IMethodSymbol bound)
        {
            IMethodSymbol method = (bound.ReducedFrom ?? bound).OriginalDefinition;
            return (method.Name == "HasAdmittedSaveFor" && IsShellType(method.ContainingType, "WorkspaceSaveCoordinator"))
                || (method.Name == "KeepsBufferOnReseat" && IsShellType(method.ContainingType, "WorkspaceViewModel"));
        }

        /// <summary>"Before" is a place in the source, so it is read from
        /// syntax; but the asking call must stand in a body that holds the
        /// reload. One asked inside another lambda or local function need
        /// never have run when the reload does.</summary>
        private static bool StandsInABodyThatHolds(SyntaxNode call, SyntaxNode reload, SyntaxNode declaration)
        {
            SyntaxNode? body = call.Ancestors()
                .TakeWhile(ancestor => ancestor != declaration)
                .FirstOrDefault(ancestor => ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
            return body is null || reload.Ancestors().Contains(body);
        }

        /// <summary>Shape (B): the call stands in a lambda assigned to a
        /// local of a delegate type, and every read of that local in the
        /// member is handed to an admission call as its operation or stands
        /// in an admission lambda. A read is every name that binds to the
        /// local and is not the left of a plain assignment, so any other
        /// use of it (a call, a `ref`, a compound assignment) counts
        /// against the shape.</summary>
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
                if (reads.Length > 0
                    && reads.All(read => _localsHandedOver.Contains(read) || read.Ancestors().Any(_admissionArguments.ContainsKey)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsAssignedTo(IdentifierNameSyntax name) =>
            name.Parent is AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment
            && assignment.Left == name;

        /// <summary>The local a lambda is assigned to, read from the bound
        /// operations: the lambda is the value of the local's initialiser,
        /// or of an assignment that is a statement of its own (one whose
        /// value is used, called at once say, hands the lambda on unseen),
        /// through nothing but forms that hand a value on and run no code,
        /// and the local is of a delegate type.</summary>
        private ILocalSymbol? LocalAssigned(AnonymousFunctionExpressionSyntax function)
        {
            IOperation? value = model.GetOperation(function);
            if (value is IDelegateCreationOperation { Target: IAnonymousFunctionOperation made })
            {
                value = made;
            }
            if (value is not IAnonymousFunctionOperation)
            {
                return null;
            }
            while (value.Parent is { } parent && HandsItsValueOn(parent, value))
            {
                value = parent;
            }
            ILocalSymbol? local = value.Parent switch
            {
                IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
                ISimpleAssignmentOperation { Target: ILocalReferenceOperation target, Parent: IExpressionStatementOperation } assignment
                    when assignment.Value == value => target.Local,
                _ => null,
            };
            return local is { Type.TypeKind: TypeKind.Delegate } ? local : null;
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
                    return IdOf(symbol);
                }
            }
            return "(no member)";
        }

        private string IdOf(ISymbol symbol)
        {
            if (!_ids.TryGetValue(symbol, out string? id))
            {
                id = Id(symbol);
                _ids[symbol] = id;
            }
            return id;
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

        /// <summary>Which members of a type rule (A) never admits, and
        /// whether the type is a save chain.</summary>
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
                    || IsCalledWithNoOperation(method)
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

        /// <summary>A member the language, the runtime or the binding engine
        /// can call with no operation that names it, so the graph may not
        /// hold all of its callers. By kind: a constructor (a derived type's
        /// constructor chains to it unwritten, and XAML and a `new()`
        /// constraint construct with no call here), a static constructor, a
        /// finaliser, a user-defined operator or conversion (a tuple
        /// comparison names none), and every accessor (a binding reads and
        /// sets a property, generated code subscribes an event, and
        /// `foreach`, `await`, ranges and patterns read `Current`,
        /// `IsCompleted`, `Length`, `Count` and an indexer). By attribute: a
        /// module initialiser. By name alone, where no kind exists: the
        /// methods `using`, `foreach`, `await`, a deconstruction, `fixed`, a
        /// range and a collection initialiser or expression call.</summary>
        private static bool IsCalledWithNoOperation(IMethodSymbol method)
        {
            switch (method.MethodKind)
            {
                case MethodKind.Constructor
                    or MethodKind.StaticConstructor
                    or MethodKind.Destructor
                    or MethodKind.UserDefinedOperator
                    or MethodKind.Conversion
                    or MethodKind.PropertyGet
                    or MethodKind.PropertySet
                    or MethodKind.EventAdd
                    or MethodKind.EventRemove:
                    return true;
                case MethodKind.Ordinary:
                    break;
                default:
                    return false;
            }
            if (method.GetAttributes().Any(attribute => attribute.AttributeClass is { Name: "ModuleInitializerAttribute" } initialiser
                && initialiser.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices"))
            {
                return true;
            }
            // By the name alone, whatever the parameters and whether it is
            // an instance or an extension method: the language calls a
            // pattern member with optional and params parameters unwritten
            // (codex round 6: Dispose(int optional = 0) on a ref struct).
            return PatternMemberNames.Contains(method.Name);
        }

        /// <summary>The names the language calls a method by with no
        /// operation that names it: `using`, `foreach`, `await`, a
        /// deconstruction, `fixed`, a range or an index, and a collection
        /// initialiser or expression. The query pattern's names (Select,
        /// Where and the rest) are not here: a query clause is an implicit
        /// invocation, so the graph holds its edge.</summary>
        private static readonly HashSet<string> PatternMemberNames = new(StringComparer.Ordinal)
        {
            "Dispose", "DisposeAsync", "GetEnumerator", "GetAsyncEnumerator", "MoveNext", "MoveNextAsync", "Current",
            "GetAwaiter", "GetResult", "IsCompleted", "OnCompleted", "UnsafeOnCompleted", "Deconstruct",
            "GetPinnableReference", "Add", "Slice", "Length", "Count",
        };

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

    /// <summary>The admitted set, refusing by default: every admission
    /// node, and every node that is NAMED (<paramref name="named"/>, as
    /// <c>File (Member)</c>) and for which the closure holds.</summary>
    internal static HashSet<string> AdmittedNodes(Reading reading, IReadOnlySet<string> named) =>
        Closure(reading, CallersByNode(reading), NodesNamed(reading, named));

    /// <summary>The closure alone, whatever is named: the least set that
    /// holds every admission node and every node that has a caller, whose
    /// callers are all in the set, and that the open rule does not exclude
    /// (<see cref="Reading.Open"/>). It admits nothing by itself. It says
    /// which rows hold and which members a row could name.</summary>
    internal static HashSet<string> ClosedUnderCallers(Reading reading) => Closure(reading, CallersByNode(reading), only: null);

    /// <summary>Each node's callers. A call a member makes to itself is no
    /// caller.</summary>
    private static Dictionary<string, HashSet<string>> CallersByNode(Reading reading)
    {
        var direct = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach ((string caller, string callee) in reading.Edges)
        {
            if (caller == callee)
            {
                continue;
            }
            if (!direct.TryGetValue(callee, out HashSet<string>? callers))
            {
                callers = new HashSet<string>(StringComparer.Ordinal);
                direct[callee] = callers;
            }
            _ = callers.Add(caller);
        }
        return direct;
    }

    private static HashSet<string> Closure(Reading reading, Dictionary<string, HashSet<string>> direct, HashSet<string>? only)
    {
        var admitted = new HashSet<string>(reading.AdmissionNodes, StringComparer.Ordinal);
        bool grew = true;
        while (grew)
        {
            grew = false;
            foreach ((string node, HashSet<string> callers) in direct)
            {
                if ((only is null || only.Contains(node))
                    && callers.Count > 0
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

    /// <summary>The nodes a set of names stands for: every member of that
    /// name in that file, never an admission node.</summary>
    private static HashSet<string> NodesNamed(Reading reading, IReadOnlySet<string> named) =>
        new(
            reading.NodeNames.Where(pair => named.Contains(pair.Value) && !reading.AdmissionNodes.Contains(pair.Key)).Select(pair => pair.Key),
            StringComparer.Ordinal);

    /// <summary>The members the rules need rule (A) for: the helpers
    /// between an admission and a site. Every node that holds a call the
    /// rules weigh, or calls a node that holds one, and for which the
    /// closure holds; and every caller of one of these for which it holds
    /// too. The second kind of start is a helper that reaches, from inside
    /// a ticket, a member an exception row answers for: the row excuses
    /// only its callers outside the chain, so the ones inside are named
    /// like any other.</summary>
    internal static HashSet<string> BetweenAnAdmissionAndASite(
        Reading reading, HashSet<string> closed, Dictionary<string, HashSet<string>> direct)
    {
        var between = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        foreach (string holder in reading.Sites
            .Where(site => site.Kind == SiteKind.Reload || IsWeighed(site.Name))
            .Select(site => site.Node)
            .Distinct(StringComparer.Ordinal))
        {
            pending.Push(holder);
            foreach (string caller in direct.GetValueOrDefault(holder) ?? [])
            {
                pending.Push(caller);
            }
        }
        while (pending.Count > 0)
        {
            string node = pending.Pop();
            if (reading.AdmissionNodes.Contains(node) || !closed.Contains(node) || !between.Add(node))
            {
                continue;
            }
            foreach (string caller in direct.GetValueOrDefault(node) ?? [])
            {
                pending.Push(caller);
            }
        }
        return between;
    }

    /// <summary>For a site the rules refuse: the members that would have
    /// to be named for rule (A) to admit it, when the closure holds for
    /// its node; else nothing.</summary>
    private static Func<Site, string> ToName(Reading reading, HashSet<string> admitted)
    {
        Dictionary<string, HashSet<string>> direct = CallersByNode(reading);
        HashSet<string> closed = Closure(reading, direct, only: null);
        return site =>
        {
            if (!closed.Contains(site.Node) || reading.AdmissionNodes.Contains(site.Node))
            {
                return string.Empty;
            }
            var unnamed = new SortedSet<string>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>([site.Node]);
            while (pending.Count > 0)
            {
                string node = pending.Pop();
                if (reading.AdmissionNodes.Contains(node) || !seen.Add(node))
                {
                    continue;
                }
                if (!admitted.Contains(node))
                {
                    _ = unnamed.Add(reading.NodeNames.GetValueOrDefault(node, node));
                }
                foreach (string caller in direct.GetValueOrDefault(node) ?? [])
                {
                    pending.Push(caller);
                }
            }
            return unnamed.Count == 0
                ? string.Empty
                : $". Every caller the graph holds is admitted, but a member is admitted by its callers only when it is named: "
                    + $"name {string.Join(", ", unnamed)} in {nameof(AdmittedByItsCallers)} if every way to reach it is inside a "
                    + "note's ticket";
        };
    }

    /// <summary>What is wrong with the table. A member the rules need
    /// rule (A) for that no row names. A row with no reason. A member
    /// named twice. A row that names no member the walk reads. A row for
    /// a member rule (A) never admits (it cannot be named). A row whose
    /// member the closure does not hold for: it has no caller, or a caller
    /// outside the chain. A row whose member lies between no admission and
    /// no site any more. A row that stands for another count of members
    /// than it says.</summary>
    internal static IReadOnlyList<string> TableProblems(Reading reading, IReadOnlyList<NamedMember> rows)
    {
        Dictionary<string, HashSet<string>> direct = CallersByNode(reading);
        HashSet<string> closed = Closure(reading, direct, only: null);
        HashSet<string> between = BetweenAnAdmissionAndASite(reading, closed, direct);
        ILookup<string, string> nodes = reading.NodeNames
            .Where(pair => !reading.AdmissionNodes.Contains(pair.Key))
            .ToLookup(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);
        string Callers(string node) => string.Join(
            "; ",
            (direct.GetValueOrDefault(node) ?? []).Select(caller => reading.NodeNames.GetValueOrDefault(caller, caller))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        List<string> problems = [.. rows.Where(row => string.IsNullOrWhiteSpace(row.Reason)).Select(row => $"{Label(row)}: a row with no reason.")];
        problems.AddRange(rows.GroupBy(Label, StringComparer.Ordinal).Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: named {group.Count()} times."));
        HashSet<string> named = Labels(rows);
        foreach (IGrouping<string, string> needed in between
            .GroupBy(node => reading.NodeNames.GetValueOrDefault(node, node), StringComparer.Ordinal)
            .Where(group => !named.Contains(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            problems.Add($"{needed.Key}: is not named, and a call the rules weigh is reached through it ({needed.Count()} member(s) "
                + $"of that name; called from [{string.Join(" | ", needed.Select(Callers))}]). Name it in "
                + $"{nameof(AdmittedByItsCallers)} if every way to reach it is inside a note's ticket.");
        }
        foreach (NamedMember row in rows)
        {
            string[] members = [.. nodes[Label(row)]];
            if (members.Length == 0)
            {
                problems.Add($"{Label(row)}: the row names no member the walk reads (delete it).");
                continue;
            }
            if (members.Any(reading.Open.Contains))
            {
                problems.Add($"{Label(row)}: cannot be named. Rule (A) never admits it: it is virtual, an override or an "
                    + "interface's, has an event handler's shape, or is a member the language calls by itself.");
                continue;
            }
            int stands = members.Count(between.Contains);
            if (stands == 0)
            {
                string[] unheld = [.. members.Where(member => !closed.Contains(member))];
                problems.Add(unheld.Length == 0
                    ? $"{Label(row)}: lies between no admission and no call the rules weigh (delete it)."
                    : $"{Label(row)}: the row does not hold: " + string.Join(
                        " ",
                        unheld.Select(member => (direct.GetValueOrDefault(member) ?? []).Where(caller => !closed.Contains(caller))
                            .Select(caller => reading.NodeNames.GetValueOrDefault(caller, caller))
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())
                            .Select(outside => outside.Length == 0
                                ? "it has no caller the graph holds."
                                : $"{string.Join("; ", outside)} calls it outside the chain.")));
                continue;
            }
            if (stands != row.Overloads)
            {
                problems.Add($"{Label(row)}: the row stands for {stands} member(s) of that name between an admission and a site, "
                    + $"and says {row.Overloads}. Check the new one, then correct Overloads.");
            }
        }
        return problems;
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
    internal static IReadOnlyList<Violation> UnadmittedWriters(Reading reading, IReadOnlySet<string> writers, IReadOnlySet<string> named)
    {
        HashSet<string> admitted = AdmittedNodes(reading, named);
        Func<Site, string> toName = ToName(reading, admitted);
        return [.. reading.Sites
            .Where(site => site.Kind == SiteKind.Session && writers.Contains(site.Name) && !IsAdmitted(site, admitted))
            .Select(site => new Violation(
                site.File, site.Line, site.Member, site.Name,
                $"VaultSession.{site.Name} writes outside the note's save chain" + toName(site))
            { Node = site.Node })];
    }

    private static HashSet<string> Writers() =>
        new(NoteWriters.Select(writer => writer.Name).Concat(AdmittedCreators), StringComparer.Ordinal);

    [Fact]
    public void EveryNoteWriterCallIsAdmittedIntoItsNotesChain() =>
        AssertOnlyTheListed(
            Shell.Value.Reading,
            UnadmittedWriters(Shell.Value.Reading, Writers(), Labels(AdmittedByItsCallers)),
            [.. OutsideTheChain.Where(row => !IsAReloadRow(row))],
            "These write a note outside its save chain, so they can write beside a save admitted for the note and "
            + "not yet published. Admit the operation (WorkspaceSaveCoordinator.Admit, INoteSaveChain.Admit or "
            + "AdmitPropertyWrite), hand its worker to TrackWorker, and end its ticket on every path:");

    // ---- Rule 3: every reload of a tab from disk is admitted or asks ------

    /// <summary>Rule 3: every call from outside the tab to a method that
    /// replaces a tab's buffer from disk, unless it is admitted, its member
    /// holds a save ticket, or its member asks the chain before it.</summary>
    internal static IReadOnlyList<Violation> UnguardedReloads(Reading reading, IReadOnlySet<string> named)
    {
        HashSet<string> admitted = AdmittedNodes(reading, named);
        Func<Site, string> toName = ToName(reading, admitted);
        return [.. reading.Sites
            .Where(site => site.Kind == SiteKind.Reload
                && !IsAdmitted(site, admitted)
                && !site.HasTicketParameter
                && !site.ChainAskedBefore)
            .Select(site => new Violation(
                site.File, site.Line, site.Member, site.Name,
                $"WorkspaceTabViewModel.{site.Name} reloads a tab from disk outside the note's save chain and "
                + "without asking it" + toName(site))
            { Node = site.Node })];
    }

    [Fact]
    public void EveryReloadOfATabFromDiskIsAdmittedOrAsksTheChain() =>
        AssertOnlyTheListed(
            Shell.Value.Reading,
            UnguardedReloads(Shell.Value.Reading, Labels(AdmittedByItsCallers)),
            [.. OutsideTheChain.Where(IsAReloadRow), .. NotThisClass],
            "These replace a tab's buffer from disk under whatever is admitted for its note. Run the reload inside "
            + "the admitted operation, or ask HasAdmittedSaveFor (or KeepsBufferOnReseat) first and keep the tab "
            + "while it holds:");

    private static bool IsAReloadRow(Row row) => row.Calls.All(ReloadMethods.Contains);

    private static void AssertOnlyTheListed(Reading reading, IReadOnlyList<Violation> found, IReadOnlyList<Row> rows, string remedy)
    {
        IReadOnlyList<string> problems = Problems(reading, found, rows, Labels(AdmittedByItsCallers));
        Assert.True(problems.Count == 0, remedy + "\n" + string.Join("\n", problems));
    }

    /// <summary>What the rows do not answer. A violation in a member no row
    /// lists. A callee whose count of sites in the member, over every
    /// overload of that name in the file, differs from the count the row
    /// lists, so a second site of a listed callee fails. For a row with
    /// excused callers, a direct caller outside the chain that the row does
    /// not excuse, and an excused caller that is no longer one. A row with
    /// no reason, date or end. A row that matches no violation.</summary>
    internal static IReadOnlyList<string> Problems(
        Reading reading, IReadOnlyList<Violation> found, IReadOnlyList<Row> rows, IReadOnlySet<string> named)
    {
        List<string> problems = [.. rows
            .Where(row => string.IsNullOrWhiteSpace(row.Reason) || string.IsNullOrWhiteSpace(row.Dated) || string.IsNullOrWhiteSpace(row.Ends))
            .Select(row => $"{Named(row)}: a row with no reason, no date or nothing that ends it.")];
        HashSet<string> admitted = AdmittedNodes(reading, named);
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
        HashSet<string> admitted = AdmittedNodes(reading, Labels(AdmittedByItsCallers));
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

    /// <summary>Rule (A) refuses by default. Every member the rules need it
    /// for is named in <see cref="AdmittedByItsCallers"/>, and every row
    /// holds: its member exists, can be named, has every caller the graph
    /// holds admitted, still lies between an admission and a site, and
    /// stands for as many members as it says.</summary>
    [Fact]
    public void EveryMemberAdmittedByItsCallersIsNamedAndEveryRowHolds()
    {
        IReadOnlyList<string> problems = TableProblems(Shell.Value.Reading, AdmittedByItsCallers);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The shell's calls are all read from bound operations. The
    /// syntax fallback reads a call only where no operation stands for it,
    /// which is source that does not compile. In the shell it reads
    /// none.</summary>
    [Fact]
    public void TheShellsCallsAreAllReadFromOperations()
    {
        List<string> fallback = Shell.Value.Reading.ReadFromSyntax;
        Assert.True(
            fallback.Count == 0,
            $"The syntax fallback read {fallback.Count} call(s), so the operation walk does not cover a form the shell "
            + "uses. Teach ReadOperation the form:\n" + string.Join("\n", fallback.Take(40)));
    }

    /// <summary>The walk as it stood on 2026-11-16, read from bound
    /// operations: 308 sources, 31 calls to a note writer, 6 to an exclusive
    /// creator, 11 reloads of a tab from outside it, 15 admission calls, and
    /// 9,239 operation trees, 17,153 edges, 9,216 members no caller
    /// admits and 20 named as admitted by their callers. The walk from
    /// syntax kinds, which this replaced, found the same sources, calls,
    /// reloads and admission calls. Fewer means the walk missed files or
    /// calls, not that the shell writes less.</summary>
    [Fact]
    public void TheWalkReadsTheShell()
    {
        Reading reading = Shell.Value.Reading;
        (string What, int Found, int AtLeast)[] floors =
        [
            ("sources", Shell.Value.Trees.Count, 300),
            ("calls to a note writer", reading.Sites.Count(site => site.Kind == SiteKind.Session && NoteWriters.Any(writer => writer.Name == site.Name)), 30),
            ("calls to an exclusive creator", reading.Sites.Count(site => site.Kind == SiteKind.Session && ExclusiveCreators.Contains(site.Name)), 5),
            ("reloads of a tab from outside it", reading.Sites.Count(site => site.Kind == SiteKind.Reload), 10),
            ("admission calls", reading.AdmissionCalls.Count, 14),
            ("operation trees walked", reading.TreesWalked, 9000),
            ("edges between members", reading.Edges.Count, 16500),
            ("members no caller admits", reading.Open.Count, 9000),
            ("members named as admitted by their callers", AdmittedByItsCallers.Length, 19),
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
        // A naming mark is a comment on the first line, so the source is
        // read as it is.
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
    [MemberData(nameof(LanguageCalls))]
    [MemberData(nameof(WhatStandsAsTheOperation))]
    public void TheRulesSeeAPlantedRoute(string form, string planted, string expected)
    {
        Reading read = Planted(planted);
        Reading all = Shell.Value.Reading.With(read);
        HashSet<string> shell = Labels(AdmittedByItsCallers);

        // As the route stands: with the members its row names.
        HashSet<string> byTheRow = new(shell.Concat(PlantedNames(planted)), StringComparer.Ordinal);
        string answer = Answer(read, all, byTheRow);
        Assert.True(expected == answer, $"{form}: expected {expected}, the rules answered {answer}");

        if (expected.Contains("unjoined", StringComparison.Ordinal) || expected.Contains("unguarded", StringComparison.Ordinal))
        {
            // The second lines alone: with every planted member named,
            // the closure and the open rule still refuse it.
            HashSet<string> every = new(shell.Concat(read.NodeNames.Values), StringComparer.Ordinal);
            string named = Answer(read, all, every);
            Assert.True(
                expected == named,
                $"{form}: with every planted member named, expected {expected}, the rules answered {named}");
        }
        else if (PlantedNames(planted).Length > 0)
        {
            // The first line alone: with nothing named, the same route
            // is refused.
            string unnamed = Answer(read, all, shell);
            Assert.True(
                unnamed is "unjoined" or "unguarded",
                $"{form}: with no planted member named, expected a refusal, the rules answered {unnamed}");
        }
    }

    private static string Answer(Reading read, Reading all, IReadOnlySet<string> named)
    {
        HashSet<string> writers = Writers();
        var seen = new List<string>();
        if (read.Sites.Any(site => site.Kind == SiteKind.Session && writers.Contains(site.Name)))
        {
            seen.Add(UnadmittedWriters(all, writers, named).Any(violation => violation.File == "Planted.cs") ? "unjoined" : "joined");
        }
        if (read.Sites.Any(site => site.Kind == SiteKind.Reload))
        {
            seen.Add(UnguardedReloads(all, named).Any(violation => violation.File == "Planted.cs") ? "unguarded" : "guarded");
        }
        if (read.ChainImplementers.Count > 0)
        {
            seen.Add("a second chain");
        }
        return seen.Count == 0 ? "clean" : string.Join(", ", seen);
    }

    private const string NamingMark = "// admitted by its callers: ";

    /// <summary>A planted source whose listed members (of Planted.cs) are
    /// named, as a row of <see cref="AdmittedByItsCallers"/> would name
    /// them.</summary>
    private static string Naming(string members, string source) => NamingMark + members + "\n" + source;

    private static string[] PlantedNames(string planted) => planted.StartsWith(NamingMark, StringComparison.Ordinal)
        ? [.. planted[NamingMark.Length..planted.IndexOf('\n', StringComparison.Ordinal)]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(member => $"Planted.cs ({member})")]
        : [];

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
            "a writer in the abandoned argument of AdmitPropertyWrite over several notes",
            InTheWorkspace(
                "void PlantedM() { AdmitPropertyWrite([\"a.md\", \"b.md\"], () => Task.CompletedTask, "
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

        // Unjoined: an expression in or around an admission call that runs
        // before any ticket exists (codex round 1, C2). Only a lambda's body
        // and a method group's target stand in the operation argument.
        {
            "a factory that writes and returns the operation, called as the operation argument",
            InAClass(
                "Func<Task> PlantedMake() { " + AWrite + " return () => Task.CompletedTask; }\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedMake()); }"),
            "unjoined"
        },
        {
            "a cast around a factory call as the operation argument",
            InAClass(
                "Func<Task> PlantedMake() { " + AWrite + " return () => Task.CompletedTask; }\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", (Func<Task>)PlantedMake()); }"),
            "unjoined"
        },
        {
            "a call in the condition of a conditional of two lambdas",
            InAClass(
                "bool PlantedAsk() { " + AWrite + " return true; }\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedAsk() ? () => Task.CompletedTask : () => Task.FromResult(1)); }"),
            "unjoined"
        },
        {
            "a lambda the operation argument invokes at once",
            InAClass(
                "void PlantedM() { _ = chain.Admit(\"a.md\", ((Func<Func<Task>>)(() => { " + AWrite
                + " return () => Task.CompletedTask; }))()); }"),
            "unjoined"
        },
        {
            "a call in the receiver of a method group passed as the operation",
            InAClass(
                "Planted PlantedSelf() { " + AWrite + " return this; }\n"
                + "Task PlantedOperation() => Task.CompletedTask;\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedSelf().PlantedOperation); }"),
            "unjoined"
        },
        {
            "a lambda in an object initialiser whose member is read as the operation",
            PlantedPrelude
            + "internal sealed class PlantedHolder { public Func<Task>? Run { get; set; } }\n"
            + "internal sealed class Planted(VaultSession session, INoteSaveChain chain)\n{\n"
            + "void PlantedM() { _ = chain.Admit(\"a.md\", new PlantedHolder { Run = () => { " + AWrite
            + " return Task.CompletedTask; } }.Run!); }\n}\n",
            "unjoined"
        },
        {
            "a writer lambda in a collection that a call in the operation argument is given",
            InAClass(
                "Func<Task> PlantedPick(Func<Task>[] from) => from[0];\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedPick([() => { " + AWrite + " return Task.CompletedTask; }])); }"),
            "unjoined"
        },
        {
            // Not followed, so it fails closed: a local function's body is
            // its member's.
            "a writer in a local function passed as the operation",
            InAClass(
                "void PlantedM() { Task PlantedLocal() { " + AWrite + " return Task.CompletedTask; } "
                + "_ = chain.Admit(\"a.md\", PlantedLocal); }"),
            "unjoined"
        },
        {
            "a getter that writes and returns the operation",
            InAClass(
                "Func<Task> PlantedOperation { get { " + AWrite + " return () => Task.CompletedTask; } }\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedOperation); }"),
            "unjoined"
        },
        {
            "a writer in the receiver of the admission call",
            InAClass(
                "INoteSaveChain PlantedChain() { " + AWrite + " return chain; }\n"
                + "void PlantedM() { _ = PlantedChain().Admit(\"a.md\", () => Task.CompletedTask); }"),
            "unjoined"
        },
        {
            "a writer in the path argument of the admission call",
            InAClass(
                "string PlantedPath() { " + AWrite + " return \"a.md\"; }\n"
                + "void PlantedM() { _ = chain.Admit(PlantedPath(), () => Task.CompletedTask); }"),
            "unjoined"
        },
        {
            "a writer lambda in a local passed as the operation and called by the member too",
            InAClass(
                "void PlantedM() { Func<Task> operation = () => { " + AWrite + " return Task.CompletedTask; }; "
                + "_ = chain.Admit(\"a.md\", operation); _ = operation(); }"),
            "unjoined"
        },
        {
            "a writer lambda called where it is assigned to a local, the local then passed as the operation",
            InAClass(
                "void PlantedM() { Func<Task> operation; _ = (operation = () => { " + AWrite + " return Task.CompletedTask; })(); "
                + "_ = chain.Admit(\"a.md\", operation); }"),
            "unjoined"
        },
        {
            // Not followed, so it fails closed: the lambda is its maker's.
            "a writer lambda that reaches the admission call as a parameter",
            InAClass(
                "void PlantedRoute(Func<Task> operation) { _ = chain.Admit(\"a.md\", operation); }\n"
                + "void PlantedM() { PlantedRoute(() => { " + AWrite + " return Task.CompletedTask; }); }"),
            "unjoined"
        },

        // Joined.
        {
            "a writer in each lambda of a conditional passed as the operation",
            InAClass(
                "void PlantedM(bool first) { _ = chain.Admit(\"a.md\", first ? () => { " + AWrite
                + " return Task.CompletedTask; } : () => { " + AWrite + " return Task.FromResult(1); }); }"),
            "joined"
        },
        {
            "a writer lambda in a local passed as the operation and read nowhere else",
            InAClass(
                "void PlantedM() { Func<Task> operation = () => { " + AWrite + " return Task.CompletedTask; }; "
                + "_ = chain.Admit(\"a.md\", operation); }"),
            "joined"
        },
        {
            "a writer lambda in a nullable local passed as the operation with a null-forgiving mark",
            InAClass(
                "void PlantedM() { Func<Task>? operation = () => { " + AWrite + " return Task.CompletedTask; }; "
                + "_ = chain.Admit(\"a.md\", operation!); }"),
            "joined"
        },
        {
            "a writer lambda under a cast, passed as the operation",
            InAClass(
                "void PlantedM() { _ = chain.Admit(\"a.md\", (Func<Task>)(() => { " + AWrite + " return Task.CompletedTask; })); }"),
            "joined"
        },
        {
            "a method handed to the chain as its operation through this",
            Naming(
                "PlantedWrite",
                InAClass(
                    "Task PlantedWrite() { " + AWrite + " return Task.CompletedTask; }\n"
                    + "void PlantedM() { _ = chain.Admit(\"a.md\", this.PlantedWrite); }")),
            "joined"
        },
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
            Naming(
                "PlantedWrite",
                InAClass(
                    "void PlantedWrite() { " + AWrite + " }\n"
                    + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => Task.Run(() => PlantedWrite())); }")),
            "joined"
        },
        {
            "two calls deep with every caller admitted",
            Naming(
                "PlantedWrite, PlantedMiddle",
                InAClass(
                    "void PlantedWrite() { " + AWrite + " }\n"
                    + "void PlantedMiddle() { PlantedWrite(); }\n"
                    + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => { PlantedMiddle(); return Task.CompletedTask; }); }")),
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
            // Plan 47 CB-9b: the admission over several notes.
            "a writer in an Admit lambda over several notes",
            InAClass(
                "void PlantedM() { _ = saves.Admit([\"a.md\", \"b.md\"], ticket => { " + AWrite
                + " return Task.CompletedTask; }); }"),
            "joined"
        },
        {
            "through AdmitPropertyWrite over several notes",
            InTheWorkspace(
                "void PlantedM() { AdmitPropertyWrite([\"a.md\", \"b.md\"], "
                + "() => { _ = _session.SaveText(\"a.md\", \"text\", null); return Task.CompletedTask; }, () => { }); }"),
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
            Naming(
                "PlantedWrite",
                InAClass(
                    "Task PlantedWrite() { " + AWrite + " return Task.CompletedTask; }\n"
                    + "void PlantedM() { _ = chain.Admit(\"a.md\", PlantedWrite); }")),
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
        HashSet<string> named = Labels(AdmittedByItsCallers);
        string[] problems =
        [
            .. Problems(all, UnadmittedWriters(all, Writers(), named), [.. OutsideTheChain.Where(row => !IsAReloadRow(row))], named),
            .. Problems(all, UnguardedReloads(all, named), [.. OutsideTheChain.Where(IsAReloadRow), .. NotThisClass], named),
        ];
        Assert.True(
            problems.Any(problem => problem.StartsWith(expected, StringComparison.Ordinal)),
            $"{form}: the rows answered it. Problems found:\n" + string.Join("\n", problems));
    }

    // ---- Rule (A) refuses by default --------------------------------------

    private static readonly string AHelperEveryCallerAdmits = InAClass(
        "void PlantedWrite() { " + AWrite + " }\n"
        + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => { PlantedWrite(); return Task.CompletedTask; }); }");

    /// <summary>The first line. A helper whose every caller is admitted is
    /// refused all the same until it is named, and the refusal says what
    /// to name. Named, it is joined and the table holds.</summary>
    [Fact]
    public void AHelperIsAdmittedByItsCallersOnlyWhenItIsNamed()
    {
        Reading all = Shell.Value.Reading.With(Planted(AHelperEveryCallerAdmits));
        HashSet<string> writers = Writers();

        Violation refused = Assert.Single(
            UnadmittedWriters(all, writers, Labels(AdmittedByItsCallers)), violation => violation.File == "Planted.cs");
        Assert.EndsWith(
            "name Planted.cs (PlantedWrite) in AdmittedByItsCallers if every way to reach it is inside a note's ticket",
            refused.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            TableProblems(all, AdmittedByItsCallers),
            problem => problem.StartsWith("Planted.cs (PlantedWrite): is not named", StringComparison.Ordinal));

        NamedMember[] named = [.. AdmittedByItsCallers, new("Planted.cs", "PlantedWrite", "planted")];
        Assert.DoesNotContain(UnadmittedWriters(all, writers, Labels(named)), violation => violation.File == "Planted.cs");
        Assert.Empty(TableProblems(all, named));
    }

    /// <summary>The second lines, and the table's upkeep. A row that should
    /// not stand is reported, and it admits nothing the closure or the
    /// open rule refuses.</summary>
    [Theory]
    [MemberData(nameof(RowsThatDoNotStand))]
    public void ARowOfAdmittedByItsCallersThatDoesNotStandIsReported(
        string form, string planted, string member, int overloads, string expected, string answer)
    {
        Reading read = Planted(planted);
        Reading all = Shell.Value.Reading.With(read);
        NamedMember[] named = [.. AdmittedByItsCallers, new("Planted.cs", member, "planted", overloads)];
        IReadOnlyList<string> problems = TableProblems(all, named);
        Assert.True(
            problems.Any(problem => problem.StartsWith($"Planted.cs ({member}): {expected}", StringComparison.Ordinal)),
            $"{form}: the table answered it. Problems found:\n" + string.Join("\n", problems));
        string seen = Answer(read, all, Labels(named));
        Assert.True(answer == seen, $"{form}: with the row, expected {answer}, the rules answered {seen}");
    }

    public static TheoryData<string, string, string, int, string, string> RowsThatDoNotStand() => new()
    {
        {
            "a named helper with a caller outside the chain",
            InAClass(
                "void PlantedWrite() { " + AWrite + " }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => { PlantedWrite(); return Task.CompletedTask; }); }\n"
                + "void PlantedBeside() { PlantedWrite(); }"),
            "PlantedWrite",
            1,
            "the row does not hold: Planted.cs (PlantedBeside) calls it outside the chain.",
            "unjoined"
        },
        {
            "a named helper with no caller",
            InAClass("void PlantedWrite() { " + AWrite + " }"),
            "PlantedWrite",
            1,
            "the row does not hold: it has no caller the graph holds.",
            "unjoined"
        },
        {
            "a named Dispose",
            Reached(Scope, "using var scope = new PlantedScope(session);"),
            "Dispose",
            1,
            "cannot be named.",
            "unjoined"
        },
        {
            "a named virtual method",
            PlantedPrelude
            + "internal class Planted(VaultSession session, WorkspaceSaveCoordinator saves)\n{\n"
            + "public virtual void Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Write(); return Task.CompletedTask; }); }\n}\n",
            "Write",
            1,
            "cannot be named.",
            "unjoined"
        },
        {
            "a named method that implements an interface member",
            PlantedPrelude
            + "internal interface IPlantedWriter { void Write(); }\n"
            + "internal sealed class Planted(VaultSession session, WorkspaceSaveCoordinator saves) : IPlantedWriter\n{\n"
            + "public void Write() { " + AWrite + " }\n"
            + "void PlantedAdmit() { _ = saves.Admit(\"a.md\", _ => { Write(); return Task.CompletedTask; }); }\n}\n",
            "Write",
            1,
            "cannot be named.",
            "unjoined"
        },
        {
            "a named member that leads to no site",
            InAClass(
                "void PlantedOther() { }\nvoid PlantedIdle() { PlantedOther(); }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => { PlantedIdle(); return Task.CompletedTask; }); }"),
            "PlantedIdle",
            1,
            "lies between no admission and no call the rules weigh (delete it).",
            "clean"
        },
        {
            "a row for a member that is not there",
            InAClass("void PlantedM() { _ = saves.Admit(\"a.md\", _ => Task.CompletedTask); }"),
            "PlantedGone",
            1,
            "the row names no member the walk reads (delete it).",
            "clean"
        },
        {
            "a row that stands for two overloads and says one",
            InAClass(
                "void PlantedWrite() { " + AWrite + " }\n"
                + "void PlantedWrite(int more) { " + AWrite + " _ = more; }\n"
                + "void PlantedM() { _ = saves.Admit(\"a.md\", _ => { PlantedWrite(); PlantedWrite(1); return Task.CompletedTask; }); }"),
            "PlantedWrite",
            1,
            "the row stands for 2 member(s) of that name between an admission and a site, and says 1.",
            "joined"
        },
    };

    // ---- What stands as the operation, by its bound value -----------------

    /// <summary>A wrapper type whose conversion from a delegate runs the
    /// delegate, before any admission call receives anything.</summary>
    private const string WrapperType =
        "internal sealed class PlantedWrapper(Func<Task> inner)\n{\n"
        + "public Func<Task> Inner => inner;\n"
        + "public static implicit operator PlantedWrapper(Func<Task> inner) { _ = inner(); return new PlantedWrapper(inner); }\n"
        + "public static implicit operator Func<Task>(PlantedWrapper wrapper) => wrapper.Inner;\n}\n";

    private static string BesideAWrapper(string members) => InAClass(members).Replace(PlantedPrelude, PlantedPrelude + WrapperType);

    private const string AWriterLambda = "() => { " + AWrite + " return Task.CompletedTask; }";

    /// <summary>Codex round 4, as a class: what stands as the operation is
    /// read from the argument's bound value, so a conversion that can run
    /// code admits nothing. The reload rows hold rule 3's two tests to the
    /// bound symbol the same way.</summary>
    public static TheoryData<string, string, string> WhatStandsAsTheOperation() => new()
    {
        // Unjoined: something that can run code stands between the lambda
        // and the parameter.
        {
            "a lambda cast through a wrapper whose conversion runs it, and cast back, as the operation",
            BesideAWrapper("void PlantedM() { _ = chain.Admit(\"a.md\", (Func<Task>)(PlantedWrapper)(Func<Task>)(" + AWriterLambda + ")); }"),
            "unjoined"
        },
        {
            "a method cast through a wrapper whose conversion runs it, and cast back, as the operation",
            BesideAWrapper(
                "Task PlantedWrite() { " + AWrite + " return Task.CompletedTask; }\n"
                + "void PlantedM() { _ = chain.Admit(\"a.md\", (Func<Task>)(PlantedWrapper)(Func<Task>)PlantedWrite); }"),
            "unjoined"
        },
        {
            "a lambda in the creation of a wrapper that converts to the operation with no cast written",
            BesideAWrapper("void PlantedM() { _ = chain.Admit(\"a.md\", new PlantedWrapper(" + AWriterLambda + ")); }"),
            "unjoined"
        },
        {
            "a lambda assigned to a local of a wrapper type, the local then passed as the operation",
            BesideAWrapper(
                "void PlantedM() { PlantedWrapper operation = (Func<Task>)(" + AWriterLambda + "); _ = chain.Admit(\"a.md\", operation); }"),
            "unjoined"
        },
        {
            // Not followed, so it fails closed.
            "a lambda in an arm of a switch expression passed as the operation",
            InAClass(
                "void PlantedM(bool first) { _ = chain.Admit(\"a.md\", first switch { true => " + AWriterLambda
                + ", _ => () => Task.CompletedTask }); }"),
            "unjoined"
        },

        // Joined: nothing but a delegate creation, a conversion that runs no
        // code, or parentheses stands between.
        {
            "a writer lambda cast to its own delegate type and parenthesised, as the operation",
            InAClass("void PlantedM() { _ = chain.Admit(\"a.md\", (((Func<Task>)(" + AWriterLambda + ")))); }"),
            "joined"
        },
        {
            "a writer lambda in a delegate creation passed as the operation",
            InAClass("void PlantedM() { _ = chain.Admit(\"a.md\", new Func<Task>(" + AWriterLambda + ")); }"),
            "joined"
        },
        {
            "a method in a delegate creation passed as the operation",
            Naming(
                "PlantedWrite",
                InAClass(
                    "Task PlantedWrite() { " + AWrite + " return Task.CompletedTask; }\n"
                    + "void PlantedM() { _ = chain.Admit(\"a.md\", new Func<Task>(PlantedWrite)); }")),
            "joined"
        },
        {
            "a writer lambda in a local of another delegate type, passed as the operation through a reference conversion",
            InAClass(
                "void PlantedM() { Func<Task<int>> operation = () => { " + AWrite + " return Task.FromResult(1); }; "
                + "_ = chain.Admit(\"a.md\", operation); }"),
            "joined"
        },
        {
            "a writer lambda cast to its delegate type where it is assigned to a local, the local passed as the operation",
            InAClass(
                "void PlantedM() { Func<Task> operation = (Func<Task>)(" + AWriterLambda + "); _ = chain.Admit(\"a.md\", operation); }"),
            "joined"
        },

        // Rule 3's tests, by the bound symbol.
        {
            "a reload in a method with a parameter of another type named SaveTicket",
            InAClass("void PlantedM(WorkspaceTabViewModel tab, SaveTicket other) { _ = other; tab.ReplaceItem(tab.Item); }")
                .Replace(PlantedPrelude, PlantedPrelude + "internal sealed class SaveTicket { }\n"),
            "unguarded"
        },
        {
            "the chain asked in a lambda that does not hold the reload",
            InAClass(
                "void PlantedM(WorkspaceTabViewModel tab) { Func<bool> ask = () => saves.HasAdmittedSaveFor(\"a.md\"); _ = ask; "
                + "tab.ReplaceItem(tab.Item); }"),
            "unguarded"
        },
        {
            "the chain asked and the reload in one lambda",
            InAClass(
                "void PlantedM(WorkspaceTabViewModel tab) { Action both = () => { if (!saves.HasAdmittedSaveFor(\"a.md\")) "
                + "{ tab.ReplaceItem(tab.Item); } }; both(); }"),
            "guarded"
        },
    };

    // ---- The calls the language makes -------------------------------------

    private const string StaticWrite = "_ = Session!.SaveText(\"a.md\", \"text\", null);";

    /// <summary>Helper types with one member that writes, and a class that
    /// reaches the member inside an admission lambda and, when
    /// <paramref name="outside"/> is given, from members no admission
    /// holds.</summary>
    private static string Reached(string helpers, string inside, string outside = "") =>
        PlantedPrelude + helpers
        + "internal sealed class Planted(VaultSession session, INoteSaveChain chain)\n{\n"
        + "void PlantedInside() { _ = chain.Admit(\"a.md\", () => { _ = session; " + inside + " return Task.CompletedTask; }); }\n"
        + outside + "\n}\n";

    private const string Writer =
        "internal sealed class PlantedWriter(VaultSession session) : System.Collections.IEnumerable\n{\n"
        + "public void Add(int item) { " + AWrite + " _ = item; }\n"
        + "public System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();\n}\n";

    private const string ListOfNumbers =
        "internal sealed class PlantedList : System.Collections.Generic.IEnumerable<int>\n{\n"
        + "public static VaultSession? Session;\n"
        + "public void Add(int item) { " + StaticWrite + " _ = item; }\n"
        + "public System.Collections.Generic.IEnumerator<int> GetEnumerator() => throw new NotSupportedException();\n"
        + "System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw new NotSupportedException();\n}\n";

    private static string Holder(string member) =>
        "internal sealed class PlantedHolder(VaultSession session)\n{\n"
        + "public readonly VaultSession Session = session;\n" + member + "\n}\n";

    private const string HolderWithASetter = "public int Value { get => 0; set { " + AWrite + " _ = value; } }";

    private const string HolderWithAGetter = "public int Value { get { " + AWrite + " return 0; } }";

    private const string HolderWithAnIndexer = "public int this[int index] { get { " + AWrite + " return index; } }";

    private const string HolderWithAnEvent = "public event EventHandler Changed { add { " + AWrite + " _ = value; } remove { } }";

    private const string HolderWithAnOperator =
        "public static PlantedHolder operator +(PlantedHolder left, PlantedHolder right) "
        + "{ _ = left.Session.SaveText(\"a.md\", \"text\", null); return right; }";

    private const string HolderWithAConversion =
        "public static implicit operator int(PlantedHolder holder) { _ = holder.Session.SaveText(\"a.md\", \"text\", null); return 0; }";

    private const string Scope =
        "internal ref struct PlantedScope(VaultSession session)\n{\npublic void Dispose() { " + AWrite + " }\n}\n";

    private const string AsyncScope =
        "internal sealed class PlantedAsyncScope(VaultSession session)\n{\n"
        + "public ValueTask DisposeAsync() { " + AWrite + " return default; }\n}\n";

    private static string Bag(string writer) =>
        "internal sealed class PlantedBag(VaultSession session)\n{\n"
        + "public PlantedBag GetEnumerator() { " + (writer == "GetEnumerator" ? AWrite : "_ = session;") + " return this; }\n"
        + "public bool MoveNext() { " + (writer == "MoveNext" ? AWrite : "_ = session;") + " return false; }\n"
        + "public int Current => 0;\n}\n";

    private static string Awaitable(string writer) =>
        "internal sealed class PlantedAwaitable(VaultSession session)\n{\n"
        + "public PlantedAwaiter GetAwaiter() { " + (writer == "GetAwaiter" ? AWrite : string.Empty) + " return new PlantedAwaiter(session); }\n}\n"
        + "internal sealed class PlantedAwaiter(VaultSession session) : System.Runtime.CompilerServices.INotifyCompletion\n{\n"
        + "public bool IsCompleted => true;\n"
        + "public void GetResult() { " + (writer == "GetResult" ? AWrite : "_ = session;") + " }\n"
        + "public void OnCompleted(Action continuation) { }\n}\n";

    private const string Pair =
        "internal sealed class PlantedPair(VaultSession session)\n{\n"
        + "public void Deconstruct(out int left, out int right) { " + AWrite + " left = 0; right = 0; }\n}\n";

    private const string BaseAndDerived =
        "internal class PlantedBase\n{\npublic static VaultSession? Session;\npublic PlantedBase() { " + StaticWrite + " }\n}\n"
        + "internal sealed class PlantedDerived : PlantedBase { }\n";

    private const string Maker =
        "internal sealed class PlantedMaker\n{\n"
        + "public PlantedMaker(VaultSession session) { " + AWrite + " }\n"
        + "public PlantedMaker(VaultSession session, int more) : this(session) { _ = more; }\n}\n";

    private const string LoneMaker =
        "internal sealed class PlantedLoneMaker\n{\npublic PlantedLoneMaker(VaultSession session) { " + AWrite + " }\n}\n";

    private const string Source =
        "internal sealed class PlantedSource(VaultSession session)\n{\n"
        + "public PlantedSource Select(Func<int, int> selector) { " + AWrite + " _ = selector; return this; }\n}\n";

    private const string Handler =
        "[System.Runtime.CompilerServices.InterpolatedStringHandler]\n"
        + "internal ref struct PlantedHandler(int literalLength, int formattedCount)\n{\n"
        + "public static VaultSession? Session;\n"
        + "public void AppendLiteral(string text) { " + StaticWrite + " _ = text.Length + literalLength + formattedCount; }\n"
        + "public void AppendFormatted<T>(T value) { _ = value; }\n}\n"
        + "internal static class PlantedLog { public static void Write(PlantedHandler handler) { _ = handler; } }\n";

    private const string Sliced =
        "internal sealed class PlantedSpan(VaultSession session)\n{\n"
        + "public int Length => 0;\n"
        + "public PlantedSpan Slice(int start, int length) { " + AWrite + " _ = start + length; return this; }\n}\n";

    private const string Printed =
        "internal sealed class PlantedText(VaultSession session)\n{\n"
        + "public override string ToString() { " + AWrite + " return string.Empty; }\n}\n";

    private const string RuntimeStart =
        "internal static class PlantedModule\n{\npublic static VaultSession? Session;\n"
        + "[System.Runtime.CompilerServices.ModuleInitializer]\n"
        + "internal static void Start() { _ = Session?.SaveText(\"a.md\", \"text\", null); }\n}\n";

    // Each `outside` below is a member of the planted class that no
    // admission holds, in which the language makes the call.
    private const string InitialiserOutside = "void PlantedOutside() { _ = new PlantedWriter(session) { 1 }; }";

    private const string SetOutside = "void PlantedOutside() { new PlantedHolder(session).Value = 2; }";

    private const string GetOutside = "void PlantedOutside() { _ = new PlantedHolder(session).Value; }";

    private const string IndexOutside = "void PlantedOutside() { _ = new PlantedHolder(session)[0]; }";

    private const string SubscribeOutside =
        "void PlantedHandle(object? sender, EventArgs e) { }\n"
        + "void PlantedOutside() { new PlantedHolder(session).Changed += PlantedHandle; }";

    private const string AddOutside = "void PlantedOutside() { PlantedHolder one = new(session); _ = one + one; }";

    private const string ConvertOutside = "void PlantedOutside() { int number = new PlantedHolder(session); _ = number; }";

    private const string ForeachOutside = "void PlantedOutside() { foreach (int item in new PlantedBag(session)) { _ = item; } }";

    private const string AwaitOutside = "async Task PlantedOutside() { await new PlantedAwaitable(session); }";

    private const string DeconstructOutside = "void PlantedOutside() { var (left, right) = new PlantedPair(session); _ = left + right; }";

    private const string QueryOutside = "void PlantedOutside() { _ = from x in new PlantedSource(session) select x + 1; }";

    private const string InterpolateOutside = "void PlantedOutside() { PlantedLog.Write($\"a{1}\"); }";

    private const string RangeOutside = "void PlantedOutside() { _ = new PlantedSpan(session)[..]; }";

    /// <summary>Codex round 2, as a class: a call the language makes with
    /// no invocation expression. In each unjoined row the member that
    /// writes is called once inside an admission lambda and once more, by
    /// the language, from a member no admission holds.</summary>
    public static TheoryData<string, string, string> LanguageCalls() => new()
    {
        {
            "Add, called by a collection initialiser outside the chain",
            Reached(Writer, "new PlantedWriter(session).Add(1);", InitialiserOutside),
            "unjoined"
        },
        {
            "Add, called by a collection expression outside the chain",
            Reached(ListOfNumbers, "new PlantedList().Add(1);", "void PlantedOutside() { PlantedList list = [1]; _ = list; }"),
            "unjoined"
        },
        {
            "a setter that writes, set outside the chain",
            Reached(Holder(HolderWithASetter), "new PlantedHolder(session).Value = 1;", SetOutside),
            "unjoined"
        },
        {
            "a getter that writes, read outside the chain",
            Reached(Holder(HolderWithAGetter), "_ = new PlantedHolder(session).Value;", GetOutside),
            "unjoined"
        },
        {
            "an indexer that writes, read outside the chain",
            Reached(Holder(HolderWithAnIndexer), "_ = new PlantedHolder(session)[0];", IndexOutside),
            "unjoined"
        },
        {
            "an event's add accessor that writes, subscribed outside the chain",
            Reached(Holder(HolderWithAnEvent), "new PlantedHolder(session).Changed += PlantedHandle;", SubscribeOutside),
            "unjoined"
        },
        {
            "a user-defined operator that writes, used outside the chain",
            Reached(Holder(HolderWithAnOperator), "PlantedHolder one = new(session); _ = one + one;", AddOutside),
            "unjoined"
        },
        {
            "an implicit conversion that writes, used outside the chain",
            Reached(Holder(HolderWithAConversion), "int number = new PlantedHolder(session); _ = number;", ConvertOutside),
            "unjoined"
        },
        {
            "Dispose, called by a using declaration outside the chain",
            Reached(
                Scope,
                "var scope = new PlantedScope(session); scope.Dispose();",
                "void PlantedOutside() { using var scope = new PlantedScope(session); }"),
            "unjoined"
        },
        {
            "DisposeAsync, called by an await using outside the chain",
            Reached(
                AsyncScope,
                "_ = new PlantedAsyncScope(session).DisposeAsync();",
                "async Task PlantedOutside() { await using var scope = new PlantedAsyncScope(session); }"),
            "unjoined"
        },
        {
            "GetEnumerator, called by a foreach outside the chain",
            Reached(Bag("GetEnumerator"), "_ = new PlantedBag(session).GetEnumerator();", ForeachOutside),
            "unjoined"
        },
        {
            "MoveNext, called by a foreach outside the chain",
            Reached(Bag("MoveNext"), "_ = new PlantedBag(session).MoveNext();", ForeachOutside),
            "unjoined"
        },
        {
            "GetAwaiter, called by an await outside the chain",
            Reached(Awaitable("GetAwaiter"), "_ = new PlantedAwaitable(session).GetAwaiter();", AwaitOutside),
            "unjoined"
        },
        {
            "GetResult, called by an await outside the chain",
            Reached(Awaitable("GetResult"), "new PlantedAwaiter(session).GetResult();", AwaitOutside),
            "unjoined"
        },
        {
            "Deconstruct, called by a deconstruction outside the chain",
            Reached(Pair, "new PlantedPair(session).Deconstruct(out _, out _);", DeconstructOutside),
            "unjoined"
        },
        {
            "a base constructor that writes, reached by a derived type's implicit constructor outside the chain",
            Reached(BaseAndDerived, "_ = new PlantedBase();", "void PlantedOutside() { _ = new PlantedDerived(); }"),
            "unjoined"
        },
        {
            "a constructor that writes, reached through this() outside the chain",
            Reached(Maker, "_ = new PlantedMaker(session);", "void PlantedOutside() { _ = new PlantedMaker(session, 1); }"),
            "unjoined"
        },
        {
            "a constructor that writes, reached from a field initialiser",
            Reached(Maker, "_ = new PlantedMaker(session);", "private readonly PlantedMaker _made = new(session);"),
            "unjoined"
        },
        {
            "Select, called by a query outside the chain",
            Reached(Source, "_ = new PlantedSource(session).Select(x => x);", QueryOutside),
            "unjoined"
        },
        {
            "an interpolated string handler's AppendLiteral, called by an interpolated string outside the chain",
            Reached(Handler, "var handler = new PlantedHandler(0, 0); handler.AppendLiteral(\"x\");", InterpolateOutside),
            "unjoined"
        },
        {
            "Slice, called by a range outside the chain",
            Reached(Sliced, "_ = new PlantedSpan(session).Slice(0, 0);", RangeOutside),
            "unjoined"
        },
        {
            "a ToString override, called by an interpolated string outside the chain",
            Reached(Printed, "_ = new PlantedText(session).ToString();", "void PlantedOutside() { _ = $\"{new PlantedText(session)}\"; }"),
            "unjoined"
        },
        {
            "a module initialiser, which the runtime calls",
            Reached(RuntimeStart, "PlantedModule.Start();"),
            "unjoined"
        },

        // Codex round 6: a pattern member is open by its name, whatever its
        // parameters. The language calls each of these with none written.
        {
            "Dispose with an optional parameter, called by a using declaration outside the chain",
            Reached(
                "internal ref struct PlantedScope(VaultSession session)\n{\n"
                + "public void Dispose(int optional = 0) { " + AWrite + " _ = optional; }\n}\n",
                "var scope = new PlantedScope(session); scope.Dispose();",
                "void PlantedOutside() { using var scope = new PlantedScope(session); }"),
            "unjoined"
        },
        {
            "DisposeAsync with an optional parameter, called by an await using outside the chain",
            Reached(
                "internal sealed class PlantedAsyncScope(VaultSession session)\n{\n"
                + "public ValueTask DisposeAsync(int optional = 0) { " + AWrite + " _ = optional; return default; }\n}\n",
                "_ = new PlantedAsyncScope(session).DisposeAsync();",
                "async Task PlantedOutside() { await using var scope = new PlantedAsyncScope(session); }"),
            "unjoined"
        },
        {
            "an extension GetEnumerator with an optional parameter, called by a foreach outside the chain",
            Reached(
                "internal sealed class PlantedBag(VaultSession session)\n{\n"
                + "public VaultSession Session => session;\npublic bool MoveNext() => false;\npublic int Current => 0;\n}\n"
                + "internal static class PlantedBagExtensions\n{\n"
                + "public static PlantedBag GetEnumerator(this PlantedBag bag, int optional = 0) "
                + "{ _ = bag.Session.SaveText(\"a.md\", \"text\", null); _ = optional; return bag; }\n}\n",
                "_ = new PlantedBag(session).GetEnumerator();",
                ForeachOutside),
            "unjoined"
        },
        {
            // No deconstruction binds to it (CS8129), so the name is all
            // that holds it open.
            "Deconstruct with one more optional parameter, called inside an admission lambda only",
            Reached(
                "internal sealed class PlantedPair(VaultSession session)\n{\n"
                + "public void Deconstruct(out int left, out int right, int extra = 0) { " + AWrite + " left = extra; right = 0; }\n}\n",
                "new PlantedPair(session).Deconstruct(out _, out _);"),
            "unjoined"
        },
        {
            "a params Add, called by a collection initialiser outside the chain",
            Reached(
                "internal sealed class PlantedWriter(VaultSession session) : System.Collections.IEnumerable\n{\n"
                + "public void Add(params int[] items) { " + AWrite + " _ = items; }\n"
                + "public System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();\n}\n",
                "new PlantedWriter(session).Add(1);",
                InitialiserOutside),
            "unjoined"
        },

        // Joined: the language's call stands inside the admission lambda
        // and nowhere else, and the graph holds its edge.
        {
            "Select, called by a query inside an admission lambda only",
            Naming("Select", Reached(Source, "_ = from x in new PlantedSource(session) select x + 1;")),
            "joined"
        },
        {
            "an interpolated string handler's AppendLiteral, called by an interpolated string inside an admission lambda only",
            Naming("AppendLiteral", Reached(Handler, "PlantedLog.Write($\"a{1}\");")),
            "joined"
        },

        // Unjoined all the same, though the only call stands inside an
        // admission lambda: the open rule's cost. No operation need name
        // these members, so their callers are never taken for all of them.
        {
            "Add, called by a collection initialiser inside an admission lambda only",
            Reached(Writer, "_ = new PlantedWriter(session) { 1 };"),
            "unjoined"
        },
        {
            "Dispose, called by a using declaration inside an admission lambda only",
            Reached(Scope, "using var scope = new PlantedScope(session);"),
            "unjoined"
        },
        {
            "GetEnumerator, called by a foreach inside an admission lambda only",
            Reached(Bag("GetEnumerator"), "foreach (int item in new PlantedBag(session)) { _ = item; }"),
            "unjoined"
        },
        {
            "Deconstruct, called by a deconstruction inside an admission lambda only",
            Reached(Pair, "var (left, right) = new PlantedPair(session); _ = left + right;"),
            "unjoined"
        },
        {
            "a constructor that writes, called inside an admission lambda only",
            Reached(LoneMaker, "_ = new PlantedLoneMaker(session);"),
            "unjoined"
        },
        {
            "a setter that writes, set inside an admission lambda only",
            Reached(Holder(HolderWithASetter), "new PlantedHolder(session).Value = 1;"),
            "unjoined"
        },
    };

    /// <summary>Layer one alone: the graph holds an edge from the member
    /// that holds a call the language makes to each member that call
    /// reaches, whatever the open rule then says of that member.</summary>
    [Theory]
    [InlineData("a collection initialiser", Writer, InitialiserOutside, "PlantedWriter.Add(")]
    [InlineData("a query", Source, QueryOutside, "PlantedSource.Select(")]
    [InlineData("an interpolated string", Handler, InterpolateOutside, "PlantedHandler.AppendLiteral(|PlantedHandler.#ctor(")]
    [InlineData("a range", Sliced, RangeOutside, "PlantedSpan.Slice(|PlantedSpan.get_Length")]
    [InlineData("a deconstruction", Pair, DeconstructOutside, "PlantedPair.Deconstruct(")]
    public void TheGraphHoldsAnEdgeForACallTheLanguageMakes(string form, string helpers, string outside, string callees) =>
        AssertEdges(form, helpers, outside, callees);

    [Theory]
    [InlineData("a property set", HolderWithASetter, SetOutside, "PlantedHolder.set_Value(")]
    [InlineData("a property read", HolderWithAGetter, GetOutside, "PlantedHolder.get_Value")]
    [InlineData("an indexer read", HolderWithAnIndexer, IndexOutside, "PlantedHolder.get_Item(")]
    [InlineData("an event subscription", HolderWithAnEvent, SubscribeOutside, "PlantedHolder.add_Changed(")]
    [InlineData("a user-defined operator", HolderWithAnOperator, AddOutside, "PlantedHolder.op_Addition(")]
    [InlineData("an implicit conversion", HolderWithAConversion, ConvertOutside, "PlantedHolder.op_Implicit(")]
    public void TheGraphHoldsAnEdgeForAnAccessorAndAnOperator(string form, string member, string outside, string callees) =>
        AssertEdges(form, Holder(member), outside, callees);

    [Theory]
    [InlineData("a foreach", "GetEnumerator", "PlantedBag.GetEnumerator|PlantedBag.MoveNext|PlantedBag.get_Current")]
    [InlineData("an await", "GetAwaiter", "PlantedAwaitable.GetAwaiter|PlantedAwaiter.GetResult|PlantedAwaiter.get_IsCompleted")]
    public void TheGraphHoldsAnEdgeForEachMemberOfAPattern(string form, string writer, string callees) =>
        AssertEdges(
            form,
            form == "a foreach" ? Bag(writer) : Awaitable(writer),
            form == "a foreach" ? ForeachOutside : AwaitOutside,
            callees);

    private static void AssertEdges(string form, string helpers, string outside, string callees)
    {
        Reading read = Planted(Reached(helpers, string.Empty, outside));
        string[] missing = [.. callees.Split('|').Where(callee => !read.Edges.Any(edge =>
            edge.Caller.Contains(".PlantedOutside", StringComparison.Ordinal) && edge.Callee.Contains(callee, StringComparison.Ordinal)))];
        Assert.True(
            missing.Length == 0,
            $"{form}: the graph holds no edge from PlantedOutside to [{string.Join(", ", missing)}]. Its edges from there:\n"
            + string.Join("\n", read.Edges.Where(edge => edge.Caller.Contains(".PlantedOutside", StringComparison.Ordinal)).Select(edge => edge.Callee).Distinct()));
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
            UnadmittedWriters(all, Writers(), Labels(AdmittedByItsCallers))
                .Where(violation => violation.File == "Planted.cs").Select(violation => violation.ToString()));
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
