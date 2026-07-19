// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Child-process probe for the host-logging census (w0_spec §W0-3 item 4,
// #715). slate-core routes non-fatal diagnostics through its `log` facade;
// `init_host_logging` installs the stderr sink. Native writes go straight
// to fd 2, which an in-process test cannot intercept — so the census runs
// this probe as a child process and asserts on its captured stderr.
//
// The deterministic warn trigger: palette recents input over the 64 KiB
// threshold logs "palette recents input exceeds ..." at warn level
// (crates/slate-core/src/palette.rs) and decodes as empty — no vault or
// timing dependence.

using uniffi.slate_uniffi;

SlateUniffiMethods.InitHostLogging(@verbose: args.Contains("--verbose"));

byte[] oversized = new byte[(1 << 16) + 1];
string[] decoded = SlateUniffiMethods.PaletteRecentsDecode(oversized);
if (decoded.Length != 0)
{
    Console.Error.WriteLine("probe-error: oversized recents input decoded non-empty");
    return 1;
}

Console.WriteLine("host-log-probe: done");
return 0;
