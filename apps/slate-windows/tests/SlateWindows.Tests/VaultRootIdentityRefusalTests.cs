// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Threading.Tasks;
using SlateWindows;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 (WD24): a vault whose folder's volume gives no file
/// identity does not open, and the reader hears core's sentence saying why
/// (<c>vault_error_detail</c> of the open's <c>Io</c> error), not the
/// binding's field dump.
/// </summary>
public sealed class VaultRootIdentityRefusalTests
{
    private const string Seam = "SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE";

    [Fact]
    public async Task AVaultWhoseFolderGivesNoIdentityRefusesTheOpenInCoresWords()
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
        string? prior = System.Environment.GetEnvironmentVariable(Seam);
        try
        {
            System.Environment.SetEnvironmentVariable(Seam, trigger);
            await lifecycle.OpenVaultAsync(fixture.Root);
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(Seam, prior);
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
}
