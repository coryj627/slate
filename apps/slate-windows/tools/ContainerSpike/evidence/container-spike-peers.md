# W3-1 container spike — provider-side (AutomationPeer) measurements

Measured in-process from the WPF `AutomationPeer` tree, so it runs without the interactive desktop a UIA client probe requires. Client-side behaviour (focus order, axe, and what JAWS/NVDA actually say) is NOT covered — see `ContainerSpikeProbe` and the manual script.

| measurement | flow/detached | flow/windowed | flowpeers/detached | flowpeers/windowed | richtext/detached | richtext/windowed | items/detached | items/windowed |
|---|---|---|---|---|---|---|---|---|
| surface control type | Document | Document | Document | Document | Document | Document | Pane | Pane |
| peers in tree | 16 | 16 | 36 | 36 | 34 | 34 | 38 | 38 |
| Text provider present | **yes** | **yes** | **yes** | **yes** | **yes** | **yes** | **no** | **no** |
| Text provider host | Document | Document | Document | Document | Document | Document | — | — |
| reading-order chars | 363 | 363 | 363 | 363 | 363 | 363 | 0 | 0 |
| landmarks found | 12/14 | 12/14 | 12/14 | 12/14 | 12/14 | 12/14 | 0/14 | 0/14 |
| landmarks missing | fn main, Embedded note | fn main, Embedded note | fn main, Embedded note | fn main, Embedded note | fn main, Embedded note | fn main, Embedded note | Reading container spike, resolved note, absent note, Second level heading, first bullet, nested bullet, ordered one, an open task, a done task, block quote, fn main, header a, cell 1, Embedded note | Reading container spike, resolved note, absent note, Second level heading, first bullet, nested bullet, ordered one, an open task, a done task, block quote, fn main, header a, cell 1, Embedded note |
| landmarks out of order | none | none | none | none | none | none | none | none |
| List peers | 0 | 0 | 2 | 2 | 2 | 2 | 1 | 1 |
| ListItem peers | 0 | 0 | 7 | 7 | 7 | 7 | 0 | 0 |
| HeadingLevel survives | **no** | **no** | **yes** | **yes** | **yes** | **yes** | **yes** | **yes** |
| hyperlink peers | 0 | 0 | 5 | 5 | 5 | 5 | 5 | 5 |
| links w/ HelpText | 0 | 0 | 2 | 2 | 2 | 2 | 2 | 2 |
| interactive children | 6 | 6 | 10 | 10 | 8 | 8 | 4 | 4 |

## flow/detached

Control types: Button=4, Text=3, Document=2, CheckBox=2, ScrollBar=2, Pane=1, Separator=1, Slider=1

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |
| Decrease Zoom | True |
| Increase Zoom | True |

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

## flow/windowed

Control types: Button=4, Text=3, Document=2, CheckBox=2, ScrollBar=2, Pane=1, Separator=1, Slider=1

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |
| Decrease Zoom | True |
| Increase Zoom | True |

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

## flowpeers/detached

Control types: ListItem=7, CheckBox=6, Text=5, Hyperlink=5, Button=4, Document=2, ScrollBar=2, List=2, Pane=1, Separator=1, Slider=1

Heading levels on peers:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | invoke provider |
|---|---|---|
| resolved note | — | True |
| absent note | Unresolved link | True |
| #tag | — | True |
| (Smith, 2020) | Smith, two thousand twenty. | True |
| resolved note | — | True |

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |
| Decrease Zoom | True |
| Increase Zoom | True |
| Task: an open task | True |
| Task: a done task | True |
| Task: an open task | True |
| Task: a done task | True |

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

## flowpeers/windowed

Control types: ListItem=7, CheckBox=6, Text=5, Hyperlink=5, Button=4, Document=2, ScrollBar=2, List=2, Pane=1, Separator=1, Slider=1

Heading levels on peers:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | invoke provider |
|---|---|---|
| resolved note | — | True |
| absent note | Unresolved link | True |
| #tag | — | True |
| (Smith, 2020) | Smith, two thousand twenty. | True |
| resolved note | — | True |

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |
| Decrease Zoom | True |
| Increase Zoom | True |
| Task: an open task | True |
| Task: a done task | True |
| Task: an open task | True |
| Task: a done task | True |

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

## richtext/detached

Control types: ListItem=7, CheckBox=6, Hyperlink=5, Text=5, Custom=4, Button=2, List=2, Document=1, Table=1, Separator=1

Heading levels on peers:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | invoke provider |
|---|---|---|
| resolved note | — | True |
| absent note | Unresolved link | True |
| #tag | — | True |
| (Smith, 2020) | Smith, two thousand twenty. | True |
| resolved note | — | True |

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |
| Task: an open task | True |
| Task: a done task | True |
| Task: an open task | True |
| Task: a done task | True |

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

## richtext/windowed

Control types: ListItem=7, CheckBox=6, Hyperlink=5, Text=5, Custom=4, Button=2, List=2, Document=1, Table=1, Separator=1

Heading levels on peers:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | invoke provider |
|---|---|---|
| resolved note | — | True |
| absent note | Unresolved link | True |
| #tag | — | True |
| (Smith, 2020) | Smith, two thousand twenty. | True |
| resolved note | — | True |

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |
| Task: an open task | True |
| Task: a done task | True |
| Task: an open task | True |
| Task: a done task | True |

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

## items/detached

Control types: Text=23, Hyperlink=5, CheckBox=2, Button=2, ScrollBar=2, Pane=1, List=1, DataItem=1, Separator=1

Heading levels on peers:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | invoke provider |
|---|---|---|
| resolved note | — | True |
| absent note | Unresolved link | True |
| #tag | — | True |
| (Smith, 2020) | Smith, two thousand twenty. | True |
| resolved note | — | True |

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |

## items/windowed

Control types: Text=23, Hyperlink=5, CheckBox=2, Button=2, ScrollBar=2, Pane=1, List=1, DataItem=1, Separator=1

Heading levels on peers:
- Level1: Reading container spike
- Level2: Second level heading

| hyperlink | HelpText | invoke provider |
|---|---|---|
| resolved note | — | True |
| absent note | Unresolved link | True |
| #tag | — | True |
| (Smith, 2020) | Smith, two thousand twenty. | True |
| resolved note | — | True |

| interactive child | invoke/toggle provider |
|---|---|
| Task: an open task | True |
| Task: a done task | True |
| Copy rust block | True |
| Embedded note resolved note | True |

