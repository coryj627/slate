// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>Mac-parity rate guard for polite scan progress announcements.</summary>
internal sealed class ScanAnnouncementGate
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(350);

    private readonly Func<DateTimeOffset> _clock;
    private DateTimeOffset _lastFiredAt = DateTimeOffset.MinValue;

    public ScanAnnouncementGate(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public A11yEvent Started(ulong totalFiles)
    {
        _lastFiredAt = _clock();
        string noun = totalFiles == 1 ? "file" : "files";
        // W0.5-3 residue: scan-progress announcement builder.
        return new A11yEvent.HostComposed(
            $"Scanning vault. {totalFiles} {noun} to index.",
            A11yPriority.Medium);
    }

    public A11yEvent? FileIndexed(ulong indexed, ulong total)
    {
        DateTimeOffset now = _clock();
        if (now - _lastFiredAt < MinimumInterval)
        {
            return null;
        }

        _lastFiredAt = now;
        // W0.5-3 residue: scan-progress announcement builder.
        return new A11yEvent.HostComposed(
            $"Indexed {indexed} of {total} files.",
            A11yPriority.Medium);
    }

    public A11yEvent Finished(ulong filesIndexed)
    {
        _lastFiredAt = _clock();
        string noun = filesIndexed == 1 ? "file" : "files";
        // W0.5-3 residue: scan-progress announcement builder.
        return new A11yEvent.HostComposed(
            $"Scan complete. {filesIndexed} {noun} indexed.",
            A11yPriority.Medium);
    }

    public void Reset()
    {
        _lastFiredAt = DateTimeOffset.MinValue;
    }
}
