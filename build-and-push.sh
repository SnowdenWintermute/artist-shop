#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

# built here and pulled by the VPS: its 2 GB can't run the .NET SDK build
IMAGE=snowd3n/artist-shop

echo "==> building $IMAGE"
docker build -t "$IMAGE" .

echo "==> pushing $IMAGE"
docker push "$IMAGE"
