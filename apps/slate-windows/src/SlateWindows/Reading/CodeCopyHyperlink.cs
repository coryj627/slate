// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation.Peers;
using System.Windows.Documents;

namespace SlateWindows.Reading;

/// <summary>
/// The Copy affordance's hyperlink: in-range text (the G23-proof idiom)
/// with BUTTON automation semantics. A real Button in a container is an
/// embedded object — blank in the text pattern — while a plain
/// Hyperlink announced "link Copy code", misreading an in-place action
/// as navigation (field, 2026-07-31). The peer inherits
/// HyperlinkAutomationPeer's Invoke pattern and in-range name and
/// overrides only the control type, which is exactly where NVDA reads
/// the control field's spoken role (UIAControlTypesToNVDARoles keyed on
/// the child element's ControlType; Button and Hyperlink are both
/// name-is-content types, so "Copy code" keeps reading once).
/// </summary>
internal sealed class CodeCopyHyperlink : Hyperlink
{
    public CodeCopyHyperlink(Inline childInline)
        : base(childInline)
    {
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new CodeCopyHyperlinkPeer(this);
}

/// <summary>Hyperlink peer that reports as a Button.</summary>
internal sealed class CodeCopyHyperlinkPeer : HyperlinkAutomationPeer
{
    public CodeCopyHyperlinkPeer(Hyperlink owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Button;

    protected override string GetClassNameCore() => "CodeCopyHyperlink";
}
