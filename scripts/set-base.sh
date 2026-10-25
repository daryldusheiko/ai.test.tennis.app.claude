#!/usr/bin/env bash
# Points a published site at the path it is served from, and adds the GitHub Pages helper files.
# Usage: scripts/set-base.sh <site-dir> [base-path]   (base-path starts and ends with /, default /)
set -euo pipefail

site="${1:?usage: set-base.sh <site-dir> [base-path]}"
base="${2:-/}"
[[ "$base" == /* && "$base" == */ ]] || { echo "base-path must start and end with /" >&2; exit 1; }

# The base href is the only edit made after the build. The service worker does not hash-check index.html,
# so this does not break the offline cache.
sed -i.bak "s|<base href=\"[^\"]*\" />|<base href=\"$base\" />|" "$site/index.html"
rm -f "$site/index.html.bak"
grep -q "<base href=\"$base\" />" "$site/index.html" || { echo "base href was not rewritten" >&2; exit 1; }

# GitHub Pages serves 404.html for unknown paths, which lets deep links such as /players load the app.
cp "$site/index.html" "$site/404.html"
# Stops GitHub Pages (Jekyll) ignoring files that start with an underscore, such as _framework.
touch "$site/.nojekyll"
