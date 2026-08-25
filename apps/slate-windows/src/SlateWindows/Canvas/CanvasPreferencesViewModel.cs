// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>
/// W6-1 PR C (#745), contract C13: the canvas announcement verbosity
/// (t0 §1.2) — app-level, persisted, and LIVE-switchable.
/// </summary>
/// <remarks>
/// <para>
/// Core holds no "current verbosity": it is a PARAMETER on the two
/// families whose template varies by it (0a-5), and the host supplies it
/// at every announce. This object is that supply, and
/// <c>CanvasDocumentViewModel.Verbosity</c> reads it through a delegate
/// rather than caching a copy — which is the whole of what "live" means
/// here: a change takes effect on the next announcement of every open
/// canvas, with nothing to push.
/// </para>
/// <para>
/// The setting announces NOTHING of its own. The three menu items are
/// checkable radio items, so a screen reader speaks the selected state
/// from the element itself (t0 §3's inspectability, and the shape mac's
/// Settings toggle has), and the honest confirmation of "you are now at
/// Verbose" is the next card you move to. Inventing a canvas event to
/// say it would put a string in the canonical corpus that mac never
/// speaks (0a-1's rule), and composing one here would be exactly the
/// host prose R-C forbids.
/// </para>
/// </remarks>
internal sealed class CanvasPreferencesViewModel : BindableBase
{
    /// <summary>
    /// The storage keys, spelled as mac's <c>CanvasVerbosity</c> cases so
    /// a future shared schema needs no VALUE migration (contract C13).
    /// The stores themselves are peers, not one file — mac's is a
    /// UserDefaults key, this one is the device-local preferences JSON.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, CanvasVerbosity> VerbosityKeys =
        new Dictionary<string, CanvasVerbosity>(StringComparer.Ordinal)
        {
            ["terse"] = CanvasVerbosity.Terse,
            ["standard"] = CanvasVerbosity.Standard,
            ["verbose"] = CanvasVerbosity.Verbose,
        };

    private readonly AppPreferencesStore? _store;
    private string _verbosityKey = "standard";

    public CanvasPreferencesViewModel(AppPreferencesStore? store = null)
    {
        _store = store;
        // No store (the tab-owned instances tests build) = the same
        // default a fresh install decodes to. An unknown or version-skewed
        // key falls to the default like every other field rather than
        // throwing out of workspace initialization.
        AppPreferencesState? loaded = store?.Load();
        if (loaded?.CanvasVerbosity is { } stored && VerbosityKeys.ContainsKey(stored))
        {
            _verbosityKey = stored;
        }
        SetVerbosityCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is string key && VerbosityKeys.ContainsKey(key))
                {
                    VerbosityKey = key;
                }
            },
            _ => true);
    }

    /// <summary>The value every canvas announcement is rendered at
    /// (t0 §1.2); <c>Standard</c> by default.</summary>
    public CanvasVerbosity Verbosity => VerbosityKeys[_verbosityKey];

    public bool IsVerbosityTerse => _verbosityKey == "terse";

    public bool IsVerbosityStandard => _verbosityKey == "standard";

    public bool IsVerbosityVerbose => _verbosityKey == "verbose";

    /// <summary>
    /// The parameterized setter the three menu items bind, each passing
    /// the value it selects — the math-verbosity precedent
    /// (<c>SetMathVerbosityCommand</c>), including its consequence: a
    /// parameterized command is not palette-reachable, which is why the
    /// three <c>windows.canvas.setVerbosity*</c> rows are unregistered
    /// dispositions rather than commands.
    /// </summary>
    public ICommand SetVerbosityCommand { get; }

    private string VerbosityKey
    {
        get => _verbosityKey;
        set
        {
            if (string.Equals(_verbosityKey, value, StringComparison.Ordinal))
            {
                return;
            }
            _verbosityKey = value;
            OnPropertyChanged(nameof(Verbosity));
            OnPropertyChanged(nameof(IsVerbosityTerse));
            OnPropertyChanged(nameof(IsVerbosityStandard));
            OnPropertyChanged(nameof(IsVerbosityVerbose));
            if (_store is { } store)
            {
                store.Save(store.Load() with { CanvasVerbosity = value });
            }
        }
    }
}
