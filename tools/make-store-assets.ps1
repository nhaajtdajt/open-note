param([string]$Root = "$PSScriptRoot\..\packaging")
# MSIX logos (packaging\msix\Images) and the Store listing logo (packaging\store), drawn from the
# same logo as the app icon.
. "$PSScriptRoot\NoteIcon.ps1"

function Save-Logo([string]$dir, [int]$width, [int]$height, [int]$logoSize, [string]$name) {
    New-Item -ItemType Directory -Force $dir | Out-Null
    $canvas = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::Transparent)
    $logo = New-NoteBitmap $logoSize
    $g.DrawImage($logo, [int](($width - $logoSize) / 2), [int](($height - $logoSize) / 2), $logoSize, $logoSize)
    $g.Dispose(); $logo.Dispose()
    $canvas.Save((Join-Path $dir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
    "  $name ($width x $height)"
}

$images = Join-Path $Root "msix\Images"
Save-Logo $images 50 50 50 "StoreLogo.png"
Save-Logo $images 44 44 44 "Square44x44Logo.png"
# Unplated taskbar / Start icons, so Windows doesn't put the logo on a coloured square.
Save-Logo $images 24 24 24 "Square44x44Logo.targetsize-24_altform-unplated.png"
Save-Logo $images 44 44 44 "Square44x44Logo.targetsize-44_altform-unplated.png"
Save-Logo $images 256 256 256 "Square44x44Logo.targetsize-256_altform-unplated.png"
Save-Logo $images 71 71 56 "Square71x71Logo.png"
Save-Logo $images 150 150 104 "Square150x150Logo.png"
Save-Logo $images 310 150 104 "Wide310x150Logo.png"

Save-Logo (Join-Path $Root "store") 300 300 300 "Logo300.png"
