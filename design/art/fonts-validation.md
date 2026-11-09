# 손글씨 EN/KO 전달·검증

추천 세트: EN 제목 Kaushan Script / 본문 Patrick Hand, KO 제목·본문 Gaegu, 공통 숫자 Jua. 라이선스·대안·실측은 [fonts.md](fonts.md), UiTheme 필드 요청은 [ui-theme.md](ui-theme.md).

출력 루트: `C:/unityjam/shared-assets/seven-steps/concepts/ui/`. 비교 페이지 `en-ko.html`, 이미지 `en/`·`ko/` 각10PNG(1280×720), 별도 글리프 근거 `font-coverage.txt`, WebGL 바이트 근거 `font-build-sizes.json`.

## 결과

- 실제 TTF 렌더20PNG: title, stage-select, hud, result(2별), result-1, result-3, loading, chapter-entry, language(첫 실행), settings.
- HUD는 설명용 격자 도식이다. 정식 게임 씬·입력·언어 서비스·번역 테이블을 변경하지 않았다. 화면 글자는 GPT가 생성하지 않고 Unity가 TTF로 렌더했다.
- GlyphCheck: Gaegu/Jua/Gamja Flower/Hi Melody의 현재 게임 UI·레벨·크레딧에서 모두 누락0.50스테이지 R1 합류 뒤 다시 통과.50단계 story/7행성의 한글도 누락0.
- role별 cmap 검사: EN 제목66자·본문66자, KO206자, 숫자18자에서 누락0. 영문 전용 폰트로 한글 전체 검사를 통과했다고 주장하지 않는다.
- 두 언어 모두2줄 힌트가 보이며, 첫 실행 English/한국어 자기 이름은 해당 언어 폰트로 그린다. 숫자 카운터·문 `=N`·별 합계는 Jua로 분리했다.
-7행성 moss/hollow/carved/frost/joinery/crumble/altar, 잠금 없음, 개별 스테이지3별·장 합계. 결과 평가 조건은 미확정이라 목표2/3 중립 문구를 썼다. [별 평가 검토](../levels/stars.md)의 결론을 아트에서 확정하지 않는다.
- 기존 스킨/트림/버튼4상태 유지, 실제 폰트를 넣은 HandwrittenButton_EN/KO 프리팹 전달. 공용 UiTheme 타입은 수정하지 않았다.

## 테스트·재현

1. `HandwrittenFontBuilder.Windows`: cmap 검증 + 아트 전용 프리뷰 씬 + Windows 플레이어 빌드.
2. `UiFonts.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -ui-capture <concepts/ui>`:20PNG 캡처, 렌더 로그에 Captured20 및1280×720.
3. `HandwrittenFontBuilder.BaselineWebGL`와 `HandwrittenWebGL`: 같은 동결 런타임의 Jua-only / 손글씨 조합 비교 빌드. WASM 해시가 같은지 확인하고 Build 폴더 파일을 합산한다.
4. `HandwrittenFontBuilder.CompileAndPrepare`: 최종 폰트/cmap 재검사와 CompileCheck. 공용 생성 파일 변경은 커밋에서 제외한다.
5. `powershell -NoProfile -ExecutionPolicy Bypass -File design/art/build-font-gallery.ps1`: EN/KO 나란히 비교 페이지 재생성. 기존 전체 갤러리 맨 위 UI EN/KO 섹션에서 링크한다.

공유 `font-logs/`에 최종 Windows/WebGL/Compile·캡처 로그를 복사한다.50스테이지 R1 베이스83ba801의 최종 비교에서 WebGL 증가량은863,413bytes(843.18KiB)이며 정확한 바이트와 동일 WASM 해시는 `font-build-sizes.json`을 따른다. 정식 전체 게임 빌드·모바일 성능을 계측한 수치가 아니다.

## 남은 연결

씬 플로우/아키텍트: locale별 title/body/number 폰트 필드와 조회 API, 번역 테이블 기반 role별 GlyphCheck, 언어 선택 저장/적용. 기존 단일 UiTheme.font는 구형 View의 기본값으로 유지한다. UI 담당: 실제 Title/HUD/로딩에 이 스타일을 연결하고 PreferredHeight/터치/긴 번역을 확인한다. Gaegu는2350음절이므로 새 문구의 빌드 전 검사와 미지원 입력 정책을 유지해야 한다. 폰트 파일은 공유 폴더에만 있으며 모든 OFL 고지와 원본 이름을 보존했다.
