// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using SlateWindows.Commands;
using System.Xml.Linq;

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741) design-pass facts: the text-editing allow-list a modal
/// surface must honour.
/// </summary>
/// <remarks>
/// These exist because the allow-list shipped broken through TWO review
/// rounds — first swallowing every modified key, then still swallowing
/// AltGr — and neither defect could be caught by a unit test while the
/// table was a <c>private static</c> on <c>MainWindow</c>. Extracting it
/// is what makes the table falsifiable.
/// </remarks>
public sealed class ModalSurfaceTests
{
    [Theory]
    // Clipboard and undo must survive the modal swallow.
    [InlineData(Key.A, ModifierKeys.Control, true)]
    [InlineData(Key.C, ModifierKeys.Control, true)]
    [InlineData(Key.V, ModifierKeys.Control, true)]
    [InlineData(Key.X, ModifierKeys.Control, true)]
    [InlineData(Key.Z, ModifierKeys.Control, true)]
    // Caret and word-wise movement.
    [InlineData(Key.Left, ModifierKeys.Control, true)]
    [InlineData(Key.Back, ModifierKeys.Control, true)]
    // Shift alone: capitals and selection.
    [InlineData(Key.T, ModifierKeys.Shift, true)]
    [InlineData(Key.Home, ModifierKeys.Shift, true)]
    // Word-wise selection.
    [InlineData(Key.Right, ModifierKeys.Control | ModifierKeys.Shift, true)]
    [InlineData(Key.Z, ModifierKeys.Control | ModifierKeys.Shift, true)]
    // Shell chords must NOT be treated as text editing — the overlay still
    // swallows them so they cannot fire underneath it.
    [InlineData(Key.W, ModifierKeys.Control, false)]
    [InlineData(Key.T, ModifierKeys.Control, false)]
    [InlineData(Key.S, ModifierKeys.Control, false)]
    [InlineData(Key.J, ModifierKeys.Control, false)]
    [InlineData(Key.E, ModifierKeys.Control | ModifierKeys.Shift, false)]
    // Unmodified keys are not this table's business; the caller handles them.
    [InlineData(Key.A, ModifierKeys.None, false)]
    public void TheAllowListKeepsTextEditingAndStillSwallowsShellChords(
        Key key, ModifierKeys modifiers, bool expected) =>
        Assert.Equal(expected, TextEditingChords.Allows(key, modifiers, rightAltDown: false));

    /// <summary>
    /// AltGr reaches WPF as <c>Control|Alt</c>. Without the right-Alt arm
    /// it falls through and is swallowed, silently dropping nine ordinary
    /// Polish letters and the German at-sign and euro sign.
    /// </summary>
    /// <remarks>
    /// Both sides matter and both are pinned here: with right Alt DOWN the
    /// key is AltGr and must reach the text field; with it UP the same
    /// modifier pair is a real Ctrl+Alt chord the overlay must still
    /// swallow, or shell chords would fire underneath it.
    /// </remarks>
    [Theory]
    // Lowercase: AltGr + letter.
    [InlineData(Key.Q, false)]
    [InlineData(Key.E, false)]
    [InlineData(Key.A, false)]
    // UPPERCASE: AltGr + Shift + letter. An exact-equality test on
    // Control|Alt swallowed every one of these while passing the
    // lowercase cases — which is exactly how the first version of this
    // fix shipped.
    [InlineData(Key.Q, true)]
    [InlineData(Key.E, true)]
    [InlineData(Key.A, true)]
    public void AltGrReachesTheTextFieldButCtrlAltStillDoesNot(Key key, bool shifted)
    {
        ModifierKeys modifiers = ModifierKeys.Control | ModifierKeys.Alt
            | (shifted ? ModifierKeys.Shift : ModifierKeys.None);

        Assert.True(
            TextEditingChords.Allows(key, modifiers, rightAltDown: true),
            $"AltGr{(shifted ? "+Shift" : string.Empty)} was swallowed — "
            + "ordinary letters are undroppable on layouts that use it.");

        Assert.False(
            TextEditingChords.Allows(key, modifiers, rightAltDown: false),
            "a real Ctrl+Alt chord was treated as text editing and would "
            + "fire the shell command underneath the overlay.");
    }

    /// <summary>
    /// Every <c>Ctrl+Alt</c> chord the shell delivers must be answered by
    /// the shell-chord deny-list, because the AltGr arm un-swallows that
    /// whole modifier pair whenever right Alt is down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the safety net under the AltGr fix. That arm returns true
    /// for ANY <c>Ctrl+Alt</c> chord while right Alt is physically down,
    /// so a shell chord on that pair would reach the surface underneath an
    /// open overlay unless the deny-list catches it first. It does today —
    /// but the deny-list was written for W1 and has already failed to
    /// track Ctrl+J, Ctrl+Shift+E and Ctrl+R, so the next Ctrl+Alt binding
    /// added without an entry is the live hazard.
    /// </para>
    /// <para>
    /// Driven from the CHORD TABLE, not from the XAML. The first version
    /// of this fact scraped <c>KeyBinding</c> elements and did not bite:
    /// Ctrl+Alt+F is delivered by a code-behind handler, so removing it
    /// from the deny-list left the fact green. The table records
    /// imperative chords too — that is the whole point of it — so it is
    /// the only source that sees the full surface.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryCtrlAltShellChordIsCoveredByTheDenyList()
    {
        // GLOBAL scope only. A focus-scoped chord — the grid's Ctrl+Alt+S,
        // the reading navigator's family — rides a RoutedCommand or a
        // surface handler that requires focus inside that surface, and the
        // palette holds focus in its own search box while open. Those
        // cannot fire underneath it, so demanding deny-list coverage for
        // them would be over-strict.
        ChordTableEntry[] ctrlAlt = ChordTable.Entries
            .Where(row => row.Scope == ChordScope.Global
                && row.WindowsChord is { } chord
                && chord.Contains("Ctrl+", StringComparison.Ordinal)
                && chord.Contains("Alt+", StringComparison.Ordinal))
            .ToArray();

        // Guard against a filter that silently matches nothing — the
        // failure mode this project has hit more than once, including in
        // the first version of this very fact.
        Assert.NotEmpty(ctrlAlt);

        foreach (ChordTableEntry row in ctrlAlt)
        {
            (Key key, ModifierKeys modifiers) = ParseChord(row.WindowsChord!);
            Assert.True(
                MainWindow.IsUnderlyingShellShortcutForTests(key, modifiers),
                $"{row.Id} delivers '{row.WindowsChord}' but the shell-chord "
                + "deny-list does not answer it. While an overlay is open and "
                + "right Alt is down, the AltGr arm would let it fire underneath.");
        }
    }

    /// <summary>
    /// Parses a chord-table string such as "Ctrl+Alt+Shift+Left".
    /// </summary>
    private static (Key Key, ModifierKeys Modifiers) ParseChord(string chord)
    {
        string[] tokens = chord.Split('+', StringSplitOptions.RemoveEmptyEntries);
        ModifierKeys modifiers = ModifierKeys.None;
        foreach (string token in tokens[..^1])
        {
            modifiers |= token switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Alt" => ModifierKeys.Alt,
                "Shift" => ModifierKeys.Shift,
                _ => throw new InvalidOperationException($"unknown modifier '{token}'"),
            };
        }

        string keyToken = tokens[^1];
        Key key = keyToken switch
        {
            "=" => Key.OemPlus,
            "-" => Key.OemMinus,
            "\\" => Key.Oem5,
            "[" => Key.OemOpenBrackets,
            "]" => Key.OemCloseBrackets,
            _ when keyToken.Length == 1 && char.IsDigit(keyToken[0]) =>
                Key.D0 + (keyToken[0] - '0'),
            _ => (Key)Enum.Parse(typeof(Key), keyToken, ignoreCase: true),
        };

        return (key, modifiers);
    }

    /// <summary>
    /// The paint order the enum encodes is the order
    /// <c>MainWindow.xaml</c> declares the overlays in — the thing that
    /// actually decides which surface is on top, since the file sets no
    /// <c>Panel.ZIndex</c> anywhere.
    /// </summary>
    [Fact]
    public void ModalSurfaceOrderMatchesTheXamlDeclarationOrder()
    {
        string xaml = File.ReadAllText(
            Path.Combine(SourceRoot(), "MainWindow.xaml"));

        Assert.DoesNotContain(
            "Panel.ZIndex",
            xaml,
            StringComparison.Ordinal);

        (ModalSurface Surface, string AutomationId)[] expected =
        [
            (ModalSurface.QuickOpen, "QuickSwitcher"),
            (ModalSurface.CommandPalette, "CommandPalette"),
            (ModalSurface.AddProperty, "AddPropertySheet"),
            (ModalSurface.BulkRename, "BulkRenameSheet"),
            (ModalSurface.CitationDetails, "CitationDetailsSheet"),
            (ModalSurface.CitationSummary, "CitationSummarySheet"),
            (ModalSurface.FilesCiting, "FilesCitingSheet"),
            (ModalSurface.DashboardEditor, "DashboardEditorSheet"),
            (ModalSurface.BaseQueryBuilder, "BaseQueryBuilderSheet"),
        ];

        int previous = -1;
        foreach ((ModalSurface surface, string automationId) in expected)
        {
            int at = xaml.IndexOf(
                $"AutomationProperties.AutomationId=\"{automationId}\"",
                StringComparison.Ordinal);
            Assert.True(at >= 0, $"{automationId} is not declared in MainWindow.xaml");
            Assert.True(
                at > previous,
                $"{surface} is declared before the surface that should paint "
                + "beneath it — ModalSurface order no longer matches the XAML, "
                + "so the topmost-surface calculation is wrong.");
            previous = at;
        }
    }

    private static string SourceRoot() =>
        Path.Combine(RepoRoot(), "apps", "slate-windows", "src", "SlateWindows");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
