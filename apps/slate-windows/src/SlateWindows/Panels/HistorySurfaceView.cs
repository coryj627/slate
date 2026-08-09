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
///
/// UIA delivery rules (red team round 1): row and diff-operation
/// hosts are AutomationNamedRowBorder — a plain Border creates NO
/// peer, so a name set on one never reaches UIA — and their visual
/// text is AutomationPresentationTextBlock so each host is ONE
/// accessible stop. Every rebuild preserves keyboard focus by
/// automation identity, because Children.Clear() under the focused
/// element otherwise drops focus out of the panel.
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
    private bool _pendingFocusHead;
    private bool _stagingFocusPending;
    private string? _renderedRefusal;

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
        };
        segments.Children.Add(_segmentThisNote);
        segments.Children.Add(_segmentDeleted);
        // The group name rides a LANDMARK border, never the panel —
        // a StackPanel gets no peer and the name is dropped (the
        // recorded W4-5 bibliography fix, re-learned in red team
        // round 1).
        var segmentsLandmark = new AutomationLandmarkBorder
        {
            Child = segments,
            Margin = new Thickness(0, 0, 0, 8),
        };
        AutomationProperties.SetName(segmentsLandmark, "History scope");

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
        DockPanel.SetDock(segmentsLandmark, Dock.Top);
        layout.Children.Add(segmentsLandmark);
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
            oldModel.FocusHeadRequested -= view.OnFocusHeadRequested;
        }
        if (e.NewValue is HistoryViewModel model)
        {
            model.Published += view.OnPublished;
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.FocusHeadRequested += view.OnFocusHeadRequested;
            view.RenderAll();
        }
    }

    private void OnPublished(object? sender, EventArgs e) => RenderAll();

    private void OnModelPropertyChanged(
        object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HistoryViewModel.Path))
        {
            _pendingFocusHead = false;
            _renderedRefusal = null;
        }
        if (e.PropertyName is nameof(HistoryViewModel.DestinationStaging)
            && Model?.DestinationStaging is null)
        {
            _stagingFocusPending = false;
        }
        if (e.PropertyName is nameof(HistoryViewModel.InlineDiff)
            or nameof(HistoryViewModel.SinceOpen)
            or nameof(HistoryViewModel.IsLoading)
            or nameof(HistoryViewModel.LoadError)
            or nameof(HistoryViewModel.IsDeletedLoading)
            or nameof(HistoryViewModel.DeletedError)
            or nameof(HistoryViewModel.DestinationStaging)
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
        (string Id, string Name)? focusKey = CaptureFocusKey();
        RenderThisNote();
        RenderDeleted();
        AfterRender(focusKey);
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
        bool stagingAnchored = false;
        foreach (HistoryDayGroup group in model.DayGroups)
        {
            _thisNotePanel.Children.Add(
                BuildDayGroup(model, group, ref stagingAnchored));
        }
        if (!stagingAnchored
            && model.DestinationStaging is { ForDeletedFile: false } orphan)
        {
            // The staged version's row is not in the visible window
            // (positions shifted past the page, or the row is marker-
            // filtered): the row stays open, un-anchored, at the end —
            // identity is staged, so the commit is still exact.
            _thisNotePanel.Children.Add(BuildVersionDestinationRow(model, orphan));
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

    private Expander BuildDayGroup(
        HistoryViewModel model, HistoryDayGroup group, ref bool stagingAnchored)
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
            if (model.DestinationStaging is { ForDeletedFile: false } staging
                && string.Equals(
                    staging.VersionHash, row.ContentHashAfter,
                    StringComparison.Ordinal))
            {
                stagingAnchored = true;
                rows.Children.Add(BuildVersionDestinationRow(model, staging));
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
        group.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(HistoryDayGroup.AccessibleName))
            {
                // The name carries the expanded/collapsed state (H4);
                // WPF never re-reads it on its own, so a toggle must
                // re-set it or the name contradicts the pattern state
                // (red team round 1).
                AutomationProperties.SetName(expander, group.AccessibleName);
            }
        };
        return expander;
    }

    private FrameworkElement BuildVersionRow(
        HistoryViewModel model, HistoryVersionRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(8, 4, 0, 4) };
        var date = new AutomationPresentationTextBlock
        {
            Text = row.AbsoluteDate,
            FontWeight = FontWeights.SemiBold,
        };
        panel.Children.Add(date);
        panel.Children.Add(PresentationCaption(row.RelativeDate));
        panel.Children.Add(PresentationCaption(row.AudioFragment));
        if (row.Annotations.Count > 0)
        {
            var chips = new AutomationPresentationTextBlock
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
        AutomationProperties.SetAutomationId(
            select, $"HistoryRowSelect{row.PositionFromTail}");
        select.Checked += (_, _) => ToggleCompare(row);
        select.Unchecked += (_, _) => ToggleCompare(row);
        var compare = RowButton(
            "Compare", $"Compare, {row.AbsoluteDate}",
            $"HistoryRowCompare{row.PositionFromTail}");
        compare.Click += (_, _) => Model?.CompareAgainstCurrent(row);
        var restore = RowButton(
            "Restore…", $"Restore, {row.AbsoluteDate}",
            $"HistoryRowRestore{row.PositionFromTail}");
        restore.Click += (_, _) => Model?.RestoreFromSurface?.Invoke(row);
        var restoreAs = RowButton(
            "Restore As…", $"Restore as, {row.AbsoluteDate}",
            $"HistoryRowRestoreAs{row.PositionFromTail}");
        AutomationProperties.SetHelpText(
            restoreAs, "Save this version as a new file.");
        restoreAs.Click += (_, _) =>
        {
            if (Model is { Path: { } notePath } open)
            {
                _stagingFocusPending = true;
                open.OpenRestoreAsStaging(
                    row, WorkspaceViewModel.SuggestedRestoreCopyPath(notePath));
            }
        };
        actions.Children.Add(select);
        actions.Children.Add(compare);
        actions.Children.Add(restore);
        actions.Children.Add(restoreAs);
        panel.Children.Add(actions);
        var border = new AutomationNamedRowBorder
        {
            Child = panel,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Focusable = true,
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

    private static Button RowButton(
        string content, string accessibleName, string automationId)
    {
        var button = new Button
        {
            Content = content,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(8, 1, 8, 1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(button, accessibleName);
        AutomationProperties.SetAutomationId(button, automationId);
        return button;
    }

    private FrameworkElement BuildVersionDestinationRow(
        HistoryViewModel model, HistoryDestinationStaging staging) =>
        BuildDestinationRow(
            staging,
            automationId: "HistoryRestoreAsRow",
            commit: (destination, done) =>
                model.RestoreAsFromSurface?.Invoke(
                    staging.VersionHash, staging.FormattedDate, destination, done),
            closeAndRefocus: () =>
            {
                model.CloseDestinationStaging();
                FocusVersionRowByHash(staging.VersionHash);
            });

    /// <summary>The inline destination row (divergence HD-2): notice,
    /// seeded suggestion, Enter commits, Escape cancels back to the
    /// anchor row. All state rides the VM's staging record — a
    /// republish re-renders draft and refusal instead of orphaning
    /// the elements a completion closed over (red team round 1).</summary>
    private FrameworkElement BuildDestinationRow(
        HistoryDestinationStaging staging,
        string automationId,
        Action<string, Action<bool, string?>> commit,
        Action closeAndRefocus)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 2, 0, 4) };
        // The row's explanation (H10 pins the deleted-collision
        // sentence; the version flow carries the mac prompt copy).
        var notice = new TextBlock
        {
            Text = staging.Notice,
            TextWrapping = TextWrapping.Wrap,
            Focusable = true,
        };
        AutomationProperties.SetAutomationId(notice, automationId + "Notice");
        panel.Children.Add(notice);
        var entry = new DockPanel();
        var label = new AutomationPresentationTextBlock
        {
            Text = "Destination path:",
            VerticalAlignment = VerticalAlignment.Center,
        };
        var box = new TextBox
        {
            Text = staging.Draft,
            Margin = new Thickness(6, 0, 0, 0),
            MinWidth = 180,
        };
        AutomationProperties.SetAutomationId(box, automationId);
        AutomationProperties.SetName(box, "Destination path");
        box.TextChanged += (_, _) => Model?.UpdateDestinationDraft(box.Text);
        var refusal = new TextBlock
        {
            Text = staging.Refusal ?? string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Visibility = staging.Refusal is null
                ? Visibility.Collapsed
                : Visibility.Visible,
            Focusable = true,
        };
        refusal.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.WarningBrush");
        AutomationProperties.SetAutomationId(refusal, automationId + "Refusal");
        if (staging.Refusal is { } message)
        {
            AutomationProperties.SetName(refusal, message);
        }
        box.PreviewKeyDown += (_, keyArgs) =>
        {
            if (keyArgs.Key == Key.Escape)
            {
                keyArgs.Handled = true;
                closeAndRefocus();
                return;
            }
            if (keyArgs.Key != Key.Enter)
            {
                return;
            }
            keyArgs.Handled = true;
            commit(box.Text, (ok, refusalMessage) =>
            {
                if (ok)
                {
                    // Success navigates (H9: Restore As opens the new
                    // file) — close without fighting that focus.
                    Model?.CloseDestinationStaging();
                    return;
                }
                if (refusalMessage is not null)
                {
                    // Staged, re-rendered, and focused by
                    // TryFocusFreshRefusal — never written onto
                    // detached elements.
                    Model?.SetDestinationRefusal(refusalMessage);
                }
            });
        };
        entry.Children.Add(label);
        entry.Children.Add(box);
        panel.Children.Add(entry);
        panel.Children.Add(refusal);
        box.Loaded += (_, _) =>
        {
            // Focus the box only when the row first OPENS — a
            // republish while it sits open must not steal focus.
            if (_stagingFocusPending)
            {
                _stagingFocusPending = false;
                _ = box.Focus();
                box.SelectAll();
            }
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
        int index = 0;
        foreach (DiffOperation operation in diff.Operations)
        {
            string accessible = operation.Detail is { Length: > 0 } detail
                ? $"{operation.SemanticDescription}. {detail}"
                : operation.SemanticDescription;
            var row = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
            var description = new AutomationPresentationTextBlock
            {
                Text = operation.SemanticDescription,
                TextWrapping = TextWrapping.Wrap,
            };
            row.Children.Add(description);
            if (operation.Detail is { Length: > 0 } detailText)
            {
                AutomationPresentationTextBlock detailBlock =
                    PresentationCaption(detailText);
                detailBlock.MaxHeight = 40;
                row.Children.Add(detailBlock);
            }
            // ONE accessible element per operation (H5): the peered
            // named host is the single stop; the child text is
            // presentation-only, so nothing dumps fragments.
            var host = new AutomationNamedRowBorder
            {
                Child = row,
                Focusable = true,
            };
            AutomationProperties.SetName(host, accessible);
            AutomationProperties.SetAutomationId(host, $"HistoryDiffOp{index}");
            index++;
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
            bool stagingAnchored = false;
            foreach (HistoryDeletedRow row in model.DeletedRows)
            {
                _deletedPanel.Children.Add(BuildDeletedRow(model, row));
                if (model.DestinationStaging is { ForDeletedFile: true } staging
                    && string.Equals(
                        staging.NotePath, row.Path, StringComparison.Ordinal))
                {
                    stagingAnchored = true;
                    _deletedPanel.Children.Add(
                        BuildDeletedDestinationRow(model, staging));
                }
            }
            if (!stagingAnchored
                && model.DestinationStaging is { ForDeletedFile: true } orphan)
            {
                _deletedPanel.Children.Add(
                    BuildDeletedDestinationRow(model, orphan));
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

    private FrameworkElement BuildDeletedDestinationRow(
        HistoryViewModel model, HistoryDestinationStaging staging) =>
        BuildDestinationRow(
            staging,
            automationId: "HistoryRecoverAsRow",
            commit: (destination, done) =>
                model.RecoverAsFromSurface?.Invoke(
                    staging.NotePath, destination, done),
            closeAndRefocus: () =>
            {
                model.CloseDestinationStaging();
                FocusDeletedRowByPath(staging.NotePath);
            });

    private FrameworkElement BuildDeletedRow(
        HistoryViewModel model, HistoryDeletedRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new AutomationPresentationTextBlock
        {
            Text = row.Path,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(PresentationCaption(row.DeletedText));
        if (row.SizeText is { } size)
        {
            panel.Children.Add(PresentationCaption(size));
        }
        if (row.Recoverable)
        {
            var restore = new Button
            {
                // The DIRECT primary flow — no follow-up prompt, so no
                // ellipsis (the mac label).
                Content = "Restore",
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
                        // Restore As… row with the pinned sentence
                        // (H10) — staged by source path.
                        _stagingFocusPending = true;
                        Model?.OpenRecoverAsStaging(
                            row,
                            WorkspaceViewModel.SuggestedRestoreCopyPath(row.Path));
                    }
                });
            panel.Children.Add(restore);
        }
        var border = new AutomationNamedRowBorder
        {
            Child = panel,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Focusable = true,
        };
        border.SetResourceReference(
            Border.BorderBrushProperty, "Slate.BorderBrush");
        AutomationProperties.SetName(border, row.AccessibleName);
        return border;
    }

    // --- Focus management (red team round 1) ---

    private void OnFocusHeadRequested()
    {
        // The reload a restore triggers is ASYNCHRONOUS in production:
        // consuming the request immediately would focus the STALE head
        // row and the publish's rebuild would then destroy it (WCAG
        // 2.4.3). Park the request; the render that follows the
        // publish consumes it against the fresh rows.
        _pendingFocusHead = true;
        _ = Dispatcher.InvokeAsync(
            () => _ = TryConsumePendingFocusHead(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private bool TryConsumePendingFocusHead()
    {
        if (!_pendingFocusHead || Model is not { } model)
        {
            return false;
        }
        if (model.Path is null)
        {
            _pendingFocusHead = false;
            return false;
        }
        if (model.IsLoading)
        {
            // Keep the request parked; the publish render consumes it.
            return false;
        }
        _pendingFocusHead = false;
        Expander? first = _thisNotePanel.Children.OfType<Expander>().FirstOrDefault();
        if (first is null)
        {
            return false;
        }
        // The head group may be collapsed (collapse persists across
        // reloads by stable id) — the NEW head row lives inside it, so
        // expand before focusing (H7).
        if (!first.IsExpanded)
        {
            first.IsExpanded = true;
        }
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (first.Content is StackPanel rows
                    && rows.Children.OfType<AutomationNamedRowBorder>()
                        .FirstOrDefault() is { } head)
                {
                    _ = head.Focus();
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
        return true;
    }

    private void AfterRender((string Id, string Name)? focusKey)
    {
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (TryConsumePendingFocusHead())
                {
                    return;
                }
                if (TryFocusFreshRefusal())
                {
                    return;
                }
                RestoreFocusKey(focusKey);
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>A freshly staged refusal must be PERCEIVABLE, not just
    /// painted: focus the named refusal block once per message (there
    /// is no live region on this surface by design).</summary>
    private bool TryFocusFreshRefusal()
    {
        string? refusal = Model?.DestinationStaging?.Refusal;
        if (refusal is null)
        {
            _renderedRefusal = null;
            return false;
        }
        if (string.Equals(refusal, _renderedRefusal, StringComparison.Ordinal))
        {
            return false;
        }
        _renderedRefusal = refusal;
        FrameworkElement? element = FindDescendant(
            this,
            candidate => AutomationProperties.GetAutomationId(candidate)
                is "HistoryRestoreAsRowRefusal" or "HistoryRecoverAsRowRefusal");
        if (element is null)
        {
            return false;
        }
        _ = element.Focus();
        return true;
    }

    /// <summary>Where was keyboard focus before the rebuild? Keyed by
    /// automation id (nearest self-or-ancestor) plus accessible name,
    /// so the equivalent fresh element can take it back.</summary>
    private (string Id, string Name)? CaptureFocusKey()
    {
        if (Keyboard.FocusedElement is not FrameworkElement focused
            || !IsAncestorOf(focused))
        {
            return null;
        }
        string name = AutomationProperties.GetName(focused) ?? string.Empty;
        DependencyObject? cursor = focused;
        while (cursor is not null && !ReferenceEquals(cursor, this))
        {
            if (cursor is FrameworkElement element
                && AutomationProperties.GetAutomationId(element)
                    is { Length: > 0 } id)
            {
                return (id, name);
            }
            cursor = System.Windows.Media.VisualTreeHelper.GetParent(cursor);
        }
        return (string.Empty, name);
    }

    private void RestoreFocusKey((string Id, string Name)? focusKey)
    {
        if (focusKey is not { } key)
        {
            return;
        }
        FrameworkElement? target = null;
        if (key.Id.Length > 0)
        {
            target = FindDescendant(
                this,
                candidate => string.Equals(
                    AutomationProperties.GetAutomationId(candidate),
                    key.Id,
                    StringComparison.Ordinal));
        }
        if (target is null && key.Name.Length > 0)
        {
            target = FindDescendant(
                this,
                candidate => candidate.Focusable
                    && string.Equals(
                        AutomationProperties.GetName(candidate),
                        key.Name,
                        StringComparison.Ordinal));
        }
        if (target is not null)
        {
            FocusElementOrFirstChild(target);
            return;
        }
        // The focused element's identity vanished with the rebuild
        // (e.g. a recovered deleted row): recover to the segment
        // radio rather than letting focus fall out of the panel.
        _ = (_deletedSegmentActive ? _segmentDeleted : _segmentThisNote).Focus();
    }

    private static void FocusElementOrFirstChild(FrameworkElement element)
    {
        if (element.Focusable)
        {
            _ = element.Focus();
            return;
        }
        _ = element.MoveFocus(
            new TraversalRequest(FocusNavigationDirection.First));
    }

    private void FocusVersionRowByHash(string versionHash)
    {
        uint? position = Model?.DayGroups
            .SelectMany(group => group.Rows)
            .FirstOrDefault(row => string.Equals(
                row.ContentHashAfter, versionHash, StringComparison.Ordinal))
            ?.PositionFromTail;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                FrameworkElement? target = position is { } anchor
                    ? FindDescendant(
                        this,
                        candidate => string.Equals(
                            AutomationProperties.GetAutomationId(candidate),
                            $"HistoryRow{anchor}",
                            StringComparison.Ordinal))
                    : null;
                if (target is not null)
                {
                    _ = target.Focus();
                }
                else
                {
                    _ = _segmentThisNote.Focus();
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FocusDeletedRowByPath(string path)
    {
        string? accessibleName = Model?.DeletedRows
            .FirstOrDefault(row => string.Equals(
                row.Path, path, StringComparison.Ordinal))
            ?.AccessibleName;
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                FrameworkElement? target = accessibleName is { } name
                    ? FindDescendant(
                        this,
                        candidate => candidate.Focusable
                            && string.Equals(
                                AutomationProperties.GetName(candidate),
                                name,
                                StringComparison.Ordinal))
                    : null;
                if (target is not null)
                {
                    _ = target.Focus();
                }
                else
                {
                    _ = _segmentDeleted.Focus();
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private static FrameworkElement? FindDescendant(
        DependencyObject root, Func<FrameworkElement, bool> match)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency)
            {
                continue;
            }
            if (dependency is FrameworkElement element && match(element))
            {
                return element;
            }
            if (FindDescendant(dependency, match) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static AutomationPresentationTextBlock PresentationCaption(string text)
    {
        var block = new AutomationPresentationTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
        };
        block.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        return block;
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
