// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1098: a citations republish restores the list SELECTION (W4-5 round
/// 4) but the publish destroys every row container, and WPF ejects
/// keyboard focus to the window root when a focused item unloads (the
/// W5-4 tree finding) — so an AT user sitting on a row had to Tab back
/// after every save. The fix samples focus ownership BEFORE the rebuild
/// (the new <c>RowsPublishing</c> pre-event) and, after the selection
/// restore, puts focus back on the restored row's container — guarded so
/// it never steals. The shell gate's citations journey asserts the
/// keypress; these facts pin the seam and the wiring.
/// </summary>
public sealed class CitationsFocusRestoreTests
{
    [Fact]
    public void PublishRaisesPublishingBeforeTheRebuildAndPublishedAfter()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "citations-publishing");
        File.WriteAllText(
            Path.Combine(fixture.Root, "cited.md"), "Cites [@knuth1984] here.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var panel = new CitationsPanelViewModel(session, _ => { }, synchronousForTests: true);

        var order = new List<string>();
        panel.RowsPublishing += (_, _) => order.Add($"publishing:{panel.Rows.Count}");
        panel.RowsPublished += (_, _) => order.Add($"published:{panel.Rows.Count}");

        panel.NoteChanged("cited.md");
        // The first publish rebuilds from nothing; the second sees the
        // previous rows still standing at Publishing time.
        Assert.Equal("publishing:0", order[0]);
        Assert.StartsWith("published:", order[1]);
        int rows = panel.Rows.Count;

        order.Clear();
        panel.Refresh();
        Assert.Equal([$"publishing:{rows}", $"published:{rows}"], order);
        panel.Shutdown();
    }

    /// <summary>
    /// The window samples at Publishing, restores selection THEN focus
    /// at Published, and the focus restore is guarded (a modal surface
    /// or a real claim elsewhere wins) — pinned at the source because
    /// the behavior needs a live window and a keypress, which the shell
    /// gate supplies.
    /// </summary>
    [Fact]
    public void TheWindowWiresTheSampleAndTheGuardedRestore()
    {
        CSharpSource citations = CSharpSource.Load("MainWindow.Citations.cs");

        MethodDeclarationSyntax observe = citations.Method("ObserveCitationPanels");
        string observeText = CSharpSource.Normalize(observe);
        Assert.Contains(
            "_observedCitations.RowsPublishing+=CitationRows_Publishing",
            observeText,
            StringComparison.Ordinal);
        Assert.Contains(
            "_observedCitations.RowsPublishing-=CitationRows_Publishing",
            observeText,
            StringComparison.Ordinal);

        MethodDeclarationSyntax sample = citations.Method("CitationRows_Publishing");
        string sampleText = CSharpSource.Normalize(sample);
        Assert.Contains("PanelCitationsList.IsKeyboardFocusWithin", sampleText, StringComparison.Ordinal);
        // The KEY is sampled here too: the publish's clear raises a
        // SelectionChanged with a null SelectedItem, and a handler that
        // nulled the key on it erased the reading position before the
        // restore ran (the shell gate measured an empty selection).
        Assert.Contains("_selectedCitationKey=", sampleText, StringComparison.Ordinal);
        MethodDeclarationSyntax selectionChanged = citations.Method("PanelCitations_SelectionChanged");
        Assert.DoesNotContain(
            "_selectedCitationKey=null",
            CSharpSource.Normalize(selectionChanged),
            StringComparison.Ordinal);

        MethodDeclarationSyntax published = citations.Method("CitationRows_Published");
        string publishedText = CSharpSource.Normalize(published);
        int selectionAt = publishedText.IndexOf("RestoreCitationSelection()", StringComparison.Ordinal);
        int focusAt = publishedText.IndexOf("RestoreCitationFocus()", StringComparison.Ordinal);
        Assert.True(
            selectionAt >= 0 && focusAt > selectionAt,
            "CitationRows_Published must restore the selection, then the focus.");

        MethodDeclarationSyntax restore = citations.Method("RestoreCitationFocus");
        string restoreText = CSharpSource.Normalize(restore);
        foreach (string guard in new[]
        {
            "_citationsListOwnedFocusBeforePublish",
            "TryFocusSearchIfTopmost()",
            "OpenModalSurface",
            "PanelCitationsList.IsKeyboardFocusWithin",
            ".Focus()",
        })
        {
            Assert.Contains(guard, restoreText, StringComparison.Ordinal);
        }
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
