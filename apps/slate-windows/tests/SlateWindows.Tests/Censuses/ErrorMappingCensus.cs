// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// §W-E error-mapping totality (w0_spec §W0-3 item 2, #715): every
// VaultError arm reaches C# as a typed exception — none as a panic or
// abort. Two layers: a reflection census pinning the generated
// VaultException subclass set to the 17 arms the FFI declares (a new or
// removed arm fails this census until the pin is updated deliberately),
// and organic triggers for a representative spread, seeded from the
// W0-1 probe's error-mapping section. CommandException mapping and the
// untyped-escape path are covered for the third foreign trait.

using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "error-mapping")]
public class ErrorMappingCensus
{
    /// <summary>
    /// The FFI's VaultError arms (crates/slate-uniffi/src/lib.rs). Update
    /// deliberately when the FFI adds or removes an arm — this census is
    /// the C#-side totality pin.
    /// </summary>
    private static readonly string[] PinnedVaultErrorArms =
    {
        "Io",
        "Db",
        "InvalidPath",
        "Trash",
        "Cancelled",
        "InvalidUtf8",
        "FileTooLarge",
        "InvalidQuery",
        "Unsupported",
        "InvalidArgument",
        "DestinationExists",
        "WriteConflict",
        "HistoryUnavailable",
        "MalformedFrontmatter",
        "BibSourceUnreadable",
        "CslStyleUnreadable",
        "PrefsUnreadable",
    };

    [Fact]
    public void GeneratedVaultExceptionSubclasses_MatchThePinnedArmSet()
    {
        var generated = typeof(VaultException)
            .Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(VaultException)))
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            PinnedVaultErrorArms.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            generated);
    }

    [Fact]
    public void RepresentativeArms_TriggerOrganicallyAsTypedExceptions()
    {
        using var vault = FixtureVault.Create(4);
        using var session = VaultSession.OpenFilesystem(vault.Root);
        using var token = new CancelToken();
        session.ScanInitial(token);

        // InvalidPath: a root under an existing file, rejected up front.
        string filePath = Path.Combine(Path.GetTempPath(), $"census-file-{Guid.NewGuid():N}");
        File.WriteAllText(filePath, "not a directory");
        try
        {
            var invalidRoot = Assert.Throws<VaultException.InvalidPath>(
                () => VaultSession.OpenFilesystem(Path.Combine(filePath, "sub")));
            Assert.NotEmpty(invalidRoot.reason);
        }
        finally
        {
            File.Delete(filePath);
        }

        // InvalidPath: traversal escape, with structured fields intact.
        var escape = Assert.Throws<VaultException.InvalidPath>(() => session.ReadText("../outside.md"));
        Assert.Equal("../outside.md", escape.path);
        Assert.NotEmpty(escape.reason);

        // Io: the index knows the file but the bytes are gone from disk.
        File.Delete(Path.Combine(vault.Root, "note3.md"));
        var io = Assert.Throws<VaultException.Io>(() => session.ReadText("note3.md"));
        Assert.NotEmpty(io.message);

        // InvalidUtf8 carries the path.
        byte[] latin1 = { 0x68, 0xE9, 0x6C, 0x6C, 0x6F }; // "héllo" in Latin-1
        File.WriteAllBytes(Path.Combine(vault.Root, "latin1.md"), latin1);
        var utf8 = Assert.Throws<VaultException.InvalidUtf8>(() => session.ReadText("latin1.md"));
        Assert.Equal("latin1.md", utf8.path);

        // InvalidQuery from a malformed FTS5 query.
        Assert.Throws<VaultException.InvalidQuery>(
            () => session.FullTextSearch("\"unterminated", new SearchScope.Vault(), token));

        // Cancelled through the token fast path.
        using var cancelled = new CancelToken();
        cancelled.Cancel();
        Assert.Throws<VaultException.Cancelled>(() => session.ScanInitial(cancelled));

        // DestinationExists from create_exclusive on a live path.
        session.SaveText("exists.md", "body\n", null);
        Assert.Throws<VaultException.DestinationExists>(
            () => session.CreateExclusive("exists.md", "other\n"));
    }

    [Fact]
    public void CommandActionErrors_RoundTripTypedAndUntypedWithoutAbort()
    {
        using var registry = new CommandRegistry();

        // Typed failure round-trips with its message.
        _ = registry.Register(
            new Command("census.fail", "Fail", null, null, CommandSection.File),
            new ScriptedAction(() => throw new CommandException.ActionFailed("boom from C#")));
        var typed = Assert.Throws<CommandException.ActionFailed>(() => registry.InvokeById("census.fail"));
        Assert.Equal("boom from C#", typed.message);

        // Foreign-controlled message truncation at the Rust trust boundary.
        _ = registry.Register(
            new Command("census.long", "Long", null, null, CommandSection.File),
            new ScriptedAction(() => throw new CommandException.ActionFailed(new string('x', 20_000))));
        var truncated = Assert.Throws<CommandException.ActionFailed>(() => registry.InvokeById("census.long"));
        Assert.True(truncated.message.Length < 20_000 && truncated.message.Contains("truncated"));

        // Unknown id is typed with the id attached.
        var unknown = Assert.Throws<CommandException.UnknownId>(() => registry.InvokeById("census.nope"));
        Assert.Equal("census.nope", unknown.id);

        // An untyped C# exception escaping the action surfaces as a managed
        // error on the caller — never a native abort. (The generated
        // binding maps it to PanicException; the census requirement is
        // "managed, catchable", so any managed exception type passes.)
        _ = registry.Register(
            new Command("census.throw", "Throw", null, null, CommandSection.File),
            new ScriptedAction(() => throw new InvalidOperationException("untyped escape")));
        Assert.ThrowsAny<Exception>(() => registry.InvokeById("census.throw"));
    }
}
