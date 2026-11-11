# Native-pixel crops from 1280x720 headless Play Mode screenshots; never rescale the count proof.
Add-Type -AssemblyName System.Drawing
$output = 'C:/unityjam/shared-assets/seven-steps/concepts/ui/props-2026-11-12'
$font = New-Object Drawing.Font('Arial', 14)
$large = New-Object Drawing.Font('Arial', 20)
$ink = [Drawing.Brushes]::Black
foreach ($stage in 15,24,38) {
    $id = '{0:D2}' -f $stage
    $before = [Drawing.Image]::FromFile("$output/readability-before-stage-$id.png")
    $after = [Drawing.Image]::FromFile("$output/readability-after-stage-$id.png")
    $sheet = New-Object Drawing.Bitmap(2560, 768)
    $g = [Drawing.Graphics]::FromImage($sheet)
    $g.Clear([Drawing.Color]::FromArgb(232,231,221))
    $g.DrawString("Stage $id / 1280 x 720 / previous physical markers", $large, $ink, 16, 8)
    $g.DrawString('Larger grouped pips + brighter glyphs + taller exit beacon', $large, $ink, 1296, 8)
    $g.DrawImageUnscaled($before, 0, 48)
    $g.DrawImageUnscaled($after, 1280, 48)
    $sheet.Save("$output/before-after-stage-$id.png", [Drawing.Imaging.ImageFormat]::Png)
    $sheet.Save("$output/readability-before-after-stage-$id.png", [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $sheet.Dispose(); $before.Dispose(); $after.Dispose()
}
$sheet = New-Object Drawing.Bitmap(760, 1060)
$g = [Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::FromArgb(232,231,221))
$g.DrawString('1:1 pixel crops / production stage 15 camera / 1280 x 720', $font, $ink, 12, 8)
$labels = @('Gate: dim/off','Gate: lit/on','Sockets: empty','Sockets: filled')
for ($group=0; $group -lt 4; $group++) { $g.DrawString($labels[$group], $font, $ink, 80+$group*170, 38) }
for ($n=1; $n -le 7; $n++) {
    $y = 70+($n-1)*140
    $g.DrawString("N=$n", $font, $ink, 12, $y+44)
    for ($group=0; $group -lt 4; $group++) {
        $index = '{0:D2}' -f ($group*7+$n)
        $image = [Drawing.Image]::FromFile("$output/counter-proof-stage-$index.png")
        $crop = New-Object Drawing.Rectangle(476,292,96,112)
        $target = New-Object Drawing.Rectangle((80+$group*170),$y,96,112)
        $part=([Drawing.Bitmap]$image).Clone($crop,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g.DrawImageUnscaled($part,$target.X,$target.Y);$part.Dispose()
        $image.Dispose()
    }
}
$sheet.Save("$output/counts-1to7-native-1280x720.png", [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $sheet.Dispose(); $font.Dispose(); $large.Dispose()
