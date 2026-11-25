// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.FileManagement;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11): nothing in the Files pane, Import… or Move To that holds
/// the keys goes off before they have left it. Each fact runs on the shipped window
/// (<see cref="ShownShell"/>, the dispatcher's context installed, so a tree publication is
/// posted as in the app) with the Files sidebar attached through the lifecycle's own
/// setter, holds the keys on the real button, presses it by its peer's Invoke, and pumps
/// to whatever turns it off: its own press, a later posted publication, or the work it
/// started settling. The keys go where WPF or the guard puts them today, only earlier: the
/// group (Expander) around the button for the File actions, Batch actions and Shortcuts
/// buttons, the Files tree for the settings notice's Retry, the sheet's first stop for
/// Move To's Retry, and the element the keys came from for the trash's Cancel. Fourteen of
/// these buttons have no x:Name; a fact finds one by its command inside its named holder
/// and names both.
/// </summary>
public sealed class FilesPaneKeysFirstTests
{
    private const string Malformed = "{\"version\":1,\"sort\":\"bogus\"}";

    /// <summary>Delete's own press starts a trash and turns it off (its busy flag, then
    /// IsTrashing): the keys go to the File actions group first.</summary>
    [Fact]
    public void DeleteMovesTheKeysToItsGroupFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var pane = new FilesPane(host);
        pane.Select("a.md");
        Button delete = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.DeleteCommand);
        pane.TakeTheKeys(delete);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Delete", delete, () =>
        {
            Press(delete);
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.TrashCompletion, TimeSpan.FromSeconds(30));
            pane.SettleThePublication();
        });

        Assert.False(File.Exists(Path.Combine(host.Root, "a.md")), "the delete did not move a.md to the bin");
        pane.AssertTheKeysAreOn(pane.Group("SidebarFileActionsExpander"), "the File actions group");
    });

    /// <summary>Move checked to Recycle Bin's own press starts a trash: the keys go to the
    /// Batch actions group first.</summary>
    [Fact]
    public void MoveCheckedToTheRecycleBinMovesTheKeysToItsGroupFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var pane = new FilesPane(host);
        Assert.True(pane.Sidebar.ToggleBatchSelection(pane.Node("a.md")));
        host.Settle();
        Button trash = pane.CommandButton("SidebarBatchActions", pane.Sidebar.BatchTrashCommand);
        pane.TakeTheKeys(trash);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Move checked to Recycle Bin", trash, () =>
        {
            Press(trash);
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.TrashCompletion, TimeSpan.FromSeconds(30));
            pane.SettleThePublication();
        });

        Assert.False(File.Exists(Path.Combine(host.Root, "a.md")), "the batch trash did not move a.md to the bin");
        pane.AssertTheKeysAreOn(pane.Group("SidebarBatchActions"), "the Batch actions group");
    });

    /// <summary>Cancel import goes off in its own press (plan 47 CB-16; the owner's answer of
    /// 2026-11-24, "Off, keys go back"), as it once went off only when the import it stopped
    /// settled: either way the keys go to the File actions group first.</summary>
    [Fact]
    public void CancelImportMovesTheKeysToItsGroupFirstWhenPressed() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { HandOverAtOnce = true, HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        pane.Sidebar.ImportCommand.Execute(null);
        host.Settle();
        Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "premise: the import's worker never started");
        Button cancel = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.CancelImportCommand);
        Assert.True(cancel.IsEnabled, "premise: Cancel import is off while the import runs");
        pane.TakeTheKeys(cancel);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Cancel import", cancel, () =>
        {
            Press(cancel);
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            host.Settle();
        });

        Assert.Equal(0, import.WorkRuns);
        pane.AssertTheKeysAreOn(pane.Group("SidebarFileActionsExpander"), "the File actions group");
    });

    /// <summary>Import… stays on, with the keys on it, while its pick is up (a box of the
    /// window's own in the app, which hands the keys back to it as it closes); once the
    /// pick returns sources the import starts and Import… goes off, and the keys go to the
    /// File actions group first.</summary>
    [Fact]
    public void AfterAPickTheKeysLeaveImportFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport();
        using var pane = new FilesPane(host, import: import);
        Button importButton = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.ImportCommand);
        pane.TakeTheKeys(importButton);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Import…", importButton, () =>
        {
            Press(importButton);
            Assert.True(PumpedDispatcher.PumpUntil(() => import.Picks == 1, TimeSpan.FromSeconds(10)), "the press never asked for the import's sources");
            host.Settle();
            Assert.True(
                importButton.IsEnabled && ReferenceEquals(Keyboard.FocusedElement, importButton),
                $"while its box was up Import… was {(importButton.IsEnabled ? "on" : "off")} and the keys were on {Describe(Keyboard.FocusedElement)}");
            import.HandOverTheSource();
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            pane.SettleThePublication();
        });

        Assert.Equal(1, import.WorkRuns);
        Assert.True(File.Exists(Path.Combine(host.Root, import.SourceName)), "the picked source was not imported");
        pane.AssertTheKeysAreOn(pane.Group("SidebarFileActionsExpander"), "the File actions group");
    });

    /// <summary>D3: Import… is on, with the keys on it, while its pick is up, and a pick
    /// that returns nothing leaves the keys on it, on. Nothing is importing while the pick
    /// is up: Cancel import is off and the other verbs are on.</summary>
    [Fact]
    public void ImportStaysOnWhileItsBoxIsUp() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport();
        using var pane = new FilesPane(host, import: import);
        Button importButton = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.ImportCommand);
        Button cancelImport = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.CancelImportCommand);
        Button newNote = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.CreateNoteCommand);
        pane.TakeTheKeys(importButton);

        KeysFirstRecorder.AssertItStaysOn("Import…", importButton, () =>
        {
            Press(importButton);
            Assert.True(PumpedDispatcher.PumpUntil(() => import.Picks == 1, TimeSpan.FromSeconds(10)), "the press never asked for the import's sources");
            host.Settle();
            Assert.True(importButton.IsEnabled, "Import… went off while its box was up");
            Assert.False(cancelImport.IsEnabled, "Cancel import came on while Import…'s box was up, with nothing importing");
            Assert.True(newNote.IsEnabled, "New note went off while Import…'s box was up, with nothing importing");
            import.HandOverNothing();
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(10));
            host.Settle();
        }, () => import.Picks == 1 && pane.Sidebar.ImportCompletion.IsCompleted && !pane.Sidebar.IsImporting);

        Assert.True(importButton.IsEnabled, "Import… is off after an empty pick");
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, importButton), $"after an empty pick the keys are on {Describe(Keyboard.FocusedElement)}, not Import…");
        Assert.Equal(0, import.WorkRuns);
    });

    /// <summary>D3: a second press of Import… while the first is picking starts nothing:
    /// no second pick, and the first import's completion is still the one the sidebar
    /// reports.</summary>
    [Fact]
    public void ASecondImportWhileOneIsPickedDoesNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport();
        using var pane = new FilesPane(host, import: import);
        Button importButton = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.ImportCommand);
        pane.TakeTheKeys(importButton);
        Press(importButton);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.Picks == 1, TimeSpan.FromSeconds(10)), "the press never asked for the import's sources");
        host.Settle();
        Task first = pane.Sidebar.ImportCompletion;

        if (importButton.IsEnabled)
        {
            Press(importButton);
        }

        pane.Sidebar.ImportCommand.Execute(null);
        host.Settle();

        Assert.Equal(1, import.Picks);
        Assert.Same(first, pane.Sidebar.ImportCompletion);
        Assert.False(first.IsCompleted, "the second press ended the first import's pick");
        import.HandOverTheSource();
        PumpedDispatcher.PumpUntilDrained(first, TimeSpan.FromSeconds(30));
        pane.SettleThePublication();
        Assert.Equal(1, import.Picks);
        Assert.Equal(1, import.WorkRuns);
    });

    /// <summary>D2b: Cancel import held the keys when the reader left the window; the
    /// import settled meanwhile and turned it off. When the reader comes back the keys are
    /// on a live element of the Files pane, not on nothing.</summary>
    [Fact]
    public void KeysThatCameBackToNothingAreLanded() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { HandOverAtOnce = true, HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        pane.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "premise: the import's worker never started");
        host.Settle();
        Button cancel = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.CancelImportCommand);
        pane.TakeTheKeys(cancel);

        WindowActivation.GoAway(host.Shell);
        Assert.True(Keyboard.FocusedElement is null, $"premise: away, the keys are still on {Describe(Keyboard.FocusedElement)}");
        import.ReleaseTheWorker();
        PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
        pane.SettleThePublication();
        Assert.False(cancel.IsEnabled, "premise: Cancel import is still on after the import settled");
        WindowActivation.ComeBack(host.Shell);
        host.Settle();

        IInputElement? keys = Keyboard.FocusedElement;
        Assert.True(
            keys is UIElement { IsVisible: true, IsEnabled: true, Focusable: true } and not Window,
            $"the window came back with the keys on {Describe(keys)}, which holds no keys");
        Assert.True(KeysFirstRecorder.TheKeysAreIn(host.Body("FilesPaneBorder")), $"the window came back with the keys on {Describe(keys)}, outside the Files pane");
    });

    /// <summary>Folder note makes the note; the posted publication then shows the folder
    /// with one and turns the button off: the keys go to the File actions group first.</summary>
    [Fact]
    public void FolderNoteMovesTheKeysToItsGroupFirstWhenThePublicationLands() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        using var pane = new FilesPane(host);
        pane.Select("docs");
        Button folderNote = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.CreateFolderNoteCommand);
        pane.TakeTheKeys(folderNote);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Folder note", folderNote, () =>
        {
            Press(folderNote);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => pane.Node("docs").HasFolderNote, TimeSpan.FromSeconds(30)),
                "the publication never showed the folder with its note");
            pane.SettleThePublication();
        });

        Assert.True(File.Exists(Path.Combine(host.Root, "docs", "docs.md")), "the folder note was not made");
        pane.AssertTheKeysAreOn(pane.Group("SidebarFileActionsExpander"), "the File actions group");
    });

    /// <summary>Delete folder note asks through its box (confirmDestructive); it is on, with
    /// the keys on it, while the box is up, so the box's close can hand them back to it.
    /// The posted publication then shows the folder without its note and turns it off: the
    /// keys go to the File actions group first.</summary>
    [Fact]
    public void DeleteFolderNoteIsOnInItsBoxAndTheKeysLeaveItFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"), ("docs/docs.md", "# Docs\n"));
        Button? deleteFolderNote = null;
        var inTheBox = new List<string>();
        using var pane = new FilesPane(host, confirm: _ =>
        {
            inTheBox.Add($"{(deleteFolderNote!.IsEnabled ? "on" : "off")}, the keys on {Describe(Keyboard.FocusedElement)}");
            return true;
        });
        pane.Select("docs");
        Assert.True(pane.Node("docs").HasFolderNote, "premise: the folder has no note");
        deleteFolderNote = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.DeleteFolderNoteCommand);
        pane.TakeTheKeys(deleteFolderNote);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Delete folder note", deleteFolderNote, () =>
        {
            Press(deleteFolderNote);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !pane.Node("docs").HasFolderNote, TimeSpan.FromSeconds(30)),
                "the publication never showed the folder without its note");
            pane.SettleThePublication();
        });

        Assert.Equal([$"on, the keys on {Describe(deleteFolderNote)}"], inTheBox);
        Assert.False(File.Exists(Path.Combine(host.Root, "docs", "docs.md")), "the folder note was not deleted");
        pane.AssertTheKeysAreOn(pane.Group("SidebarFileActionsExpander"), "the File actions group");
    });

    /// <summary>Remove shortcut's own press removes the shortcut and the selection goes
    /// with it: the keys go to the Shortcuts group first.</summary>
    [Fact]
    public void RemoveShortcutMovesTheKeysToItsGroupFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var pane = new FilesPane(host);
        pane.Select("a.md");
        pane.Sidebar.AddShortcutCommand.Execute(null);
        host.Settle();
        pane.Sidebar.SelectedShortcut = Assert.Single(pane.Sidebar.Shortcuts);
        host.Settle();
        Button remove = pane.CommandButton("SidebarShortcutsActions", pane.Sidebar.RemoveShortcutCommand);
        pane.TakeTheKeys(remove);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Remove shortcut", remove, () =>
        {
            Press(remove);
            host.Settle();
        });

        Assert.Empty(pane.Sidebar.Shortcuts);
        pane.AssertTheKeysAreOn(pane.Group("SidebarShortcutsActions"), "the Shortcuts group");
    });

    /// <summary>The settings notice's Retry clears the notice once the file reads, and its
    /// panel collapses and it goes off: the keys go to the Files tree first.</summary>
    [Fact]
    public void RetrySidebarSettingsMovesTheKeysToTheTreeFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"), (".slate/sidebar.json", Malformed));
        using var pane = new FilesPane(host);
        Assert.True(pane.Sidebar.HasSettingsNotice, "premise: the malformed settings raised no notice");
        host.Settle();
        Button retry = Assert.IsAssignableFrom<Button>(host.Body("SidebarSettingsRetry"));
        Assert.Same(pane.Sidebar.RetrySettingsCommand, retry.Command);
        File.WriteAllText(Path.Combine(host.Root, ".slate", "sidebar.json"), "{\"version\":1}");
        pane.TakeTheKeys(retry);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Retry sidebar settings", retry, () =>
        {
            Press(retry);
            host.Settle();
        });

        Assert.False(pane.Sidebar.HasSettingsNotice, "the retry left the notice");
        Assert.True(Keyboard.FocusedElement is TreeViewItem, $"the keys are on {Describe(Keyboard.FocusedElement)}, not a row of the Files tree");
        pane.AssertTheKeysAreOn(host.Body("FilesTree"), "the Files tree");
    });

    /// <summary>File actions Move to… moves the selected file into a folder the tree does
    /// not show (collapsed). Its own press never turns it off: the sheet's close hands the
    /// keys back to it, and the committed move's posted publication then lets go of the
    /// selection and turns it off. The keys go to the File actions group first. The tree's
    /// read is held until the sheet's restore has run, the order in which the keys are on
    /// the button when it goes off (the record's B13, run 2; in the other order the
    /// restore finds it off and lands on the tree, run 1).</summary>
    [Fact]
    public void MoveToFromFileActionsMovesTheKeysFirstWhenTheMovedFileIsNotShown() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        var tree = new HeldTreeWorker();
        using var pane = new FilesPane(host, treeWorker: tree.Run);
        pane.Select("a.md");
        Button moveTo = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.MoveToCommand);
        pane.TakeTheKeys(moveTo);

        try
        {
            KeysFirstRecorder.AssertTheKeysLeaveFirst("File actions Move to…", moveTo, () =>
            {
                Press(moveTo);
                MoveToPickerViewModel picker = pane.AwaitTheLoadedPicker();
                tree.Hold = true;
                picker.ActivateCommand.Execute(picker.Rows.Single(row => row.Destination == "docs"));
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => ReferenceEquals(Keyboard.FocusedElement, moveTo) && tree.Held >= 1, TimeSpan.FromSeconds(30)),
                    $"the sheet's close left the keys on {Describe(Keyboard.FocusedElement)}, not Move to…");
                tree.Release();
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => File.Exists(Path.Combine(host.Root, "docs", "a.md")) && pane.Sidebar.SelectedNode is null, TimeSpan.FromSeconds(30)),
                    "the move's publication never let go of the selection");
                pane.SettleThePublication();
            });
        }
        finally
        {
            tree.Release();
        }

        Assert.Null(pane.Sidebar.MoveToSheet);
        Assert.False(moveTo.IsEnabled, "Move to… came back on with nothing selected");
        pane.AssertTheKeysAreOn(pane.Group("SidebarFileActionsExpander"), "the File actions group");
    });

    /// <summary>The batch Move to… goes off when an import starts (from the menu or the
    /// palette, whose close hands the keys back to it, as the import's box does), with the
    /// keys on it: they go to the Batch actions group first. A move does not turn it off:
    /// the check follows the moved file, and a folder that is not shown proves nothing
    /// absent, so the check is kept.</summary>
    [Fact]
    public void TheBatchMoveToMovesTheKeysToItsGroupFirstWhenAnImportStarts() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        using var import = new HeldImport { HandOverAtOnce = true, HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        Assert.True(pane.Sidebar.ToggleBatchSelection(pane.Node("a.md")));
        host.Settle();
        Button moveTo = pane.CommandButton("SidebarBatchActions", pane.Sidebar.MoveToCommand);
        pane.TakeTheKeys(moveTo);

        try
        {
            KeysFirstRecorder.AssertTheKeysLeaveFirst("Batch actions Move to…", moveTo, () =>
            {
                pane.Sidebar.ImportCommand.Execute(null);
                Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "the import's worker never started");
                host.Settle();
            });
        }
        finally
        {
            import.ReleaseTheWorker();
        }

        PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
        pane.SettleThePublication();
        pane.AssertTheKeysAreOn(pane.Group("SidebarBatchActions"), "the Batch actions group");
    });

    /// <summary>Move To's Retry restarts the destination read: its row collapses and it
    /// goes off, and the keys go to the sheet's first stop, inside the open sheet.</summary>
    [Fact]
    public void MoveToRetryMovesTheKeysInsideTheSheetFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        using var pane = new FilesPane(host);
        int pages = 0;
        pane.Sidebar.BeforeMoveToPageForTesting = (_, _) =>
        {
            if (Interlocked.Increment(ref pages) == 1)
            {
                throw new IOException("the first destination read fails");
            }
        };
        pane.Select("a.md");
        pane.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = Assert.IsType<MoveToPickerViewModel>(pane.Sidebar.MoveToSheet);
        Assert.True(PumpedDispatcher.PumpUntil(() => picker.CanRetry, TimeSpan.FromSeconds(30)), "premise: the first read never failed");
        host.Settle();
        Button retry = Assert.IsAssignableFrom<Button>(host.Body("MoveToRetry"));
        Assert.Same(picker.RetryCommand, retry.Command);
        pane.TakeTheKeys(retry);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Move To's Retry", retry, () =>
        {
            Press(retry);
            Assert.True(PumpedDispatcher.PumpUntil(() => picker.IsReady, TimeSpan.FromSeconds(30)), "the retried read never finished");
            host.Settle();
        });

        Assert.Same(picker, pane.Sidebar.MoveToSheet);
        pane.AssertTheKeysAreOn(host.Body("MoveToFilterTextBox"), "the sheet's filter field");
    });

    /// <summary>The trash's Cancel: its press sets Stopping and turns it off. The keys go
    /// first to the element they came to it from, which is where its own restore put them
    /// after the off on main.</summary>
    [Fact]
    public void TheTrashsCancelLandsTheKeysWhereTheyCameFromFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var pane = new FilesPane(host);
        using var held = new ManualResetEventSlim();
        int workers = 0;
        pane.Sidebar.TrashWorkerStartingForTesting = token =>
        {
            if (Interlocked.Increment(ref workers) == 1)
            {
                _ = held.Wait(TimeSpan.FromSeconds(30));
            }
        };
        pane.Select("a.md");
        pane.Sidebar.DeleteCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => Volatile.Read(ref workers) == 1 && pane.Sidebar.ShowTrashProgress, TimeSpan.FromSeconds(10)), "premise: the trash never started");
        host.Settle();
        Button cancel = Assert.IsAssignableFrom<Button>(host.Body("CancelTrash"));
        Assert.Same(pane.Sidebar.CancelTrashCommand, cancel.Command);
        var filter = (TextBox)host.Shell.FindName("SidebarFilterTextBox");
        pane.TakeTheKeys(filter);
        pane.TakeTheKeys(cancel);

        try
        {
            KeysFirstRecorder.AssertTheKeysLeaveFirst("the trash's Cancel", cancel, () =>
            {
                Press(cancel);
                host.Settle();
            });
        }
        finally
        {
            held.Set();
        }

        PumpedDispatcher.PumpUntilDrained(pane.Sidebar.TrashCompletion, TimeSpan.FromSeconds(30));
        host.Settle();
        Assert.True(File.Exists(Path.Combine(host.Root, "a.md")), "the cancelled trash moved a.md");
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, filter), $"the keys are on {Describe(Keyboard.FocusedElement)}, not the filter field they came to Cancel from");
        // The merge judge's nit 6: once the keys have left Cancel, the window keeps no hold on
        // the element they came from (a tree row can be removed with its whole subtree).
        Assert.Null(host.Shell.TrashCancelCameFromForTests);
    });

    /// <summary>The trash's Cancel, when the keys came to it from nothing: they go first to
    /// the editor, as its restore's fallback put them after the off on main.</summary>
    [Fact]
    public void TheTrashsCancelLandsTheKeysInTheEditorWhenTheyCameFromNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var pane = new FilesPane(host);
        host.Workspace.OpenPath("b.md");
        host.Settle();
        using var held = new ManualResetEventSlim();
        int workers = 0;
        pane.Sidebar.TrashWorkerStartingForTesting = token =>
        {
            if (Interlocked.Increment(ref workers) == 1)
            {
                _ = held.Wait(TimeSpan.FromSeconds(30));
            }
        };
        pane.Select("a.md");
        pane.Sidebar.DeleteCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => Volatile.Read(ref workers) == 1 && pane.Sidebar.ShowTrashProgress, TimeSpan.FromSeconds(10)), "premise: the trash never started");
        host.Settle();
        Button cancel = Assert.IsAssignableFrom<Button>(host.Body("CancelTrash"));
        Keyboard.ClearFocus();
        Assert.True(Keyboard.FocusedElement is null, $"premise: the keys are still on {Describe(Keyboard.FocusedElement)}");
        pane.TakeTheKeys(cancel);

        try
        {
            KeysFirstRecorder.AssertTheKeysLeaveFirst("the trash's Cancel", cancel, () =>
            {
                Press(cancel);
                host.Settle();
            });
        }
        finally
        {
            held.Set();
        }

        PumpedDispatcher.PumpUntilDrained(pane.Sidebar.TrashCompletion, TimeSpan.FromSeconds(30));
        host.Settle();
        pane.AssertTheKeysAreOn(host.Body("ContentPaneBorder"), "the editor");
    });

    /// <summary>Plan 47 CB-16 (#1830): an import shows on the status bar while it copies, not
    /// while its pick is up, with the Files pane hidden too: core's line, and a Cancel named
    /// for what it cancels whose description is that line. It goes when the import ends.
    /// </summary>
    [Fact]
    public void AnImportShowsOnTheStatusBarWithItsCancel() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        host.Workspace.IsFilesSidebarVisible = false;
        host.Settle();
        // Found in the logical tree: a collapsed status bar item has no visual tree yet.
        var line = LogicalById<TextBlock>(host.Shell.ShellStatusBar, "ImportProgressStatus");
        var cancel = LogicalById<Button>(host.Shell.ShellStatusBar, "CancelImport");

        pane.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.Picks == 1, TimeSpan.FromSeconds(10)), "the press never asked for the import's sources");
        host.Settle();
        Assert.False(cancel.IsVisible, "the status bar showed the import while its pick was up");

        import.HandOverTheSource();
        Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "premise: the import's worker never started");
        host.Settle();
        string importing = SlateUniffiMethods.ImportStatusLine(ImportPhase.Importing);
        Assert.True(line.IsVisible && cancel.IsVisible && cancel.IsEnabled, "the status bar did not show the running import with its Cancel on");
        Assert.Equal(importing, line.Text);
        Assert.Equal("Cancel import", AutomationProperties.GetName(cancel));
        Assert.Equal(importing, AutomationProperties.GetHelpText(cancel));
        Assert.Same(pane.Sidebar.CancelImportCommand, cancel.Command);

        import.ReleaseTheWorker();
        PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
        host.Settle();
        Assert.False(cancel.IsVisible, "the status bar still showed the import after it ended");
    });

    /// <summary>Plan 47 CB-16 (#1830; the owner's answer of 2026-11-24, "Off, keys go back"):
    /// the status bar's Cancel import goes off in its own press, and the keys go first to the
    /// element they came to it from, as the trash's Cancel does.</summary>
    [Fact]
    public void TheStatusBarsImportCancelLandsTheKeysWhereTheyCameFromFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { HandOverAtOnce = true, HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        pane.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "premise: the import's worker never started");
        host.Settle();
        var cancel = LogicalById<Button>(host.Shell.ShellStatusBar, "CancelImport");
        var filter = (TextBox)host.Shell.FindName("SidebarFilterTextBox");
        pane.TakeTheKeys(filter);
        pane.TakeTheKeys(cancel);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the status bar's Cancel import", cancel, () =>
        {
            Press(cancel);
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            host.Settle();
        });

        Assert.Equal(0, import.WorkRuns);
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, filter), $"the keys are on {Describe(Keyboard.FocusedElement)}, not the filter field they came to Cancel from");
        Assert.Null(host.Shell.ImportCancelCameFromForTests);
    });

    /// <summary>The same, when the keys came to the status bar's Cancel import from nothing:
    /// they go first to the editor, as the trash's do.</summary>
    [Fact]
    public void TheStatusBarsImportCancelLandsTheKeysInTheEditorWhenTheyCameFromNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var import = new HeldImport { HandOverAtOnce = true, HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        host.Workspace.OpenPath("b.md");
        host.Settle();
        pane.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "premise: the import's worker never started");
        host.Settle();
        var cancel = LogicalById<Button>(host.Shell.ShellStatusBar, "CancelImport");
        Keyboard.ClearFocus();
        Assert.True(Keyboard.FocusedElement is null, $"premise: the keys are still on {Describe(Keyboard.FocusedElement)}");
        pane.TakeTheKeys(cancel);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the status bar's Cancel import", cancel, () =>
        {
            Press(cancel);
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            host.Settle();
        });

        pane.AssertTheKeysAreOn(host.Body("ContentPaneBorder"), "the editor");
    });

    /// <summary>An import that ends, with no press, while the keys are on the status bar's
    /// Cancel import: the item goes and the button goes off, and the keys go first to the
    /// element they came to it from.</summary>
    [Fact]
    public void AnImportThatEndsUnderTheKeysLandsThemFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { HandOverAtOnce = true, HoldTheWorker = true };
        using var pane = new FilesPane(host, import: import);
        pane.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.WorkersStarted == 1, TimeSpan.FromSeconds(10)), "premise: the import's worker never started");
        host.Settle();
        var cancel = LogicalById<Button>(host.Shell.ShellStatusBar, "CancelImport");
        var filter = (TextBox)host.Shell.FindName("SidebarFilterTextBox");
        pane.TakeTheKeys(filter);
        pane.TakeTheKeys(cancel);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the status bar's Cancel import", cancel, () =>
        {
            import.ReleaseTheWorker();
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            pane.SettleThePublication();
        });

        Assert.Equal(1, import.WorkRuns);
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, filter), $"the keys are on {Describe(Keyboard.FocusedElement)}, not the filter field they came to Cancel from");
    });

    /// <summary>The trash's Cancel when its staging ends on a folder that holds items: the
    /// trash asks to confirm, which hides Cancel and then turns it off. The keys still go
    /// first to the element they came to Cancel from (red team F1: the hide had already
    /// let go of it, and they went to the editor).</summary>
    [Fact]
    public void TheTrashsCancelHiddenForItsConfirmationLandsTheKeysWhereTheyCameFromFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        using var pane = new FilesPane(host);
        using var held = new ManualResetEventSlim();
        int workers = 0;
        pane.Sidebar.TrashWorkerStartingForTesting = token =>
        {
            if (Interlocked.Increment(ref workers) == 1)
            {
                _ = held.Wait(TimeSpan.FromSeconds(30));
            }
        };
        IInputElement? atTheQuestion = null;
        int asked = 0;
        pane.Sidebar.ConfirmRecycle = _ =>
        {
            asked++;
            atTheQuestion = Keyboard.FocusedElement;
            return false;
        };
        pane.Select("docs");
        pane.Sidebar.DeleteCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => Volatile.Read(ref workers) == 1 && pane.Sidebar.ShowTrashProgress, TimeSpan.FromSeconds(10)), "premise: the trash never started");
        host.Settle();
        Button cancel = Assert.IsAssignableFrom<Button>(host.Body("CancelTrash"));
        var filter = (TextBox)host.Shell.FindName("SidebarFilterTextBox");
        pane.TakeTheKeys(filter);
        pane.TakeTheKeys(cancel);

        try
        {
            KeysFirstRecorder.AssertTheKeysLeaveFirst("the trash's Cancel", cancel, () =>
            {
                held.Set();
                Assert.True(PumpedDispatcher.PumpUntil(() => asked > 0, TimeSpan.FromSeconds(30)), "premise: the trash never asked to confirm");
                host.Settle();
            });
        }
        finally
        {
            held.Set();
        }

        PumpedDispatcher.PumpUntilDrained(pane.Sidebar.TrashCompletion, TimeSpan.FromSeconds(30));
        host.Settle();
        Assert.True(Directory.Exists(Path.Combine(host.Root, "docs")), "the unconfirmed trash moved docs");
        Assert.True(ReferenceEquals(atTheQuestion, filter), $"at the question the keys were on {Describe(atTheQuestion)}, not the filter field they came to Cancel from");
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, filter), $"the keys are on {Describe(Keyboard.FocusedElement)}, not the filter field they came to Cancel from");
    });

    /// <summary>Rename's posted publication lets go of the renamed row and seats the new
    /// one in the same turn: Rename stays on throughout, the keys on it.</summary>
    [Fact]
    public void RenameStaysOnThroughItsPublication() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var pane = new FilesPane(host);
        pane.Select("a.md");
        pane.Sidebar.MutationName = "renamed.md";
        host.Settle();
        Button rename = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.RenameCommand);
        pane.TakeTheKeys(rename);

        KeysFirstRecorder.AssertItStaysOn("Rename", rename, () =>
        {
            Press(rename);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => pane.Sidebar.SelectedNode?.Path == "renamed.md", TimeSpan.FromSeconds(30)),
                "the publication never seated the renamed row");
            pane.SettleThePublication();
        }, () => File.Exists(Path.Combine(host.Root, "renamed.md"))
            && pane.Sidebar.TreeRefreshCompletion.IsCompleted
            && pane.Sidebar.SelectedNode?.Path == "renamed.md");

        Assert.True(ReferenceEquals(Keyboard.FocusedElement, rename), $"the keys are on {Describe(Keyboard.FocusedElement)}, not Rename");
    });

    /// <summary>Move To's Cancel closes its sheet: the button leaves the screen with it, and
    /// is never off while it is on screen with the keys on it.</summary>
    [Fact]
    public void MoveToCancelStaysOnAsItsSheetCloses() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        using var pane = new FilesPane(host);
        pane.Select("a.md");
        pane.Sidebar.MoveToCommand.Execute(null);
        MoveToPickerViewModel picker = pane.AwaitTheLoadedPicker();
        var cancel = (Button)host.Shell.FindName("MoveToCancelButton");
        Assert.Same(picker.CancelCommand, cancel.Command);
        pane.TakeTheKeys(cancel);

        KeysFirstRecorder.AssertItStaysOn("Move To's Cancel", cancel, () =>
        {
            Press(cancel);
            host.Settle();
        }, () => pane.Sidebar.MoveToSheet is null && !cancel.IsVisible);

        Assert.True(File.Exists(Path.Combine(host.Root, "a.md")), "Cancel moved a.md");
    });

    /// <summary>Expand loaded folders stays on through its run, the keys on it (the owner's
    /// answer: stay, and speak on a press). A second press while it runs starts no second
    /// run and posts core's WorkStillRunning once, and nothing else.</summary>
    [Fact]
    public void ExpandLoadedStaysOnThroughItsRunAndASecondPressSaysItIsStillRunning() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        var tree = new HeldTreeWorker();
        using var pane = new FilesPane(host, treeWorker: tree.Run);
        Button expand = Assert.IsAssignableFrom<Button>(host.Body("SidebarExpandLoaded"));
        Assert.Same(pane.Sidebar.ExpandLoadedCommand, expand.Command);
        pane.TakeTheKeys(expand);
        var secondPress = new List<A11yEvent>();
        tree.Hold = true;

        try
        {
            KeysFirstRecorder.AssertItStaysOn("Expand loaded folders", expand, () =>
            {
                Press(expand);
                Assert.True(PumpedDispatcher.PumpUntil(() => tree.Held == 1, TimeSpan.FromSeconds(10)), "the run never reached its folder read");
                host.Settle();
                Assert.True(pane.Sidebar.IsExpandingLoaded, "premise: the run is not going");
                Assert.True(expand.IsEnabled, "Expand loaded folders went off while its run went on");
                int before = pane.Said.Count;
                Press(expand);
                host.Settle();
                secondPress.AddRange(pane.Said.Skip(before));
                tree.Release();
                PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ExpandLoadedCompletion, TimeSpan.FromSeconds(30));
                host.Settle();
            }, () => tree.Held >= 1 && pane.Sidebar.ExpandLoadedCompletion.IsCompleted && !pane.Sidebar.IsExpandingLoaded);
        }
        finally
        {
            tree.Release();
        }

        Assert.Equal<A11yEvent>([new A11yEvent.WorkStillRunning(RunningWork.ExpandLoadedFolders)], secondPress);
        Assert.Equal(1, tree.Held);
        Assert.True(pane.Node("docs").IsExpanded, "the run did not expand the folder");
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, expand), $"the keys are on {Describe(Keyboard.FocusedElement)}, not Expand loaded folders");
    });

    /// <summary>One physical press, one step (red team F2): Enter held on Expand loaded
    /// folders starts one run, and its repeats press nothing, so they say nothing; a second
    /// press while the run goes on says "Folders are still expanding." once.</summary>
    [Fact]
    public void AHeldEnterOnExpandLoadedStartsOneRunAndItsRepeatsSayNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("docs/inner.md", "# Inner\n"));
        var tree = new HeldTreeWorker();
        using var pane = new FilesPane(host, treeWorker: tree.Run);
        Button expand = Assert.IsAssignableFrom<Button>(host.Body("SidebarExpandLoaded"));
        pane.TakeTheKeys(expand);
        tree.Hold = true;
        int heard = pane.Said.Count;
        int afterTheHold;

        try
        {
            HeldKeys.Hold(host.Shell, Key.Enter, repeats: 6);
            Assert.True(PumpedDispatcher.PumpUntil(() => tree.Held == 1, TimeSpan.FromSeconds(10)), "the held Enter never started a run");
            host.Settle();
            afterTheHold = pane.Said.Count;
            Press(expand);
            host.Settle();
            tree.Release();
            PumpedDispatcher.PumpUntilDrained(pane.Sidebar.ExpandLoadedCompletion, TimeSpan.FromSeconds(30));
            host.Settle();
        }
        finally
        {
            tree.Release();
        }

        Assert.DoesNotContain(pane.Said.Skip(heard).Take(afterTheHold - heard), line => line is A11yEvent.WorkStillRunning);
        Assert.Equal<A11yEvent>([new A11yEvent.WorkStillRunning(RunningWork.ExpandLoadedFolders)], [.. pane.Said.Skip(heard).OfType<A11yEvent.WorkStillRunning>()]);
        Assert.Equal(1, tree.Held);
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, expand), $"the keys are on {Describe(Keyboard.FocusedElement)}, not Expand loaded folders");
    });

    /// <summary>The merge judge's minor 1: Import… stays on while its box is up, and the
    /// box's default button is Cancel, so a held Enter's repeats closed the box and pressed
    /// Import… again, opening its box once a repeat. One physical press, one step: the
    /// repeats press nothing, and the box opens once.</summary>
    [Fact]
    public void AHeldEnterOnImportOpensItsBoxOnce() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { CancelAtOnce = true };
        using var pane = new FilesPane(host, import: import);
        Button importButton = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.ImportCommand);
        pane.TakeTheKeys(importButton);

        HeldKeys.Hold(host.Shell, Key.Enter, repeats: 6);
        host.Settle();

        Assert.True(import.Picks == 1, $"a held Enter opened Import…'s box {import.Picks} times");
        Assert.Equal(0, import.WorkRuns);
        Assert.True(importButton.IsEnabled, "Import… is off after a cancelled box");
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, importButton), $"the keys are on {Describe(Keyboard.FocusedElement)}, not Import…");
    });

    /// <summary>Codex round 2: the box takes Enter's key-up, so the window never sees it.
    /// The reader cancels the box and presses Enter again, a new press by the keyboard's own
    /// account: Import… opens its box again. WPF, which saw no key-up, took the new press for
    /// the key held down repeating, and the fence swallowed it.</summary>
    [Fact]
    public void ANewEnterOnImportAfterItsBoxTookTheKeyUpOpensItAgain() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"));
        using var import = new HeldImport { CancelAtOnce = true };
        using var pane = new FilesPane(host, import: import);
        Button importButton = pane.CommandButton("SidebarFileActionsExpander", pane.Sidebar.ImportCommand);
        pane.TakeTheKeys(importButton);

        HeldKeys.Down(host.Shell, Key.Enter, held: false);
        host.Settle();
        Assert.True(import.Picks == 1, $"premise: Enter opened Import…'s box {import.Picks} times");
        // No key-up reaches the window: the box took it.
        HeldKeys.Down(host.Shell, Key.Enter, held: false);
        HeldKeys.Up(host.Shell, Key.Enter);
        host.Settle();

        Assert.True(import.Picks == 2, $"a new Enter after the box opened Import…'s box {import.Picks} times in all, not twice");
        Assert.True(ReferenceEquals(Keyboard.FocusedElement, importButton), $"the keys are on {Describe(Keyboard.FocusedElement)}, not Import…");
    });

    /// <summary>A press as UI Automation makes it, which needs no mouse capture.</summary>
    private static void Press(Button button)
    {
        Assert.True(button.IsEnabled, $"{Describe(button)} was off when pressed");
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        PumpedDispatcher.Drain();
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        Button { Content: string content } button => $"Button \"{content}\" ({AutomationProperties.GetAutomationId(button)})",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "The Files pane keys-first fact timed out.");

    /// <summary>The element under <paramref name="root"/>'s logical tree whose AutomationId is
    /// <paramref name="id"/>: a collapsed item's content is there before it has a visual tree.
    /// </summary>
    private static T LogicalById<T>(DependencyObject root, string id)
        where T : FrameworkElement
    {
        var pending = new Stack<DependencyObject>([root]);
        while (pending.TryPop(out DependencyObject? next))
        {
            if (next is T match && AutomationProperties.GetAutomationId(match) == id)
            {
                return match;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(next))
            {
                if (child is DependencyObject element)
                {
                    pending.Push(element);
                }
            }
        }

        throw new InvalidOperationException($"no {typeof(T).Name} with AutomationId {id} under {root}");
    }

    /// <summary>The Files sidebar attached to a shown shell through the lifecycle's own
    /// setter, so the window observes it as in the app (its Move To sheet, its status bar
    /// Cancel), built on the fact's thread after the shell installed the dispatcher's
    /// context, so its tree publications are posted.</summary>
    private sealed class FilesPane : IDisposable
    {
        private readonly ShownShell _host;

        public FilesPane(
            ShownShell host,
            Func<string, bool>? confirm = null,
            HeldImport? import = null,
            Func<Action, CancellationToken, Task>? treeWorker = null)
        {
            _host = host;
            string root = host.Root;
            Sidebar = new FilesSidebarViewModel(
                host.Session,
                Said.Add,
                vaultRoot: root,
                confirmDestructive: confirm,
                pickImportSources: import is null ? null : import.Pick,
                localAppDataRoot: Path.Combine(root, "device-state"),
                treeWorker: treeWorker,
                importWorker: import is null ? null : import.Run);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
            SetLifecycleState(host, nameof(VaultLifecycleViewModel.FileSidebar), Sidebar);
            host.Shell.Activate();
            host.Settle();
        }

        public FilesSidebarViewModel Sidebar { get; }

        public List<A11yEvent> Said { get; } = [];

        public FileTreeNodeViewModel Node(string path) =>
            Assert.Single(Flatten(Sidebar.RootNodes), node => node.Path == path);

        public void Select(string path)
        {
            Sidebar.SelectedNode = Node(path);
            _host.Settle();
        }

        /// <summary>The group (Expander) <paramref name="name"/>, opened.</summary>
        public Expander Group(string name)
        {
            Expander group = Assert.IsType<Expander>(_host.Body(name));
            if (!group.IsExpanded)
            {
                group.IsExpanded = true;
                _host.Settle();
            }

            return group;
        }

        /// <summary>The button inside <paramref name="holder"/> bound to
        /// <paramref name="command"/>; a group is opened first.</summary>
        public Button CommandButton(string holder, ICommand command)
        {
            FrameworkElement around = _host.Body(holder);
            if (around is Expander { IsExpanded: false } group)
            {
                group.IsExpanded = true;
                _host.Settle();
            }

            return Assert.Single(ShownShell.Descendants(around).OfType<Button>(), button => ReferenceEquals(button.Command, command));
        }

        public void TakeTheKeys(UIElement element)
        {
            _host.Settle();
            Assert.True(
                element.Focus() && ReferenceEquals(element, Keyboard.FocusedElement),
                $"keyboard premise: {Describe(element)} could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        }

        /// <summary>The posted tree publication applied, and what it queued run.</summary>
        public void SettleThePublication()
        {
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
            _host.Settle();
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
            _host.Settle();
        }

        public MoveToPickerViewModel AwaitTheLoadedPicker()
        {
            Assert.True(PumpedDispatcher.PumpUntil(() => Sidebar.MoveToSheet is { IsReady: true }, TimeSpan.FromSeconds(30)), "the Move To sheet never loaded its folders");
            _host.Settle();
            return Sidebar.MoveToSheet!;
        }

        public void AssertTheKeysAreOn(FrameworkElement where, string what)
        {
            IInputElement? keys = Keyboard.FocusedElement;
            Assert.True(
                keys is UIElement { IsVisible: true, IsEnabled: true } && KeysFirstRecorder.TheKeysAreIn(where),
                $"the keys are on {Describe(keys)}, not {what}");
        }

        public void Dispose()
        {
            try
            {
                SetLifecycleState(_host, nameof(VaultLifecycleViewModel.FileSidebar), null);
            }
            finally
            {
                PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork, TimeSpan.FromSeconds(30));
            }
        }

        private static IEnumerable<FileTreeNodeViewModel> Flatten(IEnumerable<FileTreeNodeViewModel> nodes)
        {
            foreach (FileTreeNodeViewModel node in nodes)
            {
                yield return node;
                foreach (FileTreeNodeViewModel child in Flatten(node.Children))
                {
                    yield return child;
                }
            }
        }

        /// <summary>The lifecycle's own setter, private with no internal twin, so the
        /// observation the shell wires on it runs (SheetOpenListKeyTests' shape).</summary>
        private static void SetLifecycleState(ShownShell host, string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone; the Files pane facts attach their sidebar through that property's setter."))
            .SetValue(host.Lifecycle, value);
    }

    /// <summary>An import whose pick waits for the fact (each pick its own), unless it
    /// hands over its source at once, and whose worker can be held until the fact releases
    /// it or the import is cancelled. A cancelled held worker never runs its work.</summary>
    private sealed class HeldImport : IDisposable
    {
        private readonly string _source = Path.Combine(Path.GetTempPath(), $"slate-import-source-{Guid.NewGuid():N}.md");
        private readonly List<TaskCompletionSource<IReadOnlyList<string>>> _picks = [];
        private readonly TaskCompletionSource _worker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _workersStarted;
        private int _workRuns;

        public HeldImport() => File.WriteAllText(_source, "# Imported\n");

        public bool HandOverAtOnce { get; init; }

        /// <summary>Each pick returns nothing at once, as the box does when its default
        /// button, Cancel, is pressed.</summary>
        public bool CancelAtOnce { get; init; }

        public bool HoldTheWorker { get; init; }

        public string SourceName => Path.GetFileName(_source);

        public int Picks => _picks.Count;

        public int WorkersStarted => Volatile.Read(ref _workersStarted);

        public int WorkRuns => Volatile.Read(ref _workRuns);

        public Task<IReadOnlyList<string>> Pick()
        {
            var pick = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            _picks.Add(pick);
            if (HandOverAtOnce)
            {
                pick.SetResult([_source]);
            }
            else if (CancelAtOnce)
            {
                pick.SetResult([]);
            }

            return pick.Task;
        }

        public async Task Run(Action work, CancellationToken cancellation)
        {
            Interlocked.Increment(ref _workersStarted);
            if (HoldTheWorker)
            {
                await _worker.Task.WaitAsync(cancellation).ConfigureAwait(false);
            }

            cancellation.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _workRuns);
            await Task.Run(work, cancellation).ConfigureAwait(false);
        }

        public void HandOverTheSource() => _picks[^1].SetResult([_source]);

        public void HandOverNothing() => _picks[^1].SetResult([]);

        public void ReleaseTheWorker() => _worker.TrySetResult();

        public void Dispose()
        {
            foreach (TaskCompletionSource<IReadOnlyList<string>> pick in _picks)
            {
                _ = pick.TrySetResult([]);
            }

            _ = _worker.TrySetCanceled();
            try
            {
                File.Delete(_source);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The sidebar's tree worker, held from the moment the fact says so: every
    /// read asked after that waits for <see cref="Release"/> or its cancellation.</summary>
    private sealed class HeldTreeWorker
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _held;

        public bool Hold { get; set; }

        public int Held => Volatile.Read(ref _held);

        public async Task Run(Action work, CancellationToken cancellation)
        {
            if (Hold)
            {
                Interlocked.Increment(ref _held);
                await _gate.Task.WaitAsync(cancellation).ConfigureAwait(false);
            }

            await Task.Run(work, cancellation).ConfigureAwait(false);
        }

        public void Release() => _gate.TrySetResult();
    }
}
