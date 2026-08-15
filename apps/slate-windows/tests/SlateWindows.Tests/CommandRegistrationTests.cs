// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741): the registration bridge. One app-lifetime registry
/// registered once (P2, P17, PINV-3), a duplicate id is fatal (P2),
/// actions resolve live state through a provider rather than capturing a
/// workspace (P17), invocation is dispatcher-affine (P15), and the recents
/// store keeps the host's half of the contract (P11, PD-3).
/// </summary>
public sealed class CommandRegistrationTests
{
    /// <summary>
    /// Round 2 gate: a vault transition dismisses the palette (P14).
    /// </summary>
    /// <remarks>
    /// The round-1 fix had NO test — the verification round deleted both
    /// <c>Dismiss()</c> calls and the whole suite stayed green. Without
    /// them the palette stays open and modal across the async gap where
    /// <c>IsVaultOpen</c> is false, permanently if the new vault fails.
    /// </remarks>
    [Fact]
    public async Task AVaultTransitionDismissesThePalette()
    {
        using var fixture = FixtureVault.Create(1);
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")));

        await lifecycle.OpenVaultAsync(fixture.Root);
        Assert.True(lifecycle.IsVaultOpen);

        lifecycle.Palette.Open();
        Assert.True(lifecycle.Palette.IsOpen);

        lifecycle.CloseVault();

        Assert.False(lifecycle.IsVaultOpen);
        Assert.False(
            lifecycle.Palette.IsOpen,
            "closing the vault left the palette open — P14's forbidden state.");
    }

    /// <summary>
    /// Opening a DIFFERENT vault dismisses the palette too.
    /// </summary>
    /// <remarks>
    /// A sibling gap both the round-3 verifier and codex found
    /// independently: the close-path fact above left the open path
    /// entirely ungated, so deleting its dismissal stayed green. This is
    /// the reachable one — a second Slate launch runs close-then-open, and
    /// the palette stayed visible and modal across the whole async gap
    /// where IsVaultOpen is false.
    /// </remarks>
    [Fact]
    public async Task OpeningAnotherVaultAlsoDismissesThePalette()
    {
        using var first = FixtureVault.Create(1);
        using var second = FixtureVault.Create(1);
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(first.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(first.Root, "device-state", "recent-vaults.json")));

        await lifecycle.OpenVaultAsync(first.Root);
        lifecycle.Palette.Open();
        Assert.True(lifecycle.Palette.IsOpen);

        await lifecycle.OpenVaultAsync(second.Root);

        Assert.True(lifecycle.IsVaultOpen);
        Assert.False(
            lifecycle.Palette.IsOpen,
            "opening another vault left the palette open — it stays modal "
            + "across the gap where no vault is open, which P14 forbids.");
    }

    /// <summary>
    /// The PRODUCTION availability vocabulary, not a fake's copy of it.
    /// </summary>
    /// <remarks>
    /// The red team caught this: the palette's routing facts assert
    /// against a fake source seeded with the same three constants, so
    /// deleting a clause from the real predicate left every test green
    /// while the shipped behaviour changed. Those facts prove the palette
    /// ASKS the seam; this one proves the seam ANSWERS.
    /// </remarks>
    [Theory]
    [InlineData("NoVault", true)]
    [InlineData("Unavailable", true)]
    [InlineData("StructuralBusy", true)]
    [InlineData("Other", false)]
    public void IsAvailabilityRejection_AnswersForTheWholeVocabulary(
        string which, bool expected)
    {
        string message = which switch
        {
            "NoVault" => SlateCommandRegistrar.NoVaultReason,
            "Unavailable" => SlateCommandRegistrar.UnavailableReason,
            "StructuralBusy" => SlateCommandRegistrar.StructuralMutationBusyReason,
            _ => "The disk is full.",
        };

        Assert.Equal(expected, SlateCommandRegistrar.IsAvailabilityRejection(message));
    }

    /// <summary>
    /// A throwing <c>ICommand</c> becomes an <c>ActionFailed</c> the
    /// palette can announce, rather than escaping through the uniffi
    /// callback boundary as a non-<c>CommandException</c> and crashing the
    /// dispatcher.
    /// </summary>
    [Fact]
    public void AThrowingCommandSurfacesAsActionFailed()
    {
        var host = new FakeCommandHost
        {
            CloseVaultOverride = new StubCommand(
                () => throw new InvalidOperationException("the vault exploded"),
                () => true),
        };
        var action = new DispatcherCommandAction(
            Dispatcher.CurrentDispatcher, host, ChordTable.Ids.VaultClose);

        CommandException.ActionFailed failed =
            Assert.Throws<CommandException.ActionFailed>(action.Invoke);
        Assert.Equal("the vault exploded", failed.message);
    }

    [Fact]
    public void DuplicateId_IsAFatalRegistrationConflict()
    {
        // Contract P2: `Register` returning `true` means it REPLACED an id.
        // The Rust doc calls silent override of a slate.* id a
        // privilege-escalation footgun; the bridge fails fast at startup
        // rather than logging.
        var host = new FakeCommandHost();
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        using var registry = new CommandRegistry();

        SlateCommandRegistrar.RegisterAll(registry, host, dispatcher);

        CommandRegistrationConflictException conflict =
            Assert.Throws<CommandRegistrationConflictException>(
                () => SlateCommandRegistrar.RegisterAll(registry, host, dispatcher));
        Assert.Equal(ChordTable.RegisteredRows[0].Id, conflict.CommandId);
        Assert.Contains("already registered", conflict.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryHoldsExactlyTheDeclaredCatalog_BothDirections()
    {
        // Contract P13(a): the declared id catalog and the live registry hold
        // the same set, in both directions.
        var host = new FakeCommandHost();
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        string[] live = registry.List().Select(command => command.Id).OrderBy(
            id => id, System.StringComparer.Ordinal).ToArray();
        string[] declared = ChordTable.RegisteredRows.Select(row => row.Id).OrderBy(
            id => id, System.StringComparer.Ordinal).ToArray();
        Assert.Equal(declared, live);

        // Every registered id must also be resolvable, or its palette row is
        // a dead end no matter what the registry says (PINV-4).
        foreach (string id in declared)
        {
            Assert.Contains(id, SlateCommandRegistrar.ResolvableIds);
        }

        // And nothing resolvable is left unregistered.
        foreach (string id in SlateCommandRegistrar.ResolvableIds)
        {
            Assert.Contains(id, declared);
        }
    }

    [Fact]
    public void RegisteredCommandCarriesTheChordTablesHotkeySectionAndHint()
    {
        // PINV-5: the chord the palette displays comes from the table, not
        // from a second literal at the registration site.
        var host = new FakeCommandHost();
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        foreach (ChordTableEntry row in ChordTable.RegisteredRows)
        {
            Command? live = registry.FindById(row.Id);
            Assert.NotNull(live);
            Assert.Equal(row.Label, live!.Label);
            Assert.Equal(row.Hint, live.AccessibilityHint);
            Assert.Equal(row.WindowsChord, live.HotkeyHint);
            Assert.Equal(row.Section, live.Section);
        }

        Assert.Equal(
            "Ctrl+Shift+O",
            registry.FindById(ChordTable.Ids.VaultOpen)!.HotkeyHint);
        Assert.Null(registry.FindById(ChordTable.Ids.VaultClose)!.HotkeyHint);
    }

    [Fact]
    public void InvokeById_RunsTheLiveCommand_ThroughTheRegistry()
    {
        // PINV-4: the palette invokes through InvokeById, never by calling an
        // ICommand directly. This is the round trip that proves the action
        // adapter reaches the host's live command.
        var host = new FakeCommandHost();
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        registry.InvokeById(ChordTable.Ids.VaultOpen);
        Assert.Equal(1, host.OpenVaultInvocations);

        registry.InvokeById(ChordTable.Ids.VaultClose);
        Assert.Equal(1, host.OpenVaultInvocations);
        Assert.Equal(1, host.CloseVaultInvocations);
    }

    [Fact]
    public void UnknownId_ReachesTheRegistrysOwnUnknownIdOutcome()
    {
        // Contract P9: the availability gate must not shadow UnknownId, which
        // the palette renders as PaletteCommandNotFound.
        var host = new FakeCommandHost();
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        Assert.Null(SlateCommandRegistrar.DisabledReason(host, "slate.not.a.command"));
        CommandException.UnknownId unknown = Assert.Throws<CommandException.UnknownId>(
            () => registry.InvokeById("slate.not.a.command"));
        Assert.Equal("slate.not.a.command", unknown.id);
    }

    [Fact]
    public void ActionResolvesLiveState_RatherThanCapturingAWorkspace()
    {
        // Contract P17: commands are registered once at shell construction;
        // vault open and close must not mutate the registry. A workspace-
        // backed command therefore refuses while no workspace is mounted and
        // starts working when one appears — without re-registration.
        var host = new FakeCommandHost { IsVaultOpen = false };
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        Assert.Equal(
            SlateCommandRegistrar.NoVaultReason,
            SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.Save));
        CommandException.ActionFailed refused = Assert.Throws<CommandException.ActionFailed>(
            () => registry.InvokeById(ChordTable.Ids.Save));
        Assert.Equal(SlateCommandRegistrar.NoVaultReason, refused.message);

        // Same registry object, same registration — only the provider's live
        // state changed.
        host.IsVaultOpen = true;
        Assert.Equal(
            SlateCommandRegistrar.UnavailableReason,
            SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.Save));
    }

    [Fact]
    public void AvailabilityGate_RefusesWhenTheLiveCommandCannotExecute()
    {
        // Contract P8: ONE resolver serves the row state, the announcement,
        // and the Enter gate, re-evaluated at invoke time rather than trusted
        // from render time.
        var host = new FakeCommandHost { IsVaultOpen = true, CanOpenVault = false };
        using var registry = new CommandRegistry();
        SlateCommandRegistrar.RegisterAll(registry, host, Dispatcher.CurrentDispatcher);

        Assert.Equal(
            SlateCommandRegistrar.UnavailableReason,
            SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.VaultOpen));
        Assert.Throws<CommandException.ActionFailed>(
            () => registry.InvokeById(ChordTable.Ids.VaultOpen));
        Assert.Equal(0, host.OpenVaultInvocations);

        host.CanOpenVault = true;
        Assert.Null(SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.VaultOpen));
        registry.InvokeById(ChordTable.Ids.VaultOpen);
        Assert.Equal(1, host.OpenVaultInvocations);
    }

    [Fact]
    public void InvocationIsDispatcherAffine()
    {
        // Contract P15: InvokeById is a synchronous FFI call and
        // ForeignActionAdapter runs the foreign action on the CALLING thread,
        // so a background invocation would run UI code off the UI thread with
        // no marshalling at all. The adapter asserts instead.
        var host = new FakeCommandHost { IsVaultOpen = true };
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var action = new DispatcherCommandAction(
            dispatcher,
            host,
            ChordTable.Ids.VaultOpen);

        action.Invoke();
        Assert.Equal(1, host.OpenVaultInvocations);

        Exception? offThread = null;
        var worker = new System.Threading.Thread(() =>
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                offThread = exception;
            }
        });
        worker.Start();
        Assert.True(worker.Join(System.TimeSpan.FromSeconds(10)));

        var affinity = Assert.IsType<System.InvalidOperationException>(offThread);
        Assert.Contains("dispatcher-affine", affinity.Message, System.StringComparison.Ordinal);
        Assert.Equal(1, host.OpenVaultInvocations);
    }

    [Fact]
    public void PaletteCommandSource_SnapshotsListSidebarOrderAndVaultState()
    {
        var host = new FakeCommandHost { IsVaultOpen = true };
        string directory = NewTempDirectory();
        using var source = new PaletteCommandSource(
            host,
            Dispatcher.CurrentDispatcher,
            new CommandPaletteRecentsStore(Path.Combine(directory, "recents.json")));

        Command[] snapshot = source.ListCommands();
        Assert.Equal(ChordTable.RegisteredRows.Count, snapshot.Length);
        Assert.True(source.IsVaultOpen);
        Assert.Equal(ChordTable.SidebarPinnedOrder, source.SidebarPinnedOrder);

        source.Invoke(ChordTable.Ids.VaultOpen);
        Assert.Equal(1, host.OpenVaultInvocations);

        source.RecordInvocation(ChordTable.Ids.VaultOpen);
        Assert.Equal([ChordTable.Ids.VaultOpen], source.LoadRecents());
        Assert.Null(source.LastRecentsSaveError);
    }

    [Fact]
    public void Recents_RoundTripThroughCoresTransitionsAndFormat()
    {
        // Contract P11 + PD-3: the host owns the path and the I/O; core owns
        // every state transition. The LRU goes through PaletteRecentsAdd, not
        // a hand-rolled C# list operation.
        string path = Path.Combine(NewTempDirectory(), "nested", "recents.json");
        var store = new CommandPaletteRecentsStore(path);

        Assert.Empty(store.Load());

        string[] one = store.Add([], "slate.vault.open");
        Assert.Equal(["slate.vault.open"], one);
        Assert.Equal(one, store.Load());

        string[] two = store.Add(one, "slate.editor.save");
        Assert.Equal(["slate.editor.save", "slate.vault.open"], two);

        // Move-to-front, not append.
        string[] three = store.Add(two, "slate.vault.open");
        Assert.Equal(["slate.vault.open", "slate.editor.save"], three);
        Assert.Equal(three, store.Load());

        // Core's cap, not the host's.
        string[] many = [];
        for (int index = 0; index < CommandPaletteRecentsStore.MaxEntries + 5; index++)
        {
            many = store.Add(many, $"slate.test.command{index}");
        }

        Assert.Equal(CommandPaletteRecentsStore.MaxEntries, many.Length);
        Assert.Equal(CommandPaletteRecentsStore.MaxEntries, store.Load().Length);

        // Core's byte format, verbatim.
        Assert.Equal(
            SlateUniffiMethods.PaletteRecentsEncode(many),
            File.ReadAllBytes(path));
    }

    [Fact]
    public void Recents_BoundedReadAcceptsExactlyTheCapAndRefusesOneMore()
    {
        // Contract P11: the guard is `>`. A file of exactly 65536 bytes is
        // ACCEPTED; 65537 is refused BEFORE decoding, even when its JSON is
        // perfectly valid — which the padded payload below proves it is.
        string directory = NewTempDirectory();
        string atCap = Path.Combine(directory, "at-cap.json");
        string overCap = Path.Combine(directory, "over-cap.json");

        byte[] payload = SlateUniffiMethods.PaletteRecentsEncode(["slate.vault.open"]);
        File.WriteAllBytes(atCap, Padded(payload, CommandPaletteRecentsStore.MaxFileBytes));
        File.WriteAllBytes(overCap, Padded(payload, CommandPaletteRecentsStore.MaxFileBytes + 1));

        Assert.Equal(
            CommandPaletteRecentsStore.MaxFileBytes,
            new FileInfo(atCap).Length);
        Assert.Equal(
            CommandPaletteRecentsStore.MaxFileBytes + 1,
            new FileInfo(overCap).Length);

        Assert.Equal(
            ["slate.vault.open"],
            new CommandPaletteRecentsStore(atCap).Load());
        Assert.Empty(new CommandPaletteRecentsStore(overCap).Load());
    }

    [Fact]
    public void Recents_MalformedOrMissingFileNeverBlocksThePalette()
    {
        string directory = NewTempDirectory();
        string garbage = Path.Combine(directory, "garbage.json");
        File.WriteAllText(garbage, "{ not an array");
        Assert.Empty(new CommandPaletteRecentsStore(garbage).Load());

        string missing = Path.Combine(directory, "nope", "recents.json");
        Assert.Empty(new CommandPaletteRecentsStore(missing).Load());
    }

    [Fact]
    public void Recents_PersistenceFailureIsNonFatalAndTheListStillMoves()
    {
        // Contract P11: persistence failure is non-fatal — the in-memory list
        // still moves, so the open palette stays consistent with what the
        // user just did. Non-fatal is not the same as invisible.
        string directory = NewTempDirectory();
        string blocker = Path.Combine(directory, "blocker");
        File.WriteAllText(blocker, "not a directory");

        var store = new CommandPaletteRecentsStore(Path.Combine(blocker, "recents.json"));
        string[] updated = store.Add([], "slate.vault.open");

        Assert.Equal(["slate.vault.open"], updated);
        Assert.NotNull(store.LastSaveError);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Recents_AtomicWriteLeavesNoTemporaryFileBehind()
    {
        string directory = NewTempDirectory();
        var store = new CommandPaletteRecentsStore(Path.Combine(directory, "recents.json"));
        Assert.True(store.TrySave(["slate.vault.open"]));
        Assert.True(store.TrySave(["slate.editor.save"]));

        Assert.Equal(
            ["recents.json"],
            Directory.GetFiles(directory).Select(path => Path.GetFileName(path)!).ToArray());
    }

    [Fact]
    public void DefaultRecentsPath_IsTheGlobalDeviceLocalLocation()
    {
        // Global, device-local, never per-vault.
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Slate",
            "command-palette-recents.json");
        Assert.Equal(expected, CommandPaletteRecentsStore.DefaultFilePath);
    }

    [Fact]
    public void CommandStateRefreshEnumeratesTheRegistrationTable()
    {
        // PINV-7: a new command cannot be silently omitted from a
        // hand-maintained RaiseCommandStates list, because there is no list.
        var host = new FakeCommandHost();
        SlateCommandRegistrar.RaiseCommandStates(host);

        var relay = new RelayCommand(_ => { }, _ => true);
        host.OpenVaultRelay = relay;
        int raised = 0;
        relay.CanExecuteChanged += (_, _) => raised++;

        SlateCommandRegistrar.RaiseCommandStates(host);
        Assert.Equal(1, raised);
    }

    private static byte[] Padded(byte[] payload, int totalBytes)
    {
        // Trailing whitespace is insignificant to a JSON array, so the padded
        // payload stays valid at both sizes. That is what makes the 65537
        // refusal a size refusal rather than a parse failure.
        var builder = new List<byte>(totalBytes);
        builder.AddRange(payload);
        while (builder.Count < totalBytes)
        {
            builder.Add((byte)' ');
        }

        return [.. builder];
    }

    private static string NewTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "slate-w51-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeCommandHost : ISlateCommandHost
    {
        public WorkspaceViewModel? Workspace => null;

        public FilesSidebarViewModel? FileSidebar => null;

        public QuickSwitcherViewModel? QuickSwitcher => null;

        public bool IsVaultOpen { get; set; } = true;

        public bool CanOpenVault { get; set; } = true;

        public int OpenVaultInvocations { get; private set; }

        public int CloseVaultInvocations { get; private set; }

        /// <summary>When set, OpenVaultCommand resolves to this instead — the
        /// PINV-7 enumeration fact needs a RelayCommand to observe.</summary>
        public RelayCommand? OpenVaultRelay { get; set; }

        public ICommand OpenVaultCommand => OpenVaultRelay is { } relay
            ? relay
            : new StubCommand(() => OpenVaultInvocations++, () => CanOpenVault);

        /// <summary>When set, CloseVaultCommand resolves to this instead —
        /// the throwing-command fact needs an ICommand that faults.</summary>
        public ICommand? CloseVaultOverride { get; set; }

        public ICommand CloseVaultCommand =>
            CloseVaultOverride
            ?? new StubCommand(() => CloseVaultInvocations++, () => true);
    }

    private sealed class StubCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public StubCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => _canExecute();

        public void Execute(object? parameter) => _execute();
    }
}
