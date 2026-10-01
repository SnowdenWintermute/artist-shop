#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

TAILWIND_IN="src/ArtistShop.Web/Styles/app.css"
TAILWIND_OUT="src/ArtistShop.Web/wwwroot/app.css"

# Read from launchSettings.json rather than repeated here, so the two cannot drift. dotnet watch
# uses the first profile when none is named, and takes the first url when a profile lists several.
APP_URL=$(python3 -c 'import json; p = json.load(open("src/ArtistShop.Web/Properties/launchSettings.json"))["profiles"]; print(next(iter(p.values()))["applicationUrl"].split(";")[0])' 2>/dev/null || true)

# --phone serves the app to other devices on the Wi-Fi, under nip.io names, which resolve to the
# address inside them: site1.192-168-1-20.nip.io is 192.168.1.20. A phone can't use
# *.localhost, which names the phone itself
PHONE=false
if [[ "${1:-}" == --phone ]]; then
  PHONE=true
  LAN_ADDRESS=$(ip -4 route get 1.1.1.1 2>/dev/null | grep -oP 'src \K[0-9.]+' || true)
  if [[ -z "$LAN_ADDRESS" ]]; then
    echo "error: --phone found no network address. Is the Wi-Fi connected?" >&2
    exit 1
  fi
  PHONE_HOST="${LAN_ADDRESS//./-}.nip.io"
fi

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
pkill -9 -f 'bin/Debug/net[0-9.]*/ArtistShop\.Web' 2>/dev/null || true
pkill -9 -f 'tailwindcss -i src/ArtistShop\.Web' 2>/dev/null || true

# Postgres gets its own window, the way start.sh does it, so its boot log and SQL errors stay
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
SQL_WINDOW_TITLE=artist-shop-postgres

if pgrep -f "alacritty --title $SQL_WINDOW_TITLE" >/dev/null &&
  [[ "$(docker inspect -f '{{.State.Running}}' artist-shop-postgres 2>/dev/null)" == true ]]; then
  echo "postgres window already open, reusing it"
else
  alacritty --title "$SQL_WINDOW_TITLE" -e bash -c "cd '$PWD' && docker compose up; exec bash" &
fi

# Tailwind gets its own watcher rather than running only from BuildTailwindCss in the csproj. Hot
# reload applies razor and cs edits as deltas without an msbuild, so that target does not fire on a
# save and a class typed for the first time never reaches app.css. This watcher regenerates it on
# the same save, and dotnet watch picks the changed file up and pushes it to the browser. Plain
# --watch quits as soon as stdin closes, which it does here, so it has to be --watch=always. The
# first pass runs now, while Postgres boots, which is what gets app.css there before the build.
./tailwindcss -i "$TAILWIND_IN" -o "$TAILWIND_OUT" --watch=always &

# A new volume initialises behind a temporary server before the real one takes the port, so wait on
# the healthcheck rather than the port.
echo "waiting for postgres..."
for _ in $(seq 90); do
  [[ "$(docker inspect -f '{{.State.Health.Status}}' artist-shop-postgres 2>/dev/null)" == healthy ]] && break
  sleep 1
done
if [[ "$(docker inspect -f '{{.State.Health.Status}}' artist-shop-postgres 2>/dev/null)" != healthy ]]; then
  echo "error: postgres never became healthy -- see the postgres window." >&2
  exit 1
fi

# Every link to a site is built from its host, so the hosts are renamed to the names this run
# serves: .localhost, or the current address's nip.io name with --phone. Run either way, so a plain
# run undoes --phone, and a new address from the router only needs a restart
SITE_DOMAIN=localhost
if [[ $PHONE == true ]]; then
  SITE_DOMAIN=$PHONE_HOST
fi
docker exec -i artist-shop-postgres psql -U postgres -d artist_shop_platform -v ON_ERROR_STOP=1 -q <<SQL
UPDATE site_hosts
SET host = regexp_replace(host, '\.(localhost|[0-9-]+\.nip\.io)$', '.$SITE_DOMAIN')
WHERE host ~ '\.(localhost|[0-9-]+\.nip\.io)$';
SQL

# Not --no-hot-reload. That mode does rebuild and restart on every save, but its restart path never
# sends the browser refresh socket anything -- no wait, no reload -- so the page only updates when
# you refresh it by hand. Only the hot reload path drives that socket, and it is what turns the
# regenerated app.css above into an UpdateStaticFile push.
if [[ $PHONE == false ]]; then
  echo "app: ${APP_URL:-see the 'Now listening on' line below}"
  dotnet watch --project src/ArtistShop.Web
  exit
fi

# Signing in to a site goes through the platform, so it moves to the phone's names too. Sign in on
# the desktop through these names as well while this runs
export Platform__Host="$PHONE_HOST"

# HTTPS, as in production: browsers only give a secure page some features, such as
# crypto.randomUUID and the clipboard. The certificate comes from mkcert's own authority, which
# the phone and the desktop browser have to trust once. One per address, so a new address gets a
# new one
CERT_DIR="$PWD/.dev-certs"
CERT="$CERT_DIR/$PHONE_HOST.pem"
CERT_KEY="$CERT_DIR/$PHONE_HOST-key.pem"
if [[ ! -f "$CERT" ]]; then
  if ! command -v mkcert >/dev/null; then
    echo "error: --phone needs mkcert (apt install mkcert) for its certificate." >&2
    exit 1
  fi
  mkdir -p "$CERT_DIR"
  mkcert -cert-file "$CERT" -key-file "$CERT_KEY" "$PHONE_HOST" "*.$PHONE_HOST"
fi
export Kestrel__Certificates__Default__Path="$CERT"
export Kestrel__Certificates__Default__KeyPath="$CERT_KEY"

echo "platform: https://$PHONE_HOST:5176"
echo "No automatic refresh on the phone: dotnet watch's refresh socket is on localhost"
dotnet watch --project src/ArtistShop.Web --launch-profile phone
