# ECHO PROTOCOL CI and EC2 release candidates

For the current LTP Web-only operator release, see [WEB_RELEASE_164d078.md](WEB_RELEASE_164d078.md). It uses SCP-transferred, checksum-checked scripts and keeps production deployment behind the human gate.

## What runs now

- `.github/workflows/aed-backend.yml` checks pull requests and pushes to `LTP` and `main`: the mirrored AED contract when that script exists, .NET 8 solution build, all Backend tests with isolated PostgreSQL 16 and MongoDB 8 (`GLIBC_TUNABLES=glibc.pthread.rseq=1`), plus Next.js lint, typecheck and build. A separate job also builds both `linux/amd64` Docker images on the hosted runner. It uploads Backend TRX, Web check logs and image IDs. A zero-test run fails.
- `.github/workflows/ec2-release-candidate.yml` is manual. From `LTP` or `main`, it requires successful **push** CI for the exact branch SHA, then builds `linux/amd64` Backend and/or Web images on a GitHub runner, tags each with the **full** Git commit SHA, and uploads compressed `docker save` archives with SHA-256 and image IDs. It verifies gzip integrity and reloads each archive to compare image IDs. It does not access EC2 or production secrets. CI image checks and release archives are separate builds because CI currently uploads only image IDs; retaining full archives for every PR would consume unnecessary artifact storage.
- GitHub requires a `workflow_dispatch` workflow file on the default branch before it can be dispatched. The workflow must land on `main` before its first candidate build; a PR that exists only on `LTP` is insufficient.
- Unity EditMode/PlayMode are not CI gates yet. A licensed Unity 6000.5.8f1 runner with the required Photon Fusion packages and a reproducible noninteractive test setup is needed before those results can be trusted in GitHub Actions. Current local Editor results cannot certify a different CI commit.
- At implementation time, `npm audit --omit=dev` found 3 production dependency advisories (Next.js critical; `sharp` and `source-map-js` high). Separate Web-only security PRs #28 (`LTP`) and #29 (`main`) update Next.js to 16.4.3 and the transitive packages; the updated lockfile has 0 production advisories. Review and merge those PRs independently before a Web release.

## SWP490 evidence boundary

The verified PR runs before these final workflow changes reported 758/758 Backend tests on `LTP` and 387/387 on `main`, with Web and image jobs passing. Record the final run URLs and TRX artifacts in Report 5 only after the final SHA checks pass. The repository does not contain a committed SWP490 test-case ID ledger, Report 4/5 source, or a `review-2` ref. Therefore neither the required source baseline nor the fraction of committed test cases automated can be verified here; xUnit counts are not a system-wide coverage denominator. Create the ID ledger and map each CI test to its committed case before claiming the 50% criterion. Unity EditMode, PlayMode and manual Fusion Host–Client tests remain separate evidence.

## Existing EC2 deployment and drift

The existing instance is `i-00689d79dfa51a384` (`t3.small`, `ap-southeast-1`). The Docker Compose project is `echo-protocol` in `/home/ubuntu/echo-protocol/deploy`. The last successful direct inspection found these **three** active files:

1. `docker-compose.prod.yml`
2. `docker-compose.mongo-kernel.yml` (`GLIBC_TUNABLES=glibc.pthread.rseq=1` for MongoDB 8 on the current kernel)
3. `docker-compose.backend-migration.override.yml` (`Database__MigrateOnStartup=false`, `AdminSeed__Enabled=false`)

The repository's `docker-compose.prod.yml` alone does not describe production. Its Backend/Web build contexts refer to source directories absent from the server. Keep all existing production files and use an additional **image-only** override for a release. Do not copy the repository Compose file over production without a reviewed comparison. Production PostgreSQL and MongoDB use persistent Docker volumes; MongoDB also has a `/data/configdb` volume. A prior read-only inspection found 13 GiB free disk, but recheck before a release.

The operator subsequently completed a read-only live audit over AWS SSM: five containers were running with zero restarts, the three reviewed Compose hashes matched, the old Web image remained active, and no Web release override existed. Recheck containers, mounts, Compose hashes and available disk before deployment. Never print `.env.production` or full `docker compose config` output: it contains secrets.

## Manual release procedure on the existing instance

1. Review a successful CI run for the exact SHA. Dispatch **EC2 release candidate** on the trusted `LTP` or `main` ref. Download its `ec2-images-<sha>` artifact through GitHub. Verify `COMMIT`, `SHA256SUMS` and the recorded image IDs locally.
2. Use the operator's existing `$HOME\.ssh\echo-ssm.conf` and `echo-ec2-ssm` host alias to transfer the selected archive into a restricted directory under `/home/ubuntu/echo-protocol/deploy-artifacts/<sha>/`. Keep strict SSH host-key verification enabled. This SSM path needs no fixed operator IP or inbound TCP/22.
3. On EC2, recheck the effective three-file Compose config, current image IDs, container IDs, mounts, PostgreSQL migration history, current data counts, memory and disk. Make sure no live match lease makes the maintenance window unsafe. Save the current Compose files and `.env.production` with restricted permissions. Tag the old Backend/Web images with immutable rollback tags before loading replacements.
4. Create fresh PostgreSQL (`pg_dumpall`) and MongoDB (`mongodump --archive --gzip`) backups. Verify SHA-256, gzip integrity and restore each backup in disposable containers without production volumes. MongoDB's disposable container needs the kernel `GLIBC_TUNABLES` setting above. Keep the backups; never prune production volumes.
5. If this release adds EF migrations, generate an idempotent SQL script **for this exact commit**, review every operation, apply it to the restored PostgreSQL backup, compare preserved data, and decide whether the old Backend can start against the new schema. Apply to production only after those checks. Never rely on image rollback to reverse database changes. Keep `Database__MigrateOnStartup=false` and `AdminSeed__Enabled=false`.
6. Verify the artifact SHA, then `docker load` the chosen image archive. Write an image-only override (example below) with the full SHA. Recreate **only** the selected service with `--no-deps --no-build`; do not run `docker compose down` and do not restart PostgreSQL or MongoDB. Update Web only when its code or contract requires it.
7. Check the running image ID, `https://api.3.1.238.6.sslip.io/health` (HTTP 200 and both databases Healthy), authentication/match routes, Web HTTPS, container stability and preserved data counts. Confirm MongoDB's ID, restart count and mounts did not change. Authenticated gameplay and Unity Host–Client AED E2E require separate real tests.

```bash
cd /home/ubuntu/echo-protocol/deploy
cat > docker-compose.backend-release.yml <<'YAML'
services:
  backend:
    image: aws-lightsail-backend:<full-commit-sha>
YAML
sudo docker compose -p echo-protocol --env-file .env.production \
  -f docker-compose.prod.yml -f docker-compose.mongo-kernel.yml \
  -f docker-compose.backend-migration.override.yml -f docker-compose.backend-release.yml \
  up -d --no-deps --no-build --force-recreate backend
```

For a Web-only release, use a separate `web.image` override and name `web` as the only service. Check the resolved configuration without emitting secrets, for example by parsing `docker compose config --format json` locally on EC2 and printing only image names, the two migration/seed flags, and mount targets.

## Rollback

If health, critical APIs or startup fail, stop the rollout. Point the image-only override back to the immutable pre-release image tag and run the same three production Compose files plus the rollback override with `up -d --no-deps --no-build --force-recreate backend` (or `web`). Verify the image ID and health again. **Do not** automatically restore a production database: first assess any schema migration, shop seeding and writes made after the backup. Keep the fresh backup and failure logs with secrets redacted.

## Automated CD prerequisites

No automatic production job is enabled. This private repository currently has no `production` GitHub environment with verified required reviewers. The existing operator SSH configuration tunnels through AWS SSM with a pinned host key; it does not require inbound TCP/22. This local operator path does not grant GitHub-hosted runners production access. Production deployment still requires explicit human approval, fresh backup/restore evidence and a rollback compatibility decision.

An unattended path would require an approved GitHub deployment identity restricted to this repository and environment, a verified approval gate and private artifact transport. None is part of this PR.

Until a real approval gate and rollback rehearsal exist for unattended deployment, the release-candidate artifact plus human-operated SSM procedure above is the CD boundary.
