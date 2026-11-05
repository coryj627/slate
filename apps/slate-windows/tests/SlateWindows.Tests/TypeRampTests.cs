// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>DS-2: the shell has one type scale, and text with no size of its own is
/// body text, 14 in Segoe UI Variable Text, instead of the 12 in plain Segoe UI it
/// gets from the system.</summary>
public sealed class TypeRampTests
{
    private static readonly Dictionary<string, string> Tokens = new()
    {
        ["Caption"] = "caption",
        ["Body"] = "body",
        ["BodyStrong"] = "body-strong",
        ["BodyLarge"] = "body-large",
        ["BodyLargeStrong"] = "body-large-strong",
        ["Subtitle"] = "subtitle",
        ["Title"] = "title",
    };

    public static TheoryData<string> StepNames() => new(TypeRamp.Steps.Select(step => step.Name));

    /// <summary>The design's tokens are the source. If they and the app differ, this
    /// fails, and a change to the tokens runs this lane.</summary>
    [Theory]
    [MemberData(nameof(StepNames))]
    public void EveryRampStepIsItsToken(string name)
    {
        string css = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "design", "windows", "tokens", "typography.css"));
        TypeRampStep step = TypeRamp.Steps.Single(candidate => candidate.Name == name);
        Match token = Regex.Match(css, $@"--type-{Tokens[name]}:(\d+) (\d+)px/(\d+)px var\((--font-ui[a-z-]*)\)");
        Assert.True(token.Success, $"typography.css has no --type-{Tokens[name]}.");

        Assert.Equal(double.Parse(token.Groups[1].Value), step.Weight.ToOpenTypeWeight());
        Assert.Equal(double.Parse(token.Groups[2].Value), step.Size);
        Assert.Equal(double.Parse(token.Groups[3].Value), step.LineHeight);

        Match family = Regex.Match(css, Regex.Escape(token.Groups[4].Value) + @":""([^""]+)"",""([^""]+)""");
        Assert.True(family.Success, $"typography.css has no {token.Groups[4].Value}.");
        Assert.Equal($"{family.Groups[1].Value}, {family.Groups[2].Value}", step.Family.Source);
    }

    /// <summary>Everything DS-5 scales is a key, so a resource the styles read can't
    /// go missing from one appearance.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EveryStepsStyleReadsItsKeys(bool dark, bool highContrast)
    {
        string[] wrong = InRealWindow(dark, highContrast, window =>
        {
            var panel = new StackPanel();
            window.Content = panel;
            var problems = new List<string>();
            foreach (TypeRampStep step in TypeRamp.Steps)
            {
                var text = new TextBlock { Text = "Ag", Style = (Style)window.FindResource(step.StyleKey) };
                panel.Children.Add(text);
                window.UpdateLayout();
                if (text.FontSize != step.Size
                    || text.LineHeight != step.LineHeight
                    || text.LineStackingStrategy != LineStackingStrategy.BlockLineHeight
                    || text.FontWeight != step.Weight
                    || text.FontFamily.Source != step.Family.Source)
                {
                    problems.Add($"{step.Name}: {text.FontSize}/{text.LineHeight} {text.LineStackingStrategy} {text.FontWeight} {text.FontFamily.Source}");
                }
            }

            return problems.ToArray();
        });

        Assert.Empty(wrong);
    }

    [Fact]
    public void TheShellsDefaultTextIsBody() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        var plain = new TextBlock { Text = "Unset" };
        var button = new Button { Content = "Unset" };
        var dock = (DockPanel)host.Shell.Content;
        dock.Children.Insert(0, plain);
        dock.Children.Insert(0, button);
        host.Settle();

        var menu = (Menu)host.Shell.FindName("MainMenu");
        var status = (FrameworkElement)host.Shell.FindName("ShellStatusBar");
        var topLevel = (MenuItem)menu.Items[0];
        foreach ((string what, double size, FontFamily family) in new (string, double, FontFamily)[]
        {
            ("an unset TextBlock", plain.FontSize, plain.FontFamily),
            ("an unset Button", button.FontSize, button.FontFamily),
            ("the menu bar", menu.FontSize, menu.FontFamily),
            ("a top-level menu item", topLevel.FontSize, topLevel.FontFamily),
        })
        {
            Assert.True(size == 14, $"{what} is {size}.");
            Assert.StartsWith("Segoe UI Variable Text", family.Source, StringComparison.Ordinal);
        }

        // DS-7: the status bar is the design's 12/16 row, so its step is Caption.
        Assert.Equal(TypeRamp.Caption.Size, (double)status.GetValue(Control.FontSizeProperty));
        Assert.StartsWith("Segoe UI Variable Small", ((FontFamily)status.GetValue(Control.FontFamilyProperty)).Source, StringComparison.Ordinal);
    });

    /// <summary>A default style's setter beats inheritance, as the status bar's and
    /// the classic menu's did, so a control like that never hears the window's size.
    /// This finds any the list above missed.</summary>
    [Fact]
    public void NoControlTakesItsSizeFromTheSystem() => StaThread.Run(() =>
    {
        using var host = new ShownShell(("Lecture.md", "# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();

        string[] fromTheSystem = [.. ShownShell.Descendants(host.Shell)
            .OfType<Control>()
            .Where(control => DependencyPropertyHelper.GetValueSource(control, Control.FontSizeProperty).BaseValueSource == BaseValueSource.DefaultStyle)
            .Select(control => control.GetType().Name)
            .Distinct()];
        Assert.True(fromTheSystem.Length == 0, "Sized by a default style: " + string.Join(", ", fromTheSystem));
    });

    [Fact]
    public void TheSizeFollowsTheStartupFactor() => StaThread.Run(() =>
    {
        Assert.Equal(31.5, (double)TypeRamp.DictionaryFor(2.25)[TypeRamp.Body.SizeKey]);
        Assert.Equal(45.0, (double)TypeRamp.DictionaryFor(2.25)[TypeRamp.Body.LineHeightKey]);
        RegisterPackScheme();
        ResourceDictionary[] scaled = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false, textScale: 2.25);
        ResourceDictionary[] plain = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false);
        Assert.Equal(31.5, (double)scaled.First(layer => layer.Contains(TypeRamp.Body.SizeKey))[TypeRamp.Body.SizeKey]);
        Assert.Equal(14.0, (double)plain.First(layer => layer.Contains(TypeRamp.Body.SizeKey))[TypeRamp.Body.SizeKey]);
    });

    /// <summary>Neither is in the window's tree, so the window's size can't reach them,
    /// and neither may have a parent, so each reads the theme's implicit style with the
    /// ramp's keys in its own resources, as the app's reach it from the application.</summary>
    [Fact]
    public void AContextMenuAndATooltipUseTheRamp()
    {
        (double menuSize, string menuFamily, double tipSize, string tipFamily) = InRealWindow(dark: false, highContrast: false, window =>
        {
            var menu = new ContextMenu { Style = (Style)window.FindResource(typeof(ContextMenu)) };
            menu.Resources.MergedDictionaries.Add(TypeRamp.DictionaryFor());
            var tip = new ToolTip { Style = (Style)window.FindResource(typeof(ToolTip)) };
            tip.Resources.MergedDictionaries.Add(TypeRamp.DictionaryFor());
            return (menu.FontSize, menu.FontFamily.Source, tip.FontSize, tip.FontFamily.Source);
        });

        Assert.Equal(TypeRamp.Body.Size, menuSize);
        Assert.Equal(TypeRamp.TextFamily.Source, menuFamily);
        Assert.Equal(TypeRamp.Caption.Size, tipSize);
        Assert.Equal(TypeRamp.SmallFamily.Source, tipFamily);
    }

    /// <summary>The table's own window, with the ramp where the app's application
    /// resources would put it: a hosted window has no Application above it.</summary>
    [Fact]
    public void ATableWindowTakesTheRamp() => StaThread.Run(() =>
    {
        RegisterPackScheme();
        Window? window = SlateWindows.Reading.ReadingTableGrid.Show("| a | b |\n|---|---|\n| 1 | 2 |\n", owner: null, activate: false);
        Assert.NotNull(window);
        try
        {
            window.Resources.MergedDictionaries.Add(TypeRamp.DictionaryFor(2.25));
            Assert.Equal(31.5, window.FontSize);
            Assert.Equal(TypeRamp.TextFamily.Source, window.FontFamily.Source);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public void AHeadingStyleCarriesItsLineHeight()
    {
        double height = InRealWindow(dark: false, highContrast: false, window =>
        {
            var heading = new TextBlock
            {
                Text = "A heading long enough to wrap onto more than one line",
                TextWrapping = TextWrapping.Wrap,
                Width = 120,
                Style = (Style)window.FindResource(TypeRamp.Subtitle.StyleKey),
            };
            window.Content = new StackPanel { Children = { heading } };
            window.UpdateLayout();
            return heading.ActualHeight;
        });

        Assert.True(height >= 56, $"the heading didn't wrap: {height}.");
        Assert.Equal(0, height % 28);
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
                Height = 400,
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
