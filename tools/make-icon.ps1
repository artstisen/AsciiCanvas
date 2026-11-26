# Draws the AsciiCanvas icon: a dark gray rounded square with three blue squares that touch at their corners and form an "A",
# turned slightly to the right. Writes AsciiCanvas.png (256 px, used for the window) and AsciiCanvas.ico (several sizes, used for
# the executables). Run it from anywhere; Windows PowerShell 5.1 or later.
param([string]$Output = (Join-Path $PSScriptRoot '..\src\AsciiCanvas.Avalonia\Assets\Icons'))

Add-Type -AssemblyName System.Drawing

$background = [System.Drawing.Color]::FromArgb(255, 45, 47, 52)
$edge = [System.Drawing.Color]::FromArgb(255, 70, 73, 80)
$blue = [System.Drawing.Color]::FromArgb(255, 61, 139, 253)
$tilt = 12

function New-RoundedRectangle([single]$x, [single]$y, [single]$size, [single]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $size - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $size - $d, $y + $size - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $path
}

function New-IconBitmap([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $inset = [single]($size * 0.02)
    $plate = [single]($size - (2 * $inset))
    $path = New-RoundedRectangle $inset $inset $plate ([single]($plate * 0.22))
    $g.FillPath((New-Object System.Drawing.SolidBrush $background), $path)
    if ($size -ge 32) {
        $g.DrawPath((New-Object System.Drawing.Pen $edge, ([single][Math]::Max(1, $size / 128))), $path)
    }

    # Unit squares at (1,0), (0,1) and (2,1): the top one touches each of the others at a corner, which makes an "A".
    $unit = [single]($size * 0.25)
    $g.TranslateTransform([single]($size / 2), [single]($size / 2))
    $g.RotateTransform([single]$tilt)
    $brush = New-Object System.Drawing.SolidBrush $blue
    foreach ($cell in @(@(1, 0), @(0, 1), @(2, 1))) {
        $x = ($cell[0] - 1.5) * $unit
        $y = ($cell[1] - 1.0) * $unit
        $g.FillRectangle($brush, [single]$x, [single]$y, $unit, $unit)
    }

    $g.Dispose()
    $bitmap
}

function Get-PngBytes($bitmap) {
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $stream.ToArray()
}

New-Item -ItemType Directory -Force $Output | Out-Null
$Output = (Resolve-Path $Output).Path

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) { , (Get-PngBytes (New-IconBitmap $size)) }

[System.IO.File]::WriteAllBytes((Join-Path $Output 'AsciiCanvas.png'), $images[$images.Count - 1])

# An ICO file is a small header, one directory entry per image, and the images themselves (PNG data is allowed).
$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}

foreach ($image in $images) {
    $writer.Write([byte[]]$image)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $Output 'AsciiCanvas.ico'), $stream.ToArray())
"Wrote AsciiCanvas.png and AsciiCanvas.ico to $Output"
