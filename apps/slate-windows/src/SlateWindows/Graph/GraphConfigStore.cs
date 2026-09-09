// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
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
/// file untouched, the reason logged (Term W7). WRITE: the existing text
/// read THROWING (an unreadable file refuses the write rather than
/// clobbering it), core's merge through <c>graph_config_encode</c> (an
/// unparseable existing file never clobbered, a newer version never
/// downgraded, unknown keys preserved, the bytes canonical — 0b-12), the
/// bytes to a temporary file beside the target and one
/// <c>File.Move</c> over it, <c>.slate</c> created when missing. The
/// writer census (C-15 xv) walls this class as the ONE writer of the
/// file and this constant as the one place its name appears.
/// </summary>
internal sealed class GraphConfigStore
{
    /// <summary>The file's name, in the one place it appears (C-15 xv).</summary>
    internal const string FileName = "graph.json";

    private readonly string _path;

    public GraphConfigStore(string vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        _path = Path.Combine(vaultRoot, ".slate", FileName);
    }

    public string FilePath => _path;

    /// <summary>Test seam: the existing text's read, in place of the file's;
    /// a fact makes it throw to prove the write refuses rather than clobbers.</summary>
    internal Func<string, string>? ReadExistingForTests { get; set; }

    /// <summary>Term W7's decode arms; the file is never rewritten on a read.</summary>
    public GraphConfigLoad Read()
    {
        if (!File.Exists(_path))
        {
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDefault(), Writable: true, Failure: null);
        }
        string text;
        try
        {
            text = File.ReadAllText(_path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            string reason = $"{FileName} is unreadable: {exception.Message}";
            Trace.TraceWarning("graph config at '{0}' read as the default, read-only: {1}", _path, reason);
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDefault(), Writable: false, Failure: reason);
        }
        try
        {
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDecode(text).Config, Writable: true, Failure: null);
        }
        catch (GraphConfigException exception)
        {
            string reason = Reason(exception);
            Trace.TraceWarning("graph config at '{0}' read as the default, read-only: {1}", _path, reason);
            return new GraphConfigLoad(SlateUniffiMethods.GraphConfigDefault(), Writable: false, Failure: reason);
        }
    }

    /// <summary>The write: read-merge-write through core's codec, atomic
    /// over the target. Throws — an unreadable existing file, core's
    /// refusal, a filesystem failure — and the writer logs (rule W, Term
    /// W2); nothing here swallows.</summary>
    public void Write(GraphConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string? existing = null;
        if (File.Exists(_path))
        {
            // THROWING (the mac's finding 2): a file that exists but cannot
            // be read must not be treated like a missing one and overwritten.
            existing = ReadExistingForTests is { } read ? read(_path) : File.ReadAllText(_path);
        }
        string text = SlateUniffiMethods.GraphConfigEncode(config, existing);
        string temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            SafeFile.TryDelete(temporary);
        }
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
