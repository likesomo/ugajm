param([string]$Root='C:/unityjam/shared-assets/seven-steps/concepts/ui')
$ErrorActionPreference='Stop'
$screens=@('title','stage-select','hud','result','result-1','result-3','loading','chapter-entry','language','settings')
$sections=''
foreach($screen in $screens){
 $sections+="<h2>$screen</h2><div class='pair'>"
 foreach($locale in 'en','ko'){
  $file="$locale/$screen.png"
  if(-not(Test-Path (Join-Path $Root $file))){throw "Missing $file"}
  $sections+="<figure><a href='$file'><img src='$file' loading='lazy' alt='$locale $screen'></a><figcaption>$locale / $screen / Unity 1280 x 720</figcaption></figure>"
 }
 $sections+='</div>'
}
$coverage=[Net.WebUtility]::HtmlEncode([IO.File]::ReadAllText((Join-Path $Root 'font-coverage.txt')))
$size=if(Test-Path (Join-Path $Root 'font-build-sizes.json')){[Net.WebUtility]::HtmlEncode([IO.File]::ReadAllText((Join-Path $Root 'font-build-sizes.json')))}else{'See completion report.'}
$html=@"
<!doctype html><html lang='en'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>UI EN/KO - handwritten fonts</title><style>body{margin:24px;background:#101c32;color:#eee6d4;font:16px/1.6 system-ui}h1,h2,a{color:#c9ab6b}.pair{display:grid;grid-template-columns:repeat(2,minmax(280px,1fr));gap:16px}figure{margin:0;padding:10px;background:#263b48}img{width:100%;display:block}pre{white-space:pre-wrap;font-size:13px;background:#172638;padding:18px}h2{margin-top:32px}@media(max-width:760px){.pair{grid-template-columns:1fr}}</style><h1>UI EN/KO - handwritten fonts</h1><p>Real Unity Text + embedded TTF renders. Background art only is AI-generated; every letter is typeset with the actual font. EN: Kaushan Script / Patrick Hand. KO: Gaegu. Numbers: Jua. Original stone/wood trim and button sprites are unchanged.</p><p>Seven open planets: moss / hollow / carved / frost / joinery / crumble / altar. Three-star criteria remain a proposal. Language buttons are layout references, not a shipped language service.</p>$sections<h2>Font coverage / cmap</h2><pre>$coverage</pre><h2>WebGL size comparison</h2><pre>$size</pre><p><a href='fonts-completion-report.md'>Report / limitations / handoff</a> | <a href='index.html'>Previous UI gallery</a> | <a href='../themes/index.html'>All concepts</a></p></html>
"@
[IO.File]::WriteAllText((Join-Path $Root 'en-ko.html'),$html,[Text.UTF8Encoding]::new($false))
Write-Output (Join-Path $Root 'en-ko.html')
