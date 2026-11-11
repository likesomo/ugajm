$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$output='C:/unityjam/shared-assets/seven-steps/concepts/themes/planet-dressing-2026-11-12'
$ids=@('moss','hollow','carved','frost','joinery','crumble','altar')
$stages=@(1,8,15,22,29,36,43)
$background=[Drawing.Color]::FromArgb(13,22,34)
$font=[Drawing.Font]::new('Segoe UI',22,[Drawing.FontStyle]::Bold)
$small=[Drawing.Font]::new('Segoe UI',15)
$white=[Drawing.Brushes]::White
for($i=0;$i -lt 7;$i++) {
    $id=$ids[$i]
    $bmp=[Drawing.Bitmap]::new(2560,790)
    $g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear($background)
    $g.DrawString("$id | Stage $($stages[$i]) | BEFORE",$font,$white,24,16)
    $g.DrawString('AFTER | themed Blender objects and surface',$font,$white,1304,16)
    foreach($side in 0,1) {
        $prefix=if($side -eq 0){'before'}else{'after'}
        $img=[Drawing.Image]::FromFile("$output/$prefix-$id.png")
        $g.DrawImage($img,$side*1280,70,1280,720)
        $img.Dispose()
    }
    $bmp.Save("$output/before-after-$id.png",[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$bmp.Dispose()
}
$sheet=[Drawing.Bitmap]::new(2560,1100)
$g=[Drawing.Graphics]::FromImage($sheet);$g.Clear($background)
$g.DrawString('SEVEN PLANETS | Blender dressing and textured surfaces | actual Unity Play Mode',$font,$white,24,18)
for($i=0;$i -lt 7;$i++) {
    $x=($i%4)*640;$y=80+[math]::Floor($i/4)*490
    $img=[Drawing.Image]::FromFile("$output/after-$($ids[$i]).png")
    $g.DrawImage($img,$x,$y,640,360);$img.Dispose()
    $g.DrawString("$($ids[$i].ToUpper())  /  STAGE $($stages[$i])",$font,$white,$x+16,$y+370)
    $row=Import-Csv "$output/perf.csv" | Where-Object { $_.planet -eq $ids[$i] -and $_.before -eq 'False' }
    $contrast=([double]$row.contrast*100).ToString('F1')
    $g.DrawString("$($row.stageTris) tris | $($row.materials) materials | contrast $contrast%",$small,$white,$x+16,$y+413)
}
$g.DrawString("35 FREE PROCEDURAL BLENDER MODULES`n512 px color + packed surface maps`n5 modules per planet, one surface material`nCamera: ortho / pitch 50 / yaw 0",$font,$white,1950,650)
$sheet.Save("$output/seven-planets-sheet.png",[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose();$small.Dispose()
Write-Output 'Seven comparison pairs and seven-planets-sheet.png saved.'
