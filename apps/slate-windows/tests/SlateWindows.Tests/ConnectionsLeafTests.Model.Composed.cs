// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Graph;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-2 PR B2 (#746), contract B2-8 — THE MODEL's third family: the
/// COMPOSED routes, each a re-root or a Back with something landing inside
/// the open's dialog (the gate pumps a real dispatcher frame, IGK-20) or
/// with a particular source, target or load in flight, crossed with the
/// mode: a rename and a delete of the pin, of an entry and of the
/// just-pushed origin inside the dialog (a file and a folder); Back with
/// the reserved note renamed inside its open (nothing pops, B2-D5); a
/// re-entrant re-root and Back from the dialog; the pin's load and a
/// pre-existing load completing inside the open; the pin's and the pop's
/// load failing (the failure line); the focus request landing before and
/// after the pin's load applies (Term 9's line) and the FINAL focused
/// element — the leaf, never the editor (IGL-3); an image attachment as
/// the source; a canvas and a base as a prior pin restored by Back (IGL-1);
/// a canvas origin; a cold and a warm graph tab as the source; a
/// retirement, a tab change, a group change, a tab close, a depth change
/// and a probe inside the open (IGL-5, IGL-6); a rename of the pin with the
/// graph document alive, absent and re-seated (IGL-7). The pane is visible
/// and the leaf active throughout — the reveal's lines are the second
/// family's — and a note is in view, dirty where a dialog is wanted.
/// </summary>
public sealed partial class ConnectionsLeafTests
{
    private enum Composed
    {
        RenamePinInDialog,
        RenamePinFolderInDialog,
        RenameEntryInDialog,
        RenameOriginInDialog,
        DeletePinInDialog,
        DeletePinFolderInDialog,
        DeleteEntryInDialog,
        DeleteOriginInDialog,
        BackWithReservedRenamed,
        ReentrantReRootInDialog,
        ReentrantBackInDialog,
        PinLoadCompletesInDialog,
        PriorLoadCompletesInDialog,
        PinLoadFails,
        PopLoadFails,
        FocusLandsBeforeApply,
        FocusLandsAfterApply,
        AttachmentSource,
        CanvasPriorPinRestored,
        BasePriorPinRestored,
        CanvasOrigin,
        ColdGraphTabSource,
        WarmGraphTabSource,
        RetirementInDialog,
        TabChangeInDialog,
        GroupChangeInDialog,
        TabCloseInDialog,
        DepthChangeInDialog,
        ProbeInDialog,
        RenamePinGraphAlive,
        RenamePinGraphAbsent,
        RenamePinGraphReseated,
        // Codex post-implementation pass 1, IPC-4: the folder variants over an
        // ENTRY and over the just-pushed ORIGIN (a FOLLOWING start from Deep,
        // the folder's note), and what lands inside BACK's open.
        RenameEntryFolderInDialog,
        DeleteEntryFolderInDialog,
        RenameOriginFolderInDialog,
        DeleteOriginFolderInDialog,
        RenamePinInBackDialog,
        DeletePinInBackDialog,
        RenameEntryInBackDialog,
        DeleteEntryInBackDialog,
        ReentrantReRootInBackDialog,
        ReentrantBackInReRootDialog,
        PriorLoadCompletesInBackDialog,
        RetirementInBackDialog,
        TabChangeInBackDialog,
        GroupChangeInBackDialog,
        TabCloseInBackDialog,
        DepthChangeInBackDialog,
        ProbeInBackDialog,
    }

    private sealed record ComposedCell(Mode Mode, Composed Route)
    {
        public bool Pinned => Mode != Mode.Following;

        public override string ToString() => $"{Route} from [{Mode}]";
    }

    /// <summary>The derivation of a composed route: the timeline, the loads,
    /// the effective root, rule D's state, the last focus request, the
    /// depth, the presentation where a load's success does not decide it,
    /// how often the gate asked, and the trailing completions whose order
    /// the system does not fix.</summary>
    private sealed record ComposedDerivation(
        string[] Timeline,
        int Loads,
        string? Root,
        RootMode Mode,
        string? Focus,
        uint Depth = 1,
        ConnectionsLoadState? State = null,
        int Asked = 1,
        int Unordered = 0,
        bool EditorRequested = false,
        int? LoadsOr = null);

    private const string ReRootTarget = "10.md";
    private const string ReentrantTarget = "010.md";
    private const string RenamedReRootTarget = "10-renamed.md";
    private const string RenamedHub = "hub-renamed.md";

    /// <summary>The line the pin's tree reports at the route's depth, as it
    /// stood before the drive (a prior load released inside Back's open).</summary>
    private const string PinLinePlaceholder = "<pin-line>";

    /// <summary>The note beside for what lands inside BACK's open: not the
    /// top entry's note, whose existing tab an open would ACTIVATE without
    /// asking.</summary>
    private const string BesideForBack = ReRootTarget;

    private const int ComposedRoutes = 49;
    private const int ComposedCells = PinnedModes * ComposedRoutes;
    private const int ComposedUnreachable = 59;
    private const int ComposedDriven = 137;

    private static readonly Composed[] DialogRoutes =
    [
        Composed.RenamePinInDialog,
        Composed.RenamePinFolderInDialog,
        Composed.RenameEntryInDialog,
        Composed.RenameOriginInDialog,
        Composed.DeletePinInDialog,
        Composed.DeletePinFolderInDialog,
        Composed.DeleteEntryInDialog,
        Composed.DeleteOriginInDialog,
        Composed.BackWithReservedRenamed,
        Composed.ReentrantReRootInDialog,
        Composed.ReentrantBackInDialog,
        Composed.PinLoadCompletesInDialog,
        Composed.PriorLoadCompletesInDialog,
        Composed.RetirementInDialog,
        Composed.TabChangeInDialog,
        Composed.GroupChangeInDialog,
        Composed.TabCloseInDialog,
        Composed.DepthChangeInDialog,
        Composed.ProbeInDialog,
        Composed.RenameEntryFolderInDialog,
        Composed.DeleteEntryFolderInDialog,
        Composed.RenameOriginFolderInDialog,
        Composed.DeleteOriginFolderInDialog,
        Composed.RenamePinInBackDialog,
        Composed.DeletePinInBackDialog,
        Composed.RenameEntryInBackDialog,
        Composed.DeleteEntryInBackDialog,
        Composed.ReentrantReRootInBackDialog,
        Composed.ReentrantBackInReRootDialog,
        Composed.PriorLoadCompletesInBackDialog,
        Composed.RetirementInBackDialog,
        Composed.TabChangeInBackDialog,
        Composed.GroupChangeInBackDialog,
        Composed.TabCloseInBackDialog,
        Composed.DepthChangeInBackDialog,
        Composed.ProbeInBackDialog,
    ];

    /// <summary>The routes whose drive is BACK (the others re-root).</summary>
    private static readonly Composed[] BackRoutes =
    [
        Composed.BackWithReservedRenamed,
        Composed.ReentrantBackInDialog,
        Composed.PopLoadFails,
        Composed.CanvasPriorPinRestored,
        Composed.BasePriorPinRestored,
        Composed.RenamePinInBackDialog,
        Composed.DeletePinInBackDialog,
        Composed.RenameEntryInBackDialog,
        Composed.DeleteEntryInBackDialog,
        Composed.ReentrantReRootInBackDialog,
        Composed.PriorLoadCompletesInBackDialog,
        Composed.RetirementInBackDialog,
        Composed.TabChangeInBackDialog,
        Composed.GroupChangeInBackDialog,
        Composed.TabCloseInBackDialog,
        Composed.DepthChangeInBackDialog,
        Composed.ProbeInBackDialog,
    ];

    /// <summary>The routes that start FOLLOWING from Deep — the folder's
    /// note — so an entry or the just-pushed origin lives in a folder.</summary>
    private static readonly Composed[] FromDeepRoutes =
    [
        Composed.RenameEntryFolderInDialog,
        Composed.DeleteEntryFolderInDialog,
        Composed.RenameOriginFolderInDialog,
        Composed.DeleteOriginFolderInDialog,
    ];

    /// <summary>The routes whose pin's load is PARKED before the drive and
    /// released after it (or inside the dialog, where the route says), so
    /// what lands inside the dialog lands over a load in flight — the
    /// dialog's own pump (IGK-20) would otherwise apply a fast fetch before
    /// the action, the pool's order — and, for the probe, over a tree older
    /// than the vault. A Back route parks nothing before its drive (the
    /// pop's load follows its open); Deeper's prior load is the
    /// arrangement's own park.</summary>
    private static readonly Composed[] ParkedRoutes =
    [
        .. DialogRoutes.Where(route => route != Composed.PriorLoadCompletesInDialog && !BackRoutes.Contains(route)),
        Composed.FocusLandsBeforeApply,
    ];

    /// <summary>The routes whose parked pin load applies AFTER the dialog's
    /// action changed the vault: it speaks the tree it fetched BEFORE (the
    /// token's policy, Term 5), and the probe's mark reloads silently once
    /// after it (Term 6).</summary>
    private static readonly Composed[] SpeaksTheTreeBefore =
    [
        Composed.RenameEntryInDialog,
        Composed.RenameOriginInDialog,
        Composed.DeletePinInDialog,
        Composed.DeletePinFolderInDialog,
        Composed.DeleteEntryInDialog,
        Composed.DeleteOriginInDialog,
        Composed.RenameEntryFolderInDialog,
        Composed.DeleteEntryFolderInDialog,
        Composed.RenameOriginFolderInDialog,
        Composed.DeleteOriginFolderInDialog,
    ];

    private static string? UnreachableComposed(ComposedCell cell)
    {
        bool hasEntries = cell.Mode is Mode.PinnedFresh or Mode.PinnedDrifted;
        switch (cell.Route)
        {
            case Composed.RenamePinFolderInDialog:
            case Composed.DeletePinFolderInDialog:
                return cell.Pinned ? "the folder's only note is the pin already; the folder routes over a pin are the first family's" : null;
            case Composed.RenameEntryInDialog:
            case Composed.DeleteEntryInDialog:
                return cell.Mode == Mode.PinnedDrifted ? null : "no entry but the just-pushed origin: the entry routes need PinnedDrifted's prior pin";
            case Composed.BackWithReservedRenamed:
            case Composed.ReentrantBackInDialog:
            case Composed.PopLoadFails:
                return hasEntries ? null : "Back falls through: FOLLOWING, or an empty stack";
            case Composed.RenamePinGraphAlive:
            case Composed.RenamePinGraphAbsent:
            case Composed.RenamePinGraphReseated:
                return cell.Pinned ? null : "the shared key follows the PIN (IGL-7); FOLLOWING has none";
            case Composed.RenameEntryFolderInDialog:
            case Composed.DeleteEntryFolderInDialog:
            case Composed.RenameOriginFolderInDialog:
            case Composed.DeleteOriginFolderInDialog:
                return cell.Pinned ? "the folder's note is a FOLLOWING start's origin or entry (the pinned arrangements' origins are Two and Hub, at the root)" : null;
            case Composed.RenameEntryInBackDialog:
            case Composed.DeleteEntryInBackDialog:
                return cell.Mode == Mode.PinnedDrifted ? null : "an entry that is not the top needs PinnedDrifted's two";
            case Composed.RenamePinInBackDialog:
            case Composed.DeletePinInBackDialog:
            case Composed.ReentrantReRootInBackDialog:
            case Composed.PriorLoadCompletesInBackDialog:
            case Composed.RetirementInBackDialog:
            case Composed.TabChangeInBackDialog:
            case Composed.GroupChangeInBackDialog:
            case Composed.TabCloseInBackDialog:
            case Composed.DepthChangeInBackDialog:
            case Composed.ProbeInBackDialog:
                return hasEntries ? null : "Back falls through: FOLLOWING, or an empty stack";
            default:
                return null;
        }
    }

    /// <summary>The arranged state a composed route starts from: the mode's,
    /// or — for the folder routes — a FOLLOWING start from Deep, with Hub
    /// pinned over it for the ENTRY variants (the stack then holds
    /// <c>(FOLLOWING, Deep)</c> below the route's push).</summary>
    private static (string? Pin, string NoteInView, (string? Pin, string Effective)[] Stack) ArrangedFor(ComposedCell cell)
    {
        Cell modeCell = ModeCellOf(cell);
        return cell.Route switch
        {
            Composed.RenameEntryFolderInDialog or Composed.DeleteEntryFolderInDialog => (Hub, Hub, [(null, Deep)]),
            Composed.RenameOriginFolderInDialog or Composed.DeleteOriginFolderInDialog => (null, Deep, []),
            _ => (cell.Pinned ? PinBefore(modeCell) : null, NoteInViewBefore(modeCell)!, StackBefore(modeCell)),
        };
    }

    /// <summary>The first family's cell the arrangement borrows: the mode
    /// with a note in view, current, nothing in flight.</summary>
    private static Cell ModeCellOf(ComposedCell cell) =>
        new(cell.Mode, Pane.Visible, LeafState.Connections, RootState.Note, Presentation.Current, Pending.None, Route.SwitchToLeaf);

    /// <summary>The entry a re-root pushes from the arranged state: the pin
    /// (Term 12), or FOLLOWING the note in view.</summary>
    private static (string? Pin, string Effective) PushOf(ComposedCell cell)
    {
        Cell modeCell = ModeCellOf(cell);
        return cell.Pinned ? (PinBefore(modeCell), PinBefore(modeCell)) : (null, NoteInViewBefore(modeCell)!);
    }

    private static string FailureLine() =>
        Render(new GraphA11yEvent.GraphBlocked(new GraphBlockedReason.ConnectionsLoadFailed(InjectedTransientFailure)));

    /// <summary>What the model asserts after the route, derived per route
    /// (the comments carry the derivation).</summary>
    private static ComposedDerivation DeriveComposed(ComposedCell cell, Fixture fixture)
    {
        Cell modeCell = ModeCellOf(cell);
        (string? pinBefore, string noteInView, (string? Pin, string Effective)[] stackBefore) = ArrangedFor(cell);
        // The entry a re-root pushes from the arranged state: the pin, or
        // FOLLOWING the note in view.
        (string? Pin, string Effective) push = pinBefore is null ? (null, noteInView) : (pinBefore, pinBefore);
        List<(string? Pin, string Effective)> pushed = [.. stackBefore, push];
        string target = ReRootTarget;
        string[] reRooted = [ReRooted(target)];
        // Back's top entry, where there is one: its prior mode and its note.
        (string? topPin, string topNote) = stackBefore.Length > 0 ? stackBefore[^1] : (null, string.Empty);
        (string? Pin, string Effective)[] popped = stackBefore.Length > 0 ? stackBefore[..^1] : [];
        string? rootAfterPop = topPin ?? topNote;
        RootMode Pinned(string pin, string? inView, IEnumerable<(string? Pin, string Effective)> stack) =>
            new(pin, inView, [.. stack], StableKey(pin));
        RootMode plain = Pinned(target, target, pushed);

        switch (cell.Route)
        {
            case Composed.RenamePinInDialog:
                // Term 16 over the pin while its open's dialog is up: the pin
                // moves (Term 3(d)'s root move: a second audible load, the
                // first foreign), the key follows; the open installs the path
                // it was built for — the OLD one (B2-D10); the probe marks the
                // load in flight and its tree is current. No outline line: the
                // note has no heading.
                return new([.. reRooted, LinePlaceholder], 2, RenamedReRootTarget, Pinned(RenamedReRootTarget, target, pushed), "RightPane");
            case Composed.RenamePinFolderInDialog:
                // FOLLOWING, the pin Deep from Hub's tree: the folder moves
                // under the pin, the open installs the old path.
                return new([ReRooted(Deep), LinePlaceholder], 2, MovedPin(), Pinned(MovedPin(), Deep, [.. stackBefore, (null, noteInView)]), "RightPane");
            case Composed.RenameEntryInDialog:
                {
                    // PinnedDrifted's prior pin, Hub, renamed inside the dialog: the
                    // entry moves; the pin's load, parked, is older than the vault
                    // once released, so the probe's mark reloads it silently once.
                    List<(string? Pin, string Effective)> stack = [(null, Two), (RenamedHub, RenamedHub), push];
                    return new([.. reRooted, LinePlaceholder], 2, target, Pinned(target, target, stack), "RightPane");
                }
            case Composed.RenameOriginInDialog:
                {
                    // The just-pushed origin renamed inside the dialog — FOLLOWING
                    // the dirty tab's own note (its tab retargets; Hub's outline
                    // count is the shell's line for the new path), under a pin the
                    // pin the route pushed; the entry moves, the pin does not; the
                    // parked load's tree is older: the mark reloads once.
                    (string? Pin, string Effective) moved = cell.Pinned
                        ? (RenamedPin(modeCell), RenamedPin(modeCell))
                        : (null, RenamedHub);
                    string[] outline = cell.Pinned ? [] : ["OutlineCount"];
                    return new([.. reRooted, .. outline, LinePlaceholder], 2, target, Pinned(target, target, [.. stackBefore, moved]), "RightPane");
                }
            case Composed.DeletePinInDialog:
                // The pin's file deleted inside the dialog: the delete hook keeps
                // the pin (Term 16), no tab shows it yet so no missing-file line;
                // the parked load's tree applies and speaks, then the mark's
                // silent reload fails: Error. The open installs the path.
                return new([.. reRooted, LinePlaceholder], 2, target, plain, "RightPane", State: ConnectionsLoadState.Error);
            case Composed.DeletePinFolderInDialog:
                return new([ReRooted(Deep), LinePlaceholder], 2, Deep, Pinned(Deep, Deep, [.. stackBefore, (null, noteInView)]), "RightPane", State: ConnectionsLoadState.Error);
            case Composed.DeleteEntryInDialog:
                // PinnedDrifted's prior pin, Hub, deleted inside the dialog: its
                // entry pruned, no tab shows it; the mark's reload once.
                return new([.. reRooted, LinePlaceholder], 2, target, Pinned(target, target, [(null, Two), push]), "RightPane");
            case Composed.DeleteOriginInDialog:
                {
                    // The just-pushed origin deleted inside the dialog: its entry
                    // pruned; the shell's missing-file line iff a tab shows it (the
                    // dirty tab FOLLOWING and for PinnedFresh and PinnedNoOrigin,
                    // whose note in view is the pin; none under PinnedDrifted).
                    string[] missing = cell.Mode == Mode.PinnedDrifted ? [] : ["HostComposed"];
                    return new([.. reRooted, .. missing, LinePlaceholder], 2, target, Pinned(target, target, stackBefore), "RightPane");
                }
            case Composed.BackWithReservedRenamed:
                {
                    // Term 13 / B2-D5: the top entry's note renamed inside Back's
                    // open — the entry rewritten by the hook — while the open
                    // installs the path it was built for: the top no longer names
                    // the installed note, nothing pops; the pin stands, the note in
                    // view is the installed one; the probe reloads the pin's
                    // current tree silently (the vault moved on).
                    (string? Pin, string Effective) top = stackBefore[^1];
                    string renamed = cell.Mode == Mode.PinnedFresh ? RenamedOrigin : RenamedHub;
                    List<(string? Pin, string Effective)> stack = [.. stackBefore[..^1], (top.Pin is null ? null : renamed, renamed)];
                    return new([], 1, pinBefore, Pinned(pinBefore!, top.Effective, stack), null);
                }
            case Composed.ReentrantReRootInDialog:
                {
                    // A second re-root from inside the first open's dialog: its pin
                    // mutation pushes the first pin, pins again (the first load
                    // foreign), its open asks the same dirty tab again and installs;
                    // then the first dialog answers and the first open installs ITS
                    // path over it (the item built before the dialog, B2-D10).
                    List<(string? Pin, string Effective)> stack = [.. pushed, (target, target)];
                    return new([.. reRooted, ReRooted(ReentrantTarget), LinePlaceholder], 2, ReentrantTarget, Pinned(ReentrantTarget, target, stack), "RightPane", Asked: 2);
                }
            case Composed.ReentrantBackInDialog:
                {
                    // Back again from inside Back's open: the inner open asks the
                    // same tab and installs, the inner pop pops; the outer open
                    // finds its target in view and the outer pop finds the top
                    // changed — or the stack empty — and pops nothing.
                    (string? priorPin, string effective) = stackBefore[^1];
                    return new([ReRooted(effective), LinePlaceholder], 1, priorPin ?? effective, Pinned(priorPin ?? effective, effective, stackBefore[..^1]) with { Pin = priorPin }, "RightPane", Asked: 2);
                }
            case Composed.PinLoadCompletesInDialog:
                // The pin's load, parked, released and applied inside the dialog:
                // its line lands inside the open; the open then installs.
                return new([.. reRooted, LinePlaceholder], 1, target, plain, "RightPane");
            case Composed.PriorLoadCompletesInDialog:
                // Deeper's audible reload over the root, parked before the route:
                // the pin mutation makes it foreign; released inside the dialog it
                // is dropped silently; the pin's own load is at the depth Deeper
                // left.
                return new([.. reRooted, LinePlaceholder], 1, target, plain, "RightPane", Depth: 2, Asked: 1);
            case Composed.PinLoadFails:
                // The pin's load fails transiently: the failure line at apply
                // (Term 14), the presentation Error; a clean open.
                return new([.. reRooted, FailureLine()], 1, target, plain, "RightPane", State: ConnectionsLoadState.Error, Asked: 0);
            case Composed.PopLoadFails:
                {
                    (string? priorPin, string effective) = stackBefore[^1];
                    return new([ReRooted(effective), FailureLine()], 1, priorPin ?? effective, Pinned(priorPin ?? effective, effective, stackBefore[..^1]) with { Pin = priorPin }, "RightPane", State: ConnectionsLoadState.Error, Asked: 0);
                }
            case Composed.FocusLandsBeforeApply:
                // The boundary request answered at once, the pin's load still in
                // flight: Term 9's LoadingConnections, then the summary at apply.
                return new([.. reRooted, LoadingLine(), LinePlaceholder], 1, target, plain, "RightPane", Asked: 0);
            case Composed.FocusLandsAfterApply:
                // The request answered after the drain: a ready tree with rows
                // says nothing (Term 9); the last request is the leaf's boundary
                // and the editor's was never raised (IGL-3).
                return new([.. reRooted, LinePlaceholder], 1, target, plain, "RightPane", Asked: 0);
            case Composed.AttachmentSource:
                // An image in view FOLLOWING is the effective root: pushed. Under
                // a pin the image is the note in view and the pin the effective
                // root (Term 11): the pin is pushed (Term 12; IPC-14).
                return new([.. reRooted, LinePlaceholder], 1, target, Pinned(target, target, cell.Pinned ? pushed : [(null, Attachment)]), "RightPane", Asked: 0);
            case Composed.CanvasPriorPinRestored:
            case Composed.BasePriorPinRestored:
                {
                    // A canvas (a base) pinned over the arranged state, then a note,
                    // then Back: the open of the canvas lands IN PLACE as its own
                    // kind of tab — no Markdown candidate — and the pop restores the
                    // canvas as the pin, its tree loaded; the note in view none
                    // (IGL-1). Under a pin the arranged pin sits below the canvas's
                    // entry and stays there (IPC-14).
                    string prior = cell.Route == Composed.CanvasPriorPinRestored ? Board : NotesBase;
                    return new([ReRooted(prior), LinePlaceholder], 1, prior, new(prior, null, [.. pushed], StableKey(prior)), "RightPane", Asked: 0);
                }
            case Composed.CanvasOrigin:
                // A canvas in view has no effective root FOLLOWING (B2-D7): nothing
                // pushed; under a pin the pin is pushed (Term 12; IPC-14). The open
                // replaces the canvas's tab in place either way.
                return new([.. reRooted, LinePlaceholder], 1, target, Pinned(target, target, cell.Pinned ? pushed : []), "RightPane", Asked: 0);
            case Composed.ColdGraphTabSource:
            case Composed.WarmGraphTabSource:
                {
                    // The table's entrance from the graph tab: cold — just opened
                    // and published — or warm — re-activated by the address from a
                    // note beside it (`TabFocused`; its activation summary never
                    // lands: the open replaces the graph's tab in place, A-9, and
                    // the document retires before its line). Nothing pushed
                    // FOLLOWING (B2-D7); under a pin the pin is pushed.
                    string[] address = cell.Route == Composed.WarmGraphTabSource ? ["TabFocused"] : [];
                    List<(string? Pin, string Effective)> stack = cell.Pinned ? pushed : [.. stackBefore];
                    return new([.. address, .. reRooted, LinePlaceholder], 1, target, Pinned(target, target, stack), "RightPane", Asked: 0);
                }
            case Composed.RetirementInDialog:
                // The leaf retired inside the dialog (IGL-5): the open installs,
                // the boundary's note change is refused, the parked load's result
                // is dropped; the pin the mutation set stands, the note in view is
                // the arranged one, and Back afterwards falls through. The
                // retirement installs NoNote and clears the flight (IPC-15).
                return new(reRooted, 1, target, Pinned(target, noteInView, pushed), "RightPane", State: ConnectionsLoadState.NoNote);
            case Composed.TabChangeInDialog:
                // Another tab activated inside the dialog (`TabFocused`; the note
                // in view recorded under the pin); the open re-activates the tab it
                // captured (`TabFocused` again) and installs into it (IGL-6).
                return new([.. reRooted, "TabFocused", "TabFocused", LinePlaceholder], 1, target, plain, "RightPane");
            case Composed.GroupChangeInDialog:
                // The other group made active inside the dialog (the shell's
                // `EditorPaneFocused`; the pane-focus command asks for the
                // editor, as it does everywhere): the open installs nothing
                // (IGL-6); the note in view is the duplicate's, the same note;
                // the pin stands; the LAST request is still the leaf's.
                return new([.. reRooted, "EditorPaneFocused", LinePlaceholder], 1, target, Pinned(target, noteInView, pushed), "RightPane", EditorRequested: true);
            case Composed.TabCloseInDialog:
                // The tab beside activated and closed inside the dialog (its
                // `TabFocused`, then the captured tab's as the successor, then
                // `TabClosed`); the open installs into the captured tab.
                return new([.. reRooted, "TabFocused", "TabFocused", "TabClosed", LinePlaceholder], 1, target, plain, "RightPane");
            case Composed.DepthChangeInDialog:
                // Deeper inside the dialog: a load at the new depth supersedes the
                // parked one (Term 3(e)); its line at that depth.
                return new([.. reRooted, LinePlaceholder], 2, target, plain, "RightPane", Depth: 2);
            case Composed.ProbeInDialog:
                // The probe inside the dialog over the load in flight: the mark;
                // the tree, once applied, is at the vault's generation — nothing
                // more (Term 6).
                return new([.. reRooted, LinePlaceholder], 1, target, plain, "RightPane");
            case Composed.RenamePinGraphAlive:
            case Composed.RenamePinGraphAbsent:
            case Composed.RenamePinGraphReseated:
                {
                    // Term 16 over the pin with the graph document alive beside,
                    // absent, or re-seated over the same view state (IGL-7): the
                    // pin moves with one audible load and the key follows it in all
                    // three — a document's revalidation runs against the snapshot
                    // that carries the new node.
                    // With the pin's own tab in view the hook's fetch races the
                    // panels' metadata touch (the first family's RenameRoot): one
                    // load, or two.
                    string renamed = RenamedPin(modeCell);
                    bool pinsTab = cell.Mode != Mode.PinnedDrifted;
                    string? inView = pinsTab ? renamed : noteInView;
                    return new([LinePlaceholder], 1, renamed, Pinned(renamed, inView, stackBefore), null, Asked: 0, LoadsOr: pinsTab ? 2 : null);
                }
            case Composed.RenameEntryFolderInDialog:
                // FOLLOWING from Deep with Hub pinned over it: the entry
                // (FOLLOWING, Deep) lives in a folder; the folder moved inside
                // the re-root's dialog moves the entry (Term 16); no tab shows
                // it; the parked pin load speaks the tree before, the mark
                // reloads once.
                return new([.. reRooted, LinePlaceholder], 2, target, Pinned(target, target, [(null, MovedPin()), push]), "RightPane");
            case Composed.DeleteEntryFolderInDialog:
                // The folder deleted: the entry under it pruned, the pin's push
                // above it kept.
                return new([.. reRooted, LinePlaceholder], 2, target, Pinned(target, target, [push]), "RightPane");
            case Composed.RenameOriginFolderInDialog:
                // FOLLOWING from Deep: the just-pushed origin lives in a folder;
                // the folder moved inside the dialog moves the origin's entry and
                // the dirty tab retargets before the open replaces it (no outline
                // line: Deep has no heading).
                return new([.. reRooted, LinePlaceholder], 2, target, Pinned(target, target, [(null, MovedPin())]), "RightPane");
            case Composed.DeleteOriginFolderInDialog:
                // The origin's folder deleted: its entry pruned and its tab — the
                // dirty one — invalidated (the shell's missing-file line) before
                // the open replaces it.
                return new([.. reRooted, "HostComposed", LinePlaceholder], 2, target, Pinned(target, target, []), "RightPane");
            case Composed.RenamePinInBackDialog:
                // The pin renamed inside Back's open: Term 16 moves it with an
                // audible load, which the pop's transition makes foreign; the
                // pop restores the prior mode on the top's note and speaks it.
                return new([ReRooted(topNote), LinePlaceholder], 2, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane");
            case Composed.DeletePinInBackDialog:
                {
                    // The pin's file deleted inside Back's open: the delete hook
                    // keeps the pin and prunes nothing (the entries name Two and
                    // Hub); the shell's missing-file line iff the pin's tab is the
                    // one in view (PinnedFresh); the probe's decision — a pool
                    // read applied on the dispatcher — lands only after the
                    // dialog returned and the pop transitioned, where it finds
                    // the pop's load in flight and marks (Term 6); the pop's tree
                    // is current, so the pop's load is the one load.
                    string[] missing = cell.Mode == Mode.PinnedFresh ? ["HostComposed"] : [];
                    return new([.. missing, ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane");
                }
            case Composed.RenameEntryInBackDialog:
                // PinnedDrifted's lower entry (FOLLOWING, Two) renamed inside
                // Back's open of Hub: the entry moves; no tab shows Two; the
                // probe's decision lands after the pop and marks over its load
                // (see DeletePinInBackDialog); the pop proceeds on the top,
                // untouched — one load.
                return new([ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, [(null, RenamedOrigin)]) with { Pin = topPin }, "RightPane");
            case Composed.DeleteEntryInBackDialog:
                // The lower entry's note deleted: the entry pruned (no tab, no
                // line); the probe marks after the pop; the pop leaves the stack
                // empty — one load.
                return new([ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, []) with { Pin = topPin }, "RightPane");
            case Composed.ReentrantReRootInBackDialog:
                {
                    // A re-root from inside Back's open: its pin mutation pushes
                    // the pin and pins the new note (one load), its own open asks
                    // the same dirty tab again and installs; Back's open then
                    // installs the top's note over it and finds the top no longer
                    // names the installed note — nothing pops (B2-D5).
                    List<(string? Pin, string Effective)> stack = [.. stackBefore, (pinBefore, pinBefore!)];
                    return new([ReRooted(ReentrantTarget), LinePlaceholder], 1, ReentrantTarget, Pinned(ReentrantTarget, topNote, stack), "RightPane", Asked: 2);
                }
            case Composed.ReentrantBackInReRootDialog:
                {
                    // Back from inside a re-root's open, after the pin mutation
                    // pushed: the inner open of the top's note — the note in view
                    // itself under PinnedFresh, PinnedNoOrigin and FOLLOWING (no
                    // dialog: the target is already in view), the orphan's dirty
                    // tab under PinnedDrifted (a second dialog) — and the pop
                    // restores the pushed entry's mode with its load; the outer
                    // open then installs the target: FOLLOWING, a root change and
                    // its load (the pop's foreign); under a restored pin, recorded.
                    bool following = pinBefore is null;
                    string restored = push.Pin ?? push.Effective;
                    string rootAfter = following ? target : restored;
                    RootMode mode = new(push.Pin, target, [.. stackBefore], following ? StableKey(push.Effective) : StableKey(restored));
                    return new([.. reRooted, ReRooted(push.Effective), LinePlaceholder], following ? 3 : 2, rootAfter, mode, "RightPane", Asked: cell.Mode == Mode.PinnedDrifted ? 2 : 1);
                }
            case Composed.PriorLoadCompletesInBackDialog:
                // Deeper's reload over the pin, parked before the route, released
                // inside Back's open: it applies and speaks the pin's tree at
                // depth two (audible, the leaf active); then the pop's load at
                // that depth.
                return new([PinLinePlaceholder, ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane", Depth: 2);
            case Composed.RetirementInBackDialog:
                // The leaf retired inside Back's open: the re-admission after the
                // open refuses (IGL-5) — nothing pops, no request; the open
                // installed the top's note but the retired leaf recorded nothing;
                // the retirement installs NoNote and clears the flight (IPC-15).
                return new([], 0, pinBefore, Pinned(pinBefore!, noteInView, stackBefore), null, State: ConnectionsLoadState.NoNote);
            case Composed.TabChangeInBackDialog:
                // The tab beside activated inside Back's open (`TabFocused`,
                // recorded under the pin); the open re-activates the captured tab
                // (`TabFocused`) and installs; the pop proceeds.
                return new(["TabFocused", "TabFocused", ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane");
            case Composed.GroupChangeInBackDialog:
                // The other group made active inside Back's open: the open
                // installs nothing (IGL-6) and Back pops nothing; the pane-focus
                // command's editor request is the last one.
                return new(["EditorPaneFocused"], 0, pinBefore, Pinned(pinBefore!, noteInView, stackBefore), "editor", EditorRequested: true);
            case Composed.TabCloseInBackDialog:
                // The tab beside activated and closed inside Back's open; the open
                // installs into the captured tab; the pop proceeds.
                return new(["TabFocused", "TabFocused", "TabClosed", ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane");
            case Composed.DepthChangeInBackDialog:
                // Deeper inside Back's open: a load over the pin at the new depth,
                // foreign at the pop's transition; the pop's own load at that
                // depth.
                return new([ReRooted(topNote), LinePlaceholder], 2, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane", Depth: 2);
            case Composed.ProbeInBackDialog:
                // The probe inside Back's open over a current tree at the vault's
                // generation: nothing; the pop's load alone.
                return new([ReRooted(topNote), LinePlaceholder], 1, rootAfterPop, Pinned(rootAfterPop!, topNote, popped) with { Pin = topPin }, "RightPane");
            default:
                throw new InvalidOperationException($"the model derives no composed route {cell.Route}");
        }
    }

    /// <summary>The composed route's arrangement beyond the mode's: the
    /// files the route needs, the tabs beside, the surfaces, the dirty tab,
    /// the seams (the failure, the focus landing, the parked load) and what
    /// the dialog runs.</summary>
    private static ParkedFetch? ArrangeComposed(Host host, ComposedCell cell, Fixture fixture, GateSeam gate)
    {
        Cell modeCell = ModeCellOf(cell);
        string noteInView = NoteInViewBefore(modeCell)!;
        if (cell.Route is Composed.CanvasPriorPinRestored or Composed.BasePriorPinRestored or Composed.CanvasOrigin)
        {
            File.WriteAllText(Path.Combine(host.Root, Board), "{\"nodes\":[],\"edges\":[]}\n");
            File.WriteAllText(
                Path.Combine(host.Root, NotesBase),
                "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
            using var cancel = new CancelToken();
            host.Session.ScanInitial(cancel);
        }
        ParkedFetch? parked = Arrange(host, modeCell, fixture, 0);
        Assert.Null(parked);
        if (FromDeepRoutes.Contains(cell.Route))
        {
            // FOLLOWING from Deep, the folder's note; Hub pinned over it for the
            // entry variants, so (FOLLOWING, Deep) is an entry below the route's
            // push.
            host.OpenNote(Deep);
            host.Settle();
            if (cell.Route is Composed.RenameEntryFolderInDialog or Composed.DeleteEntryFolderInDialog)
            {
                Assert.True(host.Workspace.ReRootConnectionsOn(Hub), $"{cell}: the funnel refused Hub");
                host.Settle();
            }
            (string? pin, string inView, (string? Pin, string Effective)[] stack) = ArrangedFor(cell);
            Assert.Equal(pin, host.Leaf.Pin);
            Assert.Equal(inView, host.Leaf.NoteInView);
            Assert.Equal(stack, host.Leaf.BackStack);
            noteInView = inView;
        }

        switch (cell.Route)
        {
            case Composed.TabChangeInBackDialog:
            case Composed.TabCloseInBackDialog:
                // A tab beside that is NOT the top entry's note (Back's open
                // would activate that tab and ask nothing).
                host.Workspace.OpenPath(BesideForBack, WorkspaceOpenTarget.NewTab);
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView);
                host.Settle();
                break;
            case Composed.GroupChangeInBackDialog:
                host.Workspace.SplitRightCommand.Execute(null);
                host.Settle();
                break;
            case Composed.PriorLoadCompletesInBackDialog:
                host.Settle();
                parked = Park(host.Leaf);
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                Assert.True(parked.Reached.Wait(TimeSpan.FromSeconds(10)), $"{cell}: Deeper's reload never parked");
                break;
            case Composed.AttachmentSource:
                // The image in view: the effective root FOLLOWING; under a pin
                // the note in view alone, the pin the root (Term 11).
                host.Workspace.OpenPath(Attachment, WorkspaceOpenTarget.CurrentTab);
                host.Settle();
                Assert.Equal(cell.Pinned ? PinBefore(modeCell) : Attachment, host.Leaf.Root);
                Assert.Equal(Attachment, host.Leaf.NoteInView);
                break;
            case Composed.CanvasOrigin:
                // The canvas in view: no root FOLLOWING (B2-D7); under a pin the
                // pin stays the root and no note is in view.
                host.Workspace.OpenPath(Board, WorkspaceOpenTarget.CurrentTab);
                SettleTheDocuments(host);
                Assert.Equal(cell.Pinned ? PinBefore(modeCell) : null, host.Leaf.Root);
                Assert.Null(host.Leaf.NoteInView);
                break;
            case Composed.CanvasPriorPinRestored:
            case Composed.BasePriorPinRestored:
                {
                    string prior = cell.Route == Composed.CanvasPriorPinRestored ? Board : NotesBase;
                    (string? Pin, string Effective) push = PushOf(cell);
                    Assert.True(host.Workspace.ReRootConnectionsOn(prior));
                    SettleTheDocuments(host);
                    Assert.Equal(prior, host.Leaf.Pin);
                    Assert.Null(host.Leaf.NoteInView);
                    Assert.True(host.Workspace.ReRootConnectionsOn(ReRootTarget));
                    SettleTheDocuments(host);
                    Assert.Equal([.. StackBefore(modeCell), push, (prior, prior)], host.Leaf.BackStack);
                    break;
                }
            case Composed.ColdGraphTabSource:
                host.Workspace.OpenGraph();
                SettleTheDocuments(host);
                break;
            case Composed.WarmGraphTabSource:
                host.Workspace.OpenGraph();
                SettleTheDocuments(host);
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView);
                SettleTheDocuments(host);
                break;
            case Composed.TabChangeInDialog:
            case Composed.TabCloseInDialog:
                host.Workspace.OpenPath(Two, WorkspaceOpenTarget.NewTab);
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView);
                host.Settle();
                break;
            case Composed.GroupChangeInDialog:
                host.Workspace.SplitRightCommand.Execute(null);
                host.Settle();
                break;
            case Composed.PriorLoadCompletesInDialog:
                host.Settle();
                parked = Park(host.Leaf);
                host.Workspace.ConnectionsDeeperCommand.Execute(null);
                Assert.True(parked.Reached.Wait(TimeSpan.FromSeconds(10)), $"{cell}: Deeper's reload never parked");
                break;
            case Composed.PinLoadFails:
            case Composed.PopLoadFails:
                FailTheNextFetches(host, 1);
                break;
            case Composed.RenamePinGraphAlive:
                host.Workspace.OpenGraph();
                SettleTheDocuments(host);
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView);
                SettleTheDocuments(host);
                break;
            case Composed.RenamePinGraphReseated:
                // Opened, replaced in place by a note with no tab of its own
                // (retired — an open of a note whose tab exists would activate
                // that tab), opened again: a new document over the workspace's
                // one view state; the arranged note back in view.
                host.Workspace.OpenGraph();
                SettleTheDocuments(host);
                host.Workspace.OpenPath(Two, WorkspaceOpenTarget.CurrentTab);
                SettleTheDocuments(host);
                Assert.DoesNotContain(host.Workspace.ActiveGroup.Tabs, tab => tab.IsGraph);
                host.Workspace.OpenGraph();
                SettleTheDocuments(host);
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, noteInView);
                SettleTheDocuments(host);
                break;
        }
        // The leaf shown and active (a surface may have moved it), current.
        if (!host.Workspace.ConnectionsLeafIsActive())
        {
            host.ActivateLeaf();
        }
        if (cell.Route is not (Composed.PriorLoadCompletesInDialog or Composed.PriorLoadCompletesInBackDialog))
        {
            host.Settle();
            Assert.True(host.Leaf.Root is null || host.Leaf.IsCurrent, $"{cell}: the arrangement left the presentation stale");
        }

        if (DialogRoutes.Contains(cell.Route))
        {
            WorkspaceTabViewModel tab = host.Workspace.ActiveGroup.ActiveTab!;
            Assert.True(tab.IsMarkdown, $"{cell}: the tab in view is not a note's");
            tab.Text = "# edited and unsaved\n";
            Assert.True(tab.IsDirty, $"{cell}: the tab did not become dirty");
            gate.Decision = WorkspaceDirtyNavigationDecision.Discard;
        }
        gate.InsideTheDialog = cell.Route switch
        {
            Composed.RenamePinInDialog => () => RenameIn(host, ReRootTarget, RenamedReRootTarget),
            Composed.RenamePinFolderInDialog => () => RenameFolderIn(host, PinFolder, MovedFolder),
            Composed.RenameEntryInDialog => () => RenameIn(host, Hub, RenamedHub),
            Composed.RenameOriginInDialog => () => RenameIn(host, PushOf(cell).Effective, cell.Pinned ? RenamedPin(modeCell) : RenamedHub),
            Composed.DeletePinInDialog => () => DeleteIn(host, ReRootTarget),
            Composed.DeletePinFolderInDialog => () => DeleteFolderIn(host, PinFolder),
            Composed.DeleteEntryInDialog => () => DeleteIn(host, Hub),
            Composed.DeleteOriginInDialog => () => DeleteIn(host, PushOf(cell).Effective),
            Composed.BackWithReservedRenamed => () =>
            {
                (string? pin, string effective) = StackBefore(modeCell)[^1];
                RenameIn(host, effective, cell.Mode == Mode.PinnedFresh ? RenamedOrigin : RenamedHub);
            }
            ,
            Composed.ReentrantReRootInDialog => () => Assert.True(host.Workspace.ReRootConnectionsOn(ReentrantTarget), $"{cell}: the re-entrant re-root refused"),
            Composed.ReentrantBackInDialog => () => Assert.True(host.Workspace.ConnectionsBack(), $"{cell}: the re-entrant Back popped nothing"),
            Composed.PinLoadCompletesInDialog or Composed.PriorLoadCompletesInDialog => () =>
            {
                gate.Parked!.Gate.Set();
                host.Settle();
            }
            ,
            Composed.RetirementInDialog => () => host.Leaf.Retire(),
            Composed.TabChangeInDialog => () => host.Workspace.ActiveGroup.ActiveTab = TabFor(host, Two),
            Composed.GroupChangeInDialog => () => Assert.True(host.Workspace.FocusDirectionalPane("horizontal", -1), $"{cell}: no group to move to"),
            Composed.TabCloseInDialog => () =>
            {
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, Two);
                host.Workspace.CloseActiveTabCommand.Execute(null);
            }
            ,
            Composed.DepthChangeInDialog => () => host.Workspace.ConnectionsDeeperCommand.Execute(null),
            Composed.ProbeInDialog => () => host.Workspace.NotifyGraphOfVaultChange(),
            Composed.RenameEntryFolderInDialog or Composed.RenameOriginFolderInDialog => () => RenameFolderIn(host, PinFolder, MovedFolder),
            Composed.DeleteEntryFolderInDialog or Composed.DeleteOriginFolderInDialog => () => DeleteFolderIn(host, PinFolder),
            Composed.RenamePinInBackDialog => () => RenameIn(host, PinBefore(modeCell), RenamedPin(modeCell)),
            Composed.DeletePinInBackDialog => () => DeleteIn(host, PinBefore(modeCell)),
            Composed.RenameEntryInBackDialog => () => RenameIn(host, Two, RenamedOrigin),
            Composed.DeleteEntryInBackDialog => () => DeleteIn(host, Two),
            Composed.ReentrantReRootInBackDialog => () => Assert.True(host.Workspace.ReRootConnectionsOn(ReentrantTarget), $"{cell}: the re-entrant re-root refused"),
            // The pin mutation pushed before this open, so the inner Back has
            // an entry to pop in every mode.
            Composed.ReentrantBackInReRootDialog => () => Assert.True(host.Workspace.ConnectionsBack(), $"{cell}: the re-entrant Back popped nothing"),
            Composed.PriorLoadCompletesInBackDialog => () =>
            {
                gate.Parked!.Gate.Set();
                host.Settle();
            }
            ,
            Composed.RetirementInBackDialog => () => host.Leaf.Retire(),
            Composed.TabChangeInBackDialog => () => host.Workspace.ActiveGroup.ActiveTab = TabFor(host, BesideForBack),
            Composed.GroupChangeInBackDialog => () => Assert.True(host.Workspace.FocusDirectionalPane("horizontal", -1), $"{cell}: no group to move to"),
            Composed.TabCloseInBackDialog => () =>
            {
                host.Workspace.ActiveGroup.ActiveTab = TabFor(host, BesideForBack);
                host.Workspace.CloseActiveTabCommand.Execute(null);
            }
            ,
            Composed.DepthChangeInBackDialog => () => host.Workspace.ConnectionsDeeperCommand.Execute(null),
            Composed.ProbeInBackDialog => () => host.Workspace.NotifyGraphOfVaultChange(),
            _ => null,
        };
        return parked;
    }

    /// <summary>A rename as the lifecycle sees it: core's rename, the
    /// workspace's retarget (the rename hook), the file-change arm.</summary>
    private static void RenameIn(Host host, string from, string to)
    {
        host.Session.RenameFile(from, Path.GetFileName(to));
        host.Workspace.RetargetPath(from, to);
        host.Workspace.NotifyGraphOfVaultChange();
    }

    private static void RenameFolderIn(Host host, string from, string to)
    {
        host.Session.RenameFolder(from, Path.GetFileName(to));
        host.Workspace.RetargetPath(from, to);
        host.Workspace.NotifyGraphOfVaultChange();
    }

    /// <summary>A delete as the lifecycle sees it: the file gone and the
    /// vault rescanned, the workspace's invalidation (the delete hook), the
    /// file-change arm.</summary>
    private static void DeleteIn(Host host, string path)
    {
        DeleteTheFile(host, path);
        host.Workspace.InvalidatePath(path);
        host.Workspace.NotifyGraphOfVaultChange();
    }

    private static void DeleteFolderIn(Host host, string folder)
    {
        string path = Path.Combine(host.Root, folder);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        using (var cancel = new CancelToken())
        {
            host.Session.ScanInitial(cancel);
        }
        host.Workspace.InvalidatePath(folder);
        host.Workspace.NotifyGraphOfVaultChange();
    }

    /// <summary>Drive the composed route; returns the parked pin load where
    /// the route parks one (released by the caller after the drive).</summary>
    private static ParkedFetch? DriveComposed(Host host, ComposedCell cell, GateSeam gate, ParkedFetch? parked)
    {
        if (ParkedRoutes.Contains(cell.Route))
        {
            parked = Park(host.Leaf);
        }
        gate.Parked = parked;
        if (cell.Route == Composed.FocusLandsBeforeApply)
        {
            host.Workspace.FocusBoundaryRequested += (_, boundary) =>
            {
                if (boundary == WorkspaceFocusBoundary.RightPane)
                {
                    host.Leaf.FocusEntered();
                }
            };
        }
        switch (cell.Route)
        {
            case Composed.RenamePinFolderInDialog:
            case Composed.DeletePinFolderInDialog:
                Assert.True(host.Workspace.ReRootConnectionsOn(Deep), $"{cell}: the funnel refused");
                break;
            case Composed.BackWithReservedRenamed:
                Assert.False(host.Workspace.ConnectionsBack(), $"{cell}: Back popped over a rewritten top");
                break;
            case Composed.ReentrantBackInDialog:
                // The inner Back popped; the outer finds nothing left to pop.
                Assert.False(host.Workspace.ConnectionsBack(), $"{cell}: the outer Back popped too");
                break;
            case Composed.PopLoadFails:
            case Composed.CanvasPriorPinRestored:
            case Composed.BasePriorPinRestored:
                Assert.True(host.Workspace.ConnectionsBack(), $"{cell}: Back popped nothing");
                break;
            case Composed.ColdGraphTabSource:
            case Composed.WarmGraphTabSource:
                {
                    GraphDocumentViewModel document = host.Workspace.GraphDocument!;
                    GraphTableRow row = document.Publication.Rows.First(candidate => string.Equals(candidate.Path, ReRootTarget, StringComparison.Ordinal));
                    document.Execute(GraphRowAction.ShowConnections, row);
                    break;
                }
            case Composed.RenamePinGraphAlive:
            case Composed.RenamePinGraphAbsent:
            case Composed.RenamePinGraphReseated:
                {
                    Cell modeCell = ModeCellOf(cell);
                    string pin = PinBefore(modeCell);
                    host.Session.RenameFile(pin, RenamedPinName);
                    host.Workspace.RetargetPath(pin, RenamedPin(modeCell));
                    ProbeAfterTheMove(host, modeCell);
                    break;
                }
            case Composed.RetirementInDialog:
                Assert.True(host.Workspace.ReRootConnectionsOn(ReRootTarget), $"{cell}: the funnel refused");
                Assert.False(host.Workspace.ConnectionsBack(), $"{cell}: Back reached a retired leaf");
                break;
            case Composed.RenamePinInBackDialog:
            case Composed.DeletePinInBackDialog:
            case Composed.RenameEntryInBackDialog:
            case Composed.DeleteEntryInBackDialog:
            case Composed.PriorLoadCompletesInBackDialog:
            case Composed.TabChangeInBackDialog:
            case Composed.TabCloseInBackDialog:
            case Composed.DepthChangeInBackDialog:
            case Composed.ProbeInBackDialog:
                Assert.True(host.Workspace.ConnectionsBack(), $"{cell}: Back popped nothing");
                break;
            case Composed.ReentrantReRootInBackDialog:
            case Composed.RetirementInBackDialog:
            case Composed.GroupChangeInBackDialog:
                // The top no longer names the installed note, the leaf retired,
                // or the open installed nothing: the outer Back pops nothing.
                Assert.False(host.Workspace.ConnectionsBack(), $"{cell}: the outer Back popped");
                break;
            default:
                Assert.True(host.Workspace.ReRootConnectionsOn(ReRootTarget), $"{cell}: the funnel refused");
                break;
        }
        if (cell.Route == Composed.FocusLandsAfterApply)
        {
            SettleTheDocuments(host);
            host.Leaf.FocusEntered();
        }
        return parked;
    }

    private static IEnumerable<ComposedCell> ComposedCellsOf()
    {
        foreach (Mode mode in Enum.GetValues<Mode>())
        {
            foreach (Composed route in Enum.GetValues<Composed>())
            {
                yield return new ComposedCell(mode, route);
            }
        }
    }

    [Fact]
    public void TheModelOfRuleDDerivesEveryComposedRouteAcrossEveryMode()
    {
        Assert.Equal(ComposedRoutes, Enum.GetValues<Composed>().Length);
        ComposedCell[] cells = [.. ComposedCellsOf()];
        Assert.Equal(ComposedCells, cells.Length);
        string[] only = (Environment.GetEnvironmentVariable("SLATE_MODEL_ONLY") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var failures = new List<string>();
        var unreachable = new List<string>();
        int driven = 0;
        PumpedDispatcher.Run(() =>
        {
            Fixture? fixture = null;
            foreach (ComposedCell cell in cells)
            {
                if (UnreachableComposed(cell) is { } reason)
                {
                    unreachable.Add($"{cell}: {reason}");
                    continue;
                }
                if (only.Length > 0 && !only.All(term => cell.ToString().Contains(term, StringComparison.Ordinal)))
                {
                    continue;
                }
                using GraphVault vault = GraphVault.Copy($"composed-{driven}");
                fixture ??= FixtureOf();
                var gate = new GateSeam();
                using Host host = ReRootHost(vault.Root, gate);
                ComposedDerivation expected = DeriveComposed(cell, fixture);
                driven++;
                int before;
                ParkedFetch? parked = null;
                string? lineBefore = null;
                string? pinLineBefore = null;
                try
                {
                    parked = ArrangeComposed(host, cell, fixture, gate);
                    if (expected.Root is { } rootBefore && SpeaksTheTreeBefore.Contains(cell.Route))
                    {
                        // The tree the parked load fetched: the target's, as it stands
                        // before the dialog's action.
                        lineBefore = LineFor(host, rootBefore, expected.Depth);
                    }
                    if (host.Leaf.Pin is { } pinnedBefore)
                    {
                        // The pin's own line at the route's depth, before the drive
                        // (a prior load over the pin released inside Back's open).
                        pinLineBefore = LineFor(host, pinnedBefore, expected.Depth);
                    }
                    host.Clear();
                    before = host.Loads;
                    parked = DriveComposed(host, cell, gate, parked);
                    parked?.Gate.Set();
                    if (cell.Route is not (Composed.RetirementInDialog or Composed.RetirementInBackDialog))
                    {
                        SettleTheDocuments(host);
                    }
                    else
                    {
                        SettleTheGraph(host);
                    }
                }
                catch (Exception failure) when (failure is Xunit.Sdk.XunitException or InvalidOperationException)
                {
                    parked?.Gate.Set();
                    parked?.Dispose();
                    failures.Add($"{cell}: failed — {failure.Message.ReplaceLineEndings(" ")}");
                    continue;
                }
                int loads = host.Loads - before;
                string[] timeline =
                [
                    .. expected.Timeline.Select(entry => entry switch
                    {
                        LinePlaceholder => lineBefore ?? (expected.Root is { } root ? LineFor(host, root, expected.Depth) : "<no root to report>"),
                        PinLinePlaceholder => pinLineBefore ?? "<no pin before the route>",
                        _ => entry,
                    }),
                ];
                var mismatch = new List<string>();
                if (parked is { TimedOut: true })
                {
                    mismatch.Add("the parked fetch resumed on its own before the route released it");
                }
                if (loads != expected.Loads && loads != expected.LoadsOr)
                {
                    mismatch.Add($"loads {loads}, derived {expected.Loads}{(expected.LoadsOr is { } alternative ? $" or {alternative}" : string.Empty)}");
                }
                if (!TimelinesAgree(host.Timeline, timeline, expected.Unordered))
                {
                    mismatch.Add($"timeline [{string.Join(" | ", host.Timeline)}], derived [{string.Join(" | ", timeline)}]");
                }
                if (!string.Equals(host.Leaf.Root, expected.Root, StringComparison.Ordinal))
                {
                    mismatch.Add($"root {host.Leaf.Root ?? "none"}, derived {expected.Root ?? "none"}");
                }
                // Every route, the retirements included (IPC-15): a retired leaf
                // holds NoNote, nothing in flight, its depth and root retained.
                bool retirement = cell.Route is Composed.RetirementInDialog or Composed.RetirementInBackDialog;
                if (host.Leaf.IsRetired != retirement)
                {
                    mismatch.Add(retirement ? "the leaf survived its retirement" : "the leaf retired");
                }
                if (!retirement && host.Leaf.Root is not null && host.Leaf.IsStale)
                {
                    mismatch.Add("stale after the route");
                }
                if (host.Leaf.InFlight)
                {
                    mismatch.Add("a load still in flight after the settle");
                }
                if (host.Leaf.Root is not null && host.Leaf.Depth != expected.Depth)
                {
                    mismatch.Add($"depth {host.Leaf.Depth}, derived {expected.Depth}");
                }
                ConnectionsLoadState state = expected.State ?? (expected.Root is null ? ConnectionsLoadState.NoNote : LoadedStateOf(host, expected.Root));
                if (host.Leaf.Publication.State != state)
                {
                    mismatch.Add($"state {host.Leaf.Publication.State}, derived {state}");
                }
                if (!string.Equals(host.Leaf.Pin, expected.Mode.Pin, StringComparison.Ordinal))
                {
                    mismatch.Add($"pin {host.Leaf.Pin ?? "FOLLOWING"}, derived {expected.Mode.Pin ?? "FOLLOWING"}");
                }
                if (!string.Equals(host.Leaf.NoteInView, expected.Mode.NoteInView, StringComparison.Ordinal))
                {
                    mismatch.Add($"note in view {host.Leaf.NoteInView ?? "none"}, derived {expected.Mode.NoteInView ?? "none"}");
                }
                if (!host.Leaf.BackStack.SequenceEqual(expected.Mode.Stack))
                {
                    mismatch.Add($"stack [{StackText(host.Leaf.BackStack)}], derived [{StackText(expected.Mode.Stack)}]");
                }
                string? key = host.Workspace.GraphViewStateForTests.SelectedKey;
                if (!string.Equals(key, expected.Mode.Key, StringComparison.Ordinal))
                {
                    mismatch.Add($"key {key ?? "none"}, derived {expected.Mode.Key ?? "none"}");
                }
                if (host.Workspace.ConnectionsMountPendingForTests)
                {
                    mismatch.Add("a mount still pending after the settle");
                }
                string? focus = host.FocusRequests.LastOrDefault();
                if (!string.Equals(focus, expected.Focus, StringComparison.Ordinal))
                {
                    mismatch.Add($"focus {focus ?? "none"}, derived {expected.Focus ?? "none"}");
                }
                if (host.FocusRequests.Contains("editor") != expected.EditorRequested)
                {
                    mismatch.Add(expected.EditorRequested
                        ? "the pane-focus command's editor request never came"
                        : "the editor's focus was requested (IGL-3)");
                }
                if (gate.Asked != expected.Asked)
                {
                    mismatch.Add($"the gate asked {gate.Asked} times, derived {expected.Asked}");
                }
                if (mismatch.Count > 0)
                {
                    string tabs = $" — tabs [{string.Join(", ", host.Workspace.Groups.SelectMany(g => g.Tabs).Select(t => t.Item.Kind + ":" + (t.Path ?? t.Title) + (ReferenceEquals(t, host.Workspace.ActiveGroup.ActiveTab) ? "*" : "")))}]";
                    failures.Add($"{cell}: {string.Join("; ", mismatch)}{tabs}");
                }
                parked?.Dispose();
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} of {driven} cells diverge from the model (unreachable {unreachable.Count}):\n{string.Join("\n", failures)}");
        if (only.Length > 0)
        {
            Assert.True(driven > 0, $"the narrowing [{string.Join(", ", only)}] matched no cell");
            return;
        }
        Assert.True(
            unreachable.Count == ComposedUnreachable && driven == ComposedDriven,
            $"the model named {unreachable.Count} cells as not states of the system and drove {driven}; pinned {ComposedUnreachable} and {ComposedDriven}");
    }
}
