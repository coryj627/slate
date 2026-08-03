// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the XAML-facing half of the row VM — flat bindable
/// projections over the PropertyDraft union plus the row commands.
/// Every mutation here is DRAFT-LOCAL (feature contract 2): setters
/// rewrite the draft, never the disk; the enumerated immediate-commit
/// controls (boolean switch, date picker) route through the same
/// injected commit delegate the Save button uses.
/// </summary>
internal sealed partial class PropertyRowViewModel
{
    /// <summary>Which editor template the row takes. Fixed at row
    /// construction from the STORED value (mac parity: the control
    /// never swaps mid-edit; rows are rebuilt by every publish).</summary>
    public string EditorMode { get; private set; } = "text";

    public ObservableCollection<PropertyListItemViewModel> Items { get; } = [];

    public ICommand CommitCommand { get; private set; } = null!;
    public ICommand RevertCommand { get; private set; } = null!;
    public ICommand DeleteCommand { get; private set; } = null!;
    public ICommand StepUpCommand { get; private set; } = null!;
    public ICommand StepDownCommand { get; private set; } = null!;
    public ICommand AddItemCommand { get; private set; } = null!;
    public ICommand ToggleBooleanCommand { get; private set; } = null!;

    public bool IsTagList => Kind == "tag_list";

    public string AddItemLabel => PropertyPhrase.AddItemLabel(Key, Kind);

    /// <summary>Visible button text — a PREFIX of AddItemLabel
    /// (WCAG 2.5.3 label-in-name).</summary>
    public string AddItemVisibleText => IsTagList ? "Add tag" : "Add item";

    public string StepUpLabel => $"{PropertyPhrase.StepperLabel(Key)} up";

    public string StepDownLabel => $"{PropertyPhrase.StepperLabel(Key)} down";

    public string PickerLabel => PropertyPhrase.PickerLabel(Key);

    /// <summary>Raw-value tooltip for dates; wikilink shows
    /// [[target]] (§2.8).</summary>
    public string? EditorTooltip => _draft switch
    {
        PropertyDraft.ScalarText { Kind: "date" or "datetime" } scalar => scalar.Value,
        PropertyDraft.WikilinkDraft link => $"[[{link.Target}]]",
        _ => null,
    };

    partial void InitializeEditor()
    {
        EditorMode = _committedBaseline switch
        {
            PropertyDraft.BooleanDraft => "boolean",
            PropertyDraft.IntegerDraft => "integer",
            PropertyDraft.FloatDraft => "float",
            PropertyDraft.WikilinkDraft => "wikilink",
            PropertyDraft.ListDraft or PropertyDraft.TagListDraft => "list",
            PropertyDraft.ScalarText { Kind: "date" }
                when StoredValueTakesDatePicker(Property.Kind, Property.ValueJson) =>
                "datePicker",
            _ => "text",
        };
        CommitCommand = new RelayCommand(_ => CommitDelegate(this), _ => true);
        RevertCommand = new RelayCommand(_ => Revert(), _ => true);
        DeleteCommand = new RelayCommand(_ => RequestDeleteDelegate(this), _ => true);
        StepUpCommand = new RelayCommand(_ => Step(1), _ => true);
        StepDownCommand = new RelayCommand(_ => Step(-1), _ => true);
        AddItemCommand = new RelayCommand(_ => AddItem(), _ => true);
        ToggleBooleanCommand = new RelayCommand(_ => ToggleBoolean(), _ => true);
        RebuildItems();
    }

    partial void OnDraftReplaced()
    {
        RebuildItems();
        OnPropertyChanged(nameof(EditorText));
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(DateValue));
        OnPropertyChanged(nameof(EditorTooltip));
    }

    /// <summary>The single-line editor text for scalar drafts.</summary>
    public string EditorText
    {
        get => _draft switch
        {
            PropertyDraft.ScalarText scalar => scalar.Value,
            PropertyDraft.IntegerDraft integer => integer.Value,
            PropertyDraft.FloatDraft floating => floating.Value,
            PropertyDraft.WikilinkDraft link => link.Target,
            _ => "",
        };
        set
        {
            PropertyDraft? replaced = _draft switch
            {
                PropertyDraft.ScalarText scalar => scalar with { Value = value },
                PropertyDraft.IntegerDraft integer => integer with { Value = value },
                PropertyDraft.FloatDraft floating => floating with { Value = value },
                PropertyDraft.WikilinkDraft link => link with { Target = value },
                _ => null,
            };
            if (replaced is not null && !PropertyDraft.ValueEquals(replaced, _draft))
            {
                Draft = replaced;
            }
        }
    }

    public bool BoolValue
    {
        get => _draft is PropertyDraft.BooleanDraft { Value: true };
        set
        {
            if (_draft is PropertyDraft.BooleanDraft current && current.Value != value)
            {
                Draft = current with { Value = value };
            }
        }
    }

    /// <summary>The DatePicker projection (datePicker mode only —
    /// the stored value parsed at row construction, so this never
    /// invents a date). Setting commits immediately (mac parity).</summary>
    public DateTime? DateValue
    {
        get => _draft is PropertyDraft.ScalarText { Kind: "date" } scalar
            && DateOnly.TryParseExact(
                scalar.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly date)
            ? date.ToDateTime(TimeOnly.MinValue)
            : null;
        set
        {
            if (value is not { } picked
                || _draft is not PropertyDraft.ScalarText { Kind: "date" } scalar)
            {
                return;
            }
            string formatted = DateOnly.FromDateTime(picked)
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (formatted != scalar.Value)
            {
                Draft = scalar with { Value = formatted };
                CommitDelegate(this);
            }
        }
    }

    /// <summary>The switch flip: draft flips, commit routes through
    /// the seam. A refused commit leaves the flipped draft dirty and
    /// intact (contract 3) — the switch honestly shows the
    /// uncommitted state.</summary>
    private void ToggleBoolean()
    {
        if (_draft is PropertyDraft.BooleanDraft current)
        {
            Draft = current with { Value = !current.Value };
            CommitDelegate(this);
        }
    }

    /// <summary>Stepper: ±1 integer (Int64 overflow-guarded, pins at
    /// the rail) / ±1.0 float. Draft-local; commit is Enter/Save.</summary>
    private void Step(int direction)
    {
        switch (_draft)
        {
            case PropertyDraft.IntegerDraft integer
                when long.TryParse(integer.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long parsed):
                long stepped = direction > 0
                    ? (parsed == long.MaxValue ? parsed : parsed + 1)
                    : (parsed == long.MinValue ? parsed : parsed - 1);
                Draft = integer with
                {
                    Value = stepped.ToString(CultureInfo.InvariantCulture),
                };
                break;
            case PropertyDraft.FloatDraft floating
                when double.TryParse(floating.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double parsedFloat)
                    && !double.IsNaN(parsedFloat) && !double.IsInfinity(parsedFloat):
                Draft = floating with
                {
                    Value = (parsedFloat + direction)
                        .ToString("0.0###############", CultureInfo.InvariantCulture),
                };
                break;
        }
    }

    private int DraftItemCount => _draft switch
    {
        PropertyDraft.ListDraft list => list.Items.Count,
        PropertyDraft.TagListDraft tags => tags.Tags.Count,
        _ => 0,
    };

    private void AddItem()
    {
        switch (_draft)
        {
            case PropertyDraft.ListDraft list:
                list.Items.Add(PropertyDraft.ListElementDraft.ForNew(""));
                break;
            case PropertyDraft.TagListDraft tags:
                tags.Tags.Add("");
                break;
            default:
                return;
        }
        RebuildItems();
        OnPropertyChanged(nameof(IsDirty));
    }

    internal void RemoveItem(int index)
    {
        switch (_draft)
        {
            case PropertyDraft.ListDraft list
                when index >= 0 && index < list.Items.Count:
                list.Items.RemoveAt(index);
                break;
            case PropertyDraft.TagListDraft tags
                when index >= 0 && index < tags.Tags.Count:
                tags.Tags.RemoveAt(index);
                break;
            default:
                return;
        }
        RebuildItems();
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Typed edits mark the ELEMENT edited (contract 10):
    /// only user-touched elements convert on encode.</summary>
    internal void SetItemText(int index, string value)
    {
        switch (_draft)
        {
            case PropertyDraft.ListDraft list
                when index >= 0 && index < list.Items.Count
                    && list.Items[index].Text != value:
                list.Items[index] = list.Items[index] with { Text = value, Edited = true };
                break;
            case PropertyDraft.TagListDraft tags
                when index >= 0 && index < tags.Tags.Count && tags.Tags[index] != value:
                tags.Tags[index] = value;
                break;
            default:
                return;
        }
        OnPropertyChanged(nameof(IsDirty));
    }

    internal string ItemText(int index) => _draft switch
    {
        PropertyDraft.ListDraft list when index >= 0 && index < list.Items.Count =>
            list.Items[index].Text,
        PropertyDraft.TagListDraft tags when index >= 0 && index < tags.Tags.Count =>
            tags.Tags[index],
        _ => "",
    };

    private void RebuildItems()
    {
        Items.Clear();
        int count = DraftItemCount;
        for (int index = 0; index < count; index++)
        {
            Items.Add(new PropertyListItemViewModel(this, index, count));
        }
    }
}

/// <summary>One list/tag_list item row: a text box plus its Remove
/// button, indexed into the parent row's draft.</summary>
internal sealed class PropertyListItemViewModel : INotifyPropertyChanged
{
    private readonly PropertyRowViewModel _row;
    private readonly int _index;

    public PropertyListItemViewModel(PropertyRowViewModel row, int index, int count)
    {
        _row = row;
        _index = index;
        ItemLabel = PropertyPhrase.ListItemLabel(row.Key, row.Kind, index + 1, count);
        RemoveLabel = PropertyPhrase.RemoveItemLabel(row.Key, row.Kind, index + 1);
        RemoveCommand = new RelayCommand(_ => _row.RemoveItem(_index), _ => true);
    }

    public string ItemLabel { get; }

    public string RemoveLabel { get; }

    public ICommand RemoveCommand { get; }

    /// <summary>Enter in an item editor commits the ROW (contract 2:
    /// Enter is a commit trigger for lists too — adversarial round
    /// 1); Esc reverts it; the delete chord routes to the row.</summary>
    public ICommand CommitCommand => _row.CommitCommand;

    public ICommand RevertCommand => _row.RevertCommand;

    public ICommand DeleteCommand => _row.DeleteCommand;

    public string Text
    {
        get => _row.ItemText(_index);
        set
        {
            _row.SetItemText(_index, value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
