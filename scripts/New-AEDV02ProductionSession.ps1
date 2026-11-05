param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Usernames,
    [string]$ApiUrl,
    [ValidateSet('Adaptive','Fixed')][string]$Mode = 'Adaptive',
    [switch]$Gameplay,
    [switch]$AutoRelief,
    [ValidateRange(30,1800)][int]$TimeoutSeconds = 600
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AEDV02BackendTarget.ps1')
if ([string]::IsNullOrEmpty($ApiUrl)) { $ApiUrl = Get-AEDV02ProductionApiUrl }
# Offline configuration only: explicit runtime consent still required at launch.
$api = Resolve-AEDV02BackendTarget -ApiUrl $ApiUrl -BackendEnvironment Production -AllowProduction
if ($Usernames.Count -lt 2 -or $Usernames.Count -gt 4 -or
    @($Usernames | ForEach-Object { $_.Trim().ToLowerInvariant() } | Select-Object -Unique).Count -ne $Usernames.Count -or
    @($Usernames | Where-Object { $_ -notmatch '^[a-zA-Z0-9_]{3,32}$' }).Count) {
    throw 'Specify two to four distinct existing test-account usernames.'
}
if ($AutoRelief -and !$Gameplay) { throw '-AutoRelief requires explicit -Gameplay.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory for each run.' }
New-Item -ItemType Directory -Path $output | Out-Null
$session = 'aed-prod-' + [Guid]::NewGuid().ToString('N').Substring(0,16)
$configs = @()
for ($index = 0; $index -lt $Usernames.Count; $index++) {
    $label = if ($index -eq 0) { 'Host' } elseif ($index -eq 1) { 'Client' } else { 'Client' + $index }
    $path = Join-Path $output "$label-private-config.json"
    @{
        role = if ($index -eq 0) { 'Host' } else { 'Client' }; label = $label
        mode = $Mode; playerCount = $Usernames.Count; apiUrl = $api; backendEnvironment = 'Production'
        username = $Usernames[$index]; session = $session; output = $output
        commandsFile = Join-Path $output "$label-commands.jsonl"
        gameplay = [bool]$Gameplay; cabinet = $false; autoRelief = [bool]$AutoRelief; timeoutSeconds = $TimeoutSeconds
    } | ConvertTo-Json | Set-Content -LiteralPath $path -Encoding UTF8
    $configs += $path
}
# No network calls, registration, password collection, cloud configuration or server flag changes.
[pscustomobject]@{ output = $output; hostConfig = $configs[0]; clientConfig = $configs[1]
    additionalClientConfig = @($configs | Select-Object -Skip 2); session = $session
    backendEnvironment = 'Production'; apiUrl = $api; gameplay = [bool]$Gameplay; requiresLaunchOptIn = $true }
