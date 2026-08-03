---
title: [unclosed
  bad yaml: : :
---

# Malformed properties

An unparseable frontmatter block must yield an EMPTY property list —
never an error and never a partial parse (W4-4). Duplicate keys
poison the block the same way (pinned by the sibling fixture's
history: they may not appear in properties_metadata.md).
