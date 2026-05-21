param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Framework = "net8.0-windows"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\PathFinder\PathFinder.csproj"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishDir = Join-Path $root "dist\publish\$Runtime-portable"
$distDir = Join-Path $root "dist"
$zipPath = Join-Path $distDir "PathFinder-$Runtime-portable.zip"

Write-Host "Publishing app to $publishDir"
dotnet publish $project -c $Configuration -f $Framework -r $Runtime --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating portable installer package: $zipPath"
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Done. Installer package: $zipPath"
