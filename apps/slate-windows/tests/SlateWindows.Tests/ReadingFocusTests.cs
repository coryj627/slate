// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>W7-7 PR 8 (#1253, contract R-10): the reading surface is the
/// editor stop for a reading-mode tab, and it takes focus only through the
/// landing the shell asks for. The window's one editor landing
/// (<c>MainWindow.FocusEditorPane</c>) runs for real: the shipped shell is
/// constructed and its content pane rehosted in a shown window so keyboard
/// focus can land (the Move-To facts' shape — no Application and no shown
/// MainWindow, so no placement or recents writes). Requests travel the
/// production routes: the focus funnel (<c>RequestActiveEditorFocus</c> →
/// the window's arbiter → the queued landing) and the F6 ring's
/// <c>TryLand</c>. No production guard is copied here.</summary>
public sealed class ReadingFocusTests
{
    private const string NoteText = "A paragraph the reader is part way through.";
    private const string HeadedNote = "# Reading focus\n\n" + NoteText + "\n";
    private const string ProseOnlyNote = "Just a paragraph.\n\nAnother paragraph.\n";

    /// <summary>The funnel behind the toggle, every open and the dismissal
    /// fallbacks, with the content already merged: focus lands at once and
    /// the reader's seated caret survives it. Before R-10 this landing fell
    /// through the collapsed editor to the TabItem (NVDA record F10).</summary>
    [Fact]
    public void TheEditorLandingSeatsAReadingSurfaceThatShowsItsContent() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        Assert.Contains(NoteText, DocumentText(surface));
        Assert.False(ShowsLoadingNotice(surface));
        surface.CaretPosition = surface.Document.ContentEnd;
        int seated = surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition);
        Assert.True(seated > 2, "the fixture's seat must not be the document start");
        Assert.True(host.Sentinel.Focus());

        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();

        AssertFocused(surface, "the editor landing");
        Assert.Equal(seated, surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition));
        Assert.Contains(NoteText, UiaDocumentText(surface));
    });

    /// <summary>The production order the toggle and a fresh open produce: the
    /// landing is requested while the projection is still in flight. The
    /// surface never takes focus on its loading placeholder (NVDA would read
    /// it and never re-read the note); the F6 ring gets Pending, so it
    /// neither speaks nor moves on; and the merge that brings the content
    /// seats the reader once, at its start, and only then speaks the ring's
    /// line. A prose-only note has no landmarks (the landmark-gated claim
    /// once stranded its readers) and an empty note has no blocks at all:
    /// both still land.</summary>
    [Theory]
    [InlineData(HeadedNote, "Reading focus")]
    [InlineData(ProseOnlyNote, "Just a paragraph.")]
    [InlineData("", "")]
    public void TheEditorLandingIsHeldUntilTheContentArrives(string note, string opening) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true, note);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface);
        int landings = 0;
        surface.PreviewGotKeyboardFocus += (_, args) =>
        {
            if (ReferenceEquals(args.NewFocus, surface)) { landings++; }
        };
        int spoken = 0;
        int fellThrough = 0;
        bool focusedWhenSpoken = false;
        Assert.True(host.Sentinel.Focus());

        ShellRegionLanding landing = ((IShellRegionHost)host.Shell).TryLand(
            ShellRegionKind.Editor,
            () =>
            {
                spoken++;
                focusedWhenSpoken = ReferenceEquals(surface, Keyboard.FocusedElement);
            },
            () => fellThrough++);
        PumpedDispatcher.Drain();

        Assert.True(ShowsLoadingNotice(surface));
        Assert.True(landings == 0, $"the surface took focus on its loading placeholder ({landings} landing(s))");
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);
        Assert.True(surface.IsFocusLandingPending);
        Assert.Equal(ShellRegionLanding.Pending, landing);
        Assert.True(spoken == 0, "the ring's line was spoken before focus arrived");

        host.ReleaseProjection();

        AssertFocused(surface, "the held landing");
        Assert.False(surface.IsFocusLandingPending);
        Assert.Equal(1, landings);
        Assert.Equal(1, spoken);
        Assert.Equal(0, fellThrough);
        Assert.True(focusedWhenSpoken, "the held landing spoke before focus arrived");
        Assert.False(ShowsLoadingNotice(surface));
        Assert.StartsWith(opening, new TextRange(surface.CaretPosition, surface.Document.ContentEnd).Text);
        string read = UiaDocumentText(surface);
        Assert.Contains(opening, read);
        Assert.DoesNotContain("Loading reading view", read);
    });

    /// <summary>A held landing is a request, not a claim, and a withdrawn one
    /// neither speaks nor resumes the ring: withdrawn when the reader moves
    /// focus elsewhere before the content applies — including after a
    /// request made while focus sat on an element already on its way out (a
    /// palette-invoked toggle's collapsed search box), and a move already
    /// queued at Input behind the request — and when the view it waits on is
    /// hidden, unloaded or rebound to another projection. Leaving is latched
    /// from the request's own element: coming back does not revive the
    /// landing. Focus that WPF's recovery moved (its origin stopped being
    /// shown) is not the reader's choice, so that landing still lands, and
    /// speaks.</summary>
    [Theory]
    [InlineData("reader moved on", false)]
    [InlineData("reader moved on after a hidden origin", false)]
    [InlineData("an input move queued behind the request", false)]
    [InlineData("left and came back", false)]
    [InlineData("view hidden", false)]
    [InlineData("surface unloaded", false)]
    [InlineData("surface rebound", false)]
    [InlineData("origin hidden", true)]
    public void AHeldLandingLandsOnlyWhileItIsStillWanted(string change, bool lands) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface);
        Assert.True(host.Sentinel.Focus());
        if (change == "reader moved on after a hidden origin")
        {
            // The request runs before WPF's recovery off the collapsed
            // element does, as a palette-invoked toggle's does.
            host.Sentinel.Visibility = Visibility.Collapsed;
        }
        int spoken = 0;
        int fellThrough = 0;
        Assert.Equal(
            ShellRegionLanding.Pending,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
        Assert.True(surface.IsFocusLandingPending);
        if (change == "an input move queued behind the request")
        {
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Input, () => host.Elsewhere.Focus());
        }
        // The work already queued behind the request runs before the reader
        // can act.
        PumpedDispatcher.Drain();

        IInputElement? expected = host.Sentinel;
        switch (change)
        {
            case "an input move queued behind the request":
                Assert.False(surface.IsFocusLandingPending);
                expected = host.Elsewhere;
                break;
            case "left and came back":
                Assert.True(host.Elsewhere.Focus());
                Assert.False(surface.IsFocusLandingPending);
                Assert.True(host.Sentinel.Focus());
                break;
            case "reader moved on":
            case "reader moved on after a hidden origin":
                Assert.True(host.Elsewhere.Focus());
                expected = host.Elsewhere;
                break;
            case "view hidden":
                host.Tab.ToggleViewMode();
                Assert.False(surface.IsVisible);
                Assert.False(surface.IsFocusLandingPending);
                break;
            case "surface unloaded":
                host.DetachPane();
                Assert.False(surface.IsFocusLandingPending);
                break;
            case "surface rebound":
                // Back to the tab's own (merged) projection: the held landing
                // was asked of the one that is leaving.
                surface.Model = host.Tab.Reading;
                Assert.False(surface.IsFocusLandingPending);
                break;
            case "origin hidden":
                host.Sentinel.Visibility = Visibility.Collapsed;
                PumpedDispatcher.Drain();
                Assert.NotSame(host.Sentinel, Keyboard.FocusedElement);
                break;
        }

        host.ReleaseProjection();

        Assert.False(surface.IsFocusLandingPending);
        Assert.True(fellThrough == 0, $"the ring was resumed for a landing that was not refused ({change})");
        if (lands)
        {
            AssertFocused(surface, $"the held landing ({change})");
            Assert.Contains(NoteText, UiaDocumentText(surface));
            Assert.Equal(1, spoken);
        }
        else
        {
            Assert.False(surface.IsKeyboardFocusWithin, $"a withdrawn landing ({change}) still took focus");
            Assert.Same(expected, Keyboard.FocusedElement);
            Assert.True(spoken == 0, $"a withdrawn landing ({change}) was still announced");
        }
    });

    /// <summary>R-10: a cancel lets go of the held landing its token holds and
    /// of nothing a later request holds, and answers whether that landing was
    /// still wanted — not once focus has left where its request found it.</summary>
    [Fact]
    public void ACancelLetsGoOfItsOwnHeldLandingOnly() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface);
        Assert.True(host.Sentinel.Focus());
        Action first = () => { };
        Action second = () => { };
        Action third = () => { };

        Assert.True(surface.RequestFocusLanding(first));
        PumpedDispatcher.Drain();
        Assert.True(surface.RequestFocusLanding(second));
        PumpedDispatcher.Drain();
        Assert.False(surface.CancelFocusLanding(first));
        Assert.True(surface.IsFocusLandingPending);

        Assert.True(surface.CancelFocusLanding(second));
        Assert.False(surface.IsFocusLandingPending);

        Assert.True(surface.RequestFocusLanding(third));
        PumpedDispatcher.Drain();
        Assert.True(host.Elsewhere.Focus());
        Assert.False(surface.CancelFocusLanding(third));
        Assert.False(surface.IsFocusLandingPending);

        host.ReleaseProjection();
        Assert.Same(host.Elsewhere, Keyboard.FocusedElement);
    });

    /// <summary>R-10's refusal path: the content arrives but the surface
    /// cannot take focus. The held landing is refused — it falls through
    /// exactly once (the F6 ring resumes past the editor) and says nothing —
    /// rather than stranding the press on a stop focus never reaches.</summary>
    [Fact]
    public void AHeldLandingThatCannotTakeFocusFallsThrough() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface);
        Assert.True(host.Sentinel.Focus());
        int spoken = 0;
        int fellThrough = 0;
        Assert.Equal(
            ShellRegionLanding.Pending,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
        PumpedDispatcher.Drain();
        surface.Focusable = false;

        host.ReleaseProjection();

        Assert.Equal(1, fellThrough);
        Assert.Equal(0, spoken);
        Assert.False(surface.IsFocusLandingPending);
        Assert.False(surface.IsKeyboardFocusWithin);
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);
    });

    /// <summary>The refusal path when the load fails: only the failure notice
    /// arrives, which is not the note (the failure was announced when it
    /// happened), so the held landing falls through once and says nothing —
    /// and while the notice is shown the surface is no stop at all, so a
    /// later landing is refused at once and the ring moves on by itself.</summary>
    [Fact]
    public void AHeldLandingOnAFailedLoadFallsThrough() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface, failsTerminally: true);
        Assert.True(host.Sentinel.Focus());
        int spoken = 0;
        int fellThrough = 0;
        Assert.Equal(
            ShellRegionLanding.Pending,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
        PumpedDispatcher.Drain();

        host.ReleaseProjection();

        Assert.Contains(surface.Document.Blocks, block =>
            System.Windows.Automation.AutomationProperties.GetAutomationId(block) == "ReadingRefreshFailedNotice");
        Assert.Equal(1, fellThrough);
        Assert.Equal(0, spoken);
        Assert.False(surface.IsFocusLandingPending);
        Assert.False(surface.IsKeyboardFocusWithin);
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);

        Assert.Equal(
            ShellRegionLanding.Refused,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
        Assert.False(surface.IsKeyboardFocusWithin);
        Assert.Equal(1, fellThrough);
        Assert.Equal(0, spoken);
    });

    /// <summary>R-10's ring protocol over the shipped shell's editor arms,
    /// driven by the production F6 ring from the tab bar (the active tab
    /// item): a landing the arm seats inside the request is Landed and spoken
    /// at once; one it holds — a reading projection or a canvas load still
    /// arriving, a graph surface not yet shown — is Pending, and is spoken
    /// only when focus arrives in the surface: exactly once, the ring's own
    /// editor line, never before.</summary>
    [Theory]
    [InlineData("reading", false)]
    [InlineData("reading", true)]
    [InlineData("canvas", false)]
    [InlineData("canvas", true)]
    [InlineData("graph", false)]
    [InlineData("graph", true)]
    public void TheRingSpeaksTheEditorOnlyWhenFocusArrives(string kind, bool later) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        RingHost ring = host.UseRing();
        if (later)
        {
            host.HoldEditorLanding();
        }
        TabItem tabItem = host.FocusTabBar();

        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        RingHost.Attempt attempt = Assert.Single(ring.Attempts);
        Assert.Equal(ShellRegionKind.Editor, attempt.Region);
        Assert.Equal(later ? ShellRegionLanding.Pending : ShellRegionLanding.Landed, attempt.Outcome);
        if (later)
        {
            Assert.Empty(host.Announced);
            AssertFocused(tabItem, "the held editor landing");
            host.LetEditorLandingArrive();
        }
        Assert.True(host.EditorStop().IsKeyboardFocusWithin);
        Assert.Equal([host.EditorLine()], host.Announced);
        Assert.Single(ring.Attempts);
    });

    /// <summary>R-10: an APPLIED projection of a content-empty note is a
    /// landing like any other — never held, never refused. F6 from the tab
    /// bar lands the surface at once, the caret at the document's start, and
    /// the ring speaks the editor line exactly once. What is never focused is
    /// an UNAPPLIED surface (the loading placeholder), not an empty note.</summary>
    [Fact]
    public void AnAppliedEmptyNoteIsLandedAtItsStart() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true, note: string.Empty);
        ReadingSurface surface = host.ShownSurface();
        Assert.False(ShowsLoadingNotice(surface));
        Assert.Equal(string.Empty, DocumentText(surface).Trim());
        RingHost ring = host.UseRing();
        host.FocusTabBar();

        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        RingHost.Attempt attempt = Assert.Single(ring.Attempts);
        Assert.Equal(ShellRegionKind.Editor, attempt.Region);
        Assert.Equal(ShellRegionLanding.Landed, attempt.Outcome);
        AssertFocused(surface, "F6 onto an applied empty note");
        Assert.Equal(0, surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition));
        Assert.False(surface.IsFocusLandingPending);
        Assert.Equal([host.EditorLine()], host.Announced);
    });

    /// <summary>R-10's refusal path through the production ring, from the tab
    /// bar: an editor landing that turns out untakeable — focus refused, only
    /// the load-failure notice arrives, the model torn down before its apply,
    /// the document letting go of the request unseated or torn down, or the
    /// document refusing it at once — resumes the same press past the editor
    /// in its direction: focus lands on the right pane's content exactly once
    /// and its line is spoken once; the editor's never is.</summary>
    [Theory]
    [InlineData("reading", "focus refused")]
    [InlineData("reading", "load failed")]
    [InlineData("reading", "model torn down")]
    [InlineData("canvas", "released unseated")]
    [InlineData("canvas", "document shut down")]
    [InlineData("canvas", "refused at once")]
    [InlineData("graph", "released unseated")]
    public void ARefusedEditorLandingResumesThePressPastIt(string kind, string how) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        RingHost ring = host.UseRing();
        if (how == "refused at once")
        {
            host.Tab.Canvas!.Shutdown();
        }
        else
        {
            host.HoldEditorLanding(failsTerminally: how == "load failed");
        }
        TabItem tabItem = host.FocusTabBar();

        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        if (how != "refused at once")
        {
            Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
            Assert.Empty(host.Announced);
            AssertFocused(tabItem, "the held editor landing");
            switch (how)
            {
                case "focus refused":
                    host.EditorStop().Focusable = false;
                    host.LetEditorLandingArrive();
                    break;
                case "load failed":
                    host.LetEditorLandingArrive();
                    break;
                case "model torn down":
                    host.TearDownProjection();
                    break;
                case "released unseated":
                    host.ReleaseDocumentRequest();
                    break;
                case "document shut down":
                    host.Tab.Canvas!.Shutdown();
                    PumpedDispatcher.Drain();
                    break;
            }
        }

        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], ring.Tried);
        Assert.Equal(
            how == "refused at once" ? ShellRegionLanding.Refused : ShellRegionLanding.Pending,
            ring.Attempts[0].Outcome);
        Assert.Equal(ShellRegionLanding.Landed, ring.Attempts[1].Outcome);
        AssertFocused(host.Elsewhere, $"the resumed press ({how})");
        Assert.Equal([host.RightPaneLine()], host.Announced);
        Assert.False(host.EditorStop().IsKeyboardFocusWithin);
    });

    /// <summary>R-10's repeated press, forward — per arm (the W7-6 ring spec,
    /// §4), the reading, canvas or graph tab active and its content not yet
    /// arrived. The editor's landing is held from either neighbour: F6 from
    /// the tab bar's active tab item, or Shift+F6 from the right pane's
    /// content stop — Pending(Editor), nothing spoken, focus where the press
    /// found it. A second press, F6, CANCELS the held landing and goes on
    /// from the editor's ring position: focus lands on the right pane's
    /// content stop, the region after the editor — one landing, one line,
    /// under the press's own token. The cancelled token, completed afterwards
    /// (the ring's two completions, then the arm's own request or held
    /// landing), changes nothing, and the content arriving later seats
    /// nobody.</summary>
    [Theory]
    [InlineData("reading", "F6 from the tab item")]
    [InlineData("reading", "Shift+F6 from the right pane")]
    [InlineData("canvas", "F6 from the tab item")]
    [InlineData("canvas", "Shift+F6 from the right pane")]
    [InlineData("graph", "F6 from the tab item")]
    [InlineData("graph", "Shift+F6 from the right pane")]
    public void ARepeatedPressMovesOnFromTheHeldEditorLanding(string kind, string approach) =>
        RunSta(() => RepeatedPressWitness(kind, approach, secondBackward: false));

    /// <summary>R-10's repeated press, reversed — per arm, from the same held
    /// editor landing reached from either neighbour: Shift+F6 CANCELS it and
    /// goes back from the editor's ring position, never on in the held
    /// press's direction: focus lands on the tab bar's active tab item, the
    /// region before the editor — one landing, one line. The cancelled token,
    /// completed afterwards, changes nothing, and the content arriving later
    /// seats nobody.</summary>
    [Theory]
    [InlineData("reading", "F6 from the tab item")]
    [InlineData("reading", "Shift+F6 from the right pane")]
    [InlineData("canvas", "F6 from the tab item")]
    [InlineData("canvas", "Shift+F6 from the right pane")]
    [InlineData("graph", "F6 from the tab item")]
    [InlineData("graph", "Shift+F6 from the right pane")]
    public void AReversePressGoesBackFromTheHeldEditorLanding(string kind, string approach) =>
        RunSta(() => RepeatedPressWitness(kind, approach, secondBackward: true));

    private static void RepeatedPressWitness(string kind, string approach, bool secondBackward)
    {
        using var host = new Host();
        host.Initialize(kind);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        TabItem tabItem = host.ActiveTabItem();
        bool approachBackward = approach.StartsWith("Shift+F6", StringComparison.Ordinal);
        UIElement start = approachBackward ? host.Elsewhere : tabItem;
        Assert.True(start.Focus());
        PumpedDispatcher.Drain();
        Assert.Equal(
            approachBackward ? ShellRegionKind.RightPaneContent : ShellRegionKind.TabBar,
            ring.FocusedRegion());

        (approachBackward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();
        RingHost.Attempt stale = Assert.Single(ring.Attempts);
        Assert.Equal(ShellRegionKind.Editor, stale.Region);
        Assert.Equal(ShellRegionLanding.Pending, stale.Outcome);
        Assert.Empty(host.Announced);
        AssertFocused(start, $"the held editor landing ({approach})");
        object? staleRequest = host.EditorLandingRequest();
        Assert.True(kind == "reading" ? host.ShownSurface().IsFocusLandingPending : staleRequest is not null);

        (secondBackward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();

        ShellRegionKind destination = secondBackward ? ShellRegionKind.TabBar : ShellRegionKind.RightPaneContent;
        IInputElement landed = secondBackward ? tabItem : host.Elsewhere;
        A11yEvent line = secondBackward ? host.TabBarLine() : host.RightPaneLine();
        string route = $"{approach}, then {(secondBackward ? "Shift+F6" : "F6")} from the held editor";
        Assert.Equal([ShellRegionKind.Editor, destination], ring.Tried);
        RingHost.Attempt moved = ring.Attempts[1];
        Assert.Equal(ShellRegionLanding.Landed, moved.Outcome);
        Assert.NotSame(stale.Announce, moved.Announce);
        Assert.NotSame(stale.FallThrough, moved.FallThrough);
        AssertFocused(landed, route);
        Assert.Equal([line], host.Announced);
        // The arm let go of the cancelled landing: no request, no hold.
        Assert.Null(host.EditorLandingRequest());
        if (kind == "reading")
        {
            Assert.False(host.ShownSurface().IsFocusLandingPending);
        }

        host.CompleteStaleEditorLanding(stale, staleRequest);
        host.LetEditorLandingArrive();

        Assert.Equal([ShellRegionKind.Editor, destination], ring.Tried);
        Assert.Equal([line], host.Announced);
        AssertFocused(landed, route + ", after the stale completion and the content's arrival");
        Assert.False(host.EditorStop().IsKeyboardFocusWithin);
    }

    /// <summary>R-10: another route asking for the editor while the ring's
    /// landing is held — the funnel behind every open, the toggle and the
    /// dismissal fallbacks — takes the landing over. The ring's is withdrawn
    /// (the document's request replaced, or the surface's held landing asked
    /// again without the ring's line), so when the content arrives and focus
    /// lands there the ring neither speaks nor resumes; and the next F6, the
    /// held landing no longer the ring's, starts from where focus is — the
    /// editor — to the right pane's content, spoken once.</summary>
    [Theory]
    [InlineData("reading")]
    [InlineData("canvas")]
    [InlineData("graph")]
    public void AnotherRoutesLandingTakesTheHeldOneOverSilently(string kind) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);

        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();
        host.LetEditorLandingArrive();

        Assert.True(host.EditorStop().IsKeyboardFocusWithin);
        Assert.Empty(host.Announced);
        Assert.Single(ring.Attempts);

        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], ring.Tried);
        AssertFocused(host.Elsewhere, "F6 from the editor the other route landed");
        Assert.Equal([host.RightPaneLine()], host.Announced);
    });

    /// <summary>R-10: a newer request the document raises in place of the
    /// ring's held canvas landing withdraws it silently, even when the reader
    /// has already put focus in the surface themselves (its filter field,
    /// while the canvas is still loading — a move into the landing's own
    /// target, which is no departure): the ring's line belongs to its own
    /// landing, never to another's.</summary>
    [Fact]
    public void AReplacedDocumentLandingIsSilentEvenWithFocusInTheSurface() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("canvas");
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
        CanvasSurfaceView surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        Assert.True(surface.FilterFieldForTests.Focus());
        PumpedDispatcher.Drain();
        object? held = host.EditorLandingRequest();
        Assert.NotNull(held);

        // The document raises a landing of its own in the ring's place.
        host.Tab.Canvas!.RequestFocusLanding(host.Tab);
        PumpedDispatcher.Drain();

        Assert.NotSame(held, host.EditorLandingRequest());
        Assert.Empty(host.Announced);
        Assert.Single(ring.Attempts);
        Assert.True(surface.IsKeyboardFocusWithin);
    });

    /// <summary>R-10: a canvas or graph landing held for a tab the reader then
    /// switches away from is withdrawn with it — the tab's shared cell
    /// rebinds to the other tab — so its request is released, the content
    /// arriving later seats nobody, and no line is ever spoken for it. Back
    /// on the tab, F6 is a fresh press from the tab bar: the editor, landed
    /// at once and spoken once.</summary>
    [Theory]
    [InlineData("canvas")]
    [InlineData("graph")]
    public void AHeldDocumentLandingIsWithdrawnWhenItsTabIsSwitchedAway(string kind) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false, besideTextTab: true, documentKind: kind);
        WorkspaceTabViewModel other = host.Other!;
        host.Activate(host.Tab);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
        Assert.NotNull(host.EditorLandingRequest());

        host.Activate(other);

        Assert.Null(host.EditorLandingRequest());
        Assert.Single(ring.Attempts);
        Assert.Equal([host.TabLine(other)], host.Announced);

        host.LetEditorLandingArrive();
        host.Activate(host.Tab);

        Assert.False(host.EditorStop().IsKeyboardFocusWithin);
        Assert.Single(ring.Attempts);
        Assert.Equal([host.TabLine(other), host.TabLine(host.Tab)], host.Announced);

        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.Editor], ring.Tried);
        Assert.Equal(ShellRegionLanding.Landed, ring.Attempts[1].Outcome);
        Assert.True(host.EditorStop().IsKeyboardFocusWithin);
        Assert.Equal([host.TabLine(other), host.TabLine(host.Tab), host.EditorLine()], host.Announced);
    });

    /// <summary>R-10: readiness is the CURRENT projection, never any earlier
    /// merge. A reader who left reading mode, edited the note elsewhere and
    /// came back finds the surface still showing the old projection while the
    /// refresh their return started is in flight (production scheduling: the
    /// fetch runs on the pool): the landing holds for that refresh — never
    /// seated on text it is about to replace — and settles with it: on the
    /// edited note, the line spoken over the new text (through a retry, when
    /// the note changed again under the fetch); on the unchanged note, the
    /// reader's caret kept; and a refresh that fails is a refusal — and so is
    /// every later landing while the content it kept is shown.</summary>
    [Theory]
    [InlineData("edited")]
    [InlineData("edited while it fetched")]
    [InlineData("unchanged")]
    [InlineData("failed")]
    public void AHeldLandingWaitsForTheRefreshInFlight(string refresh) => RunSta(() =>
    {
        const string Edited = "An edited paragraph the reader has not heard yet.";
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        ReadingContentViewModel model = host.BindProjectionInFlight(surface);
        host.ReleaseProjection();
        Assert.Contains(NoteText, UiaDocumentText(surface));
        surface.CaretPosition = surface.Document.ContentEnd;
        int seated = surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition);
        Assert.True(seated > 2, "the fixture's seat must not be the document start");

        // Out of reading mode (the model stops observing the buffer), an
        // edit, and back: the return refreshes, and the fetch is held.
        model.Deactivate();
        if (refresh == "edited")
        {
            host.Tab.Text = "# Reading focus\n\n" + Edited + "\n";
        }
        host.HoldNextFetch(failsTerminally: refresh == "failed");
        model.Activate();
        Assert.True(model.RefreshInFlight);
        Assert.True(host.Sentinel.Focus());
        int spoken = 0;
        int fellThrough = 0;
        string? readWhenSpoken = null;

        ShellRegionLanding landing = ((IShellRegionHost)host.Shell).TryLand(
            ShellRegionKind.Editor,
            () =>
            {
                spoken++;
                readWhenSpoken = UiaDocumentText(surface);
            },
            () => fellThrough++);
        PumpedDispatcher.Drain();

        Assert.Equal(ShellRegionLanding.Pending, landing);
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);
        Assert.True(surface.IsFocusLandingPending);
        Assert.Equal(0, spoken);
        if (refresh == "edited while it fetched")
        {
            // The note changes under the fetch: its publish finds the tuple
            // drifted and refreshes again, and the landing waits through that
            // retry instead of landing on the projection the edit obsoleted.
            model.Deactivate();
            host.Tab.Text = "# Reading focus\n\n" + Edited + "\n";
            host.HoldNextFetch();
            host.ReleaseProjection();
            Assert.True(model.RefreshInFlight);
            Assert.True(surface.IsFocusLandingPending);
            Assert.Same(host.Sentinel, Keyboard.FocusedElement);
            Assert.Equal(0, spoken);
        }

        host.ReleaseProjection();
        // A vault-wide save (the edit's own, say) can drift the tuple again
        // and cost one more retry; the landing waits through every one.
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !surface.IsFocusLandingPending),
            $"the landing held for the refresh never settled ({refresh})");
        if (refresh == "failed")
        {
            Assert.Equal(1, fellThrough);
            Assert.Equal(0, spoken);
            Assert.Same(host.Sentinel, Keyboard.FocusedElement);
            // What stays on screen is the failed refresh's stale projection:
            // no stop at all, so a later landing is refused at once.
            Assert.Equal(
                ShellRegionLanding.Refused,
                ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
            Assert.False(surface.IsKeyboardFocusWithin);
            Assert.Equal(1, fellThrough);
            Assert.Equal(0, spoken);
            return;
        }

        AssertFocused(surface, $"the landing held for the refresh ({refresh})");
        Assert.Equal(1, spoken);
        Assert.Equal(0, fellThrough);
        if (refresh.StartsWith("edited", StringComparison.Ordinal))
        {
            Assert.Contains(Edited, readWhenSpoken);
            Assert.DoesNotContain(NoteText, readWhenSpoken);
        }
        else
        {
            Assert.Contains(NoteText, readWhenSpoken);
            Assert.Equal(seated, surface.Document.ContentStart.GetOffsetToPosition(surface.CaretPosition));
        }
    });

    /// <summary>R-10: a route that activates another pane — here a
    /// directional pane move, through the one editor-focus funnel every open,
    /// tab switch and pane move takes — withdraws the landing the F6 ring
    /// holds in the pane it leaves, synchronously: even when that pane's
    /// content arrives AHEAD of the new pane's queued landing, nothing seats
    /// in the old pane, the new pane stays active and takes the keys, and the
    /// ring never speaks. (A graph pane needs no witness: an inactive group's
    /// graph never takes the keys, Term F2.)</summary>
    [Theory]
    [InlineData("reading")]
    [InlineData("canvas")]
    public void APaneMoveWithdrawsTheHeldLandingInThePaneItLeaves(string kind) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        host.Workspace.OpenPath("other.md", WorkspaceOpenTarget.SplitRight);
        host.Settle();
        WorkspaceGroupViewModel paneB = host.Workspace.ActiveGroup;
        WorkspaceTabViewModel otherTab = paneB.ActiveTab!;
        Assert.True(host.Workspace.FocusDirectionalPane("horizontal", -1));
        host.Settle();
        Assert.NotSame(paneB, host.Workspace.ActiveGroup);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);

        Assert.True(host.Workspace.FocusDirectionalPane("horizontal", +1));
        host.LetEditorLandingArriveAhead();
        PumpedDispatcher.Drain();

        Assert.Same(paneB, host.Workspace.ActiveGroup);
        Assert.False(host.EditorStop().IsKeyboardFocusWithin);
        AssertFocused(host.ShownEditor(otherTab).TextArea, "the pane move");
        // The pane move's own line, once; nothing from the ring. (The move
        // also re-derives the right pane's leaves, which speak for
        // themselves.)
        Assert.Equal(
            [new A11yEvent.EditorPaneFocused(2, 2, otherTab.Title, string.Empty)],
            host.Announced.OfType<A11yEvent.EditorPaneFocused>());
        Assert.Single(ring.Attempts);
        Assert.Null(host.EditorLandingRequest());
    });

    /// <summary>R-10 (W7-6 §4's modal rule): a modal surface opening — the
    /// command palette, or a sheet — withdraws the landing the F6 ring holds,
    /// synchronously, for every asynchronous editor arm: its content arriving
    /// under the modal seats nothing beneath it and speaks nothing through
    /// it.</summary>
    [Theory]
    [InlineData("reading", "palette")]
    [InlineData("reading", "sheet")]
    [InlineData("canvas", "palette")]
    [InlineData("canvas", "sheet")]
    [InlineData("graph", "palette")]
    [InlineData("graph", "sheet")]
    public void AModalSurfaceOpeningWithdrawsTheHeldLanding(string kind, string modal) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        TabItem tabItem = host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);

        host.OpenModal(modal);

        Assert.False(host.Workspace.HoldsShellRegionLanding);
        Assert.Null(host.EditorLandingRequest());
        if (kind == "reading")
        {
            Assert.False(host.ShownSurface().IsFocusLandingPending);
        }

        host.LetEditorLandingArrive();

        Assert.False(host.EditorStop().IsKeyboardFocusWithin);
        Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);
        Assert.Single(ring.Attempts);
        AssertFocused(tabItem, $"the late completion under the {modal}");
    });

    /// <summary>R-10: the held region is the ring's position only while the
    /// reader is exactly where the held press left them. Held from the right
    /// pane's content stop, then moved to its OTHER stop — one region, two
    /// focusable controls — the landing is withdrawn with that move, and the
    /// next press starts from the live position: F6 goes on past the right
    /// pane (to the Files tree, the ring's next stop that can take focus
    /// here), Shift+F6 goes back to the editor and asks it again — never the
    /// right pane or the tab bar a restart from the held editor would reach.</summary>
    [Theory]
    [InlineData("reading", false)]
    [InlineData("reading", true)]
    [InlineData("canvas", false)]
    [InlineData("canvas", true)]
    public void ARepeatedPressAfterAMoveWithinTheRegionStartsFromFocus(string kind, bool backward) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        Assert.True(host.Elsewhere.Focus());
        host.Workspace.FocusPreviousPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);

        Assert.True(host.ElsewhereToo.Focus());
        Assert.Equal(ShellRegionKind.RightPaneContent, ring.FocusedRegion());
        Assert.Null(host.EditorLandingRequest());
        if (kind == "reading")
        {
            Assert.False(host.ShownSurface().IsFocusLandingPending);
        }

        (backward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();

        if (backward)
        {
            Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.Editor], ring.Tried);
            Assert.Equal(ShellRegionLanding.Pending, ring.Attempts[1].Outcome);
            Assert.Empty(host.Announced);
            AssertFocused(host.ElsewhereToo, "Shift+F6 from the moved position");
            return;
        }

        Assert.Equal(
            [ShellRegionKind.Editor, ShellRegionKind.RightPaneRail, ShellRegionKind.StatusBar,
                ShellRegionKind.MenuBar, ShellRegionKind.Files],
            ring.Tried);
        Assert.Equal([new A11yEvent.FilesRegionFocused()], host.Announced);
        AssertFocused(host.Sentinel, "F6 from the moved position");
    });

    /// <summary>R-10: ONE terminal transition per held landing, and
    /// Cancelled and Refused are exclusive. A teardown travels with a move
    /// that cancels the landing — a rebind (a tab navigated in place, or a
    /// tab switch or close) or an unload (a closed pane) — and in EITHER
    /// callback order the landing is Cancelled: the teardown's refusal is
    /// decided once the move has run, finds the landing already ended, and is
    /// ignored. So: the ring never resumes, nothing is spoken, and no stale
    /// callback moves focus — into the editor, or onward to the next region.</summary>
    [Theory]
    [InlineData("reading", "rebind", true)]
    [InlineData("reading", "rebind", false)]
    [InlineData("reading", "unload", true)]
    [InlineData("reading", "unload", false)]
    [InlineData("canvas", "rebind", true)]
    [InlineData("canvas", "rebind", false)]
    [InlineData("canvas", "unload", true)]
    [InlineData("canvas", "unload", false)]
    public void ATeardownAndAMoveCancelTheHeldLandingOnce(string kind, string move, bool tornDownFirst) => RunSta(() =>
    {
        using var host = new Host();
        if (kind == "reading")
        {
            host.Initialize(readingMode: true);
        }
        else
        {
            host.Initialize(readingMode: false, besideTextTab: true, documentKind: kind);
            host.Activate(host.Tab);
        }
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
        ReadingSurface? surface = kind == "reading" ? host.ShownSurface() : null;
        FrameworkElement stop = host.EditorStop();

        void TearDown()
        {
            if (surface is not null)
            {
                host.TearDownProjectionNow();
            }
            else
            {
                host.Tab.Canvas!.Shutdown();
            }
        }

        void Move()
        {
            if (move == "unload")
            {
                host.DetachPaneNow();
            }
            else if (surface is not null)
            {
                surface.Model = host.Tab.Reading;
            }
            else
            {
                host.Workspace.ActiveGroup.ActiveTab = host.Other;
            }
        }

        if (tornDownFirst)
        {
            TearDown();
            Move();
        }
        else
        {
            Move();
            TearDown();
        }
        IInputElement? settled = Keyboard.FocusedElement;
        PumpedDispatcher.Drain();
        PumpedDispatcher.Drain();

        Assert.Single(ring.Attempts);
        Assert.DoesNotContain(
            host.Announced, line => line is A11yEvent.EditorPaneFocused or A11yEvent.LeafPanelShown);
        Assert.Null(host.EditorLandingRequest());
        Assert.False(surface?.IsFocusLandingPending ?? false);
        Assert.False(stop.IsKeyboardFocusWithin);
        Assert.NotSame(host.Elsewhere, Keyboard.FocusedElement);
        if (move == "rebind")
        {
            // Nothing moved focus after the two callbacks ran.
            Assert.Same(settled, Keyboard.FocusedElement);
        }
    });

    /// <summary>R-10: a reader who moves INTO the held reading surface while
    /// its content arrives (a move into the landing's own target is followed,
    /// not a departure) has landed: when the content settles the ring's line
    /// is spoken, exactly once, and the press is complete.</summary>
    [Fact]
    public void AMoveIntoTheHeldSurfaceIsTheLanding() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        RingHost ring = host.UseRing();
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
        ReadingSurface surface = host.ShownSurface();

        Assert.True(surface.Focus());
        PumpedDispatcher.Drain();
        Assert.True(surface.IsFocusLandingPending);
        Assert.Empty(host.Announced);

        host.ReleaseProjection();

        Assert.False(surface.IsFocusLandingPending);
        Assert.True(surface.IsKeyboardFocusWithin);
        Assert.Equal([host.EditorLine()], host.Announced);
        Assert.False(host.Workspace.HoldsShellRegionLanding);
        Assert.Single(ring.Attempts);
    });

    /// <summary>R-10: a note longer than one build chunk streams, and a later
    /// chunk can still fail after the first is on screen. The landing held
    /// for that refresh waits for the WHOLE projection, so the failure
    /// refuses it: no focus, no line, and the ring resumed exactly once.</summary>
    [Fact]
    public void ALaterChunkFailingRefusesTheHeldLanding() => RunSta(() =>
    {
        string longNote = string.Join(
            "\n\n",
            Enumerable.Range(1, ReadingContentViewModel.BuildChunkBlocks + 50).Select(i => $"Paragraph {i}."));
        using var host = new Host();
        host.Initialize(readingMode: true, longNote);
        ReadingSurface surface = host.ShownSurface();
        ReadingContentViewModel model = host.BindProjectionInFlight(surface);
        int publishSteps = 0;
        model.PublishFaultForTests = () =>
            ++publishSteps >= 2 ? new InvalidOperationException("A later chunk fails.") : null;
        Assert.True(host.Sentinel.Focus());
        int spoken = 0;
        int fellThrough = 0;
        Assert.Equal(
            ShellRegionLanding.Pending,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
        PumpedDispatcher.Drain();

        host.ReleaseProjection();
        Assert.True(PumpedDispatcher.PumpUntil(() => !surface.IsFocusLandingPending), "the held landing never settled");
        PumpedDispatcher.Drain();

        Assert.True(publishSteps >= 2, "the fixture's note did not stream");
        Assert.Equal(1, fellThrough);
        Assert.Equal(0, spoken);
        Assert.False(surface.IsKeyboardFocusWithin);
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);
    });

    /// <summary>R-10: a graph whose load is in flight seats a SHELL landing
    /// provisionally — the keys go into its surface while the request stays
    /// live for the terminal delivery to re-seat. That is not the landing:
    /// the ring holds it (Pending, nothing spoken), and the next F6 cancels
    /// it — the request released — and goes on from the editor, so the
    /// load's terminal publication cannot pull focus back from the region
    /// the reader moved to.</summary>
    [Fact]
    public void AnInFlightGraphsProvisionalSeatIsNotTheLanding() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("graph");
        RingHost ring = host.UseRing();
        GraphDocumentViewModel graph = host.Tab.Graph!;
        using var computing = new ManualResetEventSlim(false);
        graph.BeforeComputeForTests = () => computing.Wait(TimeSpan.FromSeconds(30));
        try
        {
            Assert.True(graph.Request(new GraphRequest.Needle()));
            Assert.True(graph.IsRequestInFlight);
            host.FocusTabBar();

            host.Workspace.FocusNextPaneCommand.Execute(null);
            PumpedDispatcher.Drain();

            Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
            Assert.True(host.EditorStop().IsKeyboardFocusWithin, "the in-flight graph gave no provisional seat");
            Assert.NotNull(host.EditorLandingRequest());
            Assert.Empty(host.Announced);

            host.Workspace.FocusNextPaneCommand.Execute(null);
            PumpedDispatcher.Drain();

            Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], ring.Tried);
            AssertFocused(host.Elsewhere, "F6 on from the provisional seat");
            Assert.Null(host.EditorLandingRequest());
            Assert.Equal([host.RightPaneLine()], host.Announced);
        }
        finally
        {
            computing.Set();
        }

        PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
        Assert.False(graph.IsRequestInFlight);
        AssertFocused(host.Elsewhere, "the graph load's terminal publication");
        Assert.Equal([host.RightPaneLine()], host.Announced);
    });

    /// <summary>R-10's one owner: the surface takes focus only through a
    /// requested landing. Shown over merged content by a flip that asked for
    /// nothing, and merging content while shown, it leaves the reader where
    /// they are — the two claims it used to make on its own (on becoming
    /// visible, and on a merge into an empty document) did not.</summary>
    [Fact]
    public void TheSurfaceNeverTakesFocusOnItsOwn() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false);
        Assert.True(host.Sentinel.Focus());

        host.Tab.ToggleViewMode();
        PumpedDispatcher.Drain();
        ReadingSurface surface = host.ShownSurface();
        Assert.Contains(NoteText, DocumentText(surface));
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);

        var fresh = new ReadingSurface();
        host.Show(fresh);
        fresh.ApplyBuiltDocument(Build(ProseOnlyNote));
        PumpedDispatcher.Drain();
        Assert.True(fresh.IsVisible);
        Assert.Contains("Just a paragraph.", DocumentText(fresh));
        Assert.Same(host.Sentinel, Keyboard.FocusedElement);
    });

    /// <summary>Ctrl+Shift+] and Ctrl+Shift+[ are user tab switches, so they
    /// ask the funnel for the new tab's editor stop: into a reading tab they
    /// land its surface, and back they land the editor rather than the tab
    /// control WPF's recovery seats when the focused surface collapses.</summary>
    [Fact]
    public void CyclingTabsLandsEachTabsEditorStop() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true, besideTextTab: true);
        SlateTextEditor editor = host.ShownEditor(host.Other);
        Assert.True(editor.FocusInputOwner());
        PumpedDispatcher.Drain();

        host.Workspace.PreviousTabCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Same(host.Tab, host.Workspace.ActiveGroup.ActiveTab);
        ReadingSurface surface = host.ShownSurface();
        AssertFocused(surface, "cycling into the reading tab");
        Assert.Contains(NoteText, UiaDocumentText(surface));

        host.Workspace.NextTabCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Same(host.Other, host.Workspace.ActiveGroup.ActiveTab);
        AssertFocused(editor.TextArea, "cycling back to the text tab");
    });

    /// <summary>Ctrl+Shift+E moves focus with the view in both directions:
    /// into the shown surface, and back into the editor rather than wherever
    /// WPF's focus recovery puts it when the focused surface collapses.</summary>
    [Fact]
    public void TheReadingToggleLandsOnTheShownViewInBothDirections() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false);
        SlateTextEditor editor = host.ShownEditor();
        Assert.True(editor.FocusInputOwner());
        PumpedDispatcher.Drain();
        Assert.Same(editor.TextArea, Keyboard.FocusedElement);

        host.Workspace.ToggleReadingModeCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.True(host.Tab.IsReadingMode);
        ReadingSurface surface = host.ShownSurface();
        AssertFocused(surface, "toggling into reading");
        Assert.Contains(NoteText, UiaDocumentText(surface));

        host.Workspace.ToggleReadingModeCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.False(host.Tab.IsReadingMode);
        AssertFocused(editor.TextArea, "toggling back to the editor");
    });

    /// <summary>The request is the workspace's (the toggle is a user gesture,
    /// and the funnel serves only user-initiated moves): one per toggle, both
    /// directions, addressed to the active group.</summary>
    [Fact]
    public void TheReadingToggleRequestsEditorFocusInBothDirections() => RunSta(() =>
    {
        using var fixture = FixtureVault.Create(1, "reading-toggle-request");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken()) { session.ScanInitial(cancel); }
        using var workspace = new WorkspaceViewModel(session, fixture.Root, () => [], _ => { },
            startInteractionBackgroundWork: false,
            preferencesStore: new AppPreferencesStore(Path.Combine(fixture.Root, "preferences.json")));
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab
            ?? throw new InvalidOperationException("The note did not open.");
        var requests = new List<WorkspaceGroupViewModel>();
        workspace.EditorPaneFocusRequested += (_, group) => requests.Add(group);

        workspace.ToggleReadingModeCommand.Execute(null);
        Assert.True(tab.IsReadingMode);
        Assert.Equal([workspace.ActiveGroup], requests);

        workspace.ToggleReadingModeCommand.Execute(null);
        Assert.False(tab.IsReadingMode);
        Assert.Equal([workspace.ActiveGroup, workspace.ActiveGroup], requests);
    });

    private static void AssertFocused(IInputElement expected, string route) =>
        Assert.True(
            ReferenceEquals(expected, Keyboard.FocusedElement),
            $"{route} seated {Describe(Keyboard.FocusedElement)}, not {Describe(expected)}");

    private static bool ShowsLoadingNotice(ReadingSurface surface) =>
        surface.Document.Blocks.Any(block => System.Windows.Automation.AutomationProperties.GetAutomationId(block)
            == "ReadingLoadingNotice");

    private static string DocumentText(ReadingSurface surface) =>
        new TextRange(surface.Document.ContentStart, surface.Document.ContentEnd).Text;

    /// <summary>What a screen reader reads: the surface peer's Text pattern.</summary>
    private static string UiaDocumentText(ReadingSurface surface)
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(surface);
        var text = Assert.IsAssignableFrom<ITextProvider>(peer.GetPattern(PatternInterface.Text));
        return text.DocumentRange.GetText(-1);
    }

    /// <summary>A projection built from source, as the model builds one.</summary>
    private static FlowDocument Build(string source)
    {
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
        var model = new List<(ReadingBlock, ReadingBlockInlines)>();
        for (int i = 0; i < blocks.Length && i < inlines.Length; i++)
        {
            model.Add((blocks[i], inlines[i]));
        }
        return ReadingDocumentBuilder.Build(model).Document;
    }

    private static string Describe(IInputElement? element) => element switch
    {
        null => "nothing",
        FrameworkElement named => $"{named.GetType().Name} '{System.Windows.Automation.AutomationProperties.GetAutomationId(named)}'",
        _ => element.GetType().Name,
    };

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    // Invoke the real state setters, including their notifications and the
    // window's normal subscription management.
    private static void SetProperty(object target, string name, object? value) =>
        (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing state property: {name}"))
        .SetValue(target, value);

    /// <summary>The production ring's region host for the rehosted pane. The
    /// editor goes to the shipped shell's own landing (<c>TryLand</c>); the
    /// tab bar lands the pane's real active tab item; the two regions the
    /// rehosted pane does not carry stand in as the fixture's text boxes — the
    /// Files tree as <see cref="Host.Sentinel"/>, the right pane's content as
    /// <see cref="Host.Elsewhere"/> (its first stop) and <see
    /// cref="Host.ElsewhereToo"/> (another) — so every landing moves real
    /// keyboard focus, and where the ring stands is read from it. The modal
    /// state is the shipped shell's. Every attempt is
    /// recorded with its answer and the token the ring handed it (its two
    /// completions), so a fact can complete a stale one.</summary>
    private sealed class RingHost(Host host) : IShellRegionHost
    {
        public sealed record Attempt(
            ShellRegionKind Region, ShellRegionLanding Outcome, Action Announce, Action FallThrough);

        public List<Attempt> Attempts { get; } = [];

        /// <summary>The regions the ring tried, in order.</summary>
        public ShellRegionKind[] Tried => [.. Attempts.Select(attempt => attempt.Region)];

        public bool ModalSurfaceOpen => ((IShellRegionHost)host.Shell).ModalSurfaceOpen;

        public bool RightPaneHasContentStop => true;

        public string StatusText => string.Empty;

        public ShellRegionKind? FocusedRegion()
        {
            IInputElement? focused = Keyboard.FocusedElement;
            if (ReferenceEquals(focused, host.Sentinel))
            {
                return ShellRegionKind.Files;
            }

            if (ReferenceEquals(focused, host.Elsewhere) || ReferenceEquals(focused, host.ElsewhereToo))
            {
                return ShellRegionKind.RightPaneContent;
            }

            if (focused is TabItem)
            {
                return ShellRegionKind.TabBar;
            }

            return host.EditorStopOrNull() is { IsKeyboardFocusWithin: true } ? ShellRegionKind.Editor : null;
        }

        public ShellRegionLanding TryLand(
            ShellRegionKind region, Action announceWhenLanded, Action fallThroughWhenRefused)
        {
            ShellRegionLanding outcome;
            if (region == ShellRegionKind.Editor)
            {
                outcome = ((IShellRegionHost)host.Shell).TryLand(region, announceWhenLanded, fallThroughWhenRefused);
            }
            else
            {
                UIElement? stop = region switch
                {
                    ShellRegionKind.Files => host.Sentinel,
                    ShellRegionKind.TabBar => host.ActiveTabItem(),
                    ShellRegionKind.RightPaneContent => host.Elsewhere,
                    _ => null,
                };
                outcome = stop is not null && stop.Focus() && stop.IsKeyboardFocusWithin
                    ? ShellRegionLanding.Landed
                    : ShellRegionLanding.Refused;
            }

            Attempts.Add(new Attempt(region, outcome, announceWhenLanded, fallThroughWhenRefused));
            return outcome;
        }

        public bool WithdrawHeldLanding() => ((IShellRegionHost)host.Shell).WithdrawHeldLanding();
    }

    private sealed class Host : IDisposable
    {
        private readonly FixtureVault _fixture = FixtureVault.Create(0, "reading-focus");
        private readonly Func<bool> _priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        private readonly List<ManualResetEventSlim> _fetchGates = [new(false)];
        private readonly HashSet<ManualResetEventSlim> _awaitedFetchGates = [];
        private bool _fetchFails;
        private ReadingContentViewModel? _inFlight;
        private readonly TaskCompletionSource _canvasLoadGate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CanvasDocumentViewModel? _loadingCanvas;
        private Window? _window;

        public VaultSession Session { get; private set; } = null!;
        public MainWindow Shell { get; private set; } = null!;
        public VaultLifecycleViewModel Lifecycle { get; private set; } = null!;
        public WorkspaceViewModel Workspace { get; private set; } = null!;
        public WorkspaceTabViewModel Tab { get; private set; } = null!;
        public WorkspaceTabViewModel? Other { get; private set; }
        public TextBox Sentinel { get; private set; } = null!;
        public TextBox Elsewhere { get; private set; } = null!;
        public TextBox ElsewhereToo { get; private set; } = null!;
        public List<A11yEvent> Announced { get; } = [];
        private FrameworkElement? _heldSurface;

        /// <summary>A reading-mode note, a canvas or the graph as the active
        /// tab (<see cref="Tab"/>).</summary>
        public void Initialize(string kind)
        {
            switch (kind)
            {
                case "reading":
                    Initialize(readingMode: true);
                    break;
                case "canvas":
                case "graph":
                    Initialize(readingMode: false, documentKind: kind);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "an editor-stop kind");
            }
        }

        // Separate initialization keeps the using/finally active even when a
        // constructor, resource load, or focus assertion fails partway through.
        public void Initialize(
            bool readingMode, string note = HeadedNote, bool besideTextTab = false, string? documentKind = null)
        {
            Assert.Null(Application.Current);
            File.WriteAllText(Path.Combine(_fixture.Root, "note.md"), note);
            File.WriteAllText(Path.Combine(_fixture.Root, "other.md"), "# Other\n\nA text tab beside the reading one.\n");
            File.WriteAllText(Path.Combine(_fixture.Root, "board.canvas"), "{\"nodes\":[],\"edges\":[]}");
            Session = VaultSession.OpenFilesystem(_fixture.Root);
            using (var cancel = new CancelToken()) { Session.ScanInitial(cancel); }
            Workspace = new WorkspaceViewModel(Session, _fixture.Root, () => [], Announced.Add,
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(_fixture.Root, "preferences.json")));
            switch (documentKind)
            {
                case "canvas":
                    Workspace.OpenPath("board.canvas");
                    break;
                case "graph":
                    Workspace.OpenGraph();
                    PumpedDispatcher.PumpUntilDrained(Workspace.GraphDocument!.WhenAllWorkDrained());
                    break;
                default:
                    Workspace.OpenPath("note.md");
                    break;
            }
            Tab = Workspace.ActiveGroup.ActiveTab
                ?? throw new InvalidOperationException("The tab did not open.");
            if (readingMode)
            {
                // The tab's own flip (the create-from-template normalization
                // uses it too): the mode changes and nothing asks for focus.
                Tab.ToggleViewMode();
                Assert.True(Tab.IsReadingMode);
            }
            if (besideTextTab)
            {
                Workspace.OpenPath("other.md", WorkspaceOpenTarget.NewTab);
                Other = Workspace.ActiveGroup.ActiveTab;
                Assert.NotSame(Tab, Other);
            }

            Shell = new MainWindow();
            Lifecycle = Assert.IsType<VaultLifecycleViewModel>(Shell.DataContext);
            SetProperty(Lifecycle, nameof(VaultLifecycleViewModel.Workspace), Workspace);
            AutomationLandmarkBorder pane = Shell.ContentPaneBorder;
            Assert.IsAssignableFrom<Panel>(pane.Parent).Children.Remove(pane);
            Sentinel = new TextBox { Text = "Focus starts outside the editor pane" };
            Elsewhere = new TextBox { Text = "Somewhere else the reader can go" };
            ElsewhereToo = new TextBox { Text = "And another stop beside it" };
            var content = new DockPanel();
            DockPanel.SetDock(Sentinel, Dock.Top);
            DockPanel.SetDock(Elsewhere, Dock.Top);
            DockPanel.SetDock(ElsewhereToo, Dock.Top);
            content.Children.Add(Sentinel);
            content.Children.Add(Elsewhere);
            content.Children.Add(ElsewhereToo);
            content.Children.Add(pane);
            _window = new Window
            {
                Content = content,
                // The pane's DataContext binds Workspace off the shell's view
                // model, exactly as it did inside the shell.
                DataContext = Lifecycle,
                Width = 900,
                Height = 700,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -2000,
                Top = -2000,
            };
            // The shown editor's highlighter paints from the theme's palette
            // brushes. The window's OWN resources stand in for
            // Application.Resources (MenuItemForegroundTests' shape): the
            // class constructor registers the pack: scheme without an
            // Application.
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
                typeof(Application).TypeHandle);
            _window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/SlateWindows;component/Themes/Slate.Light.xaml",
                    UriKind.Absolute),
            });
            // The shell's own resources too: a split builds its panes from
            // templates the pane looks up dynamically, which it found in the
            // shell before the rehost.
            _window.Resources.MergedDictionaries.Add(Shell.Resources);
            _window.Show();
            _window.Activate();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
            if (Workspace.GraphDocument is { } graph)
            {
                PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
                PumpedDispatcher.Drain();
            }
        }

        /// <summary>The production ring, over <see cref="RingHost"/>.</summary>
        public RingHost UseRing()
        {
            var ring = new RingHost(this);
            Workspace.ShellRegionHost = ring;
            Announced.Clear();
            return ring;
        }

        /// <summary>The fixture tab's editor stop: its reading surface, or its
        /// canvas or graph surface (found shown or not).</summary>
        public FrameworkElement EditorStop() =>
            EditorStopOrNull() ?? throw new InvalidOperationException("The tab's editor stop is not in the pane.");

        /// <summary>The stop, or null while the pane shows another tab.</summary>
        public FrameworkElement? EditorStopOrNull() => Tab switch
        {
            { IsReadingMode: true } => Descendants<ReadingSurface>(Shell.ContentPaneBorder)
                .SingleOrDefault(surface => ReferenceEquals(surface.DataContext, Tab) && surface.IsVisible),
            { IsCanvas: true } => Descendants<CanvasSurfaceView>(Shell.ContentPaneBorder)
                .SingleOrDefault(surface => ReferenceEquals(surface.DataContext, Tab)),
            _ => Descendants<GraphSurfaceView>(Shell.ContentPaneBorder)
                .SingleOrDefault(surface => ReferenceEquals(surface.DataContext, Tab)),
        };

        /// <summary>The tab bar's stop: the pane's real tab item for the
        /// active tab.</summary>
        public TabItem ActiveTabItem()
        {
            WorkspaceGroupViewModel group = Workspace.ActiveGroup;
            TabControl tabs = Assert.Single(
                Descendants<TabControl>(Shell.ContentPaneBorder),
                candidate => ReferenceEquals(candidate.DataContext, group));
            tabs.UpdateLayout();
            return Assert.IsType<TabItem>(tabs.ItemContainerGenerator.ContainerFromItem(group.ActiveTab));
        }

        /// <summary>Focus the tab bar's stop, where a press into the editor
        /// starts.</summary>
        public TabItem FocusTabBar()
        {
            TabItem item = ActiveTabItem();
            Assert.True(item.Focus());
            PumpedDispatcher.Drain();
            return item;
        }

        /// <summary>Lay the pane out and let queued work settle (a split
        /// builds the new pane's views).</summary>
        public void Settle()
        {
            _window!.UpdateLayout();
            PumpedDispatcher.Drain();
            _window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>Make <paramref name="tab"/> the active tab, as the tab
        /// strip does, and let its work settle.</summary>
        public void Activate(WorkspaceTabViewModel tab)
        {
            Workspace.ActiveGroup.ActiveTab = tab;
            _window!.UpdateLayout();
            PumpedDispatcher.Drain();
            if (Workspace.GraphDocument is { } graph)
            {
                PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
                PumpedDispatcher.Drain();
            }
        }

        /// <summary>The ring's tab bar line for the active tab.</summary>
        public A11yEvent TabBarLine()
        {
            WorkspaceGroupViewModel group = Workspace.ActiveGroup;
            WorkspaceTabViewModel tab = group.ActiveTab!;
            return new A11yEvent.TabFocused(
                "Tab bar. ", tab.Title, (uint)(group.Tabs.IndexOf(tab) + 1), (uint)group.Tabs.Count);
        }

        /// <summary>The tab strip's line for activating <paramref name="tab"/>.</summary>
        public A11yEvent TabLine(WorkspaceTabViewModel tab)
        {
            WorkspaceGroupViewModel group = Workspace.ActiveGroup;
            return new A11yEvent.TabFocused(
                string.Empty, tab.Title, (uint)(group.Tabs.IndexOf(tab) + 1), (uint)group.Tabs.Count);
        }

        /// <summary>The ring's right pane content line.</summary>
        public A11yEvent RightPaneLine() => new A11yEvent.LeafPanelShown(Workspace.ActiveLeaf.Title);

        /// <summary>Tear the in-flight projection's model down before it
        /// applies — its own Dispose, as the tab's in-place navigation and
        /// its close do — then let its fetch run out: the disposed model
        /// publishes nothing.</summary>
        public void TearDownProjection()
        {
            TearDownProjectionNow();
            PumpedDispatcher.Drain();
            ReleaseProjection();
        }

        /// <summary>Only the teardown, with nothing pumped after it (the
        /// fixture's cleanup lets the held fetch run out).</summary>
        public void TearDownProjectionNow() => _inFlight!.Dispose();

        /// <summary>Put the editor stop where it will seat focus only LATER,
        /// in the state each arm really waits in: a reading projection still
        /// in flight; a canvas whose load has not published (its surface has
        /// nothing to seat under Loading, and the publish re-asks — contract
        /// A14); a graph surface not shown yet (a graph seats a shell request
        /// provisionally even under a load, Term F3, so what it waits on is
        /// its visibility edge, which re-asks).</summary>
        public void HoldEditorLanding(bool failsTerminally = false)
        {
            if (Tab.IsReadingMode)
            {
                BindProjectionInFlight(ShownSurface(), failsTerminally);
                return;
            }

            if (Tab.IsCanvas)
            {
                BindCanvasLoadInFlight();
                return;
            }

            // The CURRENT value, so the template's visibility binding stays
            // and a tab switch still re-evaluates it.
            _heldSurface = EditorStop();
            _heldSurface.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            PumpedDispatcher.Drain();
        }

        /// <summary>Let what <see cref="HoldEditorLanding"/> held arrive.</summary>
        public void LetEditorLandingArrive()
        {
            if (Tab.IsReadingMode)
            {
                ReleaseProjection();
                return;
            }

            if (_loadingCanvas is { } canvas)
            {
                _canvasLoadGate.SetResult();
                PumpedDispatcher.PumpUntilDrained(canvas.WhenAllWorkDrained());
                PumpedDispatcher.Drain();
                Assert.Equal(CanvasLoadState.Ready, canvas.State);
                return;
            }

            BindingExpression? shown = BindingOperations.GetBindingExpression(
                _heldSurface!, UIElement.VisibilityProperty);
            Assert.NotNull(shown);
            shown.UpdateTarget();
            _window!.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>The canvas's load in flight. The workspace loads
        /// synchronously here (no background host), so the in-flight load is
        /// a production (asynchronous) document for the same tab and path —
        /// attached through the tab's own attach funnel and read into the
        /// tab's surface through the template's own binding, its load parked
        /// at the scheduler's gate until <see cref="LetEditorLandingArrive"/>.</summary>
        private void BindCanvasLoadInFlight()
        {
            CanvasDocumentViewModel opened = Tab.Canvas
                ?? throw new InvalidOperationException("The canvas tab has no document.");
            _loadingCanvas = new CanvasDocumentViewModel(
                Session, opened.Path, new CanvasAnnouncer(_ => { }), synchronousForTests: false);
            _loadingCanvas.GateWorkOn(_canvasLoadGate.Task);
            _loadingCanvas.Load();
            Assert.Equal(CanvasLoadState.Loading, _loadingCanvas.State);
            Tab.AttachCanvasDocument(_loadingCanvas);
            var surface = Assert.IsType<CanvasSurfaceView>(EditorStop());
            BindingExpression? model = BindingOperations.GetBindingExpression(
                surface, CanvasSurfaceView.ModelProperty);
            Assert.NotNull(model);
            model.UpdateTarget();
            Assert.Same(_loadingCanvas, surface.Model);
            PumpedDispatcher.Drain();
        }

        /// <summary>The ring's editor line for this fixture's one pane.</summary>
        public A11yEvent EditorLine() =>
            new A11yEvent.EditorPaneFocused(1, 1, Tab.Title, string.Empty);

        /// <summary>The arm's own token for a canvas or graph landing: the
        /// request its document holds (a reading surface's held landing has
        /// no token but the ring's).</summary>
        public object? EditorLandingRequest() =>
            (object?)Tab.Canvas?.FocusRequest ?? Tab.Graph?.FocusRequest;

        /// <summary>Complete a cancelled press's token AFTER its replacement
        /// was issued: the ring's two completions, then the arm's own — the
        /// canvas's or graph's stale request completed, or the reading
        /// surface asked to let go of the landing that token held.</summary>
        public void CompleteStaleEditorLanding(RingHost.Attempt stale, object? staleRequest)
        {
            stale.Announce();
            stale.FallThrough();
            switch (staleRequest)
            {
                case CanvasFocusRequest canvasRequest:
                    Tab.Canvas!.CompleteFocusLanding(canvasRequest);
                    break;
                case GraphFocusRequest graphRequest:
                    Tab.Graph!.CompleteFocus(graphRequest);
                    break;
                default:
                    _ = ShownSurface().CancelFocusLanding(stale.Announce);
                    break;
            }
            PumpedDispatcher.Drain();
        }

        /// <summary>The document lets go of the shell's request unseated.</summary>
        public void ReleaseDocumentRequest()
        {
            if (Tab.Canvas is { FocusRequest: { } canvasRequest } canvas)
            {
                canvas.CompleteFocusLanding(canvasRequest);
            }
            else if (Tab.Graph is { FocusRequest: { } graphRequest } graph)
            {
                graph.CompleteFocus(graphRequest);
            }
            else
            {
                Assert.Fail("The document holds no request.");
            }
            PumpedDispatcher.Drain();
        }

        /// <summary>Take the content pane out of the window: its surfaces
        /// unload.</summary>
        public void DetachPane()
        {
            DetachPaneNow();
            PumpedDispatcher.Drain();
        }

        /// <summary>Only the removal, with nothing pumped after it (the
        /// surfaces' Unloaded follows on the dispatcher).</summary>
        public void DetachPaneNow()
        {
            var content = (DockPanel)_window!.Content;
            content.Children.Remove(Shell.ContentPaneBorder);
        }

        /// <summary>Show another element in the window, above the pane.</summary>
        public void Show(UIElement element)
        {
            var content = (DockPanel)_window!.Content;
            DockPanel.SetDock(element, Dock.Top);
            content.Children.Insert(2, element);
            _window.UpdateLayout();
        }

        public ReadingSurface ShownSurface() => Assert.Single(
            Descendants<ReadingSurface>(Shell.ContentPaneBorder),
            surface => ReferenceEquals(surface.DataContext, Tab) && surface.IsVisible);

        public SlateTextEditor ShownEditor(WorkspaceTabViewModel? tab = null) => Assert.Single(
            Descendants<SlateTextEditor>(Shell.ContentPaneBorder),
            editor => ReferenceEquals(editor.DataContext, tab ?? Tab) && editor.IsVisible);

        /// <summary>The state the toggle's first projection is in when its
        /// landing is requested: the surface shows the loading placeholder
        /// while the fetch runs on the pool. The tab's workspace projects
        /// synchronously (no background host), so the in-flight projection
        /// is a production (asynchronous) model for the same tab, bound
        /// locally, with its fetch held at the test seam until
        /// <see cref="ReleaseProjection"/>.</summary>
        public ReadingContentViewModel BindProjectionInFlight(ReadingSurface surface, bool failsTerminally = false)
        {
            Volatile.Write(ref _fetchFails, failsTerminally);
            _inFlight = new ReadingContentViewModel(Session, Tab, _ => { })
            {
                FetchFaultForTests = () =>
                {
                    _ = GateForTheFetch().Wait(TimeSpan.FromSeconds(30));
                    return Volatile.Read(ref _fetchFails)
                        ? new InvalidOperationException("The fixture's projection fails.")
                        : null;
                },
            };
            surface.Model = _inFlight;
            PumpedDispatcher.Drain();
            Assert.True(ShowsLoadingNotice(surface));
            return _inFlight;
        }

        /// <summary>Hold the in-flight model's NEXT fetch at a gate of its own,
        /// failing it terminally when asked.</summary>
        public void HoldNextFetch(bool failsTerminally = false)
        {
            Volatile.Write(ref _fetchFails, failsTerminally);
            lock (_fetchGates)
            {
                _fetchGates.Add(new ManualResetEventSlim(false));
            }
        }

        /// <summary>A fetch starting now waits at the newest gate.</summary>
        private ManualResetEventSlim GateForTheFetch()
        {
            lock (_fetchGates)
            {
                ManualResetEventSlim gate = _fetchGates[^1];
                _ = _awaitedFetchGates.Add(gate);
                return gate;
            }
        }

        /// <summary>The oldest gate still held: the one the pending fetch
        /// waits at.</summary>
        private ManualResetEventSlim? FirstHeldFetchGate()
        {
            lock (_fetchGates)
            {
                return _fetchGates.FirstOrDefault(gate => !gate.IsSet);
            }
        }

        /// <summary>Let the held content arrive AHEAD of work already queued
        /// at a lower priority (a funnel landing waits at Input): the reading
        /// fetch and the canvas body run on the pool while this thread waits
        /// unpumped, so their apply is queued first, and the caller's drain
        /// runs it first.</summary>
        public void LetEditorLandingArriveAhead()
        {
            if (Tab.IsReadingMode)
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                DispatcherOperation? posted = null;
                void Posted(object? sender, DispatcherHookEventArgs args)
                {
                    if (!dispatcher.CheckAccess())
                    {
                        Volatile.Write(ref posted, args.Operation);
                    }
                }
                dispatcher.Hooks.OperationPosted += Posted;
                try
                {
                    (FirstHeldFetchGate() ?? throw new InvalidOperationException("No fetch is held.")).Set();
                    Assert.True(
                        SpinWait.SpinUntil(() => Volatile.Read(ref posted) is not null, TimeSpan.FromSeconds(30)),
                        "the reading fetch never posted its publish");
                }
                finally
                {
                    dispatcher.Hooks.OperationPosted -= Posted;
                }
                return;
            }

            CanvasDocumentViewModel canvas = _loadingCanvas
                ?? throw new InvalidOperationException("No canvas load is held.");
            _canvasLoadGate.SetResult();
            Assert.True(canvas.WhenAllWorkDrained().Wait(TimeSpan.FromSeconds(30)), "the canvas load never ran");
        }

        /// <summary>Open a modal surface as the shell's routes do: the command
        /// palette (a lifecycle surface), or the template picker (a workspace
        /// sheet).</summary>
        public void OpenModal(string modal)
        {
            if (modal == "palette")
            {
                SetProperty(Lifecycle, nameof(VaultLifecycleViewModel.IsVaultOpen), true);
                Lifecycle.Palette.Open();
            }
            else
            {
                Workspace.OpenTemplatePicker();
            }
            Assert.NotNull(Shell.OpenModalSurface);
        }

        /// <summary>Open the oldest held gate and pump until the projection's
        /// publish, posted from the pool, has run on this dispatcher.</summary>
        public void ReleaseProjection()
        {
            ManualResetEventSlim gate = FirstHeldFetchGate()
                ?? throw new InvalidOperationException("No fetch is held.");
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherOperation? published = null;
            void Posted(object? sender, DispatcherHookEventArgs args)
            {
                if (!dispatcher.CheckAccess())
                {
                    Volatile.Write(ref published, args.Operation);
                }
            }
            dispatcher.Hooks.OperationPosted += Posted;
            try
            {
                gate.Set();
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => Volatile.Read(ref published)
                        is { Status: DispatcherOperationStatus.Completed or DispatcherOperationStatus.Aborted }),
                    "the in-flight projection never published");
                PumpedDispatcher.Drain();
            }
            finally
            {
                dispatcher.Hooks.OperationPosted -= Posted;
            }
        }

        public void Dispose()
        {
            var failures = new List<Exception>();
            void CleanUp(Action action)
            {
                try { action(); }
                catch (Exception exception) { failures.Add(exception); }
            }
            try
            {
                // A fact that failed before releasing its projection must not
                // leave the pool blocked on a gate, or fetching after the
                // session below is gone.
                while (_inFlight is not null && FirstHeldFetchGate() is { } held)
                {
                    bool awaited;
                    lock (_fetchGates)
                    {
                        awaited = _awaitedFetchGates.Contains(held);
                    }
                    if (awaited)
                    {
                        CleanUp(ReleaseProjection);
                    }
                    else
                    {
                        held.Set();
                    }
                }
                CleanUp(() => _inFlight?.Dispose());
                if (_loadingCanvas is { } canvas)
                {
                    // Retire the parked load before the session goes: its
                    // body is refused once the gate opens, and whatever the
                    // document opened is closed.
                    CleanUp(canvas.Shutdown);
                    CleanUp(() => _canvasLoadGate.TrySetResult());
                    CleanUp(() => PumpedDispatcher.PumpUntilDrained(canvas.WhenHandleClosed()));
                }
                if (Lifecycle is not null)
                {
                    CleanUp(() => SetProperty(Lifecycle, nameof(VaultLifecycleViewModel.Workspace), null));
                }
                CleanUp(() => _window?.Close());
                CleanUp(() => Workspace?.Dispose());
                CleanUp(() => Shell?.Close());
                CleanUp(() => Session?.Dispose());
                CleanUp(_fixture.Dispose);
                foreach (ManualResetEventSlim gate in _fetchGates)
                {
                    CleanUp(gate.Dispose);
                }
            }
            finally { CanvasSurfaceView.ShellOverlayIsOpen = _priorOverlayProbe; }
            if (failures.Count > 0) { throw new AggregateException("Reading focus fixture cleanup failed.", failures); }
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { PumpedDispatcher.Run(body); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "Reading focus fixture timed out.");
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }
}
