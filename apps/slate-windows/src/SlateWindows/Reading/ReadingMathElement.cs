// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace SlateWindows.Reading;

/// <summary>
/// The focusable host for one rendered math block (W3-2, gap G23's
/// guaranteed layer). Focus, Tab, and object navigation speak the
/// canonical MathCAT speech through <c>Name</c> — the route verified
/// to work in stock NVDA and JAWS — while the peer additionally serves
/// the MathML convention property for Narrator's generalizing pipeline
/// and the W-E7 NVDA appModule. The authored source is spoken on
/// request only (#1591, plan 45 XB-9): the reading view's source chord
/// reads <see cref="Source"/>; nothing on focus does.
/// </summary>
internal sealed class ReadingMathElement : ContentControl
{
    private const string ScrollerName = "FormulaScroller";

    private static readonly ControlTemplate DrawingTemplate = NewDrawingTemplate();

    public ReadingMathElement(
        string speech, string mathMl, string source, string braille)
    {
        Speech = speech;
        MathMl = mathMl;
        Source = source;
        Braille = braille;
        Focusable = true;
        AutomationProperties.SetName(this, speech);
        // #1591: the source is spoken on request, never on focus. It is
        // not HelpText (NVDA reads HelpText on Tab), and the peer answers
        // an empty HelpText rather than WPF's ToolTip fallback. The
        // ToolTip keeps it for sighted hover, and for hover only: WPF
        // also opens a ToolTip on keyboard focus, which readers announce
        // (the plan 45 XB-9 review).
        ToolTip = source;
        ToolTipService.SetShowsToolTipOnKeyboardFocus(this, false);
        ClipToBounds = true;
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

    /// <summary>The authored LaTeX, delimiters stripped: what the source
    /// chord speaks.</summary>
    public string Source { get; }

    /// <summary>Decoded braille in the session's selected code; empty
    /// when MathCAT produced none for this expression.</summary>
    public string Braille { get; }

    /// <summary>#1595: the width the speech run before this element can
    /// take on their shared line. WPF measures an embedded element against
    /// the whole line, so content that fills it (a wrapping source, a wide
    /// drawing) dropped onto a line of its own, where line reading reads
    /// only the role (the plan 45 XB-9 review). The content is measured
    /// that much narrower: a source wraps in it, and a drawing scrolls.</summary>
    public double LineReserve { get; init; }

    protected override Size MeasureOverride(Size constraint)
    {
        if (LineReserve <= 0 || double.IsInfinity(constraint.Width))
        {
            return base.MeasureOverride(constraint);
        }
        double width = Math.Max(0, constraint.Width - LineReserve);
        Size desired = base.MeasureOverride(new Size(width, constraint.Height));
        return new Size(Math.Min(desired.Width, width), desired.Height);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ReadingMathElementPeer(this);

    /// <summary>What scrolls a drawing; a source has none, because it wraps.</summary>
    internal ScrollViewer? Scroller => GetTemplateChild(ScrollerName) as ScrollViewer;

    /// <summary>DS-3: a drawing can't wrap, and shrinking one to fit the measure left a
    /// long formula too small to read, so it keeps its size and scrolls.</summary>
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (newContent is Path)
        {
            Template = DrawingTemplate;
        }
    }

    /// <summary>The element is the focus, because the scroller takes none, so the keys
    /// that scroll it are here. A formula that fits leaves them to the reading view.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && Scroller is { ScrollableWidth: > 0 } scroller)
        {
            if (e.Key == Key.Left)
            {
                scroller.LineLeft();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                scroller.LineRight();
                e.Handled = true;
            }
        }

        base.OnKeyDown(e);
    }

    private static ControlTemplate NewDrawingTemplate()
    {
        var scroller = new FrameworkElementFactory(typeof(FormulaScroller), ScrollerName);
        scroller.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        return new ControlTemplate(typeof(ReadingMathElement)) { VisualTree = scroller };
    }
}

/// <summary>Scrolls a drawn formula sideways. The wheel stays the page's, or a formula
/// under the pointer would stop the reader scrolling the note. An implicit style reaches
/// only its exact type, so this asks for Fluent's by name.</summary>
internal sealed class FormulaScroller : ScrollViewer
{
    public FormulaScroller()
    {
        Focusable = false;
        IsTabStop = false;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        SetResourceReference(StyleProperty, typeof(ScrollViewer));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
    }
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

    /// <summary>#1591: no HelpText. WPF's peer falls back to the ToolTip,
    /// which holds the source, and NVDA reads HelpText on Tab (the plan 45
    /// XB-9 spike).</summary>
    protected override string GetHelpTextCore() => string.Empty;

    /// <summary>#1591: no children. The content is the visual alone, the
    /// drawing or, for a coverage gap or core's fallback, the source as
    /// text, which object navigation would read (the diagram rule: the
    /// rendered visual carries no peer). The source chord speaks it.</summary>
    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
