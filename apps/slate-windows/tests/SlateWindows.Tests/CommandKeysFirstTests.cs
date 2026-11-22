// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using SlateWindows.Commands;
using SlateWindows.Panels;

namespace SlateWindows.Tests;

/// <summary>
/// #1866 (CD11), keys first for a command. <see cref="RelayCommand"/>,
/// <see cref="AsyncRelayCommand"/> and <see cref="ReasonedCommand"/> raise their change
/// through one seam, <see cref="CommandKeysFirst"/>. When the control holding the keys is
/// bound to the command, and the command now refuses it, the keys move before the change is
/// raised, so the control never goes off with the keys on it
/// (<see cref="RegionFocusGuard.LandBeforeLeaving(UIElement)"/>): to its own landing; else
/// to the one nearest ancestor WPF would hand them to, if that is a stop; else to the
/// landing of the scope around it.
/// </summary>
/// <remarks>
/// A window of its own, with a scope whose landing the fact registers on its own elements:
/// the guard keeps a landing only as long as its element lives, so nothing registered here
/// outlasts the fact. Three facts at the end use the real window, for the landings only it
/// has.
/// </remarks>
public sealed class CommandKeysFirstTests
{
    public static TheoryData<string> Kinds() => ["relay", "async", "reasoned"];

    /// <summary>A button inside an Expander, as the Files pane's actions are: the keys go
    /// to the Expander, where WPF hands them today after the button is off.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void AButtonInAnExpanderGoesToTheExpanderFirst(string kind) => RunSta(() =>
    {
        using var stage = new Stage();
        using var gate = new Gate(kind);
        var button = new Button { Content = "Act", Command = gate.Command };
        var group = new Expander { Header = "Actions", IsExpanded = true, Focusable = true, Content = Panel(button) };
        stage.Content.Children.Add(group);
        stage.Settle();
        TakeTheKeys(button);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the button in the Expander", button, gate.Refuse);

        Assert.Same(group, Keyboard.FocusedElement);
        Assert.Equal(1, gate.Raised);
    });

    /// <summary>A button whose nearest focusable ancestor is a tab control, which is no
    /// stop: the keys land through the scope's landing. They do not climb on to a focusable
    /// ancestor farther up, as the empty pane's host stands over the editor's tabs.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void AButtonUnderATabControlLandsThroughItsScope(string kind) => RunSta(() =>
    {
        using var stage = new Stage();
        using var gate = new Gate(kind);
        var button = new Button { Content = "Act", Command = gate.Command };
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "Tab", Content = Panel(button) });
        var farther = new ContentControl { Focusable = true, Content = tabs };
        stage.Content.Children.Add(farther);
        stage.Settle();
        TakeTheKeys(button);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the button under the tab control", button, gate.Refuse);

        Assert.Same(stage.Home, Keyboard.FocusedElement);
    });

    /// <summary>A button with nothing focusable above it but the window: the keys go to
    /// the scope's landing.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void AButtonWithNoFocusableAncestorGoesToItsScopesLanding(string kind) => RunSta(() =>
    {
        using var stage = new Stage();
        using var gate = new Gate(kind);
        var button = new Button { Content = "Act", Command = gate.Command };
        stage.Content.Children.Add(button);
        stage.Settle();
        TakeTheKeys(button);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the button", button, gate.Refuse);

        Assert.Same(stage.Home, Keyboard.FocusedElement);
    });

    /// <summary>The button's own landing comes first, as it does when the guard lands a
    /// stranded element.</summary>
    [Fact]
    public void TheButtonsOwnLandingComesFirst() => RunSta(() =>
    {
        using var stage = new Stage();
        using var gate = new Gate("relay");
        var button = new Button { Content = "Act", Command = gate.Command };
        var next = new Button { Content = "Next" };
        var group = new Expander { Header = "Actions", IsExpanded = true, Focusable = true, Content = Panel(button, next) };
        stage.Content.Children.Add(group);
        RegionFocusGuard.SetStrandedLanding(button, () => next.Focus());
        stage.Settle();
        TakeTheKeys(button);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the button", button, gate.Refuse);

        Assert.Same(next, Keyboard.FocusedElement);
    });

    /// <summary>A landing that walks for a first stop passes over the control that is
    /// leaving: here the scope's landing is a plain items host whose first item is the
    /// button going off.</summary>
    [Fact]
    public void ALandingNeverPicksTheControlThatIsLeaving() => RunSta(() =>
    {
        using var stage = new Stage();
        using var gate = new Gate("relay");
        var button = new Button { Content = "Act", Command = gate.Command };
        var other = new Button { Content = "Other" };
        var items = new ItemsControl { Focusable = false };
        items.Items.Add(button);
        items.Items.Add(other);
        stage.Content.Children.Add(items);
        stage.Landing = () => SelectorFocus.LandOnStop(items);
        stage.Settle();
        TakeTheKeys(button);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the first item", button, gate.Refuse);

        Assert.Same(other, Keyboard.FocusedElement);
    });

    /// <summary>A set that goes off together: the landing passes over every member and
    /// everything inside one, and the call answers true only once the keys are outside the
    /// set.</summary>
    [Fact]
    public void ALandingNeverPicksTheLeavingSetOrAnythingInsideIt() => RunSta(() =>
    {
        using var stage = new Stage();
        var inside = new Button { Content = "Inside" };
        var held = new Button { Content = "Held" };
        StackPanel section = Panel(inside, held);
        var alone = new Button { Content = "Alone" };
        var other = new Button { Content = "Other" };
        var items = new ItemsControl { Focusable = false };
        items.Items.Add(section);
        items.Items.Add(alone);
        items.Items.Add(other);
        stage.Content.Children.Add(items);
        stage.Landing = () => SelectorFocus.LandOnStop(items);
        stage.Settle();
        TakeTheKeys(held);
        var leavingSeen = new List<bool>();
        Func<bool> land = stage.Landing;
        stage.Landing = () =>
        {
            leavingSeen.AddRange([RegionFocusGuard.IsLeaving(inside), RegionFocusGuard.IsLeaving(alone), RegionFocusGuard.IsLeaving(other)]);
            Assert.Same(held, RegionFocusGuard.Holder);
            return land();
        };

        Assert.True(RegionFocusGuard.LandBeforeLeaving([section, alone]));

        Assert.Same(other, Keyboard.FocusedElement);
        Assert.Equal([true, true, false], leavingSeen);
        Assert.False(RegionFocusGuard.IsLeaving(inside), "the leaving set outlived its landing");
        Assert.Null(RegionFocusGuard.Holder);
    });

    /// <summary>When the only stop left is inside the leaving set, nothing takes the keys
    /// and the call says so: the control then goes off as it did, and the guard lands
    /// after.</summary>
    [Fact]
    public void WithNowhereElseToLandItAnswersFalse() => RunSta(() =>
    {
        using var stage = new Stage();
        var button = new Button { Content = "Act" };
        stage.Content.Children.Add(button);
        stage.Landing = () => button.Focus();
        stage.Settle();
        TakeTheKeys(button);

        Assert.False(RegionFocusGuard.LandBeforeLeaving(button));
        Assert.Same(button, Keyboard.FocusedElement);

        // A landing that puts the keys on another control of the set that is leaving has
        // not landed them: that control goes off too.
        var beside = new Button { Content = "Beside" };
        StackPanel section = Panel(beside);
        stage.Content.Children.Remove(button);
        section.Children.Add(button);
        stage.Content.Children.Add(section);
        stage.Settle();
        TakeTheKeys(button);
        stage.Landing = () => beside.Focus();
        Assert.False(RegionFocusGuard.LandBeforeLeaving([section]));
        Assert.Same(beside, Keyboard.FocusedElement);

        // And the keys somewhere else: nothing is asked of the landing at all.
        TakeTheKeys(stage.Outside);
        int landings = 0;
        stage.Landing = () => ++landings > 0 && stage.Home.Focus();
        Assert.False(RegionFocusGuard.LandBeforeLeaving(button));
        Assert.Equal(0, landings);
        Assert.Same(stage.Outside, Keyboard.FocusedElement);
    });

    /// <summary>A command that still admits the control holding the keys moves nothing; a
    /// change raised by another command moves nothing; and the control's own parameter is
    /// the one that is asked.</summary>
    [Fact]
    public void ACommandThatStillAdmitsMovesNothing() => RunSta(() =>
    {
        using var stage = new Stage();
        bool refuseX = false;
        var command = new RelayCommand(_ => { }, parameter => !(refuseX && Equals(parameter, "x")));
        using var other = new Gate("relay");
        var admitted = new Button { Content = "Y", Command = command, CommandParameter = "y" };
        var refused = new Button { Content = "X", Command = command, CommandParameter = "x" };
        stage.Content.Children.Add(admitted);
        stage.Content.Children.Add(refused);
        stage.Settle();
        TakeTheKeys(admitted);

        Assert.Empty(KeysFirstRecorder.DisabledWithTheKeysInside(admitted, command.RaiseCanExecuteChanged));
        Assert.Same(admitted, Keyboard.FocusedElement);

        other.Refuse();
        Assert.Same(admitted, Keyboard.FocusedElement);

        refuseX = true;
        command.RaiseCanExecuteChanged();
        Assert.Same(admitted, Keyboard.FocusedElement);
        Assert.True(admitted.IsEnabled);
        Assert.False(refused.IsEnabled);

        refuseX = false;
        command.RaiseCanExecuteChanged();
        TakeTheKeys(refused);
        refuseX = true;
        KeysFirstRecorder.AssertTheKeysLeaveFirst("the button whose parameter is refused", refused, command.RaiseCanExecuteChanged);
        Assert.Same(stage.Home, Keyboard.FocusedElement);
    });

    /// <summary>A command raised from a thread with no dispatcher raises as before and makes
    /// the thread no dispatcher: no <see cref="Keyboard"/> member is read there, since
    /// reading one gives the thread a dispatcher and an input manager. The thread here is
    /// STA, so the dispatcher is the only thing that tells it from one that holds keys; a
    /// worker's completion, which is neither, is the same case.</summary>
    [Fact]
    public void ARaiseOnAThreadWithNoDispatcherTouchesNothing()
    {
        int raised = 0;
        bool dispatcherBefore = true;
        bool dispatcherAfter = true;
        StaThread.Run(
            () =>
            {
                var command = new RelayCommand(_ => { }, _ => false);
                command.CanExecuteChanged += (_, _) => raised++;
                dispatcherBefore = Dispatcher.FromThread(Thread.CurrentThread) is not null;
                command.RaiseCanExecuteChanged();
                using (CommandKeysFirst.Hold())
                {
                    command.RaiseCanExecuteChanged();
                }

                dispatcherAfter = Dispatcher.FromThread(Thread.CurrentThread) is not null;
            },
            TimeSpan.FromSeconds(60),
            "the raise never returned");

        Assert.Equal(2, raised);
        Assert.False(dispatcherBefore, "premise: the thread had a dispatcher before the raise");
        Assert.False(dispatcherAfter, "the raise gave a thread with no dispatcher one");
    }

    /// <summary>The runner's own threads are not STA, and production code that captures
    /// <see cref="Dispatcher.CurrentDispatcher"/> gives one a dispatcher when a plain fact
    /// builds a view model there. Such a thread hosts no window and cannot have an input
    /// manager (WPF refuses to make one off an STA thread), so the raise asks nothing of
    /// the keys there.</summary>
    [Fact]
    public void ARaiseOnAThreadThatIsNotStaTouchesNothingThoughItHasADispatcher()
    {
        Assert.NotEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
        _ = Dispatcher.CurrentDispatcher;
        int raised = 0;
        var command = new RelayCommand(_ => { }, _ => false);
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }

    /// <summary>On a thread with a dispatcher and no window nothing holds the keys, and
    /// the raise is all that happens.</summary>
    [Fact]
    public void ARaiseOnAThreadWithADispatcherAndNoWindowTouchesNothing() => RunSta(() =>
    {
        using var gate = new Gate("relay");
        Assert.NotNull(Dispatcher.FromThread(Thread.CurrentThread));

        gate.Refuse();

        Assert.Equal(1, gate.Raised);
        Assert.Null(Keyboard.FocusedElement);
    });

    /// <summary>A row of an open menu is left alone: a menu row is no site, and the menu
    /// owns its own keys. The row goes off under them as it does today.</summary>
    [Fact]
    public void ARowOfAnOpenMenuIsLeftAlone() => RunSta(() =>
    {
        using var stage = new Stage();
        using var gate = new Gate("relay");
        var row = new MenuItem { Header = "Act", Command = gate.Command };
        var top = new MenuItem { Header = "File" };
        top.Items.Add(row);
        var menu = new Menu();
        menu.Items.Add(top);
        stage.Content.Children.Insert(0, menu);
        _ = stage.Window.Activate();
        stage.Settle();
        top.IsSubmenuOpen = true;
        stage.Settle();
        if (!row.IsVisible)
        {
            _ = KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu("the open menu's row is not on screen");
        }

        KeysFirstRecorder.TakeTheMenuKeys(row);

        gate.Refuse();

        Assert.Same(row, Keyboard.FocusedElement);
        Assert.False(row.IsEnabled);
        top.IsSubmenuOpen = false;
        stage.Settle();
    });

    /// <summary>Inside a held scope a raise is put off, and when the scope ends each
    /// command is raised once, in the order of its first raise. A scope inside another
    /// joins it.</summary>
    [Fact]
    public void AHeldScopeRaisesEachCommandOnceAtItsEnd() => RunSta(() =>
    {
        var order = new List<string>();
        var first = new RelayCommand(_ => { }, _ => true);
        var second = new ReasonedCommand(() => { }, () => null);
        var third = new AsyncRelayCommand(_ => Task.CompletedTask, () => true);
        first.CanExecuteChanged += (_, _) => order.Add("first");
        second.CanExecuteChanged += (_, _) => order.Add("second");
        third.CanExecuteChanged += (_, _) => order.Add("third");

        using (CommandKeysFirst.Hold())
        {
            second.RaiseCanExecuteChanged();
            first.RaiseCanExecuteChanged();
            using (CommandKeysFirst.Hold())
            {
                third.RaiseCanExecuteChanged();
                second.RaiseCanExecuteChanged();
            }

            Assert.Empty(order);
            first.RaiseCanExecuteChanged();
            Assert.Empty(order);
        }

        Assert.Equal(["second", "first", "third"], order);

        first.RaiseCanExecuteChanged();
        Assert.Equal(["second", "first", "third", "first"], order);
    });

    /// <summary>A scope that ends by an exception still raises what it held, and holds
    /// nothing afterwards.</summary>
    [Fact]
    public void AHeldScopeReleasesOnAThrow() => RunSta(() =>
    {
        int raised = 0;
        var command = new RelayCommand(_ => { }, _ => true);
        command.CanExecuteChanged += (_, _) => raised++;

        int raisedInside = -1;
        Action publication = () =>
        {
            using (CommandKeysFirst.Hold())
            {
                command.RaiseCanExecuteChanged();
                raisedInside = raised;
                throw new InvalidOperationException("the publication failed");
            }
        };
        Assert.Throws<InvalidOperationException>(publication);

        Assert.Equal(0, raisedInside);
        Assert.Equal(1, raised);
        command.RaiseCanExecuteChanged();
        Assert.Equal(2, raised);
    });

    /// <summary>A hold is its thread's: a raise on another thread is not put off by
    /// it.</summary>
    [Fact]
    public void AHoldIsItsThreadsOwn() => RunSta(() =>
    {
        int raised = 0;
        var command = new RelayCommand(_ => { }, _ => true);
        command.CanExecuteChanged += (_, _) => Interlocked.Increment(ref raised);

        using (CommandKeysFirst.Hold())
        {
            var elsewhere = new Thread(command.RaiseCanExecuteChanged);
            elsewhere.Start();
            Assert.True(elsewhere.Join(TimeSpan.FromSeconds(30)), "the raise on the other thread never returned");
            Assert.Equal(1, Volatile.Read(ref raised));
        }

        Assert.Equal(1, Volatile.Read(ref raised));
    });

    /// <summary>A held raise is delivered through the same check: while the scope is open
    /// the button stays on with the keys on it, and at its end the keys move before the
    /// button goes off. A command that refuses and admits again inside one scope never
    /// turns its button off at all.</summary>
    [Fact]
    public void AHeldRaiseMovesTheKeysFirstWhenItIsDelivered() => RunSta(() =>
    {
        using var stage = new Stage();
        bool on = true;
        var command = new RelayCommand(_ => { }, _ => on);
        var button = new Button { Content = "Act", Command = command };
        stage.Content.Children.Add(button);
        stage.Settle();
        TakeTheKeys(button);

        bool wentOff = false;
        DependencyPropertyChangedEventHandler off = (_, e) => wentOff |= e.NewValue is false;
        button.IsEnabledChanged += off;
        using (CommandKeysFirst.Hold())
        {
            on = false;
            command.RaiseCanExecuteChanged();
            on = true;
            command.RaiseCanExecuteChanged();
        }

        button.IsEnabledChanged -= off;
        Assert.False(wentOff, "a command that refused and admitted again inside one scope turned its button off");
        Assert.Same(button, Keyboard.FocusedElement);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("the button", button, () =>
        {
            using (CommandKeysFirst.Hold())
            {
                on = false;
                command.RaiseCanExecuteChanged();
                Assert.True(button.IsEnabled, "the button went off inside the scope");
                Assert.Same(button, Keyboard.FocusedElement);
            }
        });

        Assert.Same(stage.Home, Keyboard.FocusedElement);
    });

    /// <summary>The real window's sheets land on their first stop
    /// (<c>MainWindow.FirstFocusable</c>): with that stop leaving, the landing takes the
    /// next one, and with the first two leaving, the third.</summary>
    [Fact]
    public void ASheetsLandingPassesOverTheLeavingSet() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\nstatus: draft\n---\n# A\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        host.Workspace.OpenAddPropertySheet();
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName("AddPropertyOverlay");
        UIElement first = host.Shell.AddPropertyKeyTextBox;
        TakeTheKeys(first);

        Assert.True(RegionFocusGuard.LandBeforeLeaving(first), $"nothing took the keys from the sheet's first stop (they are on {Keyboard.FocusedElement})");

        var second = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        Assert.NotSame(first, second);
        Assert.True(KeysFirstRecorder.TheKeysAreIn(overlay), $"the keys left the sheet for {second}");

        TakeTheKeys(first);
        Assert.True(RegionFocusGuard.LandBeforeLeaving([first, second]));

        var third = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        Assert.NotSame(first, third);
        Assert.NotSame(second, third);
        Assert.True(KeysFirstRecorder.TheKeysAreIn(overlay), $"the keys left the sheet for {third}");
    });

    /// <summary>The Properties header's rows land on the same property's same control
    /// (<see cref="PropertiesLanding"/>): with that control leaving, the landing takes
    /// another stop of the same property.</summary>
    [Fact]
    public void ThePropertiesLandingPassesOverTheLeavingControl() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", "---\ntitle: Hello\nstatus: draft\n---\nThe body.\n"));
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        NotePropertiesViewModel properties = host.Workspace.ActiveGroup.ActiveTab?.Properties
            ?? throw new Xunit.Sdk.XunitException("premise: the markdown tab has no Properties header");
        properties.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Count == 2, TimeSpan.FromSeconds(30)),
            $"premise: the header published {properties.Rows.Count} rows");
        host.Settle();
        PropertyRowViewModel title = properties.Rows.Single(row => row.KeyIdentity == "title");
        TextBox field = ShownShell.Descendants(host.Shell).OfType<TextBox>()
            .Single(control => control.IsVisible && ReferenceEquals(control.DataContext, title));
        TakeTheKeys(field);

        Assert.True(RegionFocusGuard.LandBeforeLeaving(field), $"nothing took the keys from the property's field (they are on {Keyboard.FocusedElement})");

        var now = Assert.IsAssignableFrom<FrameworkElement>(Keyboard.FocusedElement);
        Assert.NotSame(field, now);
        Assert.Same(title, now.DataContext);
    });

    /// <summary>Red team F3: an AsyncRelayCommand whose work ends inside its own press (a
    /// completed or a faulted task) turned off and on again in that one turn. WPF left the
    /// keys on its button then, since the button was on again before WPF looked; the off
    /// must not move them now. The press's state is raised once, at its end.</summary>
    [Theory]
    [InlineData("completes")]
    [InlineData("faults")]
    public void AnAsyncCommandWhoseWorkEndsInItsOwnPressKeepsTheKeysOnItsButton(string ending) => RunSta(() =>
    {
        using var stage = new Stage();
        int ran = 0;
        var command = new AsyncRelayCommand(
            _ =>
            {
                ran++;
                return ending == "faults" ? throw new InvalidOperationException("the work failed at once") : Task.CompletedTask;
            },
            () => true);
        int raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;
        var button = new Button { Content = "Act", Command = command };
        var group = new Expander { Header = "Actions", IsExpanded = true, Focusable = true, Content = Panel(button) };
        stage.Content.Children.Add(group);
        stage.Settle();
        TakeTheKeys(button);

        List<string> off = KeysFirstRecorder.DisabledWithTheKeysInside(button, () =>
        {
            command.Execute(null);
            stage.Settle();
        });

        Assert.Equal(1, ran);
        Assert.Empty(off);
        Assert.True(button.IsEnabled, "the button stayed off after its work ended");
        Assert.True(ReferenceEquals(button, Keyboard.FocusedElement), $"the keys left the button for {Keyboard.FocusedElement}");
        Assert.True(raised <= 1, $"the press raised its state {raised} times");
    });

    private static StackPanel Panel(params UIElement[] children)
    {
        var panel = new StackPanel();
        foreach (UIElement child in children)
        {
            panel.Children.Add(child);
        }

        return panel;
    }

    private static void TakeTheKeys(UIElement element) => Assert.True(
        element.Focus() && ReferenceEquals(element, Keyboard.FocusedElement),
        $"keyboard premise: {element} could not take the keys (they are on {Keyboard.FocusedElement})");

    /// <summary>One of the three command classes, and the act that makes it refuse its
    /// control and raise: a predicate that turns false, a reason that appears, or the
    /// command's own press while its work is held.</summary>
    private sealed class Gate : IDisposable
    {
        private readonly TaskCompletionSource _held = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Action _refuse;
        private bool _on = true;

        public Gate(string kind)
        {
            switch (kind)
            {
                case "relay":
                    var relay = new RelayCommand(_ => { }, _ => _on);
                    Command = relay;
                    _refuse = () =>
                    {
                        _on = false;
                        relay.RaiseCanExecuteChanged();
                    };
                    break;
                case "reasoned":
                    var reasoned = new ReasonedCommand(() => { }, () => _on ? null : "Wait for the work to finish.");
                    Command = reasoned;
                    _refuse = () =>
                    {
                        _on = false;
                        reasoned.RaiseCanExecuteChanged();
                    };
                    break;
                case "async":
                    var asynchronous = new AsyncRelayCommand(_ => _held.Task, () => true);
                    Command = asynchronous;
                    _refuse = () => asynchronous.Execute(null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "no such command class");
            }

            Command.CanExecuteChanged += (_, _) => Raised++;
        }

        public ICommand Command { get; }

        public int Raised { get; private set; }

        public void Refuse() => _refuse();

        public void Dispose()
        {
            _held.TrySetResult();
            PumpedDispatcher.Drain();
        }
    }

    /// <summary>A window with a scope that owns a guard landing, its landing's stop first
    /// in it, and a button outside the scope.</summary>
    private sealed class Stage : IDisposable
    {
        public Stage()
        {
            RegionFocusGuard.Register();
            Home = new Button { Content = "Home" };
            Content = new StackPanel();
            Content.Children.Add(Home);
            var scope = new Border { Child = Content };
            Landing = () => Home.Focus();
            RegionFocusGuard.SetLanding(scope, () => Landing());
            Outside = new Button { Content = "Outside" };
            var root = new DockPanel();
            DockPanel.SetDock(Outside, Dock.Top);
            root.Children.Add(Outside);
            root.Children.Add(scope);
            Window = new Window
            {
                Content = root,
                Width = 500,
                Height = 400,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            Window.Show();
            Window.UpdateLayout();
        }

        public Window Window { get; }

        public StackPanel Content { get; }

        public Button Home { get; }

        public Button Outside { get; }

        public Func<bool> Landing { get; set; }

        public void Settle()
        {
            Window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public void Dispose() => Window.Close();
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
