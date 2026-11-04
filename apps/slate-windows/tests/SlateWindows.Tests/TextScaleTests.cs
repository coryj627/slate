// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Xml.Linq;
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
    /// on purpose is cut off, at the size the app opens at and at a narrower one. Trimmed
    /// text is allowed where it already trims.</summary>
    [Theory]
    [InlineData(1120, 720)]
    [InlineData(1000, 700)]
    public void NoShellTextIsClippedAtTheLargestSize(double width, double height) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "---\nkey: value\n---\n# Lecture\n\nA line of text.\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                host.Settle();
                return host.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { Properties.Rows.Count: > 0 };
            },
            TimeSpan.FromSeconds(10));
        host.Shell.Width = width;
        host.Shell.Height = height;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        host.Settle();

        string[] cut = CutText((FrameworkElement)host.Shell.Content);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    /// <summary>Every sheet, opened by hand, so what it shows is what it declares. The
    /// launched pass reads their contents.</summary>
    [Fact]
    public void NoSheetTextIsCutAtTheLargestSize() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();

        FrameworkElement[] sheets = [.. ShownShell.Descendants(host.Shell)
            .OfType<FrameworkElement>()
            .Where(System.Windows.Automation.AutomationProperties.GetIsDialog)];
        Assert.True(sheets.Length >= 10, $"only {sheets.Length} sheets were found.");
        var cut = new List<string>();
        foreach (FrameworkElement sheet in sheets)
        {
            FrameworkElement[] opened = Open(sheet);
            host.Settle();
            host.Settle();
            cut.AddRange(CutText(sheet).Select(line => $"{System.Windows.Automation.AutomationProperties.GetAutomationId(sheet)}: {line}"));
            foreach (FrameworkElement hidden in opened)
            {
                hidden.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            }
        }

        Assert.True(cut.Count == 0, $"{cut.Count} cut:\n" + string.Join('\n', cut));
    });

    /// <summary>Quick Open's rows gave a name a fixed 180, and its keys a line that
    /// couldn't wrap.</summary>
    [Fact]
    public void QuickOpenKeepsAResultsNameAndItsKeysWhole() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName("QuickSwitcherOverlay");
        _ = Open(sheet);
        foreach (TextBlock text in ShownShell.Descendants(sheet).OfType<TextBlock>())
        {
            text.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }

        ListBox list = ShownShell.Descendants(sheet).OfType<ListBox>().First();
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, new[] { new QuickRow("Markdown features", "reference/Markdown features.md") });
        host.Settle();
        host.Settle();

        TextBlock name = ShownShell.Descendants(sheet).OfType<TextBlock>().First(text => text.Text == "Markdown features");
        Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
        Assert.True(name.ActualWidth + 1 >= MeasuredWidth(name), $"the name is {name.ActualWidth:F0} wide and needs {MeasuredWidth(name):F0}.");
        string[] cut = CutText(sheet);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    private sealed record QuickRow(string DisplayName, string Path);

    /// <summary>The palette's rows scrolled sideways, its label couldn't wrap, and its
    /// reason was held to two lines of 100% text.</summary>
    [Fact]
    public void ThePalettesRowsKeepTheirTextWhole() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var list = (ListBox)host.Shell.FindName("CommandPaletteResultsList");
        var sheet = (FrameworkElement)System.Windows.Media.VisualTreeHelper.GetParent(System.Windows.Media.VisualTreeHelper.GetParent(list));
        for (DependencyObject? step = list; step is not null && !System.Windows.Automation.AutomationProperties.GetIsDialog(step); step = System.Windows.Media.VisualTreeHelper.GetParent(step))
        {
            sheet = step as FrameworkElement ?? sheet;
        }

        sheet = (FrameworkElement)System.Windows.Media.VisualTreeHelper.GetParent(sheet);
        _ = Open(sheet);
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, new[]
        {
            new PaletteRow([new PaletteSegment("Focus Previous Region", false)], "Ctrl+Shift+F6", true, "This command is not available right now, and this reason runs on."),
        });
        host.Settle();
        host.Settle();

        Assert.Equal(ScrollBarVisibility.Disabled, ScrollViewer.GetHorizontalScrollBarVisibility(list));
        TextBlock reason = ShownShell.Descendants(sheet).OfType<TextBlock>().First(text => text.Text.StartsWith("This command", StringComparison.Ordinal));
        Assert.True(double.IsPositiveInfinity(reason.MaxHeight), $"the reason is held to {reason.MaxHeight}.");
        string[] cut = CutText(sheet);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    private sealed record PaletteSegment(string Text, bool IsMatch);

    private sealed record PaletteRow(PaletteSegment[] LabelSegments, string HotkeyHint, bool IsUnavailable, string DisabledReason);

    /// <summary>A list that scrolls sideways measures its rows at any width, so nothing
    /// in them wraps or trims, and what doesn't fit is cut at the sheet's edge.</summary>
    [Fact]
    public void ASheetsListsDontScrollSideways()
    {
        XDocument shell = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);
        XElement[] lists = [.. shell.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.IsDialog") == "True")
            .SelectMany(sheet => sheet.Descendants())
            .Where(element => element.Name.LocalName.EndsWith("ListBox", StringComparison.Ordinal))];
        string[] sideways = [.. lists
            .Where(list => (string?)list.Attribute("ScrollViewer.HorizontalScrollBarVisibility") != "Disabled")
            .Select(list => $"line {((System.Xml.IXmlLineInfo)list).LineNumber}")];

        Assert.True(lists.Length >= 6, $"only {lists.Length} lists were found in the sheets.");
        Assert.True(sideways.Length == 0, "Lists that scroll sideways: " + string.Join(", ", sideways));
    }

    /// <summary>A row's width to stack at is text, so it grows with the text, and a change
    /// in size alone has to reach it.</summary>
    [Fact]
    public void APropertyRowStacksWhenTheTextNeedsTheRoom() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "---\nkey: value\n---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                host.Settle();
                return host.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { Properties.Rows.Count: > 0 };
            },
            TimeSpan.FromSeconds(10));
        host.Settle();
        PropertyRowGrid row = ShownShell.Descendants(host.Shell).OfType<PropertyRowGrid>().Single();
        Assert.Equal(0, Grid.GetRow(row.Children[1]));

        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        host.Settle();
        Assert.Equal(1, Grid.GetRow(row.Children[1]));

        TypeRamp.Apply(RampOf(host.Shell), 1);
        host.Settle();
        host.Settle();
        Assert.Equal(0, Grid.GetRow(row.Children[1]));
    });

    /// <summary>A row's width doesn't have to change for its text to outgrow it.</summary>
    [Fact]
    public void ASizeChangeAloneReachesAPropertyRow() => OnOwnThread(() =>
    {
        var row = new PropertyRowGrid { Width = 500 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition());
        for (int index = 0; index < 4; index++)
        {
            _ = row.Children.Add(new Border());
        }

        var window = new Window
        {
            Content = row,
            Width = 800,
            Height = 200,
            Left = -20_000,
            Top = -20_000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        try
        {
            window.FontSize = TypeRamp.Body.Size;
            window.Show();
            PumpedDispatcher.Drain();
            Assert.Equal(0, Grid.GetRow(row.Children[1]));

            window.FontSize = TypeRamp.Body.Size * 2.25;
            PumpedDispatcher.Drain();
            Assert.Equal(1, Grid.GetRow(row.Children[1]));

            window.FontSize = TypeRamp.Body.Size;
            PumpedDispatcher.Drain();
            Assert.Equal(0, Grid.GetRow(row.Children[1]));
        }
        finally
        {
            window.Close();
            PumpedDispatcher.Drain();
        }
    });

    /// <summary>A hosted fact can't build a <see cref="ThemeManager"/>, which needs an
    /// Application, so this reaches its handler on one built without its constructor. That
    /// the constructor subscribes it is the owner's live check.</summary>
    [Fact]
    public void TheThemeManagerWritesAChangeIntoItsRamp() => WithService(1.0, (service, factor) =>
    {
        ResourceDictionary ramp = TypeRamp.DictionaryFor();
        var manager = (ThemeManager)RuntimeHelpers.GetUninitializedObject(typeof(ThemeManager));
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(ThemeManager).GetField("_resources", Private)!.SetValue(manager, new[] { ramp });
        typeof(ThemeManager).GetField("_textScale", Private)!.SetValue(manager, service);
        factor.Value = 2;
        service.RefreshForTests();

        typeof(ThemeManager).GetMethod("OnTextScaleChanged", Private)!.Invoke(manager, null);

        Assert.Equal(TypeRamp.Body.Size * 2, (double)ramp[TypeRamp.Body.SizeKey]);
    });

    /// <summary>The header, its buttons and a row left a note none of its text at 225% in
    /// the window the app opens at.</summary>
    [Fact]
    public void ThePropertiesCardLeavesTheNoteItsText() => OnOwnThread(() =>
    {
        string keys = string.Concat(Enumerable.Range(1, 12).Select(index => $"key{index}: value\n"));
        using var host = new ShownShell(("Lecture.md", $"---\n{keys}---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        System.Threading.SpinWait.SpinUntil(
            () =>
            {
                host.Settle();
                return host.Workspace.ActiveGroup.ActiveTab is WorkspaceTabViewModel { Properties.Rows.Count: > 0 };
            },
            TimeSpan.FromSeconds(10));
        host.Settle();
        host.Settle();

        FrameworkElement card = host.Body("PropertiesHeader");
        ICSharpCode.AvalonEdit.TextEditor note = ShownShell.Descendants(host.Shell).OfType<ICSharpCode.AvalonEdit.TextEditor>().First(editor => editor.IsVisible);
        Assert.True(
            note.ActualHeight >= 3 * TypeRamp.Body.LineHeight * 2.25,
            $"the note has {note.ActualHeight:F0} of height under a {card.ActualHeight:F0} card.");
    });

    /// <summary>At 225% in a short window the toolbar alone was taller than the pane.</summary>
    [Fact]
    public void TheFilesTreeKeepsRoomUnderItsToolbar() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1000;
        host.Shell.Height = 700;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        host.Settle();

        FrameworkElement tree = host.Body("FilesTree");
        Assert.True(tree.ActualHeight >= 40, $"the tree has {tree.ActualHeight:F0} of height.");
    });

    /// <summary>A rail that fits its labels took two thirds of a 320 pane at 225%, and
    /// the leaf beside it wrapped a letter at a time.</summary>
    [Fact]
    public void TheRailGoesUnderTheLeafWhenTheTextNeedsTheRoom() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        host.Settle();
        host.Settle();
        var rail = (ListBox)host.Shell.FindName("RightPaneLeavesList");
        var leaves = (Grid)host.Shell.FindName("RightPaneLeafHost");
        Assert.True(rail.TranslatePoint(default, leaves).X > 0, "at 100% the rail isn't beside the leaf.");

        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        host.Settle();
        Point under = rail.TranslatePoint(default, leaves);
        Assert.Equal(0, under.X, 1);
        Assert.True(under.Y > 0, "at 225% the rail isn't under the leaf.");
        Assert.Equal(leaves.ActualWidth, rail.ActualWidth, 1);
        Assert.True(rail.ActualHeight <= (leaves.ActualHeight * 0.4) + 1, $"the rail takes {rail.ActualHeight:F0} of {leaves.ActualHeight:F0}.");

        TypeRamp.Apply(RampOf(host.Shell), 1);
        host.Settle();
        host.Settle();
        Assert.True(rail.TranslatePoint(default, leaves).X > 0, "the rail didn't come back beside the leaf.");
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

    /// <summary>A date property's calendar, the one its picker really opens. Its heading,
    /// weekday labels and date buttons follow the ramp in every view, nothing is cut off,
    /// and its navigation glyphs stay their size.</summary>
    [Theory]
    [InlineData(CalendarMode.Month, 1.0, 50)]
    [InlineData(CalendarMode.Year, 1.0, 13)]
    [InlineData(CalendarMode.Decade, 1.0, 13)]
    [InlineData(CalendarMode.Month, 2.25, 50)]
    [InlineData(CalendarMode.Year, 2.25, 13)]
    [InlineData(CalendarMode.Decade, 2.25, 13)]
    public void TheOpenCalendarFollowsTheFactor(CalendarMode mode, double factor, int atLeast) => OnOwnThread(() =>
    {
        var picker = new DatePicker { SelectedDate = new DateTime(2026, 11, 4) };
        var window = new Window
        {
            Content = picker,
            Width = 400,
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
            TypeRamp.Apply(RampOf(window), factor);

            // A popup on a hosted runner opens without laying out its children.
            var calendar = (Calendar)((Popup)picker.Template.FindName("PART_Popup", picker)).Child;
            calendar.DisplayMode = mode;
            calendar.Measure(new Size(4000, 4000));
            calendar.Arrange(new Rect(calendar.DesiredSize));
            calendar.UpdateLayout();
            PumpedDispatcher.Drain();
            calendar.Measure(new Size(4000, 4000));
            calendar.Arrange(new Rect(calendar.DesiredSize));
            calendar.UpdateLayout();

            TextBlock[] texts = [.. ShownShell.Descendants(calendar).OfType<TextBlock>().Where(text => !string.IsNullOrEmpty(text.Text))];
            TextBlock[] glyphs = [.. texts.Where(text => text.Text is "" or "")];
            TextBlock[] labels = [.. texts.Except(glyphs).Where(text => InShownView(text, calendar))];

            Assert.Equal(2, glyphs.Length);
            Assert.All(glyphs, glyph => Assert.Equal(8, glyph.FontSize));
            Assert.True(labels.Length >= atLeast, $"only {labels.Length} labels in the {mode} view.");
            Assert.All(labels, label => Assert.True(
                label.FontSize == TypeRamp.Body.Size * factor,
                $"\"{label.Text}\" is {label.FontSize} at factor {factor}."));
            string[] clipped = [.. labels
                .Where(label => LayoutInformation.GetLayoutClip(label) is not null)
                .Select(label => $"\"{label.Text}\" at {label.FontSize}")];
            Assert.True(clipped.Length == 0, $"{clipped.Length} clipped:\n" + string.Join('\n', clipped));
        }
        finally
        {
            window.Close();
            PumpedDispatcher.Drain();
        }
    });

    /// <summary>Text cut off by its own slot, by anything above it short of a scroll
    /// viewer, whose cut is its job, or by the edge of <paramref name="root"/>. Text that
    /// trims says so itself.</summary>
    private static string[] CutText(FrameworkElement root) =>
        [.. ShownShell.Descendants(root)
            .OfType<TextBlock>()
            .Where(text => text.IsVisible && text.TextTrimming == TextTrimming.None && !string.IsNullOrEmpty(text.Text))
            .Select(text => (Text: text, Why: WhyCut(text, root)))
            .Where(found => found.Why is not null)
            .Select(found => $"\"{found.Text.Text}\" at {found.Text.FontSize:F1}, by {found.Why}")];

    private static string? WhyCut(TextBlock text, FrameworkElement root)
    {
        for (DependencyObject? step = text; step is not null; step = System.Windows.Media.VisualTreeHelper.GetParent(step))
        {
            // Its arrows are glyphs, and a short bar squeezes them.
            if (step is ScrollBar)
            {
                return null;
            }
        }

        if (LayoutInformation.GetLayoutClip(text) is not null)
        {
            return "its own slot";
        }

        var mine = new Rect(0, 0, text.ActualWidth, text.ActualHeight);
        for (DependencyObject? step = System.Windows.Media.VisualTreeHelper.GetParent(text);
            step is not null && !ReferenceEquals(step, root);
            step = System.Windows.Media.VisualTreeHelper.GetParent(step))
        {
            if (step is ScrollViewer or ScrollContentPresenter)
            {
                return null;
            }

            if (step is FrameworkElement ancestor
                && LayoutInformation.GetLayoutClip(ancestor) is { } clip
                && !clip.Bounds.Contains(text.TransformToAncestor(ancestor).TransformBounds(mine)))
            {
                return $"{ancestor.GetType().Name} {ancestor.Name}";
            }
        }

        Rect bounds = text.TransformToAncestor(root).TransformBounds(mine);
        return bounds.Right > root.ActualWidth + 0.5 || bounds.Bottom > root.ActualHeight + 0.5 || bounds.Left < -0.5 || bounds.Top < -0.5
            ? $"the edge, at {bounds} of {root.ActualWidth:F0}x{root.ActualHeight:F0}"
            : null;
    }

    /// <summary>A sheet and what hides it, shown by hand; what was hidden comes back to
    /// hide again.</summary>
    private static FrameworkElement[] Open(FrameworkElement sheet)
    {
        var opened = new List<FrameworkElement>();
        for (DependencyObject? step = sheet; step is not null; step = System.Windows.Media.VisualTreeHelper.GetParent(step))
        {
            if (step is FrameworkElement { Visibility: not Visibility.Visible } hidden)
            {
                opened.Add(hidden);
                hidden.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            }
        }

        return [.. opened];
    }

    private static double MeasuredWidth(TextBlock text)
    {
        var copy = new TextBlock { Text = text.Text, FontSize = text.FontSize, FontFamily = text.FontFamily, FontWeight = text.FontWeight };
        copy.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return copy.DesiredSize.Width;
    }

    /// <summary>The month grid and the year grid share a cell, and one is hidden.</summary>
    private static bool InShownView(DependencyObject element, DependencyObject root)
    {
        for (DependencyObject? step = element; step is not null && !ReferenceEquals(step, root); step = System.Windows.Media.VisualTreeHelper.GetParent(step))
        {
            if (step is UIElement { Visibility: not Visibility.Visible })
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A fact's thread, with its dispatcher shut down before the thread ends.
    /// Windows' text services (msctf) tear down at the thread's exit by calling managed
    /// code, after the runtime has let the thread go, and that ends the test host; a dispatcher's
    /// shutdown takes them down first. These facts have closed their windows by then, so
    /// it hands away no foreground, which is what <see cref="StaThread"/> warns of.</summary>
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
