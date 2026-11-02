Window caption button (minimize, maximize or restore, close) for the 48 px Slate title bar; use three in a row at the right end.

```jsx
const { CaptionButton } = window.SlateDesignSystem_326dc9;
<div style={{ display: "flex", height: 48 }}>
  <CaptionButton kind="minimize" />
  <CaptionButton kind={maximized ? "restore" : "maximize"} onClick={toggle} />
  <CaptionButton kind="close" />
</div>
```

- From the Windows UI kit (Shell › Title bar): 10 px glyphs E921, E922, E923, E8BB. Hover uses the subtle hover fill; Close hover is #C42B1C with a white glyph.
- 46 px wide. The kit draws 48; Slate keeps the Windows width.
- `inactive` dims the glyph when the window loses focus.

## WPF

- **Build as:** `Button` with a `CaptionButton` style inside the `WindowChrome` caption, `WindowChrome.IsHitTestVisibleInChrome="True"`.
- **Rating:** Build: needs `WindowChrome` and, for Snap Layouts, a `WM_NCHITTEST` hook returning `HTMAXBUTTON` over maximize.
- Calls `SystemCommands.MinimizeWindow`, `MaximizeWindow`, `RestoreWindow`, `CloseWindow`.

Ratings and theme setup: `wpf-feasibility.md`.
