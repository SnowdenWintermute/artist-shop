#!/usr/bin/env bash
# The postgres image runs this once, the first time it sets up an empty data volume (anything in
# /docker-entrypoint-initdb.d does). It never runs again on that volume, so an existing one needs
# the same statement run by hand.
#
# The app logs in as artist_shop_app, not as the postgres superuser, so a SQL injection that ever
# got through couldn't read the server's files or run programs (COPY ... PROGRAM). CREATEDB is the
# one extra right it has: it creates its databases, and so owns them and everything in them.
set -euo pipefail

if [[ -z "${POSTGRES_APP_PASSWORD:-}" ]]; then
  echo "error: POSTGRES_APP_PASSWORD isn't set, so the app's role can't be made." >&2
  exit 1
fi

# :'app_password' is psql's own quoting of the variable as a string literal, so no character in the
# password can end the statement
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  --set app_password="$POSTGRES_APP_PASSWORD" <<'SQL'
CREATE ROLE artist_shop_app LOGIN CREATEDB PASSWORD :'app_password';
SQL
