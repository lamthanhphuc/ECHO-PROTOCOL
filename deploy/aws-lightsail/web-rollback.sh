#!/usr/bin/env bash
set -euo pipefail
root=${ECHO_DEPLOY_ROOT:-/home/ubuntu/echo-protocol}
cd "$root/deploy"
dir=${1:?operator script directory required}
sha=164d078ee0eb1bcfa2aa8c6379067d658e7904f8
old=sha256:f69d2511bd76aed0c117f3d86950dce54d8ef62e69608ca7f078d59efe1d4e1b
target=sha256:d4658e3fca4026840117d22f2ad2a479a46ddb16c80f74c884ec1b7e567a5150
tag="aws-lightsail-web:rollback-pre-$sha"
override=docker-compose.web-release.yml
dc=(docker compose -p echo-protocol --env-file .env.production -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml -f "$override")
test "$(docker image inspect "$tag" --format '{{.Id}}')" = "$old"
sha256sum -c "$dir/reviewed-compose.sha256" >/dev/null
test -f "$override" || { echo 'Unknown Web override state' >&2; exit 1; }
web=$("${dc[@]}" ps -q web)
current=missing
if test -n "$web"; then current=$(docker inspect --format '{{.Image}}' "$web"); fi
test "$current" = "$old" || test "$current" = "$target" || test "$current" = missing
other_before=$(for service in backend postgres mongodb caddy; do id=$("${dc[@]}" ps -q "$service"); test -n "$id" || exit 1; echo "$id:$(docker inspect --format '{{.RestartCount}}' "$id")"; done)
printf 'services:\n  web:\n    image: %s\n' "$tag" > "$dir/rollback-override.yml"
if ! cmp -s "$override" "$dir/rollback-override.yml"; then
  printf 'services:\n  web:\n    image: aws-lightsail-web:%s\n' "$sha" > "$dir/target-override.yml"
  cmp -s "$override" "$dir/target-override.yml" || { echo 'Refusing unknown Web override' >&2; exit 1; }
  cp "$dir/rollback-override.yml" "$override"
fi
if test "$current" != "$old"; then
  "${dc[@]}" up -d --pull never --no-deps --no-build --force-recreate web
fi
web=$("${dc[@]}" ps -q web)
test "$(docker inspect --format '{{.Image}}' "$web")" = "$old"
test "$other_before" = "$(for service in backend postgres mongodb caddy; do id=$("${dc[@]}" ps -q "$service"); echo "$id:$(docker inspect --format '{{.RestartCount}}' "$id")"; done)"
log_file=$(mktemp)
cleanup() { local rc=$?; rm -f "$log_file"; exit "$rc"; }
trap cleanup EXIT
docker logs --since 2m "$web" > "$log_file" 2>&1
if grep -Eiq 'Unhandled exception|FATAL|EADDRINUSE|Error:.*(startup|listen|database)' "$log_file"; then echo 'Critical Web rollback log detected' >&2; exit 1; fi
bash "$dir/web-smoke.sh"
echo WEB_ROLLBACK_VERIFIED
