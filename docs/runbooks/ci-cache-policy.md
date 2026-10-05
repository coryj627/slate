# CI cache policy and provider verification

Namespace custom tags share a volume across profiles and repositories in the
same workspace. Main and PR profile names therefore do not isolate the custom
`slate-windows-rust`, `slate-windows-app`, `slate-windows-model`, `slate-arm64`, or
`slate-mac` tags. This supersedes the earlier workflow comments claiming distinct
per-profile lineages for the same tag.

Workflow routing and optional job labels cannot enforce a boundary against a PR
that controls its YAML. Rely on a provider policy only after verifying its scope
across every profile, repository and label path capable of attaching the tag.
[Namespace cache identity and protection](https://namespace.so/docs/solutions/github-actions/caching),
[runner labels](https://namespace.so/docs/reference/github-actions/runner-configuration).

## Establish enforcement before a storage transition

1. Record current tags, requested capacity, working-set peaks, last use, effective
   branch commit policy, and all consumers. Confirm with Namespace whether a
   branch restriction is enforced when the same tag is requested through another
   profile or direct runner labels. Do not treat a disabled optional label as proof.
2. Use harmless provenance markers in a disposable test tag. A trusted run should
   persist its marker; an untrusted PR run may modify its private fork but must
   not persist that marker. Exercise the alternate attachment paths too. Inspect
   a later trusted mount to establish persistence behavior.
3. Once the enforced policy is established, perform a planned drained transition.
   Disable repository Actions, enumerate all paginated runs and drain every status
   other than `completed`, verify zero active runs, apply the enforced policy, and
   reset affected generations. Keep Actions disabled through the reset so an old
   rerun cannot promote an older unprotected fork. Re-enable and inspect the next
   trusted pre-build mount. This operational transition has not been performed by
   the Windows implementation; preserve warm caches until it is scheduled.
4. If enforcement cannot be established, use an accepted isolated cache/provider
   alternative. Renaming tags is insufficient when PR code can select those names.

## Capacity and retirement

Right-size from peak working sets plus headroom; the GitHub integration's current
minimum is 20 GB. Preserve useful separate native build graphs when combining them
would cause last-writer cache churn. Requested capacity and attached snapshots
have separate meters. An instant content size does not establish the peak or
future snapshot cost.

The license check, Windows aggregate/model verifier, and lockfile audit now use
standard hosted Linux without Namespace cache consumers. After those workflow
changes are deployed and their old runs drained, inspect provider-created caches
for these removed consumers and retire only the unused tags. Built-in profile
layers can create storage even without an explicit cache action; inspect actual
consumers before deleting anything.

The Windows comparative pilot uses either standard hosted compute or direct
Namespace machine labels **without an attached Namespace volume**. Optional
GitHub caches retain GitHub's default/base and PR merge-ref scopes. It creates no
new Namespace custom tag. A cold pilot disables both cache restore paths; record
that choice and do not describe a single warm run as stable tail-latency evidence.

The shared `windows-native-build` action keys its native graph on the toolchain,
lock and project files alone. Its earlier per-commit key missed on every new
SHA, and each successful run then uploaded another 1.7-2.6 GB archive (about
four minutes of hosted post-step time). On October 4 that held the repository
at 10.47 GB against GitHub's 10 GB default. An unchanged dependency graph now
hits exactly and saves nothing. A changed graph builds cold once rather than
inheriting stale artifacts.

## Remaining operating decisions

Verify the effective account quota/alerts, cache policy, working sets, current
demand and any plan-transition quote in the authenticated dashboard. Team,
Business and hybrid remain viable choices. Subscription allowances do not replace
explicit job bounds or an enforceable spending policy. Native provider adoption
requires complete test evidence and measured feedback/reliability; a runner label
change alone does not establish parity.
