param([string]$Output='C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/v4')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function Make-Sheet($Filename,$Items,[int]$Columns,[int]$Width=480) {
 $height=[int]($Width*720/1280);$stride=$height+32;$rows=[int][Math]::Ceiling($Items.Count/[double]$Columns)
 $bitmap=New-Object Drawing.Bitmap ($Width*$Columns),($stride*$rows)
 $g=[Drawing.Graphics]::FromImage($bitmap);$g.Clear([Drawing.Color]::FromArgb(10,22,29));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $font=New-Object Drawing.Font 'Segoe UI',11
 for($i=0;$i -lt $Items.Count;$i++) {
  $x=($i%$Columns)*$Width;$y=[int][Math]::Floor($i/$Columns)*$stride
  $g.DrawString($Items[$i][0],$font,[Drawing.Brushes]::Wheat,$x+10,$y+7)
  $img=[Drawing.Image]::FromFile((Join-Path $Output $Items[$i][1]));$g.DrawImage($img,[int]$x,[int]($y+32),$Width,$height);$img.Dispose()
 }
 $bitmap.Save((Join-Path $Output $Filename),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bitmap.Dispose();$font.Dispose()
}
foreach($kind in @('dive','advance')) {
 $items=@();for($i=0;$i -lt 7;$i++){$items+=,@(('{0:F2} s' -f ($i*.25)),('{0}-{1:00}.png' -f $kind,$i))}
 Make-Sheet ($kind+'-strip.png') $items 7 480
}
$items=@();foreach($id in @('moss','hollow','carved','frost','joinery','crumble','altar')){foreach($yaw in @(0,120,240)){$items+=,@(($id+' / yaw '+$yaw),('body-{0}-yaw{1:000}.png' -f $id,$yaw))}}
Make-Sheet 'seven-planet-turnaround.png' $items 3 480
$reference=[Drawing.Image]::FromFile('C:/unityjam/shared-assets/seven-steps/concepts/world-2026-11-10/sheets/planets.png')
$comparison=New-Object Drawing.Bitmap 1080,2744
$g=[Drawing.Graphics]::FromImage($comparison);$g.Clear([Drawing.Color]::FromArgb(10,22,29));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=New-Object Drawing.Font 'Segoe UI',12
$ids=@('moss','hollow','carved','frost','joinery','crumble','altar')
for($row=0;$row -lt 7;$row++) {
 for($col=0;$col -lt 3;$col++) {
  $label=@('v3 body','v4 stone body','approved reference')[$col]
  $g.DrawString(($ids[$row]+' / '+$label),$font,[Drawing.Brushes]::Wheat,$col*360+9,$row*392+8)
  $dst=New-Object Drawing.Rectangle ($col*360),($row*392+32),360,360
  if($col -eq 2){$refX=@(50,320,600,870,1140,1390,1640)[$row];$src=New-Object Drawing.Rectangle $refX,35,280,280;$g.DrawImage($reference,$dst,$src,[Drawing.GraphicsUnit]::Pixel)}
  else {$path=if($col -eq 0){'../v3/body-'+$ids[$row]+'-yaw000.png'}else{'body-'+$ids[$row]+'-yaw000.png'};$img=[Drawing.Image]::FromFile((Join-Path $Output $path));$src=New-Object Drawing.Rectangle 280,0,720,720;$g.DrawImage($img,$dst,$src,[Drawing.GraphicsUnit]::Pixel);$img.Dispose()}
 }
}
$comparison.Save((Join-Path $Output 'v3-v4-reference.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$font.Dispose();$comparison.Dispose();$reference.Dispose()
$main=@('map-ko-default','map-ko-focused','map-ko-mid-game','surface-moss-default')
$cards=($main|ForEach-Object {"<figure><a href='$_.png'><img loading='lazy' src='$_.png'></a><figcaption>$_</figcaption></figure>"}) -join "`n"
$flows=(@('dive','advance')|ForEach-Object {
 $kind=$_;$links=(0..6|ForEach-Object {$f=('{0}-{1:00}.png' -f $kind,$_);"<a href='$f'>$($_*.25)s</a>"}) -join ' | '
 "<h2>$kind / 1.5 seconds</h2><a href='$kind-strip.png'><img src='$kind-strip.png'></a><p>$links</p>"
}) -join "`n"
$html="<!doctype html><html lang='en'><meta charset='utf-8'><title>Seven Steps / stone worlds v4</title><style>body{background:#0a161d;color:#ddd7c7;font:16px system-ui;margin:28px}a{color:#f5daa1}h1{font-size:26px}h2{font-size:20px;margin-top:38px}img{display:block;width:100%}section{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}figcaption{padding:8px;color:#b4c4ca}header{max-width:1280px}p{line-height:1.6}</style><h1>Seven Steps / planets and stone worlds / v4</h1><p><a href='README.md'>Review notes and input map</a> | <a href='performance.csv'>Scene performance</a> | <a href='planet-bodies.csv'>Body triangle counts</a></p><header><a href='v3-v4-reference.png'><img src='v3-v4-reference.png'></a></header>$flows<h2>Map and surface places</h2><section>$cards</section><h2>Seven planet bodies / 0, 120, 240 degrees</h2><a href='seven-planet-turnaround.png'><img src='seven-planet-turnaround.png'></a></html>"
[IO.File]::WriteAllText((Join-Path $Output 'index.html'),$html,[Text.UTF8Encoding]::new($false))
Write-Output ('Packaged v4 review: '+$Output)
