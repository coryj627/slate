// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows.Reading;

/// <summary>
/// The focusable host for one rendered math block (W3-2, gap G23's
/// guaranteed layer). Focus, Tab, and object navigation speak the
/// canonical MathCAT speech through <c>Name</c> — the route verified
/// to work in stock NVDA and JAWS — while the peer additionally serves
/// the MathML convention property for Narrator's generalizing pipeline
/// and the W-E7 NVDA appModule.
/// </summary>
internal sealed class ReadingMathElement : ContentControl
{
    public ReadingMathElement(
        string speech, string mathMl, string source, string braille)
    {
        Speech = speech;
        MathMl = mathMl;
        Braille = braille;
        Focusable = true;
        AutomationProperties.SetName(this, speech);
        // The authored source rides HelpText — reachable on request
        // (the UIA analog of mac's "Source" custom content; JAWS
        // 2022+ skips HelpText when Name is set, which is the correct
        // ordering here: speech first, source on demand).
        AutomationProperties.SetHelpText(this, source);
        ToolTip = source;
        // The decoded braille (Nemeth or UEB cells per the session's
        // braille-code pref) rides ItemStatus — a standard queryable
        // UIA property, the analog of mac's "Braille" custom content
        // and the W-E7 appModule's read channel. Ctrl+Enter at the
        // caret announces it (ReadingSurface routing, round 3).
        if (braille.Length > 0)
        {
            AutomationProperties.SetItemStatus(this, braille);
        }
    }

    /// <summary>Canonical MathCAT speech ("Math expression." fallback
    /// applied by the builder before construction).</summary>
    public string Speech { get; }

    /// <summary>Complete <c>&lt;math&gt;…&lt;/math&gt;</c> markup.</summary>
    public string MathMl { get; }

    /// <summary>Decoded braille in the session's selected code; empty
    /// when MathCAT produced none for this expression.</summary>
    public string Braille { get; }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ReadingMathElementPeer(this);
}

/// <summary>
/// The math element's peer: Name = canonical speech (spoken on focus
/// and object navigation in stock NVDA/JAWS), localized control type
/// "math", and the registered MathML custom UIA property served
/// through <see cref="IMathMlUiaSource"/> (the Word/Chromium/Narrator
/// convention — see <see cref="MathMlUiaProperty"/>).
/// </summary>
internal sealed class ReadingMathElementPeer
    : FrameworkElementAutomationPeer, IMathMlUiaSource
{
    private readonly ReadingMathElement _element;

    public ReadingMathElementPeer(ReadingMathElement element) : base(element)
    {
        _element = element;
    }

    public string MathMl => _element.MathMl;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "math";

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;
}
