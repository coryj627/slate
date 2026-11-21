// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>#1890: the detail a failure sentence speaks: core's rendering
/// for a vault error, never the binding's field dump ("@message=..."), and
/// any other exception's own message. The one exempt reader of an
/// exception's Message in the shell census
/// (ShellFailureDetailCensus).</summary>
/// <remarks>Core renders only the vault error. The binding's other error
/// kinds dump their fields in Message too, so each is read here by its
/// typed fields, in the words its own surface says: a CommandException as
/// its action's message or the unknown id, a GraphConfigException through
/// GraphConfigStore.Reason, and a HotExitException through
/// HotExitStoreHost.ReasonOf. An AggregateException says each inner
/// exception's detail, joined by a space, so a vault error inside one is
/// rendered too; one with no inner exception says its own message. None of
/// them can put a field dump into text through this helper. A
/// PanicException or InternalException reaches the broad catches with the
/// panic's own text, which is not a field dump, and the helper returns it
/// as it is.</remarks>
internal static class FailureDetail
{
    internal static string Of(Exception failure) => failure switch
    {
        VaultException vault => SlateUniffiMethods.VaultErrorDetail(vault),
        CommandException.ActionFailed failed => failed.@message,
        CommandException.UnknownId unknown => $"Unknown command {unknown.@id}",
        GraphConfigException graphConfig => GraphConfigStore.Reason(graphConfig),
        HotExitException hotExit => HotExitStoreHost.ReasonOf(hotExit),
        AggregateException { InnerExceptions.Count: > 0 } aggregate =>
            string.Join(" ", aggregate.InnerExceptions.Select(Of)),
        _ => failure.Message,
    };
}
