// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-3 (#735): the task status label vocabulary — the mac
/// TaskStatusPhrase.swift strings, host-duplicated by designation
/// (§W-C label goldens: each platform pins the identical verbatim
/// strings in its own unit tests; recorded in w_c_matrix.md).
///
/// The status set is OPEN (any single char round-trips; only x/X
/// mean completed) — '/' and '-' are the Tasks-plugin conventions
/// the labels distinguish (#423), and unknown chars deliberately
/// fall back to the completed/open binary.
/// </summary>
internal static class TaskStatusPhrase
{
    /// <summary>The trailing sentence of a composed row label:
    /// "Open task." / "Done task." / "In-progress task." /
    /// "Cancelled task."</summary>
    public static string StatusPhrase(TaskItem task) => task.StatusChar switch
    {
        "/" => "In-progress task.",
        "-" => "Cancelled task.",
        _ => task.Completed ? "Done task." : "Open task.",
    };

    /// <summary>The leading status word of the note-panel row label:
    /// "Open" / "Done" / "In progress" / "Cancelled".</summary>
    public static string StatusWord(TaskItem task) => task.StatusChar switch
    {
        "/" => "In progress",
        "-" => "Cancelled",
        _ => task.Completed ? "Done" : "Open",
    };

    /// <summary>Mac priorityLabel: the signed backend scale to
    /// spoken words; anything else falls back to the raw number.</summary>
    public static string PriorityLabel(int priority) => priority switch
    {
        2 => "highest",
        1 => "high",
        -1 => "low",
        -2 => "lowest",
        _ => priority.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The .NET-representable epoch-millis window
    /// (0001-01-01 … 9999-12-31). Core enforces the same task-date
    /// domain at parse time (adversarial round 5), but a value that
    /// slips through — an old index, a future core change — must
    /// degrade, never throw: this formatter runs at ROW-PUBLISH time
    /// on the UI thread, where an ArgumentOutOfRangeException is an
    /// application crash from one line of vault input.</summary>
    internal static readonly long MinFormattableMs =
        DateTimeOffset.MinValue.ToUnixTimeMilliseconds();

    internal static readonly long MaxFormattableMs =
        DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    /// <summary>Mac formatDueDate: UTC-midnight epoch millis back to
    /// the authored "yyyy-MM-dd" — readers hear what the user wrote.
    /// Null when the value has no .NET calendar representation.</summary>
    public static string? FormatDueDate(long dueMs) =>
        dueMs < MinFormattableMs || dueMs > MaxFormattableMs
            ? null
            : DateTimeOffset.FromUnixTimeMilliseconds(dueMs)
                .UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The optional metadata parts of a row, in mac order:
    /// "Due …", "Priority …", "Repeats …". The visible caption joins
    /// them with " · ", the accessible label with ". ". An
    /// unformattable date drops its part — the same degradation the
    /// core parser applies to malformed dates.</summary>
    public static IEnumerable<string> MetadataParts(TaskItem task)
    {
        if (task.DueMs is long due && FormatDueDate(due) is { } formatted)
        {
            yield return $"Due {formatted}";
        }
        if (task.Priority is int priority)
        {
            yield return $"Priority {PriorityLabel(priority)}";
        }
        if (task.Recurrence is { Length: > 0 } recurrence)
        {
            yield return $"Repeats {recurrence}";
        }
    }
}
