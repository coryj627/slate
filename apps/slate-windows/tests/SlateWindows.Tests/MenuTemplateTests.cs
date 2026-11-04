// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>DS-4: one template serves every menu row. A popup on a hosted runner opens
/// without laying out its children, so a fact measures the popup's surface itself.</summary>
public sealed class MenuTemplateTests
{
    [Fact]
    public void EveryRowKindStartsItsLabelInOneColumn()
    {
        double[] starts = InMenu(false, false, (window, top, rows) =>
            rows.Select(row => LabelOf(row).TranslatePoint(default, Surface(top)).X).ToArray());

        Assert.Equal(4, starts.Length);
        Assert.Single(starts.Select(start => Math.Round(start, 1)).Distinct());
    }

    /// <summary>With no check or icon anywhere in the menu, the column is zero wide and
    /// the label is 16 from the row's edge.</summary>
    [Fact]
    public void AMenuWithNothingToLeadWithStartsItsLabelsAtSixteen()
    {
        double start = InMenu(false, false, plainOnly: true, (window, top, rows) =>
            LabelOf(rows[0]).TranslatePoint(default, rows[0]).X);

        Assert.Equal(16, start, 1);
    }

    [Fact]
    public void ARowsPitchIsFortyAndItsHighlightIsInsetFive()
    {
        (double pitch, Point inset, double height, CornerRadius radius) = InMenu(false, false, (window, top, rows) =>
        {
            MenuItem row = rows[0];
            var highlight = (Border)row.Template.FindName("Highlight", row);
            return (row.ActualHeight, highlight.TranslatePoint(default, row), highlight.ActualHeight, highlight.CornerRadius);
        });

        Assert.Equal(40, pitch, 1);
        Assert.Equal(5, inset.X, 1);
        Assert.Equal(3, inset.Y, 1);
        Assert.Equal(34, height, 1);
        Assert.Equal(3, radius.TopLeft);
    }

    [Fact]
    public void AMenuRowsRingSitsOnItsHighlight()
    {
        (bool named, Thickness margin, double stroke, double radius) = InMenu(false, false, (window, top, rows) =>
        {
            MenuItem row = rows[0];
            var ring = new Control { Style = row.FocusVisualStyle };
            ring.ApplyTemplate();
            var border = (Border)VisualTreeHelper.GetChild(ring, 0);
            return (
                ReferenceEquals(row.FocusVisualStyle, window.FindResource("Slate.MenuRowFocusVisual")),
                border.Margin,
                border.BorderThickness.Left,
                border.CornerRadius.TopLeft);
        });

        Assert.True(named, "the row doesn't take the menu's ring.");
        // Inside the 40 pitch, with its inner edge 1 out from the highlight at 5,3.
        Assert.Equal(new Thickness(2, 0, 2, 0), margin);
        Assert.Equal(2, stroke);
        Assert.Equal(6, radius);
    }

    [Fact]
    public void ACheckMenuItemTakesTheSlateTemplate()
    {
        bool same = InMenu(false, false, (window, top, rows) =>
            ReferenceEquals(rows[1].Template, window.FindResource("Slate.MenuRowTemplate")));

        Assert.True(same, "a CheckMenuItem kept another template.");
    }

    [Fact]
    public void ACheckedRowDrawsABareCheck()
    {
        (object? box, Visibility unchecked_, Visibility checked_, string glyph, double size) = InMenu(false, false, (window, top, rows) =>
        {
            MenuItem plain = rows[1];
            MenuItem marked = rows[2];
            var check = (TextBlock)marked.Template.FindName("Check", marked);
            return (
                marked.Template.FindName("CheckBoxIconBorder", marked),
                ((TextBlock)plain.Template.FindName("Check", plain)).Visibility,
                check.Visibility,
                check.Text,
                check.FontSize);
        });

        Assert.Null(box);
        Assert.NotEqual(Visibility.Visible, unchecked_);
        Assert.Equal(Visibility.Visible, checked_);
        Assert.Equal("", glyph);
        Assert.Equal(12, size);
    }

    /// <summary>The measurement #1756 asked for, rather than a working-out.</summary>
    [Fact]
    public void ATopLevelItemFitsATitleBar()
    {
        double height = InMenu(false, false, (window, top, rows) => top.ActualHeight + top.Margin.Top + top.Margin.Bottom);

        Assert.True(height <= 48, $"a top-level item is {height:F1} tall.");
        Assert.Equal(32, height, 1);
    }

    [Fact]
    public void AShortcutFollowsItsRow()
    {
        (double size, double before, double after, int starts) = InMenu(false, false, (window, top, rows) =>
        {
            MenuItem carrying = rows[0];
            var shortcut = (TextBlock)carrying.Template.FindName("InputGestureText", carrying);
            double width = shortcut.ActualWidth;
            foreach (MenuItem row in rows)
            {
                row.FontSize = 31.5;
            }

            Realize(top);
            return (
                shortcut.FontSize,
                width,
                shortcut.ActualWidth,
                rows.Select(row => Math.Round(LabelOf(row).TranslatePoint(default, Surface(top)).X, 1)).Distinct().Count());
        });

        Assert.Equal(31.5, size);
        Assert.True(after > before, $"the shortcut stayed {after:F0} wide at 31.5.");
        Assert.Equal(1, starts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADisabledRowsTextClearsTheFloor(bool dark)
    {
        (Color text, Color surface) = InMenu(dark, false, (window, top, rows) =>
        {
            MenuItem carrying = rows[0];
            carrying.IsEnabled = false;
            Realize(top);
            var shortcut = (TextBlock)carrying.Template.FindName("InputGestureText", carrying);
            return (
                Assert.IsType<SolidColorBrush>(shortcut.Foreground).Color,
                Assert.IsType<SolidColorBrush>(window.FindResource("FlyoutBackground")).Color);
        });

        double ratio = Contrast(text, surface);
        Assert.True(ratio >= 4.5, $"{text} on {surface} is {ratio:F2}:1.");
    }

    [Fact]
    public void AMenusShadowSitsBesideItsContent()
    {
        (Effect? content, object hint, Effect? beside, object shadow) = InMenu(false, false, (window, top, rows) =>
        {
            var surface = (Control)Surface(top);
            var border = (Border)surface.Template.FindName("Surface", surface);
            var sibling = (Border)surface.Template.FindName("Shadow", surface);
            return (border.Effect, (object)RenderOptions.GetClearTypeHint(border), sibling.Effect, window.FindResource("Slate.Shadow16"));
        });

        Assert.Null(content);
        Assert.Equal(ClearTypeHint.Enabled, hint);
        Assert.Same(shadow, beside);
    }

    /// <summary>The hint on the surface doesn't reach the text through the scroller's clip,
    /// so menu text was greyscale with the shadow and the hint both in place.
    /// Rendering isn't available to a hosted fact, so this pins where the hint is, and a
    /// launched capture measures the fringes.</summary>
    [Fact]
    public void AMenusItemsHostHasTheHintBelowItsClip()
    {
        (object top, object submenu) = InMenu(false, false, (window, top, rows) =>
        {
            MenuItem header = rows[3];
            Realize(top);
            header.ApplyTemplate();
            return (
                (object)RenderOptions.GetClearTypeHint((DependencyObject)top.Template.FindName("ItemsPresenter", top)),
                (object)RenderOptions.GetClearTypeHint((DependencyObject)header.Template.FindName("ItemsPresenter", header)));
        });

        Assert.Equal(ClearTypeHint.Enabled, top);
        Assert.Equal(ClearTypeHint.Enabled, submenu);
    }

    /// <summary>A context menu's panel is its items host, so it can't clip either, or the
    /// hint on it doesn't reach the text.</summary>
    [Fact]
    public void AContextMenusPanelHasTheHintAndNoClip()
    {
        (object hint, bool clips) = InMenu(false, false, (window, top, rows) =>
        {
            var menu = new ContextMenu();
            var host = new Border { ContextMenu = menu };
            ((DockPanel)window.Content).Children.Add(host);
            menu.Style = (Style)window.FindResource(typeof(ContextMenu));
            menu.ApplyTemplate();
            menu.Measure(new Size(400, 400));
            StackPanel panel = ShownShell.Descendants(menu).OfType<StackPanel>().Single(candidate => candidate.IsItemsHost);
            return ((object)RenderOptions.GetClearTypeHint(panel), panel.ClipToBounds);
        });

        Assert.Equal(ClearTypeHint.Enabled, hint);
        Assert.False(clips, "the panel clips, which turned the menu's ClearType off.");
    }

    /// <summary>WPF's own modes flip without flipping their offsets, which left a surface
    /// some 48 from its item at a display's edge, a gutter for the pointer to cross. These
    /// are the surface's edges, past the shadow's room, in the device pixels WPF deals in.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void ABarMenusSurfaceMeetsItsItemBelowOrAbove(double scale)
    {
        var popup = new Size(300 * scale, 500 * scale);
        var target = new Size(60 * scale, 32 * scale);

        System.Windows.Controls.Primitives.CustomPopupPlacement[] candidates =
            MenuPlacement.Candidates(MenuOpening.Vertical, popup, target, scale);

        Assert.Equal(2, candidates.Length);
        Thickness margin = MenuPlacement.SurfaceMargin;
        double below = candidates[0].Point.Y + (margin.Top * scale);
        Assert.Equal(target.Height + scale, below, 3);
        double above = candidates[1].Point.Y + popup.Height - (margin.Bottom * scale);
        Assert.Equal(-scale, above, 3);
        Assert.All(candidates, candidate => Assert.Equal(0, candidate.Point.X + (margin.Left * scale), 3));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void ASubmenusSurfaceMeetsItsRowOnEitherSide(double scale)
    {
        var popup = new Size(300 * scale, 200 * scale);
        var target = new Size(280 * scale, 34 * scale);

        System.Windows.Controls.Primitives.CustomPopupPlacement[] candidates =
            MenuPlacement.Candidates(MenuOpening.Horizontal, popup, target, scale);

        Assert.Equal(2, candidates.Length);
        Thickness margin = MenuPlacement.SurfaceMargin;
        double right = candidates[0].Point.X + (margin.Left * scale);
        Assert.Equal(target.Width, right, 3);
        double left = candidates[1].Point.X + popup.Width - (margin.Right * scale);
        Assert.Equal(0, left, 3);

        // The first row's highlight lines up with the row that opened it: the surface's
        // border and padding, and the row's own inset.
        Assert.All(candidates, candidate => Assert.Equal(0, candidate.Point.Y + ((margin.Top + 1 + 3 + 3) * scale), 3));
    }

    /// <summary>The numbers above only count if the popups use them, and the template
    /// keeps the margin they allow for.</summary>
    [Fact]
    public void EveryMenuPopupPlacesItselfAndKeepsTheMargin()
    {
        (bool bar, bool row, Thickness margin) = InMenu(false, false, (window, top, rows) =>
        {
            MenuItem header = rows[3];
            header.ApplyTemplate();
            var barPopup = (Popup)top.Template.FindName("PART_Popup", top);
            var rowPopup = (Popup)header.Template.FindName("PART_Popup", header);
            var surface = (Control)Surface(top);
            var border = (Border)surface.Template.FindName("Surface", surface);
            return (
                barPopup.Placement == PlacementMode.Custom && barPopup.CustomPopupPlacementCallback is not null && MenuPlacement.GetOpens(barPopup) == MenuOpening.Vertical,
                rowPopup.Placement == PlacementMode.Custom && rowPopup.CustomPopupPlacementCallback is not null && MenuPlacement.GetOpens(rowPopup) == MenuOpening.Horizontal,
                ((FrameworkElement)border.Parent).Margin);
        });

        Assert.True(bar, "a bar item's popup isn't placed by MenuPlacement.");
        Assert.True(row, "a row's popup isn't placed by MenuPlacement.");
        Assert.Equal(MenuPlacement.SurfaceMargin, margin);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AMenuHonoursAnimationsOff(bool animates, bool slides)
    {
        Func<bool> saved = ShellMotion.Current.ReadForTests(() => animates);
        try
        {
            bool moving = InMenu(false, false, (window, top, rows) =>
            {
                var surface = (FrameworkElement)Surface(top);
                return ((TranslateTransform)surface.RenderTransform).HasAnimatedProperties;
            });

            Assert.Equal(slides, moving);
        }
        finally
        {
            ShellMotion.Current.ReadForTests(saved);
        }
    }

    private static T InMenu<T>(bool dark, bool highContrast, Func<Window, MenuItem, MenuItem[], T> body) =>
        InMenu(dark, highContrast, plainOnly: false, body);

    private static T InMenu<T>(bool dark, bool highContrast, bool plainOnly, Func<Window, MenuItem, MenuItem[], T> body) =>
        StaThread.Run(
            () =>
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
                var window = new Window
                {
                    Width = 800,
                    Height = 600,
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

                MenuItem[] rows = plainOnly
                    ?
                    [
                        new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" },
                        new MenuItem { Header = "Paste" },
                    ]
                    :
                    [
                        new MenuItem { Header = "Copy", InputGestureText = "Ctrl+C" },
                        new CheckMenuItem { Header = "Spelling", IsCheckable = true },
                        new CheckMenuItem { Header = "Verbose", IsCheckable = true, IsChecked = true },
                        new MenuItem { Header = "Preamble", Items = { new MenuItem { Header = "Inner" } } },
                    ];
                var top = new MenuItem { Header = "Editor" };
                foreach (MenuItem row in rows)
                {
                    top.Items.Add(row);
                }

                var menu = new Menu { Items = { top } };
                window.Content = new DockPanel { Children = { menu } };
                try
                {
                    window.Show();
                    Pump();
                    top.IsSubmenuOpen = true;
                    Pump();
                    Realize(top);
                    return body(window, top, rows);
                }
                finally
                {
                    top.IsSubmenuOpen = false;
                    window.Close();
                    Pump();
                }
            },
            TimeSpan.FromSeconds(30),
            "the menu timed out.");

    private static UIElement Surface(MenuItem parent) =>
        ((Popup)parent.Template.FindName("PART_Popup", parent)).Child;

    private static FrameworkElement LabelOf(MenuItem row) =>
        (FrameworkElement)row.Template.FindName("HeaderHost", row);

    private static void Realize(MenuItem parent)
    {
        parent.ApplyTemplate();
        var surface = (FrameworkElement)Surface(parent);
        surface.Measure(new Size(600, 900));
        surface.Arrange(new Rect(surface.DesiredSize));
        surface.UpdateLayout();
        Pump();
    }

    private static double Contrast(Color text, Color surface)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Luminance(Color c) => (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));

        double alpha = text.A / 255.0;
        double over = (alpha * Luminance(text)) + ((1 - alpha) * Luminance(surface));
        double under = Luminance(surface);
        return (Math.Max(over, under) + 0.05) / (Math.Min(over, under) + 0.05);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
