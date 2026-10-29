#!/usr/bin/env bash
set -euo pipefail
health_file=$(mktemp)
cleanup() { local rc=$?; rm -f "$health_file"; exit "$rc"; }
trap cleanup EXIT
test "$(curl -fsS --retry 5 --retry-delay 2 -o "$health_file" -w '%{http_code}' https://api.3.1.238.6.sslip.io/health)" = 200
python3 - "$health_file" <<'PY'
import json, sys
data = json.load(open(sys.argv[1], encoding='utf-8'))
assert data.get('status') == 'Healthy'
assert data.get('checks', {}).get('postgresql') == 'Healthy'
assert data.get('checks', {}).get('mongodb') == 'Healthy'
PY
test "$(curl -fsS --retry 5 --retry-delay 2 -o /dev/null -w '%{http_code}' https://web.3.1.238.6.sslip.io/login)" = 200
echo 'Web /login HTTP 200; API /health HTTP 200; PostgreSQL and MongoDB Healthy'
