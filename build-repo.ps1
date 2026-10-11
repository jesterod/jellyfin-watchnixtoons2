#!/usr/bin/env pwsh
# Build and package script for WatchNixtoons2 Jellyfin plugin (Windows PowerShell)

$PluginName = "Jellyfin.Plugin.WatchNixtoons2"
$Version = "1.0.0"
$BuildDir = "publish"
$PackageDir = "package"
$RepoDir = "repo"

Write-Host "Building $PluginName v$Version..." -ForegroundColor Green

# Clean previous builds
Remove-Item -Recurse -Force $BuildDir, $PackageDir, $RepoDir -ErrorAction SilentlyContinue

# Build in Release mode
dotnet publish -c Release -o $BuildDir

# Create package directory structure
New-Item -ItemType Directory -Force -Path "$PackageDir/$PluginName" | Out-Null
Copy-Item -Path "$BuildDir/*" -Destination "$PackageDir/$PluginName/" -Recurse -Force

# Create zip package
New-Item -ItemType Directory -Force -Path $RepoDir | Out-Null
Compress-Archive -Path "$PackageDir/$PluginName" -DestinationPath "$RepoDir/${PluginName}_${Version}.zip" -Force

# Generate SHA256 checksum
$Checksum = (Get-FileHash "$RepoDir/${PluginName}_${Version}.zip" -Algorithm SHA256).Hash.ToLower()
Write-Host "SHA256: $Checksum" -ForegroundColor Cyan

# Update manifest.json
$Manifest = Get-Content manifest.json -Raw
$Manifest = $Manifest -replace '"version": "1.0.0"', "`"version`": `"$Version`""
$Manifest = $Manifest -replace 'sha256:PLACEHOLDER_REPLACE_WITH_ACTUAL_HASH', "sha256:$Checksum"
$Manifest = $Manifest -replace 'https://your-domain.com/jellyfin-watchnixtoons2/Jellyfin.Plugin.WatchNixtoons2_1.0.0.zip', "https://your-domain.com/jellyfin-watchnixtoons2/${PluginName}_${Version}.zip"
$Manifest | Set-Content manifest.json

# Copy manifest to repo directory
Copy-Item manifest.json "$RepoDir/"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Repository package created in $RepoDir/" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Files to host:"
Write-Host "  - $RepoDir/manifest.json"
Write-Host "  - $RepoDir/${PluginName}_${Version}.zip"
Write-Host ""
Write-Host "Update manifest.json with your actual domain URL" -ForegroundColor Yellow
Write-Host "Then add repository URL to Jellyfin: https://your-domain.com/jellyfin-watchnixtoons2/manifest.json" -ForegroundColor Yellow