// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275 codex round 4, the owner's option (a): every modal loop over the
/// shell seals the palette (contract 28 T13/T14, I6). These facts show REAL
/// dialogs over the shipped shell — a message box, a common folder dialog,
/// a WPF <c>ShowDialog</c>, and the unsaved-changes prompt the app raises
/// on close — and watch the palette from inside each dialog's own modal
/// loop: a parked rank completes and its count window runs out there, and
/// nothing publishes, moves or speaks. When the dialog closes the query the
/// seal took ranks again, once.
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>A dialog of each kind the probe measured, owned by the
    /// window the shell lives in.</summary>
    [Theory]
    [InlineData("MessageBox")]
    [InlineData("OpenFolderDialog")]
    [InlineData("ShowDialog")]
    public void AModalLoopOverTheShellSealsThePalette(string kind) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        Window? wpfDialog = null;
        ModalLoopWitness witness = WitnessAModalLoop(
            host,
            show: () =>
            {
                switch (kind)
                {
                    case "MessageBox":
                        _ = MessageBox.Show(host.Window, "A prompt over the shell.", "slate-fact-messagebox");
                        break;
                    case "OpenFolderDialog":
                        _ = new OpenFolderDialog { Title = "slate-fact-folder" }.ShowDialog(host.Window);
                        break;
                    default:
                        wpfDialog = new Window
                        {
                            Title = "slate-fact-wpf-dialog",
                            Owner = host.Window,
                            Width = 240,
                            Height = 120,
                            ShowInTaskbar = false,
                        };
                        _ = wpfDialog.ShowDialog();
                        break;
                }
            },
            findCloser: () => kind == "ShowDialog"
                ? wpfDialog is { IsVisible: true } dialog ? dialog.Close : null
                : Win32DialogCloser());

        witness.AssertSealedAndSilentThenResumed(host);
    });

    /// <summary>
    /// Codex round 4's own path in the shipped shell: closing the app with
    /// a dirty tab while the palette is open raises the real unsaved-changes
    /// message box (<c>MainWindow.ConfirmUnsavedClose</c>) — which WPF never
    /// marks thread-modal, so only the shell's disabled window says it is
    /// up. Cancel keeps the app open and the palette resumes.
    /// </summary>
    [Fact]
    public void TheShippedClosePromptSealsThePalette() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        host.AttachDirtyWorkspace();
        bool closed = true;
        ModalLoopWitness witness = WitnessAModalLoop(
            host,
            show: () => closed = host.Lifecycle.PrepareForApplicationClose(),
            findCloser: Win32DialogCloser);

        Assert.False(closed, "Cancel on the unsaved-changes prompt closed the app");
        witness.AssertSealedAndSilentThenResumed(host);
    });

    /// <summary>
    /// Opens the palette over the shell, parks its lane, types a query whose
    /// rank waits behind the parked item, then runs <paramref name="show"/>'s
    /// modal loop. From dispatcher ticks INSIDE that loop the witness
    /// releases the lane, waits for the parked rank to finish and for any
    /// count it could have scheduled to run out, records what the palette
    /// did, and closes the dialog through <paramref name="findCloser"/>.
    /// </summary>
    private static ModalLoopWitness WitnessAModalLoop(
        PaletteShellHost host,
        Action show,
        Func<Action?> findCloser)
    {
        CommandPaletteViewModel palette = host.Palette;
        host.OpenThePalette();
        Assert.False(host.Shell.ModalLoops.IsModalLoopActive);
        var witness = new ModalLoopWitness();
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                witness.Published++;
            }
        };

        ManualResetEventSlim lane = host.ParkTheLane();
        palette.Query = "quick open";
        Task parkedRank = palette.RankCompletion;
        Assert.True(palette.IsRankPending);
        host.Heard.Clear();
        witness.SelectedBefore = palette.SelectedId;

        int ticks = 0;
        int settled = 0;
        bool released = false;
        var timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            ticks++;
            Action? close = findCloser();
            if (close is null)
            {
                return;
            }

            if (!released)
            {
                witness.SealedAtEntry = palette.IsSealed;
                witness.MonitorAtEntry = host.Shell.ModalLoops.IsModalLoopActive;
                released = true;
                lane.Set();
                return;
            }

            bool finished = parkedRank.IsCompleted && palette.FilterCountCompletion.IsCompleted;
            if (ticks < 200 && (!finished || ++settled < 3))
            {
                return;
            }

            witness.Inside = (witness.Published, host.Heard.Count, palette.SelectedId);
            timer.Stop();
            close();
        };
        timer.Start();
        show();
        timer.Stop();
        return witness;
    }

    /// <summary>A visible #32770 dialog of this thread — a message box or a
    /// common dialog — as a closer that sends it WM_CLOSE (Cancel).</summary>
    private static Action? Win32DialogCloser()
    {
        IntPtr found = IntPtr.Zero;
        _ = DialogNativeMethods.EnumThreadWindows(
            DialogNativeMethods.GetCurrentThreadId(),
            (window, _) =>
            {
                var name = new StringBuilder(64);
                _ = DialogNativeMethods.GetClassName(window, name, name.Capacity);
                if (name.ToString() == "#32770" && DialogNativeMethods.IsWindowVisible(window))
                {
                    found = window;
                    return false;
                }

                return true;
            },
            IntPtr.Zero);
        return found == IntPtr.Zero
            ? null
            : () => _ = DialogNativeMethods.PostMessage(found, 0x0010, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>What the palette did around one modal loop.</summary>
    private sealed class ModalLoopWitness
    {
        public int Published { get; set; }

        public string? SelectedBefore { get; set; }

        public bool SealedAtEntry { get; set; }

        public bool MonitorAtEntry { get; set; }

        public (int Published, int Heard, string? Selected)? Inside { get; set; }

        /// <summary>Sealed from the loop's first tick, silent and unmoved
        /// through it; once it closed, the seal lifted and the query it took
        /// ranked again — one publication, its selection, one count.</summary>
        public void AssertSealedAndSilentThenResumed(PaletteShellHost host)
        {
            CommandPaletteViewModel palette = host.Palette;
            Assert.True(MonitorAtEntry, "the shell never reported the modal loop");
            Assert.True(SealedAtEntry, "the palette was not sealed inside the modal loop");
            Assert.NotNull(Inside);
            Assert.True(
                Inside.Value.Published == 0 && Inside.Value.Heard == 0 && Inside.Value.Selected == SelectedBefore,
                $"inside the modal loop the palette published {Inside.Value.Published} time(s), made "
                + $"{Inside.Value.Heard} announcement(s), and moved its selection from '{SelectedBefore}' "
                + $"to '{Inside.Value.Selected}'");

            Assert.False(host.Shell.ModalLoops.IsModalLoopActive);
            Assert.False(palette.IsSealed);
            Assert.True(palette.IsOpen);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
                "the query the seal took never ranked again");
            PumpedDispatcher.Drain();
            PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
            PumpedDispatcher.Drain();
            Assert.Equal(1, Published);
            Assert.Equal("slate.workspace.quickOpen", palette.SelectedId);
            Assert.Collection(
                host.Heard,
                announced => Assert.IsType<A11yEvent.PaletteCommandSelected>(announced),
                announced => Assert.Equal(
                    "quick open",
                    Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
        }
    }

    private static class DialogNativeMethods
    {
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr parameter);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
