#!/bin/sh
# Exercise the specified app without enabling input monitoring or posting keyboard events.
set -eu
if [ "$#" -ne 1 ]; then echo 'Usage: sh scripts/test-macos.sh /path/to/PawPaw.app' >&2; exit 2; fi
app=$(CDPATH= cd "$1" && pwd)
src=$(CDPATH= cd "$(dirname "$0")/../源码/macOS" && pwd)
tmp=$(mktemp -d "${TMPDIR:-/tmp}/pawpaw-test.XXXXXXXX")
pet=
cleanup() {
    if [ -n "$pet" ]; then kill "$pet" 2>/dev/null || true; wait "$pet" 2>/dev/null || true; fi
    rm -rf "$tmp"
}
trap cleanup EXIT
trap 'exit 1' HUP INT TERM
exe="$app/Contents/MacOS/PawPaw"
codesign --verify --strict --all-architectures "$app"
xcrun lipo "$exe" -verify_arch arm64 x86_64
cd "$src"
xcrun clang -O2 -Wall -Wextra -I. tests/motion_test.c PetMotion.c -o "$tmp/motion"
"$tmp/motion"
xcrun clang -fobjc-arc -O2 -I. tests/render_smoke.m ChineseInput.m PetArt.m PetMotion.c PrintArt.c \
    -framework ApplicationServices -framework Carbon -framework Cocoa -framework CoreGraphics -framework CoreText -lz -framework ImageIO -o "$tmp/render"
"$tmp/render" "$app"
# The hosted runner has an independent preferences domain and graphical session.
if [ "${GITHUB_ACTIONS:-}" = true ]; then
    xcrun clang -fobjc-arc -O2 -I. tests/lock_smoke.m ChineseInput.m PetStudio.m PressStudio.m PetArt.m PetMotion.c PrintArt.c \
        -framework ApplicationServices -framework Carbon -framework Cocoa -framework QuartzCore -framework CoreGraphics -framework CoreText -lz -framework ImageIO -o "$tmp/lock"
    "$tmp/lock" "$app"
    "$exe" -paused YES -permissionAsked YES -locked YES &
    pet=$!
    sleep 3
    kill -0 "$pet"
fi
