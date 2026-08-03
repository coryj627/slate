// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;

namespace SlateWindows.Panels;

/// <summary>
/// W4-4 (#736): the property label vocabulary — the mac
/// NotePropertiesHeader strings, host-duplicated by designation
/// (§W-C label goldens: each platform pins the identical verbatim
/// strings in its own unit tests; recorded in w_c_matrix.md).
///
/// The kind set is OPEN (core may grow inference kinds); unknown
/// kinds pass through raw so a new core kind degrades to an honest
/// label instead of throwing. Date formatting likewise degrades:
/// an unparseable stored date renders its raw text — a date is
/// never invented (the properties_metadata.md fixture pins core
/// keeping raw values under a date kind).
/// </summary>
internal static class PropertyPhrase
{
    /// <summary>The spoken type word inside editor labels.</summary>
    public static string TypeWord(string kind) => kind switch
    {
        "text" => "text",
        "number" => "number",
        "boolean" => "boolean",
        "date" => "date",
        "datetime" => "date and time",
        "wikilink" => "link",
        "list" => "list",
        "tag_list" => "tag list",
        _ => kind,
    };

    /// <summary>Type-cued editor label: "Property {key}, {type},
    /// editable".</summary>
    public static string EditorLabel(string key, string kind) =>
        $"Property {key}, {TypeWord(kind)}, editable";

    /// <summary>Read-only display label per kind.</summary>
    public static string DisplayLabel(string key, string kind, string display) => kind switch
    {
        "boolean" => $"Property {key}, boolean: {display}",
        "number" => $"Property {key}, number: {display}",
        "date" => $"Property {key}, date: {display}",
        "datetime" => $"Property {key}, date and time: {display}",
        "wikilink" => $"Property {key}, link to {display}",
        _ => $"Property {key}: {display}",
    };

    /// <summary>Read-only list label: "Property {key}, list of {n}:
    /// {joined}" (tag lists say "tag list of").</summary>
    public static string ListLabel(string key, string kind, int count, string joined) =>
        kind == "tag_list"
            ? $"Property {key}, tag list of {count}: {joined}"
            : $"Property {key}, list of {count}: {joined}";

    /// <summary>Per-item editor label.</summary>
    public static string ListItemLabel(string key, string kind, int index, int count) =>
        kind == "tag_list"
            ? $"Property {key}, tag {index} of {count}"
            : $"Property {key}, item {index} of {count}";

    public static string RemoveItemLabel(string key, string kind, int index) =>
        kind == "tag_list"
            ? $"Remove tag {index} from {key}"
            : $"Remove item {index} from {key}";

    public static string AddItemLabel(string key, string kind) =>
        kind == "tag_list" ? $"Add tag to {key}" : $"Add item to {key}";

    public static string SaveLabel(string key) => $"Save changes to {key}";

    public static string RevertLabel(string key) => $"Revert changes to {key}";

    public static string RevertHint(string key) =>
        $"Restores the last committed value for {key}.";

    public static string DeleteLabel(string key) => $"Delete property {key}";

    public static string StepperLabel(string key) => $"Step {key}";

    public static string PickerLabel(string key) => $"Pick… vault file for {key}";

    public static string ValidationLabel(string error) => $"Validation error: {error}";

    /// <summary>Header AX group name: "Properties, {n}
    /// property/properties".</summary>
    public static string HeaderGroupName(int count) =>
        count == 1 ? "Properties, 1 property" : $"Properties, {count} properties";

    /// <summary>Visible heading text: "Properties, {n} item(s)".</summary>
    public static string HeaderText(int count) =>
        count == 1 ? "Properties, 1 item" : $"Properties, {count} items";

    public const string EmptyState = "No properties yet. Add one to start.";

    public const string AddPropertyHint = "Add a property to this note.";

    public const string RenameAcrossVaultLabel = "Rename property across the vault";

    // --- Validation messages (verbatim mac twins) ---
    public const string DateShapeError = "Date must be YYYY-MM-DD.";
    public const string IntegerShapeError = "Must be a whole number.";
    public const string FloatShapeError = "Must be a finite decimal number.";
    public const string WikilinkEmptyError = "Wikilink target can't be empty.";
    public const string WikilinkBracketError = "Wikilink target can't contain ]].";
    public const string KeyEmptyError = "Key can't be empty.";
    public const string KeyDottedError = "Dotted keys aren't supported yet — use a flat key.";
    public const string NoNoteError = "No note is loaded.";

    // Add-sheet initial-value validation (Windows addition, round 3:
    // shape-derived kinds need a shape-valid seed or the
    // authoritative re-read reclassifies them — the type choice must
    // never lie).
    public const string InitialValueLabel = "Initial value";
    public const string DatetimeShapeError =
        "Enter a date and time like 2026-05-01T10:00:00.";
    public const string TagListSeedError =
        "Tag lists need at least one tag to keep their type.";
    public const string BooleanShapeError = "Enter true or false.";

    public static string KeyDuplicateError(string key) =>
        $"A property named `{key}` already exists on this note.";

    public const string AddFailedDraftKept =
        "The property was not added. Your draft is still here.";

    // --- Delete confirmation ---
    public static string DeleteConfirmTitle(string key) => $"Delete property `{key}`?";

    public static string DeleteConfirmMessage(string key) =>
        $"This removes the `{key}` key from the note's frontmatter.";

    public const string DeleteWhileDirtyReason =
        "Revert or save this property draft before deleting the property.";

    // --- Conflict dialog ---
    public const string ConflictTitle = "Property Edit Blocked";

    public static string ConflictMessage(string filename, string key) =>
        $"{filename} was modified outside the editor while you were editing the "
        + $"`{key}` property. Choose how to resolve.";

    public const string ConflictKeepMineHint =
        "Re-apply your property edit, overwriting the external change.";

    public const string ConflictReloadHint =
        "Discard this property edit and reload properties from disk. "
        + "Markdown body edits are kept.";

    public const string ConflictCancelHint =
        "Close this dialog. The properties panel stays as it was.";

    // --- Bulk rename ---
    public const string BulkRenameHeader = "Rename property across the vault";
    public const string BulkRenameOldKeyLabel = "Old property key";
    public const string BulkRenameNewKeyLabel = "New property key";
    public const string BulkRenameApplyHint =
        "Apply the previewed property rename across the vault.";
    public const string BulkRenamePreviewProgress = "Computing preview";
    public const string BulkRenameApplyProgress = "Applying rename";
    public const string BulkRenameEmptyState =
        "Run a preview to see which files would change.";
    public const string BulkRenameDirtyDraftsReason =
        "Apply or discard uncommitted property changes before renaming properties.";

    /// <summary>Displayed date: locale-medium when the stored value
    /// parses, raw text otherwise (a date is never invented).</summary>
    public static string DateDisplay(string raw)
    {
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return date.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }
        return raw;
    }
}
