// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Threading.Channels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 2 (#1250, contract R-3): a tag activation writes what core's
/// <c>sidebar_tag_filter_activation</c> answers — the query <c>#tag</c>
/// in core's grammar (<c>sidebar_filter.rs</c>, <c>parse_sidebar_filter</c>),
/// or an out-of-band tag scope for a tag containing whitespace — exactly
/// as mac's <c>activateSidebarTagScope</c>. The host used to write
/// <c>tag:"x"</c>, which the grammar reads as a name word, so every tag
/// route filtered to zero files. These facts run the real core filter
/// through the FFI, so a composition the grammar does not understand
/// fails on the results, not only on the text.
/// </summary>
public sealed class SidebarTagFilterTests
{
    [Fact]
    public async Task ActivateTag_ComposesCoreGrammar()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-grammar");
        Write(fixture, "tagged.md", "---\ntags: [atag]\n---\n\n# Tagged\n");
        Write(fixture, "nested.md", "---\ntags: [atag/child]\n---\n\n# Nested\n");
        Write(fixture, "atag.md", "# Name only\n\nNo tags.\n");
        using VaultSession session = OpenScanned(fixture);
        FilesSidebarViewModel sidebar = await NewSidebar(session, fixture, _ => { });

        // The query #tag, shown in the field (editable, and it teaches the
        // grammar), finds the tag and its descendants — and not the note
        // that merely has the tag's word for a name.
        sidebar.ActivateTag("atag");
        Assert.Equal("#atag", sidebar.FilterText);
        Assert.Null(sidebar.ScopeTag);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "nested.md", "tagged.md" }, Paths(sidebar));

        // A second activation replaces the first.
        sidebar.ActivateTag("atag/child");
        Assert.Equal("#atag/child", sidebar.FilterText);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "nested.md" }, Paths(sidebar));

        // Whitespace is core's call: the answer for a spaced tag is the
        // empty field and the scope (the next fact runs that shape).
        SidebarTagFilterActivation spaced = SlateUniffiMethods.SidebarTagFilterActivation("two words");
        Assert.Equal(string.Empty, spaced.FilterText);
        Assert.Equal("two words", spaced.ScopeTag);
    }

    /// <summary>
    /// The whitespace route actually filters, and the scope is complete
    /// filter state: it makes the filter active with an empty field; the
    /// status line (core's summary) and the spoken count (core's
    /// FileListCount with the scope) both name the tag, even when nothing
    /// matches; text typed afterwards narrows within the scope; the request
    /// is (query, scope), so a switch between two scopes of equal count
    /// still speaks, naming the new tag; and a user clear — the field
    /// emptied, or Clear Sidebar Filter through its command — drops text
    /// and scope together.
    /// </summary>
    [Fact]
    public async Task ActivateTag_ScopesAWhitespaceTagAndTypingNarrowsWithinIt()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-scope");
        Write(fixture, "spaced.md", "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        Write(fixture, "spaced note.md", "---\ntags: [\"two words\"]\n---\n\n# Second\n");
        Write(fixture, "note.md", "---\ntags: [two]\n---\n\n# Untagged by the scope\n");
        Write(fixture, "fox.md", "---\ntags: [\"red fox\"]\n---\n\n# Fox\n");
        Write(fixture, "sky.md", "---\ntags: [\"blue sky\"]\n---\n\n# Sky\n");
        using VaultSession session = OpenScanned(fixture);
        var announced = new List<A11yEvent>();
        FilesSidebarViewModel sidebar = await NewSidebar(session, fixture, announced.Add);

        sidebar.ActivateTag("two words");
        Assert.Equal(string.Empty, sidebar.FilterText);
        Assert.Equal("two words", sidebar.ScopeTag);
        Assert.True(sidebar.IsFilterActive);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "spaced.md", "spaced note.md" }, Paths(sidebar));
        Assert.Equal("2 results for #two words.", sidebar.Status);
        AssertSpoke(announced, new A11yEvent.FileListCount(2, "two words"),
            "File list, 2 items. Filtered by tag two words.");

        // Typing filters within the scope: core ANDs the query with it.
        sidebar.FilterText = "note";
        Assert.Equal("two words", sidebar.ScopeTag);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "spaced note.md" }, Paths(sidebar));
        Assert.Equal("1 result for #two words.", sidebar.Status);

        // Nothing matching within the scope still names it: shown (core's
        // summary) and spoken (core's count with the scope).
        sidebar.FilterText = "nosuchword";
        await sidebar.FilterCompletion;
        Assert.Empty(sidebar.FilterResults);
        Assert.Equal("No results for #two words.", sidebar.Status);
        AssertSpoke(announced, new A11yEvent.FileListCount(0, "two words"),
            "File list, 0 items. Filtered by tag two words.");

        // Emptying the field is the user's clear: the scope goes too.
        sidebar.FilterText = string.Empty;
        Assert.Null(sidebar.ScopeTag);
        Assert.False(sidebar.IsFilterActive);
        await sidebar.FilterCompletion;
        Assert.Empty(sidebar.FilterResults);

        // Clear Sidebar Filter drops both, from either state.
        sidebar.ActivateTag("two words");
        Assert.True(sidebar.ClearFilterCommand.CanExecute(null));
        sidebar.ClearFilterCommand.Execute(null);
        Assert.Null(sidebar.ScopeTag);
        Assert.False(sidebar.IsFilterActive);
        sidebar.ActivateTag("two words");
        sidebar.FilterText = "note";
        sidebar.ClearFilterCommand.Execute(null);
        Assert.Equal(string.Empty, sidebar.FilterText);
        Assert.Null(sidebar.ScopeTag);
        Assert.False(sidebar.IsFilterActive);
        Assert.False(sidebar.ClearFilterCommand.CanExecute(null));

        // A plain tag after a scope replaces it.
        sidebar.ActivateTag("two words");
        sidebar.ActivateTag("two");
        Assert.Null(sidebar.ScopeTag);
        Assert.Equal("#two", sidebar.FilterText);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "note.md" }, Paths(sidebar));

        // The request is (query, scope): switching between two scopes with
        // the same count still speaks the new one.
        sidebar.ActivateTag("red fox");
        await sidebar.FilterCompletion;
        AssertSpoke(announced, new A11yEvent.FileListCount(1, "red fox"),
            "File list, 1 item. Filtered by tag red fox.");
        sidebar.ActivateTag("blue sky");
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "sky.md" }, Paths(sidebar));
        AssertSpoke(announced, new A11yEvent.FileListCount(1, "blue sky"),
            "File list, 1 item. Filtered by tag blue sky.");
    }

    /// <summary>
    /// Only an EMPTY field is the user's clear (codex PR 2 round 1). A
    /// whitespace-only edit — a leading space before the words — keeps
    /// the scope: core runs the trimmed query, so the results stay scoped
    /// and the words then narrow within it, never across the vault.
    /// </summary>
    [Fact]
    public async Task ActivateTag_ScopeSurvivesAWhitespaceOnlyEdit()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-scope-space");
        Write(fixture, "spaced.md", "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        Write(fixture, "spaced note.md", "---\ntags: [\"two words\"]\n---\n\n# Second\n");
        Write(fixture, "note.md", "---\ntags: [two]\n---\n\n# Outside the scope\n");
        using VaultSession session = OpenScanned(fixture);
        FilesSidebarViewModel sidebar = await NewSidebar(session, fixture, _ => { });
        sidebar.ActivateTag("two words");
        await sidebar.FilterCompletion;

        sidebar.FilterText = " ";
        Assert.Equal("two words", sidebar.ScopeTag);
        Assert.True(sidebar.IsFilterActive);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "spaced.md", "spaced note.md" }, Paths(sidebar));

        sidebar.FilterText = " note";
        Assert.Equal("two words", sidebar.ScopeTag);
        await sidebar.FilterCompletion;
        Assert.Equal(new[] { "spaced note.md" }, Paths(sidebar));
    }

    /// <summary>
    /// Clear Sidebar Filter is seen and heard (codex PR 2 round 1). With
    /// the text and scope gone no filter run follows, so the status line
    /// kept the cleared filter's summary and nothing was said. Now the
    /// status shows core's SidebarFilterCleared sentence and that event is
    /// spoken exactly once — and, the listener having heard the filter
    /// end, the same scope activated again speaks its count again.
    /// </summary>
    [Fact]
    public async Task ClearFilterCommand_ShowsAndSpeaksTheClearOnce()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-scope-clear");
        Write(fixture, "spaced.md", "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        Write(fixture, "spaced note.md", "---\ntags: [\"two words\"]\n---\n\n# Second\n");
        using VaultSession session = OpenScanned(fixture);
        var announced = new List<A11yEvent>();
        FilesSidebarViewModel sidebar = await NewSidebar(session, fixture, announced.Add);
        sidebar.ActivateTag("two words");
        await sidebar.FilterCompletion;
        Assert.Equal("2 results for #two words.", sidebar.Status);
        int before = announced.Count;

        sidebar.ClearFilterCommand.Execute(null);
        await sidebar.FilterCompletion;

        A11yEvent cleared = Assert.Single(announced.Skip(before));
        Assert.IsType<A11yEvent.SidebarFilterCleared>(cleared);
        Assert.Equal("Filter cleared.", SlateUniffiMethods.A11yRender(cleared).Text);
        Assert.Equal("Filter cleared.", sidebar.Status);
        Assert.Empty(sidebar.FilterResults);

        sidebar.ActivateTag("two words");
        await sidebar.FilterCompletion;
        AssertSpoke(announced, new A11yEvent.FileListCount(2, "two words"),
            "File list, 2 items. Filtered by tag two words.");
    }

    /// <summary>
    /// The field emptied by the user — typed or backspaced to nothing — is
    /// the promised second clear route, and it clears exactly like the
    /// command (codex PR 2 round 2): the scope drops, the status line's
    /// stale scoped summary gives way to core's "Filter cleared.", and that
    /// is spoken exactly once.
    /// </summary>
    [Fact]
    public async Task EmptyingTheField_ClearsLikeTheCommand()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-scope-emptied");
        Write(fixture, "spaced.md", "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        Write(fixture, "spaced note.md", "---\ntags: [\"two words\"]\n---\n\n# Second\n");
        using VaultSession session = OpenScanned(fixture);
        var announced = new List<A11yEvent>();
        FilesSidebarViewModel sidebar = await NewSidebar(session, fixture, announced.Add);
        sidebar.ActivateTag("two words");
        sidebar.FilterText = "n";
        await sidebar.FilterCompletion;
        Assert.Equal("1 result for #two words.", sidebar.Status);
        int before = announced.Count;

        sidebar.FilterText = string.Empty;
        await sidebar.FilterCompletion;

        Assert.Null(sidebar.ScopeTag);
        Assert.False(sidebar.IsFilterActive);
        Assert.Empty(sidebar.FilterResults);
        Assert.Equal("Filter cleared.", sidebar.Status);
        Assert.IsType<A11yEvent.SidebarFilterCleared>(Assert.Single(announced.Skip(before)));
    }

    /// <summary>
    /// Codex PR 2 round 2, interleaving 1: Clear while a tree refresh is
    /// still to publish holds "Filter cleared." for that publication — and
    /// the next tag the user activates, before the publication, owns the
    /// status line. The publication's automatic refilter must then show and
    /// speak the tag's scoped count, not restore the cleared sentence over
    /// it in silence.
    /// </summary>
    [Fact]
    public async Task AClearHeldForAPublicationYieldsToTheNextTag()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-clear-held");
        Write(fixture, "spaced.md", "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        Write(fixture, "spaced note.md", "---\ntags: [\"two words\"]\n---\n\n# Second\n");
        Write(fixture, "sky.md", "---\ntags: [\"blue sky\"]\n---\n\n# Sky\n");
        Write(fixture, "sky two.md", "---\ntags: [\"blue sky\"]\n---\n\n# Sky two\n");
        using VaultSession session = OpenScanned(fixture);
        var announced = new List<A11yEvent>();
        using ControlledSidebar rig = await ControlledSidebar.StartAsync(session, announced);
        FilesSidebarViewModel sidebar = rig.Sidebar;
        sidebar.ActivateTag("two words");
        await rig.CompleteNextFilterAsync();
        Assert.Equal("2 results for #two words.", sidebar.Status);

        sidebar.Refresh();
        sidebar.ClearFilterCommand.Execute(null);
        Assert.Equal("Filter cleared.", sidebar.Status);
        sidebar.ActivateTag("blue sky");
        _ = await rig.NextFilterDelayAsync();
        await rig.Tree.PublishNext();
        await rig.CompleteNextFilterAsync();

        Assert.Equal("2 results for #blue sky.", sidebar.Status);
        AssertSpoke(announced, new A11yEvent.FileListCount(2, "blue sky"),
            "File list, 2 items. Filtered by tag blue sky.");
    }

    /// <summary>
    /// Codex PR 2 round 2, interleaving 2: Clear AFTER a refresh has
    /// published, while that refresh is still finishing. Nothing is left
    /// to overwrite the sentence, so nothing is held — a hold here waited
    /// for the NEXT refresh, which then put "Filter cleared." back over
    /// its own status. The finishing window is pinned by holding the
    /// refresh's completion lock across the publication and the clear.
    /// </summary>
    [Fact]
    public async Task AClearAfterThePublicationIsNotRevivedByALaterRefresh()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "tag-clear-late");
        Write(fixture, "spaced.md", "---\ntags: [\"two words\"]\n---\n\n# Spaced\n");
        Write(fixture, "spaced note.md", "---\ntags: [\"two words\"]\n---\n\n# Second\n");
        using VaultSession session = OpenScanned(fixture);
        using ControlledSidebar rig = await ControlledSidebar.StartAsync(session, []);
        FilesSidebarViewModel sidebar = rig.Sidebar;
        sidebar.ActivateTag("two words");
        await rig.CompleteNextFilterAsync();

        sidebar.Refresh();
        Action publish = await rig.Tree.Next();
        object completionGate = typeof(FilesSidebarViewModel)
            .GetField("_treeRefreshCancellationGate", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(sidebar)!;
        Monitor.Enter(completionGate);
        try
        {
            publish();
            sidebar.ClearFilterCommand.Execute(null);
            Assert.True(sidebar.IsRefreshingTree, "The clear must land while the published refresh is still finishing.");
            Assert.Equal("Filter cleared.", sidebar.Status);
        }
        finally
        {
            Monitor.Exit(completionGate);
        }

        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        sidebar.Refresh(reportCount: true);
        await rig.Tree.PublishNext();
        await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("2 top-level items.", sidebar.Status);
    }

    /// <summary>A sidebar whose tree publications and filter runs the fact
    /// releases one at a time: the tree and filter each post to their own
    /// <see cref="PublicationContext"/>, the filter's debounce is a delay
    /// the fact completes, and both workers run inline.</summary>
    private sealed class ControlledSidebar : IDisposable
    {
        private readonly Channel<TaskCompletionSource> _delays = Channel.CreateUnbounded<TaskCompletionSource>();

        private ControlledSidebar(VaultSession session, List<A11yEvent> announced)
        {
            Sidebar = new FilesSidebarViewModel(
                session,
                announced.Add,
                filterUiContext: Filter,
                treeUiContext: Tree,
                treeWorker: (work, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    work();
                    return Task.CompletedTask;
                },
                filterWorker: (work, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    work();
                    return Task.CompletedTask;
                },
                filterDelay: token =>
                {
                    var delay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Assert.True(_delays.Writer.TryWrite(delay));
                    return delay.Task.WaitAsync(token);
                });
        }

        public FilesSidebarViewModel Sidebar { get; }
        public PublicationContext Tree { get; } = new();
        public PublicationContext Filter { get; } = new();

        public static async Task<ControlledSidebar> StartAsync(VaultSession session, List<A11yEvent> announced)
        {
            var rig = new ControlledSidebar(session, announced);
            await rig.Tree.PublishNext();
            await rig.Sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            return rig;
        }

        public Task<TaskCompletionSource> NextFilterDelayAsync() =>
            _delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        /// <summary>Release the next filter run's debounce and publish its
        /// outcome.</summary>
        public async Task CompleteNextFilterAsync()
        {
            (await NextFilterDelayAsync()).SetResult();
            await Filter.PublishNext();
            await Sidebar.FilterCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        }

        public void Dispose() =>
            Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork.Wait(TimeSpan.FromSeconds(10));
    }

    /// <summary>The last announcement is the typed event carrying the
    /// scope, and core's rendering of it names the tag — the text a
    /// screen reader hears, not a host composition.</summary>
    private static void AssertSpoke(List<A11yEvent> announced, A11yEvent expected, string speech)
    {
        Assert.Equal(expected, announced[^1]);
        Assert.Equal(speech, SlateUniffiMethods.A11yRender(announced[^1]).Text);
    }

    private static string[] Paths(FilesSidebarViewModel sidebar) =>
        [.. sidebar.FilterResults.Select(row => row.Path)];

    private static void Write(FixtureVault fixture, string name, string content) =>
        File.WriteAllText(Path.Combine(fixture.Root, name), content);

    private static VaultSession OpenScanned(FixtureVault fixture)
    {
        VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }

    private static async Task<FilesSidebarViewModel> NewSidebar(
        VaultSession session, FixtureVault fixture, Action<A11yEvent> announce)
    {
        var sidebar = new FilesSidebarViewModel(
            session,
            announce,
            localAppDataRoot: Path.Combine(fixture.Root, "device-state"));
        await sidebar.TreeRefreshCompletion;
        return sidebar;
    }
}
