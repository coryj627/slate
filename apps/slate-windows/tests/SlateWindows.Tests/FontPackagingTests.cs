// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>DS-1: the editor and the reading view draw the design's fonts on any
/// machine, because the fonts and their licences ship with the app. Windows
/// doesn't install Cascadia or Open Sans, so a name that only resolves on this
/// desktop would pass a check that asked for the name alone. These facts look at
/// the file a face came from.</summary>
public sealed class FontPackagingTests
{
    private const string FontsRoot = "pack://application:,,,/SlateWindows;component/Fonts/";
    private const string EditorKey = "Slate.EditorFontFamily";
    private const string ReadingKey = "Slate.ReadingFontFamily";
    private const string CodeKey = "Slate.CodeFontFamily";

    [Theory]
    [InlineData("CascadiaCode.ttf")]
    [InlineData("CascadiaCodeItalic.ttf")]
    [InlineData("CascadiaMono.ttf")]
    [InlineData("CascadiaMonoItalic.ttf")]
    [InlineData("OpenSans-Variable.ttf")]
    [InlineData("OpenSans-Italic-Variable.ttf")]
    [InlineData("SlateEditor.CompositeFont")]
    public void TheFontsShipInsideTheApp(string file) => StaThread.Run(() =>
    {
        RegisterPackScheme();
        System.Windows.Resources.StreamResourceInfo info = Application.GetResourceStream(new Uri(FontsRoot + file));
        using Stream stream = info.Stream;
        Assert.True(stream.Length > 0, $"{file} is empty.");
    });

    /// <summary>The Nerd Font and Powerline files are most of the folder's size, and
    /// nothing draws them.</summary>
    [Theory]
    [InlineData("CascadiaCodeNF.ttf")]
    [InlineData("CascadiaCodePL.ttf")]
    [InlineData("CascadiaMonoNF.ttf")]
    [InlineData("CascadiaMonoPL.ttf")]
    public void TheFontsNothingDrawsStayOut(string file) => StaThread.Run(() =>
    {
        RegisterPackScheme();
        _ = Assert.Throws<IOException>(() => Application.GetResourceStream(new Uri(FontsRoot + file)));
    });

    /// <summary>Both are the SIL Open Font License, which has to travel with the
    /// fonts. The app's own output is what ships, so it's the one looked at.</summary>
    [Theory]
    [InlineData("Cascadia-OFL.txt", "Reserved Font Name Cascadia Code")]
    [InlineData("OpenSans-OFL.txt", "The Open Sans Project Authors")]
    public void TheLicencesShipBesideIt(string file, string names)
    {
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        string text = File.ReadAllText(Path.Combine(
            SourceText.ShellSourceRoot(), "bin", configuration, "net10.0-windows", "Licenses", file));
        Assert.Contains(names, text, StringComparison.Ordinal);
        Assert.Contains("SIL Open Font License, Version 1.1", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TheFamilyKeysResolveInEveryAppearance(bool dark, bool highContrast)
    {
        (string reading, string readingItalic, string code, double spacing, string editorName) = InRealWindow(dark, highContrast, window =>
        {
            var editor = (FontFamily)window.FindResource(EditorKey);
            return (
                FaceFile(window, ReadingKey, FontStyles.Normal),
                FaceFile(window, ReadingKey, FontStyles.Italic),
                FaceFile(window, CodeKey, FontStyles.Normal),
                editor.LineSpacing,
                editor.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage("en-US")]);
        });

        Assert.Equal("pack://application:,,,/SlateWindows;component/Fonts/OpenSans-Variable.ttf", reading, ignoreCase: true);
        Assert.Equal("pack://application:,,,/SlateWindows;component/Fonts/OpenSans-Italic-Variable.ttf", readingItalic, ignoreCase: true);
        Assert.Equal("pack://application:,,,/SlateWindows;component/Fonts/CascadiaMono.ttf", code, ignoreCase: true);
        Assert.Equal(1.5714, spacing, 4);
        Assert.Equal("Slate Editor", editorName);
    }

    /// <summary>The design's editor is 14/22, and AvalonEdit has no line-height API.
    /// The composite font's line spacing is what gets there, and it keeps the ratio
    /// at the editor's other sizes.</summary>
    [Theory]
    [InlineData(8, 12.57)]
    [InlineData(14, 22)]
    [InlineData(20, 31.43)]
    [InlineData(40, 62.86)]
    public void TheEditorIsFourteenOnTwentyTwo(double size, double lineHeight)
    {
        double measured = InRealWindow(dark: false, highContrast: false, window =>
        {
            var editor = new SlateTextEditor { Text = "first\nsecond\n", FontSize = size };
            window.Content = editor;
            window.UpdateLayout();
            Pump();
            return editor.TextArea.TextView.DefaultLineHeight;
        });

        Assert.Equal(lineHeight, measured, 2);
    }

    /// <summary>The shipped template, not a test's copy of it.</summary>
    [Fact]
    public void TheShellsEditorTakesTheFont() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n\nBody.\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        SlateTextEditor editor = ShownShell.Descendants(host.Shell).OfType<SlateTextEditor>().Single();
        Assert.Equal(14, editor.FontSize);
        Assert.Equal(22, editor.TextArea.TextView.DefaultLineHeight, 2);
    });

    /// <summary>A composite family answers false to
    /// <c>Typeface.TryGetGlyphTypeface</c>, so this reads the face from the glyph
    /// runs the editor really laid out. A simulated face would say so.</summary>
    [Fact]
    public void TheEditorsBoldAndItalicAreRealFaces()
    {
        (GlyphTypeface regular, GlyphTypeface bold, GlyphTypeface italic) = InRealWindow(dark: false, highContrast: false, window =>
        {
            const string text = "rest bold italic";
            var editor = new SlateTextEditor { Text = text, FontSize = 14 };
            editor.TextArea.TextView.LineTransformers.Add(new WeightAndStyle(
                text.IndexOf("bold", StringComparison.Ordinal),
                text.IndexOf("italic", StringComparison.Ordinal),
                text.Length));
            window.Content = editor;
            window.UpdateLayout();
            Pump();
            GlyphRun[] runs = RunsOf(editor, 1);
            return (
                RunHolding(runs, 'r').GlyphTypeface,
                RunHolding(runs, 'b').GlyphTypeface,
                RunHolding(runs, 'i').GlyphTypeface);
        });

        Assert.Equal("pack://application:,,,/SlateWindows;component/Fonts/CascadiaCode.ttf", regular.FontUri.ToString(), ignoreCase: true);
        Assert.Equal(StyleSimulations.None, regular.StyleSimulations);
        Assert.Equal(FontWeights.Normal, regular.Weight);
        Assert.Equal(StyleSimulations.None, bold.StyleSimulations);
        Assert.Equal(FontWeights.Bold, bold.Weight);
        Assert.Equal(StyleSimulations.None, italic.StyleSimulations);
        Assert.Equal(FontStyles.Italic, italic.Style);
        Assert.Contains("CascadiaCodeItalic", italic.FontUri.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Spike 2 ran on a test colorizer. This is Slate's own highlighter on a
    /// heading, whose colour run covers the operator, and on a fenced code block.
    /// Cascadia's ligatures are contextual alternates, so the glyph count stays the
    /// same and the glyph index changes.</summary>
    [Theory]
    [InlineData("# Heading -> with != operators\n", 1, "->")]
    [InlineData("# Heading -> with != operators\n", 1, "!=")]
    [InlineData("## Level === and <= here\n", 1, "===")]
    [InlineData("## Level === and <= here\n", 1, "<=")]
    [InlineData("```\nx -> y\n```\n", 2, "->")]
    public void TheEditorJoinsOperatorsThroughItsHighlighter(string text, int line, string op)
    {
        (bool joinedInCode, bool joinedInMono) = InRealWindow(dark: false, highContrast: false, window =>
        {
            using var session = new AvalonDocumentBufferSession(text, _ => { });
            var editor = new SlateTextEditor { Document = session.Document, HighlightSession = session, FontSize = 14 };
            window.Content = editor;
            window.UpdateLayout();
            Pump();
            AvalonHighlightingCoordinator coordinator = Assert.IsType<AvalonHighlightingCoordinator>(editor.HighlightingForCensus);
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (coordinator.RefreshCountForCensus == 0 && DateTime.UtcNow < deadline)
            {
                Pump();
            }

            Assert.True(coordinator.RefreshCountForCensus > 0, "the highlighter never ran.");
            Assert.NotEmpty(coordinator.ColorizerWindowForCensus!.Spans);
            bool code = Joined(RunsOf(editor, line), op);

            // The control: Cascadia Mono has no such alternates, so a check that
            // can't tell the two apart proves nothing.
            editor.TextArea.TextView.SetResourceReference(TextElement.FontFamilyProperty, CodeKey);
            window.UpdateLayout();
            Pump();
            return (code, Joined(RunsOf(editor, line), op));
        });

        Assert.True(joinedInCode, $"{op} isn't joined in the editor's font.");
        Assert.False(joinedInMono, $"{op} reads as joined in Cascadia Mono, so this check can't see a ligature.");
    }

    [Fact]
    public void TheReadingViewDrawsOpenSans()
    {
        string face = InRealWindow(dark: false, highContrast: false, window =>
        {
            var surface = new ReadingSurface();
            window.Content = surface;
            window.UpdateLayout();
            Pump();
            Assert.True(
                new Typeface(surface.Document.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal)
                    .TryGetGlyphTypeface(out GlyphTypeface? glyphs));
            return glyphs!.FontUri.ToString();
        });

        Assert.Contains("OpenSans-Variable", face, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The five sites that used to name an installed font, reached through
    /// the builder rather than a list of them.</summary>
    [Fact]
    public void CodeInTheReadingViewDrawsTheShippedMono()
    {
        (string fenced, string inline) = InRealWindow(dark: false, highContrast: false, window =>
        {
            const string source = "Some `inline` code.\n\n```\nfenced\n```\n";
            ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(source);
            ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
                source, Array.Empty<RenderedCitation>(), Array.Empty<OutgoingLink>());
            FlowDocument document = ReadingDocumentBuilder.Build([.. blocks.Zip(inlines, (block, line) => (block, line))]).Document;
            window.Content = new FlowDocumentScrollViewer { Document = document };
            window.UpdateLayout();
            Pump();

            // A code block is a section: its header, then its code.
            Paragraph[] paragraphs = [.. document.Blocks.SelectMany(ParagraphsIn)];
            Paragraph code = paragraphs
                .Single(paragraph => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Contains("fenced", StringComparison.Ordinal));
            Run inlined = paragraphs.SelectMany(Runs).Single(run => run.Text == "inline");
            return (code.FontFamily.Source, inlined.FontFamily.Source);
        });

        Assert.Contains("#Cascadia Mono", fenced, StringComparison.Ordinal);
        Assert.Contains("#Cascadia Mono", inline, StringComparison.Ordinal);
    }

    /// <summary>WPF writes a family's source into a copy's RTF font table as the font's
    /// name. A pack URI means nothing to Word, which would show it in its font box,
    /// and the URI's semicolon ends the table's entry early. This is WPF's own copy
    /// path, stopped before the clipboard, and its note holds text that looks just
    /// like a font's address, which belongs to the note.</summary>
    [Fact]
    public void ACopyNamesFontsAnotherAppCanFind()
    {
        const string lookalike = "Plain pack://application:,,,/SlateWindows;component/Fonts/#Open Sans, Segoe UI and {braces} and ";
        (string[] formats, string rtf, string pasted) = InRealWindow(dark: false, highContrast: false, window =>
        {
            ReadingSurface surface = SurfaceWithCode(window, lookalike);
            surface.SelectAll();
            IDataObject? copied = null;
            DataObject.AddCopyingHandler(surface, (_, e) =>
            {
                copied = e.DataObject;
                e.CancelCommand();
            });
            surface.Copy();
            Assert.NotNull(copied);

            string text = (string)copied.GetData(DataFormats.Rtf);
            var recipient = new FlowDocument();
            var range = new TextRange(recipient.ContentStart, recipient.ContentEnd);
            using var bytes = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(text));
            range.Load(bytes, DataFormats.Rtf);
            return (copied.GetFormats(), text, range.Text);
        });

        Assert.Contains(DataFormats.UnicodeText, formats);
        Assert.Contains(DataFormats.Rtf, formats);
        Assert.DoesNotContain(DataFormats.Xaml, formats);
        Assert.DoesNotContain(DataFormats.XamlPackage, formats);

        string table = rtf[rtf.IndexOf(@"{\fonttbl", StringComparison.Ordinal)..rtf.IndexOf(@"{\colortbl", StringComparison.Ordinal)];
        Assert.DoesNotContain("pack://", table, StringComparison.Ordinal);
        Assert.Contains(@"\fcharset0 Open Sans;", table, StringComparison.Ordinal);
        Assert.Contains(@"\fcharset0 Cascadia Mono;", table, StringComparison.Ordinal);
        Assert.Contains(lookalike, pasted, StringComparison.Ordinal);
    }

    /// <summary>A drag asks the same two questions of the surface as a copy does.</summary>
    [Fact]
    public void ADragNamesFontsAnotherAppCanFind()
    {
        (bool xaml, bool package, string rtf) = InRealWindow(dark: false, highContrast: false, window =>
        {
            ReadingSurface surface = SurfaceWithCode(window, "Plain words and ");
            using var saved = new MemoryStream();
            new TextRange(surface.Document.ContentStart, surface.Document.ContentEnd).Save(saved, DataFormats.Rtf);
            var dragged = new DataObject(DataFormats.Rtf, System.Text.Encoding.ASCII.GetString(saved.ToArray()));
            surface.RaiseEvent(new DataObjectCopyingEventArgs(dragged, isDragDrop: true));

            var asksXaml = new DataObjectSettingDataEventArgs(dragged, DataFormats.Xaml);
            surface.RaiseEvent(asksXaml);
            var asksPackage = new DataObjectSettingDataEventArgs(dragged, DataFormats.XamlPackage);
            surface.RaiseEvent(asksPackage);
            return (asksXaml.CommandCancelled, asksPackage.CommandCancelled, (string)dragged.GetData(DataFormats.Rtf));
        });

        Assert.True(xaml);
        Assert.True(package);
        Assert.DoesNotContain("pack://", rtf, StringComparison.Ordinal);
    }

    /// <summary>AvalonEdit hands the text area's family to the native IME as a LOGFONT
    /// face name, cut at 31 characters. A pack URI there would name no font at all, so
    /// the area keeps a face Windows has while the view inside it draws the text.</summary>
    [Fact]
    public void TheImeAsksForAFontWindowsHas()
    {
        (string face, bool installed, double lineHeight) = InRealWindow(dark: false, highContrast: false, window =>
        {
            var editor = new SlateTextEditor { Text = "first\n", FontSize = 14 };
            window.Content = editor;
            window.UpdateLayout();
            Pump();
            string source = editor.TextArea.FontFamily.Source;
            return (
                source,
                Fonts.SystemFontFamilies.Any(family => family.Source == source),
                editor.TextArea.TextView.DefaultLineHeight);
        });

        Assert.True(face.Length < 32, $"{face} doesn't fit a LOGFONT.");
        Assert.True(installed, $"{face} isn't a font Windows has.");
        Assert.Equal(22, lineHeight, 2);
    }

    private static ReadingSurface SurfaceWithCode(Window window, string text)
    {
        var surface = new ReadingSurface();
        window.Content = surface;
        var code = new Run("code");
        code.SetResourceReference(TextElement.FontFamilyProperty, CodeKey);
        surface.Document.Blocks.Add(new Paragraph { Inlines = { new Run(text), code } });
        window.UpdateLayout();
        Pump();
        return surface;
    }

    /// <summary>NVDA reads UI Automation's font name under Report Font Name, and WPF
    /// answers with the family's source. Measured on the launched app: the reader
    /// would hear the whole pack URI.</summary>
    [Fact]
    public void AReaderHearsAFontsNameNotItsAddress()
    {
        (object? plain, object? code, object? both, string? found) = InRealWindow(dark: false, highContrast: false, window =>
        {
            ReadingSurface surface = SurfaceWithCode(window, "Plain words and ");
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(surface);
            var text = (ITextProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Text);
            ITextRangeProvider document = text.DocumentRange;
            const int fontName = HeadingStyleTextProvider.FontNameAttribute;
            return (
                document.FindText("Plain", backward: false, ignoreCase: false).GetAttributeValue(fontName),
                document.FindText("code", backward: false, ignoreCase: false).GetAttributeValue(fontName),
                document.GetAttributeValue(fontName),
                document.FindAttribute(fontName, "Cascadia Mono", backward: false)?.GetText(-1));
        });

        Assert.Equal("Open Sans", plain);
        Assert.Equal("Cascadia Mono", code);
        Assert.Same(System.Windows.Automation.TextPattern.MixedAttributeValue, both);
        Assert.Equal("code", found);
    }

    private static IEnumerable<Paragraph> ParagraphsIn(Block block) => block switch
    {
        Paragraph paragraph => [paragraph],
        Section section => section.Blocks.SelectMany(ParagraphsIn),
        _ => [],
    };

    private static IEnumerable<Run> Runs(Paragraph paragraph) => paragraph.Inlines.SelectMany(RunsOf);

    private static IEnumerable<Run> RunsOf(Inline inline) => inline switch
    {
        Run run => [run],
        Span span => span.Inlines.SelectMany(RunsOf),
        _ => [],
    };

    private static string FaceFile(FrameworkElement scope, string key, FontStyle style)
    {
        var family = (FontFamily)scope.FindResource(key);
        Assert.True(
            new Typeface(family, style, FontWeights.Normal, FontStretches.Normal).TryGetGlyphTypeface(out GlyphTypeface? glyphs),
            $"{key} has no face.");
        return glyphs!.FontUri.ToString();
    }

    private static GlyphRun[] RunsOf(SlateTextEditor editor, int lineNumber)
    {
        DocumentLine line = editor.Document.GetLineByNumber(lineNumber);
        VisualLine visual = editor.TextArea.TextView.GetOrConstructVisualLine(line);
        return [.. visual.TextLines.SelectMany(textLine => textLine.GetIndexedGlyphRuns() ?? []).Select(indexed => indexed.GlyphRun)];
    }

    private static GlyphRun RunHolding(GlyphRun[] runs, char character) =>
        runs.First(run => run.Characters.Contains(character));

    /// <summary>Whether any character of <paramref name="op"/> drew something other
    /// than its own plain glyph. WPF can split one operator across glyph runs (it
    /// puts the <c>-</c> of <c>-&gt;</c> in a run of its own), so this reads the
    /// line's characters across all of them.</summary>
    private static bool Joined(GlyphRun[] runs, string op)
    {
        var line = new List<(char Character, bool Plain)>();
        foreach (GlyphRun run in runs)
        {
            for (int index = 0; index < run.Characters.Count; index++)
            {
                char character = run.Characters[index];
                ushort drawn = run.GlyphIndices[run.ClusterMap?[index] ?? index];
                line.Add((character, drawn == run.GlyphTypeface.CharacterToGlyphMap[character]));
            }
        }

        int at = new string([.. line.Select(glyph => glyph.Character)]).IndexOf(op, StringComparison.Ordinal);
        Assert.True(at >= 0, $"the line's glyph runs don't hold {op}.");
        return line.Skip(at).Take(op.Length).Any(glyph => !glyph.Plain);
    }

    private sealed class WeightAndStyle(int boldStart, int italicStart, int end) : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            ChangeLinePart(boldStart, italicStart - 1, element =>
            {
                Typeface face = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily, face.Style, FontWeights.Bold, face.Stretch));
            });
            ChangeLinePart(italicStart, end, element =>
            {
                Typeface face = element.TextRunProperties.Typeface;
                element.TextRunProperties.SetTypeface(new Typeface(face.FontFamily, FontStyles.Italic, face.Weight, face.Stretch));
            });
        }
    }

    private static void RegisterPackScheme() =>
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

    private static T InRealWindow<T>(bool dark, bool highContrast, Func<Window, T> body) =>
        StaThread.Run(() =>
        {
            RegisterPackScheme();
            var window = new Window
            {
                Width = 600,
                Height = 300,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10_000,
                Top = -10_000,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(dark ? SlateTheme.Dark : SlateTheme.Light, highContrast))
            {
                window.Resources.MergedDictionaries.Add(layer);
            }

            try
            {
                window.Show();
                Pump();
                return body(window);
            }
            finally
            {
                window.Close();
            }
        });

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
