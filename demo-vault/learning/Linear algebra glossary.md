# Linear algebra glossary

A short reference for the terms used across the course. Definitions only — see the lecture notes themselves ([[Linear algebra lecture 2]], [[Linear algebra lecture 3]], [[Linear algebra lecture 4]]) for derivations and worked examples.

## determinant

A scalar associated with a square matrix that captures the signed volume scaling factor of the linear map it represents. A matrix is invertible if and only if its determinant is nonzero.

## eigenvalue

A scalar $\lambda$ such that, for some nonzero vector $v$, the matrix $A$ satisfies $Av = \lambda v$. The vector $v$ is the corresponding eigenvector. The set of all eigenvalues of $A$ is called the spectrum of $A$.

Eigenvalues are the roots of the characteristic polynomial $\det(A - \lambda I)$. For an $n \times n$ matrix, the characteristic polynomial has degree $n$, so $A$ has at most $n$ distinct eigenvalues. Counted with algebraic multiplicity, there are exactly $n$ eigenvalues over the complex numbers.

## eigenvector

A nonzero vector $v$ such that $A v = \lambda v$ for some scalar $\lambda$ — the corresponding eigenvalue. Eigenvectors are unique only up to nonzero scaling: any scalar multiple of an eigenvector is also an eigenvector for the same eigenvalue.

## characteristic polynomial

For an $n \times n$ matrix $A$, the polynomial $p(\lambda) = \det(A - \lambda I)$. Its roots are the eigenvalues of $A$.

## diagonalizable

A matrix $A$ is diagonalizable if there exists an invertible matrix $P$ and a diagonal matrix $D$ such that $A = P D P^{-1}$. Equivalently, $A$ is diagonalizable if and only if it has $n$ linearly independent eigenvectors.

## orthogonal matrix

A square matrix $Q$ whose columns (and rows) form an orthonormal basis, equivalently $Q^T Q = I$. Orthogonal matrices preserve lengths and angles. The spectral theorem says that every real symmetric matrix is diagonalizable by an orthogonal matrix.

## symmetric matrix

A square matrix $A$ such that $A^T = A$. Symmetric matrices have real eigenvalues and a full set of orthogonal eigenvectors — the cleanest case for diagonalization.
