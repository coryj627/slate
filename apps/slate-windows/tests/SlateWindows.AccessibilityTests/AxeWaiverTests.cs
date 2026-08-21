// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// #1115: the ONE recorded axe waiver, pinned in both directions
/// (codoki). A waiver is a hole in a gate, so what it does NOT cover
/// matters as much as what it does: everything here that returns false
/// must keep failing the scan. These run without a window — the
/// decision is a pure function over the three properties that carry it.
/// </summary>
public sealed class AxeWaiverTests
{
    [Theory]
    // The waived shape: the rule, the class, each scrollbar part.
    [InlineData("BoundingRectangleSizeReasonable", "RepeatButton", "PageUp", true)]
    [InlineData("BoundingRectangleSizeReasonable", "RepeatButton", "PageDown", true)]
    [InlineData("BoundingRectangleSizeReasonable", "RepeatButton", "LineUp", true)]
    [InlineData("BoundingRectangleSizeReasonable", "RepeatButton", "LineDown", true)]
    // Casing is the UIA provider's business, not ours.
    [InlineData("boundingrectanglesizereasonable", "repeatbutton", "pagedown", true)]
    // A DIFFERENT rule on the same element still fails: the waiver is
    // about one geometric verdict on a transient visual state, not about
    // scrollbar parts in general.
    [InlineData("NameNotNull", "RepeatButton", "PageDown", false)]
    [InlineData("BoundingRectangleNotNull", "RepeatButton", "PageDown", false)]
    // The same rule on ANY other element still fails — the app's own
    // controls are exactly what the rule is for.
    [InlineData("BoundingRectangleSizeReasonable", "Button", "SidebarNewNote", false)]
    [InlineData("BoundingRectangleSizeReasonable", "ListBoxItem", "PageDown", false)]
    // A RepeatButton that is not a scrollbar part still fails.
    [InlineData("BoundingRectangleSizeReasonable", "RepeatButton", "SpinnerUp", false)]
    // Absent properties never widen it.
    [InlineData("BoundingRectangleSizeReasonable", "RepeatButton", null, false)]
    [InlineData("BoundingRectangleSizeReasonable", null, "PageDown", false)]
    [InlineData(null, "RepeatButton", "PageDown", false)]
    public void TheWaiverCoversExactlyTheCollapsedFluentScrollBarParts(
        string? ruleId, string? className, string? automationId, bool waived) =>
        Assert.Equal(
            waived,
            ShellAccessibilityTests.IsFluentCollapsedScrollBarPart(
                ruleId, className, automationId));
}
