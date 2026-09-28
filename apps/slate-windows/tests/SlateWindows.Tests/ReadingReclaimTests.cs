// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 4b round 3 (codex r2 F5; R-5 (h)): a re-projection replaces every
/// block of the reading view's document (<c>ReadingSurface.ApplyBuiltDocument</c>:
/// <c>Document.Blocks.Clear()</c>), taking with it whatever in the document
/// holds the keys — a link, which takes them by Tab, or a task's check box.
/// The surface reclaims them for itself first, so they land on the document,
/// once, never on the window. The existing reading facts focus the surface
/// themselves before they assert; this one asserts where the keys land.
/// </summary>
public sealed class ReadingReclaimTests
{
    [Theory]
    [InlineData("link")]
    [InlineData("task")]
    public void AReprojectionUnderTheKeysLandsThemOnTheDocument(string holding) => RunSta(() =>
    {
        using var fixture = FixtureVault.Create(1, "reading-reclaim");
        File.WriteAllText(
            Path.Combine(fixture.Root, "note0.md"),
            "# Reclaim\n\nA line with [a link](https://example.com/) in it.\n\n- [ ] an open task\n");
        using var session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        using var tab = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
            announce: _ => { },
            startInteractionBackgroundWork: false);
        using var reading = new ReadingContentViewModel(session, tab, _ => { });
        var surface = new ReadingSurface { Model = reading };
        var window = new Window
        {
            Content = surface,
            Width = 900,
            Height = 700,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20_000,
            Top = -20_000,
        };
        try
        {
            window.Show();
            reading.Activate();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Holder(surface, holding) is not null, TimeSpan.FromSeconds(30)),
                $"premise: the reading view shows no {holding}");
            IInputElement held = Holder(surface, holding)!;
            Assert.True(held.Focus(), $"premise: the {holding} refused the keys");
            Assert.Same(held, Keyboard.FocusedElement);
            FlowDocument? before = reading.Document;
            var changes = new List<IInputElement>();
            Keyboard.AddGotKeyboardFocusHandler(window, (_, e) => changes.Add(e.NewFocus));

            // A preference change drops the projection's memo and projects
            // again: every block replaced.
            reading.InvalidateForPrefsChange();
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => !ReferenceEquals(reading.Document, before) && Holder(surface, holding) is { } fresh && !ReferenceEquals(fresh, held),
                    TimeSpan.FromSeconds(30)),
                "premise: the refresh never re-projected the note");
            PumpedDispatcher.Drain();

            Assert.Same(surface, Keyboard.FocusedElement);
            Assert.Equal([surface], changes);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>The surface re-bound to another tab's model under the keys
    /// (a group's tabs share one surface): the old document's blocks are
    /// cleared for a placeholder (<c>ClearForModelSwitch</c>), and the keys on
    /// its link land on the document at once, never left on a link out of any
    /// document until the other note's projection arrives.</summary>
    [Fact]
    public void ARebindUnderAFocusedLinkLandsTheKeysOnTheDocument() => RunSta(() =>
    {
        using var fixture = FixtureVault.Create(2, "reading-rebind");
        File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), "A line with [a link](https://example.com/) in it.\n");
        File.WriteAllText(Path.Combine(fixture.Root, "note1.md"), "The other note.\n");
        using var session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        using var first = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "note0.md")),
            announce: _ => { },
            startInteractionBackgroundWork: false);
        using var second = new WorkspaceTabViewModel(
            session,
            new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "note1.md")),
            announce: _ => { },
            startInteractionBackgroundWork: false);
        using var reading = new ReadingContentViewModel(session, first, _ => { });
        using var other = new ReadingContentViewModel(session, second, _ => { });
        var surface = new ReadingSurface { Model = reading };
        var window = new Window
        {
            Content = surface,
            Width = 900,
            Height = 700,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20_000,
            Top = -20_000,
        };
        try
        {
            window.Show();
            reading.Activate();
            Assert.True(
                PumpedDispatcher.PumpUntil(() => Holder(surface, "link") is not null, TimeSpan.FromSeconds(30)),
                "premise: the reading view shows no link");
            IInputElement link = Holder(surface, "link")!;
            Assert.True(link.Focus(), "premise: the link refused the keys");
            var changes = new List<IInputElement>();
            Keyboard.AddGotKeyboardFocusHandler(window, (_, e) => changes.Add(e.NewFocus));

            surface.Model = other;

            // At once: the other note projects later, off the dispatcher, and
            // a link taken out of its document raises no re-evaluation, so
            // nothing else would move the keys off it until then.
            Assert.Same(surface, Keyboard.FocusedElement);
            PumpedDispatcher.Drain();
            Assert.Same(surface, Keyboard.FocusedElement);
            Assert.Equal([surface], changes);
        }
        finally
        {
            window.Close();
        }
    });

    /// <summary>The document's first link, or its first task's check
    /// box.</summary>
    private static IInputElement? Holder(ReadingSurface surface, string holding) =>
        Elements(surface.Document).FirstOrDefault(element => holding == "link"
            ? element is Hyperlink
            : element is System.Windows.Controls.CheckBox) as IInputElement;

    private static IEnumerable<object> Elements(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            yield return child;
            if (child is DependencyObject nested)
            {
                foreach (object inner in Elements(nested))
                {
                    yield return inner;
                }
            }
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(90)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
