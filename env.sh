#!/usr/bin/env bash
# Sourced, not executed -- by dev.sh, and by hand before any `dotnet ef` command (see commands.md).
# Run it from the repo root; the .env path is relative.
#
# The connection strings are assembled from the one secret in .env rather than stored as a second
# copy of the password. The app reads them as plain environment variables, which is how it will
# read them in production too -- only the thing setting the variables differs.

if [[ ! -f .env ]]; then
  echo "error: .env not found. Copy .env.example and set its passwords." >&2
  return 1
fi

set -a
. ./.env
set +a

# the app logs in as its own role, which postgres-init/create-app-role.sh makes; POSTGRES_PASSWORD
# is the superuser's, for psql by hand only
# The platform database, which lists the sites; each site's own database is found from it
export ConnectionStrings__ArtistShopPlatform="Host=localhost;Port=5434;Database=artist_shop_platform;Username=artist_shop_app;Password=$POSTGRES_APP_PASSWORD"
# glibc's allocator gives each thread its own memory pool, and libvips's threads fragment them until
# memory looks leaked. Two pools is what imgproxy, sharp and Mastodon recommend; production needs it too
export MALLOC_ARENA_MAX=2

export ConnectionStrings__ArtistShopIdentity="Host=localhost;Port=5434;Database=artist_shop_identity;Username=artist_shop_app;Password=$POSTGRES_APP_PASSWORD"

# the account that owns the first site, made with this password if it doesn't exist; read only
# while there are no sites. The first site's hosts are in appsettings.Development.json
export FirstSite__OwnerEmail="mike@example.com"
export FirstSite__OwnerPassword="$DEV_OWNER_PASSWORD"

# the account that runs the platform and makes sign-up codes, made with this password if it
# doesn't exist; production sets the same two in its web env file
export Platform__OperatorEmail="mike@example.com"
export Platform__OperatorPassword="$DEV_OWNER_PASSWORD"
