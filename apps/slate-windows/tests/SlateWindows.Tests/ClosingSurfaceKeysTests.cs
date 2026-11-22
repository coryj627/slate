// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Editing;
using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11): the close buttons that leave with their surface. Quick Open's
/// Close is always on now: it shows only while Quick Open does, and its press closes it, so it
/// never goes off with the keys on it. The editor popover's Close and Open source cannot be
/// always on (the window's Escape binding and the trash's Escape fallback read the close's
/// CanExecute): as the popover closes, its commands go off, and the keys move first, to the
/// editor. The real window with the sidebar attached; each press is Enter or the peer's Invoke.
/// </summary>
public sealed class ClosingSurfaceKeysTests
{
    /// <summary>Quick Open's Close by Enter, with the keys on it: it never goes off, Quick Open
    /// closes, and the keys go back to the editor. Its row is exempt as always on
    /// (QuickSwitcherViewModel.DismissCommand); this fact holds its press.</summary>
    [Fact]
    public void QuickOpensCloseStaysOnAsItsPressClosesQuickOpen() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "# A\n"), ("b.md", "# B\n"));
        using var sidebar = new ShownShellSidebar(host);
        QuickSwitcherViewModel switcher = AttachQuickOpen(host);
        try
        {
            host.Workspace.OpenPath("a.md");
            host.Settle();
            Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor did not take the keys");
            host.Settle();
            TextArea editor = ShownShell.Descendants(host.Shell.WorkspaceRoot).OfType<TextArea>().First(area => area.IsVisible);
            switcher.Open();
            host.Settle();
            Assert.True(host.Shell.QuickSwitcherOverlay.IsKeyboardFocusWithin, $"premise: Quick Open did not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
            Button close = ShownShell.Descendants(host.Shell.QuickSwitcherOverlay).OfType<Button>()
                .Single(button => AutomationProperties.GetAutomationId(button) == "QuickSwitcherClose");
            Assert.Same(switcher.DismissCommand, close.Command);
            Assert.True(
                close.Focus() && ReferenceEquals(close, Keyboard.FocusedElement),
                $"keyboard premise: Close could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

            KeysFirstRecorder.AssertItStaysOn("Quick Open's Close", close, () =>
            {
                Press(host, Key.Enter);
                host.Settle();
            }, () => !switcher.IsOpen);

            Assert.True(
                ReferenceEquals(editor, Keyboard.FocusedElement),
                $"after Quick Open closed the keys were on {Describe(Keyboard.FocusedElement)}, not on the editor");
        }
        finally
        {
            DetachQuickOpen(host, switcher);
        }
    });

    /// <summary>The embed preview's Close, by the peer's Invoke, with the keys on it: the keys
    /// go to the editor before ClosePopoverCommand goes off as the popover closes. The
    /// keys-first witness of EditorPopoverClose.</summary>
    [Fact]
    public void ThePopoversCloseLetsTheKeysGoToTheEditorFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Before ![[b]] after\n"), ("b.md", "# B\n\nBody of b.\n"));
        using var sidebar = new ShownShellSidebar(host);
        EditorInteractionCoordinator interactions = OpenThePreview(host);
        Button close = PopoverButton(host, "EditorPopoverClose");
        Assert.Same(interactions.ClosePopoverCommand, close.Command);
        Assert.True(
            close.Focus() && ReferenceEquals(close, Keyboard.FocusedElement),
            $"keyboard premise: the popover's Close could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the popover's Close", close, () =>
        {
            ((IInvokeProvider)new ButtonAutomationPeer(close).GetPattern(PatternInterface.Invoke)).Invoke();
            host.Settle();
            host.Settle();
        });

        Assert.False(interactions.IsPopoverOpen, "Close left the popover open");
        Assert.True(
            Keyboard.FocusedElement is TextArea { IsVisible: true },
            $"after the popover closed the keys were on {Describe(Keyboard.FocusedElement)}, not on the editor");
    });

    /// <summary>The embed preview's Open source, by the peer's Invoke, with the keys on it: the
    /// keys leave it before OpenPopoverSourceCommand goes off as the popover closes, and the
    /// source note opens with the keys in its editor. The keys-first witness of
    /// EditorPopoverOpenSource.</summary>
    [Fact]
    public void ThePopoversOpenSourceLetsTheKeysGoFirst() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "Before ![[b]] after\n"), ("b.md", "# B\n\nBody of b.\n"));
        using var sidebar = new ShownShellSidebar(host);
        EditorInteractionCoordinator interactions = OpenThePreview(host);
        Button openSource = PopoverButton(host, "EditorPopoverOpenSource");
        Assert.Same(interactions.OpenPopoverSourceCommand, openSource.Command);
        Assert.True(
            openSource.Focus() && ReferenceEquals(openSource, Keyboard.FocusedElement),
            $"keyboard premise: Open source could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the popover's Open source", openSource, () =>
        {
            ((IInvokeProvider)new ButtonAutomationPeer(openSource).GetPattern(PatternInterface.Invoke)).Invoke();
            host.Settle();
            host.Settle();
        });

        Assert.False(interactions.IsPopoverOpen, "Open source left the popover open");
        Assert.Equal("b.md", host.Workspace.ActiveGroup.ActiveTab?.Path);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => Keyboard.FocusedElement is TextArea { IsVisible: true }, TimeSpan.FromSeconds(10)),
            $"after the source opened the keys were on {Describe(Keyboard.FocusedElement)}, not in its editor");
    });

    /// <summary>a.md open with the keys in its editor, and Ctrl+E's preview of its embed
    /// open, its title read.</summary>
    private static EditorInteractionCoordinator OpenThePreview(ShownShell host)
    {
        host.Workspace.OpenPath("a.md");
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor did not take the keys");
        host.Settle();
        WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
        EditorInteractionCoordinator interactions = tab.EditorInteractions
            ?? throw new Xunit.Sdk.XunitException("premise: the note's tab has no editor interactions");
        interactions.RefreshArtifactCacheForTests();
        Assert.True(interactions.PreviewEmbedAt(tab.Text.IndexOf("![[b]]", StringComparison.Ordinal) + 3), "premise: the embed's preview refused");
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => interactions.IsPopoverOpen && !interactions.PopoverTitle.StartsWith("Loading", StringComparison.Ordinal),
                TimeSpan.FromSeconds(10)),
            "premise: the embed's preview never opened");
        host.Settle();
        return interactions;
    }

    private static Button PopoverButton(ShownShell host, string id) =>
        ShownShell.Descendants(host.Shell).OfType<Button>()
            .Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == id);

    /// <summary>A Quick Open the window resolves, through the lifecycle's own setter, over the
    /// shell's session (the palette shell host's shape).</summary>
    private static QuickSwitcherViewModel AttachQuickOpen(ShownShell host)
    {
        var switcher = new QuickSwitcherViewModel(host.Session, host.Root, _ => { }, localAppDataRoot: host.Root);
        SetLifecycleField(host, "_session", host.Session);
        SetLifecycleField(host, "_vaultPath", host.Root);
        SetLifecycleProperty(host, nameof(VaultLifecycleViewModel.QuickSwitcher), switcher);
        host.Settle();
        return switcher;
    }

    private static void DetachQuickOpen(ShownShell host, QuickSwitcherViewModel switcher)
    {
        switcher.Dismiss();
        SetLifecycleProperty(host, nameof(VaultLifecycleViewModel.QuickSwitcher), null);
        SetLifecycleField(host, "_session", null);
        SetLifecycleField(host, "_vaultPath", string.Empty);
        PumpedDispatcher.Drain();
        switcher.Dispose();
    }

    private static void SetLifecycleField(ShownShell host, string name, object? value) =>
        (typeof(VaultLifecycleViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
        .SetValue(host.Lifecycle, value);

    private static void SetLifecycleProperty(ShownShell host, string name, object? value) =>
        (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
        .SetValue(host.Lifecycle, value);

    /// <summary>A key through WPF's input pipeline, down then up, to the element holding
    /// the keys.</summary>
    private static void Press(ShownShell host, Key key)
    {
        foreach (RoutedEvent direction in new[] { Keyboard.PreviewKeyDownEvent, Keyboard.PreviewKeyUpEvent })
        {
            InputManager.Current.ProcessInput(
                new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, key)
                {
                    RoutedEvent = direction,
                });
        }
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

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
