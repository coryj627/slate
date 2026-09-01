// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 §E TE-6 (IE-37/IE-38): the vault-file picker's MODEL — ONE
/// GENERATION per open. The paged FFI walks to completion into a
/// generation cache (snapshot honesty: the set is the walk's, and a
/// pick is revalidated at commit time by the verb it feeds);
/// classification applies BEFORE the display cap (ED-2 — a media file
/// beyond the markdown pages' window is still admitted); a filter
/// keystroke refilters the CACHE locally and never restarts
/// enumeration; and a pick from a superseded presentation is
/// discarded by comparing this model reference — the generation IS
/// the object.
/// </summary>
internal sealed class CanvasVaultFilePickerModel
{
    /// <summary>ED-2: the display cap over the classified set.</summary>
    internal const int DisplayCap = 200;

    private readonly ImmutableArray<FileSummary> _classified;

    private CanvasVaultFilePickerModel(ImmutableArray<FileSummary> classified)
    {
        _classified = classified;
    }

    /// <summary>Walk the paged listing to completion, classify, and
    /// seal the generation. `page` is the FFI seam (`list_files` by
    /// cursor); `admit` is the kind gate — media via core's
    /// classification for Add Media, markdown for Add Note.</summary>
    internal static CanvasVaultFilePickerModel LoadAll(
        Func<string?, FileSummaryPage> page,
        Func<FileSummary, bool> admit)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(admit);
        ImmutableArray<FileSummary>.Builder classified =
            ImmutableArray.CreateBuilder<FileSummary>();
        string? cursor = null;
        do
        {
            FileSummaryPage batch = page(cursor);
            foreach (FileSummary file in batch.Items)
            {
                if (admit(file))
                {
                    classified.Add(file);
                }
            }
            cursor = batch.NextCursor;
        }
        while (cursor is not null);
        return new CanvasVaultFilePickerModel(classified.ToImmutable());
    }

    /// <summary>The whole classified generation (uncapped) — the
    /// filter's search space.</summary>
    internal ImmutableArray<FileSummary> Classified => _classified;

    /// <summary>The visible rows: the classified set filtered LOCALLY
    /// (never a re-page), capped for display, with "type to narrow"
    /// answering everything past the cap.</summary>
    internal ImmutableArray<FileSummary> Visible(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        IEnumerable<FileSummary> matched = query.Length == 0
            ? _classified
            : _classified.Where(file =>
                (file.DisplayName ?? file.Name).Contains(
                    query, StringComparison.CurrentCultureIgnoreCase)
                || file.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        return [.. matched.Take(DisplayCap)];
    }

    /// <summary>Whether a pick made against <paramref name="shown"/>
    /// still belongs to THIS generation — a superseded model's pick is
    /// discarded, never applied (IE-38's supersession rule).</summary>
    internal bool Admits(CanvasVaultFilePickerModel shown) => ReferenceEquals(this, shown);
}
