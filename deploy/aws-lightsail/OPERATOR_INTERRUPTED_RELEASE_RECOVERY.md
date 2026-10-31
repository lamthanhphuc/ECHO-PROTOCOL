# Interrupted EC2 operator release — manual recovery (no automatic deployment)

This is a production **operator runbook**, not authorization to deploy. Only an
approved operator with a maintenance window, current verified backups, and the
original production Compose configuration should perform mutating commands.
The script never restores databases or rolls back migrations automatically.

## Evidence saved before changing the image-only override

For an approved release using backup directory `pre-release-<timestamp>`, the
operator script saves:

- `operator-<service>-predeploy.state`: service, revision, old/new Docker image
  IDs, immutable ECR image reference, rollback tag, and whether a previous
  managed Compose override existed.
- `operator-<service>-override.before.yml`: **only when** a previous managed
  override existed, preserved byte-for-byte with owner-only permissions.

The backup folder also contains `.env.production`, PostgreSQL and MongoDB
backups and Compose files. Restrict access to that folder. Do not print its
contents, upload it to GitHub, or attach it to support tickets.

An interruption may leave three different states: old container still running,
new container running, or target service missing/unhealthy. **Do not infer the
running image from the Compose override file.** The existing override might have
been written even when no container replacement occurred.

## Read-only triage (operator on the approved EC2 session)

1. Stop issuing release commands; ensure the previous operator process is no
   longer running and no unrelated deployment is in progress. Do not bypass the
   `.echo-operator-release.lock` lock or delete it to force an operation.
2. Locate the exact verified backup directory for the interrupted release;
   inspect only its non-secret `operator-<service>-predeploy.state` values.
3. Read the **effective** Compose configuration using all three production
   files and the current operator override (if present). Display only service
   names, selected image references, migration/seed flags, and mount targets;
   never print raw `docker compose config` or `.env.production`.
4. Compare `docker compose -p echo-protocol ... ps -q <service>` with
   `docker inspect -f '{{.Id}}|{{.Image}}|{{.State.Running}}|{{.RestartCount}}' <container-id>`.
   Compare the actual `.Image` to both `OLD_IMAGE_ID` and `NEW_IMAGE_ID` from
   the saved state. Inspect service logs and HTTPS API health (both database
   checks Healthy), Web `/login`, and unaffected container IDs, mounts and
   restart counts. Also compare production migration IDs, not just their count,
   with the restored backup and reviewed application schema.
5. Recheck the operator approval, schema compatibility and backup age. A power
   interruption does **not** automatically extend an expired maintenance window.

## Operator decision — do not skip verification

- **Old image still running and Healthy:** If `ORIGINAL_OVERRIDE=present`,
  restore the saved `operator-<service>-override.before.yml` after manually
  verifying it points to the same old image. If absent, remove **only** the
  operator-owned image override after confirming the old image is selected by
  the remaining production Compose files. Then inspect the resolved config and
  service again. Do not recreate containers unnecessarily.
- **New image running and Healthy:** Verify the exact image ID/digest, both
  database health checks, Web smoke, all unchanged container IDs/mounts, and
  application compatibility before explicitly accepting the release. Leave
  the managed override in place if it correctly selects the actual image.
- **New image unhealthy, missing target container, image mismatch, or
  inconsistent data:** Stop automatic action. Review whether DB schema/writes
  permit old-image compatibility. With independent operator approval, point
  **only the selected service** at the saved rollback tag using the four-file
  Compose configuration and `up -d --no-deps --no-build --pull never
  --force-recreate <service>`. Verify old image ID, smoke checks, and unrelated
  containers. If compatibility is unknown, escalate; do not blindly restore a DB.

Record the selected action, actual image/container IDs, health evidence,
unchanged-container snapshots, migration compatibility decision and result.
Re-run the read-only audit. Never declare recovery PASS just because Compose
returned exit 0.
