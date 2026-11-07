# 숫자 연금술사 / Lucky Number Alchemist (브랜치 `number-chemical`)

게임잼 후보 게임. 기획과 웹 프로토타입 v2를 바탕으로 Unity 버전을 개발하는 브랜치다. 이 브랜치의 작업은 이 브랜치에서만 하고, `7lucky` 브랜치와 섞지 않는다.

## 참고 자료
- 기획: `design/gdd/game-concept.md` (v2), 로그라이트 검토 `design/gdd/roguelike-progression-review.md`
- v1 폐기 사유: `production/playtests/2026-11-06-v1-13floors.md`, 보관본 `design/gdd/archive/`, `prototypes/v1-13floors/`
- 웹 프로토타입 v2(규칙의 기준 구현): 루트의 `index.html`, `src/core.js`(규칙), `src/data.js`(데이터), `src/rng.js`(시드 RNG), `tools/sim.mjs`(시뮬레이션)
- Unity로 옮길 때 규칙 로직은 `core.js`와 같은 결과가 나오도록 순수 C# 클래스로 분리하고(MonoBehaviour 의존 없이), 시드 RNG도 같은 시퀀스를 내도록 옮긴다. 데이터는 `data.js`에서 ScriptableObject로 옮긴다

## 프로젝트 템플릿
- URP 2D (2D cross-platform). 렌더 파이프라인 에셋: `unity/Assets/Settings/UniversalRP.asset`, `Renderer2D.asset`
- 시작 씬: `unity/Assets/Scenes/SampleScene.unity`
- 포함 패키지: 2D Sprite/Tilemap/Animation/Aseprite/PSD Importer, Test Framework, Timeline

## Unity 환경
- 에디터: **Unity 6000.6.8f1** (`C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe`)
- 프로젝트 경로: `unity/` (Unity Hub에서 Add → 이 폴더 선택)
- 설치된 빌드 모듈: Windows Standalone, WebGL. 빈 프로젝트 기준 WebGL 배치 빌드 성공 확인(약 10MB)
- 입력: Input System (`InputSystem_Actions.inputactions`), UI: uGUI

## 폴더 규칙
- 게임 코드/에셋은 `unity/Assets/_Project/` 아래에 둔다: `Scripts/Runtime`, `Scripts/Editor`, `Prefabs`, `Art`, `Audio`, `UI`
- 외부 에셋 팩은 `unity/Assets/ThirdParty/<팩이름>/`에 두고 라이선스 파일을 함께 커밋한다
- 새 파일은 반드시 `.meta`와 함께 커밋한다. `.meta`가 없으면 참조가 깨진다
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `/Builds/`는 커밋하지 않는다 (`.gitignore` 처리됨)

## Git
- 이미지·모델·오디오·폰트는 Git LFS로 관리된다 (`.gitattributes`). 커밋 전에 `git lfs status`로 확인
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
