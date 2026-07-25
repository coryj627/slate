# Markdown destination fixture

Internal destinations: [plain](basic.md), [anchored](basic.md#Heading Two),
[caret path](note^block), [percent escaped](my%20note.md).

Never activatable: [js](javascript:alert(1)), [file](file:///etc/passwd),
[proto relative](//host/path), [fragment](#intro), [unknown](ftp://host/x).

Allowlisted external: [https](https://example.com), [mailto](mailto:a@b.c).

A token inside a label stays literal: [about #intro](basic.md).
