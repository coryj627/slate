// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "avalon-document-buffer")]
public sealed class AvalonDocumentBufferCensus
{
    private static readonly string[] EditFragments = ["a", "é", "中", "😀", "\r\n", "\n"];
    private readonly ITestOutputHelper _output;

    public AvalonDocumentBufferCensus(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SlateEditorAutomationPeer_ProxiesFocusToAvalonTextArea()
    {
        RunOnSta(() =>
        {
            var editor = new SlateTextEditor();
            AutomationProperties.SetName(editor, "note.md editor");
            using var surface = new EditorSurface(editor);
            AutomationPeer peer = Assert.IsType<SlateTextEditorAutomationPeer>(
                UIElementAutomationPeer.CreatePeerForElement(editor));
            AutomationPeer textAreaPeer = Assert.IsAssignableFrom<AutomationPeer>(
                UIElementAutomationPeer.FromElement(editor.TextArea));

            Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
            Assert.Equal("note.md editor", peer.GetName());
            Assert.Same(peer, textAreaPeer.EventsSource);
            Assert.Same(peer, peer.GetPattern(PatternInterface.Value));
            Assert.Same(textAreaPeer, peer.GetPattern(PatternInterface.Text));
            AutomationPeer scrollPeer = Assert.IsAssignableFrom<AutomationPeer>(
                peer.GetPattern(PatternInterface.Scroll));
            Assert.Same(peer, scrollPeer.EventsSource);
            Assert.Null(peer.GetChildren());
            Assert.True(peer.IsKeyboardFocusable());

            Keyboard.ClearFocus();
            Assert.False(peer.HasKeyboardFocus());
            peer.SetFocus();

            Assert.True(peer.HasKeyboardFocus());
            Assert.True(editor.TextArea.IsKeyboardFocusWithin);

            Keyboard.ClearFocus();
            Assert.True(editor.FocusInputOwner());
            Assert.True(peer.HasKeyboardFocus());
            Assert.True(editor.TextArea.IsKeyboardFocusWithin);
        });
    }

    /// <summary>#1839: another process takes the clipboard between the set and
    /// the paste: before the census checks the clipboard, or after the check
    /// and before the editor's own, clearing it or putting its own text there,
    /// after the editor's paste has been checked and before it reads the text,
    /// or with files, which leave the editor nothing to paste, before the
    /// editor's own last check or after it; or a process
    /// watching the clipboard reads the text before the set has flushed it,
    /// holding the clipboard open until the census's thread renders the text.
    /// The paste applies once, with the census's text.</summary>
    [Theory]
    [InlineData("read by a watcher before the flush")]
    [InlineData("after the set")]
    [InlineData("before the paste")]
    [InlineData("replaced before the paste")]
    [InlineData("replaced after the check")]
    [InlineData("replaced with files before the editor reads it")]
    [InlineData("replaced with files after the editor's last check")]
    public void APasteWhoseClipboardAnotherProcessTookIsRetried(string when)
    {
        RunOnSta(() =>
        {
            using var session = new AvalonDocumentBufferSession("prefix ", _ => { });
            var editor = new TextEditor { Document = session.Document };
            using var surface = new EditorSurface(editor);
            long before = session.AppliedDeltaCount;
            IDataObject? previousClipboard = TryGetClipboardData();
            Thread? watcher = null;
            Func<bool>? lateFiles = null;
            try
            {
                PasteThroughTheClipboard(
                    editor,
                    session,
                    "pasted",
                    afterThePostForTests: when == "read by a watcher before the flush"
                        ? () => watcher = ReadByAWatcher()
                        : null,
                    afterSetForTests: when == "after the set" ? () => WriteTheClipboard(Clipboard.Clear) : null,
                    beforePasteForTests: when switch
                    {
                        "before the paste" => () => WriteTheClipboard(Clipboard.Clear),
                        "replaced before the paste" => () => SetTheClipboard(TextData("another process's text")),
                        _ => null,
                    },
                    afterTheCheckForTests: when == "replaced after the check"
                        ? _ => SetTheClipboard(TextData("another process's text"))
                        : null,
                    beforeTheEditorReadsForTests: when switch
                    {
                        "replaced with files before the editor reads it" => ReplaceTheClipboardWithFiles,
                        "replaced with files after the editor's last check" =>
                            () => lateFiles = ReplaceTheClipboardWithFilesAfterTheEditorChecks(editor),
                        _ => null,
                    });
                Assert.True(lateFiles?.Invoke() ?? true, "premise: files replaced the text after the editor's last check");
                Assert.True(watcher?.Join(TimeSpan.FromSeconds(5)) ?? true, "the watcher's read was never served");
            }
            finally
            {
                RestoreClipboard(previousClipboard);
            }

            Assert.Equal(before + 1, session.AppliedDeltaCount);
            Assert.Equal("prefix pasted", session.Document.Text);
        });
    }

    /// <summary>#1839: an editor that drops a paste of the census's text after
    /// accepting it fails the census at once, with no retry, even though
    /// another process replaces the clipboard meanwhile: the editor had its own
    /// copy of the text, so the clipboard cannot explain the missing
    /// edit.</summary>
    [Fact]
    public void APasteTheEditorDropsAfterAcceptingItFailsAtOnce()
    {
        RunOnSta(() =>
        {
            using var session = new AvalonDocumentBufferSession("prefix ", _ => { });
            var editor = new TextEditor { Document = session.Document };
            using var surface = new EditorSurface(editor);
            long before = session.AppliedDeltaCount;
            IDataObject? previousClipboard = TryGetClipboardData();
            Exception? failure;
            try
            {
                failure = Record.Exception(() => PasteThroughTheClipboard(
                    editor,
                    session,
                    "pasted",
                    afterTheCheckForTests: args =>
                    {
                        // Stands in for an editor defect: the accepted paste is dropped.
                        args.CancelCommand();
                        SetTheClipboard(TextData("another process's text"));
                    }));
            }
            finally
            {
                RestoreClipboard(previousClipboard);
            }

            Assert.NotNull(failure);
            Assert.Contains("it had accepted", failure.Message, StringComparison.Ordinal);
            Assert.Equal(before, session.AppliedDeltaCount);
        });
    }

    [Fact]
    public void ImeCompositionLifecycleAndClipboardPaste_CommitOnlyThroughInputCommands()
    {
        RunOnSta(() =>
        {
            using var session = new AvalonDocumentBufferSession("prefix ", _ => { });
            var editor = new TextEditor { Document = session.Document };
            using var surface = new EditorSurface(editor);
            editor.TextArea.Caret.Offset = session.Document.TextLength;

            long beforeImeCommit = session.AppliedDeltaCount;
            var composition = new TextComposition(
                InputManager.Current,
                editor.TextArea,
                string.Empty,
                TextCompositionAutoComplete.Off);
            SetCompositionProperty(composition, nameof(TextComposition.CompositionText), "に");
            TextCompositionManager.StartComposition(composition);
            Assert.Equal(beforeImeCommit, session.AppliedDeltaCount);
            Assert.Equal("prefix ", session.Document.Text);

            SetCompositionProperty(composition, nameof(TextComposition.CompositionText), "日本");
            TextCompositionManager.UpdateComposition(composition);
            Assert.Equal(beforeImeCommit, session.AppliedDeltaCount);
            Assert.Equal("prefix ", session.Document.Text);

            SetCompositionProperty(composition, nameof(TextComposition.CompositionText), string.Empty);
            SetCompositionProperty(composition, nameof(TextComposition.Text), "日本語");
            TextCompositionManager.CompleteComposition(composition);
            Assert.Equal(beforeImeCommit + 1, session.AppliedDeltaCount);
            Assert.Equal("prefix 日本語", session.Document.Text);

            long beforePaste = session.AppliedDeltaCount;
            IDataObject? previousClipboard = TryGetClipboardData();
            try
            {
                PasteThroughTheClipboard(editor, session, "\r\npasted 😀");
                Assert.Equal(beforePaste + 1, session.AppliedDeltaCount);
            }
            finally
            {
                RestoreClipboard(previousClipboard);
            }

            long beforeUndo = session.AppliedDeltaCount;
            ApplicationCommands.Undo.Execute(null, editor.TextArea);
            Assert.Equal(beforeUndo + 1, session.AppliedDeltaCount);
            Assert.Equal("prefix 日本語", session.Document.Text);

            long beforeRedo = session.AppliedDeltaCount;
            ApplicationCommands.Redo.Execute(null, editor.TextArea);
            Assert.Equal(beforeRedo + 1, session.AppliedDeltaCount);
            Assert.Equal("prefix 日本語\r\npasted 😀", session.Document.Text);

            EditorSaveSnapshot snapshot = session.PrepareSaveSnapshot();
            Assert.Equal(session.Document.Text, snapshot.Text);
            Assert.Equal(
                SlateUniffiMethods.EditorTextContentHash(snapshot.Text),
                session.BufferForCensus.ContentHash());
        });
    }

    [Fact]
    public void DragDropInputEvent_UsesTheDocumentDeltaFeed()
    {
        RunOnSta(() =>
        {
            using var session = new AvalonDocumentBufferSession("drop: ", _ => { });
            var editor = new TextEditor { Document = session.Document };
            editor.Options.EnableTextDragDrop = true;
            using var surface = new EditorSurface(editor);
            long beforeDrop = session.AppliedDeltaCount;
            var data = new DataObject(DataFormats.UnicodeText, "dropped 中😀");
            DragEventArgs drop = CreateDropEventArgs(
                data,
                editor.TextArea.TextView,
                new Point(8, 8));
            drop.RoutedEvent = DragDrop.DropEvent;

            editor.TextArea.RaiseEvent(drop);

            Assert.Equal(beforeDrop + 1, session.AppliedDeltaCount);
            Assert.Contains("dropped 中😀", session.Document.Text, StringComparison.Ordinal);
            Assert.Equal(session.Document.Text, session.BufferForCensus.Text());
        });
    }

    [Fact]
    public void OffsetMapperMatchesGeneratedUnicodeAndFixtureCorpusAtEveryBoundary()
    {
        var documents = new List<string>
        {
            string.Empty,
            "ASCII",
            "é ø",
            "中日本語",
            "😀🧑🏽‍💻",
            "e\u0301 A\u030A",
            "line one\r\nline two\nline three\r",
            "aé中😀z\r\ne\u0301",
        };

        var random = new Random(724);
        for (int document = 0; document < 64; document++)
        {
            var generated = new StringBuilder();
            int tokenCount = random.Next(1, 96);
            for (int token = 0; token < tokenCount; token++)
            {
                generated.Append(EditFragments[random.Next(EditFragments.Length)]);
            }

            documents.Add(generated.ToString());
        }

        string fixtures = Path.Combine(
            RepoRoot,
            "crates",
            "slate-core",
            "tests",
            "fixtures",
            "markdown");
        Assert.True(Directory.Exists(fixtures), $"fixtures missing at {fixtures}");
        documents.AddRange(
            Directory.EnumerateFiles(fixtures, "*.md")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        foreach (string text in documents)
        {
            AssertOffsetMappings(text);
        }

        using var highlighted = new DocumentBuffer("aé中😀 #tag\r\n");
        RangedHighlight range = highlighted.HighlightInRange(0, highlighted.LenUtf16());
        Assert.NotEmpty(range.Spans);
        Assert.Equal(0U, range.AppliedStart);
        Assert.Equal(highlighted.LenUtf16(), highlighted.ByteToUtf16(range.AppliedEnd));
    }

    [Fact]
    public void EightMiBSamePathPeers_UseExactDeltasAndPreserveCrossPaneUndo()
    {
        RunOnSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "w2-peer-deltas");
            using VaultSession vault = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            vault.ScanInitial(cancel);
            using var workspace = new WorkspaceViewModel(vault, fixture.Root, () => [], _ => { });
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel source = Assert.IsType<WorkspaceTabViewModel>(
                workspace.ActiveGroup.ActiveTab);
            string original = SyntheticNote(8 * 1024 * 1024);
            source.Text = original;
            workspace.DuplicateTabCommand.Execute(null);
            Assert.Equal(2, workspace.ActiveGroup.Tabs.Count);
            WorkspaceTabViewModel peer = workspace.ActiveGroup.Tabs[1];
            AvalonDocumentBufferSession sourceSession = Assert.IsType<AvalonDocumentBufferSession>(
                source.EditorSession);
            AvalonDocumentBufferSession peerSession = Assert.IsType<AvalonDocumentBufferSession>(
                peer.EditorSession);
            var peerEditor = new TextEditor { Document = peerSession.Document };
            long sourceDeltas = sourceSession.AppliedDeltaCount;
            long peerDeltas = peerSession.AppliedDeltaCount;
            long sourceRecoveries = sourceSession.DriftRecoveryCount;
            long peerRecoveries = peerSession.DriftRecoveryCount;
            int offset = NearestProseOffset(original);

            sourceSession.Document.Insert(offset, "中😀");

            Assert.Equal(sourceDeltas + 1, sourceSession.AppliedDeltaCount);
            Assert.Equal(peerDeltas + 1, peerSession.AppliedDeltaCount);
            Assert.Equal(sourceSession.Document.Text, peerSession.Document.Text);
            Assert.Equal(
                sourceSession.BufferForCensus.ContentHash(),
                peerSession.BufferForCensus.ContentHash());

            Assert.True(ApplicationCommands.Undo.CanExecute(null, peerEditor.TextArea));
            ApplicationCommands.Undo.Execute(null, peerEditor.TextArea);

            Assert.Equal(sourceDeltas + 2, sourceSession.AppliedDeltaCount);
            Assert.Equal(peerDeltas + 2, peerSession.AppliedDeltaCount);
            Assert.Equal(original, sourceSession.Document.Text);
            Assert.Equal(original, peerSession.Document.Text);

            var samples = new double[31];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                long start = Stopwatch.GetTimestamp();
                sourceSession.Document.Insert(offset + sample, "x");
                samples[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            Array.Sort(samples);
            double p50 = samples[samples.Length / 2];
            _output.WriteLine($"W2-1 8 MiB two-pane exact-delta p50: {p50:F4} ms");
            Assert.True(p50 <= 30, $"8 MiB same-path propagation regressed to {p50:F4} ms p50");
            Assert.Equal(sourceDeltas + 2 + samples.Length, sourceSession.AppliedDeltaCount);
            Assert.Equal(peerDeltas + 2 + samples.Length, peerSession.AppliedDeltaCount);
            Assert.Equal(sourceRecoveries, sourceSession.DriftRecoveryCount);
            Assert.Equal(peerRecoveries, peerSession.DriftRecoveryCount);
            Assert.Equal(sourceSession.Document.Text, peerSession.Document.Text);
            Assert.Equal(
                sourceSession.BufferForCensus.ContentHash(),
                peerSession.BufferForCensus.ContentHash());
        });
    }

    [Fact]
    public void SavedBaselineDirtyState_TracksUndoSaveAndDirtyDuplicateSplitPeers()
    {
        RunOnSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "w2-saved-baseline");
            using VaultSession vault = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            vault.ScanInitial(cancel);
            using var workspace = new WorkspaceViewModel(vault, fixture.Root, () => [], _ => { });
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel source = Assert.IsType<WorkspaceTabViewModel>(
                workspace.ActiveGroup.ActiveTab);
            AvalonDocumentBufferSession sourceSession = Assert.IsType<AvalonDocumentBufferSession>(
                source.EditorSession);
            string savedText = source.Text;
            const string dirtySuffix = "\nunsaved duplicate baseline";

            sourceSession.Document.Insert(sourceSession.Document.TextLength, dirtySuffix);
            string dirtyText = savedText + dirtySuffix;
            AssertDirtyState(workspace, expectedDirty: true);

            string notePath = Path.Combine(fixture.Root, "note0.md");
            workspace.DuplicateTabCommand.Execute(null);
            WorkspaceTabViewModel duplicate = workspace.ActiveGroup.Tabs[1];
            Assert.Equal(dirtyText, duplicate.Text);
            AssertDirtyState(workspace, expectedDirty: true);

            workspace.SplitRightCommand.Execute(null);
            WorkspaceTabViewModel split = Assert.IsType<WorkspaceTabViewModel>(
                workspace.ActiveGroup.ActiveTab);
            AvalonDocumentBufferSession splitSession = Assert.IsType<AvalonDocumentBufferSession>(
                split.EditorSession);
            var splitEditor = new TextEditor { Document = splitSession.Document };
            Assert.Equal(3, workspace.Groups.Sum(group => group.Tabs.Count));
            AssertDirtyState(workspace, expectedDirty: true);

            splitSession.Document.Insert(splitSession.Document.TextLength, " temporary");
            ApplicationCommands.Undo.Execute(null, splitEditor.TextArea);
            Assert.Equal(dirtyText, split.Text);
            AssertDirtyState(workspace, expectedDirty: true);

            ApplicationCommands.Undo.Execute(null, splitEditor.TextArea);
            Assert.All(
                workspace.Groups.SelectMany(group => group.Tabs),
                tab => Assert.Equal(savedText, tab.Text));
            AssertDirtyState(workspace, expectedDirty: false);

            ApplicationCommands.Redo.Execute(null, splitEditor.TextArea);
            Assert.All(
                workspace.Groups.SelectMany(group => group.Tabs),
                tab => Assert.Equal(dirtyText, tab.Text));
            AssertDirtyState(workspace, expectedDirty: true);

            Assert.True(split.Save());
            AssertDirtyState(workspace, expectedDirty: false);

            var sourceEditor = new TextEditor { Document = sourceSession.Document };
            sourceSession.Document.Insert(sourceSession.Document.TextLength, " after save");
            AssertDirtyState(workspace, expectedDirty: true);
            ApplicationCommands.Undo.Execute(null, sourceEditor.TextArea);
            AssertDirtyState(workspace, expectedDirty: false);

            int compensatingOffset = sourceSession.Document.TextLength;
            sourceSession.Document.Insert(compensatingOffset, "x");
            AssertDirtyState(workspace, expectedDirty: true);
            sourceSession.Document.Remove(compensatingOffset, 1);
            Assert.False(sourceSession.Document.UndoStack.IsOriginalFile);
            Assert.All(
                workspace.Groups.SelectMany(group => group.Tabs),
                tab => Assert.Equal(dirtyText, tab.Text));
            AssertDirtyState(workspace, expectedDirty: false);

            const string externalDirtySuffix = " after external write";
            splitSession.Document.Insert(splitSession.Document.TextLength, externalDirtySuffix);
            string externalDirtyText = dirtyText + externalDirtySuffix;
            File.WriteAllText(notePath, "external bytes written after the source loaded");
            workspace.DuplicateTabCommand.Execute(null);
            WorkspaceTabViewModel externalPeer = workspace.ActiveGroup.Tabs[^1];
            AvalonDocumentBufferSession externalPeerSession = Assert.IsType<AvalonDocumentBufferSession>(
                externalPeer.EditorSession);
            Assert.Equal(dirtyText, externalPeerSession.SavedBaseline.Text);
            Assert.Equal(externalDirtyText, externalPeer.Text);
            AssertDirtyState(workspace, expectedDirty: true);

            var externalPeerEditor = new TextEditor { Document = externalPeerSession.Document };
            ApplicationCommands.Undo.Execute(null, externalPeerEditor.TextArea);
            Assert.All(
                workspace.Groups.SelectMany(group => group.Tabs),
                tab => Assert.Equal(dirtyText, tab.Text));
            AssertDirtyState(workspace, expectedDirty: false);
        });
    }

    [Fact]
    public void SavingFromAnotherPane_PreservesExistingPeerUndoRedoHistory()
    {
        RunOnSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "w2-peer-save-undo");
            using VaultSession vault = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            vault.ScanInitial(cancel);
            using var workspace = new WorkspaceViewModel(vault, fixture.Root, () => [], _ => { });
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel source = Assert.IsType<WorkspaceTabViewModel>(
                workspace.ActiveGroup.ActiveTab);
            workspace.DuplicateTabCommand.Execute(null);
            WorkspaceTabViewModel savingPeer = workspace.ActiveGroup.Tabs[1];
            AvalonDocumentBufferSession sourceSession = Assert.IsType<AvalonDocumentBufferSession>(
                source.EditorSession);
            var sourceEditor = new TextEditor { Document = sourceSession.Document };
            string original = source.Text;
            const string savedSuffix = "\nsaved from another pane";

            sourceSession.Document.Insert(sourceSession.Document.TextLength, savedSuffix);
            AssertDirtyState(workspace, expectedDirty: true);
            Assert.True(savingPeer.Save());
            AssertDirtyState(workspace, expectedDirty: false);

            Assert.True(ApplicationCommands.Undo.CanExecute(null, sourceEditor.TextArea));
            ApplicationCommands.Undo.Execute(null, sourceEditor.TextArea);
            Assert.All(
                workspace.Groups.SelectMany(group => group.Tabs),
                tab => Assert.Equal(original, tab.Text));
            AssertDirtyState(workspace, expectedDirty: true);

            Assert.True(ApplicationCommands.Redo.CanExecute(null, sourceEditor.TextArea));
            ApplicationCommands.Redo.Execute(null, sourceEditor.TextArea);
            Assert.All(
                workspace.Groups.SelectMany(group => group.Tabs),
                tab => Assert.Equal(original + savedSuffix, tab.Text));
            AssertDirtyState(workspace, expectedDirty: false);
        });
    }

    [Fact]
    public void GroupedTwoDeltaMove_RemainsOneUndoRedoUnitInEveryPane()
    {
        RunOnSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "w2-grouped-peer-undo");
            using VaultSession vault = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            vault.ScanInitial(cancel);
            using var workspace = new WorkspaceViewModel(vault, fixture.Root, () => [], _ => { });
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel source = Assert.IsType<WorkspaceTabViewModel>(
                workspace.ActiveGroup.ActiveTab);
            workspace.DuplicateTabCommand.Execute(null);
            WorkspaceTabViewModel peer = workspace.ActiveGroup.Tabs[1];
            AvalonDocumentBufferSession sourceSession = Assert.IsType<AvalonDocumentBufferSession>(
                source.EditorSession);
            AvalonDocumentBufferSession peerSession = Assert.IsType<AvalonDocumentBufferSession>(
                peer.EditorSession);
            var sourceEditor = new TextEditor { Document = sourceSession.Document };
            var peerEditor = new TextEditor { Document = peerSession.Document };
            string original = source.Text;
            string movedPrefix = original[..Math.Min(7, original.Length)];
            string moved = original[movedPrefix.Length..] + movedPrefix;
            long sourceDeltas = sourceSession.AppliedDeltaCount;
            long peerDeltas = peerSession.AppliedDeltaCount;

            using (sourceSession.Document.RunUpdate())
            {
                sourceSession.Document.Remove(0, movedPrefix.Length);
                sourceSession.Document.Insert(sourceSession.Document.TextLength, movedPrefix);
            }

            Assert.Equal(sourceDeltas + 2, sourceSession.AppliedDeltaCount);
            Assert.Equal(peerDeltas + 2, peerSession.AppliedDeltaCount);
            Assert.Equal(moved, source.Text);
            Assert.Equal(moved, peer.Text);
            AssertDirtyState(workspace, expectedDirty: true);

            ApplicationCommands.Undo.Execute(null, peerEditor.TextArea);
            Assert.Equal(original, source.Text);
            Assert.Equal(original, peer.Text);
            AssertDirtyState(workspace, expectedDirty: false);

            ApplicationCommands.Redo.Execute(null, peerEditor.TextArea);
            Assert.Equal(moved, source.Text);
            Assert.Equal(moved, peer.Text);
            AssertDirtyState(workspace, expectedDirty: true);

            ApplicationCommands.Undo.Execute(null, sourceEditor.TextArea);
            Assert.Equal(original, source.Text);
            Assert.Equal(original, peer.Text);
            AssertDirtyState(workspace, expectedDirty: false);
        });
    }

    [Fact]
    public void FlagFreeSameLengthDrift_IsFoundOnIdleAndAgainBeforeSave()
    {
        using var session = new AvalonDocumentBufferSession(
            "alpha",
            _ => { },
            TimeSpan.FromMilliseconds(10));

        session.BufferForCensus.ApplyEdit(0, 1, "A");
        session.Document.Replace(1, 1, "L");
        Assert.Equal(session.Document.TextLength, (int)session.BufferForCensus.LenUtf16());
        long recoveries = session.DriftRecoveryCount;
        RunDispatcherFor(TimeSpan.FromMilliseconds(250));
        Assert.Equal(recoveries + 1, session.DriftRecoveryCount);
        Assert.Equal(session.Document.Text, session.BufferForCensus.Text());

        session.BufferForCensus.ApplyEdit(0, 1, "A");
        EditorSaveSnapshot snapshot = session.PrepareSaveSnapshot();
        Assert.Equal("aLpha", snapshot.Text);
        Assert.Equal(session.Document.Text, session.BufferForCensus.Text());
        Assert.Equal(recoveries + 2, session.DriftRecoveryCount);
    }

    [Fact]
    public void FlagFreeSameLengthDrift_CannotBypassDirtySaveOrCloseGates()
    {
        RunOnSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "w2-drift-dirty-gates");
            using VaultSession vault = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            vault.ScanInitial(cancel);
            bool closePrompted = false;
            using var workspace = new WorkspaceViewModel(
                vault,
                fixture.Root,
                () => [],
                _ => { },
                dirtyCloseDecision: _ =>
                {
                    closePrompted = true;
                    return WorkspaceDirtyNavigationDecision.Cancel;
                });
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(
                workspace.ActiveGroup.ActiveTab);
            AvalonDocumentBufferSession session = Assert.IsType<AvalonDocumentBufferSession>(
                tab.EditorSession);
            string baseline = tab.Text;
            Assert.NotEmpty(baseline);
            string replacement = baseline[0] == 'X' ? "Y" : "X";

            session.Document.Replace(0, 1, replacement);
            string editedText = session.Document.Text;
            Assert.True(tab.IsDirty);

            session.BufferForCensus.Reset(baseline);
            using (session.Document.RunUpdate())
            {
                int end = session.Document.TextLength;
                session.Document.Insert(end, "x");
                session.Document.Remove(end, 1);
            }

            Assert.Equal(editedText, session.Document.Text);
            Assert.True(tab.IsDirty);
            RunDispatcherFor(TimeSpan.FromMilliseconds(750));
            Assert.Equal(editedText, session.BufferForCensus.Text());
            Assert.True(tab.IsDirty);

            workspace.CloseActiveTabCommand.Execute(null);
            Assert.True(closePrompted);
            Assert.Same(tab, workspace.ActiveGroup.ActiveTab);

            Assert.True(tab.Save());
            Assert.Equal(editedText, File.ReadAllText(Path.Combine(fixture.Root, "note0.md")));
            Assert.False(tab.IsDirty);
        });
    }

    [Fact]
    public void RevisionGateRetriesAnEditBetweenCompareAndSave_AndPersistsOnlyVerifiedText()
    {
        using FixtureVault fixture = FixtureVault.Create(1, "w2-revision-save");
        using VaultSession vault = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        vault.ScanInitial(cancel);
        using var workspace = new WorkspaceViewModel(vault, fixture.Root, () => [], _ => { });
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        AvalonDocumentBufferSession session = Assert.IsType<AvalonDocumentBufferSession>(tab.EditorSession);
        tab.Text = "ABCD";

        bool injected = false;
        session.BeforeSaveSnapshotAcquired = active =>
        {
            if (!injected)
            {
                injected = true;
                active.Document.Replace(0, 1, "Z");
            }
        };

        Assert.True(tab.Save());
        Assert.True(injected);
        Assert.Equal("ZBCD", File.ReadAllText(Path.Combine(fixture.Root, "note0.md")));
        Assert.Equal("ZBCD", session.BufferForCensus.Text());
        Assert.False(tab.IsDirty);
    }

    [Fact]
    public void RandomizedEditStorm_TracksAvalonDocumentByteForByte()
    {
        int operations = CensusTier.Scale(500, 2_000);
        var random = new Random(724);
        var tokens = new List<string>();
        using var session = new AvalonDocumentBufferSession(string.Empty, _ => { });
        long initialDeltas = session.AppliedDeltaCount;

        for (int operation = 0; operation < operations; operation++)
        {
            int action = tokens.Count == 0 ? 0 : random.Next(3);
            if (action == 0)
            {
                int tokenIndex = random.Next(tokens.Count + 1);
                string fragment = EditFragments[random.Next(EditFragments.Length)];
                session.Document.Insert(Utf16Offset(tokens, tokenIndex), fragment);
                tokens.Insert(tokenIndex, fragment);
            }
            else if (action == 1)
            {
                int tokenIndex = random.Next(tokens.Count);
                int offset = Utf16Offset(tokens, tokenIndex);
                session.Document.Remove(offset, tokens[tokenIndex].Length);
                tokens.RemoveAt(tokenIndex);
            }
            else
            {
                int tokenIndex = random.Next(tokens.Count);
                int offset = Utf16Offset(tokens, tokenIndex);
                string previous = tokens[tokenIndex];
                int fragmentIndex = (Array.IndexOf(EditFragments, previous) + 1) % EditFragments.Length;
                string replacement = EditFragments[fragmentIndex];
                session.Document.Replace(offset, previous.Length, replacement);
                tokens[tokenIndex] = replacement;
            }

            if (operation % 50 == 0)
            {
                Assert.True(session.VerifyIdleIntegrity());
            }
        }

        string expected = string.Concat(tokens);
        Assert.Equal(expected, session.Document.Text);
        Assert.Equal(expected, session.BufferForCensus.Text());
        Assert.Equal(initialDeltas + operations, session.AppliedDeltaCount);
        Assert.True(session.VerifyIdleIntegrity());
    }

    [Fact]
    public void DeltaFeedP50_RecordsFirstW2NumbersAgainstPinnedBudgets()
    {
        (int Bytes, double BudgetMs)[] cases =
        [
            (100 * 1024, 0.5),
            (1024 * 1024, 0.5),
            (8 * 1024 * 1024, 1.0),
        ];
        var medians = new List<double>();

        foreach ((int bytes, double budgetMs) in cases)
        {
            string fixture = SyntheticNote(bytes);
            int offset = NearestProseOffset(fixture);
            using var session = new AvalonDocumentBufferSession(
                fixture,
                _ => { },
                TimeSpan.FromHours(1));
            int hostOffset = offset;
            for (int warmup = 0; warmup < 12; warmup++)
            {
                session.Document.Insert(hostOffset, "x");
                hostOffset++;
            }

            var samples = new double[101];
            for (int sample = 0; sample < samples.Length; sample++)
            {
                long start = Stopwatch.GetTimestamp();
                session.Document.Insert(hostOffset, "x");
                hostOffset++;
                samples[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            Array.Sort(samples);
            double p50 = samples[samples.Length / 2];
            medians.Add(p50);
            bool withinBudget = p50 <= budgetMs;
            _output.WriteLine(
                $"W2-1 delta feed {bytes / 1024} KiB p50: {p50:F4} ms / {budgetMs:F1} ms: {(withinBudget ? "PASS" : "MISS")}");
        }

        double flatness = medians[2] / medians[1];
        _output.WriteLine(
            $"W2-1 8 MiB / 1 MiB flatness: {flatness:F2}x / 4.00x: {(flatness <= 4 ? "PASS" : "MISS")}");
    }

    private static string SyntheticNote(int targetBytes)
    {
        var note = new StringBuilder(targetBytes + 512);
        note.Append("---\ntitle: Big Note\ntags: [bench, editor]\n---\n\n");

        int section = 0;
        while (note.Length < targetBytes)
        {
            note.Append("## Section ").Append(section).Append("\n\n");
            note.Append("Prose with a [[Wikilink]] and #tag around a mid-sentence edit anchor.\n\n");
            note.Append("- [ ] a task\n- [x] a completed task\n\n");
            if (section % 4 == 0)
            {
                note.Append("```rust\nlet value = \"fenced content\";\n```\n\n");
            }

            section++;
        }

        return note.ToString();
    }

    private static void AssertDirtyState(WorkspaceViewModel workspace, bool expectedDirty)
    {
        Assert.All(
            workspace.Groups.SelectMany(group => group.Tabs),
            tab =>
            {
                Assert.Equal(expectedDirty, tab.IsDirty);
                AvalonDocumentBufferSession session = Assert.IsType<AvalonDocumentBufferSession>(
                    tab.EditorSession);
                Assert.Equal(!expectedDirty, session.IsAtSavedBaseline);
            });
    }
    private static string RepoRoot
    {
        get
        {
            string directory = AppContext.BaseDirectory;
            for (int level = 0; level < 8; level++)
            {
                directory = Path.GetDirectoryName(directory)!;
            }

            return directory;
        }
    }

    private static void AssertOffsetMappings(string text)
    {
        using var buffer = new DocumentBuffer(text);
        for (int utf16 = 0; utf16 <= text.Length; utf16++)
        {
            int snapped = utf16;
            if (utf16 > 0
                && utf16 < text.Length
                && char.IsHighSurrogate(text[utf16 - 1])
                && char.IsLowSurrogate(text[utf16]))
            {
                snapped--;
            }

            uint expectedByte = checked((uint)Encoding.UTF8.GetByteCount(text.AsSpan(0, snapped)));
            Assert.Equal(expectedByte, EditorOffsetMapper.Utf16ToByte(text, utf16));
            Assert.Equal((uint)snapped, EditorOffsetMapper.ByteToUtf16(buffer, expectedByte));
        }

        int byteOffset = 0;
        int utf16Offset = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            for (int interior = 0; interior < rune.Utf8SequenceLength; interior++)
            {
                Assert.Equal(
                    (uint)utf16Offset,
                    EditorOffsetMapper.ByteToUtf16(buffer, checked((uint)(byteOffset + interior))));
            }

            byteOffset += rune.Utf8SequenceLength;
            utf16Offset += rune.Utf16SequenceLength;
            Assert.Equal(
                (uint)utf16Offset,
                EditorOffsetMapper.ByteToUtf16(buffer, checked((uint)byteOffset)));
        }

        Assert.Equal(0U, EditorOffsetMapper.Utf16ToByte(text, -1));
        Assert.Equal((uint)byteOffset, EditorOffsetMapper.Utf16ToByte(text, int.MaxValue));
        Assert.Equal((uint)text.Length, EditorOffsetMapper.ByteToUtf16(buffer, uint.MaxValue));
    }

    private static void SetCompositionProperty(
        TextComposition composition,
        string propertyName,
        string value)
    {
        System.Reflection.MethodInfo setter = Assert.IsAssignableFrom<System.Reflection.MethodInfo>(
            typeof(TextComposition).GetProperty(propertyName)?.GetSetMethod(nonPublic: true));
        setter.Invoke(composition, [value]);
    }

    private static DragEventArgs CreateDropEventArgs(
        IDataObject data,
        DependencyObject target,
        Point point)
    {
        System.Reflection.ConstructorInfo constructor = Assert.IsAssignableFrom<System.Reflection.ConstructorInfo>(
            typeof(DragEventArgs).GetConstructor(
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic,
                binder: null,
                [
                    typeof(IDataObject),
                    typeof(DragDropKeyStates),
                    typeof(DragDropEffects),
                    typeof(DependencyObject),
                    typeof(Point),
                ],
                modifiers: null));
        return (DragEventArgs)constructor.Invoke(
            [data, DragDropKeyStates.None, DragDropEffects.Copy, target, point]);
    }

    /// <summary>#1839: one paste of <paramref name="text"/> through the real
    /// system clipboard, which another process on a busy desktop can take or
    /// change between the set and the paste; the paste then applies nothing.
    /// That attempt is retried. A clipboard that still holds the text and a
    /// paste that applies nothing fails at once: that is the editor's
    /// defect, not the desktop's. <paramref name="afterSetForTests"/> runs
    /// once, after the first set, <paramref name="beforePasteForTests"/> once,
    /// after the first check that the clipboard holds the text,
    /// <paramref name="afterTheCheckForTests"/> once, after the editor's paste
    /// has been checked and accepted, with that paste, <paramref name="beforeTheEditorReadsForTests"/>
    /// once, between the editor's own check and its read, and
    /// <paramref name="afterThePostForTests"/> once, after the first set has
    /// posted the text and before it is flushed, each standing in for that
    /// other process.</summary>
    private static void PasteThroughTheClipboard(
        TextEditor editor,
        AvalonDocumentBufferSession session,
        string text,
        Action? afterSetForTests = null,
        Action? beforePasteForTests = null,
        Action<DataObjectPastingEventArgs>? afterTheCheckForTests = null,
        Action? beforeTheEditorReadsForTests = null,
        Action? afterThePostForTests = null)
    {
        const int Attempts = 5;
        long before = session.AppliedDeltaCount;
        // The paste the editor is about to make: one carrying another
        // process's text is cancelled before it edits, and retried. One
        // carrying the census's text is accepted, and must then apply.
        bool foreign = false;
        bool accepted = false;
        void OnlyOurs(object sender, DataObjectPastingEventArgs args)
        {
            if (args.DataObject.GetData(DataFormats.UnicodeText) as string != text)
            {
                foreign = true;
                args.CancelCommand();
                return;
            }

            accepted = true;

            // The clipboard's data object is live and the editor reads it again
            // after this event, so it pastes a copy of the text just checked.
            var checkedText = new DataObject();
            checkedText.SetData(DataFormats.UnicodeText, text);
            args.DataObject = checkedText;
            afterTheCheckForTests?.Invoke(args);
            afterTheCheckForTests = null;
        }
        DataObject.AddPastingHandler(editor.TextArea, OnlyOurs);
        try
        {
            PasteUntilApplied();
        }
        finally
        {
            DataObject.RemovePastingHandler(editor.TextArea, OnlyOurs);
        }

        void PasteUntilApplied()
        {
            for (int attempt = 1; ; attempt++)
            {
                foreign = false;
                accepted = false;
                SetTheClipboard(TextData(text), afterThePostForTests);
                afterThePostForTests = null;
                afterSetForTests?.Invoke();
                afterSetForTests = null;
                editor.TextArea.Caret.Offset = session.Document.TextLength;
                bool held = ClipboardHolds(text);
                if (held)
                {
                    beforePasteForTests?.Invoke();
                    beforePasteForTests = null;
                    if (ApplicationCommands.Paste.CanExecute(null, editor.TextArea))
                    {
                        beforeTheEditorReadsForTests?.Invoke();
                        beforeTheEditorReadsForTests = null;
                        try
                        {
                            ApplicationCommands.Paste.Execute(null, editor.TextArea);
                        }
                        catch (ArgumentException) when (!accepted && session.AppliedDeltaCount == before && !ClipboardHolds(text))
                        {
                            // The clipboard lost its text after the editor's own last check
                            // and before it read the clipboard, and WPF refused to build a
                            // paste of text that was not there, before anything was edited.
                            foreign = true;
                        }
                    }
                    else
                    {
                        // The editor asks the clipboard itself: it refused because
                        // the text went (retried below), or it refused a paste the
                        // clipboard could serve (its defect).
                        Assert.False(ClipboardHolds(text), "the editor refused a paste the clipboard can serve");
                    }
                }

                if (session.AppliedDeltaCount != before)
                {
                    return;
                }

                // The editor was handed its own copy of the census's text, so
                // nothing another process did to the clipboard explains this.
                Assert.False(accepted, "the editor applied nothing from a paste of the census's text it had accepted");
                Assert.False(held && !foreign && ClipboardHolds(text), "the paste applied nothing, though the clipboard held its text throughout");
                Assert.True(attempt < Attempts, $"another process kept the clipboard through {Attempts} pastes");
                Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }
    }

    /// <summary>Another process copies files: the clipboard holds no text at all.</summary>
    private static void ReplaceTheClipboardWithFiles()
    {
        var files = new DataObject();
        files.SetFileDropList([Environment.ProcessPath!]);
        SetTheClipboard(files);
        Assert.True(
            Clipboard.ContainsFileDropList() && !Clipboard.ContainsText(),
            "premise: the clipboard holds files and no text");
    }

    /// <summary>Another process copies files just after the editor's own last
    /// check that the clipboard holds text, which its Paste binding makes as
    /// the command runs, and before the editor reads the clipboard. Returns
    /// whether that happened.</summary>
    private static Func<bool> ReplaceTheClipboardWithFilesAfterTheEditorChecks(TextEditor editor)
    {
        CommandBinding paste = editor.TextArea.CommandBindings
            .OfType<CommandBinding>()
            .Single(binding => binding.Command == ApplicationCommands.Paste);
        bool replaced = false;
        void AfterTheCheck(object sender, CanExecuteRoutedEventArgs args)
        {
            if (replaced || !args.CanExecute)
            {
                return;
            }

            replaced = true;
            paste.CanExecute -= AfterTheCheck;
            ReplaceTheClipboardWithFiles();
        }

        paste.CanExecute += AfterTheCheck;
        return () =>
        {
            paste.CanExecute -= AfterTheCheck;
            return replaced;
        };
    }

    /// <summary>#1839: puts <paramref name="data"/> on the system clipboard,
    /// to stay there once this thread is gone. WPF's copying set
    /// (<see cref="Clipboard.SetText(string)"/>, or
    /// <see cref="Clipboard.SetDataObject(object, bool)"/> with copy) posts the
    /// data and flushes it in one call, retrying the flush while it sleeps. A
    /// process that watches the clipboard (seen here: a remote desktop's
    /// clipboard sync) can open it to read the posted data before the flush, and
    /// that read waits for this thread to render the data, which it cannot do
    /// while it sleeps, so every flush fails with CLIPBRD_E_CANT_OPEN. So the
    /// data is posted without the flush, and the flush is retried here while
    /// this thread's messages run.</summary>
    private static void SetTheClipboard(IDataObject data, Action? afterThePostForTests = null)
    {
        WriteTheClipboard(() => Clipboard.SetDataObject(data, copy: false));
        afterThePostForTests?.Invoke();
        WriteTheClipboard(Clipboard.Flush);
    }

    /// <summary>A process that watches the clipboard reads the text just
    /// posted, before it is flushed: it opens the clipboard and asks for it,
    /// which keeps the clipboard open until this thread renders it. It asks for
    /// <see cref="WatchedFormat"/>, which only it reads, since a format a real
    /// watcher has already had rendered no longer waits on this thread.
    /// Returns the watcher, to be joined once the census is done.</summary>
    private static Thread ReadByAWatcher()
    {
        uint watched = RegisterClipboardFormat(WatchedFormat);
        Assert.True(watched != 0, "premise: the watched format is registered");
        bool openedIt = false;
        int tried = 0;
        var watcher = new Thread(() =>
        {
            for (int attempt = 0; attempt < 250 && !openedIt; attempt++)
            {
                openedIt = OpenClipboard(IntPtr.Zero);
                if (!openedIt)
                {
                    Thread.Sleep(20);
                }
            }

            Volatile.Write(ref tried, 1);
            if (!openedIt)
            {
                return;
            }

            try
            {
                _ = GetClipboardData(watched);
            }
            finally
            {
                _ = CloseClipboard();
            }
        })
        {
            IsBackground = true,
        };
        watcher.Start();
        // A wait on this thread that runs its messages would serve the
        // watcher's read here, before the flush meets it, so this one sleeps,
        // except while a real watcher (a window: this one opens with none) has
        // the clipboard open, waiting the same way for this thread to render.
        var clock = Stopwatch.StartNew();
        while (Volatile.Read(ref tried) == 0 && clock.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (GetOpenClipboardWindow() != IntPtr.Zero)
            {
                RunDispatcherFor(TimeSpan.FromMilliseconds(10));
            }
            else
            {
                Thread.Sleep(1);
            }
        }

        Assert.True(Volatile.Read(ref tried) == 1 && openedIt, "premise: the watcher opened the clipboard");
        return watcher;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    /// <summary>A format of the census's own, posted with its text, that only
    /// <see cref="ReadByAWatcher"/> asks for.</summary>
    private const string WatchedFormat = "SlateWindows.Tests.AvalonDocumentBufferCensus.Watched";

    private static DataObject TextData(string text)
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, text, autoConvert: true);
        data.SetData(WatchedFormat, text, autoConvert: false);
        return data;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string format);

    /// <summary>#1839: one write to the system clipboard. Another process can
    /// hold it open for longer than WPF's own retries (seen here: the desktop's
    /// clipboard history reading each write), and the write then fails with
    /// CLIPBRD_E_CANT_OPEN, so it is tried again a few times, with this thread's
    /// messages running in between so a read waiting on this thread can
    /// finish.</summary>
    private static void WriteTheClipboard(Action write)
    {
        const int Attempts = 10;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                write();
                return;
            }
            catch (System.Runtime.InteropServices.ExternalException) when (attempt < Attempts)
            {
                RunDispatcherFor(TimeSpan.FromMilliseconds(50 * attempt));
            }
            catch (System.Runtime.InteropServices.ExternalException e)
            {
                throw new InvalidOperationException($"the clipboard stayed open: {ClipboardHolder()}", e);
            }
        }
    }

    /// <summary>Who has the clipboard open now, for a failure message.</summary>
    private static string ClipboardHolder()
    {
        return $"open by {WindowsProcess(GetOpenClipboardWindow())}, owned by {WindowsProcess(GetClipboardOwner())}"
            + $" (this is process {Environment.ProcessId}, thread {GetCurrentThreadId()})";
    }

    private static string WindowsProcess(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return "no window";
        }

        uint thread = GetWindowThreadProcessId(window, out uint pid);
        string name;
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            name = process.ProcessName;
        }
        catch (ArgumentException)
        {
            name = "(exited)";
        }

        return $"process {pid} {name}, thread {thread}";
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetClipboardOwner();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetOpenClipboardWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    private static bool ClipboardHolds(string text)
    {
        try
        {
            return Clipboard.ContainsText() && Clipboard.GetText() == text;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    /// <summary>#1839: a copy of the reader's clipboard, taken before the census
    /// writes to it. <see cref="Clipboard.GetDataObject"/> hands back the live
    /// clipboard, which by the time it is restored reads the census's own text,
    /// and setting it back makes the clipboard serve itself, which can keep it
    /// locked through every retry. Each format that can be read is copied; one
    /// that still cannot be read after a few tries is left out.</summary>
    private static IDataObject? TryGetClipboardData()
    {
        IDataObject live;
        try
        {
            live = Clipboard.GetDataObject();
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }

        if (live is null)
        {
            return null;
        }

        var copy = new DataObject();
        string[] formats;
        try
        {
            formats = live.GetFormats(autoConvert: false);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }

        foreach (string format in formats)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    object? data = live.GetData(format, autoConvert: false);
                    if (data is not null)
                    {
                        copy.SetData(format, data, autoConvert: false);
                    }

                    break;
                }
                catch (System.Runtime.InteropServices.ExternalException) when (attempt < 3)
                {
                    RunDispatcherFor(TimeSpan.FromMilliseconds(50 * attempt));
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // A format another process renders on demand and still
                    // cannot render is left out: failing the census on the
                    // reader's clipboard would be the desktop flake #1839 removes.
                }
            }
        }

        return copy.GetFormats(autoConvert: false).Length == 0 ? null : copy;
    }

    private static void RestoreClipboard(IDataObject? previous)
    {
        if (previous is null)
        {
            WriteTheClipboard(Clipboard.Clear);
            return;
        }

        SetTheClipboard(previous);
    }

    private static int NearestProseOffset(string document)
    {
        const string anchor = "mid-sentence";
        int target = document.Length / 2;
        int afterTarget = document.IndexOf(anchor, target, StringComparison.Ordinal);
        if (afterTarget >= 0)
        {
            return afterTarget;
        }

        int beforeTarget = document.LastIndexOf(anchor, target, StringComparison.Ordinal);
        return beforeTarget >= 0 ? beforeTarget : target;
    }

    private static int Utf16Offset(IReadOnlyList<string> tokens, int tokenIndex)
    {
        int offset = 0;
        for (int index = 0; index < tokenIndex; index++)
        {
            offset += tokens[index].Length;
        }

        return offset;
    }

    private sealed class EditorSurface : IDisposable
    {
        private readonly Window _window;

        public EditorSurface(TextEditor editor)
        {
            _window = new Window
            {
                Content = editor,
                Width = 480,
                Height = 240,
                Left = -10_000,
                Top = -10_000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
            };
            _window.Show();
            _window.UpdateLayout();
            editor.Focus();
            Keyboard.Focus(editor.TextArea);
        }

        public void Dispose()
        {
            _window.Close();
        }
    }

    private static void RunDispatcherFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = duration,
        };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action action) =>
        StaThread.Run(action, TimeSpan.FromSeconds(30), "AvalonEdit STA census did not finish within 30 seconds.");
}
