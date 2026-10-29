#!/usr/bin/env bash
set -euo pipefail
root=${ECHO_DEPLOY_ROOT:-/home/ubuntu/echo-protocol}
cd "$root/deploy"
files=(-f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml)
for file in docker-compose.prod.yml docker-compose.mongo-kernel.yml docker-compose.backend-migration.override.yml .env.production; do
  test -r "$file" || { echo "Missing $file" >&2; exit 1; }
done
echo 'COMPOSE_SHA256'
sha256sum docker-compose.prod.yml docker-compose.mongo-kernel.yml docker-compose.backend-migration.override.yml
docker compose -p echo-protocol --env-file .env.production "${files[@]}" config --format json |
  python3 -c 'import json,sys; c=json.load(sys.stdin); print("PROJECT",c.get("name")); [(print("SERVICE",n,"IMAGE",s.get("image"),"MOUNTS",[(v.get("type"),v.get("source"),v.get("target")) for v in s.get("volumes",[])])) for n,s in c["services"].items()]; e=c["services"]["backend"].get("environment",{}); print("MIGRATE",e.get("Database__MigrateOnStartup"),"ADMIN_SEED",e.get("AdminSeed__Enabled"))'
if test -e docker-compose.web-release.yml; then
  echo 'WEB_OVERRIDE_SHA256'
  sha256sum docker-compose.web-release.yml
  docker compose -p echo-protocol --env-file .env.production "${files[@]}" -f docker-compose.web-release.yml config --format json |
    python3 -c 'import json,sys; c=json.load(sys.stdin); print("EFFECTIVE_WEB_IMAGE",c["services"]["web"].get("image"))'
else
  echo 'WEB_OVERRIDE_ABSENT'
fi
for service in backend web caddy postgres mongodb; do
  id=$(docker compose -p echo-protocol --env-file .env.production "${files[@]}" ps -q "$service")
  test -n "$id" || { echo "Missing container: $service" >&2; exit 1; }
  docker inspect --format '{{json .}}' "$id" | python3 -c 'import json,sys
d=json.load(sys.stdin)
state=d.get("State") or {}
health=(state.get("Health") or {}).get("Status") or "none"
mounts=[(m.get("Name") or m.get("Source") or "?")+":"+(m.get("Destination") or "?") for m in d.get("Mounts") or []]
files=(d.get("Config") or {}).get("Labels") or {}
print("SERVICE",sys.argv[1],"ID",d.get("Id"),"IMAGE",d.get("Image"),"STATUS",state.get("Status"),"HEALTH",health,"RESTARTS",d.get("RestartCount"),"COMPOSE_FILES",files.get("com.docker.compose.project.config_files"),"MOUNTS",mounts)' "$service"
done
df -h "$root"
free -h
