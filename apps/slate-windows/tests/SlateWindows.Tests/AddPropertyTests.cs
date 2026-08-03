// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-4 (#736): the add-property sheet — validation is TOTAL and
/// PRE-CORE (feature contract 6), the new key appends at the END of
/// the block, and an async failure keeps the sheet open with the
/// draft and the verbatim copy.
/// </summary>
public sealed class AddPropertyTests
{
    [Fact]
    public void ValidationRefusalsArePreCoreAndVerbatim()
    {
        var announced = new List<A11yEvent>();
        var commits = new List<string>();
        bool dispatch = true;
        var sheet = new AddPropertyViewModel(
            new PropertyAddIntent("note.md", "hash-1", ["existing"]),
            (intent, key, _) =>
            {
                Assert.Equal("note.md", intent.Path);
                Assert.Equal("hash-1", intent.ContentHash);
                commits.Add(key);
                return dispatch;
            },
            announced.Add);

        sheet.Key = "";
        Assert.False(sheet.Add());
        Assert.Equal("Key can't be empty.", sheet.ValidationError);

        sheet.Key = "a.b";
        Assert.False(sheet.Add());
        Assert.Equal(
            "Dotted keys aren't supported yet — use a flat key.",
            sheet.ValidationError);

        sheet.Key = "existing";
        Assert.False(sheet.Add());
        Assert.Equal(
            "A property named `existing` already exists on this note.",
            sheet.ValidationError);

        // A null intent (no authoritative header data at open) makes
        // every Add refuse pre-core.
        var noNote = new AddPropertyViewModel(
            null,
            (_, key, _) =>
            {
                commits.Add(key);
                return true;
            },
            announced.Add);
        noNote.Key = "anything";
        Assert.False(noNote.Add());
        Assert.Equal("No note is loaded.", noNote.ValidationError);

        // Contract 6: zero commits reached the seam; every refusal
        // spoke its reason (the mac residue-site twin).
        Assert.Empty(commits);
        Assert.Equal(4, announced.OfType<A11yEvent.HostComposed>().Count());

        // Typing clears the error; a valid key commits.
        sheet.Key = "fresh";
        Assert.Null(sheet.ValidationError);
        Assert.True(sheet.Add());
        Assert.Equal(["fresh"], commits);

        // A REFUSED dispatch is never reported as success
        // (contract 6, adversarial round 1): the seam said no, the
        // sheet keeps the draft with the verbatim copy.
        dispatch = false;
        sheet.Key = "fresh2";
        Assert.False(sheet.Add());
        Assert.Equal(
            "The property was not added. Your draft is still here.",
            sheet.ValidationError);
        Assert.Equal("fresh2", sheet.Key);
    }

    [Fact]
    public void InitialValuesValidateShapeDerivedKindsPreCore()
    {
        // Round 3, contract 10: the chosen kind must SURVIVE the
        // authoritative re-read — shape-derived kinds require a
        // shape-valid seed, refused with the verbatim message.
        Assert.Null(AddPropertyViewModel.BuildInitialValue("fresh", "text", "", out var text));
        Assert.Equal(new PropertyValue.Text(""), text);
        Assert.Null(AddPropertyViewModel.BuildInitialValue("fresh", "number", "", out var zero));
        Assert.Equal(new PropertyValue.Integer(0), zero);
        Assert.Null(AddPropertyViewModel.BuildInitialValue("fresh", "number", "1.5", out var real));
        Assert.Equal(new PropertyValue.Float(1.5), real);
        Assert.Equal(
            "Must be a finite decimal number.",
            AddPropertyViewModel.BuildInitialValue("fresh", "number", "seven", out _));
        Assert.Null(AddPropertyViewModel.BuildInitialValue("fresh", "boolean", "true", out var flag));
        Assert.Equal(new PropertyValue.Boolean(true), flag);
        Assert.Equal(
            "Enter true or false.",
            AddPropertyViewModel.BuildInitialValue("fresh", "boolean", "yep", out _));
        Assert.Equal(
            "Date must be YYYY-MM-DD.",
            AddPropertyViewModel.BuildInitialValue("fresh", "date", "", out _));
        Assert.Null(
            AddPropertyViewModel.BuildInitialValue("fresh", "date", "2026-05-01", out var date));
        Assert.Equal(new PropertyValue.Date("2026-05-01"), date);
        Assert.Equal(
            "Enter a date and time like 2026-05-01T10:00:00.",
            AddPropertyViewModel.BuildInitialValue("fresh", "datetime", "soon", out _));
        Assert.Equal(
            "Wikilink target can't be empty.",
            AddPropertyViewModel.BuildInitialValue("fresh", "wikilink", "  ", out _));
        Assert.Null(AddPropertyViewModel.BuildInitialValue("fresh", "list", "", out var list));
        Assert.Equal([], Assert.IsType<PropertyValue.List>(list).Items);
        Assert.Equal(
            "Tag lists need at least one tag to keep their type.",
            AddPropertyViewModel.BuildInitialValue("fresh", "tag_list", "", out _));
        Assert.Null(
            AddPropertyViewModel.BuildInitialValue("fresh", "tag_list", "#alpha", out var tags));
        Assert.Equal(["alpha"], Assert.IsType<PropertyValue.TagList>(tags).Tags);
    }

    /// <summary>Contract 10, the round-4 DESIGN-PASS shape: instead
    /// of trusting a hand-mirrored classifier table, this matrix
    /// pins the CONTRACT itself against core end-to-end — for every
    /// (key, kind, seed), the sheet either REFUSES pre-core or the
    /// authoritative re-read stores exactly the chosen kind. A
    /// classifier drift in either direction fails this fact.</summary>
    [Theory]
    [InlineData("fresh", "text", "hello")]
    [InlineData("fresh", "number", "7")]
    [InlineData("fresh", "number", "1.5")]
    [InlineData("fresh", "number", "1.0")]
    [InlineData("fresh", "boolean", "true")]
    [InlineData("fresh", "date", "2026-05-01")]
    [InlineData("fresh", "datetime", "2026-05-01T10:00:00Z")]
    [InlineData("fresh", "datetime", "2026-05-01")]
    [InlineData("fresh", "wikilink", "other")]
    [InlineData("fresh", "list", "")]
    [InlineData("fresh", "list", "#alpha")]
    [InlineData("fresh", "tag_list", "alpha")]
    [InlineData("fresh", "tag_list", "")]
    [InlineData("fresh", "text", "2026-05-01")]
    [InlineData("fresh", "text", "2026-05-01T10:00:00Z")]
    [InlineData("fresh", "text", "[[other]]")]
    [InlineData("fresh", "text", "true")]
    [InlineData("fresh", "text", "123")]
    [InlineData("fresh", "text", "1.5")]
    [InlineData("tags", "tag_list", "alpha")]
    [InlineData("tags", "tag_list", "")]
    [InlineData("tags", "list", "x")]
    [InlineData("tags", "text", "x")]
    public void EveryAddEitherRefusesOrStoresExactlyTheChosenKind(
        string key, string kind, string seed)
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(
                0, $"add-kind-{key}-{kind.Replace('_', '-')}-{(uint)seed.GetHashCode():x8}");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note.md"), "---\nfirst: one\n---\nBody.\n");
            File.WriteAllText(Path.Combine(fixture.Root, "other.md"), "# Other\n");
            using VaultSession session = OpenScanned(fixture.Root);
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], _ => { },
                startInteractionBackgroundWork: false);
            workspace.OpenPath("note.md");
            _ = workspace.EnsureActiveTabProperties(synchronousForTests: true);

            workspace.OpenAddPropertySheet(synchronousForTests: true);
            AddPropertyViewModel sheet = workspace.AddPropertySheet!;
            sheet.Key = key;
            sheet.Kind = kind;
            sheet.Value = seed;
            if (!sheet.Add())
            {
                // Refused pre-core: nothing may have been written.
                Assert.NotNull(sheet.ValidationError);
                Assert.DoesNotContain(
                    key + ":",
                    session.ReadNoteParts("note.md").FmSource,
                    StringComparison.Ordinal);
                return;
            }
            WaitForUi(() => workspace.AddPropertySheet is null);
            Property stored = SlateUniffiMethods
                .ParseFrontmatterProperties(session.ReadNoteParts("note.md").FmSource)
                .Single(property => property.Key == key);
            Assert.Equal(kind, stored.Kind);
        });
    }

    [Fact]
    public void SheetShownPostsTheCanonicalEventOnce()
    {
        var announced = new List<A11yEvent>();
        var sheet = new AddPropertyViewModel(null, (_, _, _) => true, announced.Add);
        sheet.SheetShown();
        A11yEvent shown = Assert.Single(announced);
        Assert.IsType<A11yEvent.AddPropertySheetShown>(shown);
        Assert.Equal("Add property", SlateUniffiMethods.A11yRender(shown).Text);
    }

    [Fact]
    public void AddAppendsAtTheEndClosesTheSheetAndKeepsTheBody()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(0, "add-property");
            string notePath = Path.Combine(fixture.Root, "note.md");
            File.WriteAllText(
                notePath, "---\nfirst: one\nsecond: two\n---\nBody stays.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
            workspace.OpenPath("note.md");
            _ = workspace.EnsureActiveTabProperties(synchronousForTests: true);

            workspace.OpenAddPropertySheet(synchronousForTests: true);
            AddPropertyViewModel sheet = workspace.AddPropertySheet!;
            Assert.NotNull(sheet);
            Assert.Contains(
                announced, item => item is A11yEvent.AddPropertySheetShown);

            sheet.Key = "third";
            sheet.Kind = "number";
            Assert.True(sheet.Add());
            WaitForUi(() => workspace.AddPropertySheet is null);

            NotePartsBundle after = session.ReadNoteParts("note.md");
            // Contract 6: appended at the END; existing keys keep
            // position; the body is untouched.
            int first = after.FmSource.IndexOf("first:", StringComparison.Ordinal);
            int second = after.FmSource.IndexOf("second:", StringComparison.Ordinal);
            int third = after.FmSource.IndexOf("third:", StringComparison.Ordinal);
            Assert.True(first >= 0 && second > first && third > second);
            Assert.Equal("Body stays.\n", after.Body);
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text
                    == "Property third updated.");
        });
    }

    [Fact]
    public void AsyncAddFailureKeepsTheSheetOpenWithTheDraft()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(0, "add-property-fault");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note.md"), "---\nfirst: one\n---\nBody.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
            workspace.OpenPath("note.md");
            _ = workspace.EnsureActiveTabProperties(synchronousForTests: true);
            WorkspaceTabViewModel tab =
                Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
            tab.PropertyWriteFaultForTests =
                () => throw new InvalidOperationException("publish crash");

            workspace.OpenAddPropertySheet(synchronousForTests: true);
            AddPropertyViewModel sheet = workspace.AddPropertySheet!;
            sheet.Key = "faulted";
            Assert.True(sheet.Add());
            WaitForUi(() => sheet.ValidationError is not null
                || workspace.AddPropertySheet is null
                || announced.OfType<A11yEvent.PropertyEditFailed>().Any());

            // §2.4: the sheet stays open, the draft survives, the
            // verbatim failure copy shows, one failure announcement.
            Assert.Same(sheet, workspace.AddPropertySheet);
            Assert.Equal("faulted", sheet.Key);
            Assert.Equal(
                "The property was not added. Your draft is still here.",
                sheet.ValidationError);
            Assert.Single(announced.OfType<A11yEvent.PropertyEditFailed>());
        });
    }

    [Fact]
    public void AddConflictsGetTheResolutionDialogAndKeepMineRetriesTheAdd()
    {
        RunSta(() =>
        {
            using FixtureVault fixture = FixtureVault.Create(0, "add-property-conflict");
            string notePath = Path.Combine(fixture.Root, "note.md");
            File.WriteAllText(notePath, "---\nfirst: one\n---\nBody.\n");
            using VaultSession session = OpenScanned(fixture.Root);
            var announced = new List<A11yEvent>();
            using var workspace = new WorkspaceViewModel(
                session, fixture.Root, () => [], announced.Add,
                startInteractionBackgroundWork: false);
            (Action KeepMine, Action Reload)? resolution = null;
            workspace.PropertyConflictDialog = (_, _, keepMine, reload) =>
                resolution = (keepMine, reload);
            workspace.OpenPath("note.md");
            _ = workspace.EnsureActiveTabProperties(synchronousForTests: true);
            WorkspaceTabViewModel tab =
                Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);

            workspace.OpenAddPropertySheet(synchronousForTests: true);
            AddPropertyViewModel sheet = workspace.AddPropertySheet!;

            // The disk moves AFTER the intent was captured; the tab
            // is unaware, so the pre-core gates pass and core CAS
            // refuses — the add must get the SAME resolution surface
            // as row edits (contract 1, adversarial round 1).
            _ = session.SaveText(
                "note.md", "---\nfirst: one\n---\nMoved body.\n", tab.SavedContentHash);
            sheet.Key = "second";
            Assert.True(sheet.Add());
            WaitForUi(() => resolution is not null);
            Assert.Same(sheet, workspace.AddPropertySheet);
            Assert.Equal(
                "The property was not added. Your draft is still here.",
                sheet.ValidationError);

            // Keep Mine re-issues the ADD against a fresh hash.
            resolution!.Value.KeepMine();
            WaitForUi(() => workspace.AddPropertySheet is null);
            Assert.Contains(
                "second:",
                File.ReadAllText(notePath),
                StringComparison.Ordinal);
            Assert.Contains(
                announced,
                item => SlateUniffiMethods.A11yRender(item).Text
                    == "Property second updated.");
        });
    }

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
