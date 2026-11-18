// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SlateWindows.Tests;

/// <summary>
/// #1635: SVG decode bodies that count their runs, per byte string, and a
/// wait that ends its budget only once the decode it waits on has entered
/// its body, so a fact reads an exact count as soon as the decode returns.
/// Use <see cref="Parked"/> with <see cref="TimeoutOnceEntered"/>, and
/// <see cref="Ending"/> with the real wait. A parked body waits for
/// <see cref="Release"/>, at most ten seconds; the fact releases it and waits
/// for its slots' <c>Live</c> to return to 0. Nothing here is disposed: a
/// decoder may still be waking on a gate when the fact ends.
/// </summary>
internal sealed class ParkedSvgDecodes
{
    private readonly ConcurrentDictionary<string, int> _runs = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<uint> _waits = new();
    private ManualResetEventSlim _release = new(false);
    private int _total;
    private int _entered;
    private int _waited;

    /// <summary>Every run of either body.</summary>
    public int Runs => Volatile.Read(ref _total);

    /// <summary>The runs of either body over these bytes.</summary>
    public int RunsOf(byte[] bytes) => _runs.GetValueOrDefault(Encoding.UTF8.GetString(bytes));

    /// <summary>The runs of either body over this SVG.</summary>
    public int RunsOf(string svg) => _runs.GetValueOrDefault(svg);

    /// <summary>A body that counts its run, says it has entered, and parks
    /// until <see cref="Release"/>, returning nothing.</summary>
    public byte[]? Parked(byte[] bytes, Func<long, bool> reserve)
    {
        Count(bytes);
        Interlocked.Increment(ref _entered);
        Volatile.Read(ref _release).Wait(TimeSpan.FromSeconds(10));
        return null;
    }

    /// <summary><see cref="Parked"/>, after reserving
    /// <paramref name="cost"/> bytes from the caller's budget.</summary>
    public Func<byte[], Func<long, bool>, byte[]?> ParkedAfterReserving(long cost) =>
        (bytes, reserve) =>
        {
            reserve(cost);
            return Parked(bytes, reserve);
        };

    /// <summary>A body that counts its run and ends at once with nothing:
    /// an ordinary failure.</summary>
    public byte[]? Ending(byte[] bytes, Func<long, bool> reserve)
    {
        Count(bytes);
        return null;
    }

    /// <summary>The milliseconds each decode served by
    /// <see cref="TimeoutOnceEntered"/> was given to wait, in order: what the
    /// app's own wait would have waited.</summary>
    public uint[] Waits => [.. _waits];

    /// <summary>The wait seam: it records the milliseconds the decode was
    /// given, and ends its budget only once the parked body of the decode it
    /// serves has entered, so that body's run is counted when the decode
    /// returns.</summary>
    public uint TimeoutOnceEntered(SafeWaitHandle handle, uint milliseconds)
    {
        _waits.Enqueue(milliseconds);
        int served = Interlocked.Increment(ref _waited);
        Assert.True(
            SpinWait.SpinUntil(() => Volatile.Read(ref _entered) >= served, TimeSpan.FromSeconds(10)),
            "the parked body never entered");
        return 0x102;
    }

    /// <summary>Asserts what each card said, in order, and names every
    /// card's sentence when one differs.</summary>
    public static void AssertSaid(IEnumerable<string?> said, params string[] expected)
    {
        string?[] actual = [.. said];
        Assert.True(
            actual.SequenceEqual(expected),
            $"expected:\n  {string.Join("\n  ", expected)}\nsaid:\n  {string.Join("\n  ", actual)}");
    }

    /// <summary>Lets every parked body go.</summary>
    public void Release() => Volatile.Read(ref _release).Set();

    /// <summary>Lets every parked body go, and parks the next ones.</summary>
    public void Repark()
    {
        ManualResetEventSlim previous = Interlocked.Exchange(ref _release, new ManualResetEventSlim(false));
        previous.Set();
    }

    private void Count(byte[] bytes)
    {
        _runs.AddOrUpdate(Encoding.UTF8.GetString(bytes), 1, (_, runs) => runs + 1);
        Interlocked.Increment(ref _total);
    }
}
