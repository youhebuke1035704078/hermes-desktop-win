# Hermes Desktop Windows - Build & Publish Script
# Usage: powershell -ExecutionPolicy Bypass -File build/publish.ps1

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "./publish"
)

$ErrorActionPreference = "Stop"

Write-Host "Building Hermes Desktop for $Runtime ($Configuration)..." -ForegroundColor Cyan

# Clean
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}

# Publish as self-contained single file
dotnet publish src/HermesDesktop/HermesDesktop.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Copy terminal assets (they may not be included in single-file)
$assetsSource = "src/HermesDesktop/Assets/Terminal"
$assetsDest = "$OutputDir/Assets/Terminal"
if (Test-Path $assetsSource) {
    New-Item -ItemType Directory -Path $assetsDest -Force | Out-Null
    Copy-Item "$assetsSource/*" $assetsDest -Force
}

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $OutputDir" -ForegroundColor Yellow

# Show output size
$exePath = Get-ChildItem "$OutputDir/HermesDesktop.exe" -ErrorAction SilentlyContinue
if ($exePath) {
    $sizeMB = [math]::Round($exePath.Length / 1MB, 1)
    Write-Host "Executable: $($exePath.Name) ($sizeMB MB)" -ForegroundColor Yellow
}
