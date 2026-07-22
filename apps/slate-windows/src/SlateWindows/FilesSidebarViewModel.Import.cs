// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
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

    internal Task ImportCompletion => _importCompletion;

    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (SetField(ref _isImporting, value))
            {
                OnPropertyChanged(nameof(ImportStatus));
                RaiseCommandStates();
            }
        }
    }

    public string ImportStatus => IsImporting ? "Import in progress" : "Import is idle";

    public void CancelImport()
    {
        CancellationTokenSource? cancellation;
        lock (_importCancellationGate)
        {
            cancellation = _importCancellation;
            _importCancellation = null;
        }

        if (cancellation is null)
        {
            return;
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
    }

    private async Task ImportAsync()
    {
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

            cancellationToken.ThrowIfCancellationRequested();

            if (sources.Count == 0 || _vaultRoot is null)
            {
                return;
            }

            string destination = SelectedNode?.IsDirectory == true
                ? SelectedNode.Path
                : ParentPath(SelectedNode?.Path);
            string[] acceptedSources = sources.Take(256).ToArray();
            int omittedSources = sources.Count - acceptedSources.Length;
            (bool admitted, ImportResult? result) = await RunImportWorkerAsync(
                acceptedSources,
                destination,
                omittedSources,
                cancellationToken);
            if (!admitted)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (result is null)
            {
                throw new InvalidOperationException("Import worker completed without a result.");
            }

            Status = ImportSummary(result, destination);
            // W0.5-3 residue: Windows import-engine completion copy.
            _announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
            Refresh();
        }
        catch (OperationCanceledException)
        {
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

            IsImporting = false;
        }
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

                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
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

    private bool IsInsideVault(string absolutePath)
    {
        string root = Path.GetFullPath(_vaultRoot!).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(absolutePath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                candidate.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryReserveImportEntry(ref int visitedEntries)
    {
        if (visitedEntries >= MaxImportEntries)
        {
            return false;
        }

        visitedEntries++;
        return true;
    }

    internal static bool HasReparsePointInPath(
        string absolutePath,
        Func<string, FileAttributes>? getAttributes = null)
    {
        getAttributes ??= File.GetAttributes;
        string current = Path.GetFullPath(absolutePath);
        while (true)
        {
            if ((getAttributes(current) & FileAttributes.ReparsePoint) != 0)
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

    private static string CopyName(string name, int suffix)
    {
        string extension = Path.GetExtension(name);
        string stem = Path.GetFileNameWithoutExtension(name);
        return $"{stem} {suffix}{extension}";
    }

    private static string ImportSummary(ImportResult result, string destination)
    {
        var copied = new List<string>();
        if (result.ImportedFiles > 0)
        {
            copied.Add($"{result.ImportedFiles:N0} {(result.ImportedFiles == 1 ? "file" : "files")}");
        }

        if (result.ImportedFolders > 0)
        {
            copied.Add($"{result.ImportedFolders:N0} {(result.ImportedFolders == 1 ? "folder" : "folders")}");
        }

        string location = string.IsNullOrEmpty(destination)
            ? "the vault root"
            : Path.GetFileName(destination.TrimEnd('/'));
        string message = copied.Count == 0
            ? $"No items were imported to {location}."
            : $"Copied {string.Join(" and ", copied)} to {location}.";
        if (result.Cancelled)
        {
            message += " Import cancelled; completed copies remain.";
        }
        else if (result.Failed > 0)
        {
            message += $" {result.Failed:N0} "
                + (result.Failed == 1 ? "item was" : "items were")
                + " not imported.";
        }

        if (result.LimitReached)
        {
            message += " Import stopped at the 10,000-item safety limit; remaining entries were not enumerated.";
        }

        return message;
    }

    private sealed record ImportResult(
        int ImportedFiles,
        int ImportedFolders,
        int Failed,
        bool Cancelled,
        bool LimitReached);
}
