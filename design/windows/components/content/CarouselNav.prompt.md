Fluent 2 "CarouselNav" component re-themed to Slate (gold brand ramp) — use it wherever Slate's Windows UI needs this control, choosing variants via props.

```jsx
const { CarouselNav } = window.SlateDesignSystem_326dc9;
<CarouselNav mode="stepsdefault" />
```

Props (from CarouselNav.d.ts):
- `mode?: "stepsdefault" | "stepsautoplay" | "imagepreviewautoplay" | "imagepreview default"`

Short values are accepted everywhere: `appearance` (alias of `style2`), sizes `small|medium|large`, booleans for on/off props. The Figma values still work.

Notes: generated from the Fluent 2 Web Figma kit; each variant renders the exact Figma geometry. Brand colours resolve through `--brand-*` tokens, which tokens/brand.css re-keys to Slate gold. Set `data-theme="dark"` on <html> for the dark mode.

## WPF

- **Build as:** `ListBox` of dots.
- **Rating:** Build: needs code or a custom control.
- Part of Carousel. Each dot needs `AutomationProperties.Name` ("Slide 2 of 5").

Ratings and theme setup: `wpf-feasibility.md`.
