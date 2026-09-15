<#
Fetches the vendored FFmpeg native libs for one RID from this repo's GitHub Release assets,
verifies them by SHA256 against OMP.Lib/native-libs.manifest.json, and extracts them into
OMP.Lib/Libs/<folder>/. Skipped entirely if a marker file matching the manifest's current sha256
is already present, so repeat builds/restores don't re-download.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("win-x64", "linux-x64")]
    [string]$Rid
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "OMP.Lib/native-libs.manifest.json"
$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json
$entry = $manifest.libraries.$Rid

if (-not $entry) {
    throw "No native-libs manifest entry for RID '$Rid'"
}

# The vendored Windows libs live under OMP.Lib/Libs/win, not .../win-x64 - see OMP.Ui.csproj's
# RID-conditional Content items (CLAUDE.md's "Native library bundling"). Linux's folder already
# matches its RID name.
$libsFolder = if ($Rid -eq "win-x64") { "win" } else { $Rid }
$libsDir = Join-Path $repoRoot "OMP.Lib/Libs/$libsFolder"
$markerPath = Join-Path $libsDir ".fetched-$($entry.sha256)"

if (Test-Path -Path $markerPath) {
    Write-Host "Native libs for $Rid already present and verified (sha256 $($entry.sha256)); skipping download."
    return
}

New-Item -ItemType Directory -Force -Path $libsDir | Out-Null

$downloadUrl = "https://github.com/$($manifest.repo)/releases/download/$($manifest.releaseTag)/$($entry.archive)"
$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) $entry.archive

Write-Host "Downloading native libs for $Rid from $downloadUrl"
Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

$sha256 = [System.Security.Cryptography.SHA256]::Create()
try {
    $fileStream = [System.IO.File]::OpenRead($zipPath)
    try {
        $hashBytes = $sha256.ComputeHash($fileStream)
    } finally {
        $fileStream.Dispose()
    }
} finally {
    $sha256.Dispose()
}
$actualHash = [System.BitConverter]::ToString($hashBytes).Replace("-", "")

if ($actualHash -ne $entry.sha256) {
    Remove-Item -Path $zipPath -Force
    throw "Checksum mismatch for $($entry.archive): expected $($entry.sha256), got $actualHash"
}

# Clear a stale marker (and any leftover files) from a previous manifest version before extracting.
Get-ChildItem -Path $libsDir -Filter ".fetched-*" -Force -ErrorAction SilentlyContinue | Remove-Item -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $libsDir)
Remove-Item -Path $zipPath -Force

New-Item -ItemType File -Path $markerPath -Force | Out-Null
Write-Host "Native libs for $Rid fetched and verified (sha256 $($entry.sha256))."
