## 1. Before you start

1. Export any site you want back (its admin Export page). Every site on the server is lost.
2. On this machine, build and push the new image (the VPS can't build it):

       ./build-and-push.sh

3. On the VPS, back up identity in case a step goes wrong:

       cd /app
       docker-compose exec -T artist-shop-postgres pg_dump -U postgres -Fc artist_shop_identity > ~/artist_shop_identity.dump

---

docker-compose stop artist-shop-web
docker-compose exec -T artist-shop-postgres psql -U postgres -c "DROP DATABASE IF EXISTS artist_shop_platform WITH (FORCE);"
docker-compose exec -T artist-shop-postgres psql -U postgres -c "\l"
sudo find /srv/artist-shop/images -mindepth 1 -delete
ls -ln /srv/artist-shop
docker-compose pull artist-shop-web
docker-compose up -d artist-shop-web
docker-compose logs -f artist-shop-web
docker image ls snowd3n/artist-shop
docker image prune
docker image prune
docker builder prune

On the VPS, never `docker system prune -a` or `docker volume prune`: other apps share it, and those remove the
images and volumes of any app that happens to be stopped.

## A brand-new Postgres volume instead

Only for a reason like a Postgres major-version upgrade. In place of step 3, using the backup from step 1:

1. Stop and remove both containers, then the volume:

       docker-compose stop artist-shop-web artist-shop-postgres
       docker-compose rm -f artist-shop-web artist-shop-postgres
       docker volume rm app_artist_shop_pg_volume

2. Start Postgres alone. The new volume runs `artist-shop-postgres-init/create-app-role.sh`, making the app's role:

       docker-compose up -d artist-shop-postgres

3. Restore identity, owned by the app's role as the app made it:

       docker-compose exec -T artist-shop-postgres psql -U postgres -c "CREATE DATABASE artist_shop_identity OWNER artist_shop_app;"
       docker-compose exec -T artist-shop-postgres pg_restore -U postgres -d artist_shop_identity --no-owner --role=artist_shop_app < ~/artist_shop_identity.dump

4. Empty the images folder (step 3.3), then carry on from step 4.
