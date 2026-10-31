#!/usr/bin/env bash
# ECHO PROTOCOL - manually approved, single-service EC2 application release.
# Safety: does not back up/restore databases, never runs docker compose down.
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  bash echo-ec2-operator-release.sh plan --service backend|web --image REGISTRY/echo-protocol/SERVICE@sha256:DIGEST --revision GIT_SHA
  bash echo-ec2-operator-release.sh apply --service backend|web --image ... --revision ... \
      --backup-dir /home/ubuntu/echo-protocol/backups/pre-release-TIMESTAMP \
      --restore-evidence /path/to/reviewed-restore-evidence.txt \
      --approval APPROVE_ECHO_APPLICATION_RELEASE

The operator must review a fresh, verified backup, an isolated restore, database
compatibility and the maintenance window BEFORE using apply. The approval argument
is an operator confirmation, not a cryptographic access-control mechanism.
EOF
}

[[ $# -gt 0 ]] || { usage; exit 2; }
if [[ "$1" == -h || "$1" == --help ]]; then usage; exit 0; fi
action=$1; shift
[[ "$action" == plan || "$action" == apply ]] || { usage; exit 2; }
service= image= revision= backup_dir= restore_evidence= approval=
while (( $# )); do
  case "$1" in
    --service|--image|--revision|--backup-dir|--restore-evidence|--approval)
      (( $# >= 2 )) || { echo "Missing argument for $1" >&2; exit 2; }
      case "$1" in
        --service) service=$2 ;;
        --image) image=$2 ;;
        --revision) revision=$2 ;;
        --backup-dir) backup_dir=$2 ;;
        --restore-evidence) restore_evidence=$2 ;;
        --approval) approval=$2 ;;
      esac
      shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
done

[[ "$service" == backend || "$service" == web ]] || { echo "Invalid service" >&2; exit 2; }
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] || { echo "Invalid Git SHA" >&2; exit 2; }
registry='430013477435.dkr.ecr.ap-southeast-1.amazonaws.com'
expected_prefix="$registry/echo-protocol/$service@sha256:"
[[ "$image" == "$expected_prefix"* && "${image#"$expected_prefix"}" =~ ^[0-9a-f]{64}$ ]] || {
  echo "Invalid ECR immutable digest or service mismatch" >&2; exit 2;
}

printf 'RELEASE_PLAN service=%s revision=%s image=%s\n' "$service" "$revision" "$image"
if [[ "$action" == plan ]]; then
  echo 'PLAN_ONLY_NO_PRODUCTION_CHANGES'
  exit 0
fi

# An explicit, per-run human approval is mandatory. Neither a CI job nor an
# environment flag can silently turn this tool into an unattended deployment.
[[ "$approval" == APPROVE_ECHO_APPLICATION_RELEASE ]] || {
  echo 'BLOCKED: explicit operator approval missing' >&2; exit 3;
}
[[ "$backup_dir" == /home/ubuntu/echo-protocol/backups/pre-release-* ]] || {
  echo 'BLOCKED: fresh pre-release backup directory required' >&2; exit 3;
}
[[ -d "$backup_dir" && -f "$backup_dir/SHA256SUMS" && -s "$backup_dir/postgres.dump" && -s "$backup_dir/mongodb.archive.gz" ]] || {
  echo 'BLOCKED: missing backup files' >&2; exit 3;
}
[[ -f "$restore_evidence" ]] || { echo 'BLOCKED: reviewed isolated-restore evidence required' >&2; exit 3; }
# Evidence file is for operator review, not proof of authenticity. Require the
# audit markers to avoid accepting the known old 29-migration backup by mistake.
grep -Fxq 'POSTGRES_RESTORE_PASS' "$restore_evidence" || {
  echo 'BLOCKED: restore evidence lacks PostgreSQL PASS' >&2; exit 3;
}
grep -Fxq 'MONGODB_RESTORE_PASS' "$restore_evidence" || {
  echo 'BLOCKED: restore evidence lacks MongoDB PASS' >&2; exit 3;
}
grep -Fxq 'PRODUCTION_SCHEMA_COMPATIBILITY_APPROVED' "$restore_evidence" || {
  echo 'BLOCKED: schema compatibility not approved' >&2; exit 3;
}
grep -Fxq 'MAINTENANCE_WINDOW_APPROVED' "$restore_evidence" || {
  echo 'BLOCKED: maintenance window not approved' >&2; exit 3;
}

# Abort if the operator uses this script from a checkout or another host.
deploy_dir=/home/ubuntu/echo-protocol/deploy
[[ -d "$deploy_dir" ]] || { echo 'BLOCKED: deployment directory missing' >&2; exit 3; }
for x in docker python3 curl sha256sum flock; do command -v "$x" >/dev/null || { echo "Missing $x" >&2; exit 3; }; done
umask 077
cd "$deploy_dir"
files=(docker-compose.prod.yml docker-compose.mongo-kernel.yml docker-compose.backend-migration.override.yml)
for f in "${files[@]}" .env.production; do [[ -r "$f" ]] || { echo "Missing $f" >&2; exit 3; }; done
# Every override created by this script carries a strict ownership marker.
# An existing managed override can be reused for the next release ONLY if its
# pinned image still resolves to the image ID running in the selected service.
# Unrecognized overrides remain a manual reconciliation gate.
override="docker-compose.operator-$service-release.yml"
exec 9>"$deploy_dir/.echo-operator-release.lock"
flock -n 9 || { echo 'BLOCKED: concurrent release in progress' >&2; exit 3; }

# Verify checksum in restricted directory; never echo contents of .env.production.
for item in postgres.dump mongodb.archive.gz; do
  grep -Eq "^[[:xdigit:]]{64} [ *]${item//./\.}$" "$backup_dir/SHA256SUMS" || {
    echo "BLOCKED: missing checksum for $item" >&2; exit 3;
  }
done
(cd "$backup_dir" && sha256sum -c SHA256SUMS >/dev/null)
# Reject backups older than 24h without a human override path.
now=$(date +%s)
for item in "$backup_dir/postgres.dump" "$backup_dir/mongodb.archive.gz"; do
  age=$(( now - $(stat -c %Y "$item") ))
  (( age >= 0 && age <= 86400 )) || { echo 'BLOCKED: backup stale or future dated' >&2; exit 3; }
done

dc=(docker compose -p echo-protocol --env-file .env.production)
for f in "${files[@]}"; do dc+=(-f "$f"); done
# Prohibit missing services and unexpected migration/seed toggles.
"${dc[@]}" config --format json | python3 -c '
import json,sys
c=json.load(sys.stdin)["services"]
assert set(("backend","web","postgres","mongodb","caddy")).issubset(c)
e=c["backend"].get("environment",{})
assert str(e.get("Database__MigrateOnStartup")).lower()=="false"
assert str(e.get("AdminSeed__Enabled")).lower()=="false"
' || { echo 'BLOCKED: Compose safety configuration invalid' >&2; exit 3; }

# Independent DB migration count gate. The user-provided restore evidence must
# also be reviewed against migration IDs and DB row counts before apply.
pg_id="$("${dc[@]}" ps -q postgres)"
[[ -n "$pg_id" ]] || { echo 'BLOCKED: postgres container missing' >&2; exit 3; }
live_migrations=$(docker exec "$pg_id" sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Atqc '\''SELECT count(*) FROM "__EFMigrationsHistory"'\''' | tr -d '\r\n')
[[ "$live_migrations" =~ ^[0-9]+$ ]] || { echo 'BLOCKED: invalid migration count' >&2; exit 3; }
grep -Fxq "RESTORED_MIGRATION_COUNT=$live_migrations" "$restore_evidence" || {
  echo 'BLOCKED: restored migration baseline does not match live production' >&2; exit 3;
}

# Check exact image already downloaded from ECR and has matching build label.
docker image inspect "$image" >/dev/null
[[ "$(docker image inspect "$image" --format '{{.Os}}/{{.Architecture}}')" == linux/amd64 ]] || exit 3
[[ "$(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')" == "$revision" ]] || exit 3
docker image inspect "$image" --format '{{range .RepoDigests}}{{println .}}{{end}}' | grep -Fxq "$image" || {
  echo 'BLOCKED: Docker image digest mismatch' >&2; exit 3;
}

# Snapshot IDs and mounts/restarts for services that must remain untouched.
unchanged=(postgres mongodb caddy)
if [[ "$service" == backend ]]; then unchanged+=(web); else unchanged+=(backend); fi
snapshot() {
  local s id
  for s in "${unchanged[@]}"; do
    id="$("${dc[@]}" ps -q "$s")"; [[ -n "$id" ]] || return 1
    docker inspect -f '{{.Id}}|{{.Image}}|{{.RestartCount}}|{{range .Mounts}}{{.Type}}:{{.Name}}:{{.Destination}};{{end}}' "$id"
  done
}
before=$(snapshot) || { echo 'BLOCKED: related service missing' >&2; exit 3; }
container_id="$("${dc[@]}" ps -q "$service")"
[[ -n "$container_id" ]] || { echo 'BLOCKED: target service missing' >&2; exit 3; }
old_id=$(docker inspect "$container_id" --format '{{.Image}}')
# Repeated operator releases must not deadlock on an override that this script
# intentionally keeps as the persisted image pin after a successful release.
if [[ -e "$override" ]]; then
  [[ -f "$override" && ! -L "$override" ]] || {
    echo 'BLOCKED: release override is not a regular file' >&2; exit 3;
  }
  existing_image="$(python3 - "$override" "$service" <<'PY_CHECK'
import pathlib, re, sys
path, service = sys.argv[1:]
lines = pathlib.Path(path).read_text(encoding='utf-8').splitlines()
expected = ['# ECHO_OPERATOR_MANAGED_OVERRIDE', 'services:', f'  {service}:']
if len(lines) != 4 or lines[:3] != expected or not lines[3].startswith('    image: '):
    raise SystemExit(1)
image = lines[3][len('    image: '):]
if not re.fullmatch(r'[A-Za-z0-9_./:@-]+', image):
    raise SystemExit(1)
print(image)
PY_CHECK
  )" || { echo 'BLOCKED: release override is not managed by this tool' >&2; exit 3; }
  existing_id="$(docker image inspect "$existing_image" --format '{{.Id}}')" || {
    echo 'BLOCKED: pinned previous release image is unavailable' >&2; exit 3;
  }
  [[ "$existing_id" == "$old_id" ]] || {
    echo 'BLOCKED: existing release override disagrees with running image' >&2; exit 3;
  }
  echo 'MANAGED_EXISTING_RELEASE_OVERRIDE_VERIFIED'
fi
new_id=$(docker image inspect "$image" --format '{{.Id}}')
[[ "$old_id" != "$new_id" ]] || { echo 'BLOCKED: target image already running; no deployment needed' >&2; exit 3; }
rollback_tag="aws-lightsail-$service:rollback-pre-$revision"
if docker image inspect "$rollback_tag" >/dev/null 2>&1; then
  [[ "$(docker image inspect "$rollback_tag" --format '{{.Id}}')" == "$old_id" ]] || {
    echo 'BLOCKED: rollback tag conflicts with current image' >&2; exit 3;
  }
else
  docker tag "$old_id" "$rollback_tag"
fi
# Make a restricted copy of production Compose settings/secret environment in
# the already approved backup folder. Never log its contents.
for f in "${files[@]}" .env.production; do cp -p "$f" "$backup_dir/"; done
chmod 600 "$backup_dir/.env.production"

# Use Caddy configured hostnames (avoid reading .env.production values).
resolve_domain() {
  docker inspect echo-protocol-caddy-1 --format '{{range .Config.Env}}{{println .}}{{end}}' |
    sed -n "s/^$1=//p"
}
web_domain=$(resolve_domain WEB_DOMAIN)
api_domain=$(resolve_domain API_DOMAIN)
domain_regex='^[A-Za-z0-9][A-Za-z0-9.-]+$'
[[ "$web_domain" =~ $domain_regex && "$api_domain" =~ $domain_regex ]] || {
  echo 'BLOCKED: cannot resolve public HTTPS domains' >&2; exit 3;
}
smoke() {
  local tmp code
  tmp=$(mktemp)
  code=$(curl --connect-timeout 4 --max-time 12 -fsS -o "$tmp" -w '%{http_code}' "https://$api_domain/health") || { rm -f "$tmp"; return 1; }
  [[ "$code" == 200 ]] || { rm -f "$tmp"; return 1; }
  python3 - "$tmp" <<'PY'
import json,sys
x=json.load(open(sys.argv[1],encoding="utf-8"))
assert x.get("status")=="Healthy"
assert x.get("checks",{}).get("postgresql")=="Healthy"
assert x.get("checks",{}).get("mongodb")=="Healthy"
PY
  local rc=$?; rm -f "$tmp"; ((rc==0)) || return 1
  [[ "$(curl --connect-timeout 4 --max-time 12 -fsS -o /dev/null -w '%{http_code}' "https://$web_domain/login")" == 200 ]]
}
smoke || { echo 'BLOCKED: baseline HTTPS smoke failed' >&2; exit 3; }

# Preserve enough evidence to reconcile a SIGKILL, host restart, or an interrupted
# Compose transaction. An interrupted run must NEVER auto-restart the databases.
# The original override (if any) and image IDs are saved before the first write
# to the live override. Restore only after inspecting live state per the runbook.
recovery_state="$backup_dir/operator-$service-predeploy.state"
previous_override="$backup_dir/operator-$service-override.before.yml"
[[ ! -e "$recovery_state" && ! -L "$recovery_state" && ! -e "$previous_override" && ! -L "$previous_override" ]] || {
  echo 'BLOCKED: recovery evidence already exists in backup directory' >&2; exit 3;
}
if [[ -e "$override" ]]; then
  cp -p -- "$override" "$previous_override"
  chmod 600 "$previous_override"
  original_override=present
else
  original_override=absent
fi
printf 'SERVICE=%s\nREVISION=%s\nOLD_IMAGE_ID=%s\nNEW_IMAGE_ID=%s\nNEW_IMAGE_REF=%s\nROLLBACK_TAG=%s\nORIGINAL_OVERRIDE=%s\n' \
  "$service" "$revision" "$old_id" "$new_id" "$image" "$rollback_tag" "$original_override" >"$recovery_state"
chmod 600 "$recovery_state"
echo 'INTERRUPTED_RELEASE_RECOVERY_EVIDENCE_SAVED'

# Atomic one-service image override; compose may retain build: but --no-build
# prevents any build. Up must never start dependent databases.
write_override() {
  local next_image=$1
  printf '# ECHO_OPERATOR_MANAGED_OVERRIDE\nservices:\n  %s:\n    image: %s\n' "$service" "$next_image" >"$override.tmp"
  chmod 600 "$override.tmp"
  mv -f "$override.tmp" "$override"
}
apply_override() {
  "${dc[@]}" -f "$override" up -d --no-deps --no-build --pull never --force-recreate "$service"
}
verify_target() {
  local expected_id=$1 target
  target="$("${dc[@]}" -f "$override" ps -q "$service")"
  [[ -n "$target" && "$(docker inspect "$target" -f '{{.Image}}')" == "$expected_id" ]] || return 1
  [[ "$(docker inspect "$target" -f '{{.State.Running}}')" == true ]] || return 1
}
check_unchanged() { [[ "$(snapshot)" == "$before" ]]; }
wait_for_smoke() {
  local n
  for n in {1..15}; do
    if smoke; then return 0; fi
    sleep 3
  done
  return 1
}
rollback() {
  echo 'RELEASE_SMOKE_FAILED_ROLLBACK_ATTEMPTED'
  write_override "$rollback_tag"
  if apply_override && verify_target "$old_id" && wait_for_smoke && check_unchanged; then
    echo 'APPLICATION_ROLLBACK_VERIFIED_PASS'
    return 0
  fi
  echo 'CRITICAL: rollback unsuccessful; operator intervention required' >&2
  return 1
}

# From this point the target service may be recreated. Explicit rollback on
# ordinary command/health failures; SIGKILL/host failures require operator.
echo 'OPERATOR_APPROVED_APPLICATION_DEPLOYMENT_START'
write_override "$image"
if apply_override && verify_target "$new_id" && wait_for_smoke && check_unchanged; then
  sleep 5
  if smoke && check_unchanged; then
    echo 'APPLICATION_RELEASE_VERIFIED_PASS'
    echo 'DATABASE_CONTAINERS_UNCHANGED_PASS'
    exit 0
  fi
fi
rollback || exit 5
exit 4  # A failed release remains failed even after a successful rollback.
