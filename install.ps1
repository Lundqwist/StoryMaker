# install.ps1 - build StoryMaker and put it in the Start menu.
#
#     .\install.ps1            build to %LOCALAPPDATA%\LunarJournal\StoryMaker and add a Start menu shortcut
#     .\install.ps1 -Remove    remove both again (settings in %APPDATA%\LunarJournal are kept)
#
# Needs the .NET 8 desktop runtime (or newer SDK) to run.

param([switch]$Remove)

$ErrorActionPreference = 'Stop'
$target = Join-Path $env:LOCALAPPDATA 'LunarJournal\StoryMaker'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Lunar Journal StoryMaker.lnk'

if ($Remove) {
    Remove-Item $shortcut -ErrorAction SilentlyContinue
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Write-Host "Removed StoryMaker and its shortcut."
    return
}

dotnet publish "$PSScriptRoot\StoryMaker.csproj" -c Release -o $target --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = Join-Path $target 'StoryMaker.exe'
$link.WorkingDirectory = $target
$link.Description = 'Write stories from your Lunar Journal and post them to lundqwist.se'
$link.Save()
Write-Host "Installed to $target. Start it from the Start menu: Lunar Journal StoryMaker."
