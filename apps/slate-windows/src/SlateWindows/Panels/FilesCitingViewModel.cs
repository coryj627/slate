// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-5 (#737): the files-citing sheet, opened from a bibliography
/// row's context menu (mac BibliographyPanel.swift:356-394).
///
/// A lookup failure yields the EMPTY state rather than error copy —
/// mac's posture (AppState.swift:12429-12433). "Which files cite
/// this?" answered with "none found" is honest for a query that can
/// only ever be advisory; an error banner would overstate it.
/// </summary>
internal sealed class FilesCitingViewModel : PanelWorkScheduler
{
    private readonly VaultSession _session;
    private readonly string _key;
    private int _requestId;
    private bool _isLoading;

    public FilesCitingViewModel(
        VaultSession session,
        string key,
        object? returnFocusToken = null,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _key = key;
        ReturnFocusToken = returnFocusToken;
    }

    public ObservableCollection<string> Paths { get; } = [];

    public string Key => _key;

    /// <summary>Identity of the row that opened the sheet, so Escape
    /// returns focus exactly there (contract 11).</summary>
    public object? ReturnFocusToken { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public string Heading => CitationPhrase.FilesCitingHeading;

    public string EmptyText => CitationPhrase.FilesCitingEmpty;

    public bool ShowEmptyState => !_isLoading && Paths.Count == 0;

    public string AutomationName => CitationPhrase.FilesCitingContainerLabel(Paths.Count);

    internal int RequestIdForTests => _requestId;

    public void Load()
    {
        int requestId = Interlocked.Increment(ref _requestId);
        IsLoading = true;
        StartWork(() =>
        {
            string[] paths;
            try
            {
                paths = _session.ListFilesCiting(_key);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                // Empty, not an error (mac parity).
                paths = [];
            }
            Post(() => Publish(requestId, paths));
        });
    }

    internal void Publish(int requestId, string[] paths)
    {
        if (IsShutDown || requestId != _requestId)
        {
            return;
        }
        Paths.Clear();
        foreach (string path in paths)
        {
            Paths.Add(path.Replace('\\', '/'));
        }
        IsLoading = false;
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(AutomationName));
    }
}
