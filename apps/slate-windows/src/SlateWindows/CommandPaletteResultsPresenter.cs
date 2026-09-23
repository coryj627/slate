// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
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
/// command source.
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
        Refresh();
    }

    /// <summary>Detaches from the list and the view model.</summary>
    public void Dispose()
    {
        _palette.PropertyChanged -= Palette_PropertyChanged;
        _list.SelectionChanged -= List_SelectionChanged;
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
    /// Grouping is applied here rather than through a XAML
    /// <c>CollectionViewSource</c> because it keys on
    /// <c>SectionTitle</c> — the section core actually PLACED the row in.
    /// Grouping on the <c>CommandSection</c> enum would file a Recent row
    /// under the very section core excluded it from. A fresh view per
    /// publish also matches the view model's replace-wholesale rows, and
    /// the groups keep core's order because a view with no sort
    /// description creates groups in encounter order (contract P1).
    /// </remarks>
    private void Refresh()
    {
        CommandPaletteRecomputeTiming? timing = _palette.LastRecomputeTiming;
        long swapStarted = timing is null ? 0 : Stopwatch.GetTimestamp();
        var grouped = new CollectionViewSource { Source = _palette.Rows };
        grouped.GroupDescriptions.Add(
            new PropertyGroupDescription(nameof(CommandPaletteRowViewModel.SectionTitle)));
        _list.ItemsSource = grouped.View;
        SyncSelection();
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
    private void SyncSelection()
    {
        CommandPaletteRowViewModel? selected = _palette.SelectedRow;
        if (ReferenceEquals(_list.SelectedItem, selected))
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
