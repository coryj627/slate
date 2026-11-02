Scroll bar for panes and lists; draw it at the edge of a scrolling region in mocks.

```jsx
const { ScrollBar } = window.SlateDesignSystem_326dc9;
<ScrollBar length={480} thumb={0.4} position={0.2} />
<ScrollBar orientation="horizontal" length={320} expanded />
```

- From the Windows UI kit (Scrolling): a 14 px lane. At rest a 2 px thumb 4 px from the outer edge; on hover a 12 px track with a 6 px thumb and caret buttons (8 px glyphs EDDB, EDDC, EDD9, EDDA).
- Thumb colour is `--border-strong` (3:1 on the surface). Drag the thumb, click the track or a caret.

## WPF

- **Build as:** `ScrollBar` / `ScrollViewer` (Fluent style ships).
- **Rating:** Native: override resources only.
- The Fluent theme already collapses the bar to a thin indicator and expands it on hover.

Ratings and theme setup: `wpf-feasibility.md`.
