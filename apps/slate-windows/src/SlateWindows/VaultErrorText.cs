// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W7-7 (#1249, R-7): a <see cref="VaultException"/> as text a person can
/// hear — the twin of mac's <c>humanReadableVaultError</c>
/// (AppState.swift), arm for arm. The binding's own <c>Message</c> is
/// uniffi's field dump ("@message=…", "@path=…, @reason=…"; a write
/// conflict's is two content hashes and a modification time): a
/// diagnostic, not copy, so no announcement detail or status line may use
/// it. Every generated variant has an arm here, and
/// <c>VaultErrorTextTests</c> constructs each one, so a variant added to
/// the binding fails that census until it is phrased.
/// </summary>
internal static class VaultErrorText
{
    /// <summary>Reached only by a variant the census has not seen yet.</summary>
    internal const string Unphrased = "The operation failed.";

    public static string HumanReadable(VaultException error) => error switch
    {
        VaultException.Io io => io.message,
        VaultException.Db db => db.message,
        VaultException.Trash trash => trash.message,
        VaultException.InvalidQuery query => query.message,
        VaultException.InvalidArgument argument => argument.message,
        VaultException.TrashConfirmationChanged changed => changed.message,
        VaultException.StructuralMutationIncomplete incomplete => incomplete.message,
        VaultException.InvalidPath invalid => $"Invalid path {invalid.path}: {invalid.reason}",
        VaultException.Cancelled => "Operation cancelled.",
        VaultException.InvalidUtf8 utf8 => $"File at {utf8.path} is not valid UTF-8.",
        VaultException.FileTooLarge large => string.Create(
            CultureInfo.InvariantCulture,
            $"File at {large.path} is {large.size} bytes — larger than this build's refuse threshold."),
        VaultException.Unsupported unsupported => $"{unsupported.feature} is not implemented yet.",
        VaultException.WriteConflict => "File changed externally.",
        VaultException.SavedButUnindexed saved =>
            $"Saved, but not indexed yet: {saved.detail} It will appear after the next scan.",
        VaultException.MalformedFrontmatter malformed =>
            $"Frontmatter at {malformed.path} is malformed: {malformed.reason}.",
        VaultException.BibSourceUnreadable source =>
            $"Bibliography source {source.path} couldn't be opened: {source.reason}.",
        VaultException.CslStyleUnreadable style =>
            $"Citation style {style.path} couldn't be loaded: {style.reason}.",
        VaultException.PrefsUnreadable prefs =>
            $"Preferences file {prefs.path} couldn't be loaded: {prefs.reason}.",
        VaultException.DestinationExists exists => $"Something named {exists.path} already exists there.",
        VaultException.HistoryUnavailable history =>
            $"History for {history.path} is unavailable: it failed an integrity check.",
        _ => Unphrased,
    };
}
