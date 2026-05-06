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

# Copy WebView assets (they may not be included in single-file)
foreach ($assetName in @("Terminal", "Markdown")) {
    $assetsSource = "src/HermesDesktop/Assets/$assetName"
    $assetsDest = "$OutputDir/Assets/$assetName"
    if (Test-Path $assetsSource) {
        New-Item -ItemType Directory -Path $assetsDest -Force | Out-Null
        Copy-Item "$assetsSource/*" $assetsDest -Force
    }
}

$exePath = Get-ChildItem "$OutputDir/HermesDesktop.exe" -ErrorAction SilentlyContinue
if ($exePath) {
    $hash = (Get-FileHash $exePath.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  HermesDesktop.exe" | Out-File -Encoding ascii "$OutputDir/HermesDesktop.exe.sha256"
}

Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $OutputDir" -ForegroundColor Yellow

# Show output size
if ($exePath) {
    $sizeMB = [math]::Round($exePath.Length / 1MB, 1)
    Write-Host "Executable: $($exePath.Name) ($sizeMB MB)" -ForegroundColor Yellow
    Write-Host "Checksum: HermesDesktop.exe.sha256" -ForegroundColor Yellow
}
