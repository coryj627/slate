// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SlateWindows.Tests;

/// <summary>
/// #1949: how slow a real SVG decode this test process tolerates. The app
/// gives a decode 5 seconds and shows core's timeout sentence past them; on
/// a loaded job VM a real decode ran past that, and a reading fact about
/// something else got the sentence in place of its image. Diagrams and
/// embeds decode on the same path. Here a decode gets
/// <see cref="Milliseconds"/>, and a fact that waits for decodes to land
/// waits <see cref="LandingWait"/>, so a decode inside the tolerance fails
/// neither. The facts about the budget set their own per thread
/// (<see cref="EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests"/>)
/// or stub the wait.
/// For the same reason (#1635) the process's own decoder slots remember no
/// timeout, so one slow decode of the 8x8 test SVG several files share does
/// not refuse it in every later fact, and a projection's decode deadline is
/// open, so two slow real decodes in one fact do not cut the second. The
/// facts about them set their own slots or seams. Three reach the process's
/// own configuration for one fact each: <c>UnhurriedSvgDecodesTests</c> pins
/// these values and the app's 20 seconds, and turns the process's memory
/// back on to show it remembers;
/// <c>ReadingSvgEmbedDecodeTests.ABackgroundRefreshReadsTheProcessDeadline</c>
/// spends the process's deadline to show a refresh's worker reads it.
/// </summary>
internal static class UnhurriedSvgDecodes
{
    /// <summary>The slowest real decode the suite tolerates: four times the
    /// app's budget, about three times the slowest seen on a job VM.</summary>
    internal const int Milliseconds = 20_000;

    /// <summary>How long a fact waits for <paramref name="decodes"/> real
    /// decodes, run one after another, and the work around them.</summary>
    internal static TimeSpan LandingWait(int decodes = 1) =>
        TimeSpan.FromMilliseconds((long)Milliseconds * decodes) + TimeSpan.FromSeconds(10);

    /// <summary>Runs once, as the test assembly loads, before xUnit runs any
    /// fact. A fact that changes one of these process-wide seams calls it
    /// again in a <c>finally</c> to put them back.</summary>
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255", Justification = "The default must precede every decode the suite makes.")]
    internal static void LetEveryRealDecodeFinish()
    {
        EditorInteractionCoordinator.SvgDecodeBudgetDefaultMillisecondsForTests = Milliseconds;
        EditorInteractionCoordinator.SvgDecodeProcessMemoryOffForTests = true;
        EditorInteractionCoordinator.SvgProjectionDecodeDeadlineDefaultMillisecondsForTests = int.MaxValue;
    }
}
