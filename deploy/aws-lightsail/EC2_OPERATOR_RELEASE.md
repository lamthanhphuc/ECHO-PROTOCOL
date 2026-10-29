# EC2 operator release: verified archive 99d06438

The release candidate is GitHub Actions run `40716370520`, artifact `ec2-images-99d06438cb26a3a31de9eaf3f56db02a0aa8d6c6`. It contains the Backend image for `linux/amd64`. This procedure is a **manual production gate**: a named operator must approve the maintenance window, backup evidence, migration plan, and application rollback compatibility before executing the mutation commands. Do not run it from an untrusted GitHub hosted runner or widen TCP/22.

## Read-only gate

Use the existing allowlisted operator network and pinned SSH host key. From the repository, copy `audit-live.sh` to the server only after approval to write a file, or pipe its reviewed contents over SSH without changing the server:

```bash
ssh -o StrictHostKeyChecking=yes -i ~/.ssh/echo-protocol-ec2-key.pem ubuntu@3.1.238.6 'bash -s' < deploy/aws-lightsail/audit-live.sh
curl -fsS https://api.3.1.238.6.sslip.io/health
```

The audit prints only Compose file hashes, selected service settings, container/image IDs, mount targets, migration count and resources. Compare the three server file hashes with copies reviewed offline. The repository Compose file is **not** the complete server configuration: it lacks the Mongo kernel and Backend migration overrides. Never print `.env.production` or unfiltered `docker compose config` output. Record container IDs, image IDs and restart counts before release. Confirm live match leases permit a maintenance window.

## Artifact and backup gate

Download the artifact from run `40716370520` on a trusted operator machine. In its extracted directory:

The Windows Docker Desktop containerd image ID can differ from the Linux Docker image ID despite matching archive, config and layer digests. Verify the archive SHA-256 on both ends; compare the recorded image ID only after `docker load` on EC2's Linux Docker Engine. Investigate any mismatch there before rollout.

```bash
sha=99d06438cb26a3a31de9eaf3f56db02a0aa8d6c6
test "$(cat COMMIT)" = "$sha"
sha256sum -c SHA256SUMS
gzip -t backend-image.tar.gz
```

Transfer the verified directory to `/home/ubuntu/echo-protocol/deploy-artifacts/$sha/` with the existing key and host-key checking. Compare `sha256sum backend-image.tar.gz` again on EC2. Check free disk before transfer and before backup.

For the approved backup step, run on EC2. `pg_dump` is a consistent logical snapshot and does not stop PostgreSQL. The Mongo dump may observe writes across collections; use a maintenance window or the existing replica-set-aware snapshot procedure if cross-collection consistency is required. These commands never mount the production volumes into a test container.

```bash
set -euo pipefail
cd /home/ubuntu/echo-protocol/deploy
stamp=$(date -u +%Y%m%dT%H%M%SZ)
backup="/home/ubuntu/echo-protocol/backups/pre-release-$stamp"
install -d -m 700 "$backup"
dc=(docker compose -p echo-protocol --env-file .env.production -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml)
pg=$("${dc[@]}" ps -q postgres)
mongo=$("${dc[@]}" ps -q mongodb)
test -n "$pg" && test -n "$mongo"
docker exec "$pg" sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner --no-acl' > "$backup/postgres.dump"
docker exec "$mongo" sh -c 'exec mongodump --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --archive --gzip' > "$backup/mongodb.archive.gz"
test -s "$backup/postgres.dump" && test -s "$backup/mongodb.archive.gz"
gzip -t "$backup/mongodb.archive.gz"
docker exec -i "$pg" pg_restore --list < "$backup/postgres.dump" > "$backup/postgres.contents.txt"
(cd "$backup" && sha256sum postgres.dump mongodb.archive.gz > SHA256SUMS)
```

Restore **both** backups on disposable containers before changing production. Use a unique test network and no production volume; the example keeps test data in disposable container filesystems. Docker removal below applies only to these named test containers/network after evidence is captured.

```bash
docker network create --internal echo-release-restore-test
docker run -d --name echo-release-pg-test --network echo-release-restore-test -e POSTGRES_PASSWORD=isolated-test-only postgres:16
docker run -d --name echo-release-mongo-test --network echo-release-restore-test -e GLIBC_TUNABLES=glibc.pthread.rseq=1 mongo:8.0
until docker exec echo-release-pg-test pg_isready -U postgres >/dev/null 2>&1; do sleep 2; done
until docker exec echo-release-mongo-test mongosh --quiet --eval 'db.adminCommand({ping:1}).ok' >/dev/null 2>&1; do sleep 2; done
docker exec echo-release-pg-test createdb -U postgres release_restore
docker exec -i echo-release-pg-test pg_restore -U postgres -d release_restore --exit-on-error < "$backup/postgres.dump"
docker exec echo-release-pg-test psql -U postgres -d release_restore -Atqc 'SELECT count(*) FROM "__EFMigrationsHistory"'
docker exec -i echo-release-mongo-test mongorestore --archive --gzip < "$backup/mongodb.archive.gz"
docker exec echo-release-mongo-test mongosh --quiet --eval 'printjson(db.adminCommand({listDatabases:1}).databases.map(x=>[x.name,x.sizeOnDisk]))'
```

Review collection counts and representative PostgreSQL table counts against read-only production queries. Apply any proposed idempotent EF SQL to the **isolated** restored database first, check data retention, then approve a separate production migration. Startup migration and admin seed must remain disabled. A changed schema may make the old Backend image unsafe; decide rollback compatibility before rollout. Cleanup only the named disposable containers/network after recording restore evidence:

```bash
docker rm -f echo-release-pg-test echo-release-mongo-test
docker network rm echo-release-restore-test
```

## Approved Backend rollout

Use the exact full SHA and recorded expected image ID from the artifact. Keep the previous image by ID with an immutable rollback tag. Preserve all three live Compose files and `.env.production`; copy them into the restricted backup directory. The image-only override is a fourth Compose file, created only after the live configuration has been audited.

```bash
set -euo pipefail
cd /home/ubuntu/echo-protocol/deploy
sha=99d06438cb26a3a31de9eaf3f56db02a0aa8d6c6
release="/home/ubuntu/echo-protocol/deploy-artifacts/$sha"
backup=/home/ubuntu/echo-protocol/backups/pre-release-REPLACE_WITH_VERIFIED_STAMP
test -s "$backup/postgres.dump" && test -s "$backup/mongodb.archive.gz"
(cd "$backup" && sha256sum -c SHA256SUMS)
dc=(docker compose -p echo-protocol --env-file .env.production -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml)
test "$(cat "$release/COMMIT")" = "$sha"
(cd "$release" && sha256sum -c SHA256SUMS)
backend=$("${dc[@]}" ps -q backend)
old_id=$(docker inspect --format '{{.Image}}' "$backend")
rollback="aws-lightsail-backend:rollback-pre-$sha"
if docker image inspect "$rollback" >/dev/null 2>&1; then
  test "$(docker image inspect "$rollback" --format '{{.Id}}')" = "$old_id"
else
  docker tag "$old_id" "$rollback"
fi
cp -p docker-compose.prod.yml docker-compose.mongo-kernel.yml docker-compose.backend-migration.override.yml .env.production "$backup/"
if test -f docker-compose.backend-release.yml; then cp -p docker-compose.backend-release.yml "$backup/"; fi
printf '%s\n' "$old_id" > "$backup/backend-image-id-before.txt"
chmod 600 "$backup/.env.production"
gzip -dc "$release/backend-image.tar.gz" | docker load
test "$(docker image inspect "aws-lightsail-backend:$sha" --format '{{.Id}}')" = "$(cat "$release/backend-image-id.txt")"
test "$(docker image inspect "aws-lightsail-backend:$sha" --format '{{.Config.Labels.org.opencontainers.image.revision}}')" = "$sha"
test "$(docker image inspect "aws-lightsail-backend:$sha" --format '{{.Os}}/{{.Architecture}}')" = linux/amd64
printf 'services:\n  backend:\n    image: aws-lightsail-backend:%s\n' "$sha" > docker-compose.backend-release.yml
chmod 600 docker-compose.backend-release.yml
"${dc[@]}" -f docker-compose.backend-release.yml config --format json | python3 -c 'import json,sys; b=json.load(sys.stdin)["services"]["backend"]; e=b["environment"]; assert str(e.get("Database__MigrateOnStartup")).lower()=="false"; assert str(e.get("AdminSeed__Enabled")).lower()=="false"; print("Startup migration and admin seed disabled")'
"${dc[@]}" -f docker-compose.backend-release.yml up -d --no-deps --no-build --force-recreate backend
test "$(docker inspect --format '{{.Image}}' "$("${dc[@]}" -f docker-compose.backend-release.yml ps -q backend)")" = "$(cat "$release/backend-image-id.txt")"
```

Replace the backup path with the verified directory from the same maintenance window. Before `up`, inspect the effective Compose JSON for image/mount/flag drift using `audit-live.sh` logic, and confirm PostgreSQL has the migration count expected by this exact Backend commit. Do not run migration SQL implicitly as part of image rollout.

Run `smoke-live.sh` with `WEB_URL` set to the real Web HTTPS origin. It requires HTTP 200 and both database checks Healthy; additionally verify auth login with a designated test account, match authority read endpoints, preserved player/wallet/match counts, and stable container logs without printing tokens or secrets. Re-run `audit-live.sh` to confirm MongoDB/PostgreSQL container IDs, restart counts and mounts are unchanged. Mark success only after those checks.

## Application rollback gate

If Backend startup or smoke checks fail, stop rollout. **First** assess whether migrations or startup catalog seeding introduced writes incompatible with the old image. Never restore the database automatically. If old-image compatibility is confirmed, use the saved rollback tag:

```bash
set -euo pipefail
cd /home/ubuntu/echo-protocol/deploy
sha=99d06438cb26a3a31de9eaf3f56db02a0aa8d6c6
rollback="aws-lightsail-backend:rollback-pre-$sha"
test -n "$(docker image inspect "$rollback" --format '{{.Id}}')"
printf 'services:\n  backend:\n    image: %s\n' "$rollback" > docker-compose.backend-release.yml
docker compose -p echo-protocol --env-file .env.production -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml -f docker-compose.backend-release.yml up -d --no-deps --no-build --force-recreate backend
test "$(docker inspect --format '{{.Image}}' "$(docker compose -p echo-protocol --env-file .env.production -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml -f docker-compose.backend-migration.override.yml -f docker-compose.backend-release.yml ps -q backend)")" = "$(docker image inspect "$rollback" --format '{{.Id}}')"
```

Run `smoke-live.sh` again and record the incident. If schema compatibility is unknown, leave the release stopped and escalate; application rollback alone cannot reverse a database migration.

## Transport and remaining blockers

The smallest secure current path is operator-mediated SSH from an existing allowlisted `/32`, using the existing key and host verification. This avoids IAM/SSM changes and avoids exposing SSH to dynamic hosted-runner IPs. EC2 has no instance profile or SSM management, so OIDC to SSM needs approved AWS changes. Keep release creation in GitHub Actions and production execution with the human operator until an independently verified approval environment and backup/rollback rehearsal exist.

Current network cannot reach port 22, so the live Compose audit and restore rehearsal are pending. No production command above is authorized by this document alone.
