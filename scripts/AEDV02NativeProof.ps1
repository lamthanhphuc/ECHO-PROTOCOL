function Find-AEDV02NativeProof {
    param([object[]]$HostRows, [object[]]$ClientRows, [string[]]$ReceiptLines)
    foreach ($row in $HostRows | Where-Object { $_.revision -gt 1 -and $_.consumerObserved -and $_.players -eq 2 -and
        $_.consumerKind -eq 'StalkerFusionRuntime' -and $_.consumerRevision -eq $_.revision -and
        $_.consumerFingerprint -eq $_.fingerprint -and $_.consumerValues.Count -eq 6 -and $_.decisionPhaseOrdinal -gt 0 -and
        $_.decisionPoint -eq 'SAFE_WINDOW' -and $_.windowId -match '^[a-f0-9]{64}$' }) {
        if ($row.consumerValues[4] -ne $row.chaseSpeed) { continue }
        $remote = $ClientRows | Where-Object { $_.matchId -eq $row.matchId -and $_.decisionId -eq $row.decisionId -and
            $_.revision -eq $row.revision -and $_.fingerprint -eq $row.fingerprint -and $_.roster -eq $row.roster -and
            $_.actionFingerprint -eq $row.actionFingerprint -and $_.scene -eq $row.scene -and $_.policy -eq $row.policy -and $_.phase -eq $row.phase -and
            $_.windowId -eq $row.windowId -and $_.decisionPhaseOrdinal -eq $row.decisionPhaseOrdinal -and $_.decisionPoint -eq $row.decisionPoint -and
            $_.chaseSpeed -eq $row.chaseSpeed -and $_.players -eq 2 -and $_.consumerObserved -and
            $_.consumerKind -eq 'StalkerAnimatorPresenter' -and $_.presentedSpeed -gt .2 -and $_.animatorState -ne 0 -and
            $_.stalkerId -eq $row.stalkerId -and $_.consumerRevision -eq $row.revision -and
            $_.consumerFingerprint -eq $row.fingerprint -and $_.consumerValues.Count -eq 6 -and
            ([string]::Join('|', $_.consumerValues) -eq [string]::Join('|', $row.consumerValues)) } | Select-Object -Last 1
        $initial = $HostRows | Where-Object { $_.matchId -eq $row.matchId -and $_.revision -eq 1 -and $_.consumerObserved } | Select-Object -First 1
        if (!$remote -or !$initial -or $row.chaseSpeed -eq $initial.chaseSpeed) { continue }
        $receipt = $ReceiptLines | Where-Object { $_.Contains('[APPLY_RECEIPT]') -and $_.Contains($row.matchId) -and $_.Contains($row.decisionId) -and
            $_.Contains('revision='+$row.revision) -and $_.Contains('fingerprint='+$row.fingerprint) -and $_.Contains('window='+$row.windowId) -and
            $_.Contains('policy='+$row.policy) } | Select-Object -Last 1
        if ($receipt) { return [pscustomobject]@{host=$row;remoteClient=$remote;initial=$initial;receipt=$receipt} }
    }
    return $null
}

function Read-AEDV02NativeRows {
    param([Parameter(Mandatory)][string]$Path)
    $text=[IO.File]::ReadAllText($Path)
    if (!$text.EndsWith("`n")) {
        $lastNewline=$text.LastIndexOf("`n")
        if ($lastNewline -lt 0) { return @() }
        $text=$text.Substring(0,$lastNewline+1)
    }
    # Only an unfinished final append may be deferred; malformed complete records remain a failure.
    @($text.Split("`n") | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_ | ConvertFrom-Json })
}
