#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

TAILWIND_IN="src/ArtistShop.Web/Styles/app.css"
TAILWIND_OUT="src/ArtistShop.Web/wwwroot/app.css"

# Read from launchSettings.json rather than repeated here, so the two cannot drift. dotnet watch
# uses the first profile when none is named, and takes the first url when a profile lists several.
APP_URL=$(python3 -c 'import json; p = json.load(open("src/ArtistShop.Web/Properties/launchSettings.json"))["profiles"]; print(next(iter(p.values()))["applicationUrl"].split(";")[0])' 2>/dev/null || true)

if [[ ! -x ./tailwindcss ]]; then
  echo "error: ./tailwindcss not found. See README for the download command." >&2
  exit 1
fi

. ./env.sh

# dotnet watch catches SIGINT and shuts down on its own schedule, and bash leaves huponexit off, so
# a Ctrl-C that never finishes -- or a closed window -- strands the watcher with its file watches
# and its port. They accumulate one per run, race over the same obj/, and the survivors end up
# serving a stale build. Start from a known-empty state instead.
pkill -f 'project src/ArtistShop\.Web' 2>/dev/null || true
pkill -f 'bin/Debug/net10\.0/ArtistShop\.Web' 2>/dev/null || true
for _ in $(seq 20); do
  pgrep -f 'project src/ArtistShop\.Web' >/dev/null || break
  sleep 0.25
done

# SQL Server gets its own window, the way start.sh does it, so its boot log and T-SQL errors stay
# readable instead of interleaving with dotnet watch.
#
# The two ways out of that window differ: Ctrl-C in it sends SIGINT to `docker compose up`, which
# stops the container. Closing the window only kills the compose client -- dockerd keeps the
# container running. `docker compose down` is the real stop.
#
# Ctrl-C on dotnet watch does not touch this window, so it outlives the run that opened it and an
# unguarded restart stacks a second window onto the same container. The title is both the label and
# what we match on. Ctrl-C *inside* the window is the case the title alone cannot see -- compose
# exits but `exec bash` keeps the window open on an idle shell -- so check the container too, and
# open a fresh window when the one already there is no longer attached to anything.
SQL_WINDOW_TITLE=artist-shop-sql

if pgrep -f "alacritty --title $SQL_WINDOW_TITLE" >/dev/null &&
  [[ "$(docker inspect -f '{{.State.Running}}' artist-shop-mssql 2>/dev/null)" == true ]]; then
  echo "sql server window already open, reusing it"
else
  alacritty --title "$SQL_WINDOW_TITLE" -e bash -c "cd '$PWD' && docker compose up; exec bash" &
fi

# Build once up front so app.css exists before the app starts. Runs while SQL Server boots.
./tailwindcss -i "$TAILWIND_IN" -o "$TAILWIND_OUT"

# 1433 accepts connections well before the engine answers queries, so wait on the healthcheck
# (sqlcmd SELECT 1) rather than the port.
echo "waiting for sql server..."
for _ in $(seq 90); do
  [[ "$(docker inspect -f '{{.State.Health.Status}}' artist-shop-mssql 2>/dev/null)" == healthy ]] && break
  sleep 1
done
if [[ "$(docker inspect -f '{{.State.Health.Status}}' artist-shop-mssql 2>/dev/null)" != healthy ]]; then
  echo "error: sql server never became healthy -- see the sql window." >&2
  exit 1
fi

# No --watch on tailwind here on purpose. The csproj runs tailwind from BuildTailwindCss, before
# static web assets are resolved, so a build always produces app.css and the manifest that
# fingerprints it together. A watcher writing app.css *after* the build leaves MapStaticAssets
# serving the build-time url, etag and last-modified for changed bytes, and the refresh that
# dotnet watch triggers can then be answered 304 from cache -- new tailwind classes silently do
# not arrive. --no-hot-reload keeps every save going through a real build, so the fingerprint
# moves whenever the css does.
echo "app: ${APP_URL:-see the 'Now listening on' line below}"

dotnet watch --no-hot-reload --project src/ArtistShop.Web
