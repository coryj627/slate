// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace SlateWindows.Search;

/// <summary>
/// One contiguous stretch of a search snippet, flagged for visual
/// emphasis. The runs of a snippet always concatenate back to the
/// marker-stripped text, and the markers themselves never appear in
/// any run (contract S3).
/// </summary>
internal sealed record SnippetRun(string Text, bool IsMatch);

/// <summary>
/// Splits core's STX/ETX-marked search snippets into emphasis runs —
/// the Windows twin of mac's <c>SearchOverlay.emphasizedSnippet</c>
/// (<c>SearchOverlay.swift:453-480</c>).
/// </summary>
/// <remarks>
/// <para>
/// Nothing in the repository guarantees the markers are balanced or
/// non-nested: core inserts them via SQLite's <c>snippet()</c> and never
/// validates the output. This is therefore a <b>toggle state machine
/// that cannot throw</b> (contract S3): a stray ETX clears emphasis, a
/// dangling STX leaves the tail emphasised, a nested pair is a no-op,
/// and an empty snippet is legal — every empty-query tag-scope row has
/// one.
/// </para>
/// </remarks>
internal static class SnippetRuns
{
    /// <summary>
    /// Snippet hit markers, transcribed from core's
    /// <c>SNIPPET_HIT_START</c> / <c>SNIPPET_HIT_END</c>
    /// (<c>crates/slate-core/src/search_db.rs:110-111</c>). The Rust
    /// constants are public but carry no <c>uniffi::export</c>, so they
    /// do not cross the FFI — the host hardcodes both and cites that
    /// line (contract S3).
    /// </summary>
    internal const char HitStart = '\u0002';

    /// <inheritdoc cref="HitStart"/>
    internal const char HitEnd = '\u0003';

    /// <summary>
    /// Split <paramref name="snippet"/> into emphasis runs. STX
    /// <b>sets</b> emphasis and ETX <b>clears</b> it — set/clear, not
    /// XOR, which is what makes a nested pair a no-op rather than an
    /// inversion. Empty runs are never emitted, so a marker-only
    /// snippet yields an empty list.
    /// </summary>
    public static IReadOnlyList<SnippetRun> Split(string snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        if (snippet.Length == 0)
        {
            return [];
        }

        var runs = new List<SnippetRun>();
        var run = new StringBuilder();
        bool emphasized = false;

        void Flush()
        {
            if (run.Length > 0)
            {
                runs.Add(new SnippetRun(run.ToString(), emphasized));
                run.Clear();
            }
        }

        foreach (char character in snippet)
        {
            if (character == HitStart)
            {
                Flush();
                emphasized = true;
            }
            else if (character == HitEnd)
            {
                Flush();
                emphasized = false;
            }
            else
            {
                run.Append(character);
            }
        }

        Flush();
        return runs;
    }

    /// <summary>
    /// <paramref name="snippet"/> with both markers removed — the audio
    /// side of contract S4, matching the strip mac performs for its
    /// <c>rowAccessibilityLabel</c> (<c>SearchOverlay.swift:519-524</c>).
    /// </summary>
    public static string StripMarkers(string snippet)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        if (snippet.IndexOfAny([HitStart, HitEnd]) < 0)
        {
            return snippet;
        }

        var stripped = new StringBuilder(snippet.Length);
        foreach (char character in snippet)
        {
            if (character != HitStart && character != HitEnd)
            {
                stripped.Append(character);
            }
        }

        return stripped.ToString();
    }
}
