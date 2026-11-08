# 3단계 플레이테스트 1차 — 준비 목록

작성: 리드, 2026-11-08 (2.5단계 정리 중). 소유: 리드. 항목이 끝나면 체크하고 커밋 해시를 적는다.

**사용자 승인 전에는 하지 않는 것**: 서버 외부 공개(Cloudflare Tunnel, Fly.io 등), itch.io 업로드. 아트 파일(`design/art/**`, `Assets/Shared/**`)은 아트 테마 리서치(Codex 워커)가 끝날 때까지 건드리지 않는다.

## A. 빌드 (리드)

- [ ] Unity 라이선스 복구 (2026-11-08 배치 실행이 전부 exit 198 "No valid Unity Editor license found". 사용자가 Unity Hub에 로그인)
- [ ] 7lucky에서 `compile` · `test` · `verify` 통과
- [ ] `build-webgl` → `smoke-webgl` (3개 스테이지 보드 픽셀 ≥500, 콘솔 예외 없음). 로그의 "1 URP assets included in build" 다음 줄이 Mobile_RPAsset인지 확인
- [ ] `build-win` → `smoke` (리플레이 4개, 예외 없음) — 진행자 PC용 예비
- [ ] 테스트용 빌드 커밋 해시를 이 문서와 `design/levels/playtest-guide.md`에 적는다
- [ ] 17스테이지 전부 직접 한 번 클리어(풀이 H로 확인) — 레버·석상 수 문·0 문 표시가 Unity에서 보이는지

## B. 테스트 대상 결정 (사용자)

- [ ] **무엇으로 테스트하나**: Unity WebGL(권장, 실제 출시물) / 웹 프로토타입(`/play/`, 지금 가이드 기준). 둘 다 같은 텔레메트리 스키마라 대시보드는 같이 쓴다
- [ ] **어디서**: 진행자 PC 현장 테스트(외부 공개 불필요, `localhost`) / 원격 테스터(→ 서버 외부 공개 필요, 사용자 승인 사항)
- [ ] 테스터 인원·일정 (목표 5명 이상, 1인 40~50분)

## C. 수집 서버 (백엔드/분석)

- [ ] `STATS_TOKEN=<토큰> node server/src/index.mjs` 로컬 실행, `node --test server/test/` 통과
- [x] 서버가 Unity WebGL 빌드를 `/webgl/`로 제공하고 `?telemetry=<이 서버>`를 자동으로 붙인다 (feat/server d6296cf). 실제 빌드에서 unity-webgl 이벤트 21건 수신 확인. 당일 절차: `server/README.md` "플레이테스트 날 실행 절차"
- [ ] 새 WebGL 빌드(라이선스 복구 후)로 `/webgl/` 다시 확인. `unity.sh serve`(8090)로 열 때는 `?telemetry=http://localhost:8787`을 붙여야 기록됨 (`AppConfig.telemetryEndpoint`가 비어 있음)
- [ ] **사용자 결정**: itch.io 빌드는 iframe이라 `?telemetry=`를 줄 수 없다 → `AppConfig.telemetryEndpoint`에 고정 HTTPS 주소가 필요(trycloudflare 임시 주소는 실행마다 바뀜). 원격 테스터는 서버가 `/webgl/`·`/play/`를 같이 제공하므로 터널 1개(`TRUST_PROXY=1`)로 둘 다 된다(`server/README.md` 절차 3). `ALLOWED_ORIGINS`는 다른 도메인에서 빌드를 열 때만
- [ ] WebGL 빌드에서 이벤트가 실제로 쌓이는지(`level_start` → `level_clear`, 탭 닫을 때 `session_end` beacon) 대시보드로 확인 — 기존 빌드 ef67c4d로 확인됨(unload·hidden 둘 다). Orca 탭 닫기는 페이지 이벤트가 없어 안 오지만 서버가 30분 무입력을 이탈로 처리
- [ ] 테스트 전 DB 초기화 방법(가짜 데이터 `fake-data.mjs` 섞이지 않게) 정리
- [ ] (승인 후에만) 원격 테스터용 Cloudflare Tunnel 주소 → `?telemetry=` 또는 `AppConfig.telemetryEndpoint`

## D. 진행 안내 (레벨 디자인)

- [x] `design/levels/playtest-guide.md`를 Unity WebGL 겸용으로 갱신, 6 숫자 문·힌트 문구 결정 기준 추가 (feat/levels 76228af) — 주소, 조작(방향키/WASD/클릭, K/X/Z/R/H), 시크릿 창, "기록 전송" 확인 방법
- [x] 스테이지별 관찰표(3장), 메모 양식 → `design/levels/playtest/TEMPLATE-notes.md` (feat/levels 7f9e6e0)
- [x] 끝난 뒤 질문(4장) 양식 (같은 파일)
- [ ] 테스트 뒤: 대시보드 CSV/JSON으로 흥미도 모델 보정, 8(내리는 벽)·6(숫자 문) 결과에 따라 `proposal-polish.md` 1.1의 후속안 결정

## E. 게임 화면 (게임플레이/UI)

- [ ] feat/view 5a2180a 합치기 (최소 몸 수 표시, 힌트 상시 표시, Esc로 설정 닫기) — 라이선스 복구 후 compile+test
- [ ] 첫 실행 시 텔레메트리 동의/전송 상태가 화면에 보이는지 (테스트 진행자가 확인할 수 있게)
- [ ] 테스터별 플레이어 ID 분리(시크릿 창이면 충분한지, 아니면 `?player=` 같은 진행자용 옵션이 필요한지) 결정
- [ ] 클리어 화면·스테이지 선택에서 다음 스테이지로 막힘 없이 넘어가는지

## F. 아트 (Codex 워커, 리드는 대기)

- [ ] 테마 리서치(7을 행운의 수로 여기는 문화권) 결과 → 사용자 선택
- 1차 테스트는 **임시 아트(URP 프리미티브)로 진행 가능**. 아트 교체는 테스트 일정을 막지 않는다. 바뀐다면 테스트 전에 `tileset-validate` 통과 필수

## G. 1차 테스트에서 답을 얻을 질문

1. 7번째 걸음 규칙을 설명 없이 이해하는가 (1~2)
2. 3(KEEP+발판 동시), 8(예측 최저 0.36), 16(종합)에서 실제로 막히거나 지치는가
3. 6(숫자 문)이 너무 쉬워 지나치는가
4. 힌트 문구가 규칙을 너무 많이 알려 주는가 (`proposal-polish.md` 2장)
5. 탑다운 임시 아트에서 상태(석상, 레버, 숫자 문 열림)를 읽을 수 있는가 → 3D 쿼터뷰·아트 우선순위
