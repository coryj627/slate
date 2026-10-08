// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// The CreateFileW boundary's long-path conversion: an ordinary long
/// absolute path gains the extended prefix, and every spelling .NET treats
/// as a device path is returned as given.
/// </summary>
public sealed class WindowsNativePathTests
{
    /// <summary>Long enough that every prefix below crosses MAX_PATH.</summary>
    private static readonly string LongTail =
        string.Join('\\', Enumerable.Repeat(new string('d', 40), 7)) + @"\note.md";

    /// <summary>A hand-edited canvas card can name an NT path (`\??\C:\…`)
    /// inside the vault. Base opened it; prefixing it again produced
    /// `\\?\\??\C:\…`, which CreateFileW refuses.</summary>
    [Theory]
    [InlineData(@"\\?\C:\")]
    [InlineData(@"\??\C:\")]
    [InlineData(@"\\.\C:\")]
    [InlineData(@"//?/C:/")]
    [InlineData(@"//./C:/")]
    [InlineData(@"\/./C:\")]
    [InlineData(@"\\?\UNC\server\share\")]
    public void DeviceAndExtendedSpellingsAreReturnedAsGiven(string prefix)
    {
        string path = prefix + LongTail;
        Assert.True(path.Length >= 260, "premise: the path crosses MAX_PATH.");
        Assert.Equal(path, WindowsNativePath.ForCreateFile(path));
    }

    [Theory]
    [InlineData(@"C:\vault\", @"\\?\C:\vault\")]
    [InlineData(@"C:/vault/", @"\\?\C:\vault\")]
    [InlineData(@"\\server\share\vault\", @"\\?\UNC\server\share\vault\")]
    public void LongOrdinaryPathsTakeTheExtendedPrefix(string prefix, string expectedPrefix)
    {
        string path = prefix + LongTail;
        Assert.Equal(expectedPrefix + LongTail, WindowsNativePath.ForCreateFile(path));
    }

    [Theory]
    [InlineData(@"C:\vault\note.md")]
    [InlineData(@"vault\note.md")]
    public void ShortOrRelativePathsAreUnchanged(string path)
    {
        Assert.Equal(path, WindowsNativePath.ForCreateFile(path));
        string relative = string.Join('\\', Enumerable.Repeat(new string('r', 40), 8));
        Assert.Equal(relative, WindowsNativePath.ForCreateFile(relative));
    }
}
