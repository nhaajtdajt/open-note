# The Open Note logo: a yellow sticky note with a curled bottom-left corner and "love yourself"
# in red across the middle. Drawn at any size (at 4x, then scaled down, so edges stay clean).
# Dot-source it: . "$PSScriptRoot\NoteIcon.ps1"
Add-Type -AssemblyName System.Drawing

function New-NoteBitmap([int]$size) {
    $ss = if ($size -ge 128) { 3 } else { 6 }          # supersampling factor
    $S = $size * $ss
    $big = New-Object System.Drawing.Bitmap $S, $S, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    function C($a, $r, $gr, $b) { [System.Drawing.Color]::FromArgb($a, $r, $gr, $b) }

    # geometry: a square note with its bottom-left corner curled up
    $N = 0.76 * $S; $L = 0.12 * $S; $T = 0.09 * $S; $R = $L + $N; $B = $T + $N
    $c = 0.22 * $N                                          # size of the curled corner

    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $body.AddLine($L, $T, $R, $T); $body.AddLine($R, $T, $R, $B)
    $body.AddLine($R, $B, $L + $c, $B)
    $body.AddBezier($L + $c, $B, $L + $c * 0.45, $B - $c * 0.12, $L + $c * 0.05, $B - $c * 0.45, $L, $B - $c)
    $body.CloseFigure()

    # soft shadow: stacked translucent copies, wider and fainter outwards
    for ($k = 9; $k -ge 1; $k--) {
        $shadow = New-Object System.Drawing.Pen (C 7 40 30 0), ($k * 0.011 * $S)
        $shadow.LineJoin = 'Round'
        $m = New-Object System.Drawing.Drawing2D.Matrix; $m.Translate(0.018 * $S, 0.03 * $S)
        $p = $body.Clone(); $p.Transform($m)
        $g.DrawPath($shadow, $p)
        $shadow.Dispose(); $p.Dispose()
    }

    # paper: darker along the top edge, lighter in the middle
    $paper = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.PointF]::new(0, $T), [System.Drawing.PointF]::new(0, $B), (C 255 226 205 70), (C 255 244 230 120))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend 4
    $blend.Colors = @((C 255 222 200 62), (C 255 240 224 108), (C 255 248 238 150), (C 255 243 230 122))
    $blend.Positions = @(0.0, 0.14, 0.45, 1.0)
    $paper.InterpolationColors = $blend
    $g.FillPath($paper, $body)
    $g.SetClip($body)
    $edge = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.PointF]::new($R - 0.16 * $N, 0), [System.Drawing.PointF]::new($R, 0), (C 0 214 190 60), (C 90 214 186 56))
    $g.FillRectangle($edge, $R - 0.16 * $N, $T, 0.16 * $N, $N)
    $g.ResetClip()

    # the curl: a darker flap folded back over the paper, with a shadow under it
    $flap = New-Object System.Drawing.Drawing2D.GraphicsPath
    $flap.AddBezier($L, $B - $c, $L + $c * 0.02, $B - $c * 1.22, $L + $c * 0.78, $B - $c * 1.02, $L + $c, $B)
    $flap.AddBezier($L + $c, $B, $L + $c * 0.45, $B - $c * 0.12, $L + $c * 0.05, $B - $c * 0.45, $L, $B - $c)
    $flap.CloseFigure()
    for ($k = 5; $k -ge 1; $k--) {
        $fs = New-Object System.Drawing.Pen (C 9 90 60 0), ($k * 0.01 * $S)
        $m = New-Object System.Drawing.Drawing2D.Matrix; $m.Translate(0.012 * $S, -0.008 * $S)
        $p = $flap.Clone(); $p.Transform($m); $g.DrawPath($fs, $p); $fs.Dispose(); $p.Dispose()
    }
    $fb = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.PointF]::new($L, $B - $c), [System.Drawing.PointF]::new($L + $c, $B), (C 255 212 156 40), (C 255 244 208 96))
    $g.FillPath($fb, $flap)
    $fpen = New-Object System.Drawing.Pen (C 120 160 110 20), (0.004 * $S)
    $g.DrawPath($fpen, $flap)

    # "love yourself", red, a little off the straight, in the middle of the note
    if ($size -ge 48) {
        $installed = (New-Object System.Drawing.Text.InstalledFontCollection).Families.Name
        $family = 'Segoe UI'
        foreach ($candidate in 'Ink Free', 'Segoe Print', 'Segoe Script', 'Comic Sans MS') { if ($installed -contains $candidate) { $family = $candidate; break } }
        $fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
        $fontSize = 0.2 * $N
        do {
            $font = New-Object System.Drawing.Font $family, $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $wide = $g.MeasureString('yourself', $font).Width
            if ($wide -le 0.8 * $N) { break }
            $fontSize *= 0.94
        } while ($fontSize -gt 4)
        $cx = ($L + $R) / 2 + 0.01 * $N; $cy = ($T + $B) / 2 - 0.03 * $N
        $g.TranslateTransform($cx, $cy); $g.RotateTransform(-6)
        $lh = $fontSize * 0.98
        foreach ($pass in 0, 1) {
            $ink = if ($pass -eq 0) { C 70 120 30 20 } else { C 255 214 36 43 }
            $brush = New-Object System.Drawing.SolidBrush $ink
            $off = if ($pass -eq 0) { 0.007 * $N } else { 0 }
            $thick = 0.0045 * $N                                  # draw the text a few times, nudged, to make the strokes bolder
            foreach ($dx in 0, $thick) {
                foreach ($dy in 0, $thick) {
                    $g.DrawString('love', $font, $brush, [System.Drawing.PointF]::new($off + $dx, -$lh * 0.46 + $off + $dy), $fmt)
                    $g.DrawString('yourself', $font, $brush, [System.Drawing.PointF]::new($off + $dx, $lh * 0.5 + $off + $dy), $fmt)
                }
            }
            $brush.Dispose()
        }
        $g.ResetTransform()
    } else {
        # too small to read: two short red strokes stand for the writing
        $bar = New-Object System.Drawing.Pen (C 255 214 36 43), (0.075 * $N)
        $bar.StartCap = 'Round'; $bar.EndCap = 'Round'
        $mid = ($T + $B) / 2 - 0.04 * $N
        $g.DrawLine($bar, $L + 0.28 * $N, $mid - 0.07 * $N, $L + 0.62 * $N, $mid - 0.09 * $N)
        $g.DrawLine($bar, $L + 0.24 * $N, $mid + 0.12 * $N, $L + 0.76 * $N, $mid + 0.10 * $N)
    }
    $g.Dispose()

    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g2 = [System.Drawing.Graphics]::FromImage($bmp)
    $g2.InterpolationMode = 'HighQualityBicubic'; $g2.PixelOffsetMode = 'HighQuality'; $g2.CompositingQuality = 'HighQuality'
    $g2.DrawImage($big, 0, 0, $size, $size)
    $g2.Dispose(); $big.Dispose()
    return $bmp
}
