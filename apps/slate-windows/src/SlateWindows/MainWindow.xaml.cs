// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W0-2 "hello, core" window (w0_spec §W0-2 item 1): opens a vault through
// the W0-1 uniffi binding and prints scan progress. Deliberately the whole
// UI — the real shell arrives with W1-1.

using System.Windows;
using Microsoft.Win32;
using uniffi.slate_uniffi;

namespace SlateWindows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenVault_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Open vault folder" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        string root = dialog.FolderName;
        OpenVaultButton.IsEnabled = false;
        ProgressList.Items.Clear();
        StatusText.Text = $"Scanning {root}…";
        try
        {
            // InvokeAsync: progress callbacks arrive on the scanner's Rust
            // thread — a blocking Invoke would stall the scan behind UI work.
            var listener = new UiProgressListener(line => Dispatcher.InvokeAsync(() => AppendLine(line)));
            ScanReport report = await Task.Run(() =>
            {
                using var session = VaultSession.OpenFilesystem(root);
                using var token = new CancelToken();
                return session.ScanInitialWithProgress(token, listener);
            });
            StatusText.Text = $"Scan finished: {report.FilesIndexed} files indexed.";
        }
        catch (VaultException ex)
        {
            StatusText.Text = $"Vault error ({ex.GetType().Name}): {ex.Message}";
        }
        catch (Exception ex)
        {
            // async void handler: anything escaping here would take down the
            // process via the WPF dispatcher — surface it instead.
            StatusText.Text = $"Unexpected error ({ex.GetType().Name}): {ex.Message}";
        }
        finally
        {
            OpenVaultButton.IsEnabled = true;
        }
    }

    private void AppendLine(string line)
    {
        ProgressList.Items.Add(line);
        ProgressList.ScrollIntoView(line);
    }
}

/// <summary>
/// Foreign <see cref="ScanProgressListener"/> that renders each event as a
/// display line. Callbacks arrive on the scanner's Rust thread; the caller
/// supplies the UI-thread marshalling.
/// </summary>
internal sealed class UiProgressListener : ScanProgressListener
{
    private readonly Action<string> _emit;

    public UiProgressListener(Action<string> emit)
    {
        _emit = emit;
    }

    public void OnProgress(ScanProgress @event)
    {
        string line = @event switch
        {
            ScanProgress.Started s => $"Scan started: {s.TotalFiles} files.",
            ScanProgress.FileIndexed f => $"Indexed {f.Indexed}/{f.Total}: {f.Path}",
            ScanProgress.Finished => "Scan finished.",
            ScanProgress.Cancelled => "Scan cancelled.",
            ScanProgress.Failed => "Scan failed.",
            _ => @event.ToString() ?? "(unknown scan event)",
        };
        _emit(line);
    }
}
