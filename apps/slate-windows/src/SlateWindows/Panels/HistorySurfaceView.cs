// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-7 (#739): the history leaf body — the mac HistoryPanel twin.
/// Two segments ("This note" / "Deleted", contract H2), the
/// day-grouped version list with the inline markers toggle (H3/H4),
/// the diff surface as a sequential walkthrough (H5 — never a
/// side-by-side dump), the compare model (H6), restore / Restore As…
/// via the coordinator seams (H7/H9), deleted recovery (H10), and
/// the since-open section (H8). Rebuilt wholesale on every VM
/// publish; every sentence is core's or a recorded label (HINV-1).
/// </summary>
internal sealed class HistorySurfaceView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(HistoryViewModel),
            typeof(HistorySurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly RadioButton _segmentThisNote;
    private readonly RadioButton _segmentDeleted;
    private readonly ScrollViewer _thisNoteScroll;
    private readonly StackPanel _thisNotePanel;
    private readonly ScrollViewer _deletedScroll;
    private readonly StackPanel _deletedPanel;
    private bool _deletedSegmentActive;
    private uint? _openRestoreAsPosition;
    private string? _openRecoverAsPath;

    public HistorySurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "HistorySurface");

        // The bibliography two-segment pattern (contract H2): a
        // radio group named "History scope"; switching never
        // announces and never eagerly re-queries.
        _segmentThisNote = new RadioButton
        {
            Content = "This note",
            GroupName = "HistoryScope",
            IsChecked = true,
            Margin = new Thickness(0, 0, 12, 0),
        };
        AutomationProperties.SetAutomationId(
            _segmentThisNote, "HistorySegmentThisNote");
        _segmentThisNote.Checked += (_, _) => SwitchSegment(deleted: false);
        _segmentDeleted = new RadioButton
        {
            Content = "Deleted",
            GroupName = "HistoryScope",
        };
        AutomationProperties.SetAutomationId(
            _segmentDeleted, "HistorySegmentDeleted");
        _segmentDeleted.Checked += (_, _) => SwitchSegment(deleted: true);
        var segments = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8),
        };
        AutomationProperties.SetName(segments, "History scope");
        segments.Children.Add(_segmentThisNote);
        segments.Children.Add(_segmentDeleted);

        _thisNotePanel = new StackPanel();
        _thisNoteScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _thisNotePanel,
        };
        _deletedPanel = new StackPanel();
        _deletedScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _deletedPanel,
            Visibility = Visibility.Collapsed,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(segments, Dock.Top);
        layout.Children.Add(segments);
        var contentHost = new Grid();
        contentHost.Children.Add(_thisNoteScroll);
        contentHost.Children.Add(_deletedScroll);
        layout.Children.Add(contentHost);
        Content = layout;
    }

    public HistoryViewModel? Model
    {
        get => (HistoryViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (HistorySurfaceView)d;
        if (e.OldValue is HistoryViewModel oldModel)
        {
            oldModel.Published -= view.OnPublished;
            oldModel.PropertyChanged -= view.OnModelPropertyChanged;
            oldModel.FocusHeadRequested -= view.FocusHeadRow;
        }
        if (e.NewValue is HistoryViewModel model)
        {
            model.Published += view.OnPublished;
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.FocusHeadRequested += view.FocusHeadRow;
            view.RenderAll();
        }
    }

    private void OnPublished(object? sender, EventArgs e) => RenderAll();

    private void OnModelPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HistoryViewModel.InlineDiff)
            or nameof(HistoryViewModel.SinceOpen)
            or nameof(HistoryViewModel.IsLoading)
            or nameof(HistoryViewModel.LoadError)
            or nameof(HistoryViewModel.IsDeletedLoading)
            or nameof(HistoryViewModel.DeletedError)
            or nameof(HistoryViewModel.Path))
        {
            RenderAll();
        }
    }

    private void SwitchSegment(bool deleted)
    {
        if (_deletedSegmentActive == deleted)
        {
            return;
        }
        _deletedSegmentActive = deleted;
        _thisNoteScroll.Visibility =
            deleted ? Visibility.Collapsed : Visibility.Visible;
        _deletedScroll.Visibility =
            deleted ? Visibility.Visible : Visibility.Collapsed;
        if (deleted)
        {
            // Lazy on first visit, reloaded on later visits (H2/H10).
            Model?.LoadDeletedFiles();
        }
    }

    // --- Rendering ---

    private void RenderAll()
    {
        RenderThisNote();
        RenderDeleted();
    }

    private void RenderThisNote()
    {
        _thisNotePanel.Children.Clear();
        if (Model is not { } model)
        {
            return;
        }
        if (model.Path is null)
        {
            _thisNotePanel.Children.Add(EmptyText(
                "Select a note to see its history.", "HistoryNoNote"));
            return;
        }
        RenderSinceOpen(model);
        RenderVersionHeader(model);
        if (model.InlineDiff is { AnchorPosition: null } headerDiff)
        {
            _thisNotePanel.Children.Add(BuildInlineDiff(headerDiff));
        }
        if (model.LoadError is { } error)
        {
            _thisNotePanel.Children.Add(CaptionText(error, "HistoryLoadError"));
            return;
        }
        if (model.ShowLoading)
        {
            _thisNotePanel.Children.Add(EmptyText(
                "Loading history…", "HistoryLoading", accessibleName: "Loading history."));
            return;
        }
        if (model.ShowEmptyState)
        {
            _thisNotePanel.Children.Add(EmptyText(
                "No versions yet. Versions are recorded as you save.",
                "HistoryNoVersions"));
            return;
        }
        foreach (HistoryDayGroup group in model.DayGroups)
        {
            _thisNotePanel.Children.Add(BuildDayGroup(model, group));
        }
        if (model.CanLoadOlder)
        {
            var older = new Button
            {
                Content = "Show older versions",
                Margin = new Thickness(0, 8, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 2, 10, 2),
            };
            AutomationProperties.SetAutomationId(older, "HistoryLoadOlder");
            older.Click += (_, _) => Model?.LoadOlder();
            _thisNotePanel.Children.Add(older);
        }
    }

    private void RenderSinceOpen(HistoryViewModel model)
    {
        if (model.SinceOpen.Kind == HistorySinceOpenKind.None)
        {
            return;
        }
        var header = new TextBlock
        {
            Text = "Since you last opened",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };
        AutomationProperties.SetHeadingLevel(header, AutomationHeadingLevel.Level3);
        AutomationProperties.SetAutomationId(header, "HistorySinceOpenHeader");
        _thisNotePanel.Children.Add(header);
        if (model.SinceOpen.Kind == HistorySinceOpenKind.BaselineCompacted)
        {
            _thisNotePanel.Children.Add(CaptionText(
                "Earlier changes have been compacted.", "HistorySinceOpenCompacted"));
        }
        else if (model.SinceOpen.Diff is { } diff)
        {
            _thisNotePanel.Children.Add(BuildDiffList(diff));
        }
        _thisNotePanel.Children.Add(new Separator
        {
            Margin = new Thickness(0, 8, 0, 8),
        });
    }

    private void RenderVersionHeader(HistoryViewModel model)
    {
        var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var title = new TextBlock
        {
            Text = model.HeaderText,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetHeadingLevel(title, AutomationHeadingLevel.Level3);
        AutomationProperties.SetAutomationId(title, "HistoryVersionHeader");
        var markers = new CheckBox
        {
            Content = "Show markers",
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = model.ShowMarkers,
        };
        AutomationProperties.SetAutomationId(markers, "HistoryShowMarkers");
        markers.Checked += (_, _) => SetShowMarkers(true);
        markers.Unchecked += (_, _) => SetShowMarkers(false);
        var compare = new Button
        {
            Content = "Compare selected versions",
            Margin = new Thickness(12, 0, 0, 0),
            Padding = new Thickness(10, 2, 10, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = model.CanCompareSelected
                ? Visibility.Visible
                : Visibility.Collapsed,
        };
        AutomationProperties.SetAutomationId(compare, "HistoryCompareSelected");
        compare.Click += (_, _) => Model?.CompareSelected();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(title);
        row.Children.Add(markers);
        row.Children.Add(compare);
        headerRow.Children.Add(row);
        _thisNotePanel.Children.Add(headerRow);
    }

    private void SetShowMarkers(bool value)
    {
        if (Model is { } model && model.ShowMarkers != value)
        {
            model.ShowMarkers = value;
        }
    }

    private Expander BuildDayGroup(HistoryViewModel model, HistoryDayGroup group)
    {
        var rows = new StackPanel();
        foreach (HistoryVersionRow row in group.Rows)
        {
            rows.Children.Add(BuildVersionRow(model, row));
            if (model.InlineDiff is { } inline
                && inline.AnchorPosition == row.PositionFromTail)
            {
                rows.Children.Add(BuildInlineDiff(inline));
            }
            if (_openRestoreAsPosition == row.PositionFromTail)
            {
                rows.Children.Add(BuildRestoreAsRow(model, row));
            }
        }
        var expander = new Expander
        {
            Header = $"{group.Title} · {group.Rows.Count}",
            IsExpanded = !group.IsCollapsed,
            Content = rows,
            Margin = new Thickness(0, 2, 0, 2),
        };
        AutomationProperties.SetAutomationId(
            expander, $"HistoryDay{group.Id}");
        AutomationProperties.SetName(expander, group.AccessibleName);
        expander.Expanded += (_, _) => group.IsCollapsed = false;
        expander.Collapsed += (_, _) => group.IsCollapsed = true;
        return expander;
    }

    private FrameworkElement BuildVersionRow(
        HistoryViewModel model, HistoryVersionRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(8, 4, 0, 4) };
        var date = new TextBlock
        {
            Text = row.AbsoluteDate,
            FontWeight = FontWeights.SemiBold,
        };
        var relative = CaptionBlock(row.RelativeDate);
        var fragment = CaptionBlock(row.AudioFragment);
        panel.Children.Add(date);
        panel.Children.Add(relative);
        panel.Children.Add(fragment);
        if (row.Annotations.Count > 0)
        {
            var chips = new TextBlock
            {
                Text = string.Join("  ·  ", row.Annotations),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
            };
            chips.SetResourceReference(
                TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
            panel.Children.Add(chips);
        }
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 2, 0, 0),
        };
        var select = new CheckBox
        {
            Content = "Select for comparison",
            IsChecked = row.SelectedForCompare,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(
            select, $"Select for comparison, {row.AbsoluteDate}");
        select.Checked += (_, _) => ToggleCompare(row);
        select.Unchecked += (_, _) => ToggleCompare(row);
        var compare = RowButton("Compare", $"Compare, {row.AbsoluteDate}");
        compare.Click += (_, _) => Model?.CompareAgainstCurrent(row);
        var restore = RowButton("Restore…", $"Restore, {row.AbsoluteDate}");
        restore.Click += (_, _) => Model?.RestoreFromSurface?.Invoke(row);
        var restoreAs = RowButton("Restore As…", $"Restore as, {row.AbsoluteDate}");
        AutomationProperties.SetHelpText(
            restoreAs, "Save this version as a new file.");
        restoreAs.Click += (_, _) =>
        {
            _openRestoreAsPosition =
                _openRestoreAsPosition == row.PositionFromTail
                    ? null
                    : row.PositionFromTail;
            _openRecoverAsPath = null;
            RenderAll();
        };
        actions.Children.Add(select);
        actions.Children.Add(compare);
        actions.Children.Add(restore);
        actions.Children.Add(restoreAs);
        panel.Children.Add(actions);
        var border = new Border
        {
            Child = panel,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        border.SetResourceReference(
            Border.BorderBrushProperty, "Slate.BorderBrush");
        AutomationProperties.SetAutomationId(
            border, $"HistoryRow{row.PositionFromTail}");
        AutomationProperties.SetName(border, row.AccessibleName);
        return border;
    }

    private void ToggleCompare(HistoryVersionRow row)
    {
        Model?.ToggleCompareSelection(row.PositionFromTail);
        // The max-two model may have dropped ANOTHER row's selection
        // (H6) — rebuild so every checkbox reflects the VM.
        RenderAll();
    }

    private static Button RowButton(string content, string accessibleName)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 1, 8, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(button, accessibleName);
        return button;
    }

    /// <summary>The inline Restore As… destination row (divergence
    /// HD-2): seeded suggestion, Enter commits, Escape cancels.</summary>
    private FrameworkElement BuildRestoreAsRow(
        HistoryViewModel model, HistoryVersionRow row)
    {
        string path = model.Path ?? string.Empty;
        return BuildDestinationRow(
            seeded: WorkspaceViewModel.SuggestedRestoreCopyPath(path),
            automationId: "HistoryRestoreAsRow",
            commit: (destination, done) =>
                model.RestoreAsFromSurface?.Invoke(row, destination, done),
            close: () =>
            {
                _openRestoreAsPosition = null;
                RenderAll();
            });
    }

    private FrameworkElement BuildDestinationRow(
        string seeded,
        string automationId,
        Action<string, Action<bool, string?>> commit,
        Action close)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 2, 0, 4) };
        var entry = new DockPanel();
        var label = new TextBlock
        {
            Text = "Destination path:",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var box = new TextBox
        {
            Text = seeded,
            Margin = new Thickness(6, 0, 0, 0),
            MinWidth = 180,
        };
        AutomationProperties.SetAutomationId(box, automationId);
        AutomationProperties.SetName(box, "Destination path");
        var refusal = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
            Focusable = true,
        };
        refusal.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.WarningBrush");
        AutomationProperties.SetAutomationId(refusal, automationId + "Refusal");
        box.PreviewKeyDown += (_, keyArgs) =>
        {
            if (keyArgs.Key == Key.Escape)
            {
                keyArgs.Handled = true;
                close();
                return;
            }
            if (keyArgs.Key != Key.Enter)
            {
                return;
            }
            keyArgs.Handled = true;
            commit(box.Text, (ok, message) =>
            {
                if (ok)
                {
                    close();
                    return;
                }
                if (message is not null)
                {
                    refusal.Text = message;
                    refusal.Visibility = Visibility.Visible;
                    AutomationProperties.SetName(refusal, message);
                    _ = box.Focus();
                }
            });
        };
        entry.Children.Add(label);
        entry.Children.Add(box);
        panel.Children.Add(entry);
        panel.Children.Add(refusal);
        box.Loaded += (_, _) =>
        {
            _ = box.Focus();
            box.SelectAll();
        };
        return panel;
    }

    /// <summary>The mac DiffOperationList twin (contract H5): the
    /// core AudioSummary header, then ONE single accessible element
    /// per operation — never a side-by-side dump.</summary>
    private static FrameworkElement BuildDiffList(StructuredDiff diff)
    {
        var panel = new StackPanel { Margin = new Thickness(8, 2, 0, 4) };
        AutomationProperties.SetAutomationId(panel, "HistoryDiffList");
        var summary = new TextBlock
        {
            Text = diff.AudioSummary,
            TextWrapping = TextWrapping.Wrap,
            Focusable = true,
            FontWeight = FontWeights.SemiBold,
        };
        AutomationProperties.SetAutomationId(summary, "HistoryDiffSummary");
        panel.Children.Add(summary);
        if (diff.Operations.Length == 0)
        {
            var none = new TextBlock { Text = "No differences.", Focusable = true };
            panel.Children.Add(none);
            return panel;
        }
        foreach (DiffOperation operation in diff.Operations)
        {
            string accessible = operation.Detail is { Length: > 0 } detail
                ? $"{operation.SemanticDescription}. {detail}"
                : operation.SemanticDescription;
            var row = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            var description = new TextBlock
            {
                Text = operation.SemanticDescription,
                TextWrapping = TextWrapping.Wrap,
            };
            row.Children.Add(description);
            if (operation.Detail is { Length: > 0 } detailText)
            {
                var detailBlock = CaptionBlock(detailText);
                detailBlock.MaxHeight = 40;
                row.Children.Add(detailBlock);
            }
            // ONE accessible element per operation (the child text is
            // non-focusable content; the named focusable host is the
            // single stop — the mac children-ignore shape).
            var host = new Border { Child = row, Focusable = true };
            AutomationProperties.SetName(host, accessible);
            panel.Children.Add(host);
        }
        return panel;
    }

    private FrameworkElement BuildInlineDiff(HistoryInlineDiff inline)
    {
        if (inline.Error is { } error)
        {
            return CaptionText(error, "HistoryDiffError");
        }
        if (inline.Diff is { } diff)
        {
            return BuildDiffList(diff);
        }
        return CaptionText("No differences.", "HistoryDiffEmpty");
    }

    private void RenderDeleted()
    {
        _deletedPanel.Children.Clear();
        if (Model is not { } model)
        {
            return;
        }
        if (model.DeletedError is { } error)
        {
            _deletedPanel.Children.Add(CaptionText(error, "HistoryDeletedError"));
        }
        else if (model.IsDeletedLoading && model.DeletedRows.Count == 0)
        {
            _deletedPanel.Children.Add(EmptyText(
                "Loading history…", "HistoryDeletedLoading",
                accessibleName: "Loading history."));
        }
        else if (model.ShowDeletedEmptyState)
        {
            _deletedPanel.Children.Add(EmptyText(
                "No recently deleted files.", "HistoryDeletedEmpty"));
        }
        else
        {
            foreach (HistoryDeletedRow row in model.DeletedRows)
            {
                _deletedPanel.Children.Add(BuildDeletedRow(model, row));
                if (string.Equals(
                    _openRecoverAsPath, row.Path, StringComparison.Ordinal))
                {
                    _deletedPanel.Children.Add(BuildDestinationRow(
                        seeded: WorkspaceViewModel.SuggestedRestoreCopyPath(row.Path),
                        automationId: "HistoryRecoverAsRow",
                        commit: (destination, done) =>
                            model.RecoverAsFromSurface?.Invoke(
                                row.Path, destination, done),
                        close: () =>
                        {
                            _openRecoverAsPath = null;
                            RenderAll();
                        }));
                }
            }
        }
        // The standing footer (always, below a divider — mac shape).
        _deletedPanel.Children.Add(new Separator
        {
            Margin = new Thickness(0, 8, 0, 4),
        });
        var footer = CaptionBlock(
            "Files deleted before Slate saved them go to the system Trash.");
        AutomationProperties.SetAutomationId(footer, "HistoryDeletedFooter");
        _deletedPanel.Children.Add(footer);
    }

    private FrameworkElement BuildDeletedRow(
        HistoryViewModel model, HistoryDeletedRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = row.Path,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(CaptionBlock(row.DeletedText));
        if (row.SizeText is { } size)
        {
            panel.Children.Add(CaptionBlock(size));
        }
        if (row.Recoverable)
        {
            var restore = new Button
            {
                Content = "Restore…",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 2, 0, 0),
                Padding = new Thickness(8, 1, 8, 1),
            };
            AutomationProperties.SetName(restore, $"Restore, {row.Path}");
            AutomationProperties.SetHelpText(restore, "Restore this deleted file.");
            restore.Click += (_, _) =>
                model.RecoverFromSurface?.Invoke(row, (ok, collision) =>
                {
                    if (collision)
                    {
                        // DestinationExists routes into the inline
                        // Restore As… row (H10).
                        _openRecoverAsPath = row.Path;
                        RenderAll();
                    }
                });
            panel.Children.Add(restore);
        }
        var border = new Border
        {
            Child = panel,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        border.SetResourceReference(
            Border.BorderBrushProperty, "Slate.BorderBrush");
        AutomationProperties.SetName(border, row.AccessibleName);
        return border;
    }

    private void FocusHeadRow()
    {
        // The first version row after a restore (WCAG 2.4.3).
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                foreach (object child in _thisNotePanel.Children)
                {
                    if (child is Expander { Content: StackPanel rows }
                        && rows.Children.Count > 0
                        && rows.Children[0] is Border first)
                    {
                        first.Focusable = true;
                        _ = first.Focus();
                        return;
                    }
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private static TextBlock CaptionBlock(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
        };
        block.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        return block;
    }

    private static TextBlock CaptionText(string text, string automationId)
    {
        TextBlock block = CaptionBlock(text);
        block.Focusable = true;
        AutomationProperties.SetAutomationId(block, automationId);
        return block;
    }

    private static TextBlock EmptyText(
        string text, string automationId, string? accessibleName = null)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
        };
        block.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(block, automationId);
        if (accessibleName is not null)
        {
            AutomationProperties.SetName(block, accessibleName);
        }
        return block;
    }
}
