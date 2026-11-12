#!/usr/bin/env bash
# Seven Steps Unity runner (Git Bash). Run from anywhere:  unity/Tools/unity.sh <command>
#
#   levels       export prototypes/seven-steps levels + JS solver answers -> Assets/_Project/Data/levels.json
#   story-check  check design/story/story.json (+ copy-levels.json) with design/story/tools/check-story.mjs (node, no Unity);
#                levels also runs it and copies story.json -> Assets/_Project/Data/story.json only when it passes
#   sfx          synthesize placeholder sound effects -> Assets/Shared/Audio/*.wav (git 밖)
#   golden       regenerate rule golden data (core.js -> Tests/Golden) and telemetry golden (server/fixtures)
#   setup        (batch) generate materials/prefabs/GameAssets, Game scene if missing, build/player settings
#   verify       (batch) check C# rules against the JS solver for every stage -> reports/seven-steps-unity-verify.json
#   compile      (batch) compile check only
#   test         (batch) EditMode tests -> reports/editmode-results.xml
#   tileset      (batch) rebuild the placeholder TileSet (Data/TileSets/TopDown_Placeholder.asset)
#   tileset-validate (batch) check every TileSet against docs/architecture.md 12 (errors -> exit 1)
#   scene-validate (batch) check build list = AppScenes, scene roots and their references (errors -> exit 1)
#   build-win    (batch) Windows build -> Builds/Windows/Seven Steps.exe
#   build-webgl  (batch) WebGL build -> Builds/WebGL/
#   qa-replay    (batch) replay reports/qa/attempts.jsonl in C# (BuildTools.QaReplay, docs/qa-attempts.md) -> reports/qa/unity-replay.json
#   play         run the Windows build
#   smoke        run the Windows build on 7 replays, save screenshots + logs to reports/smoke/
#   smoke-webgl  open Builds/WebGL in the Orca browser on 3 replays (+ ?smoke=board, + a ?smoke=empty control), fail if the board is missing
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

# Unity re-serializes these with only whitespace changed (a trailing space after "m_EditorClassIdentifier:" and
# "m_Values:") on every batch run. Put the committed file back only when the change is whitespace-only, so a real
# edit (art tuning the pipeline asset) is never thrown away.
WS_ONLY_ASSETS=("unity/Assets/Settings/Mobile_RPAsset.asset")
restore_whitespace_only() {
  local f
  for f in "${WS_ONLY_ASSETS[@]}"; do
    (cd "$REPO" && ! git diff --quiet -- "$f" && git diff -w --quiet -- "$f") || continue
    (cd "$REPO" && git checkout -- "$f") && echo "[unity.sh] restored $f (whitespace-only re-serialization)"
  done
  return 0
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
  # Captures render with the GPU: no -nographics for them.
  local gfx=(-nographics)
  [ "$name" = stageselect-capture ] && gfx=()
  (cd "$REPO" && "$U" -batchmode "${gfx[@]}" -projectPath unity "${target[@]}" -executeMethod "$method" -logFile "unity/Logs/batch-$name.log")
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
  restore_whitespace_only > /dev/null
  run_unity "$name" "$method" || code=$?
  restore_whitespace_only
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
  story-check) node "$REPO/design/story/tools/check-story.mjs" "${@:2}" ;;
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
  stageselect-capture) batch stageselect-capture SevenSteps.EditorTools.StageSelectCapture.Batch ;;   # stage select v4 review PNGs -> reports/stage-select/
  scene-validate) batch scene-validate SevenSteps.EditorTools.SceneFlowValidator.BatchValidate ;;
  test)
    # EditMode tests (Tests/EditMode). Results: reports/editmode-results.xml
    mkdir -p "$REPO/reports" "$PROJ/Logs"
    echo "[unity.sh] test: EditMode (log: unity/Logs/batch-test.log)"
    rm -f "$REPO/reports/editmode-results.xml"   # never report a previous run's results
    code=0
    (cd "$REPO" && "$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
      -testResults "$(cygpath -w "$REPO/reports/editmode-results.xml")" -logFile unity/Logs/batch-test.log) || code=$?
    restore_urp_settings || true
    restore_whitespace_only
    summary="$(grep -o '<test-run [^>]*' "$REPO/reports/editmode-results.xml" 2>/dev/null | grep -o '\(total\|passed\|failed\|skipped\)="[0-9]*"' | tr '\n' ' ')"
    echo "[unity.sh] test: ${summary:-no results}"
    grep -o '<test-case [^>]*result="Failed"[^>]*' "$REPO/reports/editmode-results.xml" 2>/dev/null | grep -o 'fullname="[^"]*"' || true
    if [ "$code" -ne 0 ]; then echo "[unity.sh] test FAILED (exit $code)"; exit "$code"; fi
    echo "[unity.sh] test OK" ;;
  playtest)
    # PlayMode tests (Tests/PlayMode, docs/scene-flow.md 9): the Unity scene flow in the Editor Play Mode, no player build.
    # Batchmode with graphics (scenes render offscreen; no -nographics). Results: reports/playmode-results.xml
    mkdir -p "$REPO/reports" "$PROJ/Logs"
    echo "[unity.sh] playtest: PlayMode (log: unity/Logs/batch-playtest.log)"
    rm -f "$REPO/reports/playmode-results.xml"
    code=0
    (cd "$REPO" && "$U" -batchmode -projectPath unity -runTests -testPlatform PlayMode \
      -testResults "$(cygpath -w "$REPO/reports/playmode-results.xml")" -logFile unity/Logs/batch-playtest.log) || code=$?
    restore_urp_settings || true
    restore_whitespace_only
    summary="$(grep -o '<test-run [^>]*' "$REPO/reports/playmode-results.xml" 2>/dev/null | grep -o '\(total\|passed\|failed\|skipped\)="[0-9]*"' | tr '\n' ' ')"
    echo "[unity.sh] playtest: ${summary:-no results}"
    grep -o '<test-case [^>]*result="Failed"[^>]*' "$REPO/reports/playmode-results.xml" 2>/dev/null | grep -o 'fullname="[^"]*"' || true
    if [ "$code" -ne 0 ]; then echo "[unity.sh] playtest FAILED (exit $code)"; exit "$code"; fi
    echo "[unity.sh] playtest OK" ;;
  playcapture)
    # Headless Play Mode capture (docs/scene-flow.md 9.2; user rule 2026-11-11: batch mode only, no Editor GUI): the
    # [Explicit] PlayMode test class PlayCaptureTests renders the cameras + UI into 1280x720 PNGs -> reports/playcapture/. Optional test name: playcapture AdvanceMidway.
    # -batchmode without -nographics: cameras need a graphics device.
    out="$REPO/reports/playcapture"; mkdir -p "$out" "$PROJ/Logs"
    echo "[unity.sh] playcapture -> reports/playcapture (log: unity/Logs/batch-playcapture.log)"
    rm -f "$REPO/reports/playcapture-results.xml"
    code=0
    (cd "$REPO" && "$U" -batchmode -projectPath unity -runTests -testPlatform PlayMode       -testFilter "SevenSteps.Tests.PlayMode.PlayCaptureTests${2:+.$2}"       -testResults "$(cygpath -w "$REPO/reports/playcapture-results.xml")" -logFile unity/Logs/batch-playcapture.log) || code=$?
    restore_urp_settings || true
    restore_whitespace_only
    grep -hF "[SevenSteps] Play capture" "$PROJ/Logs/batch-playcapture.log" | sed 's/^.*\[SevenSteps\]/[SevenSteps]/' || true
    summary="$(grep -o '<test-run [^>]*' "$REPO/reports/playcapture-results.xml" 2>/dev/null | grep -oE '(total|passed|failed)="[0-9]*"' | tr '
' ' ')"
    echo "[unity.sh] playcapture: ${summary:-no results}"
    grep -o '<test-case [^>]*result="Failed"[^>]*' "$REPO/reports/playcapture-results.xml" 2>/dev/null | grep -o 'fullname="[^"]*"' || true
    if [ "$code" -ne 0 ]; then echo "[unity.sh] playcapture FAILED (exit $code)"; exit "$code"; fi
    echo "[unity.sh] playcapture OK" ;;
  build-win)   batch build-win BuildTools.BuildWindows ;;
  build-webgl) batch build-webgl BuildTools.BuildWebGL ;;
  qa-replay)   batch qa-replay BuildTools.QaReplay ;;   # replays reports/qa/attempts.jsonl in C# (docs/qa-attempts.md 7) -> reports/qa/unity-replay.json
  play)        "$REPO/Builds/Windows/Seven Steps.exe" & ;;
  smoke)
    # Runs the Windows build on a few replays, saves screenshots + logs to reports/smoke/, fails on exceptions.
    out="$REPO/reports/smoke"; mkdir -p "$out"; fail=0
    # Windows PlayerPrefs are one registry key per product, shared by every workspace on this PC: concurrent smoke runs
    # overwrote each other's progress (title continue/map). -savekey suffixes every save key (LaunchOptions.SaveKey):
    # the workspace folder name, other characters -> _, a leading - -> _ (it would read as a flag), at most 32 chars.
    SAVEKEY="$(basename "$REPO" | sed 's/[^A-Za-z0-9_-]/_/g; s/^-/_/' | cut -c1-32)"; SAVEKEY="${SAVEKEY:-smoke}"
    echo "[unity.sh] smoke save key: $SAVEKEY"
    for spec in "1:R" "4:DURRRRRK" "8:RRRRRRDX" "10:RLRLRRR" "22:RDDLLLL" "29:DURRRRRKDDR" "43:RRRRRR"; do
      lv="${spec%%:*}"; rp="${spec#*:}"
      winout="$(cygpath -w "$out")"
      "$REPO/Builds/Windows/Seven Steps.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -savekey "$SAVEKEY" \
        -level "$lv" -replay "$rp" -screenshot "$winout\\level$lv.png" -logFile "$winout\\level$lv.log" > /dev/null || true
      grep -h "\[SevenSteps\] Screenshot" "$out/level$lv.log" || { echo "[unity.sh] smoke level $lv: no screenshot"; fail=1; }
      if grep -qE "^[A-Za-z.]*Exception" "$out/level$lv.log"; then grep -E "^[A-Za-z.]*Exception" "$out/level$lv.log"; fail=1; fi
    done
    # Flow step (docs/scene-flow.md S8): each planet's last stage + 2 mid-planet controls with -flowcheck. The solver's
    # path wins for real; the player checks the Result text (chapter_done / journey_done), Next, the chapter entry card
    # and the chapter look, logs OK or FAIL and quits. Screenshots flowN-result.png / flowN-next.png.
    for spec in $(node "$HERE/flow-stages.mjs" "$(cygpath -w "$REPO/unity/Assets/_Project/Data/levels.json")"); do
      lv="${spec%%:*}"; rp="${spec#*:}"
      winout="$(cygpath -w "$out")"
      "$REPO/Builds/Windows/Seven Steps.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -savekey "$SAVEKEY" -lang ko \
        -level "$lv" -replay "$rp" -flowcheck -screenshot "$winout\\flow$lv.png" -logFile "$winout\\flow$lv.log" > /dev/null || true
      grep -h "\[SevenSteps\] Flow check" "$out/flow$lv.log" || { echo "[unity.sh] smoke flow $lv: no result line"; fail=1; }
      grep -q "\[SevenSteps\] Flow check stage $lv: OK" "$out/flow$lv.log" || fail=1
      if grep -qE "^[A-Za-z.]*Exception" "$out/flow$lv.log"; then grep -E "^[A-Za-z.]*Exception" "$out/flow$lv.log"; fail=1; fi
    done
    # Title step (docs/scene-flow.md S8): no -level, Boot -> Title, the real buttons of each path (-titlecheck), the Game
    # scene logs where it arrived. In order: begin (fresh player) -> stage 1 via select; map -> planet 2's first stage via
    # select; continue -> that stage again via boot (resumed). Catches a dead title button that replay runs skip.
    p2="$(node "$HERE/flow-stages.mjs" "$(cygpath -w "$REPO/unity/Assets/_Project/Data/levels.json")" --planet2-first)"
    for spec in "begin:1:select:-newplayer" "map:$p2:select:" "continue:$p2:boot:"; do
      IFS=: read -r path lv via extra <<< "$spec"
      winout="$(cygpath -w "$out")"
      "$REPO/Builds/Windows/Seven Steps.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -savekey "$SAVEKEY" -lang ko $extra \
        -titlecheck "$path" -logFile "$winout\\title-$path.log" > /dev/null || true
      want="[SevenSteps] Title check $path: arrived stage $lv via $via"
      grep -h "\[SevenSteps\] Title check" "$out/title-$path.log" || echo "[unity.sh] smoke title $path: no result line"
      grep -qF "$want" "$out/title-$path.log" || { echo "[unity.sh] smoke title $path: expected \"$want\""; fail=1; }
      if grep -qE "^[A-Za-z.]*Exception" "$out/title-$path.log"; then grep -E "^[A-Za-z.]*Exception" "$out/title-$path.log"; fail=1; fi
    done
    # Journey step (docs/stage-select-transitions.md, stage select v4 behind -newmap): stage 1's win advances to stage 2 with
    # no result panel; the first planet's last stage pulls back to the star map and lights the trail. Screenshots
    # journey-KIND-mid.png / -end.png (complete also -pullback.png from the Game scene).
    for spec in $(node "$HERE/flow-stages.mjs" "$(cygpath -w "$REPO/unity/Assets/_Project/Data/levels.json")" --journey); do
      IFS=: read -r kind lv rp <<< "$spec"
      winout="$(cygpath -w "$out")"
      "$REPO/Builds/Windows/Seven Steps.exe" -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -savekey "$SAVEKEY" -lang ko -newmap \
        -level "$lv" -replay "$rp" -journeycheck "$kind" -screenshot "$winout\\journey-$kind.png" -logFile "$winout\\journey-$kind.log" > /dev/null || true
      grep -h "\[SevenSteps\] Journey check" "$out/journey-$kind.log" || { echo "[unity.sh] smoke journey $kind: no result line"; fail=1; }
      grep -q "\[SevenSteps\] Journey check $kind: OK" "$out/journey-$kind.log" || fail=1
      if grep -qE "^[A-Za-z.]*Exception" "$out/journey-$kind.log"; then grep -E "^[A-Za-z.]*Exception" "$out/journey-$kind.log"; fail=1; fi
    done
    [ "$fail" -eq 0 ] && echo "[unity.sh] smoke OK -> reports/smoke/" || { echo "[unity.sh] smoke FAILED"; exit 1; } ;;
  smoke-webgl)
    # Opens Builds/WebGL in Orca's built-in browser and screenshots 3 replays three ways (docs: CLAUDE.md "WebGL 3D 보드 누락"):
    #   normal              the real screen: console exceptions, saved for people to look at
    #   ?smoke=board        no sky/planet, camera clear #FF00FF: board pixels = pixels that differ from magenta (any palette)
    #   ?smoke=empty (1x)   same but the board is hidden: the negative control. It must score under the threshold, so a
    #                       measurement that saturates again (or a build without the smoke option) fails instead of passing.
    # Fails on a missing board, a failed control, or a console exception. Needs the `orca` CLI.
    command -v orca >/dev/null || { echo "[unity.sh] smoke-webgl needs the orca CLI (Orca built-in browser)"; exit 1; }
    out="$REPO/reports/smoke-webgl"; mkdir -p "$out"; rm -f "$out"/*; fail=0
    [ -f "$REPO/Builds/WebGL/index.html" ] || { echo "[unity.sh] smoke-webgl: no build at Builds/WebGL (run build-webgl)"; exit 1; }
    BOARD_MIN="${SMOKE_BOARD_MIN:-500}"; SMOKE_BG="FF00FF"
    # Every workspace runs this on the same PC: take a free port so we never talk to another workspace's server
    # (a busy fixed port made this check stop silently, or test another workspace's build).
    port=""; for p in $(seq 8091 8190); do netstat -ano | grep -q "[:.]$p .*LISTENING" || { port=$p; break; }; done
    [ -n "$port" ] || { echo "[unity.sh] smoke-webgl: no free port in 8091-8190"; exit 1; }
    node "$HERE/serve.mjs" "$port" "$REPO/Builds/WebGL" > "$out/serve.log" 2>&1 &
    srv=$!; page=""
    # Always stop our server and close our tab, whatever happens below.
    trap 'kill "$srv" 2>/dev/null || true; [ -n "$page" ] && orca tab close --page "$page" --json >/dev/null 2>&1 || true' EXIT
    sleep 2
    kill -0 "$srv" 2>/dev/null || { echo "[unity.sh] smoke-webgl: server did not start on port $port:"; tail -5 "$out/serve.log"; exit 1; }
    echo "[unity.sh] smoke-webgl: serving Builds/WebGL on port $port (board threshold $BOARD_MIN)"
    # shoot <name> <query>: one page load -> $out/<name>.png + .console.txt; sets px (pixels unlike magenta) and errs.
    shoot() {
      local name="$1" query="$2" shot
      px=""; errs=0
      page="$(orca tab create --url "http://localhost:$port/?$query" --json 2>/dev/null | grep -o '"browserPageId": "[^"]*"' | head -1 | cut -d'"' -f4 || true)"
      [ -z "$page" ] && { echo "[unity.sh] smoke-webgl $name: could not open a browser tab (orca tab create failed or timed out)"; fail=1; return 1; }
      orca capture start --page "$page" --json > /dev/null 2>&1 || true
      orca wait --page "$page" --load load --json > /dev/null 2>&1 || true
      sleep 25
      sout="$(orca screenshot --page "$page" 2>&1 || true)"
      shot="$(printf '%s\n' "$sout" | grep -oE '[A-Za-z]:\\[^ ]+\.png' | head -1 || true)"
      orca console --page "$page" --limit 400 > "$out/$name.console.txt" 2>&1 || true
      orca tab close --page "$page" --json > /dev/null 2>&1 || true; page=""
      if [ -z "$shot" ]; then
        echo "[unity.sh] smoke-webgl $name: no screenshot: $(printf '%s\n' "$sout" | head -1)"
        # The Orca window is minimized or shows another workspace: this workspace's tab never paints a frame.
        if printf '%s' "$sout" | grep -q "draw a frame"; then echo "[unity.sh]   show the Orca window on this workspace (not minimized) and run again"; fi
        fail=1; return 1
      fi
      cp "$(cygpath -u "$shot")" "$out/$name.png"
      px="$(powershell -NoProfile -ExecutionPolicy Bypass -File "$(cygpath -w "$HERE/board-pixels.ps1")" -Path "$(cygpath -w "$out/$name.png")" -Background "$SMOKE_BG" | tr -d '\r')"
      errs="$(grep -ciE '^\[error\]|Exception' "$out/$name.console.txt" || true)"
      if [ "${errs:-0}" -gt 0 ]; then echo "[unity.sh] smoke-webgl $name: console errors $errs"; grep -iE '^\[error\]|Exception' "$out/$name.console.txt" | head -5; fail=1; fi
      return 0
    }
    for spec in "1:R" "29:DURRRRRKDDR" "50:UUUDURUK"; do
      lv="${spec%%:*}"; rp="${spec#*:}"
      shoot "level$lv" "level=$lv&replay=$rp" || true
      shoot "level$lv.board" "level=$lv&replay=$rp&smoke=board" || continue
      echo "[unity.sh] smoke-webgl level $lv: board pixels $px (>= $BOARD_MIN)"
      if [ "${px:-0}" -lt "$BOARD_MIN" ]; then echo "[unity.sh]   board not drawn -> see $out/level$lv.board.png"; fail=1; fi
    done
    # Negative control: same stage with the board hidden must read as "no board", or the check proves nothing.
    if shoot "control.empty" "level=1&replay=R&smoke=empty"; then
      echo "[unity.sh] smoke-webgl control (board hidden): pixels $px (< $BOARD_MIN)"
      if [ "${px:-0}" -ge "$BOARD_MIN" ]; then echo "[unity.sh]   the board check cannot tell a missing board (background not magenta? -> $out/control.empty.png)"; fail=1; fi
    fi
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
