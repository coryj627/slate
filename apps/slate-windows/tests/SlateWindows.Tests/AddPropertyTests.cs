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
        var sheet = new AddPropertyViewModel(
            () => ["existing"],
            (key, _) => commits.Add(key),
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

        var noNote = new AddPropertyViewModel(
            () => null, (key, _) => commits.Add(key), announced.Add);
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
    }

    [Fact]
    public void DefaultValuesFollowTheMacSheetShape()
    {
        Assert.Equal(new PropertyValue.Text(""), AddPropertyViewModel.DefaultValueFor("text"));
        Assert.Equal(
            new PropertyValue.Integer(0), AddPropertyViewModel.DefaultValueFor("number"));
        Assert.Equal(
            new PropertyValue.Boolean(false),
            AddPropertyViewModel.DefaultValueFor("boolean"));
        Assert.Equal(new PropertyValue.Date(""), AddPropertyViewModel.DefaultValueFor("date"));
        Assert.Equal(
            new PropertyValue.Datetime(""),
            AddPropertyViewModel.DefaultValueFor("datetime"));
        Assert.Equal(
            new PropertyValue.Wikilink(""),
            AddPropertyViewModel.DefaultValueFor("wikilink"));
        Assert.IsType<PropertyValue.List>(AddPropertyViewModel.DefaultValueFor("list"));
        Assert.IsType<PropertyValue.TagList>(
            AddPropertyViewModel.DefaultValueFor("tag_list"));
        Assert.Equal(
            new PropertyValue.Text(""), AddPropertyViewModel.DefaultValueFor("unknown"));
    }

    [Fact]
    public void SheetShownPostsTheCanonicalEventOnce()
    {
        var announced = new List<A11yEvent>();
        var sheet = new AddPropertyViewModel(() => [], (_, _) => { }, announced.Add);
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
