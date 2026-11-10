param([string]$Root='C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2')
Add-Type -AssemblyName System.Drawing
$bsaFont=New-Object Drawing.Font('Arial',18)
function Draw-Panel($Graphics,[string]$Path,[int]$X,[int]$Y,[int]$W,[int]$H,[string]$Label) {
    $bsaImage=[Drawing.Image]::FromFile($Path)
    try {
        $bsaScale=[Math]::Min($W/$bsaImage.Width,$H/$bsaImage.Height)
        $bsaW=[int]($bsaImage.Width*$bsaScale);$bsaH=[int]($bsaImage.Height*$bsaScale)
        $Graphics.DrawImage($bsaImage,$X+[int](($W-$bsaW)/2),$Y+36+[int](($H-$bsaH)/2),$bsaW,$bsaH)
        $Graphics.DrawString($Label,$bsaFont,[Drawing.Brushes]::White,$X+12,$Y+5)
    } finally {$bsaImage.Dispose()}
}
$bsaOutput=Join-Path $Root unity
$bsaCompare=New-Object Drawing.Bitmap(3840,756)
$bsaGraphics=[Drawing.Graphics]::FromImage($bsaCompare)
$bsaGraphics.Clear([Drawing.Color]::FromArgb(7,14,28))
Draw-Panel $bsaGraphics "$Root/A/hero.png" 0 0 1280 720 'R2 A concept / illustration'
Draw-Panel $bsaGraphics "$bsaOutput/moss-8x6-ortho.png" 1280 0 1280 720 'Actual Unity / orthographic pitch 50 / default unchanged'
Draw-Panel $bsaGraphics "$bsaOutput/moss-8x6-proposed.png" 2560 0 1280 720 'Actual Unity / proposed orthographic framing / review only'
$bsaCompare.Save("$bsaOutput/hero-unity-default-proposed.png")
$bsaGraphics.Dispose();$bsaCompare.Dispose()
$bsaSheet=New-Object Drawing.Bitmap(1792,580)
$bsaGraphics=[Drawing.Graphics]::FromImage($bsaSheet)
$bsaGraphics.Clear([Drawing.Color]::FromArgb(7,14,28))
$bsaIds=@('moss','hollow','carved','frost','joinery','crumble','altar')
for($i=0;$i -lt $bsaIds.Length;$i++) {
    $bsaId=$bsaIds[$i];Draw-Panel $bsaGraphics "$bsaOutput/$bsaId-8x6-proposed.png" (($i%4)*448) ([Math]::Floor($i/4)*290) 448 252 $bsaId
}
$bsaSheet.Save("$bsaOutput/seven-planets-unity.png")
$bsaGraphics.Dispose();$bsaSheet.Dispose();$bsaFont.Dispose()
