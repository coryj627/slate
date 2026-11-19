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
// for the rest. A row names its file, its type, its member and how many reads
// the member holds. A read with no row, or a member holding more reads than
// its row, fails and prints a ready row; a row holding more than the member
// reads, or matching no read, fails as stale, so each later PR deletes or
// lowers the rows it ends. No row may name a Files sidebar file or the tag
// edit's EditTagAtItsTurn, which CB-15a routed.
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
// What this census cannot see:
// - A count is the key: one read routed and another added in the same member
//   keeps the count, and passes.
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
    /// own ready rows on 2026-11-18: 61 members, 73 reads (CB-15b 22 members
    /// and 26 reads, CB-15c 39 members and 47 reads).</summary>
    internal static readonly Row[] Remaining =
    [
        // CB-15b: Canvas/, Graph/ and Search/.
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasAddFileCard", 1, CB15b, Dated, "CanvasActionFailed (Create), adding a file card"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasAddLinkCard", 1, CB15b, Dated, "CanvasActionFailed (Create), adding a link card"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasConvertToNote", 1, CB15b, Dated, "CanvasBlocked (NoteReadFailed), converting a card to a note"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasCreateConnectedCard", 1, CB15b, Dated, "CanvasActionFailed (CreateConnectedCard)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasDuplicate", 1, CB15b, Dated, "CanvasActionFailed (Duplicate)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasMoveIntoGroup", 1, CB15b, Dated, "CanvasActionFailed (MoveIntoGroup)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasNewCard", 1, CB15b, Dated, "CanvasActionFailed (NewCard)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasNewGroup", 1, CB15b, Dated, "CanvasActionFailed (NewGroup)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "CanvasRemoveFromGroup", 1, CB15b, Dated, "CanvasActionFailed (RemoveFromGroup)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "RequestVaultPick", 1, CB15b, Dated, "CanvasActionFailed, the vault picker's load (Locate or add a file card)"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "SubmitBulkMarked", 1, CB15b, Dated, "CanvasActionFailed (CanvasAction), a change to the marked cards"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "SubmitGroupMarked", 1, CB15b, Dated, "CanvasActionFailed (NewGroup), grouping the marked cards"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasDocumentViewModel", "TryWhereAmI", 1, CB15b, Dated, "CanvasActionFailed (WhereAmI), the card's readback"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasPhrase", "OpenFailed", 1, CB15b, Dated, "This canvas could not be opened: {detail} ({path})"),
        new("Canvas/CanvasDocumentViewModel.cs", "CanvasPhrase", "RetargetAbsent", 1, CB15b, Dated, "CanvasBlocked (ReopenFailed), {from} moved to {to}: {detail}"),
        new("Canvas/CanvasMutationFunnel.cs", "CanvasMutationFunnel", "Transact", 1, CB15b, Dated, "CanvasActionFailed (CanvasAction), an apply core refused"),
        new("Graph/ConnectionsLeafViewModel.cs", "ConnectionsLeafViewModel", "FetchUnder", 3, CB15b, Dated, "the Connections leaf's load failure"),
        new("Graph/GraphConfigStore.cs", "GraphConfigStore", "Read", 1, CB15b, Dated, "{file} is unreadable: {detail}, the graph config's load failure"),
        new("Graph/GraphConfigStore.cs", "GraphConfigStore", "Reason", 2, CB15b, Dated, "the graph config's refusal, a newer version or an unreadable file"),
        new("Graph/GraphDocumentViewModel.cs", "GraphDocumentViewModel", "FetchUnder", 2, CB15b, Dated, "the graph's load failure"),
        new("Graph/GraphDocumentViewModel.cs", "GraphDocumentViewModel", "HumanReadable", 1, CB15b, Dated, "the graph's error arm for any other vault error (CD9)"),
        new("Search/SearchOverlayViewModel.cs", "SearchOverlayViewModel", "HumanReadable", 1, CB15b, Dated, "the search overlay's error arm for any other vault error (CD9)"),

        // CB-15c: the rest of the shell.
        new("Commands/SlateCommandRegistrar.cs", "DispatcherCommandAction", "Invoke", 1, CB15c, Dated, "CommandException.ActionFailed, a palette command's failure"),
        new("EditorInteractions.cs", "EditorInteractionCoordinator", "ResolveEmbedPreview", 1, CB15c, Dated, "EmbedUnresolvedReason.ReadError, an embed preview's read failure"),
        new("FindInNoteViewModel.cs", "FindInNoteViewModel", "CreateStrategy", 1, CB15c, Dated, "Find in note's pattern error"),
        new("Panels/BibliographyViewModel.cs", "BibliographyViewModel", "LoadEntries", 1, CB15c, Dated, "the bibliography panel's load failure"),
        new("Panels/BibliographyViewModel.cs", "BibliographyViewModel", "LoadUnresolved", 1, CB15c, Dated, "the bibliography panel's unresolved citations failure"),
        new("Panels/BulkRenameViewModel.cs", "BulkRenameViewModel", "RunPreview", 1, CB15c, Dated, "the bulk rename sheet's preview error"),
        new("Panels/BulkRenameViewModel.cs", "BulkRenameViewModel", "StartApplyWork", 1, CB15c, Dated, "the bulk rename sheet's apply error"),
        new("Panels/CitationsPanelViewModel.cs", "CitationsPanelViewModel", "Refresh", 1, CB15c, Dated, "the citations panel's load failure"),
        new("Panels/HistoryViewModel.cs", "HistoryViewModel", "LoadDeletedFiles", 1, CB15c, Dated, "the history panel's deleted files failure"),
        new("Panels/HistoryViewModel.cs", "HistoryViewModel", "LoadFirstPageBody", 1, CB15c, Dated, "the history panel's versions failure"),
        new("Panels/HistoryViewModel.cs", "HistoryViewModel", "LoadOlder", 1, CB15c, Dated, "the history panel's older versions failure"),
        new("Panels/HistoryViewModel.cs", "HistoryViewModel", "RunDiff", 1, CB15c, Dated, "the history panel's diff failure"),
        new("Panels/NotePropertiesViewModel.cs", "NotePropertiesViewModel", "RefreshProperties", 1, CB15c, Dated, "the properties panel's load failure"),
        new("Panels/RightPanePanelsViewModel.cs", "RightPanePanelsViewModel", "LoadLinks", 1, CB15c, Dated, "the links panel's load failure"),
        new("Panels/RightPanePanelsViewModel.cs", "RightPanePanelsViewModel", "LoadOutline", 1, CB15c, Dated, "the outline panel's load failure"),
        new("Panels/RightPanePanelsViewModel.cs", "RightPanePanelsViewModel", "LoadTasks", 1, CB15c, Dated, "the tasks panel's load failure"),
        new("Panels/SyncDiagnosticsViewModel.cs", "SyncDiagnosticsViewModel", "LoadBody", 1, CB15c, Dated, "the sync diagnostics panel's load failure"),
        new("Panels/TaskIndexRepairCoordinator.cs", "TaskIndexRepairCoordinator", "Retry", 1, CB15c, Dated, "the task index repair's last error"),
        new("Panels/TaskIndexRepairCoordinator.cs", "TaskIndexRepairCoordinator", "TryRepairNow", 1, CB15c, Dated, "the task index repair's error"),
        new("Panels/TasksReviewViewModel.cs", "TasksReviewViewModel", "DirectToggleAtItsTurn", 2, CB15c, Dated, "TaskToggleFailed, the task review's toggle"),
        new("Panels/TasksReviewViewModel.cs", "TasksReviewViewModel", "LoadFirstPage", 1, CB15c, Dated, "the task review's load failure"),
        new("Panels/TasksReviewViewModel.cs", "TasksReviewViewModel", "LoadMore", 1, CB15c, Dated, "the task review's next page failure"),
        new("Reading/MathMlUiaProperty.cs", "MathMlUiaProperty", "Initialize", 1, CB15c, Dated, "the MathML property's inactive reason, logged"),
        new("Reading/ReadingContentViewModel.cs", "ReadingContentViewModel", "ProjectBaseEmbed", 1, CB15c, Dated, "an embedded base's execute error in reading view"),
        new("VaultLifecycleViewModel.cs", "VaultLifecycleViewModel", "OpenVaultAsync", 2, CB15c, Dated, "Could not open vault: and Unexpected vault error:"),
        new("VaultLifecycleViewModel.cs", "VaultLifecycleViewModel", "RecordRecentVault", 1, CB15c, Dated, "Could not save recent vaults:"),
        new("VaultLifecycleViewModel.cs", "VaultLifecycleViewModel", "RemoveRecentVault", 1, CB15c, Dated, "Could not update recent vaults:"),
        new("WorkspaceTabViewModel.Properties.cs", "WorkspaceTabViewModel", "WriteProperty", 1, CB15c, Dated, "PropertyEditFailed, a property write's other fault"),
        new("WorkspaceViewModel.Citations.cs", "WorkspaceViewModel", "ReadAndSeedSources", 1, CB15c, Dated, "a bibliography source's notice"),
        new("WorkspaceViewModel.History.cs", "WorkspaceViewModel", "CommitRestoreAsDeleted", 2, CB15c, Dated, "the history panel's Restore As failure, a deleted file"),
        new("WorkspaceViewModel.History.cs", "WorkspaceViewModel", "CommitRestoreAsVersion", 2, CB15c, Dated, "the history panel's Restore As failure, a version"),
        new("WorkspaceViewModel.History.cs", "WorkspaceViewModel", "RecoverAdmitted", 3, CB15c, Dated, "the history panel's recover failure"),
        new("WorkspaceViewModel.History.cs", "WorkspaceViewModel", "RestoreAdmitted", 2, CB15c, Dated, "the history panel's restore failure"),
        new("WorkspaceViewModel.Properties.cs", "WorkspaceViewModel", "AddWriteCompletion", 1, CB15c, Dated, "PropertyEditFailed, adding a property"),
        new("WorkspaceViewModel.Properties.cs", "WorkspaceViewModel", "BulkRenameApplyAtItsTurn", 1, CB15c, Dated, "the bulk rename sheet's apply error"),
        new("WorkspaceViewModel.Properties.cs", "WorkspaceViewModel", "PropertyWriteCompletion", 1, CB15c, Dated, "PropertyEditFailed, a property write"),
        new("WorkspaceViewModel.Templates.cs", "WorkspaceViewModel", "CreateFromTemplate", 1, CB15c, Dated, "the template prompt's create refusal, shown inline"),
        new("WorkspaceViewModel.cs", "WorkspaceTabViewModel", "PerformTaskToggle", 2, CB15c, Dated, "TaskToggleFailed, a toggle's other fault"),
        new("WorkspaceViewModel.cs", "WorkspaceTabViewModel", "StartAdmittedTaskToggle", 1, CB15c, Dated, "TaskToggleFailed, a faulted toggle"),
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
        Row canvas = new("Canvas/Planted.cs", "Planted", "M", 2, CB15b, Dated, "a planted read");
        Row panel = new("Panels/Planted.cs", "Planted", "N", 1, CB15c, Dated, "a planted read");
        Assert.Empty(TableProblems([canvas, panel], rowCeiling: 2, readCeiling: 3));

        Assert.Contains(
            TableProblems([canvas, panel, panel with { Member = "O" }], rowCeiling: 2, readCeiling: 4),
            problem => problem.StartsWith("the table holds 3 rows", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel with { Reads = 2 }], rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("the table's rows hold 4 reads", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas], rowCeiling: 2, readCeiling: 2),
            problem => problem.Contains("lower RowCeiling to 1", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel], rowCeiling: 2, readCeiling: 4),
            problem => problem.Contains("lower ReadCeiling to 3", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel with { Dated = "2026-11-19" }], rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("Panels/Planted.cs (Planted.N): dated 2026-11-19", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas with { Owner = CB15c }, panel], rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("Canvas/Planted.cs (Planted.M): owned by CB-15c", StringComparison.Ordinal));
        Assert.Contains(
            TableProblems([canvas, panel with { Owner = CB15b }], rowCeiling: 2, readCeiling: 3),
            problem => problem.StartsWith("Panels/Planted.cs (Planted.N): owned by CB-15b", StringComparison.Ordinal));
    }

    /// <summary>The table's ceilings: the rows and reads it held on
    /// 2026-11-18. A PR that routes reads lowers them to what is left.</summary>
    internal const int RowCeiling = 61;

    internal const int ReadCeiling = 73;

    /// <summary>What the table itself may not do: grow past its ceilings,
    /// fall below them without lowering them, hold a row dated another day,
    /// or give a row to another chunk than its folder's.</summary>
    internal static IReadOnlyList<string> TableProblems(
        IReadOnlyList<Row> rows, int rowCeiling = RowCeiling, int readCeiling = ReadCeiling)
    {
        List<string> problems = [];
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
            Assert.Equal(("Planted", "M"), (read.Type, read.Member));
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
            [("Planted.cs", "FailureDetail", "Of", 5), ("Planted.cs", "FailureDetail", "Of", 13)];
        Assert.Equal(named, reads.Select(read => (read.File, read.Type, read.Member, read.Line)).OrderBy(read => read.Line));
    }

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
    /// Files sidebar or the tag edit.</summary>
    [Fact]
    public void TheTableIsHeldToTheReads()
    {
        Read[] reads = PlantedReads(Holders);
        Row[] exact = [.. HolderKeys.Select(key => new Row("Planted.cs", key.Type, key.Member, key.Reads, CB15c, Dated, "a planted read"))];
        Assert.Empty(Problems(reads, exact));

        // A member with two reads under a one-read row.
        IReadOnlyList<string> under = Problems(reads, [.. exact.Select(row => row.Member == "Twice" && row.Type == "Holders" ? row with { Reads = 1 } : row)]);
        Assert.Contains(under, problem => problem.Contains("Planted.cs (Holders.Twice) holds 2 reads and its row names 1", StringComparison.Ordinal));
        Assert.Contains(under, problem => problem.Contains("new(\"Planted.cs\", \"Holders\", \"Twice\", 2,", StringComparison.Ordinal));

        // A read with no row.
        IReadOnlyList<string> unrowed = Problems(reads, [.. exact.Where(row => row.Type != "Other")]);
        Assert.Contains(unrowed, problem => problem.Contains("(Other.Twice)", StringComparison.Ordinal));
        Assert.Contains(unrowed, problem => problem.Contains("new(\"Planted.cs\", \"Other\", \"Twice\", 1,", StringComparison.Ordinal));

        // A row with fewer reads in its member than it names, and a row with none.
        IReadOnlyList<string> stale = Problems(
            reads,
            [
                .. exact.Select(row => row.Member == "ByLocal" ? row with { Reads = 2 } : row),
                new("Planted.cs", "Holders", "Gone", 1, CB15c, Dated, "a read that was routed"),
            ]);
        Assert.Contains(stale, problem => problem.StartsWith("Planted.cs (Holders.ByLocal): the row names 2 reads", StringComparison.Ordinal));
        Assert.Contains(stale, problem => problem.StartsWith("Planted.cs (Holders.Gone): a row that matches no read", StringComparison.Ordinal));

        // Two rows for one member.
        Assert.Contains(
            Problems(reads, [.. exact, exact.First(row => row.Member == "Spoken")]),
            problem => problem.StartsWith("Planted.cs (Holders.Spoken): more than one row names it", StringComparison.Ordinal));

        // The sidebar's files and the tag edit never stand as rows.
        IReadOnlyList<string> sidebar = Problems(
            [],
            [
                new("FilesSidebarViewModel.TreeOperations.cs", "FilesSidebarViewModel", "Refresh", 1, CB15c, Dated, "Could not load files"),
                new("WorkspaceViewModel.Properties.cs", "WorkspaceViewModel", "EditTagAtItsTurn", 1, CB15c, Dated, "Tag edit failed"),
            ]);
        Assert.Contains(sidebar, problem => problem.StartsWith("FilesSidebarViewModel.TreeOperations.cs (FilesSidebarViewModel.Refresh): CB-15a routed", StringComparison.Ordinal));
        Assert.Contains(sidebar, problem => problem.StartsWith("WorkspaceViewModel.Properties.cs (WorkspaceViewModel.EditTagAtItsTurn): CB-15a routed", StringComparison.Ordinal));

        // A row says who ends it, when, and what it reaches.
        Assert.Contains(
            Problems(reads, [.. exact.Select(row => row.Member == "Initial" ? row with { Reaches = " " } : row)]),
            problem => problem.StartsWith("Planted.cs (Holders.Initial): a row with no owner", StringComparison.Ordinal));
    }

    /// <summary>The members the planted holders' reads count to, and how many
    /// each holds.</summary>
    private static readonly (string Type, string Member, int Reads)[] HolderKeys =
    [
        ("Derived", "Derived", 1),
        ("Holders", "ByLambda", 1),
        ("Holders", "ByLocal", 1),
        ("Holders", "Detail", 2),
        ("Holders", "Finalize", 1),
        ("Holders", "Holders", 2),
        ("Holders", "Initial", 1),
        ("Holders", "Said", 2),
        ("Holders", "Seeded", 1),
        ("Holders", "Spoken", 1),
        ("Holders", "Twice", 2),
        ("Holders", "_render", 1),
        ("Holders", "op_Addition", 1),
        ("Holders", "op_Implicit", 1),
        ("Holders", "this[]", 1),
        ("Nested", "Twice", 1),
        ("Other", "Twice", 1),
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

    /// <summary>The declaration that holds a read's operation tree, named as
    /// SaveChainAdmissionCensus names one (HoldsAnOperationTree and MemberOf):
    /// a method by its name; a constructor, static or not, by its type's
    /// name; an operator or conversion by its metadata name (op_Addition,
    /// op_Implicit); a finalizer as Finalize; every accessor (get, set, init,
    /// add, remove), an expression-bodied property or indexer and a property
    /// initializer by their property, event or indexer (this[]); a field
    /// initializer by its field; and a primary constructor's base arguments
    /// by the type's name. A lambda or local function counts to the member
    /// around it. The type is the one that declares the member. One
    /// departure: a method is named by its identifier, so an explicit
    /// interface implementation is TryCreateNote, not its qualified metadata
    /// name.</summary>
    internal static (string Type, string Member) Holder(SemanticModel model, SyntaxNode node)
    {
        for (SyntaxNode? current = node.Parent; current is not null; current = current.Parent)
        {
            string? member = current switch
            {
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                DestructorDeclarationSyntax => "Finalize",
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax =>
                    model.GetDeclaredSymbol(current)?.Name ?? "(an operator)",
                // An accessor, an arrow body or an initializer: the property.
                BasePropertyDeclarationSyntax property => PropertyName(property),
                VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax } field => field.Identifier.ValueText,
                PrimaryConstructorBaseTypeSyntax => TypeName(current),
                BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
                _ => null,
            };
            if (member is not null)
            {
                return (TypeName(current), member);
            }
        }
        return ("(no type)", "(no member)");
    }

    private static string PropertyName(BasePropertyDeclarationSyntax property) => property switch
    {
        PropertyDeclarationSyntax named => named.Identifier.ValueText,
        EventDeclarationSyntax named => named.Identifier.ValueText,
        IndexerDeclarationSyntax => "this[]",
        _ => "(a property)",
    };

    private static string TypeName(SyntaxNode node) =>
        node.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>()?.Identifier.ValueText ?? "(no type)";

    /// <summary>What the rows do not answer: a read in a member no row names,
    /// or in a member holding more reads than its row (each printed with a
    /// ready row); a row naming more reads than its member holds, or matching
    /// none (stale); a row for the Files sidebar or the tag edit; a row with
    /// no owner, date or sentence; two rows for one member.</summary>
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
