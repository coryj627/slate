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
    // The reading view decodes diagrams on the dispatcher thread, an STA.
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
        Console.WriteLine(
            image is not null ? "decoded"
            : failureReason is not null ? "abandoned"
            : "refused");
        return 0;
    }
}
