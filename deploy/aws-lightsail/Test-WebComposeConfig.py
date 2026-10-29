import json
import pathlib
import shutil
import subprocess
import tempfile

source = pathlib.Path(__file__).resolve().parent / 'docker-compose.prod.yml'
with tempfile.TemporaryDirectory(prefix='echo-web-compose-') as folder:
    root = pathlib.Path(folder)
    shutil.copyfile(source, root / source.name)
    (root / 'docker-compose.mongo-kernel.yml').write_text('services:\n  mongodb:\n    environment:\n      GLIBC_TUNABLES: glibc.pthread.rseq=1\n')
    (root / 'docker-compose.backend-migration.override.yml').write_text(
        'services:\n  backend:\n    environment:\n      Database__MigrateOnStartup: "false"\n      AdminSeed__Enabled: "false"\n')
    (root / 'docker-compose.web-release.yml').write_text(
        'services:\n  web:\n    image: aws-lightsail-web:164d078ee0eb1bcfa2aa8c6379067d658e7904f8\n')
    (root / '.env.production').write_text('\n'.join([
        'POSTGRES_DB=test', 'POSTGRES_USER=test', 'POSTGRES_PASSWORD=test-only',
        'MONGO_ROOT_USERNAME=test', 'MONGO_ROOT_PASSWORD=test-only', 'JWT_SECRET=test-only',
        'MATCH_PROOF_KEY=test-only', 'WEB_DOMAIN=web.example.test', 'API_DOMAIN=api.example.test',
        'PAYOS_CLIENT_ID=test', 'PAYOS_API_KEY=test-only', 'PAYOS_CHECKSUM_KEY=test-only']) + '\n')
    command = ['docker', 'compose', '-p', 'echo-protocol', '--env-file', '.env.production',
               '-f', 'docker-compose.prod.yml', '-f', 'docker-compose.mongo-kernel.yml',
               '-f', 'docker-compose.backend-migration.override.yml']

    def config(extra):
        return json.loads(subprocess.run(command + extra + ['config', '--format', 'json'], cwd=root,
                                         capture_output=True, text=True, check=True).stdout)

    base = config([])
    released = config(['-f', 'docker-compose.web-release.yml'])
    assert str(base['services']['backend']['environment']['Database__MigrateOnStartup']).lower() == 'false'
    assert str(base['services']['backend']['environment']['AdminSeed__Enabled']).lower() == 'false'
    base['services']['web']['image'] = 'aws-lightsail-web:164d078ee0eb1bcfa2aa8c6379067d658e7904f8'
    assert base == released, 'Compose override changed more than services.web.image'
    print('COMPOSE_WEB_IMAGE_ONLY_PASS')
