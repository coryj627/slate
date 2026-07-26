// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Documents;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// The chorded structural-navigation layer (W3-1, gap_analysis G21).
///
/// Browse-mode single-letter quick-nav cannot be requested by any app in
/// any framework — it is AT-side per-app class selection, verified in
/// NVDA source. So the APP owns chords and semantics: these commands
/// move the real caret (the AT speaks the landing line itself, which is
/// why landings are not announcement events), and a MISS posts the
/// canonical <c>ReadingNavNoTarget</c> vocabulary event. The letters
/// belong to the AT layer (W-E7: NVDA add-on, JAWS scripts) and are
/// never claimed here.
///
/// Chord policy: modified chords pass through every mode of every AT.
/// `Ctrl+Alt` + letter is the Slate spatial prefix (W1 precedent);
/// `Shift` reverses direction. Vetted against the known JAWS/NVDA
/// bindings — JAWS owns `Ctrl+Alt+arrows` (table reading), which these
/// deliberately avoid; the G18 precedent governs any future collision.
/// AltGr caveat: `Ctrl+Alt`+letter equals `AltGr`+letter on some
/// layouts; acceptable because the bindings are scoped to the read-only
/// surface, where no text entry exists — revisit if any binding ever
/// goes global.
/// </summary>
internal sealed class ReadingNavigator
{
    private readonly ReadingSurface _surface;
    private readonly Action<A11yEvent> _announce;
    private IReadOnlyList<ReadingLandmark> _landmarks = Array.Empty<ReadingLandmark>();

    public ReadingNavigator(ReadingSurface surface, Action<A11yEvent> announce)
    {
        _surface = surface;
        _announce = announce;
        Bind();
    }

    /// <summary>Swap in the freshly built document's index.</summary>
    public void SetLandmarks(IReadOnlyList<ReadingLandmark> landmarks) =>
        _landmarks = landmarks;

    private readonly Dictionary<(Key Key, ModifierKeys Modifiers), Action> _chords = new();
    private bool _suppressAltMenu;

    /// <summary>
    /// Dispatch happens in the TUNNELING phase (`PreviewKeyDown`), not
    /// through `InputBindings`. Measured 2026-07-27: `Ctrl+Alt+L` never
    /// reached a surface `KeyBinding` — NVDA's key echo proved the chord
    /// arrived at the app, and only the unshifted variant vanished, the
    /// signature of a class-level RichTextBox editing binding consuming
    /// it during the bubbling phase. Preview runs first by construction,
    /// for `L` and for whatever else is lurking in that class table.
    /// </summary>
    private void Bind()
    {
        AddChord(Key.H, shift: false, () => Move(ReadingLandmarkKind.Heading, forward: true));
        AddChord(Key.H, shift: true, () => Move(ReadingLandmarkKind.Heading, forward: false));
        AddChord(Key.K, shift: false, () => Move(ReadingLandmarkKind.Link, forward: true));
        AddChord(Key.K, shift: true, () => Move(ReadingLandmarkKind.Link, forward: false));
        AddChord(Key.L, shift: false, () => Move(ReadingLandmarkKind.List, forward: true));
        AddChord(Key.L, shift: true, () => Move(ReadingLandmarkKind.List, forward: false));
        // Alias for list navigation. Field evidence (2026-07-27, per-key
        // diagnostic log): Ctrl+Alt+L keydowns never reached the app —
        // zero Seen lines while H/T/C/E all arrived — though NVDA's key
        // echo proved the host received them. Something machine-local
        // grabs the chord globally (RegisterHotKey consumer, NVDA add-on
        // gesture, or remote-desktop host). Per the G18 collision
        // precedent the app adjusts: L stays for unafflicted machines,
        // U is the documented alias.
        AddChord(Key.U, shift: false, () => Move(ReadingLandmarkKind.List, forward: true));
        AddChord(Key.U, shift: true, () => Move(ReadingLandmarkKind.List, forward: false));
        AddChord(Key.T, shift: false, () => Move(ReadingLandmarkKind.Table, forward: true));
        AddChord(Key.T, shift: true, () => Move(ReadingLandmarkKind.Table, forward: false));
        AddChord(Key.E, shift: false, () => Move(ReadingLandmarkKind.Embed, forward: true));
        AddChord(Key.E, shift: true, () => Move(ReadingLandmarkKind.Embed, forward: false));
        AddChord(Key.C, shift: false, () => Move(ReadingLandmarkKind.CodeBlock, forward: true));
        AddChord(Key.C, shift: true, () => Move(ReadingLandmarkKind.CodeBlock, forward: false));

        for (byte level = 1; level <= 6; level++)
        {
            byte captured = level;
            AddChord(Key.D1 + (captured - 1), shift: false,
                () => MoveToHeadingLevel(captured, forward: true));
            AddChord(Key.D1 + (captured - 1), shift: true,
                () => MoveToHeadingLevel(captured, forward: false));
        }

        _surface.PreviewKeyDown += Surface_PreviewKeyDown;
        _surface.PreviewKeyUp += Surface_PreviewKeyUp;
    }

    private void AddChord(Key key, bool shift, Action action)
    {
        ModifierKeys modifiers = ModifierKeys.Control | ModifierKeys.Alt
            | (shift ? ModifierKeys.Shift : ModifierKeys.None);
        _chords[(key, modifiers)] = action;
    }

    /// <summary>The chord table, pinned by tests.</summary>
    internal bool HandlesChord(Key key, ModifierKeys modifiers) =>
        _chords.ContainsKey((key, modifiers));

    private void Surface_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt-modified keys can arrive as Key.System carrying the real
        // key in SystemKey.
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool chordShaped = (Keyboard.Modifiers
            & (ModifierKeys.Control | ModifierKeys.Alt))
            == (ModifierKeys.Control | ModifierKeys.Alt);
        if (chordShaped)
        {
            // Diagnostic tap (SLATE_UIA_DIAGNOSTICS=1). Carries the key
            // identity: the first tap's bare event names could not
            // attribute seen-vs-dispatched to a specific press.
            HostLog.WriteUiAutomationDiagnostic(
                HostDiagnosticEvent.ReadingChordSeen,
                $"{key}+{Keyboard.Modifiers}");
        }
        if (!_chords.TryGetValue((key, Keyboard.Modifiers), out Action? action))
        {
            return;
        }
        HostLog.WriteUiAutomationDiagnostic(
            HostDiagnosticEvent.ReadingChordDispatched,
            $"{key}+{Keyboard.Modifiers}");
        try
        {
            action();
        }
        catch (Exception exception)
        {
            // Visible before fatal: with no app-level exception swallow,
            // a throw here takes the process down — make sure the log
            // names the chord that did it first.
            HostLog.Write(HostDiagnosticEvent.ReadingChordActionFailed, exception);
            throw;
        }
        e.Handled = true;
        // Releasing Alt last after a chord otherwise activates the menu
        // bar — the measured "File collapsed Alt+F" focus theft that
        // yanked a reader out of the document mid-navigation.
        _suppressAltMenu = true;
    }

    private void Surface_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftAlt or Key.RightAlt)
        {
            if (_suppressAltMenu)
            {
                _suppressAltMenu = false;
                e.Handled = true;
            }
        }
        else if (key is not (Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift))
        {
            // Any other key between chord and Alt release means the user
            // moved on; menu activation is theirs again.
            _suppressAltMenu = false;
        }
    }

    internal void Move(ReadingLandmarkKind kind, bool forward) =>
        Navigate(
            landmark => landmark.Kind == kind,
            NoTargetEvent(kind, level: 0, forward),
            forward);

    internal void MoveToHeadingLevel(byte level, bool forward) =>
        Navigate(
            landmark => landmark.Kind == ReadingLandmarkKind.Heading
                && landmark.HeadingLevel == level,
            NoTargetEvent(ReadingLandmarkKind.Heading, level, forward),
            forward);

    /// <summary>
    /// Strictly-beyond-the-caret search over the document-ordered
    /// landmark list. No wrap: hitting the edge announces the miss —
    /// wrap-around navigation disorients precisely the users this layer
    /// exists for.
    /// </summary>
    private void Navigate(Func<ReadingLandmark, bool> matches, A11yEvent miss, bool forward)
    {
        TextPointer caret = _surface.CaretPosition;
        ReadingLandmark? target = null;
        if (forward)
        {
            foreach (ReadingLandmark landmark in _landmarks)
            {
                if (matches(landmark) && caret.CompareTo(landmark.Position) < 0)
                {
                    target = landmark;
                    break;
                }
            }
        }
        else
        {
            foreach (ReadingLandmark landmark in _landmarks)
            {
                if (matches(landmark) && landmark.Position.CompareTo(caret) < 0)
                {
                    target = landmark;
                }
                else if (landmark.Position.CompareTo(caret) >= 0)
                {
                    break;
                }
            }
        }

        if (target is null)
        {
            _announce(miss);
            return;
        }

        _surface.CaretPosition = target.Position;
        target.Position.Paragraph?.BringIntoView();

        // Landings are announced explicitly. The design assumed caret
        // speech was free; the 2026-07-27 NVDA pass measured otherwise —
        // NVDA echoes lines only for keys IT recognizes as caret
        // movement, so a programmatic move is silent. Core owns the
        // phrasing; the landmark supplies its own document text.
        _announce(new A11yEvent.ReadingNavLanded(
            LandedTarget(target), target.Text));
    }

    private static ReadingNavTarget LandedTarget(ReadingLandmark landmark) =>
        landmark.Kind switch
        {
            ReadingLandmarkKind.Heading when landmark.HeadingLevel > 0 =>
                new ReadingNavTarget.HeadingLevel(landmark.HeadingLevel),
            ReadingLandmarkKind.Heading => new ReadingNavTarget.Heading(),
            ReadingLandmarkKind.Link => new ReadingNavTarget.Link(),
            ReadingLandmarkKind.List => new ReadingNavTarget.List(),
            ReadingLandmarkKind.Table => new ReadingNavTarget.Table(),
            ReadingLandmarkKind.Embed => new ReadingNavTarget.Embed(),
            _ => new ReadingNavTarget.CodeBlock(),
        };

    private static A11yEvent NoTargetEvent(
        ReadingLandmarkKind kind, byte level, bool forward)
    {
        ReadingNavTarget target = kind switch
        {
            ReadingLandmarkKind.Heading when level > 0 =>
                new ReadingNavTarget.HeadingLevel(level),
            ReadingLandmarkKind.Heading => new ReadingNavTarget.Heading(),
            ReadingLandmarkKind.Link => new ReadingNavTarget.Link(),
            ReadingLandmarkKind.List => new ReadingNavTarget.List(),
            ReadingLandmarkKind.Table => new ReadingNavTarget.Table(),
            ReadingLandmarkKind.Embed => new ReadingNavTarget.Embed(),
            _ => new ReadingNavTarget.CodeBlock(),
        };
        return new A11yEvent.ReadingNavNoTarget(target, forward);
    }
}
