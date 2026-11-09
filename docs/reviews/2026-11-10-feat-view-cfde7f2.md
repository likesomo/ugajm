# 검토: feat/view cfde7f2 (챕터 머티리얼 교체를 하늘·행성에도, Fit 뒤 재적용)

검토자: 아키텍트, 2026-11-10. 직접 확인(임시 브랜치 7lucky 10fa4ce + feat/view): EditMode 745/745. 게임플레이/UI 보고: build-webgl, smoke-webgl 1/29/50 콘솔 오류 0, `?level=22` frost 스크린샷.

**결론: 승인.**

- `ChapterMaterials.Apply(root, chapter)`: BoardView에 있던 교체 코드를 그대로 옮긴 공용 함수. `sharedMaterials` 배열을 한 번 교체(인스턴스 복사·매 프레임 작업 없음, 12.8).
- `ChapterStage`: 하늘·행성 인스턴스를 만들 때 한 번, `Fit` 뒤 한 번 더(행성 `Fit`이 메시·렌더러를 새로 만들기 때문, 93477aa 요청). 스테이지당 한 번이라 비용 문제없음. 테스트가 "Fit에서 렌더러를 만드는 행성"을 흉내 낸다.
- 지금 7개 프리셋의 오버라이드는 `BaseAtlas → Environment_<id>` 하나씩이다. 보드 타일과 행성 정상부가 같은 `BaseAtlas`를 쓰므로 둘 다 행성 재질로 바뀐다(의도대로).

## 메모
- **하늘에는 지금 효과가 없다**: `RuinsHengeSky`는 하늘 메시를 `Start`(인스턴스 다음 프레임)에서 **카메라 밑**에 만든다(034c910 검토 A1). `Apply(sky, …)`는 인스턴스를 만든 직후 하늘 인스턴스 자식만 보므로 그 메시에 닿지 않는다. 하늘 재질(`MarkUnlit`)은 오버라이드 대상이 아니라 지금 화면에는 문제없다. 아트가 하늘 재질을 챕터별로 바꾸고 싶어지면 `SkyPreset`의 하늘 프리팹 필드(`nebulaTint` 등)로 하거나, 하늘이 자기 메시를 인스턴스 아래에 만들도록 바꾼 뒤 이 경로를 쓴다.
- 행성 `Fit`이 `Start`가 아니라 `ChapterStage.Fit`에서만 메시를 만든다는 전제다(지금 `RuinsHengePlanet`에 `Start`/`Awake` 없음, 확인함). 아트가 `Start`에서 메시를 만들기 시작하면 이 재적용을 비켜 간다 — 12.8 문장으로 남긴다: "환경 프리팹은 렌더러를 `Fit` 안에서만 만든다".
