<#
.SYNOPSIS
  Links unity/Assets/Shared in this work space to the shared asset folder outside git.

.DESCRIPTION
  Binary assets (models, textures/images, audio, fonts, external packages) are not committed.
  They live in one folder on this machine, and every work space points unity/Assets/Shared at it
  with a directory junction (mklink /J), so all work spaces see the same files and the same .meta GUIDs.
  Safe to re-run. Close the Unity editor for this project first.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools/link-shared-assets.ps1
  powershell -ExecutionPolicy Bypass -File tools/link-shared-assets.ps1 -Target D:\other\Assets
#>
param(
    [string]$Target = $(if ($env:SEVEN_STEPS_SHARED) { $env:SEVEN_STEPS_SHARED } else { 'C:\unityjam\shared-assets\seven-steps\Assets' })
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $repo 'unity\Assets'
$link = Join-Path $assets 'Shared'

if (-not (Test-Path $assets)) { throw "unity\Assets not found under $repo" }
if (-not (Test-Path $Target)) {
    New-Item -ItemType Directory -Path $Target | Out-Null
    Write-Host "[link-shared-assets] created $Target"
}
$Target = (Resolve-Path $Target).Path

if (Test-Path $link) {
    $item = Get-Item $link -Force
    if ($item.LinkType -eq 'Junction' -or $item.LinkType -eq 'SymbolicLink') {
        $current = @($item.Target)[0]
        if ($current -and ((Resolve-Path $current).Path -eq $Target)) {
            Write-Host "[link-shared-assets] OK: $link -> $Target (already linked)"
            exit 0
        }
        # Wrong target: remove only the link itself, never the files behind it.
        cmd /c rmdir "$link" | Out-Null
        Write-Host "[link-shared-assets] removed old link -> $current"
    }
    else {
        throw "$link is a real folder, not a junction. Move its files into $Target (keep the .meta files), delete it, and run again."
    }
}

cmd /c mklink /J "$link" "$Target" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "mklink /J failed ($LASTEXITCODE)" }

$count = (Get-ChildItem $link -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Extension -ne '.meta' }).Count
Write-Host "[link-shared-assets] OK: $link -> $Target ($count asset files)"
