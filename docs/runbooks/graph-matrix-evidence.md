# Graph matrix evidence

Issue #1192 closes the IPG-42 gap recorded in
[`35_graph_contracts.md`](../plans/35_graph_contracts.md), TGC-18.
`WcMatrixGraphEvidenceCensus` checks the graph rows in
[`w_c_matrix.md`](../plans/18_windows_port/w_c_matrix.md).

## Contract

Evidence names must resolve against the test projects' compiled inputs and
their standard xUnit `Fact` and `Theory` symbols. A method name denotes a
runnable test declaration; a class or test-file name contains such tests.
A comment, inactive preprocessor
arm, helper without a test entry point, or lookalike attribute cannot substitute
for that evidence. Custom attributes and discoverers are not inferred from
inheritance alone: their constructors or discovery rules could skip a test.
The admitted attributes must come from the referenced `xunit.core` assembly. Test
classes and theory data providers must have supported constructor, argument and
row shapes; matching a provider's name or parameter count is insufficient.
Fixture references continue to name existing fixture files.

This validates the supported declaration and data-provider shapes semantically;
it does not invoke xUnit's discoverer or execute a theory's data provider. Runtime
provider behavior remains the responsibility of the test runner, like other
runtime conditions in a test body.

An axe label must occur in a bound call to the shell journey's scan helper,
reachable from an executable test. Merely quoting the call, adding an uncalled
helper or declaring an unused lambda does not establish evidence. Bound helper
calls must preserve which overload and label are reached.

Static reachability means the test can reach the scan through the source call
graph. It does not establish that every runtime condition permits the scan, or
that an accessibility journey has passed. The actual Windows accessibility CI
gate remains responsible for execution, and the matrix's human verification
cells retain their own recorded-run requirements.

Direct source helpers, local functions and callbacks invoked by source helpers
are supported, including argument binding into helper and callback parameters.
Known boolean guards exclude unreachable scans; unknown runtime conditions
retain potential reachability. Ambiguous virtual dispatch, unused delegate bodies and
compiler-omitted conditional calls cannot certify a scan. Runtime reflection
and external callback scheduling are outside this static analysis.

## Build and maintenance

Use the normal Release build and generated Release bindings described in
[`CONTRIBUTING.md`](../../CONTRIBUTING.md). Evidence analysis must use the same
compile inputs, references, language version and preprocessor symbols as the
test projects. Missing or stale build evidence must produce an actionable
failure rather than silently scan a different configuration.

`EvidenceCompilation.targets` records each test project's inputs after a build.
The unit-test project builds the accessibility project as a dependency so the
normal project-local build also produces both manifests. `TestEvidenceTests`
contains the isolated mutation cases; `WcMatrixGraphEvidenceCensus` verifies the
real graph rows against both projects.

When extending evidence forms or call shapes, add a positive witness and a
falsification case demonstrating that a non-executed name cannot satisfy a row.
Keep the separate canvas census's existing behavior intact unless that change
has its own reviewed scope.
