# Counts pixels that differ from the background in the board area of a game screenshot. Used by unity.sh smoke-webgl.
# The area is the screen centre (x 30-70%, y 33-66%): it skips the HUD rows above and the message line below.
#   -Background FF00FF  known background (the game's ?smoke=board|empty clear colour, no sky or planet): count pixels that
#                       differ from it. Palette-independent for any TileSet; a missing board scores ~0.
#   (no -Background)    background = the most common colour in the area. Only meaningful with a flat backdrop: a chapter
#                       sky or planet that fills the area saturates it (every pixel counts), so smoke-webgl does not use it
#                       for the board check.
# Prints one number (sampled every 2nd pixel in x and y, so the full area is about (0.4*W/2)*(0.33*H/2)).
param([string]$Path, [string]$Background = "")
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($Path)
try {
  $x0 = [int]($b.Width * 0.3); $x1 = [int]($b.Width * 0.7)
  $y0 = [int]($b.Height * 0.33); $y1 = [int]($b.Height * 0.66)
  $px = New-Object System.Collections.Generic.List[int]
  $hist = @{}
  for ($y = $y0; $y -lt $y1; $y += 2) { for ($x = $x0; $x -lt $x1; $x += 2) {
    $c = $b.GetPixel($x, $y)
    $px.Add(([int]$c.R -shl 16) -bor ([int]$c.G -shl 8) -bor [int]$c.B)
    $q = (([int]$c.R -shr 4) -shl 8) -bor (([int]$c.G -shr 4) -shl 4) -bor ([int]$c.B -shr 4)
    $hist[$q] = 1 + [int]$hist[$q]
  }}
  if ($Background) {
    $v = [Convert]::ToInt32($Background.TrimStart('#'), 16)
    $br = ($v -shr 16) -band 255; $bg = ($v -shr 8) -band 255; $bb = $v -band 255
  } else {
    $mode = ($hist.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
    $br = (($mode -shr 8) -band 15) * 16 + 8; $bg = (($mode -shr 4) -band 15) * 16 + 8; $bb = ($mode -band 15) * 16 + 8
  }
  $n = 0
  foreach ($v in $px) {
    $d = [Math]::Max([Math]::Abs((($v -shr 16) -band 255) - $br), [Math]::Max([Math]::Abs((($v -shr 8) -band 255) - $bg), [Math]::Abs(($v -band 255) - $bb)))
    if ($d -gt 28) { $n++ }
  }
  $n
} finally { $b.Dispose() }
