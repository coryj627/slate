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

    /// <summary>DS-10 (#1811): a choice comes back from a new store, as the next launch
    /// reads it, and goes on disk under the key the Mac ignores.</summary>
    [Fact]
    public void AThemeChoiceSurvivesARestart()
    {
        var applied = new List<string>();
        new ThemePreference(CreateStore(), applied.Add).Choose(ThemeChoice.Light);

        Assert.Equal(ThemeChoice.Light, CreateStore().Load().Theme);
        Assert.Equal(ThemeChoice.Light, new ThemePreference(CreateStore(), applied.Add).Current);
        Assert.Contains("\"theme\": \"light\"", File.ReadAllText(StorePath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"theme\":null}")]
    [InlineData("{\"theme\":\"sepia\"}")]
    [InlineData("{\"theme\":\"LIGHT\"}")]
    [InlineData("not json")]
    public void AnUnknownThemeIsTheDefault(string file)
    {
        File.WriteAllText(StorePath, file);

        Assert.Equal(ThemeChoice.UseWindowsSetting, CreateStore().Load().Theme);
    }

    /// <summary>Every preference shares this file, so saving another must not lose it.</summary>
    [Fact]
    public void AnotherPreferenceKeepsTheTheme()
    {
        AppPreferencesStore store = CreateStore();
        new ThemePreference(store, _ => { }).Choose(ThemeChoice.Dark);
        using var preferences = new EditorPreferencesViewModel(
            _ => { },
            new FakeEditorSpellingService(),
            preferencesStore: store);

        preferences.ToggleReadingLinkTargetCommand.Execute(null);

        Assert.False(CreateStore().Load().ReadingLinksOpenInNewTab);
        Assert.Equal(ThemeChoice.Dark, CreateStore().Load().Theme);
    }

    [Fact]
    public void AThemeChoiceKeepsTheOtherPreferences()
    {
        AppPreferencesStore store = CreateStore();
        store.Save(new AppPreferencesState(ReadingLinksOpenInNewTab: false, MathVerbosity: "terse"));

        new ThemePreference(store, _ => { }).Choose(ThemeChoice.Light);

        AppPreferencesState loaded = CreateStore().Load();
        Assert.Equal(ThemeChoice.Light, loaded.Theme);
        Assert.False(loaded.ReadingLinksOpenInNewTab);
        Assert.Equal("terse", loaded.MathVerbosity);
    }

    /// <summary>A choice that can't be written still shows, as every other preference's
    /// does, and this launch keeps it.</summary>
    [Fact]
    public void AThemeThatCannotBeSavedStillApplies()
    {
        string blocked = Path.Combine(_directory, "a-file");
        File.WriteAllText(blocked, string.Empty);
        var applied = new List<string>();
        var preference = new ThemePreference(new AppPreferencesStore(Path.Combine(blocked, "preferences.json")), applied.Add);

        preference.Choose(ThemeChoice.Dark);

        Assert.Equal(new[] { ThemeChoice.Dark }, applied);
        Assert.Equal(ThemeChoice.Dark, preference.Current);
    }

    [Fact]
    public void ChoosingTheCurrentThemeDoesNothing()
    {
        var applied = new List<string>();
        var preference = new ThemePreference(CreateStore(), applied.Add);

        preference.Choose(ThemeChoice.UseWindowsSetting);

        Assert.Empty(applied);
        Assert.False(File.Exists(StorePath));
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
