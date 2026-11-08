# GPT 콘셉트 — 크림·청록·금빛

2026-11-07 내장 `image_gen` 호출로 테스트 1장과 콘셉트 3장 생성에 성공했다. 추가 로그인이나 API 키 설정은 필요하지 않았다. CLI/API 우회는 사용하지 않았으며 외부 에셋·상품 스크린샷을 입력 이미지로 제공하지 않았다.

사용자 결정에 따라 **이미지는 git에 넣지 않는다**. 실제 PNG는 아래 공유 폴더에 복사했고, 이 문서만 커밋한다. 다른 PC에서는 리드가 공유 폴더를 전달해야 하며 저장소 clone만으로 이미지가 내려오지는 않는다.

| 파일·실제 경로 | 내용 / 생성 옵션 |
|---|---|
| `C:\unityjam\shared-assets\seven-steps\concepts\imagegen-test-v1.png` | 첫 동작 테스트. 크림 배경 금색 7 메달, 배경 불투명 |
| `C:\unityjam\shared-assets\seven-steps\concepts\ui-skin-v1.png` | 패널, 기본·hover·pressed 버튼, 청록 6칸+금색 1칸, KEEP/SWITCH 카드. 배경 불투명 |
| `C:\unityjam\shared-assets\seven-steps\concepts\icons-v1.png` | 살아 있는 몸·석상, 발자국 두 색, 숫자 문 1~7. 배경 불투명 |
| `C:\unityjam\shared-assets\seven-steps\concepts\lucky-stamp-v1.png` | 바닥용 평면 7 도장, 동심원·광선·발자국. `transparent_background=true` |

모두 원본 출력 그대로이며 별도 크롭·배경 제거·색 보정은 하지 않았다. 테스트와 UI·아이콘은 1536×1024, 도장은 1254×1254 출력이다. 픽셀 치수와 알파는 파일 검사 결과를 따른다.

## 아트 방향과 선택지

추천 A: 크림 패널 + 짙은 회청색 테두리 + 청록 이동 표시 + 금색 일곱 번째 걸음. 둥근 로우폴리 KayKit과 연결하기 쉽고, KEEP은 돌 몸, SWITCH는 발자국·화살표로 구분한다. B는 같은 형태에서 패널만 어두운 석재로 바꾸는 후속 선택지이며 아직 생성하지 않았다. 사용자는 A 유지 또는 B 추가 탐색을 선택하면 된다.

눈으로 확인한 결과 UI의 7칸과 KEEP/SWITCH 철자는 맞으며 숫자 문도 1~7이 각각 보인다. 아이콘 시트에는 일반 발자국의 금색 변형과 캐릭터 금장도 생성되었으므로 최종 제작에서는 금색을 일곱 번째 상태에 한정한다. 도장은 내부 크림색 면이 있어 바닥을 가릴 수 있으므로 실제 데칼 제작 시 링·숫자 중심으로 단순화할지 결정한다. 테스트 메달은 두께·월계수 장식 때문에 실제 바닥 도장보다 복잡하여 방향 비교용으로만 보존한다.

이 PNG들은 승인용 시안이다. 최종 스프라이트 아틀라스·9-slice·TileSet·프리팹은 아니다. UI 카드의 글자와 숫자 문은 구현 때 Jua/Pretendard 또는 TMP로 분리하고, 7칸 상태도 코드로 구성한다. 몸 아이콘은 32/64px에서 얼굴·가방을 덜어낸 실루엣으로 다시 정리한다. 일곱 번째 상태에는 색뿐 아니라 별·숫자·링 형태를 함께 써서 구분한다.

## 사용한 프롬프트 원문

### 동작 테스트

```text
Small test concept for Seven Steps puzzle game: one simple golden circular lucky stamp containing digit 7, warm cream background, flat rounded low-poly friendly graphic, no other text.
```

### UI 스킨

```text
Use case: ui-mockup. Concept sheet for Seven Steps cozy low-poly dungeon puzzle, clean front-on UI components on cream background. Consistent rounded chunky shapes, dark slate outlines, parchment cream panels, muted teal accents, warm gold reserved for step seven. Show large panel, normal/hover/pressed buttons, exactly seven circular step pips in one row (six teal, final gold star), two large decision cards with exact text KEEP and SWITCH: KEEP has grey stone pilgrim body silhouette, SWITCH has teal footprint and curved arrow. Flat readable minimal graphics, restrained shadows, ample spacing, no ornamental clutter, no other text. Concept only, no screenshot frame.
```

### 몸·발자국·숫자 문

```text
Use case: stylized-concept. Seven Steps puzzle game icon concept sheet, front-facing cream background, generous separate cells. Rounded chunky flat shapes dark slate outlines, muted teal and warm gold accents, cozy low-poly dungeon UI aesthetic. Show two body icons (hooded living pilgrim teal, same silhouette grey stone statue), two simple boot footprint icons, and exactly seven stone arch number-door badges labeled with single clear digits 1 2 3 4 5 6 7 respectively. Gold only on 7 badge. Clear at small HUD sizes, no tiny details, no other text, concept sheet not production atlas.
```

### 행운 도장

```text
Use case: stylized-concept. Seventh step lucky stamp concept for Seven Steps cozy dungeon puzzle. Single top-down flat circular seal, central unmistakable digit 7, two concentric chunky golden rings, seven short radial rays, tiny boot print below digit, dark amber outline. Warm gold and pale cream only. Minimal rounded shapes, readable as 64px floor decal, no laurel, no coin thickness, no drop shadow, no text besides 7, no perspective. Isolated on genuinely transparent background.
```

출처·채택 상태는 [CREDITS.md](../CREDITS.md)에 기록했다. 이미지는 Unity 프로젝트에 임포트하지 않았다.
