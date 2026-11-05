param(
    [Parameter(Mandatory)][string]$StackJson,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string[]]$Usernames,
    [Parameter(Mandatory)][Security.SecureString[]]$Passwords,
    [ValidateSet('Adaptive','Fixed')][string]$Mode='Adaptive',
    [switch]$Cabinet,
    [switch]$Shadow,
    [switch]$UseExistingAccounts,
    [switch]$ManualInput,
    [ValidateRange(30,1800)][int]$TimeoutSeconds=1800
)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=[IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Temp/AEDV02/Native'))+[IO.Path]::DirectorySeparatorChar
$path=[IO.Path]::GetFullPath($StackJson)
if (!$path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Owned disposable stack metadata required.' }
$stack=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$head=(& rtk proxy git -C $repo rev-parse HEAD | Out-String).Trim()
if (!$stack.ready -or $stack.runId -notmatch '^aedv02-[a-z0-9-]{1,48}$' -or !([Uri]$stack.apiUrl).IsLoopback -or
    $stack.database -ne 'aedv02_native_test' -or $stack.candidate -ne $head) { throw 'A healthy same-candidate local test stack is required.' }
if ($Usernames.Count -lt 2 -or $Usernames.Count -gt 4 -or $Usernames.Count -ne $Passwords.Count -or
    @($Usernames | Select-Object -Unique).Count -ne $Usernames.Count -or @($Passwords | Where-Object { !$_.Length }).Count) {
    throw 'Two to four distinct identities with nonempty SecureString passwords are required.'
}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new native evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$session='aed-'+[Guid]::NewGuid().ToString('N').Substring(0,20)
$configs=@()
for ($index=0;$index -lt $Usernames.Count;$index++) {
    $name=$Usernames[$index]
    if ($name -notmatch '^[a-zA-Z0-9_]{3,32}$') { throw 'Use a simple local test account name.' }
    $secret=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($Passwords[$index])
    try {
        $password=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($secret)
        $body=if ($UseExistingAccounts) { @{username=$name;password=$password} } else {
            @{username=$name;email=($name+'@example.invalid');password=$password;confirmPassword=$password}
        }
        $route=if ($UseExistingAccounts) { '/api/auth/login' } else { '/api/auth/register' }
        $response=Invoke-RestMethod -Uri ($stack.apiUrl+$route) -Method Post -ContentType 'application/json' -Body ($body | ConvertTo-Json) -TimeoutSec 30
        if (!$response.success) { throw 'Local pilot authentication failed.' }
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secret)
        $password=$null;$body=$null;$response=$null
    }
    $label=if ($index -eq 0) { 'Host' } elseif ($index -eq 1) { 'Client' } else { 'Client'+$index }
    $config=Join-Path $output "$label-private-config.json"
    @{role=if ($index -eq 0) { 'Host' } else { 'Client' };label=$label;mode=$Mode;playerCount=$Usernames.Count;
        apiUrl=$stack.apiUrl;username=$name;session=$session;output=$output;commandsFile=(Join-Path $output "$label-commands.jsonl");
        gameplay=(!$Shadow);cabinet=[bool]$Cabinet;autoRelief=(!$ManualInput);timeoutSeconds=$TimeoutSeconds} |
        ConvertTo-Json | Set-Content -LiteralPath $config -Encoding UTF8
    $configs+=$config
}
# Caller retains typed credentials in memory for the launcher; no password/token is written to disk.
[pscustomobject]@{output=$output;hostConfig=$configs[0];clientConfig=$configs[1];additionalClientConfig=@($configs | Select-Object -Skip 2);session=$session}
