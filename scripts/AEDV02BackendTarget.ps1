function Get-AEDV02ProductionApiUrl {
    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../KLTN/Assets/Scripts/Api/ApiConfig.cs') -Raw
    $match = [regex]::Match($source, 'ProductionBaseUrl\s*=\s*"([^"]+)"')
    if (!$match.Success) { throw 'Production API allowlist is missing from ApiConfig.cs.' }
    return $match.Groups[1].Value
}

function Resolve-AEDV02BackendTarget {
    param([string]$ApiUrl, [string]$BackendEnvironment = 'Local', [switch]$AllowProduction)
    $uri = $null
    if ([string]::IsNullOrWhiteSpace($ApiUrl) -or ![Uri]::TryCreate($ApiUrl, [UriKind]::Absolute, [ref]$uri) -or
        $uri.UserInfo.Length -ne 0 -or $uri.Query.Length -ne 0 -or $uri.Fragment.Length -ne 0 -or $uri.AbsolutePath -ne '/') {
        throw 'Harness requires an absolute root API URL without credentials, query or fragment.'
    }
    if ([string]::IsNullOrEmpty($BackendEnvironment)) { $BackendEnvironment = 'Local' }
    if ($BackendEnvironment -ceq 'Local' -and $uri.IsLoopback -and $uri.Scheme -eq 'http') {
        return $uri.GetLeftPart([UriPartial]::Authority)
    }
    $expected = [Uri](Get-AEDV02ProductionApiUrl)
    if ($BackendEnvironment -ceq 'Production' -and $AllowProduction -and $uri.Scheme -eq 'https' -and
        [Uri]::Compare($uri, $expected, [UriComponents]::SchemeAndServer, [UriFormat]::UriEscaped, [StringComparison]::OrdinalIgnoreCase) -eq 0) {
        return $uri.GetLeftPart([UriPartial]::Authority)
    }
    throw 'Backend target rejected. Local requires HTTP loopback; Production requires the pinned HTTPS API and explicit -AllowProduction.'
}
