# Counts non-background pixels in the board area of a game screenshot. Used by unity.sh smoke-webgl:
# a WebGL build that draws only the UI (no 3D board) scores ~0, a drawn board several thousand.
# Palette-independent (works for any TileSet): the background is the most common colour in the area,
# and a pixel counts when it differs from it clearly. The area skips the HUD rows above and the message line below.
param([string]$Path)
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($Path)
try {
  $x0 = [int]($b.Width * 0.3); $x1 = [int]($b.Width * 0.7)
  $y0 = [int]($b.Height * 0.33); $y1 = [int]($b.Height * 0.66)
  $px = New-Object System.Collections.Generic.List[int]
  $hist = @{}
  for ($y = $y0; $y -lt $y1; $y += 2) { for ($x = $x0; $x -lt $x1; $x += 2) {
    $c = $b.GetPixel($x, $y)
    $px.Add(($c.R -shl 16) -bor ($c.G -shl 8) -bor $c.B)
    $q = (($c.R -shr 4) -shl 8) -bor (($c.G -shr 4) -shl 4) -bor ($c.B -shr 4)
    $hist[$q] = 1 + [int]$hist[$q]
  }}
  $mode = ($hist.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
  $br = (($mode -shr 8) -band 15) * 16 + 8; $bg = (($mode -shr 4) -band 15) * 16 + 8; $bb = ($mode -band 15) * 16 + 8
  $n = 0
  foreach ($v in $px) {
    $d = [Math]::Max([Math]::Abs((($v -shr 16) -band 255) - $br), [Math]::Max([Math]::Abs((($v -shr 8) -band 255) - $bg), [Math]::Abs(($v -band 255) - $bb)))
    if ($d -gt 28) { $n++ }
  }
  $n
} finally { $b.Dispose() }
