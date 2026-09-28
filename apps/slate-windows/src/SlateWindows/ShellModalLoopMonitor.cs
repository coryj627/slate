// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SlateWindows;

/// <summary>
/// Tells the shell when a thread-modal loop is running over it — a message
/// box, a common file or folder dialog, a WPF <c>ShowDialog</c> — so the
/// command palette can seal itself for as long as the loop runs (#1275
/// codex round 4, the owner's option (a); contract 28 T13/T14).
/// </summary>
/// <remarks>
/// <para>
/// Two signals, because neither covers every loop on its own (measured by
/// the round-4 probe):
/// </para>
/// <list type="bullet">
/// <item><description><b>The shell window is disabled.</b> Every modal loop
/// owned by the shell disables it — Win32 sends <c>WM_ENABLE</c> before the
/// loop pumps anything and again as it ends — and <c>MessageBox</c> raises
/// nothing else: it never enters WPF's thread-modal state.</description></item>
/// <item><description><b><see cref="ComponentDispatcher.IsThreadModal"/>.</b>
/// WPF dialogs and the common dialogs push it whatever their owner, which
/// covers one whose owner is not the shell.</description></item>
/// </list>
/// <para>
/// The watched window is whichever one hosts the shell's content: the
/// monitor follows that element's presentation source rather than capturing
/// a window, so it attaches when the shell first gets its HWND.
/// </para>
/// </remarks>
internal sealed class ShellModalLoopMonitor : IDisposable
{
    private const int WmEnable = 0x000A;

    private readonly UIElement _shellContent;
    private readonly Action<bool> _changed;
    private HwndSource? _source;
    private bool _modalLoop;
    private bool _disposed;

    /// <param name="shellContent">The element whose window is the shell.</param>
    /// <param name="changed">Called on the owning thread each time a modal
    /// loop begins (<see langword="true"/>) or the last one ends.</param>
    internal ShellModalLoopMonitor(UIElement shellContent, Action<bool> changed)
    {
        ArgumentNullException.ThrowIfNull(shellContent);
        ArgumentNullException.ThrowIfNull(changed);
        _shellContent = shellContent;
        _changed = changed;
        PresentationSource.AddSourceChangedHandler(shellContent, OnSourceChanged);
        ComponentDispatcher.EnterThreadModal += OnThreadModalChanged;
        ComponentDispatcher.LeaveThreadModal += OnThreadModalChanged;
        Attach(PresentationSource.FromVisual(shellContent) as HwndSource);
    }

    /// <summary>Whether a modal loop is running over the shell now.</summary>
    internal bool IsModalLoopActive => _modalLoop;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PresentationSource.RemoveSourceChangedHandler(_shellContent, OnSourceChanged);
        ComponentDispatcher.EnterThreadModal -= OnThreadModalChanged;
        ComponentDispatcher.LeaveThreadModal -= OnThreadModalChanged;
        _source?.RemoveHook(Hook);
        _source = null;
    }

    private void OnSourceChanged(object sender, SourceChangedEventArgs eventArgs) =>
        Attach(eventArgs.NewSource as HwndSource);

    private void Attach(HwndSource? source)
    {
        if (ReferenceEquals(_source, source))
        {
            return;
        }

        _source?.RemoveHook(Hook);
        _source = source;
        _source?.AddHook(Hook);
        Recompute();
    }

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // WM_ENABLE arrives after the window's enabled state has changed,
        // so the recompute reads the new state.
        if (message == WmEnable)
        {
            Recompute();
        }

        return IntPtr.Zero;
    }

    private void OnThreadModalChanged(object? sender, EventArgs eventArgs) => Recompute();

    private void Recompute()
    {
        if (_disposed)
        {
            return;
        }

        bool modalLoop = ComponentDispatcher.IsThreadModal
            || (_source is { IsDisposed: false } source && !NativeMethods.IsWindowEnabled(source.Handle));
        if (modalLoop == _modalLoop)
        {
            return;
        }

        _modalLoop = modalLoop;
        _changed(modalLoop);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowEnabled(IntPtr window);
    }
}
