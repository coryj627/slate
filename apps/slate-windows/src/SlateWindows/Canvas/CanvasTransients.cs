// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// §F TF-2: the mode transient — UI-held hypothetical geometry with an
/// IDENTITY (F1). The identity is the <see cref="CanvasLoaded"/> the
/// mode entered against — the LOADED reference, the same one every §E
/// operation carries (IF-1's correction: a selection intent publishes a
/// fresh publication but keeps the loaded triple, so navigation during
/// connect mode survives; a reload installs a different reference and
/// F1a cancels). Ids are in READING ORDER (a rigid unit for sets), the
/// originals are what cancel restores, the hypotheticals are what
/// commit writes; the outline and table never see any of it.
/// </summary>
internal sealed class CanvasTransientHolder
{
    private CanvasTransientHolder(
        CanvasLoaded identity,
        ImmutableArray<string> ids,
        ImmutableDictionary<string, CanvasRect> originals,
        bool isResize,
        bool wasOverlapping)
    {
        Identity = identity;
        Ids = ids;
        Originals = originals;
        Rects = originals;
        IsResize = isResize;
        WasOverlapping = wasOverlapping;
    }

    /// <summary>The loaded triple the mode entered against (F1).</summary>
    internal CanvasLoaded Identity { get; }

    /// <summary>The moving ids, reading-ordered (`CanvasOrderNodes`).</summary>
    internal ImmutableArray<string> Ids { get; }

    /// <summary>Entry geometry — what Esc restores, byte-exactly.</summary>
    internal ImmutableDictionary<string, CanvasRect> Originals { get; }

    /// <summary>The hypothetical geometry — what Return writes.</summary>
    internal ImmutableDictionary<string, CanvasRect> Rects { get; set; }

    internal bool IsResize { get; }

    /// <summary>The overlap two-state machine's entry state (F2): a
    /// transition speaks on onset and clearing, never per step.</summary>
    internal bool WasOverlapping { get; set; }

    /// <summary>
    /// The never-silent entry capture (F2a/IF-3): ordering, geometry
    /// and the entry overlap are read under the lease inside ONE try —
    /// any refusal or throw builds NOTHING and the caller speaks the
    /// FD-6 arm. A set member without scene geometry refuses the same
    /// way a vanished card would: gone is gone, null comes back.
    /// </summary>
    internal static CanvasTransientHolder? TryCapture(
        VaultSession session,
        CanvasLoaded loaded,
        IReadOnlyList<string> members,
        bool isResize)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0)
        {
            return null;
        }
        CanvasTransientHolder? built = null;
        try
        {
            bool ran = loaded.Lease.Invoke(
                () => true,
                handle =>
                {
                    string[] ordered = session.CanvasOrderNodes(
                        handle, [.. members]);
                    if (ordered.Length != members.Count)
                    {
                        return;
                    }
                    var originals =
                        ImmutableDictionary.CreateBuilder<string, CanvasRect>(
                            StringComparer.Ordinal);
                    foreach (string id in ordered)
                    {
                        CanvasSceneNode? node = loaded.Population.SceneByNode
                            .GetValueOrDefault(id);
                        if (node is null)
                        {
                            return;
                        }
                        originals[id] = new CanvasRect(
                            node.X, node.Y, node.Width, node.Height);
                    }
                    bool overlapping = false;
                    foreach (string id in ordered)
                    {
                        if (session.CanvasCheckOverlap(
                            handle, originals[id], [.. ordered]).Length > 0)
                        {
                            overlapping = true;
                            break;
                        }
                    }
                    built = new CanvasTransientHolder(
                        loaded,
                        [.. ordered],
                        originals.ToImmutable(),
                        isResize,
                        overlapping);
                });
            return ran ? built : null;
        }
        catch (VaultException)
        {
            return null;
        }
    }
}
