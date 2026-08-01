// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-3 (#735): the §W-C label goldens for the task status
/// vocabulary — host-duplicated by designation. The mac twin is
/// TaskStatusPhraseTests.swift; both pin the IDENTICAL verbatim
/// strings (change both together, never one).
/// </summary>
public sealed class TaskStatusPhraseTests
{
    private static TaskItem Task(
        string status,
        bool completed,
        long? dueMs = null,
        int? priority = null,
        string? recurrence = null) => new(
        Ordinal: 0, Text: "t", StatusChar: status, Completed: completed,
        DueMs: dueMs, ScheduledMs: null, Priority: priority,
        Recurrence: recurrence, Line: 1, ByteOffset: 0,
        CheckboxStartByte: 2, CheckboxEndByte: 5);

    [Fact]
    public void StatusPhrasesDistinguishInProgressAndCancelled()
    {
        Assert.Equal(
            "In-progress task.", TaskStatusPhrase.StatusPhrase(Task("/", false)));
        Assert.Equal(
            "Cancelled task.", TaskStatusPhrase.StatusPhrase(Task("-", false)));
        Assert.Equal("In progress", TaskStatusPhrase.StatusWord(Task("/", false)));
        Assert.Equal("Cancelled", TaskStatusPhrase.StatusWord(Task("-", false)));
    }

    [Fact]
    public void SpaceAndXKeepTheBinaryPhrasing()
    {
        Assert.Equal("Open task.", TaskStatusPhrase.StatusPhrase(Task(" ", false)));
        Assert.Equal("Done task.", TaskStatusPhrase.StatusPhrase(Task("x", true)));
        Assert.Equal("Open", TaskStatusPhrase.StatusWord(Task(" ", false)));
        Assert.Equal("Done", TaskStatusPhrase.StatusWord(Task("X", true)));
    }

    [Fact]
    public void UnknownStatusCharsFallBackToTheCompletedBinary()
    {
        Assert.Equal("Open task.", TaskStatusPhrase.StatusPhrase(Task("?", false)));
        Assert.Equal("Done task.", TaskStatusPhrase.StatusPhrase(Task("?", true)));
        Assert.Equal("Open", TaskStatusPhrase.StatusWord(Task("?", false)));
        Assert.Equal("Done", TaskStatusPhrase.StatusWord(Task("?", true)));
    }

    [Fact]
    public void PriorityLabelsMapTheSignedScale()
    {
        Assert.Equal("highest", TaskStatusPhrase.PriorityLabel(2));
        Assert.Equal("high", TaskStatusPhrase.PriorityLabel(1));
        Assert.Equal("low", TaskStatusPhrase.PriorityLabel(-1));
        Assert.Equal("lowest", TaskStatusPhrase.PriorityLabel(-2));
        Assert.Equal("7", TaskStatusPhrase.PriorityLabel(7));
    }

    [Fact]
    public void DueDatesRenderTheAuthoredCalendarDate()
    {
        // 2026-03-01 UTC midnight — readers hear what the user wrote.
        Assert.Equal("2026-03-01", TaskStatusPhrase.FormatDueDate(1772323200000));
    }

    [Fact]
    public void MetadataPartsFollowTheMacOrder()
    {
        var task = Task(
            " ", false, dueMs: 1772323200000, priority: 2,
            recurrence: "every day");
        Assert.Equal(
            ["Due 2026-03-01", "Priority highest", "Repeats every day"],
            TaskStatusPhrase.MetadataParts(task).ToArray());
        Assert.Empty(TaskStatusPhrase.MetadataParts(Task(" ", false)));
    }
}
