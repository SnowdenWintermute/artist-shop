#!/usr/bin/env bash
# Sourced, not executed -- by dev.sh, and by hand before any `dotnet ef` command (see commands.md).
# Run it from the repo root; the .env path is relative.
#
# The connection strings are assembled from the one secret in .env rather than stored as a second
# copy of the password. The app reads them as plain environment variables, which is how it will
# read them in production too -- only the thing setting the variables differs.

if [[ ! -f .env ]]; then
  echo "error: .env not found. Copy .env.example and set MSSQL_SA_PASSWORD." >&2
  return 1
fi

set -a
. ./.env
set +a

export ConnectionStrings__ArtistShop="Server=localhost,1433;Database=ArtistShop;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
export ConnectionStrings__ArtistShopIdentity="Server=localhost,1433;Database=ArtistShopIdentity;User Id=sa;Password=$MSSQL_SA_PASSWORD;TrustServerCertificate=True"
