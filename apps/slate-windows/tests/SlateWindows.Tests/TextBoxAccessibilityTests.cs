// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows.Tests;

/// <summary>
/// #1106: the Fluent <c>TextBox</c> clear button must not publish a
/// private-use icon glyph as its accessible name.
/// </summary>
/// <remarks>
/// <para>
/// The Fluent template carries
/// <c>&lt;Button Name="DeleteButton"&gt;&lt;TextBlock Text="&amp;#xE894;"
/// /&gt;&lt;/Button&gt;</c>, and <c>ButtonAutomationPeer</c> derives its
/// name from that content — so UIA publishes a single Segoe MDL2
/// private-use character, which a screen reader reads as garbage and which
/// fails axe's <c>NameExcludesPrivateUnicodeCharacters</c>.
/// </para>
/// <para>
/// <b>Both delivery paths are driven, and that is the point.</b> The first
/// version of this test covered only the <c>Loaded</c> path, and passed
/// against a fix that left the real defect untouched: <c>Loaded</c> never
/// fires for the command palette's search box, so the shell journey still
/// failed while this file was green. A test that cannot tell the working
/// fix from the broken one reports coverage it does not have.
/// </para>
/// </remarks>
public sealed class TextBoxAccessibilityTests
{
    /// <summary>
    /// A box whose text is set before it loads is named through
    /// <c>Loaded</c>.
    /// </summary>
    [Fact]
    public void TheClearButtonIsNamedForABoxThatRaisesLoaded() =>
        AssertNoPrivateUseName(OnStaThread(() =>
        {
            TextBox box = FluentTextBox("typed");
            var window = new Window
            {
                Content = box,
                Width = 300,
                Height = 120,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Left = -10000,
            };
            window.Show();
            try
            {
                return ClearButtonName(box);
            }
            finally
            {
                window.Close();
            }
        }));

    /// <summary>
    /// A box that never raises <c>Loaded</c> is named through
    /// <c>TextChanged</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The command palette's shape: a box under an ancestor that is
    /// collapsed when the window loads, whose text arrives later. Its
    /// search box — measured, not assumed — never raises <c>Loaded</c> at
    /// all, while the sidebar filter and Quick Open boxes both do.
    /// </para>
    /// <para>
    /// This does discriminate, verified rather than hoped: deleting the
    /// <c>TextChanged</c> registration turns it red, and turns the shell
    /// journey's post-typing axe scan red with it. A box revealed after
    /// its window has already loaded gets no second <c>Loaded</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheClearButtonIsNamedForABoxRevealedAfterItsWindowLoaded() =>
        AssertNoPrivateUseName(OnStaThread(() =>
        {
            TextBox box = FluentTextBox(string.Empty);
            var overlay = new Border { Child = box, Visibility = Visibility.Collapsed };
            var window = new Window
            {
                Content = overlay,
                Width = 300,
                Height = 120,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                Left = -10000,
            };
            window.Show();
            try
            {
                overlay.Visibility = Visibility.Visible;
                box.Text = "typed";
                return ClearButtonName(box);
            }
            finally
            {
                window.Close();
            }
        }));

    /// <summary>
    /// A name set deliberately on the part is never overwritten.
    /// </summary>
    /// <remarks>
    /// The guard exists so a future surface can give its clear button a
    /// more specific name — "Clear search", say — without this handler
    /// silently flattening it back to the generic one.
    /// </remarks>
    [Fact]
    public void ADeliberatelySetNameSurvives()
    {
        const string chosen = "Clear the search box";
        string? observed = OnStaThread(() =>
        {
            TextBox box = FluentTextBox(string.Empty);
            box.ApplyTemplate();
            var clearButton = (Button)box.Template.FindName(
                TextBoxAccessibility.ClearButtonPart, box);
            System.Windows.Automation.AutomationProperties.SetName(clearButton, chosen);

            // The handler runs on this, and must leave the name alone.
            box.Text = "typed";
            return ClearButtonName(box);
        });

        Assert.Equal(chosen, observed);
    }

    private static void AssertNoPrivateUseName(string? name)
    {
        Assert.False(
            string.IsNullOrEmpty(name),
            "the clear button published no accessible name at all, which is a "
            + "different defect from the one under test — a nameless button is "
            + "still a bare stop for a screen reader.");

        // The Basic Multilingual Plane private-use area.
        char[] privateUse = name!
            .Where(character => character >= 0xE000 && character <= 0xF8FF)
            .ToArray();
        Assert.True(
            privateUse.Length == 0,
            $"the clear button's accessible name is \"{Escape(name!)}\", which "
            + $"contains {privateUse.Length} private-use character(s). A screen "
            + "reader reads those as garbage and axe fails the name.");
    }

    /// <summary>A <c>TextBox</c> carrying the real first-party template.</summary>
    /// <remarks>
    /// The Fluent style is assigned to the box, NOT merged into
    /// <c>Application.Resources</c>. Merging is the obvious way to write
    /// this and it silently re-themes every other WPF test in the process
    /// — it broke four Bases tests that pass in isolation, which is a
    /// far more confusing failure than the one under test.
    /// </remarks>
    private static TextBox FluentTextBox(string text)
    {
        // Runs Application's STATIC constructor, which is what registers
        // the pack: scheme and its application:,,, authority — without
        // constructing an Application.
        //
        // Two wrong ways were measured first. `new Application()` runs on
        // this test's short-lived STA thread and leaves Application.Current
        // bound to a dispatcher that dies with it, which broke a shifting
        // set of unrelated WPF tests by run order while each passed alone.
        // PackUriHelper.UriSchemePack registers the scheme but NOT the
        // authority, so this file then passed only when some earlier test
        // had already booted WPF — green in the full suite, red on its own.
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(Application).TypeHandle);
        var fluent = new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/PresentationFramework.Fluent;component/"
                + "Themes/Fluent.Light.xaml",
                UriKind.Absolute),
        };

        TextBoxAccessibility.Install();
        return new TextBox
        {
            Text = text,
            Style = (Style)fluent[typeof(TextBox)],
        };
    }

    private static string? ClearButtonName(TextBox box)
    {
        box.ApplyTemplate();
        var clearButton = box.Template?.FindName(
            TextBoxAccessibility.ClearButtonPart, box) as Button;
        Assert.True(
            clearButton is not null,
            "the Fluent template no longer has a "
            + $"'{TextBoxAccessibility.ClearButtonPart}' part, so this test is "
            + "measuring nothing. Re-read the template before deleting this.");

        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(clearButton!)
            ?? throw new Xunit.Sdk.XunitException("the clear button has no automation peer.");
        return peer.GetName();
    }

    private static string? OnStaThread(Func<string?> body)
    {
        string? result = null;
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
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return failure is null
            ? result
            : throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static string Escape(string value) =>
        string.Concat(value.Select(character => character < ' ' || character > '~'
            ? $"\\u{(int)character:X4}"
            : character.ToString()));
}
