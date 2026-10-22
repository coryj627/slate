# The math speech and braille golden (#1559)

`expected.txt` pins what slate-core speaks and brailles for math, and the
MathML it produces, which Windows exposes to screen readers through UI
Automation. Every math block in the demo vault, and each expression in
`extra-expressions.txt`, renders through the real pipeline (`render_math`)
under all twelve user-selectable combinations: ClearSpeak or SimpleSpeak,
Terse, Medium or Verbose, and Nemeth or UEB. `tests/math_golden.rs` fails if
any of it changes.

Each entry lists:
- its source;
- its MathML, which depends on none of the settings;
- its six speeches, which depend only on the speech style and verbosity;
- its two braille translations, which depend only on the braille code.

The test renders every combination and fails if any of those assumptions
stops holding. Newlines in a field show as ⏎.

A MathCAT or pulldown-latex upgrade, or a pipeline change, that alters the
output fails the test and prints what changed. When the change is intended,
regenerate the golden and review its diff, because it is what screen-reader
and braille users will now hear and read:

```
SLATE_MATH_GOLDEN_UPDATE=1 cargo test -p slate-core --test math_golden
```

A change to spoken output also needs a VoiceOver field pass
(`docs/runbooks/voiceover-feature-test.md`). Braille correctness itself has
no owner yet: see #1560.

`expected.txt` is generated. Hand edits fail the test.
