// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

public sealed class AppPreferencesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"slate-app-preferences-test-{Guid.NewGuid():N}");

    public AppPreferencesStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private string StorePath => Path.Combine(_directory, "preferences.json");

    private AppPreferencesStore CreateStore() => new(StorePath);

    /// <summary>A fresh install, a corrupt file, and an oversized file
    /// all decode to the same defaults — new tab on.</summary>
    [Fact]
    public void MissingMalformedAndOversizedFilesAreTheDefaults()
    {
        AppPreferencesStore store = CreateStore();
        Assert.True(store.Load().ReadingLinksOpenInNewTab);

        File.WriteAllText(StorePath, "not json");
        Assert.True(store.Load().ReadingLinksOpenInNewTab);

        File.WriteAllText(StorePath, "null");
        Assert.True(store.Load().ReadingLinksOpenInNewTab);

        File.WriteAllBytes(StorePath, new byte[AppPreferencesStore.MaxFileBytes + 1]);
        Assert.True(store.Load().ReadingLinksOpenInNewTab);

        // Fields absent from an old file take their record defaults.
        File.WriteAllText(StorePath, "{}");
        Assert.True(store.Load().ReadingLinksOpenInNewTab);
    }

    [Fact]
    public void SaveAndLoadRoundTripsBothValues()
    {
        AppPreferencesStore store = CreateStore();

        store.Save(new AppPreferencesState(ReadingLinksOpenInNewTab: false));
        Assert.False(store.Load().ReadingLinksOpenInNewTab);

        store.Save(new AppPreferencesState(ReadingLinksOpenInNewTab: true));
        Assert.True(store.Load().ReadingLinksOpenInNewTab);
    }

    /// <summary>The Editor-menu toggle persists through the store and a
    /// NEW preferences view-model restores it — the setting survives an
    /// app restart, which is what makes it a setting (#728 G22).</summary>
    [Fact]
    public void ToggleCommandPersistsAcrossViewModelLifetimes()
    {
        AppPreferencesStore store = CreateStore();
        var announced = new List<A11yEvent>();
        using var first = new EditorPreferencesViewModel(
            announced.Add,
            new FakeEditorSpellingService(),
            preferencesStore: store);
        Assert.True(first.OpenReadingLinksInNewTab);

        first.ToggleReadingLinkTargetCommand.Execute(null);
        Assert.False(first.OpenReadingLinksInNewTab);
        Assert.Equal(
            "Reading links open in the current tab.",
            Assert.IsType<A11yEvent.HostComposed>(Assert.Single(announced)).Text);

        using var second = new EditorPreferencesViewModel(
            _ => { },
            new FakeEditorSpellingService(),
            preferencesStore: store);
        Assert.False(second.OpenReadingLinksInNewTab);

        second.ToggleReadingLinkTargetCommand.Execute(null);
        Assert.True(second.OpenReadingLinksInNewTab);
        Assert.True(CreateStore().Load().ReadingLinksOpenInNewTab);
    }
}
