Date field with a calendar flyout; use it for date properties and anywhere a single date is picked.

```jsx
const { DatePicker } = window.SlateDesignSystem_326dc9;
<DatePicker label="Due" value="2026-11-14" onChange={setDue} />
```

- From the Windows UI kit (Date and time › Calendar date picker): 32 px field, "Pick a date" placeholder, 12 px E787 calendar glyph 12 px from the right.
- Flyout: 300 px, radius 8, month header with previous and next buttons, 40 × 40 day cells with 2 px gaps. Today is an accent-filled circle; the selected day has an accent ring.
- Keyboard: arrows move by day and week, Page Up/Down by month, Home/End to the week’s ends, Enter picks, Esc closes.

## WPF

- **Build as:** `DatePicker` (Fluent style ships) or `Calendar` in a `Popup`.
- **Rating:** Native: override resources only.
- Set `FirstDayOfWeek` from the user’s locale.

Ratings and theme setup: `wpf-feasibility.md`.
