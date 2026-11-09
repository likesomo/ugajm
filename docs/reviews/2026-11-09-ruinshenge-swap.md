# 검토: AppConfig.tileSet → RuinsHenge_Chapter01 교체 (feat/art 1330618)

검토자: 아키텍트, 2026-11-09. 코드·에셋 텍스트와 아트 보고(`design/art/unity-integration.md`, `unity-phase2.md`)를 읽었다.

**직접 확인(라이선스 복구 후, feat/architecture + feat/art의 `Art/**`·`Prefabs/Tiles/**`를 작업 트리에 임시로 얹어서, 커밋하지 않음)**: CompileCheck OK(아트 에디터·프리뷰 스크립트 포함), 새 `TileSetValidator`(메시·머티리얼·셰이더 누락 오류, 벽 높이 경고 포함)로 `RuinsHenge_Chapter01` **0 오류 0 경고**, 임시 세트 0/0. EditMode 223/223(feat/architecture 단독).

**결론: OK. 세트는 계약에 맞다(1칸 1유닛, 바닥 0, 직교 pitch 50 / yaw 0, 벽 0.41칸, 자식 훅 지정, 프리팹 23개 `Prefabs/Tiles/RuinsHenge/`, 세트 `Art/RuinsHenge/` = 12.7 정식 위치). 아래 "교체 전 필수"를 처리하면 `AppConfig.tileSet` 교체 OK.** 환경(행성·하늘·조명·후처리)은 이번 교체에 넣지 않고 12.8 챕터 프리셋으로 옮긴다.

## 교체 전 필수
1. **`Art/Test/Game.unity` 별칭**: 리드 설명대로 프리뷰 빌드용 별칭으로 허용한다(12.7). 조건: 빌드 목록에 넣지 않는다. `SceneFlowValidator`의 이름 중복 검사는 빌드 목록 기준으로(씬 플로우에 전달). 정식 Game 씬이 챕터 프리셋을 쓰면 지운다. 에디터 메뉴 `Open Game Scene`은 경로로 열어서 영향 없음(확인함).
2. **smoke-webgl 판정**: 지금 검사는 보라색 바닥만 세서 이 세트에서 거짓 실패한다(아트 측정 0~90픽셀, 콘솔 예외 0). 12.9 안 A(게임 안 보드 프로브)가 들어오기 전 이 합류 한 번은 아트의 `check-ruins-pixels.ps1` + 스크린샷 3장 + 콘솔 예외 0으로 대신 확인(리드 판단).
3. **검사 범위**: feat/architecture의 `TileSetValidator`(`Assets/_Project` 전체)를 feat/art보다 먼저 합친다. 위 임시 확인에서 두 세트 모두 0/0이었다. 합친 뒤 `unity.sh tileset-validate` 한 번.

## 에디터 코드·프리뷰 스크립트 (RuinsHengeBuilder, Art/Test/*.cs)
- `Art/Editor/RuinsHengeBuilder.cs`: 폴더 이름이 `Editor`라 에디터 전용으로 컴파일된다(OK). 재실행해도 GUID를 유지하도록 기존 에셋을 `CopySerialized`로 갱신한다(좋음). **공유 폴더 FBX의 `.meta`(임포터 설정)를 고친다** → 모든 작업 공간에 즉시 반영되므로 이 빌더는 아트만 돌린다(README 에셋 규칙 5).
- 빌더의 `BuildPlayer`는 `BuildTools`를 거치지 않는다. `Unity.exe`를 직접 부르면 URP 전역 설정 손상 복구(CLAUDE.md)가 빠지니, 돌린 뒤 `git checkout -- unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.
- 코드 형식: 한 줄에 문장 여러 개를 이어 쓴 곳이 많다. 아트 전용 생성기라 막지 않지만, 런타임으로 옮기는 코드(12.8 `IStageEnvironment`)는 9장 형식(한 줄 한 문장, 단정적 검사)으로 다시 쓴다.
- `RuinsHengeProbe`(성능 표본, `-artperf`로 스크린샷 후 종료)·`RuinsHengeEnvironment`: 프리뷰 전용으로 OK. 정식 Game 씬에 붙이지 않는다(아래).

## 교체 때 같이 볼 것 (막지는 않음)
- **환경 없음**: `AppConfig.tileSet`만 바꾸면 Game 씬의 기본 조명·배경색 위에 보드만 뜬다(행성·하늘 없음). 프리뷰와 다르게 보이는 것은 정상이다. 바닥 색이 기본 조명에서 너무 밝거나 어두우면 `ChapterPreset` 전까지 Game 씬 방향광 값만 맞춘다(씬 플로우 소유).
- **`overrideCamera`**: Game 씬에서 꺼져 있는지 확인(12.2). 세트의 `cameraRig`(pitch 50, padding 0.7)가 기준이다.
- **라벨**: 숫자 문·석상 수 문·발판 글자는 UI 라벨이다. 0.41칸 벽과 pitch 50에서 `labelOffset`이 맞는지 스모크 스크린샷 7장(1, 3, 8, 9, 13, 15, 16)으로 본다.
- **세쌍둥이**: 12.6 계약(`BodyIndex`, `playerBodies`/`statueBodies`, `bodyColors`)이 합쳐진 뒤 아트가 `playerBodies`=PlayerA/B/C를 채운다. KEEP 선돌(형제 흔적 석상) 3개는 아직 없음(아트 새 작업). `bodyColors` 기본값은 승인 3색이라 세트에 따로 넣지 않아도 된다.
- **프리뷰 스크립트가 플레이어 빌드에 들어감**: `Art/Test/RuinsHengeEnvironment.cs`·`RuinsHengeProbe.cs`는 asmdef가 없어 Assembly-CSharp로 정식 빌드에 포함된다. `FindFirstObjectByType<GameController>`, `LateUpdate` 폴링, `Environment.GetCommandLineArgs()` 직접 읽기는 9장 규칙(MVC, 조립은 씬 루트, 실행 인자는 `LaunchOptions`)에 어긋난다. 프리뷰 동안은 허용하고, 12.8 `IStageEnvironment`로 옮길 때 고친다. 정식 Game 씬에는 붙이지 않는다.
- **셰이더 `SevenSteps/RuinsSurface`**: URP·Mobile 렌더러(WebGL)에서 동작 확인됨(아트). `UnityPerMaterial` CBUFFER가 있어 SRP Batcher 대상이지만 `UsePass "Universal Render Pipeline/Lit/ShadowCaster"`·`DepthOnly`는 Lit의 머티리얼 버퍼를 기대해 배칭이 깨질 수 있다. draw call 400 안팎(레벨 8: 403)이 문제가 되면 자체 ShadowCaster 패스를 쓴다. 지금은 60FPS라 막지 않는다.
- **성능**: WebGL 60FPS, Unity 할당 47MiB(아트 로컬 측정). 챕터가 늘어나면 빌드 크기와 draw call을 다시 잰다.
- **공유 폴더 GUID**: 프리팹·머티리얼이 공유 폴더의 FBX/PNG GUID를 참조한다. 아트 빌더가 GUID를 유지한다고 보고했다. 새 검사기가 누락을 오류로 잡는다.

## 담당
- 아트: 1번, `playerBodies` 채우기, 형제 흔적 석상 3개.
- 리드/씬 플로우: `Data/AppConfig.asset`의 `tileSet` 교체(에셋은 씬 플로우 영역 `GameplayScenes`가 만든다. 리드가 담당 지정).
- 게임플레이/UI: 12.6 보드 적용, 12.9 프로브.
- 아키텍트: 12.7 검사기(feat/architecture), 12.8 타입(레벨 디자인 제안 후).
