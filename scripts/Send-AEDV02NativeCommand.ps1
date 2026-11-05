param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet('Host','Client','Client2','Client3')][string]$Role,
    [Parameter(Mandatory)][ValidateSet('stop','manual-input','move-marker','move-target','aim-target','interact','hide','exit-hide','flashlight','use-tool','drop-tool','scan-core','scan-motion','transport-loss','shutdown','leave','join')][string]$Action,
    [string]$Target = '',
    [Guid]$CommandId = [Guid]::NewGuid()
)
$ErrorActionPreference = 'Stop'
if ($CommandId -eq [Guid]::Empty) { throw 'A nonempty command identity is required.' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-Path -LiteralPath $directory -PathType Container)) { throw 'Existing pilot evidence directory required.' }
$configPath = Join-Path $directory "$Role-private-config.json"
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$expectedRole=if ($Role -eq 'Host') { 'Host' } else { 'Client' }
if ($config.role -ne $expectedRole -or ($config.label -and $config.label -ne $Role) -or !([Uri]$config.apiUrl).IsLoopback -or
    [IO.Path]::GetFullPath($config.output) -ne $directory) { throw 'Pilot role/loopback/output mismatch.' }
$path = Join-Path $directory "$Role-commands.jsonl"
if (!$config.commandsFile -or [IO.Path]::GetFullPath($config.commandsFile) -ne $path) { throw 'This process was not configured for native controls.' }
$line = @{ id=$CommandId.ToString('D'); action=$Action; target=$Target } | ConvertTo-Json -Compress
$bytes = [Text.UTF8Encoding]::new($false).GetBytes($line + "`n")
$stream = [IO.File]::Open($path, [IO.FileMode]::Append, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush() } finally { $stream.Dispose() }
Write-Output "SUBMITTED $CommandId. Inspect $Role-controls.jsonl and actual world evidence; submission is not approval/effect proof."
