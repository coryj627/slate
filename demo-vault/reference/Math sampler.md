# Math sampler

A consolidated test note for the math pipeline, covering inline math, display math, environment-based forms (aligned and matrix), an integral with limits, and one intentionally malformed expression to exercise the graceful-failure path.

## Inline math

The simplest form. Inline math sits inside a paragraph and renders at text height, suitable for short expressions that read as part of the sentence. For example, the kinetic energy of a body of mass $m$ moving at velocity $v$ is $E_k = \tfrac{1}{2} m v^2$. The expression flows with the surrounding prose, which is the entire point of inline as a distinct mode.

A second inline example for good measure: the area of a circle of radius $r$ is $A = \pi r^2$, where $\pi$ is the usual constant.

## Display math

Display math sits in its own block, centered, at full height. Used for expressions that are too tall for inline (anything with a built-up fraction, summation, or integral) or that the author wants to set apart visually.

$$\sum_{i=0}^{n} i^2 = \frac{n(n+1)(2n+1)}{6}$$

The result above — the closed form for the sum of the first $n$ squares — is one of those identities that's easier to remember in display form than as a single inline run, because the structure of the right-hand side is itself part of the content.

## Aligned environment

Multi-line derivations live in an aligned environment, where each line shares an alignment column (almost always the equals sign). Three steps of an algebraic manipulation:

$$
\begin{align*}
(x + 1)^2 - (x - 1)^2
  &= \left(x^2 + 2x + 1\right) - \left(x^2 - 2x + 1\right) \\
  &= x^2 + 2x + 1 - x^2 + 2x - 1 \\
  &= 4x
\end{align*}
$$

The alignment column is set by the `&` marker. The `align*` variant (with the star) suppresses equation numbering, which is the right default for a note like this where the steps aren't being referenced from elsewhere.

## Matrix

A 3×3 matrix using `pmatrix`, which surrounds the entries with parentheses. Other variants (`bmatrix`, `vmatrix`, etc.) use square brackets, vertical bars, and so on, but `pmatrix` is the most general-purpose.

$$
A = \begin{pmatrix}
1 & 2 & 3 \\
4 & 5 & 6 \\
7 & 8 & 9
\end{pmatrix}
$$

The matrix above is singular — its rows are linearly dependent — which is unfortunate as an example matrix but harmless for the purposes of testing the renderer.

## Integral with limits

Definite integrals are written with limits attached to the integral sign. The expression below reads as "the integral from $a$ to $b$ of $f(x)$ with respect to $x$":

$$\int_a^b f(x)\,dx$$

The `\,` between $f(x)$ and $dx$ is a thin space — small enough that it barely registers visually, but conventional in mathematical typography to separate the integrand from the differential.

## Intentionally malformed

The expression below is missing a closing brace on the `\frac` argument and should fail to parse:

$\frac{a$

The expected behavior is that `math.rs` catches the parse failure, surfaces it as an error (rather than crashing or silently producing nonsense), and the rest of the note continues to render normally. This is the same pattern any robust parser uses for malformed input: localize the failure, report it, and keep going.
