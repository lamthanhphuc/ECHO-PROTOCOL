param([switch]$Check)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$unitySource = Join-Path $repository 'KLTN/Assets/Scripts/AI/Common/AED'
$backendSource = Join-Path $repository 'EchoProtocol.Backend/src/EchoProtocol.Api/Shared'
foreach ($name in @('AEDV02PilotPolicy.cs', 'AEDV02Plan.cs', 'AEDV02WorldSource.cs', 'AEDV02PilotOpportunities.cs', 'AEDV02ResourcePlacement.cs', 'AEDV02ActionContract.cs')) {
    $source = Join-Path $unitySource $name
    $target = Join-Path $backendSource $name
    if ($Check) {
        if (!(Test-Path -LiteralPath $target) -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($source)) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($target))) {
            throw "AED v0.2 contract differs: $name. Run scripts/Sync-AEDV02Contract.ps1."
        }
    } else {
        New-Item -ItemType Directory -Path $backendSource -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
}
$manifestSource = Join-Path $repository 'KLTN/Assets/Resources/AED/Rev02SceneManifest.json'
$manifestTarget = Join-Path $repository 'EchoProtocol.Backend/src/EchoProtocol.Api/Content/AEDV02/Rev02SceneManifest.json'
if (Test-Path -LiteralPath $manifestSource) {
    if ($Check) {
        if (!(Test-Path -LiteralPath $manifestTarget) -or [Convert]::ToBase64String([IO.File]::ReadAllBytes($manifestSource)) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($manifestTarget))) {
            throw 'AED v0.2 scene manifests differ. Run scripts/Sync-AEDV02Contract.ps1.'
        }
    } else {
        Copy-Item -LiteralPath $manifestSource -Destination $manifestTarget
    }
}
