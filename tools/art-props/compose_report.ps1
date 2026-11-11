# Mechanical layouts of unmodified headless Unity screenshots; no generated imagery.
Add-Type -AssemblyName System.Drawing
$output = 'C:/unityjam/shared-assets/seven-steps/concepts/ui/props-2026-11-12'
$font = New-Object Drawing.Font('Arial', 22)
$small = New-Object Drawing.Font('Arial', 16)
$ink = [Drawing.Brushes]::Black
foreach ($stage in 4,5,15,24,29,36,38,41,50) {
    $id = '{0:D2}' -f $stage
    $before = [Drawing.Image]::FromFile("$output/before-stage-$id.png")
    $after = [Drawing.Image]::FromFile("$output/after-stage-$id.png")
    $sheet = New-Object Drawing.Bitmap(2400, 810)
    $g = [Drawing.Graphics]::FromImage($sheet)
    $g.Clear([Drawing.Color]::FromArgb(232,231,221))
    $g.DrawString("Stage $id - BEFORE: UI labels", $font, $ink, 22, 14)
    $g.DrawString("AFTER: Blender geometry", $font, $ink, 1222, 14)
    $g.DrawImage($before, 0, 60, 1200, 750)
    $g.DrawImage($after, 1200, 60, 1200, 750)
    $sheet.Save("$output/before-after-stage-$id.png", [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $sheet.Dispose(); $before.Dispose(); $after.Dispose()
}
function CropPair($name, $beforeFile, $afterFile, $crop) {
    $before = [Drawing.Bitmap]::FromFile("$output/$beforeFile")
    $after = [Drawing.Bitmap]::FromFile("$output/$afterFile")
    $sheet = New-Object Drawing.Bitmap(800, 460)
    $g = [Drawing.Graphics]::FromImage($sheet)
    $g.Clear([Drawing.Color]::FromArgb(232,231,221))
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.DrawString('Before', $small, $ink, 20, 12)
    $g.DrawString('After', $small, $ink, 420, 12)
    $g.DrawImage($before, [Drawing.Rectangle]::new(0,60,400,400), $crop, [Drawing.GraphicsUnit]::Pixel)
    $g.DrawImage($after, [Drawing.Rectangle]::new(400,60,400,400), $crop, [Drawing.GraphicsUnit]::Pixel)
    $sheet.Save("$output/before-after-$name.png", [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $sheet.Dispose(); $before.Dispose(); $after.Dispose()
}
CropPair 'start-marker' 'before-stage-05.png' 'after-stage-05.png' ([Drawing.Rectangle]::new(695,325,120,120))
CropPair 'statue-rim' 'rings-before-stage-05.png' 'after-progress-stage-05.png' ([Drawing.Rectangle]::new(975,300,120,120))
$font.Dispose(); $small.Dispose()
