// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>The immutable identity an add sheet writes against
/// (contract 1, adversarial round 1): the owner path, the header's
/// publish hash, and the key set of THAT read. Captured when the
/// sheet opens; the commit never re-resolves the active tab or
/// re-reads a hash.
///
/// <paramref name="Keys"/> holds the AUTHORITATIVE top-level YAML
/// keys (round 6), not the flattened property rows: a nested
/// container like `person: {name: …}` appears in properties only as
/// `person.name`, so collision-checking against rows would let a
/// flat `person` add silently replace the whole mapping.</summary>
internal sealed record PropertyAddIntent(
    string Path, string ContentHash, IReadOnlyList<string> Keys);

/// <summary>
/// W4-4 (#736): the add-property sheet. Validation is TOTAL and
/// PRE-CORE (feature contract 6): empty, dotted, and duplicate keys
/// are refused with the verbatim message before any FFI call; a
/// blocked commit announces its disabled reason (the mac residue
/// site's twin). Commits route through the workspace seam with the
/// captured intent — the sheet itself never writes, and an
/// undispatched commit is reported honestly (never as success).
/// </summary>
internal sealed class AddPropertyViewModel : BindableBase
{
    /// <summary>The eight pickable kinds, mac sheet order.</summary>
    public static readonly string[] Kinds =
        ["text", "number", "boolean", "date", "datetime", "wikilink", "list", "tag_list"];

    private readonly PropertyAddIntent? _intent;
    private readonly Func<PropertyAddIntent, string, PropertyValue, bool> _commit;
    private readonly Action<A11yEvent> _announce;
    private string _key = "";
    private string _kind = "text";
    private string _value = "";
    private string? _validationError;

    public AddPropertyViewModel(
        PropertyAddIntent? intent,
        Func<PropertyAddIntent, string, PropertyValue, bool> commit,
        Action<A11yEvent> announce)
    {
        _intent = intent;
        _commit = commit;
        _announce = announce;
    }

    /// <summary>Posted once, on sheet open (canonical, the §W-D
    /// anchor for this family).</summary>
    public void SheetShown() => _announce(new A11yEvent.AddPropertySheetShown());

    public string Key
    {
        get => _key;
        set
        {
            if (SetField(ref _key, value))
            {
                ValidationError = null;
            }
        }
    }

    public string Kind
    {
        get => _kind;
        set
        {
            if (SetField(ref _kind, value))
            {
                ValidationError = null;
            }
        }
    }

    /// <summary>The initial value (round 3): shape-derived kinds
    /// (date, datetime, wikilink, tag_list) REQUIRE a shape-valid
    /// seed — core reclassifies stored values by shape on the
    /// authoritative re-read, so an empty seed would silently change
    /// the chosen type (or, for wikilink, be rejected outright).</summary>
    public string Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
            {
                ValidationError = null;
            }
        }
    }

    public string? ValidationError
    {
        get => _validationError;
        private set => SetField(ref _validationError, value);
    }

    /// <summary>Validate and commit against the captured intent.
    /// Returns true only when the write was actually DISPATCHED
    /// (contract 6 — a refused dispatch is never reported as
    /// success); false leaves the sheet open with the error.</summary>
    public bool Add()
    {
        string key = _key.Trim();
        string? error = null;
        if (_intent is null)
        {
            error = PropertyPhrase.NoNoteError;
        }
        else if (key.Length == 0)
        {
            error = PropertyPhrase.KeyEmptyError;
        }
        else if (key.Contains('.'))
        {
            error = PropertyPhrase.KeyDottedError;
        }
        else if (_intent.Keys.Contains(key, StringComparer.Ordinal))
        {
            error = PropertyPhrase.KeyDuplicateError(key);
        }
        PropertyValue? built = null;
        error ??= BuildInitialValue(key, _kind, _value, out built);
        if (error is not null)
        {
            ValidationError = error;
            // W0.5-3 residue: the disabled reason is spoken, the mac
            // sheet's residue-site twin.
            _announce(new A11yEvent.HostComposed(error, A11yPriority.High));
            return false;
        }
        if (!_commit(_intent!, key, built!))
        {
            // The seam refused (dirty owner, conflict, in-flight) and
            // already announced its reason; the sheet keeps the draft
            // with the verbatim failure copy.
            ValidationError = PropertyPhrase.AddFailedDraftKept;
            return false;
        }
        return true;
    }

    /// <summary>Async write failed: the sheet stays open, the draft
    /// intact, with the verbatim failure copy inline (§2.4).</summary>
    internal void MarkAddFailed() => ValidationError = PropertyPhrase.AddFailedDraftKept;

    /// <summary>Build the initial PropertyValue from the chosen KEY,
    /// kind, and seed text (contract 10). The SHAPE of the seed is
    /// checked here only to produce a good message; whether the
    /// stored kind will actually match is ASKED OF CORE
    /// (round_trip_property_kind) rather than mirrored — every host
    /// copy of core's key/shape classifier drifted (rounds 3–5), so
    /// the authority now lives in one place. Returns the error, or
    /// null with <paramref name="built"/> set.</summary>
    internal static string? BuildInitialValue(
        string key, string kind, string rawValue, out PropertyValue? built)
    {
        string? shapeError = BuildCandidate(key, kind, rawValue, out built);
        if (shapeError is not null)
        {
            built = null;
            return shapeError;
        }
        // THE authority: core runs its real emit-then-classify round
        // trip. A mismatch means the choice would silently become
        // something else on the authoritative re-read; null means the
        // value can't be stored at all.
        string? storedKind = SlateUniffiMethods.RoundTripPropertyKind(key, built!);
        if (storedKind is null)
        {
            built = null;
            return PropertyPhrase.AddFailedDraftKept;
        }
        if (!string.Equals(storedKind, kind, StringComparison.Ordinal))
        {
            built = null;
            // Core has spoken; the host only picks better WORDING for
            // the shape-typo cases (round 6).
            return kind switch
            {
                "date" => PropertyPhrase.DateShapeError,
                "datetime" => PropertyPhrase.DatetimeShapeError,
                _ => PropertyPhrase.StoredKindMismatchError(storedKind),
            };
        }
        return null;
    }

    /// <summary>Shape-level candidate construction. Its refusals are
    /// about MESSAGE QUALITY (a typo'd date says so instead of
    /// "would be stored as text"); correctness is core's answer
    /// above.</summary>
    private static string? BuildCandidate(
        string key, string kind, string rawValue, out PropertyValue? built)
    {
        built = null;
        string value = rawValue.Trim();
        if (kind == "tag_list")
        {
            // A tag list needs a seed tag on any key EXCEPT one core
            // classifies by key alone.
            if (value.Length == 0
                && !key.Equals("tags", StringComparison.OrdinalIgnoreCase))
            {
                return PropertyPhrase.TagListSeedError;
            }
            built = value.Length == 0
                ? new PropertyValue.TagList([])
                : new PropertyValue.TagList([value.TrimStart('#')]);
            return null;
        }
        switch (kind)
        {
            case "number":
                if (value.Length == 0)
                {
                    built = new PropertyValue.Integer(0);
                    return null;
                }
                if (long.TryParse(
                    value, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out long integer))
                {
                    built = new PropertyValue.Integer(integer);
                    return null;
                }
                if (double.TryParse(
                        value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double floating)
                    && !double.IsNaN(floating) && !double.IsInfinity(floating))
                {
                    built = new PropertyValue.Float(floating);
                    return null;
                }
                return PropertyPhrase.FloatShapeError;
            case "boolean":
                if (value.Length == 0)
                {
                    built = new PropertyValue.Boolean(false);
                    return null;
                }
                if (bool.TryParse(value, out bool flag))
                {
                    built = new PropertyValue.Boolean(flag);
                    return null;
                }
                return PropertyPhrase.BooleanShapeError;
            case "date":
                // No calendar check here (round 6): core classifies
                // date STRUCTURALLY, so refusing 2026-99-99 would be
                // a FALSE refusal of something core stores as a date.
                built = new PropertyValue.Date(value);
                return null;
            case "datetime":
                built = new PropertyValue.Datetime(value);
                return null;
            case "wikilink":
                if (value.Length == 0)
                {
                    return PropertyPhrase.WikilinkEmptyError;
                }
                if (value.Contains("]]"))
                {
                    return PropertyPhrase.WikilinkBracketError;
                }
                if (value.Contains('\n') || value.Contains('\r'))
                {
                    return PropertyPhrase.WikilinkNewlineError;
                }
                built = new PropertyValue.Wikilink(value);
                return null;
            case "list":
                built = value.Length == 0
                    ? new PropertyValue.List([])
                    : new PropertyValue.List([new PropertyValue.Text(value)]);
                return null;
            default:
                built = new PropertyValue.Text(rawValue);
                return null;
        }
    }
}
