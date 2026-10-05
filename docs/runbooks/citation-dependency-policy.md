# Temporary citation dependency admission

Slate currently admits exactly one Cargo Git package: citationberg 0.7.0 from
`https://github.com/typst/citationberg`, at immutable revision
`06a591e2f237d25e1dfdedac3f3d1494c496c52d`. Both the workspace patch and committed
lockfile name that full revision. This is a temporary source admission, **not a
vulnerability exception**. `.cargo/audit.toml` has no active advisory ignores;
the existing review-by enforcement remains unchanged.

## Why and what was reviewed

The published citationberg 0.7.0 still selects quick-xml 0.38.4, affected by
RUSTSEC-2026-0194 (attribute-checking CPU DoS) and RUSTSEC-2026-0195 (namespace
allocation DoS). Shared/downloaded CSL is untrusted input; treating this as only
an attacker editing their own files is insufficient. The upstream revision
above selects quick-xml 0.41.0 without changing citationberg's Rust API/code.
[Tracking issue #480](https://github.com/coryj627/slate/issues/480) retains the
historical exception and upstream-release context; its old mitigation text
does not describe today's ignore-free workspace.

The published crate archive SHA-256 was independently verified as
`756ff1e3d43a9ecc8183932fb4d9fd3971236f3ce4acb62fe51d1cd43297547d`.
Its VCS parent is `25c66d5f79d919fb8446ef33075a70977066e82c`. The 20 packaged
files match that parent when the original manifest is compared using
`Cargo.toml.orig`; Cargo-generated archive metadata is separate. A complete
Git diff from that parent to the admitted revision changes exactly one line:

```diff
-quick-xml = { version = "0.38.1", features = ["serialize", "overlapped-lists"] }
+quick-xml = { version = "0.41.0", features = ["serialize", "overlapped-lists"] }
```

The new Git pin does not have a crates.io package checksum. Git packages are
identified by their locked commit instead; an exact pin does not float on each
build. It does introduce a Git fetch/cache requirement. The source review and
CI admission below mitigate this difference; they do not make Git equivalent
to registry distribution or constitute a general supply-chain attestation.

## CI enforcement and reproduction

The existing Security audit workflow runs rejection controls and
`python3 scripts/verify_citation_dependency.py` before the fresh cargo-audit
scan. Manifest, lock, toolchain, policy-script and policy-test changes trigger
this gate; the existing weekly run remains. The checker:

- requires the exact manifest URL/revision and the same sole Git package in
  both the lock and `cargo metadata --locked --all-features --format-version 1`;
- checks the fetched citationberg checkout's actual HEAD and tracked-file
  cleanliness;
- requires one quick-xml package, version 0.41.0 from crates.io, with the reviewed
  registry checksum `e660451e55124f798a69a5af3f49ccfbefbd41910eefd25caf2393e1f3473ec1`;
- verifies normal dependency paths from both workspace shipping roots,
  slate-uniffi and slate-cli, to citationberg and quick-xml. A dev/build-only or
  disconnected parser does not satisfy the check. Metadata covers the union
  of all targets/features; it does not prove each compiled native target or
  default-feature build. The currently reviewed paths are target-unconditional;
- refuses any manifest/lock mutation. It prints the actual resolved paths;
  it does not compile an app or replace native or RustSec validation.

cargo-audit 0.22.2 matches advisories only against crates.io sources, so a scan
of the committed lock never examines the Git citationberg; a synthetic advisory
against citationberg 0.7.0 failed the base lock and passed this lock. With
`--audit-lock <path>`, the checker also writes a lock copy that differs only in
naming the admitted revision by its published registry identity, and the gate
runs a second `cargo audit --file` on that copy. The revision is published 0.7.0
plus the reviewed quick-xml line, so any citationberg 0.7.0 advisory applies to
it unchanged and fails the gate. That synthetic advisory fails the copy.

Use the repository-pinned Rust and Python 3.11+ to reproduce:

```sh
python3 -m unittest discover -s scripts -p test_citation_dependency.py
python3 scripts/verify_citation_dependency.py --audit-lock target/citation-audit/Cargo.lock
cargo audit
cargo audit --no-fetch --file target/citation-audit/Cargo.lock
```

After fetching the complete locked dependencies, the same check supports
`--offline`. A fresh disconnected machine needs **both** the registry and Git
sources, plus its toolchain and native prerequisites, supplied beforehand.
Registry-only cache preparation is insufficient. Downstream packagers that
require vendored inputs should use Cargo's vendor/source-replacement workflow
to prepare and review the complete locked workspace. This checker requires
the original fetched Git checkout and does not validate a vendored or
source-replaced tree; packagers must qualify that configuration separately.
No new third-party packager's Git/offline support is claimed here. Repository Mac and Windows
builds already qualified this dependency at revision 231b4852, with their exact
native artifact provenance preserved in the handoff.

## Removal milestone

At the first compatible **registry-published** citationberg/citation-chain
release that admits a fixed quick-xml, remove the Git patch, commit the registry
lock/source, and update this explicit admission gate in the same reviewed
change. Require the namespace-boundary regression, fresh audit, complete Rust
and native Mac/Windows qualification, and appropriate accessibility acceptance.
The upstream release alone does not automatically change this lock. Do not
restore the advisory ignores or close #480 as registry migration before that
qualification is complete.
