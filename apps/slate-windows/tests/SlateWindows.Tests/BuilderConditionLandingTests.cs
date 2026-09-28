// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b round 2 (codex r1 F5; R-5 (h), "a removed row's keys come back
/// to the rows they were in"): a query-builder condition removed from its own
/// focused Remove button took the keys with it, and the guard landed them on
/// the sheet's first stop — its footer's first button. The conditions are a
/// scope of their own now: the keys land in a remaining condition's row; the
/// last one's removal collapses the conditions and the sheet takes them. The
/// shipped builder sheet, lifted from the window's own XAML over a real
/// workspace.
/// </summary>
public sealed class BuilderConditionLandingTests
{
    [Fact]
    public void RemovingAConditionFromItsButtonLandsOnARemainingCondition() => RunSta(() =>
    {
        using var host = new Host();
        BaseQueryBuilderViewModel builder = host.Builder;
        builder.AddCondition();
        builder.AddCondition();
        host.Settle();
        Button remove = host.RemoveButton(1);
        Assert.True(remove.Focus(), "premise: the second condition's Remove refused the keys.");

        remove.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        host.Settle();

        Assert.Single(builder.ConditionRows);
        ItemsControl conditions = host.Conditions;
        Assert.True(conditions.IsKeyboardFocusWithin, $"the keys left the conditions, to {Keyboard.FocusedElement}");
        Assert.Same(builder.ConditionRows[0], ((FrameworkElement)Keyboard.FocusedElement).DataContext);
    });

    /// <summary>The last condition's removal collapses the conditions; the
    /// keys go to the sheet's landing — never the window.</summary>
    [Fact]
    public void RemovingTheLastConditionLandsInTheSheet() => RunSta(() =>
    {
        using var host = new Host();
        host.Builder.AddCondition();
        host.Settle();
        Button remove = host.RemoveButton(0);
        Assert.True(remove.Focus(), "premise: the condition's Remove refused the keys.");

        remove.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        host.Settle();

        Assert.Empty(host.Builder.ConditionRows);
        Assert.True(host.Sheet.IsKeyboardFocusWithin, $"the keys left the sheet, to {Keyboard.FocusedElement}");
    });

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(1, "builder-conditions");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly WorkspaceViewModel _workspace;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly MainWindow _shell;
        private readonly Window _window;

        public Host()
        {
            Assert.Null(Application.Current);
            File.WriteAllText(
                Path.Combine(_fixture.Root, "Notes.base"),
                "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            _workspace = new WorkspaceViewModel(
                _session, _fixture.Root, () => [], _ => { }, startInteractionBackgroundWork: false);
            _shell = new MainWindow();
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(_shell.DataContext);
            SetWorkspace(_workspace);
            _workspace.BasesNewQueryCommand.Execute(null);
            Builder = _workspace.BaseQueryBuilderSheet ?? throw new InvalidOperationException("premise: no builder opened");

            Sheet = Assert.IsAssignableFrom<FrameworkElement>(_shell.FindName("BaseQueryBuilderOverlay"));
            Assert.IsAssignableFrom<Panel>(Sheet.Parent).Children.Remove(Sheet);
            _window = new Window
            {
                Content = Sheet,
                DataContext = _lifecycle,
                Width = 1400,
                Height = 900,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Show();
            Settle();
            Assert.True(Sheet.IsVisible, "premise: the lifted builder is not shown.");
        }

        public BaseQueryBuilderViewModel Builder { get; }

        public FrameworkElement Sheet { get; }

        public ItemsControl Conditions =>
            Descendants(Sheet).OfType<ItemsControl>().Single(element => AutomationProperties.GetAutomationId(element) == "BuilderConditions");

        public void Settle()
        {
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>The Remove button of condition <paramref name="index"/>.</summary>
        public Button RemoveButton(int index)
        {
            var row = Assert.IsAssignableFrom<DependencyObject>(Conditions.ItemContainerGenerator.ContainerFromIndex(index));
            return Descendants(row).OfType<Button>().Single(button => (string?)button.Content == "Remove");
        }

        public void Dispose()
        {
            try
            {
                _window.Close();
                SetWorkspace(null);
                _shell.Close();
                _workspace.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
                _session.Dispose();
                _fixture.Dispose();
            }
        }

        private void SetWorkspace(WorkspaceViewModel? workspace) =>
            (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
                ?? throw new InvalidOperationException("Workspace is gone"))
                .SetValue(_lifecycle, workspace);

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
