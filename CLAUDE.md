# Seven Steps (브랜치 `7lucky`)

게임잼 후보 게임. 기획과 웹 프로토타입을 바탕으로 Unity 버전을 개발하는 브랜치다. 이 브랜치의 작업은 이 브랜치에서만 하고, `number-chemical` 브랜치와 섞지 않는다.

## 참고 자료
- 기획: `design/gdd/seven-steps-concept.md`
- 아트/에셋: `design/art/seven-steps-asset-needs.md`, `design/art/seven-steps-asset-research.md` (Unity 6 + URP, WebGL → itch.io 전제, KayKit Dungeon Remastered 등)
- 웹 프로토타입(규칙의 기준 구현): `prototypes/seven-steps/` — `src/core.js`(규칙), `src/levels.js`(레벨), `tools/solve.mjs`(솔버)
- Unity로 옮길 때 규칙 로직은 `core.js`와 같은 결과가 나오도록 순수 C# 클래스로 분리하고(MonoBehaviour 의존 없이), 레벨 데이터는 `levels.js`에서 옮긴다

## 프로젝트 템플릿
- URP 3D (Universal 3D blank). 렌더 파이프라인 에셋: `unity/Assets/Settings/PC_RPAsset.asset`, `Mobile_RPAsset.asset`
- 시작 씬: `unity/Assets/Scenes/SampleScene.unity`

## Unity 환경
- 에디터: **Unity 6000.6.8f1** (`C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe`)
- 프로젝트 경로: `unity/` (Unity Hub에서 Add → 이 폴더 선택)
- 설치된 빌드 모듈: Windows Standalone, WebGL. 빈 프로젝트 기준 WebGL 배치 빌드 성공 확인(약 10MB)
- 입력: Input System (`InputSystem_Actions.inputactions`), UI: uGUI

## 폴더 규칙
- 게임 코드와 텍스트 자산(스크립트·씬·프리팹·머티리얼·ScriptableObject)은 `unity/Assets/_Project/` 아래에 둔다: `Scripts/Runtime`, `Scripts/Editor`, `Prefabs`, `Art`, `Data`, `Scenes`, `UI`
- **바이너리 에셋은 git에 넣지 않는다** (모델, 텍스처·이미지, 오디오, 폰트, 외부 패키지). 공유 폴더 `C:/unityjam/shared-assets/seven-steps/Assets/`에 `.meta`와 함께 두고, 작업 공간의 `unity/Assets/Shared`가 정션으로 가리킨다. 무료 에셋만 쓰고, 추가·수정은 아트만 한다. 크레딧은 `design/art/CREDITS.md`. 자세한 규칙은 `agents/README.md` "에셋 규칙"
- **작업 공간을 처음 받으면** 에디터를 닫고 `powershell -ExecutionPolicy Bypass -File tools/link-shared-assets.ps1` 실행 (정션 생성, 다시 실행해도 안전). 정션이 없으면 글자·효과음이 비고 `setup`이 실패한다
- 새 파일은 반드시 `.meta`와 함께 커밋한다. `.meta`가 없으면 참조가 깨진다
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `/Builds/`, `/reports/`, `unity/Assets/Shared/`(+`Shared.meta`)는 커밋하지 않는다 (`.gitignore` 처리됨)

## Git
- `.gitattributes`의 LFS 규칙은 남아 있지만, 바이너리 에셋은 이제 커밋하지 않는다. 커밋 전에 `git status`에 이미지·오디오·폰트·모델이 없는지 확인
- 씬/프리팹(YAML) 충돌은 UnityYAMLMerge 드라이버가 처리하도록 로컬 git 설정이 되어 있다
- 같은 씬 파일을 여러 사람/에이전트가 동시에 고치지 않는다. 작업 단위를 프리팹으로 나눈다

## 배치 명령 (에디터가 같은 프로젝트를 열고 있으면 실패함. 먼저 에디터를 닫을 것)
```bash
U="/c/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Unity.exe"
# 컴파일 확인 (exit 0 = OK, 로그에 "[BuildTools] Compile OK.")
"$U" -batchmode -nographics -projectPath unity -executeMethod BuildTools.CompileCheck -logFile -
# 윈도우 빌드 → /Builds/Windows/
"$U" -batchmode -nographics -projectPath unity -executeMethod BuildTools.BuildWindows -logFile -
# WebGL 빌드 → /Builds/WebGL/
"$U" -batchmode -nographics -projectPath unity -executeMethod BuildTools.BuildWebGL -logFile -
```
빌드 대상 씬은 Build Settings의 활성 씬 목록을 따른다. 빌드 스크립트: `unity/Assets/_Project/Scripts/Editor/BuildTools.cs`

## 실행 방식 (`unity/Tools/unity.sh`, Git Bash)
위 배치 명령을 감싼 스크립트. 배치 명령은 에디터를 닫고 실행한다. 로그는 `unity/Logs/batch-<명령>.log`.
```bash
unity/Tools/unity.sh levels       # levels.js + JS 솔버 답 → Assets/_Project/Data/levels.json (스테이지를 바꾸면 실행)
unity/Tools/unity.sh sfx          # 임시 효과음 11개 합성 → Assets/Shared/Audio/*.wav (공유 폴더, 아트 담당)
unity/Tools/unity.sh setup        # 머티리얼·프리팹·GameAssets 생성, Game 씬(없을 때만), 빌드/플레이어 설정
unity/Tools/unity.sh golden       # 규칙 골든값(core.js → Tests/Golden) + 텔레메트리 골든값(server/fixtures) 다시 생성. core.js 나 스테이지를 바꾸면 실행 후 커밋
unity/Tools/unity.sh test         # EditMode 테스트 (Tests/EditMode) → reports/editmode-results.xml
unity/Tools/unity.sh tileset      # 임시 TileSet 다시 생성 (Data/TileSets/TopDown_Placeholder.asset)
unity/Tools/unity.sh verify       # C# 규칙이 JS 솔버와 같은 답인지 12스테이지 검사 → reports/seven-steps-unity-verify.json
unity/Tools/unity.sh build-win    # → Builds/Windows/Seven Steps.exe      (play 로 실행)
unity/Tools/unity.sh smoke        # 윈도우 빌드를 리플레이 4개로 실행, 스크린샷+로그 → reports/smoke/, 예외 있으면 실패
unity/Tools/unity.sh build-webgl  # → Builds/WebGL/                         (serve 로 http://localhost:8090)
unity/Tools/unity.sh all          # levels + setup + verify + build-win + smoke
```
- 에디터: `unity/Tools/unity.sh editor` → 메뉴 **Seven Steps > Open Game Scene** → Play. `Game` 오브젝트의 `startLevel`/`replay`로 시작 스테이지 지정
- 같은 재현 형식: 데스크톱 `"Seven Steps.exe" -level 8 -replay LRRRRDDK`, WebGL `?level=8&replay=LRRRRDDK` (U D L R 이동, K KEEP, X SWITCH)
- 조작: 방향키/WASD/옆 칸 클릭, K KEEP, X SWITCH, Z 되돌리기, R 재시작, H 풀이, `[` `]` 스테이지, Enter 다음
- Node.js: `C:\Program Files\nodejs` (스크립트가 PATH에 추가함)

## 코드 구조
구조와 공용 API의 기준 문서는 `docs/architecture.md` (아키텍트). 아래는 v0 기준 요약이고, 2단계에서 `SevenSteps.Contracts`(TileSet, GameSession, AppFlow, Services, SaveSystem, ITelemetry)를 쓰도록 바뀐다.

### v0
- `Scripts/Runtime/Core` (`SevenSteps.Core`, `noEngineReferences`): `Level`, `State`, `Rules`(core.js 이식, `Dispatch` 하나로 상태 변경), `Solver`(BFS). 규칙의 기준은 웹 프로토타입 코드이고, 기획 문서와 다른 점(구덩이·금 간 바닥·발판)은 코드 쪽을 따른다
- `Scripts/Runtime/Game` (`SevenSteps.Game`): `GameController`(입력·사운드·저장·흐름), `BoardView`(상태를 읽어 그리드 표시·트윈), `HudView`/`Ui`(uGUI를 코드로 생성), `GameAssets`(모든 에셋 참조 ScriptableObject)
- `Scripts/Editor` (`SevenSteps.Editor`): `ProjectSetup`(에셋·씬 생성), `LevelVerifier`(C#↔JS 일치 검사), `BuildTools`
- 에셋: `Art/Materials`·`Prefabs/Generated`는 `ProjectSetup`이 만든 임시 아트(URP 프리미티브). 직접 고치지 말고 `ProjectSetup.cs`를 고친 뒤 `setup`. 실제 아트로 바꿀 때는 `Data/GameAssets.asset`의 프리팹 참조를 교체한다(타일 윗면 y=0, 1칸=1유닛)
- 폰트: `Assets/Shared/Fonts/Jua-Regular.ttf` (SIL OFL, 레거시 uGUI Text용 동적 폰트). 효과음: `Assets/Shared/Audio/*.wav` (`unity/Tools/gen-sfx.mjs`로 만든 임시음)
- **URP 설정 손상 (Unity 6000.6 알려진 현상)**: 배치 실행(특히 새 작업 공간의 첫 실행)이 `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`의 `m_RuntimeSettings`를 비우고 SSAO 리소스 id를 새로 만든다. 그 상태로 빌드하면 플레이어에서 `ScreenSpaceAmbientOcclusion`/`Blitter` 예외가 난다. `unity.sh`는 배치 명령 뒤에 이를 감지해 커밋된 파일로 되돌리고, 빌드 명령이면 한 번 더 빌드한다. Unity.exe를 직접 실행했다면 `git checkout -- <그 파일>`. **이 파일의 변경은 커밋하지 않는다** (`smoke`가 예외를 잡아낸다)
