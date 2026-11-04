# Regenerates Assets\tray-white.ico and Assets\tray-black.ico (cake-slice outline).
# Drawn directly at each size on the pixel grid with whole-pixel strokes, like Windows system tray icons
# (crisp 1 px lines; supersampling made them blurry and grey).
# Usage: powershell -File tools\MakeTrayIcons.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$sizes = 16, 20, 24, 28, 32, 40, 48

function New-Frame([int]$size, [Drawing.Color]$color) {
    $bmp = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'Half'
    $k = $size / 16.0
    $width = [Math]::Max(1, [Math]::Floor($size / 16.0 + 0.1))
    $pen = New-Object Drawing.Pen $color, $width
    $pen.LineJoin = 'Round'; $pen.StartCap = 'Flat'; $pen.EndCap = 'Flat'

    # Odd stroke widths sit on pixel centres, even ones on pixel edges, so lines stay crisp.
    function Snap($v) {
        $s = $v * $k
        if ($width % 2 -eq 1) { [Math]::Floor($s) + 0.5 } else { [Math]::Round($s) }
    }
    function P($x, $y) { New-Object Drawing.PointF ([single](Snap $x)), ([single](Snap $y)) }

    # Design on a 16 x 16 grid (stroke centre line).
    # Slopes are 45 degrees so they stay clean on the pixel grid.
    $outline = [Drawing.PointF[]]@(
        (P 14.5 14.5), (P 1.5 14.5), (P 1.5 8.5), (P 9.5 1.5), (P 14.5 6.5), (P 14.5 14.5))
    $g.DrawLines($pen, $outline)
    # Cherry: a solid square dot sitting on the left slope.
    $brush = New-Object Drawing.SolidBrush $color
    $g.SmoothingMode = 'None'
    $x0 = [Math]::Round(4 * $k); $y0 = [Math]::Round(4 * $k); $d = [Math]::Max(2, [Math]::Round(2 * $k))
    $g.FillRectangle($brush, $x0, $y0, $d, $d)
    $g.SmoothingMode = 'AntiAlias'
    # Cake layers.
    $g.DrawLine($pen, (P 1.5 10.5), (P 11.5 10.5))
    $g.DrawLine($pen, (P 1.5 12.5), (P 11.5 12.5))
    $g.Dispose()
    return $bmp
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
