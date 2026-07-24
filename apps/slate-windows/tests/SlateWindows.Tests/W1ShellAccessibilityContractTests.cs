// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class W1ShellAccessibilityContractTests
{
    [Fact]
    public void PaneNavigationGesture_RequiresExactModifiersAndAnArrowKey()
    {
        ModifierKeys paneModifiers = ModifierKeys.Control | ModifierKeys.Alt;

        Assert.True(MainWindow.IsPaneNavigationGesture(Key.Left, paneModifiers));
        Assert.True(MainWindow.IsPaneNavigationGesture(Key.Down, paneModifiers));
        Assert.False(MainWindow.IsPaneNavigationGesture(Key.Tab, paneModifiers));
        Assert.False(MainWindow.IsPaneNavigationGesture(Key.Escape, paneModifiers));
        Assert.False(MainWindow.IsPaneNavigationGesture(Key.Left, ModifierKeys.Control));
        Assert.False(MainWindow.IsPaneNavigationGesture(
            Key.Left,
            paneModifiers | ModifierKeys.Shift));
    }

    [Fact]
    public void WorkspaceLandmarks_CreateNamedPanePeersInTheControlTree() =>
        RunOnStaThread(
            () =>
            {
                (FrameworkElement Element, string Id, string Name)[] landmarks =
                [
                    (new AutomationLandmarkGrid(), "WelcomeView", "Welcome"),
                    (new AutomationLandmarkGrid(), "WorkspaceView", "Workspace"),
                    (new AutomationLandmarkBorder(), "FilesPane", "Files"),
                    (new AutomationLandmarkBorder(), "ContentPane", "Editor workspace"),
                    (new AutomationLandmarkBorder(), "InspectorPane", "Inspector"),
                    (new AutomationLandmarkBorder(), "QuickSwitcher", "Quick Open"),
                ];

                foreach ((FrameworkElement element, string id, string name) in landmarks)
                {
                    AutomationProperties.SetAutomationId(element, id);
                    AutomationProperties.SetName(element, name);

                    AutomationPeer peer = Assert.IsType<AutomationLandmarkPeer>(
                        UIElementAutomationPeer.CreatePeerForElement(element));
                    Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
                    Assert.Equal(id, peer.GetAutomationId());
                    Assert.Equal(name, peer.GetName());
                    Assert.True(peer.IsControlElement());
                    Assert.False(peer.IsContentElement());

                    element.Visibility = Visibility.Collapsed;
                    Assert.False(peer.IsControlElement());
                }

                var results = new AutomationVisibilityListBox();
                AutomationPeer resultsPeer = Assert.IsType<AutomationVisibilityListBoxPeer>(
                    UIElementAutomationPeer.CreatePeerForElement(results));
                Assert.Equal(AutomationControlType.List, resultsPeer.GetAutomationControlType());
                results.Visibility = Visibility.Collapsed;
                Assert.False(resultsPeer.IsControlElement());
                Assert.False(resultsPeer.IsContentElement());

                var presentationText = new AutomationPresentationTextBlock
                {
                    Text = "Duplicated by its parent",
                };
                AutomationPeer presentationTextPeer =
                    Assert.IsType<AutomationPresentationTextBlockPeer>(
                        UIElementAutomationPeer.CreatePeerForElement(presentationText));
                Assert.False(presentationTextPeer.IsControlElement());
                Assert.False(presentationTextPeer.IsContentElement());
            },
            "Landmark peer test timed out.");

    [Fact]
    public void WorkspaceSplitHandles_KeepLegacyHorizontalIdAndExposeDistinctVerticalId() =>
        RunOnStaThread(
            () =>
            {
                var resourceUri = new Uri(
                    "/SlateWindows;component/WorkspaceTemplates.xaml",
                    UriKind.Relative);
                ResourceDictionary resources = Assert.IsType<ResourceDictionary>(
                    Application.LoadComponent(resourceUri));
                DataTemplate template = Assert.IsType<DataTemplate>(
                    resources["WorkspaceNodeChildTemplate"]);
                Grid root = Assert.IsType<Grid>(template.LoadContent());
                Thumb[] handles = root.Children.OfType<Thumb>().ToArray();

                Assert.Collection(
                    handles,
                    horizontal =>
                    {
                        Assert.Equal(
                            "WorkspaceSplitHandle",
                            AutomationProperties.GetAutomationId(horizontal));
                        Assert.Equal(
                            "Resize editor panes horizontally",
                            AutomationProperties.GetName(horizontal));
                    },
                    vertical =>
                    {
                        Assert.Equal(
                            "WorkspaceSplitHandleVertical",
                            AutomationProperties.GetAutomationId(vertical));
                        Assert.Equal(
                            "Resize editor panes vertically",
                            AutomationProperties.GetName(vertical));
                    });
                Assert.Equal(
                    handles.Length,
                    handles.Select(AutomationProperties.GetAutomationId)
                        .Distinct(StringComparer.Ordinal)
                        .Count());
            },
            "Split handle automation contract test timed out.");

    [Fact]
    public void WorkspaceTabContentTemplate_LoadsMarkdownEditorWithBoundAutomationIdentity() =>
        RunOnStaThread(
            () =>
            {
                using FixtureVault fixture = FixtureVault.Create(2, "workspace-editor-template");
                using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);
                using var firstTab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
                    startInteractionBackgroundWork: false);
                using var secondTab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "note1.md")),
                    startInteractionBackgroundWork: false);
                using var placeholderTab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Canvas, "canvas:test")),
                    startInteractionBackgroundWork: false);
                var resourceUri = new Uri(
                    "/SlateWindows;component/WorkspaceTemplates.xaml",
                    UriKind.Relative);
                ResourceDictionary resources = Assert.IsType<ResourceDictionary>(
                    Application.LoadComponent(resourceUri));
                DataTemplate template = Assert.IsType<DataTemplate>(
                    resources["WorkspaceTabContentTemplate"]);
                Grid root = Assert.IsType<Grid>(template.LoadContent());
                root.DataContext = firstTab;
                var window = new Window { Content = root };
                try
                {
                    window.Show();
                    root.UpdateLayout();
                    root.Dispatcher.Invoke(
                        DispatcherPriority.DataBind,
                        new Action(() => { }));
                    root.UpdateLayout();
                    SlateTextEditor editor = Assert.Single(
                        root.Children.OfType<SlateTextEditor>());

                    Assert.True(editor.IsVisible);
                    Assert.Equal(
                        "MarkdownEditor",
                        AutomationProperties.GetAutomationId(editor));
                    Assert.Equal(
                        "note0.md editor",
                        AutomationProperties.GetName(editor));
                    Assert.Equal(firstTab.EditorDocument, editor.Document);

                    firstTab.EditorCaretOffset = 4;
                    Assert.Equal(4, editor.CaretOffset);
                    editor.CaretOffset = 7;
                    Assert.Equal(7, firstTab.EditorCaretOffset);

                    firstTab.EditorCaretOffset = 5;
                    secondTab.EditorCaretOffset = 9;
                    root.DataContext = secondTab;
                    root.Dispatcher.Invoke(
                        DispatcherPriority.DataBind,
                        new Action(() => { }));
                    root.UpdateLayout();

                    Assert.Equal(5, firstTab.EditorCaretOffset);
                    Assert.Equal(secondTab.EditorDocument, editor.Document);
                    Assert.Equal(9, editor.CaretOffset);
                    Assert.Equal(
                        "note1.md editor",
                        AutomationProperties.GetName(editor));
                    editor.CaretOffset = 6;
                    Assert.Equal(6, secondTab.EditorCaretOffset);

                    firstTab.EditorCaretOffset = 5;
                    root.DataContext = firstTab;
                    root.DataContext = secondTab;
                    root.DataContext = firstTab;
                    root.DataContext = placeholderTab;
                    window.Content = null;
                    root.Dispatcher.Invoke(
                        DispatcherPriority.DataBind,
                        new Action(() => { }));

                    Assert.Equal(5, firstTab.EditorCaretOffset);
                    Assert.Equal(6, secondTab.EditorCaretOffset);
                    Assert.Null(editor.Document);
                }
                finally
                {
                    window.Close();
                }
            },
            "Markdown editor template test timed out.");

    [Fact]
    public void WeightedSplitPanel_RearrangesWhenAChildWeightChanges() =>
        RunOnStaThread(
            () =>
            {
                var firstNode = new WorkspacePaneNodeViewModel("horizontal");
                var secondNode = new WorkspacePaneNodeViewModel("horizontal");
                var first = new Border { DataContext = firstNode };
                var second = new Border { DataContext = secondNode };
                var panel = new WeightedSplitPanel
                {
                    Orientation = Orientation.Horizontal,
                };
                panel.Children.Add(first);
                panel.Children.Add(second);

                var size = new Size(300, 100);
                panel.Measure(size);
                panel.Arrange(new Rect(size));
                Assert.Equal(150, first.RenderSize.Width, precision: 5);
                Assert.Equal(150, second.RenderSize.Width, precision: 5);

                firstNode.Weight = 0.25;
                Assert.False(panel.IsArrangeValid);
                panel.Arrange(new Rect(size));
                Assert.Equal(60, first.RenderSize.Width, precision: 5);
                Assert.Equal(240, second.RenderSize.Width, precision: 5);
            },
            "Split layout test timed out.");

    [Fact]
    public void WeightedSplitPanel_DragHandleResizesAdjacentPanes() =>
        RunOnStaThread(
            () =>
            {
                var firstNode = new WorkspacePaneNodeViewModel("horizontal");
                var secondNode = new WorkspacePaneNodeViewModel("horizontal");
                firstNode.Weight = 0.5;
                secondNode.Weight = 0.5;
                var first = new ContentControl { DataContext = firstNode };
                var handle = new Thumb { DataContext = secondNode };
                var second = new ContentControl
                {
                    DataContext = secondNode,
                    Content = handle,
                };
                var panel = new WeightedSplitPanel
                {
                    Orientation = Orientation.Horizontal,
                };
                panel.Children.Add(first);
                panel.Children.Add(second);

                var size = new Size(300, 100);
                panel.Measure(size);
                panel.Arrange(new Rect(size));
                handle.RaiseEvent(new DragDeltaEventArgs(30, 0)
                {
                    RoutedEvent = Thumb.DragDeltaEvent,
                });
                panel.Arrange(new Rect(size));

                Assert.Equal(180, first.RenderSize.Width, precision: 5);
                Assert.Equal(120, second.RenderSize.Width, precision: 5);
            },
            "Split handle test timed out.");

    private static void RunOnStaThread(Action action, string timeoutMessage)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "w1-shell-sta-test",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), timeoutMessage);
        failure?.Throw();
    }
}
