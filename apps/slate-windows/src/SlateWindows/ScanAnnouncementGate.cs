// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>Rate guard for polite scan progress announcements (D-4). Mac's
/// guard is 350 ms; here Medium queues under All (D-1), so a progress line
/// must be able to finish before the next may queue behind it, or a large
/// vault's "Scan complete" arrives minutes after the sidebar is usable.
/// About 2.5 s at stock reader rates; start and finish are still forced.</summary>
internal sealed class ScanAnnouncementGate
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(2.5);

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

    /// <summary>OD-6 (W7-7 PR 7, contract 38 D-3 as amended): both counts
    /// from the report — files seen and core's hash-authoritative files
    /// changed, never the read count.</summary>
    public A11yEvent Finished(ulong filesSeen, ulong filesChanged)
    {
        _lastFiredAt = _clock();
        return new A11yEvent.VaultScanFinished(filesSeen, filesChanged);
    }

    public void Reset()
    {
        _lastFiredAt = DateTimeOffset.MinValue;
    }
}
