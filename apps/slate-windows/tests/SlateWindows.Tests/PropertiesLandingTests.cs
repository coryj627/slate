// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b round 3 (codex r2 F3; R-5 (h), "a removed row's keys come back
/// to the rows they were in"): the Properties header rebuilds its lists under
/// the keys. Remove on a list item rebuilds the row's items
/// (<see cref="PropertyRowViewModel.Items"/>, RebuildItems), and every
/// committed edit — Enter, the boolean switch — republishes every row
/// (<see cref="NotePropertiesViewModel.Rows"/>, PublishProperties). The keys
/// went out of the header, to the note's editor body, the editor region's
/// landing. They land back in the header, on the same place — the same
/// item's control, clamped; the same property's control — in one focus
/// change. The shipped window, shown, over a markdown note with properties.
/// </summary>
public sealed class PropertiesLandingTests
{
    private const string Note =
        "---\ncolors: [red, green, blue]\nsolo: [only]\ntitle: Hello\ndone: false\n---\nThe body.\n";

    /// <summary>Remove on the first, middle and last item: the keys land on
    /// the Remove of the item now at that place — the last one when the last
    /// went.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void RemovingAListItemLandsOnTheRemoveNowInItsPlace(int removed, int landed) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        ButtonBase remove = ItemControl<ButtonBase>(host, "colors", removed, item => item.RemoveCommand);
        Assert.True(remove.Focus(), "premise: the item's Remove refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        Click(remove);
        PumpedDispatcher.Drain();

        PropertyRowViewModel colors = Row(properties, "colors");
        Assert.Equal(2, colors.Items.Count);
        var now = Assert.IsAssignableFrom<ButtonBase>(Keyboard.FocusedElement);
        var item = Assert.IsType<PropertyListItemViewModel>(now.DataContext);
        Assert.Same(colors, item.Row);
        Assert.Equal(landed, item.Index);
        Assert.Same(item.RemoveCommand, now.Command);
        Assert.Single(changes);
    });

    /// <summary>Remove on the only item: the keys land on the row's Add
    /// item.</summary>
    [Fact]
    public void RemovingTheOnlyItemLandsOnAddItem() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        ButtonBase remove = ItemControl<ButtonBase>(host, "solo", 0, item => item.RemoveCommand);
        Assert.True(remove.Focus(), "premise: the item's Remove refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        Click(remove);
        PumpedDispatcher.Drain();

        PropertyRowViewModel solo = Row(properties, "solo");
        Assert.Empty(solo.Items);
        var now = Assert.IsAssignableFrom<ButtonBase>(Keyboard.FocusedElement);
        Assert.Same(solo.AddItemCommand, now.Command);
        Assert.Single(changes);
    });

    /// <summary>Enter in a property's editor commits it, and the write's
    /// refresh republishes every row: the keys land on the same property's
    /// editor.</summary>
    [Fact]
    public void AnEnterCommitLandsOnTheSamePropertysEditor() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel title = Row(properties, "title");
        TextBox editor = RowControl<TextBox>(host, title);
        Assert.True(editor.Focus(), "premise: the title's editor refused the keys");
        editor.Text = "Hello there";
        List<IInputElement> changes = host.RecordFocusChanges();

        title.CommitCommand.Execute(null);
        AwaitRepublished(properties, title);

        var now = Assert.IsType<TextBox>(Keyboard.FocusedElement);
        var fresh = Assert.IsType<PropertyRowViewModel>(now.DataContext);
        Assert.NotSame(title, fresh);
        Assert.Equal("title", fresh.KeyIdentity);
        Assert.Equal("Hello there", now.Text);
        Assert.Single(changes);
    });

    /// <summary>Save disables itself while its write is in flight, under the
    /// keys, and the write's refresh then republishes every row: the keys go
    /// to the same property's editor, and stay on it through the republish —
    /// never to the header, the window or the note's body.</summary>
    [Fact]
    public void ASaveClickLandsOnTheSamePropertysEditor() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel title = Row(properties, "title");
        RowControl<TextBox>(host, title).Text = "Hello there";
        host.Settle();
        ButtonBase save = ShownShell.Descendants(host.Shell).OfType<ButtonBase>()
            .Single(button => button.IsVisible && ReferenceEquals(button.DataContext, title) && ReferenceEquals(button.Command, title.CommitCommand));
        Assert.True(save.IsEnabled && save.Focus(), "premise: Save refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        Click(save);
        AwaitRepublished(properties, title);

        var now = Assert.IsType<TextBox>(Keyboard.FocusedElement);
        var fresh = Assert.IsType<PropertyRowViewModel>(now.DataContext);
        Assert.Equal("title", fresh.KeyIdentity);
        Assert.Equal("Hello there", now.Text);
        Assert.All(changes, change => Assert.IsType<TextBox>(change));
    });

    /// <summary>Enter in a list item's editor commits the row: the keys land
    /// on the same item's editor in the republished row.</summary>
    [Fact]
    public void AnEnterCommitInAListItemLandsOnTheSameItem() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel colors = Row(properties, "colors");
        TextBox editor = ItemControl<TextBox>(host, "colors", 1, item => null);
        Assert.True(editor.Focus(), "premise: the item's editor refused the keys");
        editor.Text = "teal";
        List<IInputElement> changes = host.RecordFocusChanges();

        colors.CommitCommand.Execute(null);
        AwaitRepublished(properties, colors);

        var now = Assert.IsType<TextBox>(Keyboard.FocusedElement);
        var item = Assert.IsType<PropertyListItemViewModel>(now.DataContext);
        Assert.NotSame(colors, item.Row);
        Assert.Equal("colors", item.Row.KeyIdentity);
        Assert.Equal(1, item.Index);
        Assert.Equal("teal", now.Text);
        Assert.Single(changes);
    });

    /// <summary>The boolean switch commits on the click: the keys land on
    /// the same property's switch.</summary>
    [Fact]
    public void ABooleanToggleLandsOnTheSameSwitch() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel done = Row(properties, "done");
        CheckBox toggle = RowControl<CheckBox>(host, done);
        Assert.True(toggle.Focus(), "premise: the switch refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        Click(toggle);
        AwaitRepublished(properties, done);

        var now = Assert.IsType<CheckBox>(Keyboard.FocusedElement);
        var fresh = Assert.IsType<PropertyRowViewModel>(now.DataContext);
        Assert.NotSame(done, fresh);
        Assert.Equal("done", fresh.KeyIdentity);
        Assert.True(now.IsChecked);
        Assert.Single(changes);
    });

    /// <summary>A property gone from the file under the keys (a refresh from
    /// disk): the keys land on the row now in its place — its first stop,
    /// since a switch has no text editor.</summary>
    [Fact]
    public void APropertyGoneUnderTheKeysLandsOnTheRowNowInItsPlace() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel title = Row(properties, "title");
        TextBox editor = RowControl<TextBox>(host, title);
        Assert.True(editor.Focus(), "premise: the title's editor refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Rewrite("a.md", "---\ncolors: [red, green, blue]\nsolo: [only]\ndone: false\n---\nThe body.\n");
        properties.RefreshProperties();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Count == 3, TimeSpan.FromSeconds(30)),
            "premise: the refresh never dropped the title");
        PumpedDispatcher.Drain();

        var now = Assert.IsType<CheckBox>(Keyboard.FocusedElement);
        Assert.Equal("done", Assert.IsType<PropertyRowViewModel>(now.DataContext).KeyIdentity);
        Assert.Single(changes);
    });

    /// <summary>A refresh from disk that republishes every row while the keys
    /// rest on a property's Delete: they land on the same property's
    /// Delete, not its editor.</summary>
    [Fact]
    public void ARefreshUnderTheKeysKeepsThemOnTheSameControl() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel title = Row(properties, "title");
        Assert.True(DeleteOf(host, title).Focus(), "premise: the title's Delete refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Rewrite("a.md", Note.Replace("[red, green, blue]", "[red, green, teal]", StringComparison.Ordinal));
        properties.RefreshProperties();
        AwaitRepublished(properties, title);

        var now = Assert.IsAssignableFrom<ButtonBase>(Keyboard.FocusedElement);
        var fresh = Assert.IsType<PropertyRowViewModel>(now.DataContext);
        Assert.NotSame(title, fresh);
        Assert.Equal("title", fresh.KeyIdentity);
        Assert.Same(fresh.DeleteCommand, now.Command);
        Assert.Single(changes);
    });

    /// <summary>A property gone under the keys on its Delete: they land on
    /// the Delete of the row now in its place.</summary>
    [Fact]
    public void APropertyGoneUnderItsDeleteLandsOnTheDeleteNowInItsPlace() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        Assert.True(DeleteOf(host, Row(properties, "title")).Focus(), "premise: the title's Delete refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Rewrite("a.md", "---\ncolors: [red, green, blue]\nsolo: [only]\ndone: false\n---\nThe body.\n");
        properties.RefreshProperties();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Count == 3, TimeSpan.FromSeconds(30)),
            "premise: the refresh never dropped the title");
        PumpedDispatcher.Drain();

        var now = Assert.IsAssignableFrom<ButtonBase>(Keyboard.FocusedElement);
        var neighbour = Assert.IsType<PropertyRowViewModel>(now.DataContext);
        Assert.Equal("done", neighbour.KeyIdentity);
        Assert.Same(neighbour.DeleteCommand, now.Command);
        Assert.Single(changes);
    });

    /// <summary>A property whose kind changes under the keys, and moves: the
    /// switch the keys were on is gone from its row, and they land on the
    /// same property's new editor — not on the row now at its old
    /// place.</summary>
    [Fact]
    public void APropertyWhoseKindChangesUnderTheKeysLandsOnItsNewEditor() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        PropertyRowViewModel done = Row(properties, "done");
        Assert.True(RowControl<CheckBox>(host, done).Focus(), "premise: the switch refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Rewrite("a.md", "---\ndone: maybe\ncolors: [red, green, blue]\nsolo: [only]\ntitle: Hello\n---\nThe body.\n");
        properties.RefreshProperties();
        AwaitRepublished(properties, done);

        var now = Assert.IsType<TextBox>(Keyboard.FocusedElement);
        var fresh = Assert.IsType<PropertyRowViewModel>(now.DataContext);
        Assert.Equal("done", fresh.KeyIdentity);
        Assert.Equal("maybe", now.Text);
        Assert.Single(changes);
    });

    /// <summary>The last property gone under the keys: the rows' list
    /// collapses with it, and the keys land on the header's Add
    /// property.</summary>
    [Fact]
    public void TheLastPropertyGoneUnderTheKeysLandsOnAddProperty() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        NotePropertiesViewModel properties = OpenProperties(host);
        TextBox editor = RowControl<TextBox>(host, Row(properties, "title"));
        Assert.True(editor.Focus(), "premise: the title's editor refused the keys");
        List<IInputElement> changes = host.RecordFocusChanges();

        host.Rewrite("a.md", "The body, and no properties.\n");
        properties.RefreshProperties();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Count == 0, TimeSpan.FromSeconds(30)),
            "premise: the refresh never dropped the properties");
        PumpedDispatcher.Drain();

        var now = Assert.IsAssignableFrom<FrameworkElement>(Keyboard.FocusedElement);
        Assert.Equal("PropertiesAddButton", System.Windows.Automation.AutomationProperties.GetAutomationId(now));
        Assert.Single(changes);
    });

    private const string DatedNote = "---\ndue: 2026-09-10\ntitle: Hello\n---\nThe body.\n";

    /// <summary>
    /// PR 4b's last codex check (the owner's decision): a date property's
    /// calendar commits once, when it closes. The arrows move the calendar's
    /// day without a write — each arrow wrote the note and republished every
    /// row, which closed the calendar after one step and landed the keys in
    /// the note's body. The arrows move from the row's day (a fresh WPF
    /// calendar moved them from today's). Enter closes it: ONE write, the day
    /// two arrows on, and the keys on the fresh row's date field.
    /// </summary>
    [Fact]
    public void ArrowsInTheCalendarCommitOnceWhenEnterClosesIt() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", DatedNote));
        NotePropertiesViewModel properties = OpenProperties(host, rows: 2);
        PropertyRowViewModel due = Row(properties, "due");
        StrongBox<int> writes = CountRepublications(properties);
        DatePicker picker = OpenCalendar(host, due);

        Press(Key.Right);
        Assert.Equal(new DateTime(2026, 9, 11), picker.SelectedDate);
        Press(Key.Right);
        Assert.Equal(new DateTime(2026, 9, 12), picker.SelectedDate);
        Assert.True(picker.IsDropDownOpen, "an arrow closed the calendar");
        Assert.Equal(0, writes.Value);
        List<IInputElement> changes = host.RecordFocusChanges();
        Press(Key.Enter);
        AwaitRepublished(properties, due);

        Assert.Equal(1, writes.Value);
        Assert.Contains("due: 2026-09-12", host.Read("a.md"), StringComparison.Ordinal);
        var now = Assert.IsType<DatePickerTextBox>(Keyboard.FocusedElement);
        PropertyRowViewModel fresh = Row(properties, "due");
        Assert.NotSame(due, fresh);
        Assert.Same(fresh, now.DataContext);
        Assert.Single(changes, change => ReferenceEquals(change, now));
        Assert.All(changes, change => Assert.IsType<DatePickerTextBox>(change));
    });

    /// <summary>Escape closes the calendar and puts back the day it opened
    /// on — WPF's own DatePicker restores it — so nothing is written, and the
    /// keys stay on the date field.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EscapeFromTheCalendarRevertsAndWritesNothing(bool animated) => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", DatedNote));
        NotePropertiesViewModel properties = OpenProperties(host, rows: 2);
        PropertyRowViewModel due = Row(properties, "due");
        StrongBox<int> writes = CountRepublications(properties);
        if (!animated)
        {
            // With the system's animations off, the calendar's popup closes
            // at once, inside the key press, before the picker restores the
            // day it opened on.
            Assert.IsType<Popup>(RowControl<DatePicker>(host, due).Template.FindName("PART_Popup", RowControl<DatePicker>(host, due)))
                .PopupAnimation = PopupAnimation.None;
        }

        DatePicker picker = OpenCalendar(host, due);

        Press(Key.Right);
        Press(Key.Right);
        Press(Key.Escape);
        PumpedDispatcher.Drain();

        Assert.False(picker.IsDropDownOpen);
        Assert.False(properties.IsLoading, "a write started");
        Assert.Equal(0, writes.Value);
        Assert.Same(due, Row(properties, "due"));
        Assert.Equal(new DateTime(2026, 9, 10), due.DateValue);
        Assert.Equal(new DateTime(2026, 9, 10), picker.SelectedDate);
        Assert.Contains("due: 2026-09-10", host.Read("a.md"), StringComparison.Ordinal);
        Assert.IsType<DatePickerTextBox>(Keyboard.FocusedElement);
    });

    /// <summary>A day picked with the pointer closes the calendar (WPF's
    /// day-button release) and commits once.</summary>
    [Fact]
    public void APointerPickedDayCommitsOnce() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", DatedNote));
        NotePropertiesViewModel properties = OpenProperties(host, rows: 2);
        PropertyRowViewModel due = Row(properties, "due");
        StrongBox<int> writes = CountRepublications(properties);
        DatePicker picker = OpenCalendar(host, due);
        CalendarDayButton day = CalendarDays(picker).First(button => button.DataContext is DateTime { Month: 9, Day: 15 });

        day.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
        });
        day.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonUpEvent,
        });
        AwaitRepublished(properties, due);

        Assert.False(picker.IsDropDownOpen);
        Assert.Equal(1, writes.Value);
        Assert.Contains("due: 2026-09-15", host.Read("a.md"), StringComparison.Ordinal);
    });

    /// <summary>A calendar closed because its picker left the tree — its
    /// tab closed under it — commits nothing: the day was never
    /// chosen.</summary>
    [Fact]
    public void ACalendarClosedWithItsTabWritesNothing() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", DatedNote));
        NotePropertiesViewModel properties = OpenProperties(host, rows: 2);
        PropertyRowViewModel due = Row(properties, "due");
        StrongBox<int> writes = CountRepublications(properties);
        DatePicker picker = OpenCalendar(host, due);
        Press(Key.Right);
        Press(Key.Right);

        host.Workspace.CloseActiveTabCommand.Execute(null);
        PumpedDispatcher.Drain();
        // A write, had the close committed, lands off the dispatcher: give
        // it time to reach the disk before reading it.
        PumpedDispatcher.PumpUntil(
            () => !host.Read("a.md").Contains("due: 2026-09-10", StringComparison.Ordinal), TimeSpan.FromSeconds(3));
        PumpedDispatcher.Drain();

        Assert.False(picker.IsDropDownOpen);
        Assert.Empty(host.Workspace.ActiveGroup.Tabs);
        Assert.Equal(0, writes.Value);
        Assert.Contains("due: 2026-09-10", host.Read("a.md"), StringComparison.Ordinal);
    });

    /// <summary>
    /// #1318's final merge (W7-7 PR 4b over PR 6 follow-up B, #1280): a
    /// save's write runs off the dispatcher now, so a calendar can close
    /// while its note is still being written. The day it commits takes the
    /// property write's own way: the row's gates, the note's write lease and
    /// a check against the row's hash, never the tab's save chain. The tab
    /// stays dirty until its save publishes, so a day picked inside the save
    /// is refused, said once, and nothing is written over the save. The
    /// save's publication rebuilds the header from the bytes it wrote. A day
    /// picked after that writes once, over the saved body.
    /// </summary>
    [Fact]
    public void ADayPickedWhileItsNoteSavesWritesNothingUntilTheSavePublishes() => RunSta(() =>
    {
        const string Edit = "An edit on its way to disk.";
        using var host = new ShownShell(("a.md", DatedNote));
        NotePropertiesViewModel properties = OpenProperties(host, rows: 2);
        WorkspaceTabViewModel tab = Assert.IsType<WorkspaceTabViewModel>(host.Workspace.ActiveGroup.ActiveTab);
        tab.Text += $"\n{Edit}\n";
        Assert.True(tab.IsDirty, "premise: the edit left the tab clean");
        using var parked = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        tab.SaveWriteHookForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
        };
        PropertyRowViewModel due = Row(properties, "due");
        try
        {
            host.Workspace.SaveActiveCommand.Execute(null);
            Assert.True(parked.Wait(TimeSpan.FromSeconds(10)), "premise: the save's write never started");
            StrongBox<int> writes = CountRepublications(properties);
            host.Announced.Clear();

            DatePicker picker = OpenCalendar(host, due);
            Press(Key.Right);
            Press(Key.Enter);
            PumpedDispatcher.Drain();

            Assert.False(picker.IsDropDownOpen);
            Assert.True(tab.IsDirty, "the tab went clean before its save published");
            Assert.False(due.WriteInFlight, "a property write started inside the save");
            Assert.Equal(0, writes.Value);
            string during = host.Read("a.md");
            Assert.Contains("due: 2026-09-10", during, StringComparison.Ordinal);
            Assert.DoesNotContain(Edit, during, StringComparison.Ordinal);
            _ = Assert.Single(
                host.Announced.OfType<A11yEvent.HostComposed>(),
                line => line.Text.StartsWith("Save the note before editing properties.", StringComparison.Ordinal));
        }
        finally
        {
            release.Set();
        }

        Assert.True(PumpedDispatcher.PumpUntil(() => !tab.IsDirty, TimeSpan.FromSeconds(30)), "the save never published");
        AwaitRepublished(properties, due);
        string saved = host.Read("a.md");
        Assert.Contains(Edit, saved, StringComparison.Ordinal);
        Assert.Contains("due: 2026-09-10", saved, StringComparison.Ordinal);

        PropertyRowViewModel fresh = Row(properties, "due");
        StrongBox<int> after = CountRepublications(properties);
        DatePicker again = OpenCalendar(host, fresh);
        Press(Key.Right);
        DateTime picked = Assert.IsType<DateTime>(again.SelectedDate);
        Press(Key.Enter);
        AwaitRepublished(properties, fresh);

        Assert.Equal(1, after.Value);
        string written = host.Read("a.md");
        Assert.Contains("due: " + picked.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), written, StringComparison.Ordinal);
        Assert.Contains(Edit, written, StringComparison.Ordinal);
        Assert.False(tab.IsDirty);
    });

    /// <summary>Counts the header's republications: one per write, whose
    /// refresh clears and rebuilds every row.</summary>
    private static StrongBox<int> CountRepublications(NotePropertiesViewModel properties)
    {
        var count = new StrongBox<int>();
        properties.Rows.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                count.Value++;
            }
        };
        return count;
    }

    /// <summary>The row's date field focused, and its calendar opened as
    /// Alt+Down opens it, the keys on the calendar's day.</summary>
    private static DatePicker OpenCalendar(ShownShell host, PropertyRowViewModel row)
    {
        DatePicker picker = RowControl<DatePicker>(host, row);
        // The picker hands the keys to its text field.
        _ = picker.Focus();
        Assert.True(picker.IsKeyboardFocusWithin, "premise: the date field refused the keys");
        picker.IsDropDownOpen = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => Keyboard.FocusedElement is CalendarDayButton, TimeSpan.FromSeconds(10)),
            $"premise: the calendar never took the keys ({Keyboard.FocusedElement})");
        PumpedDispatcher.Drain();
        Assert.Equal(
            picker.SelectedDate,
            Assert.IsType<CalendarDayButton>(Keyboard.FocusedElement).DataContext as DateTime?);
        return picker;
    }

    private static IEnumerable<CalendarDayButton> CalendarDays(DatePicker picker) =>
        picker.Template.FindName("PART_Popup", picker) is Popup { Child: { } calendar }
            ? ShownShell.Descendants(calendar).OfType<CalendarDayButton>()
            : [];

    /// <summary>A key press on the element holding the keys, as the keyboard
    /// raises it: the preview, then — unless it was handled — the key
    /// down.</summary>
    private static void Press(Key key)
    {
        var target = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
        PresentationSource source = PresentationSource.FromVisual(target)
            ?? throw new Xunit.Sdk.XunitException("the keys are on an element out of the tree");
        var preview = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(preview);
        if (!preview.Handled)
        {
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
        }

        PumpedDispatcher.Drain();
    }

    /// <summary>The note's Properties header, expanded, with its rows
    /// published.</summary>
    private static NotePropertiesViewModel OpenProperties(ShownShell host, int rows = 4)
    {
        host.Workspace.OpenPath("a.md");
        host.Settle();
        NotePropertiesViewModel properties = host.Workspace.ActiveGroup.ActiveTab?.Properties
            ?? throw new Xunit.Sdk.XunitException("premise: the markdown tab has no Properties header");
        properties.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Count == rows, TimeSpan.FromSeconds(30)),
            $"premise: the header published {properties.Rows.Count} rows");
        host.Settle();
        return properties;
    }

    private static PropertyRowViewModel Row(NotePropertiesViewModel properties, string key) =>
        properties.Rows.Single(row => row.KeyIdentity == key);

    /// <summary>A shown control of a property's row, by its type.</summary>
    private static T RowControl<T>(ShownShell host, PropertyRowViewModel row)
        where T : FrameworkElement =>
        ShownShell.Descendants(host.Shell).OfType<T>().Single(control => control.IsVisible && ReferenceEquals(control.DataContext, row));

    /// <summary>A property's shown Delete.</summary>
    private static ButtonBase DeleteOf(ShownShell host, PropertyRowViewModel row) =>
        ShownShell.Descendants(host.Shell).OfType<ButtonBase>().Single(button =>
            button.IsVisible && ReferenceEquals(button.DataContext, row) && ReferenceEquals(button.Command, row.DeleteCommand));

    /// <summary>A shown control of a list item, by its type and — for a
    /// button — its command.</summary>
    private static T ItemControl<T>(ShownShell host, string key, int index, Func<PropertyListItemViewModel, ICommand?> command)
        where T : FrameworkElement =>
        ShownShell.Descendants(host.Shell).OfType<T>().Single(control =>
            control.IsVisible
            && control.DataContext is PropertyListItemViewModel item
            && item.Row.KeyIdentity == key
            && item.Index == index
            && (command(item) is not { } expected || control is ButtonBase { Command: var bound } && ReferenceEquals(bound, expected)));

    /// <summary>Waits for the commit's write and the refresh that
    /// republishes <paramref name="row"/>'s property.</summary>
    private static void AwaitRepublished(NotePropertiesViewModel properties, PropertyRowViewModel row)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => !properties.IsLoading
                    && properties.Rows.FirstOrDefault(fresh => fresh.KeyIdentity == row.KeyIdentity) is { } fresh
                    && !ReferenceEquals(fresh, row),
                TimeSpan.FromSeconds(30)),
            $"the commit of {row.KeyIdentity} never republished the rows");
        PumpedDispatcher.Drain();
    }

    /// <summary>A click, as a key press or the pointer makes it: the button's
    /// own OnClick (a toggle flips, then the command runs).</summary>
    private static void Click(ButtonBase button) =>
        (typeof(ButtonBase).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("ButtonBase.OnClick is gone")).Invoke(button, null);

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
