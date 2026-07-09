// Demo JS attachment for the preserve-unknown test path.
//
// Slate should preserve this file as-is: not parse it as a note, not index
// it into the FTS5 corpus, not delete or move it. The file tree should show
// it as an attachment, and any link or embed in a markdown note should
// resolve to the file's location on disk.

const greet = (name) => `hello, ${name}`;

if (typeof window !== "undefined") {
  document.addEventListener("DOMContentLoaded", () => {
    console.log(greet("world"));
  });
}

export default greet;
