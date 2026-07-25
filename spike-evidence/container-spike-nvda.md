# W3-1 container spike — what NVDA actually says

Driven with `NvdaTestingDriver` (bundled portable NVDA). **Currency caveat:** package `0.2.0-beta`, repository unchanged since 2022-12-08, so this is not the NVDA 2026.x a user runs. A PASS here is strong evidence; a FAIL is worth re-checking by hand before acting on it.

## flow

1/16 steps produced the expected speech.

| step | expected | NVDA said | ok |
|---|---|---|---|
| say all | Reading container spike | <error: Timeout while waiting for stopping speech.> | **NO** |
| next heading #1 | Reading container spike | <error: Timeout while waiting for stopping speech.> | **NO** |
| next heading #2 | Second level heading | <error: Timeout while waiting for stopping speech.> | **NO** |
| heading level 1 | heading | <error: Timeout while waiting for stopping speech.> | **NO** |
| heading level 2 | heading | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #1 | resolved note | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #2 | absent note | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #3 | tag | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #4 | Smith | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list | list | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list item #1 | first bullet | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list item #2 | second bullet | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list item #3 | nested bullet | <error: Timeout while waiting for stopping speech.> | **NO** |
| next table | table | <error: Timeout while waiting for stopping speech.> | **NO** |
| next button | button | <error: Timeout while waiting for stopping speech.> | **NO** |
| read current line | — | <error: Timeout while waiting for stopping speech.> | yes |

## items

1/16 steps produced the expected speech.

| step | expected | NVDA said | ok |
|---|---|---|---|
| say all | Reading container spike | <error: Timeout while waiting for stopping speech.> | **NO** |
| next heading #1 | Reading container spike | <error: Timeout while waiting for stopping speech.> | **NO** |
| next heading #2 | Second level heading | <error: Timeout while waiting for stopping speech.> | **NO** |
| heading level 1 | heading | <error: Timeout while waiting for stopping speech.> | **NO** |
| heading level 2 | heading | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #1 | resolved note | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #2 | absent note | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #3 | tag | <error: Timeout while waiting for stopping speech.> | **NO** |
| next link #4 | Smith | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list | list | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list item #1 | first bullet | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list item #2 | second bullet | <error: Timeout while waiting for stopping speech.> | **NO** |
| next list item #3 | nested bullet | <error: Timeout while waiting for stopping speech.> | **NO** |
| next table | table | <error: Timeout while waiting for stopping speech.> | **NO** |
| next button | button | <error: Timeout while waiting for stopping speech.> | **NO** |
| read current line | — | <error: Timeout while waiting for stopping speech.> | yes |

## How to read this

`next link` steps are the decisive ones for the container choice: the UIA probe found NO `Hyperlink` control types in the FlowDocument variant, but NVDA can reach links through text-range embedded objects. If the link steps speak in `flow`, the missing control types are not disqualifying; if they are silent, they are.

`next list` / `next list item` test the owner call that lists expose native semantics. Neither container produced a `ListItem` control type, so silence here is expected and quantifies what custom `AutomationPeer`s must add.
