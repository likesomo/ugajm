# 검토: AppConfig.tileSet → RuinsHenge_Chapter01 교체 (feat/art 1330618)

검토자: 아키텍트, 2026-11-09. 코드·에셋 텍스트와 아트 보고(`design/art/unity-integration.md`, `unity-phase2.md`)를 읽었다. 이 PC의 Unity 라이선스 문제로 배치는 돌리지 않았다(아트 보고의 CompileCheck·검사·EditMode 221/221·빌드 결과는 아트 작업 공간 기록).

**결론: 세트 자체는 계약에 맞다(1칸 1유닛, 바닥 0, 직교 pitch 50 / yaw 0, 벽 0.41칸, 자식 훅 지정). 아래 "교체 전 필수" 3개를 처리하면 `AppConfig.tileSet` 교체 OK.** 환경(행성·하늘·조명·후처리)은 이번 교체에 넣지 않고 12.8 챕터 프리셋으로 옮긴다.

## 교체 전 필수
1. **`Art/Test/Game.unity` 삭제**(아트). 씬 이름이 `AppScenes.Game`과 같다. 빌드 목록에 없어도 에디터·검증기에서 혼동되고, 씬 플로우의 `SceneFlowValidator` 규칙(이름 중복 금지)에 걸린다. 프리뷰는 `RuinsHenge_Preview.unity` 하나로 충분하다(아트 빌더의 프리뷰 빌드는 그 씬을 쓰도록).
2. **smoke-webgl 판정**: 지금 검사는 보라색 바닥만 세서 이 세트에서 거짓 실패한다(아트 측정 0~90픽셀, 콘솔 예외 0). 12.9 안 A(게임 안 보드 프로브)가 들어오기 전 이 합류 한 번은 아트의 `check-ruins-pixels.ps1` + 스크린샷 3장 + 콘솔 예외 0으로 대신 확인(리드 판단).
3. **검사 범위**: feat/architecture의 `TileSetValidator`가 `Art/**`까지 본다(12.7). 이 변경을 먼저 합치고 `unity.sh tileset-validate`로 두 세트 모두 오류 0을 확인한다(라이선스 복구 후, 메시·머티리얼 누락 오류와 벽 높이 경고가 새로 생겼으니 한 번 돌려 볼 것).

## 교체 때 같이 볼 것 (막지는 않음)
- **환경 없음**: `AppConfig.tileSet`만 바꾸면 Game 씬의 기본 조명·배경색 위에 보드만 뜬다(행성·하늘 없음). 프리뷰와 다르게 보이는 것은 정상이다. 바닥 색이 기본 조명에서 너무 밝거나 어두우면 `ChapterPreset` 전까지 Game 씬 방향광 값만 맞춘다(씬 플로우 소유).
- **`overrideCamera`**: Game 씬에서 꺼져 있는지 확인(12.2). 세트의 `cameraRig`(pitch 50, padding 0.7)가 기준이다.
- **라벨**: 숫자 문·석상 수 문·발판 글자는 UI 라벨이다. 0.41칸 벽과 pitch 50에서 `labelOffset`이 맞는지 스모크 스크린샷 7장(1, 3, 8, 9, 13, 15, 16)으로 본다.
- **세쌍둥이**: 12.6 계약(`BodyIndex`, `playerBodies`/`statueBodies`)이 합쳐진 뒤 아트가 `playerBodies`=PlayerA/B/C를 채운다. 형제 흔적 석상 3개는 아직 없음(아트 새 작업).
- **프리뷰 스크립트가 플레이어 빌드에 들어감**: `Art/Test/RuinsHengeEnvironment.cs`·`RuinsHengeProbe.cs`는 asmdef가 없어 Assembly-CSharp로 정식 빌드에 포함된다. `FindFirstObjectByType<GameController>`, `LateUpdate` 폴링, `Environment.GetCommandLineArgs()` 직접 읽기는 9장 규칙(MVC, 조립은 씬 루트, 실행 인자는 `LaunchOptions`)에 어긋난다. 프리뷰 동안은 허용하고, 12.8 `IStageEnvironment`로 옮길 때 고친다. 정식 Game 씬에는 붙이지 않는다.
- **셰이더 `SevenSteps/RuinsSurface`**: URP·Mobile 렌더러(WebGL)에서 동작 확인됨(아트). `UnityPerMaterial` CBUFFER가 있어 SRP Batcher 대상이지만 `UsePass "Universal Render Pipeline/Lit/ShadowCaster"`·`DepthOnly`는 Lit의 머티리얼 버퍼를 기대해 배칭이 깨질 수 있다. draw call 400 안팎(레벨 8: 403)이 문제가 되면 자체 ShadowCaster 패스를 쓴다. 지금은 60FPS라 막지 않는다.
- **성능**: WebGL 60FPS, Unity 할당 47MiB(아트 로컬 측정). 챕터가 늘어나면 빌드 크기와 draw call을 다시 잰다.
- **공유 폴더 GUID**: 프리팹·머티리얼이 공유 폴더의 FBX/PNG GUID를 참조한다. 아트 빌더가 GUID를 유지한다고 보고했다. 새 검사기가 누락을 오류로 잡는다.

## 담당
- 아트: 1번, `playerBodies` 채우기, 형제 흔적 석상 3개.
- 리드/씬 플로우: `Data/AppConfig.asset`의 `tileSet` 교체(에셋은 씬 플로우 영역 `GameplayScenes`가 만든다. 리드가 담당 지정).
- 게임플레이/UI: 12.6 보드 적용, 12.9 프로브.
- 아키텍트: 12.7 검사기(feat/architecture), 12.8 타입(레벨 디자인 제안 후).
