# UI·환경 전달 검증 — 2026-11-09

출력 루트 `C:/unityjam/shared-assets/seven-steps/concepts/ui/`. `index.html`에 시안9종과 실제 Unity 화면/아틀라스를 모았다. 기존 `concepts/themes/index.html` 맨 위에도 UI 섹션을 추가했다. 이미지·폰트·FBX는 git에 포함하지 않는다.

## 테스트

| 검사 | 결과·공유 로그(logs/) |
|---|---|
| 최종 CompileCheck | PASS, ruins-ui-final-check.log, Compile OK |
| 생성기 재실행 | PASS,113기존 에셋 GUID 유지, A/B/C/KEEP 동일 몸 Mesh |
| UI 참조 검사 | PASS,17sprite·9-slice·UiTheme.Validate·패널 필수 참조 |
| TileSet 직접 검사 | PASS, 같은 최종 로그,6개 보드 머티리얼 |
| unity.sh tileset-validate | PASS, batch-tileset-validate.log, 새 세트0오류/경고1(mud 미지정→floor), Chapter01 0오류/0경고 |
| EditMode | PASS263/263, editmode-results.xml 및 batch-test.log |
| Windows 빌드 | PASS, ruins-ui-win-final.log,0오류 |
| Windows 리플레이 | PASS, unity/result-level1.png, RRRRRRR→Win/used7; 머리 최종 명도 보정 전의 검토 컷 |
| 최종 WebGL 빌드 | PASS, ruins-ui-webgl-final.log,0오류 |
| 표준 smoke-webgl | **PASS**,1/8/13 보드픽셀83864/63750/58578, 콘솔 예외0 |

이번 origin/7lucky에 팔레트 독립 보드 검사가 합쳐져 표준 smoke도 통과했다. 검사 코드는 아트 작업에서 수정하지 않았다. 비활성 DOF/Panini/FSR 스트리핑 로그는 남지만 실제 렌더 예외는 없다.

## 성능과 실제 화면

RTX3060 로컬 Orca 브라우저, 기본1280×720 캔버스,2초 대기 후5초 표본, Mobile_RPAsset:

| 스테이지 | FPS | Unity 할당 MiB | draw calls | 파일(unity/) |
|---|---:|---:|---:|---|
|1|60.0|48.9|313|webgl-level1.png / webgl-level1.console.txt|
|8|60.0|49.0|427|webgl-level8.png / webgl-level8.console.txt|
|13|60.0|49.0|377|webgl-level13.png / webgl-level13.console.txt|

Windows의 중간 품질 검토는60FPS/286.6MiB/658draws(PC_RPAsset)였다. 이후 두건을 밝힌 최종 검증은 WebGL 컷을 기준으로 한다. 위 수치는 짧은 데스크톱 로컬 표본이며 모바일/저사양의 장시간60FPS 보장은 아니다.2차 대비 WebGL은 약24/24/20draw와 약2MiB가 늘어 벽 변형/추가UI 리소스 비용이 있다. 새 HUD 전체를 연결한 최종 제품의 성능은 UI 담당 통합 후 다시 확인해야 한다.

## 완료 범위와 후속 연결

완료: 테마 기반 시안,9-slice·버튼4상태·아이콘·별·몸 상태 스프라이트, 실제 UiTheme와 Settings/Result 아트 복사 프리팹, 정확한7별/숫자 프리팹, 환경 전체/분리형 프리팹과 Chapter/Lighting/Sky SO, 벽/머리 가독성 보완. 실제 HUD/타이틀/스테이지 선택의 레이아웃 교체는 아직 아니다. 기존 HUD에는 테마 색/몸3색이 적용되지만7원형 카운터는 그대로다.

공용 요청:

1. 씬/리드: GameAssets.uiTheme 후보 연결, 공용 GameController에서 ChapterStage 생성·BoardController.UseChapters 연결, AppConfig.chapters 등록. 이 작업은 공용 파일을 바꾸지 않았다.
2. UI: RuinsHengeResources와 DipperCounter를 실제 HUD/Title/StageSelect/로딩에 연결하고 포커스·터치·긴 번역 문구를 검증. 스프라이트를 저장할 UiTheme 필드 추가 또는 보조 리소스 참조 방식 선택.
3. 기획/레벨:3별 기준과 최단 걸음 저장/계산 계약, 챕터 수·장 경계 확정. 시안의5장·3별을 확정 규칙으로 취급하지 않는다.
4. KEEP 출신색 흔적은 현재 중성 공통 석상이다. 몸3색 선택은 playerBodies로 연결됐고 KEEP 흔적은 가독성 검토 후 확장한다.

원본·재생성·필드·폰트 라이선스는 [ui-theme.md](ui-theme.md), 환경 연결 방식은 [unity-integration.md](unity-integration.md)에 기록했다.
