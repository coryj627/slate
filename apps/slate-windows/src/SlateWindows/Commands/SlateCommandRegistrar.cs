// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Commands;

/// <summary>
/// The live-state seam every registered command action resolves through
/// (contract P17).
/// </summary>
/// <remarks>
/// <para>
/// Commands are registered <b>once</b>, at shell construction, and never
/// re-registered. An action therefore may not capture a
/// <c>WorkspaceViewModel</c> — vault open and close replace those objects
/// wholesale. It asks this provider for the live one at invoke time
/// instead, which is what keeps contract P2's "a <c>true</c> return is a
/// fatal conflict" rule meaningful: re-registering per vault would make
/// replacement the normal case.
/// </para>
/// <para>
/// <c>VaultLifecycleViewModel</c> already exposes exactly this surface, so
/// the shell implements it by declaring the interface — no new state.
/// </para>
/// </remarks>
internal interface ISlateCommandHost
{
    WorkspaceViewModel? Workspace { get; }

    FilesSidebarViewModel? FileSidebar { get; }

    QuickSwitcherViewModel? QuickSwitcher { get; }

    ICommand OpenVaultCommand { get; }

    ICommand CloseVaultCommand { get; }

    /// <summary>
    /// W5-2 close-out (#742): toggles the vault-search overlay.
    /// Deliberately UNGUARDED — mac's palette action calls
    /// <c>toggleSearchOverlay()</c> with no modal gate
    /// (<c>SlateCommands.swift:1483-1494</c>), and the palette invokes
    /// before dismissing (P9), so a modal-decision-aware guard would
    /// refuse every palette invocation. The modal gate stays on the
    /// chord path (<c>MainWindow.Window_PreviewKeyDown</c>).
    /// </summary>
    ICommand ToggleSearchCommand { get; }

    bool IsVaultOpen { get; }
}

/// <summary>
/// Raised when <see cref="CommandRegistry.Register"/> reports that it
/// replaced an existing id (contract P2).
/// </summary>
/// <remarks>
/// The Rust doc calls silent override of a <c>slate.*</c> id a
/// privilege-escalation footgun and requires the caller to reject
/// conflicts at the registration site. Fail fast at startup — not a log
/// line — so a duplicate can never reach a user.
/// </remarks>
internal sealed class CommandRegistrationConflictException : InvalidOperationException
{
    public CommandRegistrationConflictException(string commandId)
        : base($"Command id '{commandId}' was already registered. "
            + "Registration replaced it, which the bridge treats as fatal.")
    {
        CommandId = commandId;
    }

    public string CommandId { get; }
}

/// <summary>
/// The <c>CommandAction</c> adapter. Dispatcher-affine by contract P15.
/// </summary>
/// <remarks>
/// <para>
/// <c>InvokeById</c> is a synchronous FFI call and Rust's
/// <c>ForeignActionAdapter</c> runs the foreign action on the calling
/// thread, so an action invoked from the UI thread runs on the UI thread
/// with no marshalling. The registry is therefore only ever invoked from
/// the dispatcher thread, and this adapter asserts it. That is deliberately
/// stricter than the <c>enqueueUi</c> pattern the scan and vault listeners
/// use — those receive events from Rust-owned background threads, whereas
/// command invocation is always user-initiated from a UI surface. A future
/// background caller marshals to the dispatcher first; the registry does
/// not acquire an <c>enqueueUi</c> seam speculatively.
/// </para>
/// </remarks>
internal sealed class DispatcherCommandAction : CommandAction
{
    private readonly Dispatcher _dispatcher;
    private readonly ISlateCommandHost _host;
    private readonly string _commandId;

    public DispatcherCommandAction(
        Dispatcher dispatcher,
        ISlateCommandHost host,
        string commandId)
    {
        _dispatcher = dispatcher;
        _host = host;
        _commandId = commandId;
    }

    /// <exception cref="InvalidOperationException">
    /// The caller is not on the dispatcher thread. A host bug, not a command
    /// failure — it must not be swallowed into a spoken
    /// <c>PaletteCommandFailed</c>.
    /// </exception>
    /// <exception cref="CommandException.ActionFailed">
    /// The command is unavailable, or its <c>ICommand</c> threw.
    /// </exception>
    public void Invoke()
    {
        if (!_dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                $"Command '{_commandId}' was invoked from thread "
                + $"{Environment.CurrentManagedThreadId}, not the dispatcher thread. "
                + "Command invocation is dispatcher-affine (contract P15); marshal "
                + "to the dispatcher before calling InvokeById.");
        }

        // Re-evaluate availability at invoke time rather than trusting what
        // the palette rendered (contract P8). One resolver serves the row
        // state, the selection announcement, and this gate — and the command
        // is resolved ONCE, so the instance the gate tested is the instance
        // that runs. Every resolver now also returns a STABLE instance, so
        // that guarantee no longer rests on resolving once.
        ICommand? command = SlateCommandRegistrar.Resolve(_host, _commandId);
        string? disabled = SlateCommandRegistrar.DisabledReason(_host, _commandId, command);
        if (disabled is not null)
        {
            throw new CommandException.ActionFailed(disabled);
        }

        try
        {
            command!.Execute(null);
        }
        catch (Exception exception) when (exception is not CommandException)
        {
            // Without this the exception escapes into the uniffi callback
            // boundary, comes back as PanicException or InternalException —
            // neither of which is a CommandException — and blows past the
            // palette's catch chain onto the dispatcher. The palette would
            // crash the app instead of announcing a failure and staying
            // open (P9/P10). It also makes this method's own documented
            // contract true.
            throw new CommandException.ActionFailed(exception.Message);
        }
    }
}

/// <summary>
/// The registration bridge: it turns <see cref="ChordTable"/>'s declared
/// catalog into the one app-lifetime <c>CommandRegistry</c> the palette
/// ranks (contracts P2, P3, P17).
/// </summary>
internal static class SlateCommandRegistrar
{
    // W0.5-3 residue: Windows palette availability copy. Two strings, both
    // rendered verbatim by `PaletteCommandUnavailable` (contract P10 — the
    // host never prefixes them).
    internal const string NoVaultReason = "Open a vault to use this command.";
    internal const string UnavailableReason = "This command is not available right now.";

    /// <summary>
    /// Mac's structural-mutation refusal, byte-identical to
    /// <c>AppState.structuralMutationBusyReason</c>. Windows has no
    /// structural-mutation gate of its own yet, so nothing here emits it —
    /// it is listed as an availability reason so that a core-side or
    /// future host-side refusal carrying this exact text is announced as
    /// a rejection rather than as "{label} failed: {reason}".
    /// </summary>
    internal const string StructuralMutationBusyReason =
        "Wait for the current file operation to finish.";

    /// <summary>
    /// Whether a failed action's message is an availability rejection
    /// rather than an operation failure (contract P10). The palette asks
    /// through <see cref="IPaletteCommandSource.IsAvailabilityRejection"/>
    /// so the vocabulary has exactly one owner.
    /// </summary>
    internal static bool IsAvailabilityRejection(string message) =>
        string.Equals(message, NoVaultReason, StringComparison.Ordinal)
        || string.Equals(message, UnavailableReason, StringComparison.Ordinal)
        || string.Equals(message, StructuralMutationBusyReason, StringComparison.Ordinal);

    /// <summary>The viewport verbs' BINDING RECORD (§D D14, obligation
    /// ID-8): one enumerable authority holding row id, the navigator
    /// member that delivers it, and the resolver — registration,
    /// resolution and the census all derive from THIS, so a row cannot
    /// register without a delivery member and cannot deliver except
    /// through the member its row names. Declared ABOVE the resolver
    /// map because static fields initialize in declaration order, and
    /// the map's builder consumes this record.</summary>
    internal static readonly
        (string Id, string NavigatorMember, Func<ISlateCommandHost, ICommand?> Resolve)[]
        CanvasViewportBindings =
        [
            (ChordTable.Ids.CanvasZoomIn, "ZoomIn",
                host => host.Workspace?.CanvasZoomInCommand),
            (ChordTable.Ids.CanvasZoomOut, "ZoomOut",
                host => host.Workspace?.CanvasZoomOutCommand),
            (ChordTable.Ids.CanvasActualSize, "ActualSize",
                host => host.Workspace?.CanvasActualSizeCommand),
            (ChordTable.Ids.CanvasFitCanvas, "FitCanvas",
                host => host.Workspace?.CanvasFitCanvasCommand),
            (ChordTable.Ids.CanvasZoomToSelection, "ZoomToSelection",
                host => host.Workspace?.CanvasZoomToSelectionCommand),
        ];

    private static readonly Dictionary<string, Func<ISlateCommandHost, ICommand?>> Resolvers =
        BuildResolvers();

    /// <summary>
    /// Register every catalog row marked as registered, exactly once.
    /// </summary>
    /// <exception cref="CommandRegistrationConflictException">
    /// An id was already present. Fatal by contract P2.
    /// </exception>
    public static void RegisterAll(
        CommandRegistry registry,
        ISlateCommandHost host,
        Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(dispatcher);

        foreach (ChordTableEntry row in ChordTable.RegisteredRows)
        {
            bool replaced = registry.Register(
                new Command(
                    Id: row.Id,
                    Label: row.Label,
                    AccessibilityHint: row.Hint,
                    HotkeyHint: row.WindowsChord,
                    Section: row.Section),
                new DispatcherCommandAction(dispatcher, host, row.Id));
            if (replaced)
            {
                throw new CommandRegistrationConflictException(row.Id);
            }
        }
    }

    /// <summary>
    /// The live <see cref="ICommand"/> behind a registered id, or
    /// <see langword="null"/> when its owning surface is not mounted.
    /// </summary>
    public static ICommand? Resolve(ISlateCommandHost host, string commandId)
    {
        ArgumentNullException.ThrowIfNull(host);
        return Resolvers.TryGetValue(commandId, out Func<ISlateCommandHost, ICommand?>? resolver)
            ? resolver(host)
            : null;
    }

    /// <summary>
    /// The single availability resolver (contract P8): it serves the palette
    /// row state, the selection announcement, and the Enter gate, so those
    /// three cannot disagree. <see langword="null"/> means the command can
    /// run right now.
    /// </summary>
    /// <remarks>
    /// An id the catalog does not register returns <see langword="null"/>
    /// deliberately: the gate must not shadow the registry's own
    /// <c>UnknownId</c> outcome, which the palette renders as
    /// <c>PaletteCommandNotFound</c> (contract P9).
    /// </remarks>
    public static string? DisabledReason(ISlateCommandHost host, string commandId) =>
        DisabledReason(host, commandId, Resolve(host, commandId));

    /// <summary>
    /// The same resolver over an already-resolved command, so a caller that
    /// is about to invoke tests the exact instance it will run.
    /// </summary>
    internal static string? DisabledReason(
        ISlateCommandHost host,
        string commandId,
        ICommand? command)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!Resolvers.ContainsKey(commandId))
        {
            return null;
        }

        if (command is null)
        {
            return host.IsVaultOpen ? UnavailableReason : NoVaultReason;
        }

        return command.CanExecute(null) ? null : UnavailableReason;
    }

    /// <summary>
    /// Requery every registered command's enabled state by <b>enumerating
    /// the registration table</b> (invariant PINV-7).
    /// </summary>
    /// <remarks>
    /// The four hand-maintained <c>RaiseCommandStates</c> lists are a
    /// verified drift surface — <c>ToggleReadingModeCommand</c> gates on
    /// <c>ActiveTab?.IsMarkdown</c> and is omitted from its list today.
    /// Enumerating the table means a newly registered command cannot be
    /// silently left out.
    /// </remarks>
    public static void RaiseCommandStates(ISlateCommandHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        foreach (ChordTableEntry row in ChordTable.RegisteredRows)
        {
            switch (Resolve(host, row.Id))
            {
                case RelayCommand relay:
                    relay.RaiseCanExecuteChanged();
                    break;
                case AsyncRelayCommand asyncRelay:
                    asyncRelay.RaiseCanExecuteChanged();
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Ids the bridge knows how to resolve. Exposed so the
    /// registration-forward drift test can assert both directions.</summary>
    public static IReadOnlyCollection<string> ResolvableIds => Resolvers.Keys;

    /// <summary>
    /// The nine sidebar shortcut slots have no <see cref="ICommand"/> of
    /// their own on the sidebar view model, so the bridge supplies one —
    /// and must supply the SAME one every time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A fresh adapter per resolve made PINV-7 quietly false for these
    /// nine: the enumerating refresh raised <c>CanExecuteChanged</c> on a
    /// throwaway object with no subscribers, so a requery could never
    /// reach whatever had bound to it. Benign while the predicate is
    /// constant, and silently wrong the moment it is not.
    /// </para>
    /// <para>
    /// Keyed on the sidebar instance through a
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/> so the adapters die
    /// with the vault that owns them — the sidebar is replaced wholesale
    /// on every vault open, and a static cache would pin the old one.
    /// </para>
    /// </remarks>
    private static ICommand ShortcutSlotCommand(FilesSidebarViewModel sidebar, int slot)
    {
        ICommand[] slots = ShortcutSlotCommands.GetValue(
            sidebar,
            owner =>
            {
                var built = new ICommand[9];
                for (int index = 0; index < built.Length; index++)
                {
                    int captured = index + 1;
                    built[index] = new RelayCommand(
                        _ => owner.OpenShortcut(captured), _ => true);
                }

                return built;
            });

        return slots[slot - 1];
    }

    private static readonly ConditionalWeakTable<FilesSidebarViewModel, ICommand[]>
        ShortcutSlotCommands = new();

    private static Dictionary<string, Func<ISlateCommandHost, ICommand?>> BuildResolvers()
    {
        var map = new Dictionary<string, Func<ISlateCommandHost, ICommand?>>(StringComparer.Ordinal)
        {
            // Vault lifecycle.
            [ChordTable.Ids.VaultOpen] = host => host.OpenVaultCommand,
            [ChordTable.Ids.VaultClose] = host => host.CloseVaultCommand,

            // Navigation.
            [ChordTable.Ids.QuickOpen] = host => host.QuickSwitcher?.OpenCommand,
            [ChordTable.Ids.JumpToBibliography] =
                host => host.Workspace?.JumpToBibliographyCommand,

            // Workspace tabs and panes.
            [ChordTable.Ids.NewTab] = host => host.Workspace?.DuplicateTabCommand,
            [ChordTable.Ids.CloseTab] = host => host.Workspace?.CloseActiveTabCommand,
            [ChordTable.Ids.ReopenClosedTab] = host => host.Workspace?.ReopenClosedTabCommand,
            [ChordTable.Ids.NextTab] = host => host.Workspace?.NextTabCommand,
            [ChordTable.Ids.PreviousTab] = host => host.Workspace?.PreviousTabCommand,
            [ChordTable.Ids.MoveTabLeft] = host => host.Workspace?.MoveTabLeftCommand,
            [ChordTable.Ids.MoveTabRight] = host => host.Workspace?.MoveTabRightCommand,
            [ChordTable.Ids.SplitRight] = host => host.Workspace?.SplitRightCommand,
            [ChordTable.Ids.SplitDown] = host => host.Workspace?.SplitDownCommand,
            [ChordTable.Ids.FocusPaneLeft] = host => host.Workspace?.FocusPaneLeftCommand,
            [ChordTable.Ids.FocusPaneRight] = host => host.Workspace?.FocusPaneRightCommand,
            [ChordTable.Ids.FocusPaneAbove] = host => host.Workspace?.FocusPaneAboveCommand,
            [ChordTable.Ids.FocusPaneBelow] = host => host.Workspace?.FocusPaneBelowCommand,
            [ChordTable.Ids.FocusNextPane] = host => host.Workspace?.FocusNextPaneCommand,
            [ChordTable.Ids.FocusPreviousPane] =
                host => host.Workspace?.FocusPreviousPaneCommand,
            [ChordTable.Ids.GrowPane] = host => host.Workspace?.GrowPaneCommand,
            [ChordTable.Ids.ShrinkPane] = host => host.Workspace?.ShrinkPaneCommand,
            [ChordTable.Ids.ClosePane] = host => host.Workspace?.ClosePaneCommand,
            [ChordTable.Ids.OpenInNewTab] = host => host.FileSidebar?.OpenNewTabCommand,
            [ChordTable.Ids.OpenInSplit] = host => host.FileSidebar?.OpenSplitCommand,
            [ChordTable.Ids.ToggleRightPane] = host => host.Workspace?.ToggleRightPaneCommand,
            // W5-2 close-out (#742): lives on the HOST, not the
            // workspace — the overlay survives vault open/close (P17)
            // and mac's action is likewise vault-lifetime-free.
            [ChordTable.Ids.ToggleSearch] = host => host.ToggleSearchCommand,
            [ChordTable.Ids.ShowHistoryPanel] = host => host.Workspace?.ShowHistoryPanelCommand,
            [ChordTable.Ids.RefreshSyncDiagnostics] =
                host => host.Workspace?.RefreshSyncDiagnosticsCommand,

            // Editor.
            [ChordTable.Ids.Save] = host => host.Workspace?.SaveActiveCommand,
            [ChordTable.Ids.ToggleViewMode] = host => host.Workspace?.ToggleReadingModeCommand,
            [ChordTable.Ids.CitationSummary] =
                host => host.Workspace?.OpenCitationSummaryCommand,
            [ChordTable.Ids.AddProperty] = host => host.Workspace?.OpenAddPropertySheetCommand,
            [ChordTable.Ids.BulkRenameProperties] =
                host => host.Workspace?.OpenBulkRenameSheetCommand,
            [ChordTable.Ids.EditorZoomIn] =
                host => host.Workspace?.EditorPreferences.ZoomInCommand,
            [ChordTable.Ids.EditorZoomOut] =
                host => host.Workspace?.EditorPreferences.ZoomOutCommand,
            [ChordTable.Ids.EditorActualSize] =
                host => host.Workspace?.EditorPreferences.ActualSizeCommand,
            [ChordTable.Ids.ToggleSpellCheck] =
                host => host.Workspace?.EditorPreferences.ToggleSpellCheckCommand,
            [ChordTable.Ids.ToggleReadingLinkTarget] =
                host => host.Workspace?.EditorPreferences.ToggleReadingLinkTargetCommand,
            [ChordTable.Ids.ToggleHistoryChangesSinceOpen] =
                host => host.Workspace?.EditorPreferences
                    .ToggleHistoryChangesSinceOpenCommand,
            [ChordTable.Ids.ReloadBibliography] =
                host => host.Workspace?.ReloadBibliographyCommand,
            [ChordTable.Ids.ActivateAtCaret] =
                host => host.Workspace?.ActiveGroup.ActiveTab?.EditorInteractions
                    ?.ActivateAtCaretCommand,
            [ChordTable.Ids.PreviewEmbed] =
                host => host.Workspace?.ActiveGroup.ActiveTab?.EditorInteractions
                    ?.PreviewEmbedCommand,

            // Tasks.
            [ChordTable.Ids.TasksReview] = host => host.Workspace?.OpenTasksReviewCommand,

            // Bases.
            [ChordTable.Ids.BasesOpenViewSwitcher] =
                host => host.Workspace?.BasesViewSwitcherCommand,
            [ChordTable.Ids.BasesNextView] = host => host.Workspace?.BasesNextViewCommand,
            [ChordTable.Ids.BasesPreviousView] =
                host => host.Workspace?.BasesPreviousViewCommand,
            [ChordTable.Ids.BasesSortByColumn] =
                host => host.Workspace?.BasesSortByColumnCommand,
            [ChordTable.Ids.BasesSaveSortToView] =
                host => host.Workspace?.BasesSaveSortToViewCommand,
            [ChordTable.Ids.BasesViewAsTable] = host => host.Workspace?.BasesViewAsTableCommand,
            [ChordTable.Ids.BasesViewAsList] = host => host.Workspace?.BasesViewAsListCommand,
            [ChordTable.Ids.BasesQuickFilter] = host => host.Workspace?.BasesQuickFilterCommand,
            [ChordTable.Ids.BasesWhereAmI] = host => host.Workspace?.BasesWhereAmICommand,
            [ChordTable.Ids.BasesOpenRow] = host => host.Workspace?.BasesOpenRowCommand,
            [ChordTable.Ids.BasesCopyLink] = host => host.Workspace?.BasesCopyLinkCommand,
            [ChordTable.Ids.BasesShowBacklinks] =
                host => host.Workspace?.BasesShowBacklinksCommand,
            [ChordTable.Ids.BasesEditProperty] =
                host => host.Workspace?.BasesEditPropertyCommand,
            [ChordTable.Ids.BasesExportCsv] = host => host.Workspace?.BasesExportCsvCommand,
            [ChordTable.Ids.BasesExportMarkdown] =
                host => host.Workspace?.BasesExportMarkdownCommand,
            [ChordTable.Ids.BasesCopyMarkdown] =
                host => host.Workspace?.BasesCopyMarkdownCommand,
            [ChordTable.Ids.BasesResultsPopover] = host => host.Workspace?.BasesResultsCommand,
            [ChordTable.Ids.BasesRefresh] = host => host.Workspace?.BasesRefreshCommand,

            // Canvas (W6-1 #745, contract A18). All three surface rows
            // are live: outline (PR A), table (PR B), visual (§D TD-6).
            [ChordTable.Ids.CanvasShowOutline] =
                host => host.Workspace?.CanvasShowOutlineCommand,
            [ChordTable.Ids.CanvasShowTable] =
                host => host.Workspace?.CanvasShowTableCommand,
            [ChordTable.Ids.CanvasShowVisual] =
                host => host.Workspace?.CanvasShowVisualCommand,

            // Canvas navigator, filter, Where-am-I and modes (W6-1 PR C,
            // contract C1). Every movement row stays enabled on a canvas
            // in ANY load state: the navigator's state mapping is what
            // answers, and a disabled row cannot say why (contract C4).
            [ChordTable.Ids.CanvasWhereAmI] = host => host.Workspace?.CanvasWhereAmICommand,
            [ChordTable.Ids.CanvasNextCard] = host => host.Workspace?.CanvasNextCardCommand,
            [ChordTable.Ids.CanvasPreviousCard] =
                host => host.Workspace?.CanvasPreviousCardCommand,
            [ChordTable.Ids.CanvasEnterGroup] =
                host => host.Workspace?.CanvasEnterGroupCommand,
            [ChordTable.Ids.CanvasExitGroup] = host => host.Workspace?.CanvasExitGroupCommand,
            [ChordTable.Ids.CanvasFollowConnectionForward] =
                host => host.Workspace?.CanvasFollowConnectionForwardCommand,
            [ChordTable.Ids.CanvasFollowConnectionBack] =
                host => host.Workspace?.CanvasFollowConnectionBackCommand,
            [ChordTable.Ids.CanvasTracePath] = host => host.Workspace?.CanvasTracePathCommand,
            [ChordTable.Ids.CanvasFilterCards] =
                host => host.Workspace?.CanvasFilterCardsCommand,
            [ChordTable.Ids.CanvasClearFilter] =
                host => host.Workspace?.CanvasClearFilterCommand,
            [ChordTable.Ids.CanvasCommitMode] =
                host => host.Workspace?.CanvasCommitModeCommand,
            [ChordTable.Ids.CanvasCancelMode] =
                host => host.Workspace?.CanvasCancelModeCommand,
            [ChordTable.Ids.CanvasToggleFollowSelection] =
                host => host.Workspace?.CanvasToggleFollowSelectionCommand,
            [ChordTable.Ids.BasesNewQuery] = host => host.Workspace?.BasesNewQueryCommand,
            [ChordTable.Ids.BasesEditViewFilters] =
                host => host.Workspace?.BasesEditViewFiltersCommand,

            // Sidebar / file management.
            [ChordTable.Ids.SidebarOpen] = host => host.FileSidebar?.OpenCurrentCommand,
            [ChordTable.Ids.NewNote] = host => host.FileSidebar?.CreateNoteCommand,
            [ChordTable.Ids.NewFromTemplate] =
                host => host.Workspace?.NewFromTemplateCommand,
            [ChordTable.Ids.NewFolder] = host => host.FileSidebar?.CreateFolderCommand,
            [ChordTable.Ids.ImportFilesAndFolders] = host => host.FileSidebar?.ImportCommand,
            [ChordTable.Ids.CancelImport] = host => host.FileSidebar?.CancelImportCommand,
            [ChordTable.Ids.RenameEntry] = host => host.FileSidebar?.RenameCommand,
            // W5-4 F4: the picker replaces the raw destination box —
            // the verb targets checks-or-selection, so the old
            // both-checks-and-typed-text CanExecute defect retires.
            [ChordTable.Ids.MoveTo] = host => host.FileSidebar?.MoveToCommand,
            // W5-4 F6 unified targeting: mac's slate.file.delete acts
            // on the tree SELECTION (the previous inversion routed it
            // at the batch checkboxes); the Windows-only trashSelected
            // id now names the batch-checkbox flow, whose semantics
            // are unchanged.
            [ChordTable.Ids.DeleteEntry] = host => host.FileSidebar?.DeleteCommand,
            [ChordTable.Ids.SidebarTrashSelected] = host => host.FileSidebar?.BatchTrashCommand,
            [ChordTable.Ids.DuplicateEntry] = host => host.FileSidebar?.DuplicateCommand,
            [ChordTable.Ids.CopyPath] = host => host.FileSidebar?.CopyPathCommand,
            [ChordTable.Ids.RevealInFinder] = host => host.FileSidebar?.RevealCommand,
            [ChordTable.Ids.CopyWikilink] = host => host.FileSidebar?.CopyWikilinkCommand,
            [ChordTable.Ids.PinNote] = host => host.FileSidebar?.PinCommand,
            [ChordTable.Ids.UnpinNote] = host => host.FileSidebar?.UnpinCommand,
            [ChordTable.Ids.UnpinAllInFolder] = host => host.FileSidebar?.UnpinAllCommand,
            [ChordTable.Ids.UseVaultDefaultSort] =
                host => host.FileSidebar?.UseVaultDefaultSortCommand,
            [ChordTable.Ids.AddShortcut] = host => host.FileSidebar?.AddShortcutCommand,
            [ChordTable.Ids.RemoveShortcut] = host => host.FileSidebar?.RemoveShortcutCommand,
            [ChordTable.Ids.ClearRecents] = host => host.FileSidebar?.ClearRecentsCommand,
            [ChordTable.Ids.CollapseAll] = host => host.FileSidebar?.CollapseAllCommand,
            [ChordTable.Ids.ExpandLoaded] = host => host.FileSidebar?.ExpandLoadedCommand,
            [ChordTable.Ids.HistoryBack] = host => host.FileSidebar?.HistoryBackCommand,
            [ChordTable.Ids.HistoryForward] = host => host.FileSidebar?.HistoryForwardCommand,
            [ChordTable.Ids.ToggleLayout] = host => host.FileSidebar?.ToggleDualPaneCommand,
            [ChordTable.Ids.AddTag] = host => host.FileSidebar?.AddTagCommand,
            [ChordTable.Ids.RemoveTag] = host => host.FileSidebar?.RemoveTagCommand,
            [ChordTable.Ids.CreateFolderNote] =
                host => host.FileSidebar?.CreateFolderNoteCommand,
            [ChordTable.Ids.DeleteFolderNote] =
                host => host.FileSidebar?.DeleteFolderNoteCommand,
            [ChordTable.Ids.SidebarRefresh] = host => host.FileSidebar?.RefreshCommand,
            [ChordTable.Ids.SidebarClearFilter] = host => host.FileSidebar?.ClearFilterCommand,
            [ChordTable.Ids.SidebarToggleTags] = host => host.FileSidebar?.ToggleTagsCommand,
        };

        // Ctrl+1…Ctrl+9 activate Shortcuts slots. The sidebar exposes the
        // capability as a method, not an ICommand, so the bridge is the one
        // place that adapts it — a fresh adapter per resolve keeps the
        // provider stateless and never outlives the view model it wraps.
        for (int slot = 1; slot <= 9; slot++)
        {
            int captured = slot;
            map[ChordTable.Ids.OpenShortcut(captured)] = host =>
                host.FileSidebar is { } sidebar
                    ? ShortcutSlotCommand(sidebar, captured)
                    : null;
        }

        // The viewport rows resolve THROUGH the binding record (§D
        // D14 / ID-8) — one authority for registration, resolution and
        // the census, never a literal entry that can drift from it.
        foreach ((string id, _, Func<ISlateCommandHost, ICommand?> resolve)
            in CanvasViewportBindings)
        {
            map[id] = resolve;
        }
        return map;
    }
}
