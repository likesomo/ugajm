# 튜토리얼 보드 계약 (T1~T3)

2026-11-12, 코어 작성(리드 결정, 사용자 요청 `shared-assets/seven-steps/reports/user-request-2026-11-12-story-tutorial.md` C). 이야기 배경: `design/story/cutscenes.md` 프롤로그 3~6장.
담당: 코어(데이터·규칙 검증·cue 판정 `TutorialCues`·저장 id), 게임플레이/UI(cue 표시·강조 칸·흐름 연결), 씬 플로우(새 게임 → 프롤로그 → T1..T3 → 스테이지 1, 맵/타이틀 다시 하기), 아키텍트(`LaunchOptions`), 백엔드/분석(텔레메트리 여부).

## 1. 무엇인가

- 스테이지 1 앞의 작은 보드 3개(약 5×5). 차례대로 한다.
  - **T1** (몸 1): 이동과 걸음 수. 출구를 **정확히 7번째 걸음**으로 밟는다.
  - **T2** (몸 2): 7번째 걸음만 일을 한다. 발판 위에서 7번째를 마치고 KEEP → 선돌이 발판을 눌러 문이 열린다. 다음 형제가 출구로.
  - **T3** (몸 3): **SWITCH를 직접 한다**(2026-11-13). 출구가 출발점에서 14걸음이라 한 형제로는 못 닿는다. 첫째가 발판에서 KEEP(문이 열림), 둘째가 중간에서 일곱째 걸음을 마치고 SWITCH(돌 없이, 막내가 바로 그 자리에서 깸), 막내가 거기서 출구까지 7걸음. KEEP 하나와 SWITCH 하나가 모두 있어야 풀린다(`expected.needsSwitch`, 최소 몸 수 3). 시작 cue가 목표를 말한다: "셋이 모두 걸어야 별 셋이 켜진다. 큰형은 먼저 발판으로."
  - 규칙 문구(리드 정정 2026-11-13): **KEEP** = 이 형제는 여기 선돌로 남고, 다음 형제는 지금 출발점에서 깬다. **SWITCH** = 돌을 남기지 않고, 다음 형제가 **바로 이 자리**에서 깨며 이 칸이 새 출발점이 된다.
- **50스테이지와 별개**: 스테이지 번호, 별 합계, 클리어 수, `ProgressData.stages`, 텔레메트리 스테이지 번호 어디에도 들어가지 않는다.
- **실패해도 괜찮다**: `Lose`가 나면 화면이 `lose` cue를 잠깐 보여 준 뒤 자동으로 Restart 한다(2.4). 건너뛰기 가능.

## 2. 데이터

### 2.1 원본과 내보내기

- 원본: `prototypes/seven-steps/src/levels.js`의 `export const TUTORIALS = [...]` (`LEVELS`와 따로, 플레이 순서).
- `unity.sh levels`(`export-levels.mjs`)가 `levels.json`의 최상위 `tutorials` 배열로 내보낸다(`levels` 50개 옆). JS 솔버 답(`expected`)도 스테이지와 같은 모양으로 싣는다.
- 검사(export, 실패하면 파일을 쓰지 않음): 모두 솔버로 풀린다, `bodies <= 3`, `level.name`이 스테이지와 튜토리얼 전체에서 겹치지 않는다, `id` 형식·중복, cue 형식(2.3), 번역(`text`) 빠짐 없음, `·`(U+00B7) 금지(Gaegu 글꼴에 없음). 그리고 보드마다 **설계 단정**: `expected.minBodies == bodies`(T3은 셋이 모두 걸어야만 풀림, T1·T2도 몸을 남기지 않음). 또 보드 중 하나 이상이 `expected.needsSwitch`(SWITCH 없이는 못 푸는 보드, 지금 T3).
- `unity.sh verify`(`LevelVerifier`)가 튜토리얼도 C#↔JS 비교한다. 골든(`gen-golden.mjs`)도 튜토리얼 보드를 `tutorial:<id>` 트레이스로 싣는다.

### 2.2 `levels.json` 모양

```json
{
  "steps": 7,
  "levels": [ ...50개, 기존 그대로... ],
  "tutorials": [
    {
      "id": "t1",
      "version": 1,
      "level": { "name": "첫 일곱 걸음", "hint": "...", "bodies": 1, "chapter": "moss", "startLine": "",
                 "text": [{ "lang": "en", "name": "...", "hint": "...", "story": "" }],
                 "map": ["######", "..."], "expected": { "minBodies": 1, "moves": 7, "needsSwitch": false, "path": "RRRDDLL" } },
      "cues": [
        { "on": "start", "line": "방향키로 한 칸씩 걷는다.", "text": [{ "lang": "en", "line": "Arrow keys: one cell per step." }], "cell": [] },
        { "on": "step", "n": 6, "line": "다음이 일곱째 걸음.", "text": [{ "lang": "en", "line": "Next is the seventh step." }], "cell": [2, 3] }
      ]
    }
  ]
}
```

C# (Core, `Core/Level.cs`, JsonUtility):

```csharp
public sealed class LevelFileDto { public int steps; public LevelDto[] levels; public TutorialDto[] tutorials = new TutorialDto[0]; }

[Serializable] public sealed class TutorialDto {
    public string id;            // "t1".."t3": 소문자·숫자·하이픈, 바꾸지 않는다(저장 id)
    public int version = 1;      // 보드 내용이 크게 바뀌면 올린다 → 끝낸 사람도 다시 "새 보드"로 본다(2.5)
    public LevelDto level;       // 스테이지와 같은 모양. ToLevel()로 Level
    public TutorialCueDto[] cues = new TutorialCueDto[0];
}
[Serializable] public sealed class TutorialCueDto {
    public string on;            // 2.3 trigger 코드
    public int n;                // on == "step": 그 걸음 번호(1..7). 다른 trigger는 0
    public int body;             // 0 = 아무 형제. 1..bodies = 그 번째 형제일 때만(1 = 첫 형제)
    public string line;          // ko 한 줄 (Gaegu 글자만, '·' 금지)
    public TutorialLineDto[] text = new TutorialLineDto[0];   // 번역 { lang, line }, 13장 규칙(ko 외 언어 전부, 빈 값 실패)
    public int[] cell = new int[0];                            // 강조 칸 [x, y] (맵 좌표, x 오른쪽, y 아래) 또는 빈 배열 = 없음
    public string Line(string lang);                           // 그 언어 줄, 없으면 ko
    public bool HasCell { get; }  public int X { get; }  public int Y { get; }
}
[Serializable] public sealed class TutorialLineDto { public string lang; public string line; }
```

- `level.chapter`: 아트 프리셋 선택용(`moss`). 챕터 순서 검사에서 빠진다. `level.story`는 비워도 된다. `level.startLine`은 `""`(시작 줄은 cue가 맡음).
- `level.hint`: HUD의 한 줄 요약(보드 목표). **H 풀이 보기는 튜토리얼에서 끈다**(풀이를 보면 배울 게 없다. 리드가 바꿀 수 있음).

### 2.3 cue trigger

판정은 Core `SevenSteps.Core.TutorialCues`(엔진 없음, 코어 구현·테스트)만 한다. 화면은 돌려받은 cue 번호의 줄과 강조 칸을 보여 준다.

| `on` | 언제 |
|---|---|
| `start` | 보드를 열었을 때 |
| `restart` | Restart(자동 재시작 포함) 뒤. 이 cue가 없으면 `start` |
| `step` | 이동이 `Step` 이벤트를 내고 그 `StepIdx == n` (1..6. 7번째 걸음은 `Step`을 내지 않으므로 `seventh`를 쓴다) |
| `seventh` | 이동이 7번째 걸음으로 끝남(출구 `Win`, `Choose`, `Stone`, `Vanish`, `Bridge`, `Seal` 중 하나를 낸 이동) |
| `choose` | KEEP/SWITCH 고르기 화면이 열림(`Choose` 이벤트) |
| `keep` / `switch` | KEEP / SWITCH 명령이 적용됨 |
| `doorOpen` | `DoorOpen` 이벤트 |
| `respawn` | 다음 형제가 깨어남(`Respawn`) |
| `win` | `Win` |
| `lose` | `Lose` |
| `doorShut` | 이동이 **지금 닫힌 문**에 막힘(`Bump` 이벤트의 칸이 문이고 그 문이 닫혀 있음, 명령 전 상태 기준). 벽 등 다른 Bump는 여전히 줄을 바꾸지 않는다. 2026-11-13 추가: 발판을 붙들 형제 없이 출구로 가려 할 때(형제를 덜 쓰려 할 때) 왜 셋이 필요한지 말한다 |

- `body`: cue를 그 형제일 때만 쓴다. 형제 번호는 **명령 전 상태** 기준 `Stars.BodiesUsed(before)`(1 = 첫 형제). `respawn` cue는 깨어난 형제 번호(명령 후 상태)로 본다.
- **한 명령에서 여러 cue가 맞으면**: 이벤트 순서대로 보고, 같은 이벤트에서는 cue 목록 순서대로 본다. **마지막으로 맞은 cue 하나**를 보여 준다. 하나도 안 맞으면 지금 줄을 그대로 둔다. 같은 cue는 다시 맞으면 다시 보인다(형제마다 같은 안내 가능).
- Undo는 cue를 바꾸지 않는다. Bump(못 가는 칸)도 바꾸지 않는다. 단 닫힌 문에 막힌 Bump는 `doorShut`. 게임은 Bump도 `SessionUpdate`(Feedback)로 `Match`에 넘긴다.
- **SWITCH 뒤 "여기서 깼다"**: SWITCH 명령의 이벤트는 `Switch`, `Respawn` 순서다. 마지막 맞은 cue가 이기므로, SWITCH 뒤 깨어난 형제에게 보일 줄은 `switch` cue에 쓰고 그 형제 번호의 `respawn` cue는 두지 않는다(두면 `switch` 줄을 덮는다). KEEP 뒤 줄도 같은 이유로 `keep`/`doorOpen` cue에 쓴다(T2, T3). 고르기 **전**에 둘의 차이를 말하는 줄은 `choose` cue(형제별 `body`).

```csharp
public static class TutorialCues {
    public static readonly string[] Ons = { "start", "restart", "step", "seventh", "choose", "keep", "switch", "doorOpen", "respawn", "win", "lose", "doorShut" };
    public static bool IsValidOn(string on);
    public static int Start(TutorialDto t);      // "start" cue 번호(마지막 것), 없으면 -1
    public static int Restart(TutorialDto t);    // "restart", 없으면 Start(t)
    // 명령 하나의 결과. Restart 명령이면 Restart(t)와 같다. 맞는 cue가 없으면 -1(지금 줄 유지)
    public static int Match(TutorialDto t, Command cmd, State before, State after, IReadOnlyList<GameEvent> events);
}
```

### 2.4 흐름 (씬 플로우·게임플레이/UI)

- 새 게임: 타이틀 → 프롤로그 8장 → T1 → T2 → T3 → 스테이지 1. 건너뛰기를 누르면 스테이지 1로(건너뛴 보드는 끝낸 것으로 저장하지 않는다).
- 맵/타이틀: "튜토리얼"을 선택 항목으로 두고 언제든 다시 할 수 있다(별·클리어 표시 없음, 끝냈는지만).
- 결과: 튜토리얼 클리어는 짧은 결과(별 셋 연출은 T3만, `Stars.Of(bodiesUsed, bodies, 0)`로 계산만 하고 **저장하지 않음**) → 다음 보드 또는 스테이지 1.
- 자동 재시작: `Lose` → `lose` cue 표시 → 약 1초 뒤 `Command.Restart` → `restart` cue.
- 실행 옵션: 데스크톱 `-tutorial N`, WebGL `?tutorial=N` (N = 1..3, 보드 하나를 바로 연다. 범위 밖이면 무시). `LaunchOptions`는 아키텍트/씬 플로우. `-replay`와 같이 쓸 수 있다(QA).
- 데이터 접근: `LevelCatalog`에 `Tutorials`(`TutorialDto[]`, 순서 그대로)를 더한다(게임플레이/UI). 50스테이지 목록·번호는 그대로.

### 2.5 저장 (Contracts, 코어 구현)

- 기존 `TutorialData.seen`(11.3.2)에 보드 id를 넣는다. 별·진행(`ProgressData`)에는 아무것도 쓰지 않는다.
- `TutorialBoards.SaveId(TutorialDto t)` = `"board-" + t.id + "@" + t.version` (예 `board-t1@1`, `TutorialIds.IsValid` 통과).
- 보드를 클리어하면 `save.MarkTutorialSeen(TutorialBoards.SaveId(t))`.
- `TutorialBoards.AllDone(IReadOnlyList<TutorialDto> boards, Func<string, bool> seen)` = 모든 보드를 끝냄. 새 게임 흐름은 이것이 false이고 스테이지를 하나도 깨지 않았을 때 튜토리얼을 넣는다. `SaveSystem.StartNewPlayer`가 `TutorialData`를 지우므로 함께 지워진다.

```csharp
public static class TutorialBoards {
    public static string SaveId(TutorialDto t);
    public static bool AllDone(IReadOnlyList<TutorialDto> boards, Func<string, bool> seen);   // 빈 목록 = true
    public static int FirstUndone(IReadOnlyList<TutorialDto> boards, Func<string, bool> seen); // 없으면 -1
}
```

### 2.6 텔레메트리

- 튜토리얼 보드는 `level_*` 이벤트를 보내지 않는다(스테이지 번호가 없다). 넣을지, 넣는다면 `tutorial_start`/`tutorial_clear` 같은 새 이벤트인지는 백엔드/분석이 `docs/telemetry.md`에 정한다.

## 3. 글자

- ko 줄은 Gaegu 글꼴에 있는 글자만. `·`(U+00B7) 금지, 대신 "이나", 쉼표. `scene-validate`의 GlyphCheck가 튜토리얼 이름·힌트·cue 줄도 검사한다.
- 줄은 짧게(ko 30자 안팎). 키 이름은 KEEP, SWITCH 그대로.

## 변경 기록

| 날짜 | 누가 | 내용 |
|---|---|---|
| 2026-11-12 | 코어 | 최초 작성: `tutorials` 형식, cue trigger·판정 규칙, 저장 id, 흐름·실행 옵션·텔레메트리 범위 |
| 2026-11-13 | 코어 | SWITCH를 직접 하는 T3(KEEP 1 + SWITCH 1, 같은 id·version 1), `doorShut` trigger, SWITCH/KEEP 뒤 줄은 `switch`/`keep` cue에(2.3), T2 choose·switch 줄, export에 needsSwitch 보드 단정, 프롤로그 8장 |
| 2026-11-13 | 코어 | cue 줄을 이야기 말투로(리드 `production/captions-story-voice-2026-11-13.json`, on/n/body/cell 그대로) |
