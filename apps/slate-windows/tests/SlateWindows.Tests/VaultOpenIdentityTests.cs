// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Threading.Tasks;
using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 (WD24): a folder whose volume gives no file identity does
/// not open, and the reader hears core's sentence saying why (PR 1); the
/// open refuses a null identity behind core, fail-closed, and a weak
/// identity opens as any other (PR 2).
/// </summary>
public sealed class VaultOpenIdentityTests
{
    private const string UnavailableSeam = "SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE";

    [Fact]
    public async Task AFolderWhoseFileSystemGivesNoFileIdDoesNotOpenAndSaysWhy()
    {
        using var fixture = FixtureVault.Create(1, "wb28-no-identity");
        string recents = Path.Combine(Path.GetTempPath(), $"wb28-recents-{System.Guid.NewGuid():N}.json");
        var store = new RecentVaultsStore(recents);
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: store);
        bool returnedToWelcome = false;
        lifecycle.ReturnedToWelcome += (_, _) => returnedToWelcome = true;

        // The fixture's folder name is unique to this fact, so no other
        // fact's vault trips the seam while it is set.
        string trigger = Path.GetFileName(fixture.Root);
        string? prior = System.Environment.GetEnvironmentVariable(UnavailableSeam);
        try
        {
            System.Environment.SetEnvironmentVariable(UnavailableSeam, trigger);
            await lifecycle.OpenVaultAsync(fixture.Root);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(UnavailableSeam, prior);
            File.Delete(recents);
        }

        Assert.Equal(
            "Could not open vault: " + SlateUniffiMethods.VaultRootIdentityUnavailableDetail(),
            lifecycle.StatusText);
        Assert.False(lifecycle.IsVaultOpen);
        Assert.Null(lifecycle.Workspace);
        Assert.True(returnedToWelcome, "the refusal returns to the welcome surface");
        Assert.Empty(store.Load());
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".slate")), "no .slate is made");
    }

    [Fact]
    public async Task OpenRefusesWhenCoreReportsNoIdentity()
    {
        using var fixture = FixtureVault.Create(1, "wb28-null-identity");
        string recents = Path.Combine(Path.GetTempPath(), $"wb28-recents-{System.Guid.NewGuid():N}.json");
        var store = new RecentVaultsStore(recents);
        var announced = new List<A11yEvent>();
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: store,
            announce: announced.Add);
        VaultSession? read = null;
        lifecycle.RootIdentityReader = session =>
        {
            read = session;
            return null;
        };
        int returnedToWelcome = 0;
        lifecycle.ReturnedToWelcome += (_, _) => returnedToWelcome++;
        try
        {
            await lifecycle.OpenVaultAsync(fixture.Root);
        }
        finally
        {
            File.Delete(recents);
        }

        Assert.Equal(
            "Could not open vault: " + SlateUniffiMethods.VaultRootIdentityUnavailableDetail(),
            lifecycle.StatusText);
        Assert.DoesNotContain(announced, @event => @event is A11yEvent.VaultOpened);
        Assert.False(lifecycle.IsVaultOpen);
        Assert.Null(lifecycle.Workspace);
        Assert.Equal(1, returnedToWelcome);
        Assert.Empty(store.Load());
        Assert.NotNull(read);
        Assert.Throws<ObjectDisposedException>(() => read!.RootIdentity());
    }

    [Fact]
    public async Task AFolderAnsweringOnlyTheFileIndexOpens()
    {
        const string Seam = "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED";
        using var fixture = FixtureVault.Create(1, "wb28-index-only");
        string recents = Path.Combine(Path.GetTempPath(), $"wb28-recents-{System.Guid.NewGuid():N}.json");
        var store = new RecentVaultsStore(recents);
        var announced = new List<A11yEvent>();
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: store,
            announce: announced.Add);
        VaultRootIdentity? identity = null;
        lifecycle.RootIdentityReader = session => identity = session.RootIdentity();
        string? prior = System.Environment.GetEnvironmentVariable(Seam);
        try
        {
            System.Environment.SetEnvironmentVariable(Seam, Path.GetFileName(fixture.Root));
            await lifecycle.OpenVaultAsync(fixture.Root);
            Assert.True(lifecycle.IsVaultOpen);
            Assert.Single(announced, @event => @event is A11yEvent.VaultOpened);
            Assert.NotNull(identity);
            Assert.False(identity!.Persistent, "premise: the file index alone is a weak identity");
            Assert.Single(store.Load());
            lifecycle.CloseVault();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(Seam, prior);
            File.Delete(recents);
        }
    }
}
