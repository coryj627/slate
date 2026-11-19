// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// #1890 (plan 47 CB-15a, CD9): a failure sentence anywhere in the shell
// says core's words. A uniffi exception's Message is the binding's field
// rendering ("@message=...", "@path=..., @reason=..."), not the error's text,
// so the shell reads an exception's Message in one place only:
// FailureDetail.Of(Exception), which is core's VaultErrorDetail for a vault
// error and the exception's own message otherwise. The helper is identified
// by its symbol, never by a name.
//
// Rule 1 of BasesErrorDetailCensus (MessageReads, one implementation for
// both) runs here over every shell source: no read of Message on a type
// deriving from System.Exception (member access, conditional access,
// property or extended pattern) outside FailureDetail.Of. The census BINDS
// the shell (ShellCompilation) and fails closed on binding.
//
// The reads that stand today are dated rows (plan 47 §4 rule 4), each owned
// by the chunk that ends it: CB-15b for Canvas/, Graph/ and Search/, CB-15c
// for the rest. A row names its file, its type by its full name (namespace,
// nesting and arity), its member as SaveChainAdmissionCensus names one, and
// how many reads the member holds. A read with no row, or a member holding
// more reads than its row, fails and prints a ready row; a row holding more
// than the member reads, or matching no read, fails as stale, so each later
// PR deletes or lowers the rows it ends. No row may name a Files sidebar
// file or the tag edit's EditTagAtItsTurn, which CB-15a routed, or a file in
// Canvas/, Graph/ or Search/, which CB-15b routed.
//
// The table only shrinks. A baseline, kept apart from it, holds each
// member's allowance as it stood on 2026-11-18: a row names a member the
// baseline names, with no more reads than its allowance, so an allowance
// never moves to another member or from one row to another. The table's
// row and read totals must equal its ceilings, which a PR lowers as it
// routes reads, so a row once deleted cannot come back.
//
// The trial (step 0, main 26d353ef, 2026-11-18): rule 1 found 96 reads in 32
// files. CB-15a ended 23 (22 in the sidebar's files, one in
// WorkspaceViewModel.Properties.cs); 73 stand as rows, 26 for CB-15b and 47
// for CB-15c. Rule 2 (an exception value reaches text only through the
// helper) stays on the Bases sources: over the shell it had 76 hits of its own
// in 29 files, none a dump reaching text by reading (typed-field reads of a
// uniffi error, stored exceptions, captures, wrappers, delegates), which is
// not few (CD9's condition; the lead's ruling of 2026-11-18).
//
// CB-15b (CD9, 2026-11-19) routed the 26 reads in Canvas/, Graph/ and Search/
// (22 rows) and removed the search's and the graph's own error arms; 39 rows
// and 47 reads stand, all CB-15c's. Its trial of rule 2 over those three
// folders had 20 hits of its own, none an exception reaching text. Five of its
// reads are held here alone. Four take only an ordinary exception, whose
// message FailureDetail.Of returns unchanged, so no fact can tell the helper
// from a direct read: GraphConfigStore.Read, the two non-vault catches of
// ConnectionsLeafViewModel.FetchUnder, and the one of
// GraphDocumentViewModel.FetchUnder. The fifth has
// no fact: CanvasDocumentViewModel.CanvasRemoveFromGroup's catch wraps only
// CanvasPlaceNew, which refuses nothing but a closed handle, after
// CanvasParentOf has asked the same handle in the same lease hold, so only a
// close that bypasses the lease's lock in the middle of that hold reaches it:
// a race the lease forbids in production (the lead's ruling on CB-15b's
// Call 8).
//
// What this census cannot see:
// - A count is the key: one read routed and another added in the same member
//   keeps the count, and passes.
// - A row a PR lowered may rise again to its allowance while another row
//   falls by as much, with the ceilings held; the raised count shows in the
//   table's diff.
// - Code under an inactive #if: the shell is parsed with no preprocessor
//   symbols (CSharpSource).
// - A XAML binding path ({Binding Exception.Message}): XAML is not bound here.
// - ToString, interpolation, concatenation and string.Format of an
//   exception: rule 2's, which runs on the Bases sources only.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "shell-failure-detail")]
public sealed class ShellFailureDetailCensus
{
    /// <summary>A member's reads of an exception's Message that stand until
    /// their owner routes them through FailureDetail.Of: the file (relative
    /// to the shell's source root), the type that declares the member, the
    /// member, how many reads it holds over every overload of that name in
    /// that type, the chunk that ends it, when it was written, and the
    /// sentence the reads reach.</summary>
    internal sealed record Row(
        string File, string Type, string Member, int Reads, string Owner, string Dated, string Reaches);

    /// <summary>One read, keyed as a row keys it.</summary>
    internal sealed record Read(string File, string Type, string Member, int Line, string Context)
    {
        public override string ToString() =>
            $"{File} line {Line} ({Type}.{Member}): `{Context}` reads an exception's Message; use FailureDetail.Of.";
    }

    private const string CB15b = "CB-15b";

    private const string CB15c = "CB-15c";

    private const string Dated = "2026-11-18";

    /// <summary>The reads that stand (plan 47 §4 rule 4), from the census's
    /// own ready rows on 2026-11-18: 61 members, 73 reads then (CB-15b 22
    /// members and 26 reads, CB-15c 39 members and 47 reads). CB-15b routed
    /// its rows; 39 members and 47 reads stand, all CB-15c's. No row names a
    /// Files sidebar file, the tag edit, or a file in Canvas/, Graph/ or
    /// Search/.</summary>
    internal static readonly Row[] Remaining =
    [
        // CB-15c: the rest of the shell.
        new("Commands/SlateCommandRegistrar.cs", "SlateWindows.Commands.DispatcherCommandAction", "Invoke", 1, CB15c, Dated, "CommandException.ActionFailed, a palette command's failure"),
        new("EditorInteractions.cs", "SlateWindows.EditorInteractionCoordinator", "ResolveEmbedPreview", 1, CB15c, Dated, "EmbedUnresolvedReason.ReadError, an embed preview's read failure"),
        new("FindInNoteViewModel.cs", "SlateWindows.FindInNoteViewModel", "CreateStrategy", 1, CB15c, Dated, "Find in note's pattern error"),
        new("Panels/BibliographyViewModel.cs", "SlateWindows.Panels.BibliographyViewModel", "LoadEntries", 1, CB15c, Dated, "the bibliography panel's load failure"),
        new("Panels/BibliographyViewModel.cs", "SlateWindows.Panels.BibliographyViewModel", "LoadUnresolved", 1, CB15c, Dated, "the bibliography panel's unresolved citations failure"),
        new("Panels/BulkRenameViewModel.cs", "SlateWindows.Panels.BulkRenameViewModel", "RunPreview", 1, CB15c, Dated, "the bulk rename sheet's preview error"),
        new("Panels/BulkRenameViewModel.cs", "SlateWindows.Panels.BulkRenameViewModel", "StartApplyWork", 1, CB15c, Dated, "the bulk rename sheet's apply error"),
        new("Panels/CitationsPanelViewModel.cs", "SlateWindows.Panels.CitationsPanelViewModel", "Refresh", 1, CB15c, Dated, "the citations panel's load failure"),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "LoadDeletedFiles", 1, CB15c, Dated, "the history panel's deleted files failure"),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "LoadFirstPageBody", 1, CB15c, Dated, "the history panel's versions failure"),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "LoadOlder", 1, CB15c, Dated, "the history panel's older versions failure"),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "RunDiff", 1, CB15c, Dated, "the history panel's diff failure"),
        new("Panels/NotePropertiesViewModel.cs", "SlateWindows.Panels.NotePropertiesViewModel", "RefreshProperties", 1, CB15c, Dated, "the properties panel's load failure"),
        new("Panels/RightPanePanelsViewModel.cs", "SlateWindows.Panels.RightPanePanelsViewModel", "LoadLinks", 1, CB15c, Dated, "the links panel's load failure"),
        new("Panels/RightPanePanelsViewModel.cs", "SlateWindows.Panels.RightPanePanelsViewModel", "LoadOutline", 1, CB15c, Dated, "the outline panel's load failure"),
        new("Panels/RightPanePanelsViewModel.cs", "SlateWindows.Panels.RightPanePanelsViewModel", "LoadTasks", 1, CB15c, Dated, "the tasks panel's load failure"),
        new("Panels/SyncDiagnosticsViewModel.cs", "SlateWindows.Panels.SyncDiagnosticsViewModel", "LoadBody", 1, CB15c, Dated, "the sync diagnostics panel's load failure"),
        new("Panels/TaskIndexRepairCoordinator.cs", "SlateWindows.Panels.TaskIndexRepairCoordinator", "Retry", 1, CB15c, Dated, "the task index repair's last error"),
        new("Panels/TaskIndexRepairCoordinator.cs", "SlateWindows.Panels.TaskIndexRepairCoordinator", "TryRepairNow", 1, CB15c, Dated, "the task index repair's error"),
        new("Panels/TasksReviewViewModel.cs", "SlateWindows.Panels.TasksReviewViewModel", "DirectToggleAtItsTurn", 2, CB15c, Dated, "TaskToggleFailed, the task review's toggle"),
        new("Panels/TasksReviewViewModel.cs", "SlateWindows.Panels.TasksReviewViewModel", "LoadFirstPage", 1, CB15c, Dated, "the task review's load failure"),
        new("Panels/TasksReviewViewModel.cs", "SlateWindows.Panels.TasksReviewViewModel", "LoadMore", 1, CB15c, Dated, "the task review's next page failure"),
        new("Reading/MathMlUiaProperty.cs", "SlateWindows.Reading.MathMlUiaProperty", "Initialize", 1, CB15c, Dated, "the MathML property's inactive reason, logged"),
        new("Reading/ReadingContentViewModel.cs", "SlateWindows.Reading.ReadingContentViewModel", "ProjectBaseEmbed", 1, CB15c, Dated, "an embedded base's execute error in reading view"),
        new("VaultLifecycleViewModel.cs", "SlateWindows.VaultLifecycleViewModel", "OpenVaultAsync", 2, CB15c, Dated, "Could not open vault: and Unexpected vault error:"),
        new("VaultLifecycleViewModel.cs", "SlateWindows.VaultLifecycleViewModel", "RecordRecentVault", 1, CB15c, Dated, "Could not save recent vaults:"),
        new("VaultLifecycleViewModel.cs", "SlateWindows.VaultLifecycleViewModel", "RemoveRecentVault", 1, CB15c, Dated, "Could not update recent vaults:"),
        new("WorkspaceTabViewModel.Properties.cs", "SlateWindows.WorkspaceTabViewModel", "WriteProperty", 1, CB15c, Dated, "PropertyEditFailed, a property write's other fault"),
        new("WorkspaceViewModel.Citations.cs", "SlateWindows.WorkspaceViewModel", "ReadAndSeedSources", 1, CB15c, Dated, "a bibliography source's notice"),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "CommitRestoreAsDeleted", 2, CB15c, Dated, "the history panel's Restore As failure, a deleted file"),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "CommitRestoreAsVersion", 2, CB15c, Dated, "the history panel's Restore As failure, a version"),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "RecoverAdmitted", 3, CB15c, Dated, "the history panel's recover failure"),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "RestoreAdmitted", 2, CB15c, Dated, "the history panel's restore failure"),
        new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "AddWriteCompletion", 1, CB15c, Dated, "PropertyEditFailed, adding a property"),
        new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "BulkRenameApplyAtItsTurn", 1, CB15c, Dated, "the bulk rename sheet's apply error"),
        new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "PropertyWriteCompletion", 1, CB15c, Dated, "PropertyEditFailed, a property write"),
        new("WorkspaceViewModel.Templates.cs", "SlateWindows.WorkspaceViewModel", "CreateFromTemplate", 1, CB15c, Dated, "the template prompt's create refusal, shown inline"),
        new("WorkspaceViewModel.cs", "SlateWindows.WorkspaceTabViewModel", "PerformTaskToggle", 2, CB15c, Dated, "TaskToggleFailed, a toggle's other fault"),
        new("WorkspaceViewModel.cs", "SlateWindows.WorkspaceTabViewModel", "StartAdmittedTaskToggle", 1, CB15c, Dated, "TaskToggleFailed, a faulted toggle"),
    ];

    /// <summary>The allowances as they stood on 2026-11-18, kept apart from
    /// the table: 61 members and 73 reads, the census's own ready rows that
    /// day. A row stands only for a member here, with no more reads than its
    /// allowance, so an allowance never moves to another member or from one
    /// row to another. Nothing is ever added here; a PR that routes reads
    /// lowers or deletes rows in the table, not entries here.</summary>
    internal static readonly Allowance[] Baseline =
    [
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasAddFileCard", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasAddLinkCard", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasConvertToNote", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasCreateConnectedCard", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasDuplicate", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasMoveIntoGroup", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasNewCard", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasNewGroup", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "CanvasRemoveFromGroup", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "RequestVaultPick", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "SubmitBulkMarked", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "SubmitGroupMarked", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel", "TryWhereAmI", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasPhrase", "OpenFailed", 1),
        new("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasPhrase", "RetargetAbsent", 1),
        new("Canvas/CanvasMutationFunnel.cs", "SlateWindows.Canvas.CanvasMutationFunnel", "Transact", 1),
        new("Graph/ConnectionsLeafViewModel.cs", "SlateWindows.Graph.ConnectionsLeafViewModel", "FetchUnder", 3),
        new("Graph/GraphConfigStore.cs", "SlateWindows.Graph.GraphConfigStore", "Read", 1),
        new("Graph/GraphConfigStore.cs", "SlateWindows.Graph.GraphConfigStore", "Reason", 2),
        new("Graph/GraphDocumentViewModel.cs", "SlateWindows.Graph.GraphDocumentViewModel", "FetchUnder", 2),
        new("Graph/GraphDocumentViewModel.cs", "SlateWindows.Graph.GraphDocumentViewModel", "HumanReadable", 1),
        new("Search/SearchOverlayViewModel.cs", "SlateWindows.Search.SearchOverlayViewModel", "HumanReadable", 1),
        new("Commands/SlateCommandRegistrar.cs", "SlateWindows.Commands.DispatcherCommandAction", "Invoke", 1),
        new("EditorInteractions.cs", "SlateWindows.EditorInteractionCoordinator", "ResolveEmbedPreview", 1),
        new("FindInNoteViewModel.cs", "SlateWindows.FindInNoteViewModel", "CreateStrategy", 1),
        new("Panels/BibliographyViewModel.cs", "SlateWindows.Panels.BibliographyViewModel", "LoadEntries", 1),
        new("Panels/BibliographyViewModel.cs", "SlateWindows.Panels.BibliographyViewModel", "LoadUnresolved", 1),
        new("Panels/BulkRenameViewModel.cs", "SlateWindows.Panels.BulkRenameViewModel", "RunPreview", 1),
        new("Panels/BulkRenameViewModel.cs", "SlateWindows.Panels.BulkRenameViewModel", "StartApplyWork", 1),
        new("Panels/CitationsPanelViewModel.cs", "SlateWindows.Panels.CitationsPanelViewModel", "Refresh", 1),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "LoadDeletedFiles", 1),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "LoadFirstPageBody", 1),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "LoadOlder", 1),
        new("Panels/HistoryViewModel.cs", "SlateWindows.Panels.HistoryViewModel", "RunDiff", 1),
        new("Panels/NotePropertiesViewModel.cs", "SlateWindows.Panels.NotePropertiesViewModel", "RefreshProperties", 1),
        new("Panels/RightPanePanelsViewModel.cs", "SlateWindows.Panels.RightPanePanelsViewModel", "LoadLinks", 1),
        new("Panels/RightPanePanelsViewModel.cs", "SlateWindows.Panels.RightPanePanelsViewModel", "LoadOutline", 1),
        new("Panels/RightPanePanelsViewModel.cs", "SlateWindows.Panels.RightPanePanelsViewModel", "LoadTasks", 1),
        new("Panels/SyncDiagnosticsViewModel.cs", "SlateWindows.Panels.SyncDiagnosticsViewModel", "LoadBody", 1),
        new("Panels/TaskIndexRepairCoordinator.cs", "SlateWindows.Panels.TaskIndexRepairCoordinator", "Retry", 1),
        new("Panels/TaskIndexRepairCoordinator.cs", "SlateWindows.Panels.TaskIndexRepairCoordinator", "TryRepairNow", 1),
        new("Panels/TasksReviewViewModel.cs", "SlateWindows.Panels.TasksReviewViewModel", "DirectToggleAtItsTurn", 2),
        new("Panels/TasksReviewViewModel.cs", "SlateWindows.Panels.TasksReviewViewModel", "LoadFirstPage", 1),
        new("Panels/TasksReviewViewModel.cs", "SlateWindows.Panels.TasksReviewViewModel", "LoadMore", 1),
        new("Reading/MathMlUiaProperty.cs", "SlateWindows.Reading.MathMlUiaProperty", "Initialize", 1),
        new("Reading/ReadingContentViewModel.cs", "SlateWindows.Reading.ReadingContentViewModel", "ProjectBaseEmbed", 1),
        new("VaultLifecycleViewModel.cs", "SlateWindows.VaultLifecycleViewModel", "OpenVaultAsync", 2),
        new("VaultLifecycleViewModel.cs", "SlateWindows.VaultLifecycleViewModel", "RecordRecentVault", 1),
        new("VaultLifecycleViewModel.cs", "SlateWindows.VaultLifecycleViewModel", "RemoveRecentVault", 1),
        new("WorkspaceTabViewModel.Properties.cs", "SlateWindows.WorkspaceTabViewModel", "WriteProperty", 1),
        new("WorkspaceViewModel.Citations.cs", "SlateWindows.WorkspaceViewModel", "ReadAndSeedSources", 1),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "CommitRestoreAsDeleted", 2),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "CommitRestoreAsVersion", 2),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "RecoverAdmitted", 3),
        new("WorkspaceViewModel.History.cs", "SlateWindows.WorkspaceViewModel", "RestoreAdmitted", 2),
        new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "AddWriteCompletion", 1),
        new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "BulkRenameApplyAtItsTurn", 1),
        new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "PropertyWriteCompletion", 1),
        new("WorkspaceViewModel.Templates.cs", "SlateWindows.WorkspaceViewModel", "CreateFromTemplate", 1),
        new("WorkspaceViewModel.cs", "SlateWindows.WorkspaceTabViewModel", "PerformTaskToggle", 2),
        new("WorkspaceViewModel.cs", "SlateWindows.WorkspaceTabViewModel", "StartAdmittedTaskToggle", 1),
    ];

    private static readonly Lazy<IReadOnlyList<Read>> Shell = new(() =>
    {
        IMethodSymbol helper = BasesErrorDetailCensus.TheHelper(ShellCompilation.Compilation);
        return [.. ShellCompilation.Sources.SelectMany(source =>
            Reads(ShellCompilation.ModelFor(source.Source), source.Relative, helper))];
    });

    /// <summary>Fail closed: a shell source that does not bind could hide an
    /// exception's type from the rule.</summary>
    [Fact]
    public void TheShellSourcesBindWithoutErrors()
    {
        string[] errors = [.. ShellCompilation.Sources.SelectMany(source => ShellCompilation.ModelFor(source.Source)
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{source.Relative}: {diagnostic}"))];
        Assert.True(errors.Length == 0, $"{errors.Length} error(s):\n" + string.Join("\n", errors.Take(20)));
    }

    [Fact]
    public void NoShellFailureBecomesTextOutsideFailureDetail()
    {
        IReadOnlyList<string> problems = Problems(Shell.Value, Remaining);
        Assert.True(
            problems.Count == 0,
            "A shell sentence reads an exception's Message, which for a uniffi error is the binding's field dump. "
            + "Say FailureDetail.Of(exception) instead; a dated row is only for a read that stood on 2026-11-18:\n"
            + string.Join("\n", problems));
    }

    /// <summary>The diff red team's minor 5: the table only shrinks. On
    /// 2026-11-18 it held 61 rows and 73 reads, each row dated that day and
    /// owned by the chunk its folder names (CB-15b for Canvas/, Graph/ and
    /// Search/, CB-15c for the rest). A row added later fails, and so does a
    /// ceiling left above the table once a PR routes reads.</summary>
    [Fact]
    public void TheTableOnlyShrinks()
    {
        IReadOnlyList<string> problems = TableProblems(Remaining);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>The ratchet on planted tables: more rows or reads than the
    /// ceilings, a ceiling above the table, a row dated another day, and a
    /// row owned by another chunk than its folder's are each named.</summary>
    [Fact]
    public void TheRatchetNamesAGrownTableALooseCeilingAnotherDateAndAnotherOwner()
    {
        Row canvas = new("Canvas/Planted.cs", "SlateWindows.Canvas.Planted", "M", 2, CB15b, Dated, "a planted read");
        Row panel = new("Panels/Planted.cs", "SlateWindows.Panels.Planted", "N", 1, CB15c, Dated, "a planted read");
        Allowance[] baseline = [AllowanceOf(canvas), AllowanceOf(panel)];
        Assert.Empty(TableProblems([canvas, panel], baseline, rowCeiling: 2, readCeiling: 3));

        Assert.Contains(
            TableProblems([canvas, panel, panel with { Member = "O" }], baseline, rowCeiling: 2, readCeiling: 4),
            problem => problem.StartsWith("the table holds 3 rows", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel with { Reads = 2 }], baseline, rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("the table's rows hold 4 reads", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas], baseline, rowCeiling: 2, readCeiling: 2),
            problem => problem.Contains("lower RowCeiling to 1", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel], baseline, rowCeiling: 2, readCeiling: 4),
            problem => problem.Contains("lower ReadCeiling to 3", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel with { Dated = "2026-11-19" }], baseline, rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("Panels/Planted.cs (SlateWindows.Panels.Planted.N): dated 2026-11-19", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas with { Owner = CB15c }, panel], baseline, rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("Canvas/Planted.cs (SlateWindows.Canvas.Planted.M): owned by CB-15c", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel with { Owner = CB15b }], baseline, rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("Panels/Planted.cs (SlateWindows.Panels.Planted.N): owned by CB-15b", StringComparison.Ordinal));
    }

    /// <summary>Codex round 1, finding 1: an allowance cannot move. A row
    /// rekeyed to another member (its read moved there), and a count moved
    /// from one row to another, with the totals, dates and owners unchanged,
    /// are each named against the baseline.</summary>
    [Fact]
    public void TheRatchetNamesAMovedAllowance()
    {
        Row canvas = new("Canvas/Planted.cs", "SlateWindows.Canvas.Planted", "M", 2, CB15b, Dated, "a planted read");
        Row panel = new("Panels/Planted.cs", "SlateWindows.Panels.Planted", "N", 1, CB15c, Dated, "a planted read");
        Allowance[] baseline = [AllowanceOf(canvas), AllowanceOf(panel)];
        Assert.Empty(TableProblems([canvas, panel], baseline, rowCeiling: 2, readCeiling: 3));

        Assert.Contains(
            TableProblems([canvas, panel with { Member = "Moved" }], baseline, rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith(
                "Panels/Planted.cs (SlateWindows.Panels.Planted.Moved): no allowance in the baseline", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas with { Reads = 1 }, panel with { Reads = 2 }], baseline, rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith(
                "Panels/Planted.cs (SlateWindows.Panels.Planted.N): the row names 2 reads and its baseline allows 1",
                StringComparison.Ordinal));
    }

    /// <summary>The allowance a row stands for: its key and its count.</summary>
    internal sealed record Allowance(string File, string Type, string Member, int Reads);

    private static Allowance AllowanceOf(Row row) => new(row.File, row.Type, row.Member, row.Reads);

    /// <summary>The table's ceilings: the rows and reads it holds now. A PR
    /// that routes reads lowers them to what is left: 61 rows and 73 reads on
    /// 2026-11-18, 39 and 47 once CB-15b routed Canvas/, Graph/ and
    /// Search/.</summary>
    internal const int RowCeiling = 39;

    internal const int ReadCeiling = 47;

    /// <summary>The baseline itself never grows: it holds the 61 members and
    /// 73 reads that stood on 2026-11-18, each once.</summary>
    [Fact]
    public void TheBaselineHoldsTheAllowancesOf20261118()
    {
        Assert.Equal(61, Baseline.Length);
        Assert.Equal(73, Baseline.Sum(entry => entry.Reads));
        Assert.Equal(Baseline.Length, Baseline.Select(entry => (entry.File, entry.Type, entry.Member)).Distinct().Count());
        Assert.All(Baseline, entry => Assert.True(entry.Reads >= 1, $"{entry.File} ({entry.Type}.{entry.Member}) allows no read."));
    }

    /// <summary>The table against its baseline and ceilings.</summary>
    internal static IReadOnlyList<string> TableProblems(IReadOnlyList<Row> rows) =>
        TableProblems(rows, Baseline, RowCeiling, ReadCeiling);

    /// <summary>What the table itself may not do: hold a row for a member the
    /// baseline does not name, or more reads in a row than its allowance (so
    /// an allowance never moves to another member or between rows); grow past
    /// its ceilings, or fall below them without lowering them; hold a row
    /// dated another day; or give a row to another chunk than its
    /// folder's.</summary>
    internal static IReadOnlyList<string> TableProblems(
        IReadOnlyList<Row> rows, IReadOnlyList<Allowance> baseline, int rowCeiling, int readCeiling)
    {
        List<string> problems = [];
        foreach (Row row in rows)
        {
            Allowance? allowed = baseline.FirstOrDefault(entry =>
                (entry.File, entry.Type, entry.Member) == (row.File, row.Type, row.Member));
            if (allowed is null)
            {
                problems.Add($"{Named(row)}: no allowance in the baseline names this member. A row stands only for a "
                    + $"read that stood on {Dated} in the member it stood in; an allowance never moves.");
            }
            else if (row.Reads > allowed.Reads)
            {
                problems.Add($"{Named(row)}: the row names {row.Reads} reads and its baseline allows {allowed.Reads}. "
                    + "An allowance never moves from one row to another.");
            }
        }
        int reads = rows.Sum(row => row.Reads);
        if (rows.Count > rowCeiling)
        {
            problems.Add($"the table holds {rows.Count} rows, more than its ceiling of {rowCeiling}: it only shrinks, "
                + "so a new read goes through FailureDetail.Of, never into a row.");
        }
        else if (rows.Count < rowCeiling)
        {
            problems.Add($"the table holds {rows.Count} rows under a ceiling of {rowCeiling}: lower RowCeiling to "
                + $"{rows.Count}, so the rows this PR routed cannot come back.");
        }
        if (reads > readCeiling)
        {
            problems.Add($"the table's rows hold {reads} reads, more than its ceiling of {readCeiling}: it only "
                + "shrinks, so a new read goes through FailureDetail.Of, never into a row.");
        }
        else if (reads < readCeiling)
        {
            problems.Add($"the table's rows hold {reads} reads under a ceiling of {readCeiling}: lower ReadCeiling "
                + $"to {reads}, so the reads this PR routed cannot come back.");
        }
        foreach (Row row in rows)
        {
            if (row.Dated != Dated)
            {
                problems.Add($"{Named(row)}: dated {row.Dated}; every row is a read that stood on {Dated}, and none "
                    + "is added after it.");
            }
            string owner = OwnerOf(row.File);
            if (row.Owner != owner)
            {
                problems.Add($"{Named(row)}: owned by {row.Owner}; a row in this folder is {owner}'s "
                    + "(CB-15b: Canvas/, Graph/ and Search/; CB-15c: the rest).");
            }
        }
        return problems;
    }

    /// <summary>The chunk that ends a row in <paramref name="file"/>'s folder
    /// (plan 47 §5).</summary>
    private static string OwnerOf(string file) =>
        file.StartsWith("Canvas/", StringComparison.Ordinal)
            || file.StartsWith("Graph/", StringComparison.Ordinal)
            || file.StartsWith("Search/", StringComparison.Ordinal)
            ? CB15b
            : CB15c;

    /// <summary>The rule's shapes, planted beside the shell and run through
    /// MessageReads alone: each is named, and a typed field, a record's own
    /// Message and the helper's call are not.</summary>
    [Theory]
    [InlineData("detail = thrown.Message;", "thrown.Message")]
    [InlineData("detail = thrown?.Message ?? \"\";", "thrown?.Message")]
    [InlineData("if (thrown is { Message: var text }) { detail = text; }", "Message: var text")]
    [InlineData("if (thrown is { InnerException.Message: var text }) { detail = text; }", "InnerException.Message: var text")]
    [InlineData("detail = thrown.GetBaseException().Message;", "thrown.GetBaseException().Message")]
    [InlineData("try { work.Wait(); } catch (VaultException.Io io) { detail = io.Message; }", "io.Message")]
    [InlineData("try { work.Wait(); } catch (VaultException.Io io) { detail = io.message; }", "")]
    [InlineData("detail = new Note(\"x\").Message;", "")]
    [InlineData("detail = FailureDetail.Of(thrown);", "")]
    public void TheRuleNamesEveryReadOfAnExceptionsMessage(string body, string named)
    {
        Read[] reads = PlantedReads($$"""
            namespace SlateWindows.Planted
            {
                using uniffi.slate_uniffi;

                internal sealed class Planted
                {
                    private sealed record Note(string Message);

                    private string M(Exception thrown, Task work)
                    {
                        string detail = "";
                        {{body}}
                        return detail;
                    }
                }
            }
            """);
        if (named.Length == 0)
        {
            Assert.Empty(reads);
        }
        else
        {
            Read read = Assert.Single(reads);
            Assert.Contains(named, read.Context, StringComparison.Ordinal);
            Assert.Equal(("SlateWindows.Planted.Planted", "M"), (read.Type, read.Member));
        }
    }

    /// <summary>The real helper's read is exempt, by its symbol. A class named
    /// FailureDetail in another namespace with a method Of, and an overload of
    /// Of in the real class, are each named.</summary>
    [Fact]
    public void OnlyTheRealHelpersReadIsExempt()
    {
        Read[] reads = PlantedReads(
            """
            namespace SlateWindows.Planted
            {
                internal static class FailureDetail
                {
                    internal static string Of(Exception failure) => failure.Message;
                }
            }

            namespace SlateWindows
            {
                internal static partial class FailureDetail
                {
                    internal static string Of(Exception failure, string lead) => lead + failure.Message;
                }
            }
            """,
            failureDetail: BasesErrorDetailCensus.PartialHelper);
        (string File, string Type, string Member, int Line)[] named =
            [("Planted.cs", "SlateWindows.Planted.FailureDetail", "Of", 5), ("Planted.cs", "SlateWindows.FailureDetail", "Of", 13)];
        Assert.Equal(named, reads.Select(read => (read.File, read.Type, read.Member, read.Line)).OrderBy(read => read.Line));
    }

    /// <summary>Codex round 1, finding 2: a read keys by its type's full name
    /// (namespace, nesting and arity) and by its member as
    /// SaveChainAdmissionCensus names one. A read moved into a type of the same
    /// name in another namespace or nested in another type, into a generic
    /// twin of another arity, or into an explicit interface implementation
    /// beside an ordinary member of the same name, keys apart, so a row for
    /// one never stands for another.</summary>
    [Fact]
    public void ARowForOneMemberNeverStandsForItsTwin()
    {
        Read[] reads = PlantedReads(Twins);
        (string Type, string Member)[] keys =
        [
            ("SlateWindows.Planted.Both", "Read"),
            ("SlateWindows.Planted.Both", "SlateWindows.Planted.IReader.Read"),
            ("SlateWindows.Planted.Pair`1", "Read"),
            ("SlateWindows.Planted.Pair`2", "Read"),
            ("SlateWindows.Planted.Store", "Read"),
            ("SlateWindows.Planted.Twin.Store", "Read"),
            ("SlateWindows.Planted.Wrapper+Store", "Read"),
        ];
        Assert.Equal(
            keys,
            reads.Select(read => (read.Type, read.Member))
                .Distinct()
                .OrderBy(key => key.Type, StringComparer.Ordinal)
                .ThenBy(key => key.Member, StringComparer.Ordinal));

        IReadOnlyList<string> problems = Problems(
            reads, [new("Planted.cs", "SlateWindows.Planted.Store", "Read", 1, CB15c, Dated, "a planted read")]);
        foreach ((string type, string member) in keys.Where(key => key.Type != "SlateWindows.Planted.Store"))
        {
            Assert.Contains(problems, problem => problem.Contains(
                $"Planted.cs ({type}.{member}) holds 1 read(s) and no row names it", StringComparison.Ordinal));
        }
    }

    /// <summary>Reads in types and members whose short names collide.</summary>
    private const string Twins = """
        namespace SlateWindows.Planted
        {
            internal sealed class Store
            {
                internal string Read(Exception failure) => failure.Message;
            }

            internal sealed class Wrapper
            {
                internal sealed class Store
                {
                    internal string Read(Exception failure) => failure.Message;
                }
            }

            internal sealed class Pair<T>
            {
                internal string Read(Exception failure) => failure.Message;
            }

            internal sealed class Pair<TFirst, TSecond>
            {
                internal string Read(Exception failure) => failure.Message;
            }

            internal interface IReader
            {
                string Read(Exception failure);
            }

            internal sealed class Both : IReader
            {
                public string Read(Exception failure) => failure.Message;

                string IReader.Read(Exception failure) => failure.Message;
            }
        }

        namespace SlateWindows.Planted.Twin
        {
            internal sealed class Store
            {
                internal string Read(Exception failure) => failure.Message;
            }
        }
        """;

    /// <summary>Each place a read can stand counts to the member and type a
    /// row names: a lambda and a local function to their member; an
    /// accessor (get, set, init, add, remove) and an expression-bodied
    /// property or indexer to their property, event or indexer (this[]); a
    /// constructor, static or not, to its type's name; an operator or
    /// conversion to its metadata name; a finalizer as Finalize; a field or
    /// property initializer to that field or property; a primary
    /// constructor's base arguments to the type; every overload of a name
    /// together; and the same name in a second or nested type apart.</summary>
    [Fact]
    public void EachReadCountsToTheMemberAndTypeItsRowNames()
    {
        Read[] reads = PlantedReads(Holders);
        Assert.Equal(
            HolderKeys,
            reads.GroupBy(read => (read.Type, read.Member))
                .Select(group => (group.Key.Type, group.Key.Member, Reads: group.Count()))
                .OrderBy(group => group.Type, StringComparer.Ordinal)
                .ThenBy(group => group.Member, StringComparer.Ordinal));
    }

    /// <summary>The table is held to the reads both ways: a member with more
    /// reads than its row, or a read with no row, is named with a ready row; a
    /// row with fewer reads in its member than it names, or none, is stale;
    /// two rows for one member are named; and a row may never stand for the
    /// Files sidebar, the tag edit, or a file in Canvas/, Graph/ or
    /// Search/.</summary>
    [Fact]
    public void TheTableIsHeldToTheReads()
    {
        Read[] reads = PlantedReads(Holders);
        Row[] exact = [.. HolderKeys.Select(key => new Row("Planted.cs", key.Type, key.Member, key.Reads, CB15c, Dated, "a planted read"))];
        Assert.Empty(Problems(reads, exact));

        // A member with two reads under a one-read row.
        IReadOnlyList<string> under = Problems(reads, [.. exact.Select(row => row.Member == "Twice" && row.Type == "SlateWindows.Planted.Holders" ? row with { Reads = 1 } : row)]);
        Assert.Contains(under, problem => problem.Contains("Planted.cs (SlateWindows.Planted.Holders.Twice) holds 2 reads and its row names 1", StringComparison.Ordinal));
        Assert.Contains(under, problem => problem.Contains("new(\"Planted.cs\", \"SlateWindows.Planted.Holders\", \"Twice\", 2,", StringComparison.Ordinal));

        // A read with no row.
        IReadOnlyList<string> unrowed = Problems(reads, [.. exact.Where(row => row.Type != "SlateWindows.Planted.Other")]);
        Assert.Contains(unrowed, problem => problem.Contains("(SlateWindows.Planted.Other.Twice)", StringComparison.Ordinal));
        Assert.Contains(unrowed, problem => problem.Contains("new(\"Planted.cs\", \"SlateWindows.Planted.Other\", \"Twice\", 1,", StringComparison.Ordinal));

        // A row with fewer reads in its member than it names, and a row with none.
        IReadOnlyList<string> stale = Problems(
            reads,
            [
                .. exact.Select(row => row.Member == "ByLocal" ? row with { Reads = 2 } : row),
                new("Planted.cs", "SlateWindows.Planted.Holders", "Gone", 1, CB15c, Dated, "a read that was routed"),
            ]);
        Assert.Contains(stale, problem => problem.StartsWith("Planted.cs (SlateWindows.Planted.Holders.ByLocal): the row names 2 reads", StringComparison.Ordinal));
        Assert.Contains(stale, problem => problem.StartsWith("Planted.cs (SlateWindows.Planted.Holders.Gone): a row that matches no read", StringComparison.Ordinal));

        // Two rows for one member.
        Assert.Contains(
            Problems(reads, [.. exact, exact.First(row => row.Member == "Spoken")]),
            problem => problem.StartsWith("Planted.cs (SlateWindows.Planted.Holders.Spoken): more than one row names it", StringComparison.Ordinal));

        // The sidebar's files and the tag edit never stand as rows.
        IReadOnlyList<string> sidebar = Problems(
            [],
            [
                new("FilesSidebarViewModel.TreeOperations.cs", "SlateWindows.FilesSidebarViewModel", "Refresh", 1, CB15c, Dated, "Could not load files"),
                new("WorkspaceViewModel.Properties.cs", "SlateWindows.WorkspaceViewModel", "EditTagAtItsTurn", 1, CB15c, Dated, "Tag edit failed"),
            ]);
        Assert.Contains(sidebar, problem => problem.StartsWith("FilesSidebarViewModel.TreeOperations.cs (SlateWindows.FilesSidebarViewModel.Refresh): CB-15a routed", StringComparison.Ordinal));
        Assert.Contains(sidebar, problem => problem.StartsWith("WorkspaceViewModel.Properties.cs (SlateWindows.WorkspaceViewModel.EditTagAtItsTurn): CB-15a routed", StringComparison.Ordinal));

        // Nor does any file in Canvas/, Graph/ or Search/, each folder planted
        // on its own so a rule that forgot one fails; a folder of the same
        // name deeper in the tree is not one of them.
        (string File, string Type)[] routed =
        [
            ("Canvas/CanvasDocumentViewModel.cs", "SlateWindows.Canvas.CanvasDocumentViewModel"),
            ("Graph/GraphConfigStore.cs", "SlateWindows.Graph.GraphConfigStore"),
            ("Search/SearchOverlayViewModel.cs", "SlateWindows.Search.SearchOverlayViewModel"),
        ];
        foreach ((string file, string type) in routed)
        {
            IReadOnlyList<string> folder = Problems([], [new(file, type, "Planted", 1, CB15b, Dated, "a planted read")]);
            Assert.Contains(folder, problem => problem.StartsWith($"{file} ({type}.Planted): CB-15b routed", StringComparison.Ordinal));
        }
        Assert.DoesNotContain(
            Problems([], [new("Panels/Canvas/Planted.cs", "SlateWindows.Panels.Planted", "Planted", 1, CB15c, Dated, "a planted read")]),
            problem => problem.Contains("CB-15b routed", StringComparison.Ordinal));

        // A row says who ends it, when, and what it reaches.
        Assert.Contains(
            Problems(reads, [.. exact.Select(row => row.Member == "Initial" ? row with { Reaches = " " } : row)]),
            problem => problem.StartsWith("Planted.cs (SlateWindows.Planted.Holders.Initial): a row with no owner", StringComparison.Ordinal));
    }

    /// <summary>The members the planted holders' reads count to, and how many
    /// each holds.</summary>
    private static readonly (string Type, string Member, int Reads)[] HolderKeys =
    [
        ("SlateWindows.Planted.Derived", "Derived", 1),
        ("SlateWindows.Planted.Holders", "ByLambda", 1),
        ("SlateWindows.Planted.Holders", "ByLocal", 1),
        ("SlateWindows.Planted.Holders", "Detail", 2),
        ("SlateWindows.Planted.Holders", "Finalize", 1),
        ("SlateWindows.Planted.Holders", "Holders", 2),
        ("SlateWindows.Planted.Holders", "Initial", 1),
        ("SlateWindows.Planted.Holders", "Said", 2),
        ("SlateWindows.Planted.Holders", "Seeded", 1),
        ("SlateWindows.Planted.Holders", "Spoken", 1),
        ("SlateWindows.Planted.Holders", "Twice", 2),
        ("SlateWindows.Planted.Holders", "_render", 1),
        ("SlateWindows.Planted.Holders", "op_Addition", 1),
        ("SlateWindows.Planted.Holders", "op_Implicit", 1),
        ("SlateWindows.Planted.Holders", "this[]", 1),
        ("SlateWindows.Planted.Holders+Nested", "Twice", 1),
        ("SlateWindows.Planted.Other", "Twice", 1),
    ];

    /// <summary>The planted holders: a read in each place a read can
    /// stand.</summary>
    private const string Holders = """
        namespace SlateWindows.Planted
        {
            internal sealed class Holders
            {
                private readonly Func<Exception, string> _render = failure => failure.Message;

                private string _last = "";

                static Holders()
                {
                    _ = new InvalidOperationException("planted").Message;
                }

                internal Holders(Exception failure)
                {
                    _last = failure.Message;
                }

                ~Holders()
                {
                    _last = Failure?.Message ?? "";
                }

                internal Exception? Failure { get; set; }

                internal string Initial { get; } = new InvalidOperationException("planted").Message;

                internal string Spoken => Failure?.Message ?? "";

                internal string Detail
                {
                    get { return Failure!.Message; }
                    set { _last = Failure!.Message + value; }
                }

                internal string Seeded
                {
                    get => _last;
                    init => _last = value + (Failure?.Message ?? "");
                }

                internal event Action<string>? Said
                {
                    add { _last = Failure!.Message; }
                    remove { _last = Failure!.Message; }
                }

                internal string this[Exception failure] => failure.Message;

                public static string operator +(Holders holders, Exception failure) => holders._last + failure.Message;

                public static implicit operator string(Holders holders) => holders.Failure?.Message ?? "";

                internal string ByLambda(Exception failure)
                {
                    Func<string> say = () => failure.Message;
                    return say() + _last + _render(failure);
                }

                internal string ByLocal(Exception failure)
                {
                    string Say() => failure.Message;
                    return Say();
                }

                internal string Twice(Exception failure) => failure.Message;

                internal string Twice(Exception failure, string lead) => lead + failure.Message;

                internal sealed class Nested
                {
                    internal string Twice(Exception failure) => failure.Message;
                }
            }

            internal sealed class Other
            {
                internal string Twice(Exception failure) => failure.Message;
            }

            internal class Base(string text)
            {
                internal string Text { get; } = text;
            }

            internal sealed class Derived(Exception failure) : Base(failure.Message);
        }
        """;

    /// <summary>Binds <paramref name="source"/> beside the shell (as
    /// BasesErrorDetailCensus.PlantedViolations does) and returns its reads;
    /// it must bind cleanly or the case proves nothing. With
    /// <paramref name="failureDetail"/>, that text stands in for
    /// <c>FailureDetail.cs</c>, whose reads are returned too.</summary>
    private static Read[] PlantedReads(string source, string? failureDetail = null)
    {
        var options = (CSharpParseOptions)ShellCompilation.Compilation.SyntaxTrees.First().Options;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, options, path: "Planted.cs");
        CSharpCompilation compilation = ShellCompilation.Compilation.AddSyntaxTrees(tree);
        SyntaxTree? helperTree = null;
        if (failureDetail is not null)
        {
            helperTree = CSharpSyntaxTree.ParseText(failureDetail, options, path: "FailureDetail.cs");
            compilation = compilation.ReplaceSyntaxTree(BasesErrorDetailCensus.FailureDetailTree(), helperTree);
        }
        SemanticModel model = compilation.GetSemanticModel(tree);
        Diagnostic[] errors = [.. model.GetDiagnostics()
            .Concat(helperTree is null ? [] : compilation.GetSemanticModel(helperTree).GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(errors.Length == 0, "premise: the planted source binds. " + string.Join("\n", errors));
        IMethodSymbol helper = BasesErrorDetailCensus.TheHelper(compilation);
        return
        [
            .. Reads(model, "Planted.cs", helper),
            .. helperTree is null ? [] : Reads(compilation.GetSemanticModel(helperTree), "FailureDetail.cs", helper),
        ];
    }

    /// <summary>Rule 1 over one bound tree, each read keyed by the member and
    /// type that hold it.</summary>
    internal static IEnumerable<Read> Reads(SemanticModel model, string file, IMethodSymbol helper) =>
        BasesErrorDetailCensus.MessageReads(model, file, BasesErrorDetailCensus.InTheHelper(model, helper))
            .Select(read =>
            {
                (string type, string member) = Holder(model, read.Name);
                return new Read(file, type, member, read.Line, read.Context);
            });

    /// <summary>The declaration that holds a read's operation tree, keyed as
    /// SaveChainAdmissionCensus keys one (HoldsAnOperationTree and MemberOf),
    /// by its declared symbol: the member is the symbol's name (a method's,
    /// an explicit interface implementation's qualified name included; a
    /// finalizer's Finalize; an operator's or conversion's metadata name), a
    /// constructor, static or not, is its type's name, and an accessor, an
    /// expression-bodied property or indexer and a property initializer are
    /// their property, event or indexer (this[]). A field initializer is its
    /// field, and a primary constructor's base arguments are the type's name.
    /// A lambda or local function counts to the member around it. The type is
    /// the one that declares the member, by its full name (TypeKey).</summary>
    internal static (string Type, string Member) Holder(SemanticModel model, SyntaxNode node)
    {
        for (SyntaxNode? current = node.Parent; current is not null; current = current.Parent)
        {
            ISymbol? symbol = current switch
            {
                BaseMethodDeclarationSyntax or AccessorDeclarationSyntax => model.GetDeclaredSymbol(current),
                ArrowExpressionClauseSyntax { Parent: BasePropertyDeclarationSyntax property } => model.GetDeclaredSymbol(property),
                VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax } => model.GetDeclaredSymbol(current),
                BasePropertyDeclarationSyntax or BaseTypeDeclarationSyntax => model.GetDeclaredSymbol(current),
                _ => null,
            };
            if (symbol is null)
            {
                continue;
            }
            INamedTypeSymbol? type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
            string member = symbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } constructor =>
                    constructor.ContainingType.Name,
                IMethodSymbol { AssociatedSymbol: { } owner } => owner.Name,
                _ => symbol.Name,
            };
            return (type is null ? "(no type)" : TypeKey(type), member);
        }
        return ("(no type)", "(no member)");
    }

    /// <summary>A type's full name as metadata spells it: its namespace, each
    /// type it is nested in (joined by +) and its arity (Pair`2), so a type
    /// of the same name in another namespace or another type, or with
    /// another number of type parameters, keys apart.</summary>
    internal static string TypeKey(INamedTypeSymbol type)
    {
        string name = type.MetadataName;
        for (INamedTypeSymbol? outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            name = outer.MetadataName + "+" + name;
        }
        return type.ContainingNamespace is { IsGlobalNamespace: false } space
            ? space.ToDisplayString() + "." + name
            : name;
    }

    /// <summary>What the rows do not answer: a read in a member no row names,
    /// or in a member holding more reads than its row (each printed with a
    /// ready row); a row naming more reads than its member holds, or matching
    /// none (stale); a row for the Files sidebar or the tag edit, which CB-15a
    /// routed, or for a file in Canvas/, Graph/ or Search/, which CB-15b
    /// routed; a row with no owner, date or sentence; two rows for one
    /// member.</summary>
    internal static IReadOnlyList<string> Problems(IReadOnlyList<Read> reads, IReadOnlyList<Row> rows)
    {
        List<string> problems = [];
        foreach (Row row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Owner) || string.IsNullOrWhiteSpace(row.Dated)
                || string.IsNullOrWhiteSpace(row.Reaches) || row.Reads < 1)
            {
                problems.Add($"{Named(row)}: a row with no owner, no date, no sentence it reaches, or no reads.");
            }
            if (Path.GetFileName(row.File).StartsWith("FilesSidebarViewModel", StringComparison.Ordinal)
                || row.Member == "EditTagAtItsTurn")
            {
                problems.Add($"{Named(row)}: CB-15a routed the Files sidebar's reads and the tag edit's through "
                    + "FailureDetail.Of; they never stand as rows.");
            }
            if (row.File.StartsWith("Canvas/", StringComparison.Ordinal)
                || row.File.StartsWith("Graph/", StringComparison.Ordinal)
                || row.File.StartsWith("Search/", StringComparison.Ordinal))
            {
                problems.Add($"{Named(row)}: CB-15b routed every read in Canvas/, Graph/ and Search/ through "
                    + "FailureDetail.Of; they never stand as rows.");
            }
        }
        problems.AddRange(rows
            .GroupBy(row => (row.File, row.Type, row.Member))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.File} ({group.Key.Type}.{group.Key.Member}): more than one row names it."));
        foreach (IGrouping<(string File, string Type, string Member), Read> member in reads
            .GroupBy(read => (read.File, read.Type, read.Member))
            .OrderBy(group => group.Key.File, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Type, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Member, StringComparer.Ordinal))
        {
            string where = $"{member.Key.File} ({member.Key.Type}.{member.Key.Member})";
            int count = member.Count();
            Row? row = rows.FirstOrDefault(candidate =>
                (candidate.File, candidate.Type, candidate.Member) == member.Key);
            if (row is null || count > row.Reads)
            {
                problems.AddRange(member.OrderBy(read => read.Line).Select(read => read.ToString()));
                problems.Add(row is null
                    ? $"  {where} holds {count} read(s) and no row names it. Route each through FailureDetail.Of; "
                        + $"only a read that stood on {Dated} has a row:"
                    : $"  {where} holds {count} reads and its row names {row.Reads}. Route the new read through "
                        + "FailureDetail.Of; the row stands only for the reads it was written for:");
                problems.Add($"    {ReadyRow(member.Key, count)}");
            }
            else if (count < row.Reads)
            {
                problems.Add($"{Named(row)}: the row names {row.Reads} reads and the member holds {count} "
                    + $"(lower the row to {count}).");
            }
        }
        problems.AddRange(rows
            .Where(row => !reads.Any(read => (read.File, read.Type, read.Member) == (row.File, row.Type, row.Member)))
            .Select(row => $"{Named(row)}: a row that matches no read (delete it)."));
        return problems;
    }

    private static string Named(Row row) => $"{row.File} ({row.Type}.{row.Member})";

    private static string ReadyRow((string File, string Type, string Member) key, int count) =>
        $"new(\"{key.File}\", \"{key.Type}\", \"{key.Member}\", {count}, <owner>, Dated, \"<the sentence it reaches>\"),";
}
