// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace SlateWindows.AccessibilityTests;

/// <summary>
/// Plan 47 CB-7 (#1788): what a gate read does to a file the app replaces,
/// and what <see cref="GateFile"/> does about it. These run without a
/// window. The holders here are the controls, so this file and
/// <c>GateFile.cs</c> are the two the unit project's rule 4 does not read.
/// </summary>
public sealed class GateFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slate-gate-file-" + Guid.NewGuid().ToString("N"));

    public GateFileTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string NewFile(string text)
    {
        string path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>The app's save, as its weakest writer makes it: a staged
    /// file renamed over the target once, with no second try
    /// (<c>AppPreferencesStore.Save</c>, <c>RecentVaultsStore.Save</c>).</summary>
    private void ReplaceOnce(string path, string text)
    {
        string staged = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(staged, text);
        File.Move(staged, path, overwrite: true);
    }

    /// <summary>The rule the gate's reads are built on, pinned as measured:
    /// Windows refuses a classic replacing rename while any handle is open
    /// on the target, and sharing deletion does not change that. The target
    /// keeps its old bytes, and the same rename lands once the handle is
    /// gone.</summary>
    [Fact]
    public void AClassicReplacingRenameIsRefusedUnderADeleteSharingHolder()
    {
        string path = NewFile("old");

        using (FileStream holder = GateFile.Open(path))
        {
            Assert.Throws<UnauthorizedAccessException>(() => ReplaceOnce(path, "new"));
            Assert.Equal("old", GateFile.TryReadText(path));
        }

        ReplaceOnce(path, "new");
        Assert.Equal("new", GateFile.TryReadText(path));
    }

    /// <summary>What sharing deletion does buy: the app can delete a file a
    /// gate read has open. The control is a reader that does not share
    /// deletion, which refuses the delete.</summary>
    [Fact]
    public void ADeleteSucceedsUnderAGateFileHolder()
    {
        string path = NewFile("held by the gate");
        using (FileStream holder = GateFile.Open(path))
        {
            File.Delete(path);
        }

        Assert.False(File.Exists(path), "the delete under a gate read did not remove the file.");

        string control = NewFile("held without delete sharing");
        using (FileStream holder = File.OpenRead(control))
        {
            Assert.Throws<IOException>(() => File.Delete(control));
        }

        Assert.True(File.Exists(control), "premise: the control's delete was refused, and the file is gone.");
    }

    /// <summary>A poll holds no handle between two reads, so a save made
    /// there lands at its one attempt. Five saves are made from the poll's
    /// own hook, each a single replacing rename with no second try, and
    /// every one lands. A read that kept its handle would refuse the first
    /// (<see cref="AClassicReplacingRenameIsRefusedUnderADeleteSharingHolder"/>).</summary>
    [Fact]
    public void APollHoldsNoHandleBetweenItsReads()
    {
        string path = NewFile("0");
        int saved = 0;

        string? text = GateFile.PollText(
            path,
            read => read == "5",
            TimeSpan.FromSeconds(30),
            betweenReads: () =>
            {
                if (saved < 5)
                {
                    saved++;
                    ReplaceOnce(path, saved.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            });

        Assert.Equal("5", text);
        Assert.Equal(5, saved);
    }

    /// <summary>A poll that never sees its text ends at its timeout with
    /// null, and an absent file is a read that was not ready.</summary>
    [Fact]
    public void APollThatNeverSeesItsTextAnswersNull()
    {
        string path = NewFile("never");
        Assert.Null(GateFile.PollText(path, read => read == "ready", TimeSpan.Zero));
        Assert.Null(GateFile.PollText(Path.Combine(_root, "absent.json"), _ => true, TimeSpan.Zero));
    }

    [Fact]
    public void TryReadTextIsNullForAnAbsentOrUnsharedFile()
    {
        Assert.Null(GateFile.TryReadText(Path.Combine(_root, "absent.json")));
        Assert.Null(GateFile.TryReadBytes(Path.Combine(_root, "no-such-folder", "absent.json")));

        string path = NewFile("unshared");
        using (var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(GateFile.TryReadText(path));
            Assert.Null(GateFile.TryReadBytes(path));
        }

        Assert.Equal("unshared", GateFile.TryReadText(path));
    }

    /// <summary>Text is decoded as <see cref="File.ReadAllText(string)"/>
    /// decodes it: UTF-8 without a mark, and whatever a byte-order mark
    /// names.</summary>
    [Fact]
    public void TextIsDecodedAsFileReadAllTextDecodesIt()
    {
        const string Text = "né ü 日本";
        foreach (Encoding encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode })
        {
            string path = NewFile(string.Empty);
            File.WriteAllText(path, Text, encoding);
            Assert.Equal(File.ReadAllText(path), GateFile.TryReadText(path));
            Assert.Equal(Text, GateFile.ReadText(path));
        }
    }

    /// <summary>A single read waits out a holder that shares nothing: the
    /// holder lets go after the first refused attempt, and the next attempt
    /// reads the file.</summary>
    [Fact]
    public void ReadTextWaitsOutABriefHold()
    {
        string path = NewFile("after the hold");
        var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        int refusals = 0;
        try
        {
            string text = GateFile.ReadText(
                path,
                TimeSpan.FromSeconds(30),
                attempts =>
                {
                    refusals = attempts;
                    holder.Dispose();
                });

            Assert.Equal("after the hold", text);
            Assert.Equal(1, refusals);
        }
        finally
        {
            holder.Dispose();
        }
    }

    /// <summary>A hold that outlasts the read's patience fails naming the
    /// path, and so does a file that is not there, at once.</summary>
    [Fact]
    public void ReadTextNamesThePathItCouldNotRead()
    {
        string path = NewFile("held for good");
        using (var holder = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            IOException held = Assert.Throws<IOException>(() => GateFile.ReadText(path, TimeSpan.Zero, refused: null));
            Assert.Contains(path, held.Message, StringComparison.Ordinal);
            Assert.Contains("refused", held.Message, StringComparison.Ordinal);
        }

        string absent = Path.Combine(_root, "absent.json");
        int refusals = 0;
        IOException missing = Assert.Throws<IOException>(() => GateFile.ReadText(absent, TimeSpan.FromSeconds(30), _ => refusals++));
        Assert.Contains(absent, missing.Message, StringComparison.Ordinal);
        Assert.Equal(0, refusals);
    }
}
