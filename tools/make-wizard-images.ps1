# make-wizard-images.ps1 - draws the installer's pictures (installer\wizard-large.bmp, wizard-small.bmp):
# the book icon on lundqwist.se's navy, with the name in gold. Run by build-package.ps1.

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$book = [Drawing.Image]::FromFile("$root\book-256.png")
$navyTop = [Drawing.Color]::FromArgb(0x0e, 0x21, 0x47)
$navyDeep = [Drawing.Color]::FromArgb(0x08, 0x14, 0x28)
$gold = [Drawing.Color]::FromArgb(0xc9, 0xa3, 0x6b)

function Canvas([int]$w, [int]$h) {
    $b = New-Object Drawing.Bitmap $w, $h, ([Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Point 0, 0), (New-Object Drawing.Point 0, $h), $navyTop, $navyDeep)
    $g.FillRectangle($bg, 0, 0, $w, $h)
    $b, $g    # two values: the picture and the drawing surface
}

# the big picture on the first and last page (Inno Setup 6, modern style: 164 x 314 at 100%; drawn at 200%)
$b, $g = Canvas 328 628
$glow = New-Object Drawing.Drawing2D.GraphicsPath; $glow.AddEllipse(14, 120, 300, 300)
$pg = New-Object Drawing.Drawing2D.PathGradientBrush($glow)
$pg.CenterColor = [Drawing.Color]::FromArgb(80, 240, 190, 110); $pg.SurroundColors = [Drawing.Color[]]@([Drawing.Color]::FromArgb(0, 240, 190, 110))
$g.FillPath($pg, $glow)
$g.DrawImage($book, 64, 160, 200, 200)
$fmt = New-Object Drawing.StringFormat; $fmt.Alignment = 'Center'
$g.DrawString('StoryMaker', (New-Object Drawing.Font('Georgia', 30)), (New-Object Drawing.SolidBrush $gold), (New-Object Drawing.RectangleF 0, 400, 328, 60), $fmt)
$g.DrawString('Lunar Journal', (New-Object Drawing.Font('Segoe UI', 14)), (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(0x94, 0xa2, 0xbd))), (New-Object Drawing.RectangleF 0, 458, 328, 40), $fmt)
$b.Save("$root\installer\wizard-large.bmp", [Drawing.Imaging.ImageFormat]::Bmp)

# the small picture at the top right of the other pages (55 x 58 at 100%; drawn at 200%)
$b, $g = Canvas 110 116
$g.DrawImage($book, 7, 8, 96, 96)
$b.Save("$root\installer\wizard-small.bmp", [Drawing.Imaging.ImageFormat]::Bmp)
$book.Dispose()
