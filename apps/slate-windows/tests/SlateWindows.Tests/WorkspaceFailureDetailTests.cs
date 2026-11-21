// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows.Panels;
using SlateWindows.Templates;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-15c (#1890, CD9): the workspace's own failure sentences say
/// core's detail after their lead, never the binding's field dump
/// (<c>@message=…</c>, <c>@path=…</c>, <c>@newContentHash=…</c>): the
/// bibliography source's notice, the history panel's restore, Restore As and
/// recover alerts, a property write's spoken failure (the wrapper of an
/// ordinary fault included) and the template prompt's inline refusal. Each
/// fact drives the site's real catch with a real core refusal; where core's
/// text holds operating-system text the oracle is core's own renderer over
/// the same call made directly, and where it is fixed and locale-free the
/// oracle is a literal. A write that landed but was not indexed
/// (<c>SavedButUnindexed</c>) ends with core's detail; its failure lead
/// belongs to CB-46 (#1955), so it is not pinned here. Found on the way: a
/// faulted toggle on a route with no completion (the editor's
/// activate-at-cursor) says so through its tab.
/// </summary>
public sealed class WorkspaceFailureDetailTests
{
    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    /// <summary>The token core's after-write fault seam matches in a path
    /// (<c>SLATE_TEST_FAULT_AFTER_WRITE</c>).</summary>
    private const string SeamToken = "cb15cunindexed";

    private const string PropsNote = "---\nstatus: draft\n---\nBody.\n";
    private const string TaskNote = "---\nstatus: draft\n---\n\n- [ ] the task\n";

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "The workspace failure fact timed out.");

    private static string Rendered(A11yEvent said) => SlateUniffiMethods.A11yRender(said).Text;

    private static A11yPriority PriorityOf(A11yEvent said) => SlateUniffiMethods.A11yRender(said).Priority;

    private static string Detail(VaultException refusal) => SlateUniffiMethods.VaultErrorDetail(refusal);

    private static WorkspaceTabViewModel ActiveTab(Rig rig) =>
        Assert.IsType<WorkspaceTabViewModel>(rig.Workspace.ActiveGroup.ActiveTab);

    /// <summary>Core's after-write fault seam is process-global: the caller
    /// holds <see cref="EnvFaultLock"/>, and the variable is cleared in a
    /// finally.</summary>
    private static void UnderTheAfterWriteSeam(Action body)
    {
        Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_AFTER_WRITE", SeamToken);
        try
        {
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_AFTER_WRITE", null);
        }
    }

    /// <summary>A real <c>SavedButUnindexed</c> from core: a save of its own
    /// note under the same seam, made directly, so the oracle is core's
    /// rendering of the refusal the site met.</summary>
    private static string UnindexedOracle(Rig rig)
    {
        VaultException.SavedButUnindexed oracle = null!;
        UnderTheAfterWriteSeam(() => oracle = Assert.Throws<VaultException.SavedButUnindexed>(
            () => rig.Session.SaveText(SeamToken + "-oracle.md", "# Oracle\n\nSaved directly.\n", expectedContentHash: null)));
        return Detail(oracle);
    }

    // ---- Row 17: a bibliography source's notice ----------------------------------

    /// <summary>A configured bibliography source that does not exist: the
    /// Bibliography leaf's notice is core's sentence naming the source and the
    /// operating system's reason, not <c>@path=…, @reason=…</c>.</summary>
    [Fact]
    public void ABibliographySourceThatCannotBeOpenedShowsCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig(
            "cb15c-bib-missing",
            background: false,
            files: [("slate.json", "{\"citations\":{\"bibliography\":\"nowhere.bib\"}}"), ("a.md", "# A\n")]);
        VaultException oracle = Assert.ThrowsAny<VaultException>(
            () => rig.Session.SetBibliographySources(rig.Session.CitationsPrefs().Sources));

        string notice = Assert.Single(rig.Workspace.Bibliography.LoadNotices);

        Assert.IsType<VaultException.BibSourceUnreadable>(oracle);
        Assert.Equal(Detail(oracle), notice);
        Assert.StartsWith("Bibliography source nowhere.bib couldn't be opened: ", notice, StringComparison.Ordinal);
        Assert.DoesNotMatch(FieldToken, notice);
    });

    // ---- Row 18: the history panel's restore -------------------------------------

    private static HistoryVersionRow TwoVersionsOpen(Rig rig, string path)
    {
        _ = rig.Session.SaveText(path, "# N\n\nVersion one body.\n", expectedContentHash: null);
        _ = rig.Session.SaveText(path, "# N\n\nVersion two body.\n", expectedContentHash: null);
        rig.Workspace.OpenPath(path);
        rig.Workspace.History.Reload();
        HistoryVersionRow oldest = rig.Workspace.History.DayGroups
            .SelectMany(group => group.Rows)
            .Where(row => !row.IsMarker)
            .Last();
        return oldest;
    }

    /// <summary>A restore over a read-only note: the alert is "Restore
    /// failed: " and core's detail of the same refusal, made
    /// directly.</summary>
    [Fact]
    public void ARestoreOverAReadOnlyNoteSaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-restore-ro", background: false, files: [("note.md", "# N\n\nv0\n")]);
        HistoryVersionRow first = TwoVersionsOpen(rig, "note.md");
        string full = Path.Combine(rig.Root, "note.md");
        string? expected = ActiveTab(rig).SavedContentHash;
        File.SetAttributes(full, FileAttributes.ReadOnly);
        try
        {
            VaultException oracle = Assert.ThrowsAny<VaultException>(
                () => rig.Session.RestoreVersion("note.md", first.ContentHashAfter, expected));
            rig.Alerts.Clear();

            rig.Workspace.RequestRestoreVersion(first);
            rig.PumpUntil(() => !rig.Alerts.IsEmpty && rig.Workspace.SavesIdle, "the restore's alert");

            string alert = Assert.Single(rig.Alerts);
            Assert.IsType<VaultException.Io>(oracle);
            Assert.Equal("Restore failed: " + Detail(oracle), alert);
            Assert.DoesNotMatch(FieldToken, alert);
        }
        finally
        {
            File.SetAttributes(full, FileAttributes.Normal);
        }
    });

    /// <summary>A restore whose write landed and whose index commit failed:
    /// the alert ends with core's detail of a <c>SavedButUnindexed</c>, holds
    /// no field token (main showed the landed hash), and the restored bytes
    /// are on disk. The lead is CB-46's (#1955).</summary>
    [Fact]
    public void ARestoreThatLandedButWasNotIndexedEndsWithCoresDetail() => RunSta(() =>
    {
        const string path = SeamToken + "-restore.md";
        using IDisposable envLock = EnvFaultLock.Acquire();
        using var rig = new Rig(
            "cb15c-restore-unindexed",
            background: false,
            files: [(path, "# N\n\nv0\n"), (SeamToken + "-oracle.md", "# Oracle\n")]);
        HistoryVersionRow first = TwoVersionsOpen(rig, path);
        rig.Alerts.Clear();

        UnderTheAfterWriteSeam(() => rig.Workspace.RequestRestoreVersion(first));
        rig.PumpUntil(() => !rig.Alerts.IsEmpty && rig.Workspace.SavesIdle, "the restore's alert");

        string alert = Assert.Single(rig.Alerts);
        Assert.EndsWith(UnindexedOracle(rig), alert, StringComparison.Ordinal);
        Assert.DoesNotMatch(FieldToken, alert);
        string landed = File.ReadAllText(Path.Combine(rig.Root, path));
        Assert.Contains("Version one body.", landed, StringComparison.Ordinal);
        Assert.DoesNotContain("Version two body.", landed, StringComparison.Ordinal);
    });

    // ---- Row 19: Restore As a version --------------------------------------------

    /// <summary>Restore As a version to a destination core refuses as a path
    /// (typed by the reader): core's sentence naming the path and its
    /// reason.</summary>
    [Theory]
    [InlineData("../escape.md", "Invalid path ../escape.md: path segments must be non-empty and not self- or parent-references")]
    [InlineData("sub\\copy.md", "Invalid path sub\\copy.md: backslash separators are not allowed; vault paths use '/'")]
    public void RestoreAsAVersionToABadDestinationSaysCoresDetail(string destination, string detail) => RunSta(() =>
    {
        using var rig = new Rig("cb15c-restore-as-path", background: false, files: [("note.md", "# N\n\nv0\n")]);
        HistoryVersionRow first = TwoVersionsOpen(rig, "note.md");
        rig.Alerts.Clear();
        var answers = new ConcurrentQueue<(bool Ok, string? Refusal)>();

        rig.Workspace.CommitRestoreAsVersion(
            first.ContentHashAfter, first.AbsoluteDate, destination, (ok, refusal) => answers.Enqueue((ok, refusal)));
        rig.PumpUntil(() => !answers.IsEmpty, "the row's answer");

        Assert.Equal((false, (string?)null), Assert.Single(answers));
        string alert = Assert.Single(rig.Alerts);
        Assert.Equal("Can't restore: " + detail, alert);
        Assert.DoesNotMatch(FieldToken, alert);
    });

    /// <summary>Restore As a version the history does not hold: core's own
    /// "no such version", without "@message=".</summary>
    [Fact]
    public void RestoreAsAVersionNotInTheHistorySaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-restore-as-hash", background: false, files: [("note.md", "# N\n\nv0\n")]);
        _ = TwoVersionsOpen(rig, "note.md");
        rig.Alerts.Clear();
        var answers = new ConcurrentQueue<(bool Ok, string? Refusal)>();

        rig.Workspace.CommitRestoreAsVersion(new string('0', 64), "a date", "copy.md", (ok, refusal) => answers.Enqueue((ok, refusal)));
        rig.PumpUntil(() => !answers.IsEmpty, "the row's answer");

        string alert = Assert.Single(rig.Alerts);
        Assert.Equal("Can't restore: no such version", alert);
        Assert.DoesNotMatch(FieldToken, alert);
    });

    // ---- Row 20: recovering a deleted file ---------------------------------------

    private static HistoryDeletedRow DeletedRow(Rig rig, string path)
    {
        rig.Workspace.History.LoadDeletedFiles();
        Assert.Null(rig.Workspace.History.DeletedError);
        HistoryDeletedRow row = Assert.Single(rig.Workspace.History.DeletedRows, candidate => candidate.Path == path);
        Assert.True(row.Recoverable, "premise: the deleted row is recoverable");
        return row;
    }

    private static Action<VaultSession> SavedThenDeleted(string path) => first =>
    {
        _ = first.SaveText(path, "# Gone\n\nSaved before deletion.\n", expectedContentHash: null);
        StaThread.Run(() => first.DeleteFile(path));
    };

    /// <summary>The same deleted row recovered twice (a second activation
    /// before the list reloads): the second says core's "no deleted-file
    /// history" sentence, without "@message=".</summary>
    [Fact]
    public void ASecondRecoverOfOneRowSaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-recover-twice", background: false, earlier: SavedThenDeleted("gone.md"));
        HistoryDeletedRow row = DeletedRow(rig, "gone.md");
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();
        rig.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
        rig.PumpUntil(() => answers.Count == 1 && rig.Workspace.SavesIdle, "the first recover");
        rig.Alerts.Clear();

        rig.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
        rig.PumpUntil(() => answers.Count == 2 && rig.Workspace.SavesIdle, "the second recover");

        Assert.Equal(new (bool, bool)[] { (true, false), (false, false) }, answers.ToArray());
        string alert = Assert.Single(rig.Alerts);
        Assert.Equal("Can't restore: no deleted-file history for \"gone.md\"", alert);
        Assert.DoesNotMatch(FieldToken, alert);
    });

    /// <summary>A deleted file whose folder is now a file: core's sentence
    /// naming the path and why nothing was created.</summary>
    [Fact]
    public void ARecoverWhoseFolderIsNowAFileSaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig(
            "cb15c-recover-folder-file",
            background: false,
            earlier: SavedThenDeleted("sub/gone.md"),
            beforeOpen: root =>
            {
                string folder = Path.Combine(root, "sub");
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
                File.WriteAllText(folder, "a file where the folder was\n");
            },
            files: [("keep.md", "# Keep\n"), ("sub/other.md", "# Other\n")]);
        HistoryDeletedRow row = DeletedRow(rig, "sub/gone.md");
        rig.Alerts.Clear();
        var answers = new ConcurrentQueue<(bool Ok, bool Collision)>();

        rig.Workspace.RecoverDeletedFile(row, (ok, collision) => answers.Enqueue((ok, collision)));
        rig.PumpUntil(() => !answers.IsEmpty && rig.Workspace.SavesIdle, "the recover");

        Assert.Equal((false, false), Assert.Single(answers));
        string alert = Assert.Single(rig.Alerts);
        Assert.Equal("Can't restore: Invalid path sub/gone.md: \"sub\" is not a folder, so nothing was created", alert);
        Assert.DoesNotMatch(FieldToken, alert);
    });

    // ---- Row 21: Restore As a deleted file ---------------------------------------

    /// <summary>Restore As a deleted file to a destination core refuses as a
    /// path: core's sentence naming the path and its reason.</summary>
    [Theory]
    [InlineData("../escape.md", "Invalid path ../escape.md: path segments must be non-empty and not self- or parent-references")]
    [InlineData("sub\\copy.md", "Invalid path sub\\copy.md: backslash separators are not allowed; vault paths use '/'")]
    public void RestoreAsADeletedFileToABadDestinationSaysCoresDetail(string destination, string detail) => RunSta(() =>
    {
        using var rig = new Rig("cb15c-recover-as-path", background: false, earlier: SavedThenDeleted("gone.md"));
        _ = DeletedRow(rig, "gone.md");
        rig.Alerts.Clear();
        var answers = new ConcurrentQueue<(bool Ok, string? Refusal)>();

        rig.Workspace.CommitRestoreAsDeleted("gone.md", destination, (ok, refusal) => answers.Enqueue((ok, refusal)));
        rig.PumpUntil(() => !answers.IsEmpty, "the row's answer");

        Assert.Equal((false, (string?)null), Assert.Single(answers));
        string alert = Assert.Single(rig.Alerts);
        Assert.Equal("Can't restore: " + detail, alert);
        Assert.DoesNotMatch(FieldToken, alert);
    });

    /// <summary>Restore As a deleted file core holds no history for: core's
    /// "no deleted-file history" sentence, without "@message=".</summary>
    [Fact]
    public void RestoreAsADeletedFileWithNoHistorySaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-recover-as-none", background: false, files: [("keep.md", "# Keep\n")]);
        rig.Alerts.Clear();
        var answers = new ConcurrentQueue<(bool Ok, string? Refusal)>();

        rig.Workspace.CommitRestoreAsDeleted("never.md", "copy.md", (ok, refusal) => answers.Enqueue((ok, refusal)));
        rig.PumpUntil(() => !answers.IsEmpty, "the row's answer");

        string alert = Assert.Single(rig.Alerts);
        Assert.Equal("Can't restore: no deleted-file history for \"never.md\"", alert);
        Assert.DoesNotMatch(FieldToken, alert);
    });

    // ---- Rows 22 and 23: a property write's spoken failure ------------------------

    private static NotePropertiesViewModel Header(Rig rig, string path)
    {
        rig.Workspace.OpenPath(path);
        NotePropertiesViewModel? header = rig.Workspace.EnsureActiveTabProperties(synchronousForTests: true);
        Assert.NotNull(header);
        Assert.NotEmpty(header.Rows);
        return header;
    }

    private static AddPropertyViewModel AddSheet(Rig rig, string path)
    {
        _ = Header(rig, path);
        rig.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        AddPropertyViewModel? sheet = rig.Workspace.AddPropertySheet;
        Assert.NotNull(sheet);
        sheet.Key = "owner";
        sheet.Value = "alice";
        return sheet;
    }

    private static A11yEvent.PropertyEditFailed AwaitPropertyFailure(Rig rig, string what)
    {
        rig.PumpUntil(() => rig.Events.Any(e => e is A11yEvent.PropertyEditFailed) && rig.Workspace.SavesIdle, what);
        return Assert.Single(rig.Events.OfType<A11yEvent.PropertyEditFailed>());
    }

    private static void AssertPropertyFailure(A11yEvent.PropertyEditFailed said, string expected)
    {
        Assert.Equal(expected, Rendered(said));
        Assert.Equal(A11yPriority.High, PriorityOf(said));
        Assert.DoesNotMatch(FieldToken, Rendered(said));
    }

    /// <summary>Row 22 (a): Add Property on a read-only note says "Property
    /// edit failed: " and core's detail of the same refusal, made directly,
    /// at High.</summary>
    [Fact]
    public void AddingAPropertyToAReadOnlyNoteSaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-add-ro", background: false, files: [("props.md", PropsNote)]);
        AddPropertyViewModel sheet = AddSheet(rig, "props.md");
        string full = Path.Combine(rig.Root, "props.md");
        string? hash = ActiveTab(rig).SavedContentHash;
        File.SetAttributes(full, FileAttributes.ReadOnly);
        try
        {
            VaultException oracle = Assert.ThrowsAny<VaultException>(
                () => rig.Session.SetProperty("props.md", "owner", new PropertyValue.Text("alice"), hash));
            rig.Events.Clear();

            Assert.True(sheet.Add(), "premise: the add was admitted");
            A11yEvent.PropertyEditFailed said = AwaitPropertyFailure(rig, "the add's failure");

            Assert.IsType<VaultException.Io>(oracle);
            AssertPropertyFailure(said, "Property edit failed: " + Detail(oracle));
        }
        finally
        {
            File.SetAttributes(full, FileAttributes.Normal);
        }
    });

    /// <summary>Row 22 (b): an add whose write landed and whose index commit
    /// failed: the sentence ends with core's detail of a
    /// <c>SavedButUnindexed</c>, holds no field token, and the property is
    /// on disk. The lead is CB-46's (#1955).</summary>
    [Fact]
    public void AnAddThatLandedButWasNotIndexedEndsWithCoresDetail() => RunSta(() =>
    {
        const string path = SeamToken + "-add.md";
        using IDisposable envLock = EnvFaultLock.Acquire();
        using var rig = new Rig(
            "cb15c-add-unindexed",
            background: false,
            files: [(path, PropsNote), (SeamToken + "-oracle.md", "# Oracle\n")]);
        AddPropertyViewModel sheet = AddSheet(rig, path);
        rig.Events.Clear();

        UnderTheAfterWriteSeam(() =>
        {
            Assert.True(sheet.Add(), "premise: the add was admitted");
            rig.PumpUntil(() => rig.Events.Any(e => e is A11yEvent.PropertyEditFailed or A11yEvent.PropertyChanged), "the add's outcome");
        });
        A11yEvent.PropertyEditFailed said = AwaitPropertyFailure(rig, "the add's failure");

        Assert.EndsWith(UnindexedOracle(rig), Rendered(said), StringComparison.Ordinal);
        Assert.Equal(A11yPriority.High, PriorityOf(said));
        Assert.DoesNotMatch(FieldToken, Rendered(said));
        Assert.Contains("owner: alice", File.ReadAllText(Path.Combine(rig.Root, path)), StringComparison.Ordinal);
    });

    /// <summary>Row 22 (c): an ordinary fault in the add's worker, which the
    /// tab wraps as a vault error for the completion: the completion says the
    /// fault's own message, not the wrapper's "@message=".</summary>
    [Fact]
    public void AnAddsOrdinaryFaultSaysItsOwnMessage() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-add-fault", background: false, files: [("props.md", PropsNote)]);
        AddPropertyViewModel sheet = AddSheet(rig, "props.md");
        ActiveTab(rig).PropertyWriteFaultForTests = () => throw new InvalidOperationException("publish crash");
        rig.Events.Clear();

        Assert.True(sheet.Add(), "premise: the add was admitted");
        A11yEvent.PropertyEditFailed said = AwaitPropertyFailure(rig, "the add's failure");

        AssertPropertyFailure(said, "Property edit failed: publish crash");
    });

    /// <summary>Row 23 (a): setting a property's value on a read-only note
    /// says "Property edit failed: " and core's detail of the same refusal,
    /// made directly, at High.</summary>
    [Fact]
    public void SettingAPropertyOnAReadOnlyNoteSaysCoresDetail() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-prop-ro", background: false, files: [("props.md", PropsNote)]);
        NotePropertiesViewModel header = Header(rig, "props.md");
        PropertyRowViewModel status = header.Rows.Single(row => row.Key == "status");
        string full = Path.Combine(rig.Root, "props.md");
        string? hash = ActiveTab(rig).SavedContentHash;
        File.SetAttributes(full, FileAttributes.ReadOnly);
        try
        {
            VaultException oracle = Assert.ThrowsAny<VaultException>(
                () => rig.Session.SetProperty("props.md", "status", new PropertyValue.Text("done"), hash));
            rig.Events.Clear();

            status.EditorText = "done";
            Assert.True(rig.Workspace.SetPanelProperty(status));
            A11yEvent.PropertyEditFailed said = AwaitPropertyFailure(rig, "the set's failure");

            Assert.IsType<VaultException.Io>(oracle);
            AssertPropertyFailure(said, "Property edit failed: " + Detail(oracle));
        }
        finally
        {
            File.SetAttributes(full, FileAttributes.Normal);
        }
    });

    /// <summary>Row 23 (b): a set whose write landed and whose index commit
    /// failed: the sentence ends with core's detail of a
    /// <c>SavedButUnindexed</c>, holds no field token, and the value is on
    /// disk. The lead is CB-46's (#1955).</summary>
    [Fact]
    public void ASetThatLandedButWasNotIndexedEndsWithCoresDetail() => RunSta(() =>
    {
        const string path = SeamToken + "-set.md";
        using IDisposable envLock = EnvFaultLock.Acquire();
        using var rig = new Rig(
            "cb15c-prop-unindexed",
            background: false,
            files: [(path, PropsNote), (SeamToken + "-oracle.md", "# Oracle\n")]);
        NotePropertiesViewModel header = Header(rig, path);
        PropertyRowViewModel status = header.Rows.Single(row => row.Key == "status");
        rig.Events.Clear();
        status.EditorText = "done";

        UnderTheAfterWriteSeam(() =>
        {
            Assert.True(rig.Workspace.SetPanelProperty(status));
            rig.PumpUntil(() => rig.Events.Any(e => e is A11yEvent.PropertyEditFailed or A11yEvent.PropertyChanged), "the set's outcome");
        });
        A11yEvent.PropertyEditFailed said = AwaitPropertyFailure(rig, "the set's failure");

        Assert.EndsWith(UnindexedOracle(rig), Rendered(said), StringComparison.Ordinal);
        Assert.Equal(A11yPriority.High, PriorityOf(said));
        Assert.DoesNotMatch(FieldToken, Rendered(said));
        Assert.Contains("status: done", File.ReadAllText(Path.Combine(rig.Root, path)), StringComparison.Ordinal);
    });

    /// <summary>Row 23 (c): an ordinary fault in the set's worker, wrapped by
    /// the tab as a vault error: the completion says the fault's own message,
    /// not the wrapper's "@message=".</summary>
    [Fact]
    public void ASetsOrdinaryFaultSaysItsOwnMessage() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-prop-fault", background: false, files: [("props.md", PropsNote)]);
        NotePropertiesViewModel header = Header(rig, "props.md");
        PropertyRowViewModel status = header.Rows.Single(row => row.Key == "status");
        ActiveTab(rig).PropertyWriteFaultForTests = () => throw new InvalidOperationException("publish crash");
        rig.Events.Clear();

        status.EditorText = "done";
        Assert.True(rig.Workspace.SetPanelProperty(status));
        A11yEvent.PropertyEditFailed said = AwaitPropertyFailure(rig, "the set's failure");

        AssertPropertyFailure(said, "Property edit failed: publish crash");
    });

    // ---- Row 24: the template prompt's inline refusal ----------------------------

    private static void WriteTemplate(string root, string name, string body)
    {
        string dir = Path.Combine(root, "Templates");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), body);
    }

    /// <summary>A template create core refuses (the typed name exists; a
    /// parent that is a file): the name step shows core's sentence, inline
    /// and focusable, and nothing is announced (T7).</summary>
    [Theory]
    [InlineData("Taken", "Something named Taken.md already exists there.")]
    [InlineData("sub/Child", "Invalid path sub/Child.md: \"sub\" is not a folder, so nothing was created")]
    public void ATemplateCreateCoreRefusesShowsCoresDetail(string typed, string detail) => RunSta(() =>
    {
        using var rig = new Rig(
            "cb15c-template",
            background: false,
            beforeOpen: root =>
            {
                WriteTemplate(root, "Plain.md", "Fresh body.\n");
                File.WriteAllText(Path.Combine(root, "sub"), "a file where a folder would be\n");
            },
            files: [("Taken.md", "Original stays.\n")]);
        rig.Workspace.OpenTemplatePicker();
        TemplatePickerViewModel picker = rig.Workspace.TemplatePickerSheet!;
        picker.ActivateCommand.Execute(picker.Rows.Single(candidate => candidate.Name == "Plain"));
        TemplateFlowViewModel flow = rig.Workspace.TemplateFlowSheet!;
        rig.Events.Clear();

        flow.NoteName = typed;
        flow.CreateCommand.Execute(null);

        Assert.Equal(detail, flow.ValidationError);
        Assert.Equal("Validation error: " + detail, flow.ValidationErrorAccessibleName);
        Assert.Empty(rig.Events);
        Assert.Equal("Original stays.\n", File.ReadAllText(Path.Combine(rig.Root, "Taken.md")));
    });

    // ---- Found on the way: a faulted toggle with no completion -------------------

    /// <summary>A task toggle on the editor's activate-at-cursor route (no
    /// completion behind the tab) whose worker faults outside its own catch:
    /// the backstop ends the toggle and says so through the tab, with the
    /// sentence the tab speaks for any failed toggle, at High, and the tab's
    /// status shows it. Before, it ended silently.</summary>
    [Fact]
    public void AFaultedToggleWithNoCompletionSaysSoThroughItsTab() => RunSta(() =>
    {
        using var rig = new Rig("cb15c-toggle-backstop", background: true, files: [("a.md", TaskNote)]);
        rig.Workspace.OpenPath("a.md");
        rig.PumpUntil(
            () => rig.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { IsMarkdown: true, SavedContentHash: not null } open
                && open.Path == "a.md",
            "the task note's tab");
        WorkspaceTabViewModel tab = ActiveTab(rig);
        TaskItem task = rig.Session.TasksForFile("a.md")[0];
        var spoken = new ConcurrentQueue<A11yEvent>();
        tab.TaskToggleBeforeWriteForTests = () => throw new OutOfMemoryException("cb15c: outside the toggle's catch");
        try
        {
            Assert.Equal(TabTaskToggle.Started, tab.ToggleTask(task, spoken.Enqueue));
            rig.PumpUntil(() => !tab.IsTaskToggleInFlight && rig.Workspace.SavesIdle, "the toggle's backstop");
        }
        finally
        {
            tab.TaskToggleBeforeWriteForTests = null;
        }

        A11yEvent.TaskToggleFailed said = Assert.Single(spoken.OfType<A11yEvent.TaskToggleFailed>());
        Assert.Equal("Task could not be toggled: cb15c: outside the toggle's catch.", Rendered(said));
        Assert.Equal(A11yPriority.High, PriorityOf(said));
        Assert.Equal(Rendered(said), tab.Status);
        Assert.Equal(TaskNote, File.ReadAllText(Path.Combine(rig.Root, "a.md")));
    });

    // ---- The rig -----------------------------------------------------------------

    /// <summary>A scanned vault and a workspace over it, with the history
    /// panel's alert captured (a real message box blocks a headless host)
    /// and its restore confirmation answered yes.</summary>
    private sealed class Rig : IDisposable
    {
        private readonly FixtureVault _fixture;

        public Rig(
            string label,
            bool background,
            Action<VaultSession>? earlier = null,
            Action<string>? beforeOpen = null,
            (string Path, string Text)[]? files = null)
        {
            _fixture = FixtureVault.Create(0, label);
            foreach ((string path, string text) in files ?? [])
            {
                string full = Path.Combine(_fixture.Root, path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }
            if (earlier is not null)
            {
                using VaultSession first = VaultSession.OpenFilesystem(_fixture.Root);
                using (var scan = new CancelToken())
                {
                    _ = first.ScanInitial(scan);
                }
                earlier(first);
            }
            beforeOpen?.Invoke(_fixture.Root);
            Session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _ = Session.ScanInitial(cancel);
            }
            Workspace = new WorkspaceViewModel(
                Session, _fixture.Root, () => [], Events.Enqueue, startInteractionBackgroundWork: background);
            Workspace.HistoryAlert = (title, message) => Alerts.Enqueue($"{title}: {message}");
            Workspace.HistoryRestoreConfirmation = _ => true;
        }

        public string Root => _fixture.Root;

        public VaultSession Session { get; }

        public WorkspaceViewModel Workspace { get; }

        public ConcurrentQueue<A11yEvent> Events { get; } = new();

        public ConcurrentQueue<string> Alerts { get; } = new();

        public void PumpUntil(Func<bool> condition, string what) =>
            Assert.True(PumpedDispatcher.PumpUntil(condition, TimeSpan.FromSeconds(30)), $"{what} never happened");

        public void Dispose()
        {
            try
            {
                _ = PumpedDispatcher.PumpUntil(() => Workspace.SavesIdle, TimeSpan.FromSeconds(30));
                Workspace.Dispose();
                Session.Dispose();
            }
            finally
            {
                _fixture.Dispose();
            }
        }
    }
}
