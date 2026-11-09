# EN/KO 손글씨 폰트 — 2026-11-09

추천: **영문 제목 Kaushan Script Regular / 영문 본문 Patrick Hand Regular / 한글 제목·본문 Gaegu Regular / 숫자 Jua Regular**. 제목은 붓의 흐름, 본문은 열린 획, 숫자는 수량 판독을 우선한다. 기존 남색 패널·석재/목재 트림·버튼4상태는 유지한다. 정식 UiTheme 타입과 언어 전환 코드는 바꾸지 않았다.

## 후보 비교

아래 심미성·가독성 평가는 이 게임의1280×720 화면을 위한 아트 판단이다. 라이선스와 배포 파일은 링크한 공식 저장소를 근거로 한다. 선택 파일은 모두 웹 요청 없이 앱에 내장할 수 있는 정적 TTF다.

| 역할·후보 | 평가와 선택 | 공식 출처/라이선스 |
|---|---|---|
| EN 제목 **Kaushan Script** | 붓글씨 기울기와 단단한 획이 유적/여정에 어울림; 제목48–70px에만 사용. 선택 | [정적 TTF](https://github.com/google/fonts/tree/main/ofl/kaushanscript), [OFL1.1](https://github.com/google/fonts/blob/main/ofl/kaushanscript/OFL.txt) |
| EN 제목 Dancing Script | 연결 필기체의 부드러움은 좋지만 작은 제목에서 장식적인 인상이 더 큼. 대안, 미다운로드 | [배포/OFL](https://github.com/google/fonts/tree/main/ofl/dancingscript) |
| EN 제목 Satisfy | 유연한 필기체 대안. 허용 라이선스지만 이번에는 OFL 관리 통일을 선호해 제외, 미다운로드 | [TTF/Apache2.0](https://github.com/google/fonts/tree/main/apache/satisfy) |
| EN 본문 **Patrick Hand** | 비연결 손글씨, 열린 내부 공간과 수직 획, 긴 버튼에서 읽기 편함. 선택 | [정적 TTF](https://github.com/google/fonts/tree/main/ofl/patrickhand), [OFL1.1](https://github.com/google/fonts/blob/main/ofl/patrickhand/OFL.txt) |
| EN 본문 Caveat | 여백 있는 필기체, 짧은 팁/주석에 적합; 기본 버튼보다 보조 문구 대안. 미다운로드 | [배포/OFL](https://github.com/google/fonts/tree/main/ofl/caveat) |
| EN 본문 Kalam | 고른 획의 손글씨, 본문 대안; 이번에는 Patrick Hand의 간결한 형태를 선택. 미다운로드 | [정적 TTF/OFL](https://github.com/google/fonts/tree/main/ofl/kalam) |
| KO **Gaegu** | 붓의 강약보다 연필 같은 편안한 결. EN 비연결 본문과 잘 맞고 기존 문구 전체 지원. 선택 | [정적 TTF](https://github.com/google/fonts/tree/main/ofl/gaegu), [OFL1.1](https://github.com/google/fonts/blob/main/ofl/gaegu/OFL.txt) |
| KO Gamja Flower | 둥근 손글씨, 현대 한글11172음절 지원. 전체 한글 입력이 필요하면 대안;12MiB 원본 비용 큼 | [정적 TTF/OFL](https://github.com/google/fonts/tree/main/ofl/gamjaflower) |
| KO Hi Melody | 가벼운 손글씨,11172음절 지원. 작은 화면에서 획이 얇아지는 점과12MiB 비용을 고려해 대안 | [정적 TTF/OFL](https://github.com/google/fonts/tree/main/ofl/himelody) |
| KO 본문/숫자 **Jua 유지** | 기존 두꺼운 획과 숫자에 익숙함. 손글씨 느낌은 약하지만20px 이하 본문 대안 및 숫자 최종 추천 | [배포/OFL](https://github.com/google/fonts/tree/main/ofl/jua) |

## 파일·용량·글리프 실측

공유 경로 `Assets/Shared/Fonts/<Family>-Regular.ttf`와 `<Family>-OFL.txt`. Jua는 기존 파일 재사용, 나머지5종은 공식 Google Fonts의 TTF/OFL만 다운로드했다. 폰트를 변형하거나 서브셋으로 재작성하지 않았다. 바이너리·폰트 meta는 공유 폴더에 유지하며 삭제/재생성하지 않는다. [`fetch-handwritten-fonts.ps1`](fetch-handwritten-fonts.ps1)은 기존 파일을 덮어쓰지 않는 재현용 다운로드 스크립트다.

| 폰트 | 원본 bytes | KiB | cmap 항목 | 한글 음절 |
|---|---:|---:|---:|---:|
| Kaushan Script |210672|205.7|397|0|
| Patrick Hand |214772|209.7|515|0|
| Gaegu |3165816|3091.6|2593|2350|
| Jua(기존) |2119352|2069.7|2519|2367|
| Gamja Flower(후보) |12615444|12319.8|12020|11172|
| Hi Melody(후보) |12623412|12327.6|12102|11172|

새 선택 폰트3종 원본 합계3,591,260bytes이고 기존 Jua도 숫자로 남는다. 미사용 Gamja/HiMelody는 비교용이며 프리뷰/게임 씬에서 참조하지 않는다. Unity import는 Dynamic·Include Font Data=true, 별도 웹폰트·OS 설치·런타임 CDN 요청을 쓰지 않는다. 가변 TTF 인스턴스 변환과 전체 한국어 SDF 아틀라스 생성도 하지 않았다.

Gaegu는 모든 현대 한글 음절을 지원하는 폰트가 아니다. **현재 표시 텍스트 전체를 지원하는 게임 텍스트 집합 방식**으로 선택한다. 현재 게임 UI·레벨·크레딧을 공용 `GlyphCheck.Check`로 검사했으며 Gaegu/Jua/Gamja/HiMelody 모두 missing0이다.50스테이지 `story-50.json`·`chapters.json`의 한글도 Gaegu에서 누락0이다. 시안 코드의 지역화 문구는 `GlyphCheck.ReadCmap`을 재사용해 EN 제목/본문의 실제 Latin·구두점66종, KO206종, 숫자18종에서 누락0을 확인했다. 최종 표본이 바뀌면 `font-coverage.txt`의 최신 수치를 따른다.

영문 전용 폰트를 전체 한글 UI 검사에 넣어 통과했다고 주장하지 않는다. 공용 단일-font GlyphCheck는 KO 본문 Gaegu 및 안전한 기본 Jua로 통과시키고, 영문 role은 실제 영문 문구로 범위를 제한한 cmap 검사를 한다. 언어별 필드가 도입되면 씬 플로우가 locale별 번역 테이블·role별폰트를 함께 검사해야 한다. 새 한글/사용자 입력이 필요하면 빌드 전 검사로 막고, 전체 음절을 지원하는 후보나 승인된 fallback을 추가한다. Jua도 전체 음절 폰트가 아니므로 무조건 안전한 한글 fallback이라고 간주하지 않는다.

숫자1–7, 숫자 문, 석상 수 `=N`, 별 합계·걸음 수는 **양 언어 모두 Jua**를 쓴다. 손글씨의1/l/I,0/O,7의 장식 획을 퍼즐 수량과 혼동하지 않게 한다. 연산자는 `=`, `/`, `+`, `-` 등 실제 쓰는 문자를 cmap로 검사하며 상태는 색과 문구를 병기한다.

## 실제 렌더·재현

`HandwrittenFontBuilder.Windows`가 아트 전용 `Art/Test/HandwrittenUI.unity`와 Windows 프리뷰를 만든다. 실행 인자 `-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -ui-capture <concepts/ui>`로 EN/KO 각8화면(타이틀·스테이지·HUD·결과·로딩·챕터·첫 언어 선택·설정) 및 결과1별/3별 상태를 실제 Unity Text/TTF로 캡처한다(총20PNG). 배경만 GPT 아트이며 텍스트는 생성 이미지에 포함되지 않는다. HUD의 격자는 읽기 비교용 도식이며 플레이 가능한 스테이지가 아니다. 스테이지 선택은7행성(moss/hollow/carved/frost/joinery/crumble/altar), 잠금 없음, 개별 스테이지3별과 챕터 합계다. 허구의 점수 기준을 확정하지 않는다.

WebGL은 같은 프리뷰 씬의 Jua-only 대조군과 손글씨 조합을 각각 빌드한다. **비교용 UI 빌드**이며 정식 전체 게임 빌드와 절대 용량을 섞지 않는다. 폰트 data 증분과 전체 Build 파일 증분을 별도로 기록한다. 실제 결과는 공유 `concepts/ui/font-build-sizes.json` 및 완료 보고에 기록한다. UI 레이아웃·폰트·주석 외 공용 씬/타입 변경은 없다.

최종 동결된 런타임 코드로 재빌드한 결과:

| gzip 배포 파일 | Jua-only 대조군 | 손글씨 EN/KO | 증분 |
|---|---:|---:|---:|
| data.unityweb |6,680,826 B|7,544,239 B|863,413 B|
| wasm.unityweb |9,561,781 B|9,561,781 B|0 B, SHA256도 동일|
| framework.js.unityweb |76,702 B|76,702 B|0 B|
| loader.js |48,779 B|48,779 B|0 B|
| Build 폴더 합계 |16,368,088 B|17,231,501 B|**843.18 KiB, 약5.27%**|

50스테이지 R1 베이스83ba801에서 같은 배경·UI 키트·동결된 런타임 코드로 폰트 참조만 바꾼 비교다. data의 gzip 헤더(1F8B)를 확인했다. HTML/TemplateData 및 정식 게임의 나머지 리소스를 포함한 전체 제품 다운로드 크기가 아니다. 원본 폰트의 상주 메모리와 압축 전송 크기는 다르다. 향후 Jua를 숫자만으로 서브셋화하면 더 줄일 수 있지만 현재는 구형 UI도 Jua를 쓰므로 기존 파일을 재사용하고 파생 폰트를 만들지 않는다.

기존 트림/버튼 상태를 그대로 쓰는 `Art/UI/HandwrittenButton_EN.prefab`과 `HandwrittenButton_KO.prefab`도 전달했다. Font만 Patrick Hand/Gaegu로 바뀌며9-slice·SpriteSwap·사이즈는 동일하다. 제목/본문/숫자 폰트 세트의 정식 UiTheme 연결은 [필드 요청](ui-theme.md)을 따른다.
