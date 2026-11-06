// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal sealed partial class VaultLifecycleViewModel
{
    /// <summary>DS-12 (#1813): the window's title, from core (DSD9): the active tab's title,
    /// the vault's name and the app's, as <c>Note — Vault — Slate</c>. A screen reader says it
    /// on the window and Alt+Tab shows it, so it isn't composed here.</summary>
    public string WindowTitle => SlateUniffiMethods.WindowTitle(Workspace?.ActiveTitle, VaultDisplayName);

    private void ObserveWorkspaceTitle(WorkspaceViewModel? previous, WorkspaceViewModel? next)
    {
        if (previous is not null)
        {
            previous.PropertyChanged -= Workspace_ActiveTitleChanged;
        }

        if (next is not null)
        {
            next.PropertyChanged += Workspace_ActiveTitleChanged;
        }

        OnPropertyChanged(nameof(WindowTitle));
    }

    private void Workspace_ActiveTitleChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkspaceViewModel.ActiveTitle))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }
}
