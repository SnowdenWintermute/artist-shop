#!/usr/bin/env bash
# Sourced, not executed -- by dev.sh, and by hand before any `dotnet ef` command (see commands.md).
# Run it from the repo root; the .env path is relative.
#
# The connection strings are assembled from the one secret in .env rather than stored as a second
# copy of the password. The app reads them as plain environment variables, which is how it will
# read them in production too -- only the thing setting the variables differs.

if [[ ! -f .env ]]; then
  echo "error: .env not found. Copy .env.example and set POSTGRES_PASSWORD." >&2
  return 1
fi

set -a
. ./.env
set +a

export ConnectionStrings__ArtistShop="Host=localhost;Port=5434;Database=artist_shop;Username=postgres;Password=$POSTGRES_PASSWORD"
# glibc's allocator gives each thread its own memory pool, and libvips's threads fragment them until
# memory looks leaked. Two pools is what imgproxy, sharp and Mastodon recommend; production needs it too
export MALLOC_ARENA_MAX=2

export ConnectionStrings__ArtistShopIdentity="Host=localhost;Port=5434;Database=artist_shop_identity;Username=postgres;Password=$POSTGRES_PASSWORD"
