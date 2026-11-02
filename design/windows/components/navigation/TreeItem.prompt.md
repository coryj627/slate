Fluent 2 "TreeItem" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { TreeItem } = window.SlateDesignSystem_326dc9;
<TreeItem appearance="subtle" size="small" />
```

Props (from TreeItem.d.ts):
- `iconBefore?: boolean`
- `chevron?: boolean`
- `checkbox?: boolean`
- `iconBeforeRegular20px?: React.ReactNode`
- `iconBeforeFilled20px?: React.ReactNode`
- `iconBeforeRegular16px?: React.ReactNode`
- `iconBeforeFilled16px?: React.ReactNode`
- `primaryText?: string`
- `iconAfter?: boolean`
- `iconAfter1Filled20px?: React.ReactNode`
- `iconAfter1Regular20px?: React.ReactNode`
- `iconAfter1Regular16px?: React.ReactNode`
- `iconAfter1Filled16px?: React.ReactNode`
- `badges?: boolean`
- `quickActions?: boolean`
- `focus?: boolean`
- `style2?: "subtle (default)" | "subtle alpha" | "transparent"`
- `size?: "sm" | "medium (default)"`
- `state?: "rest" | "hover" | "pressed" | "selected" | "disabled"`
- `leafNode?: boolean`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `TreeViewItem`.
- **Rating:** Native: the Fluent style ships, so override resource keys only.
- The Fluent `TreeView` style ships. The Slate template adds the gold pill, the pale gold selected fill and the 32/24px row heights.

Ratings and theme setup: `wpf-feasibility.md`.
