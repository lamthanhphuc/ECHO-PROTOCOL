$ErrorActionPreference = 'Stop'
$endpoint = $env:DOCKER_HOST
if (!$endpoint) {
    $endpoint = (& rtk proxy docker context inspect --format '{{.Endpoints.docker.Host}}' | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Docker endpoint could not be resolved; no test resources touched.' }
}
if ($endpoint -notmatch '^npipe:/{2,4}\./pipe/' -and $endpoint -notmatch '^unix:///' -and
    $endpoint -notmatch '^tcp://(127\.0\.0\.1|localhost|\[::1\]):\d+$') {
    throw 'AED automation requires a local Docker daemon; remote Docker contexts are not allowed.'
}
