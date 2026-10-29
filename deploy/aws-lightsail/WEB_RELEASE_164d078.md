# Web-only release 164d078: manual operator gate

Target: `LTP@164d078ee0eb1bcfa2aa8c6379067d658e7904f8`, [release run 40823239311](https://github.com/lamthanhphuc/ECHO-PROTOCOL/actions/runs/40823239311). The Web archive and its config/layer digests were verified separately. Docker's containerd store displays `sha256:d4658e3fca4026840117d22f2ad2a479a46ddb16c80f74c884ec1b7e567a5150` on EC2, while the runner recorded config digest `sha256:474ebcb859ebf5fa8920197af051963c871b1b0ca1faa99be853109c94d666d1`. The different displayed IDs alone do not invalidate the image. The deployment script checks archive SHA-256, config digest, revision label, architecture, nine root filesystem layers, and the approved EC2 image ID.

`AGENTS.md` forbids agent-run cloud deployment. These scripts are for an explicitly approving human operator. The repository has **no unattended production deployment workflow**. Do not merge this preparation with an actual rollout approval.

## 1. Read-only state audit

From Windows PowerShell with the reviewed PR branch checked out and the existing `$HOME\.ssh\echo-ssm.conf` configured. The `.gitattributes` file keeps `web-audit.sh` in LF format; the command pipes UTF-8 text to remote `bash -s` through AWS SSM without a long SSH argument:

```powershell
$OutputEncoding = [Text.UTF8Encoding]::new($false)
Get-Content -LiteralPath .\deploy\aws-lightsail\web-audit.sh -Raw -Encoding UTF8 |
  ssh -T -F "$HOME\.ssh\echo-ssm.conf" -o BatchMode=yes -o StrictHostKeyChecking=yes echo-ec2-ssm 'sudo -n bash -s'
```

Record the three Compose SHA-256 hashes, image IDs, container IDs, mounts, health, restart counts and resources. Containers without Docker health checks show `HEALTH none`; bind mounts without a volume name show their source path. Check for `docker-compose.web-release.yml` without printing `.env.production` or unfiltered `docker compose config`. Compare the actual Compose files with the reviewed copies. The operator's SSM audit found all five containers running with zero restarts, matching reviewed Compose hashes, the old Web image active and no Web override. Repeat the audit immediately before deployment to catch drift. The original SSH failure occurred before the final preflight marker, consistent with an oversized Base64 command losing quoting; public HTTP 200 alone does not prove which image runs.

Create a local UTF-8 `reviewed-compose.sha256` containing exactly the three **approved live** hashes and names, in the standard `sha256sum` format:

```text
<64 hex digits>  docker-compose.prod.yml
<64 hex digits>  docker-compose.mongo-kernel.yml
<64 hex digits>  docker-compose.backend-migration.override.yml
```

Never fill this manifest from old notes. SSM transport requires the configured AWS profile and pinned SSH host key; it does not require a fixed operator IP or inbound TCP/22. These scripts do not change security group rules.

## 2. Review the release gate

Confirm the approved Web-only maintenance window and absence of live work that cannot tolerate a Web restart. Verify the existing secure backup at `D:\ECHO_PROTOCOL_SECURE_BACKUPS\pre-release-ltp-20261029T061910Z` and its restore/count comparison. Release run `40823239311` used `services=both`: `SHA256SUMS` contains Backend and Web archive entries. The Web-only operator path requires `COMMIT`, `SHA256SUMS`, `web-image-id.txt`, and `web-image.tar.gz`; it accepts either one Web entry or Backend plus Web entries, verifies the **Web** archive hash and target commit, and transfers only Web files. The Backend archive is never loaded or deployed by this path. No backup contents or secrets are uploaded.

## 3. Human-approved Web rollout

From Windows PowerShell, after the audit and approval:

```powershell
.\deploy\aws-lightsail\Invoke-EchoWebRelease.ps1 `
  -ReviewedHashesFile .\reviewed-compose.sha256 `
  -ConfigPath "$HOME\.ssh\echo-ssm.conf" `
  -ReleaseDirectory "$HOME\Downloads\echo-release-LTP-164d078ee0eb1bcfa2aa8c6379067d658e7904f8"
```

The wrapper prompts for `DEPLOY WEB 164d078`, transfers reviewed Bash files through SCP, verifies their SHA-256 and syntax on EC2, then calls a short remote command. The remote script validates the transferred Web archive against its entry in the original `SHA256SUMS`; it does not require the Backend archive. It compares the effective three-file Compose JSON with a fourth image-only override and requires that **only `services.web.image` changes**. It rejects unknown overrides, missing Web files, incorrect image identity, or baseline health failures. `docker compose up -d --pull never --no-deps --no-build --force-recreate web` is the only service replacement. On failure after replacement begins, it attempts Web-only rollback and reports an explicit critical state if rollback fails. An interrupted SSH session has an unknown result: repeat the read-only audit before retrying.

The smoke check requires Web `/login` HTTP 200, API `/health` HTTP 200, and both database checks Healthy. The deploy script also verifies the running Web image and unchanged Backend, PostgreSQL, MongoDB and Caddy IDs. The operator must review fresh Web logs and test authentication with a designated account without printing credentials. Record the final Web container ID, image ID and restart counts.

## 4. Independent recovery

If rollout fails or SSH disconnects, audit first. If Web runs the target image and the known image-only override exists, this command can restore the existing immutable rollback tag:

```powershell
.\deploy\aws-lightsail\Invoke-EchoWebRollback.ps1 -ReviewedHashesFile .\reviewed-compose.sha256 -ConfigPath "$HOME\.ssh\echo-ssm.conf"
```

The wrapper prompts for `ROLLBACK WEB 164d078`. The remote rollback script verifies the rollback tag resolves to the exact previous image, recreates only Web with `--pull never --no-deps --no-build`, and verifies image identity, Web/API health and unchanged IDs of other services. It never restores PostgreSQL or MongoDB. If the current image or override is unknown, do not force the command; inspect and resolve the drift manually.

## CI/CD boundary

CI validates Backend, Web, Linux images and release script syntax. The manual release workflow builds an immutable archive only after successful push CI for the exact SHA. Production credentials never enter GitHub Actions. The repository remains at **CI PASS → verified release → human approval/operator SSM transport → smoke/rollback evidence**. The existing operator SSM profile is local; unattended GitHub deployment still requires a separately approved authentication and approval design.
