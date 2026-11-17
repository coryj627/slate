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

        using (FileStream holder = GateFile.HoldForTheFact(path, FileAccess.Read))
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
        using (FileStream holder = GateFile.HoldForTheFact(path, FileAccess.Read))
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

    /// <summary>A pace fed by the fact: its clock moves only when the fact
    /// or a pause moves it, a pause is written down and then taken off the
    /// clock, and each turn given is written down. No fact here reads the
    /// wall clock.</summary>
    private sealed class FedPace
    {
        public FedPace() => Pace = new GateFile.ReadPace(
            () => Now,
            pause =>
            {
                Events.Add($"pause {pause.TotalMilliseconds:0}");
                Now += pause;
            },
            _ => Events.Add("open"));

        public TimeSpan Now { get; set; } = TimeSpan.FromHours(1);

        public List<string> Events { get; } = [];

        public GateFile.ReadPace Pace { get; }

        public string Told => string.Join(", ", Events);
    }

    /// <summary>The reader keeps the pace itself: a second read of a path,
    /// straight after the first, pauses a whole interval before it opens.
    /// The path is the file, however it is written: another case is the same
    /// path.</summary>
    [Fact]
    public void TwoReadsOfOnePathBackToBackTakeAPauseBeforeTheSecond()
    {
        string path = NewFile("paced");
        var fed = new FedPace();

        Assert.Equal("paced", GateFile.TryReadText(path, fed.Pace));
        Assert.Equal("paced", GateFile.TryReadText(path, fed.Pace));
        Assert.Equal("open, pause 100, open", fed.Told);

        Assert.Equal("paced", GateFile.TryReadText(path.ToUpperInvariant(), fed.Pace));
        Assert.Equal("open, pause 100, open, pause 100, open", fed.Told);
    }

    /// <summary>A read that comes part of the way through the interval waits
    /// out what is left of it, and one that comes after it waits for
    /// nothing.</summary>
    [Fact]
    public void AReadWaitsOutWhatIsLeftOfTheIntervalAndNothingOnceItHasPassed()
    {
        string path = NewFile("paced");
        var fed = new FedPace();

        _ = GateFile.TryReadText(path, fed.Pace);
        fed.Now += TimeSpan.FromMilliseconds(40);
        _ = GateFile.TryReadText(path, fed.Pace);
        Assert.Equal("open, pause 60, open", fed.Told);

        fed.Now += GateFile.PollInterval;
        _ = GateFile.TryReadText(path, fed.Pace);
        Assert.Equal("open, pause 60, open, open", fed.Told);
    }

    /// <summary>The pace is each path's own: reads of two files take no
    /// pause.</summary>
    [Fact]
    public void ReadsOfTwoPathsTakeNoPause()
    {
        string one = NewFile("one");
        string other = NewFile("other");
        var fed = new FedPace();

        Assert.Equal("one", GateFile.TryReadText(one, fed.Pace));
        Assert.Equal("other", GateFile.TryReadText(other, fed.Pace));
        Assert.Equal("open, open", fed.Told);
    }

    /// <summary>A caller that spins on a file cannot camp on it, whatever it
    /// calls: twenty-four reads back to back, through every way in, open the
    /// file twenty-four times with a pause before each but the first, and
    /// never twice without one between.</summary>
    [Fact]
    public void ASpinningCallerOpensOncePerReadWithAPauseBetweenEveryTwo()
    {
        string path = NewFile("spun on");
        var fed = new FedPace();

        for (int read = 0; read < 8; read++)
        {
            Assert.Equal("spun on", GateFile.TryReadText(path, fed.Pace));
            Assert.NotNull(GateFile.TryReadBytes(path, fed.Pace));
            Assert.Equal("spun on", GateFile.ReadText(path, TimeSpan.Zero, refused: null, fed.Pace));
        }

        Assert.Equal(24, fed.Events.Count(step => step == "open"));
        Assert.Equal(23, fed.Events.Count(step => step == "pause 100"));
        Assert.Equal(47, fed.Events.Count);
        for (int step = 1; step < fed.Events.Count; step++)
        {
            Assert.False(
                fed.Events[step] == "open" && fed.Events[step - 1] == "open",
                $"two opens with no pause between them at step {step}: {string.Join(", ", fed.Events)}");
        }
    }

    /// <summary>Each attempt of a poll is a read at the pace, and so is each
    /// attempt at a file that is not there: an attempt that opened nothing
    /// still took its turn.</summary>
    [Fact]
    public void EveryAttemptOfAPollIsPacedAndSoIsAnAttemptAtAnAbsentFile()
    {
        string path = NewFile("polled");
        var fed = new FedPace();
        int asked = 0;

        Assert.Equal("polled", GateFile.PollText(path, _ => ++asked == 3, TimeSpan.FromSeconds(30), pace: fed.Pace));
        Assert.Equal("open, pause 100, open, pause 100, open", fed.Told);

        var absent = new FedPace();
        string nowhere = Path.Combine(_root, "absent.json");
        Assert.Null(GateFile.TryReadText(nowhere, absent.Pace));
        Assert.Null(GateFile.TryReadText(nowhere, absent.Pace));
        Assert.Equal("open, pause 100, open", absent.Told);
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

    /// <summary>A fixture is read from under the base directory, by its
    /// parts: the text is what the file there holds.</summary>
    [Fact]
    public void AFixtureIsReadFromUnderTheBaseDirectory()
    {
        string there = Path.Combine(AppContext.BaseDirectory, "fixtures", "chords.json");

        Assert.Equal(File.ReadAllText(there), GateFile.ReadFixture("fixtures", "chords.json"));
    }

    /// <summary>A rooted part is refused by name before anything is read:
    /// <c>Path.Combine</c> would drop the base directory for it, and the
    /// read would be of whatever file the part names.</summary>
    [Fact]
    public void AFixturePartThatIsRootedIsRefusedByName()
    {
        string outside = NewFile("a file the app could own");

        ArgumentException rooted = Assert.Throws<ArgumentException>(() => GateFile.ReadFixture("fixtures", outside));
        Assert.Contains(outside, rooted.Message, StringComparison.Ordinal);
        Assert.Contains("rooted", rooted.Message, StringComparison.Ordinal);

        // Rooted without a drive, and on a drive without a root.
        Assert.Throws<ArgumentException>(() => GateFile.ReadFixture(@"\vault\note.md"));
        Assert.Throws<ArgumentException>(() => GateFile.ReadFixture("C:note.md"));
        Assert.Throws<ArgumentException>(() => GateFile.ReadFixture("fixtures", string.Empty));
        Assert.Throws<ArgumentException>(() => GateFile.ReadFixture());
    }

    /// <summary>Parts that climb out of the base directory are refused,
    /// whether <c>..</c> is a part of its own or inside one, and the message
    /// says where the path ends. The base directory itself is no fixture.</summary>
    [Fact]
    public void AFixturePathThatLeavesTheBaseDirectoryIsRefused()
    {
        ArgumentException climbed = Assert.Throws<ArgumentException>(() => GateFile.ReadFixture("..", "..", "vault", "note.md"));
        Assert.Contains("not under", climbed.Message, StringComparison.Ordinal);
        Assert.Contains(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "vault", "note.md")), climbed.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(() => GateFile.ReadFixture(@"fixtures\..\..\vault\note.md"));
        Assert.Throws<ArgumentException>(() => GateFile.ReadFixture("fixtures", ".."));
    }

    /// <summary>A <c>..</c> that stays under the base directory is let
    /// through: what is checked is where the path ends, not how it is
    /// spelled.</summary>
    [Fact]
    public void AFixturePathThatClimbsAndStaysUnderTheBaseIsRead()
    {
        Assert.Equal(
            GateFile.ReadFixture("fixtures", "chords.json"),
            GateFile.ReadFixture("fixtures", "..", "fixtures", "chords.json"));
    }
}
