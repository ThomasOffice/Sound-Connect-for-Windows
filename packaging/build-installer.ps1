[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipPublish,
    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $repo 'artifacts\release'

if (-not $Version) {
    [xml]$project = Get-Content (Join-Path $repo 'src\Xm6Control.App\Xm6Control.App.csproj')
    $Version = [string]($project.Project.PropertyGroup.Version | Select-Object -First 1)
}

if (-not $Version) {
    throw 'Could not determine the app version. Pass -Version explicitly.'
}

$publishDir = Join-Path $releaseDir "publish-$Version"
$zipPath = Join-Path $releaseDir "Xm6Control-$Version-win-x64.zip"
$setupPath = Join-Path $releaseDir "Xm6Control-$Version-win-x64-setup.exe"

if (-not $SkipPublish) {
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    Write-Host "Publishing $Version to $publishDir..."
    dotnet publish (Join-Path $repo 'src\Xm6Control.App\Xm6Control.App.csproj') `
        -c Release -r win-x64 --self-contained true -p:Platform=x64 `
        -p:DebugType=None -p:DebugSymbols=false `
        -o $publishDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet publish failed.'
    }
}

$exePath = Join-Path $publishDir 'Xm6Control.App.exe'
if (-not (Test-Path $exePath)) {
    throw "Published executable not found: $exePath"
}

$fileVersion = (Get-Item $exePath).VersionInfo.FileVersion
if ($fileVersion -ne "$Version.0") {
    throw "Published executable version is $fileVersion, expected $Version.0."
}

$files = Get-ChildItem $publishDir -Recurse -File
$totalMb = [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Published files: $($files.Count), size: $totalMb MB"

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publishDir,
    $zipPath,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)
Write-Host "Portable zip: $zipPath"

$isccCandidates = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source -ErrorAction SilentlyContinue),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe'
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    throw 'ISCC.exe not found. Install Inno Setup 6 first.'
}

if (Test-Path $setupPath) {
    Remove-Item $setupPath -Force
}

Write-Host "Compiling installer with $iscc..."
& $iscc `
    "/DAppVersion=$Version" `
    "/DStagingDir=$publishDir" `
    "/DOutputDir=$releaseDir" `
    (Join-Path $PSScriptRoot 'Xm6Control.iss')
if ($LASTEXITCODE -ne 0) {
    throw 'Inno Setup compilation failed.'
}

if (-not (Test-Path $setupPath)) {
    throw "Installer was not produced: $setupPath"
}

$hashes = @(
    (Get-FileHash $zipPath -Algorithm SHA256),
    (Get-FileHash $setupPath -Algorithm SHA256)
)
$sumLines = foreach ($hash in $hashes) {
    '{0}  {1}' -f $hash.Hash.ToUpperInvariant(), (Split-Path $hash.Path -Leaf)
}
$sumLines | Set-Content (Join-Path $releaseDir 'SHA256SUMS.txt') -Encoding ascii

Write-Host ''
Write-Host 'Release assets:'
foreach ($path in @($zipPath, $setupPath)) {
    $item = Get-Item $path
    Write-Host ('  {0}  {1:N2} MB' -f $item.Name, ($item.Length / 1MB))
}
Write-Host "  SHA256SUMS.txt"
Write-Host ''
Get-Content (Join-Path $releaseDir 'SHA256SUMS.txt')

if (-not $KeepStaging) {
    Remove-Item $publishDir -Recurse -Force
}
