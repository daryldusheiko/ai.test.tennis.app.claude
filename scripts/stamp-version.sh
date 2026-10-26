#!/usr/bin/env bash
# Stamps a published site with the version the About page shows. The deploy runs this through publish.sh.
# Usage: scripts/stamp-version.sh <site-dir> <version> <commit> <released yyyy-MM-dd>
set -euo pipefail

site="${1:?usage: stamp-version.sh <site-dir> <version> <commit> <released>}"
version="${2:?missing version}"
commit="${3:?missing commit}"
released="${4:?missing release date}"

[[ "$version" =~ ^[0-9A-Za-z.+-]+$ ]] || { echo "version has unexpected characters: $version" >&2; exit 1; }
[[ "$commit" =~ ^[0-9A-Za-z]+$ ]] || { echo "commit has unexpected characters: $commit" >&2; exit 1; }
[[ "$released" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || { echo "release date must be yyyy-MM-dd: $released" >&2; exit 1; }

printf '{"version":"%s","commit":"%s","released":"%s"}\n' "$version" "$commit" "$released" > "$site/version.json"

# The service worker only updates when its own bytes change. Without this, a deploy that changes only the version
# would not reach installed copies.
printf '\n// Released %s (%s, %s)\n' "$version" "$commit" "$released" >> "$site/service-worker.js"
echo "Stamped $site as version $version ($commit, $released)"
