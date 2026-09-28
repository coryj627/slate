// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1278 (contract 38 "core owns copy"): the PRIMARY witness that a resolved
// embed card's title is core's words where a reader meets it.
//
// EmbedPreviewTitleCensus reads the source, and a static reading cannot
// enumerate every way WPF composes text: codex round 3 put a second SetName
// after the renderer's validated one, and an implicit Expander style whose
// HeaderTemplate formats 'Preview: {0}'. So this witness does not read the
// source. It realizes the shipped surfaces and reads what a reader gets.
//
// Codex round 4 showed that the first form of this witness read a REPLICA:
// it reloaded WorkspaceTemplates.xaml into an unrelated window and lifted the
// embeds list out of its ancestors, so a style put into the LIVE window
// dictionary or on an ancestor at runtime passed it. And its rows never
// reached a corrupt image's warning or a nested section, block or image card.
// So:
//
// - Host: the REAL MainWindow, shown off-screen and never activated, with
//   its own live resource instances and its whole ancestor chain. Nothing is
//   reloaded or detached. The only substitutions are the ones a test must
//   make: the window-placement store points into the fixture (showing and
//   closing the shell must not read or write the user's), and the theme's
//   application layers — Application.Resources in the app, from ThemeManager's
//   own seam — are merged BENEATH the window's own dictionaries, so the
//   process-wide Application.Resources is left alone. Every theme.
// - Surfaces, inside that shell: the workspace's tab in source mode, where
//   Ctrl+E opens the popover through the real coordinator; the embeds leaf in
//   the right pane, fed by the workspace's own panels; the same tab in
//   reading mode. Data applies on the UI thread, as in the app.
// - Reads, after layout and data binding: the popover's UIA Name through its
//   peer, its heading's peer Name and rendered text; each card's peer Name,
//   the text its Expander header actually renders (walked in the visual
//   tree), its warning's text and name, its image's name, and every nested
//   card; the reading card's header text, nested headers and landmark.
// - Coverage is asserted, not assumed: each surface must actually meet a
//   corrupt image's warning and a nested note, section, block and image card.
//   A card counts as met only once it is VISIBLE and in the surface's live
//   UIA tree: nested cards are built collapsed, so each is first expanded
//   through its ExpandCollapse pattern, as a reader expands it, and layout
//   settles — whatever runs on expansion (an Expanded handler anywhere up the
//   tree, codex's confirmation pass) has run before the card is read.
//
// The expected side is computed at test time by core from the ResolvedEmbed
// each row declares as data; the popover header adds exactly the
// " — source line N" locator, N counted from the source. The fixture
// exercises every rule core applies (a bounded heading, an image named by its
// file name, a base named without its extension), so a host spelling that
// does not reimplement them reads differently; a host spelling of core's
// exact note words is the same text, and its provenance is the census's.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
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

    /// <summary>A heading past core's display bound: core's title ends it in
    /// an ellipsis, which a host spelling of the heading would not.</summary>
    private static readonly string LongHeading = new('h', 5000);

    /// <summary>One embed of the fixture note: its markdown, what it resolves
    /// to (data, never words), the nested cards it carries in order, and
    /// whether the popover and the leaf show it (a `.base` is a card of its
    /// own kind in the reading view only; contract C10).</summary>
    private sealed record Row(string Name, string Embed, ResolvedEmbed Resolved, ResolvedEmbed[] Nested, bool OnPopoverAndLeaf);

    /// <summary>What outer.md embeds: every card kind a note can nest.</summary>
    private static readonly ResolvedEmbed[] NestedKinds =
    [
        new ResolvedEmbed.Note("leaf.md"),
        new ResolvedEmbed.Section("target.md", "Destination"),
        new ResolvedEmbed.Block("target.md"),
        new ResolvedEmbed.Image("attachments/photo.png", null),
    ];

    private static readonly Row[] Rows =
    [
        new("note", "![[target]]", new ResolvedEmbed.Note("target.md"), [], true),
        new("section", "![[target#Destination]]", new ResolvedEmbed.Section("target.md", "Destination"), [], true),
        new("section-long", $"![[target#{LongHeading}]]", new ResolvedEmbed.Section("target.md", LongHeading), [], true),
        new("block", "![[target#^block-id]]", new ResolvedEmbed.Block("target.md"), [], true),
        new("image", "![[photo.png]]", new ResolvedEmbed.Image("attachments/photo.png", null), [], true),
        new("image-alt", "![[photo.png|A bar chart]]", new ResolvedEmbed.Image("attachments/photo.png", "A bar chart"), [], true),
        new("image-corrupt", "![[broken.png]]", new ResolvedEmbed.Image("broken.png", null), [], true),
        new("nested", "![[outer]]", new ResolvedEmbed.Note("outer.md"), NestedKinds, true),
        new("base", "![[Notes.base]]", new ResolvedEmbed.Base("Notes.base"), [], false),
    ];

    /// <summary>The branches each surface must actually meet.</summary>
    private static readonly string[] RequiredBranches =
    [
        .. new[] { "Ctrl+E popover", "embeds leaf" }.SelectMany(surface => new[]
        {
            $"{surface}: warning",
            $"{surface}: nested Note",
            $"{surface}: nested Section",
            $"{surface}: nested Block",
            $"{surface}: nested Image",
        }),
        "reading card: nested Note",
        "reading card: nested Section",
        "reading card: nested Block",
        "reading card: nested Image",
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
            var met = new HashSet<string>(StringComparer.Ordinal);
            using var fixture = FixtureVault.Create(0, "embed-title-realized");
            string source = WriteFixture(fixture.Root);
            using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
            using (var cancel = new CancelToken())
            {
                session.ScanInitial(cancel);
            }

            // As in the app: the panels and the coordinator apply on the UI
            // thread, so the realized list and popover see every change there.
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var announcements = new List<A11yEvent>();
            using (var shell = new RealShell(session, fixture.Root, slateTheme, highContrast, announcements.Add))
            {
                ReadPopovers(theme, shell, announcements, source, misreads, read, met);
                ReadLeaf(theme, shell, misreads, read, met);
                ReadReadingCards(theme, shell, misreads, read, met);
            }

            foreach (string branch in RequiredBranches.Where(branch => !met.Contains(branch)))
            {
                misreads.Add($"{theme} · {branch} was never encountered, so it was never read");
            }
            Assert.True(
                misreads.Count == 0,
                $"{misreads.Count} realized surface(s) do not read as core's title:\n"
                + string.Join("\n", misreads)
                + "\n\nEvery surface read:\n" + string.Join("\n", read));
        });

    // ---- the real shell -------------------------------------------------------

    /// <summary>The real MainWindow over a real workspace, shown off-screen and
    /// never activated. Its resource instances and ancestors are its own; the
    /// placement store points into the fixture, and the theme's application
    /// layers sit beneath the window's own dictionaries.</summary>
    private sealed class RealShell : IDisposable
    {
        private readonly VaultLifecycleViewModel _lifecycle;

        internal RealShell(
            VaultSession session, string root, SlateTheme theme, bool highContrast, Action<A11yEvent> announce)
        {
            // Registers the pack: scheme and its application authority without
            // constructing an Application (TextBoxAccessibilityTests explains).
            RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            Window = new MainWindow();
            FieldInfo placement = typeof(MainWindow).GetField("_windowPlacement", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new Xunit.Sdk.XunitException("MainWindow no longer holds its placement in _windowPlacement; "
                    + "the witness must not show the shell against the user's placement store.");
            placement.SetValue(Window, new WindowPlacementManager(Window, new WindowStateStore(Path.Combine(root, "window-state.json"))));
            ResourceDictionary[] layers = ThemeManager.DictionariesFor(theme, highContrast);
            for (int index = 0; index < layers.Length; index++)
            {
                Window.Resources.MergedDictionaries.Insert(index, layers[index]);
            }
            Window.WindowStartupLocation = WindowStartupLocation.Manual;
            Window.Left = -20_000;
            Window.Top = -20_000;
            Window.ShowActivated = false;
            Window.ShowInTaskbar = false;

            _lifecycle = Assert.IsType<VaultLifecycleViewModel>(Window.DataContext);
            Workspace = new WorkspaceViewModel(
                session,
                root,
                () => [],
                announce,
                startInteractionBackgroundWork: false,
                preferencesStore: new AppPreferencesStore(Path.Combine(root, "preferences.json")));
            SetProperty(_lifecycle, nameof(VaultLifecycleViewModel.Workspace), Workspace);
            // The shell shows its workspace view for an open vault. Opening one
            // through the lifecycle would write the user's recents, so only the
            // state the view binds to is set, as the Workspace above is.
            SetProperty(_lifecycle, nameof(VaultLifecycleViewModel.IsVaultOpen), true);
            Window.Show();
            Workspace.OpenPath("source.md");
            WaitForUi(() => Workspace.ActiveGroup.ActiveTab is { Path: "source.md" });
            Tab = Workspace.ActiveGroup.ActiveTab!;
            Settle(Window);
        }

        internal MainWindow Window { get; }

        internal WorkspaceViewModel Workspace { get; }

        internal WorkspaceTabViewModel Tab { get; }

        public void Dispose()
        {
            var failures = new List<Exception>();
            foreach (Action step in new Action[]
            {
                () => Workspace.Panels.Shutdown(),
                () => SetProperty(_lifecycle, nameof(VaultLifecycleViewModel.IsVaultOpen), false),
                () => SetProperty(_lifecycle, nameof(VaultLifecycleViewModel.Workspace), null),
                () => Window.Close(),
                () => Workspace.Dispose(),
            })
            {
                try
                {
                    step();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
            if (failures.Count > 0)
            {
                throw new AggregateException(failures);
            }
        }

        private static void SetProperty(object target, string name, object? value) =>
            (target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Missing property: {name}"))
            .SetValue(target, value);
    }

    // ---- the Ctrl+E popover -------------------------------------------------

    private static void ReadPopovers(
        string where,
        RealShell shell,
        List<A11yEvent> announcements,
        string source,
        List<string> misreads,
        List<string> read,
        HashSet<string> met)
    {
        EditorInteractionCoordinator interactions = shell.Tab.EditorInteractions!;
        interactions.RefreshMathRangesForTests();
        interactions.RefreshArtifactCacheForTests();

        foreach (Row row in Rows.Where(row => row.OnPopoverAndLeaf))
        {
            string at = $"{where} · {row.Name} · Ctrl+E popover";
            announcements.Clear();
            int offset = source.IndexOf(row.Embed, StringComparison.Ordinal);
            Assert.True(offset >= 0, $"the fixture lost {row.Embed}");
            Assert.True(interactions.PreviewEmbedAt(offset + 2), $"{at}: Ctrl+E opened nothing");
            WaitForUi(() => announcements.OfType<A11yEvent.EmbedPreviewShown>().Any()
                || announcements.OfType<A11yEvent.EmbedPreviewUnavailable>().Any());
            Settle(shell.Window);

            if (announcements.OfType<A11yEvent.EmbedPreviewShown>().SingleOrDefault() is not { } shown)
            {
                misreads.Add($"{at}: announced {string.Join(", ", announcements)}, not one EmbedPreviewShown");
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

            UIElement[] popovers = VisualDescendants(shell.Window).OfType<UIElement>()
                .Where(element => AutomationProperties.GetAutomationId(element) == "EditorInteractionPopover")
                .ToArray();
            if (popovers.Count(element => element.IsVisible) != 1)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{at}: {popovers.Length} popover(s) in the shell's visual tree; visible: "
                    + string.Join(" | ", popovers.Select(Chain)) + $"; open={interactions.IsPopoverOpen}; "
                    + $"shell visible={shell.Window.IsVisible}");
            }
            UIElement popover = popovers.Single(element => element.IsVisible);
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
            ReadCard($"{at} card", "Ctrl+E popover", shell.Window, popover, view.Content as FrameworkElement,
                shown.Resolved, row.Nested, null, misreads, read, met);
            interactions.ClosePopoverCommand.Execute(null);
            Settle(shell.Window);
        }
    }

    // ---- the embeds leaf ----------------------------------------------------

    /// <summary>The embeds leaf where the app shows it: the shell's right pane,
    /// over the workspace's own panels.</summary>
    private static void ReadLeaf(
        string where, RealShell shell, List<string> misreads, List<string> read, HashSet<string> met)
    {
        shell.Workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "embeds");
        shell.Workspace.IsRightPaneVisible = true;
        WaitForUi(() => shell.Workspace.Panels.Embeds.Count == Rows.Length && !shell.Workspace.Panels.IsResolvingEmbeds);
        Settle(shell.Window);

        ItemsControl leaf = Assert.Single(
            VisualDescendants(shell.Window).OfType<ItemsControl>(),
            list => list.IsVisible && AutomationProperties.GetAutomationId(list) == "PanelEmbedsList");
        for (int index = 0; index < Rows.Length; index++)
        {
            Row row = Rows[index];
            if (!row.OnPopoverAndLeaf)
            {
                continue;
            }
            string at = $"{where} · {row.Name} · embeds leaf";
            if (!Resolves(shell.Workspace.Panels.Embeds[index].Resolution, row.Resolved))
            {
                misreads.Add($"{at}: the leaf resolved {shell.Workspace.Panels.Embeds[index].Resolution}, not {row.Resolved}");
            }
            if (leaf.ItemContainerGenerator.ContainerFromIndex(index) is not DependencyObject container)
            {
                misreads.Add($"{at}: the row was never realized");
                continue;
            }
            EditorEmbedPreviewView view = Assert.Single(VisualDescendants(container).OfType<EditorEmbedPreviewView>());
            ReadCard($"{at} card", "embeds leaf", shell.Window, leaf, view.Content as FrameworkElement,
                row.Resolved, row.Nested, null, misreads, read, met);
        }
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

    /// <summary>A card as a reader meets it. A collapsed card — every nested
    /// card is built collapsed — is first expanded the way a keyboard or
    /// screen-reader user expands it, through its ExpandCollapse pattern, and
    /// layout settles, so whatever expanding runs (an Expanded handler
    /// anywhere up the tree included) has run. Only a card that is then
    /// VISIBLE and present in the surface's live UIA tree is read and counted
    /// as met: its peer Name and the text its header renders; in its body a
    /// warning's text and name and an image's name — each likewise visible and
    /// in the UIA tree — and each nested card in order, each core's title of
    /// what it resolved to. The body's text and Jump button are not titles;
    /// anything else there is read as if it were one.</summary>
    private static void ReadCard(
        string at,
        string surface,
        Window window,
        UIElement surfaceRoot,
        FrameworkElement? card,
        ResolvedEmbed resolved,
        ResolvedEmbed[] nested,
        string? branch,
        List<string> misreads,
        List<string> read,
        HashSet<string> met)
    {
        if (card is null)
        {
            misreads.Add($"{at}: no card was realized");
            return;
        }
        if (card is not Expander expander)
        {
            misreads.Add($"{at}: the card is a {card.GetType().Name}, not a disclosure");
            return;
        }
        if (!expander.IsExpanded)
        {
            if (UIElementAutomationPeer.CreatePeerForElement(expander)?.GetPattern(PatternInterface.ExpandCollapse)
                is not IExpandCollapseProvider disclosure)
            {
                misreads.Add($"{at}: the collapsed card offers no ExpandCollapse pattern, so no reader can open it");
                return;
            }
            disclosure.Expand();
            Settle(window);
        }
        HashSet<UIElement> inUia = UiaOwners(surfaceRoot);
        if (!Realized(expander, inUia))
        {
            misreads.Add($"{at}: the card is not visible in the realized visual and UIA trees ({Chain(expander)}), "
                + "so it was never read");
            return;
        }
        if (branch is not null)
        {
            met.Add($"{surface}: {branch}");
        }

        string title = SlateUniffiMethods.ResolvedEmbedTitle(resolved);
        Expect(at, "UIA Name", PeerName(card), title, misreads, read);
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
                case Image image when !Realized(image, inUia):
                    misreads.Add($"{at}: the card's image is not visible in the realized visual and UIA trees");
                    break;
                case Image image:
                    Expect(at, "image UIA Name", PeerName(image), title, misreads, read);
                    break;
                case TextBlock warning when !Realized(warning, inUia):
                    misreads.Add($"{at}: the card's warning is not visible in the realized visual and UIA trees");
                    break;
                case TextBlock warning:
                    met.Add($"{surface}: warning");
                    Expect(at, "warning text", RenderedText(warning, exclude: null), title, misreads, read);
                    Expect(at, "warning UIA Name", PeerName(warning), title, misreads, read);
                    break;
                case Expander inner when nestedCards < nested.Length:
                    ResolvedEmbed child0 = nested[nestedCards++];
                    string kind = $"nested {child0.GetType().Name}";
                    ReadCard($"{at} › {kind}", surface, window, surfaceRoot, inner, child0, [], kind, misreads, read, met);
                    break;
                case FrameworkElement other:
                    misreads.Add($"{at}: the card body holds a {other.GetType().Name} reading "
                        + $"\"{RenderedText(other, exclude: null)}\" / named \"{PeerName(other)}\"");
                    break;
            }
        }
        if (nestedCards != nested.Length)
        {
            misreads.Add($"{at}: {nestedCards} nested card(s), expected {nested.Length}");
        }
    }

    /// <summary>Visible in the realized visual tree and present in the live
    /// UIA tree a reader walks.</summary>
    private static bool Realized(UIElement element, HashSet<UIElement> inUia) =>
        element.IsVisible && inUia.Contains(element);

    /// <summary>The elements whose automation peers a UIA client reaches from
    /// the surface's root, read fresh (each peer's child cache reset).</summary>
    private static HashSet<UIElement> UiaOwners(UIElement root)
    {
        var owners = new HashSet<UIElement>();
        var pending = new Stack<AutomationPeer>();
        if (UIElementAutomationPeer.CreatePeerForElement(root) is { } top)
        {
            pending.Push(top);
        }
        while (pending.Count > 0)
        {
            AutomationPeer peer = pending.Pop();
            if (peer is UIElementAutomationPeer { Owner: var owner })
            {
                owners.Add(owner);
            }
            peer.ResetChildrenCache();
            foreach (AutomationPeer child in peer.GetChildren() ?? [])
            {
                pending.Push(child);
            }
        }
        return owners;
    }

    // ---- the reading view -----------------------------------------------------

    private static void ReadReadingCards(
        string where, RealShell shell, List<string> misreads, List<string> read, HashSet<string> met)
    {
        shell.Tab.ToggleViewMode();
        Settle(shell.Window);
        ReadingSurface surface = Assert.Single(
            VisualDescendants(shell.Window).OfType<ReadingSurface>(), candidate => candidate.IsVisible);

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
            if (nestedHeaders.Length != row.Nested.Length)
            {
                misreads.Add($"{at}: {nestedHeaders.Length} nested header(s), expected {row.Nested.Length}");
                continue;
            }
            for (int child = 0; child < nestedHeaders.Length; child++)
            {
                met.Add($"reading card: nested {row.Nested[child].GetType().Name}");
                Expect(at, $"nested {row.Nested[child].GetType().Name} header text",
                    HeardHeader(nestedHeaders[child], at, misreads),
                    SlateUniffiMethods.ResolvedEmbedTitle(row.Nested[child]), misreads, read);
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

    /// <summary>Diagnostic: an element's visibility and each ancestor's that hides it.</summary>
    private static string Chain(DependencyObject element)
    {
        var hidden = new List<string>();
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement { Visibility: not Visibility.Visible } ui)
            {
                hidden.Add($"{ui.GetType().Name}({AutomationProperties.GetAutomationId(ui)}):{ui.Visibility}");
            }
        }
        return $"IsVisible={((UIElement)element).IsVisible} hidden-by=[{string.Join(", ", hidden)}]";
    }

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
        File.WriteAllText(
            Path.Combine(root, "outer.md"),
            "Outer body.\n\n![[leaf]]\n\n![[target#Destination]]\n\n![[target#^block-id]]\n\n![[photo.png]]\n");
        File.WriteAllText(Path.Combine(root, "leaf.md"), "Leaf body.\n");
        Directory.CreateDirectory(Path.Combine(root, "attachments"));
        File.WriteAllBytes(Path.Combine(root, "attachments", "photo.png"), TinyPng);
        // A PNG name over bytes no decoder accepts: an Image resolution whose
        // card carries the decode warning.
        File.WriteAllBytes(Path.Combine(root, "broken.png"), new byte[16]);
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
        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "A realized surface never settled.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(180), "The realized-surface witness timed out.");
}
