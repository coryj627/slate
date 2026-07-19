// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using uniffi.slate_uniffi;

namespace SlateWindows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
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

        base.OnStartup(e);
        new MainWindow().Show();
    }
}
