-- set up identity db --

`dotnet ef` runs Program.cs as far as builder.Build(), so both connection strings have to be in the
environment first. From the repo root:

. ./env.sh
dotnet ef migrations add CreateIdentity --project src/ArtistShop.Web
dotnet ef database update --project src/ArtistShop.Web
