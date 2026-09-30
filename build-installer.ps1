$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host "Step 1 of 2: Building the app (this can take a minute)..."
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true -o "$PSScriptRoot\publish"
if ($LASTEXITCODE -ne 0) { throw "Building the app failed." }

$candidates = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  throw "Inno Setup was not found. Install it with: winget install -e --id JRSoftware.InnoSetup"
}

Write-Host "Step 2 of 2: Creating the installer..."
& $iscc "$PSScriptRoot\installer.iss"
if ($LASTEXITCODE -ne 0) { throw "Creating the installer failed." }

Write-Host ""
Write-Host "Done. Your installer is here:"
Write-Host "$PSScriptRoot\installer-output\RazerBatteryTray-Setup.exe"
