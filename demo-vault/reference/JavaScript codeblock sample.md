# JavaScript codeblock sample

A representative ~25-line JavaScript snippet that exercises the tree-sitter JS grammar and the semantic-span emission for the most common constructs: an async arrow function, destructured parameters, a try/catch block, a JSDoc-style comment, and a default export.

```javascript
/**
 * Fetch a user record by id and return a normalized shape.
 *
 * @param {{ id: string, signal?: AbortSignal }} options
 * @returns {Promise<{ id: string, name: string, email: string }>}
 */
const fetchUser = async ({ id, signal }) => {
  try {
    const response = await fetch(`/api/users/${id}`, { signal });
    if (!response.ok) {
      throw new Error(`request failed: ${response.status}`);
    }
    const { id: userId, displayName, contact } = await response.json();
    return {
      id: userId,
      name: displayName ?? "unknown",
      email: contact?.email ?? "",
    };
  } catch (error) {
    if (error.name === "AbortError") {
      return null;
    }
    console.error("fetchUser failed", error);
    throw error;
  }
};

export default fetchUser;
```

The semantic spans of interest are `FunctionDefinition` on the arrow function, `Parameter` on each of the destructured names, `Comment { doc: true }` on the JSDoc block, and `Export` on the default-export line. The `try`/`catch` pair gives the grammar a control-flow construct to disambiguate from ordinary block scopes.
