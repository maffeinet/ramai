$ErrorActionPreference = 'Stop'
$loader = Join-Path $PSScriptRoot 'Import-DevEnvironment.ps1'
$fixtureDir = Join-Path ([IO.Path]::GetTempPath()) ('ramai-env-test-' + [guid]::NewGuid())
[IO.Directory]::CreateDirectory($fixtureDir) | Out-Null
$fixture = Join-Path $fixtureDir 'test.env'
$originalPassword = [Environment]::GetEnvironmentVariable('RAMAI_DB_PASSWORD', 'Process')
$originalHost = [Environment]::GetEnvironmentVariable('RAMAI_DB_HOST', 'Process')
$cases = @(
    @{ Text = '# comment' + "`nRAMAI_DB_PASSWORD='fixture-only'`nRAMAI_DB_HOST=localhost"; Valid = $true },
    @{ Text = 'RAMAI_DB_PASSWORD=replace-with-a-local-password'; Valid = $false },
    @{ Text = 'RAMAI_DB_PASSWORD='; Valid = $false },
    @{ Text = "RAMAI_DB_PASSWORD=fixture-only`nRAMAI_DB_PASSWORD=duplicate"; Valid = $false },
    @{ Text = "RAMAI_DB_PASSWORD=fixture-only`nPATH=untrusted"; Valid = $false },
    @{ Text = "RAMAI_DB_PASSWORD=fixture-only`ninvalid line"; Valid = $false }
)
try {
    foreach ($case in $cases) {
        [Environment]::SetEnvironmentVariable('RAMAI_DB_HOST', 'unchanged', 'Process')
        [IO.File]::WriteAllText($fixture, $case.Text)
        $failed = $false
        try { $output = @(& $loader -Path $fixture) } catch { $failed = $true }
        if ($failed -eq $case.Valid) { throw 'Unexpected loader validation result.' }
        if ($case.Valid) {
            if ($output.Count -ne 0 -or $env:RAMAI_DB_PASSWORD -ne 'fixture-only' -or
                $env:RAMAI_DB_HOST -ne 'localhost') { throw 'Invalid import or unexpected output.' }
        } elseif ($env:RAMAI_DB_HOST -ne 'unchanged') {
            throw 'Invalid file partially changed the environment.'
        }
    }
    $failed = $false
    try { & $loader -Path (Join-Path $fixtureDir 'missing.env') } catch { $failed = $true }
    if (-not $failed) { throw 'Missing file accepted.' }
    Write-Output 'PASS: 7 environment-loader cases; no values printed.'
} finally {
    [Environment]::SetEnvironmentVariable('RAMAI_DB_PASSWORD', $originalPassword, 'Process')
    [Environment]::SetEnvironmentVariable('RAMAI_DB_HOST', $originalHost, 'Process')
    Remove-Item -LiteralPath $fixture -ErrorAction SilentlyContinue
    [IO.Directory]::Delete($fixtureDir)
}
