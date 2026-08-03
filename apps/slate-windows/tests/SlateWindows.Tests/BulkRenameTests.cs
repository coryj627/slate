// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-4 (#736): the vault-wide bulk-rename sheet — the exact-pair
/// arming rule, report partition rendering, the A11yRender footer
/// identity, and post-apply tab reconciliation (feature contracts
/// 7 and 8).
/// </summary>
public sealed class BulkRenameTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public BulkRenameTests()
    {
        _fixture = FixtureVault.Create(0, "bulk-rename");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "a.md"),
            "---\noldkey: alpha\n---\nBody a.\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "b.md"),
            "---\noldkey: beta\nnewkey: taken\n---\nBody b.\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "c.md"), "No frontmatter.\n");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "tagged.md"),
            "---\ntags:\n  - alpha\n---\nBody.\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private BulkRenameViewModel MakeSheet(
        List<A11yEvent>? announced = null,
        Func<bool>? anyDirty = null,
        List<RenameReport>? reconciled = null)
        => new(
            _session,
            (announced ?? []).Add,
            anyDirty ?? (() => false),
            report => reconciled?.Add(report),
            synchronousForTests: true);

    [Fact]
    public void ApplyArmsOnlyAfterAPreviewOnTheExactKeyPair()
    {
        var sheet = MakeSheet();
        sheet.OldKey = "oldkey";
        sheet.NewKey = "renamed";
        Assert.True(sheet.CanPreview);
        Assert.False(sheet.CanApply);

        sheet.Preview();
        Assert.True(sheet.CanApply);
        int rowsAfterPreview = sheet.Rows.Count;
        Assert.True(rowsAfterPreview > 0);

        // Contract 7: ANY key edit disarms; the grid stays visible.
        sheet.NewKey = "renamed2";
        Assert.False(sheet.CanApply);
        Assert.Equal(rowsAfterPreview, sheet.Rows.Count);

        sheet.Preview();
        Assert.True(sheet.CanApply);
    }

    [Fact]
    public void PreviewRendersThePartitionsWithVerbatimStatuses()
    {
        var announced = new List<A11yEvent>();
        var sheet = MakeSheet(announced);
        sheet.OldKey = "oldkey";
        sheet.NewKey = "newkey";
        sheet.Preview();

        // a.md will apply; b.md collides (KeyCollision never
        // overwrites). Rows carry the typed-reason strings.
        Assert.Contains(
            sheet.Rows, row => row.Path == "a.md" && row.Status == "Will apply");
        Assert.Contains(
            sheet.Rows,
            row => row.Path == "b.md"
                && row.Status == "Skipped: new key already exists");

        // Footer == A11yRender(RenameSummary) verbatim; the same
        // event was announced (§2.5).
        var summary = new A11yEvent.RenameSummary(
            Applied: false, Renamed: 1, Skipped: 1, Failed: 0);
        Assert.Equal(SlateUniffiMethods.A11yRender(summary).Text, sheet.FooterText);
        Assert.Contains(announced, item => item is A11yEvent.RenameSummary
        {
            Applied: false, Renamed: 1, Skipped: 1, Failed: 0,
        });
    }

    [Fact]
    public void ApplyWritesRenamesAndReportsThePartition()
    {
        var announced = new List<A11yEvent>();
        var reconciled = new List<RenameReport>();
        var sheet = MakeSheet(announced, reconciled: reconciled);
        sheet.OldKey = "oldkey";
        sheet.NewKey = "newkey";
        sheet.Preview();
        Assert.True(sheet.Apply());

        string a = File.ReadAllText(Path.Combine(_fixture.Root, "a.md"));
        Assert.Contains("newkey: alpha", a, StringComparison.Ordinal);
        Assert.DoesNotContain("oldkey:", a, StringComparison.Ordinal);
        // KeyCollision never overwrites (contract 7).
        string b = File.ReadAllText(Path.Combine(_fixture.Root, "b.md"));
        Assert.Contains("oldkey: beta", b, StringComparison.Ordinal);
        Assert.Contains("newkey: taken", b, StringComparison.Ordinal);

        Assert.Contains(
            sheet.Rows, row => row.Path == "a.md" && row.Status == "Applied");
        RenameReport report = Assert.Single(reconciled);
        Assert.Contains(report.Affected, entry => entry.Path == "a.md" && entry.Applied);
        // Applied runs disarm until the next preview.
        Assert.False(sheet.CanApply);
        Assert.Contains(
            announced,
            item => item is A11yEvent.RenameSummary { Applied: true, Renamed: 1 });
    }

    [Fact]
    public void ApplyRefusesWhileAnyOpenDraftIsDirty()
    {
        var announced = new List<A11yEvent>();
        var reconciled = new List<RenameReport>();
        bool dirty = true;
        var sheet = MakeSheet(announced, () => dirty, reconciled);
        sheet.OldKey = "oldkey";
        sheet.NewKey = "blockedkey";
        sheet.Preview();

        Assert.False(sheet.Apply());
        Assert.Contains(
            announced.OfType<A11yEvent.HostComposed>(),
            item => item.Text
                == "Apply or discard uncommitted property changes before renaming "
                    + "properties.");
        Assert.Empty(reconciled);
        Assert.Contains(
            "oldkey: alpha",
            File.ReadAllText(Path.Combine(_fixture.Root, "a.md")),
            StringComparison.Ordinal);

        dirty = false;
        Assert.True(sheet.Apply());
    }

    [Fact]
    public void InvalidKeysSurfaceTheErrorSurfaceNotRows()
    {
        var announced = new List<A11yEvent>();
        var sheet = MakeSheet(announced);
        sheet.OldKey = "same";
        sheet.NewKey = "same";
        sheet.Preview();

        Assert.NotNull(sheet.ErrorText);
        Assert.StartsWith("Error: ", sheet.ErrorText, StringComparison.Ordinal);
        Assert.False(sheet.CanApply);
        Assert.Contains(announced, item => item is A11yEvent.RenameFailed);
    }

    [Fact]
    public void SkipAndFailureStatusStringsAreExhaustivelyPinned()
    {
        Assert.Equal(
            "Skipped: key not present",
            BulkRenameViewModel.SkipStatus(RenameSkipReason.NoSuchKey));
        Assert.Equal(
            "Skipped: new key already exists",
            BulkRenameViewModel.SkipStatus(RenameSkipReason.KeyCollision));
        Assert.Equal(
            "Skipped: would change tags / list type",
            BulkRenameViewModel.SkipStatus(RenameSkipReason.TagsKeyTypeDrift));
        Assert.Equal(
            "Failed: external write",
            BulkRenameViewModel.FailStatus(RenameFailureKind.WriteConflict, ""));
        Assert.Equal(
            "Failed: malformed YAML",
            BulkRenameViewModel.FailStatus(RenameFailureKind.MalformedFrontmatter, ""));
        Assert.Equal(
            "Failed: cancelled",
            BulkRenameViewModel.FailStatus(RenameFailureKind.Cancelled, ""));
        Assert.Equal(
            "Failed: error: boom",
            BulkRenameViewModel.FailStatus(RenameFailureKind.Other, "boom"));
    }

    [Fact]
    public void TagsBoundaryRenameSurfacesTheTypeDriftSkip()
    {
        var sheet = MakeSheet();
        sheet.OldKey = "tags";
        sheet.NewKey = "labels";
        sheet.Preview();

        // Renaming across the tags boundary with a list value yields
        // the per-file TagsKeyTypeDrift skip — surfaced, not hidden
        // (the first user rename involving tags must not look like a
        // bug).
        Assert.Contains(
            sheet.Rows,
            row => row.Path == "tagged.md"
                && row.Status == "Skipped: would change tags / list type");
    }

    [Fact]
    public void StalePublishesAndShutdownDiscardResults()
    {
        var sheet = MakeSheet();
        sheet.OldKey = "oldkey";
        sheet.NewKey = "renamed";
        sheet.Preview();
        int rows = sheet.Rows.Count;

        // A stale requestId publish is a no-op.
        sheet.PublishRun(-1, true, "oldkey", "renamed", null, "stale");
        Assert.Equal(rows, sheet.Rows.Count);
        Assert.Null(sheet.ErrorText);

        sheet.Shutdown();
        sheet.Preview();
        Assert.Equal(rows, sheet.Rows.Count);
    }

    [Fact]
    public void ApplyReportsReconcileTabsEvenAfterTheSheetShutsDown()
    {
        var announced = new List<A11yEvent>();
        var reconciled = new List<RenameReport>();
        var sheet = MakeSheet(announced, reconciled: reconciled);
        var report = new RenameReport(
            Affected:
            [
                new RenameAffected("a.md", "old", "new", true, "hash-after"),
            ],
            Skipped: [],
            Failed: []);

        // Contract 7/8 (adversarial round 1): a close mid-apply must
        // not discard the landed writes' reconciliation — disk truth
        // publishes regardless of UI liveness.
        sheet.Shutdown();
        sheet.PublishRun(0, dryRun: false, "oldkey", "newkey", report, null);
        Assert.Same(report, Assert.Single(reconciled));
        Assert.Contains(
            announced,
            item => item is A11yEvent.RenameSummary { Applied: true, Renamed: 1 });
        Assert.Empty(sheet.Rows);
    }

    [Fact]
    public void ArmingSurvivesIncidentalWhitespaceInTheKeyFields()
    {
        var sheet = MakeSheet();
        sheet.OldKey = "oldkey ";
        sheet.NewKey = " renamed";
        sheet.Preview();
        // Round-3 below-bar: runs consume trimmed keys, so the
        // arming comparison trims too — whitespace never leaves a
        // valid preview unarmed.
        Assert.True(sheet.CanApply);
        sheet.NewKey = " renamed2";
        Assert.False(sheet.CanApply);
    }

    [Fact]
    public void WorkspaceDisposalShutsDownAnInFlightRenameSheet()
    {
        var reconciled = new List<RenameReport>();
        var announced = new List<A11yEvent>();
        BulkRenameViewModel sheet;
        using var inFlightCancel = new CancelToken();
        using (var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], announced.Add,
            startInteractionBackgroundWork: false))
        {
            workspace.OpenBulkRenameSheet(synchronousForTests: true);
            sheet = workspace.BulkRenameSheet!;
            sheet.MarkWorkInFlightForTests();
            sheet.SetInFlightCancelForTests(inFlightCancel);
        }

        // Round-3/4 below-bar: disposal must actually CANCEL the
        // in-flight core run — this bites if Dispose stops calling
        // CancelInFlight.
        Assert.True(inFlightCancel.IsCancelled());

        // Round 3: disposal cancelled and SHUT DOWN the sheet — a
        // late terminal publish cannot mutate UI state, while an
        // apply report's disk truth still reconciles (unconditional
        // block).
        sheet.OldKey = "oldkey";
        sheet.PublishRun(
            sheet.RequestIdForTests, dryRun: true, "oldkey", "renamed",
            new RenameReport(Affected: [], Skipped: [], Failed: []), null);
        Assert.Empty(sheet.Rows);
        Assert.Equal("", sheet.FooterText);
    }

    [Fact]
    public void RunsAreSerializedAndStaleApplyReportsStillLandDiskTruth()
    {
        var announced = new List<A11yEvent>();
        var reconciled = new List<RenameReport>();
        var sheet = MakeSheet(announced, reconciled: reconciled);
        sheet.OldKey = "oldkey";
        sheet.NewKey = "renamed";

        // Adversarial round 2: a run can never start while another is
        // in flight — Preview cannot supersede an in-flight Apply.
        sheet.MarkWorkInFlightForTests();
        int before = sheet.RequestIdForTests;
        sheet.Preview();
        Assert.Equal(before, sheet.RequestIdForTests);
        Assert.False(sheet.CanPreview);
        Assert.False(sheet.CanApply);

        // And even a stale-requestId APPLY report reconciles and
        // announces — landed writes are never discarded by later
        // requests.
        var report = new RenameReport(
            Affected: [new RenameAffected("a.md", "old", "new", true, "hash-after")],
            Skipped: [],
            Failed: []);
        sheet.PublishRun(
            sheet.RequestIdForTests - 1, dryRun: false, "oldkey", "renamed", report, null);
        Assert.Same(report, Assert.Single(reconciled));
        Assert.Contains(
            announced,
            item => item is A11yEvent.RenameSummary { Applied: true, Renamed: 1 });
    }

    [Fact]
    public void CloseDuringAnInFlightRunSettlesAtTheTerminalPublish()
    {
        var sheet = MakeSheet();
        int settled = 0;
        sheet.CloseSettled += () => settled++;

        // Idle close settles immediately.
        sheet.RequestClose();
        Assert.Equal(1, settled);

        // A close requested mid-run defers to the terminal publish.
        sheet.OldKey = "oldkey";
        sheet.NewKey = "renamed";
        sheet.MarkWorkInFlightForTests();
        sheet.RequestClose();
        Assert.Equal(1, settled);
        sheet.PublishRun(sheet.RequestIdForTests, true, "oldkey", "renamed", null, "cancelled");
        Assert.Equal(2, settled);
    }

    [Fact]
    public void DirtyTabsTakePropertyStaleCopyAfterAnAppliedRename()
    {
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { },
            startInteractionBackgroundWork: false);
        var announced = new List<A11yEvent>();
        workspace.OpenPath("a.md");
        _ = workspace.EnsureActiveTabProperties(synchronousForTests: true);
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        tab.Text += "\nUnsaved body edit.";
        Assert.True(tab.IsDirty);

        // Contract 8's dirty branch with contract 9's identity
        // (adversarial round 1): the containment is spoken with
        // PROPERTY copy — the task-toggle wording would be factually
        // wrong here.
        Assert.True(tab.RebaselineAfterPropertyWrite("some-new-hash", announced.Add));
        Assert.True(tab.IsExternallyStale);
        A11yEvent.HostComposed stale = Assert.IsType<A11yEvent.HostComposed>(
            Assert.Single(announced));
        Assert.StartsWith(
            "Properties changed on disk", stale.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Task", stale.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspaceApplyReconcilesOpenCleanTabsToTheNewHash()
    {
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
        workspace.OpenPath("a.md");
        NotePropertiesViewModel properties =
            workspace.EnsureActiveTabProperties(synchronousForTests: true)!;
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);

        workspace.OpenBulkRenameSheet(synchronousForTests: true);
        BulkRenameViewModel sheet = workspace.BulkRenameSheet!;
        // The old key prefills from the active note's first property.
        Assert.Equal("oldkey", sheet.OldKey);
        sheet.NewKey = "movedkey";
        sheet.Preview();
        Assert.True(sheet.Apply());

        // Contract 8: the open clean tab re-baselines to the report
        // hash and its header re-derives the renamed key.
        RenameAffected applied = _session.ReadNoteParts("a.md") is { } parts
            ? new RenameAffected("a.md", "", "", true, parts.ContentHash)
            : throw new InvalidOperationException();
        Assert.Equal(applied.NewContentHash, tab.SavedContentHash);
        Assert.False(tab.IsDirty);
        Assert.Contains(properties.Rows, row => row.Key == "movedkey");
        workspace.CloseBulkRenameSheet();
        Assert.Null(workspace.BulkRenameSheet);
    }
}
