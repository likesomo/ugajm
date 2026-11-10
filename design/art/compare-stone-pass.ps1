param([string]$Root='C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2')
$stoneAfter="$Root/unity/stone-pass/after";$stoneBefore="$Root/unity/stone-pass/before"
. "$PSScriptRoot/compare-board-shape-a.ps1" -Root $Root -Output $stoneAfter -Before $stoneBefore -BeforeLabel 'Before / 4760464'
$bsaFont=New-Object Drawing.Font('Arial',18)
$bsaSheet=New-Object Drawing.Bitmap(1792,580);$bsaGraphics=[Drawing.Graphics]::FromImage($bsaSheet)
$bsaGraphics.Clear([Drawing.Color]::FromArgb(7,14,28))
$stoneIds=@('moss','hollow','carved','frost','joinery','crumble','altar')
for($i=0;$i -lt $stoneIds.Length;$i++) {
    $id=$stoneIds[$i];Draw-Panel $bsaGraphics "$stoneAfter/$id-stone-closeup.png" (($i%4)*448) ([Math]::Floor($i/4)*290) 448 252 "$id / actual Unity stone"
}
$bsaSheet.Save("$stoneAfter/seven-stone-closeups-unity.png");$bsaGraphics.Dispose();$bsaSheet.Dispose()
$bsaSheet=New-Object Drawing.Bitmap(2560,756);$bsaGraphics=[Drawing.Graphics]::FromImage($bsaSheet)
$bsaGraphics.Clear([Drawing.Color]::FromArgb(7,14,28))
Draw-Panel $bsaGraphics "$stoneBefore/moss-8x6-proposed.png" 0 0 1280 720 'Before / 4760464'
Draw-Panel $bsaGraphics "$stoneAfter/moss-8x6-proposed.png" 1280 0 1280 720 'After / grey-white granite / actual Unity'
$bsaSheet.Save("$stoneAfter/moss-stone-before-after.png");$bsaGraphics.Dispose();$bsaSheet.Dispose();$bsaFont.Dispose()
