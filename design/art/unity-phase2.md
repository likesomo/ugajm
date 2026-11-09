# Unity 2차 다듬기 — 2026-11-09

1차 `7e93396`와 같은 스테이지1·8·13, 같은 리플레이로 비교했다. 퍼즐 배치·게임 규칙·공용 AppConfig·UI·카메라 입력은 수정하지 않았다. 프리뷰만 새 환경을 사용한다.

비교 페이지: `C:/unityjam/shared-assets/seven-steps/concepts/themes/ruins-henge-planets/unity/phase2/index.html`. 원본 화면은 `before/`, 개선 화면은 `after/`, 로그는 `logs/`에 있다. 재생성은 `powershell -NoProfile -ExecutionPolicy Bypass -File design/art/build-unity-phase2-comparison.ps1`이다. HTML에서만 브라우저 흰 여백을 숨기며 PNG를 편집하지 않는다. 시안은 `../../01-moss-orbit/board.png`를 함께 보여 주되 실제 레벨 배치가 다름을 표시했다.

## 변경과 제작 기준

- 벽: 원본 FBX의 분할·베벨 메시를 유지하고 5개 복제 Mesh에 UV2 스타일/높이 데이터를 넣었다. 청회색 `#60778A`와 높이 명도차를 URP 셰이더로 적용한다. 벽 높이는0.55×0.75=0.4125m, 규격0.6m 이하다. 밝은 황토색 바닥 중앙과 실루엣·색·높이로 구분한다. 바닥 중앙의 이끼는 늘리지 않고 기존 가장자리/줄눈 마스크를 유지한다.
- 조명: 따뜻한 키 `#FFE2B9` 강도2, 차가운 필 `#A8CFFA` .55, 약한 림 `#E4CEFF` .28, 쿨 앰비언트. 키만 그림자를 켠다. 셰이더에 vertex additional light와 최소 앰비언트를 적용한다. PC/Mobile_RPAsset의 기존 HDR·vertex additional light 설정은 충분해 변경하지 않았다.
- 후처리: 아트 소유 `PreviewPostFX.asset`의 Bloom .08/threshold1.25/Quarter/최대2단, Neutral 톤매핑, 노출+.15/대비+5/채도+8. DOF·모션블러·SSAO는 사용하지 않는다. URL `artfx=off` 또는 Windows `-artfxoff`로 같은 빌드에서 끈다. 렌더 파이프라인 공용 GlobalSettings는 변경하지 않았다.
- 행성: 프리뷰 전용 `RuinsHengeEnvironment`가 실제 W/H에 맞춘 둥근 사각 정상부를 y=-.12에 생성한다. 보드 아래 곡률·층진 청회색 암석, 좁은 이끼 테두리와 풀 카드/깨진 돌을 배치한다. 넓은 녹색 원판을 제거했다. 행성은 레벨1처럼 긴 실제 맵에서는 길쭉해진다. 정상부는 평평하며 그리드와 입력을 휘지 않는다.
- 하늘: 카메라 뒤 opaque vertex-color 성운/별 결합 메시1개, 저폴리 원경 행성3개. 텍스처·반투명 안개·실제 들판/산 원경을 추가하지 않는다. 풀과 돌 장식도 결합 메시1개다. 재질은 기존6개를 공유한다.
- 세쌍둥이: 동일 몸 메시와 승인3색을 유지하고 색 링 폭 .028→.075m, 반경 .30→.34m로 확대했다. 외경 .755m로 한 칸 안에 들어간다. 머플러보다 화면에서 넓게 보이는 링을 선택해1280×720에서 B/C를 구분한다. KEEP는 중성 돌이다. B/C/KEEP 전시대를 실제 맵 바깥으로 이동시켜8·13스테이지와 겹치지 않게 했다.

환경은 프리뷰 씬에만 붙는다. 정식 Game 씬 전환 시 이 아트 컴포넌트·Volume·조명·세트도 함께 연결할지는 리드/아키텍트가 결정해야 한다. 공용 씬을 직접 수정하지 않았다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| CompileCheck | PASS, `logs/ruins-phase2-final.log`, Compile OK |
| 새 세트 직접 TileSetValidator | PASS, 같은 로그,0오류/0경고 |
| unity.sh tileset-validate | PASS, `logs/batch-tileset-validate.log`; 기본 검색 범위는 Data/TileSets라 새 Art 경로는 위 직접 검사로 보완 |
| EditMode | PASS221/221, `logs/editmode-results.xml`, `logs/batch-test.log` |
| 재생성 | PASS, 기존85에셋 GUID 유지, A/B/C/KEEP 몸 Mesh 공유 |
| Windows 빌드/레벨1 | PASS, `logs/ruins-phase2-win.log`, RRRRRRR→Win/used7 |
| WebGL 빌드 | PASS, `logs/ruins-phase2-webgl.log`, Mobile_RPAsset |
| 표준 smoke-webgl | **FAIL(exit1)**, 옛 보라색 검사1/8/13=90/64/43, 콘솔 예외0 |
| 새 팔레트 검사·실화면 | PASS, 크림 픽셀1/8/13=41738/28296/25420, 실제 보드·벽·행성 표시 |

표준 smoke 실패는 숨기지 않는다. 기존 `board-pixels.ps1`이 보라색만 인정해 새 황토 바닥을 놓친다. 공용 검사는 수정하지 않았고 아트 전용 `check-ruins-pixels.ps1`로 보완했다. WebGL 콘솔에는 비활성 DOF/Panini/FSR 셰이더 스트리핑 경고가 있지만 Bloom/톤매핑 오류나 런타임 예외는 없다.

RTX3060 로컬 브라우저, 기본1280×720 캔버스,2초 후5초 표본:

| WebGL | FPS | Unity 할당 MiB | draw calls | 1차 draw calls |
|---|---:|---:|---:|---:|
| 레벨1, FX 켬 | 60.0 | 47.1 | 289 | 279 |
| 레벨8, FX 켬 | 60.0 | 47.4 | 403 | 393 |
| 레벨13, FX 켬 | 60.0 | 47.4 | 357 | 347 |
| 레벨1, FX 끔 | 60.0 | 47.1 | 284 | — |

후처리 켬/끔 모두 해당 짧은 로컬 표본에서60FPS였다. Windows1280×720 켬58.1FPS/295.4MiB/439draws, 끔58.9FPS/293.6MiB/434draws는 WebGL 컴파일 병행 중의 참고 표본이다. 모바일 성능이나 장시간60FPS를 보장하지 않는다. 브라우저 viewport/CSS를 바꾸며 reload한 별도 진단에서는1.2–1.6FPS 표본이 나왔으므로 정상 캡처와 분리해 `after/webgl-resize-diagnostic.console.json`에 보존했다. 기본 페이지 새 탭의 표준 측정60FPS를 판정 근거로 사용하며 리사이즈/백그라운드 재현은 남은 브라우저 진단이다.

## 남은 공용 요청과 한계

1. AppConfig.tileSet 정식 전환과 정식 Game 씬 환경 연결은 리드 소유다.
2. 몸 인덱스별 A/B/C 선택 계약이 없어 실제 조작 말은 아직 A이고 B/C는 프리뷰 전시다. 확대 링은3색 프리팹 모두에 적용했다. KEEP 출신 색 표식 연결도 이 계약 이후다.
3. TileSetValidator의 Art 경로 검색과 smoke 보드 검사를 팔레트 독립으로 확장하는 것은 아키텍트/툴 소유다.
4. HUD의 기존 숫자 원형 UI는 공용 UI 담당 후속이다. 현재 컷은 조명·색·행성 개선이며 시안 수준의 디테일 완성을 주장하지 않는다. draw calls가 레벨8에서403이므로50스테이지 확장 전 메시 결합/배칭을 검토한다.

이미지·FBX·PNG·blend는 공유 폴더에만 두고, 스크립트·문서·Unity 네이티브 텍스트 에셋과 meta만 커밋한다. 추가 외부 에셋·라이선스는 없다.
