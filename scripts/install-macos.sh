#!/bin/bash
set -euo pipefail

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
  archive="$tmp/$asset"
  curl --fail --location --silent --show-error \
    "https://github.com/pagict/DevConverter/releases/latest/download/$asset" \
    --output "$archive"
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
