# Regenerates Assets\tray-white.ico and Assets\tray-black.ico (cake-slice outline).
# Stroke is ~6% of the icon size to match thin Windows system tray icons.
# Usage: powershell -File tools\MakeTrayIcons.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$sizes = 16, 20, 24, 28, 32, 40, 48

function New-Frame([int]$size, [Drawing.Color]$color) {
    $ss = 8
    $px = $size * $ss
    $big = New-Object Drawing.Bitmap $px, $px, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = 'AntiAlias'
    $k = $px / 256.0
    $stroke = [Math]::Max(1.0, $size * 0.0625) * $ss
    $pen = New-Object Drawing.Pen $color, $stroke
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Round'; $pen.EndCap = 'Round'

    function P($x, $y) { New-Object Drawing.PointF ([single]($x * $k)), ([single]($y * $k)) }

    # Slice outline, drawn along the stroke centre line (256-unit grid).
    $outline = [Drawing.PointF[]]@(
        (P 36 232), (P 24 220), (P 24 134), (P 68 100), (P 118 52), (P 138 30), (P 150 24), (P 162 30),
        (P 226 98), (P 232 112), (P 232 220), (P 220 232), (P 36 232))
    $g.DrawLines($pen, $outline)
    # Cherry ring.
    $r = 25 * $k
    $g.DrawEllipse($pen, (88 * $k - $r), (74 * $k - $r), (2 * $r), (2 * $r))
    # Cake layers.
    $g.DrawLine($pen, (P 24 140), (P 188 140))
    $g.DrawLine($pen, (P 24 188), (P 188 188))
    $g.Dispose()

    $small = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [Drawing.Graphics]::FromImage($small)
    $g2.InterpolationMode = 'HighQualityBicubic'; $g2.PixelOffsetMode = 'HighQuality'
    $g2.DrawImage($big, 0, 0, $size, $size)
    $g2.Dispose(); $big.Dispose()
    return $small
}

function Save-Ico([string]$path, [Drawing.Color]$color) {
    $frames = foreach ($s in $sizes) {
        $bmp = New-Frame $s $color
        $ms = New-Object IO.MemoryStream
        $bw = New-Object IO.BinaryWriter $ms
        # BITMAPINFOHEADER + bottom-up BGRA pixels + 1bpp AND mask (all zero).
        $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
        $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
        for ($y = $s - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $s; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
        $bw.Write((New-Object byte[] ($maskRow * $s)))
        $bw.Flush()
        $bmp.Dispose()
        [pscustomobject]@{ Size = $s; Data = $ms.ToArray() }
    }
    $out = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $out
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($f in $frames) {
        $w.Write([byte]$f.Size); $w.Write([byte]$f.Size); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$f.Data.Length); $w.Write([int]$offset)
        $offset += $f.Data.Length
    }
    foreach ($f in $frames) { $w.Write($f.Data) }
    $w.Flush()
    [IO.File]::WriteAllBytes($path, $out.ToArray())
}

Save-Ico (Join-Path $root 'Assets\tray-white.ico') ([Drawing.Color]::White)
Save-Ico (Join-Path $root 'Assets\tray-black.ico') ([Drawing.Color]::Black)
Write-Host 'Done: tray icons regenerated.'
