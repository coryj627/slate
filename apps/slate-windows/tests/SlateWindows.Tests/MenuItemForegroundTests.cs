// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-5 (#1239): the first human NVDA field pass, in the dark theme,
// found the Editor menu's ENABLED items drawn dim and its DISABLED
// items drawn bright. Keyboard navigation skips disabled items, so the
// reader landed only on the dim ones and, hearing no "unavailable",
// concluded the disabled items were the ones taking focus. The Slate
// dictionaries define no menu brushes; everything the menu draws comes
// from Fluent, resolved through the window's resource chain. This test
// renders a real menu through the REAL dictionaries — Fluent then Slate,
// the ThemeManager's merge order — and pins the one contract that
// matters to a reader with low vision: enabled text is the brighter of
// the two against the popup, in every appearance.

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace SlateWindows.Tests;

public sealed class MenuItemForegroundTests
{
    private const string FluentRoot =
        "pack://application:,,,/PresentationFramework.Fluent;component/Themes/";
    private const string SlateRoot =
        "pack://application:,,,/SlateWindows;component/Themes/";

    [Theory]
    [InlineData("Fluent.Dark.xaml", "Slate.Dark.xaml")]
    [InlineData("Fluent.Light.xaml", "Slate.Light.xaml")]
    public void EnabledMenuItemsAreBrighterThanDisabledOnes(string fluent, string slate)
    {
        Measurement measured = OnStaThread(() => Measure(fluent, slate));

        Assert.True(measured.EnabledIsEnabled, "the enabled item reports IsEnabled == false");
        Assert.False(measured.DisabledIsEnabled, "the disabled item reports IsEnabled == true");

        double popup = Luminance(measured.PopupBackground, measured.PopupBackground);
        double enabled = Math.Abs(Luminance(measured.EnabledForeground, measured.PopupBackground) - popup);
        double disabled = Math.Abs(Luminance(measured.DisabledForeground, measured.PopupBackground) - popup);
        Assert.True(
            enabled > disabled,
            $"{slate}: enabled menu text ({measured.EnabledForeground}, |dL| {enabled:F3}) is not "
            + $"brighter than disabled text ({measured.DisabledForeground}, |dL| {disabled:F3}) "
            + $"against the popup ({measured.PopupBackground}). {measured.Diagnosis}");
    }

    private sealed record Measurement(
        bool EnabledIsEnabled,
        bool DisabledIsEnabled,
        Color EnabledForeground,
        Color DisabledForeground,
        Color PopupBackground,
        string Diagnosis);

    private static Measurement Measure(string fluent, string slate)
    {
        // Runs Application's static constructor (registers the pack:
        // scheme and its application authority) without constructing an
        // Application — see TextBoxAccessibilityTests for why the
        // alternatives break other WPF tests by run order.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Application).TypeHandle);

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
        // The window's OWN resources stand in for Application.Resources so
        // the process-wide theme is left alone; the merge order is the
        // ThemeManager's (Fluent first, Slate last).
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(FluentRoot + fluent, UriKind.Absolute),
        });
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(SlateRoot + slate, UriKind.Absolute),
        });
        window.SetResourceReference(Control.BackgroundProperty, "Slate.WindowBackgroundBrush");
        window.SetResourceReference(Control.ForegroundProperty, "Slate.TextBrush");

        var enabledItem = new MenuItem
        {
            Header = "Enabled",
            Command = new RelayCommand(_ => { }, _ => true),
        };
        var disabledItem = new MenuItem
        {
            Header = "Disabled",
            Command = new RelayCommand(_ => { }, _ => false),
        };
        var top = new MenuItem { Header = "Editor" };
        top.Items.Add(enabledItem);
        top.Items.Add(disabledItem);
        var menu = new Menu();
        // MainWindow.xaml gives its Menu a LOCAL style (the modal-surface
        // disable block); mirrored here so the measurement is of the
        // shipped shape, not of Fluent's implicit style.
        var menuStyle = new Style(typeof(Menu));
        menuStyle.Setters.Add(new Setter(UIElement.IsEnabledProperty, true));
        menu.Style = menuStyle;
        // ... and whatever Foreground reference the shipped Menu declares
        // (none, before the fix), read from the source so this test fails
        // the day the declaration is dropped rather than measuring a mirror.
        if (DeclaredMenuForegroundKey() is { } key)
        {
            menu.SetResourceReference(Control.ForegroundProperty, key);
        }
        menu.Items.Add(top);
        window.Content = new DockPanel { Children = { menu } };

        try
        {
            window.Show();
            Pump();
            top.IsSubmenuOpen = true;
            Pump();

            Popup popup = FindPopup(top)
                ?? throw new Xunit.Sdk.XunitException("the submenu popup did not open.");
            return new Measurement(
                enabledItem.IsEnabled,
                disabledItem.IsEnabled,
                HeaderForeground(enabledItem),
                HeaderForeground(disabledItem),
                PopupBackground(popup, window),
                Diagnose(menu, top, enabledItem));
        }
        finally
        {
            top.IsSubmenuOpen = false;
            window.Close();
            Pump();
        }
    }

    /// <summary>The <c>Foreground="{DynamicResource X}"</c> key on
    /// <c>MainMenu</c> in MainWindow.xaml, or null when the bar declares
    /// no foreground and inherits the OS theme's MenuText.</summary>
    private static string? DeclaredMenuForegroundKey()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        XElement menu = window.Descendants()
            .Single(element => element.Name.LocalName == "Menu"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "MainMenu");
        Match reference = Regex.Match(
            (string?)menu.Attribute("Foreground") ?? "",
            @"^\{DynamicResource\s+([^}\s]+)\s*\}$");
        return reference.Success ? reference.Groups[1].Value : null;
    }

    /// <summary>The colour the header text is actually drawn in: the
    /// first TextBlock under the item's own visual, composited over
    /// nothing (alpha is folded into the luminance by the caller).</summary>
    private static Color HeaderForeground(MenuItem item)
    {
        TextBlock text = Descendants(item).OfType<TextBlock>().FirstOrDefault()
            ?? throw new Xunit.Sdk.XunitException(
                $"no TextBlock rendered the header of '{item.Header}'.");
        return text.Foreground is SolidColorBrush brush
            ? brush.Color
            : throw new Xunit.Sdk.XunitException(
                $"'{item.Header}' draws its header with {text.Foreground?.GetType().Name ?? "no brush"}.");
    }

    /// <summary>Where each Foreground along the chain comes from, for the
    /// failure message: the fix depends on WHICH layer sets the colour.</summary>
    private static string Diagnose(Menu menu, MenuItem top, MenuItem item)
    {
        static string Describe(DependencyObject element, string label)
        {
            ValueSource source = DependencyPropertyHelper.GetValueSource(element, Control.ForegroundProperty);
            string colour = element.GetValue(Control.ForegroundProperty) is SolidColorBrush brush
                ? brush.Color.ToString()
                : "(not a solid brush)";
            return $"{label}={colour} via {source.BaseValueSource}";
        }

        TextBlock? text = Descendants(item).OfType<TextBlock>().FirstOrDefault();
        string textSource = text is null
            ? "text=(none)"
            : $"text via {DependencyPropertyHelper.GetValueSource(text, TextBlock.ForegroundProperty).BaseValueSource}";
        return $"[{Describe(menu, "menu")}; {Describe(top, "top")}; {Describe(item, "item")}; {textSource}]";
    }

    private static Color PopupBackground(Popup popup, Window window)
    {
        foreach (DependencyObject visual in Descendants(popup.Child))
        {
            if (visual is Border { Background: SolidColorBrush brush } && brush.Color.A > 0)
            {
                return brush.Color;
            }
        }

        return window.Background is SolidColorBrush fallback
            ? fallback.Color
            : Colors.Black;
    }

    private static Popup? FindPopup(MenuItem top)
    {
        top.ApplyTemplate();
        return Descendants(top).OfType<Popup>().FirstOrDefault()
            ?? top.Template?.FindName("PART_Popup", top) as Popup;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject? root)
    {
        if (root is null)
        {
            yield break;
        }

        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            DependencyObject current = pending.Pop();
            yield return current;
            int count = current is Visual ? VisualTreeHelper.GetChildrenCount(current) : 0;
            for (int index = count - 1; index >= 0; index--)
            {
                pending.Push(VisualTreeHelper.GetChild(current, index));
            }

            // A Popup's child is not a visual child of the popup: cross the
            // seam explicitly so the submenu's items are reachable.
            if (current is Popup { Child: { } child })
            {
                pending.Push(child);
            }
        }
    }

    private static double Luminance(Color color, Color over)
    {
        // WCAG relative luminance of the colour COMPOSITED over the popup:
        // Fluent's disabled text is a translucent brush, so it lands
        // between its own colour and the background.
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double Opaque(Color c) =>
            0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        double alpha = color.A / 255.0;
        return alpha * Opaque(color) + (1 - alpha) * Opaque(over);
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static T OnStaThread<T>(Func<T> body)
    {
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "menu rendering timed out.");
        return failure is null
            ? result!
            : throw new Xunit.Sdk.XunitException(failure.ToString());
    }
}
