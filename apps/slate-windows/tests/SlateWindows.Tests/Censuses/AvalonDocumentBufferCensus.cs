// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;
using Xunit.Abstractions;

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
            using var surface = new EditorSurface(editor);
            AutomationPeer peer = Assert.IsType<SlateTextEditorAutomationPeer>(
                UIElementAutomationPeer.CreatePeerForElement(editor));

            Assert.Equal(AutomationControlType.Document, peer.GetAutomationControlType());
            Assert.Same(peer, peer.GetPattern(PatternInterface.Value));
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
                Clipboard.SetText("\r\npasted 😀");
                editor.TextArea.Caret.Offset = session.Document.TextLength;
                Assert.True(ApplicationCommands.Paste.CanExecute(null, editor.TextArea));
                ApplicationCommands.Paste.Execute(null, editor.TextArea);
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

    private static IDataObject? TryGetClipboardData()
    {
        try
        {
            return Clipboard.GetDataObject();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static void RestoreClipboard(IDataObject? previous)
    {
        if (previous is null)
        {
            Clipboard.Clear();
            return;
        }

        Clipboard.SetDataObject(previous);
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

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("AvalonEdit STA census did not finish within 30 seconds.");
        }

        failure?.Throw();
    }
}
