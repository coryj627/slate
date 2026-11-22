// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows.Reading;

/// <summary>
/// The focusable host for one rendered diagram block (W3-3). Focus,
/// Tab, and object navigation speak the CORE structured description
/// through <c>Name</c> — the mac contract verbatim: the description is
/// never composed with prefixes, and the rendered image itself is
/// never what AT announces. The authored source rides HelpText (the
/// UIA analog of mac's "Source" custom content) and the tooltip is
/// the mac 3-line/120-char preview (WCAG 1.4.13 posture, audit #254
/// M2).
/// </summary>
internal sealed class ReadingDiagramElement : ContentControl
{
    public ReadingDiagramElement(string description, string source)
    {
        Description = description;
        Focusable = true;
        AutomationProperties.SetName(this, description);
        AutomationProperties.SetHelpText(
            this,
            string.IsNullOrWhiteSpace(source)
                ? "Source not available."
                : source.Trim());
        string preview = TooltipPreview(source);
        if (preview.Length > 0)
        {
            ToolTip = preview;
        }
    }

    /// <summary>Canonical structured description ("Mermaid diagram."
    /// fallback applied by the builder before construction), or the
    /// placeholder's "Diagram, rendering" until the stage completes it.</summary>
    public string Description { get; private set; }

    /// <summary>#1708: the width the description run before this element can
    /// take on their shared line (the math element's rule, #1595). WPF
    /// measures an embedded element against the whole line, so a drawing
    /// that fills it drops to a line of its own, where line reading reads
    /// only the role. The content is measured that much narrower. Settable,
    /// and measured again when set, because the stage completes this element
    /// in place after it was first measured.</summary>
    public double LineReserve
    {
        get => _lineReserve;
        set
        {
            if (_lineReserve != value)
            {
                _lineReserve = value;
                InvalidateMeasure();
            }
        }
    }

    private double _lineReserve;

    protected override Size MeasureOverride(Size constraint) =>
        ReadingLineReserve.Measure(constraint, _lineReserve, base.MeasureOverride);

    /// <summary>Completes this element in place (WB-20, #1317): the name
    /// becomes <paramref name="description"/> and, when the render produced
    /// one, the frozen <paramref name="visual"/> becomes its content. The
    /// element and its peer stay the same objects, so keyboard focus on it
    /// survives and a UIA client's reference stays valid; the name change
    /// is raised on the peer when one exists. HelpText (the source) and the
    /// tooltip are left alone.</summary>
    internal void Complete(string description, System.Windows.Media.ImageSource? visual)
    {
        string old = Description;
        Description = description;
        AutomationProperties.SetName(this, description);
        if (visual is not null)
        {
            Content = new ReadingDiagramImage
            {
                Source = visual,
                Stretch = System.Windows.Media.Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                // Mac caps at a Dynamic-Type-scaled 600pt (audit #254 M1);
                // WPF has no text-size metric to scale by, so the base cap
                // ships and the scaling is a recorded matrix note.
                MaxHeight = 600,
            };
        }
        if (!string.Equals(old, description, StringComparison.Ordinal)
            && UIElementAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, old, description);
        }
    }

    /// <summary>The mac tooltip rule (audit #254 M2): first three
    /// lines joined with spaces, capped at 120 chars plus an
    /// ellipsis. Empty source yields an empty preview (no
    /// tooltip).</summary>
    internal static string TooltipPreview(string source)
    {
        string trimmed = source.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }
        string joined = string.Join(
            ' ',
            trimmed
                .Split('\n')
                .Take(3)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));
        return joined.Length <= 120 ? joined : joined[..120] + "…";
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ReadingDiagramElementPeer(this);
}

/// <summary>
/// The rendered diagram's Image with NO automation peer — the exact
/// analog of mac's <c>accessibilityHidden(true)</c> on the SwiftUI
/// Image: AT never announces "image"; the containing element's Name
/// (the structured description) is the entire announcement. An embed
/// card's image is one too, after the hidden run of its name (#1708).
/// </summary>
internal sealed class ReadingDiagramImage : Image
{
    /// <summary>#1708: the width the name run before an embed card's image
    /// takes on their shared line; 0 inside a diagram element, whose own
    /// reserve already narrows it.</summary>
    public double LineReserve
    {
        get => _lineReserve;
        set
        {
            if (_lineReserve != value)
            {
                _lineReserve = value;
                InvalidateMeasure();
            }
        }
    }

    private double _lineReserve;

    protected override Size MeasureOverride(Size constraint) =>
        ReadingLineReserve.Measure(constraint, _lineReserve, base.MeasureOverride);

    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}

/// <summary>#1595 and #1708: an element after a hidden speech run is
/// measured narrower by the run's width, or WPF, which measures an embedded
/// element against the whole line, drops a wide one to a line of its own,
/// where line reading reads only the role or a blank.</summary>
internal static class ReadingLineReserve
{
    internal static Size Measure(Size constraint, double reserve, Func<Size, Size> measure)
    {
        if (reserve <= 0 || double.IsInfinity(constraint.Width))
        {
            return measure(constraint);
        }
        double width = Math.Max(0, constraint.Width - reserve);
        Size desired = measure(new Size(width, constraint.Height));
        return new Size(Math.Min(desired.Width, width), desired.Height);
    }
}

/// <summary>
/// The diagram element's peer: Name = the structured description
/// (spoken on focus and object navigation in stock NVDA/JAWS) and
/// localized control type "diagram". No custom-property bridge — the
/// description IS the whole convention (mac parity: no MathML-style
/// side channel exists for diagrams).
/// </summary>
internal sealed class ReadingDiagramElementPeer : FrameworkElementAutomationPeer
{
    public ReadingDiagramElementPeer(ReadingDiagramElement element)
        : base(element)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "diagram";

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;
}
