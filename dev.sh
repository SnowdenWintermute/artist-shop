#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

TAILWIND_IN="src/ArtistShop.Web/Styles/app.css"
TAILWIND_OUT="src/ArtistShop.Web/wwwroot/app.css"

if [[ ! -x ./tailwindcss ]]; then
  echo "error: ./tailwindcss not found. See README for the download command." >&2
  exit 1
fi

# Build once up front so app.css exists before the app starts.
./tailwindcss -i "$TAILWIND_IN" -o "$TAILWIND_OUT"

./tailwindcss -i "$TAILWIND_IN" -o "$TAILWIND_OUT" --watch=always &
tailwind_pid=$!
trap 'kill "$tailwind_pid" 2>/dev/null || true' EXIT

dotnet watch --project src/ArtistShop.Web
