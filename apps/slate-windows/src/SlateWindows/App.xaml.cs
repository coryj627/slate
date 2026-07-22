// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using uniffi.slate_uniffi;

namespace SlateWindows;

public partial class App : Application
{
    private ThemeManager? _themeManager;
    private SingleInstanceCoordinator? _singleInstanceCoordinator;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // W1-1 Mica policy: every text-bearing Slate surface has a solid,
        // token-backed background. Disable Fluent's window backdrop so it
        // cannot bleed into those measurable text/background pairs.
        AppContext.SetSwitch(
            "Switch.System.Windows.Appearance.DisableFluentThemeWindowBackdrop",
            true);
        if (!DpiAwarenessProbe.EnsurePerMonitorV2())
        {
            Console.Error.WriteLine("Slate could not establish PerMonitorV2 DPI awareness on its UI thread.");
        }

        // Standing host obligation (w0 baseline): route native stderr into
        // the durable app log, then install the host_logging sink so
        // slate-core's non-fatal diagnostics surface there. Verbose only
        // for debug builds — a release install pins the sink at warn
        // (privacy floor, host_logging.rs).
        HostLog.RedirectNativeStderrToAppLog();
#if DEBUG
        SlateUniffiMethods.InitHostLogging(@verbose: true);
#else
        SlateUniffiMethods.InitHostLogging(@verbose: false);
#endif

        // Explicit dictionaries instead of the still-experimental ThemeMode
        // API. ThemeManager also swaps both Fluent and Slate token layers on
        // live Windows Contrast transitions.
        _themeManager = new ThemeManager(this, ThemeManager.ReadSystemTheme());

        if (e.Args.Contains("--census-dpi-probe"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            var probeWindow = new Window
            {
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            IntPtr probeHandle = new WindowInteropHelper(probeWindow).EnsureHandle();
            bool isPerMonitorV2 = DpiAwarenessProbe.IsWindowPerMonitorV2(probeHandle);
            probeWindow.Close();
            Shutdown(isPerMonitorV2 ? 0 : 1);
            return;
        }

        if (e.Args.Contains("--census-theme-probe"))
        {
            ThemeManager.ValidateResourceDictionaries();
            Shutdown(0);
            return;
        }

        // Census hook (HostLoggingCensus): exercise the real WinExe
        // startup path — trigger the deterministic palette-recents warn
        // and exit before any window shows, so the census can assert the
        // diagnostic reached the app log of the production binary.
        if (e.Args.Contains("--census-log-probe"))
        {
            _ = SlateUniffiMethods.PaletteRecentsDecode(new byte[(1 << 16) + 1]);
            Shutdown(0);
            return;
        }

        string? censusInstanceIdentity =
            Environment.GetEnvironmentVariable("SLATE_CENSUS_INSTANCE_ID");
        _singleInstanceCoordinator = string.IsNullOrEmpty(censusInstanceIdentity)
            ? SingleInstanceCoordinator.CreateForCurrentUser()
            : new SingleInstanceCoordinator(censusInstanceIdentity);
        if (!_singleInstanceCoordinator.IsPrimary)
        {
            bool delivered = _singleInstanceCoordinator.SendActivation(
                e.Args,
                TimeSpan.FromSeconds(3));
            if (!delivered)
            {
                Console.Error.WriteLine(
                    "Could not deliver activation to the running Slate instance.");
            }

            Shutdown(delivered ? 0 : 1);
            return;
        }

        string? activationCensusOutput = ActivationArguments.OptionValue(
            e.Args,
            "--census-single-instance-primary");
        if (activationCensusOutput is not null)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            _singleInstanceCoordinator.StartListening(arguments =>
            {
                try
                {
                    File.WriteAllBytes(
                        activationCensusOutput,
                        JsonSerializer.SerializeToUtf8Bytes(arguments));
                }
                finally
                {
                    _ = Dispatcher.InvokeAsync(() => Shutdown(0));
                }
            });
            File.WriteAllText(activationCensusOutput + ".ready", "ready");
            return;
        }

        base.OnStartup(e);
        _mainWindow = new MainWindow();
        _singleInstanceCoordinator.StartListening(QueueActivation);
        _mainWindow.Show();

        string? initialVaultPath = ActivationArguments.FindVaultPath(e.Args);
        if (initialVaultPath is not null)
        {
            _ = _mainWindow.ActivateFromExternalRequestAsync(initialVaultPath);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceCoordinator?.Dispose();
        _themeManager?.Dispose();
        base.OnExit(e);
    }

    private void QueueActivation(string[] arguments)
    {
        string? vaultPath = ActivationArguments.FindVaultPath(arguments);
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (_mainWindow is not null)
            {
                _ = _mainWindow.ActivateFromExternalRequestAsync(vaultPath);
            }
        });
    }
}
