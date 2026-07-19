# Code fixture

A fenced block with a known grammar:

```rust
fn main() {
    let greeting = "hello";
    // a comment token
    println!("{greeting}, world: {}", 40 + 2);
}
```

An indented continuation paragraph, then a fence with no language:

```
plain fence body
```

Inline `let x = 1;` code closes the file.
