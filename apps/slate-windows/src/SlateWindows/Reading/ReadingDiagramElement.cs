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
    /// fallback applied by the builder before construction).</summary>
    public string Description { get; }

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
/// (the structured description) is the entire announcement.
/// </summary>
internal sealed class ReadingDiagramImage : Image
{
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
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
