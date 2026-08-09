// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.IO;

namespace SlateWindows;

/// <summary>
/// W4-8 (#740) contract SD8 — the Windows twin of the shipped mac
/// <c>SyncMarkerWatcher</c> (#638): live re-detection of sync markers
/// through a BOUNDED directory watch.
///
/// Detection otherwise runs once per vault open plus manual refresh, so
/// a sync system that starts managing the vault mid-session (<c>git
/// init</c>, a LiveSync install, Dropbox pointed at the folder) leaves
/// the leaf stale until reopen. This closes that gap WITHOUT building
/// general vault-watching infrastructure — core's vault watcher is a
/// Milestone-A stub by design, and the scanner skips dot-entries, so no
/// marker event ever reaches the host through the normal event stream.
///
/// Scope and non-goals, deliberately (SD8):
/// - Exactly three NON-recursive watches, on the directories core's
///   in-vault probes read: the vault root, <c>.obsidian</c>, and
///   <c>.obsidian/plugins</c>. Entry-level churn only; never a recursive
///   watch, never content reading.
/// - Ancestor/location signals (an ancestor <c>.dropbox.cache</c>, the
///   OneDrive env roots) describe where the vault LIVES, which does not
///   change mid-session; the per-open probe covers them.
/// - Note saves at the vault root churn the root's entry list too
///   (temp-file + rename), so events over-fire. That is fine: the
///   debounce collapses bursts and the probes behind a refresh are
///   bounded exact-path checks — correctness never depends on event
///   filtering.
///
/// Divergence SDD-1: three <see cref="FileSystemWatcher"/> instances
/// stand in for mac's <c>DispatchSource</c> fds, with identical scope,
/// debounce, ceiling, generation identity, and teardown semantics. The
/// OS event vocabulary differs (Created/Deleted/Renamed/Changed vs
/// write/delete/rename) and is normalized to "entry churn" before the
/// debouncer.
///
/// Threading: this type is UI-free on purpose — <c>fire</c> is invoked
/// on a THREADPOOL context and the OWNER marshals to the UI and
/// re-checks liveness there (SDINV-5). That keeps the watcher's facts
/// dispatcher-free. The callback contract is therefore narrow: it must
/// not block and must not re-enter the watcher (see
/// <see cref="FireIfCurrent"/> for why). The contract is not merely
/// documented — the owner satisfies it STRUCTURALLY by handing the UI
/// hop to the threadpool rather than trusting whatever enqueue
/// delegate it was constructed with.
/// </summary>
internal sealed class SyncMarkerWatcher : IDisposable
{
    /// <summary>The vault-relative directories core's in-vault probes
    /// read: root markers (<c>.git</c>, <c>.stfolder</c>,
    /// <c>.stignore</c>, <c>.dropbox</c>, the OneDrive GUID files …) and
    /// the LiveSync plugin tree. <c>""</c> is the vault root itself.
    /// Exactly three, ordered parent-first so one reconcile pass can arm
    /// a whole chain that appeared at once.</summary>
    internal static readonly string[] WatchedSubdirectories =
        ["", ".obsidian", ".obsidian/plugins"];

    /// <summary>Mac parity: a 2.5 s trailing quiet period.</summary>
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromSeconds(2.5);

    private readonly object _gate = new();
    private readonly Dictionary<string, ArmedWatch> _watches = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _watchedPaths = new(StringComparer.Ordinal);

    /// <summary>Monotonic by construction — a wall clock can step
    /// backwards over a burst and turn the ceiling into a no-op.</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Action _fire;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _maxLatency;

    /// <summary>Session-lifetime and never reused, so a stale handler
    /// can prove whether it belongs to the watch currently sitting at
    /// its key. A handle or object identity would be ABA-vulnerable (a
    /// freed-then-reallocated watcher can alias the old one); a
    /// monotonic counter cannot.</summary>
    private long _nextGeneration;

    /// <summary>The debounce ticket. Every schedule takes a new one and
    /// the elapsed continuation acts only while it still holds the
    /// current ticket (the sidebar refresh idiom, plus the ceiling).
    /// </summary>
    private long _timerTicket;

    /// <summary>When the current burst's FIRST event landed; null means
    /// no burst is in flight. The max-latency ceiling is anchored here,
    /// never on the latest event.</summary>
    private TimeSpan? _burstStart;
    private bool _started;
    private bool _stopped;
    private string? _armFailure;

    /// <param name="vaultRoot">The vault root — the same path core's
    /// detector probes.</param>
    /// <param name="fire">Invoked already-debounced on a THREADPOOL
    /// thread and INSIDE this watcher's lock (see
    /// <see cref="FireIfCurrent"/> for why that is deliberate). The
    /// contract is therefore hard: enqueue and return. It must not
    /// block — a blocking hop (a dispatcher <c>Invoke</c>) stalls
    /// <see cref="Stop"/>, and therefore vault close, for exactly as
    /// long as it blocks — and it must not call back into this
    /// watcher.</param>
    /// <param name="debounce">Trailing quiet period; injectable so facts
    /// need not wait production-scale seconds.</param>
    /// <param name="maxLatency">Ceiling on how long continuous churn may
    /// defer a fire; defaults to 4x the debounce and is clamped to at
    /// least the debounce (a smaller ceiling could never fire before the
    /// trailing deadline, so it would be meaningless).</param>
    public SyncMarkerWatcher(
        string vaultRoot,
        Action fire,
        TimeSpan? debounce = null,
        TimeSpan? maxLatency = null)
    {
        _fire = fire;
        _debounce = debounce is { } injected && injected > TimeSpan.Zero
            ? injected
            : DefaultDebounce;
        TimeSpan ceiling = maxLatency ?? (_debounce * 4);
        _maxLatency = ceiling < _debounce ? _debounce : ceiling;

        string root = vaultRoot;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(vaultRoot));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            // SDR-2: an unusable root arms nothing and is not a fault.
            _armFailure = $"root: {exception.GetType().Name}";
        }

        foreach (string subdir in WatchedSubdirectories)
        {
            _watchedPaths[subdir] = subdir.Length == 0
                ? root
                : Path.Combine(root, subdir.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    /// <summary>SDR-2: the first arm failure seen (a provider-virtualized
    /// root, a share that refuses a change-notification handle), recorded
    /// rather than thrown. Whatever armed keeps watching, and detection
    /// still runs at vault open and on manual refresh.</summary>
    internal string? ArmFailureForTests
    {
        get
        {
            lock (_gate)
            {
                return _armFailure;
            }
        }
    }

    /// <summary>Test seam: how many watches currently hold an OPEN
    /// directory handle. Zero before <see cref="Start"/>, zero after
    /// <see cref="Stop"/>, and — the fact that matters — zero for a
    /// watcher whose owner lost the arm/teardown race. Three
    /// permanently armed handles would otherwise outlive the vault
    /// session and block deleting or renaming the vault directory for
    /// the rest of the process (SDINV-5/SDINV-8).</summary>
    internal int ArmedWatchCountForTests
    {
        get
        {
            lock (_gate)
            {
                return _watches.Values.Count(watch => !watch.Retired);
            }
        }
    }

    /// <summary>Test seam: the generation stamped on the watch sitting
    /// at <paramref name="subdir"/>, or -1 when nothing is armed
    /// there. A CHANGED generation is how a fact proves a watch was
    /// retired and re-armed rather than merely left alone.</summary>
    internal long ArmedGenerationForTests(string subdir)
    {
        lock (_gate)
        {
            return _watches.TryGetValue(subdir, out ArmedWatch? armed) && !armed.Retired
                ? armed.Generation
                : -1;
        }
    }

    /// <summary>Test seam (SDR-1): drive the OS ERROR channel — the
    /// <c>InternalBufferOverflow</c> path — without provoking a real
    /// overflow, which would take thousands of events inside one
    /// buffer window and could never be made deterministic. The error
    /// is raised against whatever generation is armed at
    /// <paramref name="subdir"/> right now, which is exactly what the
    /// OS does. Returns false when nothing is armed there, so a fact
    /// cannot silently assert against a watch that never opened.
    /// </summary>
    internal bool RaiseWatchErrorForTests(string subdir)
    {
        long generation;
        lock (_gate)
        {
            if (!_watches.TryGetValue(subdir, out ArmedWatch? armed) || armed.Retired)
            {
                return false;
            }

            generation = armed.Generation;
        }

        HandleWatchError(subdir, generation);
        return true;
    }

    /// <summary>
    /// Arm every watched directory that currently exists, and return
    /// only once their handles are open.
    ///
    /// The synchronous completion is the contract, not an implementation
    /// detail: SD4 requires ARM-THEN-PROBE, so the caller runs the
    /// vault-open probe immediately after this returns. A marker landing
    /// after the arm emits an event; anything already present is seen by
    /// the probe. Neither order alone closes the window — a probe-first
    /// watcher would miss a marker that lands during setup and stay
    /// stale until the next manual refresh.
    ///
    /// Missing subdirectories (no <c>.obsidian</c> yet) are simply not
    /// armed; the parent watch picks them up when they appear.
    /// Idempotent, and a no-op after <see cref="Stop"/>.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_stopped || _started)
            {
                return;
            }

            _started = true;
            Reconcile();
        }
    }

    /// <summary>
    /// Drop every watch and the pending debounce. Idempotent, and once
    /// it returns <c>fire</c> will not be invoked again — see
    /// <see cref="FireIfCurrent"/> for how that is guaranteed rather
    /// than merely likely (SDINV-5).
    /// </summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            // Retire the pending ticket as well as the watches: a
            // continuation already queued behind this lock finds both
            // the stopped flag and a stale ticket.
            _timerTicket++;
            _burstStart = null;
            foreach (ArmedWatch armed in _watches.Values)
            {
                Retire(armed);
            }

            _watches.Clear();
        }
    }

    public void Dispose() => Stop();

    /// <summary>Bring <see cref="_watches"/> back in line with the
    /// filesystem: retire watches whose directory has vanished or whose
    /// handle the OS reported dead, then arm anything watched that now
    /// exists and is unwatched. Called on every event so a chain created
    /// mid-session (<c>.obsidian</c>, then <c>plugins</c>) is covered
    /// before the next event in it. Caller holds <see cref="_gate"/>.
    /// </summary>
    private void Reconcile()
    {
        if (_stopped)
        {
            return;
        }

        foreach (string subdir in WatchedSubdirectories)
        {
            string path = _watchedPaths[subdir];
            bool exists = Directory.Exists(path);
            if (_watches.TryGetValue(subdir, out ArmedWatch? armed))
            {
                if (exists && !armed.Retired)
                {
                    continue;
                }

                Retire(armed);
                _watches.Remove(subdir);
            }

            if (exists)
            {
                Arm(subdir, path);
            }
        }
    }

    /// <summary>Open one non-recursive watch under a NEW generation.
    /// Caller holds <see cref="_gate"/>.</summary>
    private void Arm(string subdir, string path)
    {
        long generation = ++_nextGeneration;
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(path)
            {
                // The mac fd's `.write` on a directory means "the entry
                // list changed". FileName/DirectoryName are that signal;
                // LastWrite adds the OS's "a direct child was written"
                // churn, which over-fires harmlessly into the debounce.
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite,
                // Every marker this watch exists for is an extensionless
                // dot-name (.git, .obsidian, .stfolder), so the filter
                // has to be "*" and not a pattern with an extension.
                Filter = "*",
                IncludeSubdirectories = false,
            };
            // All four OS event kinds normalize to entry churn (SDD-1).
            // Deleted/Renamed additionally carry the path that VANISHED,
            // which is how a watched child dir loses its watch.
            watcher.Created += (_, _) => HandleEntryChurn(subdir, generation, null);
            watcher.Changed += (_, _) => HandleEntryChurn(subdir, generation, null);
            watcher.Deleted += (_, args) =>
                HandleEntryChurn(subdir, generation, args.FullPath);
            watcher.Renamed += (_, args) =>
                HandleEntryChurn(subdir, generation, args.OldFullPath);
            watcher.Error += (_, _) => HandleWatchError(subdir, generation);
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (
            exception is IOException
                or ArgumentException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
        {
            // SDR-2: non-fatal. Record the first failure and carry on
            // with whatever did arm; a later reconcile retries this key.
            watcher?.Dispose();
            _armFailure ??= $"{(subdir.Length == 0 ? "root" : subdir)}: {exception.GetType().Name}";
            return;
        }

        _watches[subdir] = new ArmedWatch(watcher, generation);
    }

    /// <summary>
    /// One raw OS event, normalized. Caller is a threadpool callback.
    /// </summary>
    /// <param name="vanishedPath">For Deleted/Renamed, the path that
    /// went away; null for Created/Changed.</param>
    private void HandleEntryChurn(string subdir, long generation, string? vanishedPath)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            // The ABA rule (SD8): act only if the watch sitting at this
            // key is still the one that fired. A handler queued before a
            // drop-and-re-arm must never touch the freshly armed watch —
            // that is how live detection used to die silently on the mac
            // side before generations existed.
            if (!_watches.TryGetValue(subdir, out ArmedWatch? armed)
                || armed.Generation != generation
                || armed.Retired)
            {
                return;
            }

            // A watched child dir that just went away loses its watch NOW
            // rather than waiting for its own Error to land: a fast
            // delete-then-recreate would otherwise leave a dead handle
            // sitting over a live directory.
            if (vanishedPath is not null)
            {
                RetireWatchAt(vanishedPath);
            }

            Reconcile();
            ScheduleFire();
        }
    }

    /// <summary>SDR-1: <c>InternalBufferOverflow</c> (events were
    /// DROPPED) and "the watched directory itself vanished" both surface
    /// here. Neither is an error surface — the watch is rebuilt and the
    /// gap is repaired by a re-detection, which is exactly what a fire
    /// is.</summary>
    private void HandleWatchError(string subdir, long generation)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            if (!_watches.TryGetValue(subdir, out ArmedWatch? armed)
                || armed.Generation != generation)
            {
                return;
            }

            armed.Retired = true;
            Reconcile();
            ScheduleFire();
        }
    }

    /// <summary>Mark the watch whose directory is <paramref name="path"/>
    /// dead so the following reconcile disposes it and re-arms a fresh
    /// one if the directory returns. Marking rather than removing keeps
    /// the generation in place, so a late handler from the dead watch
    /// still fails the ABA check. Caller holds <see cref="_gate"/>.
    /// </summary>
    private void RetireWatchAt(string path)
    {
        foreach (KeyValuePair<string, ArmedWatch> entry in _watches)
        {
            if (string.Equals(
                    _watchedPaths[entry.Key],
                    Path.TrimEndingDirectorySeparator(path),
                    StringComparison.OrdinalIgnoreCase))
            {
                entry.Value.Retired = true;
            }
        }
    }

    /// <summary>
    /// Coalesce a burst into one fire after <see cref="_debounce"/> of
    /// quiet — but never past the burst's max-latency ceiling.
    ///
    /// The ceiling is the #638 anti-starvation finding: a pure trailing
    /// debounce can be starved forever, because continuous sub-interval
    /// churn (a busy sync tool, a Ctrl+S habit) reschedules the deadline
    /// every time and it never arrives. Anchoring the ceiling on the
    /// burst's FIRST event bounds the wait:
    /// <c>deadline = min(lastEvent + debounce, firstEvent + ceiling)</c>.
    /// Caller holds <see cref="_gate"/>.
    /// </summary>
    private void ScheduleFire()
    {
        TimeSpan now = _clock.Elapsed;
        _burstStart ??= now;
        TimeSpan trailing = now + _debounce;
        TimeSpan ceiling = _burstStart.Value + _maxLatency;
        TimeSpan deadline = trailing < ceiling ? trailing : ceiling;
        TimeSpan delay = deadline - now;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        long ticket = ++_timerTicket;
        _ = Task.Delay(delay).ContinueWith(
            _ => FireIfCurrent(ticket),
            TaskScheduler.Default);
    }

    /// <summary>
    /// The burst deadline arrived. THE GUARANTEE (SDINV-5): the stopped
    /// check and the invocation happen inside ONE acquisition of the
    /// same lock <see cref="Stop"/> contends for, so a fire is either
    /// wholly before Stop takes the lock or never starts — "fire is
    /// never invoked after Stop returns" is structural, not a race we
    /// win most of the time. The price is that <c>fire</c> runs under
    /// the lock, which is why its contract is "enqueue to the UI context
    /// and return": it must not block and must not re-enter the watcher.
    ///
    /// That price is paid on BOTH sides rather than only asserted here:
    /// the owner's callback hands the UI hop to the threadpool instead
    /// of invoking the injected enqueue delegate directly, so a
    /// blocking delegate cannot be wired in by accident and turn this
    /// lock into a vault-close deadlock.
    /// </summary>
    private void FireIfCurrent(long ticket)
    {
        lock (_gate)
        {
            if (_stopped || ticket != _timerTicket)
            {
                return;
            }

            // Retire the ticket and end the burst before invoking, so
            // the next event anchors a fresh ceiling.
            _timerTicket++;
            _burstStart = null;
            try
            {
                _fire();
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                // The owner's callback is a UI-context enqueue that
                // cannot meaningfully fail; letting a throw escape would
                // only surface as an unobserved task exception, and a
                // detection miss must never become a crash (SDINV-2).
            }
        }
    }

    private static void Retire(ArmedWatch armed)
    {
        armed.Retired = true;
        try
        {
            armed.Watcher.Dispose();
        }
        catch (Exception exception) when (
            exception is IOException or ObjectDisposedException)
        {
            // A handle whose directory already vanished can fault on
            // close; the watch is gone either way.
        }
    }

    /// <summary>One armed watch plus the generation stamped when it was
    /// armed.</summary>
    private sealed class ArmedWatch
    {
        public ArmedWatch(FileSystemWatcher watcher, long generation)
        {
            Watcher = watcher;
            Generation = generation;
        }

        public FileSystemWatcher Watcher { get; }

        public long Generation { get; }

        /// <summary>Set when the directory vanished or the OS reported
        /// the handle dead/lossy; the next reconcile disposes it and
        /// re-arms.</summary>
        public bool Retired { get; set; }
    }
}
