// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Bases;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// CB-19, the Escape and Enter find: four sheets take Escape and Enter on the
/// tunnelling route, before the control that holds the keys sees them, so with
/// a combo box's list open Escape closed the whole sheet and Enter in Add
/// property and Bulk rename ran the sheet's own action and left the list open.
/// A list open in a sheet now keeps both keys, as it does in Settings, whose
/// handler bubbles: Escape closes only the list, Enter chooses the highlighted
/// row and nothing else. The keys go through WPF's input manager into the
/// shipped window, through the real handlers.
/// </summary>
public sealed class SheetOpenListKeyTests
{
    public static TheoryData<string> Sheets => new()
    {
        "Query builder",
        "Dashboard editor",
        "Add property",
        "Bulk rename",
        "Settings",
    };

    /// <summary>Fact 8 (and fact 10's Settings arm): Escape with a list open
    /// closes the list and leaves the sheet open with the keys on the combo
    /// box; a second Escape closes the sheet.</summary>
    [Theory]
    [MemberData(nameof(Sheets))]
    public void EscapeWithAListOpenClosesOnlyTheList(string name) => OpenLists.RunSta(() =>
    {
        using ShownShell host = OpenLists.NewShell();
        Sheet sheet = Sheet.For(host, name);
        ComboBox combo = sheet.OpenWithItsCombo();
        OpenLists.Open(host, combo, name);
        Assert.True(
            Keyboard.FocusedElement is ComboBoxItem inside && ReferenceEquals(ItemsControl.ItemsControlFromItemContainer(inside), combo),
            $"{name}: the keys did not move into the open list (they are on {Describe(Keyboard.FocusedElement)}).");

        OpenLists.Press(host, Key.Escape);

        Assert.True(sheet.IsOpen(), $"{name}: Escape with the combo box's list open closed the whole sheet.");
        Assert.False(combo.IsDropDownOpen, $"{name}: Escape left the list open.");
        Assert.True(ReferenceEquals(combo, Keyboard.FocusedElement), $"{name}: the keys left the combo box for {Describe(Keyboard.FocusedElement)}.");

        OpenLists.Press(host, Key.Escape);
        Assert.False(sheet.IsOpen(), $"{name}: a second Escape, with no list open, did not close the sheet.");
    });

    /// <summary>Fact 9 (and fact 10's Settings arm): after an arrow moves the
    /// highlight, Enter with the list open chooses that row and closes the list,
    /// and runs nothing else: no property is added and no preview runs. A second
    /// Enter, with the list closed, does what Enter does there today.</summary>
    [Theory]
    [MemberData(nameof(Sheets))]
    public void EnterWithAListOpenChoosesTheRowAndNothingElse(string name) => OpenLists.RunSta(() =>
    {
        using ShownShell host = OpenLists.NewShell();
        Sheet sheet = Sheet.For(host, name);
        ComboBox combo = sheet.OpenWithItsCombo();
        OpenLists.Open(host, combo, name);
        object? chosenBefore = combo.SelectedItem;
        OpenLists.Press(host, combo.SelectedIndex == combo.Items.Count - 1 ? Key.Up : Key.Down);
        Assert.True(
            Keyboard.FocusedElement is ComboBoxItem row && combo.ItemContainerGenerator.ItemFromContainer(row) is { } item && !Equals(item, chosenBefore),
            $"{name}: the arrow did not move the highlight to another row (the keys are on {Describe(Keyboard.FocusedElement)}).");
        object highlighted = combo.ItemContainerGenerator.ItemFromContainer((ComboBoxItem)Keyboard.FocusedElement);
        string actionBefore = sheet.Action();

        OpenLists.Press(host, Key.Enter);

        Assert.True(sheet.IsOpen(), $"{name}: Enter with the combo box's list open closed the sheet.");
        Assert.True(
            actionBefore == sheet.Action(),
            $"{name}: Enter with the combo box's list open ran the sheet's own action: before '{actionBefore}', after '{sheet.Action()}'.");
        Assert.False(combo.IsDropDownOpen, $"{name}: Enter left the list open.");
        Assert.Equal(highlighted, combo.SelectedItem);

        // A second press, not the first one held: the key is let go between, as a keyboard
        // does (plan 47 CB-17b: a held Enter's repeats are no presses in Bulk Rename).
        OpenLists.Release(host, Key.Enter);
        OpenLists.Press(host, Key.Enter);
        sheet.AssertTheSecondEnter(actionBefore);
    });

    /// <summary>Fact 11, a guard: why the four sheets keep the tunnelling route.
    /// WPF's DataGrid takes Enter whenever a cell is current, so a bubbling
    /// handler would never see Enter from Bulk rename's preview grid. With two
    /// rows previewed and the keys on a cell of the first row, Enter runs Preview
    /// again (a run is published and the rename summary is announced), the
    /// current cell stays on the first row and the sheet stays open; Escape from
    /// the same cell closes the sheet.</summary>
    [Fact]
    public void EnterInBulkRenamesPreviewGridStillRunsPreview() => OpenLists.RunSta(() =>
    {
        using ShownShell host = OpenLists.NewShell();
        host.Workspace.OpenBulkRenameSheet(synchronousForTests: true);
        BulkRenameViewModel rename = Assert.IsType<BulkRenameViewModel>(host.Workspace.BulkRenameSheet);
        int runs = 0;
        rename.RunPublished += () => runs++;
        rename.OldKey = "kind";
        rename.NewKey = "type";
        OpenLists.Settle(host);
        rename.Preview();
        Assert.True(PumpedDispatcher.PumpUntil(() => runs == 1 && !rename.WorkInFlight), "the first preview was never published.");
        Assert.Equal(2, rename.Rows.Count);
        OpenLists.Settle(host);

        DataGridCell cell = FirstRowsCell(host);
        Assert.True(cell.Focus(), "a cell of the preview's first row took no keys.");
        OpenLists.Settle(host);
        int announced = host.Announced.Count;

        OpenLists.Press(host, Key.Enter);

        Assert.True(
            PumpedDispatcher.PumpUntil(() => runs == 2 && !rename.WorkInFlight),
            $"Enter on a cell of the preview grid ran no preview (runs published: {runs}).");
        Assert.Contains(host.Announced.Skip(announced), line => line is A11yEvent.RenameSummary);
        Assert.NotNull(host.Workspace.BulkRenameSheet);
        DataGrid grid = GridOf(host);
        Assert.True(
            grid.CurrentCell.Item is { } current && grid.Items.IndexOf(current) == 0,
            $"Enter moved the preview grid's current cell off the first row (row {(grid.CurrentCell.Item is { } now ? grid.Items.IndexOf(now) : -1)}).");

        Assert.True(FirstRowsCell(host).Focus(), "the first row's cell took no keys again.");
        OpenLists.Settle(host);
        OpenLists.Press(host, Key.Escape);
        Assert.Null(host.Workspace.BulkRenameSheet);
    });

    /// <summary>Fact 12, the fifth route: the window cancels a running import on
    /// Escape in its own tunnelling handler, ahead of every control. A combo box
    /// outside any sheet now keeps Escape while its list is open: the list
    /// closes, the keys stay on the combo box and the import goes on (the source
    /// it is handed afterwards is imported). A second import's Escape, with no
    /// list open, cancels it as before (its source is never imported).</summary>
    [Theory]
    [InlineData("ConnectionsDepth")]
    [InlineData("BaseViewPicker")]
    public void EscapeWithAListOpenDuringAnImportClosesTheListAndKeepsTheImport(string key) => OpenLists.RunSta(() =>
    {
        using ShownShell host = OpenLists.NewShell();
        using var imports = new ImportsOnHold();
        FilesSidebarViewModel sidebar = AttachASidebar(host, imports);
        try
        {
            ComboBox combo = OpenLists.Reach(host, key);
            sidebar.ImportCommand.Execute(null);
            Assert.True(sidebar.IsImporting);
            OpenLists.Open(host, combo, key);

            OpenLists.Press(host, Key.Escape);

            Assert.False(combo.IsDropDownOpen, $"{key}: Escape during an import left the list open (the import took the key).");
            Assert.True(ReferenceEquals(combo, Keyboard.FocusedElement), $"{key}: the keys left the combo box for {Describe(Keyboard.FocusedElement)}.");
            imports.HandOverTheSource();
            PumpedDispatcher.PumpUntilDrained(sidebar.ImportCompletion);
            Assert.False(sidebar.IsImporting);
            Assert.True(imports.WorkerRuns == 1, $"{key}: Escape with the list open cancelled the import.");

            sidebar.ImportCommand.Execute(null);
            Assert.True(sidebar.IsImporting);
            Assert.True(combo.Focus(), $"{key}: the combo box took no keys for the second import.");
            OpenLists.Settle(host);
            Assert.False(combo.IsDropDownOpen);
            OpenLists.Press(host, Key.Escape);
            imports.HandOverTheSource();
            PumpedDispatcher.PumpUntilDrained(sidebar.ImportCompletion);
            Assert.True(imports.WorkerRuns == 1, $"{key}: Escape with no list open did not cancel the second import.");
        }
        finally
        {
            DetachTheSidebar(host, sidebar);
        }
    });

    private static DataGrid GridOf(ShownShell host) =>
        Descendants(host.Shell.BulkRenameOverlay).OfType<DataGrid>().Single(grid => grid.IsVisible);

    private static DataGridCell FirstRowsCell(ShownShell host)
    {
        DataGrid grid = GridOf(host);
        grid.UpdateLayout();
        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        return Descendants(row).OfType<DataGridCell>().First(cell => cell.Focusable && cell.IsVisible);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static string Describe(IInputElement? element) => element switch
    {
        null => "nothing",
        FrameworkElement { Name.Length: > 0 } named => $"{named.GetType().Name} {named.Name}",
        FrameworkElement framework when System.Windows.Automation.AutomationProperties.GetAutomationId(framework) is { Length: > 0 } id
            => $"{framework.GetType().Name} {id}",
        _ => element.GetType().Name,
    };

    /// <summary>One sheet: how it opens and is prepared, its combo box, whether
    /// it is open, its own action's trace, and what a second Enter does there
    /// today.</summary>
    private sealed class Sheet
    {
        private Sheet(string name, Func<ComboBox> openWithItsCombo, Func<bool> isOpen, Func<string> action, Action<string> secondEnter)
        {
            Name = name;
            OpenWithItsCombo = openWithItsCombo;
            IsOpen = isOpen;
            Action = action;
            AssertTheSecondEnter = secondEnter;
        }

        public string Name { get; }

        public Func<ComboBox> OpenWithItsCombo { get; }

        public Func<bool> IsOpen { get; }

        /// <summary>What the sheet's own Enter does, as it stands: Add property's
        /// refusal (no note is loaded, so Add answers with one), and Bulk
        /// rename's published previews. The Bases sheets and Settings take no
        /// Enter.</summary>
        public Func<string> Action { get; }

        public Action<string> AssertTheSecondEnter { get; }

        public static Sheet For(ShownShell host, string name)
        {
            switch (name)
            {
                case "Query builder":
                    return new Sheet(
                        name,
                        () => OpenLists.Reach(host, "BuilderCombinator"),
                        () => host.Workspace.BaseQueryBuilderSheet is not null,
                        () => "the builder takes no Enter",
                        _ => Assert.NotNull(host.Workspace.BaseQueryBuilderSheet));
                case "Dashboard editor":
                    return new Sheet(
                        name,
                        () => OpenLists.Reach(host, "View override"),
                        () => host.Workspace.DashboardEditorSheet is not null,
                        () => "the editor takes no Enter",
                        _ => Assert.NotNull(host.Workspace.DashboardEditorSheet));
                case "Add property":
                    AddPropertyViewModel? add = null;
                    return new Sheet(
                        name,
                        () =>
                        {
                            ComboBox combo = OpenLists.Reach(host, "AddPropertyType");
                            add = Assert.IsType<AddPropertyViewModel>(host.Workspace.AddPropertySheet);
                            add.Key = "probekey";
                            OpenLists.Settle(host);
                            return combo;
                        },
                        () => host.Workspace.AddPropertySheet is not null,
                        () => $"Add's refusal: '{add!.ValidationError}'",
                        before =>
                        {
                            Assert.NotEqual(before, $"Add's refusal: '{add!.ValidationError}'");
                            Assert.Equal(PropertyPhrase.NoNoteError, add!.ValidationError);
                        });
                case "Bulk rename":
                    BulkRenameViewModel? rename = null;
                    int runs = 0;
                    return new Sheet(
                        name,
                        () =>
                        {
                            ComboBox combo = OpenLists.Reach(host, "BulkRenameOldKeyType");
                            rename = Assert.IsType<BulkRenameViewModel>(host.Workspace.BulkRenameSheet);
                            rename.RunPublished += () => runs++;
                            rename.OldKey = "status";
                            rename.NewKey = "state";
                            OpenLists.Settle(host);
                            return combo;
                        },
                        () => host.Workspace.BulkRenameSheet is not null,
                        () =>
                        {
                            Assert.True(PumpedDispatcher.PumpUntil(() => !rename!.WorkInFlight), "a preview never finished.");
                            return $"previews published: {runs}, rows: {rename!.Rows.Count}";
                        },
                        _ =>
                        {
                            Assert.True(
                                PumpedDispatcher.PumpUntil(() => runs == 1 && !rename!.WorkInFlight),
                                $"a second Enter with the list closed ran no preview (previews published: {runs}).");
                        });
                case "Settings":
                    return new Sheet(
                        name,
                        () => OpenLists.Reach(host, "SettingsTheme"),
                        () => host.Lifecycle.SettingsSheet is not null,
                        () => "Settings takes no Enter",
                        _ => Assert.NotNull(host.Lifecycle.SettingsSheet));
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "no such sheet");
            }
        }
    }

    /// <summary>The Files sidebar the window's import cancel reads, attached
    /// through the lifecycle's own setter, with imports whose source pickers
    /// stay open until the fact hands them a file.</summary>
    private static FilesSidebarViewModel AttachASidebar(ShownShell host, ImportsOnHold imports)
    {
        string root = host.Root;
        var sidebar = new FilesSidebarViewModel(
            host.Session,
            _ => { },
            vaultRoot: root,
            pickImportSources: imports.PickSources,
            localAppDataRoot: Path.Combine(root, "device-state"),
            importWorker: imports.Run);
        PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion);
        SetLifecycleState(host, nameof(VaultLifecycleViewModel.FileSidebar), sidebar);
        OpenLists.Settle(host);
        return sidebar;
    }

    private static void DetachTheSidebar(ShownShell host, FilesSidebarViewModel sidebar)
    {
        SetLifecycleState(host, nameof(VaultLifecycleViewModel.FileSidebar), null);
        PumpedDispatcher.PumpUntilDrained(sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
    }

    /// <summary>The lifecycle's own setter, which is private and has no
    /// internal twin, so the observation the shell wires on it runs
    /// (ImportEscapePrecedenceTests' shape).</summary>
    private static void SetLifecycleState(ShownShell host, string name, object? value) =>
        (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"VaultLifecycleViewModel.{name} is gone; the import fact attaches its sidebar through that property's setter."))
        .SetValue(host.Lifecycle, value);

    /// <summary>Imports whose source picker stays open until the fact hands it
    /// one real file outside the vault (PendingImport's shape, one picker for
    /// each import). A kept import runs its worker once; a cancelled one never
    /// runs it.</summary>
    private sealed class ImportsOnHold : IDisposable
    {
        private readonly string _source = Path.Combine(Path.GetTempPath(), $"slate-import-source-{Guid.NewGuid():N}.md");
        private readonly List<TaskCompletionSource<IReadOnlyList<string>>> _pickers = [];
        private int _workerRuns;

        public ImportsOnHold() => File.WriteAllText(_source, "# Imported\n");

        public int WorkerRuns => Volatile.Read(ref _workerRuns);

        public Task<IReadOnlyList<string>> PickSources()
        {
            var picker = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pickers.Add(picker);
            return picker.Task;
        }

        public Task Run(Action work, CancellationToken cancellation)
        {
            Interlocked.Increment(ref _workerRuns);
            return Task.Run(work, cancellation);
        }

        public void HandOverTheSource() => _pickers[^1].SetResult([_source]);

        public void Dispose()
        {
            foreach (TaskCompletionSource<IReadOnlyList<string>> picker in _pickers)
            {
                _ = picker.TrySetResult([]);
            }

            try
            {
                File.Delete(_source);
            }
            catch (IOException)
            {
            }
        }
    }
}
