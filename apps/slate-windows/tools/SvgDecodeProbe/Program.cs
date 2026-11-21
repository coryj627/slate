// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Child-process probe for the SVG preview decode (D4). A document that
// gets past SvgPreviewGuard can end the process that decodes it, which an
// in-process test cannot survive, so SvgDecodeOutOfProcessTests runs this
// probe once per document and reads its exit code and its verdict.

using System.IO;
using SlateWindows;

namespace SvgDecodeProbe;

internal static class Program
{
    // The reading view decodes on its refresh's worker since #1634. The probe
    // keeps an STA because a decode's caller may still be one, and the
    // decode's plain Win32 wait is written for it.
    [STAThread]
    private static int Main(string[] args)
    {
        byte[] bytes = File.ReadAllBytes(args[0]);
        // The guard's verdict, not the preview's time budget (#1581): a
        // document the guard should have refused must still run out the
        // census's minute, or end this process, rather than be abandoned at
        // the budget and reported as refused.
        EditorInteractionCoordinator.SvgDecodeBudgetMillisecondsForTests = int.MaxValue;
        var image = EditorInteractionCoordinator.DecodeImage(
            bytes, "image/svg+xml", out string? failureReason);
        // The guard's cost refusal carries core's sentence (#1610): it is the
        // guard's verdict, not an abandoned decode.
        Console.WriteLine(
            image is not null ? "decoded"
            : failureReason is null
                || failureReason == EditorInteractionCoordinator.SvgDecodeCostRefusedMessage ? "refused"
            : "abandoned");
        return 0;
    }
}
