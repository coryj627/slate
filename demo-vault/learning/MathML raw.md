# MathML raw

A test note for the raw-MathML-in-source passthrough path. The block below is hand-authored MathML, not LaTeX rendered to MathML — so it bypasses the pulldown-latex parser and lands directly in the MathML representation that MathCAT consumes for speech and braille.

The expression is the quadratic formula, written out longhand as MathML elements:

<math xmlns="http://www.w3.org/1998/Math/MathML" display="block">
  <mrow>
    <mi>x</mi>
    <mo>=</mo>
    <mfrac>
      <mrow>
        <mo>&#x2212;</mo>
        <mi>b</mi>
        <mo>&#x00B1;</mo>
        <msqrt>
          <mrow>
            <msup>
              <mi>b</mi>
              <mn>2</mn>
            </msup>
            <mo>&#x2212;</mo>
            <mn>4</mn>
            <mi>a</mi>
            <mi>c</mi>
          </mrow>
        </msqrt>
      </mrow>
      <mrow>
        <mn>2</mn>
        <mi>a</mi>
      </mrow>
    </mfrac>
  </mrow>
</math>

This is the kind of source you'd rarely write by hand — it's verbose enough to be a chore — but it's important to support because it's the lingua franca for math interchange between systems that don't share a LaTeX dialect. MathML is also what MathCAT consumes internally, so the raw-MathML path is the shortest one through the pipeline.

The expected behavior is that Slate recognizes the `<math>` element as a math block, hands it straight to MathCAT (skipping the LaTeX-to-MathML stage), and emits the same speech and braille artifacts it would for a LaTeX expression that produced equivalent MathML.
