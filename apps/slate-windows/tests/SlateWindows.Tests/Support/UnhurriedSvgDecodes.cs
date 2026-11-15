// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SlateWindows.Tests;

/// <summary>
/// #1949: a real SVG decode in this test process runs to its end however
/// slow the machine. The app gives a decode 5 seconds and shows core's
/// timeout sentence past them; on a loaded job VM a real decode ran past
/// that, and a reading fact about something else got the sentence in place
/// of its image. Diagrams and embeds decode on the same path. The facts
/// about the budget set their own per thread
/// (<see cref="EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests"/>)
/// or stub the wait.
/// </summary>
internal static class UnhurriedSvgDecodes
{
    /// <summary>How long a real decode may take here. A fact that waits for
    /// one to land waits longer than this.</summary>
    internal const int Milliseconds = 60_000;

    /// <summary>Runs once, as the test assembly loads, before xUnit runs any
    /// fact.</summary>
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "The default must precede every decode the suite makes.")]
    internal static void LetEveryRealDecodeFinish() =>
        EditorInteractionCoordinator.SvgDecodeBudgetDefaultMillisecondsForTests = Milliseconds;
}
