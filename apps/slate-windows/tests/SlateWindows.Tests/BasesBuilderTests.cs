// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-6 (#738) phase E5 facts: the query builder (contracts C11, D-18)
/// — core-produced documents, core-validated expressions, guarded
/// preview with balanced handles, the preserved-filters refusal, and
/// the save flows.
/// </summary>
public sealed class BasesBuilderTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public BasesBuilderTests()
    {
        _fixture = FixtureVault.Create(3, "bases-builder");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Filtered.base"),
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    filters: 'file.ext == \"md\"'\n" +
            "    order:\n" +
            "      - file.name\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private BaseQueryBuilderViewModel NewBuilder() =>
        BaseQueryBuilderViewModel.NewQuery(
            _session, _announced.Add, synchronousForTests: true);

    /// <summary>The command route's shape: fetch the view's edit JSON
    /// through the document seam (inline in synchronous mode), then
    /// construct the edit-context builder.</summary>
    private BaseQueryBuilderViewModel ForViewBuilder(BaseDocumentViewModel document)
    {
        string? json = null;
        string? failure = null;
        document.ViewEditQueryJson((fetched, error) => (json, failure) = (fetched, error));
        Assert.True(json is not null, $"ViewEditQueryJson failed: {failure}");
        return BaseQueryBuilderViewModel.ForView(
            _session, json!, document.Path, document.ActiveViewIndex,
            _announced.Add, synchronousForTests: true);
    }

    [Fact]
    public void NewQueryPreviewsFromACoreSeed()
    {
        BaseQueryBuilderViewModel builder = NewBuilder();
        builder.RunPreview();

        Assert.Equal(BuilderPreviewState.Ready, builder.PreviewState);
        Assert.NotNull(builder.PreviewResult);
        // Loading then Ready announced through the canonical preview
        // events (the mac sequence).
        Assert.Contains(_announced, e => e is A11yEvent.BaseQueryPreviewLoading);
        Assert.Contains(_announced, e => e is A11yEvent.BaseQueryPreviewReady);
        builder.Shutdown();
    }

    [Fact]
    public void ConditionsValidateThroughCoreAndFilterThePreview()
    {
        BaseQueryBuilderViewModel builder = NewBuilder();
        builder.RunPreview();
        ulong unfiltered = builder.PreviewResult!.ShownCount;

        BuilderConditionRow row = builder.AddCondition();
        row.Expression = "file.ext == \"md\"";
        Assert.True(builder.ValidateRow(row));
        Assert.Null(row.ValidationMessage);
        builder.RunPreview();
        Assert.Equal(BuilderPreviewState.Ready, builder.PreviewState);
        Assert.True(builder.PreviewResult!.ShownCount < unfiltered);

        // An invalid expression carries core's message + span and
        // contributes no node.
        row.Expression = "file.ext ==";
        Assert.False(builder.ValidateRow(row));
        Assert.NotNull(row.ValidationMessage);
        builder.Shutdown();
    }

    [Fact]
    public void PreservedFiltersRefuseToCombineWithNewConditions()
    {
        using WorkspaceViewModel workspace = new(
            _session, _fixture.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Filtered.base");
        BaseQueryBuilderViewModel builder = ForViewBuilder(document);

        // The existing filters entered as ONE preserved row.
        BuilderConditionRow preserved = Assert.Single(builder.ConditionRows);
        Assert.True(preserved.IsPreserved);

        BuilderConditionRow added = builder.AddCondition();
        added.Expression = "file.hasTag(\"test\")";
        Assert.True(builder.ValidateRow(added));

        Assert.False(builder.SyncDocument());
        Assert.Contains(
            "Existing filters cannot be combined",
            builder.SaveError,
            StringComparison.Ordinal);

        // Deleting the preserved row unlocks the new conditions.
        builder.RemoveCondition(preserved);
        Assert.True(builder.SyncDocument());
        Assert.Equal(string.Empty, builder.SaveError);
        builder.Shutdown();
    }

    [Fact]
    public void SaveToViewWritesTheFiltersYamlAndReExecutes()
    {
        using WorkspaceViewModel workspace = new(
            _session, _fixture.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Filtered.base");
        BaseQueryBuilderViewModel builder = ForViewBuilder(document);
        builder.RemoveCondition(builder.ConditionRows.Single());
        BuilderConditionRow row = builder.AddCondition();
        row.Expression = "file.hasTag(\"test\")";
        _announced.Clear();

        bool? savedToView = null;
        builder.SaveToView(document, saved => savedToView = saved);
        string failureDetail = string.Join(
            "; ",
            _announced.OfType<A11yEvent.BasesViewSaveFailed>()
                .Select(failed => failed.Detail));
        Assert.True(savedToView == true, $"SaveToView failed: {failureDetail}");

        Assert.Contains(_announced, e => e is A11yEvent.BasesBuilderSaved);
        string content = File.ReadAllText(
            Path.Combine(_fixture.Root, "Filtered.base"));
        Assert.Contains("file.hasTag", content, StringComparison.Ordinal);
        // The document re-executed against the rewritten view.
        Assert.Equal(BaseLoadState.Ready, document.State);
        builder.Shutdown();
    }

    [Fact]
    public void SaveAsBaseIsExclusiveCreateWithMacWording()
    {
        BaseQueryBuilderViewModel builder = NewBuilder();

        Assert.False(builder.SaveAsBase("   "));
        Assert.Equal("Enter a .base path before saving.", builder.SaveError);

        Assert.True(builder.SaveAsBase("NewQuery"));
        Assert.True(File.Exists(
            Path.Combine(_fixture.Root, "NewQuery.base")));

        Assert.False(builder.SaveAsBase("NewQuery.base"));
        Assert.StartsWith(
            "A file already exists at",
            builder.SaveError,
            StringComparison.Ordinal);
        builder.Shutdown();
    }

    [Fact]
    public void SavedQueryFlowsAnnounceCanonically()
    {
        BaseQueryBuilderViewModel builder = NewBuilder();
        _announced.Clear();

        Assert.False(builder.SaveAsSavedQuery("  ", null));
        Assert.Contains(
            _announced, e => e is A11yEvent.BasesSavedQueryNameNeeded);
        _announced.Clear();

        Assert.True(builder.SaveAsSavedQuery("From builder", null));
        var created = Assert.IsType<A11yEvent.BasesSavedQueryCreated>(
            Assert.Single(_announced));
        Assert.Equal("From builder", created.Name);

        // Round-trip: edit the created query in the builder and update.
        SavedQuerySummary summary = _session.ListSavedQueries().Single();
        SavedQuery savedQuery = _session.GetSavedQuery(summary.Id);
        var editor = BaseQueryBuilderViewModel.ForSavedQuery(
            _session, savedQuery, _announced.Add, synchronousForTests: true);
        _announced.Clear();
        Assert.True(editor.UpdateSavedQuery());
        Assert.Contains(_announced, e => e is A11yEvent.BasesSavedQueryUpdated);
        builder.Shutdown();
        editor.Shutdown();
    }
}
