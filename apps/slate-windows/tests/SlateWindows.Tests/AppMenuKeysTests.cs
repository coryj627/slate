// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>DS-13 (#1814): which menu under the logo Alt and a letter opens. A hosted
/// <see cref="KeyEventArgs"/> can't carry <c>SystemKey</c>, and a hosted popup can't take
/// the keys, so the rule is pure here and the keys themselves are the gate's.</summary>
public sealed class AppMenuKeysTests
{
    [Theory]
    [InlineData(Key.F, "FileMenu")]
    [InlineData(Key.W, "WorkspaceMenu")]
    [InlineData(Key.B, "BaseMenu")]
    [InlineData(Key.E, "EditorMenu")]
    [InlineData(Key.N, "CanvasMenu")]
    [InlineData(Key.G, "GraphMenu")]
    public void AppMenuKeysDecide(Key letter, string menu)
    {
        Assert.Equal(menu, AppMenuKeys.Decide(letter, modalOpen: false, liveAccessKey: () => false));

        // Nothing under a modal surface, and nothing when a live control holds the letter
        // (the find bar's Alt+N and Alt+W).
        Assert.Null(AppMenuKeys.Decide(letter, modalOpen: true, liveAccessKey: () => false));
        Assert.Null(AppMenuKeys.Decide(letter, modalOpen: false, liveAccessKey: () => true));
    }

    /// <summary>Only the letters the design lists: H waits for Help (#756), and the rest
    /// stay WPF's. Nor is any other key asked about as an access key: one is a single
    /// character, and asking about Alt's own key down ("LeftAlt") threw inside the window's
    /// key handler, after which the app took no more keys.</summary>
    [Theory]
    [InlineData(Key.H)]
    [InlineData(Key.A)]
    [InlineData(Key.F10)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.Space)]
    public void OtherKeysAreLeftAlone(Key key) =>
        Assert.Null(AppMenuKeys.Decide(key, modalOpen: false, liveAccessKey: () => throw new InvalidOperationException($"{key} was asked about as an access key.")));
}
