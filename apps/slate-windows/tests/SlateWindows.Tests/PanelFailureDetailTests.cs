// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-15c (#1890): every panel failure sentence built from a caught
/// failure says core's detail, never the binding's field dump
/// (<c>@message=…</c>, <c>@path=…</c>). One fact per panel read whose text a
/// reader meets, each driving the read's real catch with a real core refusal
/// over a real vault. Where ordinary input reaches the refusal, the fact uses
/// it (keys core refuses, a citation style that is missing or is not CSL, a
/// note that is not UTF-8 or is gone from disk); elsewhere it constructs the
/// state core refuses: a table's root page zeroed in the vault's index, the
/// op-log's magic overwritten, core's own repair fault seam, or an apply the
/// sheet never arms. Each oracle is core's own words: a literal where core's
/// text is fixed and locale-free, or core's renderer over the same core call
/// made directly where it holds a temporary path or operating-system text.
/// </summary>
/// <remarks>
/// Four panel reads have no fact here. <c>TaskIndexRepairCoordinator.TryRepairNow</c>'s
/// out value is discarded by both its callers, and the Tasks Review's direct
/// toggle speaks core's rendering for a vault error already and takes only an
/// ordinary exception's own message otherwise, so no reader meets a change;
/// ShellFailureDetailCensus holds those three. The sync diagnostics read is
/// reached by no real core call, and its corrected test
/// (<c>SyncDiagnosticsPanelTests.AFailedConfigReadFailsThePairAndKeepsTheRenderedReport</c>)
/// holds it.
/// </remarks>
public sealed class PanelFailureDetailTests
{
    /// <summary>A binding field token: a uniffi exception's Message, never
    /// core's sentence.</summary>
    private const string FieldToken = @"@\w+=";

    /// <summary>SQLite's own text for a damaged page, as core relays it.</summary>
    private const string Malformed = "sqlite error: database disk image is malformed";

    private const string Identical = "old_key and new_key are identical";

    private static readonly DateTimeOffset Clock = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private static string Render(A11yEvent @event) => SlateUniffiMethods.A11yRender(@event).Text;

    private static A11yPriority PriorityOf(A11yEvent @event) => SlateUniffiMethods.A11yRender(@event).Priority;

    /// <summary>Core's words for a refusal: core's renderer over the same
    /// core call made directly, which must refuse with
    /// <typeparamref name="T"/>.</summary>
    private static string DetailOf<T>(Action call) where T : VaultException =>
        SlateUniffiMethods.VaultErrorDetail(Assert.Throws<T>(call));

    private static void Scan(VaultSession session)
    {
        using var cancel = new CancelToken();
        _ = session.ScanInitial(cancel);
    }

    private static VaultSession OpenScanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        Scan(session);
        return session;
    }

    /// <summary>Zeroes the first byte of <paramref name="table"/>'s root page
    /// in the vault's index, so core's next read of that table refuses it as
    /// malformed. The page patched is the live one: with no session open the
    /// closed sessions checkpointed their WAL, and with sessions open
    /// (<paramref name="sessionsOpen"/>) the WAL holds no frame yet.</summary>
    private static void DamageTable(string root, string table, bool sessionsOpen)
    {
        string db = Path.Combine(root, ".slate", "cache.sqlite");
        var wal = new FileInfo(db + "-wal");
        if (sessionsOpen)
        {
            Assert.True(
                wal.Exists && wal.Length == 0,
                $"premise: the open sessions' WAL exists and holds no frame ({(wal.Exists ? $"{wal.Length} bytes" : "absent")})");
        }
        else
        {
            long walBytes = wal.Exists ? wal.Length : 0;
            Assert.True(
                walBytes == 0,
                $"premise: the closed session checkpointed its WAL ({walBytes} bytes left), so the page patched is the live one");
        }
        byte[] bytes;
        using (var read = new FileStream(db, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var copy = new MemoryStream())
        {
            read.CopyTo(copy);
            bytes = copy.ToArray();
        }
        int pageSize = (bytes[16] << 8) | bytes[17];
        if (pageSize == 1)
        {
            pageSize = 65536;
        }
        byte[] name = Encoding.ASCII.GetBytes("table" + table + table);
        int at = bytes.AsSpan().IndexOf(name);
        Assert.True(at >= 0, $"premise: the schema names the {table} table");
        int create = bytes.AsSpan(at + name.Length).IndexOf("CREATE TABLE"u8);
        Assert.InRange(create, 1, 4);
        int page = 0;
        for (int i = 0; i < create; i++)
        {
            page = (page << 8) | bytes[at + name.Length + i];
        }
        Assert.True(page > 1, $"premise: a root page past the schema ({page})");
        long offset = (long)(page - 1) * pageSize;
        Assert.True(
            bytes[offset] is 0x02 or 0x05 or 0x0A or 0x0D,
            $"premise: page {page} starts with a b-tree page type, not 0x{bytes[offset]:X2}");
        using var stream = new FileStream(db, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        stream.Position = offset;
        stream.WriteByte(0x00);
    }

    /// <summary>The note's one op-log with its first eight bytes overwritten,
    /// so core refuses every read of the note's history.</summary>
    private static void BreakTheOplog(string root)
    {
        string log = Assert.Single(Directory.GetFiles(Path.Combine(root, ".slate", "oplog"), "*.oplog"));
        using var stream = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        stream.Write("NOTSLATE"u8);
    }

    // ---- The bibliography leaf ------------------------------------------------

    private static void BibliographyVault(string root)
    {
        File.WriteAllText(
            Path.Combine(root, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n  author = {Knuth, Donald E.},\n  year = {1984}\n}\n");
        File.WriteAllText(Path.Combine(root, "slate.json"), "{\"citations\":{\"bibliography\":\"library.bib\"}}");
        File.WriteAllText(Path.Combine(root, "cited.md"), "# Cited\n\nKnown [@knuth1984] and unknown [@ghostkey].\n");
        using VaultSession first = OpenScanned(root);
        _ = first.SetBibliographySources(first.CitationsPrefs().Sources);
    }

    /// <summary>The entries segment over a damaged <c>bibliography_entries</c>
    /// table. The open reads that table with the leaf's own query, so damage
    /// before the open fails the open; this damages it under the open
    /// session, and a second session's commit drops the first session's page
    /// cache, so its next read meets the damage.</summary>
    [Fact]
    public void TheBibliographyEntriesOverATableDamagedUnderTheSessionSayCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-bib-entries");
        BibliographyVault(fixture.Root);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using VaultSession other = VaultSession.OpenFilesystem(fixture.Root);
        Assert.NotEmpty(session.GetBibliographyEntries());
        DamageTable(fixture.Root, "bibliography_entries", sessionsOpen: true);
        _ = other.SaveText("touch.md", "# Touch\n", null);
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(() => session.GetBibliographyEntries()));
        var leaf = new BibliographyViewModel(session, synchronousForTests: true);

        leaf.EnsureLoaded();

        Assert.Equal("Bibliography couldn't be loaded. " + Malformed, leaf.EntriesErrorSpoken);
        Assert.DoesNotMatch(FieldToken, leaf.EntriesErrorSpoken!);
        leaf.Shutdown();
    }

    /// <summary>The unresolved segment over a damaged <c>file_citations</c>
    /// table: the entries segment still loads, and only the unresolved
    /// segment says core's detail.</summary>
    [Fact]
    public void TheUnresolvedCitationsOverADamagedTableSayCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-bib-unresolved");
        BibliographyVault(fixture.Root);
        DamageTable(fixture.Root, "file_citations", sessionsOpen: false);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(() => session.ListUnresolvedCitations()));
        var leaf = new BibliographyViewModel(session, synchronousForTests: true);
        leaf.EnsureLoaded();
        Assert.Null(leaf.EntriesError);

        leaf.Segment = BibliographySegment.Unresolved;

        Assert.Equal("Bibliography couldn't be loaded. " + Malformed, leaf.UnresolvedErrorSpoken);
        Assert.DoesNotMatch(FieldToken, leaf.UnresolvedErrorSpoken!);
        leaf.Shutdown();
    }

    // ---- The bulk rename sheet --------------------------------------------------

    private static VaultSession BulkRenameVault(string root)
    {
        File.WriteAllText(Path.Combine(root, "a.md"), "---\noldkey: alpha\n---\nBody a.\n");
        File.WriteAllText(Path.Combine(root, "b.md"), "---\noldkey: beta\nnewkey: taken\n---\nBody b.\n");
        return OpenScanned(root);
    }

    private static BulkRenameViewModel Sheet(
        VaultSession session,
        List<A11yEvent> heard,
        Action<BulkRenameViewModel, BulkRenameViewModel.BulkRenameApply>? requestApply = null) =>
        new(session, heard.Add, () => false, requestApply ?? BulkRenameTests.ApplyAtOnce(heard.Add), synchronousForTests: true);

    /// <summary>The sheet shows "Error: " and core's detail, and speaks one
    /// <c>RenameFailed</c> with it at High.</summary>
    private static void AssertRenameFailed(BulkRenameViewModel sheet, List<A11yEvent> heard, string detail)
    {
        Assert.Equal("Error: " + detail, sheet.ErrorText);
        Assert.DoesNotMatch(FieldToken, sheet.ErrorText!);
        A11yEvent.RenameFailed failed = Assert.Single(heard.OfType<A11yEvent.RenameFailed>());
        Assert.Equal("Rename failed: " + detail, Render(failed));
        Assert.Equal(A11yPriority.High, PriorityOf(failed));
        Assert.DoesNotMatch(FieldToken, Render(failed));
    }

    /// <summary>A preview of keys core refuses, typed into the sheet: the same
    /// key twice, a dotted new key, a dotted old key, and an old key that is
    /// not of the key type chosen.</summary>
    [Theory]
    [InlineData("same", "same", null, Identical)]
    [InlineData(
        "oldkey",
        "new.key",
        null,
        "rename refuses dotted keys (\"oldkey\" → \"new.key\"); the read path's dotted-key flattening isn't symmetric with the writer")]
    [InlineData(
        "old.key",
        "renamed",
        null,
        "rename refuses dotted keys (\"old.key\" → \"renamed\"); the read path's dotted-key flattening isn't symmetric with the writer")]
    [InlineData(
        "abc",
        "renamed",
        PropertyKeyType.Integer,
        "invalid property value: the old key does not match the selected YAML key type")]
    public void ABulkRenamePreviewOfKeysCoreRefusesSaysCoresDetail(
        string oldKey, string newKey, PropertyKeyType? keyType, string detail)
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-bulk-preview");
        using VaultSession session = BulkRenameVault(fixture.Root);
        Assert.Equal(detail, DetailOf<VaultException.InvalidArgument>(() =>
        {
            using var cancel = new CancelToken();
            _ = keyType is { } kind
                ? session.RenamePropertyByIdentityAcrossVault(
                    SlateUniffiMethods.PropertyKeyIdentityForType(oldKey, kind), newKey, dryRun: true, cancel)
                : session.RenamePropertyAcrossVault(oldKey, newKey, dryRun: true, cancel);
        }));
        var heard = new List<A11yEvent>();
        BulkRenameViewModel sheet = Sheet(session, heard);
        sheet.OldKey = oldKey;
        sheet.NewKey = newKey;
        if (keyType is { } chosen)
        {
            sheet.OldKeyType = BulkRenameViewModel.KeyTypes.Single(choice => choice.Kind == chosen);
        }
        Assert.True(sheet.CanPreview);

        sheet.Preview();

        AssertRenameFailed(sheet, heard, detail);
        sheet.Shutdown();
    }

    /// <summary>An apply of keys core refuses, published as the workspace
    /// publishes an apply's outcome. Constructed: the sheet arms an apply only
    /// on a pair its preview took, and a pair core refuses never arms, so the
    /// armed apply is re-keyed to the same key twice.</summary>
    [Fact]
    public void ABulkRenameApplyOfKeysCoreRefusesSaysCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-bulk-apply");
        using VaultSession session = BulkRenameVault(fixture.Root);
        var heard = new List<A11yEvent>();
        BulkRenameViewModel.BulkRenameApply? asked = null;
        BulkRenameViewModel sheet = Sheet(session, heard, (_, apply) => asked = apply);
        sheet.OldKey = "oldkey";
        sheet.NewKey = "renamed";
        sheet.Preview();
        Assert.True(sheet.CanApply);
        Assert.True(sheet.Apply());
        Assert.NotNull(asked);
        BulkRenameViewModel.BulkRenameApply refused = asked with { NewKey = asked.OldKey };
        Assert.Equal(Identical, DetailOf<VaultException.InvalidArgument>(() =>
        {
            using var cancel = new CancelToken();
            _ = session.RenamePropertyAcrossVault(refused.OldKey, refused.NewKey, dryRun: true, cancel);
        }));
        BulkRenameViewModel.ApplyOutcome? outcome = null;
        _ = sheet.StartApplyWork(refused, ran => outcome = ran);
        Assert.NotNull(outcome);
        Assert.Null(outcome.Report);
        heard.Clear();

        sheet.PublishApply(refused, outcome.Report, outcome.Error);

        AssertRenameFailed(sheet, heard, Identical);
        sheet.Shutdown();
    }

    // ---- The citations leaf -----------------------------------------------------

    /// <summary>A citation style core cannot load, named by the vault's
    /// <c>slate.json</c>: a style file that is missing, and one that is not
    /// CSL.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("broken")]
    public void ACitationStyleCoreCannotLoadSaysCoresDetail(string style)
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-citations");
        File.WriteAllText(
            Path.Combine(fixture.Root, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n  author = {Knuth, Donald E.},\n  year = {1984}\n}\n");
        if (style == "broken")
        {
            File.WriteAllText(Path.Combine(fixture.Root, "broken.csl"), "this is not CSL\n");
        }
        File.WriteAllText(
            Path.Combine(fixture.Root, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"" + style + "\"}}");
        File.WriteAllText(Path.Combine(fixture.Root, "cited.md"), "# Cited\n\nResolved [@knuth1984].\n");
        using VaultSession session = OpenScanned(fixture.Root);
        _ = session.SetBibliographySources(session.CitationsPrefs().Sources);
        string styleId = Path.GetFileNameWithoutExtension(session.CitationsPrefs().DefaultStyle!);
        CitationReference[] references = session.ListCitationsInFile("cited.md");
        Assert.NotEmpty(references);
        string detail = DetailOf<VaultException.CslStyleUnreadable>(() => session.RenderCitation(references[0], styleId));
        var panel = new CitationsPanelViewModel(session, _ => { }, synchronousForTests: true);

        panel.NoteChanged("cited.md");

        Assert.Equal("Citations couldn't be loaded. " + detail, panel.ErrorSpoken);
        Assert.DoesNotMatch(FieldToken, panel.ErrorSpoken!);
        panel.Shutdown();
    }

    // ---- The History panel ------------------------------------------------------

    private static string SaveVersions(VaultSession session, int count)
    {
        string? hash = null;
        for (int i = 0; i < count; i++)
        {
            hash = session.SaveText("n.md", $"# N\n\nversion {i}\n", hash).NewContentHash;
        }
        return hash!;
    }

    [Fact]
    public void TheHistoryFirstPageOverABrokenOplogSaysCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-history");
        using VaultSession session = OpenScanned(fixture.Root);
        _ = SaveVersions(session, 3);
        BreakTheOplog(fixture.Root);
        string detail = DetailOf<VaultException.Io>(() => session.ListVersions("n.md", new Paging(null, 50)));
        var history = new HistoryViewModel(session, synchronousForTests: true);

        history.NoteChanged("n.md");

        Assert.Equal(detail, history.LoadError);
        Assert.DoesNotMatch(FieldToken, history.LoadError!);
        history.Shutdown();
    }

    [Fact]
    public void TheHistoryOlderPageOverABrokenOplogSaysCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-history-older");
        using VaultSession session = OpenScanned(fixture.Root);
        _ = SaveVersions(session, 55);
        var history = new HistoryViewModel(session, synchronousForTests: true);
        history.NoteChanged("n.md");
        Assert.Null(history.LoadError);
        Assert.True(history.CanLoadOlder);
        string? cursor = session.ListVersions("n.md", new Paging(null, 50)).NextCursor;
        Assert.NotNull(cursor);
        BreakTheOplog(fixture.Root);
        string detail = DetailOf<VaultException.Io>(() => session.ListVersions("n.md", new Paging(cursor, 50)));

        history.LoadOlder();

        Assert.Equal(detail, history.LoadError);
        Assert.DoesNotMatch(FieldToken, history.LoadError!);
        history.Shutdown();
    }

    /// <summary>Compare against current over a broken op-log. (Compare
    /// against current after an edit outside Slate refuses as "no such
    /// version", a defect of its own, so it is not this fact's
    /// input.)</summary>
    [Fact]
    public void TheHistoryCompareOverABrokenOplogSaysCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-history-diff");
        using VaultSession session = OpenScanned(fixture.Root);
        string current = SaveVersions(session, 3);
        var history = new HistoryViewModel(session, synchronousForTests: true)
        {
            CurrentContentHashProvider = () => current,
        };
        history.NoteChanged("n.md");
        HistoryVersionRow row = history.DayGroups.SelectMany(group => group.Rows).Last(r => !r.IsMarker);
        BreakTheOplog(fixture.Root);
        string detail = DetailOf<VaultException.Io>(() => session.DiffVersions("n.md", row.ContentHashAfter, current));

        history.CompareAgainstCurrent(row);

        Assert.Equal(detail, history.InlineDiff?.Error);
        Assert.DoesNotMatch(FieldToken, history.InlineDiff!.Error!);
        history.Shutdown();
    }

    [Fact]
    public void TheDeletedFilesOverADamagedTableSayCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "panel-failure-history-deleted");
        using (VaultSession first = OpenScanned(fixture.Root))
        {
            _ = first.SaveText("gone.md", "# Gone\n", null);
            first.DeleteFile("gone.md");
        }
        DamageTable(fixture.Root, "structural_ops", sessionsOpen: false);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(() => session.ListDeletedFiles(new Paging(null, 200))));
        var history = new HistoryViewModel(session, synchronousForTests: true);

        history.LoadDeletedFiles();

        Assert.Equal(Malformed, history.DeletedError);
        history.Shutdown();
    }

    // ---- The properties header --------------------------------------------------

    /// <summary>A note core cannot read: one with a byte that is not UTF-8
    /// after its frontmatter, and one missing on disk. The header's error
    /// (shown as "Error: …") and the one spoken reload failure, at High, say
    /// core's detail.</summary>
    [Theory]
    [InlineData("not-utf8")]
    [InlineData("missing")]
    public void ThePropertiesOfANoteCoreCannotReadSayCoresDetail(string kind)
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-properties");
        string path = kind == "missing" ? "gone.md" : "bad.md";
        if (kind == "not-utf8")
        {
            File.WriteAllBytes(Path.Combine(fixture.Root, path), [.. "---\ntitle: a\n---\n"u8.ToArray(), 0xFF, (byte)'\n']);
        }
        using VaultSession session = OpenScanned(fixture.Root);
        string detail = kind == "missing"
            ? DetailOf<VaultException.Io>(() => session.ReadNoteParts(path))
            : DetailOf<VaultException.InvalidUtf8>(() => session.ReadNoteParts(path));
        if (kind == "not-utf8")
        {
            Assert.Equal("File at bad.md is not valid UTF-8.", detail);
        }
        var heard = new List<A11yEvent>();
        var properties = new NotePropertiesViewModel(session, _ => { }, _ => { }, _ => { }, heard.Add, synchronousForTests: true);

        properties.Load(path);

        Assert.Equal(detail, properties.LoadError);
        Assert.DoesNotMatch(FieldToken, properties.LoadError!);

        properties.ReloadDiscarding("title");

        A11yEvent.PropertiesReloadFailed failed = Assert.Single(heard.OfType<A11yEvent.PropertiesReloadFailed>());
        Assert.Equal("Properties could not be reloaded: " + detail, Render(failed));
        Assert.Equal(A11yPriority.High, PriorityOf(failed));
        properties.Shutdown();
    }

    /// <summary>The header's error also reaches an add's failure: a note
    /// deleted on disk after its tab opened, its header refreshed over the
    /// missing file, then Add Property. The add waits for a header holding the
    /// saved bytes, the header's read fails, and the add speaks one
    /// <c>PropertyEditFailed</c> with the header's error, core's detail, at
    /// High.</summary>
    [Fact]
    public void AnAddOnANoteDeletedOnDiskSaysCoresDetail() => StaThread.Run(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-properties-add");
        string notePath = Path.Combine(fixture.Root, "gone.md");
        File.WriteAllText(notePath, "---\ntitle: a\n---\nBody.\n");
        using VaultSession session = OpenScanned(fixture.Root);
        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], announced.Add, startInteractionBackgroundWork: false);
        workspace.OpenPath("gone.md");
        NotePropertiesViewModel header = workspace.EnsureActiveTabProperties(synchronousForTests: true)!;
        Assert.Null(header.LoadError);
        workspace.OpenAddPropertySheet(synchronousForTests: true);
        AddPropertyViewModel sheet = workspace.AddPropertySheet!;
        File.Delete(notePath);
        string detail = DetailOf<VaultException.Io>(() => session.ReadNoteParts("gone.md"));
        header.RefreshProperties();
        Assert.NotNull(header.LoadError);
        sheet.Key = "added";

        Assert.True(sheet.Add());

        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => announced.OfType<A11yEvent.PropertyEditFailed>().Any(), TimeSpan.FromSeconds(20)),
            "the add's failure was never spoken");
        A11yEvent.PropertyEditFailed failed = Assert.Single(announced.OfType<A11yEvent.PropertyEditFailed>());
        Assert.Equal("Property edit failed: " + detail, Render(failed));
        Assert.Equal(A11yPriority.High, PriorityOf(failed));
        Assert.DoesNotMatch(FieldToken, Render(failed));
        Assert.Equal(detail, header.LoadError);
        Assert.False(File.Exists(notePath));
    }, TimeSpan.FromSeconds(120), "STA test body timed out.");

    // ---- The right pane's links, outline and tasks leaves ------------------------

    /// <summary>A vault whose <c>n.md</c> has a heading, a task and a link,
    /// with one table of its index damaged, and the right pane over that
    /// note.</summary>
    private sealed class DamagedPane : IDisposable
    {
        private readonly FixtureVault _fixture;

        public DamagedPane(string table)
        {
            _fixture = FixtureVault.Create(3, "panel-failure-right-pane");
            File.WriteAllText(Path.Combine(_fixture.Root, "n.md"), "# N\n\n## Part\n\n- [ ] a task\n\nSee [[note0]].\n");
            using (VaultSession first = OpenScanned(_fixture.Root))
            {
            }
            DamageTable(_fixture.Root, table, sessionsOpen: false);
            Session = VaultSession.OpenFilesystem(_fixture.Root);
            Panels = new RightPanePanelsViewModel(
                Session, _ => { }, (_, _) => true, _ => true, (_, _) => { }, (_, _) => true, (_, _) => { }, null,
                synchronousForTests: true);
        }

        public VaultSession Session { get; }

        public RightPanePanelsViewModel Panels { get; }

        public void Dispose()
        {
            Panels.Shutdown();
            Session.Dispose();
            _fixture.Dispose();
        }
    }

    private static bool SaysALoadFailure(string? notice) =>
        notice is not null && notice.StartsWith("Could not", StringComparison.Ordinal);

    /// <summary>A damaged <c>links</c> table breaks the links and embeds
    /// leaves only.</summary>
    [Fact]
    public void TheLinksLeavesOverADamagedTableSayCoresDetail()
    {
        using var pane = new DamagedPane("links");
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(() => pane.Session.NoteLinkPanels(
            "n.md",
            new Paging(null, 200),
            RightPanePanelsViewModel.MaxOutgoingRows,
            RightPanePanelsViewModel.MaxEmbedRows + 1)));

        pane.Panels.NoteChanged("n.md");

        Assert.Equal("Could not load links: " + Malformed, pane.Panels.BacklinksEmptyMessage);
        Assert.Equal("Could not load links: " + Malformed, pane.Panels.OutgoingLinksEmptyMessage);
        Assert.Equal("Could not resolve embeds: " + Malformed, pane.Panels.EmbedsEmptyMessage);
        Assert.False(SaysALoadFailure(pane.Panels.OutlineEmptyMessage));
        Assert.False(SaysALoadFailure(pane.Panels.TasksEmptyMessage));
    }

    /// <summary>A damaged <c>headings</c> table breaks the outline leaf
    /// only.</summary>
    [Fact]
    public void TheOutlineLeafOverADamagedTableSaysCoresDetail()
    {
        using var pane = new DamagedPane("headings");
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(
            () => pane.Session.NoteOutline("n.md", RightPanePanelsViewModel.MaxOutlineRows)));

        pane.Panels.NoteChanged("n.md");

        Assert.Equal("Could not load outline: " + Malformed, pane.Panels.OutlineEmptyMessage);
        Assert.False(SaysALoadFailure(pane.Panels.BacklinksEmptyMessage));
        Assert.False(SaysALoadFailure(pane.Panels.TasksEmptyMessage));
    }

    /// <summary>A damaged <c>tasks</c> table breaks the tasks leaf
    /// only.</summary>
    [Fact]
    public void TheTasksLeafOverADamagedTableSaysCoresDetail()
    {
        using var pane = new DamagedPane("tasks");
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(
            () => pane.Session.NoteTasks("n.md", RightPanePanelsViewModel.MaxTaskRows)));

        pane.Panels.NoteChanged("n.md");

        Assert.Equal("Could not load tasks: " + Malformed, pane.Panels.TasksEmptyMessage);
        Assert.False(SaysALoadFailure(pane.Panels.BacklinksEmptyMessage));
        Assert.False(SaysALoadFailure(pane.Panels.OutlineEmptyMessage));
    }

    // ---- The task index repair and the Tasks Review -----------------------------

    private static VaultSession TasksVault(string root)
    {
        File.WriteAllText(Path.Combine(root, "a.md"), "- [ ] overdue one \U0001F4C5 2026-03-01\n- [ ] undated\n");
        File.WriteAllText(Path.Combine(root, "repair-fault.md"), "- [ ] repair me\n");
        return OpenScanned(root);
    }

    private static TasksReviewViewModel Review(
        VaultSession session, List<A11yEvent> heard, TaskIndexRepairCoordinator? repairs = null) =>
        new(
            session,
            heard.Add,
            (_, _, _) => ReviewOpenRoute.Opened,
            (_, _, _) => ReviewToggleRoute.NoOpenTab,
            new TasksReviewTests.IdleChain(),
            () => Clock,
            repairs,
            synchronousForTests: true);

    /// <summary>A pending repair core refuses with a database error, by core's
    /// own fault seam: the sweep's last error is what the Tasks Review and the
    /// right pane's tasks leaf say, core's detail after each lead.</summary>
    [Fact]
    public void ARepairSweepCoreRefusesSaysCoresDetail()
    {
        const string Refused = "sqlite error: Query is not read-only";
        using IDisposable faultLock = EnvFaultLock.Acquire();
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-repair");
        using VaultSession session = TasksVault(fixture.Root);
        var repairs = new TaskIndexRepairCoordinator(session);
        Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_REINDEX_DB", "repair-fault");
        try
        {
            Assert.Equal(Refused, DetailOf<VaultException.Db>(() => session.RepairOrContainPath("repair-fault.md")));
            Assert.False(repairs.TryRepairNow("repair-fault.md", out _));
            Assert.True(repairs.HasPendingFor("repair-fault.md"));
            var heard = new List<A11yEvent>();
            TasksReviewViewModel review = Review(session, heard, repairs);
            var panels = new RightPanePanelsViewModel(
                session, heard.Add, (_, _) => true, _ => true, (_, _) => { }, (_, _) => true, (_, _) => { }, repairs,
                synchronousForTests: true);

            review.EnsureLoaded();
            panels.NoteChanged("a.md");

            Assert.Equal("Couldn’t load tasks. " + Refused, review.EmptyMessage);
            Assert.Equal("Could not load tasks: " + Refused, panels.TasksEmptyMessage);
            review.Shutdown();
            panels.Shutdown();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_REINDEX_DB", null);
        }
    }

    [Fact]
    public void TheTasksReviewFirstPageOverADamagedTableSaysCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-review");
        using (VaultSession first = TasksVault(fixture.Root))
        {
        }
        DamageTable(fixture.Root, "tasks", sessionsOpen: false);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        var heard = new List<A11yEvent>();
        TasksReviewViewModel review = Review(session, heard);
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(() => session.TasksInVault(
            review.ToTaskFilter(TaskReviewFilter.All), new Paging(null, TasksReviewViewModel.PageSize))));

        review.EnsureLoaded();

        Assert.Equal("Couldn’t load tasks. " + Malformed, review.EmptyMessage);
        review.Shutdown();
    }

    /// <summary>Load more over a damaged <c>tasks</c> table. Constructed: the
    /// first page is a real page a healthy session read before the damage,
    /// published through the review's own publication.</summary>
    [Fact]
    public void TheTasksReviewNextPageOverADamagedTableSaysCoresDetail()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "panel-failure-review-more");
        var many = new StringBuilder();
        for (int i = 0; i < 230; i++)
        {
            many.Append("- [ ] bulk ").Append(i).Append('\n');
        }
        File.WriteAllText(Path.Combine(fixture.Root, "many.md"), many.ToString());
        TaskWithLocationPage firstPage;
        using (VaultSession first = OpenScanned(fixture.Root))
        {
            firstPage = first.TasksInVault(new TaskFilter(null, null, null, null), new Paging(null, TasksReviewViewModel.PageSize));
        }
        Assert.NotNull(firstPage.NextCursor);
        DamageTable(fixture.Root, "tasks", sessionsOpen: false);
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        var heard = new List<A11yEvent>();
        TasksReviewViewModel review = Review(session, heard);
        TaskFilter all = review.ToTaskFilter(TaskReviewFilter.All);
        Assert.Equal(Malformed, DetailOf<VaultException.Db>(
            () => session.TasksInVault(all, new Paging(firstPage.NextCursor, TasksReviewViewModel.PageSize))));
        review.PublishFirstPage(review.LoadRequestIdForTests, firstPage, null, session.TasksIndexRevision(), all, TaskReviewFilter.All);
        Assert.True(review.HasMore);

        review.LoadMore();

        Assert.Equal("Couldn’t load tasks. " + Malformed, review.EmptyMessage);
        review.Shutdown();
    }
}
