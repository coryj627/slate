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

    /// <summary>Every sheet, opened by hand, so what it shows is what it declares, in the
    /// window the app opens at and at its minimum height. Past that a sheet scrolls, and
    /// keys reach what's scrolled off. The launched pass reads their contents.</summary>
    [Theory]
    [InlineData(720)]
    [InlineData(480)]
    public void NoSheetTextIsCutAtTheLargestSize(double height) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = height;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();

        FrameworkElement[] sheets = ShownShell.Descendants(host.Shell)
            .OfType<FrameworkElement>()
            .Where(System.Windows.Automation.AutomationProperties.GetIsDialog).ToArray();
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

    /// <summary>A sheet's buttons can take their text from what it shows, so a hosted sheet
    /// has none to measure. "Walk through citations" ran off the start of its sheet, beside
    /// Done. Each button with no text here gets text that long.</summary>
    [Fact]
    public void ASheetsButtonsHaveRoomForTheirBoundText() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();

        FrameworkElement[] sheets = ShownShell.Descendants(host.Shell)
            .OfType<FrameworkElement>()
            .Where(System.Windows.Automation.AutomationProperties.GetIsDialog).ToArray();
        var cut = new List<string>();
        int filled = 0;
        foreach (FrameworkElement sheet in sheets)
        {
            FrameworkElement[] opened = Open(sheet);
            host.Settle();
            host.Settle();
            foreach (Button button in ShownShell.Descendants(sheet).OfType<Button>().Where(button => button.Content is null or ""))
            {
                button.SetCurrentValue(ContentControl.ContentProperty, "Walk through citations");
                filled++;
            }

            host.Settle();
            host.Settle();
            cut.AddRange(CutText(sheet).Select(line => $"{System.Windows.Automation.AutomationProperties.GetAutomationId(sheet)}: {line}"));
            foreach (FrameworkElement hidden in opened)
            {
                hidden.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            }
        }

        Assert.True(filled >= 3, $"only {filled} buttons had no text.");
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

    /// <summary>When ranking fails the summary is a sentence, and it took the row from the
    /// keys beside it, which wrapped a word at a time and left the results no room.</summary>
    [Theory]
    [InlineData(720)]
    [InlineData(480)]
    public void QuickOpensErrorSummaryLeavesItsKeysAndResultsRoom(double height) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = height;
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
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, Enumerable.Range(1, 5).Select(index => new QuickRow($"Note {index}", $"Note {index}.md")).ToArray());
        TextBlock keys = ShownShell.Descendants(sheet).OfType<TextBlock>().First(text => text.Text.StartsWith("Enter open", StringComparison.Ordinal));
        TextBlock summary = ShownShell.Descendants(System.Windows.Media.VisualTreeHelper.GetParent(keys)).OfType<TextBlock>().First();
        summary.SetCurrentValue(TextBlock.TextProperty, "Quick Open could not rank files.");
        host.Settle();
        host.Settle();

        Assert.True(keys.ActualHeight <= keys.FontSize * 4, $"the keys are {keys.ActualHeight:F0} tall and {keys.ActualWidth:F0} wide.");
        Assert.True(list.ActualHeight >= keys.FontSize * 2, $"the results have {list.ActualHeight:F0}.");
        string[] cut = CutText(sheet);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    /// <summary>Two notes with one long name differ only by their paths, so a name can't
    /// take the whole row. At 100% its column is the 180 it always was.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.25)]
    public void AQuickOpenNameLeavesItsPathRoom(double factor) => OnOwnThread(() =>
    {
        const string LongName = "A very long file name that goes on and on and on and on and on and on and on";
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), factor);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName("QuickSwitcherOverlay");
        _ = Open(sheet);
        ListBox list = ShownShell.Descendants(sheet).OfType<ListBox>().First();
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, new[] { new QuickRow(LongName, "reference/one/two.md") });
        host.Settle();
        host.Settle();

        TextBlock[] texts = ShownShell.Descendants(list).OfType<TextBlock>().ToArray();
        TextBlock name = texts.First(text => text.Text == LongName);
        TextBlock path = texts.First(text => text.Text == "reference/one/two.md");
        Assert.Equal(180 * factor, name.ActualWidth, 1);
        Assert.True(path.ActualWidth >= 100, $"the path has {path.ActualWidth:F0} beside a name of {name.ActualWidth:F0}.");
        Assert.True(
            path.TransformToAncestor(list).TransformBounds(new Rect(0, 0, path.ActualWidth, path.ActualHeight)).Right <= list.ActualWidth + 0.5,
            "the path runs past the list.");
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
        XElement[] lists = shell.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.IsDialog") == "True")
            .SelectMany(sheet => sheet.Descendants())
            .Where(element => element.Name.LocalName.EndsWith("ListBox", StringComparison.Ordinal)).ToArray();
        string[] sideways = lists
            .Where(list => (string?)list.Attribute("ScrollViewer.HorizontalScrollBarVisibility") != "Disabled")
            .Select(list => $"line {((System.Xml.IXmlLineInfo)list).LineNumber}").ToArray();

        Assert.True(lists.Length >= 6, $"only {lists.Length} lists were found in the sheets.");
        Assert.True(sideways.Length == 0, "Lists that scroll sideways: " + string.Join(", ", sideways));
    }

    /// <summary>With no sideways scrolling, a row's text that neither wraps nor trims is
    /// simply cut: entries sharing a long prefix can't be told apart. A shortcut keeps its
    /// own column, and a group's name is short.</summary>
    [Fact]
    public void ASheetsRowsWrapOrTrimTheirText()
    {
        XDocument shell = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);
        var offenders = new List<string>();
        foreach (XElement list in shell.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.IsDialog") == "True")
            .SelectMany(sheet => sheet.Descendants())
            .Where(element => element.Name.LocalName.EndsWith("ListBox", StringComparison.Ordinal)
                || element.Name.LocalName.EndsWith("ItemsControl", StringComparison.Ordinal)))
        {
            string where = $"line {((System.Xml.IXmlLineInfo)list).LineNumber}";
            XElement? template = list.Elements().FirstOrDefault(element => element.Name.LocalName.EndsWith(".ItemTemplate", StringComparison.Ordinal));
            if (template is null)
            {
                offenders.Add($"{where}: its rows have no template of their own");
                continue;
            }

            offenders.AddRange(template.Descendants()
                .Where(element => element.Name.LocalName is "TextBlock" or "AutomationPresentationTextBlock")
                .Where(text => ((string?)text.Attribute("Text"))?.StartsWith("{Binding", StringComparison.Ordinal) == true)
                .Where(text => (string?)text.Attribute("Text") != "{Binding HotkeyHint}")
                .Where(text => text.Attribute("TextWrapping") is null && text.Attribute("TextTrimming") is null)
                .Select(text => $"{where}: {(string?)text.Attribute("Text")} at line {((System.Xml.IXmlLineInfo)text).LineNumber}"));
            offenders.AddRange(template.Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Where(button => ((string?)button.Attribute("Content"))?.StartsWith("{Binding", StringComparison.Ordinal) == true)
                .Select(button => $"{where}: a button's {(string?)button.Attribute("Content")} at line {((System.Xml.IXmlLineInfo)button).LineNumber}"));
        }

        Assert.True(offenders.Count == 0, "Rows that cut their text:\n" + string.Join('\n', offenders));
    }

    /// <summary>A pane can be as narrow as a split leaves it, so buttons side by side have
    /// to be able to wrap. A sheet's width is fixed, and the sheet facts read its text.</summary>
    [Theory]
    [InlineData("MainWindow.xaml")]
    [InlineData("WorkspaceTemplates.xaml")]
    public void APanesButtonsCanWrap(string file)
    {
        XDocument source = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), file), LoadOptions.SetLineInfo);
        string[] offenders = source.Descendants()
            .Where(element => element.Name.LocalName == "StackPanel" && (string?)element.Attribute("Orientation") == "Horizontal")
            .Where(row => row.Elements().Count(child => child.Name.LocalName == "Button") >= 2)
            .Where(row => !row.Ancestors().Any(ancestor => (string?)ancestor.Attribute("AutomationProperties.IsDialog") == "True"))
            .Select(row => $"line {((System.Xml.IXmlLineInfo)row).LineNumber}")
            .ToArray();

        Assert.True(offenders.Length == 0, "Buttons in a row that can't wrap: " + string.Join(", ", offenders));
    }

    /// <summary>With both sidebars open and a split, a property row has little more than a
    /// button's width, and Delete lost most of its word. Revert only shows on a dirty row.</summary>
    [Fact]
    public void APropertyRowsButtonsWrapInANarrowPane() => OnOwnThread(() =>
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
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        PropertyRowGrid row = ShownShell.Descendants(host.Shell).OfType<PropertyRowGrid>().Single();
        row.SetCurrentValue(FrameworkElement.WidthProperty, 180.0);
        Button[] buttons = ShownShell.Descendants(row).OfType<Button>().Where(button => button.Content is "Save" or "Revert" or "Delete").ToArray();
        Assert.Equal(3, buttons.Length);
        foreach (Button button in buttons)
        {
            button.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }

        host.Settle();
        host.Settle();

        foreach (Button button in buttons)
        {
            Rect bounds = button.TransformToAncestor(row).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
            Assert.True(bounds.Right <= row.ActualWidth + 0.5, $"{button.Content} ends at {bounds.Right:F0} of {row.ActualWidth:F0}.");
        }

        string[] cut = CutText(row);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    /// <summary>Text a sheet binds is as long as what it names: a tag's whole path, or a
    /// file's name in a title. A hosted sheet has none of it to measure, so each one says
    /// what it does with too little room. A key hint keeps a column of its own.</summary>
    [Fact]
    public void ASheetsBoundTextWrapsOrTrims()
    {
        XDocument shell = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);
        string[] offenders = shell.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.IsDialog") == "True")
            .SelectMany(sheet => sheet.Descendants())
            .Where(element => element.Name.LocalName is "TextBlock" or "AutomationPresentationTextBlock")
            .Where(text => ((string?)text.Attribute("Text"))?.StartsWith("{Binding", StringComparison.Ordinal) == true)
            .Where(text => (string?)text.Attribute("Text") != "{Binding HotkeyHint}")
            .Where(text => text.Attribute("TextWrapping") is null && text.Attribute("TextTrimming") is null)
            .Select(text => $"{(string?)text.Attribute("Text")} at line {((System.Xml.IXmlLineInfo)text).LineNumber}")
            .ToArray();

        Assert.True(offenders.Length == 0, "Bound text with no room to spare:\n" + string.Join('\n', offenders));
    }

    /// <summary>A recent search is what its reader typed, however long they made it, and it
    /// ran off the end of its row.</summary>
    [Fact]
    public void ARecentSearchKeepsItsWholeQuery() => OnOwnThread(() =>
    {
        const string Query = "path:Projects/Accessibility meeting notes 2026 tag:review";
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName("SearchOverlay");
        _ = Open(sheet);
        host.Settle();
        host.Settle();

        // The section is collapsed until there's a search, so nothing under it is built.
        LayoutItemsControl recents = InLogicalTree(sheet).OfType<LayoutItemsControl>()
            .First(control => System.Windows.Automation.AutomationProperties.GetName(control) == "Recent searches");
        for (DependencyObject? step = recents; step is not null && !ReferenceEquals(step, sheet); step = LogicalTreeHelper.GetParent(step))
        {
            (step as FrameworkElement)?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }

        recents.SetCurrentValue(ItemsControl.ItemsSourceProperty, new[] { Query });
        host.Settle();
        host.Settle();

        TextBlock text = ShownShell.Descendants(recents).OfType<TextBlock>().Single(found => found.Text == Query);
        Assert.True(text.ActualHeight > text.FontSize * 2, $"the query stays on one line, {text.ActualWidth:F0} wide.");
        string[] cut = CutText(sheet);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    private static IEnumerable<DependencyObject> InLogicalTree(DependencyObject root) =>
        LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().SelectMany(child => InLogicalTree(child).Prepend(child));

    /// <summary>The canvas lists at the largest size: a long label's end, which is what
    /// tells it from its neighbours, is on screen.</summary>
    [Theory]
    [InlineData("CanvasCardPickerOverlay", "CanvasCardPickerRows")]
    [InlineData("CanvasPromptOverlay", "CanvasPromptChoices")]
    public void ACanvasListKeepsTheEndOfALongLabel(string sheetName, string id) => OnOwnThread(() =>
    {
        const string Label = "A card with a long shared prefix that runs on and on and on and on and on, the first";
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName(sheetName);
        _ = Open(sheet);
        host.Settle();
        ListBox list = ShownShell.Descendants(sheet).OfType<ListBox>()
            .First(found => System.Windows.Automation.AutomationProperties.GetAutomationId(found) == id);
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, new[] { new LongRow(Label, Label) });
        host.Settle();
        host.Settle();

        TextBlock text = ShownShell.Descendants(list).OfType<TextBlock>().First(found => found.Text == Label);
        double right = text.TransformToAncestor(list).TransformBounds(new Rect(0, 0, text.ActualWidth, text.ActualHeight)).Right;
        Assert.True(text.DesiredSize.Height > text.FontSize * 2, $"the label stays on one line, {text.ActualWidth:F0} wide.");
        Assert.True(right <= list.ActualWidth, $"the label ends at {right:F0} in a list {list.ActualWidth:F0} wide.");
    });

    /// <summary>The prompt's list sat in a stack with no height of its own, so wrapped rows
    /// ran off the window, where no scrolling could reach them or Clear All Marks. 480 is
    /// the window's minimum.</summary>
    [Theory]
    [InlineData(720, 1.0)]
    [InlineData(480, 1.0)]
    [InlineData(720, 2.25)]
    [InlineData(480, 2.25)]
    public void ACanvasPromptsChoicesScrollInsideTheWindow(double height, double factor) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = height;
        TypeRamp.Apply(RampOf(host.Shell), factor);
        host.Settle();
        var list = (ListBox)host.Shell.FindName("CanvasPromptChoicesList");
        var sheet = (FrameworkElement)host.Shell.FindName("CanvasPromptOverlay");
        var clear = (FrameworkElement)host.Shell.FindName("CanvasPromptClearMarksButton");
        _ = Open(sheet);
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(
            ItemsControl.ItemsSourceProperty,
            Enumerable.Range(1, 30).Select(index => new LongRow(string.Empty, $"A long choice that wraps onto a second line at this size, number {index}")).ToArray());
        host.Settle();
        host.Settle();

        ScrollViewer scroller = ShownShell.Descendants(list).OfType<ScrollViewer>().First();
        var root = (FrameworkElement)host.Shell.Content;
        double bottom = clear.TransformToAncestor(sheet).Transform(new Point(0, clear.ActualHeight)).Y;
        double sheetBottom = sheet.TransformToAncestor(root).Transform(new Point(0, sheet.ActualHeight)).Y;
        Assert.True(scroller.ScrollableHeight > 0, "the list has nothing to scroll, so its rows run past it.");
        Assert.True(bottom <= sheet.ActualHeight, $"Clear All Marks ends at {bottom:F0} in a sheet {sheet.ActualHeight:F0} tall.");
        Assert.True(sheetBottom <= root.ActualHeight, $"the sheet ends at {sheetBottom:F0} in a {root.ActualHeight:F0} window.");
    });

    /// <summary>A sheet's title, status and buttons can take all of a short window, and a
    /// list that only scrolls itself then has no room to show a row. Its whole body scrolls
    /// instead, so the list keeps its size.</summary>
    [Theory]
    [InlineData("MoveToOverlay", "MoveToList")]
    [InlineData("TemplatePickerOverlay", "TemplatePickerList")]
    [InlineData("DashboardEditorOverlay", "DashboardEditorSections")]
    public void ASheetsListKeepsItsRowsInTheShortestWindow(string sheetName, string id) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 480;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName(sheetName);
        _ = Open(sheet);
        host.Settle();
        host.Settle();
        ListBox list = ShownShell.Descendants(sheet).OfType<ListBox>()
            .First(found => System.Windows.Automation.AutomationProperties.GetAutomationId(found) == id);
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, Enumerable.Range(1, 20).Select(index => new LongRow($"Row {index}", $"Row {index}")).ToArray());
        host.Settle();
        host.Settle();

        Assert.True(list.ActualHeight >= 120, $"the list has {list.ActualHeight:F0} of height in a {sheet.ActualHeight:F0} sheet.");
    });

    /// <summary>A scroller inside a scroller grows to fit with nothing left to scroll, and
    /// still takes the wheel, so the sheet stops scrolling under the pointer. One inside a
    /// sheet's scroller has to hold a height of its own, and so does a list.</summary>
    [Fact]
    public void AScrollerInsideASheetsScrollerHoldsItsHeight()
    {
        XDocument shell = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);
        string[] unheld = shell.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.IsDialog") == "True")
            .SelectMany(sheet => sheet.Descendants())
            .Where(element => element.Name.LocalName == "ScrollViewer"
                || element.Name.LocalName.EndsWith("ListBox", StringComparison.Ordinal)
                || element.Name.LocalName == "AccessibleDataGrid")
            .Where(element => element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "ScrollViewer"))
            .Where(element => element.Attribute("MaxHeight") is null)
            .Select(element => $"{element.Name.LocalName} at line {((System.Xml.IXmlLineInfo)element).LineNumber}")
            .ToArray();

        Assert.True(unheld.Length == 0, "Scrollers that grow to fit inside a scroller:\n" + string.Join('\n', unheld));
    }

    /// <summary>The wheel over a field in an overflowing sheet moves the sheet.</summary>
    [Theory]
    [InlineData("TemplateFlowOverlay", "TemplateFlowPromptsList")]
    [InlineData("BaseQueryBuilderOverlay", "BuilderCombinatorBox")]
    public void TheWheelOverASheetsFieldsScrollsTheSheet(string sheetName, string fieldName) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 480;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName(sheetName);
        _ = Open(sheet);
        host.Settle();
        host.Settle();
        ScrollViewer body = ShownShell.Descendants(sheet).OfType<ScrollViewer>().First();
        Assert.True(body.ScrollableHeight > 0, "premise: the sheet doesn't overflow here.");

        ((UIElement)host.Shell.FindName(fieldName)).RaiseEvent(
            new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.MouseWheelEvent });
        host.Settle();
        host.Settle();

        Assert.True(body.VerticalOffset > 0, "the wheel didn't move the sheet.");
    });

    /// <summary>The welcome view's recent vaults sat below a short window, where nothing
    /// could reach them. Its text is what a reader meets first, so none of it is cut.</summary>
    [Fact]
    public void TheWelcomeViewReachesItsLastRecentVault() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 480;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.SetVaultOpen(false);
        var welcome = (FrameworkElement)host.Shell.FindName("WelcomeRoot");
        _ = Open(welcome);
        host.Settle();
        host.Settle();
        LayoutItemsControl recents = ShownShell.Descendants(welcome).OfType<LayoutItemsControl>().First();
        for (DependencyObject? step = recents; step is not null && !ReferenceEquals(step, welcome); step = System.Windows.Media.VisualTreeHelper.GetParent(step))
        {
            (step as FrameworkElement)?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }

        recents.SetCurrentValue(ItemsControl.ItemsSourceProperty, Enumerable.Range(1, 4).Select(index => new RecentVault($@"C:\vaults\v{index}", $"v{index}", index)).ToArray());
        host.Settle();
        host.Settle();

        Button last = ShownShell.Descendants(recents).OfType<Button>().Last();
        last.BringIntoView();
        host.Settle();
        host.Settle();

        Rect bounds = last.TransformToAncestor(welcome).TransformBounds(new Rect(0, 0, last.ActualWidth, last.ActualHeight));
        Assert.True(bounds.Bottom <= welcome.ActualHeight + 1, $"the last vault ends at {bounds.Bottom:F0} in {welcome.ActualHeight:F0} of welcome view.");
        string[] cut = CutText(welcome);
        Assert.True(cut.Length == 0, $"{cut.Length} cut:\n" + string.Join('\n', cut));
    });

    /// <summary>A short list has nowhere to scroll, and still took the wheel, so the sheet
    /// stopped under the pointer. A long one gets the wheel until it reaches its end.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(30)]
    public void AListInASheetPassesOnTheWheelItCantUse(int rows) => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 480;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName("MoveToOverlay");
        _ = Open(sheet);
        host.Settle();
        host.Settle();
        var list = (ListBox)host.Shell.FindName("MoveToList");
        list.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        list.SetCurrentValue(ItemsControl.ItemsSourceProperty, Enumerable.Range(1, rows).Select(index => new LongRow($"Row {index}", $"Row {index}")).ToArray());
        host.Settle();
        host.Settle();
        ScrollViewer body = ShownShell.Descendants(sheet).OfType<ScrollViewer>().First();
        ScrollViewer inner = ShownShell.Descendants(list).OfType<ScrollViewer>().First();
        TextBlock row = ShownShell.Descendants(list).OfType<TextBlock>().First(text => text.Text == "Row 1");
        Assert.True(body.ScrollableHeight > 0, "premise: the sheet doesn't overflow here.");

        if (inner.ScrollableHeight > 0)
        {
            Wheel(row);
            host.Settle();
            Assert.True(inner.VerticalOffset > 0, "a list that can scroll didn't get the wheel.");
            Assert.Equal(0, body.VerticalOffset);
            inner.ScrollToEnd();
            host.Settle();
            host.Settle();

            // The first rows have been virtualized away by now.
            row = ShownShell.Descendants(list).OfType<TextBlock>().First(text => text.Text.StartsWith("Row", StringComparison.Ordinal));
        }

        Wheel(row);
        host.Settle();
        Assert.True(body.VerticalOffset > 0, $"the sheet stayed put under a list with {inner.ScrollableHeight:F0} to scroll.");

        // And back: a list at its top passes on a notch up.
        double sheetWasAt = body.VerticalOffset;
        inner.ScrollToHome();
        host.Settle();
        host.Settle();
        row = ShownShell.Descendants(list).OfType<TextBlock>().First(text => text.Text.StartsWith("Row", StringComparison.Ordinal));
        Wheel(row, 120);
        host.Settle();
        Assert.True(body.VerticalOffset < sheetWasAt, $"the sheet stayed at {sheetWasAt:F0} under a list at its top.");
    });

    /// <summary>An open combo box's list is in a popup, so the walk up from it never meets
    /// the sheet's scroller. It keeps its own wheel, and the sheet stays where it is.</summary>
    [Fact]
    public void AComboBoxsOpenListKeepsItsOwnWheel() => OnOwnThread(() =>
    {
        var combo = new ComboBox { ItemsSource = Enumerable.Range(1, 40).Select(index => $"Row {index}").ToArray() };
        var scroller = new ScrollViewer
        {
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { combo, new Border { Height = 2000 } } },
        };
        WheelChain.SetIsEnabled(scroller, true);
        var window = new Window
        {
            Content = scroller,
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
            combo.IsDropDownOpen = true;
            PumpedDispatcher.Drain();
            UIElement list = ((Popup)combo.Template.FindName("PART_Popup", combo)).Child;

            Wheel(list);
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.Equal(0, scroller.VerticalOffset);

            combo.IsDropDownOpen = false;
            PumpedDispatcher.Drain();
            Wheel(combo);
            window.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.True(scroller.VerticalOffset > 0, "premise: the wheel over the closed combo box should move the scroller.");
        }
        finally
        {
            window.Close();
            PumpedDispatcher.Drain();
        }
    });

    /// <summary>Off, a scroller leaves the wheel alone. On again, it raises one wheel, not
    /// one for each time it was switched on.</summary>
    [Fact]
    public void TheWheelChainFollowsItsSwitch() => OnOwnThread(() =>
    {
        var scroller = new ScrollViewer();
        int raised = 0;
        scroller.AddHandler(UIElement.MouseWheelEvent, new System.Windows.Input.MouseWheelEventHandler((_, _) => raised++), handledEventsToo: true);
        bool Preview()
        {
            var preview = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
            scroller.RaiseEvent(preview);
            return preview.Handled;
        }

        WheelChain.SetIsEnabled(scroller, true);
        WheelChain.SetIsEnabled(scroller, false);
        Assert.False(Preview(), "the chain still took the wheel when it was off.");
        Assert.Equal(0, raised);

        WheelChain.SetIsEnabled(scroller, true);
        Assert.True(Preview(), "the chain didn't take the wheel when it was on again.");
        Assert.Equal(1, raised);
    });

    /// <summary>A wheel notch, as input delivers it: the preview, then the event.</summary>
    private static void Wheel(UIElement element, int delta = -120)
    {
        var preview = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        element.RaiseEvent(preview);
        if (!preview.Handled)
        {
            element.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, delta) { RoutedEvent = UIElement.MouseWheelEvent });
        }
    }

    /// <summary>Each sheet's own scroller passes the wheel down only to what can use it.</summary>
    [Fact]
    public void ASheetsScrollerChainsTheWheel()
    {
        XDocument shell = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"), LoadOptions.SetLineInfo);
        XElement[] scrollers = shell.Descendants()
            .Where(element => (string?)element.Attribute("AutomationProperties.IsDialog") == "True")
            .SelectMany(sheet => sheet.Descendants())
            .Where(element => element.Name.LocalName == "ScrollViewer" && element.Attribute("MaxHeight") is null)
            .Where(element => element.Descendants().Any(child => child.Name.LocalName == "ScrollViewer"
                || child.Name.LocalName.EndsWith("ListBox", StringComparison.Ordinal)
                || child.Name.LocalName == "AccessibleDataGrid"))
            .ToArray();
        string[] unchained = scrollers
            .Where(element => !element.Attributes().Any(attribute => attribute.Name.LocalName == "WheelChain.IsEnabled"))
            .Select(element => $"line {((System.Xml.IXmlLineInfo)element).LineNumber}")
            .ToArray();

        Assert.True(scrollers.Length >= 5, $"only {scrollers.Length} sheet scrollers hold something that scrolls.");
        Assert.True(unchained.Length == 0, "Sheet scrollers that don't chain the wheel: " + string.Join(", ", unchained));
    }

    /// <summary>A long subtitle took all of the step's room, so its field had no height,
    /// and a scroller round the field alone couldn't help. The whole step scrolls, so keys
    /// reach the field wherever a long subtitle pushes it.</summary>
    [Fact]
    public void TheTemplateNameFieldIsReachableUnderALongSubtitle() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 480;
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        var sheet = (FrameworkElement)host.Shell.FindName("TemplateFlowOverlay");
        _ = Open(sheet);
        host.Settle();
        host.Settle();
        FrameworkElement[] elements = ShownShell.Descendants(sheet).OfType<FrameworkElement>().ToArray();
        var subtitle = (TextBlock)elements.First(element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == "TemplateNameSubtitle");
        subtitle.SetCurrentValue(TextBlock.TextProperty, string.Concat(Enumerable.Repeat("from Accessibility research meeting notes, in a folder with a long path. ", 5)));
        var name = (TextBox)elements.First(element => System.Windows.Automation.AutomationProperties.GetAutomationId(element) == "TemplateFlowName");
        host.Settle();
        name.BringIntoView();
        host.Settle();
        host.Settle();

        Rect bounds = name.TransformToAncestor(sheet).TransformBounds(new Rect(0, 0, name.ActualWidth, name.ActualHeight));
        Assert.True(bounds.Height >= 30, $"the field is {bounds.Height:F0} tall.");
        Assert.True(bounds.Top >= 0 && bounds.Bottom <= sheet.ActualHeight, $"the field is at {bounds} in a sheet {sheet.ActualHeight:F0} tall.");
    });

    private sealed record LongRow(string Label, string Name);

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

    /// <summary>Off, the rail stays where it is. On again, it places itself.</summary>
    [Fact]
    public void TheRailFollowsItsSwitch() => OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1120;
        host.Shell.Height = 720;
        host.Settle();
        host.Settle();
        var rail = (ListBox)host.Shell.FindName("RightPaneLeavesList");
        var leaves = (Grid)host.Shell.FindName("RightPaneLeafHost");

        RightPaneRail.SetIsEnabled(rail, false);
        TypeRamp.Apply(RampOf(host.Shell), 2.25);
        host.Settle();
        host.Settle();
        Assert.True(rail.TranslatePoint(default, leaves).X > 0, "the rail moved with its switch off.");

        RightPaneRail.SetIsEnabled(rail, true);
        host.Settle();
        host.Settle();
        Assert.Equal(0, rail.TranslatePoint(default, leaves).X, 1);
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
    [InlineData(CalendarMode.Month, 1.0, 50, "en-US")]
    [InlineData(CalendarMode.Year, 1.0, 13, "en-US")]
    [InlineData(CalendarMode.Decade, 1.0, 13, "en-US")]
    [InlineData(CalendarMode.Month, 2.25, 50, "en-US")]
    [InlineData(CalendarMode.Year, 2.25, 13, "en-US")]
    [InlineData(CalendarMode.Decade, 2.25, 13, "en-US")]
    [InlineData(CalendarMode.Month, 2.25, 50, "ru-RU")]
    [InlineData(CalendarMode.Year, 2.25, 13, "ru-RU")]
    public void TheOpenCalendarFollowsTheFactor(CalendarMode mode, double factor, int atLeast, string language) => OnOwnThread(() =>
    {
        var picker = new DatePicker
        {
            SelectedDate = new DateTime(2026, 11, 4),
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(language),
        };
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

            TextBlock[] texts = ShownShell.Descendants(calendar).OfType<TextBlock>().Where(text => !string.IsNullOrEmpty(text.Text)).ToArray();
            TextBlock[] glyphs = texts.Where(text => text.Text is "\uEDDB" or "\uEDDC").ToArray();
            TextBlock[] labels = texts.Except(glyphs).Where(text => InShownView(text, calendar)).ToArray();

            Assert.Equal(2, glyphs.Length);
            Assert.All(glyphs, glyph => Assert.Equal(8, glyph.FontSize));
            Assert.True(labels.Length >= atLeast, $"only {labels.Length} labels in the {mode} view.");
            Assert.All(labels, label => Assert.True(
                label.FontSize == TypeRamp.Body.Size * factor,
                $"\"{label.Text}\" is {label.FontSize} at factor {factor}."));
            string[] clipped = labels
                .Where(label => WhyCut(label, calendar) is not null)
                .Select(label => $"\"{label.Text}\" at {label.FontSize}, by {WhyCut(label, calendar)}").ToArray();
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
        ShownShell.Descendants(root)
            .OfType<TextBlock>()
            .Where(text => text.IsVisible && text.TextTrimming == TextTrimming.None && !string.IsNullOrEmpty(text.Text))
            .Select(text => (Text: text, Why: WhyCut(text, root)))
            .Where(found => found.Why is not null)
            .Select(found => $"\"{found.Text.Text}\" at {found.Text.FontSize:F1}, by {found.Why}").ToArray();

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
            // A scroller's cut is its job only along a way it scrolls.
            if (step is ScrollContentPresenter { ScrollOwner.HorizontalScrollBarVisibility: ScrollBarVisibility.Disabled } view)
            {
                Rect inView = text.TransformToAncestor(view).TransformBounds(mine);
                return inView.Left < -0.5 || inView.Right > view.ActualWidth + 0.5
                    ? $"the side of a scroller, at {inView} of {view.ActualWidth:F0} wide"
                    : null;
            }

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

        return opened.ToArray();
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
    internal static void OnOwnThread(Action body) => StaThread.Run(() =>
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
