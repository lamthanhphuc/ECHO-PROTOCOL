#!/usr/bin/env bash
set -euo pipefail
cd /home/ubuntu/echo-protocol/deploy
files=(-f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml)
for file in docker-compose.prod.yml docker-compose.mongo-kernel.yml docker-compose.backend-migration.override.yml .env.production; do
  test -r "$file" || { echo "Missing required production file: $file" >&2; exit 1; }
done
echo 'Compose file SHA-256:'
sha256sum docker-compose.prod.yml docker-compose.mongo-kernel.yml docker-compose.backend-migration.override.yml
docker compose -p echo-protocol --env-file .env.production "${files[@]}" config --format json |
  python3 -c 'import json,sys; c=json.load(sys.stdin); print("Project:",c.get("name")); [(print("Service:",n,"image:",s.get("image"),"mount targets:",[v.get("target") for v in s.get("volumes",[])],"migrate-on-startup:",s.get("environment",{}).get("Database__MigrateOnStartup"),"admin-seed:",s.get("environment",{}).get("AdminSeed__Enabled"))) for n,s in c["services"].items()]'
echo 'Containers (IDs, images, state, health):'
docker compose -p echo-protocol --env-file .env.production "${files[@]}" ps --format json |
  python3 -c 'import json,sys; raw=sys.stdin.read().strip(); rows=json.loads(raw) if raw.startswith("[") else [json.loads(x) for x in raw.splitlines()]; [(print(x.get("Service"),x.get("ID"),x.get("Image"),x.get("State"),x.get("Health"))) for x in rows]'
for service in backend web caddy postgres mongodb; do
  id=$(docker compose -p echo-protocol --env-file .env.production "${files[@]}" ps -q "$service")
  test -n "$id" || { echo "Missing container: $service" >&2; continue; }
  docker inspect --format "$service image-id={{.Image}} restart-count={{.RestartCount}} mounts={{range .Mounts}}{{.Name}}:{{.Destination}} {{end}}" "$id"
done
echo 'Resources:'
df -h /home/ubuntu/echo-protocol
free -h
echo 'PostgreSQL migration count:'
pg=$(docker compose -p echo-protocol --env-file .env.production "${files[@]}" ps -q postgres)
docker exec "$pg" sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atqc '\''SELECT count(*) FROM "__EFMigrationsHistory"'\'''
