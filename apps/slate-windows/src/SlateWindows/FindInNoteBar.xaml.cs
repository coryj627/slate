// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// WB-22 (#1112): the find bar beside the note (DataContext: the tab's
/// <see cref="FindInNoteViewModel"/>). It attaches the editor as the search
/// surface while open, and runs its own keys from <see cref="Bind"/>'s map
/// in the tunnelling phase, so they are live only while focus is inside it.
/// </summary>
internal partial class FindInNoteBar : UserControl
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(SlateTextEditor),
        typeof(FindInNoteBar),
        new PropertyMetadata(null, (owner, e) => ((FindInNoteBar)owner).OnEditorChanged(
            e.OldValue as SlateTextEditor, e.NewValue as SlateTextEditor)));

    private readonly Dictionary<(Key Key, ModifierKeys Modifiers), Func<bool>> _chords = [];
    private FindInNoteViewModel? _model;
    private EditorFindSurface? _surface;

    public FindInNoteBar()
    {
        InitializeComponent();
        Bind();
        DataContextChanged += (_, e) => OnModelChanged(e.OldValue as FindInNoteViewModel, e.NewValue as FindInNoteViewModel);
    }

    /// <summary>The note's editor (the template's <c>MarkdownEditor</c>).</summary>
    internal SlateTextEditor? Editor
    {
        get => (SlateTextEditor?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    internal TextBox Query => QueryBox;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_chords.TryGetValue((e.Key, Keyboard.Modifiers), out Func<bool>? action) && action())
        {
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    /// <summary>The bar's own keys (<c>ChordScope.FindInNote</c>): Enter and
    /// Shift+Enter step only while the query holds the focus (a focused
    /// button keeps its own Enter); Escape closes from anywhere in the
    /// bar.</summary>
    private void Bind()
    {
        AddChord(Key.Enter, ModifierKeys.None, () => StepFromQuery(forward: true));
        AddChord(Key.Enter, ModifierKeys.Shift, () => StepFromQuery(forward: false));
        AddChord(Key.Escape, ModifierKeys.None, CloseFromBar);
    }

    private void AddChord(Key key, ModifierKeys modifiers, Func<bool> action) => _chords[(key, modifiers)] = action;

    private bool StepFromQuery(bool forward)
    {
        if (_model is not { IsOpen: true } model || !QueryBox.IsKeyboardFocusWithin)
        {
            return false;
        }
        if (forward)
        {
            model.Next();
        }
        else
        {
            model.Previous();
        }
        return true;
    }

    private bool CloseFromBar()
    {
        if (_model is not { IsOpen: true } model)
        {
            return false;
        }
        model.Close();
        return true;
    }

    private void OnModelChanged(FindInNoteViewModel? oldModel, FindInNoteViewModel? newModel)
    {
        if (oldModel is not null)
        {
            oldModel.PropertyChanged -= Model_PropertyChanged;
            oldModel.FocusRequested -= FocusQuery;
            // A tab switch rebinds the shared template: the outgoing tab's
            // surface is detached (its highlights cleared).
            oldModel.AttachSurface(null);
        }
        _surface = null;
        _model = newModel;
        if (newModel is not null)
        {
            newModel.PropertyChanged += Model_PropertyChanged;
            newModel.FocusRequested += FocusQuery;
        }
        Reattach();
    }

    private void OnEditorChanged(SlateTextEditor? oldEditor, SlateTextEditor? newEditor)
    {
        if (oldEditor is not null)
        {
            oldEditor.DocumentChanged -= Editor_DocumentChanged;
            oldEditor.DataContextChanged -= Editor_DataContextChanged;
        }
        if (newEditor is not null)
        {
            newEditor.DocumentChanged += Editor_DocumentChanged;
            newEditor.DataContextChanged += Editor_DataContextChanged;
        }
        _model?.AttachSurface(null);
        _surface = null;
        Reattach();
    }

    // Ownership is decided on the editor's tab as well as its document, so a
    // context that arrives without a document change is answered too.
    private void Editor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => Reattach();

    private void Editor_DocumentChanged(object? sender, EventArgs e)
    {
        _model?.AttachSurface(null);
        _surface = null;
        Reattach();
    }

    private void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FindInNoteViewModel.IsOpen) or nameof(FindInNoteViewModel.Mode))
        {
            Reattach();
        }
    }

    /// <summary>The editor is the surface while the bar is open in editor
    /// mode; otherwise nothing is attached.</summary>
    private void Reattach()
    {
        if (_model is null)
        {
            return;
        }
        // WPF may update the shared template's bindings in any order on a tab
        // switch: the editor and its document are attached only once BOTH
        // belong to this bar's tab, so another tab's text never becomes the
        // search surface (nor rebases this tab's restore point).
        if (_model is { IsOpen: true, Mode: FindInNoteMode.Editor }
            && Editor is { Document: { } document, DataContext: WorkspaceTabViewModel tab } editor
            && ReferenceEquals(tab.FindInNote, _model)
            && ReferenceEquals(tab.EditorDocument, document))
        {
            if (_surface is null || !ReferenceEquals(_surface.Document, document))
            {
                _surface = new EditorFindSurface(document, editor);
            }
            _model.AttachSurface(_surface);
            return;
        }
        _surface = null;
        _model.AttachSurface(null);
    }

    private void FocusQuery()
    {
        if (!TryFocusQuery())
        {
            // The bar has just become visible; focus once layout lets it.
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => TryFocusQuery()));
        }
    }

    private bool TryFocusQuery()
    {
        if (!QueryBox.Focus())
        {
            return false;
        }
        QueryBox.SelectAll();
        return true;
    }
}
