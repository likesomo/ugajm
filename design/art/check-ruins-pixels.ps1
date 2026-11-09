param([Parameter(Mandatory=$true)][string]$Path)
# Art-local counterpart of the placeholder-purple checker; no common tooling changes.
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::FromFile($Path)
$count=0
try {
    for($y=[int]($bitmap.Height*.30);$y -lt [int]($bitmap.Height*.73);$y+=2){
        for($x=[int]($bitmap.Width*.20);$x -lt [int]($bitmap.Width*.80);$x+=2){
            $c=$bitmap.GetPixel($x,$y)
            if($c.R -ge 100 -and $c.G -ge 90 -and $c.B -ge 55 -and $c.R -gt $c.B -and $c.G -gt $c.B -and [Math]::Abs([int]$c.R-[int]$c.G) -lt 65){$count++}
        }
    }
} finally {$bitmap.Dispose()}
Write-Output $count
if($count -lt 500){throw "Ruins board not detected: $count cream pixels"}
