# The generator only uses Python's standard library; Blender's bundled Python is available on this art workstation.
& 'C:/Program Files/Blender Foundation/Blender 5.2/5.2/python/bin/python.exe' (Join-Path $PSScriptRoot 'build-weather-texture.py')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
