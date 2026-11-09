param([string]$Root='C:/unityjam/shared-assets/seven-steps/concepts/themes/ruins-henge-planets/distinct-2026-11-10')
Add-Type -AssemblyName System.Drawing
$ids=@('moss','hollow','carved','frost','joinery','crumble','altar')
$sheet=New-Object System.Drawing.Bitmap(3136,392)
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(12,18,29))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=New-Object System.Drawing.Font('Arial',20)
for($i=0;$i -lt 7;$i++) {
 $im=[System.Drawing.Image]::FromFile((Join-Path $Root ($ids[$i]+'.png')))
 $g.DrawImage($im,(448*$i),0,448,336)
 $g.DrawString(($i+1).ToString()+'  '+$ids[$i],$font,[System.Drawing.Brushes]::White,(448*$i+16),348)
 $im.Dispose()
}
$sheet.Save((Join-Path $Root 'contact-sheet.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$sheet.Dispose()
