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
docker exec -i artist-shop-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "ALTER DATABASE ArtistShop SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ArtistShop;"
