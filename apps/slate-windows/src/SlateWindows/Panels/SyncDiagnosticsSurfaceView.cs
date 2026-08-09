// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-8 (#740): the sync-diagnostics leaf body — the mac
/// SyncDiagnosticsPanel twin. Five mutually-exclusive states (SD2)
/// and, when populated, the SD3 order: count header + Refresh, the
/// multi-sync warning FIRST, one row per provider (each followed by
/// its own Evidence expander), then the LiveSync configuration
/// section. Every sentence is core's verbatim (SD1) or a SyncPhrase
/// golden (SD10).
///
/// UIA delivery rules: composed row names ride
/// <see cref="AutomationNamedRowBorder"/> — a name on a plain
/// Border/StackPanel creates no peer and is silently DROPPED — and
/// their visual text rides <see cref="AutomationPresentationTextBlock"/>
/// so each row is ONE accessible stop. Standalone report lines are
/// focusable TextBlocks (the W4-5 notice convention) so a keyboard
/// user can reach every sentence.
/// </summary>
internal sealed class SyncDiagnosticsSurfaceView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(SyncDiagnosticsViewModel),
            typeof(SyncDiagnosticsSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    private readonly StackPanel _content;

    /// <summary>Which providers' evidence disclosures are OPEN, keyed
    /// by core's provider kind — the HistorySurfaceView
    /// <c>_collapsedGroupIds</c> idiom. Every publish rebuilds the leaf
    /// wholesale, so without this the disclosure a reader just opened
    /// snaps shut 2.5 s after any entry churn at the vault root. Mac
    /// gets it for free: <c>ForEach(..., id: \.kind)</c> gives the
    /// DisclosureGroup a stable identity, so a republish leaves it
    /// open.</summary>
    private readonly HashSet<SyncProviderKind> _expandedEvidence = [];

    // The last state actually RENDERED. A watcher fire is entry churn,
    // not a content change — republishing an identical report must not
    // rebuild the leaf (see Render).
    private bool _hasRendered;
    private SyncDetectionReport? _renderedReport;
    private LiveSyncConfigStatus? _renderedConfig;
    private string? _renderedError;

    public SyncDiagnosticsSurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "SyncDiagnosticsSurface");
        _content = new StackPanel();
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _content,
        };
        var layout = new DockPanel();
        layout.Children.Add(scroll);
        Content = layout;
    }

    /// <summary>PUBLIC, not internal: the leaf body's
    /// Model="{Binding SyncDiagnostics}" resolves through WPF
    /// reflection, which only sees public properties — an internal
    /// property fails SILENTLY and the surface renders nothing (the
    /// recorded W4-4 lesson).</summary>
    public SyncDiagnosticsViewModel? Model
    {
        get => (SyncDiagnosticsViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>ONE render trigger, deliberately. <c>Published</c> is
    /// the VM's declared republish signal and it is raised from the
    /// only place the VM's published state changes, so a second
    /// <c>PropertyChanged</c> subscription bought nothing and cost two
    /// extra teardown+rebuild passes per probe — each one a fresh UIA
    /// focus-changed event that makes a screen reader re-read the whole
    /// composed row.</summary>
    private static void OnModelChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (SyncDiagnosticsSurfaceView)d;
        if (e.OldValue is SyncDiagnosticsViewModel oldModel)
        {
            oldModel.Published -= view.OnPublished;
        }
        if (e.NewValue is SyncDiagnosticsViewModel model)
        {
            model.Published += view.OnPublished;
            // A different document is a different vault: neither the
            // render snapshot nor the disclosure state carries over.
            view.ResetRenderState();
            view.Render();
        }
    }

    private void OnPublished(object? sender, EventArgs e) => Render();

    // --- Rendering ---

    /// <summary>How many times the leaf was actually REBUILT. Test seam
    /// for the "one publish, one render pass" and "an unchanged
    /// republish does not rebuild" facts — element identity alone
    /// cannot tell a skipped render from a rebuilt-identical one.
    /// </summary>
    internal int RenderCountForTests { get; private set; }

    private void ResetRenderState()
    {
        _hasRendered = false;
        _renderedReport = null;
        _renderedConfig = null;
        _renderedError = null;
        _expandedEvidence.Clear();
    }

    private void Render()
    {
        if (Model is not { } model)
        {
            _content.Children.Clear();
            ResetRenderState();
            return;
        }
        if (RendersTheSameLeaf(model))
        {
            // Nothing a reader can perceive changed. Ordinary entry
            // churn at the vault root fires the watcher whether or not
            // the REPORT changed, and rebuilding here would hand focus
            // to a fresh element every debounce interval — UIA raises
            // focus-changed and the reader re-reads the entire composed
            // row. Mac's republish is a SwiftUI diff and says nothing;
            // this is the same silence.
            return;
        }
        _renderedReport = model.Report;
        _renderedConfig = model.LiveSyncConfig;
        _renderedError = model.LoadError;
        _hasRendered = true;
        RenderCountForTests++;
        // The leaf re-renders only on a load publish (SDINV-7's three
        // triggers), never per keystroke — but a watcher fire is
        // EXTERNAL, so a rebuild can land under a user who is reading
        // a provider row. Capture and restore by automation identity
        // so focus never falls out of the panel.
        string? focusId = CaptureFocusId();
        _content.Children.Clear();
        switch (model.State)
        {
            case SyncDiagnosticsState.Unsupported:
                // Core's pre-rendered sentence, relayed (SDD-2): mac
                // duplicates it in Swift, Windows renders the report's
                // own AudioSummary — byte-identical output.
                _content.Children.Add(FocusableLine(
                    model.Report?.AudioSummary ?? string.Empty,
                    "SyncDiagnosticsUnsupported"));
                break;
            case SyncDiagnosticsState.Error:
                RenderError(model.LoadError ?? string.Empty);
                break;
            case SyncDiagnosticsState.Loading:
                _content.Children.Add(FocusableLine(
                    SyncPhrase.Loading, "SyncDiagnosticsLoading"));
                break;
            case SyncDiagnosticsState.Empty:
                _content.Children.Add(FocusableLine(
                    model.Report?.AudioSummary ?? string.Empty,
                    "SyncDiagnosticsEmpty"));
                break;
            default:
                RenderPopulated(model, model.Report!);
                break;
        }
        RestoreFocusId(focusId);
    }

    /// <summary>Would a rebuild produce a leaf a reader could tell
    /// apart? Report IDENTITY is not the question: every probe
    /// allocates a fresh record whose <c>Providers</c>/
    /// <c>EvidencePaths</c> arrays compare by reference, so an
    /// unchanged vault yields a brand-new, equal report on every
    /// watcher fire. This walks the perceivable fields instead — the
    /// state inputs (supported, error), every rendered sentence
    /// (summary, warning, recommendation, evidence), and every
    /// provider's kind, name and risk level.</summary>
    private bool RendersTheSameLeaf(SyncDiagnosticsViewModel model) =>
        _hasRendered
        && string.Equals(_renderedError, model.LoadError, StringComparison.Ordinal)
        && Equals(_renderedConfig, model.LiveSyncConfig)
        && SameReport(_renderedReport, model.Report);

    private static bool SameReport(
        SyncDetectionReport? left, SyncDetectionReport? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null
            || right is null
            || left.Supported != right.Supported
            || !string.Equals(
                left.AudioSummary, right.AudioSummary, StringComparison.Ordinal)
            || !string.Equals(
                left.MultiSyncWarning, right.MultiSyncWarning, StringComparison.Ordinal)
            || left.Providers.Length != right.Providers.Length)
        {
            return false;
        }
        for (int index = 0; index < left.Providers.Length; index++)
        {
            if (!SameProvider(left.Providers[index], right.Providers[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameProvider(
        DetectedSyncProvider left, DetectedSyncProvider right) =>
        left.Kind == right.Kind
        && left.RiskLevel == right.RiskLevel
        && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
        && string.Equals(
            left.Recommendation, right.Recommendation, StringComparison.Ordinal)
        && left.EvidencePaths.SequenceEqual(right.EvidencePaths, StringComparer.Ordinal);

    private void RenderError(string message)
    {
        _content.Children.Add(FocusableLine(
            SyncPhrase.LoadError(message), "SyncDiagnosticsError"));
        var retry = new Button
        {
            Content = SyncPhrase.Retry,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(10, 2, 10, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetAutomationId(retry, "SyncDiagnosticsRetry");
        retry.Click += (_, _) => RequestRefresh();
        _content.Children.Add(retry);
    }

    private void RenderPopulated(
        SyncDiagnosticsViewModel model, SyncDetectionReport report)
    {
        _content.Children.Add(BuildHeader(report.Providers.Length));
        // The multi-sync warning is the single most consequential line
        // in the report — FIRST, before any provider row (SD3(2)).
        if (report.MultiSyncWarning is { } warning)
        {
            _content.Children.Add(BuildWarningRow(warning));
        }
        foreach (DetectedSyncProvider provider in report.Providers)
        {
            _content.Children.Add(BuildProviderRow(provider));
            _content.Children.Add(BuildEvidence(provider));
        }
        // The config section renders only when the LiveSync provider
        // is in the report AND the config read returned (SD3(4)).
        bool hasLiveSync = report.Providers.Any(
            candidate => candidate.Kind == SyncProviderKind.LiveSync);
        if (hasLiveSync && model.LiveSyncConfig is { } status)
        {
            _content.Children.Add(BuildLiveSyncSection(status));
        }
    }

    private FrameworkElement BuildHeader(int providerCount)
    {
        var header = new TextBlock
        {
            Text = SyncPhrase.CountHeader(providerCount),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetHeadingLevel(header, AutomationHeadingLevel.Level3);
        AutomationProperties.SetAutomationId(header, "SyncDiagnosticsHeader");
        var refresh = new Button
        {
            // WCAG 2.5.3 label-in-name: the VISIBLE "Refresh" is a
            // contiguous prefix of the accessible name, so a Voice
            // Access "click Refresh" still matches.
            Content = SyncPhrase.Refresh,
            Margin = new Thickness(12, 0, 0, 0),
            Padding = new Thickness(10, 2, 10, 2),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(refresh, SyncPhrase.RefreshAccessibleName);
        AutomationProperties.SetAutomationId(refresh, "SyncDiagnosticsRefresh");
        refresh.Click += (_, _) => RequestRefresh();
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8),
        };
        row.Children.Add(header);
        row.Children.Add(refresh);
        return row;
    }

    /// <summary>Both the Refresh button and the error state's Retry
    /// route through the workspace's refresh funnel (SD7 — the mac
    /// wires both to <c>refreshSyncDiagnostics()</c>), so the
    /// disposal guard lives in exactly one place. The direct fallback
    /// keeps the surface usable when no coordinator installed the
    /// seam; it is the same operation minus that guard.</summary>
    private void RequestRefresh()
    {
        if (Model is not { } model)
        {
            return;
        }
        if (model.RefreshFromSurface is { } seam)
        {
            seam();
            return;
        }
        model.Reload();
    }

    private static FrameworkElement BuildWarningRow(string warning)
    {
        var text = new AutomationPresentationTextBlock
        {
            Text = warning,
            TextWrapping = TextWrapping.Wrap,
        };
        text.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.WarningBrush");
        var border = new AutomationNamedRowBorder
        {
            Child = text,
            Padding = new Thickness(0, 2, 0, 6),
            Focusable = true,
        };
        AutomationProperties.SetName(border, SyncPhrase.Warning(warning));
        AutomationProperties.SetAutomationId(border, "SyncDiagnosticsWarning");
        return border;
    }

    private static FrameworkElement BuildProviderRow(DetectedSyncProvider provider)
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal };
        var risk = new AutomationPresentationTextBlock
        {
            Text = SyncPhrase.RiskWord(provider.RiskLevel),
            Margin = new Thickness(0, 0, 8, 0),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };
        risk.SetResourceReference(
            TextBlock.ForegroundProperty, RiskBrushKey(provider.RiskLevel));
        heading.Children.Add(risk);
        heading.Children.Add(new AutomationPresentationTextBlock
        {
            // Core's display name, never re-cased (SD1).
            Text = provider.DisplayName,
            FontWeight = FontWeights.SemiBold,
        });
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 2) };
        panel.Children.Add(heading);
        panel.Children.Add(new AutomationPresentationTextBlock
        {
            // Core's normative recommendation sentence, verbatim.
            Text = provider.Recommendation,
            TextWrapping = TextWrapping.Wrap,
        });
        var border = new AutomationNamedRowBorder
        {
            Child = panel,
            Focusable = true,
        };
        AutomationProperties.SetName(
            border,
            $"{provider.DisplayName}: {SyncPhrase.RiskWord(provider.RiskLevel)}. "
            + provider.Recommendation);
        AutomationProperties.SetAutomationId(
            border, "SyncDiagnosticsProvider" + provider.Kind);
        return border;
    }

    /// <summary>Risk reads by WORD first (SyncPhrase.RiskWord); the
    /// brush is redundant emphasis, never the only channel (SD3).
    /// The mac mapping: High → destructive, Medium → warning, Low →
    /// secondary.</summary>
    private static string RiskBrushKey(RiskLevel risk) => risk switch
    {
        RiskLevel.High => "Slate.ErrorBrush",
        RiskLevel.Medium => "Slate.WarningBrush",
        _ => "Slate.SecondaryTextBrush",
    };

    /// <summary>The evidence disclosure is a SEPARATE operable sibling
    /// of the provider row (SD3(3)) — never folded into the row's
    /// combined name. Each path is its own focusable line, verbatim
    /// from core (SDINV-6).</summary>
    private FrameworkElement BuildEvidence(DetectedSyncProvider provider)
    {
        var paths = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        int index = 0;
        foreach (string path in provider.EvidencePaths)
        {
            TextBlock line = FocusableLine(
                path, $"SyncDiagnosticsEvidence{provider.Kind}Path{index}");
            line.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            line.Margin = new Thickness(0, 1, 0, 1);
            paths.Children.Add(line);
            index++;
        }
        var expander = new Expander
        {
            Header = SyncPhrase.Evidence,
            Content = paths,
            // Expansion SURVIVES the rebuild, keyed by core's provider
            // kind: a watcher republish must not snap shut the
            // disclosure a reader is walking (mac's ForEach id: \.kind
            // makes this free).
            IsExpanded = _expandedEvidence.Contains(provider.Kind),
            Margin = new Thickness(0, 0, 0, 8),
        };
        // Accessible name binds the disclosure to ITS provider —
        // identical "Evidence" siblings are indistinguishable to a
        // reader walking the leaf (axe SiblingUniqueAndFocusable;
        // SDD-4).
        AutomationProperties.SetName(
            expander, SyncPhrase.EvidenceFor(provider.DisplayName));
        AutomationProperties.SetAutomationId(
            expander, "SyncDiagnosticsEvidence" + provider.Kind);
        // The state rides the EXPANDER's OWN events. A subscription on
        // a persistent model object would accumulate one handler (and
        // root one dead visual subtree) per re-render — the recorded
        // round-2 lesson from the history day groups.
        SyncProviderKind kind = provider.Kind;
        expander.Expanded += (_, _) => _ = _expandedEvidence.Add(kind);
        expander.Collapsed += (_, _) => _ = _expandedEvidence.Remove(kind);
        return expander;
    }

    private static FrameworkElement BuildLiveSyncSection(LiveSyncConfigStatus status)
    {
        var panel = new StackPanel();
        var heading = new TextBlock
        {
            Text = SyncPhrase.LiveSyncConfiguration,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 4, 0, 4),
        };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level3);
        AutomationProperties.SetAutomationId(heading, "SyncDiagnosticsLiveSyncHeader");
        panel.Children.Add(heading);
        switch (status)
        {
            case LiveSyncConfigStatus.Parsed parsed:
                LiveSyncConfig config = parsed.Config;
                panel.Children.Add(ConfigRow(
                    SyncPhrase.ServerHost,
                    config.ServerHost ?? SyncPhrase.Unknown,
                    "ServerHost"));
                panel.Children.Add(ConfigRow(
                    SyncPhrase.Database,
                    config.Database ?? SyncPhrase.Unknown,
                    "Database"));
                panel.Children.Add(ConfigRow(
                    SyncPhrase.LiveSyncEnabled,
                    SyncPhrase.OnOff(config.LiveSyncEnabled),
                    "LiveSyncEnabled"));
                panel.Children.Add(ConfigRow(
                    SyncPhrase.SyncOnSave,
                    SyncPhrase.OnOff(config.SyncOnSave),
                    "SyncOnSave"));
                panel.Children.Add(ConfigRow(
                    SyncPhrase.SyncOnStart,
                    SyncPhrase.OnOff(config.SyncOnStart),
                    "SyncOnStart"));
                panel.Children.Add(ConfigRow(
                    SyncPhrase.EndToEndEncryption,
                    SyncPhrase.OnOff(config.EndToEndEncryption),
                    "EndToEndEncryption"));
                break;
            case LiveSyncConfigStatus.Malformed malformed:
                panel.Children.Add(FocusableLine(
                    SyncPhrase.ConfigMalformed(malformed.Reason),
                    "SyncDiagnosticsLiveSyncMalformed"));
                break;
            default:
                panel.Children.Add(FocusableLine(
                    SyncPhrase.ConfigAbsent, "SyncDiagnosticsLiveSyncAbsent"));
                break;
        }
        // The section name rides a LANDMARK border, never the
        // StackPanel — a panel gets no peer and the name is dropped.
        var landmark = new AutomationLandmarkBorder
        {
            Child = panel,
            Margin = new Thickness(0, 8, 0, 0),
        };
        AutomationProperties.SetName(landmark, SyncPhrase.LiveSyncConfiguration);
        AutomationProperties.SetAutomationId(landmark, "SyncDiagnosticsLiveSync");
        return landmark;
    }

    /// <summary>One combined element per config row: "{label}:
    /// {value}" (SD3(4)); the visual label/value pair inside is
    /// presentation-only.</summary>
    private static FrameworkElement ConfigRow(
        string label, string value, string idSuffix)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var labelBlock = new AutomationPresentationTextBlock
        {
            Text = label + ":",
            Margin = new Thickness(0, 0, 6, 0),
        };
        labelBlock.SetResourceReference(
            TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        row.Children.Add(labelBlock);
        row.Children.Add(new AutomationPresentationTextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
        });
        var border = new AutomationNamedRowBorder
        {
            Child = row,
            Padding = new Thickness(0, 1, 0, 1),
            Focusable = true,
        };
        AutomationProperties.SetName(border, SyncPhrase.ConfigRow(label, value));
        AutomationProperties.SetAutomationId(
            border, "SyncDiagnosticsLiveSync" + idSuffix);
        return border;
    }

    /// <summary>A standalone report line that a keyboard user must be
    /// able to reach: focusable, named by its own text (the W4-5
    /// notice convention).</summary>
    private static TextBlock FocusableLine(string text, string automationId)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Focusable = true,
        };
        AutomationProperties.SetAutomationId(block, automationId);
        return block;
    }

    // --- Focus preservation across rebuilds ---

    private string? CaptureFocusId()
    {
        // FrameworkElement, not DependencyObject: IsAncestorOf throws
        // on a non-Visual descendant (a focused ContentElement).
        if (Keyboard.FocusedElement is not FrameworkElement focused
            || !IsAncestorOf(focused))
        {
            return null;
        }
        DependencyObject? cursor = focused;
        while (cursor is not null && !ReferenceEquals(cursor, this))
        {
            if (cursor is FrameworkElement element
                && AutomationProperties.GetAutomationId(element)
                    is { Length: > 0 } id)
            {
                return id;
            }
            cursor = System.Windows.Media.VisualTreeHelper.GetParent(cursor);
        }
        return null;
    }

    private void RestoreFocusId(string? focusId)
    {
        if (focusId is null)
        {
            return;
        }
        // Queued at Input priority: a freshly added element cannot take
        // keyboard focus until the tree it joined has been laid out
        // (the HistorySurfaceView idiom).
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                FrameworkElement? target = FindDescendant(
                    _content, candidate => HasAutomationId(candidate, focusId));
                // Focus() RETURNING FALSE is the failure that matters:
                // an element can report Focusable and still refuse
                // focus — an evidence line inside a collapsed
                // disclosure is the live example. Discarding the result
                // left keyboard focus wherever Children.Clear() dropped
                // it, the window root, and Tab restarted at the top of
                // the shell.
                if (target is not null && target.Focus())
                {
                    return;
                }
                FocusLeafAnchor();
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>The captured element is gone (a provider disappeared, or
    /// the state changed under the reader) or refused focus: recover to
    /// a stable control INSIDE the leaf rather than letting focus fall
    /// out of the panel — the HistorySurfaceView segment-radio rule.
    /// Refresh anchors the populated states, Retry the error state, and
    /// the single focusable report line the rest.</summary>
    private void FocusLeafAnchor()
    {
        FrameworkElement? anchor =
            FindDescendant(
                _content,
                candidate => HasAutomationId(candidate, "SyncDiagnosticsRefresh"))
            ?? FindDescendant(
                _content,
                candidate => HasAutomationId(candidate, "SyncDiagnosticsRetry"))
            ?? FindDescendant(_content, candidate => candidate.Focusable);
        _ = anchor?.Focus();
    }

    private static bool HasAutomationId(FrameworkElement element, string id) =>
        string.Equals(
            AutomationProperties.GetAutomationId(element), id, StringComparison.Ordinal);

    private static FrameworkElement? FindDescendant(
        DependencyObject root, Func<FrameworkElement, bool> match)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency)
            {
                continue;
            }
            if (dependency is FrameworkElement element && match(element))
            {
                return element;
            }
            if (FindDescendant(dependency, match) is { } found)
            {
                return found;
            }
        }
        return null;
    }
}
