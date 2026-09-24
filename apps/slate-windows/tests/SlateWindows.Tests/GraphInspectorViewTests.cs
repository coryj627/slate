// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR E (#746), rule I Terms I1, I5, I7; rules X, Y, Z, K; contracts
/// E-11, E-15; ED-10; E-D6, E-D8: the inspector's VIEW — the inventory's
/// names, the automation ids, the notices and their order, the gate over
/// the four sections, the routes from every control into the view model,
/// the sliders' value text and keys, the group rows' composed labels,
/// core's vectors in the pickers and the keys' landings on add and remove.
/// </summary>
public sealed class GraphInspectorViewTests
{
    private sealed class Host : IDisposable
    {
        public FixtureVault Vault { get; }

        public VaultSession Session { get; }

        public WorkspaceViewModel Workspace { get; }

        public List<string> GraphLines { get; } = [];

        public Host(int notes, string label, Action<string>? beforeOpen = null)
        {
            Vault = FixtureVault.Create(notes, label);
            beforeOpen?.Invoke(Vault.Root);
            Session = VaultSession.OpenFilesystem(Vault.Root);
            using var cancel = new CancelToken();
            Session.ScanInitial(cancel);
            Workspace = new WorkspaceViewModel(
                Session,
                Vault.Root,
                () => [],
                _ => { },
                startInteractionBackgroundWork: false,
                announceRendered: line => GraphLines.Add(line.Text));
        }

        public GraphInspectorViewModel Inspector => Workspace.Inspector;

        public GraphPreferencesViewModel Preferences => Workspace.GraphPreferences;

        public GraphViewState State => Workspace.GraphViewStateForTests;

        public GraphDocumentViewModel Open()
        {
            Workspace.OpenGraph();
            GraphDocumentViewModel document = Workspace.GraphDocument!;
            Settle();
            return document;
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
            Vault.Dispose();
        }
    }

    private sealed class HostedWindow(Window window) : IDisposable
    {
        internal Window Window => window;

        public void Dispose() => window.Close();
    }

    private static HostedWindow HostInWindow(UIElement content)
    {
        var window = new Window
        {
            Content = content,
            Width = 420,
            Height = 900,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            ShowActivated = true,
        };
        window.Show();
        window.UpdateLayout();
        return new HostedWindow(window);
    }

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
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void Toggle(UIElement element)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(element);
        ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)!).Toggle();
    }

    private static void Invoke(UIElement element)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(element);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
        PumpedDispatcher.Drain();
    }

    private static string Id(DependencyObject element) => AutomationProperties.GetAutomationId(element);

    private static string Name(DependencyObject element) => AutomationProperties.GetName(element);

    private static string Help(DependencyObject element) => AutomationProperties.GetHelpText(element);

    private static (GraphInspectorView View, HostedWindow Window) Shown(Host host)
    {
        var view = new GraphInspectorView { Model = host.Inspector };
        HostedWindow window = HostInWindow(view);
        PumpedDispatcher.Drain();
        return (view, window);
    }

    // --- E-11, E-15: the names and the ids ---------------------------------------------------

    [Fact]
    public void TheViewCarriesTheInventorysNamesTheIdsAndTheSlidersRange()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-names");
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                Assert.Equal("GraphInspector", Id(view.RootForTests));
                Assert.Equal(GraphPhrase.InspectorName, Name(view.RootForTests));
                Assert.Equal(
                    ["GraphInspectorFilters", "GraphInspectorGroups", "GraphInspectorDisplay", "GraphInspectorForces"],
                    view.SectionsForTests.Select(Id));
                Assert.Equal(
                    [GraphPhrase.InspectorFiltersSection, GraphPhrase.InspectorGroupsSection, GraphPhrase.InspectorDisplaySection, GraphPhrase.InspectorForcesSection],
                    view.SectionsForTests.Select(Name));
                // The notices first, in order; then the four sections.
                var root = (Panel)view.RootForTests;
                Assert.Equal("GraphInspectorInactive", Id(root.Children[0]));
                Assert.Equal("GraphInspectorReadOnly", Id(root.Children[1]));
                Assert.Equal(GraphPhrase.InspectorInactiveText, view.InactiveNoticeForTests.Text);
                Assert.Equal("GraphInspectorFilters", Id(root.Children[2]));
                // The name field: T39's label, the ONE AX name of C-5.
                Assert.Equal("GraphInspectorNameQuery", Id(view.NameQueryForTests));
                Assert.Equal(GraphPhrase.FilterFieldName, Name(view.NameQueryForTests));
                Assert.Equal(GraphPhrase.InspectorNameFieldLabel, ((TextBlock)AutomationProperties.GetLabeledBy(view.NameQueryForTests)!).Text);
                // The flags (T40–T42) and the display's toggle (T51).
                Assert.Equal(["GraphInspectorAttachments", "GraphInspectorGhosts", "GraphInspectorOrphans"], view.FlagsForTests.Select(Id));
                Assert.Equal([GraphPhrase.InspectorAttachmentsLabel, GraphPhrase.InspectorUnresolvedLabel, GraphPhrase.InspectorOrphansLabel], view.FlagsForTests.Select(f => (string)f.Content));
                Assert.Equal([GraphPhrase.InspectorAttachmentsHint, GraphPhrase.InspectorUnresolvedHint, GraphPhrase.InspectorOrphansHint], view.FlagsForTests.Select(Help));
                Assert.Equal("GraphInspectorArrows", Id(view.ArrowsForTests));
                Assert.Equal(GraphPhrase.InspectorArrowsLabel, (string)view.ArrowsForTests.Content);
                Assert.Equal(GraphPhrase.InspectorArrowsHint, Help(view.ArrowsForTests));
                // The add button (T45) and the empty text (T44).
                Assert.Equal("GraphInspectorAddGroup", Id(view.AddGroupForTests));
                Assert.Equal(GraphPhrase.InspectorAddGroupLabel, (string)view.AddGroupForTests.Content);
                Assert.Equal(GraphPhrase.InspectorAddGroupHint, Help(view.AddGroupForTests));
                Assert.Equal(GraphPhrase.InspectorNoGroupsText, view.NoGroupsForTests.Text);
                Assert.Equal(Visibility.Visible, view.NoGroupsForTests.Visibility);
                // The seven sliders (T52–T54, T56–T59): the id, the Name, the HelpText, the range, Term K6's keys, the value text (T60).
                (string Id, string Name, string Hint, double Min, double Max)[] expected =
                [
                    ("GraphInspectorTextFade", GraphPhrase.InspectorTextFadeLabel, GraphPhrase.InspectorTextFadeHint, 0.1, 2.0),
                    ("GraphInspectorNodeSize", GraphPhrase.InspectorNodeSizeLabel, GraphPhrase.InspectorNodeSizeHint, 0.5, 2.0),
                    ("GraphInspectorLinkThickness", GraphPhrase.InspectorLinkThicknessLabel, GraphPhrase.InspectorLinkThicknessHint, 0.5, 4.0),
                    ("GraphInspectorCenter", GraphPhrase.InspectorCenterLabel, GraphPhrase.InspectorCenterHint, 0, 1),
                    ("GraphInspectorRepel", GraphPhrase.InspectorRepelLabel, GraphPhrase.InspectorRepelHint, 0, 1),
                    ("GraphInspectorLink", GraphPhrase.InspectorLinkForceLabel, GraphPhrase.InspectorLinkForceHint, 0, 1),
                    ("GraphInspectorLinkDistance", GraphPhrase.InspectorLinkDistanceLabel, GraphPhrase.InspectorLinkDistanceHint, 0, 1),
                ];
                Assert.Equal(expected.Length, view.SlidersForTests.Count);
                for (int i = 0; i < expected.Length; i++)
                {
                    Slider slider = view.SlidersForTests[i];
                    Assert.Equal(expected[i].Id, Id(slider));
                    Assert.Equal(expected[i].Name, Name(slider));
                    Assert.Equal(expected[i].Hint, Help(slider));
                    Assert.Equal(expected[i].Min, slider.Minimum);
                    Assert.Equal(expected[i].Max, slider.Maximum);
                    Assert.Equal(GraphInspectorView.SliderSmallChange, slider.SmallChange);
                    Assert.Equal(GraphInspectorView.SliderLargeChange, slider.LargeChange);
                    Assert.Equal(GraphPhrase.InspectorSliderValue(slider.Value), view.ValueTextOf(slider).Text);
                    // The RangeValue pattern IS the value (ED-10).
                    var peer = UIElementAutomationPeer.CreatePeerForElement(slider);
                    var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue)!;
                    Assert.Equal(slider.Value, range.Value);
                    Assert.Equal(expected[i].Min, range.Minimum);
                    Assert.Equal(expected[i].Max, range.Maximum);
                }
                Assert.Equal(host.Preferences.CurrentConfig.Forces.Repel, view.SlidersForTests[4].Value);
            }
        });
    }

    // --- Term I5: the pane's first stop -----------------------------------------------------------

    /// <summary>W6-2 PR E (Term I5; E-D1): the shell's right-pane boundary
    /// lands on the name field through FocusFirstStop — only while the field
    /// can take the keys (the graph effective); otherwise false, and the
    /// shell falls back to the leaves list.</summary>
    [Fact]
    public void FocusFirstStopLandsOnTheNameFieldOnlyWhileTheGraphIsEffective()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-first-stop");
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                Assert.False(view.FocusFirstStop());
                Assert.NotSame(view.NameQueryForTests, Keyboard.FocusedElement);
                host.Open();
                PumpedDispatcher.Drain();
                Assert.True(view.FocusFirstStop());
                Assert.Same(view.NameQueryForTests, Keyboard.FocusedElement);
            }
        });
    }

    /// <summary>IPI-1-1: the boundary's deferred landing moves nothing once
    /// the inspector is no longer the shown leaf (a hide or another leaf's
    /// reveal interleaved before the Background callback); while shown, the
    /// first stop, or the rail when the field refuses.</summary>
    [Fact]
    public void TheDeferredBoundaryLandsNothingOnceTheInspectorIsNotShown()
    {
        int firstStop = 0;
        int rail = 0;
        GraphInspectorView.LandBoundary(false, () => { firstStop++; return true; }, () => rail++);
        Assert.Equal(0, firstStop);
        Assert.Equal(0, rail);
        GraphInspectorView.LandBoundary(true, () => { firstStop++; return true; }, () => rail++);
        Assert.Equal(1, firstStop);
        Assert.Equal(0, rail);
        GraphInspectorView.LandBoundary(true, () => { firstStop++; return false; }, () => rail++);
        Assert.Equal(2, firstStop);
        Assert.Equal(1, rail);
    }

    // --- Terms I7, Y6: the gate and the notices ----------------------------------------------------

    [Fact]
    public void TheSectionsAreEnabledOnlyWhileTheGraphIsEffectiveUnderTheInactiveNotice()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-gate");
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                Assert.All(view.SectionsForTests, section => Assert.False(section.IsEnabled));
                Assert.Equal(Visibility.Visible, view.InactiveNoticeForTests.Visibility);
                Assert.Equal(Visibility.Collapsed, view.ReadOnlyNoticeForTests.Visibility);
                host.Open();
                PumpedDispatcher.Drain();
                Assert.All(view.SectionsForTests, section => Assert.True(section.IsEnabled));
                Assert.Equal(Visibility.Collapsed, view.InactiveNoticeForTests.Visibility);
                host.Workspace.CloseActiveTabCommand.Execute(null);
                host.Settle();
                Assert.All(view.SectionsForTests, section => Assert.False(section.IsEnabled));
                Assert.Equal(Visibility.Visible, view.InactiveNoticeForTests.Visibility);
            }
        });
    }

    [Fact]
    public void TheReadOnlyNoticeShowsTheReasonSecondAndDisablesNothing()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-read-only", root =>
            {
                string path = Path.Combine(root, ".slate", GraphConfigStore.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, GraphConfigs.InvalidUtf8(withBom: false));
            });
            Assert.False(host.Preferences.IsWritable);
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                // Both notices: the inactive one first (IGX-3).
                Assert.Equal(Visibility.Visible, view.InactiveNoticeForTests.Visibility);
                Assert.Equal(Visibility.Visible, view.ReadOnlyNoticeForTests.Visibility);
                Assert.Equal(GraphPhrase.InspectorReadOnlyPrefix + host.Preferences.LoadFailure, view.ReadOnlyNoticeForTests.Text);
                var root = (Panel)view.RootForTests;
                Assert.True(root.Children.IndexOf(view.InactiveNoticeForTests) < root.Children.IndexOf(view.ReadOnlyNoticeForTests));
                // Effective: the read-only state alone disables nothing; its notice stays.
                host.Open();
                PumpedDispatcher.Drain();
                Assert.All(view.SectionsForTests, section => Assert.True(section.IsEnabled));
                Assert.Equal(Visibility.Collapsed, view.InactiveNoticeForTests.Visibility);
                Assert.Equal(Visibility.Visible, view.ReadOnlyNoticeForTests.Visibility);
                // An edit stays live (Term Y6).
                Invoke(view.AddGroupForTests);
                Assert.Single(host.State.Groups);
            }
        });
    }

    // --- The routes: every control one call into the view model ---------------------------------------

    [Fact]
    public void TheFlagsTheNeedleTheDisplayAndTheForcesRouteThroughTheViewModelAndFollowOutsideWrites()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-routes");
            host.Open();
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                // A flag: the view state's filter through the document's route (Term X1).
                bool attachments = host.State.Filter.IncludeAttachments;
                Toggle(view.FlagsForTests[0]);
                host.Settle();
                Assert.Equal(!attachments, host.State.Filter.IncludeAttachments);
                Assert.Equal(!attachments, view.FlagsForTests[0].IsChecked);
                Assert.Equal(!attachments, host.Preferences.CurrentConfig.Filters.IncludeAttachments);
                // The needle: the navigator's writer (Term X3); an outside write re-renders the field.
                view.NameQueryForTests.Text = "hub";
                Assert.Equal("hub", host.State.NameQuery);
                host.Workspace.GraphNavigator.SetNameQuery("alpha");
                Assert.Equal("alpha", view.NameQueryForTests.Text);
                // The display: the toggle and a slider (Term Z1), the value text following (Term Z3).
                bool arrows = host.Preferences.CurrentConfig.Display.Arrows;
                Toggle(view.ArrowsForTests);
                Assert.Equal(!arrows, host.Preferences.CurrentConfig.Display.Arrows);
                Slider nodeSize = view.SlidersForTests[1];
                nodeSize.Value = 1.5;
                Assert.Equal(1.5, host.Preferences.CurrentConfig.Display.NodeSizeMultiplier);
                Assert.Equal("1.50", view.ValueTextOf(nodeSize).Text);
                // A force: one small step is one SetForces (Terms K1, K6); the value text follows.
                Slider repel = view.SlidersForTests[4];
                double before = host.Preferences.CurrentConfig.Forces.Repel;
                repel.Value = before + GraphInspectorView.SliderSmallChange;
                Assert.Equal(before + GraphInspectorView.SliderSmallChange, host.Preferences.CurrentConfig.Forces.Repel, 9);
                Assert.Equal(GraphPhrase.InspectorSliderValue(before + GraphInspectorView.SliderSmallChange), view.ValueTextOf(repel).Text);
                // An outside write of the forces: the slider follows without a second write.
                ulong? generation = host.Preferences.PendingGenerationForTests;
                host.Preferences.SetForces(host.Preferences.CurrentConfig.Forces with { Center = 0.25 });
                Assert.Equal(0.25, view.SlidersForTests[3].Value);
                Assert.Equal("0.25", view.ValueTextOf(view.SlidersForTests[3]).Text);
                Assert.Equal(0.25, host.Preferences.CurrentConfig.Forces.Center);
                Assert.NotEqual(generation, host.Preferences.PendingGenerationForTests);
                generation = host.Preferences.PendingGenerationForTests;
                PumpedDispatcher.Drain();
                Assert.Equal(generation, host.Preferences.PendingGenerationForTests);
            }
        });
    }

    // --- Rule Y: the rows, the pickers, the keys -----------------------------------------------------------

    /// <summary>W7-7 PR 3 (#1246, R-4; the spec review, round 23): a group
    /// row's pickers, as the inspector builds them, given two entries core
    /// titled alike — each reads apart by its place, the rest bare.</summary>
    [Fact]
    public void AGroupRowsPickersTellEntriesTitledAlikeApart()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-namesakes");
            host.Open();
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                Invoke(view.AddGroupForTests);
                Panel row = Assert.IsAssignableFrom<Panel>(Assert.Single(view.RowsForTests.Children));
                ComboBox[] combos = [.. row.Children.OfType<ComboBox>()];
                ComboBox ring = combos.Single(c => Id(c) == "GraphInspectorGroupRing:1");
                ComboBox colour = combos.Single(c => Id(c) == "GraphInspectorGroupColour:1");
                // As core gives them, each title reads bare.
                Assert.Equal(
                    host.Inspector.RingStyles.Select(spec => spec.Title),
                    ItemContainerNameBindingTests.ItemNames(ring));
                ring.IsDropDownOpen = false;
                GraphRingStyleSpec[] styles = [.. host.Inspector.RingStyles];
                ring.ItemsSource = new[]
                {
                    styles[0] with { Title = "Shared" },
                    styles[1] with { Title = "Shared" },
                    styles[^1] with { Title = "Own" },
                };
                Assert.Equal(["Shared, style 1", "Shared, style 2", "Own"], ItemContainerNameBindingTests.ItemNames(ring));
                ring.IsDropDownOpen = false;
                GraphColorTokenSpec[] tokens = [.. host.Inspector.ColorTokens];
                colour.ItemsSource = new[]
                {
                    tokens[0] with { Title = "Shared" },
                    tokens[1] with { Title = "Own" },
                    tokens[2] with { Title = "Shared" },
                };
                Assert.Equal(["Shared, colour 1", "Own", "Shared, colour 3"], ItemContainerNameBindingTests.ItemNames(colour));
                colour.IsDropDownOpen = false;
            }
        });
    }

    [Fact]
    public void TheGroupRowsComposeTheirNamesListCoresVectorsAndMoveTheKeys()
    {
        RunSta(() =>
        {
            using var host = new Host(2, "inspector-view-groups");
            host.Open();
            (GraphInspectorView view, HostedWindow window) = Shown(host);
            using (window)
            {
                Assert.Equal(Visibility.Visible, view.NoGroupsForTests.Visibility);
                // Add: one row in core's first style, the keys in its query field (Term Y3).
                Invoke(view.AddGroupForTests);
                Assert.Equal(Visibility.Collapsed, view.NoGroupsForTests.Visibility);
                Panel first = Assert.IsAssignableFrom<Panel>(Assert.Single(view.RowsForTests.Children));
                TextBox query = first.Children.OfType<TextBox>().Single();
                ComboBox[] combos = [.. first.Children.OfType<ComboBox>()];
                Button remove = first.Children.OfType<Button>().Single();
                Assert.Equal("GraphInspectorGroupQuery:1", Id(query));
                Assert.Equal(GraphPhrase.InspectorGroupQueryName(1), Name(query));
                ComboBox colour = combos.Single(c => Id(c) == "GraphInspectorGroupColour:1");
                ComboBox ring = combos.Single(c => Id(c) == "GraphInspectorGroupRing:1");
                Assert.Equal(GraphPhrase.InspectorGroupColourName(1), Name(colour));
                Assert.Equal(GraphPhrase.InspectorGroupRingName(1), Name(ring));
                Assert.Equal("GraphInspectorRemoveGroup:1", Id(remove));
                Assert.Equal(GraphPhrase.InspectorRemoveGroupName(1), Name(remove));
                // The pickers list core's vectors, the titles core's (Term Y4).
                Assert.Same(host.Inspector.ColorTokens, colour.ItemsSource);
                Assert.Same(host.Inspector.RingStyles, ring.ItemsSource);
                // A picker item's accessible name is core's Title, not the
                // record's ToString — read under the sibling rule the picker
                // declares (W7-7 PR 3, R-4): every title as core gives it.
                foreach (ComboBox picker in new[] { colour, ring })
                {
                    Setter name = picker.ItemContainerStyle.Setters.OfType<Setter>().Single(setter => setter.Property == AutomationProperties.NameProperty);
                    Assert.Same(SiblingNames.Converter, ((System.Windows.Data.Binding)name.Value).Converter);
                    Assert.Equal("Title", SiblingNames.GetNamePath(picker));
                }
                GraphGroupStyle style = SlateUniffiMethods.GraphConfigNextGroupStyle(0);
                Assert.Equal(style.ColorToken, ((GraphColorTokenSpec)colour.SelectedItem!).Token);
                Assert.Equal(style.RingStyle, ((GraphRingStyleSpec)ring.SelectedItem!).Style);
                Assert.Same(query, Keyboard.FocusedElement);
                // The query typed: the row's edit in place, the field keeping the keys.
                query.Text = "note";
                Assert.Equal("note", host.State.Groups[0].Query);
                Assert.Same(query, view.RowsForTests.Children.OfType<Panel>().Single().Children.OfType<TextBox>().Single());
                Assert.Same(query, Keyboard.FocusedElement);
                // The colour picked.
                colour.SelectedItem = host.Inspector.ColorTokens.First(spec => spec.Token == GraphColorToken.Pink);
                Assert.Equal(GraphColorToken.Pink, host.State.Groups[0].ColorToken);
                // A second row, then the first removed: the survivor is row 1 again, the keys on it (Term Y5).
                Invoke(view.AddGroupForTests);
                Assert.Equal(2, view.RowsForTests.Children.Count);
                Invoke(first.Children.OfType<Button>().Single());
                Panel survivor = Assert.IsAssignableFrom<Panel>(Assert.Single(view.RowsForTests.Children));
                TextBox survivorQuery = survivor.Children.OfType<TextBox>().Single();
                Assert.Equal("GraphInspectorGroupQuery:1", Id(survivorQuery));
                Assert.Equal(string.Empty, survivorQuery.Text);
                Assert.Same(survivorQuery, Keyboard.FocusedElement);
                // The last removed: the empty text back, the keys on Add Group.
                Invoke(survivor.Children.OfType<Button>().Single());
                Assert.Empty(view.RowsForTests.Children);
                Assert.Equal(Visibility.Visible, view.NoGroupsForTests.Visibility);
                Assert.Same(view.AddGroupForTests, Keyboard.FocusedElement);
                Assert.Empty(host.State.Groups);
            }
        });
    }
}
