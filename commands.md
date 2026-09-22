-- regenerate the identity migration (the app applies it at startup, creating the database) --

. ./env.sh
rm -rf src/ArtistShop.Web/Identity/Migrations
dotnet ef migrations add CreateIdentity --project src/ArtistShop.Web --output-dir Identity/Migrations

-- wipe only the domain database (keeps the login; the app rebuilds it at startup) --
docker exec artist-shop-postgres psql -U postgres \
-c "DROP DATABASE IF EXISTS artist_shop WITH (FORCE);"

-- wipe the domain, identity and test databases --
docker exec artist-shop-postgres psql -U postgres \
-c "DROP DATABASE IF EXISTS artist_shop WITH (FORCE);" \
-c "DROP DATABASE IF EXISTS artist_shop_identity WITH (FORCE);" \
-c "DROP DATABASE IF EXISTS artist_shop_tests WITH (FORCE);"

-- open a SQL prompt on the domain database --
docker exec -it artist-shop-postgres psql -U postgres -d artist_shop
