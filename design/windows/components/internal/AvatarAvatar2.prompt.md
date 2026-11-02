Fluent 2 "AvatarAvatar2" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { AvatarAvatar2 } = window.SlateDesignSystem_326dc9;
<AvatarAvatar2 type="image" size="16" />
```

Props (from AvatarAvatar2.d.ts):
- `pxRegular16?: React.ReactNode`
- `pxRegular24?: React.ReactNode`
- `pxRegular20?: React.ReactNode`
- `pxRegular28?: React.ReactNode`
- `pxRegular12?: React.ReactNode`
- `pxRegular32?: React.ReactNode`
- `type?: "image" | "icon" | "initials"`
- `size?: "16" | "20" | "24" | "28" | "32 (default)" | "36" | "40" | "48" | "56" | "64" | "72" | "96" | "120"`
- `pxRegular48?: React.ReactNode`
- `initialsString1Max?: string`
- `initialsString2Max?: string`
- `activityRing?: boolean`
- `pxRegularBadgeIcon12?: React.ReactNode`
- `badgeIcon?: boolean`
- `pxRegularBadgeIcon16?: React.ReactNode`
- `pxRegularBadgeIcon24?: React.ReactNode`
- `presenceBadge?: boolean`
- `icon1?: React.ReactNode`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.
