// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>#1795: holds the row the sidebars and the editor share. WPF never arranges
/// an element narrower than it asks to be, so the row and everything around it report
/// at least the sum of its columns however narrow the window gets. This asks for none
/// of the row's width, so its own width is the room the window really has.</summary>
internal sealed class WorkspaceRowHost : Decorator
{
    protected override Size MeasureOverride(Size constraint)
    {
        Child?.Measure(constraint);
        return new Size(0, Child?.DesiredSize.Height ?? 0);
    }
}
