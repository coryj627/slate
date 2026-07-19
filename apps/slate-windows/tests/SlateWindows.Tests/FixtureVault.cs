// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// Disposable temp-directory vault fixture: N small Markdown notes with
/// wikilinks and tags, so scans index real structure. Content is
/// deterministic — assertions can rely on exact counts. (Seeded from the
/// W0-1 probe's fixture; the probe retires with W0-3.)
/// </summary>
internal sealed class FixtureVault : IDisposable
{
    public string Root { get; }
    public int NoteCount { get; }

    private FixtureVault(string root, int noteCount)
    {
        Root = root;
        NoteCount = noteCount;
    }

    public static FixtureVault Create(int notes, string? label = null)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slate-windows-test-{label ?? "vault"}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        for (int i = 0; i < notes; i++)
        {
            string body =
                $"---\ntags: [test]\n---\n\n# Note {i}\n\n" +
                $"Links to [[note{(i + 1) % notes}]] and back to [[note0]].\n\n" +
                $"Body paragraph for note {i} with #test tag inline.\n";
            File.WriteAllText(Path.Combine(root, $"note{i}.md"), body);
        }
        return new FixtureVault(root, notes);
    }

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; leaked temp dirs are acceptable in tests.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
