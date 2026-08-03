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
        set => SetField(ref _kind, value);
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
        if (error is not null)
        {
            ValidationError = error;
            // W0.5-3 residue: the disabled reason is spoken, the mac
            // sheet's residue-site twin.
            _announce(new A11yEvent.HostComposed(error, A11yPriority.High));
            return false;
        }
        if (!_commit(_intent!, key, DefaultValueFor(_kind)))
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

    /// <summary>The kind-appropriate empty/default value a fresh key
    /// starts with (mac sheet shape).</summary>
    internal static PropertyValue DefaultValueFor(string kind) => kind switch
    {
        "number" => new PropertyValue.Integer(0),
        "boolean" => new PropertyValue.Boolean(false),
        "date" => new PropertyValue.Date(""),
        "datetime" => new PropertyValue.Datetime(""),
        "wikilink" => new PropertyValue.Wikilink(""),
        "list" => new PropertyValue.List([]),
        "tag_list" => new PropertyValue.TagList([]),
        _ => new PropertyValue.Text(""),
    };
}
