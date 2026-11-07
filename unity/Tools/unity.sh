#!/usr/bin/env bash
# Seven Steps Unity runner (Git Bash). Run from anywhere:  unity/Tools/unity.sh <command>
#
#   levels       export prototypes/seven-steps levels + JS solver answers -> Assets/_Project/Data/levels.json
#   sfx          synthesize placeholder sound effects -> Assets/Shared/Audio/*.wav (git 밖)
#   setup        (batch) generate materials/prefabs/GameAssets, Game scene if missing, build/player settings
#   verify       (batch) check C# rules against the JS solver for every stage -> reports/seven-steps-unity-verify.json
#   compile      (batch) compile check only
#   test         (batch) EditMode tests -> reports/editmode-results.xml
#   tileset      (batch) rebuild the placeholder TileSet (Data/TileSets/TopDown_Placeholder.asset)
#   build-win    (batch) Windows build -> Builds/Windows/Seven Steps.exe
#   build-webgl  (batch) WebGL build -> Builds/WebGL/
#   play         run the Windows build
#   smoke        run the Windows build on 4 replays, save screenshots + logs to reports/smoke/
#   serve [port] serve Builds/WebGL on http://localhost:8090 (default)
#   editor       open the project in the Unity editor
#   all          levels + setup + verify + build-win + smoke
#
# Batch commands fail if the editor already has this project open: close it first.
# Logs: unity/Logs/batch-<command>.log (ignored by git). Override the editor path with UNITY_EXE.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJ="$(cd "$HERE/.." && pwd)"
REPO="$(cd "$PROJ/.." && pwd)"
U="${UNITY_EXE:-/c/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Unity.exe}"
export PATH="/c/Program Files/nodejs:$PATH"

# Known Unity 6000.6 issue: a batch run (often the first one in a fresh work space) can re-serialize the
# URP global settings with an empty m_RuntimeSettings list and regenerated SSAO resource ids. A player
# built that way throws ScreenSpaceAmbientOcclusion/Blitter exceptions. Restore the committed file then.
URPGS="unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"
runtime_list_empty() { grep -A1 "m_RuntimeSettings:" "$1" | grep -q "m_List: \[\]"; }
restore_urp_settings() {
  local stat added deleted
  stat="$(cd "$REPO" && git diff --numstat -- "$URPGS")"
  [ -z "$stat" ] && return 1
  read -r added deleted _ <<< "$stat"
  local committed; committed="$(mktemp)"
  (cd "$REPO" && git show "HEAD:$URPGS") > "$committed"
  if { runtime_list_empty "$REPO/$URPGS" && ! runtime_list_empty "$committed"; } || { [ "$added" = "0" ] && [ "$deleted" != "0" ]; }; then
    rm -f "$committed"
    (cd "$REPO" && git checkout -- "$URPGS")
    echo "[unity.sh] restored $URPGS (Unity emptied/stripped m_RuntimeSettings: +$added -$deleted)"
    return 0
  fi
  rm -f "$committed"
  echo "[unity.sh] WARNING: $URPGS changed (+$added -$deleted). Check before committing."
  return 1
}

run_unity() {
  local name="$1" method="$2"
  (cd "$REPO" && "$U" -batchmode -nographics -projectPath unity -executeMethod "$method" -logFile "unity/Logs/batch-$name.log")
}

batch() {
  local name="$1" method="$2"
  local log="$PROJ/Logs/batch-$name.log"
  mkdir -p "$PROJ/Logs"
  echo "[unity.sh] $name: $method (log: unity/Logs/batch-$name.log)"
  local code=0
  run_unity "$name" "$method" || code=$?
  if restore_urp_settings && [ "$code" -eq 0 ] && [[ "$name" == build-* ]]; then
    echo "[unity.sh] $name: rebuilding with the restored URP settings"
    run_unity "$name" "$method" || code=$?
    restore_urp_settings || true
  fi
  grep -E "\[SevenSteps\]|\[BuildTools\]|^(OK|FAIL) |error CS|^[A-Za-z.]*Exception|Aborting batchmode" "$log" || true
  if [ "$code" -ne 0 ]; then echo "[unity.sh] $name FAILED (exit $code). See $log"; exit "$code"; fi
  echo "[unity.sh] $name OK"
}

cmd="${1:-help}"
case "$cmd" in
  levels)      node "$HERE/export-levels.mjs" ;;
  sfx)         node "$HERE/gen-sfx.mjs" ;;
  setup)       batch setup SevenSteps.EditorTools.ProjectSetup.BatchSetup ;;
  verify)      batch verify SevenSteps.EditorTools.LevelVerifier.BatchVerify ;;
  compile)     batch compile BuildTools.CompileCheck ;;
  tileset)     batch tileset SevenSteps.EditorTools.TileSetBuilder.BatchBuild ;;
  test)
    # EditMode tests (Tests/EditMode). Results: reports/editmode-results.xml
    mkdir -p "$REPO/reports" "$PROJ/Logs"
    echo "[unity.sh] test: EditMode (log: unity/Logs/batch-test.log)"
    code=0
    (cd "$REPO" && "$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
      -testResults "$(cygpath -w "$REPO/reports/editmode-results.xml")" -logFile unity/Logs/batch-test.log) || code=$?
    restore_urp_settings || true
    summary="$(grep -o '<test-run [^>]*' "$REPO/reports/editmode-results.xml" 2>/dev/null | grep -o '\(total\|passed\|failed\|skipped\)="[0-9]*"' | tr '\n' ' ')"
    echo "[unity.sh] test: ${summary:-no results}"
    grep -o '<test-case [^>]*result="Failed"[^>]*' "$REPO/reports/editmode-results.xml" 2>/dev/null | grep -o 'fullname="[^"]*"' || true
    if [ "$code" -ne 0 ]; then echo "[unity.sh] test FAILED (exit $code)"; exit "$code"; fi
    echo "[unity.sh] test OK" ;;
  build-win)   batch build-win BuildTools.BuildWindows ;;
  build-webgl) batch build-webgl BuildTools.BuildWebGL ;;
  play)        "$REPO/Builds/Windows/Seven Steps.exe" & ;;
  smoke)
    # Runs the Windows build on a few replays, saves screenshots + logs to reports/smoke/, fails on exceptions.
    out="$REPO/reports/smoke"; mkdir -p "$out"; fail=0
    for spec in "1:R" "3:DURRRRR" "8:LRRRRDDKLLDD" "12:DUDURRRDURRRRRKDDDD"; do
      lv="${spec%%:*}"; rp="${spec#*:}"
      winout="$(cygpath -w "$out")"
      "$REPO/Builds/Windows/Seven Steps.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
        -level "$lv" -replay "$rp" -screenshot "$winout\\level$lv.png" -logFile "$winout\\level$lv.log" > /dev/null || true
      grep -h "\[SevenSteps\] Screenshot" "$out/level$lv.log" || { echo "[unity.sh] smoke level $lv: no screenshot"; fail=1; }
      if grep -qE "^[A-Za-z.]*Exception" "$out/level$lv.log"; then grep -E "^[A-Za-z.]*Exception" "$out/level$lv.log"; fail=1; fi
    done
    [ "$fail" -eq 0 ] && echo "[unity.sh] smoke OK -> reports/smoke/" || { echo "[unity.sh] smoke FAILED"; exit 1; } ;;
  serve)
    port="${2:-8090}"
    echo "[unity.sh] http://localhost:$port  (Ctrl+C to stop)"
    cd "$REPO/Builds/WebGL" && python -m http.server "$port" ;;
  editor)      "$U" -projectPath "$PROJ" & ;;
  all)
    "$0" levels
    "$0" setup
    "$0" verify
    "$0" build-win
    "$0" smoke ;;
  *)
    sed -n '2,24p' "$0" ;;
esac
