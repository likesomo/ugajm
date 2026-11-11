# Run from the repository root before PropsReviewCapture.Run -props-baseline true.
# Baseline assets are temporary and must not be committed.
$destination = 'unity/Assets/_Project/Art/Test/PropsBaseline'
New-Item -ItemType Directory -Force $destination | Out-Null
$commands = @'
for k in Exit Gate CountDoor Plate Door Bridge Cracked FlipUp FlipDown Ice Lever Footprint Mud Start PlayerA PlayerB PlayerC; do git show 0c08d82:unity/Assets/_Project/Prefabs/Tiles/RuinsHenge/$k.prefab > unity/Assets/_Project/Art/Test/PropsBaseline/$k.prefab; done
git show 0c08d82:unity/Assets/_Project/Art/RuinsHenge/RuinsHenge_Chapter01.asset > unity/Assets/_Project/Art/Test/PropsBaseline/Set.asset
'@
& 'C:/Program Files/Git/bin/bash.exe' -lc $commands
