// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using uniffi.slate_uniffi;

namespace SlateWindows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Standing host obligation (w0 baseline): install the host_logging
        // sink at startup so slate-core's non-fatal diagnostics reach the
        // app log (stderr). Verbose only for debug builds — a release
        // install pins the sink at warn (privacy floor, host_logging.rs).
#if DEBUG
        SlateUniffiMethods.InitHostLogging(@verbose: true);
#else
        SlateUniffiMethods.InitHostLogging(@verbose: false);
#endif
        base.OnStartup(e);
    }
}
