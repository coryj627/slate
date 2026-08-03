// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the add-property sheet. Validation is TOTAL and
/// PRE-CORE (feature contract 6): empty, dotted, and duplicate keys
/// are refused with the verbatim message before any FFI call; a
/// blocked commit announces its disabled reason (the mac residue
/// site's twin). Commits route through the same workspace seam the
/// editor rows use — the sheet itself never writes.
/// </summary>
internal sealed class AddPropertyViewModel : BindableBase
{
    /// <summary>The eight pickable kinds, mac sheet order.</summary>
    public static readonly string[] Kinds =
        ["text", "number", "boolean", "date", "datetime", "wikilink", "list", "tag_list"];

    private readonly Func<IReadOnlyList<string>?> _currentKeys;
    private readonly Action<string, PropertyValue> _commit;
    private readonly Action<A11yEvent> _announce;
    private string _key = "";
    private string _kind = "text";
    private string? _validationError;

    public AddPropertyViewModel(
        Func<IReadOnlyList<string>?> currentKeys,
        Action<string, PropertyValue> commit,
        Action<A11yEvent> announce)
    {
        _currentKeys = currentKeys;
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

    /// <summary>Validate and commit. Returns true when the write was
    /// dispatched; false leaves the sheet open with the error.</summary>
    public bool Add()
    {
        string key = _key.Trim();
        var keys = _currentKeys();
        string? error = null;
        if (keys is null)
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
        else if (keys.Contains(key, StringComparer.Ordinal))
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
        _commit(key, DefaultValueFor(_kind));
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
