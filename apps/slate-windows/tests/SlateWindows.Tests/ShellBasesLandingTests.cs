// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Bases;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b (#1247, R-5; the completeness sweep's G20): the editor
/// region's landing on a Base tab — F6's, a restore's last resort — lands
/// INSIDE the surface: its current or first cell, silently. It landed on the
/// tab's header, which F6 then read as a refusal of the editor region and
/// moved on. The shipped window, shown, over a real workspace with a Base
/// open.
/// </summary>
public sealed class ShellBasesLandingTests : IDisposable
{
    private readonly FixtureVault _fixture = FixtureVault.Create(3, "shell-bases-landing");
    private readonly VaultSession _session;

    public ShellBasesLandingTests()
    {
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void TheEditorRegionLandsInsideABaseTab() => RunSta(() =>
    {
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            _session, _fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        var shell = new MainWindow
        {
            ShowActivated = false,
            ShowInTaskbar = false,
            Width = 1200,
            Height = 800,
        };
        var lifecycle = Assert.IsType<VaultLifecycleViewModel>(shell.DataContext);
        System.Reflection.PropertyInfo slot = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.Workspace))
            ?? throw new InvalidOperationException("Workspace is gone");
        System.Reflection.PropertyInfo open = typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen))
            ?? throw new InvalidOperationException("IsVaultOpen is gone");
        try
        {
            slot.SetValue(lifecycle, workspace);
            open.SetValue(lifecycle, true);
            workspace.OpenPath("Notes.base");
            shell.Show();
            shell.UpdateLayout();
            PumpedDispatcher.Drain();
            BaseSurfaceView surface = Descendants(shell).OfType<BaseSurfaceView>().Single(view => view.IsVisible);
            Assert.True(surface.GridForTests.IsVisible, "premise: the Base shows its table");

            Assert.True(((IShellRegionHost)shell).TryLand(ShellRegionKind.Editor), "the editor region refused a Base tab");

            Assert.IsType<DataGridCell>(Keyboard.FocusedElement);
            Assert.True(surface.IsKeyboardFocusWithin, "the keys are not in the Base's surface");
            Assert.Equal(ShellRegionKind.Editor, ((IShellRegionHost)shell).FocusedRegion());
        }
        finally
        {
            open.SetValue(lifecycle, false);
            slot.SetValue(lifecycle, null);
            shell.Close();
        }
    });

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

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
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
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "STA test body timed out.");
        CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
