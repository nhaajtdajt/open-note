# The Open Note logo (a sticky note with an adhesive strip and a folded corner), drawn at any size.
# Dot-source it: . "$PSScriptRoot\NoteIcon.ps1"
Add-Type -AssemblyName System.Drawing

function New-NoteBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $m = [Math]::Max(1, [int]($s * 0.08))
    $w = $s - 2 * $m
    $fold = [int]($w * 0.28)
    $x0 = $m; $y0 = $m; $x1 = $m + $w; $y1 = $m + $w

    # note body with folded bottom-right corner
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @(
        (New-Object System.Drawing.PointF $x0, $y0),
        (New-Object System.Drawing.PointF $x1, $y0),
        (New-Object System.Drawing.PointF $x1, ($y1 - $fold)),
        (New-Object System.Drawing.PointF ($x1 - $fold), $y1),
        (New-Object System.Drawing.PointF $x0, $y1)
    )
    $path.AddPolygon($pts)
    $body = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 214, 74))
    $g.FillPath($body, $path)

    # adhesive strip
    $strip = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 240, 180, 30))
    $g.FillRectangle($strip, $x0, $y0, $w, [Math]::Max(2, [int]($w * 0.2)))

    # fold
    $foldPts = @(
        (New-Object System.Drawing.PointF $x1, ($y1 - $fold)),
        (New-Object System.Drawing.PointF ($x1 - $fold), ($y1 - $fold)),
        (New-Object System.Drawing.PointF ($x1 - $fold), $y1)
    )
    $foldBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 214, 160, 20))
    $g.FillPolygon($foldBrush, $foldPts)

    # text lines
    if ($s -ge 20) {
        $lineBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 120, 80, 0))
        $lh = [Math]::Max(1, [int]($s * 0.06))
        $lx = $x0 + [int]($w * 0.16)
        foreach ($i in 0..2) {
            $ly = $y0 + [int]($w * (0.36 + $i * 0.17))
            $lw = if ($i -eq 2) { [int]($w * 0.38) } else { [int]($w * 0.62) }
            $g.FillRectangle($lineBrush, $lx, $ly, $lw, $lh)
        }
    }
    $g.Dispose()
    return $bmp
}
