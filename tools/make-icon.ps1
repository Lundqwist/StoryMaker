# make-icon.ps1 - draws StoryMaker's icon: an old leather-bound book with gold corners, a clasp
# and a crescent moon on the cover. Writes StoryMaker.ico (16, 24, 32, 48, 64, 128 and 256 pixels)
# and book-256.png next to the project file.
#
#     .\tools\make-icon.ps1

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$out = Split-Path $PSScriptRoot

function C([string]$hex, [int]$a = 255) { [Drawing.Color]::FromArgb($a, [Convert]::ToInt32($hex.Substring(0, 2), 16), [Convert]::ToInt32($hex.Substring(2, 2), 16), [Convert]::ToInt32($hex.Substring(4, 2), 16)) }

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); $p
}

function Poly([float[]]$pts) {
    $list = for ($i = 0; $i -lt $pts.Length; $i += 2) { New-Object Drawing.PointF($pts[$i], $pts[$i + 1]) }
    [Drawing.PointF[]]$list
}

# Everything is drawn on a 256 x 256 grid and scaled; small sizes leave out the fine details
function Draw([int]$size) {
    $bmp = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.ScaleTransform($size / 256, $size / 256)
    $fine = $size -ge 48

    # shadow
    if ($fine) { $g.FillEllipse((New-Object Drawing.SolidBrush (C '000000' 70)), 30, 222, 200, 22) }

    # back cover, then the block of pages showing at the right and bottom
    $g.FillPath((New-Object Drawing.SolidBrush (C '3a1a0c')), (RoundRect 44 26 182 206 12))
    $pages = RoundRect 52 30 166 194 6
    $pageBrush = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF 150, 30), (New-Object Drawing.PointF 220, 30), (C 'f3e7c8'), (C 'cdb98d'))
    $g.FillPath($pageBrush, $pages)
    if ($fine) {
        $line = New-Object Drawing.Pen((C 'a8916a' 150), 1.2)
        for ($y = 40; $y -le 214; $y += 7) { $g.DrawLine($line, 206, $y, 216, $y + 1) }
        for ($x = 70; $x -le 204; $x += 9) { $g.DrawLine($line, $x, 214, $x + 1, 222) }
    }

    # front cover: worn leather
    $cover = RoundRect 34 18 176 200 11
    $leather = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF 34, 18), (New-Object Drawing.PointF 210, 218), (C '8a4a22'), (C '4b2310'))
    $g.FillPath($leather, $cover)
    if ($fine) {
        # a soft light from the top left, and darker worn edges
        $glow = New-Object Drawing.Drawing2D.PathGradientBrush($cover)
        $glow.CenterPoint = New-Object Drawing.PointF 110, 80
        $glow.CenterColor = C 'b8693a' 90
        $glow.SurroundColors = [Drawing.Color[]]@(C '2a1206' 0)
        $g.FillPath($glow, $cover)
        $g.DrawPath((New-Object Drawing.Pen((C '2a1206'), 3)), $cover)
    }

    # spine with raised bands
    $spine = RoundRect 34 18 30 200 11
    $g.FillPath((New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF 34, 0), (New-Object Drawing.PointF 66, 0), (C '3a1a0b'), (C '6b3519'))), $spine)
    $g.DrawLine((New-Object Drawing.Pen((C '1f0c04' 200), 2.5)), 64, 22, 64, 214)
    if ($fine) {
        foreach ($y in 48, 84, 152, 188) {
            $g.FillRectangle((New-Object Drawing.SolidBrush (C 'c9963f')), 36, $y, 27, 5)
            $g.FillRectangle((New-Object Drawing.SolidBrush (C '7a5520')), 36, ($y + 5), 27, 2)
        }
    }

    $gold = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF 0, 18), (New-Object Drawing.PointF 0, 218), (C 'f6d27a'), (C 'b07a28'))
    $goldPen = New-Object Drawing.Pen($gold, $(if ($fine) { 3.2 } else { 6 }))

    # gold frame on the cover
    if ($size -ge 32) { $g.DrawPath($goldPen, (RoundRect 78 34 116 168 6)) }

    # gold corner pieces
    $g.FillPolygon($gold, (Poly @(210, 18, 210, 58, 194, 58, 194, 34, 170, 34, 170, 18)))
    $g.FillPolygon($gold, (Poly @(210, 218, 210, 178, 194, 178, 194, 202, 170, 202, 170, 218)))
    if ($fine) {
        $edge = New-Object Drawing.Pen((C '7a5017'), 1.5)
        $g.DrawPolygon($edge, (Poly @(210, 18, 210, 58, 194, 58, 194, 34, 170, 34, 170, 18)))
        $g.DrawPolygon($edge, (Poly @(210, 218, 210, 178, 194, 178, 194, 202, 170, 202, 170, 218)))
    }

    # the clasp across the page edge
    $g.FillPath((New-Object Drawing.SolidBrush (C '3a1a0b')), (RoundRect 188 100 36 36 6))
    $g.FillPath($gold, (RoundRect 200 106 24 24 5))
    if ($fine) { $g.FillEllipse((New-Object Drawing.SolidBrush (C '6d4512')), 207, 113, 10, 10) }

    # crescent moon on the cover
    $moon = New-Object Drawing.Drawing2D.GraphicsPath
    $moon.AddEllipse(100, 72, 76, 76)
    $cut = New-Object Drawing.Drawing2D.GraphicsPath
    $cut.AddEllipse(122, 60, 72, 72)
    $region = New-Object Drawing.Region($moon)
    $region.Exclude($cut)
    $g.FillRegion($gold, $region)
    if ($fine) {
        # three small stars
        foreach ($s in @(@(150, 150, 5), @(166, 132, 3.5), @(104, 170, 3))) {
            $x = $s[0]; $y = $s[1]; $r = $s[2]
            $g.FillPolygon($gold, (Poly @($x, ($y - 2.6 * $r), ($x + 0.7 * $r), ($y - 0.7 * $r), ($x + 2.6 * $r), $y, ($x + 0.7 * $r), ($y + 0.7 * $r), $x, ($y + 2.6 * $r), ($x - 0.7 * $r), ($y + 0.7 * $r), ($x - 2.6 * $r), $y, ($x - 0.7 * $r), ($y - 0.7 * $r))))
        }
    }

    $g.Dispose()
    $bmp
}

# A classic icon image: BITMAPINFOHEADER, 32-bit BGRA rows bottom-up, then a 1-bit AND mask.
# Used for every size below 256, which older readers (WinForms' Icon among them) can't take as PNG.
function Dib([Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $ms = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($ms)
    $w.Write([uint32]40); $w.Write([int32]$s); $w.Write([int32]($s * 2)); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]0); $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) { $c = $bmp.GetPixel($x, $y); $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A) }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32) * 4)
    for ($y = $s - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $maskRow
        for ($x = 0; $x -lt $s; $x++) { if ($bmp.GetPixel($x, $y).A -eq 0) { $row[[int][Math]::Floor($x / 8)] = $row[[int][Math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8)) } }
        $w.Write($row)
    }
    , $ms.ToArray()   # the comma keeps PowerShell from unrolling the bytes
}

# The .ico: small sizes as classic bitmaps, 256 as PNG (as Windows itself does)
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) {
    $bmp = Draw $s
    if ($s -eq 256) {
        $ms = New-Object IO.MemoryStream
        $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
        $bmp.Save((Join-Path $out 'book-256.png'), [Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    } else {
        [byte[]]$bytes = Dib $bmp
    }
    $bmp.Dispose()
    , $bytes
}
$file = New-Object IO.MemoryStream
$w = New-Object IO.BinaryWriter($file)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write([byte[]]$img) }
[IO.File]::WriteAllBytes((Join-Path $out 'StoryMaker.ico'), $file.ToArray())
Write-Host "Wrote $(Join-Path $out 'StoryMaker.ico') and book-256.png"
