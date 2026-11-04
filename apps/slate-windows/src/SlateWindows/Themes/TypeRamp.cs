// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SlateWindows;

/// <summary>One step of the design's type ramp. Sizes and line heights are at
/// 100%; <see cref="TypeRamp.DictionaryFor"/> scales them.</summary>
internal sealed record TypeRampStep(string Name, double Size, double LineHeight, FontFamily Family, FontWeight Weight)
{
    internal string SizeKey => "Slate.FontSize" + Name;

    internal string LineHeightKey => "Slate.LineHeight" + Name;

    internal string StyleKey => $"Slate.{Name}TextBlockStyle";

    internal string FamilyKey =>
        ReferenceEquals(Family, TypeRamp.SmallFamily) ? TypeRamp.SmallFamilyKey
        : ReferenceEquals(Family, TypeRamp.DisplayFamily) ? TypeRamp.DisplayFamilyKey
        : TypeRamp.TextFamilyKey;
}

/// <summary>One step of the reading view's own scale, the tokens' <c>--reader-*</c>.
/// They aren't ramp steps, because the ramp is for chrome. They're keys so text size
/// scales them with everything else.</summary>
internal sealed record ReaderStep(string Name, double Size, double LineHeight)
{
    internal string SizeKey => "Slate.FontSizeReader" + Name;

    internal string LineHeightKey => "Slate.LineHeightReader" + Name;
}

/// <summary>
/// DS-2: the shell's one type scale, the WinUI ramp the design adopted
/// (design/windows/tokens/typography.css), as dynamic resources so text size can
/// scale it. Fluent's own TextBlock styles take their sizes by StaticResource, so
/// they can't. The app merges this first, for every appearance, and
/// Slate.Text.xaml's styles read it. Only the steps a surface uses are here.
/// </summary>
internal static class TypeRamp
{
    internal const string TextFamilyKey = "Slate.FontFamilyText";
    internal const string SmallFamilyKey = "Slate.FontFamilySmall";
    internal const string DisplayFamilyKey = "Slate.FontFamilyDisplay";

    internal static readonly FontFamily TextFamily = new("Segoe UI Variable Text, Segoe UI");
    internal static readonly FontFamily SmallFamily = new("Segoe UI Variable Small, Segoe UI");
    internal static readonly FontFamily DisplayFamily = new("Segoe UI Variable Display, Segoe UI");

    internal static readonly TypeRampStep Caption = new("Caption", 12, 16, SmallFamily, FontWeights.Normal);
    internal static readonly TypeRampStep Body = new("Body", 14, 20, TextFamily, FontWeights.Normal);
    internal static readonly TypeRampStep BodyStrong = new("BodyStrong", 14, 20, TextFamily, FontWeights.SemiBold);
    internal static readonly TypeRampStep BodyLarge = new("BodyLarge", 18, 24, TextFamily, FontWeights.Normal);
    internal static readonly TypeRampStep BodyLargeStrong = new("BodyLargeStrong", 18, 24, TextFamily, FontWeights.SemiBold);
    internal static readonly TypeRampStep Subtitle = new("Subtitle", 20, 28, DisplayFamily, FontWeights.SemiBold);
    internal static readonly TypeRampStep Title = new("Title", 28, 36, DisplayFamily, FontWeights.SemiBold);

    internal static readonly IReadOnlyList<TypeRampStep> Steps =
        [Caption, Body, BodyStrong, BodyLarge, BodyLargeStrong, Subtitle, Title];

    internal static readonly ReaderStep ReaderBody = new("Body", 16, 26);
    internal static readonly ReaderStep ReaderH1 = new("H1", 32, 40);
    internal static readonly ReaderStep ReaderH2 = new("H2", 24, 32);
    internal static readonly ReaderStep ReaderH3 = new("H3", 20, 28);
    internal static readonly ReaderStep ReaderH4 = new("H4", 18, 26);
    internal static readonly ReaderStep ReaderSmall = new("Small", 14, 22);

    internal static readonly IReadOnlyList<ReaderStep> ReaderSteps =
        [ReaderBody, ReaderH1, ReaderH2, ReaderH3, ReaderH4, ReaderSmall];

    /// <summary>A heading's step. Levels 5 and 6 fold into H4, the smallest the
    /// tokens name.</summary>
    internal static ReaderStep ReaderHeading(byte level) => level switch
    {
        1 => ReaderH1,
        2 => ReaderH2,
        3 => ReaderH3,
        _ => ReaderH4,
    };

    /// <summary><paramref name="fluent"/> with <paramref name="step"/>'s family and
    /// size. Built here because a dictionary can't reach a sibling's style.</summary>
    internal static Style StyleOn(Style fluent, TypeRampStep step)
    {
        var style = new Style(fluent.TargetType, fluent);
        style.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(step.FamilyKey)));
        style.Setters.Add(new Setter(Control.FontSizeProperty, new DynamicResourceExtension(step.SizeKey)));
        return style;
    }

    /// <summary>A window of the app's own: nothing above it sets a size, so it sets
    /// Body itself, as the main window's XAML does. <c>TypeRampCensus</c> holds every
    /// window to this.</summary>
    internal static void UseBody(Window window)
    {
        window.SetResourceReference(Control.FontFamilyProperty, TextFamilyKey);
        window.SetResourceReference(Control.FontSizeProperty, Body.SizeKey);
    }

    /// <summary>The ramp's keys at <paramref name="factor"/> times their size.</summary>
    internal static ResourceDictionary DictionaryFor(double factor = 1)
    {
        var ramp = new ResourceDictionary
        {
            [TextFamilyKey] = TextFamily,
            [SmallFamilyKey] = SmallFamily,
            [DisplayFamilyKey] = DisplayFamily,
        };
        foreach (TypeRampStep step in Steps)
        {
            ramp[step.SizeKey] = step.Size * factor;
            ramp[step.LineHeightKey] = step.LineHeight * factor;
        }

        foreach (ReaderStep step in ReaderSteps)
        {
            ramp[step.SizeKey] = step.Size * factor;
            ramp[step.LineHeightKey] = step.LineHeight * factor;
        }

        return ramp;
    }
}
