// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using SlateWindows.Canvas;
using SlateWindows.Graph;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

/// <summary>DS-5: Windows Text size scales the whole shell, live, from one service. The
/// registry itself is the owner's check, because an agent doesn't change that setting,
/// so these drive the ramp and the service's read.</summary>
public sealed partial class TextScaleTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.25)]
    public void TheRampFollowsTheFactor(double factor) => OnOwnThread(() =>
    {
        ResourceDictionary ramp = TypeRamp.DictionaryFor();
        TypeRamp.Apply(ramp, factor);

        Assert.All(TypeRamp.Steps, step =>
        {
            Assert.Equal(step.Size * factor, (double)ramp[step.SizeKey]);
            Assert.Equal(step.LineHeight * factor, (double)ramp[step.LineHeightKey]);
        });
        Assert.All(TypeRamp.ReaderSteps, step =>
        {
            Assert.Equal(step.Size * factor, (double)ramp[step.SizeKey]);
            Assert.Equal(step.LineHeight * factor, (double)ramp[step.LineHeightKey]);
        });
    });

    /// <summary>A hosted test merges what <c>DictionariesFor</c> gives it, so it can't
    /// depend on the desktop's setting. At 225% about 29 shell tests used to fail.</summary>
    [Fact]
    public void AHostedTestIgnoresTheDesktopsSetting() => OnOwnThread(() =>
    {
        // Registers the pack: scheme without constructing an Application.
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
        ResourceDictionary ramp = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false)[0];
        Assert.Equal(TypeRamp.Body.Size, (double)ramp[TypeRamp.Body.SizeKey]);
    });

    [Fact]
    public void ALiveChangeKeepsFocusAndReflows() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        PumpedDispatcher.PumpUntilDrained(host.Workspace.SeedWorkForTests);
        host.Settle();
        ListBoxItem? found = null;
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                PumpedDispatcher.Drain();
                found = ShownShell.Descendants(host.Shell).OfType<ListBoxItem>().FirstOrDefault(item => item.IsVisible);
                return found is not null;
            },
            TimeSpan.FromSeconds(10));
        ListBoxItem row = Assert.IsType<ListBoxItem>(found, exactMatch: false);
        row.Focus();
        host.Settle();
        object? focused = System.Windows.Input.FocusManager.GetFocusedElement(host.Shell);
        Assert.Same(row, focused);
        double before = row.ActualHeight;

        ResourceDictionary ramp = RampOf(host.Shell);
        TypeRamp.Apply(ramp, 2);
        host.Settle();
        Assert.Same(row, System.Windows.Input.FocusManager.GetFocusedElement(host.Shell));
        Assert.True(row.ActualHeight > before, $"the row stayed {row.ActualHeight:F1} tall at twice the size.");

        TypeRamp.Apply(ramp, 1);
        host.Settle();
        Assert.Same(row, System.Windows.Input.FocusManager.GetFocusedElement(host.Shell));
        Assert.Equal(before, row.ActualHeight, 1);
    });

    [Fact]
    public void ALiveChangeDoesNotRebuildTheReadingDocument() => OnOwnThread(() =>
    {
        var surface = new ReadingSurface();
        var window = new Window
        {
            Content = surface,
            Width = 1400,
            Height = 600,
            Left = -20_000,
            Top = -20_000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false))
        {
            window.Resources.MergedDictionaries.Add(layer);
        }

        try
        {
            window.Show();
            surface.Document.Blocks.Add(new Paragraph(new Run(string.Concat(Enumerable.Repeat("Words on a line. ", 40)))));
            surface.UpdateLayout();
            PumpedDispatcher.Drain();
            FlowDocument document = surface.Document;
            surface.CaretPosition = document.ContentStart.GetPositionAtOffset(25)!;
            int caret = document.ContentStart.GetOffsetToPosition(surface.CaretPosition);
            double padding = document.PagePadding.Left;

            TypeRamp.Apply(RampOf(window), 1.5);
            surface.UpdateLayout();
            PumpedDispatcher.Drain();

            Assert.Same(document, surface.Document);
            Assert.Equal(caret, document.ContentStart.GetOffsetToPosition(surface.CaretPosition));
            Assert.Equal(TypeRamp.ReaderBody.Size * 1.5, document.FontSize);
            Assert.True(document.PagePadding.Left < padding, "the measure didn't follow the body's size.");
        }
        finally
        {
            window.Close();
            PumpedDispatcher.Drain();
        }
    });

    /// <summary>The preference and its sentence stay in points the reader chose; what's
    /// drawn is that times the factor.</summary>
    [Fact]
    public void TheEditorCombinesItsPreferenceAndTheFactor() => WithService(1.5, (service, factor) =>
    {
        var announced = new List<string>();
        using var preferences = new EditorPreferencesViewModel(
            announcement => announced.Add(uniffi.slate_uniffi.SlateUniffiMethods.A11yRender(announcement).Text),
            new FakeEditorSpellingService());
        var raised = new List<string?>();
        preferences.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        preferences.FontSize = 16;
        Assert.Equal(16, preferences.FontSize);
        Assert.Equal(24, preferences.EffectiveFontSize);
        Assert.Contains("Editor text size 16 points.", announced);

        factor.Value = 2;
        raised.Clear();
        service.RefreshForTests();
        Assert.Equal(32, preferences.EffectiveFontSize);
        Assert.Contains(nameof(EditorPreferencesViewModel.EffectiveFontSize), raised);
    });

    /// <summary>The numbers under a menu's check and chevron are the only literal sizes
    /// the ramp's census lets by, so they have to be what they say they are.</summary>
    [Fact]
    public void IconGlyphsKeepTheirSize()
    {
        string root = SourceText.ShellSourceRoot();
        var scaled = new List<string>();
        int glyphs = 0;
        foreach (string file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)[0].Equals("bin", StringComparison.Ordinal)
                && !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)[0].Equals("obj", StringComparison.Ordinal)))
        {
            foreach (Match tag in XamlTag().Matches(File.ReadAllText(file)))
            {
                if (!tag.Value.Contains("SymbolThemeFontFamily", StringComparison.Ordinal))
                {
                    continue;
                }

                glyphs++;
                if (!LiteralFontSize().IsMatch(tag.Value))
                {
                    scaled.Add($"{Path.GetFileName(file)}: {tag.Value[..Math.Min(80, tag.Value.Length)]}");
                }
            }
        }

        Assert.True(glyphs > 0, "the census found no glyphs, so it isn't reading the source.");
        Assert.True(scaled.Count == 0, "A glyph without a literal size:\n" + string.Join('\n', scaled));
    }

    /// <summary>What a reader at the largest size most needs: nothing that doesn't trim
    /// on purpose is cut off by its slot. Trimmed text is allowed where it already trims.</summary>
    [Fact]
    public void NoShellTextIsClippedAtTheLargestSize() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n\nA line of text.\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        host.Shell.Width = 1600;
        host.Shell.Height = 1000;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();

        string[] clipped = [.. ShownShell.Descendants(host.Shell)
            .OfType<TextBlock>()
            .Where(text => text.IsVisible && text.TextTrimming == TextTrimming.None && !string.IsNullOrEmpty(text.Text))
            .Where(text => LayoutInformation.GetLayoutClip(text) is not null)
            .Select(text => $"\"{text.Text}\" at {text.FontSize:F1}")];
        Assert.True(clipped.Length == 0, $"{clipped.Length} clipped:\n" + string.Join('\n', clipped));
    });

    [Fact]
    public void AHostedViewWithNoServiceDrawsAtOne() => OnOwnThread(() =>
    {
        Assert.Null(TextScaleService.Current);
        Assert.Equal(1.0, TextScaleService.CurrentFactor);
        var canvas = new CanvasRendererView();
        var diagram = new GraphDiagramView();
        canvas.AttachTextScale();
        Assert.False(canvas.IsAttachedToTextScaleForTests, "a canvas attached to nothing.");
        Assert.Equal(1.0, diagram.TextScaleFactorForTests);
        Assert.False(diagram.IsAttachedToTextScaleForTests, "a diagram attached to nothing.");
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClosingOneViewLeavesScalingRunningForTheOthers(bool closeTheCanvas) => WithService(1.0, (service, factor) =>
    {
        var canvas = new CanvasRendererView();
        var diagram = new GraphDiagramView();
        canvas.AttachTextScale();
        _ = diagram.TextScaleFactorForTests;
        Assert.True(canvas.IsAttachedToTextScaleForTests && diagram.IsAttachedToTextScaleForTests);

        if (closeTheCanvas)
        {
            canvas.Shutdown();
        }
        else
        {
            diagram.Shutdown();
        }

        Assert.Equal(!closeTheCanvas, canvas.IsAttachedToTextScaleForTests);
        Assert.Equal(closeTheCanvas, diagram.IsAttachedToTextScaleForTests);

        int heard = 0;
        service.Changed += () => heard++;
        factor.Value = 1.5;
        service.RefreshForTests();
        Assert.Equal(1, heard);
        Assert.Equal(1.5, service.Factor);
        Assert.Equal(1.5, closeTheCanvas ? diagram.TextScaleFactorForTests : TextScaleService.CurrentFactor);
    });

    /// <summary>The service outlives every view, so its event mustn't hold a closed one.</summary>
    [Fact]
    public void AClosedViewIsReleased() => WithService(1.0, (service, _) =>
    {
        (WeakReference canvas, WeakReference diagram) = OpenAndCloseViews();
        for (int attempt = 0; attempt < 5 && (canvas.IsAlive || diagram.IsAlive); attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            PumpedDispatcher.Drain();
        }

        Assert.False(canvas.IsAlive, "the service holds a closed canvas.");
        Assert.False(diagram.IsAlive, "the service holds a closed diagram.");
        GC.KeepAlive(service);
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Canvas, WeakReference Diagram) OpenAndCloseViews()
    {
        var canvas = new CanvasRendererView();
        var diagram = new GraphDiagramView();
        canvas.AttachTextScale();
        _ = diagram.TextScaleFactorForTests;
        canvas.Shutdown();
        diagram.Shutdown();
        return (new WeakReference(canvas), new WeakReference(diagram));
    }

    /// <summary>A view that was out when the size changed heard nothing, and a canvas
    /// doesn't leave its window with its engine shut down, so it can come back.</summary>
    [Fact]
    public void AViewComingBackUsesTheCurrentFactor() => WithService(1.0, (service, factor) =>
    {
        var canvas = new CanvasRendererView();
        var holder = new Grid { Children = { canvas } };
        var window = new Window
        {
            Content = holder,
            Width = 300,
            Height = 200,
            Left = -20_000,
            Top = -20_000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        try
        {
            window.Show();
            PumpedDispatcher.Drain();
            Assert.True(canvas.IsAttachedToTextScaleForTests, "loading didn't attach the canvas.");

            holder.Children.Remove(canvas);
            PumpedDispatcher.Drain();
            Assert.False(canvas.IsAttachedToTextScaleForTests, "unloading left the canvas attached.");

            factor.Value = 2;
            service.RefreshForTests();
            int revision = service.Revision;

            holder.Children.Add(canvas);
            PumpedDispatcher.Drain();
            Assert.True(canvas.IsAttachedToTextScaleForTests, "coming back didn't attach the canvas.");
            Assert.Equal(revision, canvas.TextScaleRevisionForTests);
        }
        finally
        {
            window.Close();
            PumpedDispatcher.Drain();
        }

        var diagram = new GraphDiagramView();
        _ = diagram.TextScaleFactorForTests;
        diagram.Shutdown();
        factor.Value = 1.5;
        service.RefreshForTests();
        Assert.Equal(1.5, diagram.TextScaleFactorForTests);
        Assert.True(diagram.IsAttachedToTextScaleForTests, "a redraw didn't attach the diagram again.");
    });

    /// <summary>The rows' reserve holds four of the editor's lines and the chrome around
    /// it, which all grow with text, so the reserve has to as well.</summary>
    [Theory]
    [InlineData(1.0, 600, 280)]
    [InlineData(1.0, 416, 96)]
    [InlineData(2.0, 1000, 280)]
    [InlineData(2.0, 800, 160)]
    [InlineData(2.0, 416, 80)]
    public void ThePropertyRowsReserveFollowsTheFactor(double factor, double pane, double rows) => OnOwnThread(() =>
    {
        var converter = new CappedShareConverter { Reserve = 320, Floor = 80, Cap = 280 };
        object result = ((System.Windows.Data.IMultiValueConverter)converter).Convert(
            [pane, TypeRamp.Body.Size * factor],
            typeof(double),
            null!,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(rows, (double)result);
    });

    /// <summary>A drawn formula is geometry at a fixed 20, so it has to scale with the
    /// text around it, without the document being rebuilt.</summary>
    [Fact]
    public void ADrawnFormulaFollowsTheFactor() => OnOwnThread(() =>
    {
        var drawing = new System.Windows.Shapes.Path
        {
            Data = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, 100, 20)),
        };
        var element = new ReadingMathElement("speech", "<math></math>", "x", string.Empty) { Content = drawing };
        var window = new Window
        {
            Content = new Grid { Children = { element } },
            Width = 800,
            Height = 200,
            Left = -20_000,
            Top = -20_000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        foreach (ResourceDictionary layer in ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false))
        {
            window.Resources.MergedDictionaries.Add(layer);
        }

        try
        {
            window.Show();
            PumpedDispatcher.Drain();
            Assert.Equal(100, drawing.TransformToAncestor(window).TransformBounds(new Rect(0, 0, 100, 20)).Width, 1);
            TypeRamp.Apply(RampOf(window), 2);
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.Equal(200, drawing.TransformToAncestor(window).TransformBounds(new Rect(0, 0, 100, 20)).Width, 1);
        }
        finally
        {
            window.Close();
            PumpedDispatcher.Drain();
        }
    });

    /// <summary>A fact's thread, with its dispatcher shut down before the thread ends. A window or an editor's text services left to the thread's exit can run managed code after the runtime has let the thread go, and that takes the test host down.</summary>
    private static void OnOwnThread(Action body) => StaThread.Run(() =>
    {
        try
        {
            body();
        }
        finally
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
        }
    });

    private sealed class Box
    {
        public double Value { get; set; }
    }

    private static void WithService(double start, Action<TextScaleService, Box> body) => OnOwnThread(() =>
    {
        var factor = new Box { Value = start };
        using var service = new TextScaleService(() => factor.Value);
        TextScaleService? previous = TextScaleService.Current;
        TextScaleService.Current = service;
        try
        {
            body(service, factor);
        }
        finally
        {
            TextScaleService.Current = previous;
        }
    });

    private static ResourceDictionary RampOf(FrameworkElement owner) =>
        owner.Resources.MergedDictionaries.First(layer => layer.Contains(TypeRamp.Body.SizeKey));

    [GeneratedRegex(@"<[A-Za-z][^<>]*>", RegexOptions.Singleline)]
    private static partial Regex XamlTag();

    [GeneratedRegex(@"\bFontSize=""\d")]
    private static partial Regex LiteralFontSize();
}
