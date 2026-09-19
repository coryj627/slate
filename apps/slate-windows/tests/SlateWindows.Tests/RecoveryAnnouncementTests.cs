// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class RecoveryAnnouncementTests
{
    [Theory]
    [InlineData("save")]
    [InlineData("save-all")]
    [InlineData("close")]
    public void ConflictingSaveSpeaksOnceAndPreservesBothVersions(string action)
    {
        using var host = new Host();
        WorkspaceTabViewModel tab = host.OpenNote();
        tab.Text += "\nUnsaved local edit.";
        string local = tab.Text;
        const string external = "# Externally changed\n";
        File.WriteAllText(host.NotePath, external);
        host.Announced.Clear();

        switch (action)
        {
            case "save": host.Workspace.SaveActiveCommand.Execute(null); break;
            case "save-all": Assert.False(host.Workspace.SaveAll()); break;
            case "close": host.Workspace.CloseTabCommand.Execute(tab); break;
        }

        Assert.Equal(external, File.ReadAllText(host.NotePath));
        Assert.Equal(local, tab.Text);
        Assert.True(tab.IsDirty);
        Assert.Contains(tab, host.Workspace.ActiveGroup.Tabs);
        Assert.DoesNotContain(host.Announced, e => e is A11yEvent.NoteSaved);
        RenderedAnnouncement failure = Assert.Single(host.Rendered);
        Assert.StartsWith("Save blocked.", failure.Text);
        Assert.Contains("note0.md", failure.Text);
        Assert.DoesNotContain("dialog", failure.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(A11yPriority.High, failure.Priority);
    }

    [Fact]
    public void SuccessfulSaveKeepsItsExistingAnnouncement()
    {
        using var host = new Host();
        WorkspaceTabViewModel tab = host.OpenNote();
        tab.Text += "\nSaved edit.";
        host.Announced.Clear();
        host.Workspace.SaveActiveCommand.Execute(null);
        Assert.IsType<A11yEvent.NoteSaved>(Assert.Single(host.Announced));
        Assert.False(tab.IsDirty);
        Assert.EndsWith("Saved edit.", File.ReadAllText(host.NotePath));
    }

    [Fact]
    public void ReopeningMissingFileKeepsTheTabAndSpeaksMissingInsteadOfSuccess()
    {
        using var host = new Host();
        host.CloseNote();
        // Do not rescan: the live provider must win over stale index metadata.
        File.Delete(host.NotePath);
        host.Announced.Clear();
        host.Workspace.ReopenClosedTabCommand.Execute(null);
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(host.Workspace.ActiveGroup.ActiveTab);
        Assert.Equal("note0.md", tab.Path);
        Assert.True(tab.IsMissingFromDisk);
        Assert.Single(host.Announced.OfType<A11yEvent.ReopenTargetMissing>());
        Assert.DoesNotContain(host.Announced, e => e is A11yEvent.ReopenedFile);
    }

    [Fact]
    public void ReopeningUnreadableFileReportsFailureWithoutClaimingItIsMissing()
    {
        using var host = new Host();
        host.CloseNote();
        File.WriteAllBytes(host.NotePath, [0xff, 0xfe, 0xff]);
        host.Announced.Clear();
        host.Workspace.ReopenClosedTabCommand.Execute(null);
        Assert.DoesNotContain(host.Announced, e => e is A11yEvent.ReopenedFile or A11yEvent.ReopenTargetMissing);
        RenderedAnnouncement failure = Assert.Single(host.Rendered, e => e.Text.StartsWith("Could not reopen"));
        Assert.Contains("note0.md", failure.Text);
        Assert.Equal(A11yPriority.High, failure.Priority);
    }

    [Fact]
    public void ReopeningExistingFileKeepsItsExistingAnnouncement()
    {
        using var host = new Host();
        host.CloseNote();
        host.Announced.Clear();
        host.Workspace.ReopenClosedTabCommand.Execute(null);
        Assert.Single(host.Announced.OfType<A11yEvent.ReopenedFile>());
        Assert.DoesNotContain(host.Announced, e => e is A11yEvent.ReopenTargetMissing);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("reload")]
    [InlineData("shutdown")]
    public async Task DashboardFailureSpeaksOnlyWhenItsCurrentPublicationRuns(string retirement)
    {
        using var fixture = FixtureVault.Create(0, "dashboard-failure-announcement");
        using var session = VaultSession.OpenFilesystem(fixture.Root);
        var context = new PublicationContext();
        var announced = new List<A11yEvent>();
        SynchronizationContext? previous = SynchronizationContext.Current;
        DashboardViewModel document;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            document = new DashboardViewModel(session, "removed-dashboard", "Reading", announced.Add);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        try
        {
            document.Load();
            Action publish = await context.Next();
            await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Empty(announced);
            Assert.Empty(document.Sections);
            if (retirement == "shutdown") { document.Shutdown(); }
            if (retirement == "reload") { document.Load(); }
            publish();
            if (retirement != "live")
            {
                Assert.Empty(announced);
                Assert.Empty(document.Sections);
            }
            if (retirement == "shutdown") { return; }
            if (retirement == "reload")
            {
                Action latest = await context.Next();
                await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
                latest();
            }
            Assert.Equal(DashboardSectionState.Failed, Assert.Single(document.Sections).State);
            RenderedAnnouncement failure = SlateUniffiMethods.A11yRender(Assert.Single(announced));
            Assert.StartsWith("Dashboard Reading could not be loaded:", failure.Text);
            Assert.Equal(A11yPriority.High, failure.Priority);
        }
        finally
        {
            document.Shutdown();
            await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>D-12 makes a failed load audible when published; an unchanged
    /// failure re-published by the next refresh funnel is not news. A docked
    /// dashboard whose target stays broken must not interrupt the user at High
    /// on every note save and vault change.</summary>
    [Fact]
    public async Task AnUnchangedDashboardFailureIsAnnouncedOnceAcrossPublications()
    {
        using var fixture = FixtureVault.Create(0, "dashboard-failure-dedupe");
        using var session = VaultSession.OpenFilesystem(fixture.Root);
        var context = new PublicationContext();
        var announced = new List<A11yEvent>();
        SynchronizationContext? previous = SynchronizationContext.Current;
        DashboardViewModel document;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            document = new DashboardViewModel(session, "removed-dashboard", "Reading", announced.Add);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        try
        {
            for (int load = 0; load < 2; load++)
            {
                document.Load();
                Action publish = await context.Next();
                await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
                publish();
                Assert.Equal(DashboardSectionState.Failed, Assert.Single(document.Sections).State);
            }
            Assert.Single(announced.OfType<A11yEvent.BasesDashboardLoadFailed>());
        }
        finally
        {
            document.Shutdown();
            await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>The failed section shows the sentence the user hears: core
    /// owns the failure copy (D-12), so the visible message is its rendering,
    /// not a second host spelling that can drift.</summary>
    [Fact]
    public async Task TheFailedSectionShowsTheSentenceThatIsSpoken()
    {
        using var fixture = FixtureVault.Create(0, "dashboard-failure-message");
        using var session = VaultSession.OpenFilesystem(fixture.Root);
        var context = new PublicationContext();
        var announced = new List<A11yEvent>();
        SynchronizationContext? previous = SynchronizationContext.Current;
        DashboardViewModel document;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            document = new DashboardViewModel(session, "removed-dashboard", "Reading", announced.Add);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        try
        {
            document.Load();
            Action publish = await context.Next();
            await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
            publish();
            RenderedAnnouncement failure = SlateUniffiMethods.A11yRender(Assert.Single(announced));
            Assert.Equal(failure.Text, Assert.Single(document.Sections).Message);
        }
        finally
        {
            document.Shutdown();
            await document.WhenAllWorkDrained().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "recovery-announcements");
        private readonly VaultSession _session;
        internal List<A11yEvent> Announced { get; } = [];
        internal IEnumerable<RenderedAnnouncement> Rendered => Announced.Select(SlateUniffiMethods.A11yRender);
        internal WorkspaceViewModel Workspace { get; }
        internal string NotePath => Path.Combine(_fixture.Root, "note0.md");
        internal Host()
        {
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using var cancel = new CancelToken();
            _session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(_session, _fixture.Root, () => [], Announced.Add,
                dirtyCloseDecision: _ => WorkspaceDirtyNavigationDecision.Save,
                startInteractionBackgroundWork: false);
        }
        internal WorkspaceTabViewModel OpenNote()
        {
            Workspace.OpenPath("note0.md");
            return Assert.IsType<WorkspaceTabViewModel>(Workspace.ActiveGroup.ActiveTab);
        }
        internal void CloseNote() => Workspace.CloseTabCommand.Execute(OpenNote());
        public void Dispose()
        {
            Workspace.Dispose();
            _session.Dispose();
            _fixture.Dispose();
        }
    }
}
