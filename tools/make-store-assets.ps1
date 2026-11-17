# make-store-assets.ps1 - draws the MSIX package's pictures into msix\Assets from book-512.png
# (made by make-icon.ps1): app icons for the taskbar and Start menu, the Start tiles on lundqwist.se's
# navy, and the Store logo. Run by build-msix.ps1.

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$out = Join-Path $root 'msix\Assets'
New-Item -ItemType Directory -Force $out | Out-Null
$book = [Drawing.Image]::FromFile("$root\book-512.png")
$navy = [Drawing.Color]::FromArgb(0x0e, 0x21, 0x47)
$navyDeep = [Drawing.Color]::FromArgb(0x08, 0x14, 0x28)

# The book on a canvas: $fill = how much of the shorter side it takes; $plate = navy background or none
function Save([string]$name, [int]$w, [int]$h, [double]$fill, [bool]$plate) {
    $b = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
    if ($plate) {
        $bg = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Point 0, 0), (New-Object Drawing.Point 0, $h), $navy, $navyDeep)
        $g.FillRectangle($bg, 0, 0, $w, $h)
    } else {
        $g.Clear([Drawing.Color]::Transparent)
    }
    $s = [int]([Math]::Min($w, $h) * $fill)
    $g.DrawImage($book, [int](($w - $s) / 2), [int](($h - $s) / 2), $s, $s)
    $g.Dispose()
    $b.Save((Join-Path $out $name), [Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
}

# App icon (taskbar, Start list, title bar): transparent, nearly full size
foreach ($scale in @(@(100, 44), @(125, 55), @(150, 66), @(200, 88), @(400, 176))) {
    Save "Square44x44Logo.scale-$($scale[0]).png" $scale[1] $scale[1] 0.92 $false
}
foreach ($t in 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256) {
    Save "Square44x44Logo.targetsize-$t.png" $t $t 0.96 $false
    Save "Square44x44Logo.targetsize-${t}_altform-unplated.png" $t $t 0.96 $false
    Save "Square44x44Logo.targetsize-${t}_altform-lightunplated.png" $t $t 0.96 $false
}
# Start tiles: the book on navy
foreach ($scale in @(@(100, 1), @(125, 1.25), @(150, 1.5), @(200, 2), @(400, 4))) {
    $f = $scale[1]
    Save "Square150x150Logo.scale-$($scale[0]).png" ([int](150 * $f)) ([int](150 * $f)) 0.62 $true
    Save "Wide310x150Logo.scale-$($scale[0]).png" ([int](310 * $f)) ([int](150 * $f)) 0.74 $true
    Save "SmallTile.scale-$($scale[0]).png" ([int](71 * $f)) ([int](71 * $f)) 0.70 $true
    Save "LargeTile.scale-$($scale[0]).png" ([int](310 * $f)) ([int](310 * $f)) 0.60 $true
}
# Store logo (the package's own picture in the Store and in Settings -> Apps)
foreach ($scale in @(@(100, 50), @(125, 63), @(150, 75), @(200, 100), @(400, 200))) {
    Save "StoreLogo.scale-$($scale[0]).png" $scale[1] $scale[1] 0.96 $false
}
$book.Dispose()
Write-Host "Wrote $((Get-ChildItem $out).Count) pictures to $out"
