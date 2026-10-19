$ErrorActionPreference = 'Stop'
# Public versioned corpus only. Never submit user passwords to this script or network.
$revision = '1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56'
$baseUri = "https://raw.githubusercontent.com/danielmiessler/SecLists/$revision"
$response = Invoke-WebRequest -Uri "$baseUri/Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt"
$bytes = [byte[]]$response.RawContentStream.ToArray()
if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)) -ne 'C2E5696882C603B76BB67A47EE970897E5A76FC4C3F5547ABE3D0CA340C576E0') {
    throw 'Public corpus checksum mismatch.'
}
$license = Invoke-WebRequest -Uri "$baseUri/LICENSE"
$licenseBytes = [byte[]]$license.RawContentStream.ToArray()
if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($licenseBytes)) -ne '3DBDC93D5F8829DE0941744841730A09C106D0732E5AE0E98CA1D77BE7DED66C') {
    throw 'License checksum mismatch.'
}
$hashes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$entries = 0
foreach ($line in ([Text.Encoding]::UTF8.GetString($bytes) -split "`n")) {
    $value = $line.TrimEnd("`r")
    if ($value.Length -eq 0) { continue }
    $entries++
    $normalized = $value.Normalize()
    $count = 0
    foreach ($rune in $normalized.EnumerateRunes()) { $count++ }
    if ($count -ge 15 -and $count -le 128) {
        [void]$hashes.Add([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($normalized.ToUpperInvariant()))))
    }
}
$target = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../src/backend/Persistence/Ramai.Modules.Identity.Persistence/AccountSecurity/Data'))
[void][IO.Directory]::CreateDirectory($target)
# Deterministic generated data; no cleartext corpus/passwords in repository.
[IO.File]::WriteAllText((Join-Path $target 'ncsc-eligible.sha256'), (($hashes | Sort-Object) -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllBytes((Join-Path $target 'SecLists-LICENSE.txt'), $licenseBytes)
Write-Output "Public corpus entries: $entries; eligible distinct digests: $($hashes.Count). No passwords printed."
