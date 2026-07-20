#!/bin/bash
set -euo pipefail

repository="${DEVCONVERTER_REPOSITORY:-pagict/DevConverter}"
archive="${1:-}"
arch="$(uname -m)"
case "$arch" in
  arm64) asset="DevConverter-macos-arm64.zip" ;;
  x86_64) asset="DevConverter-macos-x64.zip" ;;
  *) echo "Unsupported macOS architecture: $arch" >&2; exit 1 ;;
esac

tmp="$(mktemp -d "${TMPDIR:-/tmp}/devconverter.XXXXXX")"
trap 'rm -rf "$tmp"' EXIT

if [[ -z "$archive" ]]; then
  command -v gh >/dev/null 2>&1 || { echo 'GitHub CLI (gh) is required to download this private repository release.' >&2; exit 1; }
  gh release download --repo "$repository" --pattern "$asset" --dir "$tmp" --clobber
  archive="$tmp/$asset"
fi

ditto -x -k "$archive" "$tmp/unpacked"
app_source="$tmp/unpacked/DevConverter Spotlight.app"
[[ -d "$app_source" ]] || { echo 'The archive does not contain DevConverter Spotlight.app.' >&2; exit 1; }

mkdir -p "$HOME/Applications" "$HOME/.local/bin"
rm -rf "$HOME/Applications/DevConverter Spotlight.app"
ditto "$app_source" "$HOME/Applications/DevConverter Spotlight.app"
ln -sfn "$HOME/Applications/DevConverter Spotlight.app/Contents/Resources/devconvert" "$HOME/.local/bin/devconvert"
open "$HOME/Applications/DevConverter Spotlight.app"

echo 'Installed DevConverter Spotlight.'
echo 'In System Settings > Spotlight > Quick Keys, assign "dev" to the Dev Convert action.'
