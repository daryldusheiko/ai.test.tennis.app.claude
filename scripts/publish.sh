#!/usr/bin/env bash
# Publishes the app as static files ready for GitHub Pages (or any HTTPS static host).
# Usage: scripts/publish.sh <output-dir> [base-path]
#   The site is written to <output-dir>/site. base-path is the repository subpath, e.g. /my-repo/ (default /).
set -euo pipefail

output="${1:?usage: publish.sh <output-dir> [base-path]}"
base="${2:-/}"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rm -rf "$output"
dotnet publish "$root/app.claude.tennis" -c Release -o "$output/raw"
mv "$output/raw/wwwroot" "$output/site"
rm -rf "$output/raw"

"$root/scripts/set-base.sh" "$output/site" "$base"
echo "Published to $output/site (base $base)"
