// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class LineEndingParityTests
{
    [Theory]
    [InlineData("basic.md", false, true)]
    [InlineData("endings_crlf.md", true, false)]
    [InlineData("endings_mixed.md", true, true)]
    public void BindingReadEditSaveAndReopenPreservesCallerSuppliedLineEndings(
        string fixtureName,
        bool expectsCrLf,
        bool expectsBareLf)
    {
        string fixturePath = Path.Combine(FixturesDirectory, fixtureName);
        byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
        string initial = new UTF8Encoding(false, true).GetString(fixtureBytes);
        Assert.Equal(expectsCrLf, initial.Contains("\r\n", StringComparison.Ordinal));
        Assert.Equal(expectsBareLf, ContainsBareLf(initial));

        string vaultRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-line-ending-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(vaultRoot);
        string vaultFile = Path.Combine(vaultRoot, fixtureName);
        File.Copy(fixturePath, vaultFile);

        try
        {
            string edited = initial.Replace("# ", "# Edited ", StringComparison.Ordinal);
            Assert.NotEqual(initial, edited);

            using (var session = VaultSession.OpenFilesystem(vaultRoot))
            using (var cancel = new CancelToken())
            {
                session.ScanInitial(cancel);
                Assert.Equal(initial, session.ReadText(fixtureName));
                NotePartsBundle parts = session.ReadNoteParts(fixtureName);

                SaveReport report = session.SaveText(
                    fixtureName,
                    edited,
                    parts.ContentHash);

                Assert.NotEqual(parts.ContentHash, report.NewContentHash);
                Assert.Equal((ulong)Encoding.UTF8.GetByteCount(edited), report.NewSizeBytes);
                Assert.Equal(edited, session.ReadText(fixtureName));
            }

            Assert.Equal(Encoding.UTF8.GetBytes(edited), File.ReadAllBytes(vaultFile));

            using var reopened = VaultSession.OpenFilesystem(vaultRoot);
            using var reopenCancel = new CancelToken();
            reopened.ScanInitial(reopenCancel);
            Assert.Equal(edited, reopened.ReadText(fixtureName));
            Assert.Equal(expectsCrLf, edited.Contains("\r\n", StringComparison.Ordinal));
            Assert.Equal(expectsBareLf, ContainsBareLf(edited));
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
        }
    }

    private static bool ContainsBareLf(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n' && (index == 0 || text[index - 1] != '\r'))
            {
                return true;
            }
        }

        return false;
    }

    private static string FixturesDirectory
    {
        get
        {
            string directory = AppContext.BaseDirectory;
            for (int index = 0; index < 8; index++)
            {
                directory = Path.GetDirectoryName(directory)!;
            }

            return Path.Combine(
                directory,
                "crates",
                "slate-core",
                "tests",
                "fixtures",
                "markdown");
        }
    }
}
