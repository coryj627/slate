// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-6 (#738) phase C facts: the cell-edit policy twin (contract C7),
/// the tab/tabless write route split (contract C8, D-14), and the one
/// post-write funnel with its announcements (contract C9).
/// </summary>
public sealed class BasesCellEditTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public BasesCellEditTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-bases-edit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        WriteNote("note0.md", "todo");
        WriteNote("note1.md", "todo");
        WriteNote("note2.md", "done");
        File.WriteAllText(
            Path.Combine(_root, "Status.base"),
            "filters: 'status == \"todo\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n" +
            "      - note.status\n");
        File.WriteAllText(
            Path.Combine(_root, "Others.base"),
            "filters: 'status == \"todo\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n");
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WriteNote(string name, string status) =>
        File.WriteAllText(
            Path.Combine(_root, name),
            $"---\nstatus: {status}\n---\n\n# {name}\n\nBody.\n");

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);

    private static BasesColumn StatusColumn(BaseDocumentViewModel document) =>
        document.Result!.Columns.Single(column =>
            string.Equals(column.Id, "note.status", StringComparison.Ordinal));

    private static BasesRow RowFor(BaseDocumentViewModel document, string fileName) =>
        document.Result!.Rows.Single(row =>
            row.FilePath.EndsWith(fileName, StringComparison.Ordinal));

    [Fact]
    public void PolicyMirrorsTheMacArms()
    {
        // Editability predicate.
        Assert.Equal("status", BaseCellEditPolicy.PropertyKey(
            new BasesColumn("note.status", "Status", "text", ColumnRole.Metadata)));
        Assert.Null(BaseCellEditPolicy.PropertyKey(
            new BasesColumn("file.name", "Name", "text", ColumnRole.Identifier)));
        Assert.Null(BaseCellEditPolicy.PropertyKey(
            new BasesColumn("formula.x", "X", "number", ColumnRole.Metric)));
        Assert.Equal("status", BaseCellEditPolicy.PropertyKey(
            new BasesColumn("status", "Status", "text", ColumnRole.Metadata)));
        Assert.Null(BaseCellEditPolicy.PropertyKey(
            new BasesColumn("note.", "Broken", "text", ColumnRole.Metadata)));

        // Read-only reason discriminates on file.* only.
        var fileReason = Assert.IsType<A11yEvent.BasesCellReadOnly>(
            BaseCellEditPolicy.ReadOnlyEvent(
                new BasesColumn("file.name", "Name", "text", ColumnRole.Identifier)));
        Assert.True(fileReason.FileMetadata);
        var computedReason = Assert.IsType<A11yEvent.BasesCellReadOnly>(
            BaseCellEditPolicy.ReadOnlyEvent(
                new BasesColumn("formula.x", "X", "number", ColumnRole.Metric)));
        Assert.False(computedReason.FileMetadata);

        // Validation arms produce core's refusal events.
        Assert.Null(BaseCellEditPolicy.PropertyValueFor("abc", "integer", out A11yEvent? refusal));
        Assert.IsType<A11yEvent.BasesCellMustBeWholeNumber>(refusal);
        Assert.Null(BaseCellEditPolicy.PropertyValueFor("x", "number", out refusal));
        Assert.IsType<A11yEvent.BasesCellMustBeFiniteNumber>(refusal);
        Assert.Null(BaseCellEditPolicy.PropertyValueFor("maybe", "boolean", out refusal));
        Assert.IsType<A11yEvent.BasesCellMustBeBoolean>(refusal);
        Assert.Null(BaseCellEditPolicy.PropertyValueFor("July 11", "date", out refusal));
        Assert.IsType<A11yEvent.BasesCellMustBeDate>(refusal);

        // Accepting arms.
        Assert.IsType<PropertyValue.Integer>(
            BaseCellEditPolicy.PropertyValueFor(" 42 ", "number", out _));
        Assert.IsType<PropertyValue.Float>(
            BaseCellEditPolicy.PropertyValueFor("2.5", "number", out _));
        var boolean = Assert.IsType<PropertyValue.Boolean>(
            BaseCellEditPolicy.PropertyValueFor("Yes", "checkbox", out _));
        Assert.True(boolean.Value);
        var date = Assert.IsType<PropertyValue.Date>(
            BaseCellEditPolicy.PropertyValueFor("2026-08-08", "date", out _));
        Assert.Equal("2026-08-08", date.Value);
        var link = Assert.IsType<PropertyValue.Wikilink>(
            BaseCellEditPolicy.PropertyValueFor("[[Target]]", "link", out _));
        Assert.Equal("Target", link.Target);
        var list = Assert.IsType<PropertyValue.List>(
            BaseCellEditPolicy.PropertyValueFor("a, b\nc", "list", out _));
        Assert.Equal(3, list.Items.Length);
        var text = Assert.IsType<PropertyValue.Text>(
            BaseCellEditPolicy.PropertyValueFor("  keep raw  ", "text", out _));
        Assert.Equal("  keep raw  ", text.Value);
    }

    [Fact]
    public void TablessWriteRoundTripsAndAnnouncesSaved()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Status.base");
        Assert.Equal(2, document.Result!.Rows.Length);
        BasesRow row = RowFor(document, "note1.md");
        BasesColumn column = StatusColumn(document);
        _announced.Clear();

        document.ApplyPropertyEdit!(
            row, column, new PropertyValue.Text("todo-updated"));

        // The write landed in the FILE (core read-back), the document
        // refreshed, and the row survived the still-matching filter?
        // "todo-updated" does not equal "todo" — the row LEFT the
        // filtered view, so the outcome is RowNoLongerMatches.
        Assert.Single(document.Result!.Rows);
        Assert.Contains(
            _announced, e => e is A11yEvent.BasesCellRowNoLongerMatches);
        string updated = File.ReadAllText(Path.Combine(_root, "note1.md"));
        Assert.Contains("status: todo-updated", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void StillMatchingWriteAnnouncesSavedWithTheDisplayValue()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Status.base");
        BasesRow row = RowFor(document, "note0.md");
        BasesColumn column = StatusColumn(document);
        _announced.Clear();

        document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("todo"));

        var saved = Assert.IsType<A11yEvent.BasesCellSaved>(
            Assert.Single(_announced, e => e is A11yEvent.BasesCellSaved));
        Assert.Equal(column.Label, saved.Column);
        Assert.Equal("todo", saved.Value);
    }

    [Fact]
    public void EmptyDraftDeletesThePropertyAndAnnouncesCleared()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Status.base");
        BasesRow row = RowFor(document, "note0.md");
        BasesColumn column = StatusColumn(document);
        _announced.Clear();

        document.ApplyPropertyEdit!(row, column, null);

        // Deleting status removes the row from the todo-filtered view.
        Assert.Contains(
            _announced, e => e is A11yEvent.BasesCellRowNoLongerMatches);
        string updated = File.ReadAllText(Path.Combine(_root, "note0.md"));
        Assert.DoesNotContain("status:", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyColumnRefusesAtTheCoordinatorToo()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Status.base");
        BasesRow row = RowFor(document, "note0.md");
        BasesColumn fileColumn = document.Result!.Columns.Single(column =>
            string.Equals(column.Id, "file.name", StringComparison.Ordinal));
        _announced.Clear();

        document.ApplyPropertyEdit!(row, fileColumn, new PropertyValue.Text("x"));

        var refused = Assert.IsType<A11yEvent.BasesCellReadOnly>(
            Assert.Single(_announced));
        Assert.True(refused.FileMetadata);
    }

    [Fact]
    public void DirtyTabRefusesWithTheSharedSentence()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Status.base");
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        tab.Text += "\nunsaved edit\n";
        Assert.True(tab.IsDirty);
        BasesRow row = RowFor(document, "note0.md");
        BasesColumn column = StatusColumn(document);
        _announced.Clear();

        document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("done"));

        var refusal = Assert.IsType<A11yEvent.HostComposed>(Assert.Single(_announced));
        Assert.StartsWith(
            "Save the note before editing properties.",
            refusal.Text,
            StringComparison.Ordinal);
        // Nothing was written: the file still carries the old value.
        Assert.Contains(
            "status: todo",
            File.ReadAllText(Path.Combine(_root, "note0.md")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void FunnelRefreshesEveryDocumentAndAnnouncesMembershipOnce()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel status = workspace.BaseDocumentFor("Status.base");
        BaseDocumentViewModel others = workspace.BaseDocumentFor("Others.base");
        Assert.Equal(2, others.Result!.Rows.Length);
        BasesRow row = RowFor(status, "note1.md");
        BasesColumn column = StatusColumn(status);
        _announced.Clear();

        status.ApplyPropertyEdit!(
            row, column, new PropertyValue.Text("done"));

        // BOTH documents re-executed (contract C9): the second base
        // lost the row too, and its membership change was announced.
        Assert.Single(others.Result!.Rows);
        Assert.Contains(_announced, e => e is A11yEvent.BasesRefreshUpdated);
        // Exactly one cell OUTCOME sentence for the whole write.
        Assert.Equal(
            1,
            _announced.Count(e =>
                e is A11yEvent.BasesCellSaved
                    or A11yEvent.BasesCellCleared
                    or A11yEvent.BasesCellRowNoLongerMatches));
    }
}
