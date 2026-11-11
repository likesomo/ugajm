param([string]$Output='C:/unityjam/shared-assets/seven-steps/concepts/ui/stage-select-2026-11-11')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$items=@(
    @('Before / actual PlanetMapView','before-ko-current-map.png'),
    @('A / constellation of worlds','A-ko-map-mid-game.png'),
    @('B / orbital atlas','B-ko-map-mid-game.png'),
    @('Before / actual PlanetStagesPanel','before-ko-current-moss-stages.png'),
    @('A / summit footsteps','A-ko-moss-stages-cleared-mix.png'),
    @('B / satellite stepping stones','B-ko-moss-stages-cleared-mix.png')
)
$sheet=New-Object System.Drawing.Bitmap 1920,800
$g=[System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::FromArgb(12,22,30))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=New-Object System.Drawing.Font 'Segoe UI',14
for($i=0;$i -lt $items.Count;$i++) {
    $x=($i%3)*640; $y=[Math]::Floor($i/3)*400
    $g.DrawString($items[$i][0],$font,[System.Drawing.Brushes]::Wheat,$x+18,$y+8)
    $src=[System.Drawing.Image]::FromFile((Join-Path $Output $items[$i][1]))
    $g.DrawImage($src,[int]$x,[int]($y+38),640,360)
    $src.Dispose()
}
$sheet.Save((Join-Path $Output 'before-after-sheet.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$sheet.Dispose();$font.Dispose()
$cards=Get-ChildItem -LiteralPath $Output -Filter '*.png' | Where-Object {$_.Name -ne 'unity-planet-cache.png' -and $_.Name -ne 'before-after-sheet.png'} | Sort-Object Name | ForEach-Object {"<figure><a href='$($_.Name)'><img loading='lazy' src='$($_.Name)'></a><figcaption>$($_.BaseName)</figcaption></figure>"}
$html="<!doctype html><html lang='en'><meta charset='utf-8'><title>Seven Steps / stage select / A &amp; B</title><style>body{background:#0c161e;color:#ded7c6;font:16px system-ui;margin:32px}a{color:#f5daa1}h1{font-size:24px}section{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:24px}figure{margin:0}img{width:100%;display:block}figcaption{padding:10px 0;color:#a8bbc3}header img{max-width:1400px}</style><h1>Seven Steps / stage select / 11 November 2026</h1><p><a href='README.md'>One-page review notes</a> · <a href='performance.csv'>Performance counts</a> · <a href='cache-bake-performance.csv'>World cache bake counts</a></p><header><a href='before-after-sheet.png'><img src='before-after-sheet.png'></a></header><section>$($cards -join "`n")</section></html>"
[System.IO.File]::WriteAllText((Join-Path $Output 'index.html'),$html,[System.Text.UTF8Encoding]::new($false))
Write-Output "Review sheet and index written to $Output"
