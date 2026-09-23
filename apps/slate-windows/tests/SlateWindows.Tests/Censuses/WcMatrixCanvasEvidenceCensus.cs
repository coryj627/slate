// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §H TH-8 (H5, IH-12, IH-41; §W-C): the canvas rows of
/// <c>w_c_matrix.md</c> are TRUE of the implemented surfaces, not merely
/// shaped. A manifest names each canvas surface with what the sources
/// say about it — the automation ids the shell sets, the peer classes
/// with the control types their bases or overrides return and the
/// patterns they handle, the members that source Names, the journeys
/// and facts that evidence it and the axe labels they scan — and every
/// row is checked cell by cell against it: exactly ten cells, the
/// manifest's tokens present in the cell that owns them, every evidence
/// name resolving in the test tree, every axe label scanned by a
/// journey, the human cells Pending until a named run, and no canvas
/// row the manifest does not know.
/// </summary>
[Trait("census", "w-c-matrix-canvas")]
public sealed class WcMatrixCanvasEvidenceCensus
{
    private sealed record Peer(string Class, string[] ControlTypes, string[] Patterns);

    private sealed record Surface(
        string Title,
        string[] Ids,
        Peer[] Peers,
        string[] NativeControlTypes,
        string[] NativePatterns,
        string[] NameSources,
        string[] Evidence,
        string[] AxeLabels);

    /// <summary>The manifest — sourced from the shell, not from the matrix.</summary>
    private static readonly Surface[] Manifest =
    [
        new(
            "Canvas outline (W6-1 PR A)",
            ["CanvasSurface", "CanvasSurfaceSwitcher", "CanvasOutlineTree", "CanvasWarningRows", "CanvasEmptyOnboarding", "CanvasDegradedBanner"],
            [
                new("CanvasOutlineTreeAutomationPeer", ["Tree"], []),
                new("CanvasOutlineRowDataPeer", ["TreeItem"], ["Invoke"]),
                new("CanvasOutlineItemAutomationPeer", ["TreeItem"], ["Invoke"]),
            ],
            ["Group", "List"],
            ["SelectionItem", "ExpandCollapse"],
            ["CanvasPhrase.CardReference", "CanvasPhrase.RowStatus"],
            ["CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean", "ThePlanServesMacsOrderPerSurfaceAndTarget"],
            ["canvas-outline", "canvas-degraded"]),
        new(
            "Canvas table (W6-1 PR B)",
            ["CanvasTableGrid"],
            [],
            ["Grid", "DataGrid"],
            ["Grid", "Table", "Selection", "Invoke"],
            ["canvas_table_rows", "speakable_name"],
            ["CanvasSurfaces_TableGridSortSelectionAndActivation_AreClean", "TheRowMenuEqualsThePlansGridProjectionAndToggleMarkIsLive", "OpenSampleExposesOutlineTableAndScene"],
            ["canvas-table"]),
        new(
            "Canvas navigator, filter and Where-am-I (W6-1 PR C)",
            ["CanvasFilterField", "CanvasFilterSummary", "CanvasClearFilter", "CanvasWhereAmIPanel", "CanvasWhereAmIReadback", "CanvasWhereAmIClose", "CanvasCommitMode", "CanvasCancelMode"],
            [],
            ["Edit", "Text", "Group", "Button"],
            ["Value", "Invoke"],
            ["Filter cards", "Filter results"],
            // W7-7 R-12 (#1255): the visual board's arrows, pinned here as the
            // row now cites them.
            ["CanvasSurfaces_NavigatorFilterAndWhereAmI_AreClean", "CanvasModes_MoveResizeAndConnectPicker_AreReachable", "TheZoomVerbsSpeakCoresZoomEventWithTheirContext",
                "CanvasSurfaces_VisualBoardPeersAndZoom_AreClean", "TheBoardsDownAndUpAreTheReadingOrderMoveThroughTheAnnouncedDoor", "EveryBoardMoveAsksThePresenterToRevealItsSeat"],
            ["canvas-navigator", "canvas-move-mode-active"]),
        new(
            "Canvas visual (W6-1 §D)",
            [],
            [
                new("CanvasRendererAutomationPeer", ["Group"], ["Value", "Selection", "ItemContainer"]),
                new("CanvasCardAutomationPeer", ["Button"], ["Invoke", "SelectionItem", "VirtualizedItem"]),
            ],
            [],
            [],
            ["speakable_name", "Zoom N percent"],
            ["CanvasSurfaces_VisualBoardPeersAndZoom_AreClean", "TheZoomValueIsCoresRenderMinusItsPeriod", "FitCanvasContainsAndCentresCoresBounds",
                "TheBoardsRightAndLeftFollowConnectionsAndEveryMoveRevealsItsSeat"],
            ["canvas-visual"]),
        new(
            "Canvas card editor (W6-1 §E)",
            ["CanvasCardEditorSheet", "CanvasCardEditorText"],
            [new("AutomationNamedGroupPeer", ["Group"], [])],
            ["Edit"],
            ["Value", "Text"],
            ["card's reference"],
            ["CanvasAuthoring_NewCanvasCardEditorAndUndo_AreReachable", "AuthoringLoopThenUndoChainRestoresTheCommittedBytes"],
            ["canvas-card-editor"]),
        new(
            "Canvas prompt sheets (W6-1 §F, §G, §G2)",
            ["CanvasPromptSheet", "CanvasPromptDraft", "CanvasPromptChoices", "CanvasPromptClearMarks"],
            [new("AutomationNamedGroupPeer", ["Group"], [])],
            ["Edit", "List", "ListItem", "Button"],
            ["Value", "Selection", "SelectionItem", "Invoke"],
            ["Title", "Name", "Status"],
            ["CanvasMarks_ToggleListJumpDeleteAndUndo_AreReachable", "CanvasVerbs_GroupConnectDuplicateLinkConvertAndUndo_AreReachable", "TheSubmitFamilyAndTheChoiceStatusAreShapedAsFrozen"],
            ["canvas-marks-list", "canvas-verbs-group-prompt"]),
        new(
            "Canvas pickers (W6-1 §E, §F, §G2)",
            ["CanvasCardPickerSheet", "CanvasCardPickerFilter", "CanvasCardPickerRows"],
            [new("AutomationNamedGroupPeer", ["Group"], [])],
            ["Edit", "List", "ListItem"],
            ["Value", "Selection", "SelectionItem"],
            ["SheetName", "FilterName", "RowsName", "Label", "Status"],
            ["CanvasModes_MoveResizeAndConnectPicker_AreReachable", "CanvasVerbs_GroupConnectDuplicateLinkConvertAndUndo_AreReachable", "AStaleVaultPickRefusesPickDifferentTarget"],
            ["canvas-card-picker", "canvas-verbs-note-picker"]),
        new(
            "Canvas context menus and row actions (W6-1 §E, §G2)",
            [],
            [],
            ["Menu", "MenuItem"],
            ["Invoke"],
            ["CanvasContextMenuPlan.Label"],
            // W7-7 R-12 (#1256, OD-3): the board's card menu and the keyboard
            // request that opens a fresh row's or the seated card's menu.
            ["TheOutlineMenuEqualsThePlan", "TheRowMenuEqualsThePlansGridProjectionAndToggleMarkIsLive", "AConnectionRowsVerbsActOnTheCapturedEdgeFromItsSeatedSource", "CanvasSurfaces_TableGridSortSelectionAndActivation_AreClean",
                "TheBoardMenuEqualsThePlan", "TheBoardsCardMenuIsTheApplicableVerbInventoryPerKind", "AKeyboardRequestOnAFreshRowOpensThatRowsMenu", "AKeyboardRequestOnTheBoardOpensTheSeatedCardsMenu", "Canvas_ContextMenus_OpenTheCardMenuByKeyboard"],
            []),
    ];

    private static readonly Dictionary<string, string> BaseControlTypes = new()
    {
        ["TreeViewAutomationPeer"] = "Tree",
        ["TreeViewDataItemAutomationPeer"] = "TreeItem",
        ["TreeViewItemAutomationPeer"] = "TreeItem",
        ["ListBoxAutomationPeer"] = "List",
        ["TextBlockAutomationPeer"] = "Text",
    };

    private sealed record Row(string Title, string[] Cells);

    /// <summary>Review round 1 (IH-57): a cell HAS a token when the token
    /// stands as a whole word in it — `NotDataGrid` does not carry
    /// `DataGrid`; a substring is not a claim.</summary>
    private static bool CellHas(string cell, string token) =>
        Regex.IsMatch(cell, @"(?<![A-Za-z0-9_])" + Regex.Escape(token) + @"(?![A-Za-z0-9_])");

    private static string RepoRoot => SourceText.RepoRoot();

    /// <summary>The canvas rows, from the shared parser — escaped pipes and
    /// all, so a row counts the same cells in every census.</summary>
    private static List<Row> CanvasRows() =>
        WcMatrixEvidenceCensus.MatrixRows("Canvas ").Select(cells => new Row(cells[0], cells)).ToList();

    private static string ShellText()
    {
        string root = SourceText.ShellSourceRoot();
        var all = new System.Text.StringBuilder();
        foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(root, "*.xaml", SearchOption.AllDirectories)))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }
            all.Append(File.ReadAllText(path)).Append('\n');
        }
        return all.ToString();
    }

    [Fact]
    public void EveryManifestSurfaceIsARowOfTenCellsAndNoCanvasRowIsUnknown()
    {
        List<Row> rows = CanvasRows();
        Assert.Equal(
            Manifest.Select(s => s.Title).OrderBy(t => t, StringComparer.Ordinal),
            rows.Select(r => r.Title).OrderBy(t => t, StringComparer.Ordinal));
        foreach (Row row in rows)
        {
            Assert.True(row.Cells.Length == 10, $"{row.Title}: {row.Cells.Length} cells, not ten — a column is lost on render");
            for (int i = 1; i < 7; i++)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Cells[i]), $"{row.Title}: cell {i} is empty");
            }
            // Human dispositions and named records are owned by WcMatrixEvidenceCensus.
        }
    }

    [Fact]
    public void TheControlTypesPatternsAndNameSourcesAreTheSourcesOwn()
    {
        string shell = ShellText();
        var failures = new List<string>();
        foreach (Surface surface in Manifest)
        {
            Row row = Assert.Single(CanvasRows(), r => r.Title == surface.Title);
            string controlCell = row.Cells[1];
            string nameCell = row.Cells[2];
            string patternCell = row.Cells[3];
            foreach (string id in surface.Ids)
            {
                if (!shell.Contains($"\"{id}\"", StringComparison.Ordinal))
                {
                    failures.Add($"{surface.Title}: the shell sets no automation id {id}");
                }
                if (!row.Cells.Any(c => c.Contains($"`{id}`", StringComparison.Ordinal)))
                {
                    failures.Add($"{surface.Title}: the row does not name `{id}`");
                }
            }
            foreach (Peer peer in surface.Peers)
            {
                Match decl = Regex.Match(shell, @"class " + peer.Class + @"\b[^{]*");
                if (!decl.Success)
                {
                    failures.Add($"{surface.Title}: no peer class {peer.Class}");
                    continue;
                }
                int start = decl.Index;
                int end = shell.IndexOf("\ninternal sealed class ", start + 1, StringComparison.Ordinal);
                string span = end > 0 ? shell[start..end] : shell[start..];
                foreach (string type in peer.ControlTypes)
                {
                    bool fromBase = BaseControlTypes.Any(b => decl.Value.Contains(b.Key, StringComparison.Ordinal) && b.Value == type);
                    bool fromOverride = span.Contains($"AutomationControlType.{type}", StringComparison.Ordinal);
                    if (!fromBase && !fromOverride)
                    {
                        failures.Add($"{surface.Title}: {peer.Class} does not return control type {type}");
                    }
                    if (!CellHas(controlCell, type))
                    {
                        failures.Add($"{surface.Title}: the control-type cell lacks {type} ({peer.Class})");
                    }
                }
                foreach (string pattern in peer.Patterns)
                {
                    if (!span.Contains($"PatternInterface.{pattern}", StringComparison.Ordinal))
                    {
                        failures.Add($"{surface.Title}: {peer.Class} does not handle PatternInterface.{pattern}");
                    }
                    if (!CellHas(patternCell, pattern))
                    {
                        failures.Add($"{surface.Title}: the patterns cell lacks {pattern} ({peer.Class})");
                    }
                }
            }
            foreach (string type in surface.NativeControlTypes)
            {
                if (!CellHas(controlCell, type))
                {
                    failures.Add($"{surface.Title}: the control-type cell lacks {type}");
                }
            }
            foreach (string pattern in surface.NativePatterns)
            {
                if (!CellHas(patternCell, pattern))
                {
                    failures.Add($"{surface.Title}: the patterns cell lacks {pattern}");
                }
            }
            foreach (string source in surface.NameSources)
            {
                if (!CellHas(nameCell, source))
                {
                    failures.Add($"{surface.Title}: the Name/HelpText cell lacks {source}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>The manifest's evidence names and axe labels are in each
    /// row's evidence cell. That every name there binds to an executable
    /// test, that every label is scanned and that the human cells are
    /// Pending or recorded is the shared gate's
    /// (<see cref="WcMatrixEvidenceCensus.EveryRowHasTenCellsAndExecutableEvidence"/>),
    /// which covers these rows in its own run.</summary>
    [Fact]
    public void EveryEvidenceNameResolvesAndEveryAxeLabelIsScanned()
    {
        foreach (Surface surface in Manifest)
        {
            Row row = Assert.Single(CanvasRows(), r => r.Title == surface.Title);
            foreach (string name in surface.Evidence.Concat(surface.AxeLabels))
            {
                Assert.Contains($"`{name}`", row.Cells[6], StringComparison.Ordinal);
            }
        }
    }

    /// <summary>§H TH-10 (H8, IH-20, IH-46): the manual AT checklist carries
    /// T's ten items one to one — the three clauses of T's first item as
    /// three checks — with the five dictated commands verbatim and the
    /// field form's header fields present; the matrix's wave-close status
    /// links it. Its rows' automated twins and human cells are the shared
    /// gate's (<see cref="WcMatrixEvidenceCensus.ChecklistRowsHaveSpecRoutesEvidenceAndUninventedHumanResults"/>),
    /// which binds each twin to an executable test instead of matching
    /// text a comment or a string could carry.</summary>
    [Fact]
    public void TheAtChecklistCarriesTsTenItemsWithTheirTwinsPending()
    {
        string path = Path.Combine(RepoRoot, "docs", "plans", "18_windows_port", "reports", "w6_1_canvas_at_checklist.md");
        Assert.True(File.Exists(path), "the W6-1 AT checklist is missing");
        string text = File.ReadAllText(path);
        // The checklist retains its recording fields. Completed cells link
        // independent, reader-specific records validated by the shared gate.
        foreach (string field in (string[])["Tester", "AT", "OS", "Build", "Corpus", "Method", "Run date", "Evidence reference"])
        {
            Match value = Regex.Match(text, @"\*\*" + Regex.Escape(field) + @":\*\* ([^·\n]+)");
            Assert.True(value.Success && value.Groups[1].Value.Trim().Length > 0, $"the checklist's {field} field is missing or empty");
        }
        var rows = text.Split('\n').Where(l => Regex.IsMatch(l, @"^\| \d+ \|")).Select(l => l.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray()).ToList();
        Assert.Equal(11, rows.Count);
        var tItems = new HashSet<int>();
        foreach (string[] row in rows)
        {
            Assert.Equal(9, row.Length);
            foreach (string item in row[1].Split(',').Select(s => s.Trim()))
            {
                tItems.Add(int.Parse(item, System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        Assert.Equal(Enumerable.Range(1, 10), tItems.OrderBy(i => i));
        string voice = Assert.Single(rows, r => r[0] == "6")[3];
        foreach (string command in (string[])["\"Click 3\"", "\"Toggle Mark\"", "\"Connect To\"", "\"Delete Marked Cards\"", "\"Where am I\""])
        {
            Assert.Contains(command, voice);
        }
        string matrix = File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "18_windows_port", "w_c_matrix.md"));
        Assert.Contains("reports/w6_1_canvas_at_checklist.md", matrix);
    }

}
