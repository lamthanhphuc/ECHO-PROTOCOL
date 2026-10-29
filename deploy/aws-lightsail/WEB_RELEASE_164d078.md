# Web-only release 164d078: manual operator gate

Target: `LTP@164d078ee0eb1bcfa2aa8c6379067d658e7904f8`, [release run 40823239311](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/40823239311). The Web archive and its config/layer digests were verified separately. Docker's containerd store displays `sha256:d4658e3fca4026840117d22f2ad2a479a46ddb16c80f74c884ec1b7e567a5150` on EC2, while the runner recorded config digest `sha256:474ebcb859ebf5fa8920197af051963c871b1b0ca1faa99be853109c94d666d1`. The different displayed IDs alone do not invalidate the image. The deployment script checks archive SHA-256, config digest, revision label, architecture, nine root filesystem layers, and the approved EC2 image ID.

`AGENTS.md` forbids agent-run cloud deployment. These scripts are for an explicitly approving human operator. The repository has **no unattended production deployment workflow**. Do not merge this preparation with an actual rollout approval.

## 1. Read-only state audit

From Windows PowerShell on an existing SSH-allowlisted network, with the reviewed repository checked out:

```powershell
Get-Content -LiteralPath .\deploy\aws-lightsail\web-audit.sh -Raw |
  ssh -o BatchMode=yes -o StrictHostKeyChecking=yes -i "$HOME\Downloads\echo-protocol-ec2-key.pem" ubuntu@3.1.238.6 'sudo -n bash -s'
```

Record the three Compose SHA-256 hashes, image IDs, container IDs, mounts, health, restart counts and resources. Check for `docker-compose.web-release.yml` without printing `.env.production` or unfiltered `docker compose config`. Compare the actual Compose files with the reviewed copies. If any live state is unknown, stop and investigate; the scripts refuse an unknown Web image or override. The original SSH failure occurred before the final preflight marker, consistent with an oversized Base64 command losing quoting. Its exact mutation point must be established from this audit; public HTTP 200 alone does not prove which image runs.

Create a local UTF-8 `reviewed-compose.sha256` containing exactly the three **approved live** hashes and names, in the standard `sha256sum` format:

```text
<64 hex digits>  docker-compose.prod.yml
<64 hex digits>  docker-compose.mongo-kernel.yml
<64 hex digits>  docker-compose.backend-migration.override.yml
```

Never fill this manifest from old notes. If SSH times out, use an already allowlisted operator network. These scripts do not add or revoke any security group rule; they cannot leave temporary access behind or remove another operator's rule. Do not open TCP/22 to the world.

## 2. Review the release gate

Confirm the approved Web-only maintenance window and absence of live work that cannot tolerate a Web restart. Verify the existing secure backup at `D:\ECHO_PROTOCOL_SECURE_BACKUPS\pre-release-ltp-20261029T061910Z` and its restore/count comparison. Verify that the artifact directory contains `COMMIT`, `SHA256SUMS`, `web-image-id.txt`, and `web-image.tar.gz`. Its `COMMIT` must be the full target SHA. The PowerShell wrapper checks backup and release hashes before requesting the operator confirmation. No backup contents or secrets are uploaded; only the Web release archive and reviewed scripts are transferred.

## 3. Human-approved Web rollout

From Windows PowerShell, after the audit and approval:

```powershell
.\deploy\aws-lightsail\Invoke-EchoWebRelease.ps1 `
  -ReviewedHashesFile .\reviewed-compose.sha256 `
  -ReleaseDirectory "$HOME\Downloads\echo-release-LTP-164d078ee0eb1bcfa2aa8c6379067d658e7904f8"
```

The wrapper prompts for `DEPLOY WEB 164d078`, transfers reviewed Bash files through SCP, verifies their SHA-256 and syntax on EC2, then calls a short remote command. The remote script compares the effective three-file Compose JSON with a fourth image-only override and requires that **only `services.web.image` changes**. It rejects unknown overrides, missing backups/release files, incorrect image identity, or baseline health failures. `docker compose up -d --pull never --no-deps --no-build --force-recreate web` is the only service replacement. On failure after replacement begins, it attempts Web-only rollback and reports an explicit critical state if rollback fails. An interrupted SSH session has an unknown result: repeat the read-only audit before retrying.

The smoke check requires Web `/login` HTTP 200, API `/health` HTTP 200, and both database checks Healthy. The deploy script also verifies the running Web image and unchanged Backend, PostgreSQL, MongoDB and Caddy IDs. The operator must review fresh Web logs and test authentication with a designated account without printing credentials. Record the final Web container ID, image ID and restart counts.

## 4. Independent recovery

If rollout fails or SSH disconnects, audit first. If Web runs the target image and the known image-only override exists, this command can restore the existing immutable rollback tag:

```powershell
.\deploy\aws-lightsail\Invoke-EchoWebRollback.ps1 -ReviewedHashesFile .\reviewed-compose.sha256
```

The wrapper prompts for `ROLLBACK WEB 164d078`. The remote rollback script verifies the rollback tag resolves to the exact previous image, recreates only Web with `--pull never --no-deps --no-build`, and verifies image identity, Web/API health and unchanged IDs of other services. It never restores PostgreSQL or MongoDB. If the current image or override is unknown, do not force the command; inspect and resolve the drift manually.

## CI/CD boundary

CI validates Backend, Web, Linux images and release script syntax. The manual release workflow builds an immutable archive only after successful push CI for the exact SHA. Production credentials never enter GitHub Actions. The repository remains at **CI PASS → verified release → human approval/operator transport → smoke/rollback evidence**. Hosted runners cannot SSH through the restricted allowlist; OIDC/SSM would require approved IAM/instance changes and is outside this release.
