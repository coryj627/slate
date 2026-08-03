// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using Xunit;

namespace SlateWindows.Tests;

/// <summary>
/// W4-4 §W-C label goldens: PropertyPhrase strings pinned VERBATIM.
/// The mac twin pins the identical strings (PropertiesWidgetTests);
/// the designation is recorded in w_c_matrix.md. Any change here
/// must change both platforms and the matrix together.
/// </summary>
public class PropertyPhraseTests
{
    [Theory]
    [InlineData("text", "text")]
    [InlineData("number", "number")]
    [InlineData("boolean", "boolean")]
    [InlineData("date", "date")]
    [InlineData("datetime", "date and time")]
    [InlineData("wikilink", "link")]
    [InlineData("list", "list")]
    [InlineData("tag_list", "tag list")]
    [InlineData("future_kind", "future_kind")]
    public void TypeWordsAreThePinnedVocabulary(string kind, string expected) =>
        Assert.Equal(expected, PropertyPhrase.TypeWord(kind));

    [Fact]
    public void EditorAndDisplayLabelsAreVerbatim()
    {
        Assert.Equal(
            "Property due, date, editable",
            PropertyPhrase.EditorLabel("due", "date"));
        Assert.Equal(
            "Property published, boolean: true",
            PropertyPhrase.DisplayLabel("published", "boolean", "true"));
        Assert.Equal(
            "Property source, link to basic",
            PropertyPhrase.DisplayLabel("source", "wikilink", "basic"));
        Assert.Equal(
            "Property title: Property metadata",
            PropertyPhrase.DisplayLabel("title", "text", "Property metadata"));
        Assert.Equal(
            "Property created, date and time: 2026-03-01T09:30:00Z",
            PropertyPhrase.DisplayLabel("created", "datetime", "2026-03-01T09:30:00Z"));
    }

    [Fact]
    public void ListLabelsDistinguishTagLists()
    {
        Assert.Equal(
            "Property aliases, list of 2: property fixture, metadata fixture",
            PropertyPhrase.ListLabel("aliases", "list", 2, "property fixture, metadata fixture"));
        Assert.Equal(
            "Property tags, tag list of 2: #fixture, #properties",
            PropertyPhrase.ListLabel("tags", "tag_list", 2, "#fixture, #properties"));
        Assert.Equal(
            "Property aliases, item 1 of 2",
            PropertyPhrase.ListItemLabel("aliases", "list", 1, 2));
        Assert.Equal(
            "Property tags, tag 2 of 2",
            PropertyPhrase.ListItemLabel("tags", "tag_list", 2, 2));
        Assert.Equal("Remove item 1 from aliases",
            PropertyPhrase.RemoveItemLabel("aliases", "list", 1));
        Assert.Equal("Remove tag 2 from tags",
            PropertyPhrase.RemoveItemLabel("tags", "tag_list", 2));
        Assert.Equal("Add item to aliases", PropertyPhrase.AddItemLabel("aliases", "list"));
        Assert.Equal("Add tag to tags", PropertyPhrase.AddItemLabel("tags", "tag_list"));
    }

    [Fact]
    public void ActionLabelsAndHintsAreVerbatim()
    {
        Assert.Equal("Save changes to due", PropertyPhrase.SaveLabel("due"));
        Assert.Equal("Revert changes to due", PropertyPhrase.RevertLabel("due"));
        Assert.Equal(
            "Restores the last committed value for due.",
            PropertyPhrase.RevertHint("due"));
        Assert.Equal("Delete property due", PropertyPhrase.DeleteLabel("due"));
        Assert.Equal("Step count", PropertyPhrase.StepperLabel("count"));
        Assert.Equal("Pick… vault file for source", PropertyPhrase.PickerLabel("source"));
        Assert.Equal(
            "Validation error: Date must be YYYY-MM-DD.",
            PropertyPhrase.ValidationLabel(PropertyPhrase.DateShapeError));
    }

    [Fact]
    public void HeaderAndEmptyStateAreVerbatim()
    {
        Assert.Equal("Properties, 1 property", PropertyPhrase.HeaderGroupName(1));
        Assert.Equal("Properties, 3 properties", PropertyPhrase.HeaderGroupName(3));
        Assert.Equal("Properties, 1 item", PropertyPhrase.HeaderText(1));
        Assert.Equal("Properties, 3 items", PropertyPhrase.HeaderText(3));
        Assert.Equal("No properties yet. Add one to start.", PropertyPhrase.EmptyState);
        Assert.Equal("Add a property to this note.", PropertyPhrase.AddPropertyHint);
    }

    [Fact]
    public void ValidationAndDialogStringsAreVerbatim()
    {
        Assert.Equal("Key can't be empty.", PropertyPhrase.KeyEmptyError);
        Assert.Equal(
            "Dotted keys aren't supported yet — use a flat key.",
            PropertyPhrase.KeyDottedError);
        Assert.Equal(
            "A property named `due` already exists on this note.",
            PropertyPhrase.KeyDuplicateError("due"));
        Assert.Equal("No note is loaded.", PropertyPhrase.NoNoteError);
        Assert.Equal(
            "The property was not added. Your draft is still here.",
            PropertyPhrase.AddFailedDraftKept);
        Assert.Equal("Delete property `due`?", PropertyPhrase.DeleteConfirmTitle("due"));
        Assert.Equal(
            "This removes the `due` key from the note's frontmatter.",
            PropertyPhrase.DeleteConfirmMessage("due"));
        Assert.Equal(
            "Revert or save this property draft before deleting the property.",
            PropertyPhrase.DeleteWhileDirtyReason);
        Assert.Equal("Property Edit Blocked", PropertyPhrase.ConflictTitle);
        Assert.Equal(
            "note.md was modified outside the editor while you were editing the "
            + "`due` property. Choose how to resolve.",
            PropertyPhrase.ConflictMessage("note.md", "due"));
        Assert.Equal(
            "Apply or discard uncommitted property changes before renaming properties.",
            PropertyPhrase.BulkRenameDirtyDraftsReason);
        Assert.Equal(
            "Run a preview to see which files would change.",
            PropertyPhrase.BulkRenameEmptyState);
        Assert.Equal(
            "Apply the previewed property rename across the vault.",
            PropertyPhrase.BulkRenameApplyHint);
    }

    [Fact]
    public void DateDisplayDegradesInsteadOfInventing()
    {
        // A parseable date renders locale-medium; an unparseable one
        // (the properties_metadata fixture's 2026-13-45) renders its
        // raw text — a date is never invented.
        Assert.Equal("2026-13-45", PropertyPhrase.DateDisplay("2026-13-45"));
        Assert.NotEqual("2026-03-01", PropertyPhrase.DateDisplay("2026-03-01"));
        Assert.Contains("2026", PropertyPhrase.DateDisplay("2026-03-01"));
    }
}
