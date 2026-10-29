# Generates Assets\Pulse.ico (16..256 px, PNG-compressed entries) and Assets\Pulse.png
# Design: rounded squircle with a violet->cyan gradient and a white heartbeat line.
param([string]$OutDir = (Join-Path $PSScriptRoot '..\Assets'))
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-PulseBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.5, $s * 0.04); $w = $s - 2 * $pad; $r = $w * 0.26
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $x = $pad; $y = $pad; $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $w - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $rect = New-Object System.Drawing.RectangleF $x, $y, $w, $w
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 124, 58, 237)), ([System.Drawing.Color]::FromArgb(255, 6, 182, 212)), 45.0
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
    $blend.Colors = @([System.Drawing.Color]::FromArgb(255, 109, 40, 217), [System.Drawing.Color]::FromArgb(255, 79, 70, 229), [System.Drawing.Color]::FromArgb(255, 6, 182, 212))
    $blend.Positions = @(0.0, 0.5, 1.0)
    $grad.InterpolationColors = $blend
    $g.FillPath($grad, $path)

    # soft top highlight
    $hlRect = New-Object System.Drawing.RectangleF $x, ($y - 1), $w, ($w * 0.56 + 1)
    $hl = New-Object System.Drawing.Drawing2D.LinearGradientBrush $hlRect, ([System.Drawing.Color]::FromArgb(55, 255, 255, 255)), ([System.Drawing.Color]::FromArgb(0, 255, 255, 255)), 90.0
    $g.SetClip($path)
    $g.FillRectangle($hl, $x, $y, $w, $w * 0.55)
    $g.ResetClip()

    # heartbeat line
    $pts = @(
        @(0.16, 0.55), @(0.34, 0.55), @(0.42, 0.36), @(0.52, 0.74),
        @(0.60, 0.28), @(0.68, 0.55), @(0.84, 0.55)
    ) | ForEach-Object { New-Object System.Drawing.PointF ($x + $_[0] * $w), ($y + $_[1] * $w) }
    $pw = [Math]::Max(1.4, $s * 0.075)
    $glow = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(60, 255, 255, 255)), ($pw * 2.2)
    $glow.LineJoin = 'Round'; $glow.StartCap = 'Round'; $glow.EndCap = 'Round'
    if ($s -ge 48) { $g.DrawLines($glow, [System.Drawing.PointF[]]$pts) }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $pw
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    $g.DrawLines($pen, [System.Drawing.PointF[]]$pts)

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$blobs = @()
foreach ($s in $sizes) {
    $b = New-PulseBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $blobs += , ($ms.ToArray())
    if ($s -eq 256) { $b.Save((Join-Path $OutDir 'Pulse.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $b.Dispose()
}

$icoPath = Join-Path $OutDir 'Pulse.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $blobs[$i].Length
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$len); $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($blob in $blobs) { $bw.Write($blob) }
$bw.Close()
Write-Host "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes)"
