param([Parameter(Mandatory)][string]$StackJson, [switch]$ResetData,
    [ValidateSet('StopStack','BackendOffline','BackendOnline')][string]$Action = 'StopStack')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Assert-AEDV02LocalDocker.ps1')
$repo = Split-Path $PSScriptRoot -Parent
$root = [IO.Path]::GetFullPath((Join-Path $repo 'KLTN/Temp/AEDV02/Native')) + [IO.Path]::DirectorySeparatorChar
$path = [IO.Path]::GetFullPath($StackJson)
if (!$path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Only disposable AED native stack metadata is allowed.' }
$stack = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$directory = [IO.Path]::GetDirectoryName($path)
$composePath = Join-Path $directory 'compose.json'
if ($stack.runId -notmatch '^aedv02-[a-z0-9-]{1,48}$' -or $stack.database -ne 'aedv02_native_test' -or
    !([Uri]$stack.apiUrl).IsLoopback -or [IO.Path]::GetFullPath($stack.compose) -ne $composePath) { throw 'Stack ownership mismatch.' }
$compose = Get-Content -LiteralPath $composePath -Raw | ConvertFrom-Json
if ($compose.services.backend.environment.ASPNETCORE_ENVIRONMENT -ne 'Development' -or
    $compose.services.postgres.environment.POSTGRES_DB -ne 'aedv02_native_test') { throw 'Development test topology required.' }
foreach ($service in $compose.services.PSObject.Properties.Value) {
    if ($service.container_name -or @($service.ports | Where-Object { $_ -notmatch '^127\.0\.0\.1:\d+:\d+$' }).Count -gt 0) {
        throw 'Compose has non-test container or port bindings.'
    }
}
$ids = @(& rtk proxy docker compose -p $stack.runId -f $composePath ps -aq)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect owned containers.' }
foreach ($id in $ids) {
    if (!$id.Trim()) { continue }
    $labels = (& rtk proxy docker inspect --format '{{json .Config.Labels}}' $id | Out-String) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $labels.'com.docker.compose.project' -ne $stack.runId) { throw 'Container ownership mismatch.' }
}
$arguments = @('compose','-p',$stack.runId,'-f',$composePath,'down')
if ($Action -ne 'StopStack') {
    if ($ResetData) { throw 'ResetData is only valid when stopping the entire owned stack.' }
    $operation = if ($Action -eq 'BackendOffline') { @('stop','backend') } else { @('up','-d','--no-build','backend') }
    & rtk proxy docker compose -p $stack.runId -f $composePath @operation
    if ($LASTEXITCODE -ne 0) { throw 'Owned Backend availability command failed.' }
    Write-Output "SUBMITTED $Action. Inspect HTTP/world logs; no approval or effect is inferred."
    return
}
if ($ResetData) { $arguments += '--volumes' }
& rtk proxy docker @arguments
if ($LASTEXITCODE -ne 0) { throw 'Disposable stack shutdown failed.' }
$stack.ready = $false
$stack | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
Write-Output 'Owned test stack stopped. Evidence retained; no workspace files or other Docker projects removed.'
