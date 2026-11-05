// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// DS-10 (#1811): one setting as a card: its label and description, and the control on the
/// right, which takes its name from the label and its help from the description. The row
/// itself isn't a stop or a group a reader hears: the control already says all of it.
/// </summary>
internal sealed class SettingRow : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    private FrameworkElement? _label;

    // #1767: a subclass gets no implicit style.
    public SettingRow()
    {
        Focusable = false;
        SetResourceReference(TemplateProperty, "Slate.SettingRowTemplate");
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _label = GetTemplateChild("PART_Label") as FrameworkElement;

        // A LabeledBy name reads the label's peer, and WPF only finds one that already exists.
        // A client walking the tree makes it, which can be after a client asks for the name.
        if (_label is not null)
        {
            _ = UIElementAutomationPeer.CreatePeerForElement(_label);
        }

        LabelTheControl();
    }

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        LabelTheControl();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SettingRowPeer(this);

    private static void OnDescriptionChanged(DependencyObject row, DependencyPropertyChangedEventArgs change) =>
        ((SettingRow)row).LabelTheControl();

    private void LabelTheControl()
    {
        if (Content is DependencyObject control)
        {
            AutomationProperties.SetLabeledBy(control, _label);
            AutomationProperties.SetHelpText(control, Description);
        }
    }

    private sealed class SettingRowPeer(SettingRow owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}
