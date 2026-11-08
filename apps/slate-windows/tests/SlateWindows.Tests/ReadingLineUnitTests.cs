// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation.Text;
using System.Windows.Documents;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>
/// #1687: NVDA reads the reading view's line as the caret's range expanded to a Line through
/// the surface's Text pattern. That expansion held the caret's line and the next one, so each
/// arrow spoke two lines. A caret at the start of each line (a heading, a paragraph, a list
/// item, the last item before a heading) expands to that line only.
/// </summary>
public sealed class ReadingLineUnitTests
{
    private const string Note =
        "# Big list\n\nIntro line.\n\n- item 1\n- item 2\n- item 3\n\n## After the list\n\nClosing paragraph.\n\n1. First\n2. Second\n";

    [Fact]
    public void ACaretsLineIsThatLineOnly() => RunSta(() =>
    {
        using var host = new ShownShell(("Lines.md", Note));
        host.Workspace.OpenPath("Lines.md");
        host.Settle();
        host.Workspace.ToggleReadingModeCommand.Execute(null);
        ReadingSurface? surface = null;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () =>
                {
                    host.Settle();
                    surface = ShownShell.Descendants(host.Shell).OfType<ReadingSurface>().FirstOrDefault(s => s.IsVisible);
                    return surface is not null && Paragraphs(surface.Document).Any(p => Text(p) == "Closing paragraph.");
                },
                TimeSpan.FromSeconds(30)),
            "the reading view never showed the note");
        host.Settle();

        var provider = Assert.IsAssignableFrom<ITextProvider>(
            UIElementAutomationPeer.CreatePeerForElement(surface!).GetPattern(PatternInterface.Text));
        Assert.True(surface!.Focus(), "premise: the reading view refused the keys");
        host.Settle();

        // The issue's steps: Ctrl+End, then Up line by line, reading each line as NVDA does.
        EditingCommands.MoveToDocumentEnd.Execute(null, surface);
        host.Settle();
        var read = new List<string>();
        var wrong = new List<string>();
        for (int press = 0; press < 16; press++)
        {
            ITextRangeProvider line = provider.GetSelection()[0];
            line.ExpandToEnclosingUnit(TextUnit.Line);
            string spoken = line.GetText(-1).TrimEnd('\r', '\n');
            string shown = spoken.Replace("\r", "\\r").Replace("\n", "\\n");
            read.Add(shown);
            if (spoken.Contains('\r') || spoken.Contains('\n'))
            {
                wrong.Add($"Up {press}: \"{shown}\"");
            }

            EditingCommands.MoveUpByLine.Execute(null, surface);
            host.Settle();
        }

        Assert.True(wrong.Count == 0, "Lines read as more than one line:\n" + string.Join('\n', wrong) + "\nAll: " + string.Join(" | ", read));
    });

    private static IEnumerable<Paragraph> Paragraphs(FlowDocument document) =>
        document.Blocks.SelectMany(Paragraphs);

    private static IEnumerable<Paragraph> Paragraphs(Block block) => block switch
    {
        Paragraph paragraph => [paragraph],
        List list => list.ListItems.SelectMany(item => item.Blocks.SelectMany(Paragraphs)),
        Section section => section.Blocks.SelectMany(Paragraphs),
        _ => [],
    };

    private static string Text(Paragraph paragraph) =>
        new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
}
