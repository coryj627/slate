// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// WB-9 (#1296; the owner's call of 2026-10-26): a second launch or a
/// jump-list open — the single-instance handoff — that arrives while a modal
/// loop runs over the shell, the palette's seal holds, or a close decides,
/// waits, and runs once when the hold ends; of several, the last runs. With
/// nothing holding the shell it opens at once, as before. Every open is real,
/// over the fixture's recents file.
/// </summary>
public sealed class MainWindowHandoffTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    [Fact]
    public void AHandoffDuringAModalLoopParksAndRunsOnceWhenItEnds() => RunSta(() =>
    {
        using var other = FixtureVault.Create(1, "handoff-other");
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        var prompt = new CommandPaletteTests.SyntheticPrompt(shell.Shell, CommandPaletteTests.LoopSignals.DisablesShell);
        int underThePrompt = -1;
        InsideThePrompt(shell, prompt, () =>
        {
            Assert.True(shell.Shell.ModalLoops.IsModalLoopActive, "premise: the prompt is a modal loop over the shell");
            _ = shell.Shell.ActivateFromExternalRequestAsync(other.Root);
        }, () => underThePrompt = shell.OpenRequests.Count);

        Assert.Equal(0, underThePrompt);
        List<IInputElement> changes = shell.RecordFocusChanges();
        WaitForOpenRequests(shell, 1);
        Assert.Equal([other.Root], shell.OpenRequests);
        shell.WaitForTheOpenToEnd();
        // The parked run brings the window forward and opens; the keys move
        // to the launch landing, never to the bare window (red team).
        Assert.DoesNotContain(changes, change => ReferenceEquals(change, shell.Shell));
    });

    /// <summary>Red team: a hold that ends straight into an open — the folder
    /// picker's loop, then the open it starts in the same turn — keeps the
    /// request parked until that open has ended, then runs it; the open's
    /// busy refusal never swallows it.</summary>
    [Fact]
    public void AHandoffParkedBehindAPromptThatEndsIntoAnOpenRunsAfterTheOpen() => RunSta(() =>
    {
        using var other = FixtureVault.Create(1, "handoff-into-an-open");
        using var shell = new WelcomeShell();
        var prompt = new CommandPaletteTests.SyntheticPrompt(shell.Shell, CommandPaletteTests.LoopSignals.DisablesShell);
        InsideThePrompt(shell, prompt, () => _ = shell.Shell.ActivateFromExternalRequestAsync(other.Root), () => { });

        // The prompt's end runs into an open in the same turn, as the
        // picker's does.
        shell.Lifecycle.OpenVaultCommand.Execute(null);
        shell.WaitForTheOpenSessionPark();
        PumpedDispatcher.Drain();
        Assert.Equal([shell.VaultRoot], shell.OpenRequests);

        shell.ReleaseParks();
        WaitForOpenRequests(shell, 2);
        shell.WaitForTheOpenToEnd();
        Assert.Equal([shell.VaultRoot, other.Root], shell.OpenRequests);
        Assert.Equal(other.Root, shell.Lifecycle.VaultPath);
    });

    /// <summary>Red team: a handoff that arrives while an open runs waits for
    /// it, then runs — where it used to meet the open's busy refusal and be
    /// dropped without a word.</summary>
    [Fact]
    public void AHandoffDuringAnOpenRunsOnceTheOpenEnds() => RunSta(() =>
    {
        using var other = FixtureVault.Create(1, "handoff-during-an-open");
        using var shell = new WelcomeShell();
        shell.Lifecycle.OpenVaultCommand.Execute(null);
        shell.WaitForTheOpenSessionPark();

        _ = shell.Shell.ActivateFromExternalRequestAsync(other.Root);
        PumpedDispatcher.Drain();
        Assert.Equal([shell.VaultRoot], shell.OpenRequests);

        shell.ReleaseParks();
        WaitForOpenRequests(shell, 2);
        shell.WaitForTheOpenToEnd();
        PumpedDispatcher.Drain();
        Assert.Equal([shell.VaultRoot, other.Root], shell.OpenRequests);
        Assert.Equal(other.Root, shell.Lifecycle.VaultPath);
    });

    /// <summary>Codex round 1: a handoff that arrives after a hold has ended
    /// but before the parked request has run is the newer request: it runs,
    /// and the older one never does.</summary>
    [Fact]
    public void ANewerHandoffBeforeTheParkedOneRunsSupersedesIt() => RunSta(() =>
    {
        using var older = FixtureVault.Create(1, "handoff-older");
        using var newer = FixtureVault.Create(1, "handoff-newer");
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        var prompt = new CommandPaletteTests.SyntheticPrompt(shell.Shell, CommandPaletteTests.LoopSignals.DisablesShell);
        InsideThePrompt(shell, prompt, () => _ = shell.Shell.ActivateFromExternalRequestAsync(older.Root), () => { });

        // The prompt has ended and its release waits at Input priority; the
        // pipe's next delivery runs first.
        _ = shell.Shell.ActivateFromExternalRequestAsync(newer.Root);
        WaitForOpenRequests(shell, 1);
        shell.WaitForTheOpenToEnd();
        PumpedDispatcher.Drain();
        shell.WaitForTheOpenToEnd();

        Assert.Equal([newer.Root], shell.OpenRequests);
        Assert.Equal(newer.Root, shell.Lifecycle.VaultPath);
    });

    [Fact]
    public void TwoHandoffsDuringOnePromptRunOnlyTheLast() => RunSta(() =>
    {
        using var first = FixtureVault.Create(1, "handoff-first");
        using var second = FixtureVault.Create(1, "handoff-second");
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        var prompt = new CommandPaletteTests.SyntheticPrompt(shell.Shell, CommandPaletteTests.LoopSignals.DisablesShell);
        InsideThePrompt(shell, prompt, () =>
        {
            _ = shell.Shell.ActivateFromExternalRequestAsync(first.Root);
            _ = shell.Shell.ActivateFromExternalRequestAsync(second.Root);
        }, () => Assert.Empty(shell.OpenRequests));

        WaitForOpenRequests(shell, 1);
        shell.WaitForTheOpenToEnd();
        Assert.Equal([second.Root], shell.OpenRequests);
    });

    /// <summary>The palette's seal holds the handoff — a command running,
    /// held here directly — and lifting it through the palette's own path
    /// runs the parked request: the window listens for the seal's release,
    /// not only for the modal loop's end.</summary>
    [Fact]
    public void AHandoffWhileAPaletteCommandIsSealedWaitsForTheSeal() => RunSta(() =>
    {
        using var other = FixtureVault.Create(1, "handoff-sealed");
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        CommandPaletteViewModel palette = shell.Lifecycle.Palette;
        int released = 0;
        palette.SealReleased += (_, _) => released++;
        System.Reflection.FieldInfo invoking = typeof(CommandPaletteViewModel).GetField(
                "_isInvoking", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("the palette no longer seals through _isInvoking");
        invoking.SetValue(palette, true);
        Assert.True(palette.IsSealed, "premise: a command holds the seal");

        _ = shell.Shell.ActivateFromExternalRequestAsync(other.Root);
        PumpedDispatcher.Drain();
        Assert.Empty(shell.OpenRequests);

        // The command returns, and a loop's end lifts the seal on the
        // palette's own path (the window's modal-loop callback never runs).
        invoking.SetValue(palette, false);
        palette.SetModalLoop(true);
        palette.SetModalLoop(false);

        WaitForOpenRequests(shell, 1);
        Assert.Equal(1, released);
        Assert.Equal([other.Root], shell.OpenRequests);
        shell.WaitForTheOpenToEnd();
    });

    /// <summary>The owner's call: a close deciding with the window enabled
    /// (#1622 pumps it) — here inside its unsaved-changes prompt, answered
    /// from a nested frame that disables nothing — holds the handoff, which
    /// moves no keys while it waits and runs once the close has decided.</summary>
    [Fact]
    public void AHandoffWhileACloseDecidesWaitsForTheDecision() => RunSta(() =>
    {
        using var other = FixtureVault.Create(1, "handoff-closing");
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        shell.Lifecycle.OpenVaultCommand.Execute(null);
        shell.WaitForTheOpenToEnd();
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(shell.Lifecycle.Workspace);
        workspace.OpenPath("note0.md");
        Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab).Text += "\nUnsaved.";
        int openedBefore = shell.OpenRequests.Count;
        int duringTheClose = -1;
        bool decidingWhenItArrived = false;
        IInputElement? keysBefore = null;
        IInputElement? keysWhileParked = null;
        shell.Shell.ShowUnsavedClosePrompt = (_, _, _, _, _, _) =>
        {
            var frame = new DispatcherFrame();
            _ = shell.Shell.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                decidingWhenItArrived = shell.Lifecycle.IsDecidingClose
                    && !shell.Shell.ModalLoops.IsModalLoopActive
                    && !shell.Lifecycle.Palette.IsSealed;
                keysBefore = Keyboard.FocusedElement;
                _ = shell.Shell.ActivateFromExternalRequestAsync(other.Root);
                _ = shell.Shell.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    duringTheClose = shell.OpenRequests.Count;
                    keysWhileParked = Keyboard.FocusedElement;
                    frame.Continue = false;
                });
            });
            Dispatcher.PushFrame(frame);
            return MessageBoxResult.No;
        };

        shell.Lifecycle.CloseVaultCommand.Execute(null);

        Assert.True(decidingWhenItArrived, "premise: the handoff arrived while the close decided, with no modal and no seal");
        Assert.Equal(openedBefore, duringTheClose);
        Assert.NotNull(keysBefore);
        Assert.NotSame(shell.Shell, keysBefore);
        Assert.Same(keysBefore, keysWhileParked);
        WaitForOpenRequests(shell, openedBefore + 1);
        shell.WaitForTheOpenToEnd();
        Assert.Equal(other.Root, shell.OpenRequests[^1]);
        Assert.Equal(other.Root, shell.Lifecycle.VaultPath);
    });

    [Fact]
    public void AHandoffWithNoModalBehavesAsToday() => RunSta(() =>
    {
        using var other = FixtureVault.Create(1, "handoff-plain");
        using var shell = new WelcomeShell();
        shell.ReleaseParks();

        _ = shell.Shell.ActivateFromExternalRequestAsync(other.Root);

        Assert.Equal([other.Root], shell.OpenRequests);
        shell.WaitForTheOpenToEnd();
    });

    /// <summary>Run <paramref name="prompt"/>; inside its loop run
    /// <paramref name="inside"/>, then — a pass later — <paramref name="then"/>,
    /// and close it.</summary>
    private static void InsideThePrompt(
        WelcomeShell shell, CommandPaletteTests.SyntheticPrompt prompt, Action inside, Action then)
    {
        _ = shell.Shell.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            inside();
            _ = shell.Shell.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                try
                {
                    then();
                }
                finally
                {
                    prompt.Closer()!();
                }
            });
        });
        prompt.Run();
        Assert.True(prompt.Ran, "the prompt never ran");
    }

    private static void WaitForOpenRequests(WelcomeShell shell, int count) =>
        Assert.True(
            PumpedDispatcher.PumpUntil(() => shell.OpenRequests.Count >= count, Bound),
            $"the parked handoff never ran ({shell.OpenRequests.Count} open request(s))");

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
