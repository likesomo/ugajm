# Counts pixels that differ from the background in the board area of a game screenshot. Used by unity.sh smoke-webgl.
#   -Background FF00FF  known background: the game's ?smoke=board|empty clear colour (no sky or planet). The game canvas
#                       is found as the bounding box of that colour, so a WebGL page with margins around the canvas
#                       measures the same area as a full-window desktop shot. Palette-independent; a missing board ~0.
#                       No pixel of that colour at all (a build without ?smoke) -> the whole image, which saturates.
#   (no -Background)    background = the most common colour in the area, the whole image is the canvas. Only meaningful
#                       on a flat backdrop: a chapter sky or planet that fills the area saturates it.
# The area is the canvas centre (x 30-70%, y 33-66%, override with -Area "x0,x1,y0,y1"). Prints one number: pixels
# sampled every 2nd pixel in x and y that differ from the background by more than 28 in some channel. With -Info it
# prints "count area canvasX canvasY canvasW canvasH" instead.
param([string]$Path, [string]$Background = "", [string]$Area = "0.3,0.7,0.33,0.66", [switch]$Info)
Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile($Path)
try {
  $W = $bmp.Width; $H = $bmp.Height
  # Read every pixel once (GetPixel is far too slow for full-page WebGL screenshots).
  $rect = New-Object System.Drawing.Rectangle 0, 0, $W, $H
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $stride = $data.Stride
  $buf = New-Object byte[] ($stride * $H)
  [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
  $bmp.UnlockBits($data)

  $cx0 = 0; $cy0 = 0; $cx1 = $W; $cy1 = $H
  if ($Background) {
    $v = [Convert]::ToInt32($Background.TrimStart('#'), 16)
    $br = ($v -shr 16) -band 255; $bg = ($v -shr 8) -band 255; $bb = $v -band 255
    # Canvas = bounding box of background-coloured pixels (BGRA byte order).
    $minX = $W; $minY = $H; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $H; $y += 2) { $row = $y * $stride; for ($x = 0; $x -lt $W; $x += 2) {
      $o = $row + 4 * $x
      if ([Math]::Abs([int]$buf[$o + 2] - $br) -le 28 -and [Math]::Abs([int]$buf[$o + 1] - $bg) -le 28 -and [Math]::Abs([int]$buf[$o] - $bb) -le 28) {
        if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
        if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
      }
    }}
    if ($maxX -ge 0) { $cx0 = $minX; $cy0 = $minY; $cx1 = $maxX + 1; $cy1 = $maxY + 1 }
  }
  $a = $Area.Split(',') | ForEach-Object { [double]$_ }
  $cw = $cx1 - $cx0; $ch = $cy1 - $cy0
  $x0 = $cx0 + [int]($cw * $a[0]); $x1 = $cx0 + [int]($cw * $a[1])
  $y0 = $cy0 + [int]($ch * $a[2]); $y1 = $cy0 + [int]($ch * $a[3])

  if (-not $Background) {
    $hist = @{}
    for ($y = $y0; $y -lt $y1; $y += 2) { $row = $y * $stride; for ($x = $x0; $x -lt $x1; $x += 2) {
      $o = $row + 4 * $x
      $q = (([int]$buf[$o + 2] -shr 4) -shl 8) -bor (([int]$buf[$o + 1] -shr 4) -shl 4) -bor ([int]$buf[$o] -shr 4)
      $hist[$q] = 1 + [int]$hist[$q]
    }}
    $mode = ($hist.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 1).Key
    $br = (($mode -shr 8) -band 15) * 16 + 8; $bg = (($mode -shr 4) -band 15) * 16 + 8; $bb = ($mode -band 15) * 16 + 8
  }
  $n = 0; $total = 0
  for ($y = $y0; $y -lt $y1; $y += 2) { $row = $y * $stride; for ($x = $x0; $x -lt $x1; $x += 2) {
    $o = $row + 4 * $x; $total++
    if ([Math]::Abs([int]$buf[$o + 2] - $br) -gt 28 -or [Math]::Abs([int]$buf[$o + 1] - $bg) -gt 28 -or [Math]::Abs([int]$buf[$o] - $bb) -gt 28) { $n++ }
  }}
  if ($Info) { "$n $total $cx0 $cy0 $cw $ch" } else { $n }
} finally { $bmp.Dispose() }
