param(
    [Parameter(Mandatory = $false)]
    [string]$Url = $env:CONTENT_ARCHIVE_URL,

    [Parameter(Mandatory = $false)]
    [string]$TargetDir = "src/Celeste.Android/ContentPackage"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Url)) {
    throw "Provide -Url <content-archive-url> or set CONTENT_ARCHIVE_URL environment variable."
}

$repoRoot = Split-Path -Path $PSScriptRoot -Parent
if (-not [System.IO.Path]::IsPathRooted($TargetDir)) {
    $TargetDir = Join-Path $repoRoot $TargetDir
}

Write-Host "[content] target: $TargetDir"
if (Test-Path $TargetDir) {
    Remove-Item -Path $TargetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

$archivePath = Join-Path $TargetDir "content.archive"
Write-Host "[content] downloading: $Url"
Invoke-WebRequest -Uri $Url -OutFile $archivePath

$pythonScript = @"
import os
import sys
import tarfile
import zipfile

archive_path = sys.argv[1]
target_dir = sys.argv[2]

if zipfile.is_zipfile(archive_path):
    with zipfile.ZipFile(archive_path) as archive:
        archive.extractall(target_dir)
elif tarfile.is_tarfile(archive_path):
    with tarfile.open(archive_path) as archive:
        archive.extractall(target_dir)
else:
    raise SystemExit(f"Unsupported archive format: {archive_path}")

content_dir = os.path.join(target_dir, "Content")
if not os.path.isdir(content_dir):
    raise SystemExit(
        f"Archive extracted but '{content_dir}' was not found. "
        "Expected archive root to contain a 'Content/' folder."
    )
"@

Write-Host "[content] extracting archive"
python -c $pythonScript $archivePath $TargetDir

Remove-Item -Path $archivePath -Force
Write-Host "[content] ready: $TargetDir/Content"
