// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-28 PR 2: the tests may build a vault store with no session, by
/// the root's path (<see cref="VaultRootAnchor.ByPathForTests"/>); the app
/// never may, so only this assembly turns it on, before any test runs.
/// </summary>
internal static class TestRootAnchors
{
    [ModuleInitializer]
    internal static void AllowByPathRoots() => VaultRootAnchor.ByPathAllowed = true;
}
