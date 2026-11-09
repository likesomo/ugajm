# RuinsHenge Unity 연결·테스트

## UI 작업 이후: 정식 씬용 환경 전달(2026-11-09)

`Prefabs/Tiles/RuinsHenge/Env/Planet_Chapter01.prefab`은 행성/성운·별/원경3행성/키·필·림/Volume를 묶은 수동 배치용 프리팹이다. 루트 `RuinsHengePlanet`의 `Fit(BoardGrid)`를 스테이지 진입 시 호출한다. 입력·게임 객체·커맨드라인을 읽지 않으며 geometry는 런타임에 생성한다. 보드1칸=1m·y=0, 정상부y=-.12, 메시6재질 공유를 유지한다.

**기존 ChapterStage 경로에는 아래 분리형을 권장한다.** `Art/RuinsHenge/Chapter01.asset` → `Sky_Chapter01.asset` → `PlanetSummit_Chapter01.prefab`(IStageEnvironment 루트) 및 `Sky_Chapter01.prefab`, `Lighting_Chapter01.asset`로 전달했다. ChapterStage가 행성을 월드 원점에 놓고 Fit를 호출하며 하늘은 카메라 자식, 조명은 씬 소유3등에 값을 복사한다. 분리형과 전체 환경 프리팹을 동시에 쓰면 조명/하늘이 중복되므로 **둘 중 한 경로만** 쓴다. 전체형도 메시 Fit 전에는 빈 정상부이며 생성 실패가 아니다.

리드/씬 플로우의 연결 요청: 공용 AppConfig.chapters에 Chapter01 또는 확정된 장 preset를 등록하고 레벨 chapter id를 맞춘다(`moss-ruins`, 빈id는첫장). `ChapterStage`와 `BoardController.UseChapters` 헬퍼는 있지만 현재 GameController에서 실제 생성·연결하는 호출은 아직 없으므로 씬/조립 담당이 연결해야 한다. 이때 전체 프리팹을 추가하기보다 위 split Sky/Lighting을 사용한다. UI는 GameAssets.uiTheme를 `Art/UI/RuinsHengeTheme.asset`로 지정한다. 실제 UiTheme에는 스프라이트 필드가 없어 `Art/UI/RuinsHengeResources.asset`도 전달한다. UI 담당이 HUD/Title/StageSelect를 [UI 명세](ui-theme.md)로 연결한다. 공용 씬·AppConfig·GameAssets는 이 작업에서 수정하지 않았다.

새 TileSet.playerBodies에는 A/B/C 실제 외형을 채웠다. KEEP는 중성 공통 외형을 사용한다. 이전 문서의 ‘몸별 계약 없음’ 요청은 최신12.6 계약으로 해결됐으며 출신색 KEEP 흔적은 후속 선택 사항이다. `RuinsWallVariations`는 좌표 해시로 기본/V2/V3/목재를 선택하는 장식 컴포넌트이고 타일 규칙·충돌을 바꾸지 않는다. 머리는 같은0.88m 규격 안에서 폭/깊이만 확대하고 밝은 UV·최소조명으로 두건 뭉침을 줄였다.

정식 계약 구현 `RuinsHengePlanet`/`RuinsHengeSky`/`RuinsWallVariations`는 Art/RuinsHenge/Runtime에 있다. Art/Test의 기존 Environment는 프리뷰 어댑터만 남겼다. 생성기 `RuinsHengeEnvironmentBuilder`와 `RuinsHengeUiBuilder`는 RuinsHengeBuilder.Build에서 호출되어 GUID를 보존하며 재실행한다.

> 아래는1차 연결 기록이다. 최신 조명·벽 높이·색 링·행성 환경과 재검증 결과는 [2차 다듬기](unity-phase2.md)를 따른다.

작업:2026-11-09, Unity6000.6.8f1. 아트 전용 생성기 `Assets/_Project/Art/Editor/RuinsHengeBuilder.cs`를 사용한다. 공용 AppConfig/씬/게임플레이 계약은 수정하지 않는다. 기본 Game 씬을 읽어 아트 소유 `Assets/_Project/Art/Test/RuinsHenge_Preview.unity`로 저장하고 별도 `Art/RuinsHenge/PreviewConfig.asset`을 연결한다.

## 재실행

프로젝트를 여는 Unity 에디터가 없을 때 실행한다. 원본 공유 에셋은 `tools/link-shared-assets.ps1`로 연결한다. FBX/PNG와 .meta를 지우거나 GUID를 재생성하지 않는다.

```text
Unity.exe -batchmode -nographics -projectPath unity -executeMethod SevenSteps.Art.RuinsHengeBuilder.BatchBuild -logFile unity/Logs/ruins-build.log
Unity.exe -batchmode -nographics -projectPath unity -executeMethod SevenSteps.Art.RuinsHengeBuilder.BatchValidate -logFile unity/Logs/ruins-art-validate.log
Unity.exe -batchmode -nographics -projectPath unity -buildTarget Win64 -executeMethod SevenSteps.Art.RuinsHengeBuilder.BuildPreviewWindows -logFile unity/Logs/ruins-win.log
Unity.exe -batchmode -nographics -projectPath unity -buildTarget WebGL -executeMethod SevenSteps.Art.RuinsHengeBuilder.BuildWebGL -logFile unity/Logs/ruins-webgl.log
```

일반 CompileCheck, `unity/Tools/unity.sh tileset-validate`, `unity/Tools/unity.sh test`도 병행한다. 프리뷰 빌드는 해당 테스트 씬만 사용하여 공용 EditorBuildSettings/기본 TileSet 참조를 바꾸지 않는다. 출력은 `Builds/RuinsHenge/Windows/`와 `Builds/RuinsHenge/WebGL/`이다.

## 임포트·6재질

FBX globalScale1/useFileScale/bakeAxisConversion, Normals Import, Animation/Rig None, 카메라/조명/블렌드셰이프 제외. 원본의6재질 식별자를 `_Project/Art/RuinsHenge/Materials`의 외부 머티리얼로 리맵한다. FBX 내부 재질을 개별 파일로 무작정 추출해 중복시키지 않는다. 모델 .meta는 AssetImporter로 갱신하고 GUID는 유지한다.

4종(PlayerAtlas/MenhirAtlas/MetalAtlas/FrostOpaque)은 URP Simple Lit, BaseAtlas와 MarkUnlit은 URP 조명/그림자·포그에 연결한 작은 아트 전용 셰이더다. BaseAtlas는 기본 Simple Lit이 읽지 않는 FBX 버텍스 R 이끼 마스크를 처리하고, MarkUnlit은 같은 머티리얼로 링·마커의 버텍스 색을 표시한다. 추가 형제별 재질을 만들지 않는다. 공유 RH_Atlas512_Triplets를 사용하고 중성 KEEP는 무채색 #B9BFC0이다.

형제는 같은0.88m 몸/머플러 실루엣을 보존하고 **발밑 지름0.6m 색 링**을 추가했다. 작은 머플러·밑단보다 화면에서 면적이 커져 보드 크기에서 형제 색을 인지하기 쉽고 몸 실루엣을 더 넓히지 않는다. 링은 한 Marker 머티리얼과 색만 다른 메시를 공유하며 A/B/C 색은 승인 HEX를 따른다. 링은 보조 식별이며 숫자/형제 인덱스를 대체하지 않는다.

## TileSet 매핑

세트: `Assets/_Project/Art/RuinsHenge/RuinsHenge_Chapter01.asset`,1칸1유닛,바닥0,중앙 피벗,직교 pitch50/yaw0.

| TileSet | 프리팹·FBX | 상태 |
|---|---|---|
| floor/floorAlt | Floor=Floor_Moss / FloorAlt=Floor_Clean | randomYaw90,그 외 방향 기믹은 고정 |
| wall/pit/ice/cracked/bridge/exit | Wall/Pit/Ice/Cracked/Bridge/Exit | 윗면/셀 크기 규격 |
| gate | Gate=NumberDoor | GatePanel inactiveChild/materialTarget,별도 숫자 라벨 |
| doors[0..2] | Door=NumberDoor | 같은 문 실루엣,A/B/C는 기존 UI 라벨 |
| countDoor | CountDoor | GatePanel inactiveChild,정확한 수는 기존 UI 라벨 |
| plates[0..2] | Plate | 같은 발판,글자는 UI |
| lever | Lever | RaisedHandle inactiveChild/PulledHandle activeChild |
| flipWalls[0/1] | FlipUp/FlipDown | variant0/1,별도 프리팹 |
| player/statue | PlayerA/Statue | B/C 프리팹도 생성하지만 공용 계약 연결은 별도 요청 |
| start/oldStart/footprint/luckyRing | Start/OldStart/Footprint/LuckyRing | 생성된 링 메시,한 Marker 재질 |

프리팹은 모델을 완전히 Unpack한 후 자식 훅을 지정하므로 FBX 중첩 프리팹의 재부모화 제약을 피한다. 재실행 시 기존 프리팹/머티리얼/메시/세트 파일을 갱신하여 .meta GUID를 유지한다. TileVisual 레이블은 별도 UI이며 숫자/별을 메시 텍스처에 굽지 않는다.

## 공용 파일 변경 요청

1. 아키텍트/리드가 실제 `AppConfig.tileSet`을 새 세트로 교체해야 한다. 현재는 프리뷰 설정만 새 세트를 가리킨다.
2. `TileSetValidator.ValidateAll`은 `Data/TileSets`만 찾는다. `Art/**` 검색 추가 또는 정식 Data/TileSets 경로로 배치할 소유권 승인이 필요하다. 이번에는 `TileSetValidator.Validate(새 세트)`를 직접 호출하여 실제 세트를 검사했다.
3. 현재 계약은 player/statue 각각 한 필드이며 BoardView는 몸 인덱스별 프리팹을 선택하지 않는다. 세쌍둥이 A/B/C·KEEP 출신 표식을 실제 플레이에 연결하려면 아키텍트/게임플레이가 몸 인덱스→외형 선택 경계를 추가해야 한다. 이번에는 프리뷰에 B/C와 KEEP를 별도 전시하고 실제 조작 캐릭터는 A를 사용한다.

## 검증 기록

최종 통과/실패·스크린샷·성능 수치는 완료 보고서에 기록한다. 라이선스 첫 시도에서 entitlement와 Compile OK를 확인해10분 재시도는 필요하지 않았다. 새로운 세트는 공용 기본 참조를 바꾸기 전까지 별도 프리뷰 전용이다.

공용 SceneLoader가 씬 이름 Game을 요구하므로 프리뷰를 같은 아트 폴더의 `Art/Test/Game.unity` 별칭으로도 저장하여 빌드한다. 원본 `Scenes/Game.unity`는 수정하지 않는다. 에디터에서 해당 별칭을 Play하거나 생성기로 빌드하면 공용 라우팅 변경 없이 실행된다.

A/B/C와 KEEP 몸 MeshFilter는 동일한 A FBX의 Mesh 참조를 사용한다(단순히 같은 좌표의 복사본이 아니다). 색 액세서리 UV/재질과 링만 다르다. 실제 화면 가림을 줄이기 위해 벽은 원본0.55m에서 세로0.65배(약0.36m)로 낮췄다. 셀 규격과0.88m 몸은 유지한다.

추가 공용 요청: `board-pixels.ps1`은 옛 보라색 바닥만 판정하므로 새 크림색 세트에서는 smoke-webgl이 허위 실패한다. 아키텍트/리드는 팔레트 독립 판정 또는 세트별 판정 방식을 검토해야 한다. 이번에는 공용 검사를 바꾸지 않고 [아트 전용 크림색 검사](check-ruins-pixels.ps1)와 실제3D 화면/콘솔을 함께 확인했다.

| 검사 | 결과·근거 |
|---|---|
| CompileCheck | PASS, unity/Logs/ruins-license-01.log 및 ruins-final-check.log |
| 세트 직접 검사 | PASS, RuinsHengeBuilder.Validate→TileSetValidator.Validate,0오류/0경고 |
| unity.sh tileset-validate | PASS, 기본세트0오류/0경고; 검색 범위 차이는 위에 명시 |
| unity.sh test | PASS,221/221 EditMode, reports/editmode-results.xml |
| 생성기 재실행 | PASS,79기존 에셋 GUID 유지·A/B/C/KEEP 몸 Mesh 공유 |
| Windows 프리뷰 빌드/리플레이 | PASS, errors0,1스테이지 RRRRRRR→Win/used7 |
| WebGL 프리뷰 빌드 | PASS, errors0,Mobile_RPAsset 대상으로 시작 |
| 기본 smoke-webgl | FAIL(exit1): 보라 픽셀1/8/13=0/29/14,콘솔예외0 |
| 새 아트 픽셀 검사/화면 확인 | PASS: 크림 픽셀1/8/13=34190/27444/24282,실제 보드 표시 |

RTX3060 Windows1280×720의5초 표본:59.4FPS,할당285.6MiB,429draw calls. Orca 브라우저 WebGL5초 표본(기본1280×720 캔버스):1/8/13 모두약60FPS,할당47.3/47.1/47.1MiB,279/393/347draw calls. 프레임 제한60과 짧은 로컬 표본이며 모바일 성능 보장은 아니다. 카운터가 지원되지 않으면0으로 표시한다. 현재draw calls는50스테이지 환경 확장 전 최적화 검토 대상이다. 후처리 DOF/Panini/FSR 스트리핑 경고는 있었지만 렌더 예외는 없었다.

스크린샷·복사 로그 루트: `C:/unityjam/shared-assets/seven-steps/concepts/themes/ruins-henge-planets/unity/`. Windows는 windows-level1.png/windows-perf.png, WebGL은 webgl-level1.png/webgl-level8.png/webgl-level13.png다. Unity 모델·재질 연결은 검증했지만 사용자에게 확정할 최종 조명·UI·배경 완성도를 달성한 것으로 주장하지 않는다.
