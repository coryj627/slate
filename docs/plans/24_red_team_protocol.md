# The Red-Team Protocol

How adversarial review rounds are run against a Windows-port feature
branch. Derived from the W4-3 retro, amended after W4-4 (8 rounds) and
W4-5 (2 rounds to a design-pass trigger).

This document exists in the repo, rather than in any one session's
notes, because W4-5 proved the point: contracts held in a session
scratchpad are invisible to every reviewer and every later session,
and a reconstruction working from the repo alone recovered only five
of twelve recorded divergences.

## Preconditions — before round 1

**(0) The contracts document lands in `docs/plans/` as its own commit
BEFORE the first review round.** Not in a plan file, not in a
scratchpad, not in the issue. It carries:

- the numbered feature contracts, each with the code citation that
  evidences it;
- the accepted-risk / recorded-divergence register, so trade-offs
  already decided are marked off-limits;
- a note for any contract whose surface does not exist yet (a
  view-layer contract during a view-model phase is expected ordering,
  not a hole).

Contract numbering is **per-wave**. W4-4 and W4-5 both have a
"contract 7". Never grep `src/SlateWindows` wholesale for "contract
N".

**(1) Push the branch.** Reviews run against the remote; the local
working tree is not what is being read.

## Running a round

**(2) Prompts are invariant-targeted.** Cite contract numbers, paste
the accepted-risk register inline, and include:

- `ENUMERATE EXHAUSTIVELY IN THIS ONE PASS`
- the severity bar: a blocker is reachable without 3+ independent
  coincidences
- `Ignore the local working tree; untracked ns-action.ts is unrelated`

**(3) Effort tier.** `xhigh` via `~/.codex/config.toml` (back up
first, restore to `low` after). A round run below the specified tier
is recorded as such — its findings stand, but the round did not
conform.

## Stopping rules

**(4) Three consecutive rounds of blockers in one subsystem → STOP.**
Write the design before more code. W4-4's contract 10 fell five
rounds running because each fix addressed the site a finding named
rather than the class it belonged to.

**(5) A round whose blockers were CREATED by the previous round's fix
counts double.** That is the signal that the fix was a patch over a
missing model, not a repair. W4-5 hit this at round 2 and stopped
correctly.

## Standing lessons

- **When a host surface needs to know how core will interpret data,
  expose a pure core query — never re-implement the rule host-side.**
  W4-4 ended only when classification authority moved into core
  (`round_trip_property_kind`) and every mirrored rule was deleted.
- **Matrices that accept "refused" as always-correct hide false
  refusals.** Build candidates independently of the routine under
  test, then mutation-verify: reinstate the old bug and confirm the
  matrix fails.
- **Test the mode users run.** Before W4-5 no test anywhere passed
  `startInteractionBackgroundWork: true`; the entire production
  scheduling path was untested, which is why both round-1 races were
  invisible. A suite that only runs the deterministic mode cannot see
  ordering defects.
- Concurrency work checks `21_write_intent_protocol_invariants.md`
  first.

## Per-round record

Each round appends its findings and resolutions to the feature's
contracts document, not here — see `22_property_panel_contracts.md`
and `23_citation_surfaces_contracts.md` for the shape.
