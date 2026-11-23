// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-22b: a name a user supplies, with an underscore in it, reaches no
/// presenter that reads the underscore as an access-key marker. On <c>main</c> at
/// 38f6edde two places did: the note header's Folders menu (a folder <c>my_notes</c>
/// painted "mynotes", named "mynotes" and carried the key N) and the embed cards'
/// headers (Fluent's Expander header: "Embedded note: my_notes.md" painted without its
/// underscore, and N registered on the window's source while the Embeds leaf showed,
/// which took Alt+N from the Canvas menu). The card's Jump button lost the underscore
/// from its name the same way (its peer strips a string content's marker from an
/// explicit name too), and an underscore title did not wrap (an <c>AccessText</c> drew
/// it, which the DS-2 wrap missed).
/// </summary>
/// <remarks>
/// Each fact has a part every desktop runs: the glyphs the header paints, read from
/// its drawing (an <c>AccessText</c>'s own TextBlock keeps the raw string as its one
/// run while it paints the name without the underscore, so its text proves nothing),
/// the peers' names and access keys, and the registrations in the menu's scope and on
/// the window's source. Not the global query: with another window active it finds
/// nothing on <c>main</c> too. The key-press part has a premise (the keys on a row of
/// the open menu; the shell the keyboard's active source) and skips where the desktop
/// refuses it (#1680).
/// </remarks>
public sealed class DataHeaderAccessKeyTests
{
    private static readonly string[] CardTargets = ["my_notes.md", "more_notes.md", "due_date.md", "a_b_c.md"];

    [Theory]
    [InlineData("my_notes")]
    [InlineData("due_date")]
    [InlineData("a_b_c")]
    public void AFolderWithAnUnderscoreShowsWholeInTheFoldersMenu(string folder) => StaThread.RunPumped(() =>
    {
        string path = folder + "/Note.md";
        using var host = new ShownShell((path, "# Note\n"));
        (ContextMenu menu, MenuItem[] rows) = OpenTheFoldersMenu(host, path);
        MenuItem row = Assert.Single(rows);
        Assert.True(
            AutomationProperties.GetName(row) == folder,
            $"premise: the row's composed name is {Q(AutomationProperties.GetName(row))}, not the folder's {Q(folder)}");

        var misreads = new List<string>(RowMisreads(row, folder));
        if (Registered(menu) != "none")
        {
            misreads.Add($"the menu's scope registers {Registered(menu)}; a folder's name holds no key");
        }

        if (Registered(WindowSource(host)) != "none")
        {
            misreads.Add($"the window's source registers {Registered(WindowSource(host))} with the menu open");
        }

        Assert.True(misreads.Count == 0, $"the Folders menu for {Q(folder)}:\n" + string.Join("\n", misreads));

        // The keys on a row of the open menu, else the desktop refused the menu (#1680).
        TakeTheKeysOnto(host, menu, rows);
        MenuItem before = (MenuItem)Keyboard.FocusedElement;
        var clicks = new List<string>();
        row.Click += (_, _) => clicks.Add(folder);
        char letter = folder[folder.IndexOf('_', StringComparison.Ordinal) + 1];
        Type(letter);
        host.Settle();

        Assert.True(clicks.Count == 0, $"'{letter}' typed in the open Folders menu clicked {string.Join(", ", clicks)}");
        Assert.True(menu.IsOpen, $"'{letter}' typed in the open Folders menu closed it");
        Assert.True(
            ReferenceEquals(Keyboard.FocusedElement, before),
            $"'{letter}' typed in the open Folders menu moved the keys from {Describe(before)} to {Describe(Keyboard.FocusedElement)}");
        menu.IsOpen = false;
        host.Settle();
    });

    [Fact]
    public void TwoFoldersThatGiveOneLetterTakeNoKeys() => StaThread.RunPumped(() =>
    {
        const string path = "more_notes/my_notes/Other.md";
        using var host = new ShownShell((path, "# Other\n"));
        (ContextMenu menu, MenuItem[] rows) = OpenTheFoldersMenu(host, path);
        Assert.Equal(["more_notes", "my_notes"], rows.Select(row => AutomationProperties.GetName(row)));
        var misreads = new List<string>(rows.SelectMany(row => RowMisreads(row, AutomationProperties.GetName(row))));
        if (AccessKeyManager.IsKeyRegistered(menu, "N"))
        {
            misreads.Add($"the menu's scope registers N for two folders (all it registers: {Registered(menu)})");
        }

        Assert.True(misreads.Count == 0, "the Folders menu for more_notes/my_notes:\n" + string.Join("\n", misreads));

        TakeTheKeysOnto(host, menu, rows);
        MenuItem before = (MenuItem)Keyboard.FocusedElement;
        var clicks = new List<string>();
        foreach (MenuItem row in rows)
        {
            string name = AutomationProperties.GetName(row);
            row.Click += (_, _) => clicks.Add(name);
        }

        Type('n');
        host.Settle();

        Assert.True(clicks.Count == 0, $"N typed in the open Folders menu clicked {string.Join(", ", clicks)}");
        Assert.True(menu.IsOpen, "N typed in the open Folders menu closed it");
        Assert.True(
            ReferenceEquals(Keyboard.FocusedElement, before),
            $"N typed in the open Folders menu moved the keys from {Describe(before)} to {Describe(Keyboard.FocusedElement)}");
        menu.IsOpen = false;
        host.Settle();
    });

    [Fact]
    public void AnEmbedTitleWithAnUnderscoreShowsWholeAndTakesNoWindowKey() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(
            ("host.md", "# Host\n\n![[my_notes]]\n\n![[more_notes]]\n\n![[due_date]]\n\n![[a_b_c]]\n"),
            ("my_notes.md", "# My notes\n\nBody one.\n"),
            ("more_notes.md", "# More notes\n\nBody two.\n"),
            ("due_date.md", "# Due date\n\nBody three.\n"),
            ("a_b_c.md", "# ABC\n\nBody four.\n"));
        host.Workspace.OpenPath("host.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        host.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "embeds");
        host.Workspace.IsRightPaneVisible = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => host.Workspace.Panels.Embeds.Count == CardTargets.Length && !host.Workspace.Panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            $"premise: the Embeds leaf resolved {host.Workspace.Panels.Embeds.Count} of {CardTargets.Length} embeds");
        host.Settle();

        var misreads = new List<string>();
        foreach (string target in CardTargets)
        {
            string title = SlateUniffiMethods.ResolvedEmbedTitle(new ResolvedEmbed.Note(target));
            Expander? card = null;
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () =>
                    {
                        card = ShownShell.Descendants(host.Shell).OfType<Expander>()
                            .FirstOrDefault(expander => expander.IsVisible && AutomationProperties.GetName(expander) == title);
                        return card is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                $"premise: no card named {Q(title)} is shown");
            ToggleButton header = HeaderButtonOf(card!);
            string painted = Painted(header);
            if (Squeezed(painted) != Squeezed(title))
            {
                misreads.Add($"{target}: the card's header paints {Q(painted)}, not {Q(title)}");
            }

            AutomationPeer peer = PeerOf(header);
            if (peer.GetName() != title || peer.GetAccessKey().Length > 0)
            {
                misreads.Add($"{target}: the header button's peer is named {Q(peer.GetName())} with key {Q(peer.GetAccessKey())}, not {Q(title)} with none");
            }

            string jumpName = "Jump to source: " + target;
            Button? jump = ShownShell.Descendants(card!).OfType<Button>()
                .FirstOrDefault(button => button.IsVisible && AutomationProperties.GetName(button) == jumpName);
            if (jump is null)
            {
                misreads.Add($"{target}: the card shows no Jump button named {Q(jumpName)}");
            }
            else if (PeerOf(jump).GetName() != jumpName)
            {
                misreads.Add($"{target}: the Jump button's peer is named {Q(PeerOf(jump).GetName())}, not {Q(jumpName)}");
            }

            if (ShownShell.Descendants(card!).OfType<AccessText>().FirstOrDefault() is { } access)
            {
                misreads.Add($"{target}: the card draws {Q(access.Text)} through an AccessText (key {Q(access.AccessKey.ToString())})");
            }
        }

        PresentationSource source = WindowSource(host);
        string[] taken = [.. new[] { "B", "D", "N" }.Where(letter => AccessKeyManager.IsKeyRegistered(source, letter))];
        if (taken.Length > 0)
        {
            misreads.Add($"the window's source registers {string.Join(", ", taken)} while the Embeds leaf shows (all it registers: {Registered(source)})");
        }

        Assert.True(misreads.Count == 0, string.Join("\n", misreads));

        // The window's KeyDown decision reads the global query, which answers for the
        // keyboard's active source only (the spec red team, measured).
        if (!ReferenceEquals(Keyboard.PrimaryDevice.ActiveSource, source))
        {
            Assert.Skip("this desktop did not make the shell the keyboard's active source (#1680), so the app menu's "
                + "Alt-and-letter decision was not measured on it");
        }

        MenuItem appMenu = host.Shell.AppMenuItem;
        Assert.Equal("BaseMenu", AppMenuKeys.Decide(Key.B, false, () => !appMenu.IsSubmenuOpen && AccessKeyManager.IsKeyRegistered(null, "B")));
        Assert.Equal("CanvasMenu", AppMenuKeys.Decide(Key.N, false, () => !appMenu.IsSubmenuOpen && AccessKeyManager.IsKeyRegistered(null, "N")));
    });

    /// <summary>DS-2 for a title with an underscore: at 160 wide its header wraps, read as
    /// the lines its glyphs are painted on, as <c>NarrowWindowLayoutTests</c>' title does.</summary>
    [Fact]
    public void AnEmbedTitleWithAnUnderscoreWraps() => StaThread.RunPumped(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        const string title = "Embedded note: my_notes/a title long enough to need a second line.md";
        var view = new EditorEmbedPreviewView
        {
            Width = 160,
            Root = new EditorEmbedPreviewNode(title, [], null, null, IsDisclosure: true, InitiallyExpanded: false, IsWarning: false),
        };
        DockPanel.SetDock(view, Dock.Top);
        ((DockPanel)host.Shell.Content).Children.Insert(0, view);
        host.Settle();

        Expander expander = ShownShell.Descendants(view).OfType<Expander>().Single();
        ToggleButton header = HeaderButtonOf(expander);
        double[] lines = PaintedLines(header);
        Assert.True(
            lines.Length >= 2,
            $"the title paints on {lines.Length} line(s) in a {expander.ActualWidth:F0}px card, so it didn't wrap: {Q(Painted(header))}");
    });

    // ---- the Folders menu -----------------------------------------------------

    /// <summary>The compact header's Folders menu for <paramref name="path"/>, opened as the
    /// keyboard opens it (the button's Click without a pointer), its rows laid out.</summary>
    private static (ContextMenu Menu, MenuItem[] Rows) OpenTheFoldersMenu(ShownShell host, string path)
    {
        host.Workspace.OpenPath(path);
        host.Shell.Width = 500;
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        NoteHeaderView header = ShownShell.Descendants(host.Shell).OfType<NoteHeaderView>().First(view => view.IsVisible);
        Assert.True(header.IsCompact, "premise: the note header is not compact at 500 wide");
        Button folders = ShownShell.Descendants(header).OfType<Button>()
            .Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == "NoteHeaderFolders");
        ContextMenu menu = folders.ContextMenu!;
        MenuItem[] rows = [.. menu.Items.OfType<MenuItem>()];
        _ = folders.Focus();
        folders.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, folders));
        Assert.True(
            PumpedDispatcher.PumpUntil(() => menu.IsOpen && rows.All(row => row.IsLoaded && HeaderHostOf(row) is not null), TimeSpan.FromSeconds(10)),
            $"the Folders menu for {path} did not open with its rows laid out (open={menu.IsOpen})");
        host.Settle();
        return (menu, rows);
    }

    /// <summary>What the row for <paramref name="name"/> shows and says that is not the name
    /// whole with no key.</summary>
    private static IEnumerable<string> RowMisreads(MenuItem row, string name)
    {
        ContentPresenter presenter = HeaderHostOf(row)
            ?? throw new Xunit.Sdk.XunitException($"the row {Q(name)} has no HeaderHost presenter");
        string painted = Painted(presenter);
        AutomationPeer peer = PeerOf(row);
        if (painted != name)
        {
            yield return $"the row {Q(name)} paints {Q(painted)}";
        }

        if (peer.GetName() != name)
        {
            yield return $"the row {Q(name)}'s peer is named {Q(peer.GetName())}";
        }

        if (peer.GetAccessKey().Length > 0)
        {
            yield return $"the row {Q(name)}'s peer carries the access key {Q(peer.GetAccessKey())}";
        }

        if (ShownShell.Descendants(row).OfType<AccessText>().FirstOrDefault() is { } access)
        {
            yield return $"the row {Q(name)} draws through an AccessText (key {Q(access.AccessKey.ToString())})";
        }
    }

    /// <summary>The keys on a row of the open menu: where the open did not land them, onto
    /// the first row; a desktop that refuses that skips (#1680).</summary>
    private static void TakeTheKeysOnto(ShownShell host, ContextMenu menu, MenuItem[] rows)
    {
        if (!PumpedDispatcher.PumpUntil(() => HoldsTheKeys(rows), TimeSpan.FromSeconds(5)))
        {
            KeysFirstRecorder.TakeTheMenuKeys(rows[0]);
            host.Settle();
        }

        if (!menu.IsOpen || !HoldsTheKeys(rows))
        {
            _ = KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu(
                $"the keys are on {Describe(Keyboard.FocusedElement)}, not a row of the open Folders menu");
        }
    }

    private static bool HoldsTheKeys(MenuItem[] rows) => rows.Any(row => ReferenceEquals(row, Keyboard.FocusedElement));

    private static ContentPresenter? HeaderHostOf(MenuItem row) =>
        row.Template?.FindName("HeaderHost", row) as ContentPresenter;

    /// <summary>A letter typed where the keys are, as WPF turns a key's WM_CHAR into text
    /// input; the access-key manager reads that input.</summary>
    private static void Type(char letter)
    {
        IInputElement target = Keyboard.FocusedElement
            ?? throw new Xunit.Sdk.XunitException("nothing holds the keys to type into");
        _ = TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, target, letter.ToString()));
    }

    // ---- reading what is shown -------------------------------------------------

    private static ToggleButton HeaderButtonOf(Expander card) =>
        ShownShell.Descendants(card).OfType<ToggleButton>().FirstOrDefault(button => ReferenceEquals(button.TemplatedParent, card))
            ?? throw new Xunit.Sdk.XunitException($"the card {Q(AutomationProperties.GetName(card))} has no header button");

    private static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element)
            ?? throw new Xunit.Sdk.XunitException($"{element.GetType().Name} has no automation peer");

    private static PresentationSource WindowSource(ShownShell host) =>
        PresentationSource.FromVisual(host.Shell)
            ?? throw new Xunit.Sdk.XunitException("the shell has no presentation source");

    private static string Registered(object? scope)
    {
        var keys = new List<char>();
        for (char letter = 'A'; letter <= 'Z'; letter++)
        {
            if (AccessKeyManager.IsKeyRegistered(scope, letter.ToString()))
            {
                keys.Add(letter);
            }
        }

        return keys.Count == 0 ? "none" : new string([.. keys]);
    }

    /// <summary>The characters <paramref name="root"/> and its visible descendants paint, in
    /// tree order, from their drawings' glyph runs; icon glyphs (the private use area) are
    /// left out.</summary>
    private static string Painted(DependencyObject root) =>
        string.Concat(GlyphRunsOf(root).SelectMany(run => run.Run.Characters ?? []).Where(character => character is < '' or > ''));

    /// <summary>The distinct baselines, in <paramref name="root"/>'s space, of the glyph runs
    /// that paint text: one per painted line.</summary>
    private static double[] PaintedLines(Visual root) =>
        [.. GlyphRunsOf(root)
            .Where(run => run.Run.Characters?.Any(character => character is < '' or > '' && !char.IsWhiteSpace(character)) == true)
            .Select(run => Math.Round(run.Owner.TransformToAncestor(root).Transform(run.Run.BaselineOrigin).Y * 2) / 2)
            .Distinct()
            .Order()];

    private static IEnumerable<(Visual Owner, GlyphRun Run)> GlyphRunsOf(DependencyObject root)
    {
        foreach (Visual visual in ShownShell.Descendants(root).Prepend(root).OfType<Visual>())
        {
            if (visual is UIElement { IsVisible: false } || VisualTreeHelper.GetDrawing(visual) is not { } drawing)
            {
                continue;
            }

            foreach (GlyphRun run in GlyphRunsIn(drawing))
            {
                yield return (visual, run);
            }
        }
    }

    private static IEnumerable<GlyphRun> GlyphRunsIn(Drawing drawing) => drawing switch
    {
        DrawingGroup group => group.Children.SelectMany(GlyphRunsIn),
        GlyphRunDrawing { GlyphRun: { } run } => [run],
        _ => [],
    };

    private static string Squeezed(string text) => string.Concat(text.Where(character => !char.IsWhiteSpace(character)));

    private static string Describe(IInputElement? element) => element switch
    {
        null => "nothing",
        MenuItem item => $"the row {Q(AutomationProperties.GetName(item))}",
        FrameworkElement framework => $"{framework.GetType().Name} '{AutomationProperties.GetAutomationId(framework)}'",
        _ => element.GetType().Name,
    };

    private static string Q(string? text) => text is null ? "<null>" : "\"" + text + "\"";
}
