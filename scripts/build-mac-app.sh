#!/usr/bin/env bash
# Build and optionally run the SlateMac SwiftUI smoke-test app.
#
# Pipeline:
#   1. cargo build -p slate-uniffi
#   2. uniffi-bindgen generate (Swift)
#   3. copy generated headers + Swift into apps/slate-mac/Sources/
#   4. swift build inside apps/slate-mac/
#   5. (optional, with --run) launch the resulting binary

set -euo pipefail

WORKSPACE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$WORKSPACE_ROOT"

PROFILE="${PROFILE:-debug}"
CARGO_PROFILE_FLAG=""
if [[ "$PROFILE" == "release" ]]; then
    CARGO_PROFILE_FLAG="--release"
fi

RUN=0
SKIP_A11Y_CHECK=0
BUNDLE=0
BINDINGS_ONLY=0
for arg in "$@"; do
    case "$arg" in
        --run) RUN=1; BUNDLE=1 ;;
        --bundle) BUNDLE=1 ;;
        --skip-a11y-check) SKIP_A11Y_CHECK=1 ;;
        --bindings-only) BINDINGS_ONLY=1 ;;
        --help|-h)
            echo "usage: $0 [--run] [--bundle] [--skip-a11y-check] [--bindings-only]"
            echo "  --bindings-only     Build slate-uniffi and regenerate + stage the"
            echo "                      Swift FFI bindings, then stop (no swift build,"
            echo "                      no bundle, no a11y-check). Used by"
            echo "                      \`make regenerate-bindings\`."
            echo "  --run               Build, wrap in .app bundle, and launch."
            echo "                      Implies --bundle."
            echo "  --bundle            Wrap the SwiftPM binary in a SlateMac.app"
            echo "                      bundle so macOS Accessibility (VoiceOver)"
            echo "                      can introspect it. SwiftPM-built bare"
            echo "                      executables aren't registered with"
            echo "                      LaunchServices, so VoiceOver can't reach"
            echo "                      their AX tree when launched from a shell."
            echo "  --skip-a11y-check   Don't run a11y-check after swift build."
            echo "                      Useful for CI jobs that only care about"
            echo "                      build/test pass and leave a11y to its"
            echo "                      own workflow."
            exit 0
            ;;
    esac
done

TARGET_DIR="target/$PROFILE"
GENERATED_DIR="target/generated/swift"
APP_DIR="apps/slate-mac"

# Source rustup environment if cargo isn't already on PATH.
if ! command -v cargo >/dev/null 2>&1; then
    if [[ -f "$HOME/.cargo/env" ]]; then
        # shellcheck disable=SC1091
        source "$HOME/.cargo/env"
    fi
fi

echo "==> Building slate-uniffi ($PROFILE)"
cargo build -p slate-uniffi $CARGO_PROFILE_FLAG

echo "==> Generating Swift bindings"
mkdir -p "$GENERATED_DIR"
cargo run -p slate-uniffi $CARGO_PROFILE_FLAG --bin uniffi-bindgen -- \
    generate \
    --library "$TARGET_DIR/libslate_uniffi.dylib" \
    --language swift \
    --out-dir "$GENERATED_DIR"

echo "==> Staging generated bindings into $APP_DIR"
cp "$GENERATED_DIR/slate_uniffi.swift"  "$APP_DIR/Sources/SlateMac/slate_uniffi.swift"
cp "$GENERATED_DIR/slate_uniffiFFI.h"   "$APP_DIR/Sources/slate_uniffiFFI/slate_uniffiFFI.h"

if [[ "$BINDINGS_ONLY" == "1" ]]; then
    echo
    echo "==> Bindings regenerated and staged (--bindings-only); stopping before swift build."
    exit 0
fi

echo "==> swift build ($APP_DIR)"
cd "$APP_DIR"
swift build

BINARY=".build/$PROFILE/SlateMac"
ABS_BINARY="$WORKSPACE_ROOT/$APP_DIR/$BINARY"

echo
echo "Built: $ABS_BINARY"
echo "Run with:"
echo "  DYLD_LIBRARY_PATH=\"$WORKSPACE_ROOT/$TARGET_DIR\" $ABS_BINARY"

APP_BUNDLE="$WORKSPACE_ROOT/$APP_DIR/.build/$PROFILE/SlateMac.app"
if [[ "$BUNDLE" == "1" ]]; then
    # Wrap the SwiftPM binary in a minimal .app bundle so the AX bridge
    # works. Without an Info.plist + LaunchServices registration, VoiceOver
    # can see the window but can't navigate the AX tree — observed when
    # launching the bare binary from a shell.
    echo
    echo "==> Wrapping binary in $APP_BUNDLE"
    rm -rf "$APP_BUNDLE"
    mkdir -p "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Frameworks"

    cp -f "$ABS_BINARY" "$APP_BUNDLE/Contents/MacOS/SlateMac"
    # The binary links against the dylib at its build path (with `deps/`).
    # We mirror that exact path inside the bundle for the rewrite below,
    # using `target/$PROFILE/libslate_uniffi.dylib` as the source — both
    # copies have the same content; we don't depend on `deps/` existing
    # in the bundle.
    cp -f "$WORKSPACE_ROOT/$TARGET_DIR/libslate_uniffi.dylib" \
        "$APP_BUNDLE/Contents/Frameworks/libslate_uniffi.dylib"

    # The binary's recorded reference points at the absolute deps/ path.
    # Read it back from the binary itself rather than reconstructing —
    # APFS's case-insensitive matching means `pwd` and the recorded
    # path can differ in case (e.g. /Users/coryj/Dev/... vs
    # /Users/coryj/dev/...), and `install_name_tool -change` does
    # exact-string matching.
    OLD_DYLIB_PATH=$(otool -L "$APP_BUNDLE/Contents/MacOS/SlateMac" \
        | awk '/libslate_uniffi\.dylib/{print $1; exit}')
    if [[ -z "$OLD_DYLIB_PATH" ]]; then
        echo "error: could not find libslate_uniffi.dylib reference in binary" >&2
        exit 1
    fi
    install_name_tool -change "$OLD_DYLIB_PATH" \
        "@executable_path/../Frameworks/libslate_uniffi.dylib" \
        "$APP_BUNDLE/Contents/MacOS/SlateMac"
    # The dylib's own LC_ID_DYLIB still points at the absolute deps/
    # path. Rewriting it lets any future tools that re-resolve via
    # install_name see the bundle-relative one.
    install_name_tool -id \
        "@executable_path/../Frameworks/libslate_uniffi.dylib" \
        "$APP_BUNDLE/Contents/Frameworks/libslate_uniffi.dylib"

    cat > "$APP_BUNDLE/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
"http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>Slate</string>
    <key>CFBundleExecutable</key>
    <string>SlateMac</string>
    <key>CFBundleIdentifier</key>
    <string>com.startingblind.slate.dev</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>Slate</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>0.0.1</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>15.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSSupportsAutomaticTermination</key>
    <false/>
    <key>NSSupportsSuddenTermination</key>
    <false/>
</dict>
</plist>
PLIST

    # `install_name_tool` invalidates the ad-hoc signature SwiftPM
    # gave the binary; re-sign so Gatekeeper / hardened-runtime don't
    # refuse to launch and AX permissions don't get confused. Ad-hoc
    # signing ("-") is fine for local dev; CI / distribution would
    # need a real identity.
    codesign --force --sign - "$APP_BUNDLE/Contents/Frameworks/libslate_uniffi.dylib"
    codesign --force --sign - "$APP_BUNDLE/Contents/MacOS/SlateMac"
    codesign --force --sign - "$APP_BUNDLE"

    # Re-register with LaunchServices so the AX system sees the new
    # bundle right away (Spotlight/LS sometimes caches the previous
    # location otherwise). Non-fatal: a missing lsregister just means
    # the user has to open Finder once before VoiceOver picks it up.
    LSREGISTER="/System/Library/Frameworks/CoreServices.framework/Versions/A/Frameworks/LaunchServices.framework/Versions/A/Support/lsregister"
    if [[ -x "$LSREGISTER" ]]; then
        "$LSREGISTER" -f "$APP_BUNDLE" >/dev/null 2>&1 || true
    fi

    echo
    echo "App bundle: $APP_BUNDLE"
    echo "Launch with VoiceOver-friendly AX wiring:"
    echo "  open \"$APP_BUNDLE\""
fi

# Best-effort SwiftUI accessibility check using
# cvs-health/ios-swiftui-accessibility-techniques' a11y-check. CI runs
# the canonical version of this; locally we surface findings inline so
# you don't have to push to hear about a regression. CI jobs that only
# care about build/test (e.g. the Swift tests workflow) pass
# `--skip-a11y-check` to avoid conflating an a11y regression with a
# build/test failure — a11y has its own dedicated workflow.
cd "$WORKSPACE_ROOT"
if [[ "$SKIP_A11Y_CHECK" == "1" ]]; then
    echo
    echo "==> Skipping a11y-check (--skip-a11y-check)."
elif command -v a11y-check >/dev/null 2>&1; then
    echo
    echo "==> Running a11y-check on apps/slate-mac/Sources/SlateMac"
    if ! a11y-check apps/slate-mac/Sources/SlateMac --only error; then
        echo
        echo "a11y-check reported errors above. CI will fail until they're fixed." >&2
        exit 1
    fi
else
    echo
    echo "==> Skipping a11y-check (not installed)."
    echo "    Install with:"
    echo "      brew tap cvs-health/ios-swiftui-accessibility-techniques \\"
    echo "        https://github.com/cvs-health/ios-swiftui-accessibility-techniques.git"
    echo "      brew install --HEAD cvs-health/ios-swiftui-accessibility-techniques/a11y-check"
fi

if [[ "$RUN" == "1" ]]; then
    echo
    echo "==> Launching SlateMac (.app bundle)"
    # `open` hands off to LaunchServices, which registers the bundle
    # with the window server / AX system. The bare binary path
    # ($ABS_BINARY) works for non-AX UI testing but is invisible to
    # VoiceOver — use the bundle for any screen-reader work.
    #
    # `-n` forces a fresh process even if another SlateMac.app (e.g.
    # an older build from a different path, or a still-running prior
    # `--run`) is already registered with LaunchServices under the
    # same bundle ID. Without it `open` would just activate the
    # existing instance and you'd test stale code.
    open -n "$APP_BUNDLE"
fi
