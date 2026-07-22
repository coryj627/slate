// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows;

namespace SlateWindows.Tests;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public async Task SecondaryInstanceForwardsArgumentsToPrimary()
    {
        string identity = $"slate-single-instance-test-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(identity);
        using var secondary = new SingleInstanceCoordinator(identity);
        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);

        var received = new TaskCompletionSource<string[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        primary.StartListening(arguments => received.TrySetResult(arguments));

        string[] expected = ["--from-jump-list", @"C:\Vaults\My Notes"];
        Assert.True(secondary.SendActivation(expected, TimeSpan.FromSeconds(5)));

        Assert.Equal(expected, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void OversizedActivationIsRejectedBeforeConnecting()
    {
        string identity = $"slate-single-instance-test-{Guid.NewGuid():N}";
        using var primary = new SingleInstanceCoordinator(identity);
        using var secondary = new SingleInstanceCoordinator(identity);

        string oversized = new('x', SingleInstanceCoordinator.MaxMessageBytes + 1);
        Assert.False(secondary.SendActivation([oversized], TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void IdentityIsScopedToTheCurrentWindowsSession()
    {
        string identity = $"slate-single-instance-session-{Guid.NewGuid():N}";
        using var firstSession = new SingleInstanceCoordinator(identity, sessionId: 101);
        using var secondSession = new SingleInstanceCoordinator(identity, sessionId: 202);

        Assert.True(firstSession.IsPrimary);
        Assert.True(secondSession.IsPrimary);
        Assert.NotEqual(firstSession.PipeNameForTesting, secondSession.PipeNameForTesting);
        Assert.EndsWith("-S101", firstSession.PipeNameForTesting, StringComparison.Ordinal);
        Assert.EndsWith("-S202", secondSession.PipeNameForTesting, StringComparison.Ordinal);
    }

    [Fact]
    public void VaultPathRoutingSkipsOptionsAndHonorsSeparator()
    {
        Assert.Equal(
            @"C:\Vaults\Alpha",
            ActivationArguments.FindVaultPath(["--verbose", @"C:\Vaults\Alpha"]));
        Assert.Equal(
            "--vault-named-folder",
            ActivationArguments.FindVaultPath(["--verbose", "--", "--vault-named-folder"]));
        Assert.Null(ActivationArguments.FindVaultPath(["--census-log-probe"]));
        Assert.Equal(
            "result.json",
            ActivationArguments.OptionValue(
                ["--other", "--census-single-instance-primary", "result.json"],
                "--census-single-instance-primary"));
    }

    [Theory]
    [InlineData(@"C:\Vaults\Alpha", @"C:\Vaults\Alpha")]
    [InlineData(@"C:\Vaults\My Notes", "\"C:\\Vaults\\My Notes\"")]
    [InlineData("", "\"\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    public void WindowsCommandLineQuotingIsDeterministic(string input, string expected)
    {
        Assert.Equal(expected, ActivationArguments.QuoteForWindowsCommandLine(input));
    }
}
