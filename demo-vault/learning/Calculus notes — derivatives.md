# Calculus notes — derivatives

Lecture notes covering the two most-used differentiation rules — the chain rule and the product rule — and the derivatives of the handful of common functions that show up in almost every problem. The math is written as LaTeX source; Slate renders it to MathML with MathCAT-generated speech and Nemeth/UEB braille.

## The chain rule

The chain rule handles composition. If $y = f(g(x))$, then the derivative of $y$ with respect to $x$ is the derivative of the outer function evaluated at the inner function, times the derivative of the inner function:

$$\frac{dy}{dx} = f'(g(x)) \cdot g'(x)$$

In Leibniz notation, with $u = g(x)$, this reads more cleanly:

$$\frac{dy}{dx} = \frac{dy}{du} \cdot \frac{du}{dx}$$

The Leibniz form is the one I reach for first when working a problem by hand, because the chain becomes literal — you can see the intermediate variable doing its job. The Lagrange notation ($f'$, $g'$) is more compact and what I use when writing out a final answer.

A worked example. Let $y = \sin(x^2 + 1)$. Set $u = x^2 + 1$, so $y = \sin(u)$. Then $\frac{dy}{du} = \cos(u)$ and $\frac{du}{dx} = 2x$, giving:

$$\frac{dy}{dx} = \cos(x^2 + 1) \cdot 2x = 2x \cos(x^2 + 1)$$

The chain rule generalizes to any depth of composition. For $y = f(g(h(x)))$:

$$\frac{dy}{dx} = f'(g(h(x))) \cdot g'(h(x)) \cdot h'(x)$$

In practice anything past two levels of composition is a sign to introduce a substitution and work it as two separate chain-rule applications, because keeping track of which prime applies to which function gets error-prone fast.

## The product rule

The product rule handles multiplication of two functions. If $y = u(x) \cdot v(x)$, then:

$$\frac{dy}{dx} = u'(x) \cdot v(x) + u(x) \cdot v'(x)$$

The mnemonic I use is "first times derivative of second, plus second times derivative of first" — though the order doesn't matter since addition is commutative, the symmetry of the formula is what makes it memorable.

A worked example. Let $y = x^2 \sin(x)$. With $u = x^2$ and $v = \sin(x)$:

$$\frac{dy}{dx} = 2x \sin(x) + x^2 \cos(x)$$

For three factors, the product rule extends by linearity. If $y = u \cdot v \cdot w$:

$$\frac{dy}{dx} = u' v w + u v' w + u v w'$$

The pattern generalizes: for $n$ factors, the derivative is a sum of $n$ terms, each with exactly one factor differentiated.

## Common derivatives

The derivatives that come up enough to commit to memory. None of these are derived here — they all follow from the limit definition or from previous rules — but having them at the front of recall saves real time during problem-solving.

Polynomial:

$$\frac{d}{dx}\left[x^n\right] = n x^{n-1}$$

Trigonometric:

$$\frac{d}{dx}\left[\sin(x)\right] = \cos(x)$$

$$\frac{d}{dx}\left[\cos(x)\right] = -\sin(x)$$

$$\frac{d}{dx}\left[\tan(x)\right] = \sec^2(x)$$

Exponential and logarithmic:

$$\frac{d}{dx}\left[e^x\right] = e^x$$

$$\frac{d}{dx}\left[\ln(x)\right] = \frac{1}{x}$$

$$\frac{d}{dx}\left[a^x\right] = a^x \ln(a)$$

Inverse trig (the two that show up most often):

$$\frac{d}{dx}\left[\arcsin(x)\right] = \frac{1}{\sqrt{1 - x^2}}$$

$$\frac{d}{dx}\left[\arctan(x)\right] = \frac{1}{1 + x^2}$$

## Combining rules

Real problems almost always combine the chain rule, the product rule, and a few common derivatives. The discipline is to identify the outermost structure first — is this fundamentally a product, a composition, a quotient? — and then peel inward.

Example: differentiate $y = x^2 \cdot e^{\sin(x)}$. The outermost structure is a product of $x^2$ and $e^{\sin(x)}$. Apply the product rule:

$$\frac{dy}{dx} = 2x \cdot e^{\sin(x)} + x^2 \cdot \frac{d}{dx}\left[e^{\sin(x)}\right]$$

The remaining derivative is a composition — $e$ to the $\sin(x)$ — so apply the chain rule with $u = \sin(x)$:

$$\frac{d}{dx}\left[e^{\sin(x)}\right] = e^{\sin(x)} \cdot \cos(x)$$

Substituting back:

$$\frac{dy}{dx} = 2x e^{\sin(x)} + x^2 \cos(x) e^{\sin(x)} = e^{\sin(x)}\left(2x + x^2 \cos(x)\right)$$

The factored form on the right is what I'd write as a final answer. Factoring out the common $e^{\sin(x)}$ isn't required, but it usually makes the next step — evaluation, integration, whatever follows — easier.
