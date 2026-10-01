// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Canvas;
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
    /// <summary>A legitimate same-key publication while details is open
    /// retires the opener's container and provider. Escape restores the
    /// current selected citation, whose logical identity survives the
    /// publication; the retained provider is correctly unavailable.</summary>
    [Fact]
    public void EscapeAfterASameKeyRepublishFocusesTheCurrentCitationRow() => RunSta(() =>
    {
        using var host = new ShownShell(
            ("library.bib", "@article{knuth1984,\n title={Literate Programming},\n"
                + " author={Knuth, Donald E.},\n year={1984}\n}\n"),
            ("cited.md", "# Cited\n\nA ghost [@ghostkey] and a citation [@knuth1984].\n"),
            ("ieee.csl", File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "demo-vault", "csl", "ieee.csl"))),
            ("slate.json", "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}"));
        WorkspaceViewModel workspace = host.Workspace;
        PumpedDispatcher.PumpUntilDrained(workspace.SeedWorkForTests);
        workspace.OpenPath("cited.md");
        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.Single(leaf => leaf.Id == "citations");
        workspace.IsRightPaneVisible = true;
        SettleCitations(host);
        ListBox list = Assert.IsType<ListBox>(host.Shell.FindName("PanelCitationsList"));
        CitationRowViewModel oldRow = Knuth(workspace);
        Assert.NotSame(workspace.Citations.Rows[0], oldRow);
        list.SelectedItem = oldRow;
        host.Settle();
        ListBoxItem oldContainer = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(oldRow));
        Assert.True(oldContainer.Focus(), "premise: the Knuth row refused focus.");
        Assert.Same(oldContainer, Keyboard.FocusedElement);
        host.Settle();

        // Connect the actual WPF root through WM_GETOBJECT, then walk the
        // list's exposed logical items and request its public Selection
        // provider, as a client does before reading the selected row.
        _ = SendMessage(new WindowInteropHelper(host.Shell).Handle, 0x003D, IntPtr.Zero, new IntPtr(-25));
        IRawElementProviderSimple oldProvider = SelectedProviderOf(list, oldRow, oldContainer);
        int[] oldRuntimeId = Assert.IsAssignableFrom<IRawElementProviderFragment>(oldProvider).GetRuntimeId();
        Assert.True((bool)oldProvider.GetPropertyValue(AutomationElement.HasKeyboardFocusProperty.Id));

        Assert.True(PressPreview(oldContainer, Key.Enter), "the citation row did not handle Return.");
        host.Settle();
        CitationDetailsViewModel details = Assert.IsType<CitationDetailsViewModel>(workspace.CitationDetails);
        Assert.Same(oldContainer, details.ReturnFocusToken);
        Button close = Assert.IsType<Button>(host.Shell.FindName("CitationDetailsCloseButton"));
        Assert.Same(close, Keyboard.FocusedElement);

        int publishing = 0;
        int published = 0;
        workspace.Citations.RowsPublishing += (_, _) => publishing++;
        workspace.Citations.RowsPublished += (_, _) => published++;
        workspace.Citations.Refresh();
        SettleCitations(host);
        CitationRowViewModel freshRow = Knuth(workspace);
        ListBoxItem freshContainer = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(freshRow));
        Assert.Equal(1, publishing);
        Assert.Equal(1, published);
        Assert.NotSame(oldRow, freshRow);
        Assert.NotSame(oldContainer, freshContainer);
        Assert.False(oldContainer.IsLoaded);
        Assert.Null(PresentationSource.FromVisual(oldContainer));
        Assert.Same(details, workspace.CitationDetails);
        Assert.Same(close, Keyboard.FocusedElement);
        IRawElementProviderSimple freshProvider = SelectedProviderOf(list, freshRow, freshContainer);
        int[] freshRuntimeId = Assert.IsAssignableFrom<IRawElementProviderFragment>(freshProvider).GetRuntimeId();
        Assert.False(oldRuntimeId.SequenceEqual(freshRuntimeId), "the rebuilt row retained its old provider identity.");
        AssertRetired(oldProvider);

        // Raise Escape on the actual focused Close button. No focus call
        // after publication repairs the landing under test: the shipped
        // details handler and RestoreFocusTo must perform it themselves.
        Assert.True(PressPreview(close, Key.Escape), "the details sheet did not handle Escape.");
        host.Settle();
        Assert.Null(workspace.CitationDetails);
        Assert.Same(freshRow, list.SelectedItem);
        Assert.Same(freshContainer, Keyboard.FocusedElement);
        Assert.True((bool)freshProvider.GetPropertyValue(AutomationElement.HasKeyboardFocusProperty.Id));
        AssertRetired(oldProvider);
    });

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
            // The landing itself: the restored row, else the first, seated
            // once its container exists — or the notice of an emptied list —
            // through the one list-landing helper, never the bare list
            // (W7-7 PR 4, #1247, R-5).
            "SelectorFocus.FocusFirstOrSelectedItem(PanelCitationsList,CitationNotices)",
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

    private static CitationRowViewModel Knuth(WorkspaceViewModel workspace) =>
        Assert.Single(workspace.Citations.Rows, row => row.Reference.Citations.Any(citation => citation.Key == "knuth1984"));

    private static void SettleCitations(ShownShell host)
    {
        PumpedDispatcher.PumpUntilDrained(host.Workspace.Citations.WhenAllWorkDrained());
        host.Settle();
    }

    private static IRawElementProviderSimple SelectedProviderOf(
        ListBox list, CitationRowViewModel row, ListBoxItem container)
    {
        // A ListBox exposes logical ItemAutomationPeers, with its container
        // peers aggregated into them. Walk the actual list as a client does
        // before requesting Selection, so the setup does not depend on an
        // external client having initialized the container's EventsSource.
        var listPeer = Assert.IsType<ListBoxAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(list));
        ItemAutomationPeer itemPeer = Assert.Single(
            (listPeer.GetChildren() ?? []).OfType<ItemAutomationPeer>(),
            peer => ReferenceEquals(peer.Item, row));
        AutomationPeer containerPeer = UIElementAutomationPeer.CreatePeerForElement(container);
        Assert.Same(itemPeer, containerPeer.EventsSource);
        Assert.Same(row, list.SelectedItem);
        var selection = Assert.IsAssignableFrom<ISelectionProvider>(listPeer.GetPattern(PatternInterface.Selection));
        return Assert.IsAssignableFrom<IRawElementProviderSimple>(Assert.Single(selection.GetSelection()));
    }

    private static void AssertRetired(IRawElementProviderSimple provider)
    {
        ElementNotAvailableException retired = Assert.Throws<ElementNotAvailableException>(
            () => provider.GetPropertyValue(AutomationElement.HasKeyboardFocusProperty.Id));
        Assert.Equal(unchecked((int)0x80040201), retired.HResult);
    }

    private static bool PressPreview(UIElement target, Key key)
    {
        Assert.Equal(ModifierKeys.None, Keyboard.Modifiers);
        var press = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(target) ?? throw new InvalidOperationException("the key target is not hosted."),
            Environment.TickCount,
            key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(press);
        return press.Handled;
    }

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(60), "The citation republish focus fixture timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
