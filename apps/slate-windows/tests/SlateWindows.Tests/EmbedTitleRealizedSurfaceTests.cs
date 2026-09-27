// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1278 (contract 38 "core owns copy"), round 4: the PRIMARY witness that a
// resolved embed card's title is core's words where a reader meets it.
//
// EmbedPreviewTitleCensus reads the source, and a static reading cannot
// enumerate every way WPF composes text: codex round 3 put a second
// SetName after the renderer's validated one, and an implicit Expander
// style (its TargetType spelled through a namespace alias) whose
// HeaderTemplate formats 'Preview: {0}'. Neither is a title sink in the
// source; both change what a reader hears or sees. So this witness does not
// read the source at all. It builds the shipped surfaces, realizes them in a
// window under the app's real resources, and reads the values a reader gets:
//
// - Resources: the application layers ThemeManager merges for the theme
//   (Fluent, then Slate's tokens — the same dictionaries, from the same
//   seam) and the shell's window dictionary (MainWindow merges
//   WorkspaceTemplates.xaml and nothing else — checked, not assumed), in the
//   app's precedence, on the window's OWN resources so the process-wide
//   Application.Resources is left alone. Every theme the app can load.
// - Surfaces: the shipped tab template in source mode, where Ctrl+E opens
//   the popover through the real coordinator and the card renderer runs with
//   its InteractionSession; the shipped embeds-leaf list lifted out of a real
//   (never shown) MainWindow, fed by the production panels view model; and
//   the shipped tab template in reading mode, whose ReadingSurface builds the
//   reading cards.
// - Reads, after layout and data binding: the popover's UIA Name through its
//   automation peer, its heading's peer Name and rendered text; each card's
//   peer Name, the text its Expander header actually renders (walked in the
//   visual tree, so a HeaderTemplate, StringFormat or converter shows), its
//   image's and warning's name and text, and its nested card; the reading
//   card's header text (the range a reader's caret reads) and its landmark
//   (what ReadingNavLanded speaks).
//
// The expected side is computed here, at test time, by core: the row
// declares WHAT the embed resolves to as data (a ResolvedEmbed), and core's
// ResolvedEmbedTitle / A11yRender render it — never a title written in this
// file. The popover header adds exactly the " — source line N" locator,
// N counted from the source text. The row's data is also compared with what
// the surface was built from (the announced event, the leaf's resolution),
// so a wrong kind cannot pass as the right words.
//
// What a reading of the surface cannot see: a host that spells core's exact
// words. The fixture exercises every rule core applies (a bounded heading, an
// image named by its file name, a base named without its extension), so a
// host spelling that does not reimplement them reads differently; a note's
// or a block's title has no such rule, and its provenance is the static
// census's (EmbedPreviewTitleCensus names the split literal at its sink).

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Panels;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

[Trait("witness", "embed-title-realized")]
public sealed class EmbedTitleRealizedSurfaceTests
{
    private const string Locator = " — source line ";

    /// <summary>A 1×1 PNG, so the image card decodes and names its image.</summary>
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8"
        + "z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>One embed of the fixture note: its markdown, its authored
    /// target, what it resolves to (data, never words), the nested card it
    /// carries, and the surfaces it is a card on. A `.base` is a card of
    /// its own kind in the reading view (contract C10), so that row is read
    /// there.</summary>
    private sealed record Row(
        string Name,
        string Embed,
        string Target,
        ResolvedEmbed Resolved,
        ResolvedEmbed? Nested,
        bool OnPopoverAndLeaf);

    /// <summary>A heading past core's display bound: core's title ends it in
    /// an ellipsis, which a host spelling of the heading would not.</summary>
    private static readonly string LongHeading = new('h', 5000);

    /// <summary>The fixture exercises every rule core applies to a title —
    /// a heading bounded for display, an alias-less image named by its file
    /// name (it lives under attachments/), a base named without its
    /// extension — so a host spelling that does not reimplement them reads
    /// differently here. A note's or a block's title has no such rule: a host
    /// spelling of core's exact words is the same text, which no reading of
    /// the surface can tell apart; that case is EmbedPreviewTitleCensus's.</summary>
    private static readonly Row[] Rows =
    [
        new("note", "![[target]]", "target", new ResolvedEmbed.Note("target.md"), null, true),
        new("section", "![[target#Destination]]", "target",
            new ResolvedEmbed.Section("target.md", "Destination"), null, true),
        new("section-long", $"![[target#{LongHeading}]]", "target",
            new ResolvedEmbed.Section("target.md", LongHeading), null, true),
        new("block", "![[target#^block-id]]", "target", new ResolvedEmbed.Block("target.md"), null, true),
        new("image", "![[photo.png]]", "photo.png",
            new ResolvedEmbed.Image("attachments/photo.png", null), null, true),
        new("image-alt", "![[photo.png|A bar chart]]", "photo.png",
            new ResolvedEmbed.Image("attachments/photo.png", "A bar chart"), null, true),
        new("nested", "![[outer]]", "outer", new ResolvedEmbed.Note("outer.md"),
            new ResolvedEmbed.Note("leaf.md"), true),
        new("base", "![[Notes.base]]", "Notes.base", new ResolvedEmbed.Base("Notes.base"), null, false),
    ];

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("high-contrast")]
    public void EveryResolvedKindReadsAsCoresTitleOnEveryRealizedSurface(string theme) =>
        RunSta(() =>
        {
            (SlateTheme slateTheme, bool highContrast) = theme switch
            {
                "light" => (SlateTheme.Light, false),
                "dark" => (SlateTheme.Dark, false),
                "high-contrast" => (SlateTheme.Light, true),
                _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
            };
            var misreads = new List<string>();
            var read = new List<string>();
            using var fixture = FixtureVault.Create(0, "embed-title-realized");
            string source = WriteFixture(fixture.Root);
            using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
            using (var cancel = new CancelToken())
            {
                session.ScanInitial(cancel);
            }

            // The production panels resolve the leaf's rows before any
            // window exists: no UI context is captured, so each apply runs
            // where its compute finished and the drain below completes it.
            var panels = new RightPanePanelsViewModel(
                session, _ => { }, (_, _) => true, _ => true, (_, _) => { },
                (_, _) => true, (_, _) => { });
            panels.NoteChanged("source.md");
            panels.DrainForTests().GetAwaiter().GetResult();

            var shell = new MainWindow();
            Window? host = null;
            var announcements = new List<A11yEvent>();
            using var sourceTab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
                announce: announcements.Add,
                startInteractionBackgroundWork: false);
            using var readingTab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(Guid.NewGuid(), new WorkspaceItemState(WorkspaceItemKind.Markdown, "source.md")),
                startInteractionBackgroundWork: false);
            try
            {
                (host, DataTemplate tabTemplate) = Host(shell, slateTheme, highContrast);
                string where = theme;

                ReadPopovers(where, host, tabTemplate, sourceTab, announcements, source, misreads, read);
                ReadLeaf(where, host, shell, panels, misreads, read);
                ReadReadingCards(where, host, tabTemplate, readingTab, misreads, read);
            }
            finally
            {
                host?.Close();
                shell.Close();
                panels.Shutdown();
                panels.DrainForTests().GetAwaiter().GetResult();
            }

            Assert.True(
                misreads.Count == 0,
                $"{misreads.Count} realized surface(s) do not read as core's title:\n"
                + string.Join("\n", misreads)
                + "\n\nEvery surface read:\n" + string.Join("\n", read));
        });

    // ---- the host ------------------------------------------------------------

    /// <summary>An off-screen, never-activated window whose own resources are
    /// the app's: the theme's application layers, then the shell's window
    /// dictionary, so later layers win as the window's do over the app's.</summary>
    private static (Window Host, DataTemplate TabTemplate) Host(
        MainWindow shell, SlateTheme theme, bool highContrast)
    {
        // The shell's window resources are WorkspaceTemplates.xaml and
        // nothing else; if that changes, this host no longer stands for it.
        Assert.Empty(shell.Resources.Keys);
        ResourceDictionary windowLayer = Assert.Single(shell.Resources.MergedDictionaries);
        Assert.EndsWith("WorkspaceTemplates.xaml", windowLayer.Source?.OriginalString, StringComparison.Ordinal);

        var host = new Window
        {
            Width = 1000,
            Height = 1600,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20_000,
            Top = -20_000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
        };
        foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(theme, highContrast))
        {
            host.Resources.MergedDictionaries.Add(layer);
        }
        var templates = (ResourceDictionary)Application.LoadComponent(
            new Uri("/SlateWindows;component/WorkspaceTemplates.xaml", UriKind.Relative));
        host.Resources.MergedDictionaries.Add(templates);
        host.SetResourceReference(Control.BackgroundProperty, "Slate.WindowBackgroundBrush");
        host.SetResourceReference(Control.ForegroundProperty, "Slate.TextBrush");
        host.Show();
        return (host, (DataTemplate)templates["WorkspaceTabContentTemplate"]);
    }

    // ---- the Ctrl+E popover -------------------------------------------------

    private static void ReadPopovers(
        string where,
        Window host,
        DataTemplate tabTemplate,
        WorkspaceTabViewModel tab,
        List<A11yEvent> announcements,
        string source,
        List<string> misreads,
        List<string> read)
    {
        EditorInteractionCoordinator interactions = tab.EditorInteractions!;
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();
        var presenter = new ContentPresenter { Content = tab, ContentTemplate = tabTemplate };
        host.Content = presenter;
        Settle(host);

        foreach (Row row in Rows.Where(row => row.OnPopoverAndLeaf))
        {
            string at = $"{where} · {row.Name} · Ctrl+E popover";
            announcements.Clear();
            int offset = source.IndexOf(row.Embed, StringComparison.Ordinal);
            Assert.True(offset >= 0, $"the fixture lost {row.Embed}");
            Assert.True(interactions.PreviewEmbedAt(offset + 2), $"{at}: Ctrl+E opened nothing");
            WaitForUi(() => announcements.Count > 0);
            Settle(host);

            if (Assert.Single(announcements) is not A11yEvent.EmbedPreviewShown shown)
            {
                misreads.Add($"{at}: announced {announcements[0]}, not EmbedPreviewShown");
                continue;
            }
            if (!Equals(shown.Resolved, row.Resolved))
            {
                misreads.Add($"{at}: the event carries {shown.Resolved}, not {row.Resolved}");
            }

            // Core, now, from what the event carries.
            string title = SlateUniffiMethods.ResolvedEmbedTitle(shown.Resolved);
            string name = SlateUniffiMethods.A11yRender(shown).Text;
            int line = source[..offset].Count(character => character == '\n') + 1;

            UIElement popover = Assert.Single(
                VisualDescendants(host).OfType<UIElement>(),
                element => AutomationProperties.GetAutomationId(element) == "EditorInteractionPopover");
            Expect(at, "the popover's UIA Name", PeerName(popover), name, misreads, read);

            FrameworkElement[] headings = VisualDescendants(popover).OfType<FrameworkElement>()
                .Where(element => element.IsVisible
                    && AutomationProperties.GetHeadingLevel(element) != AutomationHeadingLevel.None)
                .ToArray();
            if (headings.Length != 1)
            {
                misreads.Add($"{at}: the popover shows {headings.Length} headings; its header is one");
            }
            else
            {
                string header = title + Locator + line;
                Expect(at, "the header's UIA Name", PeerName(headings[0]), header, misreads, read);
                Expect(at, "the header's rendered text", RenderedText(headings[0], exclude: null), header, misreads, read);
            }

            EditorEmbedPreviewView view = Assert.Single(VisualDescendants(popover).OfType<EditorEmbedPreviewView>());
            ReadCard($"{at} card", view.Content as FrameworkElement, shown.Resolved, row.Nested, misreads, read);
            interactions.ClosePopoverCommand.Execute(null);
            Settle(host);
        }
    }

    // ---- the embeds leaf ----------------------------------------------------

    /// <summary>The shipped leaf list, lifted out of a real (never shown)
    /// shell — the Move-To and palette fixtures' technique — and re-hosted
    /// over the production panels' rows.</summary>
    private static void ReadLeaf(
        string where,
        Window host,
        MainWindow shell,
        RightPanePanelsViewModel panels,
        List<string> misreads,
        List<string> read)
    {
        ItemsControl leaf = Assert.Single(
            LogicalDescendants(shell).OfType<ItemsControl>(),
            list => AutomationProperties.GetAutomationId(list) == "PanelEmbedsList");
        switch (leaf.Parent)
        {
            case ContentControl owner:
                owner.Content = null;
                break;
            case Panel owner:
                owner.Children.Remove(leaf);
                break;
            case Decorator owner:
                owner.Child = null;
                break;
            default:
                throw new Xunit.Sdk.XunitException($"the leaf list's parent is a {leaf.Parent?.GetType().Name}");
        }
        leaf.DataContext = new LeafContext(panels);
        host.Content = leaf;
        Settle(host);

        Assert.Equal(Rows.Length, panels.Embeds.Count);
        for (int index = 0; index < Rows.Length; index++)
        {
            Row row = Rows[index];
            if (!row.OnPopoverAndLeaf)
            {
                continue;
            }
            string at = $"{where} · {row.Name} · embeds leaf";
            EmbedRowViewModel embed = panels.Embeds[index];
            if (!Resolves(embed.Resolution, row.Resolved))
            {
                misreads.Add($"{at}: the leaf resolved {embed.Resolution}, not {row.Resolved}");
            }
            if (leaf.ItemContainerGenerator.ContainerFromIndex(index) is not DependencyObject container)
            {
                misreads.Add($"{at}: the row was never realized");
                continue;
            }
            EditorEmbedPreviewView view = Assert.Single(VisualDescendants(container).OfType<EditorEmbedPreviewView>());
            ReadCard($"{at} card", view.Content as FrameworkElement, row.Resolved, row.Nested, misreads, read);
        }
    }

    /// <summary>The leaf list's data context in the shell exposes the panels
    /// as <c>Panels</c>; this stands in for it so the shipped binding runs.</summary>
    private sealed class LeafContext(RightPanePanelsViewModel panels)
    {
        public RightPanePanelsViewModel Panels { get; } = panels;
    }

    private static bool Resolves(EmbedResolution resolution, ResolvedEmbed resolved) => (resolution, resolved) switch
    {
        (EmbedResolution.FullNote note, ResolvedEmbed.Note expected) => note.TargetPath == expected.TargetPath,
        (EmbedResolution.Section section, ResolvedEmbed.Section expected) =>
            section.TargetPath == expected.TargetPath && section.Heading == expected.Heading,
        (EmbedResolution.Block block, ResolvedEmbed.Block expected) => block.TargetPath == expected.TargetPath,
        (EmbedResolution.Image image, ResolvedEmbed.Image expected) =>
            image.TargetPath == expected.TargetPath && image.Alt == expected.Alt,
        _ => false,
    };

    // ---- a card, wherever it is shown ----------------------------------------

    /// <summary>A realized card: its peer Name and the text its header
    /// renders; in its body, a warning's text and name, an image's name and
    /// a nested card — each core's title of what it resolved to. The body's
    /// text and Jump button are not titles; anything else there is read as
    /// if it were one.</summary>
    private static void ReadCard(
        string at,
        FrameworkElement? card,
        ResolvedEmbed resolved,
        ResolvedEmbed? nested,
        List<string> misreads,
        List<string> read)
    {
        if (card is null)
        {
            misreads.Add($"{at}: no card was realized");
            return;
        }
        string title = SlateUniffiMethods.ResolvedEmbedTitle(resolved);
        Expect(at, "UIA Name", PeerName(card), title, misreads, read);
        if (card is not Expander expander)
        {
            misreads.Add($"{at}: the card is a {card.GetType().Name}, not a disclosure");
            return;
        }
        Expect(at, "rendered header", RenderedText(expander, exclude: expander.Content as DependencyObject), title, misreads, read);

        int nestedCards = 0;
        System.Collections.IEnumerable body =
            (System.Collections.IEnumerable?)(expander.Content as Panel)?.Children ?? Array.Empty<object>();
        foreach (object child in body)
        {
            switch (child)
            {
                case TextBox or Button:
                    break;
                case Image image:
                    Expect(at, "image UIA Name", PeerName(image), title, misreads, read);
                    break;
                case TextBlock warning:
                    Expect(at, "warning text", RenderedText(warning, exclude: null), title, misreads, read);
                    Expect(at, "warning UIA Name", PeerName(warning), title, misreads, read);
                    break;
                case Expander inner when nested is not null:
                    nestedCards++;
                    ReadCard($"{at} › nested", inner, nested, null, misreads, read);
                    break;
                case FrameworkElement other:
                    misreads.Add($"{at}: the card body holds a {other.GetType().Name} reading "
                        + $"\"{RenderedText(other, exclude: null)}\" / named \"{PeerName(other)}\"");
                    break;
            }
        }
        if (nestedCards != (nested is null ? 0 : 1))
        {
            misreads.Add($"{at}: {nestedCards} nested card(s), expected {(nested is null ? 0 : 1)}");
        }
    }

    // ---- the reading view -----------------------------------------------------

    private static void ReadReadingCards(
        string where,
        Window host,
        DataTemplate tabTemplate,
        WorkspaceTabViewModel tab,
        List<string> misreads,
        List<string> read)
    {
        tab.ToggleViewMode();
        host.Content = new ContentPresenter { Content = tab, ContentTemplate = tabTemplate };
        Settle(host);
        ReadingSurface surface = Assert.Single(
            VisualDescendants(host).OfType<ReadingSurface>(), candidate => candidate.IsVisible);

        Section[] cards = AllBlocks(surface.Document.Blocks)
            .OfType<Section>()
            .Where(ReadingSemantics.IsEmbedSection)
            .ToArray();
        ReadingLandmark[] landmarks = surface.LandmarksForTests
            .Where(landmark => landmark.Kind == ReadingLandmarkKind.Embed)
            .ToArray();
        Assert.Equal(Rows.Length, cards.Length);
        Assert.Equal(Rows.Length, landmarks.Length);

        for (int index = 0; index < Rows.Length; index++)
        {
            Row row = Rows[index];
            string at = $"{where} · {row.Name} · reading card";
            string title = SlateUniffiMethods.ResolvedEmbedTitle(row.Resolved);
            Paragraph[] headers = cards[index].Blocks.OfType<Paragraph>()
                .Where(paragraph => ReadingSemantics.EmbedHeaderKeyOf(paragraph) is not null)
                .ToArray();
            if (headers.Length == 0 || !ReferenceEquals(headers[0], cards[index].Blocks.FirstBlock))
            {
                misreads.Add($"{at}: the card does not open on its header");
                continue;
            }
            Expect(at, "header text", HeardHeader(headers[0], at, misreads), title, misreads, read);
            Expect(at, "landmark", landmarks[index].Text, title, misreads, read);

            Paragraph[] nestedHeaders = headers[1..];
            if (nestedHeaders.Length != (row.Nested is null ? 0 : 1))
            {
                misreads.Add($"{at}: {nestedHeaders.Length} nested header(s), expected {(row.Nested is null ? 0 : 1)}");
            }
            else if (row.Nested is { } nested)
            {
                Expect(at, "nested header text", HeardHeader(nestedHeaders[0], at, misreads),
                    SlateUniffiMethods.ResolvedEmbedTitle(nested), misreads, read);
            }
        }
    }

    /// <summary>What a reader's caret reads on a header line, up to its Jump
    /// link; nothing may follow the link.</summary>
    private static string HeardHeader(Paragraph header, string at, List<string> misreads)
    {
        Hyperlink? jump = header.Inlines.OfType<Hyperlink>().LastOrDefault();
        if (jump is null)
        {
            misreads.Add($"{at}: the header has no Jump link");
            return new TextRange(header.ContentStart, header.ContentEnd).Text;
        }
        string after = new TextRange(jump.ElementEnd, header.ContentEnd).Text;
        if (after.Trim().Length > 0)
        {
            misreads.Add($"{at}: the header says \"{after}\" after its Jump link");
        }
        return new TextRange(header.ContentStart, jump.ElementStart).Text.TrimEnd();
    }

    // ---- reading what is realized ----------------------------------------------

    private static void Expect(
        string at, string surface, string? actual, string expected, List<string> misreads, List<string> read)
    {
        read.Add($"{at} · {surface}: \"{Show(actual)}\"");
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            misreads.Add($"{at} · {surface} reads \"{Show(actual)}\"; core renders \"{Show(expected)}\"");
        }
    }

    /// <summary>A long value shown by its ends and its length, so a bounded
    /// title and an unbounded one differ visibly in a failure.</summary>
    private static string? Show(string? value) =>
        value is { Length: > 160 } ? $"{value[..80]}…[{value.Length} chars]…{value[^60..]}" : value;

    private static string? PeerName(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element)?.GetName();

    /// <summary>The text an element renders, in visual order: every visible
    /// TextBlock and AccessText under it, a subtree excluded. Icon glyphs
    /// (Private Use Area characters, e.g. an expander's chevron) are not
    /// words and are skipped.</summary>
    private static string RenderedText(DependencyObject root, DependencyObject? exclude)
    {
        var parts = new List<string>();
        void Walk(DependencyObject node)
        {
            if (ReferenceEquals(node, exclude) || node is UIElement { IsVisible: false })
            {
                return;
            }
            switch (node)
            {
                case TextBlock text:
                    parts.Add(text.Text);
                    return;
                case AccessText access:
                    parts.Add(access.Text);
                    return;
            }
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                Walk(VisualTreeHelper.GetChild(node, index));
            }
        }
        Walk(root);
        return string.Concat(parts.Where(part => !part.All(character =>
            char.IsWhiteSpace(character) || character is >= '' and <= '')));
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject nested in VisualDescendants(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<DependencyObject> LogicalDescendants(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject element)
            {
                yield return element;
                foreach (DependencyObject nested in LogicalDescendants(element))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<Block> AllBlocks(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            yield return block;
            if (block is Section section)
            {
                foreach (Block nested in AllBlocks(section.Blocks))
                {
                    yield return nested;
                }
            }
        }
    }

    // ---- the fixture and the dispatcher -------------------------------------------

    private static string WriteFixture(string root)
    {
        File.WriteAllText(
            Path.Combine(root, "target.md"),
            $"# Lead\n\n## Destination\n\nSection body.\n\n## {LongHeading}\n\nLong section body.\n\n"
            + "Block body ^block-id\n");
        File.WriteAllText(Path.Combine(root, "outer.md"), "Outer body.\n\n![[leaf]]\n");
        File.WriteAllText(Path.Combine(root, "leaf.md"), "Leaf body.\n");
        Directory.CreateDirectory(Path.Combine(root, "attachments"));
        File.WriteAllBytes(Path.Combine(root, "attachments", "photo.png"), TinyPng);
        File.WriteAllText(
            Path.Combine(root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
        string source = "# Source\n\n" + string.Join("\n\n", Rows.Select(row => row.Embed)) + "\n";
        File.WriteAllText(Path.Combine(root, "source.md"), source);
        return source;
    }

    /// <summary>Layout, data binding and the layout that causes, twice over.</summary>
    private static void Settle(Window host)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            host.UpdateLayout();
            host.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
        }
        host.UpdateLayout();
    }

    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The embed preview never landed.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "The realized-surface witness timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
