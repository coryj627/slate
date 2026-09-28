// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SlateWindows.Canvas;
using SlateWindows.Panels;

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

    /// <summary>The note's Properties header, expanded, with its rows
    /// published.</summary>
    private static NotePropertiesViewModel OpenProperties(ShownShell host)
    {
        host.Workspace.OpenPath("a.md");
        host.Settle();
        NotePropertiesViewModel properties = host.Workspace.ActiveGroup.ActiveTab?.Properties
            ?? throw new Xunit.Sdk.XunitException("premise: the markdown tab has no Properties header");
        properties.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Count == 4, TimeSpan.FromSeconds(30)),
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
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "STA test body timed out.");
        CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
