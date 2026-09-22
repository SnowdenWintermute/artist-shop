# syntax=docker/dockerfile:1

# build-and-push.sh builds this and pushes it to Docker Hub as snowd3n/artist-shop, and the VPS
# pulls it. docker-compose.production.yml runs it locally.
#
# Two stages: the SDK image (large) builds the app, and only the published output is copied into
# the much smaller runtime image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# the Tailwind CLI isn't in the repo (it's gitignored), so the build downloads the same version
# that dev uses; check it with `./tailwindcss --help` when upgrading
ARG TAILWIND_VERSION=v4.3.3

WORKDIR /source

# the csproj runs ../../tailwindcss from the project folder, which lands here
ADD --chmod=755 https://github.com/tailwindlabs/tailwindcss/releases/download/${TAILWIND_VERSION}/tailwindcss-linux-x64 tailwindcss

# Restoring from the project file alone first lets Docker reuse that layer when only code changed
COPY src/ArtistShop.Web/ArtistShop.Web.csproj src/ArtistShop.Web/
RUN dotnet restore src/ArtistShop.Web/ArtistShop.Web.csproj

COPY src/ src/
RUN dotnet publish src/ArtistShop.Web/ArtistShop.Web.csproj --configuration Release --no-restore --output /app

# Ubuntu-based (glibc), which the bundled libvips needs; an Alpine (musl) image would not load it
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# see env.sh: fewer glibc memory pools, so libvips doesn't fragment memory
ENV MALLOC_ARENA_MAX=2

# uploaded originals and variants live on a volume mounted here, not inside the image
ENV ImageStorage__RootPath=/data/images

# The image runs as the non-root "app" user. A named volume copies the ownership of the folder it
# is mounted over, so creating these here as "app" makes the volumes writable. The second folder is
# where ASP.NET Core keeps the keys that sign login cookies and antiforgery tokens; without a volume
# there, every redeploy would log everyone out.
RUN mkdir -p /data/images /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /data/images /home/app/.aspnet

WORKDIR /app
COPY --from=build /app .

USER app

# the .NET images listen on 8080 by default; a reverse proxy in front handles HTTPS
EXPOSE 8080

ENTRYPOINT ["dotnet", "ArtistShop.Web.dll"]
