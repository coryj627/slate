# Inline styles fixture

Emphasis spanning a token: **strong [[basic]] tail** and *emphasis #tag tail*
and ~~struck [@smith2020] tail~~.

Code suppression: `[[not a link]]` and `#nottag` and `[@notacite]`.

```text
[[fenced link]] and #fencedtag
```

Delimiters inside a token never pair outside it: [[a*b]] * not emphasis *.

Two adjacent identical tokens stay two runs: [[basic]][[basic]].
