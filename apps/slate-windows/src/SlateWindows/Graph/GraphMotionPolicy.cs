// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR D (#746), rule G, Term G4 (DD-9): Reduce Motion is the SYSTEM's
/// animation preference — <see cref="SystemParameters.ClientAreaAnimation"/>
/// false, the "animation effects" setting — read at every settle start and
/// observed through <see cref="SystemParameters.StaticPropertyChanged"/>
/// (the theme manager's channel); no app setting. The document owns one
/// policy for its life; a fact injects its own read and flips it (the
/// mac's <c>motionFlip</c>).
/// </summary>
internal sealed class GraphMotionPolicy : IDisposable
{
    private readonly Func<bool> _reduceMotion;
    private readonly bool _observesSystem;

    /// <summary>A policy over an injected read — a fact's; the system's is
    /// <see cref="OfTheSystem"/>.</summary>
    internal GraphMotionPolicy(Func<bool> reduceMotion)
        : this(reduceMotion, observesSystem: false)
    {
    }

    private GraphMotionPolicy(Func<bool> reduceMotion, bool observesSystem)
    {
        ArgumentNullException.ThrowIfNull(reduceMotion);
        _reduceMotion = reduceMotion;
        _observesSystem = observesSystem;
    }

    /// <summary>The system's policy: the animation-effects preference,
    /// observed until <see cref="Dispose"/> (the document's retirement).</summary>
    internal static GraphMotionPolicy OfTheSystem()
    {
        var policy = new GraphMotionPolicy(static () => !SystemParameters.ClientAreaAnimation, observesSystem: true);
        SystemParameters.StaticPropertyChanged += policy.OnSystemParameterChanged;
        return policy;
    }

    /// <summary>Read at every settle start (Term G4): true converges once
    /// off-dispatcher and applies one frame.</summary>
    internal bool ReduceMotion => _reduceMotion();

    /// <summary>A flip of the preference; the document restarts a settle in
    /// flight on it.</summary>
    internal event Action? Changed;

    /// <summary>A fact's flip: the read changed underneath, the channel fires.</summary>
    internal void Flip() => Changed?.Invoke();

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
        {
            Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        if (_observesSystem)
        {
            SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
        }
    }
}
