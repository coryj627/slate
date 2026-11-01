// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>What the store's read answered (rule W, Term W7's decode
/// arms): the config in memory, whether the file may be written back,
/// and — when the default stood in — the reason.</summary>
internal sealed record GraphConfigLoad(GraphConfig Config, bool Writable, string? Failure);

/// <summary>
/// W6-2 PR C (#746), contract C-10: the host I/O 0bD-3 leaves to the host
/// — the mac's <c>GraphConfigStore.swift</c> twin — and nothing else.
/// READ: a missing file is the default and writable; an unreadable,
/// unparseable or newer-version file is the default, NOT writable, the
/// file untouched, a privacy-safe diagnostic logged (Term W7). WRITE:
/// the existing text read THROWING (an unreadable file refuses the write rather than
/// clobbering it), core's merge through <c>graph_config_encode</c> (an
/// unparseable existing file never clobbered, a newer version never
/// downgraded, unknown keys preserved, the bytes canonical — 0b-12), the
/// bytes published atomically through <see cref="AnchoredVaultStore"/>,
/// <c>.slate</c> created when missing inside a root that exists (WB-12,
/// #1475: a gone root is refused, never recreated). The
/// writer census (C-15 xv) walls this class as the ONE writer of the
/// file and this constant as the one place its name appears.
/// </summary>
internal sealed class GraphConfigStore
{
    /// <summary>The file's name, in the one place it appears (C-15 xv).</summary>
    internal const string FileName = "graph.json";

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);

    /// <summary>The existing file's read bound on the write (WB-12): a
    /// larger file refuses the write rather than being read whole.</summary>
    internal const int MaxReadBytes = 1024 * 1024;

    private readonly string _vaultRoot;
    private readonly VaultRootAnchor? _root;
    private readonly string _path;

    /// <summary>
    /// The root this store reaches the vault through (plan 44 WB-28): the
    /// session's held root in production; a store built with no anchor
    /// opens by path, which only the tests may do.
    /// </summary>
    private VaultRootAnchor Root => _root ?? VaultRootAnchor.ByPathForTests(_vaultRoot);

    public GraphConfigStore(string vaultRoot, VaultRootAnchor? root = null)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        _vaultRoot = vaultRoot;
        _root = root;
        _path = Path.Combine(vaultRoot, ".slate", FileName);
    }

    public string FilePath => _path;

    /// <summary>Test seam: the existing text's read, in place of the file's;
    /// a fact makes it throw to prove the write refuses rather than clobbers.</summary>
    internal Func<string, string>? ReadExistingForTests { get; set; }

    /// <summary>Term W7's decode arms; the file is never rewritten on a read.</summary>
    public GraphConfigLoad Read()
    {
        string text;
        try
        {
            text = ReadText();
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            // MISSING is the read's own answer, never `File.Exists`: that
            // probe answers false for a file it cannot stat — a denied ACL,
            // a device error — and Term W7's unreadable arm would then be
            // read as the missing one, handing back a WRITABLE default whose
            // save overwrites a config nobody could read (IPG-8).
            _ = exception;
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDefault(), Writable: true, Failure: null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            string reason = $"{FileName} is unreadable: {exception.Message}";
            HostLog.Write(HostDiagnosticEvent.GraphConfigReadFailed, exception);
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDefault(), Writable: false, Failure: reason);
        }
        try
        {
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDecode(text).Config, Writable: true, Failure: null);
        }
        catch (GraphConfigException exception)
        {
            string reason = Reason(exception);
            HostLog.Write(HostDiagnosticEvent.GraphConfigDecodeFailed, exception);
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDefault(), Writable: false, Failure: reason);
        }
    }

    /// <summary>The write: read-merge-write through core's codec, atomic
    /// over the target. Throws — a vault root that is gone, an unreadable
    /// existing file, core's refusal, a filesystem failure — and the writer
    /// logs (rule W, Term W2); nothing here swallows.</summary>
    public void Write(GraphConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        // #1475 (WB-12): anchored to the root as it is now. A renamed or
        // deleted root throws DirectoryNotFoundException and nothing is
        // recreated at its old path; `.slate` is made only inside a root
        // that exists.
        using AnchoredVaultStore store = AnchoredVaultStore.Open(Root, createDirectory: true)
            ?? throw new DirectoryNotFoundException("The vault's .slate folder could not be opened.");
        string? existing = null;
        try
        {
            // THROWING (the mac's finding 2): a file that exists but cannot
            // be read must not be treated like a missing one and overwritten
            // — so the ABSENCE is the read's own answer here too, and every
            // other failure propagates and refuses the write (IPG-8). An
            // `File.Exists` gate let a denied ACL through as "absent" and
            // then clobbered the file it had never read.
            existing = ReadExistingForTests is { } read ? read(_path) : ReadExisting(store);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            _ = exception;
        }
        string text = SlateUniffiMethods.GraphConfigEncode(config, existing);
        store.WriteAtomically(FileName, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
    }

    /// <summary>The existing file through the anchored store: null when it
    /// is absent; a file that exists but cannot be opened (denied, held, a
    /// directory at its path) throws the anchored store's
    /// <see cref="IOException"/>; invalid UTF-8 throws the strict decoder's
    /// exception. Only the exception's type is ever logged.</summary>
    private static string? ReadExisting(AnchoredVaultStore store)
    {
        byte[]? bytes = store.ReadAllBytesBounded(FileName, MaxReadBytes);
        if (bytes is null)
        {
            return null;
        }
        ReadOnlySpan<byte> text = bytes;
        ReadOnlySpan<byte> preamble = StrictUtf8.Preamble;
        return StrictUtf8.GetString(text.StartsWith(preamble) ? text[preamble.Length..] : text);
    }

    private string ReadText()
    {
        // Through the held root too (plan 44 WB-28 PR 2), bounded as the
        // write's read is, the preamble consumed by the throwing decoder
        // (BOM autodetection could swap in a lenient one and silently
        // repair bytes a later save would overwrite). An absent .slate or
        // file is Term W7's missing arm.
        using AnchoredVaultStore store = AnchoredVaultStore.Open(Root, createDirectory: false)
            ?? throw new FileNotFoundException("The vault has no .slate folder.");
        return ReadExisting(store) ?? throw new FileNotFoundException("The vault has no graph config.");
    }

    /// <summary>The host-facing reason for each core refusal (the mac's
    /// adapter mapping): the parser's message, or the version not downgraded.</summary>
    internal static string Reason(GraphConfigException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is GraphConfigException.NewerVersion
            ? $"{FileName} is a newer version; not downgrading ({exception.Message})"
            : exception.Message;
    }
}
