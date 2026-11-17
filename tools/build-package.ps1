# build-package.ps1 - makes the installer: dist\StoryMaker-<version>-Setup.exe
#
#     .\tools\build-package.ps1
#
# The version comes from <Version> in StoryMaker.csproj. Needs the .NET 8 SDK and Inno Setup 6
# (winget install JRSoftware.InnoSetup). The program is published self-contained, so people who
# install it don't need .NET.

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$version = ([xml](Get-Content "$root\StoryMaker.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'No <Version> in StoryMaker.csproj.' }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 not found. Install it: winget install JRSoftware.InnoSetup' }

Write-Host "StoryMaker $version"
$app = Join-Path $root 'publish\app'
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
dotnet publish "$root\StoryMaker.csproj" -c Release -r win-x64 --self-contained true -o $app --nologo -v q `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# the wizard's pictures, drawn from the book icon in the program's navy and gold
& "$PSScriptRoot\make-wizard-images.ps1"

& $iscc /Q "/DAppVersion=$version" "/DSourceDir=$app" "$root\installer\StoryMaker.iss"
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }
$setup = Join-Path $root "dist\StoryMaker-$version-Setup.exe"
Write-Host ("Made {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB))
