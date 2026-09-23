// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// W7-7 PR 3 (#1246, contract R-4): the item-name census's verdict,
/// pinned in both directions without a window (the AxeWaiverTests
/// shape). Every name the census rejects is one WPF's <c>ToString()</c>
/// fallback produced, most of them heard verbatim by the 2026-09-22 NVDA
/// pass (record F3); every name it accepts is one a surface legitimately
/// speaks — a census that failed on those would be switched off, not
/// obeyed.
/// </summary>
public sealed class ItemNameCensusTests
{
    [Theory]
    // .NET type names, as object.ToString() prints them.
    [InlineData("SlateWindows.FileTreeNodeViewModel")]
    [InlineData("SlateWindows.WorkspacePaneNodeViewModel")]
    [InlineData("SlateWindows.Panels.PropertyRowViewModel")]
    [InlineData("SlateWindows.Templates.TemplatePromptFieldViewModel")]
    [InlineData("SlateWindows.Bases.BaseGridRowViewModel")]
    // A nested type prints with '+'; an array with brackets (a markdown
    // table row is a string[]); a generic with its arity and arguments.
    [InlineData("SlateWindows.Tests.Fixture+Row")]
    [InlineData("System.String[]")]
    [InlineData("System.Collections.Generic.List`1[System.String]")]
    [InlineData("uniffi.slate_uniffi.SlateSession")]
    // Every root the discriminator knows reaches the type shape.
    [InlineData("ICSharpCode.AvalonEdit.Document.TextDocument")]
    [InlineData("Microsoft.Win32.OpenFileDialog")]
    [InlineData("GridConformanceHost.Program+FixtureRow")]
    // Record dumps, as a C# record's synthesized ToString() prints them —
    // the type name qualified or not (codex PR 3 round 1).
    [InlineData(@"RecentVault { Path = C:\Vaults\at-vault, DisplayName = at-vault, LastOpenedMs = 1790112463550 }")]
    [InlineData("KeyTypeChoice { Label = Any key type, Kind =  }")]
    [InlineData("CanvasTableRow { NodeId = grp-research, Kind = group, Title = Research, SpeakableName = Research, GroupPath = System.String[] }")]
    [InlineData("FixtureRow { Index = 0, Name = Note 00000, Status = Open, Notes = fixture row 0 }")]
    [InlineData("SlateWindows.Foo.BarRow { Name = value }")]
    [InlineData("Outer+InnerRow { Name = value }")]
    public void TypeNamesAndRecordDumpsAreUnspeakable(string name) =>
        Assert.True(ShellAccessibilityTests.IsUnspeakableItemName(name), name);

    [Theory]
    // File names and other dotted user content are row identities (the
    // Bases grid, the file trees, the rename preview): spec §4.2's pattern
    // matched these, and so did a capitalised-last-segment rule, which is
    // why a type name must start from one of the app's roots to count.
    [InlineData("note.md")]
    [InlineData("notes.v2.md")]
    [InlineData("README.MD")]
    [InlineData("Part.One")]
    [InlineData("Smith.Jones")]
    [InlineData("Systems.Thinking")]
    [InlineData("child.md")]
    [InlineData("Folder/child.md")]
    [InlineData("child.md, file")]
    [InlineData("board.canvas")]
    // The names the fixes give.
    [InlineData("Note 00000")]
    [InlineData("Editor panes")]
    [InlineData("Recent vaults")]
    [InlineData("Accessible grids (2021)")]
    [InlineData("Property title, text, editable")]
    [InlineData("Any key type")]
    [InlineData("Name (A to Z)")]
    [InlineData("Row 2")]
    // Sentences with dots and braces that are not records.
    [InlineData("Level 2 heading: Ingredients")]
    [InlineData("e.g. a note")]
    [InlineData("Version 1.2.3")]
    [InlineData("Use { and } to fold")]
    [InlineData("")]
    [InlineData(null)]
    public void SpokenNamesAreSpeakable(string? name) =>
        Assert.False(ShellAccessibilityTests.IsUnspeakableItemName(name), name);
}
