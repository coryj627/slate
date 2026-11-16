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
/// identity opens as any other (PR 2). Plan 47 CB-10 (#1730): a switch whose
/// new vault does not open, for any reason, returns to the welcome view once.
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

    /// <summary>Codex round 1: a switch from an open vault to one refused for
    /// its identity hands the keys to the welcome view once, not twice (each
    /// return queues a focus move).</summary>
    [Fact]
    public async Task ASwitchRefusedForNoIdentityReturnsToWelcomeOnce()
    {
        using var first = FixtureVault.Create(1, "wb28-switch-from");
        using var second = FixtureVault.Create(1, "wb28-switch-to");
        string recents = Path.Combine(Path.GetTempPath(), $"wb28-recents-{System.Guid.NewGuid():N}.json");
        var store = new RecentVaultsStore(recents);
        var announced = new List<A11yEvent>();
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(null),
            enqueueUi: action => action(),
            recentVaultsStore: store,
            announce: announced.Add);
        try
        {
            await lifecycle.OpenVaultAsync(first.Root);
            Assert.True(lifecycle.IsVaultOpen);
            int returnedToWelcome = 0;
            lifecycle.ReturnedToWelcome += (_, _) => returnedToWelcome++;
            announced.Clear();
            lifecycle.RootIdentityReader = _ => null;

            await lifecycle.OpenVaultAsync(second.Root);

            Assert.Equal(1, returnedToWelcome);
            Assert.Equal(
                "Could not open vault: " + SlateUniffiMethods.VaultRootIdentityUnavailableDetail(),
                lifecycle.StatusText);
            Assert.DoesNotContain(announced, @event => @event is A11yEvent.VaultOpened);
            Assert.False(lifecycle.IsVaultOpen);
            Assert.DoesNotContain(store.Load(), recent => string.Equals(recent.Path, second.Root, System.StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(recents);
        }
    }

    /// <summary>#1730: a switch from an open vault to one whose open fails
    /// hands the keys to the welcome view once, at the switch's close. The
    /// failure says why and raises no second return, whichever arm catches
    /// it and whether it fails at the open or in the scan.</summary>
    [Theory]
    [InlineData("a vault error at the open")]
    [InlineData("an unexpected error at the open")]
    [InlineData("a vault error in the scan")]
    public async Task ASwitchWhoseOpenFailsReturnsToWelcomeOnce(string arm)
    {
        (Exception fault, string site, string sentence) = arm switch
        {
            "a vault error at the open" => VaultFault("injected open failure", "open"),
            "an unexpected error at the open" => (
                new InvalidOperationException("injected open failure"),
                "open",
                "Unexpected vault error: injected open failure"),
            "a vault error in the scan" => VaultFault("injected scan failure", "scan"),
            _ => throw new ArgumentOutOfRangeException(nameof(arm), arm, "no such arm"),
        };
        using var first = FixtureVault.Create(1, "cb10-switch-from");
        using var second = FixtureVault.Create(1, "cb10-switch-to");
        string recents = Path.Combine(Path.GetTempPath(), $"cb10-recents-{System.Guid.NewGuid():N}.json");
        var store = new RecentVaultsStore(recents);
        var announced = new List<A11yEvent>();
        // Both seams pass through until the fact arms one of them.
        string? armed = null;
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(null),
            enqueueUi: action => action(),
            recentVaultsStore: store,
            announce: announced.Add,
            sessionLoadWorker: work => Task.Run(() => Volatile.Read(ref armed) == "scan" ? throw fault : work()),
            openSession: root => Volatile.Read(ref armed) == "open"
                ? Task.FromException<VaultSession>(fault)
                : Task.Run(() => VaultSession.OpenFilesystem(root)));
        try
        {
            await lifecycle.OpenVaultAsync(first.Root);
            Assert.True(lifecycle.IsVaultOpen, "premise: the first vault is open");
            int returnedToWelcome = 0;
            lifecycle.ReturnedToWelcome += (_, _) => returnedToWelcome++;
            announced.Clear();
            Volatile.Write(ref armed, site);

            await lifecycle.OpenVaultAsync(second.Root);

            Assert.Equal(1, returnedToWelcome);
            Assert.False(lifecycle.IsVaultOpen);
            Assert.Null(lifecycle.Workspace);
            Assert.Equal(sentence, lifecycle.StatusText);
            A11yEvent.HostComposed said = Assert.Single(announced.OfType<A11yEvent.HostComposed>());
            Assert.Equal(sentence, said.Text);
            Assert.Equal(A11yPriority.High, said.Priority);
            if (site == "open")
            {
                // The open never got as far as its vault-opened line or the recents.
                Assert.DoesNotContain(announced, @event => @event is A11yEvent.VaultOpened);
                Assert.DoesNotContain(store.Load(), recent => string.Equals(recent.Path, second.Root, System.StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                // The scan runs after the vault-opened line and the record,
                // and the failure publishes what was recorded.
                _ = Assert.Single(announced, @event => @event is A11yEvent.VaultOpened);
                Assert.Equal(second.Root, lifecycle.RecentVaults[0].Path);
            }
        }
        finally
        {
            File.Delete(recents);
        }
    }

    /// <summary>#1730's other half: once the new vault has shown, the
    /// workspace holds the keys, so a failure after that returns to the
    /// welcome view again. A switch then raises two returns: its close, and
    /// the failure after the vault showed.</summary>
    [Fact]
    public async Task ASwitchThatFailsAfterTheNewVaultShowedReturnsToWelcomeAgain()
    {
        using var first = FixtureVault.Create(1, "cb10-shown-from");
        using var second = FixtureVault.Create(1, "cb10-shown-to");
        string recents = Path.Combine(Path.GetTempPath(), $"cb10-recents-{System.Guid.NewGuid():N}.json");
        var store = new RecentVaultsStore(recents);
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(null),
            enqueueUi: action => action(),
            recentVaultsStore: store);
        try
        {
            await lifecycle.OpenVaultAsync(first.Root);
            Assert.True(lifecycle.IsVaultOpen, "premise: the first vault is open");
            int returnedToWelcome = 0;
            lifecycle.ReturnedToWelcome += (_, _) => returnedToWelcome++;
            // The publication's last step throws, once: the new vault has
            // shown by then (WorkspaceReady is raised after IsVaultOpen).
            bool shownWhenItThrew = false;
            void ThrowOnce(object? sender, EventArgs e)
            {
                lifecycle.WorkspaceReady -= ThrowOnce;
                shownWhenItThrew = lifecycle.IsVaultOpen;
                throw new InvalidOperationException("injected publish failure");
            }

            lifecycle.WorkspaceReady += ThrowOnce;

            await lifecycle.OpenVaultAsync(second.Root);

            Assert.True(shownWhenItThrew, "premise: the failure came after the new vault showed");
            Assert.Equal(2, returnedToWelcome);
            Assert.False(lifecycle.IsVaultOpen);
            Assert.Null(lifecycle.Workspace);
            Assert.Equal("Unexpected vault error: injected publish failure", lifecycle.StatusText);
        }
        finally
        {
            File.Delete(recents);
        }
    }

    private static (Exception Fault, string Site, string Sentence) VaultFault(string message, string site)
    {
        var fault = new VaultException.Io(message);
        return (fault, site, "Could not open vault: " + SlateUniffiMethods.VaultErrorDetail(fault));
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
