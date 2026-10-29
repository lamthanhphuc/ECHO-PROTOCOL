#!/usr/bin/env bash
set -euo pipefail
api=${API_URL:-https://api.3.1.238.6.sslip.io}
web=${WEB_URL:?Set WEB_URL to the production HTTPS Web origin}
health=$(mktemp)
trap 'rm -f "$health"' EXIT
test "$(curl -fsS --retry 5 --retry-delay 3 --output "$health" --write-out '%{http_code}' "$api/health")" = 200
python3 - "$health" <<'PY'
import json,sys
data=json.load(open(sys.argv[1],encoding='utf-8'))
assert data.get('status') == 'Healthy', 'API unhealthy'
assert data.get('checks',{}).get('postgresql') == 'Healthy', 'PostgreSQL unhealthy'
assert data.get('checks',{}).get('mongodb') == 'Healthy', 'MongoDB unhealthy'
print('API health: HTTP 200; PostgreSQL and MongoDB healthy')
PY
test "$(curl -fsS --retry 3 --output /dev/null --write-out '%{http_code}' "$web/login")" = 200
echo 'Web login page: HTTP 200'
