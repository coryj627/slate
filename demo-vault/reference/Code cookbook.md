# Code cookbook

One fenced code block per language in Slate's V1 set, each 5–10 lines of representative idiomatic code. Three edge cases at the end: a block with no language tag, a block with an unknown language tag, and a block with very long lines.

## JavaScript

```javascript
const debounce = (fn, ms) => {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
};
```

## TypeScript

```typescript
type Result<T, E = Error> = { ok: true; value: T } | { ok: false; error: E };

function parse<T>(input: string, schema: (v: unknown) => T): Result<T> {
  try {
    return { ok: true, value: schema(JSON.parse(input)) };
  } catch (error) {
    return { ok: false, error: error as Error };
  }
}
```

## Python

```python
from functools import lru_cache

@lru_cache(maxsize=None)
def fib(n: int) -> int:
    if n < 2:
        return n
    return fib(n - 1) + fib(n - 2)

if __name__ == "__main__":
    print([fib(i) for i in range(10)])
```

## R

```r
summarize_group <- function(df, group_col, value_col) {
  df |>
    dplyr::group_by({{ group_col }}) |>
    dplyr::summarize(
      mean = mean({{ value_col }}, na.rm = TRUE),
      sd   = sd({{ value_col }}, na.rm = TRUE),
      n    = dplyr::n()
    )
}
```

## Julia

```julia
function trapezoidal(f::Function, a::Real, b::Real, n::Int=100)
    h = (b - a) / n
    s = (f(a) + f(b)) / 2
    for i in 1:(n - 1)
        s += f(a + i * h)
    end
    return h * s
end
```

## Rust

```rust
use std::collections::HashMap;

pub fn word_count(text: &str) -> HashMap<String, usize> {
    let mut counts = HashMap::new();
    for word in text.split_whitespace() {
        *counts.entry(word.to_lowercase()).or_insert(0) += 1;
    }
    counts
}
```

## Go

```go
package main

import "fmt"

func fibonacci(n int) []int {
    seq := make([]int, n)
    for i := range seq {
        if i < 2 {
            seq[i] = i
        } else {
            seq[i] = seq[i-1] + seq[i-2]
        }
    }
    return seq
}

func main() {
    fmt.Println(fibonacci(10))
}
```

## Java

```java
import java.util.List;
import java.util.stream.Collectors;

public class Words {
    public static List<String> longWords(List<String> input, int minLength) {
        return input.stream()
            .filter(s -> s.length() >= minLength)
            .map(String::toLowerCase)
            .collect(Collectors.toList());
    }
}
```

## C++

```cpp
#include <vector>
#include <numeric>
#include <iostream>

int main() {
    std::vector<int> v{1, 2, 3, 4, 5};
    auto sum = std::accumulate(v.begin(), v.end(), 0);
    std::cout << "sum = " << sum << '\n';
    return 0;
}
```

## Swift

```swift
import Foundation

func loadData(from url: URL) async throws -> Data {
    let (data, response) = try await URLSession.shared.data(from: url)
    guard let http = response as? HTTPURLResponse, http.statusCode == 200 else {
        throw URLError(.badServerResponse)
    }
    return data
}
```

## HTML

```html
<article>
  <h2>Section title</h2>
  <p>A short paragraph with an <a href="https://example.com">inline link</a>.</p>
  <ul>
    <li>First item</li>
    <li>Second item</li>
  </ul>
</article>
```

## YAML

```yaml
name: build
on:
  push:
    branches: [main]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: make test
```

## TOML

```toml
[package]
name = "demo"
version = "0.1.0"
edition = "2021"

[dependencies]
serde = { version = "1.0", features = ["derive"] }
tokio = { version = "1.0", features = ["full"] }
```

## JSON

```json
{
  "name": "demo",
  "version": "0.1.0",
  "scripts": {
    "build": "tsc",
    "test": "vitest"
  },
  "dependencies": {
    "zod": "^3.22.0"
  }
}
```

## SQL

```sql
SELECT u.id, u.email, COUNT(o.id) AS order_count
FROM users u
LEFT JOIN orders o ON o.user_id = u.id
WHERE u.created_at >= DATE('now', '-30 days')
GROUP BY u.id, u.email
ORDER BY order_count DESC
LIMIT 10;
```

## Bash

```bash
#!/usr/bin/env bash
set -euo pipefail

target="${1:-.}"
find "$target" -type f -name '*.md' -print0 \
  | xargs -0 wc -l \
  | sort -nr \
  | head -20
```

## CSS

```css
:root {
  --text: #1a1a1a;
  --bg: #fafafa;
  --accent: #0645ad;
}

body {
  font: 1rem/1.6 system-ui, sans-serif;
  color: var(--text);
  background: var(--bg);
}

a:focus-visible {
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}
```

## LaTeX

```latex
\documentclass{article}
\usepackage{amsmath}
\begin{document}

The quadratic formula:
\begin{equation}
  x = \frac{-b \pm \sqrt{b^2 - 4ac}}{2a}
\end{equation}

\end{document}
```

## Edge cases

### No language tag (plain-text fallback)

```
plain text in a fenced block with no language tag.
the renderer should fall back to monospace formatting
without attempting any syntax highlighting at all.
no semantic spans are expected.
```

### Unknown language tag (syntect fallback)

```esoteric
;; an invented language called 'esoteric' that the tree-sitter
;; primary path won't recognize. the expected behavior is to
;; fall through to syntect, which may or may not have a grammar
;; for it; either way, the failure mode should be graceful.
(define (greet name) (display (string-append "hello, " name)))
```

### Very long lines (wrap behavior)

```javascript
const veryLongIdentifierName = (firstArgumentWithAVeryLongName, secondArgumentThatIsAlsoQuiteLong, thirdArgumentForGoodMeasure) => { return firstArgumentWithAVeryLongName + secondArgumentThatIsAlsoQuiteLong + thirdArgumentForGoodMeasure; };
const anotherVeryLongLine = "this is a string literal that goes on for a very long time without any line breaks because the author of this code apparently does not believe in formatting conventions of any kind whatsoever and just lets the line run on indefinitely";
```
