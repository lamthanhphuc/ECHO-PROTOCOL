$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AEDV02NativeProof.ps1')
$baseline=[pscustomobject]@{matchId='match';revision=1;consumerObserved=$true;chaseSpeed=7.5}
$hostRow=[pscustomobject]@{matchId='match';decisionId='decision';revision=2;fingerprint='plan';roster='roster';actionFingerprint='action';
    scene='scene';policy='policy';phase='CORE_COLLECTION';chaseSpeed=7;players=2;consumerObserved=$true;consumerKind='StalkerFusionRuntime';
    consumerRevision=2;consumerFingerprint='plan';consumerValues=@(1.25,18,1,2.25,7,420);stalkerId='stalker';presentedSpeed=0;animatorState=0}
$hostRow | Add-Member -NotePropertyName windowId -NotePropertyValue ('a'*64)
$hostRow | Add-Member -NotePropertyName decisionPoint -NotePropertyValue 'SAFE_WINDOW'
$hostRow | Add-Member -NotePropertyName decisionPhaseOrdinal -NotePropertyValue 1
$remoteRow=$hostRow | ConvertTo-Json -Depth 5 | ConvertFrom-Json
$remoteRow.consumerKind='StalkerAnimatorPresenter';$remoteRow.presentedSpeed=5;$remoteRow.animatorState=123
$cases=@('valid','plan-only','stale-revision','wrong-fingerprint','missing-receipt','no-effect','wrong-actor','wrong-consumer',
    'not-moving','no-animator','missing-values','wrong-phase','wrong-roster','wrong-policy','wrong-window','wrong-decision-phase')
foreach ($case in $cases) {
    $remote=$remoteRow | ConvertTo-Json -Depth 5 | ConvertFrom-Json
    $initial=$baseline | ConvertTo-Json | ConvertFrom-Json
    $receipt=@('[AED_V02][APPLY_RECEIPT] match=match decision=decision revision=2 fingerprint=plan window='+('a'*64)+' policy=policy')
    switch ($case) {
        'plan-only' {$remote.consumerObserved=$false}
        'stale-revision' {$remote.consumerRevision=1}
        'wrong-fingerprint' {$remote.consumerFingerprint='wrong'}
        'missing-receipt' {$receipt=@()}
        'no-effect' {$initial.chaseSpeed=7}
        'wrong-actor' {$remote.stalkerId='other'}
        'wrong-consumer' {$remote.consumerKind='ReplicatedPlan'}
        'not-moving' {$remote.presentedSpeed=0}
        'no-animator' {$remote.animatorState=0}
        'missing-values' {$remote.consumerValues=@()}
        'wrong-phase' {$remote.phase='FINAL_HUNT'}
        'wrong-roster' {$remote.roster='old'}
        'wrong-policy' {$remote.policy='old'}
        'wrong-window' {$remote.windowId='b'*64}
        'wrong-decision-phase' {$remote.decisionPhaseOrdinal=2}
    }
    $proof=Find-AEDV02NativeProof -HostRows @($initial,$hostRow) -ClientRows @($remote) -ReceiptLines $receipt
    if (($null -ne $proof) -ne ($case -eq 'valid')) { throw "Native proof comparator failed case $case." }
}
Write-Output "Native proof comparator: $($cases.Count) cases passed. UNIT FIXTURES ONLY; no native gameplay was run."
foreach ($count in @(2,3,4)) {
    $topologyHost=$hostRow | ConvertTo-Json -Depth 5 | ConvertFrom-Json
    $topologyClient=$remoteRow | ConvertTo-Json -Depth 5 | ConvertFrom-Json
    $topologyHost.players=$count;$topologyClient.players=$count
    $receipt=@('[AED_V02][APPLY_RECEIPT] match=match decision=decision revision=2 fingerprint=plan window='+('a'*64)+' policy=policy')
    $valid=Find-AEDV02NativeProof -HostRows @($baseline,$topologyHost) -ClientRows @($topologyClient) -ReceiptLines $receipt -ExpectedPlayers $count
    if (!$valid) { throw "Matching $count-player topology was rejected." }
    $topologyClient.players=1
    if (Find-AEDV02NativeProof -HostRows @($baseline,$topologyHost) -ClientRows @($topologyClient) -ReceiptLines $receipt -ExpectedPlayers $count) {
        throw 'A remote topology mismatch must not prove native integration.'
    }
}
$fixturePath=Join-Path ([IO.Path]::GetTempPath()) ('aed-native-proof-'+[Guid]::NewGuid().ToString('N')+'.jsonl')
try {
    [IO.File]::WriteAllText($fixturePath, '{"revision":2}'+"`n"+'{"revision":')
    $rows=@(Read-AEDV02NativeRows -Path $fixturePath)
    if ($rows.Count -ne 1 -or $rows[0].revision -ne 2) { throw 'An unfinished final append must not invalidate preceding complete rows.' }
    [IO.File]::WriteAllText($fixturePath, '{bad-json}'+"`n")
    $rejected=$false
    try { Read-AEDV02NativeRows -Path $fixturePath | Out-Null } catch { $rejected=$true }
    if (!$rejected) { throw 'Malformed complete evidence must fail closed.' }
} finally { if (Test-Path -LiteralPath $fixturePath) { Remove-Item -LiteralPath $fixturePath } }
