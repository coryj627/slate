// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using SlateWindows.Commands;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR C (#746), contracts C-9 and C-12: the Graph menu — its ids,
/// commands and accelerator scraped from the XAML; the Verbosity submenu
/// declared empty and BUILT from core's vector through the window's one
/// populating helper, following a level change, rebuilt for a second
/// workspace with nothing of the first retained; the Where Am I? item
/// following the seam's availability.
/// </summary>
public sealed class GraphMenuTests
{
    private static string MainWindowXaml() =>
        Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateWindows", "MainWindow.xaml");

    private static XElement MenuByAutomationId(string automationId) =>
        XDocument.Load(MainWindowXaml()).Descendants()
            .Single(element => element.Attributes().Any(attribute =>
                attribute.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal)
                && attribute.Value == automationId));

    private static string? AutomationIdOf(XElement element) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName.EndsWith("AutomationId", StringComparison.Ordinal))?.Value;

    /// <summary>A workspace over the graph vault (C-9's precedent), the relay's
    /// lines captured.</summary>
    private sealed class Host : IDisposable
    {
        public string Root { get; }

        public VaultSession Session { get; }

        public WorkspaceViewModel Workspace { get; }

        public List<string> GraphLines { get; } = [];

        public Host(string label)
        {
            string source = Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures", "graph_vault");
            Root = Path.Combine(Path.GetTempPath(), $"slate-graph-menu-{label}-{Guid.NewGuid():N}");
            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(Root, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
            Session = VaultSession.OpenFilesystem(Root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => GraphLines.Add(line.Text));
        }

        public void Settle()
        {
            if (Workspace.GraphDocument is { } document)
            {
                PumpedDispatcher.PumpUntilDrained(document.WhenAllWorkDrained());
            }
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            Workspace.Dispose();
            Session.Dispose();
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>WPF controls need an STA thread (the surface tests' shape).</summary>
    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
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
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    // --- C-12: the menu's shape, scraped ------------------------------------

    [Fact]
    public void TheGraphMenusIdsCommandsAndAccelerator()
    {
        XElement menu = MenuByAutomationId("GraphMenu");
        Assert.Equal("_Graph", menu.Attribute("Header")?.Value);
        (string Id, string Header, string Command, string? Gesture)[] items = menu.Elements()
            .Where(element => element.Name.LocalName == "MenuItem")
            .Select(element => (
                AutomationIdOf(element) ?? string.Empty,
                element.Attribute("Header")?.Value ?? string.Empty,
                element.Attribute("Command")?.Value ?? string.Empty,
                element.Attribute("InputGestureText")?.Value))
            .ToArray();
        Assert.Equal(
            [
                ("GraphOpenTabMenuItem", "_Open Graph", "{Binding Workspace.OpenGraphCommand}", null),
                ("GraphOrphansMenuItem", "Orphaned _Notes", "{Binding Workspace.GraphOrphansCommand}", null),
                ("GraphUnresolvedMenuItem", "_Unresolved Links", "{Binding Workspace.GraphUnresolvedCommand}", null),
                ("GraphMostLinkedMenuItem", "_Most Linked Notes", "{Binding Workspace.GraphMostLinkedCommand}", null),
                ("GraphWhereAmIMenuItem", "_Where Am I?", "{Binding Workspace.GraphWhereAmICommand}", "{cmd:ChordText slate.graph.whereAmI}"),
                ("GraphVerbosityMenu", "_Verbosity", string.Empty, null),
            ],
            items);
        // Two separators: after Open Graph and before Verbosity.
        string[] order = menu.Elements().Select(e => e.Name.LocalName == "Separator" ? "-" : AutomationIdOf(e) ?? "?").ToArray();
        Assert.Equal(
            ["GraphOpenTabMenuItem", "-", "GraphOrphansMenuItem", "GraphUnresolvedMenuItem", "GraphMostLinkedMenuItem", "GraphWhereAmIMenuItem", "-", "GraphVerbosityMenu"],
            order);
        // Every command is the registrar's for the same id (drift test 2's
        // shape): the resolver text names the same workspace member.
        string registrar = File.ReadAllText(Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateWindows", "Commands", "SlateCommandRegistrar.cs"));
        foreach ((string id, string member) in new[]
        {
            (ChordTable.Ids.GraphOpenTab, "OpenGraphCommand"),
            (ChordTable.Ids.GraphOrphans, "GraphOrphansCommand"),
            (ChordTable.Ids.GraphUnresolved, "GraphUnresolvedCommand"),
            (ChordTable.Ids.GraphMostLinked, "GraphMostLinkedCommand"),
            (ChordTable.Ids.GraphWhereAmI, "GraphWhereAmICommand"),
        })
        {
            string constant = "ChordTable.Ids." + typeof(ChordTable.Ids).GetFields().Single(f => Equals(f.GetValue(null), id)).Name;
            Assert.Contains($"[{constant}] = host => host.Workspace?.{member}", registrar, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheSubmenuDeclaresNoLiteralLevel()
    {
        XElement submenu = MenuByAutomationId("GraphVerbosityMenu");
        Assert.Equal("GraphVerbosityMenuItem", submenu.Attributes().Single(a => a.Name.LocalName == "Name").Value);
        Assert.Empty(submenu.Elements());
        Assert.Null(submenu.Attribute("ItemsSource"));
        // The populating loop iterates the preferences' Choices (C-9's census
        // shape, asserted here until the census lands).
        string builder = File.ReadAllText(Path.Combine(
            SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateWindows", "Graph", "GraphVerbosityMenu.cs"));
        Assert.Contains("foreach (GraphVerbosityChoice choice in preferences.Choices)", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("\"terse\"", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Terse\"", builder, StringComparison.Ordinal);
    }

    // --- C-9: the built submenu ------------------------------------------------

    [Fact]
    public void TheBuiltMenuEqualsTheVectorInOrder()
    {
        RunSta(() =>
        {
            using var host = new Host("built");
            GraphPreferencesViewModel preferences = host.Workspace.GraphPreferences;
            var submenu = new MenuItem { Header = "_Verbosity" };
            GraphVerbosityMenu.Populate(submenu, preferences);
            CheckMenuItem[] items = submenu.Items.Cast<CheckMenuItem>().ToArray();
            GraphVerbositySpec[] vector = SlateUniffiMethods.GraphVerbosities();
            Assert.Equal(vector.Select(l => l.Title), items.Select(i => (string)i.Header));
            Assert.Equal(vector.Select(l => l.Tag), items.Select(i => (string)i.CommandParameter!));
            Assert.Equal(vector.Select(l => "GraphVerbosity." + l.Tag), items.Select(AutomationProperties.GetAutomationId));
            Assert.All(items, item => Assert.True(item.IsCheckable));
            Assert.All(items, item => Assert.Same(preferences.SetVerbosityCommand, item.Command));
            Assert.Equal([false, true, false], items.Select(i => i.IsChecked));
            // The check follows a level change, and a click is the setter.
            preferences.SetVerbosityCommand.Execute("verbose");
            Assert.Equal([false, false, true], items.Select(i => i.IsChecked));
            items[0].Command!.Execute(items[0].CommandParameter);
            Assert.Equal(GraphVerbosity.Terse, preferences.Verbosity);
            Assert.Equal([true, false, false], items.Select(i => i.IsChecked));
            // m1's rule through the built item: a re-selection re-asserts.
            items[0].Command!.Execute(items[0].CommandParameter);
            Assert.Equal([true, false, false], items.Select(i => i.IsChecked));
            Assert.Equal(GraphVerbosity.Terse, preferences.Verbosity);
        });
    }

    [Fact]
    public void TheMenuFollowsAVaultSwitchAndRetainsNoOldWorkspace()
    {
        RunSta(() =>
        {
            var submenu = new MenuItem { Header = "_Verbosity" };
            var first = new Host("first");
            GraphPreferencesViewModel firstPreferences = first.Workspace.GraphPreferences;
            GraphVerbosityMenu.Populate(submenu, firstPreferences);
            CheckMenuItem[] firstItems = submenu.Items.Cast<CheckMenuItem>().ToArray();
            firstPreferences.SetVerbosityCommand.Execute("verbose");
            Assert.Equal([false, false, true], firstItems.Select(i => i.IsChecked));

            // The switch: the old workspace unwired, the new one populated.
            GraphVerbosityMenu.Clear(submenu);
            first.Dispose();
            Assert.Empty(submenu.Items);
            Assert.All(firstItems, item => Assert.Null(item.Command));
            using var second = new Host("second");
            GraphPreferencesViewModel secondPreferences = second.Workspace.GraphPreferences;
            GraphVerbosityMenu.Populate(submenu, secondPreferences);
            CheckMenuItem[] secondItems = submenu.Items.Cast<CheckMenuItem>().ToArray();
            Assert.Equal(3, secondItems.Length);
            Assert.All(secondItems, item => Assert.Same(secondPreferences.SetVerbosityCommand, item.Command));
            Assert.Equal([false, true, false], secondItems.Select(i => i.IsChecked));
            // The old workspace's level moves nothing in the rebuilt menu, and
            // the old items — their bindings cleared — observe nothing.
            Assert.Equal([false, false, false], firstItems.Select(i => i.IsChecked));
            firstPreferences.SetVerbosityCommand.Execute("terse");
            Assert.Equal([false, true, false], secondItems.Select(i => i.IsChecked));
            Assert.Equal([false, false, false], firstItems.Select(i => i.IsChecked));
            secondPreferences.SetVerbosityCommand.Execute("terse");
            Assert.Equal([true, false, false], secondItems.Select(i => i.IsChecked));
        });
    }

    // --- C-8: the Where Am I? item follows the seam's availability ---------

    [Fact]
    public void TheWhereAmIItemFollowsTheSeamsAvailability()
    {
        RunSta(() =>
        {
            using var host = new Host("where-am-i-item");
            var item = new MenuItem { Header = "_Where Am I?", Command = host.Workspace.GraphWhereAmICommand };
            // No graph tab: the seam does not answer — listed and disabled.
            Assert.False(item.IsEnabled);
            host.Workspace.OpenGraph();
            // In flight: still disabled.
            Assert.True(host.Workspace.GraphDocument!.IsRequestInFlight);
            Assert.False(item.IsEnabled);
            host.Settle();
            Assert.True(item.IsEnabled);
            // A needle in flight disables; landed re-enables.
            host.Workspace.GraphNavigator.SetNameQuery("hub");
            Assert.False(item.IsEnabled);
            host.Settle();
            Assert.True(item.IsEnabled);
            // The close retires the document: disabled again.
            host.Workspace.CloseActiveTabCommand.Execute(null);
            host.Settle();
            Assert.False(item.IsEnabled);
        });
    }
}
