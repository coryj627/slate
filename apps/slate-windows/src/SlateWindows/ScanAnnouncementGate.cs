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
        return new A11yEvent.VaultScanStarted(totalFiles);
    }

    public A11yEvent? FileIndexed(ulong indexed, ulong total)
    {
        DateTimeOffset now = _clock();
        if (now - _lastFiredAt < MinimumInterval)
        {
            return null;
        }

        _lastFiredAt = now;
        return new A11yEvent.VaultScanProgress(indexed, total);
    }

    public A11yEvent Finished(ulong filesIndexed)
    {
        _lastFiredAt = _clock();
        return new A11yEvent.VaultScanFinished(filesIndexed);
    }

    public void Reset()
    {
        _lastFiredAt = DateTimeOffset.MinValue;
    }
}
