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

    /// <summary>A board with cards: the Visual projection renders only a
    /// board that has some (an empty one shows its onboarding instead).</summary>
    private const string CardBoard =
        "{\"nodes\":["
        + "{\"id\":\"alpha\",\"type\":\"text\",\"text\":\"Alpha card\",\"x\":0,\"y\":0,\"width\":240,\"height\":120},"
        + "{\"id\":\"beta\",\"type\":\"text\",\"text\":\"Beta card\",\"x\":0,\"y\":160,\"width\":240,\"height\":120}"
        + "],\"edges\":[]}";

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
    /// still wanted — not once focus has left where its request found it: the
    /// window's one slot (OD-12) has cancelled it by then, and a withdrawal
    /// finds nothing held.</summary>
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
        object firstToken = surface.HeldFocusLanding!;
        PumpedDispatcher.Drain();
        Assert.True(surface.RequestFocusLanding(second));
        object secondToken = surface.HeldFocusLanding!;
        PumpedDispatcher.Drain();
        Assert.NotSame(firstToken, secondToken);
        Assert.False(surface.CancelFocusLanding(firstToken));
        Assert.True(surface.IsFocusLandingPending);

        Assert.True(surface.CancelFocusLanding(secondToken));
        Assert.False(surface.IsFocusLandingPending);
        Assert.Null(surface.HeldFocusLanding);

        Assert.Equal(
            ShellRegionLanding.Pending,
            ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, third, () => { }));
        object thirdToken = surface.HeldFocusLanding!;
        PumpedDispatcher.Drain();
        Assert.True(host.Elsewhere.Focus());
        Assert.False(surface.CancelFocusLanding(thirdToken));
        Assert.False(surface.IsFocusLandingPending);
        Assert.False(((IShellRegionHost)host.Shell).WithdrawHeldLanding());

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
    /// arrived, and for each shape of the right pane: showing a content stop,
    /// showing only its rail, or hidden (so the region after the editor is the
    /// content stop, the rail, or the status bar). The editor's landing is
    /// held from either neighbour: F6 from the tab bar's active tab item, or
    /// Shift+F6 from the region after the editor — Pending(Editor), nothing
    /// spoken, focus where the press found it. A second press, F6, CANCELS the
    /// held landing and goes on from the editor's ring position: focus lands on
    /// the region after the editor — one landing, one line, under the press's
    /// own token. The cancelled token, completed afterwards (the ring's two
    /// completions, then the arm's own request or held landing), changes
    /// nothing — the canvas's seated card and the graph's node included — and
    /// the content arriving later seats nobody.</summary>
    [Theory]
    [MemberData(nameof(RepeatedPressShapes))]
    public void ARepeatedPressMovesOnFromTheHeldEditorLanding(string kind, string approach, string rightPane) =>
        RunSta(() => RepeatedPressWitness(kind, approach, rightPane, secondBackward: false));

    /// <summary>R-10's repeated press, reversed — per arm and per right-pane
    /// shape, from the same held editor landing reached from either neighbour:
    /// Shift+F6 CANCELS it and goes back from the editor's ring position, never
    /// on in the held press's direction: focus lands on the tab bar's active
    /// tab item, the region before the editor — one landing, one line. The
    /// cancelled token, completed afterwards, changes nothing, and the content
    /// arriving later seats nobody.</summary>
    [Theory]
    [MemberData(nameof(RepeatedPressShapes))]
    public void AReversePressGoesBackFromTheHeldEditorLanding(string kind, string approach, string rightPane) =>
        RunSta(() => RepeatedPressWitness(kind, approach, rightPane, secondBackward: true));

    public static TheoryData<string, string, string> RepeatedPressShapes()
    {
        var shapes = new TheoryData<string, string, string>();
        foreach (string kind in new[] { "reading", "canvas", "graph" })
        {
            foreach (string approach in new[] { "F6 from the tab item", "Shift+F6 from the region after the editor" })
            {
                foreach (string rightPane in new[] { "with a content stop", "with no content stop", "hidden" })
                {
                    shapes.Add(kind, approach, rightPane);
                }
            }
        }
        return shapes;
    }

    private static void RepeatedPressWitness(string kind, string approach, string rightPane, bool secondBackward)
    {
        using var host = new Host();
        // A canvas with cards: a landing wrongly delivered would seat one.
        host.Initialize(kind, kind == "canvas" ? CardBoard : null);
        if (rightPane == "hidden")
        {
            host.Workspace.IsRightPaneVisible = false;
            host.Settle();
        }
        RingHost ring = host.UseRing();
        ring.RightPaneHasContentStop = rightPane == "with a content stop";
        (ShellRegionKind after, UIElement afterStop, A11yEvent afterLine) = rightPane switch
        {
            "with a content stop" => (ShellRegionKind.RightPaneContent, (UIElement)host.Elsewhere, host.RightPaneLine()),
            "with no content stop" => (ShellRegionKind.RightPaneRail, host.Rail, new A11yEvent.ShellRegionFocused(new ShellRegion.RightPaneRail())),
            _ => (ShellRegionKind.StatusBar, host.Status, new A11yEvent.ShellRegionFocused(new ShellRegion.StatusBar(string.Empty))),
        };
        host.HoldEditorLanding();
        TabItem tabItem = host.ActiveTabItem();
        bool approachBackward = approach.StartsWith("Shift+F6", StringComparison.Ordinal);
        UIElement start = approachBackward ? afterStop : tabItem;
        Assert.True(start.Focus());
        PumpedDispatcher.Drain();
        Assert.Equal(approachBackward ? after : ShellRegionKind.TabBar, ring.FocusedRegion());

        (approachBackward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();
        RingHost.Attempt stale = Assert.Single(ring.Attempts);
        Assert.Equal(ShellRegionKind.Editor, stale.Region);
        Assert.Equal(ShellRegionLanding.Pending, stale.Outcome);
        Assert.Empty(host.Announced);
        AssertFocused(start, $"the held editor landing ({approach}, the right pane {rightPane})");
        object? staleRequest = host.EditorLandingRequest();
        Assert.NotNull(staleRequest);

        (secondBackward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();

        ShellRegionKind destination = secondBackward ? ShellRegionKind.TabBar : after;
        IInputElement landed = secondBackward ? tabItem : afterStop;
        A11yEvent line = secondBackward ? host.TabBarLine() : afterLine;
        string route = $"{approach}, the right pane {rightPane}, then {(secondBackward ? "Shift+F6" : "F6")} from the held editor";
        Assert.Equal([ShellRegionKind.Editor, destination], ring.Tried);
        RingHost.Attempt moved = ring.Attempts[1];
        Assert.Equal(ShellRegionLanding.Landed, moved.Outcome);
        Assert.NotSame(stale.Announce, moved.Announce);
        Assert.NotSame(stale.FallThrough, moved.FallThrough);
        AssertFocused(landed, route);
        Assert.Equal([line], host.Announced);
        // The arm let go of the cancelled landing: no request, no hold.
        Assert.Null(host.EditorLandingRequest());
        string? seated = host.SeatedNode();

        host.CompleteStaleEditorLanding(stale, staleRequest);
        // The stale completion moved nothing: the canvas's seated card and the
        // graph's node are where the reader's moves left them.
        Assert.Equal(seated, host.SeatedNode());
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
    [InlineData("reading", "the ring")]
    [InlineData("reading", "a route")]
    [InlineData("canvas", "the ring")]
    [InlineData("canvas", "a route")]
    public void APaneMoveWithdrawsTheHeldLandingInThePaneItLeaves(string kind, string heldBy) => RunSta(() =>
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
        if (heldBy == "the ring")
        {
            host.Workspace.FocusNextPaneCommand.Execute(null);
            PumpedDispatcher.Drain();
            Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
        }
        else
        {
            // A route's landing — the focus funnel behind every open — held
            // for the same content.
            host.Workspace.RequestActiveEditorFocus();
            PumpedDispatcher.Drain();
            Assert.NotNull(host.EditorLandingRequest());
            Assert.Empty(ring.Attempts);
        }
        FrameworkElement leftStop = host.EditorStop();
        int arrivalsInTheLeftPane = 0;
        leftStop.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                arrivalsInTheLeftPane++;
            }
        };

        Assert.True(host.Workspace.FocusDirectionalPane("horizontal", +1));
        host.LetEditorLandingArriveAhead();
        PumpedDispatcher.Drain();

        Assert.Same(paneB, host.Workspace.ActiveGroup);
        Assert.False(leftStop.IsKeyboardFocusWithin);
        Assert.Equal(0, arrivalsInTheLeftPane);
        AssertFocused(host.ShownEditor(otherTab).TextArea, "the pane move");
        // The pane move's own line, once; nothing from the ring. (The move
        // also re-derives the right pane's leaves, which speak for
        // themselves.)
        Assert.Equal(
            [new A11yEvent.EditorPaneFocused(2, 2, otherTab.Title, string.Empty)],
            host.Announced.OfType<A11yEvent.EditorPaneFocused>());
        Assert.Equal(heldBy == "the ring" ? 1 : 0, ring.Attempts.Count);
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
    /// pane's content (to its rail, the ring's next stop), Shift+F6 goes back
    /// to the editor and asks it again — never the right pane's content or the
    /// tab bar a restart from the held editor would reach.</summary>
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

        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneRail], ring.Tried);
        Assert.Equal([new A11yEvent.ShellRegionFocused(new ShellRegion.RightPaneRail())], host.Announced);
        AssertFocused(host.Rail, "F6 from the moved position");
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

    /// <summary>R-10: a graph whose rows are being refreshed seats a shell
    /// landing PROVISIONALLY on the grid it still shows, so focus is in the
    /// surface while the landing is held. When the refresh then ends without
    /// a terminal seat — a rows-only failure, or a rejection — the document
    /// releases the request unseated, and that is a delayed REFUSAL whatever
    /// focus the provisional seat left behind: no editor line, and the same
    /// press resumes at the next region, spoken once there.</summary>
    [Theory]
    [InlineData("rows-only failure")]
    [InlineData("rejection")]
    public void AnUnseatedReleaseAfterAProvisionalSeatIsARefusal(string end) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("graph");
        GraphDocumentViewModel graph = host.Tab.Graph!;
        Assert.True(graph.Publication.HoldsSnapshot);
        RingHost ring = host.UseRing();
        using var reached = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        graph.FetchGateForTests = () =>
        {
            reached.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            if (end == "rows-only failure")
            {
                throw new InvalidOperationException("The fixture's rows fail.");
            }
        };
        try
        {
            Assert.True(graph.Request(new GraphRequest.Sort(new GraphTableSort(GraphTableColumn.Note, true))));
            Assert.True(reached.Wait(TimeSpan.FromSeconds(30)), "the refresh never started");
            GraphLoadToken token = graph.CurrentForTests!;
            host.FocusTabBar();

            host.Workspace.FocusNextPaneCommand.Execute(null);
            PumpedDispatcher.Drain();

            Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
            Assert.True(host.EditorStop().IsKeyboardFocusWithin, "the refreshing graph gave no provisional seat");
            Assert.NotNull(host.EditorLandingRequest());
            Assert.Empty(host.Announced);

            if (end == "rejection")
            {
                // The envelope for the CURRENT token whose query is not the
                // request's: rejected, and the lineage ends.
                GraphVisibilityQuery foreign = token.Request.Query with { NameQuery = "not-the-request" };
                GraphTableRows rows = host.Session.GraphTableRows(foreign, token.Request.Sort);
                graph.ReceiveForTests(new GraphLoadEnvelope(token, foreign.Filter, foreign, token.Request.Sort, null, rows, null));
            }
        }
        finally
        {
            release.Set();
        }

        PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
        PumpedDispatcher.Drain();

        Assert.False(graph.IsRequestInFlight);
        Assert.Null(host.EditorLandingRequest());
        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], ring.Tried);
        AssertFocused(host.Elsewhere, $"the press resumed past the refused editor ({end})");
        Assert.Equal([host.RightPaneLine()], host.Announced);
        Assert.False(host.Workspace.HoldsShellRegionLanding);
    });

    /// <summary>R-10's tri-state terminal delivery: a canvas or graph whose
    /// REALIZED terminal target refuses focus — the empty canvas's onboarding,
    /// the Visual board's renderer, the graph's state host over nothing to
    /// show — ends the landing REFUSED, inside the press or when the load it
    /// waited on publishes. It is never left pending with nothing that will
    /// ever seat it: the same press resumes at the next region, spoken once,
    /// and the editor line is never spoken.</summary>
    [Theory]
    [InlineData("canvas onboarding", false)]
    [InlineData("canvas onboarding", true)]
    [InlineData("canvas renderer", false)]
    [InlineData("graph state host", false)]
    public void ATerminalSeatThatRefusesFocusIsARefusal(string target, bool afterItsLoad) => RunSta(() =>
    {
        using var host = new Host();
        if (target == "canvas renderer")
        {
            host.Initialize(readingMode: false, documentKind: "canvas", board: CardBoard);
            host.Tab.Canvas!.ShowSurface(CanvasSurfaceKind.Visual);
        }
        else
        {
            host.Initialize(target.StartsWith("canvas", StringComparison.Ordinal) ? "canvas" : "graph");
        }
        if (target == "graph state host")
        {
            GraphDocumentViewModel graph = host.Tab.Graph!;
            host.Workspace.GraphNavigator.SetNameQuery("zzz-nothing-matches");
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(GraphLoadState.Empty, graph.Publication.State);
        }
        if (afterItsLoad)
        {
            host.HoldEditorLanding();
        }
        host.Settle();
        RingHost ring = host.UseRing();
        UIElement refusing = host.EditorStop() switch
        {
            GraphSurfaceView graphView => graphView.StateHostForTests,
            CanvasSurfaceView canvasView when target == "canvas renderer" => canvasView.VisualForTests,
            CanvasSurfaceView canvasView => canvasView.OnboardingForTests,
            _ => throw new InvalidOperationException("an unexpected editor stop"),
        };
        refusing.Focusable = false;
        host.FocusTabBar();

        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        if (afterItsLoad)
        {
            Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
            Assert.Empty(host.Announced);
            host.LetEditorLandingArrive();
        }

        string route = $"{target}{(afterItsLoad ? ", after its load" : string.Empty)}";
        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.RightPaneContent], ring.Tried);
        Assert.Equal(afterItsLoad ? ShellRegionLanding.Pending : ShellRegionLanding.Refused, ring.Attempts[0].Outcome);
        AssertFocused(host.Elsewhere, $"the press resumed past the refused editor ({route})");
        Assert.Equal([host.RightPaneLine()], host.Announced);
        Assert.Null(host.EditorLandingRequest());
        Assert.False(host.Workspace.HoldsShellRegionLanding);
    });

    /// <summary>R-10's routes: every editor landing a route asks for carries a
    /// refusal continuation, and the close fallback speaks the pane only once
    /// focus is really somewhere. A reading landing held for a load that then
    /// FAILS — after the reading toggle, after an in-place open (Quick Open's
    /// commit), or through the palette's close fallback — falls back to the
    /// tab's item: focus is never left on the window root or a closed overlay.
    /// The close fallback's pane line is spoken exactly once, after the
    /// fallback lands, and never while the landing was held; the funnel's
    /// routes speak no pane line.</summary>
    [Theory]
    [InlineData("the reading toggle")]
    [InlineData("an in-place open")]
    [InlineData("the palette's close fallback")]
    public void ARefusedRouteLandingFallsBackToTheTabItem(string route) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: route != "the reading toggle");
        ReadingSurface surface = host.ReadingSurfaceOfTab();
        switch (route)
        {
            case "the reading toggle":
                Assert.True(host.ShownEditor().FocusInputOwner());
                PumpedDispatcher.Drain();
                host.Announced.Clear();
                host.Workspace.ToggleReadingModeCommand.Execute(null);
                // The fixture projects at once: the production (asynchronous)
                // projection stands in, and fails, before the queued landing runs.
                _ = host.BindProjectionInFlight(surface, failsTerminally: true);
                break;
            case "an in-place open":
                Assert.True(host.Sentinel.Focus());
                PumpedDispatcher.Drain();
                host.Announced.Clear();
                host.Workspace.OpenPath("other.md");
                _ = host.BindProjectionInFlight(surface, failsTerminally: true);
                break;
            default:
                // The palette opened over a stop that is gone when it closes,
                // with focus nowhere: its restore falls back to the editor pane.
                _ = host.BindProjectionInFlight(surface, failsTerminally: true);
                var transient = new TextBox { Text = "Where the palette was opened from" };
                host.Show(transient);
                Assert.True(transient.Focus());
                PumpedDispatcher.Drain();
                host.OpenModal("palette");
                PumpedDispatcher.Drain();
                host.Remove(transient);
                Keyboard.ClearFocus();
                host.Announced.Clear();
                host.Lifecycle.Palette.Dismiss();
                PumpedDispatcher.Drain();
                break;
        }

        Assert.True(surface.IsFocusLandingPending, $"the route's landing was not held ({route})");
        Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);

        host.ReleaseProjection();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !surface.IsFocusLandingPending),
            $"the route's held landing never ended ({route})");
        PumpedDispatcher.Drain();

        AssertFocused(host.ActiveTabItem(), $"the refused route's fallback ({route})");
        Assert.Equal(
            route == "the palette's close fallback" ? 1 : 0,
            host.Announced.Count(line => line is A11yEvent.EditorPaneFocused));
    });

    /// <summary>R-10: the ring creates NO document request for a canvas or
    /// graph tab that no surface is realized for yet (the shared cell not yet
    /// bound to it). The press is refused there and lands the next region
    /// once; when the surface is realized for the tab afterwards — its
    /// DataContext edge re-asks the landing — it moves nothing, because no
    /// request was left for it to seat.</summary>
    [Theory]
    [InlineData("canvas")]
    [InlineData("graph")]
    public void ARingPressBeforeTheSurfaceIsRealizedCreatesNoLanding(string kind) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        Assert.Null(host.EditorLandingRequest());
        RingHost ring = host.UseRing();
        FrameworkElement surface = host.EditorStop();
        // No surface realized for the tab yet: the cell is bound to nothing.
        surface.DataContext = null;
        PumpedDispatcher.Drain();
        Assert.Null(host.EditorStopOrNull());
        Assert.True(host.Elsewhere.Focus());
        PumpedDispatcher.Drain();

        host.Workspace.FocusPreviousPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.Equal([ShellRegionKind.Editor, ShellRegionKind.TabBar], ring.Tried);
        Assert.Equal(ShellRegionLanding.Refused, ring.Attempts[0].Outcome);
        TabItem tabItem = host.ActiveTabItem();
        AssertFocused(tabItem, "the press past the unrealized editor");
        Assert.Equal([host.TabBarLine()], host.Announced);
        Assert.Null(host.EditorLandingRequest());

        // The surface is realized for the tab: its DataContext edge re-asks.
        surface.ClearValue(FrameworkElement.DataContextProperty);
        host.Settle();
        if (host.Tab.Graph is { } graph)
        {
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        Assert.Same(surface, host.EditorStopOrNull());
        AssertFocused(tabItem, "the surface realized after the press");
        Assert.Equal([host.TabBarLine()], host.Announced);
        Assert.Null(host.EditorLandingRequest());
    });

    /// <summary>R-10 over locked contract 34 D4: a named landing held for the
    /// Visual board survives a filter answer that EXCLUDES its card — on the
    /// board a filter dims cards and narrows nothing — so when the board can
    /// take the landing, the renderer is focused with that exact, dimmed card
    /// seated and the filter intact.</summary>
    [Fact]
    public void AnExcludingFilterKeepsANamedBoardLanding() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false, documentKind: "canvas", board: CardBoard);
        CanvasDocumentViewModel board = host.Tab.Canvas!;
        board.ShowSurface(CanvasSurfaceKind.Visual);
        host.Settle();
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        // The board is not shown yet, so the named landing waits for it (the
        // CURRENT value, so the template's visibility binding stays).
        surface.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        PumpedDispatcher.Drain();
        Assert.True(host.Sentinel.Focus());
        board.RequestFocusLanding(host.Tab, "beta");
        PumpedDispatcher.Drain();
        CanvasFocusRequest named = Assert.IsType<CanvasFocusRequest>(board.FocusRequest);

        // A needle alpha alone matches: the named card is excluded.
        board.FilterText = "Alpha";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => board.FilterActive && board.FilteredOutline.Count == 1),
            "the needle never narrowed the rows to alpha");
        PumpedDispatcher.PumpUntilDrained(board.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
        Assert.DoesNotContain(board.FilteredOutline, row => row.NodeId == "beta");
        Assert.Same(named, board.FocusRequest);

        BindingExpression? shown = BindingOperations.GetBindingExpression(surface, UIElement.VisibilityProperty);
        Assert.NotNull(shown);
        shown.UpdateTarget();
        host.Settle();

        AssertFocused(surface.VisualForTests, "the named landing on the filtered board");
        Assert.Equal("beta", board.Selection.Selected);
        Assert.Equal("Alpha", board.FilterText);
        Assert.Null(board.FocusRequest);
    });

    /// <summary>R-10: the answer inside the press is the document's own
    /// account too, never where focus sits. A canvas whose document takes no
    /// landing (shut down) is Refused even with the reader already inside its
    /// surface: no editor line, and the press resumes past it.</summary>
    [Fact]
    public void ADocumentThatTakesNoLandingIsRefusedWithFocusInItsSurface() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("canvas");
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        Assert.True(surface.FilterFieldForTests.Focus());
        host.Tab.Canvas!.Shutdown();
        PumpedDispatcher.Drain();
        Assert.True(surface.IsKeyboardFocusWithin);
        int spoken = 0;
        int fellThrough = 0;

        ShellRegionLanding landing = ((IShellRegionHost)host.Shell).TryLand(
            ShellRegionKind.Editor, () => spoken++, () => fellThrough++);
        PumpedDispatcher.Drain();

        Assert.Equal(ShellRegionLanding.Refused, landing);
        Assert.Equal(0, spoken);
        Assert.Equal(0, fellThrough);
    });

    /// <summary>R-10: entering the held editor stop is followed ONCE — the
    /// reader stepping into a loading canvas, or an in-flight graph's
    /// provisional seat (its state host over a load with nothing held, its
    /// grid over the rows it holds) — and any later move inside the stop is
    /// the reader moving on within it: the landing is cancelled and its
    /// request released, so the content arriving afterwards neither re-seats
    /// them nor speaks. The one later move that IS the landing is the
    /// document's own terminal seat, the move that completes its request: the
    /// ring's line, exactly once.</summary>
    [Theory]
    [InlineData("canvas", false)]
    [InlineData("canvas", true)]
    [InlineData("graph over a load", false)]
    [InlineData("graph over a load", true)]
    [InlineData("graph over its rows", false)]
    [InlineData("graph over its rows", true)]
    public void OnlyTheTerminalSeatFollowsTheEntryIntoTheHeldStop(string kind, bool readerMovesOn) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind == "canvas" ? "canvas" : "graph");
        GraphDocumentViewModel? graph = host.Tab.Graph;
        if (kind == "graph over a load")
        {
            // Nothing held: a failed pair leaves ERROR, and the next request
            // loads from scratch — LOADING, whose provisional seat is the
            // state host.
            graph!.FetchGateForTests = () => throw new InvalidOperationException("The fixture's pair fails.");
            Assert.True(graph.Request(new GraphRequest.Preset(GraphPreset.Orphans)));
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(GraphLoadState.Error, graph.Publication.State);
            graph.FetchGateForTests = null;
        }
        RingHost ring = host.UseRing();
        using var computing = new ManualResetEventSlim(false);
        if (graph is not null)
        {
            graph.BeforeComputeForTests = () => computing.Wait(TimeSpan.FromSeconds(30));
            Assert.True(graph.Request(new GraphRequest.Needle()));
            Assert.True(graph.IsRequestInFlight);
            Assert.Equal(kind == "graph over its rows", graph.Publication.HoldsSnapshot);
        }
        else
        {
            host.HoldEditorLanding();
        }
        TabItem tabItem = host.FocusTabBar();
        IInputElement? entered;
        UIElement? movedTo = null;
        try
        {
            host.Workspace.FocusNextPaneCommand.Execute(null);
            PumpedDispatcher.Drain();

            Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
            FrameworkElement stop = host.EditorStop();
            if (stop is CanvasSurfaceView canvasView)
            {
                // The loading canvas seats nothing; the reader steps in.
                AssertFocused(tabItem, "the held canvas landing");
                Assert.True(canvasView.FilterFieldForTests.Focus());
                PumpedDispatcher.Drain();
            }
            else if (kind == "graph over a load")
            {
                AssertFocused(((GraphSurfaceView)stop).StateHostForTests, "the graph's provisional seat over its load");
            }
            Assert.True(stop.IsKeyboardFocusWithin, $"nothing entered the held stop ({kind})");
            entered = Keyboard.FocusedElement;
            Assert.NotNull(host.EditorLandingRequest());
            Assert.True(host.Workspace.HoldsShellRegionLanding);
            Assert.Empty(host.Announced);

            if (readerMovesOn)
            {
                movedTo = stop is GraphSurfaceView graphView
                    ? graphView.FilterFieldForTests
                    : ((CanvasSurfaceView)stop).OutlineChoiceForTests;
                Assert.True(movedTo.Focus(), $"the other control took no focus ({kind})");
                PumpedDispatcher.Drain();

                Assert.Null(host.EditorLandingRequest());
                Assert.False(((IShellRegionHost)host.Shell).WithdrawHeldLanding(), "the landing was still held");
            }
        }
        finally
        {
            computing.Set();
        }

        if (graph is not null)
        {
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.False(graph.IsRequestInFlight);
        }
        else
        {
            host.LetEditorLandingArrive();
        }

        Assert.Single(ring.Attempts);
        Assert.Null(host.EditorLandingRequest());
        if (movedTo is not null)
        {
            AssertFocused(movedTo, $"the content arriving after the reader moved on ({kind})");
            Assert.Empty(host.Announced);
            return;
        }

        Assert.False(host.Workspace.HoldsShellRegionLanding);
        Assert.True(host.EditorStop().IsKeyboardFocusWithin, $"the terminal seat left the stop ({kind})");
        if (kind == "canvas")
        {
            // The seat moved the reader off the control they stepped onto,
            // which stayed shown: only the seat's declaration kept the move
            // from reading as the reader's own.
            Assert.NotSame(entered, Keyboard.FocusedElement);
        }
        Assert.Equal([host.EditorLine()], host.Announced);
    });

    /// <summary>R-10: every graph seat that completes the request — its rows,
    /// its state host over nothing to show, its diagram — is declared the
    /// document's own TERMINAL seat, so a held landing's watch over the
    /// surface follows the move even off a control that stays shown, while the
    /// reader's own move afterwards is still a departure. (Under the ring a
    /// graph seats terminally only after its provisional seat was hidden or
    /// rebound, which the watch follows anyway; this pins the declaration.)</summary>
    [Theory]
    [InlineData("rows")]
    [InlineData("nothing matches")]
    [InlineData("diagram")]
    public void TheGraphsTerminalSeatIsDeclaredItsOwn(string state) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("graph");
        GraphDocumentViewModel graph = host.Tab.Graph!;
        if (state == "nothing matches")
        {
            host.Workspace.GraphNavigator.SetNameQuery("zzz-nothing-matches");
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
            Assert.Equal(GraphLoadState.Empty, graph.Publication.State);
        }
        else if (state == "diagram")
        {
            Assert.True(graph.SetMode(GraphSurfaceMode.Diagram));
            Assert.True(PumpedDispatcher.PumpUntil(() => graph.HasLiveDiagram), "the diagram never went live");
            host.Settle();
        }
        var surface = (GraphSurfaceView)host.EditorStop();
        TextBox filter = surface.FilterFieldForTests;
        Assert.True(filter.Focus());
        PumpedDispatcher.Drain();
        int departures = 0;
        // A landing of the fact's own, held in the window's one slot (OD-12)
        // over the surface: its departures are counted.
        host.Shell.EditorLandings.Hold(new HeldEditorLanding(
            target: () => surface,
            isLive: () => departures == 0,
            withdraw: () => ++departures == 1,
            stillWhereAsked: () => true,
            scope: [],
            ringRegion: null));

        graph.RequestFocusLanding(host.Tab);
        PumpedDispatcher.Drain();

        Assert.Null(graph.FocusRequest);
        Assert.True(filter.IsVisible);
        Assert.False(filter.IsKeyboardFocusWithin, $"the {state} seat did not move the reader");
        Assert.True(surface.IsKeyboardFocusWithin);
        Assert.Equal(0, departures);

        Assert.True(filter.Focus());
        Assert.Equal(1, departures);
    });

    /// <summary>R-10: readiness outranks focus already inside the surface. A
    /// reader IN the reading surface when its note stops being ready — an
    /// in-place navigation swaps in the destination while its fetch runs, or
    /// a refresh is in flight — has not landed: the request holds the landing
    /// and first parks them on the tab's item, so the unapplied surface never
    /// keeps the keys, and when the content settles the surface is focused
    /// exactly ONCE, on the applied content — the one arrival NVDA reads (and
    /// the ring's line, once, when the ring asked). A park no stop takes
    /// leaves them in place, and the landing is still held, not Landed: the
    /// ring speaks once, when the content settles.</summary>
    [Theory]
    [InlineData("navigated in place")]
    [InlineData("refreshing")]
    [InlineData("refreshing, no stop to park on")]
    public void ReadinessOutranksFocusAlreadyInTheSurface(string how) => RunSta(() =>
    {
        const string Edited = "An edited paragraph the reader has not heard yet.";
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        ReadingContentViewModel? model = null;
        if (how != "navigated in place")
        {
            model = host.BindProjectionInFlight(surface);
            host.ReleaseProjection();
            Assert.Contains(NoteText, DocumentText(surface));
        }
        Assert.True(surface.Focus());
        PumpedDispatcher.Drain();
        var arrivals = new List<string>();
        surface.IsKeyboardFocusWithinChanged += (_, e) =>
        {
            if (e.NewValue is true)
            {
                arrivals.Add(DocumentText(surface));
            }
        };
        host.Announced.Clear();
        bool parks = how != "refreshing, no stop to park on";
        int spoken = 0;
        int fellThrough = 0;
        string expected;

        if (model is null)
        {
            // Enter on a link, the in-place preference: the tab now shows the
            // destination. The fixture's workspace projects at once, so the
            // production (asynchronous) projection stands in for it, its fetch
            // held — bound before the navigation's queued landing runs.
            host.Tab.NavigateFromReading(new EditorNavigationRequest("other.md", null, null));
            _ = host.BindProjectionInFlight(surface);
            expected = "A text tab beside the reading one.";
        }
        else
        {
            model.Deactivate();
            host.Tab.Text = "# Reading focus\n\n" + Edited + "\n";
            host.HoldNextFetch();
            model.Activate();
            Assert.True(model.RefreshInFlight);
            Assert.True(surface.IsKeyboardFocusWithin);
            if (!parks)
            {
                host.ActiveTabItem().Focusable = false;
            }
            Assert.Equal(
                ShellRegionLanding.Pending,
                ((IShellRegionHost)host.Shell).TryLand(ShellRegionKind.Editor, () => spoken++, () => fellThrough++));
            PumpedDispatcher.Drain();
            expected = Edited;
        }

        Assert.True(surface.IsFocusLandingPending, $"the landing was not held ({how})");
        if (parks)
        {
            AssertFocused(host.ActiveTabItem(), $"the reader waiting for the content ({how})");
        }
        else
        {
            Assert.True(surface.IsKeyboardFocusWithin);
        }
        Assert.Empty(arrivals);
        Assert.Equal(0, spoken);

        host.ReleaseProjection();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !surface.IsFocusLandingPending),
            $"the held landing never settled ({how})");
        PumpedDispatcher.Drain();

        AssertFocused(surface, $"the landing on the applied content ({how})");
        Assert.Contains(expected, DocumentText(surface));
        Assert.Equal(0, fellThrough);
        if (parks)
        {
            string landed = Assert.Single(arrivals);
            Assert.Contains(expected, landed);
            Assert.DoesNotContain("Loading reading view", landed, StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(arrivals);
        }
        if (model is null)
        {
            // The navigation's own line, once; no editor line on the way (the
            // panels announce the destination's outline for themselves).
            Assert.Equal(
                new A11yEvent.InternalNavigated("wikilink", "other.md"),
                Assert.Single(host.Announced, line => line is A11yEvent.InternalNavigated));
            Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);
        }
        else
        {
            Assert.Equal(1, spoken);
        }
    });

    /// <summary>R-10: failure outranks focus already inside the surface too.
    /// A surface showing a failure — the load-failure notice, or the stale
    /// content a failed refresh kept — is no stop even with the reader in it:
    /// the editor landing is refused, nothing is held, and the funnel's own
    /// fallback, the tab's item, takes the keys.</summary>
    [Theory]
    [InlineData("load failed")]
    [InlineData("refresh failed")]
    public void AFailureIsNoStopEvenWithFocusInIt(string failure) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        ReadingContentViewModel model = host.BindProjectionInFlight(surface, failsTerminally: failure == "load failed");
        host.ReleaseProjection();
        if (failure == "load failed")
        {
            Assert.True(model.PublishedFailureNotice);
        }
        else
        {
            model.Deactivate();
            host.HoldNextFetch(failsTerminally: true);
            model.Activate();
            host.ReleaseProjection();
            Assert.True(PumpedDispatcher.PumpUntil(() => !model.RefreshInFlight), "the refresh never ended");
            Assert.True(model.LastRefreshFailed);
            Assert.Contains(NoteText, DocumentText(surface));
        }
        Assert.True(surface.Focus());
        PumpedDispatcher.Drain();

        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();

        AssertFocused(host.ActiveTabItem(), $"the editor landing over a failure ({failure})");
        Assert.False(surface.IsFocusLandingPending);
    });

    /// <summary>R-10 over locked contract 34 D15: the renderer is the Visual
    /// board's single focus stop, so a canvas showing its board is an editor
    /// stop like any other, never a silent skip. From the tab bar (F6) and
    /// from the right pane's content stop (Shift+F6) a ready board is Landed
    /// on the renderer; one still loading is held (Pending), then landed
    /// there when its load publishes. The ring speaks the editor exactly
    /// once either way, and the document's request is complete.</summary>
    [Theory]
    [InlineData("F6 from the tab bar", false)]
    [InlineData("Shift+F6 from the right pane", false)]
    [InlineData("F6 from the tab bar", true)]
    public void AVisualBoardLandsOnItsRenderer(string press, bool loading) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false, documentKind: "canvas", board: CardBoard);
        if (loading)
        {
            host.HoldEditorLanding();
        }
        host.Tab.Canvas!.ShowSurface(CanvasSurfaceKind.Visual);
        host.Settle();
        RingHost ring = host.UseRing();
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        bool backward = press.StartsWith("Shift+F6", StringComparison.Ordinal);
        UIElement start = backward ? host.Elsewhere : host.ActiveTabItem();
        Assert.True(start.Focus());
        PumpedDispatcher.Drain();

        (backward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();

        RingHost.Attempt attempt = Assert.Single(ring.Attempts);
        Assert.Equal(ShellRegionKind.Editor, attempt.Region);
        string route = press + (loading ? ", the board loading" : string.Empty);
        if (loading)
        {
            Assert.Equal(ShellRegionLanding.Pending, attempt.Outcome);
            Assert.Empty(host.Announced);
            AssertFocused(start, $"the held landing ({route})");
            host.LetEditorLandingArrive();
        }
        else
        {
            Assert.Equal(ShellRegionLanding.Landed, attempt.Outcome);
        }

        AssertFocused(surface.VisualForTests, $"the Visual board's landing ({route})");
        // Seated on the node the outline or the table would seat (the first
        // card, nothing having been activated), silently.
        Assert.Equal("alpha", host.Tab.Canvas!.Selection.Selected);
        Assert.Equal([host.EditorLine()], host.Announced);
        Assert.Single(ring.Attempts);
        Assert.Null(host.EditorLandingRequest());
        Assert.False(host.Workspace.HoldsShellRegionLanding);
    });

    /// <summary>Locked contract 34 D15 with contract A14's addressed landing:
    /// a request that names a card (the canvas prompt's jump) lands the
    /// Visual board on its renderer with THAT card seated — silently, the
    /// reader hearing the board they land on — and the request complete.</summary>
    [Fact]
    public void ANamedLandingOnTheBoardSeatsItsCard() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false, documentKind: "canvas", board: CardBoard);
        CanvasDocumentViewModel board = host.Tab.Canvas!;
        board.ShowSurface(CanvasSurfaceKind.Visual);
        host.Settle();
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        Assert.NotEqual("beta", board.Selection.Selected);
        Assert.True(host.Sentinel.Focus());
        PumpedDispatcher.Drain();
        host.Announced.Clear();

        board.RequestFocusLanding(host.Tab, "beta");
        PumpedDispatcher.Drain();

        AssertFocused(surface.VisualForTests, "the named landing on the Visual board");
        Assert.Equal("beta", board.Selection.Selected);
        Assert.Null(board.FocusRequest);
        Assert.Empty(host.Announced);
    });

    /// <summary>Locked contract 34 D15: seating the canvas's showing
    /// projection — the Escape ladder's and the Where-am-I panel's
    /// fallback — puts the reader on the Visual board's renderer, not on the
    /// outline collapsed behind it; and a needle matching no card changes
    /// nothing (D4: a Visual filter dims cards and narrows nothing), where the
    /// outline and the table would fall to the filter field.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeatingTheVisualProjectionFocusesTheRenderer(bool zeroMatchFilter) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false, documentKind: "canvas", board: CardBoard);
        host.Tab.Canvas!.ShowSurface(CanvasSurfaceKind.Visual);
        if (zeroMatchFilter)
        {
            FilterTheBoardToNothing(host);
        }
        host.Settle();
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        Assert.True(host.Sentinel.Focus());
        PumpedDispatcher.Drain();

        Assert.True(surface.FocusProjection());
        PumpedDispatcher.Drain();

        AssertFocused(surface.VisualForTests, $"the Visual projection's seat (zero-match filter: {zeroMatchFilter})");
    });

    /// <summary>R-10 over locked contract 34 D4 and D15: a needle that matches
    /// no card DIMS the Visual board's cards and narrows nothing, so the board
    /// is still the editor stop. F6 from the tab bar and Shift+F6 from the
    /// right pane land on the renderer — never on the filter field — with the
    /// first card of the full scene seated silently though the needle dims
    /// it, the editor line spoken once, and the filter left as it was.</summary>
    [Theory]
    [InlineData("F6 from the tab bar")]
    [InlineData("Shift+F6 from the right pane")]
    public void AZeroMatchFilterStillLandsTheBoardOnItsRenderer(string press) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: false, documentKind: "canvas", board: CardBoard);
        CanvasDocumentViewModel board = host.Tab.Canvas!;
        board.ShowSurface(CanvasSurfaceKind.Visual);
        // The reader last activated a card that is NOT the scene's first
        // (codex PR 8 round 8): a zero-match landing still seats the first.
        board.LastActivatedNode = "beta";
        string needle = FilterTheBoardToNothing(host);
        host.Settle();
        RingHost ring = host.UseRing();
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        bool backward = press.StartsWith("Shift+F6", StringComparison.Ordinal);
        UIElement start = backward ? host.Elsewhere : host.ActiveTabItem();
        Assert.True(start.Focus());
        PumpedDispatcher.Drain();

        (backward ? host.Workspace.FocusPreviousPaneCommand : host.Workspace.FocusNextPaneCommand).Execute(null);
        PumpedDispatcher.Drain();

        Assert.Equal(ShellRegionLanding.Landed, Assert.Single(ring.Attempts).Outcome);
        AssertFocused(surface.VisualForTests, $"the zero-match Visual board's landing ({press})");
        Assert.False(surface.FilterFieldForTests.IsKeyboardFocusWithin);
        Assert.Equal("alpha", board.Selection.Selected);
        Assert.DoesNotContain(board.FilteredOutline, row => row.NodeId == "alpha");
        Assert.Equal(needle, board.FilterText);
        Assert.Equal([host.EditorLine()], host.Announced);
        Assert.Null(host.EditorLandingRequest());
    });

    /// <summary>R-10: a Visual board with no cards at all is still an empty
    /// canvas: F6 lands on its onboarding, the editor line spoken once.</summary>
    [Fact]
    public void AnEmptyVisualBoardLandsOnItsOnboarding() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("canvas");
        CanvasDocumentViewModel board = host.Tab.Canvas!;
        Assert.Empty(board.Outline);
        board.ShowSurface(CanvasSurfaceKind.Visual);
        host.Settle();
        RingHost ring = host.UseRing();
        var surface = Assert.IsType<CanvasSurfaceView>(host.EditorStop());
        host.FocusTabBar();

        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();

        Assert.Equal(ShellRegionLanding.Landed, Assert.Single(ring.Attempts).Outcome);
        AssertFocused(surface.OnboardingForTests, "the empty Visual board's landing");
        Assert.Equal([host.EditorLine()], host.Announced);
    });

    /// <summary>A needle no card of <see cref="CardBoard"/> matches, applied
    /// and answered; the board keeps every card.</summary>
    private static string FilterTheBoardToNothing(Host host)
    {
        const string Needle = "zzz-matches-no-card";
        CanvasDocumentViewModel board = host.Tab.Canvas!;
        board.FilterText = Needle;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => board.FilterActive && board.FilteredOutline.Count == 0),
            "the needle never narrowed the rows to none");
        PumpedDispatcher.PumpUntilDrained(board.WhenAllWorkDrained());
        PumpedDispatcher.Drain();
        Assert.Equal(2, board.Outline.Count);
        return Needle;
    }

    // --- PR 8, codex round 6: reproductions. Each fails on the round-7 code
    // and states the behaviour the owner's fix must produce. -----------------

    /// <summary>Codex r6 finding 1 (reproduction). A ROUTE's canvas or graph
    /// landing requested before a surface is realized for the tab is watched
    /// through the document alone — no departure watch — so after the reader
    /// moves away (to the Files stand-in), realizing the surface still seats
    /// the editor, or, when its terminal target refuses focus, still runs the
    /// route's fallback: either way focus is taken off where the reader went.
    /// Expected: the reader's move cancels the landing, and realizing the
    /// surface afterwards moves nothing.</summary>
    [Theory]
    [InlineData("canvas", "seats")]
    [InlineData("canvas", "refuses")]
    [InlineData("graph", "seats")]
    [InlineData("graph", "refuses")]
    public void R6_ASurfacelessRouteLandingIsCancelledWhenTheReaderMoves(string kind, string target) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        FrameworkElement surface = host.EditorStop();
        if (target == "refuses")
        {
            if (surface is GraphSurfaceView graphView)
            {
                GraphDocumentViewModel graph = host.Tab.Graph!;
                host.Workspace.GraphNavigator.SetNameQuery("zzz-nothing-matches");
                PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
                PumpedDispatcher.Drain();
                graphView.StateHostForTests.Focusable = false;
            }
            else
            {
                ((CanvasSurfaceView)surface).OnboardingForTests.Focusable = false;
            }
        }
        TabItem tabItem = host.FocusTabBar();
        // No surface is realized for the tab: the shared cell is bound to nothing.
        surface.DataContext = null;
        PumpedDispatcher.Drain();
        Assert.Null(host.EditorStopOrNull());

        // A route asks for the editor (the funnel behind every open).
        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();
        Assert.NotNull(host.EditorLandingRequest());
        AssertFocused(tabItem, "the route's landing, held for the unrealized surface");

        // The reader moves away, then the surface is realized for the tab.
        Assert.True(host.Sentinel.Focus());
        PumpedDispatcher.Drain();
        surface.ClearValue(FrameworkElement.DataContextProperty);
        host.Settle();
        if (host.Tab.Graph is { } settling)
        {
            PumpedDispatcher.PumpUntilDrained(settling.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        AssertFocused(host.Sentinel, $"where the reader moved, after the {kind}'s surface realized ({target})");
        Assert.Null(host.EditorLandingRequest());
    });

    /// <summary>Codex r6 finding 2 (reproduction). The departure watch samples
    /// the focused element once, when the landing is held, and installs
    /// nothing when focus is NOWHERE — as it is after the palette closes over
    /// a stop that is gone. A route's reading landing held from there is never
    /// latched: the reader moves to the Files stand-in, and the content's
    /// arrival seats the surface, or its failure runs the route's fallback to
    /// the tab item. Expected: the reader's move cancels the landing; focus
    /// stays where they moved.</summary>
    [Theory]
    [InlineData("the content arrives")]
    [InlineData("the load fails")]
    public void R6_ALandingHeldWithFocusNowhereIsCancelledWhenTheReaderMoves(string settle) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ReadingSurfaceOfTab();
        _ = host.BindProjectionInFlight(surface, failsTerminally: settle == "the load fails");
        var transient = new TextBox { Text = "Where the palette was opened from" };
        host.Show(transient);
        Assert.True(transient.Focus());
        PumpedDispatcher.Drain();
        host.OpenModal("palette");
        PumpedDispatcher.Drain();
        host.Remove(transient);
        Keyboard.ClearFocus();
        host.Lifecycle.Palette.Dismiss();
        PumpedDispatcher.Drain();
        Assert.True(surface.IsFocusLandingPending, "the close fallback's landing was not held");
        Assert.Null(Keyboard.FocusedElement);

        // The reader moves to the Files stand-in before the content settles.
        Assert.True(host.Sentinel.Focus());
        PumpedDispatcher.Drain();
        host.ReleaseProjection();
        _ = PumpedDispatcher.PumpUntil(() => !surface.IsFocusLandingPending);
        PumpedDispatcher.Drain();

        AssertFocused(host.Sentinel, $"where the reader moved ({settle})");
        Assert.False(surface.IsFocusLandingPending);
    });

    /// <summary>Codex r6 finding 3 (reproduction). A modal surface opening
    /// withdraws only a landing the F6 ring holds; a landing a ROUTE holds —
    /// for a reading, canvas or graph tab, or for a canvas with no surface
    /// realized yet — stays live under the modal, and its content arriving
    /// seats the editor beneath it. Expected: the modal's opening withdraws
    /// every held editor landing, whoever asked for it, and nothing seats
    /// beneath it.</summary>
    [Theory]
    [InlineData("reading")]
    [InlineData("canvas")]
    [InlineData("graph")]
    [InlineData("canvas with no surface realized")]
    public void R6_AModalOpeningWithdrawsARouteHeldLanding(string kind) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind.StartsWith("canvas", StringComparison.Ordinal) ? "canvas" : kind);
        FrameworkElement? unrealized = null;
        if (kind == "canvas with no surface realized")
        {
            unrealized = host.EditorStop();
        }
        else
        {
            host.HoldEditorLanding();
        }
        host.FocusTabBar();
        if (unrealized is not null)
        {
            unrealized.DataContext = null;
            PumpedDispatcher.Drain();
        }
        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();
        Assert.NotNull(host.EditorLandingRequest());

        host.OpenModal("palette");
        PumpedDispatcher.Drain();
        if (unrealized is not null)
        {
            unrealized.ClearValue(FrameworkElement.DataContextProperty);
            host.Settle();
        }
        else
        {
            host.LetEditorLandingArrive();
        }

        Assert.NotNull(host.Shell.OpenModalSurface);
        Assert.False(host.EditorStop().IsKeyboardFocusWithin, $"the {kind} landing seated beneath the open palette");
        Assert.Null(host.EditorLandingRequest());
    });

    /// <summary>Codex r6 note (reproduction). A route's refused landing falls
    /// back through FallBackFromEditor, which gives up when the group's tab
    /// control is not realized — without trying the Files tree — and reports
    /// nothing; the close fallback then speaks the editor pane although no
    /// fallback took focus. Expected: the pane line is spoken only when focus
    /// really landed.</summary>
    [Fact]
    public void R6_ARouteFallbackThatTakesNoFocusSpeaksNoPane() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        WorkspaceGroupViewModel group = host.Workspace.ActiveGroup;
        TabControl tabs = Assert.Single(
            Descendants<TabControl>(host.Shell.ContentPaneBorder),
            candidate => ReferenceEquals(candidate.DataContext, group));
        var transient = new TextBox { Text = "Where the palette was opened from" };
        host.Show(transient);
        Assert.True(transient.Focus());
        PumpedDispatcher.Drain();
        host.OpenModal("palette");
        PumpedDispatcher.Drain();
        // The group's tab control is not realized for it: the fallback finds none.
        tabs.DataContext = null;
        host.Remove(transient);
        Keyboard.ClearFocus();
        host.Announced.Clear();

        host.Lifecycle.Palette.Dismiss();
        PumpedDispatcher.Drain();

        Assert.Null(Keyboard.FocusedElement);
        Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);
    });

    // --- PR 8, OD-12: the window's one landing slot, for every caller ----------

    /// <summary>The callers × scenarios the window's one slot answers for
    /// (OD-12): every caller that can hold an editor landing — the F6 ring (from
    /// the tab bar), the focus funnel (from the tab bar, and from nowhere), and
    /// the close fallback (from nowhere: the stop it would restore is gone) —
    /// over every arm that holds (a reading projection in flight, a canvas load
    /// in flight, a graph surface not shown, and a canvas or graph with NO
    /// surface realized yet, which the ring never holds), against every way the
    /// reader can be elsewhere: they move, a modal opens, the active tab
    /// changes.</summary>
    public static TheoryData<string, string, string> HeldLandingCases()
    {
        var cases = new TheoryData<string, string, string>();
        foreach (string kind in new[] { "reading", "canvas", "graph", "canvas, no surface yet", "graph, no surface yet" })
        {
            foreach (string caller in new[] { "the ring", "the funnel", "the funnel from nowhere", "the close fallback" })
            {
                if (caller == "the ring" && kind.EndsWith("no surface yet", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (string scenario in new[] { "the reader moves", "a modal opens", "the tab changes" })
                {
                    cases.Add(kind, caller, scenario);
                }
            }
        }

        return cases;
    }

    /// <summary>R-10, OD-12: whoever asked for it and whatever arm holds it, a
    /// held editor landing ends — silently, its request released — the moment
    /// the reader is elsewhere: they move (from where the request found them,
    /// or from nowhere), a modal opens over it, or the active tab changes under
    /// it. Nothing arrives afterwards: the content settling (or the surface
    /// realizing) seats nobody, and no editor line is spoken.</summary>
    [Theory]
    [MemberData(nameof(HeldLandingCases))]
    public void EveryHeldEditorLandingEndsWhenTheReaderIsElsewhere(string kind, string caller, string scenario) =>
        RunSta(() =>
        {
            using var host = new Host();
            string documentKind = kind.Split(',')[0];
            bool surfaceless = kind.EndsWith("no surface yet", StringComparison.Ordinal);
            host.Initialize(
                readingMode: documentKind == "reading",
                besideTextTab: true,
                documentKind: documentKind == "reading" ? null : documentKind);
            host.Activate(host.Tab);
            FrameworkElement? unrealized = null;
            if (surfaceless)
            {
                unrealized = host.EditorStop();
            }
            else
            {
                host.HoldEditorLanding();
            }

            RingHost? ring = caller == "the ring" ? host.UseRing() : null;
            host.FocusTabBar();
            if (unrealized is not null)
            {
                unrealized.DataContext = null;
                PumpedDispatcher.Drain();
            }

            host.Announced.Clear();
            switch (caller)
            {
                case "the ring":
                    host.Workspace.FocusNextPaneCommand.Execute(null);
                    PumpedDispatcher.Drain();
                    Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring!.Attempts).Outcome);
                    break;
                case "the funnel":
                    host.Workspace.RequestActiveEditorFocus();
                    PumpedDispatcher.Drain();
                    break;
                case "the funnel from nowhere":
                    Keyboard.ClearFocus();
                    host.Workspace.RequestActiveEditorFocus();
                    PumpedDispatcher.Drain();
                    Assert.Null(Keyboard.FocusedElement);
                    break;
                default:
                    host.CloseAPaletteOverAGoneStop();
                    Assert.Null(Keyboard.FocusedElement);
                    break;
            }

            Assert.True(((IShellRegionHost)host.Shell).HoldsLanding, $"{caller} held no landing ({kind})");
            Assert.NotNull(host.EditorLandingRequest());

            switch (scenario)
            {
                case "the reader moves":
                    Assert.True(host.Sentinel.Focus());
                    break;
                case "a modal opens":
                    host.OpenModal("palette");
                    break;
                default:
                    host.Workspace.ActiveGroup.ActiveTab = host.Other!;
                    host.Settle();
                    break;
            }

            PumpedDispatcher.Drain();
            Assert.False(((IShellRegionHost)host.Shell).HoldsLanding, $"the {kind} landing {caller} held survived: {scenario}");
            Assert.Null(host.EditorLandingRequest());

            if (scenario == "the tab changes")
            {
                // Back to the tab by the strip alone (no route asks): nothing
                // the cancelled landing asked for is delivered.
                host.Activate(host.Tab);
                unrealized?.ClearValue(FrameworkElement.DataContextProperty);
                host.Settle();
            }
            else if (unrealized is not null)
            {
                unrealized.ClearValue(FrameworkElement.DataContextProperty);
                host.Settle();
                if (host.Tab.Graph is { } graph)
                {
                    PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
                    PumpedDispatcher.Drain();
                }
            }
            else
            {
                host.LetEditorLandingArrive();
            }

            Assert.False(
                host.EditorStopOrNull() is { IsKeyboardFocusWithin: true },
                $"the {kind} landing {caller} held seated after {scenario}");
            Assert.Null(host.EditorLandingRequest());
            Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);
            if (scenario == "the reader moves")
            {
                AssertFocused(host.Sentinel, $"where the reader moved ({kind}, {caller})");
            }
        });

    /// <summary>OD-12: a route's canvas or graph landing asked before a surface
    /// is realized for the tab is held by the window's slot, which resolves the
    /// tab's surface when it reads a move — so the surface realizing later, and
    /// its seat moving the reader off the tab item they asked from, is the
    /// landing's own entry, not a departure: it seats, by the document's own
    /// account, and the close fallback's pane line is spoken once, when focus
    /// is there (the funnel speaks nothing of its own).</summary>
    [Theory]
    [InlineData("canvas", "the close fallback")]
    [InlineData("canvas", "the funnel from the tab bar")]
    [InlineData("graph", "the close fallback")]
    [InlineData("graph", "the funnel from the tab bar")]
    public void ASurfacelessRouteLandingSeatsWhenItsSurfaceRealizes(string kind, string route) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        FrameworkElement surface = host.EditorStop();
        TabItem tabItem = host.FocusTabBar();
        surface.DataContext = null;
        PumpedDispatcher.Drain();
        host.Announced.Clear();

        if (route == "the close fallback")
        {
            host.CloseAPaletteOverAGoneStop();
        }
        else
        {
            host.Workspace.RequestActiveEditorFocus();
            PumpedDispatcher.Drain();
            AssertFocused(tabItem, "the route's landing, held for the unrealized surface");
        }
        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding);
        Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);

        surface.ClearValue(FrameworkElement.DataContextProperty);
        host.Settle();
        if (host.Tab.Graph is { } graph)
        {
            PumpedDispatcher.PumpUntilDrained(graph.WhenAllWorkDrained());
            PumpedDispatcher.Drain();
        }

        Assert.True(surface.IsKeyboardFocusWithin, $"the {kind} surface realized but did not seat the landing");
        Assert.Null(host.EditorLandingRequest());
        Assert.False(((IShellRegionHost)host.Shell).HoldsLanding);
        DocumentLandingEnded? ended = host.Tab.Canvas?.LastFocusLandingEnd ?? host.Tab.Graph?.LastFocusEnd;
        Assert.Equal(DocumentLandingEnd.Seated, ended?.End);
        A11yEvent[] lines = route == "the close fallback" ? [host.EditorLine()] : [];
        Assert.Equal(lines, host.Announced.OfType<A11yEvent.EditorPaneFocused>());
    });

    /// <summary>OD-12: the slot resolves a surface-less landing's target when it
    /// reads a move, not when the landing was held. The canvas is realized for
    /// the tab after the route asked (its load still in flight), and the reader
    /// steps into it — the landing's one entry, not a departure: the landing
    /// stays held, and the load publishing seats it, by the document's own
    /// account.</summary>
    [Fact]
    public void AStepIntoASurfaceRealizedAfterTheRequestIsTheLandingsEntry() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("canvas");
        host.HoldEditorLanding();
        var surface = (CanvasSurfaceView)host.EditorStop();
        host.FocusTabBar();
        surface.DataContext = null;
        PumpedDispatcher.Drain();

        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();
        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding);
        surface.ClearValue(FrameworkElement.DataContextProperty);
        host.Settle();
        Assert.True(surface.FilterFieldForTests.Focus());
        PumpedDispatcher.Drain();

        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding, "the step into the realized surface ended the landing");
        host.LetEditorLandingArrive();

        Assert.True(surface.IsKeyboardFocusWithin);
        Assert.False(surface.FilterFieldForTests.IsKeyboardFocusWithin, "the load did not seat the landing");
        Assert.Equal(DocumentLandingEnd.Seated, host.Tab.Canvas!.LastFocusLandingEnd?.End);
    });

    /// <summary>OD-12's modal hook is the EDGE: a modal surface opening
    /// withdraws what is held, and the modal's own changes while it stays open
    /// withdraw nothing. A palette command's route raises the tab's landing
    /// under the open palette (the funnel's request, held from the moment it is
    /// raised); the palette's own changes before it closes leave it held, and
    /// the route's queued landing then lands it once the palette is gone.</summary>
    [Fact]
    public void AModalsOwnChangesLeaveALandingRaisedUnderIt() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("canvas");
        host.HoldEditorLanding();
        host.FocusTabBar();
        host.OpenModal("palette");
        PumpedDispatcher.Drain();

        host.Workspace.RequestActiveEditorFocus();
        object? raised = host.EditorLandingRequest();
        Assert.NotNull(raised);
        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding);

        host.Lifecycle.Palette.Query = "a query the palette ranks";

        Assert.Same(raised, host.EditorLandingRequest());
        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding, "the palette's own change withdrew the landing");

        host.Lifecycle.Palette.Dismiss();
        PumpedDispatcher.Drain();
        host.LetEditorLandingArrive();

        Assert.True(host.EditorStop().IsKeyboardFocusWithin, "the route's landing did not land after the palette closed");
        Assert.Null(host.EditorLandingRequest());
    });

    /// <summary>OD-12: no route landing is created while a modal surface is
    /// open. A route queued behind a modal that opened first — the funnel's
    /// landing waits at Input — lands nothing beneath it: the stop is not
    /// focused, no request is left behind, and nothing is held.</summary>
    [Fact]
    public void ARouteLandingQueuedBehindAModalLandsNothingBeneathIt() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        TabItem tabItem = host.FocusTabBar();

        host.Workspace.RequestActiveEditorFocus();
        host.OpenModal("palette");
        PumpedDispatcher.Drain();

        Assert.NotNull(host.Shell.OpenModalSurface);
        Assert.False(host.ShownSurface().IsKeyboardFocusWithin, "the queued landing seated beneath the open palette");
        AssertFocused(tabItem, "the route queued behind the palette");
        Assert.Null(host.EditorLandingRequest());
        Assert.False(((IShellRegionHost)host.Shell).HoldsLanding);
    });

    /// <summary>OD-12: the funnel's own canvas or graph request (contract A14's
    /// workspace-level instruction) is held from the moment it is raised — not
    /// only once the funnel's queued landing runs. A later focus request that
    /// supersedes that queued landing in the window's arbiter (a pane-boundary
    /// move) leaves the request to the slot, and the reader moving away cancels
    /// it: the load publishing, or the surface being shown, seats nobody.</summary>
    [Theory]
    [InlineData("canvas")]
    [InlineData("graph")]
    public void TheFunnelsOwnRequestIsHeldFromTheMomentItIsRaised(string kind) => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(kind);
        host.HoldEditorLanding();
        host.FocusTabBar();

        host.Workspace.RequestActiveEditorFocus();
        Assert.NotNull(host.EditorLandingRequest());
        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding, "the funnel's request was not held when raised");
        // No pane to the left: the boundary route (focus the Files region),
        // whose request supersedes the funnel's queued landing.
        Assert.False(host.Workspace.FocusDirectionalPane("horizontal", -1));
        PumpedDispatcher.Drain();
        Assert.True(host.Sentinel.Focus());
        PumpedDispatcher.Drain();
        host.LetEditorLandingArrive();

        AssertFocused(host.Sentinel, $"where the reader moved ({kind})");
        Assert.Null(host.EditorLandingRequest());
        Assert.False(((IShellRegionHost)host.Shell).HoldsLanding);
    });

    /// <summary>OD-12: a canvas jump's named landing (the marks list's Enter)
    /// is raised outside the funnel — it names the card — and the window's
    /// slot holds it from the moment it is raised: the reader moving away
    /// before the surface can seat it cancels it, and showing the surface
    /// afterwards seats nobody.</summary>
    [Fact]
    public void ACanvasJumpsNamedLandingEndsWhenTheReaderMoves() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize("canvas", CardBoard);
        CanvasDocumentViewModel board = host.Tab.Canvas!;
        board.SeatSelectionSilently("beta");
        board.ToggleMark();
        board.SeatSelectionSilently("alpha");
        board.OpenMarksList(host.Tab);
        var sheet = Assert.IsType<CanvasMarksListPrompt>(host.Workspace.CanvasPromptSheet);
        Assert.Equal("beta", sheet.SelectedChoice?.Value);
        host.HideStop();
        host.FocusTabBar();

        host.Workspace.SubmitCanvasPrompt();
        PumpedDispatcher.Drain();

        Assert.Null(host.Workspace.CanvasPromptSheet);
        CanvasFocusRequest request = Assert.IsType<CanvasFocusRequest>(host.EditorLandingRequest());
        Assert.Equal("beta", request.NodeId);
        Assert.True(((IShellRegionHost)host.Shell).HoldsLanding, "the jump's landing was not held");

        Assert.True(host.Sentinel.Focus());
        PumpedDispatcher.Drain();
        Assert.Null(host.EditorLandingRequest());
        host.ShowStop();

        AssertFocused(host.Sentinel, "where the reader moved before the jump could land");
    });

    /// <summary>OD-12: the window losing activation — a dialog, another
    /// application — withdraws the held landing, whoever holds it: the content
    /// arriving while the window is away seats nobody and speaks nothing.</summary>
    [Fact]
    public void TheWindowLosingActivationWithdrawsTheHeldLanding() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        host.HoldEditorLanding();
        RingHost ring = host.UseRing();
        TabItem tabItem = host.FocusTabBar();
        host.Workspace.FocusNextPaneCommand.Execute(null);
        PumpedDispatcher.Drain();
        Assert.Equal(ShellRegionLanding.Pending, Assert.Single(ring.Attempts).Outcome);
        Assert.True(host.Workspace.HoldsShellRegionLanding);

        RaiseWindowEvent(host.Shell, "OnDeactivated");

        Assert.False(host.Workspace.HoldsShellRegionLanding);
        host.LetEditorLandingArrive();
        AssertFocused(tabItem, "the landing held when the window lost activation");
        Assert.Empty(host.Announced);
    });

    /// <summary>OD-12: WPF's restore when the window comes forward — focus put
    /// back from nowhere before any input — is not the reader leaving. A route
    /// landing raised while the window was away (focus nowhere) survives the
    /// restore and lands when its content arrives.</summary>
    [Fact]
    public void TheWindowsActivationRestoreIsNotTheReaderLeaving() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface);
        Keyboard.ClearFocus();
        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();
        Assert.True(surface.IsFocusLandingPending);

        RaiseWindowEvent(host.Shell, "OnActivated");
        Assert.True(host.Sentinel.Focus());

        Assert.True(surface.IsFocusLandingPending, "the activation restore cancelled the landing");
        host.ReleaseProjection();
        AssertFocused(surface, "the landing raised while the window was away");
    });

    /// <summary>OD-12: the slot's departure observer sees every focus move on
    /// the thread, a popup's included — a popup has a root of its own, which a
    /// window's handlers would never hear. The reader moving into a popup's
    /// field is leaving: the held landing ends, and the content arriving seats
    /// nobody.</summary>
    [Fact]
    public void AMoveIntoAPopupIsTheReaderLeaving() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        ReadingSurface surface = host.ShownSurface();
        host.BindProjectionInFlight(surface);
        host.FocusTabBar();
        host.Workspace.RequestActiveEditorFocus();
        PumpedDispatcher.Drain();
        Assert.True(surface.IsFocusLandingPending);
        var field = new TextBox { Text = "A popup's field" };
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            Child = field,
            PlacementTarget = host.Sentinel,
            StaysOpen = true,
        };
        try
        {
            popup.IsOpen = true;
            PumpedDispatcher.Drain();

            Assert.True(field.Focus(), "the popup's field took no focus");
            Assert.False(surface.IsFocusLandingPending, "a move into a popup did not end the landing");
            host.ReleaseProjection();
            Assert.False(surface.IsKeyboardFocusWithin);
        }
        finally
        {
            popup.IsOpen = false;
        }
    });

    /// <summary>OD-12 (codex round 6's note): a route's refused landing whose
    /// group has no realized tab control still tries the Files tree — the
    /// chain's last resort — and the answer decides the line: focus on the
    /// Files tree, and no pane line.</summary>
    [Fact]
    public void ARouteFallbackWithNoTabControlLandsOnTheFilesTree() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize(readingMode: true);
        host.ShowFilesPane();
        WorkspaceGroupViewModel group = host.Workspace.ActiveGroup;
        TabControl tabs = Assert.Single(
            Descendants<TabControl>(host.Shell.ContentPaneBorder),
            candidate => ReferenceEquals(candidate.DataContext, group));
        tabs.DataContext = null;
        host.Announced.Clear();

        host.CloseAPaletteOverAGoneStop();

        Assert.True(host.Shell.FilesTree.IsKeyboardFocusWithin, $"the fallback left focus on {Describe(Keyboard.FocusedElement)}");
        Assert.DoesNotContain(host.Announced, line => line is A11yEvent.EditorPaneFocused);
    });

    private static void RaiseWindowEvent(Window window, string method) =>
        typeof(Window).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [EventArgs.Empty]);

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

        /// <summary>Whether the right pane shows a content stop; without one
        /// its rail is the pane's only stop (the ring's presence rule).</summary>
        public bool RightPaneHasContentStop { get; set; } = true;

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

            if (ReferenceEquals(focused, host.Rail))
            {
                return ShellRegionKind.RightPaneRail;
            }

            if (ReferenceEquals(focused, host.Status))
            {
                return ShellRegionKind.StatusBar;
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
                    ShellRegionKind.RightPaneRail => host.Rail,
                    ShellRegionKind.StatusBar => host.Status,
                    _ => null,
                };
                outcome = stop is not null && stop.Focus() && stop.IsKeyboardFocusWithin
                    ? ShellRegionLanding.Landed
                    : ShellRegionLanding.Refused;
            }

            Attempts.Add(new Attempt(region, outcome, announceWhenLanded, fallThroughWhenRefused));
            return outcome;
        }

        public bool HoldsLanding => ((IShellRegionHost)host.Shell).HoldsLanding;

        public ShellRegionKind? HeldRingRegion => ((IShellRegionHost)host.Shell).HeldRingRegion;

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

        /// <summary>The right pane's rail stand-in (the ring's RightPaneRail).</summary>
        public TextBox Rail { get; private set; } = null!;

        /// <summary>The status bar's stand-in (the ring's StatusBar).</summary>
        public TextBox Status { get; private set; } = null!;
        public List<A11yEvent> Announced { get; } = [];
        private FrameworkElement? _heldSurface;

        /// <summary>A reading-mode note, a canvas or the graph as the active
        /// tab (<see cref="Tab"/>); a canvas loads <paramref name="board"/>
        /// when one is given.</summary>
        public void Initialize(string kind, string? board = null)
        {
            switch (kind)
            {
                case "reading":
                    Initialize(readingMode: true);
                    break;
                case "canvas" when board is not null:
                    Initialize(readingMode: false, documentKind: kind, board: board);
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
            bool readingMode,
            string note = HeadedNote,
            bool besideTextTab = false,
            string? documentKind = null,
            string board = "{\"nodes\":[],\"edges\":[]}")
        {
            Assert.Null(Application.Current);
            File.WriteAllText(Path.Combine(_fixture.Root, "note.md"), note);
            File.WriteAllText(Path.Combine(_fixture.Root, "other.md"), "# Other\n\nA text tab beside the reading one.\n");
            File.WriteAllText(Path.Combine(_fixture.Root, "board.canvas"), board);
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
            Rail = new TextBox { Text = "The right pane's rail" };
            Status = new TextBox { Text = "The status bar" };
            var content = new DockPanel();
            DockPanel.SetDock(Sentinel, Dock.Top);
            DockPanel.SetDock(Elsewhere, Dock.Top);
            DockPanel.SetDock(ElsewhereToo, Dock.Top);
            DockPanel.SetDock(Rail, Dock.Top);
            DockPanel.SetDock(Status, Dock.Top);
            content.Children.Add(Sentinel);
            content.Children.Add(Elsewhere);
            content.Children.Add(ElsewhereToo);
            content.Children.Add(Rail);
            content.Children.Add(Status);
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

            HideStop();
        }

        /// <summary>Collapse the tab's canvas or graph surface — the CURRENT
        /// value, so the template's visibility binding stays and a tab switch
        /// still re-evaluates it.</summary>
        public void HideStop()
        {
            _heldSurface = EditorStop();
            _heldSurface.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            PumpedDispatcher.Drain();
        }

        /// <summary>Show what <see cref="HideStop"/> collapsed: the binding
        /// re-evaluated.</summary>
        public void ShowStop()
        {
            BindingExpression? shown = BindingOperations.GetBindingExpression(
                _heldSurface!, UIElement.VisibilityProperty);
            Assert.NotNull(shown);
            shown.UpdateTarget();
            _window!.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        /// <summary>The close fallback's route, from nowhere: the palette opened
        /// from a stop that is gone by the time it closes, so its restore finds
        /// nothing and the editor landing (<c>FocusActiveEditorPane</c>) runs
        /// with focus nowhere.</summary>
        public void CloseAPaletteOverAGoneStop()
        {
            var transient = new TextBox { Text = "Where the palette was opened from" };
            Show(transient);
            Assert.True(transient.Focus());
            PumpedDispatcher.Drain();
            OpenModal("palette");
            PumpedDispatcher.Drain();
            Remove(transient);
            Keyboard.ClearFocus();
            Lifecycle.Palette.Dismiss();
            PumpedDispatcher.Drain();
        }

        /// <summary>Rehost the shell's Files pane beside the content pane, so
        /// its tree can take focus.</summary>
        public void ShowFilesPane()
        {
            FrameworkElement files = Shell.FilesPaneBorder;
            Assert.IsAssignableFrom<Panel>(files.Parent).Children.Remove(files);
            Show(files);
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

            ShowStop();
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

        /// <summary>Where the tab's document has the reader seated: the
        /// canvas's selected card or the graph's selected node (null for a
        /// reading tab).</summary>
        public string? SeatedNode() => Tab.Canvas?.Selection.Selected ?? Tab.Graph?.ViewState.SelectedKey;

        /// <summary>The ring's editor line for this fixture's one pane.</summary>
        public A11yEvent EditorLine() =>
            new A11yEvent.EditorPaneFocused(1, 1, Tab.Title, string.Empty);

        /// <summary>The arm's own token for the editor landing: the request a
        /// canvas or graph document holds, or the reading surface's held
        /// landing.</summary>
        public object? EditorLandingRequest() =>
            (object?)Tab.Canvas?.FocusRequest
            ?? Tab.Graph?.FocusRequest
            ?? (Tab.IsReadingMode ? ShownSurfaceOrNull()?.HeldFocusLanding : null);

        private ReadingSurface? ShownSurfaceOrNull() =>
            Descendants<ReadingSurface>(Shell.ContentPaneBorder)
                .SingleOrDefault(surface => ReferenceEquals(surface.DataContext, Tab) && surface.IsVisible);

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
                case { } readingToken:
                    _ = ShownSurface().CancelFocusLanding(readingToken);
                    break;
            }
            PumpedDispatcher.Drain();
        }

        /// <summary>The document lets go of the shell's request unseated.</summary>
        public void ReleaseDocumentRequest()
        {
            if (Tab.Canvas is { FocusRequest: { } canvasRequest } canvas)
            {
                canvas.ReleaseFocusLanding(canvasRequest);
            }
            else if (Tab.Graph is { FocusRequest: { } graphRequest } graph)
            {
                graph.ReleaseFocus(graphRequest);
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

        /// <summary>The tab's reading surface, shown or not (the markdown
        /// template carries it beside the editor).</summary>
        public ReadingSurface ReadingSurfaceOfTab() => Assert.Single(
            Descendants<ReadingSurface>(Shell.ContentPaneBorder),
            surface => ReferenceEquals(surface.DataContext, Tab));

        /// <summary>Take an element <see cref="Show"/> added out of the window.</summary>
        public void Remove(UIElement element)
        {
            var content = (DockPanel)_window!.Content;
            content.Children.Remove(element);
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
