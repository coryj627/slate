# Linear algebra lecture 3

Lecture notes for the third session of the linear algebra course. The focus today is on eigenvalues and eigenvectors — the central machinery for everything that follows — building on the matrix algebra from the previous lecture and setting up the diagonalization material next time.

## Where we are in the course

A quick orientation before the new material. The previous lecture covered matrix multiplication, the determinant, and the conditions under which a matrix is invertible. If any of that feels shaky, the embedded preview below pulls the relevant definitions forward; the full notes are at [[Linear algebra lecture 2]] for context.

![[Linear algebra lecture 2]]

Today's session — eigenvalues and eigenvectors — is the natural next step. The diagonalization theorem we'll prove next time depends on both pieces being in place, so the lecture after this one (see [[Linear algebra lecture 4]] for the agenda) won't make sense without them.

## Eigenvalues and eigenvectors

The definition. For a square matrix $A$, a nonzero vector $v$ is an **eigenvector** of $A$ if there exists a scalar $\lambda$ such that:

$$A v = \lambda v$$

The scalar $\lambda$ is the corresponding **eigenvalue**. The geometric reading: an eigenvector is a direction that $A$ stretches (or compresses, or reflects) without rotating. The eigenvalue is the factor by which $A$ stretches it. For a precise formal statement of what an eigenvalue is in the general case, see [[Linear algebra glossary#eigenvalue]].

A worked example. Take:

$$A = \begin{pmatrix} 4 & -2 \\ 1 & 1 \end{pmatrix}$$

To find the eigenvalues, solve the characteristic equation $\det(A - \lambda I) = 0$:

$$\det \begin{pmatrix} 4 - \lambda & -2 \\ 1 & 1 - \lambda \end{pmatrix} = (4 - \lambda)(1 - \lambda) - (-2)(1) = \lambda^2 - 5\lambda + 6 = 0$$

Factoring: $(\lambda - 2)(\lambda - 3) = 0$, so $\lambda_1 = 2$ and $\lambda_2 = 3$.

The corresponding eigenvectors come from solving $(A - \lambda I) v = 0$ for each eigenvalue. For $\lambda = 2$, the eigenvector (up to scaling) is $v_1 = (1, 1)^T$. For $\lambda = 3$, it's $v_2 = (2, 1)^T$. The "up to scaling" matters: any nonzero scalar multiple of an eigenvector is also an eigenvector for the same eigenvalue.

## Why this matters

The reason to bother with eigenvalues isn't to compute them by hand on 2×2 matrices forever — most real problems push that work to a numerical library — but because the **set** of eigenvalues of a matrix tells you almost everything that matters about its long-run behavior under repeated application. If you iterate $A$ on a vector, the components along eigenvectors with $|\lambda| > 1$ blow up, the components along eigenvectors with $|\lambda| < 1$ decay to zero, and the components along eigenvectors with $|\lambda| = 1$ persist indefinitely. That single observation is the foundation of Markov chain steady states, PageRank, principal component analysis, and a long list of other techniques that look unrelated until you see the eigenstructure.

We'll formalize this next time and prove the spectral theorem for symmetric matrices, which is the cleanest case and the one that most applications reduce to.

## Reading for next time

The full glossary at [[Linear algebra glossary]] has the formal definitions of everything we touched today. If a term in this note isn't familiar — eigenvalue, eigenvector, characteristic equation, characteristic polynomial — start there.

There's also a supplementary handout I haven't filed yet, which would normally be linked here as [[Linear algebra supplementary]] but that note doesn't exist in the vault and the link should resolve as a broken link until I get around to writing it.
