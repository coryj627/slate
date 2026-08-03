// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): one property editor row. Rows are SNAPSHOTS
/// (the W4-3 adversarial-round-2 identity rule): each row pins the
/// note's content hash at the read that produced it, and every
/// commit carries that snapshot hash — never a hash re-read at
/// commit time — so a stale row surfaces the conflict dialog
/// instead of silently overwriting an external change.
///
/// The row never writes: Commit/Revert/Delete raise the injected
/// delegates and the workspace owns the write seam (refusals,
/// leases, CAS, announcements).
/// </summary>
internal sealed partial class PropertyRowViewModel : INotifyPropertyChanged
{
    private PropertyDraft _draft;
    private PropertyDraft _committedBaseline;
    private string? _validationError;
    private bool _writeInFlight;

    public PropertyRowViewModel(
        Property property,
        string ownerPath,
        string contentHash,
        Action<PropertyRowViewModel> commit,
        Action<PropertyRowViewModel> revertAnnounce,
        Action<PropertyRowViewModel> requestDelete)
    {
        Property = property;
        OwnerPath = ownerPath;
        ContentHash = contentHash;
        CommitDelegate = commit;
        RevertAnnounceDelegate = revertAnnounce;
        RequestDeleteDelegate = requestDelete;
        _committedBaseline = PropertyValueCodec.Decode(property.Kind, property.ValueJson);
        _draft = PropertyDraft.Copy(_committedBaseline);
        InitializeEditor();
    }

    partial void InitializeEditor();

    partial void OnDraftReplaced();

    public Property Property { get; }

    /// <summary>The header VM that published this row. The conflict
    /// resolution discards the draft on THIS header (round 7), not on
    /// whichever duplicate tab happens to be enumerated first.</summary>
    internal NotePropertiesViewModel? Owner { get; set; }

    /// <summary>The note PATH of the read that produced this row —
    /// the write seam resolves the target tab by THIS, never by the
    /// active tab (contract 1; adversarial round 1: an old row must
    /// not mutate whichever note happens to be active).</summary>
    public string OwnerPath { get; }

    /// <summary>The note's content hash AT the read that produced
    /// this row — the CAS token for every write from it.</summary>
    public string ContentHash { get; }

    public Action<PropertyRowViewModel> CommitDelegate { get; }

    public Action<PropertyRowViewModel> RevertAnnounceDelegate { get; }

    public Action<PropertyRowViewModel> RequestDeleteDelegate { get; }

    public string Key => Property.Key;

    public string Kind => Property.Kind;

    public PropertyDraft Draft
    {
        get => _draft;
        set
        {
            _draft = value;
            OnPropertyChanged(nameof(Draft));
            OnPropertyChanged(nameof(IsDirty));
            OnDraftReplaced();
        }
    }

    public PropertyDraft CommittedBaseline => _committedBaseline;

    public bool IsDirty => !PropertyDraft.ValueEquals(_draft, _committedBaseline);

    public bool WriteInFlight
    {
        get => _writeInFlight;
        set
        {
            _writeInFlight = value;
            OnPropertyChanged(nameof(WriteInFlight));
        }
    }

    public string? ValidationError
    {
        get => _validationError;
        private set
        {
            _validationError = value;
            OnPropertyChanged(nameof(ValidationError));
            OnPropertyChanged(nameof(ValidationLabel));
        }
    }

    public string? ValidationLabel =>
        _validationError is null ? null : PropertyPhrase.ValidationLabel(_validationError);

    public string AutomationName => PropertyPhrase.EditorLabel(Key, Kind);

    public string SaveLabel => PropertyPhrase.SaveLabel(Key);

    public string RevertLabel => PropertyPhrase.RevertLabel(Key);

    public string RevertHint => PropertyPhrase.RevertHint(Key);

    public string DeleteLabel => PropertyPhrase.DeleteLabel(Key);

    /// <summary>Picker eligibility gates on the STORED value, never
    /// the in-flight draft — the editor control must not swap while
    /// the user types (mac parity, test-pinned).</summary>
    public static bool StoredValueTakesDatePicker(string kind, string valueJson)
    {
        if (kind == "date")
        {
            string raw = TrimJsonString(valueJson);
            return DateOnly.TryParseExact(
                raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _);
        }
        if (kind == "datetime")
        {
            string raw = TrimJsonString(valueJson);
            return DateTimeOffset.TryParse(
                    raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _)
                || DateTime.TryParseExact(
                    raw, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _);
        }
        return false;
    }

    private static string TrimJsonString(string valueJson)
    {
        string trimmed = valueJson.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1]
            : trimmed;
    }

    /// <summary>Shape validation at commit, pre-core. Returns true
    /// when the draft may encode; false sets ValidationError.</summary>
    public bool ValidateForCommit()
    {
        switch (_draft)
        {
            case PropertyDraft.IntegerDraft i
                when !long.TryParse(i.Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out _):
                ValidationError = PropertyPhrase.IntegerShapeError;
                return false;
            case PropertyDraft.FloatDraft f
                when !double.TryParse(f.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double parsed)
                    || double.IsNaN(parsed) || double.IsInfinity(parsed):
                ValidationError = PropertyPhrase.FloatShapeError;
                return false;
            // NOTE: date/datetime shape is NOT calendar-checked here.
            // Core classifies dates STRUCTURALLY (the fixture's
            // `almost_date: 2026-13-45` is a real stored date), so a
            // calendar check would falsely refuse values core stores
            // fine — the round-7 finding. Kind preservation is asked
            // of core below, once, for every draft.
            case PropertyDraft.WikilinkDraft blank when blank.Target.Trim().Length == 0:
                // Trim before the emptiness check (mac parity) — a
                // whitespace-only target is empty.
                ValidationError = PropertyPhrase.WikilinkEmptyError;
                return false;
            case PropertyDraft.WikilinkDraft w when w.Target.Contains("]]"):
                ValidationError = PropertyPhrase.WikilinkBracketError;
                return false;
            case PropertyDraft.WikilinkDraft crlf
                when crlf.Target.Contains('\n') || crlf.Target.Contains('\r'):
                // Core rejects newline targets; refuse pre-core with
                // the host copy (round-4 below-bar).
                ValidationError = PropertyPhrase.WikilinkNewlineError;
                return false;
            default:
                break;
        }

        // THE authority (round 7 class fix): a date/datetime row whose
        // draft would no longer be stored as that kind is a typo, not
        // an intent to retype the property — refuse with the shape
        // message, exactly as mac does, but using CORE's rule instead
        // of a host mirror of it. Other kinds may legitimately change
        // kind when the user changes the value, so they only need to
        // be storable at all.
        PropertyValue candidate;
        try
        {
            candidate = PropertyValueCodec.Encode(_draft);
        }
        catch (Exception exception) when (
            exception is FormatException or OverflowException)
        {
            ValidationError = PropertyPhrase.IntegerShapeError;
            return false;
        }
        string? storedKind = PropertyKindAuthority.WouldStoreAs(Key, candidate);
        if (storedKind is null)
        {
            ValidationError = PropertyPhrase.AddFailedDraftKept;
            return false;
        }
        if (Kind is "date" or "datetime"
            && !string.Equals(storedKind, Kind, StringComparison.Ordinal))
        {
            ValidationError = Kind == "date"
                ? PropertyPhrase.DateShapeError
                : PropertyPhrase.DatetimeShapeError;
            return false;
        }
        ValidationError = null;
        return true;
    }

    /// <summary>Restore the last committed value byte-exactly and
    /// announce once (through the injected delegate).</summary>
    public void Revert()
    {
        Draft = PropertyDraft.Copy(_committedBaseline);
        ValidationError = null;
        RevertAnnounceDelegate(this);
    }

    /// <summary>Called by the workspace after a successful write with
    /// the DRAFT THAT WAS DISPATCHED (adversarial round 2): the user
    /// may have kept editing while the write was in flight, and
    /// marking the CURRENT draft committed would bless input the disk
    /// never saw. The baseline becomes the dispatched value; a
    /// mid-flight edit stays honestly dirty against it.</summary>
    public void MarkCommitted(PropertyDraft dispatched)
    {
        _committedBaseline = PropertyDraft.Copy(dispatched);
        ValidationError = null;
        OnPropertyChanged(nameof(IsDirty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
