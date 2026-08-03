// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>The immutable identity an add sheet writes against
/// (contract 1, adversarial round 1): the owner path, the header's
/// publish hash, and the key set of THAT read. Captured when the
/// sheet opens; the commit never re-resolves the active tab or
/// re-reads a hash.</summary>
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
    /// kind, and seed text (rounds 3+4, contract 10): every
    /// advertised kind must SURVIVE the authoritative re-read as
    /// itself. Core classifies stored values by KEY and SHAPE, so
    /// validation is key-aware and shape-aware — a selection that
    /// cannot be represented with the given seed refuses with the
    /// verbatim message rather than lying about the stored type. The
    /// either-refuse-or-match matrix fact pins this table against
    /// core end-to-end. Returns the error, or null with
    /// <paramref name="built"/> set.</summary>
    internal static string? BuildInitialValue(
        string key, string kind, string rawValue, out PropertyValue? built)
    {
        built = null;
        string value = rawValue.Trim();
        // Core classifies EVERY list under the `tags` key as a tag
        // list; only that choice is representable there. An empty
        // seed is fine — the key itself carries the classification.
        if (key == "tags")
        {
            if (kind != "tag_list")
            {
                return PropertyPhrase.TagsKeyKindError;
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
                if (!DateOnly.TryParseExact(
                    value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                {
                    return PropertyPhrase.DateShapeError;
                }
                built = new PropertyValue.Date(value);
                return null;
            case "datetime":
                // A date-only seed parses via DateTimeOffset but
                // core would classify it DATE (round 4): a datetime
                // requires an explicit time component.
                if (!value.Contains('T')
                    || (!DateTimeOffset.TryParse(
                            value, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out _)
                        && !DateTime.TryParseExact(
                            value, "yyyy-MM-ddTHH:mm:ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out _)))
                {
                    return PropertyPhrase.DatetimeShapeError;
                }
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
                // A #-prefixed element would classify the whole list
                // as a tag list (round 4).
                if (value.StartsWith('#'))
                {
                    return PropertyPhrase.StoredKindMismatchError("tag_list");
                }
                built = value.Length == 0
                    ? new PropertyValue.List([])
                    : new PropertyValue.List([new PropertyValue.Text(value)]);
                return null;
            case "tag_list":
                // An EMPTY tag list has no #-shape for the classifier
                // to recognize — it would re-read as a plain list, so
                // the type choice requires a seed tag.
                if (value.Length == 0)
                {
                    return PropertyPhrase.TagListSeedError;
                }
                built = new PropertyValue.TagList([value.TrimStart('#')]);
                return null;
            default:
                // Core QUOTES YAML-scalar shapes (true/123/1.5) so
                // those stay text, but date/datetime/wikilink shapes
                // are emitted bare and would reclassify (round 4 —
                // pinned empirically by the either-refuse-or-match
                // matrix).
                if (DateOnly.TryParseExact(
                    value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
                {
                    return PropertyPhrase.StoredKindMismatchError("date");
                }
                if (value.Contains('T')
                    && DateTimeOffset.TryParse(
                        value, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out _))
                {
                    return PropertyPhrase.StoredKindMismatchError("datetime");
                }
                if (value.StartsWith("[[", StringComparison.Ordinal)
                    && value.EndsWith("]]", StringComparison.Ordinal))
                {
                    return PropertyPhrase.StoredKindMismatchError("wikilink");
                }
                built = new PropertyValue.Text(rawValue);
                return null;
        }
    }
}
