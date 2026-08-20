// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using SlateWindows.Templates;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-3 (#743): create-from-template — the picker over core
/// `list_templates`, the prompt/name flow, the exclusive create, and
/// the caret park. Contracts: docs/plans/30_templates_contracts.md
/// (T1–T13); the mac twins are `TemplatePicker.swift`,
/// `TemplatePromptSheet.swift`, and `AppState.swift:22867-23624`.
/// </summary>
public sealed class TemplateFlowTests
{
    // ---- The picker (T3, T10) -----------------------------------------

    [Fact]
    public void PickerListsCoreOrderWithDescriptionsAndAnnouncesTheCanonicalCount()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-picker");
            WriteTemplate(
                fixture.Root, "beta.md",
                "---\ndescription: Second one\n---\nBody.\n");
            WriteTemplate(fixture.Root, "Alpha.md", "First line doubles as blurb.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add,
                startInteractionBackgroundWork: false);

            workspace.OpenTemplatePicker();

            TemplatePickerViewModel picker = workspace.TemplatePickerSheet!;
            Assert.NotNull(picker);
            Assert.Equal(TemplatePickerState.Available, picker.State);
            // Core's order (case-insensitive by name), consumed verbatim
            // — the host never re-sorts (T1).
            Assert.Equal(
                ["Alpha", "beta"],
                picker.Rows.Select(row => row.Name).ToArray());
            Assert.Equal(
                "First line doubles as blurb.",
                picker.Rows[0].Description);
            Assert.Equal("Second one", picker.Rows[1].Description);
            // mac's rowAccessibilityLabel appends its own period, so a
            // description that already ends with one doubles it — that
            // is the mac-verbatim composition, pinned as-is.
            Assert.Equal(
                "Alpha. First line doubles as blurb..",
                picker.Rows[0].AccessibleName);

            A11yEvent opened = Assert.Single(
                announced, item => item is A11yEvent.TemplatePickerOpened);
            Assert.Equal(
                "Template picker opened. 2 templates available.",
                SlateUniffiMethods.A11yRender(opened).Text);
        });
    }

    [Fact]
    public void PickerWithoutATemplatesFolderPresentsEmptyAnnouncesTheReasonAndRetryRecovers()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-empty");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add,
                startInteractionBackgroundWork: false);

            workspace.OpenTemplatePicker();

            TemplatePickerViewModel picker = workspace.TemplatePickerSheet!;
            Assert.Equal(TemplatePickerState.Empty, picker.State);
            A11yEvent reason = Assert.Single(
                announced, item => item is A11yEvent.HostComposed);
            Assert.Equal(
                TemplatePickerViewModel.EmptyReason,
                SlateUniffiMethods.A11yRender(reason).Text);

            // A template created while the picker is up: Try Again
            // re-enumerates in place — same sheet, fresh list (T3).
            WriteTemplate(fixture.Root, "Fresh.md", "Hello.\n");
            picker.RetryCommand.Execute(null);
            Assert.Equal(TemplatePickerState.Available, picker.State);
            Assert.Equal("Fresh", Assert.Single(picker.Rows).Name);
            Assert.Contains(
                announced, item => item is A11yEvent.TemplatePickerOpened);
        });
    }

    [Fact]
    public void AReadFailureAtSelectCancelsTheWholeFlow()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-gone");
            WriteTemplate(fixture.Root, "Doomed.md", "Body.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            workspace.OpenTemplatePicker();
            TemplatePickerViewModel picker = workspace.TemplatePickerSheet!;
            TemplatePickerRowViewModel row = Assert.Single(picker.Rows);

            // Deleted between list and select: the terminal reset, not
            // an error sheet (T3; mac performSelectTemplate).
            File.Delete(Path.Combine(fixture.Root, "Templates", "Doomed.md"));
            picker.ActivateCommand.Execute(row);

            Assert.Null(workspace.TemplatePickerSheet);
            Assert.Null(workspace.TemplateFlowSheet);
        });
    }

    // ---- The prompt step (T4) -----------------------------------------

    [Fact]
    public void MetadataDrivesThePromptSequenceInDeclarationOrderSeededEmpty()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-prompts");
            WriteTemplate(
                fixture.Root, "Meeting.md",
                "# {{prompt:Topic}}\nWith {{prompt:Attendees}} on {{prompt:Date}}\n"
                + "and {{prompt:Topic}} again.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Meeting");

            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            Assert.Null(workspace.TemplatePickerSheet);
            Assert.Equal(TemplateFlowStep.Prompts, flow.Step);
            // Declaration order, labels verbatim, duplicates deduped
            // (core's extract_template_metadata, consumed as-is — T1).
            Assert.Equal(
                ["Topic", "Attendees", "Date"],
                flow.PromptFields.Select(field => field.Label).ToArray());
            Assert.All(
                flow.PromptFields,
                field => Assert.Equal(string.Empty, field.Value));
        });
    }

    [Fact]
    public void APromptlessTemplateSkipsStraightToTheNameStep()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-fastpath");
            WriteTemplate(fixture.Root, "Plain.md", "# {{title}}\nNo prompts here.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Plain");

            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            Assert.Equal(TemplateFlowStep.Name, flow.Step);
            Assert.Empty(flow.PromptFields);
            Assert.Equal("Plain.md", flow.NoteName);
        });
    }

    // ---- The create (T7) + cursor (T8) --------------------------------

    [Fact]
    public void TheFullFlowRendersWritesOpensAndParksTheCaretWhereRequested()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-create");
            WriteTemplate(
                fixture.Root, "Meeting.md",
                "# {{title}}\nTopic: {{prompt:Topic}}\nNotes: {{prompt:Extra}}\n"
                + "Café {{cursor}}tail\n");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add,
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Meeting");
            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            // One prompt answered, one left untouched: the untouched
            // field substitutes EMPTY, never a literal marker (T4).
            flow.PromptFields.Single(field => field.Label == "Topic").Value =
                "Q3 review";
            flow.NextCommand.Execute(null);
            Assert.Equal(TemplateFlowStep.Name, flow.Step);

            flow.NoteName = "Standup Café";
            flow.CreateCommand.Execute(null);

            Assert.Null(workspace.TemplateFlowSheet);
            string createdPath = Path.Combine(fixture.Root, "Standup Café.md");
            string expected =
                "# Standup Café\nTopic: Q3 review\nNotes: \nCafé tail\n";
            Assert.Equal(expected, File.ReadAllText(createdPath));

            // The created announcement, canonical and High (T10).
            A11yEvent created = Assert.Single(
                announced, item => item is A11yEvent.TemplateNoteCreated);
            RenderedAnnouncement rendered = SlateUniffiMethods.A11yRender(created);
            Assert.Equal("Created Standup Café.md from Meeting.", rendered.Text);
            Assert.Equal(A11yPriority.High, rendered.Priority);

            // The note opened in the current tab and the caret sits at
            // the `{{cursor}}` site — after a multibyte char, so this
            // pins the UTF-8→UTF-16 conversion (T8).
            WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
            Assert.Equal("Standup Café.md", tab.Path);
            Assert.Equal(tab.Text.IndexOf("tail", StringComparison.Ordinal), tab.EditorCaretOffset);
        });
    }

    [Fact]
    public void ANoMarkerTemplateParksTheCaretAtTheEndOfTheDocument()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-nocursor");
            WriteTemplate(fixture.Root, "Plain.md", "Line one.\nLine two.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Plain");
            workspace.TemplateFlowSheet!.CreateCommand.Execute(null);

            WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
            Assert.Equal("Plain.md", tab.Path);
            Assert.Equal(tab.Text.Length, tab.EditorCaretOffset);
        });
    }

    [Fact]
    public void TheDestinationFreezesAtOpenAndCreatesUnderIt()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-dest");
            WriteTemplate(fixture.Root, "Plain.md", "Body.\n");
            Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);
            string destination = "sub";
            workspace.TemplateCreationParentProvider = () => destination;

            workspace.OpenTemplatePicker();
            // Selection changes AFTER open must not move the target
            // (T12: frozen at open).
            destination = "elsewhere";
            TemplatePickerViewModel picker = workspace.TemplatePickerSheet!;
            Assert.Equal("sub", picker.DestinationDescription);
            picker.ActivateCommand.Execute(Assert.Single(picker.Rows));
            workspace.TemplateFlowSheet!.CreateCommand.Execute(null);

            Assert.True(File.Exists(Path.Combine(fixture.Root, "sub", "Plain.md")));
        });
    }

    [Fact]
    public void AnOccupiedDestinationRepresentsTheNameStepWithCoresMessageAndTheRetryName()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-conflict");
            WriteTemplate(fixture.Root, "Plain.md", "Fresh body.\n");
            string occupiedPath = Path.Combine(fixture.Root, "Taken.md");
            File.WriteAllText(occupiedPath, "Original stays.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Plain");
            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            flow.NoteName = "Taken";
            flow.CreateCommand.Execute(null);

            // The sheet stays up at the name step with the user's exact
            // entry and core's message relayed verbatim (T7) — and the
            // occupied file is byte-untouched (the no-clobber pin).
            Assert.Same(flow, workspace.TemplateFlowSheet);
            Assert.Equal(TemplateFlowStep.Name, flow.Step);
            Assert.Equal("Taken", flow.NoteName);
            Assert.NotNull(flow.ValidationError);
            Assert.Equal("Original stays.\n", File.ReadAllText(occupiedPath));
        });
    }

    [Fact]
    public void CancellationAtEveryStepWritesNothing()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-cancel");
            WriteTemplate(
                fixture.Root, "Meeting.md", "{{prompt:Topic}} {{cursor}}\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);
            string[] before = VaultFiles(fixture.Root);

            // Cancel at the picker.
            workspace.OpenTemplatePicker();
            workspace.TemplatePickerSheet!.CancelCommand.Execute(null);
            Assert.Null(workspace.TemplatePickerSheet);

            // Cancel at the prompt step.
            OpenFlowFor(workspace, "Meeting");
            Assert.Equal(
                TemplateFlowStep.Prompts, workspace.TemplateFlowSheet!.Step);
            workspace.TemplateFlowSheet.CancelCommand.Execute(null);
            Assert.Null(workspace.TemplateFlowSheet);

            // Cancel at the name step, with edits staged.
            OpenFlowFor(workspace, "Meeting");
            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            flow.PromptFields[0].Value = "Discarded";
            flow.NextCommand.Execute(null);
            flow.NoteName = "Never Written";
            flow.CancelCommand.Execute(null);
            Assert.Null(workspace.TemplateFlowSheet);

            // The vault is byte-identical: the only write in the whole
            // flow is the explicit Create (T7, pinned).
            Assert.Equal(before, VaultFiles(fixture.Root));
        });
    }

    [Fact]
    public void ReentryWhileAFlowIsUpIsRefused()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-reentry");
            WriteTemplate(fixture.Root, "Plain.md", "Body.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Plain");
            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;

            workspace.OpenTemplatePicker();

            // The open was refused: no picker appeared and the live
            // flow was not restarted out from under its sheet (T9).
            Assert.Null(workspace.TemplatePickerSheet);
            Assert.Same(flow, workspace.TemplateFlowSheet);
        });
    }

    [Fact]
    public void ValidationErrorsAreInlineVerbatimAndNeverAnnounced()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-validate");
            WriteTemplate(fixture.Root, "Plain.md", "Body.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add,
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Plain");
            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            int announcedBefore = announced.Count;
            flow.NoteName = "../escape";
            flow.CreateCommand.Execute(null);

            Assert.Equal(
                "Note name cannot contain `..` segments.", flow.ValidationError);
            Assert.Same(flow, workspace.TemplateFlowSheet);
            Assert.Equal(announcedBefore, announced.Count);
        });
    }

    // ---- The pure rules (T6, T8) --------------------------------------

    [Theory]
    [InlineData("Daily", true)]
    [InlineData("daily", true)]
    [InlineData("Daily Standup", true)]
    [InlineData("Daily-notes", true)]
    [InlineData("Daily.md", true)]
    [InlineData("Dailyness", false)]
    [InlineData("Daily123", false)]
    [InlineData("Weekly", false)]
    public void DefaultNoteNameAppliesTheDailyWordRule(string name, bool dated)
    {
        var utcNow = new DateTime(2026, 8, 20, 17, 0, 0, DateTimeKind.Utc);
        string expected = dated ? $"{name} 2026-08-20.md" : $"{name}.md";
        Assert.Equal(expected, TemplateNameRules.DefaultNoteName(name, utcNow));
    }

    [Theory]
    [InlineData("Note", "Note.md")]
    [InlineData("Note.md", "Note.md")]
    [InlineData("Note.MD", "Note.MD")]
    [InlineData("archive.tar.MD", "archive.tar.MD")]
    [InlineData("archive.tar", "archive.tar.md")]
    [InlineData("Note.txt", "Note.txt.md")]
    public void NormalizeNoteNamePreservesExistingMdExtensions(
        string entered, string expected) =>
        Assert.Equal(expected, TemplateNameRules.NormalizeNoteName(entered));

    [Theory]
    [InlineData("", "Note name cannot be empty.")]
    [InlineData(".", "Note name cannot be `.` or `..`.")]
    [InlineData("..", "Note name cannot be `.` or `..`.")]
    [InlineData("/abs", "Note name must be vault-relative, not absolute.")]
    [InlineData("\\abs", "Note name must be vault-relative, not absolute.")]
    [InlineData("C:/abs", "Note name must be vault-relative, not absolute.")]
    [InlineData("c:\\abs", "Note name must be vault-relative, not absolute.")]
    [InlineData("../up", "Note name cannot contain `..` segments.")]
    [InlineData("a/../b", "Note name cannot contain `..` segments.")]
    public void ValidateRejectsTheMacInvalidShapes(string candidate, string expected) =>
        Assert.Equal(expected, TemplateNameRules.Validate(candidate));

    [Theory]
    [InlineData("plain", "plain sub/nested", "deep/plain.md")]
    public void ValidateAcceptsOrdinaryRelativeNames(params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            Assert.Null(TemplateNameRules.Validate(candidate));
        }
    }

    [Fact]
    public void CaretIndexConvertsUtf8BytesToUtf16Units()
    {
        // ASCII: bytes and UTF-16 units coincide.
        Assert.Equal(3, TemplateCursor.CaretIndex("abcdef", 3));
        // é is 2 UTF-8 bytes, 1 UTF-16 unit: byte 6 sits after "Café ".
        Assert.Equal(5, TemplateCursor.CaretIndex("Café tail", 6));
        // 𝄞 is 4 UTF-8 bytes, 2 UTF-16 units (a surrogate pair).
        Assert.Equal(2, TemplateCursor.CaretIndex("𝄞x", 4));
        // No marker: the end of the document (T8).
        Assert.Equal(9, TemplateCursor.CaretIndex("Café tail", null));
        // Out of range clamps to the end rather than throwing.
        Assert.Equal(4, TemplateCursor.CaretIndex("abcd", 99));
    }

    [Fact]
    public void TheCaretParkSurvivesEditorMaterialization()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(1, "tpl-materialize");
            // The journey's exact fixture shape: template frontmatter
            // renders through into the note, and the multibyte char
            // sits before {{cursor}} so a byte/UTF-16 confusion moves
            // the caret visibly.
            WriteTemplate(
                fixture.Root, "Meeting.md",
                "---\ndescription: Journey fixture\n---\n# {{title}}\n\n"
                + "Topic: {{prompt:Topic}}\n\nCafé {{cursor}}end\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);

            OpenFlowFor(workspace, "Meeting");
            TemplateFlowViewModel flow = workspace.TemplateFlowSheet!;
            flow.PromptFields[0].Value = "Quarterly sync";
            flow.NextCommand.Execute(null);
            flow.CreateCommand.Execute(null);

            WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
            int expected = tab.Text.IndexOf("end", StringComparison.Ordinal);
            Assert.Equal(expected, tab.EditorCaretOffset);

            // Materialize a REAL editor bound the way the tab template
            // binds it (WorkspaceTemplates.xaml:390-393) AFTER the park
            // — the journey found the live caret at 0 while the VM-only
            // assertion above stayed green, so this is the regression
            // gate for whatever the view layer does to a parked caret
            // during materialization.
            var editor = new SlateTextEditor();
            // DataContext FIRST, then the caret binding BEFORE the
            // document binding: each SetBinding then transfers
            // immediately, so the caret value arrives while the editor
            // still holds its default empty document — the order the
            // live template materialization was MEASURED applying them
            // (the journey's probe: caretDpChanged new=89 with
            // doclen=0, then the destructive clamp wrote 0 back
            // through the TwoWay binding and poisoned the tab). WPF
            // does not promise attribute-order application on a fresh
            // template, so the editor must survive either order.
            editor.DataContext = tab;
            _ = editor.SetBinding(
                SlateTextEditor.EditorCaretOffsetProperty,
                new System.Windows.Data.Binding("EditorCaretOffset")
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger =
                        System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
                });
            _ = editor.SetBinding(
                ICSharpCode.AvalonEdit.TextEditor.DocumentProperty,
                new System.Windows.Data.Binding("EditorDocument"));
            _ = editor.SetBinding(
                SlateTextEditor.HighlightSessionProperty,
                new System.Windows.Data.Binding("EditorSession"));
            _ = editor.SetBinding(
                SlateTextEditor.InteractionSessionProperty,
                new System.Windows.Data.Binding("EditorInteractions"));
            var window = new System.Windows.Window
            {
                Content = editor,
                Width = 500,
                Height = 300,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
            };
            window.Show();
            try
            {
                WaitForUi(() => editor.IsLoaded);
                Assert.Equal(expected, editor.CaretOffset);
                Assert.Equal(expected, tab.EditorCaretOffset);
            }
            finally
            {
                window.Close();
            }
        });
    }

    // ---- Helpers ------------------------------------------------------

    private static void WriteTemplate(string root, string name, string body)
    {
        string dir = Path.Combine(root, "Templates");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), body);
    }

    /// <summary>Open the picker and activate the named template.</summary>
    private static void OpenFlowFor(WorkspaceViewModel workspace, string name)
    {
        workspace.OpenTemplatePicker();
        TemplatePickerViewModel picker = workspace.TemplatePickerSheet!;
        Assert.NotNull(picker);
        TemplatePickerRowViewModel row =
            picker.Rows.Single(candidate => candidate.Name == name);
        picker.ActivateCommand.Execute(row);
    }

    /// <summary>Every user-visible vault file with its content — the
    /// byte-identity snapshot the cancellation fact compares. The
    /// `.slate` index directory is excluded: reads may legitimately
    /// touch it, and the T7 pin is about the user's files.</summary>
    private static string[] VaultFiles(string root) =>
        [.. Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path
                .Replace('\\', '/')
                .Contains("/.slate/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"{path}|{File.ReadAllText(path)}")];

    private static VaultSession OpenScanned(string root)
    {
        var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous action timed out.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
