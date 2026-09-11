-- set up identity db --

. ./env.sh
dotnet ef migrations add CreateIdentity --project src/ArtistShop.Web --output-dir Identity/Migrations
dotnet ef database update --project src/ArtistShop.Web

-- wipe all identity data and restart --

dotnet ef database drop --project src/ArtistShop.Web --force
rm -rf src/ArtistShop.Web/Identity/Migrations
. ./env.sh
dotnet ef migrations add CreateIdentity --project src/ArtistShop.Web --output-dir Identity/Migrations
dotnet ef database update --project src/ArtistShop.Web

-- wipe domain database --
cd ~/projects/artist-shop && set -a && . ./.env && set +a
docker exec artist-shop-mssql /opt/mssql-tools18/bin/sqlcmd \
-S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -I -d master \
-Q "DROP DATABASE ArtistShop;"
