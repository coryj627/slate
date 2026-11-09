// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;

namespace SlateWindows;

internal enum SlateTheme
{
    Light,
    Dark,
}

/// <summary>
/// Layers Slate-owned tokens over the first-party WPF Fluent dictionaries.
/// High Contrast is reactive in both directions and never leaves a
/// Slate-drawn surface using a fixed light/dark brush.
/// </summary>
internal sealed class ThemeManager : IDisposable
{
    private const string FluentRoot =
        "pack://application:,,,/PresentationFramework.Fluent;component/Themes/";
    private const string SlateRoot =
        "pack://application:,,,/SlateWindows;component/Themes/";

    private readonly Application _application;
    private readonly TextScaleService? _textScale;
    private ResourceDictionary[] _resources = [];
    private string _preference;
    private SlateTheme _theme;
    private AccentSource _accent;
    private AccentPalette _palette;

    internal static event EventHandler? ResourcesChanged;

    /// <summary>The app's manager. <see langword="null"/> under test, which can't build one.</summary>
    internal static ThemeManager? Current { get; private set; }

    private static readonly string[] RequiredSlateBrushKeys =
    [
        "Slate.WindowBackgroundBrush",
        "Slate.SurfaceBrush",
        "Slate.RaisedSurfaceBrush",
        "Slate.TextBrush",
        "Slate.SecondaryTextBrush",
        "Slate.BorderBrush",
        "Slate.AccentBrush",
        "Slate.AccentTextBrush",
        "Slate.SelectedSurfaceBrush",
        "Slate.SelectedTextBrush",
        "Slate.SelectedCaptionBrush",
        "Slate.AccentIndicatorBrush",
        "Slate.SelectionBackgroundBrush",
        "Slate.SelectionTextBrush",
        TextBoxSelection.ReadingBrushKey,
        "Slate.FocusBrush",
        "Slate.ErrorBrush",
        "Slate.WarningBrush",
        EditorSyntaxPalette.HeadingBrushKey,
        EditorSyntaxPalette.CodeBrushKey,
        EditorSyntaxPalette.WikilinkBrushKey,
        EditorSyntaxPalette.TagBrushKey,
        EditorSyntaxPalette.MetadataBrushKey,
        // W3-4 reading-view token palette (mac CodeTokenTheme parity;
        // the Contrast dictionary collapses all six to window text,
        // mirroring the mac increase-contrast rule).
        "Slate.CodeKeywordBrush",
        "Slate.CodeStringBrush",
        "Slate.CodeNumberBrush",
        "Slate.CodeCommentBrush",
        "Slate.CodeTypeBrush",
        "Slate.CodeFunctionBrush",
    ];

    /// <param name="application">The application whose resources this owns.</param>
    /// <param name="preference">DS-10: what the user chose, read before the first paint.</param>
    /// <param name="accent">DS-11: the same for the accent.</param>
    public ThemeManager(Application application, string preference, string accent)
    {
        _application = application;
        _preference = ThemeChoice.Normalize(preference);
        _theme = ResolvePreference();
        _accent = SlateAccent.SourceOf(accent);
        _palette = AccentPalette.ReadSystem();
        Current = this;
        _textScale = TextScaleService.Current;
        ApplyResources();
        if (_textScale is not null)
        {
            _textScale.Changed += OnTextScaleChanged;
        }

        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    public static SlateTheme ReadSystemTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0
                ? SlateTheme.Dark
                : SlateTheme.Light;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return SlateTheme.Light;
        }
    }

    /// <summary>
    /// Startup-census seam: eagerly resolve every explicit Fluent dictionary
    /// and assert identical Slate token structure across all three modes.
    /// </summary>
    public static void ValidateResourceDictionaries()
    {
        foreach (string fluentName in
                 new[] { "Fluent.Light.xaml", "Fluent.Dark.xaml", "Fluent.HC.xaml" })
        {
            _ = LoadDictionary(FluentRoot + fluentName);
        }

        foreach (string slateName in
                 new[] { "Slate.Light.xaml", "Slate.Dark.xaml", "Slate.Contrast.xaml" })
        {
            ResourceDictionary dictionary = LoadDictionary(SlateRoot + slateName);
            foreach (string key in RequiredSlateBrushKeys)
            {
                if (!dictionary.Contains(key))
                {
                    throw new InvalidOperationException(
                        $"{slateName} is missing required token {key}.");
                }
            }
        }

        _ = LoadDictionary(SlateRoot + "Slate.Text.xaml");
        _ = LoadDictionary(SlateRoot + "Slate.Menus.xaml");
        _ = LoadDictionary(SlateRoot + "Slate.Calendar.xaml");
        _ = LoadDictionary(SlateRoot + "Slate.SystemColors.xaml");
    }

    /// <summary>DS-10: a new choice, applied at once.</summary>
    public void SetPreference(string preference)
    {
        _preference = ThemeChoice.Normalize(preference);
        ApplyPreference();
    }

    /// <summary>DS-11: a new accent choice, applied at once.</summary>
    public void SetAccent(string accent)
    {
        _accent = SlateAccent.SourceOf(accent);
        ApplyResources();
    }

    /// <summary>DS-11: applies again when Windows' accent changes, which only a window of the
    /// app's can see. WPF is still walking that window when it says so, so this goes after.
    /// See <see cref="SystemAccentWatch"/>.</summary>
    public void WatchSystemAccent(FrameworkElement root) =>
        SystemAccentWatch.Attach(root, () => _ = _application.Dispatcher.InvokeAsync(ApplyPreference));

    public void Dispose()
    {
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        if (_textScale is not null)
        {
            _textScale.Changed -= OnTextScaleChanged;
        }

        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

    /// <summary>The ramp is always the first layer. Writing to it is what makes a change
    /// live, and it isn't a theme change, so <see cref="ResourcesChanged"/> stays quiet.</summary>
    private void OnTextScaleChanged() => TypeRamp.Apply(_resources[0], _textScale!.Factor);

    private void SystemEvents_UserPreferenceChanged(
        object sender,
        UserPreferenceChangedEventArgs eventArgs)
    {
        if (eventArgs.Category is not (
            UserPreferenceCategory.Color
            or UserPreferenceCategory.General
            or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        if (_application.Dispatcher.CheckAccess())
        {
            ApplyPreference();
        }
        else
        {
            _ = _application.Dispatcher.InvokeAsync(ApplyPreference);
        }
    }

    private SlateTheme ResolvePreference() =>
        ThemeChoice.Resolve(_preference, ReadSystemTheme() == SlateTheme.Dark);

    private void ApplyPreference()
    {
        SlateTheme next = ResolvePreference();
        if (_theme != next
            || SlateAccent.ChangeNeedsReapply(_accent, _palette, AccentPalette.ReadSystem()))
        {
            _theme = next;
            ApplyResources();
        }
    }

    private void SystemParameters_StaticPropertyChanged(
        object? sender,
        System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(SystemParameters.HighContrast))
        {
            return;
        }

        if (_application.Dispatcher.CheckAccess())
        {
            ApplyResources();
        }
        else
        {
            _ = _application.Dispatcher.InvokeAsync(ApplyResources);
        }
    }

    /// <summary>
    /// The application-level dictionaries a theme resolves to, in merge
    /// order: the type ramp at <paramref name="textScale"/>, Fluent, then Slate's
    /// tokens, then outside High Contrast the system colours those tokens answer
    /// for, then Slate's text styles and menus. Only the app passes a scale, so a hosted test
    /// doesn't depend on the desktop's Text size, and only the app and a fact that asks pass
    /// an accent, so a hosted test stays gold. A fact passes its own
    /// <paramref name="palette"/>.
    /// <see cref="ApplyResources"/> merges exactly
    /// these; a hosted test merges the same layers into its own window to
    /// realize a surface under the app's real resources without touching the
    /// process-wide <see cref="Application.Resources"/>.
    /// </summary>
    internal static ResourceDictionary[] DictionariesFor(
        SlateTheme theme,
        bool highContrast,
        double textScale = 1,
        AccentSource accent = AccentSource.SlateGold,
        AccentPalette? palette = null)
    {
        string fluentName = highContrast
            ? "Fluent.HC.xaml"
            : theme == SlateTheme.Dark ? "Fluent.Dark.xaml" : "Fluent.Light.xaml";
        string slateName = highContrast
            ? "Slate.Contrast.xaml"
            : theme == SlateTheme.Dark ? "Slate.Dark.xaml" : "Slate.Light.xaml";
        ResourceDictionary slate = LoadDictionary(SlateRoot + slateName);
        if (accent != AccentSource.SlateGold)
        {
            _ = SlateAccent.TryApplyWindowsAccent(
                slate, accent, palette ?? AccentPalette.ReadSystem(), theme == SlateTheme.Dark, highContrast);
        }

        // #1769: Fluent's DataGrid animates its Disabled state to these keys, and
        // every Fluent theme gives them fixed colours, so a table behind an open
        // overlay turned white under the theme's pale text.
        slate["ControlLightColor"] = highContrast ? SystemColors.WindowColor : (Color)slate["Slate.SurfaceColor"];
        slate["DisabledForegroundColor"] = highContrast ? SystemColors.GrayTextColor : (Color)slate["Slate.DisabledTextColor"];
        ResourceDictionary fluent = LoadDictionary(FluentRoot + fluentName);
        // DS-2: this isn't in the window's tree, so the window's size can't reach it.
        // A context menu is the same, and Slate.Menus.xaml sets its size.
        if (fluent[typeof(ToolTip)] is Style toolTip)
        {
            // DS-6: and its shadow goes beside its text, so the text keeps ClearType.
            Style scaled = TypeRamp.StyleOn(toolTip, TypeRamp.Caption);
            scaled.Setters.Add(new Setter(Control.TemplateProperty, LoadDictionary(SlateRoot + "Slate.Text.xaml")["Slate.ToolTipTemplate"]));
            slate[typeof(ToolTip)] = scaled;
        }

        // DS-5: Fluent fixes a calendar's labels at 14, and a date picker asks for its
        // calendar's style by this key.
        if (fluent["DatePickerCalendarStyle"] is Style calendar)
        {
            var scaled = new Style(typeof(Calendar), calendar);
            scaled.Setters.Add(new Setter(
                Calendar.CalendarItemStyleProperty,
                LoadDictionary(SlateRoot + "Slate.Calendar.xaml")["Slate.CalendarItemStyle"]));
            scaled.Setters.Add(new Setter(
                Calendar.CalendarButtonStyleProperty,
                CalendarButtonOn((Style)fluent["DefaultCalendarButtonStyle"], 70)));
            scaled.Setters.Add(new Setter(
                Calendar.CalendarDayButtonStyleProperty,
                CalendarButtonOn((Style)fluent["DefaultCalendarDayButtonStyle"], 60)));
            slate["DatePickerCalendarStyle"] = scaled;
        }

        return highContrast
            ?
            [
                TypeRamp.DictionaryFor(textScale),
                fluent,
                slate,
                LoadDictionary(SlateRoot + "Slate.Text.xaml"),
                LoadDictionary(SlateRoot + "Slate.Menus.xaml"),
            ]
            :
            [
                TypeRamp.DictionaryFor(textScale),
                fluent,
                slate,
                LoadDictionary(SlateRoot + "Slate.SystemColors.xaml"),
                LoadDictionary(SlateRoot + "Slate.Text.xaml"),
                LoadDictionary(SlateRoot + "Slate.Menus.xaml"),
            ];
    }

    /// <summary>Fluent's date button with the ramp's size, and its largest size, which it
    /// gives at 14, following the text: a month's short name can be longer than three
    /// letters.</summary>
    private static Style CalendarButtonOn(Style fluent, double largestAtBody)
    {
        Style style = TypeRamp.StyleOn(fluent, TypeRamp.Body);
        var largest = new CappedShareConverter { Share = largestAtBody / TypeRamp.Body.Size };
        foreach (DependencyProperty cap in new[] { FrameworkElement.MaxWidthProperty, FrameworkElement.MaxHeightProperty })
        {
            style.Setters.Add(new Setter(cap, new Binding(nameof(Control.FontSize)) { RelativeSource = RelativeSource.Self, Converter = largest }));
        }

        return style;
    }

    private void ApplyResources()
    {
        _palette = AccentPalette.ReadSystem();
        ResourceDictionary[] layers = DictionariesFor(
            _theme, SystemParameters.HighContrast, TextScaleService.CurrentFactor, _accent, _palette);

        var dictionaries = _application.Resources.MergedDictionaries;
        foreach (ResourceDictionary previous in _resources)
        {
            dictionaries.Remove(previous);
        }

        // Fluent first; Slate after it so Slate-owned surfaces always
        // resolve through our explicit light/dark/Contrast layer.
        foreach (ResourceDictionary layer in layers)
        {
            dictionaries.Add(layer);
        }

        _resources = layers;
        ResourcesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ResourceDictionary LoadDictionary(string uri) => new()
    {
        Source = new Uri(uri, UriKind.Absolute),
    };
}
