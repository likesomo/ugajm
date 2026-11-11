param([string]$Output='C:/unityjam/shared-assets/seven-steps/concepts/ui/board-readability-2026-11-11')
Add-Type -AssemblyName System.Drawing
function Sheet($target,$files,$columns,$width,$height,$labels) {
 $rows=[int][Math]::Ceiling($files.Count/$columns);$canvas=New-Object Drawing.Bitmap ($columns*$width),($rows*($height+32));$g=[Drawing.Graphics]::FromImage($canvas);$g.Clear([Drawing.Color]::FromArgb(13,24,29));$font=New-Object Drawing.Font 'Segoe UI',14
 for($i=0;$i -lt $files.Count;$i++){$image=[Drawing.Image]::FromFile((Join-Path $Output $files[$i]));$x=($i%$columns)*$width;$y=[int][Math]::Floor($i/$columns)*($height+32);$g.DrawString($labels[$i],$font,[Drawing.Brushes]::Wheat,$x+12,$y+4);$g.DrawImage($image,$x,($y+32),$width,$height);$image.Dispose()}
 $canvas.Save((Join-Path $Output $target),[Drawing.Imaging.ImageFormat]::Png);$font.Dispose();$g.Dispose();$canvas.Dispose()
}
foreach($id in @('moss','hollow','carved','frost','joinery','crumble','altar')) {Sheet "part1-before-after-stage03-$id.png" @("part1-before-stage03-$id.png","part1-after-stage03-$id.png") 2 1280 720 @("Before - stage 3 / $id","After - stage 3 / $id")}
foreach($planet in @('moss','frost')) {
 $files=@();$labels=@();foreach($concept in @('A','B','C')){foreach($step in @(0,3,6,7)){$files+="part2-$concept-$planet-$step-steps.png";$labels+="$concept / $planet / $step steps"}}
 Sheet "part2-sheet-$planet.png" $files 4 640 360 $labels
}
Copy-Item (Join-Path $Output 'part2-sheet-moss.png') (Join-Path $Output 'part2-sheet.png') -Force
