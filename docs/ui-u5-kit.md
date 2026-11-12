# U5 UI 키트 계약

담당: 아키텍트(이 문서, `UiStyleToggle`). 구현: gameplay-ui(키트, 게임 화면). 사용: scene-flow(Title·지도·표면·Boot·엔딩). 2026-11-12.
- 근거:
  - 사용자 결정 `C:/unityjam/shared-assets/seven-steps/reports/user-decisions-2026-11-12.md` 1번
  - 리드 계획 `production/plan-2026-11-12.md` 2장
  - 시안 `C:/unityjam/shared-assets/seven-steps/concepts/ui/ui-redesign-2026-11-12/`(EVAL.md "U5", U5-sheet, U5-flow-sheet, `touch-targets.csv`)
- `ui-concepts` 브랜치 코드는 검토용이다. 옮겨 오지 않는다.
- 이 문서와 `docs/architecture.md` 14장(UI 레이아웃 규칙)이 다르면 14장이 우선한다. 다른 점은 6장에 적었다.

## 1. 키트 구성 (gameplay-ui가 먼저 만든다, scene-flow는 따로 만들지 않는다)

위치: `Scripts/Runtime/Game/UI/U5/`, 네임스페이스 `SevenSteps.Game.UI.U5`. 새 asmdef를 만들지 않는다(`SevenSteps.Game`).

| 부품 | 크기 (1280×720 캔버스 단위) | 하는 일 |
|---|---|---|
| `IconButton` | 누르는 영역 **48×48** (그림은 40 안쪽 권장) | 아이콘 하나 + `Clicked` 이벤트. 보이는 글자 없음. 이름(설명)은 `FocusPlate`로 보인다 |
| `GlyphButton` | **56×56** | KEEP·SWITCH 전용 큰 그림 버튼. 동작은 `IconButton`과 같다 |
| `FocusPlate` | 너비 **250** 기준, 높이는 글자에 맞춤(6장) | 캔버스마다 **하나**. 지금 포커스·hover·길게 누른 버튼의 설명 한 줄. 두 번째 판은 만들지 않는다 |
| `IconRow` | 버튼 48, 간격 28(중심 간격 76) | 일시정지·설정처럼 짧은 아이콘 줄. 가운데 정렬, 한 줄. 화면 전체 패널이 아니다 |
| `UiIcon` (enum) | — | `Undo, Restart, Hint, Pause, Play, Settings, Map, Retry, Next, Back, Language, Credits, Info, SoundOn, SoundOff, Motion, Close, Keep, Switch` (append-only) |
| `UiIconSet` (ScriptableObject) | — | `UiIcon` → `Sprite`. 에셋 `Data/UiIconSet.asset`, `GameAssets.uiIcons`가 참조한다. **타입·에셋·채우는 도구는 gameplay-ui 소유**(1.1). 스프라이트는 Codex 아트(1.1) |

- **대체 그림 (gameplay-ui 제안 채택)**: 스프라이트가 비어 있는 `UiIcon`은 절차적 선 아이콘으로 그린다.
  - `IconShapes`의 선 → 96px 흰색 스프라이트로 `UiIcon`마다 한 번 래스터한다. 색은 `Image.color`로 준다.
  - 글자(↶ ↻)는 쓰지 않는다. Jua에 없을 수 있어서 `GlyphCheck`에 걸린다.
  - 한 번 만든 스프라이트는 캐시하고 다시 만들지 않는다. 정적 캐시면 `SubsystemRegistration`에서 비운다(도메인 리로드 꺼짐, `PlayResetTests`).
  - `scene-validate`는 아트 스프라이트가 없는 `UiIcon`마다 **경고**한다. 출시 빌드 전에는 0이어야 한다.

### 1.1 Codex U5 아트와 manifest (`feat/art-ui-u5`, 아직 origin에 없음)

- **들어오는 것**: PNG + `.meta`, 그리고 manifest `Art/UI/U5/u5-icons.json`(id → 경로·GUID).
  - 내용: 48 아이콘, KEEP·SWITCH 56 글리프, 시작 삼각형, 별자리 지도 글리프(scene-flow), 움직임 줄이기 토글, 걸음 별 C 스프라이트(별·연결선), 설명 판, 목도리 점, 별돌(켜짐·꺼짐).
  - PNG는 바이너리 규칙대로 공유 폴더 쪽이다(`Assets/Shared/...`). git에는 텍스트(manifest, 필요하면 `.meta`)만 들어간다. 어디에 무엇이 있는지는 그 브랜치가 정하고, 병합 때 아키텍트가 검토한다.
- **manifest 형식 (실제 파일 확인, a3470bb, `unity/Assets/_Project/Art/UI/U5/u5-icons.json`)**: 예상과 같고 필드가 더 있다.
  - 맨 위 키: `version`(1), `contract`, `icons`(25), `worldStar`, `importPolicy`, `committedImportSettings`, `artRevision`.
  - `icons[]`의 필드
    - `id`, `path`, `guid`: 대표 소스이고 4x다. 예: `Assets/Shared/Art/UI/U5/4x/undo.png`.
    - `size`(px), `logicalSize`(48·56·250), `sourceScale`, `pixelsPerUnit`(100 × 배율)
    - `sources[]`: 2x·4x 각각의 `path`·`guid`·`fileId`·`sha256`
    - `states`: normal·pressed·disabled의 색조. pressed는 0.94배이고 그림만 줄인다. 누르는 영역 48·56은 그대로다.
    - 설명 판에만 `border`/`logicalBorder`/`renderMode`가 있다.
  - id 25개는 1.1 예상과 같다. `UiIcon` 19개와 아이콘이 아닌 6개(`caption-plate scarf-dot star-stone-lit star-stone-unlit step-star step-star-link`)다.
  - `worldStar`: 클리어 별돌 모델 `Assets/Shared/Art/UI/U5/world-star-stone.fbx`(노드 `Stone`·`StarInlay`)과 머티리얼 `Art/UI/U5/U5StarLit|U5StarUnlit|U5StoneShell.mat`(git에 있음). 클리어 단계(gameplay-ui)가 쓴다. 스프라이트 `star-stone-lit/unlit`는 2D 대체용이다.
  - 가져오기 설정(`importPolicy`): Sprite Single, FullRect, Bilinear, Clamp, mipmap 없음, WebGL RGBA32(압축 없음), 4x 소스를 쓴다. 바꾸면 Codex 아트 몫이다.
- **설명 판 스프라이트의 테두리 (아키텍트 결정 2026-11-12)**
  - `caption-plate`는 250×30 논리 크기, 9-slice 테두리 8의 **채운 둥근 사각형**이다. 테두리 선이 없다.
  - 그래서 테두리는 판 안쪽 여백으로 친다. 여백은 각 변 `max(8, border)`이므로 판 높이는 **글자 + 16**(약 40)이다. `border + 8`로 계산하지 않는다. 그렇게 하면 높이가 약 56이 된다.
  - `FocusPlate.PaddingOf`를 이렇게 바꾼다(gameplay-ui).
  - `UiLayoutCheck`(14.4)는 `FocusPlate` 컴포넌트가 붙은 판에 같은 계산을 쓴다(scene-flow). 이름 목록이 아니라 컴포넌트로 판정한다(14.8 원칙).
  - 테두리 선이 있는 다른 9-slice 판은 지금 규칙(테두리를 뺀 안쪽 + 8)이다.
- **소유와 읽는 길**
  - gameplay-ui: `UiIconSet` 타입·에셋, manifest를 읽어 `UiIconSet`을 채우는 에디터 도구. GUID로 찾고, 경로는 보조다.
    - 모르는 id는 경고한다.
    - 없는 id는 대체 그림(1장)을 쓴다.
    - 런타임은 manifest를 읽지 않는다. 에셋 참조만 쓴다.
  - 아이콘이 아닌 스프라이트도 `UiIconSet`의 이름 붙은 필드로 둔다. 설명 판, 목도리 점, 별돌, 걸음 별 C가 그렇다. 걸음 별 C는 core가 같은 에셋에서 읽는다.
  - scene-flow: 시작·지도 글리프는 같은 `UiIconSet`으로 쓴다(`UiIcon.Play`, `UiIcon.Map`). 자기 스프라이트 참조를 따로 만들지 않는다.
  - 설명 판 스프라이트가 9-slice면 14.4 안쪽 여백은 `border`를 뺀 영역으로 잰다. 그래서 `FocusPlate` 높이 계산(6장)이 그 border를 반영한다.
- 아트가 병합되기 전에도 키트·화면은 대체 그림으로 돈다. 아트 병합은 그 에셋에 스프라이트를 채우는 일만이다.
- 모든 부품은 Edit Mode에서 `Build(...)`로 만들 수 있어야 한다. `UiLayoutCheck`에 필요하다(14.9).

## 2. 토글 `UiStyleToggle` (Contracts, 이 커밋에 있음)

- `LaunchOptions.Ui5`(`bool?`)를 쓴다.
  - 명령줄: `-ui5` 켬, `-ui4` 끔
  - URL: `?ui5=1|true|0|false`. 그 밖의 값은 `Warnings`에 넣고 기본값을 쓴다.
- `UiStyleToggle.U5(launch)` = `launch.Ui5 ?? DefaultOn`. **`DefaultOn = false`**다. 11/13 플레이테스트는 지금 UI이고, U5가 안정되면 리드가 기본값만 바꾼다.
- 묻는 곳: **씬 루트·흐름 컨트롤러가 화면을 만들 때 한 번** 묻는다. 그 결과로 U5 뷰와 지금 뷰 중 하나를 만든다.
  - 뷰와 키트 부품은 토글을 읽지 않는다.
  - 실행 중에 바꾸지 않는다. 바꾸려면 다시 시작한다.
- 꺼져 있으면 지금 화면·`UiLayoutCheck` 등록·smoke가 바이트 단위로 그대로여야 한다. 지금 UI 코드를 고치지 않고, U5 뷰를 옆에 더한다.
- 둘 다 될 때까지 지금 UI를 지우지 않는다. 지우는 것은 리드 결정이다.

## 3. 화면과 누르는 영역 (`touch-targets.csv` U5 행, 1280×720 기준)

- 모든 버튼은 안전 영역(가장자리 24) 안에 둔다.
- 누르는 영역끼리는 8 이상 띄운다. 시안은 12 이상이다.
- 크기는 시안보다 작게 하지 않는다.

| 화면 (담당) | 버튼 (크기, 위치) |
|---|---|
| Title (scene-flow) | 시작 64(가운데 아래 608,503), 지도 48 왼쪽 위, 설정 48 오른쪽 위, 언어 48 오른쪽 아래, 정보·크레딧 48 왼쪽 아래 |
| 언어 (scene-flow) | 언어 48, 다음 48 (y 613) |
| 크레딧 (scene-flow) | 뒤로 48 (가운데 아래) |
| 지도·행성 표면 (scene-flow) | 뒤로 48 왼쪽 위, 설정 48 오른쪽 위. 표면의 선돌 일곱은 월드 대상이고 80 이상(v4 그대로) |
| 엔딩 (scene-flow) | 지도 48, 정보 48 (y 618) |
| HUD (gameplay-ui) | 일시정지 48 왼쪽 위(24,24). 오른쪽 위에 힌트·되돌리기·다시 하기 48(1088/1148/1208, y 24). 작은 스테이지 제목(본문 22)과 목도리 점 3 |
| 선택 (gameplay-ui) | HUD 넷 + KEEP·SWITCH `GlyphButton` 56 두 개를 형제 옆에 둔다(간격 12). 보드 영역 규칙은 14.11 선택 화면 배치를 그대로 따른다. `FocusPlate` 하나 |
| 클리어 (gameplay-ui) | 별돌 셋은 월드 오브젝트다. 다음 56은 출구 근처(월드 → 화면 투영, 보드 영역 밖). 지도 48 왼쪽 위, 다시 하기 48 오른쪽 위 |
| 일시정지 (gameplay-ui) | `IconRow`: 계속·다시 하기·지도·힌트·설정 (y 604) |
| 설정 (gameplay-ui) | `IconRow`: 소리·움직임 줄이기·언어·정보·뒤로 (y 604) |
| 실패 (gameplay-ui) | 되돌리기·다시 하기 **52** (가운데 아래 y 591) |

- 위치 수치는 시안 값이다. 바꿔도 되지만 `UiLayoutCheck`(14장)를 통과해야 한다.
- HUD가 위 띠로 모였으므로 gameplay-ui는 `HudView.BoardFrame`을 다시 계산한다. 14.11에서 아래 Undo·Reset 자리가 비게 된다.

## 4. 동작

- **마우스·키보드**: hover나 포커스가 가면 `FocusPlate`에 그 버튼 설명이 보인다. 클릭·Enter·단축키는 바로 동작한다.
  - 단축키는 지금 그대로다: Z, R, H, Esc, K, X.
  - 설명 문구 끝에 키를 붙인다. 예: "되돌리기 · Z".
- **터치**
  - 아이콘: 탭하면 바로 동작한다. 0.5초 이상 누르면 설명만 보이고 동작하지 않는다.
  - KEEP·SWITCH(되돌릴 수 있지만 결정 순간):
    - 첫 탭은 포커스와 설명만 준다. 같은 글리프를 두 번째 탭하면 확정한다.
    - 다른 글리프를 탭하면 포커스가 옮겨 간다.
    - 마우스 클릭은 한 번에 확정한다.
- **선택 설명의 첫 방문과 다음 방문**
  - 첫 선택(튜토리얼 `TutorialIds.Choose`를 아직 보지 않음)에는 포커스된 글리프의 설명을 처음부터 보인다. 기본 포커스는 KEEP이다.
  - 다음 방문부터는 설명 없이 글리프만 나온다. 포커스·hover·길게 누르기가 있을 때만 보인다.
  - 판정은 기존 `TutorialTracker`가 한다. 새 저장 항목을 만들지 않는다.
- **키보드 포커스 이동**: 키트가 `Navigation`을 **명시적으로** 연결한다(`IconRow` 좌우, HUD 줄 좌우).
  - 자동 이동은 쓰지 않는다. 숨은 버튼에 포커스가 멈추는 문제(cd38eb8 R1)가 있었다.
  - 숨긴 버튼은 `interactable=false`로 두어 이동에서 뺀다.

## 5. 문자열 (KO/EN, `Data/Strings/{ko,en}.json`)

- 새 키 묶음은 `ui5.*`이다. 아이콘 설명은 `ui5.icon.<icon>`(소문자, 예: `ui5.icon.undo`)이고, 선택 설명은 `ui5.choice.keep`, `ui5.choice.switch`다.
- 둘 다 **한 줄**이다. 판 안쪽 너비에 들어가야 한다(6장). 기준은 KO 12자 안팎, EN 28자 안팎이다.
  - 들어가지 않으면 문구를 줄인다. 글자 크기를 줄이지 않는다.
  - 기존 `hud.keep_desc`는 너무 길어서 새 키를 쓴다.
- 문구 초안은 gameplay-ui가 만든다. 이야기·톤은 기존 문자열 담당 규칙을 따른다.
- 키 누락은 `LocalizationCheck`가 잡는다(13.6). 두 언어 모두 있어야 한다.

## 6. architecture.md 14장과 맞추기 (이 문서가 정한다)

- **`FocusPlate` 크기**: 시안의 250×30은 14.4(판 안쪽 여백 8)를 지키면 만들 수 없다. 글자 높이가 들어갈 자리가 없다. 그래서 다음처럼 한다.
  - 글자는 **보조 18**(`UiLayout.secondary`)이다. 14.2 보조 목록에 "U5 포커스 설명"을 더한다.
  - 판 높이는 글자 높이 + 16이다. 약 40이다.
  - 너비는 250 기준이고, EN이 길면 **320까지** 늘린다. 그보다 길면 문구를 고친다.
  - 판 알파는 0.75 이상이고, 대비는 4.5:1이다.
  - 판은 보드 영역(14.6)과 보이는 다른 버튼을 피한다. 선택 화면에서는 글리프 바로 위나 아래에 둔다.
- **아이콘 버튼**: 보이는 글자가 없으므로 14.4(판)는 해당하지 않는다. 그림은 장식이다. 배경 대비 3:1, 선 굵기 2 이상(14.3)이다.
- **목도리 점·별돌**: 장식·월드 대상이다. 색만이 아니라 모양으로도 구분한다(14.11).
- **모달**: 일시정지·설정 `IconRow`는 화면 전체 패널이 아니다. 그래도 그 아래 보드 조작은 막아야 하므로 14.7의 막을 쓴다(알파 0.6 이상, 또는 0.25 + HUD 숨김). 아래 HUD가 막 위에 보이면 안 된다.
- **등록 화면**(scene-flow, `UiLayoutCheck`): U5를 켠 상태로 3장 표의 각 화면을 ko·en으로 등록한다. 지금 등록은 꺼진 상태 그대로 둔다. 선택은 첫 방문(설명 보임)과 다음 방문(포커스 설명) 둘이다.

## 7. MVC와 단정 (architecture.md 9장, scene-flow.md 11장)

- **View**(키트 부품과 U5 화면)
  - 그리고, 사용자 조작을 이벤트로 낸다(`Clicked`, `FocusChanged`).
  - `GameSession`·`AppFlow`·저장을 직접 부르지 않는다.
  - 토글·`Services`를 읽지 않는다.
- **Controller**
  - 기존 컨트롤러(`BoardController`, `GameFlowController`, `MenuController`, `TitleFlowController` 등)가 View 이벤트를 받아 명령을 부른다.
  - U5와 지금 UI가 **같은 컨트롤러 메서드**를 부른다. 예: 되돌리기는 `BoardController.Send(Command.Undo)` 하나다. 그래서 규칙·텔레메트리·저장은 UI 방식과 무관하게 같다.
- **Model**: 바뀌지 않는다. `FocusPlate`가 무엇을 보일지는 작은 순수 C# 모델(`FocusTip`: 지금 주인과 키)로 두고 EditMode로 테스트한다.
- **단정**
  - `Build`의 필수 인자(부모, `UiIconSet`, `Localizer`, 콜백)가 null이면 바로 던진다.
  - 정의되지 않은 `UiIcon` 값이면 `ArgumentOutOfRangeException`이다.
  - 캔버스 하나에 `FocusPlate`가 두 개 생기면 `InvalidOperationException`이다.
  - 인스펙터 참조(`GameAssets.uiIcons`)는 `Awake`에서 검사한다.
  - 빈 스프라이트는 예외가 아니라 1장의 글자 대체 그림을 쓰고 검증기가 경고한다.
- **WebGL**: 새 API는 없다. 길게 누르기는 `Time.unscaledTime`으로 잰다. 매 프레임 할당하지 않는다.

## 8. 확인 (헤드리스만, CLAUDE.md 검증 규칙)

- `unity.sh compile`, `test`(EditMode): `FocusTip`, `UiStyleToggle`, 키트 `Build` 크기·내비게이션·단정.
- `unity.sh scene-validate`(`UiLayoutCheck` U5 등록), `playtest`(U5 켬으로 Boot → Title → Game 한 바퀴).
- 화면은 배치 캡처(`-batchmode`, 카메라 렌더 때만 `-nographics` 제외)로 본다.
- 플레이어 빌드는 만들지 않는다.

## 9. 순서

1. 아키텍트: 이 문서와 `UiStyleToggle`/`LaunchOptions.Ui5`(이 커밋).
2. gameplay-ui: 키트(1·4·5·7장) + EditMode 테스트 → 아키텍트 검토 → 리드 병합. scene-flow는 이 병합 뒤에 키트를 쓴다.
3. gameplay-ui의 HUD·선택·클리어·일시정지·설정·실패와 scene-flow의 Title·언어·크레딧·지도·표면·Boot·엔딩은 병렬로 한다. 각 브랜치는 병합 전에 아키텍트가 검토한다.
4. 아트 요청(48 아이콘, 56 글리프, 별돌)은 리드가 코디네이터에 보낸다. 키트는 글자 대체 그림으로 먼저 돈다.
