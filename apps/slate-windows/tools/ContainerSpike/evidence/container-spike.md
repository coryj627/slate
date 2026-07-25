# W3-1 container spike — UIA measurements

Programmatic half of the §10.6 spike. The JAWS/NVDA behavioural half is NOT covered here — see the manual script at the end.

| measurement | flow | items |
|---|---|---|
| surface control type | Document | Pane |
| Text pattern present | **yes** | **no** |
| Text pattern host | Document | — |
| reading-order chars | 363 | 0 |
| landmarks found | 12/14 | 0/14 |
| landmarks missing | fn main, Embedded note | none |
| landmarks out of order | none | none |
| List control types | 0 | 1 |
| ListItem control types | 0 | 0 |
| HeadingLevel reaches UIA | **no** | **yes** |
| hyperlinks | 0 | 5 |
| hyperlinks w/ HelpText | 0 | 2 |
| hyperlinks focusable | 0 | 0 |
| interactive children | 7 | 7 |
| interactive focusable | 4 | 4 |
| focusable elements | 9 | 9 |
| axe errors | **2** | **2** |

## flow

Control-type histogram: Button=5, Text=3, Document=2, CheckBox=2, TitleBar=1, MenuBar=1, MenuItem=1, Separator=1

| interactive child | focusable | invoke/toggle |
|---|---|---|
| Minimize | False | True |
| Maximize | False | True |
| Close | False | True |
| Task | True | True |
| Task | True | True |
| Copy rust block | True | True |
| Embedded note resolved note | True | True |

axe-windows errors:
- SiblingUniqueAndFocusable: Focusable sibling elements must not have the same Name and LocalizedControlType.
- NameNotNull: The Name property of a focusable element must not be null.

<details><summary>reading-order text</summary>

```
Reading container spike
A paragraph with a resolved note link, an absent note link, a #tag, a citation (Smith, 2020), and bold plus code spans.
Second level heading
•	first bullet
•	second bullet
•	nested bullet
•	ordered one
•	ordered two
•	 an open task
•	 a done task
a block quote with a resolved note link
 
header a	header b
cell 1	cell 2
 
 
```

</details>

## items

Control-type histogram: Text=23, Button=5, Hyperlink=5, CheckBox=2, TitleBar=1, MenuBar=1, MenuItem=1, Pane=1, List=1, DataItem=1, Separator=1

Heading levels exposed:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | focusable | invoke |
|---|---|---|---|
| resolved note | — | False | True |
| absent note | Unresolved link | False | True |
| #tag | — | False | True |
| (Smith, 2020) | Smith, two thousand twenty. | False | True |
| resolved note | — | False | True |

| interactive child | focusable | invoke/toggle |
|---|---|---|
| Minimize | False | True |
| Maximize | False | True |
| Close | False | True |
| Task | True | True |
| Task | True | True |
| Copy rust block | True | True |
| Embedded note resolved note | True | True |

axe-windows errors:
- NameNotNull: The Name property of a focusable element must not be null.
- SiblingUniqueAndFocusable: Focusable sibling elements must not have the same Name and LocalizedControlType.

## Manual AT pass (not covered above)

UIA exposure is necessary but not sufficient: §10.6 makes recorded JAWS + NVDA evidence the pass criterion, and no probe can hear a screen reader. For EACH variant (`ContainerSpike.exe --container flow|items`):

1. **Say-all** (NVDA `Insert+Down`, JAWS `Insert+Down`) from the top — does it read the whole note without stalling at the code block, table or embed card, and in authored order?
2. **Heading nav** (`H`, then `1`/`2`) — does it land on both headings and announce their level?
3. **Link nav** (`K`) — does it reach all links, and is the unresolved one announced differently from the resolved one?
4. **List nav** (`L` to jump to a list, `I` to move by item) — does it work at all, and is the nested item announced with its depth?
5. **Tab** — do the task checkboxes, the code Copy button and the embed card take focus in reading order, and does Space/Enter act on them?
6. **Browse vs focus mode** — does the reader enter browse mode on the surface automatically, and does typing still reach the app?

Record the answers in `w_c_matrix.md` with the AT versions used.
