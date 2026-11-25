// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Runtime.InteropServices;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// Owns sidebar import selection, cancellation, bounded traversal, collision
/// handling, source validation, and completion reporting.
/// </summary>
internal sealed partial class FilesSidebarViewModel
{
    internal const int MaxImportEntries = 10_000;
    internal const long MaxImportFileBytes = 256L * 1024 * 1024;
    private readonly Func<Task<IReadOnlyList<string>>> _pickImportSources;
    private readonly Func<Action, CancellationToken, Task> _runImportWorker;
    private readonly string? _vaultRoot;
    private readonly object _importCancellationGate = new();
    private CancellationTokenSource? _importCancellation;
    private Task _importCompletion = Task.CompletedTask;
    private bool _isImporting;
    private bool _isImportRunning;
    private bool _isImportCancelling;

    internal Task ImportCompletion => _importCompletion;

    /// <summary>Plan 47 CB-16 (#1830): called after each file the import copies, on its
    /// worker, so a fact can cancel part way through.</summary>
    internal Action? AfterImportFileCopiedForTesting { get; set; }

    /// <summary>
    /// The import's admission: set at the press and cleared when the import ends, its pick
    /// included. A rescan is refused while it is set (R-9), a second press starts nothing,
    /// and a vault close cancels the import first. Plan 47 CB-17b (#1866, D3): it no longer
    /// turns Import… or the other verbs off; <see cref="IsImportRunning"/> does, once the
    /// pick has returned sources, so Import… is on while its own box is up and the box's
    /// close can hand the keys back to it.
    /// </summary>
    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (SetField(ref _isImporting, value))
            {
                RaiseCommandStates();
            }
        }
    }

    /// <summary>Plan 47 CB-17b (#1866, D3): the import is copying, from the moment its pick
    /// returned sources until it ends. Import…, Cancel import, the status line and the
    /// other verbs read it, as they read <see cref="IsImporting"/> before.</summary>
    public bool IsImportRunning
    {
        get => _isImportRunning;
        private set
        {
            if (SetField(ref _isImportRunning, value))
            {
                OnPropertyChanged(nameof(ImportStatusLine));
                RaiseCommandStates();
            }
        }
    }

    /// <summary>Plan 47 CB-16 (#1830, W3): the running import has been asked to stop. Set
    /// by a cancel, cleared as the next import starts copying and when this one ends, after
    /// <see cref="IsImportRunning"/>, so Cancel import is never on again in between.</summary>
    public bool IsImportCancelling
    {
        get => _isImportCancelling;
        private set
        {
            if (SetField(ref _isImportCancelling, value))
            {
                OnPropertyChanged(nameof(ImportStatusLine));
                RaiseCommandStates();
            }
        }
    }

    /// <summary>Plan 47 CB-16 (#1830, CD10): what the status bar shows while an import runs,
    /// in core's words ("Importing…", then "Cancelling import…" once it is asked to stop).
    /// Empty while no import is copying.</summary>
    public string ImportStatusLine => IsImportRunning
        ? SlateUniffiMethods.ImportStatusLine(IsImportCancelling ? ImportPhase.Cancelling : ImportPhase.Importing)
        : string.Empty;

    /// <summary>Plan 47 CB-16 (#1830; the owner's answer of 2026-11-24): Cancel import's
    /// press, from any route (the status bar, the Files pane, the File menu, the palette,
    /// Escape). It asks the import to stop and says so at once, in core's words; the
    /// summary follows when the import settles. A press that finds nothing left to stop
    /// says nothing.</summary>
    private void RequestImportCancellation()
    {
        if (TryCancelImport())
        {
            _announce(new A11yEvent.ImportCancelling());
        }
    }

    /// <summary>Asks the import to stop without a word: a vault close (which says its own
    /// refusal) and the session's shutdown.</summary>
    public void CancelImport() => _ = TryCancelImport();

    private bool TryCancelImport()
    {
        CancellationTokenSource? cancellation;
        lock (_importCancellationGate)
        {
            cancellation = _importCancellation;
            _importCancellation = null;
        }

        if (cancellation is null)
        {
            return false;
        }

        if (IsImportRunning)
        {
            IsImportCancelling = true;
        }

        try
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception)
            {
                // Cancellation is best-effort during teardown. A callback
                // failure must not prevent later producers from being canceled.
                HostLog.Write(HostDiagnosticEvent.SidebarImportShutdownFailed, exception);
            }
        }
        finally
        {
            cancellation.Dispose();
        }

        return true;
    }

    /// <summary>Import…'s press. Plan 47 CB-17b (#1866, D3): the command admits while it
    /// executes, so Import… stays on through its pick; a press while an import is admitted
    /// starts nothing, and the admitted import's completion stays the one reported.</summary>
    private Task StartImport() => IsImporting ? Task.CompletedTask : _importCompletion = ImportAsync();

    private async Task ImportAsync()
    {
        // W7-7 PR 7 (codex PR 7 round 4, finding 2): never during a rescan.
        if (StructuralMutationBlocked is not null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = cancellation.Token;
        lock (_importCancellationGate)
        {
            if (SessionShutdownStarted)
            {
                cancellation.Dispose();
                return;
            }

            _importCancellation = cancellation;
        }

        IsImporting = true;
        // Plan 47 CB-16 (#1830, W1): where the copy goes, once the pick has chosen sources;
        // null until then, so a cancel before the pick returned reports nothing.
        string? destination = null;
        try
        {
            IReadOnlyList<string> sources;
            try
            {
                sources = await _pickImportSources();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                ReportImportFailure(
                    HostDiagnosticEvent.SidebarImportPickerFailed,
                    exception,
                    "Could not choose import sources.");
                return;
            }

            // An empty pick (the box's Cancel, the dialog's Cancel) is no import: nothing to
            // report, whatever else happened meanwhile (the red team's low 1).
            if (sources.Count == 0 || _vaultRoot is null)
            {
                return;
            }

            destination = SelectedNode?.IsDirectory == true
                ? SelectedNode.Path
                : ParentPath(SelectedNode?.Path);
            // A pick that returned sources after a close or a shutdown asked the import to
            // stop: nothing was copied, and the import says so (below).
            cancellationToken.ThrowIfCancellationRequested();
            string[] acceptedSources = sources.Take(256).ToArray();
            int omittedSources = sources.Count - acceptedSources.Length;
            // D3: Import… goes off here, the keys moved off it first. CB-16 (W3): every
            // import starts copying at Importing, whatever the last one ended in.
            IsImportCancelling = false;
            IsImportRunning = true;
            (bool admitted, ImportResult? result) = await RunImportWorkerAsync(
                acceptedSources,
                destination,
                omittedSources,
                cancellationToken);
            if (!admitted)
            {
                return;
            }

            if (result is null)
            {
                throw new InvalidOperationException("Import worker completed without a result.");
            }

            // Plan 47 CB-16 (#1830, W1): what the worker did is reported, cancelled or not.
            // The worker's own result says whether the copy was cut short: a cancel that
            // came after the last copy reports a full copy.
            ReportImportFinished(result, destination, refresh: true);
        }
        catch (OperationCanceledException)
        {
            // Plan 47 CB-16 (#1830, W1): cancelled after the pick chose sources and before
            // the worker produced a result, so nothing was copied. Said, as a cancel during
            // the copy is; a cancel before the pick returned has nothing to report.
            if (destination is not null)
            {
                ReportImportFinished(
                    new ImportResult(0, 0, 0, Cancelled: true, LimitReached: false),
                    destination,
                    refresh: false);
            }
        }
        catch (Exception exception)
        {
            ReportImportFailure(
                HostDiagnosticEvent.SidebarImportFailed,
                exception,
                "Could not import the selected items.");
        }
        finally
        {
            bool ownsCancellation = false;
            lock (_importCancellationGate)
            {
                if (ReferenceEquals(_importCancellation, cancellation))
                {
                    _importCancellation = null;
                    ownsCancellation = true;
                }
            }

            if (ownsCancellation)
            {
                cancellation.Dispose();
            }

            // Running first: Cancel import reads both, and must not come on in between.
            IsImportRunning = false;
            IsImportCancelling = false;
            IsImporting = false;
        }
    }

    /// <summary>Plan 47 CB-16 (#1830, CD10, W1): the import's outcome, in core's words
    /// (<c>ImportFinished</c>), with <see cref="ReportResult"/>'s discipline: the refresh
    /// first, whose "Loading files…" this line then replaces; the line held for that
    /// refresh's publication; the event announced. Silent while the session shuts down.
    /// </summary>
    private void ReportImportFinished(ImportResult result, string destination, bool refresh)
    {
        if (SessionShutdownStarted)
        {
            return;
        }

        var finished = new A11yEvent.ImportFinished(
            CopiedFiles: (uint)result.ImportedFiles,
            CopiedFolders: (uint)result.ImportedFolders,
            MovedItems: 0,
            ReconciledItems: 0,
            Destination: destination,
            Cancelled: result.Cancelled,
            NotImported: (uint)result.Failed,
            Warnings: 0,
            StoppedAtEntryLimit: result.LimitReached ? (uint)MaxImportEntries : null);
        if (refresh)
        {
            Refresh();
        }

        Status = SlateUniffiMethods.A11yRender(finished).Text;
        HoldStatusForPendingPublication();
        _announce(finished);
    }

    private async Task<(bool Admitted, ImportResult? Result)> RunImportWorkerAsync(
        string[] acceptedSources,
        string destination,
        int omittedSources,
        CancellationToken cancellationToken)
    {
        if (!TryBeginSessionWork(out SessionWorkLease? lease))
        {
            return (false, null);
        }

        ImportResult? result = null;
        using (lease)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _runImportWorker(
                () => result = ImportSources(
                    acceptedSources,
                    destination,
                    cancellationToken,
                    omittedSources),
                cancellationToken).ConfigureAwait(false);
        }

        return (true, result);
    }

    private void ReportImportFailure(
        HostDiagnosticEvent diagnosticEvent,
        Exception exception,
        string message)
    {
        HostLog.Write(diagnosticEvent, exception);
        try
        {
            ReportFailure(message);
        }
        catch (Exception callbackException)
        {
            HostLog.Write(diagnosticEvent, callbackException);
        }
    }

    private ImportResult ImportSources(
        IEnumerable<string> sources,
        string destination,
        CancellationToken cancellationToken,
        int initialFailures = 0)
    {
        int importedFiles = 0;
        int importedFolders = 0;
        int failed = initialFailures;
        int visitedEntries = 0;
        bool limitReached = false;
        foreach (string source in sources)
        {
            if (!TryReserveImportEntry(ref visitedEntries))
            {
                limitReached = true;
                break;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string absolute = Path.GetFullPath(source);
                if (IsInsideVault(absolute)
                    || HasReparsePointInPath(absolute))
                {
                    failed++;
                    continue;
                }

                if (File.Exists(absolute))
                {
                    ImportFile(absolute, destination, cancellationToken);
                    importedFiles++;
                    AfterImportFileCopiedForTesting?.Invoke();
                }
                else if (Directory.Exists(absolute))
                {
                    string importedRoot = CreateUniqueFolder(
                        destination,
                        new DirectoryInfo(absolute).Name);
                    importedFolders++;
                    var pending = new Stack<(string Source, string Destination)>();
                    pending.Push((absolute, importedRoot));
                    while (pending.Count > 0 && !limitReached)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        (string sourceDirectory, string destinationDirectory) = pending.Pop();
                        foreach (string child in Directory.EnumerateFileSystemEntries(sourceDirectory))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!TryReserveImportEntry(ref visitedEntries))
                            {
                                limitReached = true;
                                pending.Clear();
                                break;
                            }

                            var (childAttributes, childTag) = InspectEntry(child);
                            if (IsLinkEntry(childAttributes, childTag))
                            {
                                failed++;
                                continue;
                            }

                            if (Directory.Exists(child))
                            {
                                string childDestination = CombineVaultPath(
                                    destinationDirectory,
                                    Path.GetFileName(child));
                                _session.CreateFolderExclusive(childDestination);
                                pending.Push((child, childDestination));
                                importedFolders++;
                            }
                            else
                            {
                                ImportFile(child, destinationDirectory, cancellationToken);
                                importedFiles++;
                                AfterImportFileCopiedForTesting?.Invoke();
                            }
                        }
                    }
                }
                else
                {
                    failed++;
                }
            }
            catch (OperationCanceledException)
            {
                return new ImportResult(
                    importedFiles,
                    importedFolders,
                    failed,
                    Cancelled: true,
                    limitReached);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException
                    or System.Security.SecurityException
                    or VaultException)
            {
                failed++;
            }
        }

        return new ImportResult(
            importedFiles,
            importedFolders,
            failed,
            Cancelled: false,
            limitReached);
    }

    private void ImportFile(string source, string destination, CancellationToken cancellationToken)
    {
        byte[] bytes = SafeFile.ReadAllBytesBounded(
            source,
            MaxImportFileBytes,
            cancellationToken: cancellationToken);
        string originalName = Path.GetFileName(source);
        for (int suffix = 1; suffix <= 100; suffix++)
        {
            string name = suffix == 1 ? originalName : CopyName(originalName, suffix);
            try
            {
                _session.CreateExclusiveBytes(CombineVaultPath(destination, name), bytes);
                return;
            }
            catch (VaultException.DestinationExists) when (suffix < 100)
            {
            }
        }
    }

    private string CreateUniqueFolder(string destination, string originalName)
    {
        for (int suffix = 1; suffix <= 100; suffix++)
        {
            string name = suffix == 1 ? originalName : $"{originalName} {suffix}";
            string path = CombineVaultPath(destination, name);
            try
            {
                _session.CreateFolderExclusive(path);
                return path;
            }
            catch (VaultException.DestinationExists) when (suffix < 100)
            {
            }
        }

        throw new IOException($"Could not reserve a name for {originalName}.");
    }

    private bool IsInsideVault(string absolutePath) =>
        VaultPaths.IsInside(_vaultRoot!, absolutePath);

    internal static bool TryReserveImportEntry(ref int visitedEntries)
    {
        if (visitedEntries >= MaxImportEntries)
        {
            return false;
        }

        visitedEntries++;
        return true;
    }

    /// <summary>
    /// Whether the import source <paramref name="absolutePath"/> or a folder
    /// above it is a link: a reparse point whose tag is a name surrogate (a
    /// junction, a symbolic link), or one whose tag cannot be read. A Cloud
    /// Files placeholder (a OneDrive Files On-Demand file or folder, which
    /// includes a Desktop or Documents that OneDrive backs up) redirects
    /// nothing and is not one (plan 44 WB-24, #1492; core's walks draw the
    /// same line, #1491).
    /// </summary>
    internal static bool HasReparsePointInPath(
        string absolutePath,
        Func<string, (FileAttributes Attributes, uint? ReparseTag)>? inspect = null)
    {
        inspect ??= InspectEntry;
        string current = Path.GetFullPath(absolutePath);
        while (true)
        {
            var (attributes, tag) = inspect(current);
            if (IsLinkEntry(attributes, tag))
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            current = parent;
        }
    }

    /// <summary>
    /// Whether an entry with <paramref name="attributes"/> and reparse
    /// <paramref name="tag"/> is a link the import refuses: a reparse point
    /// whose tag is a name surrogate or could not be read. A Cloud Files
    /// placeholder (the bit clear) is the file or folder it stands for. The
    /// source's ancestors and every entry inside an imported folder take this
    /// rule (plan 44 WB-24, #1492).
    /// </summary>
    internal static bool IsLinkEntry(FileAttributes attributes, uint? tag) =>
        (attributes & FileAttributes.ReparsePoint) != 0
        && (tag is not { } reparseTag || (reparseTag & NameSurrogateBit) != 0);

    /// <summary>The name-surrogate bit of a reparse tag (MS-FSCC 2.1.2.1).</summary>
    private const uint NameSurrogateBit = 0x2000_0000;

    /// <summary>
    /// The entry's attributes and, for a reparse point, its tag as
    /// <c>FindFirstFileW</c> reports it in <c>dwReserved0</c> (the entry
    /// itself, never what it points at); <c>null</c> when it cannot be read.
    /// </summary>
    private static (FileAttributes Attributes, uint? ReparseTag) InspectEntry(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) == 0)
        {
            return (attributes, null);
        }

        IntPtr find = FindFirstFileW(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), out FindData data);
        if (find == InvalidHandle)
        {
            return (attributes, null);
        }

        FindClose(find);
        return ((data.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0
            ? (attributes, data.Reserved0)
            : (attributes, null));
    }

    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FindData
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint Reserved0;
        public uint Reserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string FileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string AlternateFileName;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstFileW(string fileName, out FindData data);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr find);

    private static string CopyName(string name, int suffix)
    {
        string extension = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        return $"{stem} {suffix}{extension}";
    }

    private sealed record ImportResult(
        int ImportedFiles,
        int ImportedFolders,
        int Failed,
        bool Cancelled,
        bool LimitReached);
}
