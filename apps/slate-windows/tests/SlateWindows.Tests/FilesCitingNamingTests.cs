// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-5 (#737): what the files-citing sheet is CALLED while its lookup
/// is still in flight.
///
/// A dialog's name is read on APPEAR and never re-read, so the name at
/// that instant is the only one a screen-reader user ever hears. The
/// sheet used to be named from <c>Paths.Count</c> unconditionally,
/// which is zero until the background query lands — so a key cited by
/// twelve notes announced "Files citing this entry. 0 files." and the
/// corrected name arrived with nobody listening.
///
/// This lives in a unit test rather than the FlaUI journey on purpose:
/// by the time UIA can poll a newly-shown element the load has already
/// landed, so the end-to-end assertion passes against the broken code
/// (confirmed by mutation). Holding the publish is the only way to
/// observe the state that actually matters.
/// </summary>
public sealed class FilesCitingNamingTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public FilesCitingNamingTests()
    {
        _fixture = FixtureVault.Create(0, "files-citing-naming");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "cited.md"),
            "# Cited\n\nA citation [@knuth1984].\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void TheSheetDoesNotClaimACountBeforeTheLookupLands()
    {
        // Async mode: Load() queues the query rather than running it,
        // so the sheet is observable in exactly the state the user's
        // screen reader sees at open.
        var sheet = new FilesCitingViewModel(
            _session, "knuth1984", returnFocusToken: null, synchronousForTests: false);

        sheet.Load();

        Assert.True(sheet.IsLoading);
        Assert.DoesNotContain("0 file", sheet.AutomationName, StringComparison.OrdinalIgnoreCase);
        // And it does not assert the empty state either, which would
        // put "No files in this vault cite this entry." on screen over
        // a query that has not returned.
        Assert.False(sheet.ShowEmptyState);
    }

    [Fact]
    public void TheSheetNamesTheRealCountOncePublished()
    {
        var sheet = new FilesCitingViewModel(
            _session, "knuth1984", returnFocusToken: null, synchronousForTests: true);

        sheet.Load();

        Assert.False(sheet.IsLoading);
        Assert.Equal(
            CitationPhrase.FilesCitingContainerLabel(sheet.Paths.Count),
            sheet.AutomationName);
    }
}
