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

# dotnet watch traps SIGTERM and then never finishes shutting down, and bash leaves huponexit off,
# so neither a plain pkill nor a closed window clears one. They accumulate one per run, keep their
# file watches, race over the same obj/, and the survivors end up serving a stale build. SIGKILL is
# the only signal that actually clears them, so start from a known-empty state with that.
pkill -9 -f 'project src/ArtistShop\.Web' 2>/dev/null || true
pkill -9 -f 'bin/Debug/net10\.0/ArtistShop\.Web' 2>/dev/null || true
pkill -9 -f 'tailwindcss -i src/ArtistShop\.Web' 2>/dev/null || true

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

# Tailwind gets its own watcher rather than running only from BuildTailwindCss in the csproj. Hot
# reload applies razor and cs edits as deltas without an msbuild, so that target does not fire on a
# save and a class typed for the first time never reaches app.css. This watcher regenerates it on
# the same save, and dotnet watch picks the changed file up and pushes it to the browser. Plain
# --watch quits as soon as stdin closes, which it does here, so it has to be --watch=always. The
# first pass runs now, while SQL Server boots, which is what gets app.css there before the build.
./tailwindcss -i "$TAILWIND_IN" -o "$TAILWIND_OUT" --watch=always &

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

# Not --no-hot-reload. That mode does rebuild and restart on every save, but its restart path never
# sends the browser refresh socket anything -- no wait, no reload -- so the page only updates when
# you refresh it by hand. Only the hot reload path drives that socket, and it is what turns the
# regenerated app.css above into an UpdateStaticFile push.
echo "app: ${APP_URL:-see the 'Now listening on' line below}"

dotnet watch --project src/ArtistShop.Web
