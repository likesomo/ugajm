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
| [scene-flow.md](scene-flow.md) | 씬 플로우 엔지니어 (씬 구조·화면 흐름) | Claude | `feat/scene-flow` | Orca 작업 공간 `scene-flow` (2026-11-08 추가) |
| [backend-analytics.md](backend-analytics.md) | 백엔드/분석 (수집 서버·대시보드) | Claude | `feat/server` | Orca 작업 공간 `server` |

`number-chemical` 에이전트는 종료한다. 작업 공간과 브랜치는 남긴다.

## 현재 상태 (2026-11-07 기준)

사용자 지시로 리드(7lucky 에이전트)가 첫 플레이 가능 버전(v0)을 만드는 중이다. 이 v0가 모든 에이전트의 출발점이다.

- `SevenSteps.Core` (asmdef, `noEngineReferences`): `Rules.cs`, `State.cs`, `Level.cs`, `Solver.cs`. `core.js`를 옮긴 것
- `SevenSteps.Game` (asmdef): `GameController`, `BoardView`, `HudView`, `Ui`, `GameAssets`. UI를 코드로 생성
- 데이터: `unity/Assets/_Project/Data/levels.json` (`unity/Tools/export-levels.mjs`로 `levels.js`에서 변환)
- 효과음: `unity/Tools/gen-sfx.mjs`로 생성한 wav 11개 → `Assets/Shared/Audio`, 폰트: Jua (OFL) → `Assets/Shared/Fonts` (둘 다 git 밖 공유 폴더, 아래 "에셋 규칙")

**v0가 `7lucky`에 커밋·푸시된 뒤에** 다른 에이전트의 작업 공간을 만든다. 그래야 모두 같은 코드에서 시작한다.

## 진행 단계

| 단계 | 누가 | 끝나는 조건 |
|---|---|---|
| 0. v0 완성 | 리드 | `7lucky`에서 탑다운으로 12스테이지 플레이 가능, CompileCheck 통과, 푸시 |
| 1. 구조 정리 | 아키텍트 (리드는 규칙 확정 병행) | `docs/architecture.md`와 구조 리팩터가 `7lucky`에 합쳐짐. 코어 공개 API와 프리팹 매핑 방식 고정 |
| 2. 병렬 개발 | 코어, 게임플레이/UI, 아트, 레벨 디자인, 백엔드/분석 | 각자 브랜치에서 작업, 아키텍트 검토 후 리드가 합침 |
| 2.5 정리 | 리드 | 모든 기능 브랜치의 미합류 커밋이 `7lucky`에 모이고 WebGL 빌드가 브라우저에서 정상 동작 |
| 3. 플레이테스트 1차 | 리드, 레벨 디자인, 백엔드/분석 | Unity WebGL로 5명 이상 테스트, 대시보드로 흥미도 모델 보정 (`production/playtest-1-prep.md`). 원격 공개는 사용자 승인 후 |
| 4. 아트 확정 (2.5 다음에 바로 시작, 3과 병행) | 아트(Codex), 아키텍트, 게임플레이/UI, 씬 플로우 | 확정 테마 세트가 정식 `AppConfig.tileSet`으로 바뀌고, 세쌍둥이 3색·KEEP 선돌·챕터(행성) 프리셋이 Unity에 적용됨. WebGL 스모크 통과 |
| 5. 50스테이지·다듬기 | 전원 | 몸 3개 기준 50스테이지(6~8챕터), 챕터 전환 연출, 쿼터뷰, itch.io 업로드(사용자 승인) |

1단계 동안 아트 에이전트는 무료 에셋 후보 조사와 GPT 생성 시안 작업만 하고 프로젝트에는 아직 넣지 않는다.
레벨 디자이너는 웹 프로토타입만으로 작업할 수 있으므로 v0 푸시 직후 바로 시작한다 (흥미도 곡선, 기믹 후보).

### 새 기믹 흐름

레벨 디자이너가 흥미도 곡선에서 떨어지는 지점을 찾아 기믹 제안 → 리드 승인 → 코어가 규칙 구현(JS + C#, 테스트) → 게임플레이/UI·아트가 표시와 리소스 → 레벨 디자이너가 스테이지 제작.

### 플레이 분석 흐름

백엔드/분석이 이벤트 스키마(`docs/telemetry.md`)를 고정하고 리드·아키텍트에게 알림 → 아키텍트가 Unity 쪽 `ITelemetry` 인터페이스와 서버 주소 설정을 프로그램 기본 틀(Boot→Title→StageSelect→Game→Result, 저장, 설정, 서비스 등록)에 넣음 → 리드가 배분: 게임플레이/UI는 Unity 전송 구현(배치, 재시도, WebGL 페이지 종료 시 `sendBeacon` jslib), 레벨 디자이너는 대시보드 CSV/JSON으로 흥미도 모델 보정. 웹 프로토타입 연동(`telemetry.js`)은 백엔드/분석이 먼저 해서 바로 플레이테스트에 쓴다. 서버의 외부 공개(배포)는 사용자 결정.

## 아트 방향 (확정, 2026-11-08~09 사용자 결정)

**기준 문서는 `design/art/art-bible.md`다.** 아트와 관련된 판단은 모두 이 문서를 따르고, 과거 테마 시안(북두칠성 퓨전 23861f4 등)은 폐기된 탐색 기록이다.

- 테마: 이끼 낀 유적 × 선돌(스톤헨지 서사) × 동양 석재·목재 블록. 챕터 = 우주 속 작은 행성, 챕터마다 컨셉이 바뀐다
- 주인공: 선돌 원형 제단을 찾아가는 세쌍둥이 방랑자, A안(같은 실루엣, 머플러 1번 청록 #568C88 / 2번 황토 #B99A57 / 3번 적갈 #B96C59). KEEP한 몸 = 중성 화강암 선돌
- 몸 상한 3, 전체 50스테이지 (`design/gdd/seven-steps-concept.md`)
- **시안과 아트 제작은 아트(Codex 워커)만 한다.** Claude 에이전트는 이미지·아트 디자인을 만들지 않는다. 아트의 Unity 작업 결과는 코디네이터가 받아 리드에게 넘긴다

## 파일 소유권 (충돌 방지 규칙)

Unity 씬·프리팹은 git으로 합치기 어렵다. **한 파일은 한 에이전트만 수정한다.** 다른 사람 소유 파일을 고쳐야 하면 소유자에게 요청한다.

| 경로 (`unity/` 기준) | 소유자 |
|---|---|
| `Assets/_Project/Scripts/Runtime/Core/**`, `Assets/_Project/Tests/**`, `prototypes/seven-steps/src/core.js` | 코어 |
| `Assets/_Project/Data/levels.json`, `prototypes/seven-steps/src/levels.js`, `design/levels/**`, `prototypes/seven-steps/experiments/**`, 웹 프로토타입 화면(`prototypes/seven-steps/src/main.js`·`style.css`, 텔레메트리 훅 제외) | 레벨 디자인 |
| `Assets/_Project/Scripts/Runtime/Game/**` (아래 씬 플로우 영역 제외. 화면 내용 `BoardView`·`HudView`·패널 요소는 게임플레이/UI), `Assets/_Project/UI/**` (UI 프리팹) | 게임플레이/UI |
| `Assets/_Project/Scenes/**`, `Assets/_Project/Scripts/Runtime/Game/App/**`, 새 흐름·전환 코드, `docs/scene-flow.md` (2026-11-08부터) | 씬 플로우 |
| `Assets/Shared/**` (git 밖 공유 폴더, **아트만 추가·수정**), `Assets/_Project/Art/**`, `Assets/_Project/Prefabs/Tiles/**`, `Assets/Settings/*RPAsset*`, `design/art/**` (`design/art/CREDITS.md` 포함) | 아트 |
| `*.asmdef`, `docs/architecture.md`, 공용 인터페이스(아키텍트가 지정한 파일), `Packages/manifest.json` | 아키텍트 |
| `server/**`, `docs/telemetry.md`, `prototypes/seven-steps/src/telemetry.js`, `prototypes/seven-steps/src/main.js`의 텔레메트리 훅 부분 (main.js의 나머지는 건드리지 않는다) | 백엔드/분석 |
| `CLAUDE.md`, `design/**`, `agents/**`, `ProjectSettings/EditorBuildSettings.asset`, `Assets/_Project/Scripts/Editor/BuildTools.cs` | 리드 |

`Prefabs/Generated/**`는 v0에서 리드가 만든 임시 프리팹이다. 최종 타일 프리팹은 아트가 `Prefabs/Tiles/**`에 만들고, 아키텍트의 `TileSet` 매핑으로 교체한다.

패키지 추가는 아키텍트에게 요청한다 (manifest.json 충돌 방지).

## API·인터페이스 변경 고지

공용 API(코어 명령·상태·이벤트, `TileSet`, 레벨 데이터 형식)를 바꾸는 에이전트는 `docs/architecture.md`의 변경 기록에 날짜와 내용을 적고, 리드에게 보고한다. 리드는 영향받는 에이전트에게 알린다. 고지 없이 공용 API를 바꾸지 않는다.

## 브랜치와 합치기

- 통합 브랜치: `7lucky`. 기능 브랜치는 항상 최신 `7lucky`에서 만들고, 작업 단위가 끝나면 `7lucky`를 받아 충돌을 먼저 해결한다.
- 합치기 전 확인: 에디터를 닫은 상태로 `BuildTools.CompileCheck` 통과, `.meta` 포함, `reports/`·`Builds/`·`Library/`·바이너리 에셋 미포함.
- 아키텍트가 코드 검토 → 리드가 `7lucky`에 합치고 푸시.
- 커밋 메시지는 영어 한 줄 요약 + 본문. 기능 브랜치는 각 에이전트가 원격에 푸시해도 된다. `7lucky` 푸시는 리드만 한다.

## 공통 환경

- Unity 6000.6.8f1, URP 3D, Windows + WebGL 모듈. 배치 명령은 `CLAUDE.md` 참고
- Node.js: `export PATH="/c/Program Files/nodejs:$PATH"` (오래된 셸에는 PATH가 없음)
- 작업 공간마다 `unity/Library`를 새로 만든다. 첫 열기·첫 배치 실행은 몇 분 걸리고 디스크를 1~2GB 쓴다
- 한 Unity 프로젝트 폴더는 한 프로세스만 열 수 있다. 배치 명령 전에 그 작업 공간의 에디터를 닫는다

## 에셋 규칙 (2026-11-07 사용자 결정)

1. **무료 에셋만 쓴다.** 유료 에셋은 후보 목록에도 넣지 않는다. 라이선스는 상업적 이용·WebGL 배포가 가능해야 한다 (CC0 > Unity Asset Store 무료 > CC-BY·OFL처럼 표기 의무가 있는 것).
2. **에셋 파일은 git에 커밋하지 않는다.** 여기서 에셋은 바이너리 리소스 전부다: 모델, 텍스처·이미지(GPT 생성 포함), 오디오, 폰트, 외부 패키지. 스크립트·씬·프리팹·머티리얼·ScriptableObject와 그 `.meta`는 계속 커밋한다.
3. **공유 에셋 폴더**: `C:/unityjam/shared-assets/seven-steps/Assets/` (git 밖, 이 PC 한 곳). 각 작업 공간의 `unity/Assets/Shared`가 디렉터리 정션으로 이 폴더를 가리킨다. 작업 공간을 처음 받거나 새로 만들면 에디터를 닫고 다음을 실행한다:
   `powershell -ExecutionPolicy Bypass -File tools/link-shared-assets.ps1`
   정션이 없으면 폰트·효과음 참조가 비어 화면에 글자가 안 나오고 `setup`이 실패한다.
4. **`.meta`는 에셋과 함께 공유 폴더에 둔다.** GUID가 모든 작업 공간에서 같아야 프리팹·`GameAssets` 참조가 유지된다. 공유 폴더 안에서 파일을 옮기거나 이름을 바꿀 때는 `.meta`도 같이 옮긴다 (Unity 에디터 안에서 옮기면 자동).
5. **추가·수정은 아트만 한다.** 다른 에이전트는 읽기만 한다. 모든 작업 공간이 같은 폴더를 보므로 아트가 바꾸면 즉시 모두에게 반영된다. 기존 파일의 GUID를 바꾸거나 지우기 전에 리드에게 알린다.
6. **Asset Store 무료 에셋**: 에이전트는 Unity 계정에 로그인할 수 없다. 아트가 후보 목록을 주면 사용자가 내 에셋에 추가하고 Package Manager에서 받는다. 가져온 폴더는 Unity 에디터 안에서 `Assets/Shared/ThirdParty/<팩이름>/`으로 옮긴다 (GUID 유지). UPM 레지스트리 패키지(예: 트윈 라이브러리)는 `manifest.json` 참조만 커밋되므로 아키텍트에게 요청한다.
7. **크레딧**: 쓰는 에셋마다 `design/art/CREDITS.md`에 이름·작가·URL·라이선스·공유 폴더 경로를 적는다 (텍스트라 커밋한다).
8. **GPT 이미지 생성 권한**: Codex 워커 시작 직후 이미지 생성이 되는지 확인한다. 안 되면 사용자가 Codex에 로그인한다.
