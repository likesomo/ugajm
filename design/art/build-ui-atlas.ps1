param([string]$Output='C:/unityjam/shared-assets/seven-steps/Assets/UI/RuinsHenge')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Output | Out-Null
$b=New-Object Drawing.Bitmap 1024,1024
$g=[Drawing.Graphics]::FromImage($b)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::Transparent)
$script:items=@()
function C($hex){[Drawing.ColorTranslator]::FromHtml("#$hex")}
function Brush($hex){New-Object Drawing.SolidBrush (C $hex)}
function Pen($hex,$width=3){New-Object Drawing.Pen (C $hex),$width}
function Box($name,$x,$y,$w,$h,$bg,$edge,$border=0){
 $f=Brush $bg;$p=Pen $edge 3;$g.FillRectangle($f,$x+3,$y+3,$w-6,$h-6);$g.DrawRectangle($p,$x+3,$y+3,$w-7,$h-7)
 $p2=Pen $edge 1;$g.DrawRectangle($p2,$x+10,$y+10,$w-21,$h-21);$f.Dispose();$p.Dispose();$p2.Dispose()
 $script:items+=@{name=$name;x=$x;y=1024-$y-$h;width=$w;height=$h;border=$border}
}
Box 'button_default' 0 0 512 80 '263B48' '9A8B67' 20
Box 'button_hover' 0 88 512 80 '34515A' 'E5C684' 20
Box 'button_pressed' 0 176 512 80 '172C37' 'BCA06B' 20
Box 'button_disabled' 0 264 512 80 '29313B' '56616B' 20
Box 'panel_stone' 0 368 256 256 '142238' '84969B' 32
Box 'panel_wood' 272 368 256 256 '172539' 'AD9367' 32
# Fine outside-only seam/moss marks: the central fill remains plain and stretchable.
$m=Pen '748D57' 3;foreach($x in 18,74,172,232){$g.DrawLine($m,$x,374,$x+8,379)};$m.Dispose()
$w=Pen '6E5B41' 2;foreach($y in 392,600){$g.DrawLine($w,287,$y,510,$y)};$w.Dispose()
function Icon($name,$x,$y,$kind,$color='EEE6D4'){
 $p=Pen $color 4;$p.LineJoin=[Drawing.Drawing2D.LineJoin]::Round;$f=Brush $color
 switch($kind){
  'star' { $pts=@();for($i=0;$i -lt 10;$i++){ $a=($i*36-90)*[Math]::PI/180;$r=if($i%2 -eq 0){24}else{11};$pts+=New-Object Drawing.PointF ([single]($x+32+[Math]::Cos($a)*$r)),([single]($y+32+[Math]::Sin($a)*$r)) };$g.FillPolygon($f,[Drawing.PointF[]]$pts) }
  'empty' { $pts=@();for($i=0;$i -lt 10;$i++){ $a=($i*36-90)*[Math]::PI/180;$r=if($i%2 -eq 0){24}else{11};$pts+=New-Object Drawing.PointF ([single]($x+32+[Math]::Cos($a)*$r)),([single]($y+32+[Math]::Sin($a)*$r)) };$g.DrawPolygon($p,[Drawing.PointF[]]$pts) }
  'undo' { $g.DrawArc($p,$x+14,$y+15,34,34,210,280);$g.DrawLines($p,[Drawing.PointF[]]@((New-Object Drawing.PointF ($x+10),($y+13)),(New-Object Drawing.PointF ($x+12),($y+30)),(New-Object Drawing.PointF ($x+29),($y+27)))) }
  'restart' { $g.DrawArc($p,$x+11,$y+11,42,42,40,285);$g.DrawLines($p,[Drawing.PointF[]]@((New-Object Drawing.PointF ($x+40),($y+8)),(New-Object Drawing.PointF ($x+53),($y+14)),(New-Object Drawing.PointF ($x+47),($y+27)))) }
  'hint' { $g.DrawEllipse($p,$x+18,$y+8,28,32);$g.DrawLine($p,$x+24,$y+45,$x+40,$y+45);$g.DrawLine($p,$x+27,$y+53,$x+37,$y+53) }
  'settings' { $g.DrawEllipse($p,$x+17,$y+17,30,30);$g.DrawEllipse($p,$x+26,$y+26,12,12);for($i=0;$i -lt 8;$i++){$a=$i*[Math]::PI/4;$g.DrawLine($p,[single]($x+32+16*[Math]::Cos($a)),[single]($y+32+16*[Math]::Sin($a)),[single]($x+32+23*[Math]::Cos($a)),[single]($y+32+23*[Math]::Sin($a)))} }
  'sound' { $g.DrawPolygon($p,[Drawing.PointF[]]@((New-Object Drawing.PointF ($x+10),($y+25)),(New-Object Drawing.PointF ($x+21),($y+25)),(New-Object Drawing.PointF ($x+32),($y+15)),(New-Object Drawing.PointF ($x+32),($y+49)),(New-Object Drawing.PointF ($x+21),($y+39)),(New-Object Drawing.PointF ($x+10),($y+39))));$g.DrawArc($p,$x+26,$y+17,25,30,-65,130) }
  'body' { $g.FillEllipse($f,$x+22,$y+8,20,20);$g.FillPolygon($f,[Drawing.PointF[]]@((New-Object Drawing.PointF ($x+24),($y+27)),(New-Object Drawing.PointF ($x+40),($y+27)),(New-Object Drawing.PointF ($x+50),($y+54)),(New-Object Drawing.PointF ($x+14),($y+54)))) }
 }
 $p.Dispose();$f.Dispose();$script:items+=@{name=$name;x=$x;y=1024-$y-64;width=64;height=64;border=0}
}
$icons=@('undo','restart','hint','settings','sound','empty','star')
for($i=0;$i -lt $icons.Count;$i++){Icon $icons[$i] (552+($i%5)*88) (24+[Math]::Floor($i/5)*88) $icons[$i] $(if($icons[$i] -eq 'star'){'C9AB6B'}else{'EEE6D4'})}
Icon 'body_a' 552 220 'body' '568C88';Icon 'body_b' 640 220 'body' 'B99A57';Icon 'body_c' 728 220 'body' 'B96C59';Icon 'body_stone' 816 220 'body' 'B9BFC0'
$b.Save((Join-Path $Output 'RH_UI_Atlas.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$b.Dispose()
$items | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $Output 'RH_UI_Atlas.slices.json') -Encoding UTF8
Write-Output "Created atlas and $($items.Count) deterministic sprite slices in $Output"
