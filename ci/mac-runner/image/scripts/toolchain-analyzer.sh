#!/bin/bash
# Toolchain layer: the SwiftUI accessibility analyzer that a11y-check.yml
# builds on every cache miss, baked once instead. Runs inside the guest as
# admin. Works both with build-time passwordless sudo (stage 3) and after it
# was tightened (stage 3d, ADMIN_PASSWORD set).
set -euo pipefail

sudo_() {
  # Password on stdin from the printf builtin, never as an argument (see warm-build.sh).
  if sudo -n /usr/bin/true 2>/dev/null; then sudo "$@"
  else printf '%s\n' "${ADMIN_PASSWORD:?admin sudo needs a password}" | sudo -S -p '' "$@"; fi
}

src="$HOME/a11y-src"
rm -rf "$src"
echo "==> cvs-health/ios-swiftui-accessibility-techniques @ $A11Y_CHECK_REF"
git clone --quiet --filter=blob:none --no-checkout \
  https://github.com/cvs-health/ios-swiftui-accessibility-techniques.git "$src"
git -C "$src" checkout --quiet "$A11Y_CHECK_REF"
# The pin is a full SHA in practice; resolving it lets a tag or branch work too.
[ "$(git -C "$src" rev-parse HEAD)" = "$(git -C "$src" rev-parse --verify "$A11Y_CHECK_REF^{commit}")" ]

echo "==> swift build -c release"
start=$(date +%s)
(cd "$src/a11y-check" && swift build -c release 2>&1 | tail -3)
echo "   took $(( $(date +%s) - start )) s"

sudo_ install -o root -g wheel -m 755 "$src/a11y-check/.build/release/a11y-check" /usr/local/slate-runner/bin/a11y-check
rm -rf "$src"
echo "==> $(/usr/local/slate-runner/bin/a11y-check --version 2>&1 | head -1)"
