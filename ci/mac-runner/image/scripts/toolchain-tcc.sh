#!/bin/bash
# Toolchain layer, stage 3b: Finder Apple Events grants for the runner account.
# docs/plans/42_self_hosted_mac_runner_plan.md §4.6.
#
# Writes rows into the system TCC database and into runner's per-user one,
# the way actions/runner-images (configure-tccdb-macos-27.sh) and Cirrus
# (update-tcc-database.sh) do. On macOS 27 the user database lives in a
# per-user ProtectedSystem container; the only reliable way to find it is to
# ask which file that user's tccd holds open. Needs SIP off and runner
# logged in (auto-login switched in stage 3).
set -euo pipefail

uid="$(id -u runner)"
agent=/usr/local/bin/tart-guest-agent

echo "==> runner's TCC database (uid $uid)"
db="$(sudo lsof -a -u "$uid" -c tccd -Fn 2>/dev/null \
  | sed -n 's|^n\(/private/var/containers/Data/ProtectedSystem/.*/Library/Application Support/com.apple.TCC/TCC.db\)$|\1|p' \
  | sort -u)"
count="$(printf '%s\n' "$db" | grep -c . || true)"
if [ "$count" != 1 ]; then
  echo "expected exactly one open user TCC database for runner, found $count:" >&2
  printf '%s\n' "$db" >&2
  echo "is runner logged in? auto-login: $(sudo sysadminctl -autologin status 2>&1 | tail -1)" >&2
  exit 1
fi
echo "    $db"
[ "$(sudo stat -f %u "$db")" = "$uid" ] || { echo "unexpected owner for $db" >&2; exit 1; }

grant() {
  # Apple Events to Finder for every process in the runner's chain. TCC blames
  # the responsible process, most likely the guest agent that tart exec runs
  # through; the others cover direct invocations.
  sudo sqlite3 "$1" <<EOF
INSERT OR REPLACE INTO access
  (service, client_type, client, auth_value, auth_reason, auth_version,
   indirect_object_identifier_type, indirect_object_identifier)
VALUES
  ('kTCCServiceAppleEvents', 1, '$agent', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '/bin/bash', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '/bin/zsh', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '/bin/sh', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '/usr/bin/osascript', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '/Users/runner/actions-runner/bin/Runner.Listener', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '/Users/runner/actions-runner/bin/Runner.Worker', 2, 0, 1, 0, 'com.apple.finder'),
  ('kTCCServiceAppleEvents', 1, '$agent', 2, 0, 1, 0, 'com.apple.systemevents'),
  ('kTCCServiceAccessibility', 1, '$agent', 2, 0, 1, NULL, 'UNUSED'),
  ('kTCCServicePostEvent', 1, '$agent', 2, 0, 1, NULL, 'UNUSED');
EOF
}

echo "==> Writing grants"
grant "/Library/Application Support/com.apple.TCC/TCC.db"
grant "$db"

echo "==> Rows now in runner's database"
sudo sqlite3 "$db" "SELECT service, client, indirect_object_identifier, auth_value FROM access WHERE service = 'kTCCServiceAppleEvents';"
