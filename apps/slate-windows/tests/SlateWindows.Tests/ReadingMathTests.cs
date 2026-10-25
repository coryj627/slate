// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation.Peers;
using SlateWindows.Reading;
using WpfMath.Parsers;
using WpfMath.Rendering;
using XamlMath;
using XamlMath.Exceptions;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W3-2 math foundations: the MathML-over-UIA convention layer and
/// the WPFMath rendering spike.
/// </summary>
public sealed class ReadingMathTests
{
    /// <summary>
    /// THE CONDITION THE OWNER CALL ATTACHED to accepting the WPF
    /// private-reflection dependency (gap G23): if dotnet/wpf ever
    /// changes the AutomationPeer property-table shape, this fails
    /// LOUDLY here and in CI instead of the convention layer rotting
    /// silently. InactiveReason names the exact break.
    /// </summary>
    [Fact]
    public void MathMlUiaPropertyBridgeInstalls()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(
                MathMlUiaProperty.IsActive,
                $"the MathML UIA convention layer is OFF: {MathMlUiaProperty.InactiveReason}");
            Assert.True(MathMlUiaProperty.PropertyIdForTests > 0);
        });
    }

    /// <summary>
    /// The bridge answers through any peer implementing
    /// <see cref="IMathMlUiaSource"/> — verified end to end through
    /// AutomationPeer's own internal property dispatch, the exact
    /// path a UIA client request takes inside WPF.
    /// </summary>
    [Fact]
    public void RegisteredPropertyResolvesThroughThePeerTable()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(MathMlUiaProperty.IsActive, MathMlUiaProperty.InactiveReason);

            var peer = new FakeMathPeer("<math><mi>x</mi></math>");
            System.Reflection.MethodInfo dispatch = typeof(AutomationPeer).GetMethod(
                "GetPropertyValue",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance,
                types: new[] { typeof(int) })!;
            object? value = dispatch.Invoke(
                peer, new object[] { MathMlUiaProperty.PropertyIdForTests });
            Assert.Equal("<math><mi>x</mi></math>", value);

            var empty = new FakeMathPeer(string.Empty);
            Assert.Null(dispatch.Invoke(
                empty, new object[] { MathMlUiaProperty.PropertyIdForTests }));
        });
    }

    /// <summary>
    /// WPFMath currency spike (w3_spec §W3-2 first task): v2.1.0
    /// parses and builds representative formulas under .NET 10, and
    /// signals unsupported input with TexException — the documented
    /// boundary the renderer degrades on.
    /// </summary>
    [Fact]
    public void WpfMathParsesRepresentativeFormulasAndSignalsGaps()
    {
        RunSta(() =>
        {
            TexFormulaParser parser = WpfTeXFormulaParser.Instance;

            // Representative supported set (docs/matrices.md).
            foreach (string supported in new[]
            {
                @"x^2 + \frac{a}{b}",
                @"\sum_{i=0}^{n} i^2",
                @"\sqrt{b^2 - 4ac}",
                @"\begin{pmatrix} a & b \\ c & d \end{pmatrix}",
                @"\text{speed} = \frac{d}{t}",
            })
            {
                TexFormula formula = parser.Parse(supported);
                TexEnvironment environment =
                    WpfTeXEnvironment.Create(TexStyle.Display, 20.0, "Arial");
                System.Windows.Media.Imaging.BitmapSource bitmap =
                    formula.RenderToBitmap(environment);
                Assert.True(
                    bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0,
                    $"'{supported}' rendered to an empty bitmap");
            }

            // The documented coverage boundary: unsupported environments
            // throw TexException — the signal the reading renderer
            // degrades on (monospace source, matrix-rowed).
            Assert.Throws<TexParseException>(() =>
                parser.Parse(@"\begin{bmatrix} 1 \\ 2 \end{bmatrix}"));
        });
    }

    /// <summary>
    /// W3-2 core rendering path over a REAL vault + MathCAT: a display
    /// fence matches its canonical artifact, renders into a focusable
    /// element whose Name is genuine MathCAT speech (not the empty
    /// fallback), carries the MathML for the convention property, and
    /// lands in the landmark index with the speech as landing text.
    /// </summary>
    [Fact]
    public void MathBlockRendersAFocusableElementSpeakingCanonicalSpeech()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-basic");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "# Math\n\n$$x^2 + 1$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingMathElement element = FindMathElements(surface.Document).Single();
            Assert.True(element.Focusable);
            string name = System.Windows.Automation.AutomationProperties.GetName(element);
            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.NotEqual("Math expression.", name);
            Assert.StartsWith("<math", element.MathMl.TrimStart());

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            Assert.Equal(name, landmark.Text);
        });
    }

    /// <summary>
    /// The documented WPFMath coverage boundary: line reading speaks the
    /// speech, the source is visible as the element's content and spoken
    /// on request (#1595, #1591, plan 45 XB-9), the block stays a nav
    /// stop — and the CORE artifacts stay retrievable (round 4: a
    /// host-only rendering failure must not hide valid braille or
    /// MathML, or lie "Braille not available." while core produced it).
    /// </summary>
    [Fact]
    public void UnrenderableMathSpeaksItsSpeechInRangeAndItsSourceOnRequest()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-gap");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "$$\\begin{bmatrix} 1 \\\\ 2 \\end{bmatrix}$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);

            ReadingMathElement element =
                Assert.Single(FindMathElements(surface.Document));
            Assert.Equal(
                element.Speech, RangeTextOf(MathParagraphOf(element)));
            Assert.DoesNotContain(
                "bmatrix",
                new System.Windows.Documents.TextRange(
                    surface.Document.ContentStart,
                    surface.Document.ContentEnd).Text,
                StringComparison.Ordinal);
            Assert.Contains(
                "bmatrix",
                Assert.IsType<System.Windows.Controls.TextBlock>(element.Content).Text,
                StringComparison.Ordinal);
            Assert.True(element.Focusable);
            Assert.StartsWith("<math", element.MathMl.TrimStart());
            Assert.NotEmpty(element.Braille);
            Assert.Equal(
                element.Braille,
                System.Windows.Automation.AutomationProperties
                    .GetItemStatus(element));

            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            Assert.Equal(
                element.Braille,
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);

            announced.Clear();
            Assert.True(surface.TrySpeakMathSourceAtCaret());
            Assert.Equal(
                @"Source: \begin{bmatrix} 1 \\ 2 \end{bmatrix}.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>
    /// #1595 (plan 45 XB-9, the spike's shape a1h): a drawn formula's
    /// text range, which line reading and caret re-entry read, is its
    /// MathCAT speech, never its LaTeX; the drawing stays the element's
    /// content.
    /// </summary>
    [Fact]
    public void LineReadingTextOfADrawnFormulaIsItsSpeech()
        => WithReadingSurface("reading-math-line-drawn", "$$\\nabla^2 f$$\n", (_, surface, _) =>
        {
            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            Assert.IsType<System.Windows.Shapes.Path>(element.Content);
            string speech = System.Windows.Automation.AutomationProperties.GetName(element);
            Assert.NotEqual("Math expression.", speech);
            string range = RangeTextOf(MathParagraphOf(element));
            Assert.Equal(speech, range);
            Assert.DoesNotContain("\\", range, StringComparison.Ordinal);
            Assert.DoesNotContain("$", range, StringComparison.Ordinal);
        });

    /// <summary>#1595: a WPFMath coverage gap reads as its speech too; the
    /// source is visible as the element's content, never in range.</summary>
    [Fact]
    public void LineReadingTextOfACoverageGapFormulaIsItsSpeech()
        => WithReadingSurface("reading-math-line-gap", "$$\\mathbb{R}$$\n", (_, surface, _) =>
        {
            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            Assert.Equal(
                "$$\\mathbb{R}$$",
                Assert.IsType<System.Windows.Controls.TextBlock>(element.Content).Text);
            Assert.DoesNotContain("Math expression", element.Speech, StringComparison.Ordinal);
            string range = RangeTextOf(MathParagraphOf(element));
            Assert.Equal(element.Speech, range);
            Assert.DoesNotContain("\\", range, StringComparison.Ordinal);
            Assert.DoesNotContain("$", range, StringComparison.Ordinal);
        });

    /// <summary>#1595: core's fallback (a source pulldown-latex cannot
    /// parse) reads as core's fallback speech, not as the fenced
    /// source.</summary>
    [Fact]
    public void LineReadingTextOfACoreFallbackIsItsSpeech()
        => WithReadingSurface("reading-math-line-fallback", "$${n \\choose k}$$\n", (_, surface, _) =>
        {
            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            Assert.StartsWith("Math expression: ", element.Speech, StringComparison.Ordinal);
            Assert.IsType<System.Windows.Controls.TextBlock>(element.Content);
            string range = RangeTextOf(MathParagraphOf(element));
            Assert.Equal(element.Speech, range);
            Assert.DoesNotContain("$", range, StringComparison.Ordinal);
        });

    /// <summary>#1595: a block with no core artifact has no speech, so its
    /// source stays in range, and it still carries an element, so Tab and
    /// the source chord reach it.</summary>
    [Fact]
    public void AnUnmatchedMathBlockKeepsItsSourceInRangeAndCarriesAnElement()
        => WithReadingSurface("reading-math-unmatched", "$$x^2 + 1$$\n", (tab, surface, announced) =>
        {
            tab.EditorDocument!.Replace(
                tab.Text.IndexOf("x^2 + 1", StringComparison.Ordinal), 7, "y^3 - 2");
            tab.Reading!.Refresh();

            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            Assert.Null(element.Content);
            Assert.Equal("$$y^3 - 2$$", element.Speech);
            Assert.Equal("y^3 - 2", element.Source);
            Assert.Equal("$$y^3 - 2$$", RangeTextOf(MathParagraphOf(element)));

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TrySpeakMathSourceAtCaret());
            Assert.Equal(
                "Source: y^3 - 2.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });

    /// <summary>#1591 (plan 45 XB-9): Tab onto a math element speaks its
    /// speech and role, never the source: no HelpText, and the peer does
    /// not fall back to the ToolTip, which keeps the source for sighted
    /// hover.</summary>
    [Fact]
    public void TabFocusOnAMathElementCarriesNoHelpText()
        => WithReadingSurface("reading-math-tab", "$$\\nabla^2 f$$\n", (_, surface, _) =>
        {
            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            Assert.Equal(
                string.Empty,
                System.Windows.Automation.AutomationProperties.GetHelpText(element));
            Assert.Equal(
                string.Empty,
                UIElementAutomationPeer.CreatePeerForElement(element).GetHelpText());
            Assert.Equal("\\nabla^2 f", element.ToolTip);
            Assert.False(System.Windows.Controls.ToolTipService.GetShowsToolTipOnKeyboardFocus(element));
        });

    /// <summary>#1591: Ctrl+Shift+Enter on a math block speaks core's source
    /// sentence, once; off a math block it declines, so the key falls
    /// through.</summary>
    [Fact]
    public void CtrlShiftEnterSpeaksTheSourceOnRequest()
        => WithReadingSurface("reading-math-source", "Text.\n\n$$\\nabla^2 f$$\n", (_, surface, announced) =>
        {
            surface.CaretPosition = surface.Document.ContentStart.GetInsertionPosition(
                System.Windows.Documents.LogicalDirection.Forward);
            Assert.False(surface.TrySpeakMathSourceAtCaret());
            Assert.Empty(announced);

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TrySpeakMathSourceAtCaret());
            A11yEvent spoken = Assert.Single(announced);
            Assert.IsType<A11yEvent.MathSourceSpoken>(spoken);
            Assert.Equal("Source: \\nabla^2 f.", SlateUniffiMethods.A11yRender(spoken).Text);
        });

    /// <summary>#1591: a math block with no source to speak says so rather
    /// than staying silent. Core never projects an empty formula (math.rs
    /// skips one), so the fact builds the paragraph by hand.</summary>
    [Fact]
    public void CtrlShiftEnterWithoutASourceSaysSo()
        => WithReadingSurface("reading-math-no-source", "Text.\n", (_, surface, announced) =>
        {
            var paragraph = new System.Windows.Documents.Paragraph(
                new System.Windows.Documents.InlineUIContainer(
                    new ReadingMathElement("x", string.Empty, " ", string.Empty)));
            ReadingSemantics.MarkMathBlock(paragraph, "x");
            surface.Document.Blocks.Add(paragraph);
            surface.CaretPosition = paragraph.ContentStart;
            Assert.True(surface.TrySpeakMathSourceAtCaret());
            Assert.Equal(
                "Source not available.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });

    /// <summary>#1591 (the plan 45 XB-9 review): a coverage gap's element
    /// shows its source as text, and object navigation finds no child to
    /// read it from: the element's peer has no children, as the diagram's
    /// rendered visual has no peer.</summary>
    [Fact]
    public void AGapElementShowsNoSourceToObjectNavigation()
        => WithReadingSurface("reading-math-gap-children", "$$\\mathbb{R}$$\n", (_, surface, _) =>
            WithHostedSurface(surface, 600, () =>
            {
                ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
                Assert.IsType<System.Windows.Controls.TextBlock>(element.Content);
                Assert.True(System.Windows.Media.VisualTreeHelper.GetChildrenCount(element) > 0);
                Assert.True(
                    UIElementAutomationPeer.CreatePeerForElement(element).GetChildren()
                        is null or { Count: 0 });
            }));

    /// <summary>#1595 (the plan 45 XB-9 review): an element whose content
    /// would fill its line (a wrapping source, a drawing wider than the
    /// page) still shares the line with its speech run, so Down never lands
    /// on a line holding only the role.</summary>
    [Fact]
    public void ALongFormulaSharesItsLineWithItsSpeech()
        => WithReadingSurface(
            "reading-math-long-line",
            "$$\\begin{bmatrix} " + string.Join(" & ", Enumerable.Range(1, 60))
                + " \\end{bmatrix}$$\n\n$$"
                + string.Join(" + ", Enumerable.Range(1, 80).Select(i => $"x_{{{i}}}"))
                + "$$\n",
            (_, surface, _) => WithHostedSurface(surface, 420, () =>
            {
                List<ReadingMathElement> elements = FindMathElements(surface.Document).ToList();
                Assert.Equal(2, elements.Count);
                Assert.IsType<System.Windows.Controls.TextBlock>(elements[0].Content);
                Assert.IsType<System.Windows.Shapes.Path>(elements[1].Content);
                foreach (ReadingMathElement element in elements)
                {
                    var container = (System.Windows.Documents.InlineUIContainer)element.Parent;
                    System.Windows.Documents.Paragraph paragraph = MathParagraphOf(element);
                    Assert.Equal(
                        0,
                        paragraph.ContentStart.GetLineStartPosition(0)!.CompareTo(
                            container.ElementStart.GetLineStartPosition(0)));
                }
            }));

    /// <summary>#1595 (the plan 45 XB-9 review): a math prefs change
    /// rewrites every formula's speech, and with it its paragraph's length;
    /// the reader's caret stays in the paragraph it was in.</summary>
    [Fact]
    public void AMathPrefsChangeKeepsTheCaretInItsParagraph()
    {
        RunSta(() =>
        {
            string directory = Path.Combine(
                Path.GetTempPath(), $"slate-math-caret-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var store = new AppPreferencesStore(
                    Path.Combine(directory, "preferences.json"));
                using var fixture = FixtureVault.Create(1, "reading-math-caret");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "note0.md"),
                    "$$\\frac{a}{b} + x^2$$\n\n$$\\frac{c}{d}$$\n\nThe target paragraph.\n");
                using var session = VaultSession.OpenFilesystem(fixture.Root);
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);

                using var workspace = new WorkspaceViewModel(
                    session,
                    fixture.Root,
                    () => [],
                    _ => { },
                    startInteractionBackgroundWork: false,
                    preferencesStore: store);
                workspace.OpenPath("note0.md");
                WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };
                System.Windows.Documents.Paragraph target = surface.Document.Blocks
                    .OfType<System.Windows.Documents.Paragraph>()
                    .Single(paragraph => RangeTextOf(paragraph) == "The target paragraph.");
                surface.CaretPosition = target.ContentStart.GetPositionAtOffset(4);
                string speechBefore = FindMathElements(surface.Document).First().Speech;

                workspace.EditorPreferences.SetMathVerbosityCommand.Execute("verbose");

                Assert.NotEqual(speechBefore, FindMathElements(surface.Document).First().Speech);
                Assert.Equal(
                    "The target paragraph.",
                    RangeTextOf(surface.CaretPosition.Paragraph!));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    /// <summary>#1591 (the plan 45 XB-9 review): with keyboard focus ON a
    /// math element (Tab), Enter speaks it, Ctrl+Enter its braille and
    /// Ctrl+Shift+Enter its source; any other modifier falls through.</summary>
    [Fact]
    public void AFocusedMathElementAnswersItsThreeRequests()
        => WithReadingSurface("reading-math-focused", "$$\\nabla^2 f$$\n", (_, surface, announced) =>
        {
            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            Assert.True(surface.TryAnswerMath(element, System.Windows.Input.ModifierKeys.None));
            Assert.True(surface.TryAnswerMath(element, System.Windows.Input.ModifierKeys.Control));
            Assert.True(surface.TryAnswerMath(
                element,
                System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift));
            Assert.False(surface.TryAnswerMath(element, System.Windows.Input.ModifierKeys.Alt));
            Assert.NotEmpty(element.Braille);
            Assert.Equal(
                new[] { element.Speech + ", math.", element.Braille, "Source: \\nabla^2 f." },
                announced.Select(item => SlateUniffiMethods.A11yRender(item).Text));
        });

    /// <summary>#1595 (the plan 45 XB-9 review): a copy across a math block
    /// carries its fenced source, not the speech its text range reads; a
    /// selection that reaches no math paragraph's speech copies as WPF
    /// builds it.</summary>
    [Fact]
    public void CopyingAcrossAMathBlockCopiesItsSource()
        => WithReadingSurface("reading-math-copy", "Before.\n\n$$\\nabla^2 f$$\n\nAfter.\n", (_, surface, _) =>
        {
            System.Windows.Documents.FlowDocument document = surface.Document;
            ReadingMathElement element = Assert.Single(FindMathElements(surface.Document));
            string copied = surface.CopyTextOf(document.ContentStart, document.ContentEnd)!;
            Assert.StartsWith("Before.\r\n$$\\nabla^2 f$$\r\nAfter.", copied, StringComparison.Ordinal);
            Assert.DoesNotContain(element.Speech, copied, StringComparison.Ordinal);
            System.Windows.Documents.Paragraph after = document.Blocks
                .OfType<System.Windows.Documents.Paragraph>()
                .Last();
            Assert.Null(surface.CopyTextOf(after.ContentStart, after.ContentEnd));
        });

    /// <summary>Enter at the caret on a math block speaks the
    /// canonical landed vocabulary — "{speech}, math."</summary>
    [Fact]
    public void EnterAtTheCaretSpeaksTheMathBlock()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-enter");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$x^2$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret());
            string spoken = SlateUniffiMethods.A11yRender(
                Assert.Single(announced)).Text;
            Assert.EndsWith(", math.", spoken);
            Assert.Contains("squared", spoken, StringComparison.OrdinalIgnoreCase);

            // Ctrl+Enter reads the braille artifact — the accessible
            // detail the Nemeth/UEB pref selects (round 3 [high]). The
            // announced text is the element's decoded braille verbatim.
            announced.Clear();
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            string brailleSpoken = SlateUniffiMethods.A11yRender(
                Assert.Single(announced)).Text;
            Assert.Equal(
                Assert.Single(FindMathElements(surface.Document)).Braille,
                brailleSpoken);
            Assert.NotEmpty(brailleSpoken);
        });
    }

    /// <summary>
    /// Round 5/6 end-to-end: an oversized authored formula of
    /// SUPPORTED TeX degrades in CORE under the per-formula source
    /// budget (typed speech, no MathML/braille, no MathCAT worker
    /// time) — and the host honors that verdict: WPFMath is never
    /// entered (a supported oversized formula would otherwise parse
    /// into a giant Geometry on the dispatcher), the source renders
    /// in range, and interactions stay honest.
    /// </summary>
    [Fact]
    public void OversizedFormulaDegradesEndToEndWithoutEnteringTheRenderer()
    {
        RunSta(() =>
        {
            // Plain "x+x+…+x": WPFMath parses this happily, which is
            // exactly why the core verdict must gate the call.
            string oversized = string.Concat(
                Enumerable.Repeat("x+", (17 * 1024) / 2)) + "x";
            using var fixture = FixtureVault.Create(1, "reading-math-oversized");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "$$" + oversized + "$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var rendererEntered = new List<string>();
            ReadingDocumentBuilder.FormulaRenderProbeForTests = rendererEntered.Add;
            try
            {
                var announced = new List<A11yEvent>();
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(
                            WorkspaceItemKind.Markdown, "note0.md")),
                    announce: announced.Add,
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };

                Assert.Empty(rendererEntered);
                ReadingMathElement element =
                    Assert.Single(FindMathElements(surface.Document));
                Assert.IsType<System.Windows.Controls.TextBlock>(element.Content);
                Assert.Equal(
                    "Math expression too large to render to accessible speech.",
                    RangeTextOf(MathParagraphOf(element)));
                Assert.Equal(
                    "Math expression too large to render to accessible speech.",
                    System.Windows.Automation.AutomationProperties.GetName(element));
                Assert.Empty(element.Braille);
                Assert.Empty(element.MathMl);

                ReadingLandmark landmark = Assert.Single(
                    surface.LandmarksForTests,
                    candidate => candidate.Kind == ReadingLandmarkKind.Math);
                surface.CaretPosition = landmark.Position;
                Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
                Assert.Equal(
                    "Braille not available.",
                    SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
            }
            finally
            {
                ReadingDocumentBuilder.FormulaRenderProbeForTests = null;
            }
        });
    }

    /// <summary>
    /// Round 7: the projection-wide visual pool bounds AGGREGATE
    /// WPFMath work across a chunk of within-budget formulas —
    /// renderer entries stop when the pool drains, and every formula
    /// past that point keeps its full artifacts with the source
    /// visual fallback. The override seam keeps the fixture cheap;
    /// the production pool value is pinned alongside.
    /// </summary>
    [Fact]
    public void ProjectionPoolBoundsAggregateFormulaRendering()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-pool");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                string.Concat(Enumerable.Repeat("$$x+1$$\n\n", 40)));
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var rendererEntered = new List<string>();
            ReadingDocumentBuilder.FormulaRenderProbeForTests = rendererEntered.Add;
            // "x+1" costs 3 bytes; a 6-byte pool admits exactly two.
            ReadingDocumentBuilder.ProjectionMathRenderByteBudgetOverrideForTests = 6;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(
                            WorkspaceItemKind.Markdown, "note0.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };

                // This harness projects twice (mode toggle, then the
                // surface-bind re-projection), and the pool is
                // per-projection by design — the bound is 2 renderer
                // entries per projection, never one per formula.
                Assert.InRange(rendererEntered.Count, 2, 4);
                List<ReadingMathElement> elements =
                    FindMathElements(surface.Document).ToList();
                Assert.Equal(40, elements.Count);
                Assert.Equal(
                    2,
                    elements.Count(element => element.Content is System.Windows.Shapes.Path));
                Assert.All(elements, element =>
                {
                    string name = System.Windows.Automation.AutomationProperties
                        .GetName(element);
                    Assert.False(string.IsNullOrWhiteSpace(name));
                    Assert.NotEqual("Math expression.", name);
                });
                // The production pool: sixteen maximal formulas, or
                // hundreds of ordinary ones.
                Assert.Equal(
                    16 * ReadingDocumentBuilder.MaximumRenderedFormulaSourceBytes,
                    ReadingDocumentBuilder.ProjectionMathRenderByteBudget);
            }
            finally
            {
                ReadingDocumentBuilder.FormulaRenderProbeForTests = null;
                ReadingDocumentBuilder.ProjectionMathRenderByteBudgetOverrideForTests = null;
            }
        });
    }

    /// <summary>
    /// Round 7: a formula WITHIN core's budgets (real speech and
    /// MathML) but past the host's per-formula visual cap never
    /// enters WPFMath — the visual degrades to source-in-range while
    /// every accessibility artifact stays live.
    /// </summary>
    [Fact]
    public void MidSizedFormulaKeepsArtifactsButSkipsTheRenderer()
    {
        RunSta(() =>
        {
            string formula = string.Concat(Enumerable.Repeat("x+", 2_200)) + "x";
            using var fixture = FixtureVault.Create(1, "reading-math-midsize");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"),
                "$$" + formula + "$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var rendererEntered = new List<string>();
            ReadingDocumentBuilder.FormulaRenderProbeForTests = rendererEntered.Add;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(
                            WorkspaceItemKind.Markdown, "note0.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };

                Assert.Empty(rendererEntered);
                ReadingMathElement element =
                    Assert.Single(FindMathElements(surface.Document));
                Assert.IsType<System.Windows.Controls.TextBlock>(element.Content);
                string name = System.Windows.Automation.AutomationProperties
                    .GetName(element);
                Assert.Equal(name, RangeTextOf(MathParagraphOf(element)));
                Assert.Contains("plus", name, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("<math", element.MathMl.TrimStart());
            }
            finally
            {
                ReadingDocumentBuilder.FormulaRenderProbeForTests = null;
            }
        });
    }

    /// <summary>A math block whose artifact carries no braille (core
    /// degradation, e.g. a source pulldown-latex cannot parse, which core
    /// speaks as its source) answers Ctrl+Enter honestly instead of
    /// staying silent.</summary>
    [Fact]
    public void CtrlEnterWithoutBrailleSaysSo()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-no-braille");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$${n \\choose k}$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };

            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            surface.CaretPosition = landmark.Position;
            Assert.True(surface.TryActivateAtCaret(brailleRequested: true));
            Assert.Equal(
                "Braille not available.",
                SlateUniffiMethods.A11yRender(Assert.Single(announced)).Text);
        });
    }

    /// <summary>The real math element's peer answers the registered
    /// MathML property through WPF's own dispatch and localizes its
    /// control type as "math".</summary>
    [Fact]
    public void MathElementPeerServesTheConventionProperty()
    {
        RunSta(() =>
        {
            MathMlUiaProperty.Initialize();
            Assert.True(MathMlUiaProperty.IsActive, MathMlUiaProperty.InactiveReason);

            var element = new ReadingMathElement(
                "x squared", "<math><mi>x</mi></math>", "x^2", "⠭⠘⠆");
            var peer = new ReadingMathElementPeer(element);
            Assert.Equal("math", peer.GetLocalizedControlType());
            Assert.Equal("⠭⠘⠆", peer.GetItemStatus());

            System.Reflection.MethodInfo dispatch = typeof(AutomationPeer).GetMethod(
                "GetPropertyValue",
                System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance,
                types: new[] { typeof(int) })!;
            Assert.Equal(
                "<math><mi>x</mi></math>",
                dispatch.Invoke(
                    peer, new object[] { MathMlUiaProperty.PropertyIdForTests }));
        });
    }

    /// <summary>
    /// W3-2 MathPrefs parity: preferences persist across view-model
    /// lifetimes, announce through the canonical vocabulary, reject
    /// unknown keys, and — the part nothing else covers — a change
    /// re-renders an OPEN reading projection with the new prefs
    /// (verbosity flips the MathCAT speech with identical live text).
    /// </summary>
    [Fact]
    public void MathPrefsPersistAnnounceAndReprojectOpenReadingViews()
    {
        RunSta(() =>
        {
            string directory = Path.Combine(
                Path.GetTempPath(), $"slate-math-prefs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var store = new AppPreferencesStore(
                    Path.Combine(directory, "preferences.json"));
                using var fixture = FixtureVault.Create(1, "reading-math-prefs");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "note0.md"),
                    "$$\\frac{a}{b} + x^2$$\n");
                using var session = VaultSession.OpenFilesystem(fixture.Root);
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);

                var announced = new List<A11yEvent>();
                using var workspace = new WorkspaceViewModel(
                    session,
                    fixture.Root,
                    () => [],
                    announced.Add,
                    startInteractionBackgroundWork: false,
                    preferencesStore: store);
                workspace.OpenPath("note0.md");
                WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
                tab.ToggleViewMode();
                var surface = new ReadingSurface { Model = tab.Reading };
                string mediumSpeech = System.Windows.Automation.AutomationProperties
                    .GetName(FindMathElements(surface.Document).Single());
                string nemethBraille = System.Windows.Automation.AutomationProperties
                    .GetItemStatus(FindMathElements(surface.Document).Single());
                Assert.NotEmpty(nemethBraille);

                announced.Clear();
                System.Windows.Documents.FlowDocument before = tab.Reading!.Document!;
                workspace.EditorPreferences.SetMathVerbosityCommand.Execute("terse");
                Assert.Contains(
                    announced,
                    item => SlateUniffiMethods.A11yRender(item).Text
                        == "Math verbosity: Terse.");
                // The braille code flips the artifact bytes for EVERY
                // formula (verbosity speech can coincide), so it is the
                // deterministic proof that a prefs change reaches the
                // session AND re-projects the open reading view: the
                // content digest misses the memo and the document
                // rebuilds with identical live text.
                workspace.EditorPreferences.SetMathBrailleCodeCommand.Execute("ueb");
                Assert.Contains(
                    announced,
                    item => SlateUniffiMethods.A11yRender(item).Text
                        == "Math braille code: UEB.");
                Assert.NotSame(before, tab.Reading!.Document);
                Assert.Equal(
                    mediumSpeech.Length > 0,
                    System.Windows.Automation.AutomationProperties
                        .GetName(FindMathElements(surface.Document).Single())
                        .Length > 0);
                // Round 3 [high]: the pref must change something an AT
                // user can RETRIEVE, not merely rebuild the document —
                // the element's ItemStatus carries the decoded braille,
                // and Nemeth vs UEB cells differ for this formula.
                string uebBraille = System.Windows.Automation.AutomationProperties
                    .GetItemStatus(FindMathElements(surface.Document).Single());
                Assert.NotEmpty(uebBraille);
                Assert.NotEqual(nemethBraille, uebBraille);

                // Unknown key: no change, no announcement.
                announced.Clear();
                workspace.EditorPreferences.SetMathVerbosityCommand.Execute("shouty");
                Assert.Empty(announced);

                // Persistence across lifetimes.
                using var second = new EditorPreferencesViewModel(
                    _ => { },
                    new FakeEditorSpellingService(),
                    preferencesStore: store);
                Assert.True(second.IsMathVerbosityTerse);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    /// <summary>
    /// W3-2 round 1 [high]: a same-position unsaved edit keeps byte
    /// containment, and speaking the OLD equation would be a lie —
    /// the artifact only applies under live-source coherence; the
    /// stale case degrades to the live source in range.
    /// </summary>
    [Fact]
    public void UnsavedMathEditsNeverSpeakTheStaleFormula()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-stale");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$x^2 + 1$$\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            var surface = new ReadingSurface { Model = tab.Reading };
            Assert.Single(FindMathElements(surface.Document));

            // Same-length, same-position unsaved edit inside the fence.
            tab.EditorDocument!.Replace(
                tab.Text.IndexOf("x^2 + 1", StringComparison.Ordinal), 7, "y^3 - 2");
            tab.Reading!.Refresh();

            ReadingMathElement stale = Assert.Single(FindMathElements(surface.Document));
            Assert.Equal("$$y^3 - 2$$", stale.Speech);
            Assert.Equal("y^3 - 2", stale.Source);
            Assert.Empty(stale.MathMl);
            Assert.Empty(stale.Braille);
            string text = new System.Windows.Documents.TextRange(
                surface.Document.ContentStart,
                surface.Document.ContentEnd).Text;
            Assert.Contains("y^3 - 2", text, StringComparison.Ordinal);
            Assert.DoesNotContain("x^2 + 1", text, StringComparison.Ordinal);
            ReadingLandmark landmark = Assert.Single(
                surface.LandmarksForTests,
                candidate => candidate.Kind == ReadingLandmarkKind.Math);
            Assert.Contains("y^3", landmark.Text, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// W3-2 round 1 [medium]: a prefs change must NOT reactivate
    /// hidden projections — an unbound model keeps its document and
    /// starts no background work; it rebuilds with the new prefs on
    /// its next genuine bind.
    /// </summary>
    [Fact]
    public void PrefsChangesLeaveHiddenProjectionsAloneUntilRebind()
    {
        RunSta(() =>
        {
            string directory = Path.Combine(
                Path.GetTempPath(), $"slate-math-hidden-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                var store = new AppPreferencesStore(
                    Path.Combine(directory, "preferences.json"));
                using var fixture = FixtureVault.Create(1, "reading-math-hidden");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "first.md"), "$$a+b$$\n");
                File.WriteAllText(
                    Path.Combine(fixture.Root, "second.md"), "$$c+d$$\n");
                using var session = VaultSession.OpenFilesystem(fixture.Root);
                using var cancel = new CancelToken();
                session.ScanInitial(cancel);

                using var workspace = new WorkspaceViewModel(
                    session,
                    fixture.Root,
                    () => [],
                    _ => { },
                    startInteractionBackgroundWork: false,
                    preferencesStore: store);
                workspace.OpenPath("first.md");
                WorkspaceTabViewModel hidden = workspace.ActiveGroup.ActiveTab!;
                hidden.ToggleViewMode();
                System.Windows.Documents.FlowDocument hiddenDocument =
                    hidden.Reading!.Document!;

                // No surface bound anywhere: the pref flip must not
                // touch the hidden projection.
                workspace.EditorPreferences.SetMathBrailleCodeCommand.Execute("ueb");
                Assert.Same(hiddenDocument, hidden.Reading!.Document);

                // The next genuine bind rebuilds with the new prefs.
                var surface = new ReadingSurface { Model = hidden.Reading };
                Assert.NotSame(hiddenDocument, hidden.Reading!.Document);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        });
    }

    /// <summary>
    /// W3-2 round 2 [medium]: a prefs change while an UNBOUND model's
    /// refresh is in flight poisons that generation — the stale-prefs
    /// publish is rejected, and the next bind restarts fresh instead
    /// of adopting the obsolete artifact (set_math_prefs advances no
    /// session generation, so nothing else would catch it).
    /// </summary>
    [Fact]
    public void PrefsChangeDuringHiddenInFlightRefreshNeverPublishesStale()
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, "reading-math-race");
            File.WriteAllText(
                Path.Combine(fixture.Root, "note0.md"), "$$a+b$$ body\n\n# H\n");
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                startInteractionBackgroundWork: false);
            using var reading = new SlateWindows.Reading.ReadingContentViewModel(
                session, tab, _ => { });

            // Gate the first (hidden) fetch so the prefs change lands
            // while it is in flight.
            using var gate = new ManualResetEventSlim(initialState: false);
            int fetches = 0;
            reading.FetchFaultForTests = () =>
            {
                Interlocked.Increment(ref fetches);
                gate.Wait(TimeSpan.FromSeconds(20));
                return null;
            };

            reading.Refresh();
            reading.InvalidateForPrefsChange();

            // Bind BEFORE the old fetch is released: the poisoned
            // generation means EnsureProjected must start a fresh
            // refresh rather than waiting on the doomed one.
            var surface = new ReadingSurface { Model = reading };
            gate.Set();
            WaitForUi(() => reading.Document is not null);
            WaitForUi(() =>
                new System.Windows.Documents.TextRange(
                    surface.Document.ContentStart,
                    surface.Document.ContentEnd).Text.Contains(
                        "body", StringComparison.Ordinal));
            Assert.True(
                fetches >= 2,
                $"expected the poisoned fetch plus a fresh one, saw {fetches}");
        });
    }

    /// <summary>#1056: a persisted "mathSpeak" (a prior build's tag)
    /// migrates to ClearSpeak — MathCAT never implemented MathSpeak, so
    /// ClearSpeak is what that user was already hearing; the migration
    /// changes nothing audible and never infers SimpleSpeak.</summary>
    [Fact]
    public void PersistedMathSpeakMigratesToClearSpeak()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"slate-mathspeak-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "preferences.json");
            File.WriteAllText(path, "{\"mathSpeechStyle\":\"mathSpeak\"}");
            var store = new AppPreferencesStore(path);
            Assert.Equal("clearSpeak", store.Load().MathSpeechStyle);

            using var preferences = new EditorPreferencesViewModel(
                _ => { },
                new FakeEditorSpellingService(),
                preferencesStore: store);
            Assert.True(preferences.IsMathSpeechClearSpeak);
            Assert.False(preferences.IsMathSpeechSimpleSpeak);
            Assert.Equal(
                MathSpeechStyle.ClearSpeak,
                preferences.CurrentMathPrefs.SpeechStyle);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>#1056: SimpleSpeak — the second style MathCAT actually
    /// ships — is selectable, announced by its enum-verbatim name, and
    /// reaches the FFI prefs the session re-projects from.</summary>
    [Fact]
    public void SimpleSpeakIsSelectableAndAnnounced()
    {
        var announced = new List<A11yEvent>();
        using var preferences = new EditorPreferencesViewModel(
            announced.Add,
            new FakeEditorSpellingService());
        Assert.True(preferences.IsMathSpeechClearSpeak);

        preferences.SetMathSpeechStyleCommand.Execute("simpleSpeak");
        Assert.True(preferences.IsMathSpeechSimpleSpeak);
        Assert.False(preferences.IsMathSpeechClearSpeak);
        Assert.Equal(
            MathSpeechStyle.SimpleSpeak,
            preferences.CurrentMathPrefs.SpeechStyle);
        A11yEvent.MathSpeechStyle announcement =
            Assert.IsType<A11yEvent.MathSpeechStyle>(Assert.Single(announced));
        Assert.Equal("SimpleSpeak", announcement.Name);
    }

    /// <summary>#1056: a persisted "simpleSpeak" restores as the active
    /// style — the round trip the menu's persist writes.</summary>
    [Fact]
    public void PersistedSimpleSpeakRestores()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"slate-simplespeak-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "preferences.json");
            File.WriteAllText(path, "{\"mathSpeechStyle\":\"simpleSpeak\"}");
            var store = new AppPreferencesStore(path);
            Assert.Equal("simpleSpeak", store.Load().MathSpeechStyle);

            using var preferences = new EditorPreferencesViewModel(
                _ => { },
                new FakeEditorSpellingService(),
                preferencesStore: store);
            Assert.True(preferences.IsMathSpeechSimpleSpeak);
            Assert.Equal(
                MathSpeechStyle.SimpleSpeak,
                preferences.CurrentMathPrefs.SpeechStyle);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>One note's reading surface over a real vault and
    /// MathCAT, with the announcements it posts.</summary>
    private static void WithReadingSurface(
        string slug, string markdown, Action<WorkspaceTabViewModel, ReadingSurface, List<A11yEvent>> body)
    {
        RunSta(() =>
        {
            using var fixture = FixtureVault.Create(1, slug);
            File.WriteAllText(Path.Combine(fixture.Root, "note0.md"), markdown);
            using var session = VaultSession.OpenFilesystem(fixture.Root);
            using var cancel = new CancelToken();
            session.ScanInitial(cancel);

            var announced = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(
                        WorkspaceItemKind.Markdown, "note0.md")),
                announce: announced.Add,
                startInteractionBackgroundWork: false);
            tab.ToggleViewMode();
            body(tab, new ReadingSurface { Model = tab.Reading }, announced);
        });
    }

    /// <summary>The surface in a laid-out window of this width, off
    /// screen and never activated.</summary>
    private static void WithHostedSurface(ReadingSurface surface, double width, Action body)
    {
        var window = new System.Windows.Window
        {
            Content = surface,
            Width = width,
            Height = 600,
            Left = -10000,
            Top = -10000,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStyle = System.Windows.WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            body();
        }
        finally
        {
            window.Close();
        }
    }

    private static System.Windows.Documents.Paragraph MathParagraphOf(
        ReadingMathElement element) =>
        (System.Windows.Documents.Paragraph)
            ((System.Windows.Documents.InlineUIContainer)element.Parent).Parent;

    /// <summary>What line reading reads: the paragraph's text range. WPF
    /// writes the embedded element as one trailing space, which no reader
    /// speaks.</summary>
    private static string RangeTextOf(System.Windows.Documents.Paragraph paragraph) =>
        new System.Windows.Documents.TextRange(
            paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd(' ');

    private static IEnumerable<ReadingMathElement> FindMathElements(
        System.Windows.Documents.FlowDocument document)
    {
        foreach (System.Windows.Documents.Block block in document.Blocks)
        {
            if (block is not System.Windows.Documents.Paragraph paragraph)
            {
                continue;
            }
            foreach (System.Windows.Documents.Inline inline in paragraph.Inlines)
            {
                if (inline is System.Windows.Documents.InlineUIContainer
                    {
                        Child: ReadingMathElement element,
                    })
                {
                    yield return element;
                }
            }
        }
    }

    private sealed class FakeMathPeer
        : FrameworkElementAutomationPeer, IMathMlUiaSource
    {
        public FakeMathPeer(string mathMl)
            : base(new System.Windows.Controls.Border())
        {
            MathMl = mathMl;
        }

        public string MathMl { get; }
    }

    /// <summary>Pump the STA dispatcher until the condition holds.</summary>
    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous action timed out.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    /// <summary>WPF objects require STA; xunit runs MTA.</summary>
    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
