# HTML codeblock sample dummy txt

A small but complete HTML page held inside a single fenced code block. The point of this note is to exercise the `html` language tag through the code-block extraction path, including nested-language quirks where `<script>` and `<style>` blocks contain JavaScript and CSS respectively.

```html
<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Demo page</title>
    <style>
      body { font-family: system-ui, sans-serif; margin: 2rem; line-height: 1.5; }
      article { max-width: 40rem; }
      h1 { font-size: 1.5rem; }
      a { color: #0645ad; }
      a:focus { outline: 2px solid #0645ad; outline-offset: 2px; }
    </style>
  </head>
  <body>
    <main>
      <article>
        <h1>A short article</h1>
        <p>
          This is a tiny static page used to exercise the HTML grammar inside a
          fenced code block. It has just enough structure to be representative
          without becoming distracting.
        </p>
        <p>
          The link below opens in a new tab and is marked as external so that
          assistive technology can announce the change in context:
          <a href="https://example.com/about" target="_blank" rel="noopener noreferrer">
            visit the example site
          </a>.
        </p>
      </article>
    </main>
    <script>
      // Announce page readiness without blocking parse.
      document.addEventListener("DOMContentLoaded", () => {
        console.log("page ready");
      });
    </script>
  </body>
</html>
```

The nested `<style>` and `<script>` blocks are the interesting cases for the tree-sitter HTML grammar — they're islands of CSS and JavaScript inside an HTML document, and the parser should hand them off to the appropriate sub-grammars rather than treating their contents as opaque text. Semantic-span emission should mark tags, attributes, attribute values, and the embedded-language content distinctly.
