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
        Console.WriteLine(
            EditorInteractionCoordinator.DecodeImage(bytes, "image/svg+xml") is null
                ? "refused"
                : "decoded");
        return 0;
    }
}
