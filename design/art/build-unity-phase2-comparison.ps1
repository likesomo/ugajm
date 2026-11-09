param([string]$Root='C:/unityjam/shared-assets/seven-steps/concepts/themes/ruins-henge-planets/unity/phase2')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System.Drawing;
public static class RuinsScreenshotFrame {
 public static int[] Read(string path) {
  using(var b=new Bitmap(path)) {
   int top=-1,bottom=-1;
   for(int y=0;y<b.Height;y++){int dark=0;for(int x=0;x<b.Width;x+=4){var c=b.GetPixel(x,y);if(c.R<240||c.G<240||c.B<240)dark++;}if(dark>b.Width*.35/4){if(top<0)top=y;bottom=y;}}
   if(top<0)return new[]{b.Width,b.Height,0,0,b.Width,b.Height};
   int left=b.Width,right=0,mid=(top+bottom)/2;
   for(int x=0;x<b.Width;x++){var c=b.GetPixel(x,mid);if(c.R<240||c.G<240||c.B<240){left=System.Math.Min(left,x);right=x;}}
   return new[]{b.Width,b.Height,left,top,right-left+1,bottom-top+1};
  }
 }
}
'@
function Card([string]$Path,[string]$Label){
    $p=Join-Path $Root $Path
    $b=[RuinsScreenshotFrame]::Read($p)
    $width=100.0*$b[0]/$b[4];$left=-100.0*$b[2]/$b[4];$top=-100.0*$b[3]/$b[5]
    return "<figure><a href='$Path'><div class='frame' style='aspect-ratio:$($b[4])/$($b[5])'><img src='$Path' style='width:$($width.ToString('0.000',[cultureinfo]::InvariantCulture))%;left:$($left.ToString('0.000',[cultureinfo]::InvariantCulture))%;top:$($top.ToString('0.000',[cultureinfo]::InvariantCulture))%' alt='$Label'></div></a><figcaption>$Label</figcaption></figure>"
}
$sections=''
foreach($lv in @(1,8,13)){$sections+="<h2>WebGL · 스테이지 $lv</h2><div class='pair'>"+(Card "before/webgl-level$lv.png" '1차 · 7e93396')+(Card "after/webgl-level$lv.png" '2차 · 색·조명·작은 행성·성운')+'</div>'}
$sections+="<h2>Windows · 스테이지1, 같은7걸음 완료</h2><div class='pair'>"+(Card 'before/windows-level1.png' '1차 · Win')+(Card 'after/windows-level1.png' '2차 · Win')+'</div>'
$metrics=if(Test-Path (Join-Path $Root 'metrics.json')){[System.Net.WebUtility]::HtmlEncode((Get-Content (Join-Path $Root 'metrics.json') -Raw -Encoding UTF8))}else{'성능 표본은 완료 보고서 참고'}
$html=@"
<!doctype html><html lang='ko'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>RuinsHenge Unity 2차 비교</title><style>body{margin:24px;background:#eee9df;color:#263441;font:16px/1.6 system-ui}h1{font-size:27px}h2{margin-top:30px}.pair{display:grid;grid-template-columns:repeat(2,minmax(300px,1fr));gap:16px}figure{margin:0;background:#fffaf2;padding:12px}.frame{position:relative;overflow:hidden;background:#0c1730}.frame img{display:block;max-width:none;position:absolute;height:auto}figcaption{padding-top:8px;font-weight:600}.reference{max-width:1000px;width:100%;display:block}a{color:inherit}pre{white-space:pre-wrap;background:#fffaf2;padding:16px;font-size:13px}@media(max-width:700px){.pair{grid-template-columns:1fr}}</style>
<h1>Unity 2차 다듬기 · 같은 스테이지 전후</h1><p>청회색 분할 석재 벽 / 밝은 평탄 바닥·이음새 이끼 / 따뜻한 키·차가운 필·약한 림 / 스테이지에 맞춘 암석 행성 / 성운·별 / 굵어진 형제 색 링.</p><p>스크린샷 원본은 바꾸지 않고 HTML에서 브라우저의 흰 바깥 여백만 잘라 같은 게임 화면 크기로 보여 줍니다. 클릭하면 전체 원본이 열립니다. 퍼즐 배치·입력·공용 UI는 바꾸지 않았습니다.</p>
$sections
<h2>시안 비교 · 재질·조명·우주 배경의 기준</h2><a href='../../01-moss-orbit/board.png'><img class='reference' src='../../01-moss-orbit/board.png' alt='확정 테마의 첫 행성 시안'></a><p>시안은 분위기 기준이며 위 스테이지의 실제 맵과 같은 퍼즐 배치가 아닙니다. UI 칠성 스타일은 공용 UI 담당 후속입니다.</p><h2>후처리 켬/끔 시험</h2><p>약한 Bloom(0.08,2단·Quarter),Neutral 톤매핑,노출+0.15·대비+5·채도+8. WebGL 성능과 화면을 함께 비교합니다.</p><div class='pair'>$(if(Test-Path (Join-Path $Root 'after/webgl-fx-off.png')){(Card 'after/webgl-fx-off.png' '2차 · 후처리 끔')+(Card 'after/webgl-level1.png' '2차 · 후처리 켬')})</div><h2>성능 기록</h2><pre>$metrics</pre><p><a href='completion-report.md'>테스트·로그·남은 공용 요청</a> · <a href='../index.html'>1차 Unity 검증</a></p></html>
"@
[IO.File]::WriteAllText((Join-Path $Root 'index.html'),$html,[Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $Root 'index.html')
