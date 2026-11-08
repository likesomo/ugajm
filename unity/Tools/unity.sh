#!/usr/bin/env bash
# Seven Steps Unity runner (Git Bash). Run from anywhere:  unity/Tools/unity.sh <command>
#
#   levels       export prototypes/seven-steps levels + JS solver answers -> Assets/_Project/Data/levels.json
#   sfx          synthesize placeholder sound effects -> Assets/Shared/Audio/*.wav (git 밖)
#   golden       regenerate rule golden data (core.js -> Tests/Golden) and telemetry golden (server/fixtures)
#   setup        (batch) generate materials/prefabs/GameAssets, Game scene if missing, build/player settings
#   verify       (batch) check C# rules against the JS solver for every stage -> reports/seven-steps-unity-verify.json
#   compile      (batch) compile check only
#   test         (batch) EditMode tests -> reports/editmode-results.xml
#   tileset      (batch) rebuild the placeholder TileSet (Data/TileSets/TopDown_Placeholder.asset)
#   tileset-validate (batch) check every TileSet against docs/architecture.md 12 (errors -> exit 1)
#   build-win    (batch) Windows build -> Builds/Windows/Seven Steps.exe
#   build-webgl  (batch) WebGL build -> Builds/WebGL/
#   play         run the Windows build
#   smoke        run the Windows build on 7 replays, save screenshots + logs to reports/smoke/
#   smoke-webgl  open Builds/WebGL in the Orca browser on 3 replays, fail if the 3D board is missing
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
  URP_EMPTIED=0
  if runtime_list_empty "$REPO/$URPGS" && ! runtime_list_empty "$committed"; then URP_EMPTIED=1; fi
  if [ "$URP_EMPTIED" = "1" ] || { [ "$added" = "0" ] && [ "$deleted" != "0" ]; }; then
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
  # Open the editor on the build's own target. URP's build step picks the pipeline assets for shader stripping
  # from the active target at startup: a WebGL build started while Standalone is active strips the Mobile
  # pipeline's shaders and the WebGL player draws the UI but no 3D board (2026-11-07).
  local target=()
  case "$name" in
    build-webgl) target=(-buildTarget WebGL) ;;
    build-win)   target=(-buildTarget Win64) ;;
  esac
  (cd "$REPO" && "$U" -batchmode -nographics -projectPath unity "${target[@]}" -executeMethod "$method" -logFile "unity/Logs/batch-$name.log")
}

batch() {
  local name="$1" method="$2"
  local log="$PROJ/Logs/batch-$name.log"
  mkdir -p "$PROJ/Logs"
  echo "[unity.sh] $name: $method (log: unity/Logs/batch-$name.log)"
  local code=0
  # Before: never start from a stripped file (a WebGL build legitimately strips the SSAO resources, which
  # the next Windows build needs). After: put the committed file back. Rebuild only when Unity emptied the
  # whole m_RuntimeSettings list during this run (cause unknown, so stay conservative).
  restore_urp_settings > /dev/null || true
  run_unity "$name" "$method" || code=$?
  URP_EMPTIED=0
  if restore_urp_settings && [ "$URP_EMPTIED" = "1" ] && [ "$code" -eq 0 ] && [[ "$name" == build-* ]]; then
    echo "[unity.sh] $name: rebuilding (m_RuntimeSettings was emptied during the build)"
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
  golden)
    # Rule golden first: the telemetry golden reads Tests/Golden/traces.golden.json as input.
    node "$HERE/gen-golden.mjs"
    node "$REPO/server/tools/telemetry-golden.mjs"
    (cd "$REPO" && git status --short -- unity/Assets/_Project/Tests/Golden server/fixtures) ;;
  setup)       batch setup SevenSteps.EditorTools.ProjectSetup.BatchSetup ;;
  verify)      batch verify SevenSteps.EditorTools.LevelVerifier.BatchVerify ;;
  compile)     batch compile BuildTools.CompileCheck ;;
  tileset)     batch tileset SevenSteps.EditorTools.TileSetBuilder.BatchBuild ;;
  tileset-validate) batch tileset-validate SevenSteps.EditorTools.TileSetValidator.BatchValidate ;;
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
    for spec in "1:R" "3:DURRRRR" "8:DURRRRRKDDR" "9:LRRRRDDKLLDD" "13:DUDUDURKDD" "15:RLRRRRRX" "16:DUDURRRDURRRRRKDDDD"; do
      lv="${spec%%:*}"; rp="${spec#*:}"
      winout="$(cygpath -w "$out")"
      "$REPO/Builds/Windows/Seven Steps.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 \
        -level "$lv" -replay "$rp" -screenshot "$winout\\level$lv.png" -logFile "$winout\\level$lv.log" > /dev/null || true
      grep -h "\[SevenSteps\] Screenshot" "$out/level$lv.log" || { echo "[unity.sh] smoke level $lv: no screenshot"; fail=1; }
      if grep -qE "^[A-Za-z.]*Exception" "$out/level$lv.log"; then grep -E "^[A-Za-z.]*Exception" "$out/level$lv.log"; fail=1; fi
    done
    [ "$fail" -eq 0 ] && echo "[unity.sh] smoke OK -> reports/smoke/" || { echo "[unity.sh] smoke FAILED"; exit 1; } ;;
  smoke-webgl)
    # Opens Builds/WebGL in Orca's built-in browser on a few replays, screenshots each and counts board pixels.
    # Fails when the 3D board is missing (UI only) or the console shows an exception. Needs the `orca` CLI.
    command -v orca >/dev/null || { echo "[unity.sh] smoke-webgl needs the orca CLI (Orca built-in browser)"; exit 1; }
    out="$REPO/reports/smoke-webgl"; mkdir -p "$out"; rm -f "$out"/*; fail=0; port=8091
    node "$HERE/serve.mjs" "$port" "$REPO/Builds/WebGL" > "$out/serve.log" 2>&1 &
    sleep 2
    for spec in "1:R" "8:DURRRRRKDDR" "13:DUDUDURKDD"; do
      lv="${spec%%:*}"; rp="${spec#*:}"
      page="$(orca tab create --url "http://localhost:$port/?level=$lv&replay=$rp" --json 2>/dev/null | grep -o '"browserPageId": "[^"]*"' | head -1 | cut -d'"' -f4)"
      [ -z "$page" ] && { echo "[unity.sh] smoke-webgl level $lv: could not open a browser tab"; fail=1; continue; }
      orca capture start --page "$page" --json > /dev/null 2>&1 || true
      orca wait --page "$page" --load load --json > /dev/null 2>&1 || true
      sleep 25
      shot="$(orca screenshot --page "$page" 2>/dev/null | grep -oE '[A-Za-z]:\\[^ ]+\.png' | head -1)"
      orca console --page "$page" --limit 400 > "$out/level$lv.console.txt" 2>&1 || true
      orca tab close --page "$page" --json > /dev/null 2>&1 || true
      if [ -z "$shot" ]; then echo "[unity.sh] smoke-webgl level $lv: no screenshot"; fail=1; continue; fi
      cp "$(cygpath -u "$shot")" "$out/level$lv.png"
      px="$(powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$HERE/board-pixels.ps1")" -Path "$(cygpath -w "$out/level$lv.png")" | tr -d '\r')"
      errs="$(grep -ciE '^\[error\]|Exception' "$out/level$lv.console.txt" || true)"
      echo "[unity.sh] smoke-webgl level $lv: board pixels $px, console errors $errs"
      if [ "${px:-0}" -lt 500 ]; then echo "[unity.sh]   board not drawn (UI only?) -> see $out/level$lv.png"; fail=1; fi
      if [ "${errs:-0}" -gt 0 ]; then grep -iE '^\[error\]|Exception' "$out/level$lv.console.txt" | head -5; fail=1; fi
    done
    for p in $(netstat -ano | grep ":$port .*LISTENING" | awk '{print $5}' | sort -u); do taskkill //PID "$p" //F > /dev/null; done
    [ "$fail" -eq 0 ] && echo "[unity.sh] smoke-webgl OK -> reports/smoke-webgl/" || { echo "[unity.sh] smoke-webgl FAILED"; exit 1; } ;;
  serve)
    port="${2:-8090}"
    echo "[unity.sh] http://localhost:$port  (Ctrl+C to stop)"
    node "$HERE/serve.mjs" "$port" "$REPO/Builds/WebGL" ;;
  editor)      "$U" -projectPath "$PROJ" & ;;
  all)
    "$0" levels
    "$0" setup
    "$0" verify
    "$0" build-win
    "$0" smoke ;;
  *)
    sed -n '2,27p' "$0" ;;
esac
