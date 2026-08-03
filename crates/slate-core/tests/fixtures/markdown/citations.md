# Citation sites

W4-5 (#737) corpus note. Every citation shape the renderer must handle
appears exactly once below, in a stable order.

A resolved bracketed site [@knuth1984] opens the set.

A multi-key site [@knuth1984; @lamport1994] joins two resolved entries.

An in-text site @lamport1994 carries the author inline.

A suppress-author site [-@knuth1984] drops the name.

A locator site [@knuth1984, p. 12] carries a page.

An unlabelled-locator site [see @lamport1994, ch. 3] yields locator
label "unknown" — the parser does not populate prefix/suffix from this
syntax, and `raw` comes back normalized rather than as the source slice.

An unresolved site [@nosuchkey] has no entry.

A mixed site [@knuth1984; @nosuchkey] resolves one key and not the other.
