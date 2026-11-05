// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// DS-6: a sheet or the palette reads as raised, and its text keeps ClearType, because
// the shadow sits on a sibling behind it. Nothing a screen reader reaches changes.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace SlateWindows.Tests;

public sealed class OverlayElevationTests
{
    /// <summary>Driven by the enum, through the table its paint order is checked against,
    /// so a new surface can't be forgotten.</summary>
    private static IEnumerable<(ModalSurface Surface, XElement Landmark)> Overlays()
    {
        XElement[] elements = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml")).Descendants().ToArray();
        foreach ((ModalSurface surface, string id) in ModalSurfaceTests.SurfaceAutomationIds)
        {
            yield return (surface, elements.Single(element => (string?)element.Attribute("AutomationProperties.AutomationId") == id));
        }
    }

    public static TheoryData<string> Surfaces() => new(Enum.GetNames<ModalSurface>());

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EveryOverlayCarriesItsShadowBesideItsContent(string surfaceName)
    {
        ModalSurface surface = Enum.Parse<ModalSurface>(surfaceName);
        XElement landmark = Overlays().Single(overlay => overlay.Surface == surface).Landmark;
        string name = (string)landmark.Attributes().Single(attribute => attribute.Name.LocalName == "Name");

        Assert.DoesNotContain(landmark.Elements(), element => element.Name.LocalName.EndsWith(".Effect", StringComparison.Ordinal));
        Assert.DoesNotContain(landmark.Attributes(), attribute => attribute.Name.LocalName == "Effect");
        Assert.StartsWith("{Binding", (string?)landmark.Attribute("Visibility"), StringComparison.Ordinal);
        Assert.Equal("Enabled", (string?)landmark.Attribute("RenderOptions.ClearTypeHint"));

        XElement wrapper = landmark.Parent!;
        Assert.Equal("Grid", wrapper.Name.LocalName);
        Assert.Null(wrapper.Attribute("Visibility"));
        Assert.Null(wrapper.Attribute("Background"));
        XElement shadow = wrapper.Elements().Single(element => element != landmark);
        Assert.Equal("Border", shadow.Name.LocalName);
        Assert.True(landmark.ElementsBeforeSelf().Contains(shadow), "the shadow draws over its sheet.");
        string level = surface is ModalSurface.QuickOpen or ModalSurface.SearchOverlay or ModalSurface.CommandPalette ? "28" : "64";
        Assert.Equal($"{{DynamicResource Slate.OverlayShadow{level}}}", (string?)shadow.Attribute("Style"));
        Assert.Equal($"{{Binding Visibility, ElementName={name}}}", (string?)shadow.Attribute("Visibility"));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EveryModalSurfaceDrawsTheScrim(string surfaceName)
    {
        ModalSurface surface = Enum.Parse<ModalSurface>(surfaceName);
        XElement wrapper = Overlays().Single(overlay => overlay.Surface == surface).Landmark.Parent!;
        XElement? layer = wrapper.ElementsBeforeSelf().LastOrDefault();
        Assert.NotNull(layer);
        Assert.Equal("Border", layer.Name.LocalName);
        Assert.Equal("{DynamicResource Slate.ScrimBrush}", (string?)layer.Attribute("Background"));
        Assert.StartsWith("{Binding", (string?)layer.Attribute("Visibility"), StringComparison.Ordinal);
    }

    [Fact]
    public void NoEffectIsLeftInTheShellsMarkup() =>
        Assert.DoesNotContain("DropShadowEffect", File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml")), StringComparison.Ordinal);

    /// <summary>Every shadow level in the styles exists in the design's tokens, in both
    /// themes, and falls straight down.</summary>
    [Theory]
    [InlineData(false, "Slate.OverlayShadow28", 14, 42)]
    [InlineData(false, "Slate.OverlayShadow64", 32, 96)]
    [InlineData(true, "Slate.OverlayShadow28", 14, 42)]
    [InlineData(true, "Slate.OverlayShadow64", 32, 96)]
    public void AnOverlaysShadowIsTheDesignsLevel(bool dark, string key, double depth, double blur) => TextScaleTests.OnOwnThread(() =>
    {
        using Sheet sheet = Sheet.Shown(dark);
        var shadow = new Border();
        shadow.SetResourceReference(FrameworkElement.StyleProperty, key);
        sheet.Window.Content = shadow;
        sheet.Window.UpdateLayout();
        var effect = Assert.IsType<System.Windows.Media.Effects.DropShadowEffect>(shadow.Effect);
        Assert.Equal(depth, effect.ShadowDepth);
        Assert.Equal(blur, effect.BlurRadius);
        Assert.Equal(270, effect.Direction);
        Assert.Equal(new CornerRadius(8), shadow.CornerRadius);
        Assert.False(shadow.IsHitTestVisible, "a shadow takes a pointer.");
    });

    [Theory]
    [InlineData("QuickSwitcherOverlay")]
    [InlineData("SearchOverlay")]
    [InlineData("CommandPaletteOverlay")]
    [InlineData("AddPropertyOverlay")]
    [InlineData("BulkRenameOverlay")]
    [InlineData("CitationDetailsOverlay")]
    [InlineData("CitationSummaryOverlay")]
    [InlineData("FilesCitingOverlay")]
    [InlineData("DashboardEditorOverlay")]
    [InlineData("BaseQueryBuilderOverlay")]
    [InlineData("TemplatePickerOverlay")]
    [InlineData("TemplateFlowOverlay")]
    [InlineData("MoveToOverlay")]
    [InlineData("CanvasCardEditorOverlay")]
    [InlineData("CanvasCardPickerOverlay")]
    [InlineData("CanvasPromptOverlay")]
    public void AnOverlaysCornersAreEight(string name) => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        var overlay = (Border)host.Shell.FindName(name);
        Assert.Equal(new CornerRadius(8), overlay.CornerRadius);
    });

    /// <summary>A sheet with little in it is still the design's dialog width, up to what
    /// the window has, because the wrapper stretches to its most and centres there.</summary>
    [Theory]
    [InlineData("AddPropertyOverlay", 1400)]
    [InlineData("AddPropertyOverlay", 800)]
    [InlineData("CitationDetailsOverlay", 1120)]
    [InlineData("CitationSummaryOverlay", 1120)]
    [InlineData("FilesCitingOverlay", 1120)]
    [InlineData("TemplatePickerOverlay", 1120)]
    [InlineData("TemplateFlowOverlay", 1120)]
    [InlineData("MoveToOverlay", 1120)]
    public void AOneFormSheetIsTheDialogWidth(string name, double width) => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = width;
        host.Shell.Height = 720;
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName(name);
        OpenUpTo(overlay);
        host.Settle();
        host.Settle();

        Assert.Equal(448, overlay.ActualWidth, 1);
        var area = (FrameworkElement)VisualTreeHelper.GetParent(VisualTreeHelper.GetParent(overlay));
        double left = overlay.TranslatePoint(default, area).X;
        Assert.Equal(area.ActualWidth / 2, left + (overlay.ActualWidth / 2), 0);
    });

    private static void OpenUpTo(FrameworkElement overlay)
    {
        for (DependencyObject? step = overlay; step is not null; step = VisualTreeHelper.GetParent(step))
        {
            (step as FrameworkElement)?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }
    }

    [Theory]
    [InlineData("BulkRenameOverlay", 760)]
    [InlineData("DashboardEditorOverlay", 560)]
    [InlineData("BaseQueryBuilderOverlay", 640)]
    [InlineData("SearchOverlay", 640)]
    [InlineData("CommandPaletteOverlay", 640)]
    [InlineData("QuickSwitcherOverlay", 600)]
    public void AWideSheetKeepsItsWidthAsItsMost(string name, double most) => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Shell.Width = 1400;
        host.Shell.Height = 720;
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName(name);
        OpenUpTo(overlay);
        host.Settle();
        host.Settle();

        Assert.Equal(most, overlay.ActualWidth, 1);
    });

    /// <summary>A shadow's sibling isn't shown behind a closed sheet, and the wrapper is
    /// never what hides it, because a landmark's visibility is read from itself.</summary>
    [Fact]
    public void AClosedOverlaysShadowIsGoneAndItsWrapperPaintsNothing() => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName("AddPropertyOverlay");
        var wrapper = (Grid)VisualTreeHelper.GetParent(overlay);
        Border shadow = wrapper.Children.OfType<Border>().First(child => !ReferenceEquals(child, overlay));
        Assert.Equal(Visibility.Collapsed, overlay.Visibility);
        Assert.Equal(Visibility.Collapsed, shadow.Visibility);
        Assert.Equal(Visibility.Visible, wrapper.Visibility);
        Assert.Null(wrapper.Background);
        Assert.Null(wrapper.InputHitTest(new Point(1, 1)));

        overlay.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        host.Settle();
        Assert.Equal(Visibility.Visible, shadow.Visibility);
        Assert.Equal(overlay.ActualWidth, shadow.ActualWidth, 1);
        Assert.Equal(overlay.ActualHeight, shadow.ActualHeight, 1);
    });

    /// <summary>The scrim is one layer over the middle and a thin one over each of the rows
    /// beside it, so the whole window dims. The thin ones take no pointer.</summary>
    [Fact]
    public void TheMenuAndStatusRowsDimUnderAModalSurface() => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Settle();
        var overMenu = (FrameworkElement)host.Shell.FindName("ModalScrimOverMenu");
        var overStatus = (FrameworkElement)host.Shell.FindName("ModalScrimOverStatus");
        Assert.Equal(Visibility.Collapsed, overMenu.Visibility);
        Assert.Equal(Visibility.Collapsed, overStatus.Visibility);

        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        host.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        host.Settle();
        host.Settle();
        Assert.NotNull(host.Shell.OpenModalSurface);
        Assert.Equal(Visibility.Visible, overMenu.Visibility);
        Assert.Equal(Visibility.Visible, overStatus.Visibility);
        Assert.False(overMenu.IsHitTestVisible, "the scrim over the menu takes a pointer.");
        Assert.False(overStatus.IsHitTestVisible, "the scrim over the status bar takes a pointer.");
        Assert.Equal(host.Shell.MainMenu.ActualHeight, overMenu.ActualHeight, 1);
        Assert.Equal(host.Shell.ShellStatusBarBorder.ActualHeight, overStatus.ActualHeight, 1);
        Assert.Same(host.Shell.TryFindResource("Slate.ScrimBrush"), ((Border)overMenu).Background);
        Assert.Same(host.Shell.TryFindResource("Slate.ScrimBrush"), ((Border)overStatus).Background);

        host.Workspace.CloseAddPropertySheetCommand.Execute(null);
        host.Settle();
        host.Settle();
        Assert.Null(host.Shell.OpenModalSurface);
        Assert.Equal(Visibility.Collapsed, overMenu.Visibility);
        Assert.Equal(Visibility.Collapsed, overStatus.Visibility);
    });

    /// <summary>A vault can close with a surface open, and what held its flag goes without
    /// a word from it. The rows beside the sheets mustn't stay dim on the welcome view.</summary>
    [Fact]
    public void TheRowsStopDimmingWhenWhatHeldTheSurfaceGoesAway() => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        host.Workspace.OpenAddPropertySheet(synchronousForTests: true);
        host.Settle();
        host.Settle();
        var overMenu = (FrameworkElement)host.Shell.FindName("ModalScrimOverMenu");
        Assert.Equal(Visibility.Visible, overMenu.Visibility);

        host.ShowWelcomeWithoutAWorkspace();
        host.Settle();
        host.Settle();

        Assert.Null(host.Shell.OpenModalSurface);
        Assert.Equal(Visibility.Collapsed, overMenu.Visibility);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)host.Shell.FindName("ModalScrimOverStatus")).Visibility);
    });

    /// <summary>A contrast theme has its own mapping, as the theme's README asks of a new
    /// brush Slate draws with, and it lets what's under it show.</summary>
    [Fact]
    public void AContrastThemesScrimLetsTheWindowShow() => TextScaleTests.OnOwnThread(() =>
    {
        using Sheet sheet = Sheet.Shown(dark: false, highContrast: true);
        var brush = Assert.IsAssignableFrom<SolidColorBrush>(sheet.Window.TryFindResource("Slate.ScrimBrush"));
        Assert.True(brush.Opacity * brush.Color.A / 255.0 < 0.5, "a contrast theme's scrim hides the window.");
    });

    /// <summary>Fluent's ring: its inner edge is 1 outside the control. It's right as it
    /// is, so this pins it for whatever changes next.</summary>
    [Fact]
    public void AControlsRingMatchesTheDesign() => TextScaleTests.OnOwnThread(() =>
    {
        using Sheet sheet = Sheet.Shown(dark: false);
        var style = Assert.IsType<Style>(sheet.Window.TryFindResource("DefaultControlFocusVisualStyle"));
        var template = Assert.IsType<ControlTemplate>(style.Setters.OfType<Setter>().Single(setter => setter.Property == Control.TemplateProperty).Value);
        var ring = Assert.IsType<System.Windows.Shapes.Rectangle>(template.LoadContent());
        Assert.Equal(new Thickness(-3), ring.Margin);
        Assert.Equal(2, ring.StrokeThickness);
    });

    /// <summary>The tooltip's shadow sat on the border round its text, which turns its
    /// ClearType off. It's on a sibling now, at the design's level for a tooltip.</summary>
    [Fact]
    public void ATooltipsShadowIsBesideItsText() => TextScaleTests.OnOwnThread(() =>
    {
        using Sheet sheet = Sheet.Shown(dark: false);
        var style = Assert.IsType<Style>(sheet.Window.TryFindResource(typeof(ToolTip)));
        var template = Assert.IsType<ControlTemplate>(style.Setters.OfType<Setter>().Single(setter => setter.Property == Control.TemplateProperty).Value);

        // A tooltip can't be hosted in a window, so its template stands in for it.
        var root = Assert.IsAssignableFrom<FrameworkElement>(template.LoadContent());
        sheet.Window.Content = root;
        sheet.Window.UpdateLayout();
        ContentPresenter text = ShownShell.Descendants(root).OfType<ContentPresenter>().First();
        for (DependencyObject? step = text; step is not null; step = VisualTreeHelper.GetParent(step))
        {
            Assert.Null((step as UIElement)?.Effect);
            if (ReferenceEquals(step, root))
            {
                break;
            }
        }

        Border[] borders = ShownShell.Descendants(root).OfType<Border>().ToArray();
        Border shadow = Assert.Single(borders, border => border.Effect is not null);
        Assert.Same(sheet.Window.TryFindResource("Slate.Shadow8"), shadow.Effect);
        Border surface = Assert.Single(borders, border => border.Effect is null);
        Assert.Equal(new CornerRadius(4), surface.CornerRadius);
        Assert.Equal(ClearTypeHint.Enabled, RenderOptions.GetClearTypeHint(surface));
        Assert.True(borders.ToList().IndexOf(shadow) < borders.ToList().IndexOf(surface), "the shadow draws over the text.");
    });

    private sealed class Sheet : IDisposable
    {
        private Sheet(Window window) => Window = window;

        public Window Window { get; }

        public static Sheet Shown(bool dark, bool highContrast = false)
        {
            var window = new Window
            {
                Width = 400,
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

            window.Show();
            PumpedDispatcher.Drain();
            return new Sheet(window);
        }

        public void Dispose()
        {
            Window.Close();
            PumpedDispatcher.Drain();
        }
    }
}
