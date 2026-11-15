# Draws the Polish app icon and writes polish.ico (16-256 px) plus a preview PNG.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1
# Theme: Water element (navy / ocean blue) + Metal, which feeds Water (white / silver).
# Prosperity motifs: a silver square-hole coin and an incoming wave. No yellow/brown (Earth) or orange (Fire).
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

$navy   = [Drawing.Color]::FromArgb(8, 27, 61)
$ocean  = [Drawing.Color]::FromArgb(22, 101, 192)
$silver = [Drawing.Color]::FromArgb(236, 241, 247)
$steel  = [Drawing.Color]::FromArgb(150, 166, 186)

function New-RoundRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

function New-Wave([single]$s, [single]$base, [single]$amp, [single]$phase) {
    # Filled wave from y=$base down to the bottom edge
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $pts = New-Object 'System.Collections.Generic.List[Drawing.PointF]'
    for ($i = 0; $i -le 40; $i++) {
        $x = $s * $i / 40.0
        $pts.Add((New-Object Drawing.PointF($x, [single]($base + $amp * [Math]::Sin(($x / $s) * 2 * [Math]::PI * 1.25 + $phase)))))
    }
    $pts.Add((New-Object Drawing.PointF($s, $s))); $pts.Add((New-Object Drawing.PointF(0, $s)))
    $p.AddPolygon($pts.ToArray()); return $p
}

function Draw-Icon([int]$s) {
    $bmp = New-Object Drawing.Bitmap($s, $s, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'
    $m = [single][Math]::Max(0.5, $s * 0.03)
    $rect = New-RoundRect $m $m ($s - 2 * $m) ($s - 2 * $m) ([single]($s * 0.22))
    $grad = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF(0, 0)), (New-Object Drawing.PointF($s, $s)), $navy, $ocean)
    $g.FillPath($grad, $rect)

    # Waves (wealth flowing in), clipped to the tile
    if ($s -ge 32) {
        $g.SetClip($rect)
        $g.FillPath((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(38, 255, 255, 255))), (New-Wave $s ($s * 0.76) ($s * 0.035) 0.0))
        $g.FillPath((New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(48, 190, 225, 255))), (New-Wave $s ($s * 0.85) ($s * 0.03) 2.2))
        $g.ResetClip()
    }

    # Silver "P" (Metal feeds Water)
    $fontPx = [single]($s * 0.70)
    $font = New-Object Drawing.Font('Segoe UI', $fontPx, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $dx = if ($s -ge 32) { -$s * 0.06 } else { 0 }
    $pBrush = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.PointF(0, ($s * 0.15))), (New-Object Drawing.PointF(0, ($s * 0.9))),
        [Drawing.Color]::White, [Drawing.Color]::FromArgb(206, 220, 236))
    $g.DrawString('P', $font, $pBrush, (New-Object Drawing.RectangleF($dx, ($s * 0.03), $s, $s)), $sf)

    # Silver square-hole coin (prosperity) on larger sizes
    if ($s -ge 40) {
        $cx = $s * 0.765; $cy = $s * 0.245; $r = $s * 0.145
        $coinRect = New-Object Drawing.RectangleF(($cx - $r), ($cy - $r), (2 * $r), (2 * $r))
        $coin = New-Object Drawing.Drawing2D.LinearGradientBrush($coinRect, $silver, $steel, [single]45)
        $g.FillEllipse($coin, $coinRect)
        if ($s -ge 48) {
            $ring = New-Object Drawing.Pen([Drawing.Color]::FromArgb(120, 70, 90, 120), [single]([Math]::Max(1, $s * 0.012)))
            $ir = $r * 0.78
            $g.DrawEllipse($ring, ($cx - $ir), ($cy - $ir), (2 * $ir), (2 * $ir))
        }
        $h = $r * 0.24
        if ($s -ge 48) {
            # raised square rim around the hole, as on an ancient coin
            $sq = $h * 1.75
            $g.DrawRectangle((New-Object Drawing.Pen([Drawing.Color]::FromArgb(120, 70, 90, 120), [single]([Math]::Max(1, $s * 0.01)))), ($cx - $sq), ($cy - $sq), (2 * $sq), (2 * $sq))
        }
        $g.FillRectangle((New-Object Drawing.SolidBrush($navy)), ($cx - $h), ($cy - $h), (2 * $h), (2 * $h))
    }

    # Thin silver rim
    if ($s -ge 32) {
        $rim = New-Object Drawing.Pen([Drawing.Color]::FromArgb(110, 200, 215, 235), [single]([Math]::Max(1, $s * 0.012)))
        $g.DrawPath($rim, $rect)
    }
    $g.Dispose(); return $bmp
}

function Get-DibBytes([Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $data = $bmp.LockBits((New-Object Drawing.Rectangle(0, 0, $s, $s)), 'ReadOnly', [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $raw = New-Object byte[] ($data.Stride * $s)
    [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $raw, 0, $raw.Length); $bmp.UnlockBits($data)
    $ms = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($ms)
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $w.Write([int]40); $w.Write([int]$s); $w.Write([int]($s * 2)); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]0); $w.Write([int]($s * $s * 4 + $maskRow * $s)); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0); $w.Write([int]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($raw, $y * $data.Stride, $s * 4) }   # bottom-up BGRA
    $w.Write((New-Object byte[] ($maskRow * $s)))                                     # AND mask (alpha is used)
    $w.Flush(); return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = @()
$preview = New-Object Drawing.Bitmap(560, 270)
$pg = [Drawing.Graphics]::FromImage($preview); $pg.Clear([Drawing.Color]::White)
$px = 6
foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    if ($s -eq 256) { $ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $images += , $ms.ToArray() }
    else { $images += , [byte[]](Get-DibBytes $bmp) }
    if ($s -eq 256) { $bmp.Save((Join-Path $root 'tools\polish-icon-preview.png'), [Drawing.Imaging.ImageFormat]::Png); $pg.DrawImage($bmp, 296, 6) }
    else { $pg.DrawImage($bmp, $px, 6); $px += $s + 8 }
    $bmp.Dispose()
}
$pg.Dispose(); $preview.Save((Join-Path $root 'tools\icon-sizes-preview.png'), [Drawing.Imaging.ImageFormat]::Png); $preview.Dispose()
$out = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter($out)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0); $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$images[$i].Length); $w.Write([int]$offset); $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write([byte[]]$img) }
$w.Flush(); [IO.File]::WriteAllBytes((Join-Path $root 'polish.ico'), $out.ToArray())
Write-Host "Wrote polish.ico ($($out.Length) bytes, sizes $($sizes -join ', '))"
