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
