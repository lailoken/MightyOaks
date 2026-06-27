#!/usr/bin/env bash
# Publish MightyOaks 1.2.1 to GitHub Release, Thunderstore, and Nexus.
#
# Prerequisites:
#   - Git tag pushed (./package_release.sh && git push origin main --tags)
#   - GITHUB_TOKEN or `gh auth login` for GitHub Release
#   - TCLI_AUTH_TOKEN from https://valheim.thunderstore.io/settings/access-tokens/
#   - Nexus: manual upload at https://www.nexusmods.com/valheim/mods/3269
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
VERSION="$(grep -oP '(?<="version_number": ")[^"]+' "$ROOT/manifest.json")"
ZIP="$ROOT/dist/lailoken-MightyOaks-${VERSION}.zip"

"$ROOT/package_release.sh"

echo
echo "=== GitHub Release ==="
if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
  gh release view "$VERSION" --repo lailoken/MightyOaks >/dev/null 2>&1 \
    && gh release upload "$VERSION" "$ZIP" --repo lailoken/MightyOaks --clobber \
    || gh release create "$VERSION" "$ZIP" \
         --repo lailoken/MightyOaks \
         --title "MightyOaks $VERSION" \
         --notes "Fix Valheim 1.0 test build crash (GetStableHashCode removed). Existing saves unchanged."
  echo "GitHub release: https://github.com/lailoken/MightyOaks/releases/tag/$VERSION"
else
  echo "Install and auth gh, or create release manually:"
  echo "  https://github.com/lailoken/MightyOaks/releases/new?tag=$VERSION"
  echo "  Upload: $ZIP"
fi

echo
echo "=== Thunderstore ==="
if command -v tcli >/dev/null 2>&1 && [[ -n "${TCLI_AUTH_TOKEN:-}" ]]; then
  (cd "$ROOT/dist/staging" && tcli publish --token "$TCLI_AUTH_TOKEN")
  echo "Published to https://thunderstore.io/c/valheim/p/lailoken/MightyOaks/"
else
  echo "Upload manually (or: dotnet tool install -g tcli && export TCLI_AUTH_TOKEN=...):"
  echo "  https://valheim.thunderstore.io/create/"
  echo "  Zip: $ZIP"
fi

echo
echo "=== Nexus Mods ==="
echo "Upload manually at:"
echo "  https://www.nexusmods.com/valheim/mods/3269?tab=files"
echo "  File: $ZIP (or same contents: DLL + manifest + README + icon)"
echo
echo "Done. Package: $ZIP"
