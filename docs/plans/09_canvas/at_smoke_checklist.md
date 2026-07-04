# Milestone T — manual AT smoke checklist (t0 §4, #365)

Run at close-out on a real machine with the shipped build. Automated
gates (a11y-check 100/100, APCA matrix, announcement-grammar suite,
E2E) are CI-enforced; this list covers what only a human + real
assistive tech can verify. Record the run date, macOS version, and
result per item.

| # | Check | How | Result |
|---|---|---|---|
| 1 | VoiceOver walk | Open sample canvas; VO-navigate outline → table → visual; every card reads title + N-of-M + color name; connection rows read direction + label | ☐ |
| 2 | VO Quick Nav | Arrow-only traversal in each surface; rotors jump by card / group / connection | ☐ |
| 3 | VO on the visual board | VO next/prev over card elements pans the window (no dead end at the window edge); Press selects | ☐ |
| 4 | Full Keyboard Access | Tab-through the canvas header (switcher, filter), surfaces, and sheets; focus ring always visible | ☐ |
| 5 | Voice Control | "Show numbers" numbers every visible card; dictate: "Click 3", "Toggle Mark", "Connect To", "Delete Marked Cards", "Where am I" | ☐ |
| 6 | Switch Control | Cycle into move mode, nudge, commit; Esc ladder exits mode → filter → surface | ☐ |
| 7 | Braille inspectability | With a braille display (or VO braille viewer): mode state, marks, filter state readable from element VALUES without waiting for announcements | ☐ |
| 8 | Dynamic Type | Largest accessibility text size: renderer labels scale, no clipping; sheets/pickers reflow | ☐ |
| 9 | Increase Contrast | Fills collapse, names remain, selection ring visible on every preset fill | ☐ |
| 10 | Reduce Motion | Pan/zoom/selection changes land instantly (no spring) | ☐ |

Residual: items 1–10 require human judgment with real AT; CI cannot
substitute. File findings as `audit`-labeled issues per repo
convention, credited to the tester.
