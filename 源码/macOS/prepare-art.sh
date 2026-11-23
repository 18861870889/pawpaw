#!/bin/sh
# Extract a matched index and atlas from a published or offline public Mac ZIP.
set -eu
dest=${1:?Usage: prepare-art.sh DESTINATION}
mkdir -p "$dest"
archive=${PAWPAW_ART_ARCHIVE:-}
if [ -z "$archive" ]; then
    base=https://github.com/18861870889/pawpaw/releases/download/v2.0.0
    filename=PawPaw-macOS-universal.zip
    archive="$dest/$filename"
    curl --proto '=https' --proto-redir '=https' -fsSL --retry 3 "$base/SHA256SUMS.txt" -o "$dest/checksums"
    sha=$(awk -v n="$filename" '$2==n {print $1}' "$dest/checksums")
    [ "${#sha}" -eq 64 ] || { echo 'Missing public artwork checksum' >&2; exit 1; }
    curl --proto '=https' --proto-redir '=https' -fsSL --retry 3 "$base/$filename" -o "$archive"
    [ "$(shasum -a 256 "$archive" | cut -d ' ' -f 1)" = "$sha" ] || { echo 'Archive checksum mismatch' >&2; exit 1; }
fi
for file in Artwork.bin Artwork.json; do
    unzip -p "$archive" "PawPaw.app/Contents/Resources/$file" > "$dest/$file"
    [ -s "$dest/$file" ] || exit 1
done
