One setting as a card: label, a one-line description and the control on the right. Use inside SettingsLayout.

```jsx
const { SettingRow, Switch } = window.SlateDesignSystem_326dc9;
<SettingRow icon="E736" label="Open reading links in a new tab" description="Off opens links in the current tab.">
  <Switch checked layout="switch" />
</SettingRow>
```

- Minimum height 68 px, padding 12 × 16, 4 px radius, `--surface-card` on a subtle border.
- Label is 14/20 primary and description 12/16 secondary. With `disabled` both use `--text-disabled`, and you should disable the control as well.
- Switch for on/off, a dropdown for 3–7 choices, SpinButton for numbers such as font size. Write labels in sentence case, and say what the setting does rather than what it is.
- `stacked` puts the control under the text, indented to the label when there's an icon.

## WPF

- **Build as:** `Grid`.
- **Rating:** Template: a new `Style` or `ControlTemplate` on an existing WPF control.
- Label, description and control. Point the control's `AutomationProperties.LabeledBy` at the label and put the description in `HelpText`. Toggles use the Switch template.

Ratings and theme setup: `wpf-feasibility.md`.
