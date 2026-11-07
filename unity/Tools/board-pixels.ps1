# Counts lit-floor pixels (muted purple) in the board area of a game screenshot. Used by unity.sh smoke-webgl:
# a WebGL build that draws only the UI (no 3D board) scores ~0, a drawn board several thousand.
param([string]$Path)
Add-Type -AssemblyName System.Drawing
$b = [System.Drawing.Bitmap]::FromFile($Path)
$n = 0
for ($y = [int]($b.Height * 0.35); $y -lt [int]($b.Height * 0.72); $y += 2) { for ($x = [int]($b.Width * 0.3); $x -lt [int]($b.Width * 0.7); $x += 2) {
  $c = $b.GetPixel($x, $y)
  # lit floor tiles: muted purple (#5f577a / #575070 under light)
  if ($c.B -ge 95 -and $c.B -le 175 -and $c.R -ge 70 -and $c.R -le 140 -and $c.G -ge 60 -and $c.G -le 125 -and ($c.B - $c.G) -ge 18 -and ($c.B - $c.R) -ge 8) { $n++ }
}}
$b.Dispose()
$n
