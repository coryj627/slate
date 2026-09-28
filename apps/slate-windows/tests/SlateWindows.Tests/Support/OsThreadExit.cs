// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SlateWindows.Tests;

/// <summary>
/// A thread's OS-level exit, as a wait handle: signalled only once the
/// operating system has finished the thread — and so, for a GUI thread,
/// once the system has destroyed its windows.
/// </summary>
/// <remarks>
/// A managed <see cref="Thread.Join()"/> does not always wait that long.
/// The runtime marks a thread dead when its managed code ends, and a Join
/// that begins after that answers at once, while the OS thread is still in
/// its exit path — DLL detach, COM teardown, and only then the kernel
/// destroying its windows. Measured: with the OS exit held 300 ms (an FLS
/// callback), a Join issued once <see cref="Thread.IsAlive"/> is false
/// returned in 0.0 ms with the OS thread still running, and every class
/// the thread registered refused to unregister ("Class still has open
/// windows"); issued before the thread died, Join waited the full 300 ms.
/// On CI a runner descheduled between <c>Start</c> and <c>Join</c> for
/// longer than an 18 ms fact takes is the late Join. Anything that frees a
/// thread's window classes therefore waits on this first.
/// </remarks>
internal sealed class OsThreadExit : WaitHandle
{
    private const uint Synchronize = 0x00100000;

    private OsThreadExit(SafeWaitHandle handle) => SafeWaitHandle = handle;

    /// <summary>The exit of the CALLING thread. Opened on the thread itself,
    /// while it runs: a thread id names another thread once it has ended.</summary>
    internal static OsThreadExit OfCurrentThread()
    {
        IntPtr handle = OpenThread(Synchronize, false, GetCurrentThreadId());
        return handle == IntPtr.Zero
            ? throw new Win32Exception()
            : new OsThreadExit(new SafeWaitHandle(handle, ownsHandle: true));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenThread(uint access, bool inheritHandle, uint threadId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
