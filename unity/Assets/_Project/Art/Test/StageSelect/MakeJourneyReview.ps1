param([string]$Output='C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11/v3')
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
foreach($kind in @('dive','advance','complete')) {
 $items=@();for($i=0;$i -lt 7;$i++){$items+=,@(('{0:F2} s' -f ($i*.25)),('{0}-{1:00}.png' -f $kind,$i))}
 Make-Sheet ($kind+'-strip.png') $items 7 480
}
$items=@();foreach($id in @('moss','hollow','carved','frost','joinery','crumble','altar')){foreach($yaw in @(0,120,240)){$items+=,@(($id+' / yaw '+$yaw),('body-{0}-yaw{1:000}.png' -f $id,$yaw))}}
Make-Sheet 'seven-planet-turnaround.png' $items 3 480
$items=@(
 @('v2 / board worlds','../v2/A-ko-map-default.png'),@('v3 / whole planet bodies','map-ko-default.png'),
 @('v2 / moss satellite menu','../v2/B-ko-moss-stages-cleared-mix.png'),@('v3 / moss places in the ground','surface-moss-cleared-mix.png'),
 @('v2 / altar satellite menu','../v2/B-ko-altar-stages-cleared-mix.png'),@('v3 / altar places in the ground','surface-altar-cleared-mix.png')
)
Make-Sheet 'v2-v3-before-after.png' $items 2 640
$main=@('map-ko-default','map-ko-focused','map-ko-mid-game','map-ko-all-complete','map-en-focused','surface-moss-default','surface-moss-focused','surface-moss-cleared-mix','surface-altar-default','surface-altar-focused','surface-altar-cleared-mix')
$cards=($main|ForEach-Object {"<figure><a href='$_.png'><img loading='lazy' src='$_.png'></a><figcaption>$_</figcaption></figure>"}) -join "`n"
$flows=(@('dive','advance','complete')|ForEach-Object {
 $kind=$_;$links=(0..6|ForEach-Object {$f=('{0}-{1:00}.png' -f $kind,$_);"<a href='$f'>$($_*.25)s</a>"}) -join ' | '
 "<h2>$kind / 1.5 seconds</h2><a href='$kind-strip.png'><img src='$kind-strip.png'></a><p>$links</p>"
}) -join "`n"
$html="<!doctype html><html lang='en'><meta charset='utf-8'><title>Seven Steps / journey v3</title><style>body{background:#0a161d;color:#ddd7c7;font:16px system-ui;margin:28px}a{color:#f5daa1}h1{font-size:26px}h2{font-size:20px;margin-top:38px}img{display:block;width:100%}section{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}figcaption{padding:8px;color:#b4c4ca}header{max-width:1280px}p{line-height:1.6}</style><h1>Seven Steps / planets and continuous journey / v3</h1><p><a href='README.md'>Review notes and input map</a> | <a href='performance.csv'>Scene performance</a> | <a href='planet-bodies.csv'>Body triangle counts</a></p><header><a href='v2-v3-before-after.png'><img src='v2-v3-before-after.png'></a></header>$flows<h2>Map and surface places</h2><section>$cards</section><h2>Seven planet bodies / 0, 120, 240 degrees</h2><a href='seven-planet-turnaround.png'><img src='seven-planet-turnaround.png'></a></html>"
[IO.File]::WriteAllText((Join-Path $Output 'index.html'),$html,[Text.UTF8Encoding]::new($false))
Write-Output ('Packaged v3 review: '+$Output)
