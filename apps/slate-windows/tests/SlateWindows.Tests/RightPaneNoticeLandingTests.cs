// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Canvas;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4 (#1247, contract R-5, spec §5.2.2; codex round 4): an EMPTY
/// right-pane leaf's stop is its notice. In the no-note, empty and failure
/// states the landing into a leaf — the reveal's, which is the ring's
/// right-pane content landing — puts the keys on the notice, the notice's
/// name is its sentence, and every arrow from it stays in the leaf.
/// </summary>
/// <remarks>
/// The leaves are the shipped window's, built by its own XAML, lifted with
/// the rail into a window of their own between buttons on every side and
/// bound to a real workspace over a real vault. The failure states are
/// injected into the leaf models' own error properties — a core read
/// failure is not reachable from a healthy vault. Keys are real presses
/// through the input manager, so WPF's directional navigation answers them.
/// </remarks>
public sealed class RightPaneNoticeLandingTests
{
    public static TheoryData<string, string> Cases()
    {
        var cases = new TheoryData<string, string>();
        foreach (string leaf in new[] { "backlinks", "outgoingLinks", "outline", "embeds", "tasks", "citations" })
        {
            foreach (string state in new[] { "no note", "empty", "failed" })
            {
                cases.Add(leaf, state);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnEmptyLeafLandsOnItsNoticeAndKeepsEveryArrow(string leaf, string state) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, state);
        (TextBlock notice, string expected) = host.ExpectedNotice(leaf, state);

        foreach (Key key in new[] { Key.Left, Key.Right, Key.Up, Key.Down })
        {
            Assert.True(host.Beside.Focus());
            host.Shell.LandInRightPane();

            Assert.Same(notice, Keyboard.FocusedElement);
            Assert.Equal(expected, notice.Text);
            Assert.Equal(expected, UIElementAutomationPeer.CreatePeerForElement(notice).GetName());

            host.Press(key);

            FrameworkElement body = host.VisibleLeafBody();
            Assert.True(
                body.IsKeyboardFocusWithin,
                $"{leaf} ({state}): {key} took the keys out of the leaf, to {Keyboard.FocusedElement}");
        }
    });

    /// <summary>A LOADING sentence is not a stop — it gives way to the rows
    /// within moments — while the final sentences are. The leaf models'
    /// own loading flags, set the way their loaders set them.</summary>
    [Theory]
    [InlineData("backlinks")]
    [InlineData("outgoingLinks")]
    [InlineData("outline")]
    [InlineData("embeds")]
    [InlineData("tasks")]
    public void ALoadingNoticeIsNotAStop(string leaf) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(leaf, "empty");
        RightPanePanelsViewModel panels = host.Panels;
        Assert.True(IsAStop(panels, leaf), $"premise: {leaf}'s empty sentence is a stop.");

        Loading(panels, leaf);

        Assert.StartsWith(leaf == "embeds" ? "Resolving" : "Loading", Message(panels, leaf), StringComparison.Ordinal);
        Assert.False(IsAStop(panels, leaf));
    });

    private static bool IsAStop(RightPanePanelsViewModel panels, string leaf) => leaf switch
    {
        "backlinks" => panels.BacklinksNoticeIsAStop,
        "outgoingLinks" => panels.OutgoingLinksNoticeIsAStop,
        "outline" => panels.OutlineNoticeIsAStop,
        "embeds" => panels.EmbedsNoticeIsAStop,
        _ => panels.TasksNoticeIsAStop,
    };

    private static string? Message(RightPanePanelsViewModel panels, string leaf) => leaf switch
    {
        "backlinks" => panels.BacklinksEmptyMessage,
        "outgoingLinks" => panels.OutgoingLinksEmptyMessage,
        "outline" => panels.OutlineEmptyMessage,
        "embeds" => panels.EmbedsEmptyMessage,
        _ => panels.TasksEmptyMessage,
    };

    private static void Loading(RightPanePanelsViewModel panels, string leaf)
    {
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        if (leaf == "tasks")
        {
            typeof(RightPanePanelsViewModel).GetField("_isLoadingTasks", any)!.SetValue(panels, true);
            return;
        }

        string property = leaf switch
        {
            "outline" => nameof(RightPanePanelsViewModel.IsLoadingOutline),
            "embeds" => nameof(RightPanePanelsViewModel.IsResolvingEmbeds),
            _ => nameof(RightPanePanelsViewModel.IsLoadingLinks),
        };
        typeof(RightPanePanelsViewModel).GetProperty(property, any)!.GetSetMethod(nonPublic: true)!.Invoke(panels, [true]);
    }

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(0, "leaf-notices");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private VaultSession? _session;
        private WorkspaceViewModel? _workspace;
        private Window? _window;
        private Grid _leafHost = null!;

        public MainWindow Shell { get; private set; } = null!;

        public Button Beside { get; private set; } = null!;

        public RightPanePanelsViewModel Panels => _workspace!.Panels;

        public void Initialize(string leaf, string state)
        {
            Assert.Null(Application.Current);
            File.WriteAllText(Path.Combine(_fixture.Root, "plain.md"), "Just a line of text.\n");
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _workspace = new WorkspaceViewModel(
                _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
            if (state != "no note")
            {
                _workspace.OpenPath("plain.md");
            }

            if (state == "failed")
            {
                Fail(_workspace, leaf);
            }

            _workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(option => option.Id == leaf);

            Shell = new MainWindow();
            _leafHost = Assert.IsType<Grid>(Shell.FindName("RightPaneLeafHost"));
            var border = Assert.IsAssignableFrom<Decorator>(_leafHost.Parent);
            border.Child = null;
            _leafHost.DataContext = _workspace;

            var grid = new Grid();
            for (int index = 0; index < 3; index++)
            {
                grid.RowDefinitions.Add(new RowDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            Button Place(string text, int row, int column)
            {
                var button = new Button { Content = text };
                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                grid.Children.Add(button);
                return button;
            }

            Beside = Place("Above", 0, 1);
            _ = Place("Below", 2, 1);
            _ = Place("Left", 1, 0);
            _ = Place("Right", 1, 2);
            Grid.SetRow(_leafHost, 1);
            Grid.SetColumn(_leafHost, 1);
            grid.Children.Add(_leafHost);
            _window = new Window
            {
                Content = grid,
                Width = 1100,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>The leaf's notice for this state, and the sentence its
        /// model says it shows.</summary>
        public (TextBlock Notice, string Text) ExpectedNotice(string leaf, string state)
        {
            WorkspaceViewModel workspace = _workspace!;
            (string id, string? text) = leaf switch
            {
                "backlinks" => ("PanelBacklinksNotice", workspace.Panels.BacklinksEmptyMessage),
                "outgoingLinks" => ("PanelOutgoingLinksNotice", workspace.Panels.OutgoingLinksEmptyMessage),
                "outline" => ("PanelOutlineNotice", workspace.Panels.OutlineEmptyMessage),
                "embeds" => ("PanelEmbedsNotice", workspace.Panels.EmbedsEmptyMessage),
                "tasks" => ("PanelTasksNotice", workspace.Panels.TasksEmptyMessage),
                _ => state switch
                {
                    "no note" => ("PanelCitationsNoFile", workspace.Citations.NoFileText),
                    "empty" => ("PanelCitationsEmpty", workspace.Citations.EmptyText),
                    _ => ("PanelCitationsError", workspace.Citations.ErrorSpoken),
                },
            };
            Assert.False(string.IsNullOrEmpty(text), $"premise: {leaf} ({state}) shows no notice.");
            TextBlock notice = Descendants(_leafHost).OfType<TextBlock>()
                .Single(block => AutomationProperties.GetAutomationId(block) == id);
            Assert.True(notice.IsVisible, $"premise: {id} is not shown.");
            return (notice, text!);
        }

        public FrameworkElement VisibleLeafBody() =>
            _leafHost.Children.OfType<DockPanel>().Single(body => Grid.GetColumn(body) == 0 && body.IsVisible);

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(_window!)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            try
            {
                _window?.Close();
                Shell?.Close();
                _workspace?.Dispose();
                _session?.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }

        /// <summary>A core read failure, set on the leaf model's own error
        /// state the way its loader sets it.</summary>
        private static void Fail(WorkspaceViewModel workspace, string leaf)
        {
            const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            void Set(object model, string property) =>
                (model.GetType().GetProperty(property, any)?.GetSetMethod(nonPublic: true)
                    ?? throw new InvalidOperationException($"{property} has no setter"))
                .Invoke(model, ["the index could not be read"]);

            switch (leaf)
            {
                case "backlinks" or "outgoingLinks":
                    Set(workspace.Panels, nameof(RightPanePanelsViewModel.LinksLoadError));
                    break;
                case "outline":
                    Set(workspace.Panels, nameof(RightPanePanelsViewModel.OutlineLoadError));
                    break;
                case "embeds":
                    Set(workspace.Panels, nameof(RightPanePanelsViewModel.EmbedsLoadError));
                    break;
                case "tasks":
                    (typeof(RightPanePanelsViewModel).GetField("_tasksLoadError", any)
                        ?? throw new InvalidOperationException("_tasksLoadError is gone"))
                        .SetValue(workspace.Panels, "the index could not be read");
                    (typeof(RightPanePanelsViewModel).GetMethod("RaiseHeaderChanges", any)
                        ?? throw new InvalidOperationException("RaiseHeaderChanges is gone"))
                        .Invoke(workspace.Panels, null);
                    break;
                default:
                    Set(workspace.Citations, nameof(CitationsPanelViewModel.LoadError));
                    break;
            }
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (DependencyObject nested in Descendants(child))
                {
                    yield return nested;
                }
            }
        }
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
}
