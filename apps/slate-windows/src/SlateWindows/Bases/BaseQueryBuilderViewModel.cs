// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json.Nodes;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Bases;

/// <summary>One builder condition row (contract C11, divergence D-18):
/// a raw expression validated through core, or a one-level group of
/// expressions, or the PRESERVED opaque node existing filters enter
/// as (keep verbatim or delete — never silently rewritten).</summary>
internal sealed class BuilderConditionRow : BindableBase
{
    private string _expression = string.Empty;
    private string? _validationMessage;

    /// <summary>Non-null for the preserved row: the view's existing
    /// filter node, core-encoded, kept byte-verbatim.</summary>
    public JsonNode? PreservedNode { get; init; }

    public bool IsPreserved => PreservedNode is not null;

    /// <summary>Non-null for a GROUP row (builder.addGroup): the
    /// member expressions, OR-combined (the one-level mac group).</summary>
    public System.Collections.ObjectModel.ObservableCollection<BuilderConditionRow>?
        GroupMembers
    { get; init; }

    public bool IsGroup => GroupMembers is not null;

    public string Expression
    {
        get => _expression;
        set => SetField(ref _expression, value);
    }

    /// <summary>Core's message + span, or null when valid/empty.</summary>
    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetField(ref _validationMessage, value);
    }

    /// <summary>The core-encoded Expr JSON for the CURRENT text; null
    /// until validated. The row contributes no filter node while
    /// invalid (the mac rule).</summary>
    public string? ExprJson { get; set; }

    public string Label =>
        IsPreserved ? "Existing filters (preserved)"
        : IsGroup ? "Any of:"
        : "Condition";
}

internal sealed class BuilderSortRow : BindableBase
{
    private string _property = string.Empty;
    private bool _ascending = true;

    public string Property
    {
        get => _property;
        set => SetField(ref _property, value);
    }

    public bool Ascending
    {
        get => _ascending;
        set => SetField(ref _ascending, value);
    }
}

internal sealed class BuilderFormulaRow : BindableBase
{
    private string _name = string.Empty;
    private string _expression = string.Empty;
    private string? _validationMessage;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Expression
    {
        get => _expression;
        set => SetField(ref _expression, value);
    }

    public string? ValidationMessage
    {
        get => _validationMessage;
        set => SetField(ref _validationMessage, value);
    }
}

internal enum BuilderPreviewState
{
    Idle,
    Loading,
    Ready,
    Failed,
}

/// <summary>What the builder was opened FOR — decides which save
/// affordances exist (the mac EditingBaseView/EditingSavedQuery).</summary>
internal sealed record BuilderEditContext(
    string? ViewPath,
    int ViewIndex,
    string? SavedQueryId,
    string? SavedQueryName);

/// <summary>
/// W4-6 (#738) phase E5: the query builder (contracts C11, D-18) — an
/// in-window overlay's model. The query document is ALWAYS core-
/// produced JSON (the new-query seed comes from core via OpenDql →
/// BaseViewQueryJson; edit contexts start from the view's or saved
/// query's own JSON); the builder mutates filters/sort/formulas at
/// the node level with core-encoded expressions and previews through
/// OpenQuery → BaseExecute → ALWAYS CloseBase.
/// </summary>
internal sealed class BaseQueryBuilderViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly JsonObject _document;
    private int _previewGeneration;
    private BuilderPreviewState _previewState = BuilderPreviewState.Idle;
    private string? _previewMessage;
    private BasesResultSet? _previewResult;
    private string _combinator = "and";
    private string _saveError = string.Empty;

    private BaseQueryBuilderViewModel(
        VaultSession session,
        JsonObject document,
        BuilderEditContext context,
        Action<A11yEvent> announce,
        bool synchronousForTests)
        : base(synchronousForTests)
    {
        _session = session;
        _document = document;
        Context = context;
        _announce = announce;
        if (_document["filters"] is { } existing)
        {
            ConditionRows.Add(new BuilderConditionRow
            {
                PreservedNode = existing.DeepClone(),
            });
        }
        if (_document["sort"] is JsonArray sortArray)
        {
            // Existing sort keys enter read-only-ish: expr JSON has no
            // text rendering, so they surface by node count only and
            // are REPLACED wholesale when the user edits sort rows —
            // the same preserved-or-replaced rule as filters.
            PreservedSortCount = sortArray.Count;
        }
    }

    /// <summary>New query: the seed document is CORE's serialization
    /// of a minimal DQL query (never host-synthesized schema).</summary>
    public static BaseQueryBuilderViewModel NewQuery(
        VaultSession session,
        Action<A11yEvent> announce,
        bool synchronousForTests = false)
    {
        ulong handle = session.OpenDql("TABLE file.name", thisPath: null);
        string seed;
        try
        {
            seed = session.BaseViewQueryJson(handle, 0);
        }
        finally
        {
            session.CloseBase(handle);
        }
        // The seed keeps core's own serialization untouched — every
        // field is schema-required, and the file.name column default
        // is an honest starting projection.
        var document = (JsonObject)JsonNode.Parse(seed)!;
        return new BaseQueryBuilderViewModel(
            session,
            document,
            new BuilderEditContext(null, 0, null, null),
            announce,
            synchronousForTests);
    }

    /// <summary>Edit a view's filters (slate.bases.editViewFilters):
    /// starts from the view's own EDIT query JSON, fetched by the
    /// caller off the dispatcher (the document's ViewEditQueryJson
    /// seam — INV-6).</summary>
    public static BaseQueryBuilderViewModel ForView(
        VaultSession session,
        string editQueryJson,
        string viewPath,
        int viewIndex,
        Action<A11yEvent> announce,
        bool synchronousForTests = false) =>
        new(
            session,
            (JsonObject)JsonNode.Parse(editQueryJson)!,
            new BuilderEditContext(viewPath, viewIndex, null, null),
            announce,
            synchronousForTests);

    public static BaseQueryBuilderViewModel ForSavedQuery(
        VaultSession session,
        SavedQuery savedQuery,
        Action<A11yEvent> announce,
        bool synchronousForTests = false) =>
        new(
            session,
            (JsonObject)JsonNode.Parse(savedQuery.QueryJson)!,
            new BuilderEditContext(null, 0, savedQuery.Id, savedQuery.Name),
            announce,
            synchronousForTests);

    public BuilderEditContext Context { get; }

    public string Title =>
        Context.SavedQueryId is not null ? $"Edit saved query: {Context.SavedQueryName}"
        : Context.ViewPath is not null ? "Edit view filters"
        : "New Bases query";

    public System.Collections.ObjectModel.ObservableCollection<BuilderConditionRow>
        ConditionRows
    { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<BuilderSortRow>
        SortRows
    { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<BuilderFormulaRow>
        FormulaRows
    { get; } = [];

    public int PreservedSortCount { get; }

    /// <summary>"and" | "or" — the top-level combinator.</summary>
    public string Combinator
    {
        get => _combinator;
        set => SetField(ref _combinator, value);
    }

    public BuilderPreviewState PreviewState
    {
        get => _previewState;
        private set => SetField(ref _previewState, value);
    }

    public string? PreviewMessage
    {
        get => _previewMessage;
        private set => SetField(ref _previewMessage, value);
    }

    public BasesResultSet? PreviewResult
    {
        get => _previewResult;
        private set => SetField(ref _previewResult, value);
    }

    public event EventHandler? PreviewPublished;

    /// <summary>Inline save-error line (the mac
    /// baseQueryBuilderSaveError), cleared on any input change.</summary>
    public string SaveError
    {
        get => _saveError;
        set => SetField(ref _saveError, value);
    }

    // --- Conditions (builder.addCondition / addGroup /
    // editCondition [in-place text edit] / removeCondition) ---

    public BuilderConditionRow AddCondition()
    {
        var row = new BuilderConditionRow();
        ConditionRows.Add(row);
        return row;
    }

    public BuilderConditionRow AddGroup()
    {
        var row = new BuilderConditionRow { GroupMembers = [] };
        row.GroupMembers!.Add(new BuilderConditionRow());
        ConditionRows.Add(row);
        return row;
    }

    public void RemoveCondition(BuilderConditionRow row) =>
        _ = ConditionRows.Remove(row);

    /// <summary>Validate through core; empty input shows no error and
    /// contributes no node (the mac rule).</summary>
    public bool ValidateRow(BuilderConditionRow row)
    {
        string trimmed = row.Expression.Trim();
        if (trimmed.Length == 0)
        {
            row.ValidationMessage = null;
            row.ExprJson = null;
            return false;
        }
        BaseExpressionValidation validation =
            _session.ValidateBaseExpression(trimmed);
        if (!validation.Valid || validation.ExprJson is null)
        {
            string message = validation.Message ?? "Expression invalid";
            row.ValidationMessage = validation.SpanEnd > validation.SpanStart
                ? $"{message} at characters {validation.SpanStart}-{validation.SpanEnd}"
                : message;
            row.ExprJson = null;
            return false;
        }
        row.ValidationMessage = null;
        row.ExprJson = validation.ExprJson;
        return true;
    }

    public bool ValidateFormula(BuilderFormulaRow row)
    {
        string trimmed = row.Expression.Trim();
        if (trimmed.Length == 0 || row.Name.Trim().Length == 0)
        {
            row.ValidationMessage =
                trimmed.Length == 0 ? null : "Formula needs a name.";
            return false;
        }
        BaseExpressionValidation validation =
            _session.ValidateBaseExpression(trimmed);
        row.ValidationMessage = validation.Valid
            ? null
            : validation.Message ?? "Expression invalid";
        return validation.Valid;
    }

    // --- The query document ---

    /// <summary>Rebuild the document's filters/sort/formulas from the
    /// rows. Returns false (with the reason in SaveError) when the
    /// preserved row coexists with new conditions, and when ANY
    /// non-blank row fails validation — an invalid row must never be
    /// silently dropped from what gets saved (C11: semantics are
    /// never silently rewritten; red team round 1 found the drop
    /// could erase a view's filters while announcing success). Blank
    /// rows contribute nothing and block nothing (the mac rule).</summary>
    public bool SyncDocument()
    {
        var stmtNodes = new List<JsonNode>();
        JsonNode? preserved = null;
        foreach (BuilderConditionRow row in ConditionRows)
        {
            if (row.IsPreserved)
            {
                preserved = row.PreservedNode!.DeepClone();
                continue;
            }
            if (row.IsGroup)
            {
                var members = new List<JsonNode>();
                foreach (BuilderConditionRow member in row.GroupMembers!)
                {
                    if (ValidateRow(member) && member.ExprJson is { } memberJson)
                    {
                        members.Add(new JsonObject
                        {
                            ["Stmt"] = JsonNode.Parse(memberJson),
                        });
                    }
                    else if (member.Expression.Trim().Length > 0)
                    {
                        SaveError = member.ValidationMessage
                            ?? "Group condition invalid.";
                        return false;
                    }
                }
                if (members.Count > 0)
                {
                    stmtNodes.Add(new JsonObject
                    {
                        ["Or"] = new JsonArray([.. members]),
                    });
                }
                continue;
            }
            if (ValidateRow(row) && row.ExprJson is { } exprJson)
            {
                stmtNodes.Add(new JsonObject
                {
                    ["Stmt"] = JsonNode.Parse(exprJson),
                });
            }
            else if (row.Expression.Trim().Length > 0)
            {
                SaveError = row.ValidationMessage ?? "Condition invalid.";
                return false;
            }
        }
        if (preserved is not null && stmtNodes.Count > 0)
        {
            SaveError =
                "Existing filters cannot be combined with new conditions. "
                + "Remove the preserved row first.";
            return false;
        }
        if (preserved is not null)
        {
            _document["filters"] = preserved;
        }
        else if (stmtNodes.Count == 0)
        {
            _document.Remove("filters");
        }
        else if (stmtNodes.Count == 1)
        {
            _document["filters"] = stmtNodes[0];
        }
        else
        {
            _document["filters"] = new JsonObject
            {
                [Combinator == "or" ? "Or" : "And"] = new JsonArray([.. stmtNodes]),
            };
        }

        if (SortRows.Count > 0)
        {
            var sortArray = new JsonArray();
            foreach (BuilderSortRow sort in SortRows)
            {
                string property = sort.Property.Trim();
                if (property.Length == 0)
                {
                    continue;
                }
                BaseExpressionValidation validation =
                    _session.ValidateBaseExpression(property);
                if (!validation.Valid || validation.ExprJson is null)
                {
                    SaveError = $"Sort property invalid: {property}";
                    return false;
                }
                sortArray.Add(new JsonObject
                {
                    ["expr"] = JsonNode.Parse(validation.ExprJson),
                    ["ascending"] = sort.Ascending,
                });
            }
            _document["sort"] = sortArray;
        }

        if (FormulaRows.Count > 0)
        {
            var formulas = new JsonArray();
            foreach (BuilderFormulaRow formula in FormulaRows)
            {
                if (!ValidateFormula(formula))
                {
                    // Fully blank rows contribute nothing; a TYPED
                    // formula that fails must refuse the save, never
                    // vanish from it (red team round 1).
                    if (formula.Expression.Trim().Length == 0
                        && formula.Name.Trim().Length == 0)
                    {
                        continue;
                    }
                    SaveError = formula.ValidationMessage ?? "Formula invalid.";
                    return false;
                }
                BaseExpressionValidation validation =
                    _session.ValidateBaseExpression(formula.Expression.Trim());
                if (validation.ExprJson is { } exprJson)
                {
                    formulas.Add(new JsonArray(
                        JsonNode.Parse($"\"{formula.Name.Trim()}\""),
                        JsonNode.Parse(exprJson)));
                }
            }
            _document["formulas"] = formulas;
        }
        SaveError = string.Empty;
        return true;
    }

    public string QueryJson()
    {
        _ = SyncDocument();
        return _document.ToJsonString();
    }

    // --- Preview (300 ms debounce is the OVERLAY's job; this is the
    // guarded pipeline) ---

    public void RunPreview()
    {
        if (IsShutDown)
        {
            return;
        }
        if (!SyncDocument())
        {
            PreviewState = BuilderPreviewState.Failed;
            PreviewMessage = SaveError;
            AnnouncePreview();
            return;
        }
        string json = _document.ToJsonString();
        int generation = Interlocked.Increment(ref _previewGeneration);
        PreviewState = BuilderPreviewState.Loading;
        AnnouncePreview();
        StartWork(() => PreviewBody(generation, json));
    }

    private void PreviewBody(int generation, string json)
    {
        BasesResultSet result;
        try
        {
            ulong handle = _session.OpenQuery(json, thisPath: Context.ViewPath);
            try
            {
                using var cancel = new CancelToken();
                result = _session.BaseExecute(
                    handle, view: 0, thisPath: Context.ViewPath,
                    quickFilter: null, cancel);
            }
            finally
            {
                // ALWAYS closed — preview handles never leak (INV-2).
                _session.CloseBase(handle);
            }
        }
        catch (VaultException failure)
        {
            Post(() =>
            {
                if (Volatile.Read(ref _previewGeneration) != generation)
                {
                    return;
                }
                PreviewState = BuilderPreviewState.Failed;
                PreviewMessage = failure.Message;
                PreviewResult = null;
                AnnouncePreview();
                PreviewPublished?.Invoke(this, EventArgs.Empty);
            });
            return;
        }
        catch (ObjectDisposedException)
        {
            // The session died under the preview (vault teardown mid
            // overlay); there is no surface left to tell.
            return;
        }
        Post(() =>
        {
            if (Volatile.Read(ref _previewGeneration) != generation)
            {
                return;
            }
            if (result.ViewError is { Length: > 0 } viewError)
            {
                // A view error publishes FAILED, not ready (the mac
                // rule).
                PreviewState = BuilderPreviewState.Failed;
                PreviewMessage = viewError;
                PreviewResult = null;
            }
            else
            {
                PreviewState = BuilderPreviewState.Ready;
                PreviewMessage = null;
                PreviewResult = result;
            }
            AnnouncePreview();
            PreviewPublished?.Invoke(this, EventArgs.Empty);
        });
    }

    private void AnnouncePreview()
    {
        A11yEvent @event = PreviewState switch
        {
            BuilderPreviewState.Idle => new A11yEvent.BaseQueryPreviewIdle(),
            BuilderPreviewState.Loading => new A11yEvent.BaseQueryPreviewLoading(),
            BuilderPreviewState.Failed => new A11yEvent.BaseQueryPreviewFailed(
                PreviewMessage ?? string.Empty),
            _ => ReadyEvent(),
        };
        _announce(@event);
    }

    private A11yEvent ReadyEvent()
    {
        string? first = PreviewResult?.Rows.FirstOrDefault()?.AudioDescription;
        return new A11yEvent.BaseQueryPreviewReady(
            PreviewResult?.AudioSummary ?? string.Empty,
            string.IsNullOrWhiteSpace(first) ? null : first);
    }

    // --- Save flows ---

    /// <summary>Save as a NEW .base file (exclusive-create in core).</summary>
    public bool SaveAsBase(string path)
    {
        string trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            SaveError = "Enter a .base path before saving.";
            return false;
        }
        if (!trimmed.EndsWith(".base", StringComparison.Ordinal))
        {
            if (trimmed.EndsWith(".", StringComparison.Ordinal)
                || trimmed.Contains('.', StringComparison.Ordinal))
            {
                SaveError = "Base paths must end in .base.";
                return false;
            }
            trimmed += ".base";
        }
        if (!SyncDocument())
        {
            return false;
        }
        try
        {
            _session.SaveQueryAsBase(_document.ToJsonString(), trimmed);
        }
        catch (VaultException failure)
        {
            SaveError = failure is VaultException.DestinationExists
                ? $"A file already exists at {trimmed}. Choose a different Base path."
                : $"Base file could not be saved: {failure.Message}";
            return false;
        }
        SaveError = string.Empty;
        return true;
    }

    public bool SaveAsSavedQuery(string name, string? description)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            _announce(new A11yEvent.BasesSavedQueryNameNeeded());
            return false;
        }
        if (!SyncDocument())
        {
            return false;
        }
        try
        {
            _ = _session.SaveQuery(
                trimmed, description, _document.ToJsonString(),
                SavedQuerySourceSyntax.Builder);
        }
        catch (VaultException failure)
        {
            _announce(new A11yEvent.BasesSavedQueryCreateFailed(failure.Message));
            return false;
        }
        _announce(new A11yEvent.BasesSavedQueryCreated(trimmed));
        return true;
    }

    public bool UpdateSavedQuery()
    {
        if (Context.SavedQueryId is not { } id || !SyncDocument())
        {
            return false;
        }
        try
        {
            _session.UpdateSavedQuery(
                id, description: null, _document.ToJsonString(),
                SavedQuerySourceSyntax.Builder);
        }
        catch (VaultException failure)
        {
            _announce(new A11yEvent.BasesSavedQueryUpdateFailed(failure.Message));
            return false;
        }
        _announce(new A11yEvent.BasesSavedQueryUpdated(
            Context.SavedQueryName ?? string.Empty));
        return true;
    }

    /// <summary>Save to the edited view (contract C11): minimal
    /// BaseEdits over the document's own handle — filters from the
    /// EXPRESSION rows as the .base and/or YAML lists, formulas via
    /// SetFormula. Refuses when the preserved row coexists with new
    /// conditions, and when ANY typed row fails validation — a typo
    /// must never save less than what the user wrote (red team round
    /// 1: the silent drop could REMOVE a view's filters while
    /// announcing success). Every outcome is announced; the
    /// continuation runs on the UI context.</summary>
    public void SaveToView(BaseDocumentViewModel target, Action<bool> completed)
    {
        if (Context.ViewPath is null)
        {
            completed(false);
            return;
        }
        var edits = new List<BaseEdit>();
        var expressions = new List<(bool IsGroup, List<string> Members)>();
        JsonNode? preserved = null;
        foreach (BuilderConditionRow row in ConditionRows)
        {
            if (row.IsPreserved)
            {
                preserved = row.PreservedNode;
                continue;
            }
            if (row.IsGroup)
            {
                var members = new List<string>();
                foreach (BuilderConditionRow member in row.GroupMembers!)
                {
                    if (ValidateRow(member))
                    {
                        members.Add(member.Expression.Trim());
                    }
                    else if (member.Expression.Trim().Length > 0)
                    {
                        RefuseSaveToView(
                            member.ValidationMessage ?? "group condition invalid",
                            completed);
                        return;
                    }
                }
                if (members.Count > 0)
                {
                    expressions.Add((true, members));
                }
                continue;
            }
            if (ValidateRow(row))
            {
                expressions.Add((false, [row.Expression.Trim()]));
            }
            else if (row.Expression.Trim().Length > 0)
            {
                RefuseSaveToView(
                    row.ValidationMessage ?? "condition invalid", completed);
                return;
            }
        }
        if (preserved is not null && expressions.Count > 0)
        {
            RefuseSaveToView(
                "existing filters cannot be combined with new conditions; "
                + "remove the preserved row first",
                completed);
            return;
        }
        if (preserved is null)
        {
            if (expressions.Count == 0)
            {
                // Reached only when every row is deliberately blank or
                // deleted — an intentional clear-all (typos refuse
                // above, so a mistake can no longer strip the view).
                edits.Add(new BaseEdit.RemoveViewKey(
                    (uint)Context.ViewIndex, "filters"));
            }
            else
            {
                edits.Add(new BaseEdit.SetViewFilters(
                    (uint)Context.ViewIndex, FiltersYaml(expressions)));
            }
        }
        foreach (BuilderFormulaRow formula in FormulaRows)
        {
            if (ValidateFormula(formula))
            {
                edits.Add(new BaseEdit.SetFormula(
                    formula.Name.Trim(), formula.Expression.Trim()));
            }
            else if (formula.Expression.Trim().Length > 0
                || formula.Name.Trim().Length > 0)
            {
                RefuseSaveToView(
                    formula.ValidationMessage ?? "formula invalid", completed);
                return;
            }
        }
        if (edits.Count == 0)
        {
            _announce(new A11yEvent.BasesBuilderSaved());
            completed(true);
            return;
        }
        target.ApplyBuilderEdits(edits, applied =>
        {
            if (applied)
            {
                SaveError = string.Empty;
                _announce(new A11yEvent.BasesBuilderSaved());
            }
            completed(applied);
        });
    }

    private void RefuseSaveToView(string detail, Action<bool> completed)
    {
        SaveError = char.ToUpperInvariant(detail[0]) + detail[1..];
        _announce(new A11yEvent.BasesViewSaveFailed(detail));
        completed(false);
    }

    /// <summary>The .base filters YAML — expression strings in an
    /// and/or list (the file's own authoring format; see the demo
    /// fixtures), quoted with the shared YAML quoting. Groups nest as
    /// one-level or: lists.</summary>
    internal string FiltersYaml(
        IReadOnlyList<(bool IsGroup, List<string> Members)> expressions)
    {
        // The fragment must START WITH THE KEY: core's
        // key_value_fragment embeds it verbatim then; otherwise it
        // would inline-prefix "filters: " ahead of a multiline block
        // and produce invalid YAML (measured: "mapping values are not
        // allowed in this context").
        var lines = new List<string>
        {
            "filters:",
            Combinator == "or" ? "  or:" : "  and:",
        };
        foreach ((bool isGroup, List<string> members) in expressions)
        {
            if (isGroup)
            {
                lines.Add("    - or:");
                foreach (string member in members)
                {
                    lines.Add("        - " + QuoteYaml(member));
                }
                continue;
            }
            lines.Add("    - " + QuoteYaml(members[0]));
        }
        return string.Join("\n", lines);
    }

    /// <summary>The ONE shared YAML quoter (red team round 1: two
    /// hand-rolled copies had diverged, one with no-op escapes).</summary>
    private static string QuoteYaml(string value) =>
        BaseDocumentViewModel.QuoteYamlString(value);

    /// <summary>A closed overlay must never publish or announce a
    /// late preview (red team round 1: the generation was not bumped,
    /// so an in-flight preview spoke into whatever surface came
    /// next).</summary>
    internal override void Shutdown()
    {
        base.Shutdown();
        Interlocked.Increment(ref _previewGeneration);
    }
}
