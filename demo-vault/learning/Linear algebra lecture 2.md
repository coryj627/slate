# Linear algebra lecture 2

The second lecture in the linear algebra course, covering matrix multiplication, the determinant, and the conditions for matrix invertibility. These three pieces are the prerequisites for the eigenvalue work in [[Linear algebra lecture 3]].

## Matrix multiplication

The product $AB$ of an $m \times n$ matrix $A$ and an $n \times p$ matrix $B$ is an $m \times p$ matrix whose $(i, j)$ entry is the dot product of row $i$ of $A$ with column $j$ of $B$. The inner dimensions must match — the $n$ from $A$ and the $n$ from $B$ — or the product is undefined.

Matrix multiplication is associative ($A(BC) = (AB)C$) and distributes over addition ($A(B + C) = AB + AC$), but it is **not** commutative in general: $AB \ne BA$ for most matrix pairs, even when both products are defined.

## The determinant

The determinant of a square matrix is a scalar that captures how the matrix transforms volume. For a 2×2 matrix:

$$\det \begin{pmatrix} a & b \\ c & d \end{pmatrix} = ad - bc$$

For larger matrices, the determinant is defined recursively via cofactor expansion along any row or column.

## Invertibility

A square matrix $A$ is **invertible** if and only if $\det(A) \ne 0$. When it exists, the inverse $A^{-1}$ satisfies $A A^{-1} = A^{-1} A = I$. A matrix that fails this condition — that is, has determinant zero — is called **singular**.

The next lecture is [[Linear algebra lecture 3]], which uses all three of these pieces.
