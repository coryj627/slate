// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9 over R-5), codex's merge-delta check (finding 3):
/// the rescan's silent re-rank of an open Quick Open rebuilds its results
/// under a reader on a result row — every row container replaced. The
/// switcher keeps the reader's path selected; the results list now keeps the
/// keys on it too, through R-5's publication keeper.
/// </summary>
public sealed class QuickSwitcherRescanLandingTests
{
    /// <summary>The keys on a result that is NOT the first, a note created
    /// outside Slate ranked ahead of it by the rescan's replacement: the keys
    /// are on the same path's fresh row after the re-rank — one focus change,
    /// never the bare list or the window — and nothing is said.</summary>
    [Fact]
    public void ARescansSilentReRankKeepsTheKeysOnTheReadersResult() => StaThread.RunPumped(
        () =>
        {
            using var host = new Host();
            host.OpenQuickOpen();
            ListBox results = host.Shell.QuickSwitcherResultsList;
            QuickSwitcherRowViewModel reader = host.Switcher.Results[2];
            host.Switcher.SelectedRow = reader;
            results.UpdateLayout();
            Assert.True(Assert.IsType<ListBoxItem>(results.ItemContainerGenerator.ContainerFromItem(reader)).Focus());
            PumpedDispatcher.Drain();
            Assert.Equal(reader.Path, ResultWithKeys());
            host.FocusChanges.Clear();
            host.Announced.Clear();

            QuickSwitcherViewModel.ReplacementJournal journal = host.Switcher.BeginReplacement();
            Task replaced = host.Switcher.ReplaceFilesAsync(
                [new SwitcherFile("a-created-outside.md", "a-created-outside.md"), .. host.Files],
                journal,
                CancellationToken.None);
            Assert.True(PumpedDispatcher.PumpUntil(() => replaced.IsCompleted, TimeSpan.FromSeconds(10)), "the silent re-rank");
            host.Settle();

            Assert.Contains(host.Switcher.Results, row => row.Path == "a-created-outside.md");
            Assert.DoesNotContain(host.Switcher.Results, row => ReferenceEquals(row, reader));
            Assert.Equal(reader.Path, ResultWithKeys());
            Assert.Equal(reader.Path, host.Switcher.SelectedRow?.Path);
            Assert.True(
                host.FocusChanges.Count == 1 && host.FocusChanges[0] is ListBoxItem,
                "the keys moved " + string.Join(" → ", host.FocusChanges.Select(focus => focus.GetType().Name)));
            Assert.Empty(host.Announced);
        },
        TimeSpan.FromSeconds(120),
        "Quick Open landing fixture timed out.");

    private static string? ResultWithKeys() =>
        Keyboard.FocusedElement is ListBoxItem { DataContext: QuickSwitcherRowViewModel row } ? row.Path : null;

    /// <summary>The shipped shell's content — its Quick Open overlay among
    /// it — over a scanned four-note vault, lifted into a shown window of its
    /// own (the palette shell host's technique, not activated). The switcher
    /// is attached through the lifecycle's own setter, so the shell observes
    /// it as it does a real vault's.</summary>
    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(4, "quick-open-landing");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly VaultSession _session;
        private readonly VaultLifecycleViewModel _lifecycle;
        private readonly Window _window;

        public Host()
        {
            Assert.Null(Application.Current);
            _session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Files = [.. Enumerable.Range(0, 4).Select(index => new SwitcherFile($"note{index}.md", $"note{index}.md"))];
            Shell = new MainWindow();
            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            Switcher = new QuickSwitcherViewModel(
                _session,
                _fixture.Root,
                Announced.Add,
                initialFiles: Files,
                localAppDataRoot: _fixture.Root);
            SetField("_session", _session);
            SetField("_vaultPath", _fixture.Root);
            SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), true);
            SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), Switcher);

            UIElement content = Assert.IsAssignableFrom<UIElement>(Shell.Content);
            Shell.Content = null;
            _window = new Window
            {
                Content = content,
                DataContext = _lifecycle,
                Width = 1120,
                Height = 720,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            _window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/SlateWindows;component/Themes/Slate.Light.xaml",
                    UriKind.Absolute),
            });
            _window.AddHandler(
                Keyboard.GotKeyboardFocusEvent,
                new KeyboardFocusChangedEventHandler((_, e) => FocusChanges.Add(e.NewFocus)),
                handledEventsToo: true);
            _window.Show();
            Settle();
        }

        public MainWindow Shell { get; }

        public QuickSwitcherViewModel Switcher { get; }

        public SwitcherFile[] Files { get; }

        public List<A11yEvent> Announced { get; } = [];

        public List<IInputElement> FocusChanges { get; } = [];

        /// <summary>Open Quick Open and pump until its first rank has
        /// published every note.</summary>
        public void OpenQuickOpen()
        {
            Switcher.Open();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Switcher.Results.Count == Files.Length, TimeSpan.FromSeconds(10)),
                "Quick Open's first rank never published");
            Settle();
        }

        /// <summary>Layout, then everything queued, twice over.</summary>
        public void Settle()
        {
            for (int round = 0; round < 2; round++)
            {
                _window.UpdateLayout();
                PumpedDispatcher.Drain();
            }
        }

        public void Dispose()
        {
            try
            {
                Switcher.Dismiss();
                SetProperty(nameof(VaultLifecycleViewModel.QuickSwitcher), null);
                SetProperty(nameof(VaultLifecycleViewModel.IsVaultOpen), false);
                SetField("_session", null);
                SetField("_vaultPath", string.Empty);
                _window.Close();
                Shell.Close();
                PumpedDispatcher.Drain();
                Switcher.Dispose();
                _session.Dispose();
                _fixture.Dispose();
            }
            finally
            {
                CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe;
            }
        }

        private void SetField(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);

        private void SetProperty(string name, object? value) =>
            (typeof(VaultLifecycleViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"VaultLifecycleViewModel.{name} is gone"))
            .SetValue(_lifecycle, value);
    }
}
