# 검토: feat/server ca6d7db (LAN 플레이테스트: 같은 주소 전송 허용, LAN 주소 출력, 방화벽 단락)

검토자: 아키텍트, 2026-11-10. 범위: `server/src/app.mjs`(`/v1/events` 출처 검사), `server/src/index.mjs`, `server/test/server.test.mjs`, `docs/telemetry.md` 6장. 리드 no-commit 병합에서 `node --test` 통과.

**결론: 승인.**

## 리드 질문
- **TRUST_PROXY일 때 X-Forwarded-***: `selfOrigin`은 `trustProxy`가 켜졌을 때만 `X-Forwarded-Proto`·`X-Forwarded-Host`(첫 값)를 쓰고, 꺼져 있으면 `http` + `Host` 헤더. 기존 `?telemetry=` 주입(같은 함수)과 같은 규칙이라 터널 뒤(https 도메인)에서도 페이지 주소와 비교가 맞는다. TRUST_PROXY 없이 터널을 쓰면 `http`≠`https`라 허용 목록으로 떨어질 뿐(지금과 같음).
- **Host 위조로 다른 사이트 POST 통과**: 브라우저는 `Host`·`Origin`을 페이지가 바꿀 수 없다(금지 헤더). evil.example 페이지의 POST는 `Origin: https://evil.example`, `Host: <PC IP>:8787`이라 불일치 → 허용 목록 → 403. 브라우저에서 `X-Forwarded-*`를 붙이면 사용자 정의 헤더라 사전 요청(OPTIONS)이 먼저 가는데, 그 단계의 출처 검사가 403이라 본 요청이 나가지 않는다.
  - 남는 경로 1: **DNS 리바인딩**(evil.example이 PC IP로 풀리게 해서 `Host`와 `Origin`이 모두 evil.example:8787). 이번 변경 전에는 403이었고 이제 통과한다. 결과는 가짜 이벤트 저장뿐(읽기 API는 토큰 보호, 응답에 비밀 없음).
  - 남는 경로 2: curl 같은 비브라우저는 원래 `Origin`을 마음대로 보내거나 빼므로(Origin 없음은 이전부터 허용) 출처 검사는 인증이 아니다.
  - 둘 다 이미 열려 있던 "누구나 이벤트를 보낼 수 있다" 범위 안이라 새 위험으로 보지 않는다. 데이터 오염은 rate limit과 대시보드 빌드·기간 필터로 다룬다.

## 메모 (막지 않음)
- 테스트에 `trustProxy: true` + `X-Forwarded-Host/Proto` 일치 사례 하나, 그리고 TRUST_PROXY가 꺼져 있을 때 `X-Forwarded-Host`를 무시하는 사례 하나를 더하면 이 질문이 회귀 테스트로 남는다.
- LAN 주소 출력은 `HOST`가 `0.0.0.0`일 때만. 기본값이 그것이면 충분.
