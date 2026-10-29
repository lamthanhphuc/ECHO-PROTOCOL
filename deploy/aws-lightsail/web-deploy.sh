#!/usr/bin/env bash
set -euo pipefail
umask 077
root=${ECHO_DEPLOY_ROOT:-/home/ubuntu/echo-protocol}
cd "$root/deploy"
dir=${1:?operator script directory required}
sha=164d078ee0eb1bcfa2aa8c6379067d658e7904f8
old=sha256:f69d2511bd76aed0c117f3d86950dce54d8ef62e69608ca7f078d59efe1d4e1b
target=sha256:d4658e3fca4026840117d22f2ad2a479a46ddb16c80f74c884ec1b7e567a5150
tag="aws-lightsail-web:$sha"
rollback="aws-lightsail-web:rollback-pre-$sha"
override=docker-compose.web-release.yml
base=(docker compose -p echo-protocol --env-file .env.production -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml)
dc=("${base[@]}" -f "$override")
release="$root/deploy-artifacts/$sha"
rollback_needed=0
base_json=
target_json=
log_file=
on_exit() {
  rc=$?
  trap - EXIT
  test -z "$base_json" || rm -f "$base_json"
  test -z "$target_json" || rm -f "$target_json"
  test -z "$log_file" || rm -f "$log_file"
  if test "$rc" -ne 0 && test "$rollback_needed" -eq 1; then
    if bash "$dir/web-rollback.sh" "$dir"; then echo WEB_DEPLOY_FAILED_ROLLED_BACK; else echo CRITICAL_WEB_ROLLBACK_FAILED >&2; exit 2; fi
  fi
  exit "$rc"
}
trap on_exit EXIT
trap 'exit 1' HUP INT TERM
test "$(cat "$release/COMMIT")" = "$sha"
python3 - "$release/SHA256SUMS" "$release/web-image.tar.gz" "$release/web-image-id.txt" <<'PY'
import hashlib, json, pathlib, re, sys, tarfile
entries = [re.fullmatch(r'([0-9a-fA-F]{64})  (backend-image\.tar\.gz|web-image\.tar\.gz)', line)
           for line in pathlib.Path(sys.argv[1]).read_text().splitlines()]
assert entries and all(entries)
checksums = {entry[2]: entry[1].lower() for entry in entries}
assert len(entries) == len(checksums) and 'web-image.tar.gz' in checksums
digest = hashlib.sha256()
with open(sys.argv[2], 'rb') as image:
    for chunk in iter(lambda: image.read(1024 * 1024), b''):
        digest.update(chunk)
assert digest.hexdigest() == checksums['web-image.tar.gz'], 'Web archive checksum mismatch'
with tarfile.open(sys.argv[2], 'r:gz') as archive:
    manifest = json.load(archive.extractfile('manifest.json'))
    assert len(manifest) == 1
    config = archive.extractfile(manifest[0]['Config']).read()
    assert 'sha256:' + hashlib.sha256(config).hexdigest() == pathlib.Path(sys.argv[3]).read_text().strip()
PY
test "$(docker image inspect "$tag" --format '{{.Id}}')" = "$target"
test "$(docker image inspect "$tag" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')" = "$sha"
test "$(docker image inspect "$tag" --format '{{.Os}}/{{.Architecture}}')" = linux/amd64
test "$(docker image inspect "$tag" --format '{{len .RootFS.Layers}}')" = 9
test "$(docker image inspect "$rollback" --format '{{.Id}}')" = "$old"
python3 - "$dir/reviewed-compose.sha256" <<'PY'
import pathlib, sys
names = [line.split('  ', 1)[1] for line in pathlib.Path(sys.argv[1]).read_text().splitlines()]
assert set(names) == {'docker-compose.prod.yml', 'docker-compose.mongo-kernel.yml', 'docker-compose.backend-migration.override.yml'} and len(names) == 3
PY
sha256sum -c "$dir/reviewed-compose.sha256" >/dev/null
test -r .env.production
base_json=$(mktemp)
target_json=$(mktemp)
"${base[@]}" config --format json > "$base_json"
printf 'services:\n  web:\n    image: %s\n' "$tag" > "$dir/target-override.yml"
if test -e "$override"; then
  cmp -s "$override" "$dir/target-override.yml" || {
    printf 'services:\n  web:\n    image: %s\n' "$rollback" > "$dir/rollback-override.yml"
    cmp -s "$override" "$dir/rollback-override.yml" || { echo 'Refusing unknown Web override' >&2; exit 1; }
  }
fi
"${base[@]}" -f "$dir/target-override.yml" config --format json > "$target_json"
python3 - "$base_json" "$target_json" "$tag" <<'PY'
import json, sys
base, new = [json.load(open(path, encoding='utf-8')) for path in sys.argv[1:3]]
backend = base['services']['backend']['environment']
assert str(backend['Database__MigrateOnStartup']).lower() == 'false'
assert str(backend['AdminSeed__Enabled']).lower() == 'false'
base['services']['web']['image'] = sys.argv[3]
assert base == new, 'Effective Compose changed beyond web.image'
PY
web=$("${base[@]}" ps -q web)
current=$(docker inspect --format '{{.Image}}' "$web")
test "$current" = "$old" || test "$current" = "$target"
other_before=$(for service in backend postgres mongodb caddy; do id=$("${base[@]}" ps -q "$service"); test -n "$id" || exit 1; echo "$id:$(docker inspect --format '{{.RestartCount}}' "$id")"; done)
bash "$dir/web-smoke.sh"
if test "$current" = "$target"; then
  test -f "$override" && cmp -s "$override" "$dir/target-override.yml"
  echo WEB_DEPLOYED_ALREADY
  exit 0
fi
echo 'FINAL_RELEASE_PREFLIGHT_PASSED'
cp "$dir/target-override.yml" "$override"
rollback_needed=1
"${dc[@]}" up -d --pull never --no-deps --no-build --force-recreate web
web=$("${dc[@]}" ps -q web)
test "$(docker inspect --format '{{.Image}}' "$web")" = "$target"
test "$other_before" = "$(for service in backend postgres mongodb caddy; do id=$("${dc[@]}" ps -q "$service"); echo "$id:$(docker inspect --format '{{.RestartCount}}' "$id")"; done)"
log_file=$(mktemp)
docker logs --since 2m "$web" > "$log_file" 2>&1
if grep -Eiq 'Unhandled exception|FATAL|EADDRINUSE|Error:.*(startup|listen|database)' "$log_file"; then echo 'Critical Web startup log detected' >&2; exit 1; fi
bash "$dir/web-smoke.sh"
rollback_needed=0
echo WEB_DEPLOYED
