# Regenerates src/Scrapbook/Scrapbook.ico: a white clipboard on a teal rounded tile.
# Each size is drawn separately (not scaled from 256) so the small taskbar sizes stay crisp.
# Usage: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\Scrapbook\Scrapbook.ico'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    if ($d -le 0) { $p.AddRectangle((New-Object System.Drawing.RectangleF $x, $y, $w, $h)); return $p }
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconPng([int]$size) {
    $s = $size / 256.0
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Shapes are laid out on a 256 grid and snapped to whole pixels at each size.
    function Px($v) { [single][Math]::Round($v * $s) }

    $teal     = [System.Drawing.Color]::FromArgb(255, 13, 148, 136)   # tile
    $tealDark = [System.Drawing.Color]::FromArgb(255, 17, 94, 89)     # clip and text lines
    $white    = [System.Drawing.Color]::White

    # Tile
    $tile = New-RoundRect (Px 8) (Px 8) (Px 240) (Px 240) ([Math]::Max(2, (Px 52)))
    $g.FillPath((New-Object System.Drawing.SolidBrush $teal), $tile)

    # Clipboard board
    $board = New-RoundRect (Px 58) (Px 56) (Px 140) (Px 168) (Px 18)
    $g.FillPath((New-Object System.Drawing.SolidBrush $white), $board)

    # Clip across the top edge
    $clip = New-RoundRect (Px 94) (Px 34) (Px 68) (Px 44) (Px 12)
    $g.FillPath((New-Object System.Drawing.SolidBrush $tealDark), $clip)

    # Text lines: three when there's room, two at the smallest sizes
    $lineBrush = New-Object System.Drawing.SolidBrush $tealDark
    $lineH = [Math]::Max(1, (Px 16))
    $lines = if ($size -ge 32) { @(@(104, 108), @(140, 108), @(176, 72)) } else { @(@(112, 108), @(160, 72)) }
    foreach ($l in $lines) {
        $g.FillPath($lineBrush, (New-RoundRect (Px 74) (Px $l[0]) (Px $l[1]) $lineH ([Math]::Min($lineH / 2, (Px 8)))))
    }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    if ($size -ge 256) {
        # 256 px is stored as PNG, as Windows expects.
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # Smaller sizes are stored as 32-bit DIBs, which every icon reader understands.
        $bw = New-Object System.IO.BinaryWriter $ms
        $bw.Write([UInt32]40); $bw.Write([Int32]$size); $bw.Write([Int32]($size * 2))  # height covers XOR + AND
        $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]0)
        $bw.Write([UInt32]0); $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
        for ($y = $size - 1; $y -ge 0; $y--) {          # rows bottom-up, BGRA
            for ($x = 0; $x -lt $size; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $maskRow = [int]([Math]::Ceiling($size / 32.0) * 4)  # AND mask unused with alpha; all zero
        $bw.Write((New-Object byte[] ($maskRow * $size)))
        $bw.Flush()
    }
    $bmp.Dispose()
    return , $ms.ToArray()
}

# ICO container: header, one directory entry per image, then the image payloads.
$images = foreach ($sz in $sizes) { , (New-IconPng $sz) }
$fs = [System.IO.File]::Create([System.IO.Path]::GetFullPath($out))
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$images[$i].Length); $w.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Dispose()
Write-Output "Wrote $([System.IO.Path]::GetFullPath($out)) ($($sizes -join ', ') px)"
