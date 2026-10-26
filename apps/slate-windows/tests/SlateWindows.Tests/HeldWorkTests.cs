// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>WB-4: the hold the interleaving facts rely on observes its own
/// expiry, so a fact cannot pass a race it lost.</summary>
public sealed class HeldWorkTests
{
    [Fact]
    public void AHoldThatExpiresFailsTheFactAndAHoldThatIsReleasedDoesNot()
    {
        // A hold whose wait runs out before it is released.
        Action? seam = null;
        using (var expiring = new HeldWork(hook => seam = hook, TimeSpan.FromMilliseconds(100)))
        {
            Assert.Same(expiring.Hook, seam);
            Task worker = Task.Run(expiring.Hook);
            Assert.True(worker.Wait(TimeSpan.FromSeconds(10)), "the expiring hold never returned");
            Assert.True(expiring.Entered);
            Exception failure = Assert.ThrowsAny<Exception>(expiring.AssertHeldUntilReleased);
            Assert.Contains("the hold timed out before the fact released it", failure.Message, StringComparison.Ordinal);
            expiring.Release();
            Assert.Null(seam);
        }

        // A hold released while the work waits on it.
        Action? second = null;
        int installs = 0;
        using var released = new HeldWork(
            hook =>
            {
                installs++;
                second = hook;
            },
            TimeSpan.FromSeconds(10));
        Task held = Task.Run(released.Hook);
        released.WaitUntilEntered();
        Assert.False(held.Wait(TimeSpan.FromMilliseconds(200)), "the hold let the work go before it was released");
        released.Release();
        Assert.True(held.Wait(TimeSpan.FromSeconds(10)), "the released work never ran on");
        released.AssertHeldUntilReleased();
        Assert.Null(second);

        // Released is released: a worker that reaches the hook afterwards
        // runs straight through, and Release and Dispose take the hook off
        // its seam once.
        Assert.True(Task.Run(released.Hook).Wait(TimeSpan.FromSeconds(1)), "a hook reached after release still held");
        released.Release();
        released.Dispose();
        Assert.Equal(2, installs);
        released.AssertHeldUntilReleased();
    }
}
