# build-msix.ps1 - makes the Microsoft Store package: dist\StoryMaker-<version>.0-x64.msix
#
#     .\tools\build-msix.ps1
#
# The Store identity comes from msix\store-identity.json, which you make once after reserving the app's
# name in Partner Center (Product management -> Product identity):
#
#     { "IdentityName": "12345Lundqwist.StoryMaker",
#       "Publisher": "CN=xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
#       "PublisherDisplayName": "Lundqwist",
#       "DisplayName": "StoryMaker" }
#
# Without that file a test identity is used: the package is then only good for checking it builds.
# The package is not signed: the Store signs it when it's submitted.
# Needs the .NET 8 SDK and the Windows SDK (MakeAppx.exe, MakePri.exe).

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$version = ([xml](Get-Content "$root\StoryMaker.csproj")).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'No <Version> in StoryMaker.csproj.' }
$packageVersion = "$version.0"   # the Store wants four numbers, the last one 0

$identityFile = Join-Path $root 'msix\store-identity.json'
if (Test-Path $identityFile) {
    $id = Get-Content $identityFile -Raw | ConvertFrom-Json
} else {
    Write-Warning 'No msix\store-identity.json yet: using a test identity. Reserve the name in Partner Center first.'
    $id = [pscustomobject]@{ IdentityName = 'Lundqwist.StoryMaker.Test'; Publisher = 'CN=Lundqwist'; PublisherDisplayName = 'Lundqwist'; DisplayName = 'StoryMaker' }
}

$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $sdk) { throw 'Windows SDK not found (MakeAppx.exe). Install it: winget install Microsoft.WindowsSDK.10.0.26100' }
$makeappx = $sdk.FullName
$makepri = Join-Path $sdk.DirectoryName 'makepri.exe'

Write-Host "StoryMaker $packageVersion for the Store ($($id.IdentityName))"

# 1. the program, self-contained (as one folder: the package is compressed anyway)
$layout = Join-Path $root 'publish\msix'
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
dotnet publish "$root\StoryMaker.csproj" -c Release -r win-x64 --self-contained true -o $layout --nologo -v q -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# 2. pictures and the manifest
& "$PSScriptRoot\make-store-assets.ps1"
Copy-Item (Join-Path $root 'msix\Assets') (Join-Path $layout 'Assets') -Recurse
$manifest = Get-Content (Join-Path $root 'msix\AppxManifest.xml') -Raw
foreach ($pair in @{ '$IdentityName$' = $id.IdentityName; '$Publisher$' = $id.Publisher; '$PublisherDisplayName$' = $id.PublisherDisplayName;
                     '$DisplayName$' = $id.DisplayName; '$Version$' = $packageVersion }.GetEnumerator()) {
    $manifest = $manifest.Replace($pair.Key, [Security.SecurityElement]::Escape($pair.Value))
}
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding $false))

# 3. the resource index, so Windows finds the right size of each picture (Assets\x.scale-200.png etc.)
$priConfig = Join-Path $root 'publish\priconfig.xml'
& $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
& $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'MakePri failed.' }

# 4. the package
New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null
$msix = Join-Path $root "dist\StoryMaker-$packageVersion-x64.msix"
& $makeappx pack /d $layout /p $msix /o /h SHA256 | Select-Object -Last 2
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed.' }
Write-Host ("Made {0} ({1:N1} MB)" -f $msix, ((Get-Item $msix).Length / 1MB))
