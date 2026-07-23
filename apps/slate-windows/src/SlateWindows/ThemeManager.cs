// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Security;
using System.Windows;
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
    private ResourceDictionary? _fluentResources;
    private ResourceDictionary? _slateResources;
    private SlateTheme _theme;

    internal static event EventHandler? ResourcesChanged;

    private static readonly string[] RequiredSlateBrushKeys =
    [
        "Slate.WindowBackgroundBrush",
        "Slate.SurfaceBrush",
        "Slate.RaisedSurfaceBrush",
        "Slate.TextBrush",
        "Slate.SecondaryTextBrush",
        "Slate.BorderBrush",
        "Slate.AccentBrush",
        "Slate.SelectionBackgroundBrush",
        "Slate.SelectionTextBrush",
        "Slate.FocusBrush",
        "Slate.ErrorBrush",
        EditorSyntaxPalette.HeadingBrushKey,
        EditorSyntaxPalette.CodeBrushKey,
        EditorSyntaxPalette.WikilinkBrushKey,
        EditorSyntaxPalette.TagBrushKey,
        EditorSyntaxPalette.MetadataBrushKey,
    ];

    public ThemeManager(Application application, SlateTheme theme)
    {
        _application = application;
        _theme = theme;
        ApplyResources();
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
    }

    public void SetTheme(SlateTheme theme)
    {
        if (_theme == theme)
        {
            return;
        }

        _theme = theme;
        ApplyResources();
    }

    public void Dispose()
    {
        SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
    }

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

        void RefreshSystemTheme()
        {
            SlateTheme next = ReadSystemTheme();
            if (_theme != next)
            {
                _theme = next;
                ApplyResources();
            }
        }

        if (_application.Dispatcher.CheckAccess())
        {
            RefreshSystemTheme();
        }
        else
        {
            _ = _application.Dispatcher.InvokeAsync(RefreshSystemTheme);
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

    private void ApplyResources()
    {
        bool highContrast = SystemParameters.HighContrast;
        string fluentName = highContrast
            ? "Fluent.HC.xaml"
            : _theme == SlateTheme.Dark ? "Fluent.Dark.xaml" : "Fluent.Light.xaml";
        string slateName = highContrast
            ? "Slate.Contrast.xaml"
            : _theme == SlateTheme.Dark ? "Slate.Dark.xaml" : "Slate.Light.xaml";

        ResourceDictionary fluentResources = LoadDictionary(FluentRoot + fluentName);
        ResourceDictionary slateResources = LoadDictionary(SlateRoot + slateName);

        var dictionaries = _application.Resources.MergedDictionaries;
        if (_fluentResources is not null)
        {
            dictionaries.Remove(_fluentResources);
        }

        if (_slateResources is not null)
        {
            dictionaries.Remove(_slateResources);
        }

        // Fluent first; Slate tokens last so Slate-owned surfaces always
        // resolve through our explicit light/dark/Contrast layer.
        dictionaries.Add(fluentResources);
        dictionaries.Add(slateResources);
        _fluentResources = fluentResources;
        _slateResources = slateResources;
        ResourcesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static ResourceDictionary LoadDictionary(string uri) => new()
    {
        Source = new Uri(uri, UriKind.Absolute),
    };
}
