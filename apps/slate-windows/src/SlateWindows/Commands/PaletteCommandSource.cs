// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Commands;

/// <summary>
/// The production <see cref="IPaletteCommandSource"/>: one app-lifetime
/// <c>CommandRegistry</c>, one availability resolver, one recents store.
/// </summary>
/// <remarks>
/// <para>
/// Constructing this type registers the whole catalog exactly once
/// (contract P17). It owns the registry for the shell's lifetime — there is
/// no second instance (PINV-3) — and vault open and close never mutate it,
/// because actions resolve live state through <see cref="ISlateCommandHost"/>
/// rather than capturing a workspace. A registry holding vault-scoped
/// commands with no vault open is a reachable but harmless state: those
/// commands refuse through the availability gate, and the palette itself
/// refuses to open without a vault.
/// </para>
/// </remarks>
internal sealed class PaletteCommandSource : IPaletteCommandSource, IDisposable
{
    private readonly CommandRegistry _registry;
    private readonly ISlateCommandHost _host;
    private readonly CommandPaletteRecentsStore _recentsStore;
    private readonly Dispatcher _dispatcher;
    private readonly bool _ownsRegistry;
    private string[] _recents;
    private bool _recentsLoaded;

    public PaletteCommandSource(
        ISlateCommandHost host,
        Dispatcher dispatcher,
        CommandPaletteRecentsStore? recentsStore = null,
        CommandRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _host = host;
        _dispatcher = dispatcher;
        _recentsStore = recentsStore ?? new CommandPaletteRecentsStore();
        _ownsRegistry = registry is null;
        _registry = registry ?? new CommandRegistry();
        _recents = [];
        SlateCommandRegistrar.RegisterAll(_registry, host, dispatcher);
    }

    /// <summary>The one registry. Exposed so the shell can requery command
    /// state and the drift tests can read what the app actually holds.</summary>
    public CommandRegistry Registry => _registry;

    /// <inheritdoc />
    public Command[] ListCommands() => _registry.List();

    /// <inheritdoc />
    public string[] SidebarPinnedOrder => ChordTable.SidebarPinnedOrder;

    /// <inheritdoc />
    public string? DisabledReason(string commandId) =>
        SlateCommandRegistrar.DisabledReason(_host, commandId);

    /// <inheritdoc />
    public void Invoke(string commandId)
    {
        // Contract P15. The adapter asserts this too, but asserting at the
        // seam as well means a background caller fails here — with a usable
        // stack — rather than inside the FFI callback where the exception
        // becomes an opaque UNEXPECTED_ERROR.
        if (!_dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                $"Command '{commandId}' was invoked from thread "
                + $"{Environment.CurrentManagedThreadId}, not the dispatcher thread. "
                + "Command invocation is dispatcher-affine (contract P15).");
        }

        _registry.InvokeById(commandId);
    }

    /// <inheritdoc />
    public string[] LoadRecents()
    {
        if (!_recentsLoaded)
        {
            _recents = _recentsStore.Load();
            _recentsLoaded = true;
        }

        return _recents;
    }

    /// <inheritdoc />
    public void RecordInvocation(string commandId)
    {
        // The in-memory list moves even when the write fails, so the open
        // palette stays consistent with what the user just did.
        _recents = _recentsStore.Add(LoadRecents(), commandId);
    }

    /// <inheritdoc />
    public bool IsVaultOpen => _host.IsVaultOpen;

    /// <inheritdoc />
    public bool IsAvailabilityRejection(string message) =>
        SlateCommandRegistrar.IsAvailabilityRejection(message);

    /// <summary>The last recents-persistence failure, or
    /// <see langword="null"/>. Non-fatal, but not invisible.</summary>
    public Exception? LastRecentsSaveError => _recentsStore.LastSaveError;

    /// <summary>Releases the registry only when this instance created it —
    /// an injected registry belongs to its caller.</summary>
    public void Dispose()
    {
        if (_ownsRegistry)
        {
            _registry.Dispose();
        }
    }
}
