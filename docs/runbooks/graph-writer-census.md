# Graph writer census

Issue #1191 closes the IPG-40 gap recorded in
[`35_graph_contracts.md`](../plans/35_graph_contracts.md), C-15(xv) and TGC-18.
The census protects the Windows graph configuration writer's ownership contract.

The filename literal was already counted across the shell. The missing check
was a filesystem mutation outside `Graph/` that reused the existing filename
or path, or used an API spelling the old syntax list did not recognize.

## Contract

- `GraphConfigStore.FileName` owns the single `graph.json` literal.
- The existing writer instance and caller censuses remain in force.
- Graph paths and graph-derived temporary paths are checked across shell source
  files, through bound symbols and source calls rather than directory or API
  spelling alone.
- Mutations belong to the `GraphConfigStore.Write` operation. Its temporary-file
  cleanup through `SafeFile.TryDelete` is part of that operation even though the
  helper's `File.Delete` invocation physically lives in another file.
- A shared helper called with a graph path from another operation does not
  inherit the writer's authorization. Unrelated file writes and read-only graph
  access do not violate writer ownership.

This is a source regression guard for a specific ownership contract, not a
filesystem security boundary or proof about arbitrary runtime-generated paths.
Keep the analyzer's supported propagation and API rules explicit. When a new
graph-path shape or filesystem API is introduced, add a falsification case for
the unauthorized form alongside its intended use.

## Supported analysis and limits

`GraphConfigMutationCensus` binds the shell with `ShellCompilation`. It follows
locals, assignments, fields and properties, source helper arguments and returns,
`ref`/`out` values, source object wrappers (including classic and primary
constructor initializers), arrays, tuples, collection carriers, and task-wrapped
path values. String concatenation/interpolation and path
transforms preserve provenance. Member state and aliases retain graph-path
effects without making every member of a workspace a graph path. Intrinsic
fields and properties keep separate provenance entries; input-dependent object
and collection carriers conservatively combine their possible contents.

Bound `System.IO.File` and `FileInfo` operations distinguish mutation arguments
from read-only sources, including copy destinations, both move paths and replace
backup paths. Stream writers, writable file streams and handle-based
`RandomAccess` operations are covered. Read-only opens must have known safe
options; `DeleteOnClose` is a mutation even with read access. File contents read
from the configuration do not become paths just because the input was a path.

The analysis joins possible flows conservatively: overwriting a graph path does
not necessarily remove its provenance. Ambiguous open options, unclassified
`System.IO` effects with graph inputs, unresolved graph delegate calls, recursive
graph flows and exhausting the context limit fail rather than silently truncate
analysis. Native interop, reflection and arbitrary external effects are outside
this source guard; it is not a general C# or filesystem verifier.

## Validation

Use the normal Release build and generated Release bindings described in
[`CONTRIBUTING.md`](../../CONTRIBUTING.md). The existing
`GraphNavigatorCensus` includes the whole-shell writer check.
`GraphConfigMutationCensusTests` combines a real-shell injected writer with small
compiled mutation fixtures. These distinguish an outside writer using the
existing path from the already-detected duplicate literal, and cover qualified/aliased filesystem
APIs, indirect paths, shared helpers, unrelated writes and read-only access.
