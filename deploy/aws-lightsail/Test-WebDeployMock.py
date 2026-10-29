import hashlib
import io
import json
import os
import pathlib
import subprocess
import sys
import tarfile
import tempfile

SHA = '164d078ee0eb1bcfa2aa8c6379067d658e7904f8'
OLD = 'sha256:f69d2511bd76aed0c117f3d86950dce54d8ef62e69608ca7f078d59efe1d4e1b'
TARGET = 'sha256:d4658e3fca4026840117d22f2ad2a479a46ddb16c80f74c884ec1b7e567a5150'
ROOT = pathlib.Path(__file__).resolve().parent
BASH = os.environ.get('BASH_EXE', 'D:/Git/bin/bash.exe' if os.name == 'nt' else 'bash')


def bash_path(path):
    value = str(path).replace('\\', '/')
    return '/' + value[0].lower() + value[2:] if len(value) > 1 and value[1] == ':' else value


MOCK_DOCKER = r'''#!/usr/bin/env bash
set -euo pipefail
if [[ "$1 $2" == 'image inspect' ]]; then
  tag=$3; format=$5
  if [[ $format == *'.Id'* ]]; then
    if [[ $tag == *rollback* ]]; then echo "$MOCK_OLD"; else echo "${MOCK_TARGET_ID:-$MOCK_TARGET}"; fi
  elif [[ $format == *'revision'* ]]; then echo "$MOCK_SHA"
  elif [[ $format == *'.Os'* ]]; then echo linux/amd64
  elif [[ $format == *'RootFS.Layers'* ]]; then echo 9
  else exit 1; fi
elif [[ $1 == inspect ]]; then
  if [[ $3 == *'.RestartCount'* ]]; then echo 0
  elif [[ $4 == web-* ]]; then
    if [[ $(cat "$MOCK_STATE") == old ]]; then echo "$MOCK_OLD"; else echo "$MOCK_TARGET"; fi
  else exit 1; fi
elif [[ $1 == logs ]]; then
  :
elif [[ $1 == compose ]]; then
  args=" $* "
  if [[ $args == *' ps -q '* ]]; then
    service=${!#}
    if [[ $service == web ]]; then echo "web-$(cat "$MOCK_STATE")"; else echo "id-$service"; fi
  elif [[ $args == *' config --format json '* ]]; then
    python3 - "$@" <<'PYCODE'
import json,sys
c={'name':'echo-protocol','services':{n:{'image':n,'environment':{}} for n in ['backend','postgres','mongodb','caddy','web']}}
c['services']['backend']['environment']={'Database__MigrateOnStartup':'false','AdminSeed__Enabled':'false'}
if any('target-override.yml' in a for a in sys.argv): c['services']['web']['image']='aws-lightsail-web:'+__import__('os').environ['MOCK_SHA']
print(json.dumps(c))
PYCODE
  elif [[ $args == *' up '* ]]; then
    [[ $args == *' --no-deps '* && $args == *' --no-build '* && $args == *' --pull never '* && ${!#} == web ]] || exit 97
    if grep -q rollback "${MOCK_ROOT}/deploy/docker-compose.web-release.yml"; then echo old > "$MOCK_STATE"; else echo target > "$MOCK_STATE"; fi
    echo up >> "$MOCK_UP_COUNT"
    if [[ $(cat "$MOCK_STATE") == target && ${MOCK_UP_FAIL:-0} == 1 ]]; then exit 1; fi
    if [[ $(cat "$MOCK_STATE") == old && ${MOCK_ROLLBACK_FAIL:-0} == 1 ]]; then exit 1; fi
  else exit 1; fi
else exit 1; fi
'''

MOCK_CURL = r'''#!/usr/bin/env bash
set -euo pipefail
args=" $* "
if [[ $args == *'/health'* ]]; then
  file=''
  while (($#)); do if [[ $1 == -o ]]; then file=$2; shift; fi; shift; done
  printf '{"status":"Healthy","checks":{"postgresql":"Healthy","mongodb":"Healthy"}}' > "$file"
  printf 200
elif [[ $args == *'/login'* ]]; then
  if [[ $(cat "$MOCK_STATE") == target && ${MOCK_WEB_FAIL:-0} == 1 ]]; then exit 22; fi
  printf 200
else exit 1; fi
'''


with tempfile.TemporaryDirectory(prefix='echo-web-mock-') as folder:
    root = pathlib.Path(folder)
    deploy = root / 'deploy'
    release = root / 'deploy-artifacts' / SHA
    scripts = root / 'scripts'
    fakebin = root / 'bin'
    for directory in (deploy, release, scripts, fakebin):
        directory.mkdir(parents=True)
    for name in ('docker-compose.prod.yml', 'docker-compose.mongo-kernel.yml', 'docker-compose.backend-migration.override.yml'):
        (deploy / name).write_text('services: {}\n')
    (deploy / '.env.production').write_text('TEST_ONLY=1\n')
    (scripts / 'reviewed-compose.sha256').write_bytes(''.join(
        hashlib.sha256((deploy / name).read_bytes()).hexdigest() + '  ' + name + '\n'
        for name in ('docker-compose.prod.yml', 'docker-compose.mongo-kernel.yml', 'docker-compose.backend-migration.override.yml')).encode())
    for name in ('web-smoke.sh', 'web-rollback.sh'):
        (scripts / name).write_bytes((ROOT / name).read_bytes())
    config = b'{"test":true}'
    config_name = hashlib.sha256(config).hexdigest() + '.json'
    with tarfile.open(release / 'web-image.tar.gz', 'w:gz') as archive:
        for name, data in ((config_name, config), ('manifest.json', json.dumps([{'Config': config_name, 'Layers': ['layer'] * 9}]).encode())):
            info = tarfile.TarInfo(name)
            info.size = len(data)
            archive.addfile(info, io.BytesIO(data))
    (release / 'web-image-id.txt').write_text('sha256:' + hashlib.sha256(config).hexdigest())
    (release / 'COMMIT').write_text(SHA)
    archive = release / 'web-image.tar.gz'
    (release / 'SHA256SUMS').write_bytes((hashlib.sha256(archive.read_bytes()).hexdigest() + '  web-image.tar.gz\n').encode())
    (fakebin / 'docker').write_text(MOCK_DOCKER)
    (fakebin / 'curl').write_text(MOCK_CURL)
    if os.name == 'nt':
        (fakebin / 'python3').write_bytes(("#!/usr/bin/env bash\nexec '" + bash_path(sys.executable) + "' \"$@\"\n").encode())
    for name in ('docker', 'curl', 'python3') if os.name == 'nt' else ('docker', 'curl'):
        (fakebin / name).chmod(0o755)
    state = root / 'state'
    count = root / 'up-count'
    env = os.environ.copy()
    env.update(ECHO_DEPLOY_ROOT=bash_path(root), MOCK_ROOT=bash_path(root), MOCK_STATE=bash_path(state),
               MOCK_UP_COUNT=bash_path(count), MOCK_SHA=SHA, MOCK_OLD=OLD, MOCK_TARGET=TARGET,
               PATH=bash_path(fakebin) + ':/usr/bin:/bin:' + bash_path(pathlib.Path(sys.executable).parent) if os.name == 'nt'
               else bash_path(fakebin) + ':' + env['PATH'])
    curl_path = subprocess.run([BASH, '-c', 'PATH="$1:$PATH"; command -v curl', 'bash', bash_path(fakebin)], env=env, capture_output=True, text=True).stdout
    assert bash_path(fakebin) in curl_path, curl_path

    def run(**extra):
        return subprocess.run([BASH, '-c', 'PATH="$1:$PATH"; shift; export PATH; exec bash "$@"', 'bash', bash_path(fakebin),
                               bash_path(ROOT / 'web-deploy.sh'), bash_path(scripts)],
                              env=env | extra, capture_output=True, text=True)

    state.write_bytes(b'old\n')
    (release / 'SHA256SUMS').write_bytes(('0' * 64 + '  web-image.tar.gz\n').encode())
    result = run()
    assert result.returncode != 0 and not count.exists(), 'checksum mismatch reached deployment'
    (release / 'SHA256SUMS').write_bytes((hashlib.sha256(archive.read_bytes()).hexdigest() + '  web-image.tar.gz\n').encode())
    result = run(MOCK_TARGET_ID='sha256:wrong')
    assert result.returncode != 0 and not count.exists(), 'image mismatch reached deployment'
    result = run(MOCK_UP_FAIL='1')
    assert result.returncode == 1 and 'WEB_DEPLOY_FAILED_ROLLED_BACK' in result.stdout, (result.stdout, result.stderr, state.read_text())
    assert state.read_text().strip() == 'old', 'rollback did not restore old image'
    assert count.read_text().count('up') == 2, 'unrelated service was recreated'
    override = deploy / 'docker-compose.web-release.yml'
    before = count.read_text()
    override.write_bytes(b'services:\n  web:\n    image: unknown\n')
    result = run()
    assert result.returncode != 0 and count.read_text() == before, 'unknown override was overwritten'
    override.write_bytes(('services:\n  web:\n    image: aws-lightsail-web:rollback-pre-' + SHA + '\n').encode())
    result = run(MOCK_WEB_FAIL='1')
    assert result.returncode == 1 and 'WEB_DEPLOY_FAILED_ROLLED_BACK' in result.stdout, (result.stdout, result.stderr, state.read_text())
    assert state.read_text().strip() == 'old', 'health failure did not roll back'
    result = run(MOCK_UP_FAIL='1', MOCK_ROLLBACK_FAIL='1')
    assert result.returncode == 2 and 'CRITICAL_WEB_ROLLBACK_FAILED' in result.stderr, (result.stdout, result.stderr)
    override.write_bytes(('services:\n  web:\n    image: aws-lightsail-web:' + SHA + '\n').encode())
    state.write_bytes(b'old\n')
    result = run()
    assert result.returncode == 0 and 'WEB_DEPLOYED' in result.stdout, result.stderr
    assert state.read_text().strip() == 'target'
    before = count.read_text()
    result = run()
    assert result.returncode == 0 and 'WEB_DEPLOYED_ALREADY' in result.stdout
    assert count.read_text() == before, 'safe retry recreated Web'
    print('CHECKSUM_IMAGE_ROLLBACK_RETRY_TESTS_PASS')
