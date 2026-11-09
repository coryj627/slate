# WinUI kit review

Reviewed on 2 November 2026: "Windows UI kit (Community).fig" against this design system and the WPF Fluent theme (`dotnet/wpf`, `main`). Nothing from the kit has been imported yet. Its 115 variables are saved for reference in `scraps/winui/fig-tokens.css`, which `styles.css` doesn't load.

## Summary

- WPF's Fluent theme is a port of the WinUI control styles, so this kit shows what the Slate app looks like by default. Spot checks agree. For example, the check box is a 20 px box in a 32 px row in both the kit and `CheckBox.xaml`.
- This design system's components come from the Fluent 2 Web kit. In several places it differs from WinUI in ways a user would see: check boxes and radio buttons are smaller, menu rows are spaced differently, and the type ramp and shadow strengths are different. Designs made with the current components won't match the app there.
- Most of the kit is out of scope: Windows shell surfaces, Windows app icons and repeated sets.

## What the kit contains

22 pages: 1,142 component sets, 2,645 standalone components, 115 variables in 6 collections, and no text styles. The type ramp is a guidance chart instead.

- **Relevant to Slate** (WPF has a matching control): Basic input, Text fields, Menus and toolbars, Navigation, Lists and collections, Dialogs and flyouts, Status and info, Scrolling, Date and time, the title bar on the Shell page, and the guidance charts (typography, elevation, corners, layering and materials, colour, motion).
- **Not relevant:**
  - Windows shell surfaces: quick-settings toggles (Airplane mode, Battery saver, Bluetooth), cursors, touch gestures, UAC and shell dialogs, Sticky Keys, input keys.
  - 104 Windows app icons (the Apps set, 351 variants).
  - The splash screen, app examples and documentation pages.
  - Many sets repeat. For example, Breadcrumb's Chevron Node part appears more than 20 times.

## Foundations

| Area | WinUI kit | This system now | Proposal |
| --- | --- | --- | --- |
| Type ramp | Caption 12/16 · Body 14/20 · Body Strong 14/20 semibold · Body Large 18/24 · Body Large Strong 18/24 semibold · Subtitle 20/28 · Title 28/36 · Title Large 40/52 · Display 68/92 (all semibold from Subtitle up) | Fluent 2 Web ramp: 10/14, 12/16, 14/20, 16/22, 20/28, 24/32, 28/36, 32/40, 40/52, 68/92. No 18/24 | Make the WinUI ramp Slate's UI ramp. WPF ships matching `TextBlock` styles (Caption, Body, BodyStrong, Subtitle, Title, TitleLarge, Display) but not Body Large, so Slate adds that one. Keep the Web tokens for the Web components |
| Optical sizes | Small at 12, Text at 14–18, Display from 20 | Same | No change |
| Elevation | Card: 1px stroke + `0 2 4` at 4% (dark 13%). Tooltip: stroke + `0 2 4` at 10% (26%). Flyout: stroke + `0 8 16` at 14% (26%). Dialog: stroke + `0 32 64` at 24% (48%) + `0 0 8` at 20% (32%) | Six levels (2, 4, 8, 16, 28, 64), one layer, 18% (dark 36%) | Use WinUI's four levels and opacities. Drop the dialog's second layer for WPF. The command palette moves from 28 to the flyout or dialog level |
| Modal scrim | 30% black in both themes (WPF `SmokeFillColorDefault` is also 30%) | 20% | 30% |
| Focus ring | 2px outer ring in the primary text colour, plus a 1px inner ring (white in light, 70% black in dark), 1px outside the control | 2px ring in the text colour, 1px outside | Add the 1px inner ring. It keeps focus visible on gold fills |
| Corners | Controls 4 (the fill inside the 1px border is 3), overlays 8, compact controls 3 | 4 and 8, tabs 8 (TabView) | No change |
| Colour | Windows default blue accent. Translucent fills designed for Mica (control fill 70% white, text 89.56% / 60.63% black) | Slate's opaque neutrals from the app's theme files (APCA-gated), gold accent | No change. The translucent fills assume Mica, which Slate doesn't use |
| Dialog width | 448 (320 minimum, 520 maximum) | Web Dialog: 600 (480 minimum), small 320 | 448, 320 to 520 |

## Title bar

- **WinUI kit ("Tall", the default):** 48 high. Caption buttons are 48 × 48 (the group is 144 wide). The 16px app icon and the title (Caption 12/16) start 16px from the left edge, with a 16px gap between them. An optional 300 × 32 search box sits in the centre. The bar is drawn on Mica.
- **This system:** 48 high, caption buttons 46 × 48, the logo app-menu button, title in Caption 12/16, then the palette and settings buttons. The surface is solid.
- **Proposal:** make the caption buttons 48 × 48. Keep the solid surface and the logo menu.

## Controls

| Control | WinUI kit | WPF Fluent theme | This system (Web kit) | Gap |
| --- | --- | --- | --- | --- |
| Button | 32 high, radius 4, padding 4/11/6/11, 1px gradient border | Same (ported) | 32 high, radius 4, 12 px side padding | Small. Keep Slate's flat 3:1 border |
| Subtle icon button | 40 × 40 target, 30 × 30 fill, 16px glyph | Not checked | 32 × 32 | Pick one size for the title bar and toolbars |
| Check box | 20 px box (18 + 1px stroke, radius 3) in a 32px row, 8px gap | `CheckBoxSize` 20, `CheckBoxHeight` 32 | 16 px (medium) | Visible: designs show smaller boxes than the app |
| Radio button | 20 px in a 32 px row, 8 px gap | 20 px | 16 px (medium) | Visible |
| Toggle switch | 40 × 20 in a 32 px row, 12 px gap | None (Slate templates a `CheckBox`) | 40 × 20 | Matches |
| Text box | 32 high, radius 4, fill 70% white, placeholder 57.83% black | Same (ported) | 32 high | Small |
| Menu item | 40 px pitch with a 34 px highlight inset 5 px from the menu edges, radius 3; text 16 px from the edge | Content margin 8, 6, 8, 6 | 32 px rows, highlight runs the full width | Slate's own menu template is planned anyway; choose WinUI or Web spacing |
| Navigation item (compact) | 48 × 40 with a 38 × 34 inset highlight, 16 px icon, 3 px rounded pill | No NavigationView | Web Nav | Affects PaneSwitcher and the Settings categories |

## What the system lacks

WinUI defines these and Slate uses them:
- **Scroll bar:** thin at rest, wider on hover. Every pane scrolls, and the system has no scroll bar.
- **Title bar caption buttons:** these exist only in the UI kit, not as a component.
- **Date and time pickers:** for date properties.
- **Tab view:** the note tabs are hand-built in the UI kit.

These exist in Web form and are close enough to keep: Expander (Accordion), InfoBar (MessageBar), NumberBox (SpinButton), ContentDialog (Dialog), TeachingTip (TeachingPopover).

## Recommendation

1. **Adopt the WinUI foundations:** type ramp, elevation, 30% scrim, inner focus ring, caption-button size and dialog widths. It's a small change, and designs then match the app.
2. **Rebuild only the controls that look different:** check box, radio button, menu item, navigation item, and the subtle icon button (pick a size). Also add a scroll bar, caption buttons, a tab view and a date picker from the kit. Keep the Web components everywhere else.
3. **Don't import the whole file.** Most of it is Windows shell, app icons and repeated sets, and it would multiply the bundle size.

## Decisions (2 November 2026)

**Adopted:**
- The WinUI type ramp, as `--type-*` tokens.
- A 30% modal scrim (`--scrim`).
- Dialog width 448 (320 to 520).
- Menu rows at a 40 px pitch with a 34 px highlight inset 5 px, radius 3.
- Subtle icon buttons at 40 × 40 with a 30 × 30 fill in the title bar, and 32 × 32 in panel toolbars and the note header, where the rows are 40 (settled 3 November 2026, plan 46, DSD6).

**Not adopted:**
- WinUI shadows: Slate keeps one layer per level at 18% (36% dark).
- The inner focus ring.
- 48 × 48 caption buttons: Slate keeps 46 × 48.

**Rebuilt under the same names:**
- Checkbox and Radio.
- MenuItem and MenuMenu.
- NavNodeMedium, now also used for the Settings categories.
- Button, in its subtle icon-only medium variant.

**Added:** CaptionButton, TabView, ScrollBar and DatePicker. The UI kit uses CaptionButton and TabView in the title bar and tab strip.

**Not built:** everything else in the kit. See "What the kit contains" above for the out-of-scope groups.
