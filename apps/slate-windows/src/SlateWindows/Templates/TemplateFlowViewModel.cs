// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Templates;

/// <summary>Which step the flow sheet is showing (contracts doc TD-5:
/// one sheet hosting mac's two — step order, skip rule, and cancel
/// semantics identical; only the presentation mechanics differ).</summary>
internal enum TemplateFlowStep
{
    Prompts,
    Name,
}

/// <summary>One prompt row: the label is the template author's text
/// (never the slug key — mac labels its fields the same way), the
/// value is seeded empty (mac's defaults) so an untouched field
/// substitutes empty rather than leaving its marker literal (T4).</summary>
internal sealed class TemplatePromptFieldViewModel : BindableBase
{
    private string _value = string.Empty;

    public TemplatePromptFieldViewModel(TemplatePrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        Key = prompt.Key;
        Label = prompt.Label;
    }

    public string Key { get; }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }
}

/// <summary>
/// The prompt/name flow sheet (contracts T4–T7): typed prompt fields
/// in declaration order, then the note name with mac's validation and
/// default seed. The sheet owns presentation state only — the create
/// itself (render + exclusive write + open + caret) is the workspace
/// coordinator's, injected as <c>createRequested</c>, and a failed
/// create lands back here through <see cref="PresentCreateFailure"/>
/// with the user's exact name preserved (T7).
/// </summary>
internal sealed class TemplateFlowViewModel : BindableBase
{
    private readonly Action<TemplateFlowViewModel> _createRequested;
    private readonly Action _cancelled;
    private readonly Func<DateTime> _utcNow;

    private TemplateFlowStep _step;
    private string _noteName = string.Empty;
    private string? _validationError;
    private bool _nameSeeded;
    private ICommand? _nextCommand;
    private ICommand? _createCommand;
    private ICommand? _cancelCommand;

    public TemplateFlowViewModel(
        TemplateSummary template,
        IReadOnlyList<TemplatePrompt> prompts,
        string destinationDescription,
        Action<TemplateFlowViewModel> createRequested,
        Action cancelled,
        Func<DateTime>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(prompts);
        ArgumentNullException.ThrowIfNull(destinationDescription);
        ArgumentNullException.ThrowIfNull(createRequested);
        ArgumentNullException.ThrowIfNull(cancelled);
        Template = template;
        DestinationDescription = destinationDescription;
        _createRequested = createRequested;
        _cancelled = cancelled;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);

        var fields = new List<TemplatePromptFieldViewModel>(prompts.Count);
        foreach (TemplatePrompt prompt in prompts)
        {
            fields.Add(new TemplatePromptFieldViewModel(prompt));
        }

        PromptFields = fields;

        // The no-variable fast path (T2, pinned): zero prompts skip
        // the prompt step entirely, mac's exact routing.
        if (fields.Count == 0)
        {
            EnterNameStep();
        }
        else
        {
            _step = TemplateFlowStep.Prompts;
        }
    }

    public TemplateSummary Template { get; }

    public string DestinationDescription { get; }

    public IReadOnlyList<TemplatePromptFieldViewModel> PromptFields { get; }

    public TemplateFlowStep Step
    {
        get => _step;
        private set
        {
            if (SetField(ref _step, value))
            {
                OnPropertyChanged(nameof(IsPromptStep));
                OnPropertyChanged(nameof(IsNameStep));
            }
        }
    }

    public bool IsPromptStep => Step == TemplateFlowStep.Prompts;

    public bool IsNameStep => Step == TemplateFlowStep.Name;

    public string NoteName
    {
        get => _noteName;
        set => SetField(ref _noteName, value);
    }

    /// <summary>Inline, focusable, never announced (T6/T7).</summary>
    public string? ValidationError
    {
        get => _validationError;
        private set
        {
            if (SetField(ref _validationError, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public bool HasValidationError => ValidationError is not null;

    /// <summary>Prompts → Name (mac's Next). No validation on prompt
    /// values — any string including empty is a valid answer (T4).</summary>
    public ICommand NextCommand => _nextCommand ??= new RelayCommand(
        _ => EnterNameStep(), _ => true);

    public ICommand CreateCommand => _createCommand ??= new RelayCommand(
        _ => RequestCreate(), _ => true);

    public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(
        _ => _cancelled(), _ => true);

    /// <summary>Every prompt key with its current value — all keys
    /// present, untouched fields as empty strings (T4).</summary>
    public Dictionary<string, string> PromptValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (TemplatePromptFieldViewModel field in PromptFields)
        {
            values[field.Key] = field.Value;
        }

        return values;
    }

    /// <summary>A failed create re-presents the name step with the
    /// user's exact prior entry and core's message verbatim (T7).</summary>
    public void PresentCreateFailure(string retryName, string error)
    {
        ArgumentNullException.ThrowIfNull(retryName);
        ArgumentNullException.ThrowIfNull(error);
        NoteName = retryName;
        ValidationError = error;
        Step = TemplateFlowStep.Name;
    }

    private void EnterNameStep()
    {
        if (!_nameSeeded)
        {
            // Seeded once, at name-step entry (mac's `didSeed` on the
            // name sheet's onAppear) — the daily-date suffix reads the
            // clock when the step appears, not when the flow began.
            NoteName = TemplateNameRules.DefaultNoteName(Template.Name, _utcNow());
            _nameSeeded = true;
        }

        ValidationError = null;
        Step = TemplateFlowStep.Name;
    }

    private void RequestCreate()
    {
        string trimmed = NoteName.Trim();
        if (TemplateNameRules.Validate(trimmed) is { } problem)
        {
            ValidationError = problem;
            return;
        }

        ValidationError = null;
        _createRequested(this);
    }
}
