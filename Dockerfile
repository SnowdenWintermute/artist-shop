# syntax=docker/dockerfile:1

# build-and-push.sh builds this and pushes it to Docker Hub as snowd3n/artist-shop, and the VPS
# pulls it and runs it with docker-compose.production.yml; docker-compose.rehearsal.yml runs it
# locally.
#
# Two stages: the SDK image (large) builds the app, and only the published output is copied into
# the much smaller runtime image.

# exact release-candidate tags until .NET 11 is released in November, so a new RC is a
# deliberate change rather than whatever the moving 11.0 tag points at
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1 AS build

# the Tailwind CLI isn't in the repo (it's gitignored), so the build downloads the same version
# that dev uses; check it with `./tailwindcss --help` when upgrading
ARG TAILWIND_VERSION=v4.3.3

WORKDIR /source

# the csproj runs ../../tailwindcss from the project folder, which lands here. The build fails if
# the download's SHA-256 doesn't match; when changing the version, take the new value from the
# release's sha256sums.txt
ADD --checksum=sha256:dc61b3ac6b8c9ca874c0cc4c57b2409791a64c5540404ca5f5367360babc313a --chmod=755 https://github.com/tailwindlabs/tailwindcss/releases/download/${TAILWIND_VERSION}/tailwindcss-linux-x64 tailwindcss

# Restoring from the project file alone first lets Docker reuse that layer when only code changed
COPY src/ArtistShop.Web/ArtistShop.Web.csproj src/ArtistShop.Web/
RUN dotnet restore src/ArtistShop.Web/ArtistShop.Web.csproj

COPY src/ src/
RUN dotnet publish src/ArtistShop.Web/ArtistShop.Web.csproj --configuration Release --no-restore --output /app

# the folders the volumes mount over, made here because the runtime image has no shell to make them
RUN mkdir -p /volume-folders/images /volume-folders/aspnet/DataProtection-Keys

# Chiseled Ubuntu: only what .NET needs, with no shell or package manager. glibc, which the bundled
# libvips needs; an Alpine (musl) image would not load it. The -extra variant, because it carries
# ICU, which DatabaseCollationComparer's culture comparison and the slugs' Normalize rely on
FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1-resolute-chiseled-extra AS runtime

# see env.sh: fewer glibc memory pools, so libvips doesn't fragment memory
ENV MALLOC_ARENA_MAX=2

# uploaded originals and variants live on a volume mounted here, not inside the image
ENV ImageStorage__RootPath=/data/images

# The image runs as the non-root "app" user, APP_UID (1654). A named volume copies the ownership of
# the folder it is mounted over, so these are copied in owned by that user, which makes the volumes
# writable. The second folder is where ASP.NET Core keeps the keys that sign login cookies and
# antiforgery tokens; without a volume there, every redeploy would log everyone out.
COPY --from=build --chown=$APP_UID:$APP_UID /volume-folders/images /data/images
COPY --from=build --chown=$APP_UID:$APP_UID /volume-folders/aspnet /home/app/.aspnet

WORKDIR /app
COPY --from=build /app .

USER $APP_UID

# the .NET images listen on 8080 by default; a reverse proxy in front handles HTTPS
EXPOSE 8080

ENTRYPOINT ["dotnet", "ArtistShop.Web.dll"]
