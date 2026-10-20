#requires -Version 7.4
[CmdletBinding()]
param(
    [switch]$Windows,
    [switch]$Linux,
    [string]$Image = 'node@sha256:a9f5f7c91a432850b2a8a7797adf5eadb6c733ceed61167806cee7ea7fbc29df'
)
$ErrorActionPreference = 'Stop'
if (-not $Windows -and -not $Linux) { throw 'Select -Windows and/or -Linux.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRoot = Join-Path ([IO.Path]::GetTempPath()) ('ramai-clean-conformance-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRoot | Out-Null
$evidence = Join-Path $taskRoot 'evidence'
New-Item -ItemType Directory -Path $evidence | Out-Null
$git = (Get-Command git).Source
$npm = (Get-Command npm.cmd).Source
$node = (Get-Command node).Source
$head = (& $git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve base HEAD.' }
$files = @(& $git -C $repoRoot ls-files --cached --others --exclude-standard -- tooling/frontend-conformance)
if ($LASTEXITCODE -ne 0 -or -not $files.Count) { throw 'No prototype snapshot files.' }
$snapshot = @()
foreach ($relative in $files) {
    if (-not $relative.StartsWith('tooling/frontend-conformance/') -or $relative.Contains('..')) {
        throw 'Unexpected snapshot path.'
    }
    $text = [IO.File]::ReadAllText((Join-Path $repoRoot $relative)).Replace("`r`n", "`n")
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
    $snapshot += [pscustomobject]@{ Path = $relative; SHA256_LF = $hash }
}
[pscustomobject]@{ BaseCommit = $head; Normalization = 'UTF-8 LF snapshot, no node_modules/cache/.env'; Files = $snapshot } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'candidate-files.json') -Encoding utf8

function New-ReviewClone([string]$name) {
    $destination = Join-Path $taskRoot $name
    & $git clone --no-hardlinks --quiet -- $repoRoot $destination
    if ($LASTEXITCODE -ne 0) { throw 'Clean clone failed.' }
    $initial = @(& $git -C $destination status --porcelain)
    if ($LASTEXITCODE -ne 0 -or $initial.Count) { throw 'Initial clone is not clean.' }
    foreach ($entry in $snapshot) {
        $target = Join-Path $destination $entry.Path
        New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
        $source = [IO.File]::ReadAllText((Join-Path $repoRoot $entry.Path)).Replace("`r`n", "`n")
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($source)))
        if ($actual -ne $entry.SHA256_LF) { throw 'Candidate changed during snapshot.' }
        # A generated verification snapshot, never an edit to the source checkout.
        [IO.File]::WriteAllText($target, $source, [Text.UTF8Encoding]::new($false))
    }
    & $git -C $destination diff --exit-code -- src .github
    if ($LASTEXITCODE -ne 0) { throw 'Snapshot changed application or CI.' }
    Write-Host "Clean HEAD clone + prototype snapshot: $destination"
    return $destination
}
$failed = $false
if ($Windows) {
    $clone = New-ReviewClone 'windows'
    $env:npm_config_cache = Join-Path $taskRoot 'windows-npm-cache'
    $env:NEXT_TELEMETRY_DISABLED = '1'
    $stages = @(
        @('baseline-install', 'src/frontend/ramai-web', 'ci', '--ignore-scripts', '--strict-peer-deps'),
        @('prototype-install', 'tooling/frontend-conformance', 'ci', '--ignore-scripts', '--strict-peer-deps'),
        @('prototype-lint', 'tooling/frontend-conformance', 'run', 'lint'),
        @('prototype-conformance', 'tooling/frontend-conformance', 'run', 'report'),
        @('prototype-audit', 'tooling/frontend-conformance', 'audit', '--json'),
        @('baseline-lint', 'src/frontend/ramai-web', 'run', 'lint'),
        @('baseline-types', 'src/frontend/ramai-web', 'run', 'type-check'),
        @('baseline-build', 'src/frontend/ramai-web', 'run', 'build'),
        @('baseline-production-audit', 'src/frontend/ramai-web', 'audit', '--omit=dev', '--json')
    )
    Push-Location $clone
    try {
        & $node --version
        & $npm --version
        foreach ($stage in $stages) {
            $label, $folder, $arguments = $stage[0], $stage[1], $stage[2..($stage.Count - 1)]
            Write-Host "STAGE $label"
            & $npm --prefix $folder @arguments
            $code = $LASTEXITCODE
            Write-Host "RESULT $label exit=$code"
            if ($code -ne 0) { $failed = $true }
        }
    } finally { Pop-Location }
    foreach ($file in @('results.json', 'tests.tap')) {
        $source = Join-Path $clone "artifacts/frontend-conformance/$file"
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $evidence "windows-$file") }
    }
}
if ($Linux) {
    $clone = New-ReviewClone 'linux'
    $docker = (Get-Command docker -ErrorAction SilentlyContinue).Source
    if (-not $docker) { $docker = 'C:/Program Files/Docker/Docker/resources/bin/docker.exe' }
    & $docker run --rm --user 1000:1000 --read-only --cpus 2 --memory 4g --pids-limit 256 `
        --cap-drop ALL --security-opt no-new-privileges `
        --mount "type=bind,source=$clone,target=/input,readonly" `
        --mount "type=bind,source=$evidence,target=/evidence" `
        --tmpfs '/work:rw,exec,nosuid,nodev,uid=1000,gid=1000,size=2147483648' `
        --tmpfs '/tmp:rw,nosuid,nodev,size=536870912' --workdir /work $Image `
        sh -c 'cp -R /input/. /work/ && sh tooling/frontend-conformance/verify-linux.sh'
    if ($LASTEXITCODE -ne 0) { $failed = $true }
}
Write-Host "Verification evidence retained: $evidence"
if ($failed) { throw 'One or more verification gates failed; inspect the retained evidence. No migration performed.' }
