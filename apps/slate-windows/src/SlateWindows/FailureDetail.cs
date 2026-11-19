// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>#1890: the detail a failure sentence speaks: core's rendering
/// for a vault error, never the binding's field dump ("@message=..."), and
/// any other exception's own message. The one exempt reader of an
/// exception's Message in the shell census (ShellFailureDetailCensus); the
/// reads that stood before it are that census's dated rows.</summary>
/// <remarks>Limits: CommandException, GraphConfigException and
/// HotExitException dump their fields too, and core renders none of them;
/// each is read by its typed fields where it is spoken. An AggregateException
/// that holds a vault error embeds that error's dump in its own Message.
/// Neither reaches a caller of this helper on today's code.</remarks>
internal static class FailureDetail
{
    internal static string Of(Exception failure) =>
        failure is VaultException vault ? SlateUniffiMethods.VaultErrorDetail(vault) : failure.Message;
}
