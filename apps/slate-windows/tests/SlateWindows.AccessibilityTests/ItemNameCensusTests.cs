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
    // Generic arity on any segment of a nested type (codex PR 3 round 2).
    [InlineData("System.Collections.Generic.Dictionary`2+Enumerator[System.String,System.Int32]")]
    [InlineData("SlateWindows.Outer`1+Inner`1[System.String]")]
    [InlineData("SlateWindows.Outer`1+Inner")]
    // Record dumps, as a C# record's synthesized ToString() prints them —
    // the type name qualified or not (codex PR 3 round 1).
    [InlineData(@"RecentVault { Path = C:\Vaults\at-vault, DisplayName = at-vault, LastOpenedMs = 1790112463550 }")]
    [InlineData("KeyTypeChoice { Label = Any key type, Kind =  }")]
    [InlineData("CanvasTableRow { NodeId = grp-research, Kind = group, Title = Research, SpeakableName = Research, GroupPath = System.String[] }")]
    [InlineData("FixtureRow { Index = 0, Name = Note 00000, Status = Open, Notes = fixture row 0 }")]
    [InlineData("SlateWindows.Foo.BarRow { Name = value }")]
    [InlineData("Outer+InnerRow { Name = value }")]
    // A dump broken over lines is still a dump (the spec review, round 21).
    [InlineData("RecentVault {\n  Path = C:\\Vaults\\at-vault,\n  DisplayName = at-vault\n}")]
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
    // A root and a dot is a file name when the tail is not a type, and a
    // record-like name that does not END at the dump's brace is no dump
    // (codex PR 3 round 2).
    [InlineData("System.md")]
    [InlineData("System.String.md")]
    [InlineData("SlateWindows.note.md")]
    [InlineData("Plan { owner = Alice }.md")]
    [InlineData("child.md")]
    [InlineData("Folder/child.md")]
    [InlineData("child.md, file")]
    [InlineData("board.canvas")]
    // The names the fixes give.
    [InlineData("Note 00000")]
    [InlineData("Vault root, folder Vault root")]
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

    /// <summary>Codex PR 3 round 4 (AR-20): Quick Open speaks a note by
    /// core's extension-stripped label, so the census meets what Quick Open
    /// EXPOSES, never the file name the cases above test. Each legal name's
    /// label, as core derives it, is speakable.</summary>
    [Theory]
    [InlineData("note.md", "note")]
    [InlineData("notes.v2.md", "notes.v2")]
    [InlineData("README.md", "README")]
    [InlineData("Part.One.md", "Part.One")]
    [InlineData("Smith.Jones.md", "Smith.Jones")]
    public void WhatQuickOpenExposesForALegalNameIsSpeakable(string file, string label)
    {
        Assert.Equal(label, QuickOpenLabel(file));
        Assert.False(ShellAccessibilityTests.IsUnspeakableItemName(label), label);
    }

    /// <summary>A legal note titled like a type name or a record dump:
    /// its label alone matches the census's shapes, but its Quick Open row
    /// publishes the note's vault path as HelpText, and the label is that
    /// file's name with the extension stripped — so the row is spared
    /// (codex PR 3 round 4; AR-20 now covers only hosts with no such
    /// provenance).</summary>
    [Theory]
    [InlineData("System.String.md", "System.String")]
    [InlineData("Plan { owner = Alice }.md", "Plan { owner = Alice }")]
    public void WhatQuickOpenExposesForATypeShapedTitleIsSparedByItsPath(string file, string label)
    {
        Assert.Equal(label, QuickOpenLabel(file));
        Assert.True(ShellAccessibilityTests.IsUnspeakableItemName(label), label);
        Assert.False(ShellAccessibilityTests.IsUnspeakableItemName(label, $"Notes/{file}"), label);
    }

    /// <summary>The provenance, pinned both ways. A Quick Open row named
    /// "System.String" whose HelpText is ".../System.String.md" is its file's
    /// label and passes.</summary>
    [Theory]
    [InlineData("System.String", "Notes/System.String.md")]
    [InlineData("System.String", "System.String.md")]
    [InlineData("System.String", @"Notes\System.String.markdown")]
    [InlineData("Plan { owner = Alice }", "Projects/Plan { owner = Alice }.md")]
    public void ANameThatIsItsOwnFilesLabelIsSpeakable(string name, string helpText) =>
        Assert.False(ShellAccessibilityTests.IsUnspeakableItemName(name, helpText), $"{name} / {helpText}");

    /// <summary>...and the same name with an unrelated HelpText, a HelpText
    /// that is no file path, or none at all is still flagged — as is a real
    /// ToString leak beside its row's path, which never equals that file's
    /// name.</summary>
    [Theory]
    [InlineData("System.String", "Notes/notes.md")]
    [InlineData("System.String", "Opens the note.")]
    [InlineData("System.String", "Notes/System.String")]
    [InlineData("System.String", "Notes/System.String.md.bak")]
    [InlineData("System.String", "")]
    [InlineData("System.String", null)]
    [InlineData("Plan { owner = Alice }", "Projects/plan.md")]
    [InlineData("SlateWindows.QuickSwitcherRowViewModel", "A/note.md")]
    [InlineData("QuickSwitcherRowViewModel { Path = A/note.md, Name = note.md }", "A/note.md")]
    public void ANameWithoutItsOwnFilesProvenanceIsStillFlagged(string name, string? helpText) =>
        Assert.True(ShellAccessibilityTests.IsUnspeakableItemName(name, helpText), $"{name} / {helpText}");

    /// <summary>The label Quick Open shows and speaks for a file, from core's
    /// own ranking (the QuickSwitcherViewModel's source).</summary>
    private static string QuickOpenLabel(string file) =>
        Assert.Single(uniffi.slate_uniffi.SlateUniffiMethods.SwitcherRankTop(
            [new uniffi.slate_uniffi.SwitcherFile(file, file)], string.Empty, [], 5).Rows).DisplayName;
}
