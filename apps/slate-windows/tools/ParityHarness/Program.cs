// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-A differential-harness skeleton, Windows side (w0_spec §W0-3 item 5,
// #715): serialize → artifact → (diff elsewhere). Copies the fixture
// corpus into a temp vault (scans write .slate/ cache — fixtures stay
// pristine), runs the skeleton surfaces, and writes one canonical
// artifact per fixture plus vault-level search + links artifacts.
//
//   dotnet run --project apps/slate-windows/tools/ParityHarness -- \
//     --fixtures crates/slate-core/tests/fixtures/markdown --out <dir>
//
// Diff against another platform's artifacts (or the committed goldens)
// with scripts/diff-parity-artifacts.py.

using ParityHarness;
using uniffi.slate_uniffi;

string? fixtures = null;
string? outDir = null;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--fixtures")
    {
        fixtures = args[i + 1];
    }
    if (args[i] == "--out")
    {
        outDir = args[i + 1];
    }
}
if (fixtures == null || outDir == null)
{
    Console.Error.WriteLine("usage: ParityHarness --fixtures <dir> --out <dir>");
    return 2;
}

var files = Directory.EnumerateFiles(fixtures, "*.md")
    .Select(Path.GetFileName)
    .Where(f => f != null)
    .Select(f => f!)
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToList();
if (files.Count == 0)
{
    Console.Error.WriteLine($"no .md fixtures under {fixtures}");
    return 2;
}

// Temp vault: fixtures copied byte-exact so the scan's .slate/ cache never
// lands in the checkout.
string vaultRoot = Path.Combine(Path.GetTempPath(), $"parity-harness-{Guid.NewGuid():N}");
Directory.CreateDirectory(vaultRoot);
try
{
    foreach (var f in files)
    {
        File.Copy(Path.Combine(fixtures, f), Path.Combine(vaultRoot, f));
    }

    Directory.CreateDirectory(outDir);
    using var session = VaultSession.OpenFilesystem(vaultRoot);
    using var cancel = new CancelToken();
    session.ScanInitial(cancel);

    foreach (var f in files)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(vaultRoot, f));
        string text = System.Text.Encoding.UTF8.GetString(bytes);
        WriteArtifact(Path.Combine(outDir, f + ".json"), SurfaceSerializer.FileArtifact(f, text));
    }
    WriteArtifact(Path.Combine(outDir, "search.json"), SurfaceSerializer.SearchArtifact(session, cancel));
    WriteArtifact(Path.Combine(outDir, "links.json"), SurfaceSerializer.LinksArtifact(session, files));

    Console.WriteLine($"parity-harness: {files.Count + 2} artifacts -> {outDir}");
    return 0;
}
finally
{
    try
    {
        Directory.Delete(vaultRoot, recursive: true);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}

static void WriteArtifact(string path, string content)
{
    // Byte-exact: UTF-8, no BOM, LF only (the serializer never emits \r).
    File.WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(content));
}
