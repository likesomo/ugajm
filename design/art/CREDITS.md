<<<<<<< HEAD
# 일곱 걸음 — 리소스 출처 및 채택 상태

갱신: 2026-11-07, Phase 1. **후보와 실제 제작물을 구분**한다. 이번 작업에서는 외부 팩을 다운로드하거나 Unity에 임포트하지 않았다. 무료만 사용하며, CC0/OFL/GPT 생성물까지 모든 바이너리를 git에서 제외한다. 아래는 기존 v0 전체를 감사한 목록이 아니라 이번 아트 작업의 출처 장부다.

## 실제 제작물

| 제작물 | 제작 방식·출처 | 저장 위치·상태 |
|---|---|---|
| 테스트 7 메달, UI 스킨, 몸·발자국·숫자 문, 행운 도장 | OpenAI GPT 이미지 생성, Codex 내장 image_gen, 2026-11-07. 외부 입력 이미지 없음. CC0/OFL/Asset Store 에셋으로 표시하지 않음 | `C:\unityjam\shared-assets\seven-steps\concepts\`의 PNG 4장. 콘셉트만 생성, 게임 반영 전. 프롬프트는 [README](concepts/README.md) |

## 권장 무료 출처 (아직 미채택)

| 제작자 / 팩 | 원문·라이선스 | 용도 |
|---|---|---|
| Kay Lousberg / KayKit Dungeon Remastered | [제작자](https://kaylousberg.itch.io/kaykit-dungeon-remastered), 무료판 CC0 | 바닥·벽·문·아치 |
| Kay Lousberg / Adventurers, Character Animations | [캐릭터](https://kaylousberg.itch.io/kaykit-adventurers), [동작](https://kaylousberg.itch.io/kaykit-character-animations), 무료판 CC0 | 순례자·석상·동작 |
| Kenney / Mini Dungeon, Mini Characters | [환경](https://kenney.nl/assets/mini-dungeon), [캐릭터](https://kenney.nl/assets/mini-characters), CC0 | 일관된 대체 조합 |
| Kenney / UI Pack, Game Icons, Particle Pack | [UI](https://kenney.nl/assets/ui-pack), [아이콘](https://kenney.nl/assets/game-icons), [입자](https://kenney.nl/assets/particle-pack), CC0 | UI·VFX |
| Kenney / RPG Audio, Impact Sounds, Interface Sounds, Music Jingles | [발소리](https://kenney.nl/assets/rpg-audio), [충격음](https://kenney.nl/assets/impact-sounds), [UI음](https://kenney.nl/assets/interface-sounds), [징글](https://kenney.nl/assets/music-jingles), CC0 | 효과음 후보 |
| 우아한형제들 / Jua | [소스·OFL](https://github.com/google/fonts/tree/main/ofl/jua), SIL OFL 1.1 | 한글 제목 (v0에도 Jua가 있으나 이번 작업에서 변경 없음) |
| 길형진 / Pretendard | [소스·라이선스](https://github.com/orioncactus/pretendard), SIL OFL 1.1 | 한글 본문 후보 |
| Noto / Noto Sans KR | [소스·OFL](https://github.com/google/fonts/tree/main/ofl/notosanskr), SIL OFL 1.1 | 한글 대안 |

CC0 크레딧은 자발적으로 표시한다. OFL 폰트를 실제로 배포할 때 저작권·OFL 원문을 함께 보존한다.

## Asset Store 후보 (모두 미다운로드·미채택)

상품별 URL·가격·라이선스·호환성은 [후보 목록](asset-store-shortlist.md)의 각 행이 출처다. 제작자는 Broken Vector, GlassOfCoins, Dungeon Mason, Explosive, Valley Land Games, Nayrissa, Jean Moreno, Unity Technologies, Dustyroom, Bamsfx, Jazz Create Games, YukiPixels다. 이 목록에 이름을 적었다고 에셋 사용 권한을 취득한 것은 아니며 사용자가 직접 무료 라이선스를 취득한다.

무료 Asset Store 상품 원본도 공개 저장소에 올리지 않는다. OFL이 확인되지 않은 Store 폰트 두 종은 비교 후보이며 한글 P0 채택 대상에서 보류했다. 실제 채택 후 상품명·버전·취득 계정 주체·원문 라이선스 파일·공유 경로·가공 내역을 추가한다.

## 추후 크레딧 화면 문구 초안

실제 채택한 항목만 남긴다: `3D: KayKit by Kay Lousberg (CC0) · UI & SFX: Kenney (CC0) · Fonts: Jua / Pretendard (SIL OFL 1.1) · Concept images: GPT image generation`.
=======
# 일곱 걸음 — 에셋 크레딧

> 게임에 쓰는 모든 외부·생성 에셋을 여기에 적는다. 파일 자체는 git 밖 공유 폴더(`C:/unityjam/shared-assets/seven-steps/Assets/`, 작업 공간에서는 `unity/Assets/Shared/`)에 있다.
> 규칙: 무료 에셋만. CC BY-NC 등 비상업 라이선스 금지. CC-BY·OFL은 itch.io 페이지와 게임 크레딧 화면에 표기한다. 자세한 규칙은 `agents/README.md` "에셋 규칙".
> 관리: 아트. 에셋을 추가·교체·삭제할 때 이 표도 함께 고친다.

| 에셋 | 작가 / 출처 | 라이선스 | 표기 의무 | 공유 폴더 경로 | 쓰는 곳 | 추가일 |
|---|---|---|---|---|---|---|
| Jua (주아체) | The Jua Project Authors / https://github.com/google/fonts/tree/main/ofl/jua | SIL OFL 1.1 (`Fonts/Jua-OFL.txt`) | 있음 (폰트 이름 + OFL 고지) | `Fonts/Jua-Regular.ttf` | 모든 UI 텍스트 (`GameAssets.font`) | 2026-11-07 |
| 임시 효과음 11개 (step, lucky, stone, switch, fall, bump, bridge, crumble, undo, win, lose) | 팀 자체 제작: `unity/Tools/gen-sfx.mjs`로 합성 | 자체 제작 | 없음 | `Audio/*.wav` | `GameAssets.sfx*` | 2026-11-07 |

## 게임 크레딧 화면·itch.io용 문구 (현재)

```
Font: Jua by The Jua Project Authors (SIL Open Font License 1.1)
```
>>>>>>> origin/7lucky
