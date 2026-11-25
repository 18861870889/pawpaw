#!/bin/sh
# Build a complete, ad-hoc signed universal application.
set -eu
here=$(CDPATH= cd "$(dirname "$0")" && pwd)
target=${1:-$here/build/PawPaw.app}
case "$target" in *.app) ;; *) echo 'The output path must end in .app' >&2; exit 2 ;; esac
mkdir -p "$(dirname "$target")"
target=$(CDPATH= cd "$(dirname "$target")" && pwd)/$(basename "$target")
if [ -e "$target" ]; then
    old=$(/usr/libexec/PlistBuddy -c 'Print CFBundleExecutable' "$target/Contents/Info.plist" 2>/dev/null || true)
    [ "$old" = PawPaw ] || { echo 'Output already contains a different application' >&2; exit 1; }
fi
tmp=$(mktemp -d "$(dirname "$target")/.pawpaw-build.XXXXXXXX")
cleanup() {
    if [ -e "$tmp/previous" ] && [ ! -e "$target" ]; then mv "$tmp/previous" "$target"; fi
    rm -rf "$tmp"
}
trap cleanup EXIT
trap 'exit 1' HUP INT TERM
art=${PAWPAW_ART_DIR:-}
if [ -z "$art" ]; then art="$tmp/art"; sh "$here/prepare-art.sh" "$art"; fi
for file in Artwork.bin Artwork.json; do [ -s "$art/$file" ] || { echo "Missing $file" >&2; exit 1; }; done
if ! /usr/bin/plutil -extract version xml1 -o - "$art/Artwork.json" | /usr/bin/grep -q '<integer>9</integer>'; then
    echo 'Use a PawPaw 2.2 artwork archive or export the art from this checkout.' >&2
    exit 1
fi
app="$tmp/PawPaw.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
for arch in arm64 x86_64; do
    xcrun --sdk macosx clang -arch "$arch" -mmacosx-version-min=11.0 -fobjc-arc -O2 \
        "$here/main.m" "$here/ChineseInput.m" "$here/PetStudio.m" "$here/PressStudio.m" "$here/PetArt.m" "$here/PetMotion.c" "$here/PrintArt.c" \
        -framework ApplicationServices -framework Carbon -framework Cocoa -framework QuartzCore -framework CoreGraphics -framework CoreText -lz -framework ImageIO -framework CoreFoundation \
        -o "$tmp/$arch"
done
xcrun lipo -create "$tmp/arm64" "$tmp/x86_64" -output "$app/Contents/MacOS/PawPaw"
chmod 755 "$app/Contents/MacOS/PawPaw"
cp "$here/Info.plist" "$app/Contents/Info.plist"
cp "$here/../ChineseWords.tsv" "$app/Contents/Resources/"
cp "$art/Artwork.bin" "$art/Artwork.json" "$app/Contents/Resources/"
for file in AppIcon.icns PrintInks.rgb StudioCovers.png StudioItems.png StudioMark.png; do
    cp "$here/Resources/$file" "$app/Contents/Resources/$file"
done
/usr/bin/codesign --force --sign - --timestamp=none "$app"
/usr/bin/codesign --verify --strict --all-architectures "$app"
if [ -e "$target" ]; then mv "$target" "$tmp/previous"; fi
mv "$app" "$target"
printf '%s\n' "$target"
