// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SlateWindows;

/// <summary>
/// #1755: a text box draws its selection in the theme's solid pair. WPF's
/// default draws the brush as a 40% overlay and ignores the selection text
/// brush, which leaves the design system's pale gold almost invisible on a
/// white field. Without the overlay WPF draws selected text in
/// <see cref="TextBoxBase.SelectionTextBrush"/>, whose default is white, so
/// the two go together. Spike 6 in design/windows/wpf-feasibility.md proved the
/// switch with right-to-left text.
/// <para>A <see cref="RichTextBox"/> ignores the switch and always draws its
/// selection on top of the text, so the reading view sets its own brush
/// (<see cref="Reading.ReadingSurface"/>). This handler couldn't reach it:
/// WPF only raises Loaded on an element whose style or instance asks for it,
/// and the surface has neither.</para>
/// </summary>
internal static class TextBoxSelection
{
    internal const string AdornerSwitch =
        "Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering";
    internal const string BackgroundBrushKey = "Slate.SelectionBackgroundBrush";
    internal const string TextBrushKey = "Slate.SelectionTextBrush";
    internal const string ReadingBrushKey = "Slate.ReadingSelectionBrush";

    private static int _installed;

    /// <summary>
    /// Sets the switch and registers the handler. Runs before any window
    /// exists: WPF reads the switch once, and a class handler covers every
    /// text box the app will create. Idempotent for the same reason as
    /// <see cref="TextBoxAccessibility.Install"/>.
    /// </summary>
    internal static void Install()
    {
        if (System.Threading.Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        AppContext.SetSwitch(AdornerSwitch, false);
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBoxLoaded));
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is TextBox box)
        {
            box.SetResourceReference(TextBoxBase.SelectionBrushProperty, BackgroundBrushKey);
            box.SetResourceReference(TextBoxBase.SelectionTextBrushProperty, TextBrushKey);
            box.SelectionOpacity = 1;
        }
    }
}
