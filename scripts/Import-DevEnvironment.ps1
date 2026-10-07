param(
    [string]$Path = (Join-Path (Split-Path $PSScriptRoot -Parent) '.env')
)

# Never evaluate or print file contents. Validate all lines before importing.
$allowed = @(
    'ASPNETCORE_ENVIRONMENT', 'ASPNETCORE_URLS',
    'RAMAI_DB_HOST', 'RAMAI_DB_PORT', 'RAMAI_DB_NAME', 'RAMAI_DB_USER',
    'RAMAI_DB_PASSWORD', 'RAMAI_REDIS_HOST', 'RAMAI_REDIS_PORT',
    'RAMAI_FRONTEND_URL', 'RAMAI_API_URL'
)
if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw 'File .env assente: copiare .env.example e configurare la password locale.'
}
$values = @{}
$lineNumber = 0
foreach ($line in Get-Content -LiteralPath $Path) {
    $lineNumber++
    if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
        continue
    }
    if ($line -notmatch '^\s*([A-Z][A-Z0-9_]*)\s*=(.*)$') {
        throw "Formato .env non valido alla riga $lineNumber."
    }
    $key = $Matches[1]
    $value = $Matches[2].Trim()
    if ($key -notin $allowed -or $values.ContainsKey($key)) {
        throw "Chiave non consentita o duplicata alla riga $lineNumber."
    }
    if ($value.Length -ge 2 -and (
        ($value.StartsWith('"') -and $value.EndsWith('"')) -or
        ($value.StartsWith("'") -and $value.EndsWith("'")))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    $values[$key] = $value
}
if ([string]::IsNullOrWhiteSpace($values['RAMAI_DB_PASSWORD']) -or
    $values['RAMAI_DB_PASSWORD'] -eq 'replace-with-a-local-password') {
    throw 'Configurare una password locale non vuota e diversa dal placeholder.'
}
foreach ($key in $values.Keys) {
    [Environment]::SetEnvironmentVariable($key, $values[$key], 'Process')
}
