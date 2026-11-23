#!/usr/bin/env bash
# Publish only the two tested artifacts produced by the same tag workflow.
set -euo pipefail
tag=${GITHUB_REF_NAME:?}
repo=${GITHUB_REPOSITORY:?}
[[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]
[[ "$(git rev-parse HEAD)" == "$(git rev-parse "refs/tags/$tag^{commit}")" ]]
[[ "$(gh api "repos/$repo/commits/$tag" --jq .sha)" == "$(git rev-parse HEAD)" ]]

assets=(PawPaw-Windows.exe PawPaw-macOS-universal.zip)
for name in "${assets[@]}"; do [[ -s "dist/$name" ]]; done
(cd dist && sha256sum "${assets[@]}" > SHA256SUMS.txt)
assets+=(SHA256SUMS.txt)
notes=$(mktemp)
trap 'rm -f "$notes"' EXIT
awk -v heading="## ${tag#v}" '/^## /{printing=($0==heading);next} printing' CHANGELOG.md > "$notes"
grep -q '[^[:space:]]' "$notes" || { echo "Add notes for $tag to CHANGELOG.md" >&2; exit 1; }

# A failed lookup must not be mistaken for permission to create another draft.
records=$(gh api --paginate "repos/$repo/releases?per_page=100" | jq -sc --arg tag "$tag" 'add | map(select(.tag_name==$tag))')
case "$(jq length <<<"$records")" in
  0) gh release create "$tag" --repo "$repo" --verify-tag --draft --title "爪爪 PawPaw ${tag#v}" --notes-file "$notes" ;;
  1) ;;
  *) echo "Multiple releases already exist for $tag" >&2; exit 1 ;;
esac
release=$(gh release view "$tag" --repo "$repo" --json databaseId,isDraft)
id=$(jq -r .databaseId <<<"$release")
draft=$(jq -r .isDraft <<<"$release")
route="repos/$repo/releases/$id/assets"
for name in "${assets[@]}"; do
    digest="sha256:$(sha256sum "dist/$name" | cut -d ' ' -f 1)"
    existing=$(gh api "$route" | jq -r --arg name "$name" '.[] | select(.name==$name) | .digest // "incomplete"')
    if [[ "$existing" == "$digest" ]]; then continue; fi
    [[ "$draft" == true ]] || { echo "Published asset differs: $name" >&2; exit 1; }
    # Drafts from interrupted attempts can be repaired. A public asset is never overwritten.
    gh release upload "$tag" "dist/$name" --repo "$repo" --clobber
    actual=$(gh api "$route" | jq -r --arg name "$name" '.[] | select(.name==$name and .state=="uploaded") | .digest')
    [[ "$actual" == "$digest" ]] || { echo "Uploaded digest differs: $name" >&2; exit 1; }
done
if [[ "$draft" == true ]]; then
    gh api --method PATCH "repos/$repo/releases/$id" -F draft=false -f make_latest=legacy --silent
fi
gh release view "$tag" --repo "$repo" --json url,isDraft
