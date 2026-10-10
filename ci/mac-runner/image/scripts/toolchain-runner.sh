#!/bin/bash
# Toolchain layer: guest agent, Actions runner, admission hook, Rust.
# Runs inside the guest as admin (build-time passwordless sudo).
#
# Ownership (plan §4.4): the agent and the hook are root's; the runner tree
# and the Rust toolchains belong to runner. builder can write none of them.
set -euo pipefail
S="$HOME/stage"

echo "==> Tart guest agent (serves tart exec in the logged-in session only)"
# A fresh macOS install has no /usr/local/bin.
sudo install -d -o root -g wheel -m 755 /usr/local/bin
mkdir -p "$S/agent"
tar -C "$S/agent" -xzf "$S/tart-guest-agent.tar.gz"
bin="$(find "$S/agent" -type f -name 'tart-guest-agent*' ! -name '*.txt' | head -1)"
[ -n "$bin" ] || { echo "no agent binary in tarball" >&2; exit 1; }
sudo install -o root -g wheel -m 755 "$bin" /usr/local/bin/tart-guest-agent
/usr/local/bin/tart-guest-agent --version 2>&1 | head -1 || true
sudo install -o root -g wheel -m 644 "$S/data/com.slate.tart-guest-agent.plist" /Library/LaunchAgents/com.slate.tart-guest-agent.plist

echo "==> Actions runner, owned by runner"
sudo install -d -o runner -g staff -m 755 /Users/runner/actions-runner
sudo tar -C /Users/runner/actions-runner -xzf "$S/actions-runner.tar.gz"
sudo chown -R runner:staff /Users/runner/actions-runner
# .env and .path are read by the runner at start and shape every job's
# environment. The hook path is what control 5 of the plan relies on.
sudo -u runner tee /Users/runner/actions-runner/.env >/dev/null <<'EOF'
ACTIONS_RUNNER_HOOK_JOB_STARTED=/usr/local/slate-runner/hooks/job-started.sh
CARGO_HOME=/Users/runner/toolchains/cargo
RUSTUP_HOME=/Users/runner/toolchains/rustup
RUNNER_TOOL_CACHE=/Users/runner/hostedtoolcache
EOF
sudo -u runner tee /Users/runner/actions-runner/.path >/dev/null <<'EOF'
/usr/local/slate-runner/bin:/Users/runner/toolchains/cargo/bin:/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin
EOF
ls /Users/runner/actions-runner/bin/Runner.Listener >/dev/null

echo "==> Admission hook, root-owned (shim + implementation; tests live in ci/mac-runner/hook/tests)"
sudo install -o root -g wheel -m 755 "$S/hooks/job-started.sh" /usr/local/slate-runner/hooks/job-started.sh
sudo install -o root -g wheel -m 644 "$S/hooks/admission.py"  /usr/local/slate-runner/hooks/admission.py
/usr/bin/python3 -I -c 'import ast,sys; ast.parse(open(sys.argv[1]).read())' /usr/local/slate-runner/hooks/admission.py

echo "==> Rust $RUST_VERSION for runner (shared toolchain path, see .env)"
sudo -u runner env HOME=/Users/runner \
  CARGO_HOME=/Users/runner/toolchains/cargo RUSTUP_HOME=/Users/runner/toolchains/rustup \
  sh "$S/rustup-init.sh" -y --no-modify-path --profile minimal \
  --default-toolchain "$RUST_VERSION" -c rustfmt -c clippy
sudo -u runner env CARGO_HOME=/Users/runner/toolchains/cargo RUSTUP_HOME=/Users/runner/toolchains/rustup \
  /Users/runner/toolchains/cargo/bin/cargo --version

echo "==> Check ownership"
stat -f '%Su %Sp %N' /usr/local/bin/tart-guest-agent /usr/local/slate-runner/hooks/job-started.sh \
  /Users/runner/actions-runner /Users/runner/actions-runner/.env /Users/runner/toolchains
