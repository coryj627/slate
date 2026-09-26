// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Frozen;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 7 (#1252, R-9, rounds 26-27): core's document classification,
/// for the paths the host classifies without a core row in hand — a
/// Slate-owned file-change event's path. Core exports both sets
/// (<c>openable_document_extensions</c>, <c>markdown_document_extensions</c>)
/// and a fact pins these equal to them, so the event path, Quick Open and
/// the Bases dependents classify exactly as core's index does: all four
/// Markdown extensions (md, markdown, mdown, mkd), not a host list of one.
/// A rescan's delta rows carry core's own openable flag instead.
/// </summary>
internal static class CoreDocumentClassification
{
    /// <summary>Core's Markdown extensions: what its index marks
    /// <c>is_markdown</c>.</summary>
    internal static readonly FrozenSet<string> MarkdownExtensions =
        FrozenSet.ToFrozenSet(["md", "markdown", "mdown", "mkd"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Core's openable documents: the Markdown extensions plus
    /// canvas and base (<c>FileFilter::OpenableDocuments</c>).</summary>
    internal static readonly FrozenSet<string> OpenableExtensions =
        FrozenSet.ToFrozenSet(
            ["md", "markdown", "mdown", "mkd", "canvas", "base"], StringComparer.OrdinalIgnoreCase);

    internal static bool IsMarkdown(string path) => MarkdownExtensions.Contains(ExtensionOf(path));

    internal static bool IsOpenable(string path) => OpenableExtensions.Contains(ExtensionOf(path));

    private static string ExtensionOf(string path) => System.IO.Path.GetExtension(path).TrimStart('.');
}
