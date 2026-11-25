// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// The command palette's results list (W5-1, #741): the grouped view over
/// the view model's rows, the push of its selection into the list, and the
/// pointer route back. The view model is the only selection authority;
/// this keeps the list showing what the view model chose.
/// </summary>
/// <remarks>
/// Its own type rather than more of <see cref="MainWindow"/> so that the
/// list the shell's XAML actually declares can be hosted against a fake
/// command source — the R-11 facts run the shipped list, not a copy of its
/// settings.
/// </remarks>
internal sealed class CommandPaletteResultsPresenter : IDisposable
{
    private readonly ListBox _list;
    private readonly CommandPaletteViewModel _palette;
    private bool _syncingSelection;

    internal CommandPaletteResultsPresenter(ListBox list, CommandPaletteViewModel palette)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(palette);
        _list = list;
        _palette = palette;
        _palette.PropertyChanged += Palette_PropertyChanged;
        _list.SelectionChanged += List_SelectionChanged;
        _list.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(List_GotKeyboardFocus));
        _list.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(List_PreviewMouseDown));
        // #1834: no letter search in the list. WPF's default search matched every row's type
        // name, so "s" took the keys and the selection to the first row from any other row, and
        // a search over the labels moved the keys with a "Selected:" line that NVDA then
        // repeated as the focused row. With the keys on a row a letter now moves nothing.
        _list.IsTextSearchEnabled = false;
        Refresh();
    }

    /// <summary>Detaches from the list and the view model.</summary>
    public void Dispose()
    {
        _palette.PropertyChanged -= Palette_PropertyChanged;
        _list.SelectionChanged -= List_SelectionChanged;
        _list.RemoveHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(List_GotKeyboardFocus));
        _list.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(List_PreviewMouseDown));
    }

    private void Palette_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(CommandPaletteViewModel.Rows):
                Refresh();
                break;

            case nameof(CommandPaletteViewModel.SelectedRow):
                SyncSelection();
                break;
        }
    }

    /// <summary>
    /// Rebuilds the grouped view over the current rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Grouping is applied here rather than through a XAML
    /// <c>CollectionViewSource</c> because it keys on
    /// <c>SectionTitle</c> — the section core actually PLACED the row in.
    /// Grouping on the <c>CommandSection</c> enum would file a Recent row
    /// under the very section core excluded it from. A fresh view per
    /// publish also matches the view model's replace-wholesale rows, and
    /// the groups keep core's order because a view with no sort
    /// description creates groups in encounter order (contract P1).
    /// </para>
    /// <para>
    /// R-11 (#1254): the swap runs inside the selection-sync guard. The
    /// list drops the old row from its selection as the view changes, and
    /// a list synchronized with its view's current item would then select
    /// the new FIRST row — which the pointer route used to hand to the
    /// view model as a user's choice, so it announced "Selected: {first
    /// row}" and then its own survivor again (contract 28 P7, P10). The
    /// XAML also turns the synchronization off; the guard keeps the swap
    /// silent even if a template or style turns it back on.
    /// </para>
    /// </remarks>
    private void Refresh()
    {
        CommandPaletteRecomputeTiming? timing = _palette.LastRecomputeTiming;
        long swapStarted = timing is null ? 0 : Stopwatch.GetTimestamp();
        var grouped = new CollectionViewSource { Source = _palette.Rows };
        // DS-10: choices are one list, with no section to head it.
        if (!_palette.IsChoosing)
        {
            grouped.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(CommandPaletteRowViewModel.SectionTitle)));
        }

        _syncingSelection = true;
        try
        {
            _list.ItemsSource = grouped.View;
        }
        finally
        {
            _syncingSelection = false;
        }

        SyncSelection();
        _list.UpdateLayout();
        if (timing is not null)
        {
            ReportQueryChangeTiming(timing, Stopwatch.GetTimestamp() - swapStarted);
        }
    }

    /// <summary>
    /// Pushes the view model's selection into the list and scrolls it into
    /// view. The view model is the authority: a two-way
    /// <c>SelectedItem</c> binding would push null on every ItemsSource
    /// replacement and destroy the selection the view model just
    /// preserved across a query change (contract P7).
    /// </summary>
    /// <remarks>
    /// A recompute publishes its rows before its selection, so while the
    /// swap runs the view model's row is still the previous query's object,
    /// in no row of the new view. The list would refuse it and the scroll
    /// would find nothing; the <c>SelectedRow</c> change that follows
    /// carries the row to show, so a stale one is skipped here.
    /// <para>
    /// DS-10: what this and <see cref="Refresh"/> change is laid out here, not left to the
    /// render pass. TSF can be granted its lock between dispatcher operations, and its
    /// <c>GetTextExt</c> then runs the layout and automation events it finds pending. Inside
    /// that lock a UIA event can't be raised while a client reads the field: the two wait on
    /// each other until UIA gives up, 20 seconds a time, with the app and NVDA frozen. Measured
    /// under NVDA; the record is in contract 28. A publication changes the rows and then the
    /// selection, so the selection's scroll is the last of it, and a scroll the list has to
    /// defer can't arise: the rows' containers were generated by the flush before it. A
    /// publication that ends with no selection has only that flush.
    /// </para>
    /// </remarks>
    private void SyncSelection()
    {
        CommandPaletteRowViewModel? selected = _palette.SelectedRow;
        if (ReferenceEquals(_list.SelectedItem, selected)
            || (selected is not null && !_palette.Rows.Contains(selected)))
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            _list.SelectedItem = selected;
            if (selected is not null)
            {
                _list.ScrollIntoView(selected);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        _list.UpdateLayout();
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        if (_list.SelectedItem is CommandPaletteRowViewModel row)
        {
            _palette.Select(row);
        }
    }

    /// <summary>
    /// #1834 (contract 28 P7): a row that takes the keys from another element — Tab onto a
    /// row other than the selection, or a UIA SetFocus — is the selection, and the reader's,
    /// so the row the keys are on is the row Enter runs and a query keeps while it lists it.
    /// Keys that land on the
    /// selection itself, as Tab's do, are no move of the reader's
    /// (<see cref="CommandPaletteViewModel.RowTookTheKeys"/>); a click is
    /// <see cref="List_PreviewMouseDown"/>'s.
    /// </summary>
    /// <remarks>
    /// Not when a publication swapped out the row the keys were on: the list then carries the
    /// keys onto its selection, which is already what the publication made it (measured on
    /// <c>main</c> for #1834: the keys land on the selected row of the new view, after the
    /// publication, in every case), and carrying them is not a choice the reader made. Nor when
    /// the keys come back with nothing before them — the window activated again, a dialog
    /// closing back to the list: the window's arrows can have moved the selection off the row
    /// the keys stayed on, and a restore is no move of the reader's either.
    /// </remarks>
    private void List_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_syncingSelection
            || e.OldFocus is null
            || e.NewFocus is not DependencyObject focused
            || ItemsControl.ContainerFromElement(_list, focused) is not ListBoxItem { DataContext: CommandPaletteRowViewModel row }
            || (e.OldFocus is ListBoxItem previous && PresentationSource.FromVisual(previous) is null))
        {
            return;
        }

        _palette.RowTookTheKeys(row);
    }

    /// <summary>
    /// #1834 (contract 28 P7): a pointer pressed on a row makes it the selection, and the
    /// reader's (<see cref="CommandPaletteViewModel.RowPressed"/>). Heard at the press,
    /// before the click moves the keys or the selection: a click on the row that already holds
    /// both changes neither, so neither <see cref="List_GotKeyboardFocus"/> nor
    /// <see cref="List_SelectionChanged"/> would hear it.
    /// </summary>
    private void List_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is MouseButton.Left or MouseButton.Right
            && e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(_list, source) is ListBoxItem { DataContext: CommandPaletteRowViewModel row })
        {
            _palette.RowPressed(row);
        }
    }

    /// <summary>
    /// Finishes one #1254 profile reading (SLATE_UIA_DIAGNOSTICS=1 only —
    /// the caller holds a timing only then). The swap is measured in place;
    /// the rest of the query change settles by the second Loaded-priority
    /// hop: the selection push, the layout and render passes that realize
    /// the rows, the <c>ScrollIntoView</c> they queued at Loaded, and any
    /// higher-priority work the dispatcher runs first — the search box's
    /// own text-layout notification among it. One line per query change,
    /// with the view model's step costs, and nothing typed: counts and
    /// milliseconds only (W1-RT-01).
    /// </summary>
    private void ReportQueryChangeTiming(CommandPaletteRecomputeTiming timing, long swapTicks)
    {
        long swapped = Stopwatch.GetTimestamp();
        Dispatcher dispatcher = _list.Dispatcher;
        _ = dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            _ = dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                long settled = Stopwatch.GetTimestamp();
                HostLog.WriteUiAutomationDiagnostic(
                    HostDiagnosticEvent.PaletteQueryChangeTimed,
                    $"change={timing.QueryChange}, rows={timing.Rows}, "
                    + $"rankMs={Milliseconds(timing.RankTicks)}, "
                    + $"availabilityMs={Milliseconds(timing.AvailabilityTicks)}, "
                    + $"rowBuildMs={Milliseconds(timing.RowBuildTicks)}, "
                    + $"swapMs={Milliseconds(swapTicks)}, "
                    + $"settleMs={Milliseconds(settled - swapped)}, "
                    + $"totalMs={Milliseconds(settled - timing.StartTimestamp)}");
            }));

        static string Milliseconds(long ticks) =>
            (ticks * 1000.0 / Stopwatch.Frequency).ToString("F1", CultureInfo.InvariantCulture);
    }
}
