// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1635 (CD19): the embeds leaf resolves and decodes every embed of the
/// active note on every activation, shown or not, so its batch is a
/// projection with a decode deadline of its own. The panels run inline here
/// (<c>synchronousForTests</c>), so this thread's decode seams reach the
/// batch. A small fixture of its own: <see cref="RightPanePanelsBudgetTests"/>
/// writes about 21 MiB of payloads per fact.
/// </summary>
public sealed class EmbedsLeafSvgDecodeDeadlineTests
{
    private const string TookLonger = "Image not shown: decoding took longer than 5 seconds.";

    private const string ProjectionSpent =
        "Image not shown: this note's images used their 20-second decoding budget.";

    [Fact]
    public void TheEmbedsLeafsImagesShareOneDecodeDeadline()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "embeds-leaf-svg-deadline");
        foreach ((string name, string fill) in new[] { ("a.svg", "navy"), ("b.svg", "maroon"), ("c.svg", "purple") })
        {
            File.WriteAllText(
                Path.Combine(fixture.Root, name),
                "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'>"
                + $"<rect width='8' height='8' fill='{fill}'/></svg>");
        }
        File.WriteAllText(
            Path.Combine(fixture.Root, "host.md"), "![[a.svg]]\n\n![[b.svg]]\n\n![[c.svg]]\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var slots = new EditorInteractionCoordinator.SvgDecodeSlots();
        var decodes = new ParkedSvgDecodes();
        EditorInteractionCoordinator.SvgDecodeSlotsForTests = slots;
        EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = 100;
        EditorInteractionCoordinator.SvgDecodeBodyForTests = decodes.Parked;
        EditorInteractionCoordinator.SvgDecodeWaitForTests = decodes.TimeoutOnceEntered;
        EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = 150;
        try
        {
            var panels = new RightPanePanelsViewModel(
                session,
                _ => { },
                (_, _) => true,
                _ => true,
                (_, _) => { },
                (_, _) => true,
                (_, _) => { },
                synchronousForTests: true);
            panels.NoteChanged("host.md");

            ParkedSvgDecodes.AssertSaid(
                panels.Embeds.Select(row => Assert.Single(row.Node.Parts).Text),
                TookLonger,
                ProjectionSpent,
                ProjectionSpent);
            Assert.Equal(2, decodes.Runs);
        }
        finally
        {
            EditorInteractionCoordinator.SvgProjectionDecodeDeadlineMillisecondsForTests = null;
            EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
            EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
            EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = null;
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
            decodes.Release();
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref slots.Live) == 0, TimeSpan.FromSeconds(10)),
                "the parked decodes never ended");
        }
    }
}
