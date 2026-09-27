// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275 codex round 4, the owner's option (a): every modal loop over the
/// shell seals the palette (contract 28 T13/T14, I6). These facts run modal
/// loops over the shipped shell — each shape of loop the round-4 probe
/// measured, and the unsaved-changes prompt the app raises on close — and
/// watch the palette from inside the loop: a parked rank completes and its
/// count window runs out there, and nothing publishes, moves or speaks. When
/// the loop ends the query the seal took ranks again, once.
/// </summary>
/// <remarks>
/// CI's Windows session is non-interactive: a native message box or common
/// dialog never becomes a visible window there, so nothing can find and
/// close it and the fact hangs (the #1298 failure). The native prompts are
/// therefore replaced by <see cref="SyntheticPrompt"/>, a nested dispatcher
/// frame that raises exactly the signals the probe measured for each — a
/// message box disables its owner and never enters WPF's thread-modal state;
/// a common dialog does both — so the shell's monitor sees the same
/// <c>WM_ENABLE</c> and <c>EnterThreadModal</c> it would. The WPF
/// <c>ShowDialog</c> kind stays real: a WPF window is found through its own
/// object, not as a visible native window. That the native dialogs raise
/// those signals is the probe's measurement, not CI's (contract 40 AR-47).
/// </remarks>
public sealed partial class CommandPaletteTests
{
    /// <summary>What a modal loop tells the shell it is up.</summary>
    [Flags]
    public enum LoopSignals
    {
        /// <summary>No signal: a command's own nested frame.</summary>
        None = 0,

        /// <summary>The shell's window disabled (<c>WM_ENABLE</c>) — what a
        /// message box does to its owner.</summary>
        DisablesShell = 1,

        /// <summary>WPF's thread-modal state
        /// (<c>ComponentDispatcher.PushModal</c>).</summary>
        ThreadModal = 2,
    }

    /// <summary>Each shape of modal loop over the window the shell lives in:
    /// a message box's (the shell disabled, not thread-modal), a common
    /// dialog's (both), a thread-modal section that leaves the shell enabled,
    /// and a real WPF <c>ShowDialog</c>.</summary>
    [Theory]
    [InlineData("MessageBoxShaped")]
    [InlineData("CommonDialogShaped")]
    [InlineData("ThreadModalOnly")]
    [InlineData("ShowDialog")]
    public void AModalLoopOverTheShellSealsThePalette(string kind) => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        Window? wpfDialog = null;
        SyntheticPrompt? prompt = kind switch
        {
            "MessageBoxShaped" => new SyntheticPrompt(host.Window, LoopSignals.DisablesShell),
            "CommonDialogShaped" => new SyntheticPrompt(host.Window, LoopSignals.DisablesShell | LoopSignals.ThreadModal),
            "ThreadModalOnly" => new SyntheticPrompt(host.Window, LoopSignals.ThreadModal),
            _ => null,
        };
        ModalLoopWitness witness = WitnessAModalLoop(
            host,
            show: () =>
            {
                if (prompt is not null)
                {
                    prompt.Run();
                    return;
                }

                wpfDialog = new Window
                {
                    Title = "slate-fact-wpf-dialog",
                    Owner = host.Window,
                    Width = 240,
                    Height = 120,
                    ShowInTaskbar = false,
                };
                _ = wpfDialog.ShowDialog();
            },
            findCloser: () => prompt is not null
                ? prompt.Closer()
                : wpfDialog is { IsVisible: true } dialog ? dialog.Close : null);

        witness.AssertSealedAndSilentThenResumed(host);
    });

    /// <summary>
    /// Codex round 4's own path in the shipped shell: closing the app with
    /// a dirty tab while the palette is open raises the unsaved-changes
    /// prompt through the lifecycle's own prompt seam — here a message-box
    /// shaped loop, which never enters WPF's thread-modal state, so only the
    /// shell's disabled window says it is up. Cancel keeps the app open and
    /// the palette resumes.
    /// </summary>
    [Fact]
    public void TheShippedClosePromptSealsThePalette() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        host.AttachDirtyWorkspace();
        SyntheticPrompt prompt = InstallClosePrompt(host.Lifecycle, host.Window, LoopSignals.DisablesShell);
        bool closed = true;
        ModalLoopWitness witness = WitnessAModalLoop(
            host,
            show: () => closed = host.Lifecycle.PrepareForApplicationClose(),
            findCloser: prompt.Closer);

        Assert.True(prompt.Ran, "closing the app never raised the unsaved-changes prompt");
        Assert.False(closed, "Cancel on the unsaved-changes prompt closed the app");
        witness.AssertSealedAndSilentThenResumed(host);
    });

    /// <summary>
    /// Opens the palette over the shell, parks its lane, types a query whose
    /// rank waits behind the parked item, then runs <paramref name="show"/>'s
    /// modal loop. From dispatcher ticks INSIDE that loop the witness
    /// releases the lane, waits for the parked rank to finish and for any
    /// count it could have scheduled to run out, records what the palette
    /// did, and closes the loop through <paramref name="findCloser"/>.
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
                // No loop came up: stop rather than spin, and let the
                // witness's own assertions say so.
                if (ticks >= 200)
                {
                    timer.Stop();
                }

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

    /// <summary>Replaces the lifecycle's unsaved-changes prompt — the seam
    /// the shell fills with its message box — with <paramref name="signals"/>'
    /// loop over <paramref name="shell"/>'s window, answering Cancel.</summary>
    private static SyntheticPrompt InstallClosePrompt(
        VaultLifecycleViewModel lifecycle, Visual? shell, LoopSignals signals)
    {
        var prompt = new SyntheticPrompt(shell, signals);
        (typeof(VaultLifecycleViewModel).GetField("_confirmUnsavedClose", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("VaultLifecycleViewModel._confirmUnsavedClose is gone"))
            .SetValue(lifecycle, (Func<VaultCloseDecision>)(() =>
            {
                prompt.Run();
                return VaultCloseDecision.Cancel;
            }));
        return prompt;
    }

    /// <summary>
    /// A modal loop with a native prompt's signals and no native window: it
    /// disables the window the shell lives in and/or enters WPF's
    /// thread-modal state, as the round-4 probe measured each prompt doing,
    /// then pumps a nested dispatcher frame — timers and posted work run
    /// inside it, as they do inside a message box's loop — until
    /// <see cref="Closer"/>'s action ends it; then it undoes the signals in
    /// the prompt's order (the owner re-enabled as the box closes).
    /// </summary>
    private sealed class SyntheticPrompt(Visual? shell, LoopSignals signals)
    {
        private DispatcherFrame? _frame;

        /// <summary>Whether the loop has run.</summary>
        public bool Ran { get; private set; }

        /// <summary>Ends the loop while it runs; null otherwise.</summary>
        public Action? Closer() => _frame is { } frame ? () => frame.Continue = false : null;

        public void Run()
        {
            IntPtr window = IntPtr.Zero;
            if (signals.HasFlag(LoopSignals.DisablesShell))
            {
                window = shell is not null && PresentationSource.FromVisual(shell) is HwndSource source
                    ? source.Handle
                    : throw new InvalidOperationException("a loop that disables the shell needs the shell's window");
            }

            if (signals.HasFlag(LoopSignals.ThreadModal))
            {
                ComponentDispatcher.PushModal();
            }

            if (window != IntPtr.Zero)
            {
                _ = PromptNativeMethods.EnableWindow(window, false);
            }

            _frame = new DispatcherFrame();
            Ran = true;
            try
            {
                Dispatcher.PushFrame(_frame);
            }
            finally
            {
                _frame = null;
                if (window != IntPtr.Zero)
                {
                    _ = PromptNativeMethods.EnableWindow(window, true);
                }

                if (signals.HasFlag(LoopSignals.ThreadModal))
                {
                    ComponentDispatcher.PopModal();
                }
            }
        }
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

    private static class PromptNativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnableWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool enable);
    }
}
