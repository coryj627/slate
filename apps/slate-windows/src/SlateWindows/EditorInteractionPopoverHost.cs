// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// Gives editor interaction popovers a deterministic keyboard entry point and
/// keeps Escape behavior local to the transient surface.
/// </summary>
internal sealed class EditorInteractionPopoverHost : ContentControl
{
    public static readonly DependencyProperty InteractionSessionProperty =
        DependencyProperty.Register(
            nameof(InteractionSession),
            typeof(EditorInteractionCoordinator),
            typeof(EditorInteractionPopoverHost),
            new PropertyMetadata(null, InteractionSession_Changed));

    private EditorInteractionCoordinator? _attachedInteraction;
    internal int InitialFocusQueueCountForTests { get; private set; }

    public EditorInteractionPopoverHost()
    {
        Loaded += PopoverHost_Loaded;
        Unloaded += PopoverHost_Unloaded;
        PreviewKeyDown += PopoverHost_PreviewKeyDown;
        MouseEnter += (_, _) => InteractionSession?.HoldCitationPopoverOpen();
        MouseLeave += (_, _) => InteractionSession?.ReleaseCitationPopover();
    }

    public EditorInteractionCoordinator? InteractionSession
    {
        get => (EditorInteractionCoordinator?)GetValue(InteractionSessionProperty);
        set => SetValue(InteractionSessionProperty, value);
    }

    private static void InteractionSession_Changed(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        ((EditorInteractionPopoverHost)dependencyObject).AttachInteraction();
    }

    private void PopoverHost_Loaded(object sender, RoutedEventArgs e)
    {
        AttachInteraction();
        if (_attachedInteraction?.ConsumePopoverFocusRequest() == true)
        {
            QueueInitialFocus();
        }
    }

    private void PopoverHost_Unloaded(object sender, RoutedEventArgs e) =>
        DetachInteraction();

    private void AttachInteraction()
    {
        DetachInteraction();
        _attachedInteraction = InteractionSession;
        if (_attachedInteraction is not null)
        {
            _attachedInteraction.PopoverFocusRequested += Interaction_PopoverFocusRequested;
        }
    }

    private void DetachInteraction()
    {
        if (_attachedInteraction is not null)
        {
            _attachedInteraction.PopoverFocusRequested -= Interaction_PopoverFocusRequested;
            _attachedInteraction = null;
        }
    }

    private void Interaction_PopoverFocusRequested(object? sender, EventArgs e)
    {
        if (IsLoaded && _attachedInteraction?.ConsumePopoverFocusRequest() == true)
        {
            QueueInitialFocus();
        }
    }

    private void QueueInitialFocus()
    {
        InitialFocusQueueCountForTests++;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, FocusInitialControl);
    }

    private void FocusInitialControl()
    {
        Button? close = FindDescendant<Button>(
            this,
            button => AutomationProperties.GetAutomationId(button) == "EditorPopoverClose");
        if (close is not null)
        {
            close.Focus();
            Keyboard.Focus(close);
        }
    }

    private void PopoverHost_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || InteractionSession?.IsPopoverOpen != true)
        {
            return;
        }

        InteractionSession.ClosePopoverCommand.Execute(null);
        e.Handled = true;
    }

    private static T? FindDescendant<T>(
        DependencyObject parent,
        Predicate<T> predicate)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T candidate && predicate(candidate))
            {
                return candidate;
            }

            T? nested = FindDescendant(child, predicate);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
