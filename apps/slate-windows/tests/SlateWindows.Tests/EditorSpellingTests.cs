// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>The Windows spell checker is bound on first use, never at
/// construction: every workspace owns a preferences view model, and a
/// checker bound for nothing cost about 180 ms to release on Windows 11
/// (the Connections model, a workspace per case, spent two thirds of its
/// time there on the home runner — 2026-10-11).</summary>
public sealed class EditorSpellingTests
{
    [Fact]
    public void AServiceNobodyAskedIsNeverBoundAndIsFreeToDispose()
    {
        var service = (WindowsEditorSpellingService)WindowsEditorSpellingService.CreateForCurrentUser();
        Assert.False(service.IsBoundForTests);
        service.Dispose();
        Assert.False(service.IsBoundForTests);
        Assert.False(service.IsAvailable);
        Assert.False(service.IsBoundForTests);
    }

    [Fact]
    public void TheFirstQuestionBindsThePlatformsAnswer()
    {
        using var service = (WindowsEditorSpellingService)WindowsEditorSpellingService.CreateForCurrentUser();
        Assert.Empty(service.Check(string.Empty));
        Assert.False(service.IsBoundForTests);
        bool available = service.IsAvailable;
        Assert.True(service.IsBoundForTests);
        // The same answer again, and a check consistent with it, whatever
        // the platform offered.
        Assert.Equal(available, service.IsAvailable);
        IReadOnlyList<EditorSpellingError> errors = service.Check("teh");
        Assert.True(available || errors.Count == 0);
    }

    [Fact]
    public void DefaultPreferencesLeaveThePlatformAloneUntilAsked()
    {
        using var preferences = new EditorPreferencesViewModel();
        var service = Assert.IsType<WindowsEditorSpellingService>(preferences.SpellingService);
        Assert.False(service.IsBoundForTests);
    }
}
