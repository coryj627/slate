// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-4 (#736): the in-note properties header and its workspace
/// write seam over real core, pinning the feature contracts in
/// docs/plans/22_property_panel_contracts.md — CAS snapshot identity
/// (1), draft locality (2), refusal totality (3), publication from
/// authoritative bytes (4), delete safety (5), and round-trip
/// fidelity (10).
/// </summary>
public sealed class NotePropertiesTests
{
    private const string PropsFrontmatter =
        "---\n"
        + "title: Hello\n"
        + "count: 42\n"
        + "rating: 1.5\n"
        + "published: true\n"
        + "due: 2026-05-01\n"
        + "created: 2026-05-01T10:00:00Z\n"
        + "edited: 2026-05-01T10:00:00\n"
        + "source: \"[[other]]\"\n"
        + "aliases:\n"
        + "  - one\n"
        + "  - two\n"
        + "tags:\n"
        + "  - alpha\n"
        + "---\n";

    private const string PropsBody = "Body line one.\n\nBody line two.\n";

    private static FixtureVault MakeVault(string name)
    {
        FixtureVault fixture = FixtureVault.Create(0, name);
        File.WriteAllText(
            Path.Combine(fixture.Root, "props.md"), PropsFrontmatter + PropsBody);
        File.WriteAllText(Path.Combine(fixture.Root, "other.md"), "# Other\n");
        File.WriteAllText(Path.Combine(fixture.Root, "bare.md"), "No frontmatter.\n");
        File.WriteAllText(
            Path.Combine(fixture.Root, "single.md"), "---\nonly: value\n---\nBody.\n");
        return fixture;
    }

    private static VaultSession OpenScanned(string root)
    {
        var session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static NotePropertiesViewModel AttachProperties(
        WorkspaceViewModel workspace, string path)
    {
        workspace.OpenPath(path);
        NotePropertiesViewModel properties =
            workspace.EnsureActiveTabProperties(synchronousForTests: true)!;
        Assert.NotNull(properties);
        return properties;
    }

    [Fact]
    public void LoadPublishesRowsInDocumentOrderWithTheSnapshotHash()
    {
        using FixtureVault fixture = MakeVault("props-load");
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");

        Assert.Equal(
            new[]
            {
                "title", "count", "rating", "published", "due",
                "created", "edited", "source", "aliases", "tags",
            },
            properties.Rows.Select(row => row.Key).ToArray());
        Assert.Equal("Properties, 10 items", properties.HeaderText);
        Assert.Equal("Properties, 10 properties", properties.HeaderGroupName);
        Assert.False(properties.ShowEmptyState);
        Assert.Null(properties.LoadError);

        // Contract 1: every row pins the publish-time hash; the add
        // seam pins the same one on the header VM.
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.All(
            properties.Rows,
            row => Assert.Equal(tab.SavedContentHash, row.ContentHash));
        Assert.Equal(tab.SavedContentHash, properties.ContentHash);

        // Editor modes derive from the STORED values.
        Assert.Equal("text", properties.Rows[0].EditorMode);
        Assert.Equal("integer", properties.Rows[1].EditorMode);
        Assert.Equal("float", properties.Rows[2].EditorMode);
        Assert.Equal("boolean", properties.Rows[3].EditorMode);
        Assert.Equal("datePicker", properties.Rows[4].EditorMode);
        Assert.Equal("text", properties.Rows[5].EditorMode);
        Assert.Equal("text", properties.Rows[6].EditorMode);
        Assert.Equal("wikilink", properties.Rows[7].EditorMode);
        Assert.Equal("list", properties.Rows[8].EditorMode);
        Assert.Equal("list", properties.Rows[9].EditorMode);
        Assert.True(properties.Rows[9].IsTagList);
        Assert.Equal(2, properties.Rows[8].Items.Count);
        Assert.Equal(
            "Property aliases, item 1 of 2", properties.Rows[8].Items[0].ItemLabel);
    }

    [Fact]
    public void EmptyNotePublishesTheEmptyStateWithAUsableAddHash()
    {
        using FixtureVault fixture = MakeVault("props-empty");
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        NotePropertiesViewModel properties = AttachProperties(workspace, "bare.md");

        Assert.Empty(properties.Rows);
        Assert.True(properties.ShowEmptyState);
        Assert.Equal("No properties yet. Add one to start.", properties.EmptyStateText);
        Assert.Equal("Properties, 0 items", properties.HeaderText);
        // Adds on a bare note still have a CAS token.
        Assert.NotEqual("", properties.ContentHash);
    }

    [Fact]
    public void StalePublishesAreDiscardedAndLoadFaultsSurfaceHonestly()
    {
        using FixtureVault fixture = MakeVault("props-stale");
        using VaultSession session = OpenScanned(fixture.Root);
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");
        int rowCount = properties.Rows.Count;
        string hash = properties.ContentHash;

        // Stale generation and stale requestId both discard.
        properties.PublishProperties(
            long.MaxValue, int.MaxValue, [], "bogus", null);
        Assert.Equal(rowCount, properties.Rows.Count);
        Assert.Equal(hash, properties.ContentHash);

        // A load fault clears rows (no ghosts) and surfaces the error;
        // the add hash is withdrawn.
        properties.Load("missing-note.md");
        Assert.True(
            properties.LoadError is not null,
            $"rows={properties.Rows.Count} hash='{properties.ContentHash}' "
                + $"loading={properties.IsLoading}");
        Assert.Empty(properties.Rows);
        Assert.Equal("", properties.ContentHash);
        Assert.False(properties.ShowEmptyState);
    }

    [Fact]
    public void CommitRoutesTheSnapshotHashAndTheBodySurvivesByteIdentical()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = MakeVault("props-commit");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
            NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");
            WorkspaceTabViewModel tab =
                Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
            string bodyBefore = session.ReadNoteParts("props.md").Body;

            PropertyRowViewModel title = properties.Rows[0];
            title.EditorText = "Renamed";
            Assert.True(title.IsDirty);
            title.CommitCommand.Execute(null);
            WaitForUi(() => properties.Rows.Count > 0
                && properties.Rows[0].EditorText == "Renamed"
                && !properties.Rows[0].IsDirty
                && !title.WriteInFlight);

            // Contract 4: authoritative bytes, body byte-identical,
            // tab re-baselined before any later save.
            NotePartsBundle after = session.ReadNoteParts("props.md");
            Assert.Contains("title: Renamed", after.FmSource, StringComparison.Ordinal);
            Assert.Equal(bodyBefore, after.Body);
            Assert.Equal(after.ContentHash, tab.SavedContentHash);
            Assert.Equal(after.ContentHash, properties.ContentHash);
            Assert.False(tab.IsDirty);

            // Contract 9: exactly one canonical announcement.
            Assert.Equal(
                1,
                announced.Count(item =>
                    SlateUniffiMethods.A11yRender(item).Text == "Property title updated."));
        });
    }

    [Fact]
    public void RoundTripCommitsPreserveStoredFormsVerbatim()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = MakeVault("props-roundtrip");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
            NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");

            // Contract 10: committing UNCHANGED drafts is
            // value-preserving — Z stays Z, naive stays naive,
            // integer never flips to float, dates survive verbatim.
            // (Core re-emits YAML, so this is a VALUE guarantee
            // pinned via re-parse, not a byte guarantee.)
            Property[] before = SlateUniffiMethods.ParseFrontmatterProperties(
                session.ReadNoteParts("props.md").FmSource);
            foreach (string key in new[] { "due", "created", "edited", "rating", "count" })
            {
                PropertyRowViewModel row = properties.Rows.Single(r => r.Key == key);
                row.CommitCommand.Execute(null);
                WaitForUi(() => properties.Rows.All(r => !r.WriteInFlight));
            }

            Property[] after = SlateUniffiMethods.ParseFrontmatterProperties(
                session.ReadNoteParts("props.md").FmSource);
            foreach (string key in new[] { "due", "created", "edited", "rating", "count" })
            {
                Property earlier = before.Single(p => p.Key == key);
                Property later = after.Single(p => p.Key == key);
                Assert.Equal(earlier.Kind, later.Kind);
                Assert.Equal(earlier.ValueJson, later.ValueJson);
            }
        });
    }

    [Fact]
    public void DirtyTabRefusesWithTheSpokenReasonAndWritesNothing()
    {
        using FixtureVault fixture = MakeVault("props-dirty-refusal");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        string diskBefore = File.ReadAllText(Path.Combine(fixture.Root, "props.md"));

        tab.Text += "\nUnsaved.";
        Assert.True(tab.IsDirty);
        PropertyRowViewModel title = properties.Rows[0];
        title.EditorText = "Blocked";
        title.CommitCommand.Execute(null);

        // Contract 3: zero core calls, draft intact, one typed refusal.
        Assert.False(title.WriteInFlight);
        Assert.True(title.IsDirty);
        Assert.Equal(
            diskBefore, File.ReadAllText(Path.Combine(fixture.Root, "props.md")));
        A11yEvent.HostComposed refusal = Assert.IsType<A11yEvent.HostComposed>(
            Assert.Single(
                announced,
                item => item is A11yEvent.HostComposed composed
                    && composed.Text.StartsWith(
                        "Save the note before editing properties.",
                        StringComparison.Ordinal)));
        Assert.Equal(
            "Save the note before editing properties. "
                + "The editor has unsaved changes in props.md.",
            refusal.Text);
        Assert.Equal(A11yPriority.High, refusal.Priority);
    }

    [Fact]
    public void StaleRowHashSurfacesTheConflictDialogAndNeverWrites()
    {
        using FixtureVault fixture = MakeVault("props-stale-row");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        var conflictRequests = new List<(string Filename, string Key)>();
        workspace.PropertyConflictDialog = (filename, key, _, _) =>
            conflictRequests.Add((filename, key));
        NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");

        // A DIRECT session write (another surface: sync, bulk rename,
        // CLI) moves the disk hash without going through the tab; the
        // refreshed rows pin the NEW hash while the tab still holds
        // the old baseline — the seam must refuse (contract 1/3).
        string notePath = Path.Combine(fixture.Root, "props.md");
        WorkspaceTabViewModel tabBefore =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        _ = session.SaveText(
            "props.md",
            PropsFrontmatter + PropsBody + "\nExternal edit.\n",
            tabBefore.SavedContentHash);
        properties.RefreshProperties();
        PropertyRowViewModel title = properties.Rows[0];
        // Setup premise: the refreshed rows pin the moved disk hash.
        WorkspaceTabViewModel staleTab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.NotEqual(staleTab.SavedContentHash, title.ContentHash);
        title.EditorText = "Blocked";
        string diskAfterExternal = File.ReadAllText(notePath);

        title.CommitCommand.Execute(null);

        Assert.False(title.WriteInFlight);
        Assert.Equal(diskAfterExternal, File.ReadAllText(notePath));
        Assert.Single(announced.OfType<A11yEvent.PropertyEditConflict>());
        Assert.Equal(("props.md", "title"), Assert.Single(conflictRequests));
    }

    [Fact]
    public void MidFlightExternalWriteSurfacesWriteConflictNotAnOverwrite()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = MakeVault("props-cas");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
            workspace.PropertyConflictDialog = (_, _, _, _) => { };
            NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");

            // The tab and the row agree on the OLD hash, so the gates
            // pass — but the disk moved underneath. Core CAS must
            // refuse; the external content survives.
            string notePath = Path.Combine(fixture.Root, "props.md");
            string external = PropsFrontmatter + "External body wins.\n";
            File.WriteAllText(notePath, external);

            PropertyRowViewModel title = properties.Rows[0];
            title.EditorText = "MustNotLand";
            title.CommitCommand.Execute(null);
            WaitForUi(() => announced.OfType<A11yEvent.PropertyEditConflict>().Any());

            Assert.Equal(external, File.ReadAllText(notePath));
            Assert.DoesNotContain(
                "MustNotLand", File.ReadAllText(notePath), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void DeleteConfirmationDefaultsToCancelAndDirtyRowsRefuse()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = MakeVault("props-delete");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
            bool confirmationAsked = false;
            bool confirmationAnswer = false;
            workspace.PropertyDeleteConfirmation = (_, _) =>
            {
                confirmationAsked = true;
                return confirmationAnswer;
            };
            NotePropertiesViewModel properties = AttachProperties(workspace, "single.md");
            string notePath = Path.Combine(fixture.Root, "single.md");

            // Dirty row: refused with the spoken reason, confirmation
            // never shown (contract 5).
            PropertyRowViewModel only = properties.Rows.Single();
            only.EditorText = "changed";
            only.DeleteCommand.Execute(null);
            Assert.False(confirmationAsked);
            Assert.Contains(
                announced.OfType<A11yEvent.HostComposed>(),
                item => item.Text
                    == "Revert or save this property draft before deleting the property.");
            only.Revert();

            // Declined confirmation (the Cancel default): no write.
            only.DeleteCommand.Execute(null);
            Assert.True(confirmationAsked);
            Assert.Contains(
                "only: value", File.ReadAllText(notePath), StringComparison.Ordinal);

            // Confirmed: the last key removes the frontmatter shell.
            // (The predicate tolerates the core writer's transient
            // exclusive lock on the file.)
            confirmationAnswer = true;
            only.DeleteCommand.Execute(null);
            WaitForUi(() => announced.Any(item =>
                SlateUniffiMethods.A11yRender(item).Text == "Property only deleted."));
            WaitForUi(() => TryReadAllText(notePath) is { } text
                && !text.Contains("only: value", StringComparison.Ordinal));
            Assert.DoesNotContain(
                "---", File.ReadAllText(notePath), StringComparison.Ordinal);
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text
                    == "Property only deleted.");
        });
    }

    [Fact]
    public void RevertRestoresTheBaselineAndAnnouncesOnce()
    {
        using FixtureVault fixture = MakeVault("props-revert");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        NotePropertiesViewModel properties = AttachProperties(workspace, "props.md");

        PropertyRowViewModel aliases = properties.Rows.Single(r => r.Key == "aliases");
        aliases.Items[0].Text = "mutated";
        aliases.AddItemCommand.Execute(null);
        Assert.True(aliases.IsDirty);

        aliases.RevertCommand.Execute(null);
        Assert.False(aliases.IsDirty);
        Assert.Equal(2, aliases.Items.Count);
        Assert.Equal("one", aliases.Items[0].Text);
        Assert.Equal(
            1,
            announced.OfType<A11yEvent.HostComposed>()
                .Count(item => item.Text == "Reverted changes to aliases."));
    }

    [Fact]
    public void SaveFunnelRederivesRowsFromHandEditedFrontmatter()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = MakeVault("props-save-funnel");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
            NotePropertiesViewModel properties = AttachProperties(workspace, "single.md");
            WorkspaceTabViewModel tab =
                Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);

            // Typing YAML directly into the buffer and saving must
            // re-derive the header (contract 4 via the save funnel).
            tab.Text = "---\nonly: value\nadded: fresh\n---\nBody.\n";
            workspace.SaveActiveCommand.Execute(null);
            WaitForUi(() => properties.Rows.Count == 2);
            Assert.Equal(
                new[] { "only", "added" },
                properties.Rows.Select(row => row.Key).ToArray());
        });
    }

    [Fact]
    public void StoredValueDatePickerTruthTableIsPinned()
    {
        Assert.True(PropertyRowViewModel.StoredValueTakesDatePicker(
            "date", "\"2026-05-01\""));
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker(
            "date", "\"2026-13-45\""));
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker(
            "date", "\"May 1, 2026\""));
        Assert.True(PropertyRowViewModel.StoredValueTakesDatePicker(
            "datetime", "\"2026-05-01T10:00:00Z\""));
        Assert.True(PropertyRowViewModel.StoredValueTakesDatePicker(
            "datetime", "\"2026-05-01T10:00:00\""));
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker(
            "datetime", "\"soon\""));
        Assert.False(PropertyRowViewModel.StoredValueTakesDatePicker(
            "text", "\"2026-05-01\""));
    }

    [Fact]
    public void SteppersAreOverflowGuardedAndDraftLocal()
    {
        var row = new PropertyRowViewModel(
            new Property("count", "number", long.MaxValue.ToString()),
            "hash",
            _ => Assert.Fail("steppers must not commit"),
            _ => { },
            _ => { });
        Assert.Equal("integer", row.EditorMode);

        row.StepUpCommand.Execute(null);
        Assert.Equal(long.MaxValue.ToString(), row.EditorText);
        row.StepDownCommand.Execute(null);
        Assert.Equal((long.MaxValue - 1).ToString(), row.EditorText);
        Assert.True(row.IsDirty);
    }

    private static string? TryReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous action timed out.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
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
