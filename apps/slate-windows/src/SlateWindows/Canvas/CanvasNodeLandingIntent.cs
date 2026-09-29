// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 8 (R-10, OD-12's one entry): a canvas jump's ask to land the
/// editor on <paramref name="NodeId"/> of <paramref name="Document"/>, for
/// <paramref name="Tab"/> — the marks list's Enter (IG-39). Carries no request:
/// the shell's one landing entry validates the tab against the active group
/// and the modal state, holds the landing, and only then raises the
/// document's addressed request.
/// </summary>
internal sealed record CanvasNodeLandingIntent(
    WorkspaceTabViewModel Tab,
    CanvasDocumentViewModel Document,
    string NodeId);
