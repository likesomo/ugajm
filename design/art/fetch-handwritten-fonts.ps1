param([string]$Output='C:/unityjam/shared-assets/seven-steps/Assets/Fonts')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Output | Out-Null
$families=@{kaushanscript='KaushanScript';patrickhand='PatrickHand';gaegu='Gaegu';gamjaflower='GamjaFlower';himelody='HiMelody'}
foreach($slug in $families.Keys){
 $name=$families[$slug]
 foreach($file in @("$name-Regular.ttf",'OFL.txt')){
  $target=if($file -eq 'OFL.txt'){Join-Path $Output "$name-OFL.txt"}else{Join-Path $Output $file}
  if(-not(Test-Path $target)){Invoke-WebRequest -UseBasicParsing "https://raw.githubusercontent.com/google/fonts/main/ofl/$slug/$file" -OutFile $target}
 }
 Get-FileHash (Join-Path $Output "$name-Regular.ttf") -Algorithm SHA256
}
# Never replace an existing file/meta: shared GUIDs must remain stable.
