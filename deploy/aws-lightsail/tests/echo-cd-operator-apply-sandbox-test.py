#!/usr/bin/env python3
"""Fault-injection test of the real operator Bash release script with mocked Docker and curl.

RUN ONLY INSIDE an ephemeral Docker container with --network none and WITHOUT Docker socket.
Does not verify actual EC2 deployment or application contracts.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

SCRIPT = Path('/opt/echo-ec2-operator-release.sh')
BASE = Path('/home/ubuntu/echo-protocol')
DEPLOY = BASE / 'deploy'
BACKUP = BASE / 'backups' / 'pre-release-isolated-test'
MOCK = Path('/tmp/echo-cd-test-mocks')
STATE = Path('/tmp/echo-cd-test-state')
REV = 'b' * 40
REGISTRY = '430013477435.dkr.ecr.ap-southeast-1.amazonaws.com'
NEW_IMAGE = {s: f'{REGISTRY}/echo-protocol/{s}@sha256:' + 'a'*64 for s in ('backend','web')}
OLD_ID = 'sha256:'+'1'*64
NEW_ID = 'sha256:'+'2'*64

DOCKER_MOCK = r'''#!/bin/bash
set -euo pipefail
s="${ECHO_TEST_DIR:?}"
service="${ECHO_TEST_SERVICE:?}"
image="${ECHO_TEST_IMAGE:?}"
revision="${ECHO_TEST_REV:?}"
oldid="sha256:$(printf '1%.0s' {1..64})"
newid="sha256:$(printf '2%.0s' {1..64})"
active=$(cat "$s/active")
if [[ $1 == compose ]]; then
  for arg in "$@"; do
    case "$arg" in
      config)
        cat <<'JSON'
{"services":{"backend":{"environment":{"Database__MigrateOnStartup":"false","AdminSeed__Enabled":"false"}},"web":{},"postgres":{},"mongodb":{},"caddy":{}}}
JSON
        exit 0 ;;
      ps)
        target="${@: -1}"
        case "$target" in backend|web|postgres|mongodb|caddy) printf 'sim-%s-1\n' "$target";; *) exit 44;; esac
        exit 0 ;;
      up)
        [[ ${@: -1} == "$service" ]] || exit 44
        for opt in --no-deps --no-build --pull never --force-recreate; do [[ " $* " == *" $opt "* ]] || exit 44; done
        ov="/home/ubuntu/echo-protocol/deploy/docker-compose.operator-$service-release.yml"
        if grep -Fq "$image" "$ov"; then next=$newid
        elif grep -Fq "aws-lightsail-$service:rollback-pre-$revision" "$ov"; then next=$oldid
        else exit 44; fi
        echo "$next" > "$s/active"
        printf '%s|%s\n' "$service" "$next" >> "$s/ups"
        exit 0 ;;
    esac
  done
  exit 44
fi
if [[ $1 == exec ]]; then
  [[ $2 == sim-postgres-1 ]] || exit 44
  echo 31; exit 0
fi
if [[ $1 == image && $2 == inspect ]]; then
  target=$3
  if [[ "$target" == "$image" ]]; then identity=$newid
  elif [[ "$target" == "aws-lightsail-$service:rollback-pre-$revision" && -e "$s/tagged" ]]; then identity=$oldid
  else exit 44; fi
  case "$*" in
    *'.Os'* ) echo linux/amd64;;
    *'org.opencontainers.image.revision'* ) echo "$revision";;
    *'.RepoDigests'* ) echo "$image";;
    *'.Id'* ) echo "$identity";;
  esac
  exit 0
fi
if [[ $1 == inspect ]]; then
  if [[ $2 == -f || $2 == --format ]]; then target=${@: -1}; else target=$2; fi
  if [[ "$target" == echo-protocol-caddy-1 ]]; then
    printf 'WEB_DOMAIN=web.example.test\nAPI_DOMAIN=api.example.test\n'; exit 0
  fi
  [[ "$target" == sim-*-1 ]] || exit 44
  name="${target#sim-}"; name="${name%-1}"
  case "$name" in backend|web|postgres|mongodb|caddy) ;; *) exit 44;; esac
  if [[ "$name" == "$service" ]]; then id=$active
  else id="sha256:$(printf '3%.0s' {1..64})"; fi
  case "$*" in
    *'{{.Id}}|'* ) printf '%s|%s|0|volume:mock_%s:/data;\n' "$target" "$id" "$name";;
    *'.State.Running'* ) echo true;;
    *'.Image'* ) echo "$id";;
    * ) exit 44;;
  esac
  exit 0
fi
if [[ $1 == tag ]]; then
  [[ $2 == "$oldid" && $3 == "aws-lightsail-$service:rollback-pre-$revision" ]] || exit 44
  touch "$s/tagged"; exit 0
fi
exit 44
'''

CURL_MOCK = r'''#!/bin/bash
set -euo pipefail
s="${ECHO_TEST_DIR:?}"
oldid="sha256:$(printf '1%.0s' {1..64})"
newid="sha256:$(printf '2%.0s' {1..64})"
current=$(cat "$s/active")
scenario="${ECHO_TEST_SCENARIO:?}"
if [[ "$current" == "$newid" && "$scenario" != healthy ]]; then exit 22; fi
if [[ "$scenario" == rollback_bad && "$current" == "$oldid" && $(wc -l < "$s/ups") -ge 2 ]]; then exit 22; fi
args=("$@")
for ((i=0;i<${#args[@]};i++)); do
  if [[ "${args[i]}" == -o && "${args[i+1]}" != /dev/null ]]; then
     printf '{"status":"Healthy","checks":{"postgresql":"Healthy","mongodb":"Healthy"}}' > "${args[i+1]}"
  fi
done
for ((i=0;i<${#args[@]};i++)); do
  if [[ "${args[i]}" == -w ]]; then printf 200; break; fi
done
'''

def setup_fixture(service, scenario, evidence_migrations=31, stale=False):
    if BASE.exists():
        shutil.rmtree(BASE)
    MOCK.mkdir(parents=True,exist_ok=True)
    DEPLOY.mkdir(parents=True,exist_ok=True)
    BACKUP.mkdir(parents=True,exist_ok=True)
    for name in ('docker-compose.prod.yml','docker-compose.mongo-kernel.yml','docker-compose.backend-migration.override.yml'):
        (DEPLOY/name).write_text('services: {}\n')
    (DEPLOY/'.env.production').write_text('ISOLATED_FAKE_CONFIG=1\n')
    for name in ('postgres.dump','mongodb.archive.gz'):
        (BACKUP/name).write_bytes((name+'-synthetic-fixture').encode())
    (BACKUP/'SHA256SUMS').write_text(''.join(f"{hashlib.sha256((name+'-synthetic-fixture').encode()).hexdigest()}  {name}\n" for name in ('postgres.dump','mongodb.archive.gz')))
    (BACKUP/'reviewed-restore-evidence.txt').write_text('\n'.join(('POSTGRES_RESTORE_PASS','MONGODB_RESTORE_PASS','PRODUCTION_SCHEMA_COMPATIBILITY_APPROVED','MAINTENANCE_WINDOW_APPROVED',f'RESTORED_MIGRATION_COUNT={evidence_migrations}'))+'\n')
    if stale:
        import time
        t=time.time()-100000
        for n in ('postgres.dump','mongodb.archive.gz'): os.utime(BACKUP/n,(t,t))
    if STATE.exists(): shutil.rmtree(STATE)
    STATE.mkdir()
    (STATE/'active').write_text(OLD_ID+'\n')
    (STATE/'ups').write_text('')


def test_case(label, service, scenario, expected_exit, evidence_migrations=31, stale=False):
    setup_fixture(service,scenario,evidence_migrations,stale)
    args=['bash',str(SCRIPT),'apply','--service',service,'--image',NEW_IMAGE[service],'--revision',REV,'--backup-dir',str(BACKUP),'--restore-evidence',str(BACKUP/'reviewed-restore-evidence.txt'),'--approval','APPROVE_ECHO_APPLICATION_RELEASE']
    env=dict(os.environ)
    env['PATH']=f'{MOCK}:{env["PATH"]}'
    env.update({'ECHO_TEST_DIR':str(STATE),'ECHO_TEST_SERVICE':service,'ECHO_TEST_IMAGE':NEW_IMAGE[service], 'ECHO_TEST_REV':REV, 'ECHO_TEST_SCENARIO':scenario})
    p=subprocess.run(args,cwd='/tmp',env=env,capture_output=True,text=True,timeout=25)
    active=(STATE/'active').read_text().strip()
    ups=(STATE/'ups').read_text().strip().splitlines() if (STATE/'ups').read_text().strip() else []
    if p.returncode!=expected_exit:
        raise AssertionError(f'{label}: expected exit {expected_exit} but got {p.returncode}\nSTDOUT:\n{p.stdout}\nSTDERR:\n{p.stderr}')
    if expected_exit==0:
        assert 'APPLICATION_RELEASE_VERIFIED_PASS' in p.stdout
        assert len(ups)==1 and active==NEW_ID
    elif expected_exit==4:
        assert 'APPLICATION_ROLLBACK_VERIFIED_PASS' in p.stdout
        assert len(ups)==2 and active==OLD_ID
    elif expected_exit==5:
        assert 'CRITICAL: rollback unsuccessful' in p.stderr
        assert len(ups)==2
    elif expected_exit==3:
        assert 'BLOCKED' in p.stderr and len(ups)==0
    assert all(x.startswith(service+'|') for x in ups)
    print('MOCK_'+label+'_PASS')



def test_preexisting_override(label, content, expected_exit, tag_old=False):
    service = 'backend'
    scenario = 'healthy'
    setup_fixture(service, scenario)
    existing_path = DEPLOY / f'docker-compose.operator-{service}-release.yml'
    existing_path.write_text(content)
    if tag_old:
        (STATE / 'tagged').touch()
    args = ['bash', str(SCRIPT), 'apply', '--service', service,
            '--image', NEW_IMAGE[service], '--revision', REV,
            '--backup-dir', str(BACKUP),
            '--restore-evidence', str(BACKUP / 'reviewed-restore-evidence.txt'),
            '--approval', 'APPROVE_ECHO_APPLICATION_RELEASE']
    env = dict(os.environ)
    env['PATH'] = f'{MOCK}:{env["PATH"]}'
    env.update({'ECHO_TEST_DIR': str(STATE), 'ECHO_TEST_SERVICE': service,
                'ECHO_TEST_IMAGE': NEW_IMAGE[service], 'ECHO_TEST_REV': REV,
                'ECHO_TEST_SCENARIO': scenario})
    p = subprocess.run(args, cwd='/tmp', env=env, capture_output=True,
                       text=True, timeout=25)
    if p.returncode != expected_exit:
        raise AssertionError(f'{label}: expected {expected_exit}, got {p.returncode}\n'
                             f'STDOUT: {p.stdout}\nSTDERR: {p.stderr}')
    ups = (STATE / 'ups').read_text().splitlines()
    if expected_exit == 0:
        assert 'MANAGED_EXISTING_RELEASE_OVERRIDE_VERIFIED' in p.stdout
        assert 'APPLICATION_RELEASE_VERIFIED_PASS' in p.stdout
        assert len(ups) == 1
        assert (STATE / 'active').read_text().strip() == NEW_ID
        assert existing_path.read_text().startswith('# ECHO_OPERATOR_MANAGED_OVERRIDE\n')
    else:
        assert 'BLOCKED' in p.stderr and len(ups) == 0
    print('MOCK_' + label + '_PASS')


def main():
    if os.environ.get('ECHO_CD_ISOLATED_TEST')!='1':
        raise SystemExit('BLOCKED: test must run in isolated Docker container')
    if Path('/var/run/docker.sock').exists():
        raise SystemExit('BLOCKED: Docker socket must not be mounted')
    # The fixture teardown deletes /home/ubuntu/echo-protocol. Ensure this is
    # a disposable tmpfs, never the host or an existing project directory.
    mounts = [line.split(' - ', 1) for line in Path('/proc/self/mountinfo').read_text().splitlines()
              if line.split()[4] == '/home']
    if len(mounts) != 1 or mounts[0][1].split()[0] != 'tmpfs':
        raise SystemExit('BLOCKED: /home must be a dedicated tmpfs mount')
    if BASE.exists() or BASE.is_symlink():
        raise SystemExit('BLOCKED: test fixture root already exists')
    if not SCRIPT.is_file():
        raise SystemExit('Missing operator release script bind mount')
    MOCK.mkdir(parents=True,exist_ok=True)
    for name,contents in [('docker',DOCKER_MOCK),('curl',CURL_MOCK),('sleep','#!/bin/sh\nexit 0\n'),('flock','#!/bin/sh\n[ "$1" = -n ] && [ "$2" = 9 ]\n')]:
        f=MOCK/name
        f.write_text(contents)
        f.chmod(0o755)
    # Safety check: all Docker & curl invocations are mocked, with network disabled.
    test_case('BACKEND_HEALTHY_APPLY','backend','healthy',0)
    test_case('BACKEND_FAULT_ROLLBACK','backend','candidate_bad',4)
    test_case('BACKEND_ROLLBACK_FAILURE','backend','rollback_bad',5)
    test_case('WEB_HEALTHY_APPLY','web','healthy',0)
    test_case('WEB_FAULT_ROLLBACK','web','candidate_bad',4)
    test_case('MIGRATION_MISMATCH_BLOCK','backend','healthy',3,evidence_migrations=29)
    test_case('STALE_BACKUP_BLOCK','backend','healthy',3,stale=True)
    marker = '# ECHO_OPERATOR_MANAGED_OVERRIDE\nservices:\n  backend:\n    image: '
    tag = f'aws-lightsail-backend:rollback-pre-{REV}'
    test_preexisting_override('MANAGED_SECOND_RELEASE', marker + tag + '\n', 0, tag_old=True)
    test_preexisting_override('UNKNOWN_OVERRIDE_BLOCK', 'services:\n  backend:\n    image: evil:latest\n', 3)
    test_preexisting_override('DRIFTED_OVERRIDE_BLOCK', marker + NEW_IMAGE['backend'] + '\n', 3)
    print('MOCK_OPERATOR_APPLY_TESTS_V2_PASS')
    print('MOCKED_DEPENDENCIES_ONLY_NO_REAL_DOCKER_OR_EC2')

if __name__=='__main__':
    main()
