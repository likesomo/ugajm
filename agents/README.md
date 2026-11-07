# 일곱 걸음 (Seven Steps) 에이전트 팀

이 폴더는 일곱 걸음 Unity 개발에 쓰는 에이전트들의 역할 정의다. 각 파일은 해당 에이전트를 띄울 때 첫 지시(브리프)로 그대로 쓴다.
사용자가 이 문서를 확인한 뒤에 에이전트를 띄운다.

## 팀 구성

| 파일 | 에이전트 | 엔진 | 브랜치 | 띄우는 방법 |
|---|---|---|---|---|
| [lead.md](lead.md) | 리드 (PM/통합) | Claude | `7lucky` | 이미 실행 중인 7lucky 에이전트에 역할 전달 |
| [architect.md](architect.md) | Unity 아키텍트 | Claude | `feat/architecture` | Orca 작업 공간 신규 생성 |
| [core.md](core.md) | 코어 프로그래머 | Claude | `feat/core` | Orca 작업 공간 신규 생성 |
| [gameplay-ui.md](gameplay-ui.md) | 게임플레이/UI 프로그래머 | Claude | `feat/view` | Orca 작업 공간 신규 생성 |
| [art.md](art.md) | 아트/리소스 | **Codex (GPT)** | `feat/art` | **Orca orchestration 워커** (결과를 리드에게 보고) |
| [level-design.md](level-design.md) | 레벨 디자이너 | Claude | `feat/levels` | Orca 작업 공간 신규 생성 |

`number-chemical` 에이전트는 종료한다. 작업 공간과 브랜치는 남긴다.

## 현재 상태 (2026-11-07 기준)

사용자 지시로 리드(7lucky 에이전트)가 첫 플레이 가능 버전(v0)을 만드는 중이다. 이 v0가 모든 에이전트의 출발점이다.

- `SevenSteps.Core` (asmdef, `noEngineReferences`): `Rules.cs`, `State.cs`, `Level.cs`, `Solver.cs`. `core.js`를 옮긴 것
- `SevenSteps.Game` (asmdef): `GameController`, `BoardView`, `HudView`, `Ui`, `GameAssets`. UI를 코드로 생성
- 데이터: `unity/Assets/_Project/Data/levels.json` (`unity/Tools/export-levels.mjs`로 `levels.js`에서 변환)
- 효과음: `unity/Tools/gen-sfx.mjs`로 생성한 wav 11개, 폰트: `ThirdParty/Jua` (OFL)

**v0가 `7lucky`에 커밋·푸시된 뒤에** 다른 에이전트의 작업 공간을 만든다. 그래야 모두 같은 코드에서 시작한다.

## 진행 단계

| 단계 | 누가 | 끝나는 조건 |
|---|---|---|
| 0. v0 완성 | 리드 | `7lucky`에서 탑다운으로 12스테이지 플레이 가능, CompileCheck 통과, 푸시 |
| 1. 구조 정리 | 아키텍트 (리드는 규칙 확정 병행) | `docs/architecture.md`와 구조 리팩터가 `7lucky`에 합쳐짐. 코어 공개 API와 프리팹 매핑 방식 고정 |
| 2. 병렬 개발 | 코어, 게임플레이/UI, 아트, 레벨 디자인 | 각자 브랜치에서 작업, 아키텍트 검토 후 리드가 합침 |
| 3. 다듬기 | 전원 | 3D 쿼터뷰 전환, 연출, 13~15스테이지, 사람 플레이테스트, itch.io 업로드 |

1단계 동안 아트 에이전트는 Asset Store 후보 조사와 GPT 생성 시안 작업만 하고 프로젝트에는 아직 넣지 않는다.
레벨 디자이너는 웹 프로토타입만으로 작업할 수 있으므로 v0 푸시 직후 바로 시작한다 (흥미도 곡선, 기믹 후보).

### 새 기믹 흐름

레벨 디자이너가 흥미도 곡선에서 떨어지는 지점을 찾아 기믹 제안 → 리드 승인 → 코어가 규칙 구현(JS + C#, 테스트) → 게임플레이/UI·아트가 표시와 리소스 → 레벨 디자이너가 스테이지 제작.

## 파일 소유권 (충돌 방지 규칙)

Unity 씬·프리팹은 git으로 합치기 어렵다. **한 파일은 한 에이전트만 수정한다.** 다른 사람 소유 파일을 고쳐야 하면 소유자에게 요청한다.

| 경로 (`unity/` 기준) | 소유자 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Core/**`, `Assets/_Project/Tests/**`, `prototypes/seven-steps/src/core.js` | 코어 |
| `Assets/_Project/Data/levels.json`, `prototypes/seven-steps/src/levels.js`, `design/levels/**`, `prototypes/seven-steps/experiments/**` | 레벨 디자인 |
| `Assets/_Project/Scripts/Runtime/Game/**`, `Assets/_Project/Scenes/**`, `Assets/_Project/UI/**` (UI 프리팹) | 게임플레이/UI |
| `Assets/_Project/Art/**`, `Assets/_Project/Audio/**`, `Assets/_Project/Prefabs/Tiles/**`, `Assets/ThirdParty/**`, `Assets/Settings/*RPAsset*` | 아트 |
| `*.asmdef`, `docs/architecture.md`, 공용 인터페이스(아키텍트가 지정한 파일), `Packages/manifest.json` | 아키텍트 |
| `CLAUDE.md`, `design/**`, `agents/**`, `ProjectSettings/EditorBuildSettings.asset`, `Assets/_Project/Scripts/Editor/BuildTools.cs` | 리드 |

`Prefabs/Generated/**`는 v0에서 리드가 만든 임시 프리팹이다. 최종 타일 프리팹은 아트가 `Prefabs/Tiles/**`에 만들고, 아키텍트의 `TileSet` 매핑으로 교체한다.

패키지 추가는 아키텍트에게 요청한다 (manifest.json 충돌 방지).

## API·인터페이스 변경 고지

공용 API(코어 명령·상태·이벤트, `TileSet`, 레벨 데이터 형식)를 바꾸는 에이전트는 `docs/architecture.md`의 변경 기록에 날짜와 내용을 적고, 리드에게 보고한다. 리드는 영향받는 에이전트에게 알린다. 고지 없이 공용 API를 바꾸지 않는다.

## 브랜치와 합치기

- 통합 브랜치: `7lucky`. 기능 브랜치는 항상 최신 `7lucky`에서 만들고, 작업 단위가 끝나면 `7lucky`를 받아 충돌을 먼저 해결한다.
- 합치기 전 확인: 에디터를 닫은 상태로 `BuildTools.CompileCheck` 통과, `.meta` 포함, `reports/`·`Builds/`·`Library/` 미포함.
- 아키텍트가 코드 검토 → 리드가 `7lucky`에 합치고 푸시.
- 커밋 메시지는 영어 한 줄 요약 + 본문. 기능 브랜치는 각 에이전트가 원격에 푸시해도 된다. `7lucky` 푸시는 리드만 한다.

## 공통 환경

- Unity 6000.6.8f1, URP 3D, Windows + WebGL 모듈. 배치 명령은 `CLAUDE.md` 참고
- Node.js: `export PATH="/c/Program Files/nodejs:$PATH"` (오래된 셸에는 PATH가 없음)
- 작업 공간마다 `unity/Library`를 새로 만든다. 첫 열기·첫 배치 실행은 몇 분 걸리고 디스크를 1~2GB 쓴다
- 한 Unity 프로젝트 폴더는 한 프로세스만 열 수 있다. 배치 명령 전에 그 작업 공간의 에디터를 닫는다

## 아직 정하지 않은 것 (사용자 결정 필요)

1. **Asset Store 에셋과 공개 저장소**: `likesomo/ugajm`은 공개 저장소다. Asset Store 에셋 원본은 공개 저장소에 올리면 안 된다.
   저장소를 비공개로 바꾸거나(권장), Asset Store 에셋을 git에서 제외해야 한다. 정해질 때까지 아트 에이전트는 Asset Store 에셋을 커밋하지 않는다.
2. **유료 에셋 예산**: 무료만 쓸지, 유료를 포함한다면 한도는 얼마인지.
3. **Asset Store 다운로드**: 에이전트는 Unity 계정에 로그인할 수 없다. 아트 에이전트가 후보 목록을 주면 사용자가 내 에셋에 추가하고 Package Manager에서 다운로드한다.
4. **GPT 이미지 생성 권한**: Codex 워커 시작 직후 이미지 생성이 되는지 확인한다. 안 되면 사용자가 Codex에 로그인한다.
