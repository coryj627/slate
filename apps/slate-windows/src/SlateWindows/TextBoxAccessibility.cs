// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SlateWindows;

/// <summary>
/// Gives the Fluent <c>TextBox</c> clear button an accessible name (#1106).
/// </summary>
/// <remarks>
/// <para>
/// The first-party Fluent <c>TextBox</c> template ends with:
/// </para>
/// <code>
/// &lt;Button Name="DeleteButton" Visibility="Collapsed" IsTabStop="False"&gt;
///     &lt;TextBlock Name="GlyphElement" Text="&amp;#xE894;"
///                FontFamily="{DynamicResource SymbolThemeFontFamily}" /&gt;
/// &lt;/Button&gt;
/// </code>
/// <para>
/// <c>ButtonAutomationPeer</c> takes its name from that content, so UIA
/// publishes one Segoe MDL2 private-use character. A screen reader reads it
/// as garbage, and axe fails it under
/// <c>NameExcludesPrivateUnicodeCharacters</c>. It affects every
/// <c>TextBox</c> in the app — the W5-1 palette journey was simply the
/// first axe scan anywhere that typed before scanning.
/// </para>
/// <para>
/// <b>A class handler, not a style.</b> The button carries its own
/// <c>Style</c> from the template's <c>Grid.Resources</c> AND sets
/// <c>OverridesDefaultStyle</c>, so no implicit <c>Button</c> style at any
/// scope can reach it. That is why the first recorded attempt could never
/// have worked, independently of where the style was declared.
/// </para>
/// <para>
/// <b>The part is found through the template, not a visual-tree walk.</b>
/// The button is not created lazily: it is in the template from the start
/// at <c>Visibility="Collapsed"</c>, revealed by an
/// <c>IsKeyboardFocusWithin</c> trigger and re-collapsed when the text is
/// empty, read-only, multi-line, or wrapped.
/// </para>
/// <para>
/// <b><c>TextChanged</c> is the hook that matters, and it was measured.</b>
/// <c>Loaded</c> never fires for the command palette's search box — the
/// sidebar filter and Quick Open boxes both raise it, that one does not,
/// so a Loaded-only fix left the palette exactly as broken while passing a
/// unit test. <c>TextChanged</c> is also the semantically right moment:
/// the button cannot be visible until the box is non-empty, so every state
/// in which it can be announced is preceded by one. <c>Loaded</c> is kept
/// as well, for boxes that do raise it, so the name is in place before the
/// first keystroke rather than one event behind it.
/// </para>
/// </remarks>
internal static class TextBoxAccessibility
{
    /// <summary>The clear button's part name in the Fluent template.</summary>
    internal const string ClearButtonPart = "DeleteButton";

    /// <summary>
    /// What the button is announced as. Matches the verb the template
    /// already advertises through its <c>AcceleratorKey</c>.
    /// </summary>
    internal const string ClearButtonName = "Clear text";

    /// <summary>
    /// Set once this box's template has been inspected, so the
    /// per-keystroke path costs a property read instead of a template
    /// lookup.
    /// </summary>
    /// <remarks>
    /// Set whether or not the part was found. A box with a non-Fluent
    /// template has no clear button and never will, and only flagging the
    /// success case left every keystroke in such a box repeating an
    /// <c>ApplyTemplate</c> and a failed <c>FindName</c> forever.
    /// </remarks>
    private static readonly DependencyProperty ClearButtonCheckedProperty =
        DependencyProperty.RegisterAttached(
            "ClearButtonChecked", typeof(bool), typeof(TextBoxAccessibility),
            new PropertyMetadata(false));

    private static int _installed;

    /// <summary>
    /// Registers the app-wide handlers. Idempotent, and atomically so:
    /// WPF class handlers cannot be removed, so two callers racing a
    /// plain bool check would both register and every TextBox event would
    /// be handled twice for the life of the process.
    /// </summary>
    internal static void Install()
    {
        if (System.Threading.Interlocked.Exchange(ref _installed, 1) == 1)
        {
            return;
        }

        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBoxLoaded));
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler(OnTextBoxTextChanged));
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is TextBox box)
        {
            NameClearButton(box);
        }
    }

    private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (sender is TextBox box)
        {
            NameClearButton(box);
        }
    }

    /// <summary>
    /// Names the clear button if this box has one. Safe to call repeatedly,
    /// and on a box whose template has no such part.
    /// </summary>
    internal static void NameClearButton(TextBox box)
    {
        if ((bool)box.GetValue(ClearButtonCheckedProperty))
        {
            return;
        }

        // Forces template expansion: the template may not have been applied
        // yet on a box that has never been measured.
        box.ApplyTemplate();
        if (box.Template is null)
        {
            // No template to inspect yet — stay unflagged so the next event
            // tries again, rather than deciding this box has no part.
            return;
        }

        if (box.Template.FindName(ClearButtonPart, box) is Button clearButton
            && AutomationProperties.GetName(clearButton) is not { Length: > 0 })
        {
            // Never override a name someone set deliberately.
            AutomationProperties.SetName(clearButton, ClearButtonName);
        }

        box.SetValue(ClearButtonCheckedProperty, true);
    }
}
