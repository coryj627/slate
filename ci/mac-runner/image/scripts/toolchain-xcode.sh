#!/bin/bash
# Toolchain layer: install the host's Xcode from the uploaded tarball.
# Runs inside the guest as admin (build-time passwordless sudo).
set -euo pipefail

echo "==> Xcode from tarball"
# Files belong to root, not to the host account that owned them.
sudo tar --no-same-owner -C /Applications -xf "$HOME/stage/Xcode.tar"
rm -f "$HOME/stage/Xcode.tar"
sudo xattr -dr com.apple.quarantine /Applications/Xcode.app 2>/dev/null || true

build="$(/usr/libexec/PlistBuddy -c 'Print :ProductBuildVersion' /Applications/Xcode.app/Contents/version.plist)"
if [ "$build" != "$XCODE_BUILD" ]; then
  echo "Xcode build is $build, expected $XCODE_BUILD" >&2
  exit 1
fi

echo "==> Select, accept the licence, first launch (no simulator platforms)"
sudo xcode-select -s /Applications/Xcode.app
sudo xcodebuild -license accept
sudo xcodebuild -runFirstLaunch
# Lets standard accounts (runner, builder) use the developer tools.
sudo DevToolsSecurity -enable

echo "==> Versions"
xcodebuild -version
swift --version 2>&1 | head -1
echo "simulator runtimes installed:"
xcrun simctl runtime list 2>/dev/null | tail -n +2 || echo "  (none)"
