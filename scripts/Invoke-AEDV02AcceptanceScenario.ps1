param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidateSet('E2E-P4-01','E2E-P4-02','E2E-P4-03','E2E-CAB-01','E2E-CAB-02','E2E-CAB-03','E2E-PP-01','E2E-PP-02','E2E-Z3-01','E2E-TT-01','E2E-F4-01','E2E-AED-01')][string]$Scenario,
    [ValidateSet('Host','Client','Client2','Client3')][string]$Role = 'Client',
    [string]$CabinetMarker,
    [string]$StackJson,
    [ValidateRange(30,1800)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AEDV02NativeProof.ps1')
$output = [IO.Path]::GetFullPath($OutputDirectory)
$repo = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content -LiteralPath (Join-Path $repo 'KLTN/Assets/Resources/AED/Rev02SceneManifest.json') -Raw | ConvertFrom-Json
$journal = Join-Path $output ($Scenario + '-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssfff') + '.jsonl')
$record = [ordered]@{scenario=$Scenario;role=$Role;status='STARTED';utc='';match='';revision=0;reason=''}
$offline = $false
function Save-Scenario([string]$status,[string]$reason) {
    $record.status=$status;$record.reason=$reason;$record.utc=[DateTime]::UtcNow.ToString('O')
    $record | ConvertTo-Json -Compress | Add-Content -LiteralPath $journal -Encoding UTF8
}
function Read-World([string]$side) {
    $path=Join-Path $output "$side-world.jsonl"
    if (!(Test-Path -LiteralPath $path)) { return $null }
    $row=@(Read-AEDV02NativeRows -Path $path) | Select-Object -Last 1
    if (!$row -or [DateTime]::UtcNow - [DateTime]::Parse($row.utc).ToUniversalTime() -gt [TimeSpan]::FromSeconds(5)) { return $null }
    return $row
}
function Wait-World([scriptblock]$condition,[string]$reason) {
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $world=Read-World 'Host';$remote=Read-World $Role
        if ($world -and $remote -and (& $condition $world $remote)) {
            if ($record.match -and $record.match -ne $world.match) { throw 'Cross-match scenario evidence rejected.' }
            $record.match=$world.match;$record.revision=$world.revision;Save-Scenario 'OBSERVED' $reason
            return $world
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Missing real prerequisite/effect: $reason. No pressure/history/approval is synthesized."
}
function Send-Control([string]$action,[string]$target='') {
    & (Join-Path $PSScriptRoot 'Send-AEDV02NativeCommand.ps1') -OutputDirectory $output -Role $Role -Action $action -Target $target | Out-Null
    Save-Scenario 'REQUEST_SUBMITTED' "$action $target"
}
function Local-Player($remote) { return @($remote.players | Where-Object local) | Select-Object -First 1 }
function Pressure($world,$remote) {
    $source=$world.verifiedSource
    if (!$source -or !$source.sourceEndedAtUtc -or $source.planRevision -ne $world.revision -or
        [DateTime]::UtcNow - [DateTime]::Parse($source.sourceEndedAtUtc).ToUniversalTime() -gt [TimeSpan]::FromSeconds(10)) { return $null }
    $local=Local-Player $remote
    $bound=@($world.players | Where-Object { $_.id -eq $local.id }) | Select-Object -First 1
    return @($world.verifiedSource.players | Where-Object { $_.userId -eq $bound.userId }) | Select-Object -First 1
}
function Enter-Cabinet {
    $remote=Read-World $Role;$local=Local-Player $remote
    $script:chosenCabinet=@($manifest.Markers | Where-Object { $_.Kind -eq 'CABINET' -and $_.Zone -in @(1,2) -and (!$CabinetMarker -or $_.Id -eq $CabinetMarker) } |
        Sort-Object { [Math]::Pow($_.X-$local.x,2)+[Math]::Pow($_.Y-$local.y,2)+[Math]::Pow($_.Z-$local.z,2) } | Select-Object -First 1)
    if ($script:chosenCabinet.Count -ne 1) { throw 'Registered pilot Cabinet required.' }
    Send-Control 'move-marker' $script:chosenCabinet[0].Id
    Wait-World { param($w,$r) $p=Local-Player $r; $c=$script:chosenCabinet[0];
        [Math]::Pow($p.x-$c.X,2)+[Math]::Pow($p.y-$c.Y,2)+[Math]::Pow($p.z-$c.Z,2) -le 4 } 'ordinary movement reached Cabinet' | Out-Null
    Send-Control 'stop';Send-Control 'hide' $script:chosenCabinet[0].HideSpotId
    Wait-World { param($w,$r) (Local-Player $r).hidden } 'actual hidden state' | Out-Null
}
function Wait-CabinetLog([string]$pattern) {
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $lines=@(Get-Content -LiteralPath (Join-Path $output 'Host.log')) | Select-Object -Skip $script:logStart
        if ($script:encounterId) { $lines=@($lines | Where-Object { $_ -match ('encounter=' + [Regex]::Escape($script:encounterId)) }) }
        $matched=@($lines | Where-Object { $_ -match $pattern }) | Select-Object -Last 1
        if ($matched) { Save-Scenario 'OBSERVED_LOG' $pattern; return $matched }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "No real Cabinet transition: $pattern"
}
try {
    $script:logStart=@(Get-Content -LiteralPath (Join-Path $output 'Host.log')).Count
    $script:encounterId=$null
    Wait-World { param($w,$r) $w.sessionState -eq 'InMatch' -and $r.sessionState -eq 'InMatch' } 'native Host and remote role in match' | Out-Null
    switch -Wildcard ($Scenario) {
        'E2E-P4-*' {
            Wait-World { param($w,$r) $p=Pressure $w $r; $w.verifiedSource.pressureComplete -and $p -and $p.tier -eq 4 } 'canonical PP4 for selected player' | Out-Null
            Enter-Cabinet
            Wait-World { param($w,$r) $p=Local-Player $r;$p.hidden -and $p.protectionToken -and $p.protectionSeconds -gt 0 } 'actual replicated P4 token' | Out-Null
            Send-Control 'flashlight'
            if ($Scenario -eq 'E2E-P4-03') {
                if (!$StackJson) { throw 'Owned disposable StackJson required for outage.' }
                & (Join-Path $PSScriptRoot 'Stop-AEDV02NativeBackend.ps1') -StackJson $StackJson -Action BackendOffline | Out-Null
                $offline=$true
            }
            Wait-World { param($w,$r) $p=Local-Player $r;
                if ($p.protectionSeconds -gt 0 -and !$p.hidden) { throw 'Unauthorized exit while protected.' }
                $p.protectionSeconds -le 0 } 'protection reached actual timer expiry without injected state' | Out-Null
            if ($Scenario -eq 'E2E-P4-02') {
                Wait-World { param($w,$r) (Local-Player $r).graceSeconds -gt 0 } 'real inspection attempt starts post-token grace' | Out-Null
            }
        }
        'E2E-CAB-*' {
            Wait-World { param($w,$r) $p=Pressure $w $r; $w.verifiedSource.pressureComplete -and $p -and $p.tier -in @(0,1) -and
                $w.verifiedSource.teamPressureStatus -eq 'Verified' -and $w.verifiedSource.teamPressureTier -in @(3,4) } 'canonical PP0/1 and TP3/4' | Out-Null
            Enter-Cabinet
            $reserved=Wait-CabinetLog ('\[CABINET_RESERVED\].*match=' + [Regex]::Escape($record.match) + '.*spot=' + $script:chosenCabinet[0].HideSpotId + '\b')
            $script:encounterId=[Regex]::Match($reserved,'encounter=([0-9a-fA-F-]{36})').Groups[1].Value
            if (!$script:encounterId) { throw 'Cabinet encounter identity missing.' }
            Wait-CabinetLog '\[CABINET_PHASE\].*phase=WarningLead'
            if ($Scenario -eq 'E2E-CAB-02') { Send-Control 'exit-hide';Wait-CabinetLog '\[CABINET_PHASE\].*phase=Backoff' }
            elseif ($Scenario -eq 'E2E-CAB-03') {
                if ($Role -eq 'Host') { throw 'Cabinet reconnect scenario requires remote Client.' }
                Send-Control 'transport-loss'
                Wait-World { param($w,$r) $r.sessionState -eq 'InMatch' -and $r.reconnectAttempt -gt 0 -and !$r.reconnecting } 'actual reconnect completion' | Out-Null
            } else {
                Wait-CabinetLog '\[CABINET_PHASE\].*phase=JumpIn';Wait-CabinetLog '\[CABINET_PHASE\].*phase=Approach'
                Wait-CabinetLog '\[CABINET_PHASE\].*phase=Inspect';Wait-CabinetLog '\[CABINET_PHASE\].*phase=Recovery'
            }
        }
        'E2E-PP-01' {
            Wait-World { param($w,$r) $w.players.Count -eq 4 -and $w.verifiedSource.pressureComplete -and
                @($w.verifiedSource.players.tier | Select-Object -Unique).Count -ge 2 } 'four real players with distinct canonical pressure' | Out-Null
            Save-Scenario 'MANUAL_REVIEW_REQUIRED' 'Four-player pressure tiers and target transitions were captured; owner must review fairness, forced-target/core-carrier exceptions and attack-freeze evidence.'
        }
        'E2E-PP-02' {
            $w=Read-World 'Host';$minion=@($w.minions | Where-Object { $_.state -notin @('Dying','Flee') }) | Select-Object -First 1
            if (!$minion) { throw 'A real active Minion is required.' }
            Send-Control 'move-target' $minion.id
            Wait-World { param($w,$r) $id=(Local-Player $r).id; @($w.players | Where-Object { $_.id -eq $id -and $_.slowUntilTick -gt $w.tick }).Count -eq 1 } 'actual Minion slow' | Out-Null
            $w=Read-World 'Host';Send-Control 'move-target' $w.stalkers[0].id
            Wait-World { param($w,$r) (Local-Player $r).life -eq 'DOWNED' } 'actual Stalker Down after Minion pressure' | Out-Null
        }
        'E2E-Z3-01' {
            Wait-World { param($w,$r) $w.phase -eq 'Zone3FindFrigate' -and @($w.minions | Where-Object zone -eq 3).Count -eq 1 } 'approved Adaptive Zone3 actual Minion spawn' | Out-Null
            Save-Scenario 'MANUAL_REVIEW_REQUIRED' 'Adaptive cap and live census were captured; owner must observe relief 1->0 without force-despawn and repeat Fixed Normal with Zone3 at zero.'
        }
        'E2E-TT-01' {
            Wait-World { param($w,$r) $w.phase -eq 'Zone2Objective' -and $w.tools.Count -ge 5 } 'materialized Zone2 supply' | Out-Null
            $ids=@($w.tools | ForEach-Object { [int]$_.toolId } | Sort-Object -Unique)
            $required=@(1,2,3,4,6)
            if (@($required | Where-Object { $ids -notcontains $_ }).Count -ne 0) { throw 'Required five Tool types are not all materialized in the authoritative world journal.' }
            Save-Scenario 'MANUAL_REVIEW_REQUIRED' 'Required Tool instances were captured; owner must correlate reservation/materialization IDs, optional-budget changes, support effect and receipt.'
        }
        'E2E-F4-01' {
            Wait-World { param($w,$r) $w.phase -in @('FinalHunt','Escape') } 'normal objectives reached final phase' | Out-Null
            Save-Scenario 'MANUAL_REVIEW_REQUIRED' 'Final phase was captured; owner must inspect decision audit for zero new positive pressure and confirm objective completion without softlock.'
        }
        'E2E-AED-01' {
            if (!$StackJson) { throw 'Owned disposable StackJson required for outage.' }
            & (Join-Path $PSScriptRoot 'Stop-AEDV02NativeBackend.ps1') -StackJson $StackJson -Action BackendOffline | Out-Null
            $offline=$true;Start-Sleep -Seconds 12
            & (Join-Path $PSScriptRoot 'Stop-AEDV02NativeBackend.ps1') -StackJson $StackJson -Action BackendOnline | Out-Null
            $offline=$false
            Wait-World { param($w,$r) $w.verifiedSource.pressureComplete } 'canonical collection recovered after real HTTP outage' | Out-Null
            Save-Scenario 'MANUAL_REVIEW_REQUIRED' 'Backend recovery was observed; owner must correlate PreMatch/Boundary, two distinct same-phase SafeWindow IDs, retry identity, consumer readback and receipts.'
        }
    }
    Save-Scenario 'MANUAL_REVIEW_REQUIRED' 'This driver records real conditions/actions; it does not certify human gameplay or audiovisual assertions.'
} catch {
    Save-Scenario 'BLOCKED_OR_FAILED' $_.Exception.Message
    throw
} finally {
    if ($offline) { & (Join-Path $PSScriptRoot 'Stop-AEDV02NativeBackend.ps1') -StackJson $StackJson -Action BackendOnline | Out-Null }
}
