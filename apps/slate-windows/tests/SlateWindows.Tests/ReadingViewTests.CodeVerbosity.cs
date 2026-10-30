// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Documents;
using SlateWindows.Reading;
using uniffi.slate_uniffi;
using AutomationPeer = System.Windows.Automation.Peers.AutomationPeer;

namespace SlateWindows.Tests;

/// <summary>WB-21b (#1526): the code block's peer speaks core's preamble at
/// the reader's verbosity — the first line or the whole block after the
/// preamble — while the in-range caption keeps the short preamble, and every
/// route that makes a Reading model projects at the saved verbosity.</summary>
public sealed partial class ReadingViewTests
{
    private const string RustFence = "```rust\nfn main() {\n}\n```\n";

    [Fact]
    public void ACodeBlockPeerSpeaksTheFirstLineUnderThatVerbosity()
    {
        RunSta(() =>
        {
            FlowDocument document = BuildSourceAt(RustFence, CodePreambleVerbosity.FirstLine);

            Assert.Equal("Code block, rust, 2 lines. fn main() {", CodePeerName(document));
            // The in-range caption is the short preamble: the code itself
            // follows in range, so the verbose name there would read it twice.
            string text = new TextRange(document.ContentStart, document.ContentEnd).Text;
            Assert.Contains("Code block, rust, 2 lines.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("2 lines. fn main", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ACodeBlockPeerSpeaksEveryTokenUnderThatVerbosity()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-all-tokens");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), RustFence);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            using var preferences = new EditorPreferencesViewModel(_ => { }, new FakeEditorSpellingService());
            preferences.SetCodePreambleVerbosityCommand.Execute("preambleAllTokens");

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                editorPreferences: preferences,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            // Coherent: the saved block's tokens colour the code, and the
            // name is the short preamble, a space and the interior.
            Assert.True(FindCodeParagraph(surface.Document).Inlines.Count > 1, "premise: the fence is highlighted");
            Assert.Equal(
                SlateUniffiMethods.CodeBlockPreamble("rust", "fn main() {\n}") + " fn main() {\n}",
                CodePeerName(surface.Document));

            // Incoherent (an unsaved edit inside the fence): the live
            // interior, never the saved source.
            tab.EditorDocument!.Replace(tab.Text.IndexOf("main", StringComparison.Ordinal), 4, "live");
            tab.Reading!.Refresh();
            Assert.Single(FindCodeParagraph(surface.Document).Inlines);
            Assert.Equal("Code block, rust, 2 lines. fn live() {\n}", CodePeerName(surface.Document));
        });
    }

    [Fact]
    public void ChangingTheCodeVerbosityReprojectsOpenReadingViews()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-verbosity-change");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), "Before.\n\n" + RustFence);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session,
                fixture.Root,
                () => [],
                announced.Add,
                startInteractionBackgroundWork: false);
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel reader = workspace.ActiveGroup.ActiveTab!;
            reader.ToggleViewMode();
            var surface = new ReadingSurface { Model = reader.Reading };
            Assert.Equal("Code block, rust, 2 lines.", CodePeerName(surface.Document));
            string textBefore = SurfaceText(surface);
            announced.Clear();

            workspace.EditorPreferences.SetCodePreambleVerbosityCommand.Execute("preambleFirstLine");
            PumpedDispatcher.Drain();

            Assert.Equal("Code block, rust, 2 lines. fn main() {", CodePeerName(surface.Document));
            Assert.Equal(textBefore, SurfaceText(surface));
            Assert.Equal(
                "Code preamble verbosity: Preamble + first line.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>Red team round 1: a reading tab hidden behind an editor tab
    /// when the setting changes is unbound, and the surface's rebind fast
    /// path reuses a complete projection without projecting — so the change
    /// must mark it stale, and showing it again speaks the new verbosity.</summary>
    [Fact]
    public void AHiddenReadingTabSpeaksTheNewVerbosityWhenShownAgain()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-verbosity-hidden");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), "Before.\n\n" + RustFence);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            using var workspace = new WorkspaceViewModel(
                session,
                fixture.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false);
            workspace.OpenPath("note0.md");
            WorkspaceTabViewModel reader = workspace.ActiveGroup.ActiveTab!;
            reader.ToggleViewMode();
            ReadingContentViewModel model = reader.Reading!;
            var surface = new ReadingSurface { Model = model };
            FlowDocument shown = surface.Document;
            Assert.Equal("Code block, rust, 2 lines.", CodePeerName(shown));

            // Another tab's editor takes the surface (its Reading is null).
            surface.Model = null;
            workspace.EditorPreferences.SetCodePreambleVerbosityCommand.Execute("preambleFirstLine");
            PumpedDispatcher.Drain();
            surface.Model = model;
            PumpedDispatcher.Drain();

            Assert.Equal("Code block, rust, 2 lines. fn main() {", CodePeerName(surface.Document));
        });
    }

    [Fact]
    public void ARestoredReadingTabProjectsAtTheSavedVerbosity()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-restored");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), RustFence);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            using var saved = SavedVerbosity.Holding("preambleFirstLine");

            // The restored-reading branch projects inside the constructor.
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md"),
                    Mode: "reading"),
                editorPreferences: saved.Preferences,
                startInteractionBackgroundWork: false);

            Assert.Equal("Code block, rust, 2 lines. fn main() {", CodePeerName(tab.Reading!.Document!));
        });
    }

    [Fact]
    public void AReadingViewOpenedByTheToggleProjectsAtTheSavedVerbosity()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-code-toggle");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), RustFence);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            using var saved = SavedVerbosity.Holding("preambleAllTokens");

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                editorPreferences: saved.Preferences,
                startInteractionBackgroundWork: false);
            Assert.Null(tab.Reading);
            tab.ToggleViewMode();

            string name = CodePeerName(tab.Reading!.Document!);
            Assert.Equal("Code block, rust, 2 lines. fn main() {\n}", name);
            Assert.NotEqual("Code block, rust, 2 lines.", name);
        });
    }

    [Fact]
    public void AReplacedReadingModelProjectsAtTheSavedVerbosity()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(2, "reading-code-replaced");
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), RustFence);
            File.WriteAllText(Path.Combine(fixture.Root, "note1.md"), "```rust\nfn other() {\n}\n```\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);
            using var saved = SavedVerbosity.Holding("preambleFirstLine");

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md"),
                    Mode: "reading"),
                editorPreferences: saved.Preferences,
                startInteractionBackgroundWork: false);
            ReadingContentViewModel first = tab.Reading!;

            tab.ReplaceItem(new WorkspaceItemState(WorkspaceItemKind.Markdown, "note1.md"));

            Assert.NotSame(first, tab.Reading);
            Assert.Equal("Code block, rust, 2 lines. fn other() {", CodePeerName(tab.Reading!.Document!));
        });
    }

    private static FlowDocument BuildSourceAt(string source, CodePreambleVerbosity verbosity)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        var model = new List<(ReadingBlock, ReadingBlockInlines)>();
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            model.Add((blocks[i], inlines[i]));
        }
        return ReadingDocumentBuilder.Build(
            model, new ReadingListBuildContext { CodePreambleVerbosity = verbosity }).Document;
    }

    /// <summary>The single code block's UIA peer name, found the way the
    /// surface's structural walk finds it.</summary>
    private static string CodePeerName(FlowDocument document)
    {
        var children = new List<AutomationPeer>();
        foreach (Block block in document.Blocks)
        {
            ReadingSurfacePeer.AppendStructuralForTest(block, children);
        }
        return Assert.Single(children, candidate => candidate is ReadingCodeBlockPeer).GetName();
    }

    /// <summary>An editor-preferences object over a temp store holding a
    /// saved verbosity key, as the workspace would load it.</summary>
    private sealed class SavedVerbosity : IDisposable
    {
        private readonly string _directory;

        private SavedVerbosity(string key)
        {
            _directory = Path.Combine(Path.GetTempPath(), $"slate-code-verbosity-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            var store = new AppPreferencesStore(Path.Combine(_directory, "preferences.json"));
            store.Save(store.Load() with { CodePreambleVerbosity = key });
            Preferences = new EditorPreferencesViewModel(_ => { }, new FakeEditorSpellingService(), preferencesStore: store);
        }

        public static SavedVerbosity Holding(string key) => new(key);

        public EditorPreferencesViewModel Preferences { get; }

        public void Dispose()
        {
            Preferences.Dispose();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
