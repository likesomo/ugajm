using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game.Flow;
using SevenSteps.Game.Telemetry;
using SevenSteps.Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace SevenSteps.Game
{
    /// <summary>
    /// Board progression in the Game scene (docs/scene-flow.md S3): owns the <see cref="GameSession"/>, the board view, messages,
    /// hint, sound and the telemetry recorder. Board keys and HUD board buttons become Commands sent to the session.
    /// Knows nothing about AppFlow: <see cref="Flow.GameFlowController"/> tells it which stage to load (<see cref="IStageBoard"/>)
    /// and listens to Won / WinPresented / Lost / Recovered. Added at runtime by the scene root <see cref="GameController"/>.
    /// Gameplay/UI owns the content and cleans it up after S3 (split from GameController in S3).
    /// </summary>
    public sealed class BoardController : MonoBehaviour, IStageBoard
    {
        static readonly Dictionary<GameEventType, string> Messages = new Dictionary<GameEventType, string>
        {
            { GameEventType.Choose, "행운의 7번째 걸음. 이 몸을 어떻게 할까?" },
            { GameEventType.Stone, "KEEP. 몸이 석상이 되었다. 다음 몸은 출발점에서 깨어난다." },
            { GameEventType.Switch, "SWITCH. 이 자리가 새 출발점이 되었다." },
            { GameEventType.Bridge, "7번째 걸음으로 구덩이에 몸을 던졌다. 다리가 되었다." },
            { GameEventType.Seal, "금 간 바닥 위에서 석상이 되었다. 바닥이 영원히 굳었다." },
            { GameEventType.Fall, "구덩이에 떨어졌다. 7번째 걸음이 아니면 아무것도 남지 않는다." },
            { GameEventType.Vanish, "출발점에서는 석상이 될 수 없다. 몸 하나를 잃었다." },
            { GameEventType.Crumble, "금 간 바닥이 무너졌다." },
            { GameEventType.Win, "7번째 걸음으로 출구에 닿았다!" },
            { GameEventType.Lose, "몸을 모두 잃었다. Z로 되돌리거나 R로 다시 시작." },
            { GameEventType.Undo, "한 걸음 되돌렸다." },
            { GameEventType.Restart, "처음부터." },
            { GameEventType.Flip, "7번째 걸음으로 레버를 당겼다. 벽이 뒤집혔다." },
        };

        const string ChooseNudge = "먼저 고르자: K = KEEP (석상), X = SWITCH (새 출발점).";

        Level[] levels;
        GameSession session;
        SaveSystem save;
        GameAssets assets;
        string message;
        HudView hud;
        BoardView board;
        AudioSource sfx;
        Camera cam;
        int loadedStage = -1;
        bool lost;
        TelemetryRecorder recorder;
        readonly Dictionary<int, string> hintCache = new Dictionary<int, string>();

        public GameSession Session => session;
        public State State => session?.State;
        /// 0-based stage on the board (-1 before the first LoadStage).
        public int LevelIndex => loadedStage;
        public int LevelCount => levels.Length;
        public Level[] Levels => levels;

        /// Raised when a stage is loaded, with how it was entered (boot, url, next, select, again).
        public event Action<int, string> StageEntered;
        /// Raised just before a stage is loaded, with the 0-based stage about to start (Session.State is still the old one).
        public event Action<int> StageLeaving;
        /// Raised for HUD-only actions that do not touch the session (hint).
        public event Action HintShown;

        // IStageBoard (docs/scene-flow.md S3): the game flow controller listens.
        public event Action<State> Won;
        public event Action<State> WinPresented;
        public event Action<State> Lost;
        public event Action Recovered;

        /// Set by the scene root: true while board input must be ignored (an overlay is open, a scene is loading).
        public Func<bool> InputBlocked = () => false;
        /// Set by the scene root: the flow takes a command instead of the board (Result: R = again). True = taken.
        public Func<Command, bool> FlowTakes = _ => false;

        /// Called once by GameController.Awake (the scene root) after the bootstrap. The board side: session, board view, HUD, sound, telemetry.
        public void Init(GameAssets assets, Level[] levels, SaveSystem save, HudView hud, Camera cam, TileSet tiles, CameraRig? cameraOverride, ITelemetry telemetry)
        {
            if (session != null) throw new InvalidOperationException("BoardController.Init twice");
            this.assets = assets ?? throw new ArgumentNullException(nameof(assets));
            this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            this.cam = cam != null ? cam : throw new ArgumentNullException(nameof(cam));
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            ApplyVolume(save.Settings);
            save.SettingsChanged += ApplyVolume;

            hud.UndoRequested += () => Send(Command.Undo);
            hud.RestartRequested += Restart;
            hud.HintRequested += ShowHint;
            hud.KeepRequested += () => Send(Command.Keep);
            hud.SwitchRequested += () => Send(Command.Switch);

            board = gameObject.AddComponent<BoardView>();
            board.Init(tiles, hud.LabelLayer, cam, cameraOverride);

            session = new GameSession(board);
            board.Hurry = () => session.QueuedCount > 0;
            session.Updated += OnSessionUpdated;
            session.Presented += OnPresented;
            if (telemetry is HttpTelemetry http) recorder = new TelemetryRecorder(this, session, http);
        }

        void OnDestroy()
        {
            recorder?.Dispose();
            if (save != null) save.SettingsChanged -= ApplyVolume;
        }

        /// IStageBoard: load a stage the flow chose.
        public void LoadStage(int stage, string via)
        {
            if (session == null) throw new InvalidOperationException("BoardController.LoadStage before Init");
            if (stage < 0 || stage >= levels.Length) throw new ArgumentOutOfRangeException(nameof(stage), stage, "stage outside levels.json");
            if (string.IsNullOrEmpty(via)) throw new ArgumentException("telemetry via is required", nameof(via));
            StageLeaving?.Invoke(stage);
            loadedStage = stage;
            lost = false;
            session.Load(levels[stage]);
            StageEntered?.Invoke(stage, via);
        }

        /// Replay a move string on the current stage (-replay / editor replay).
        public void Replay(string moves) => session.Replay(moves);

        // ---------- Input (board keys; flow keys are read by GameController, the scene root) ----------

        void Update()
        {
            if (session == null || InputBlocked()) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Send(Command.Move(Dir.Up));
                else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Send(Command.Move(Dir.Down));
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Send(Command.Move(Dir.Left));
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Send(Command.Move(Dir.Right));
                else if (kb.kKey.wasPressedThisFrame) Send(Command.Keep);
                else if (kb.xKey.wasPressedThisFrame) Send(Command.Switch);
                else if (kb.zKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Send(Command.Undo);
                else if (kb.rKey.wasPressedThisFrame) Restart();
                else if (kb.hKey.wasPressedThisFrame) ShowHint();
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUi())
                ClickCell(mouse.position.ReadValue());
        }

        static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        void ClickCell(Vector2 screen)
        {
            int cell = board.Grid.RayToCell(cam.ScreenPointToRay(screen));
            var s = session.State;
            if (cell < 0 || s == null) return;
            var l = s.Level;
            int dx = l.X(cell) - l.X(s.Pos), dy = l.Y(cell) - l.Y(s.Pos);
            if (dx == 0 && dy == -1) Send(Command.Move(Dir.Up));
            else if (dx == 0 && dy == 1) Send(Command.Move(Dir.Down));
            else if (dx == -1 && dy == 0) Send(Command.Move(Dir.Left));
            else if (dx == 1 && dy == 0) Send(Command.Move(Dir.Right));
        }

        /// Every input goes here. The flow may take it (Result); otherwise the session decides whether it applies now,
        /// is queued, or is dropped.
        public void Send(Command cmd)
        {
            if (FlowTakes(cmd)) return;
            session.Send(cmd);
        }

        public void Restart() => Send(Command.Restart);

        /// The solver starts from a fresh stage, so the hint is the full route from the beginning (same as the prototype).
        public void ShowHint()
        {
            var level = session.Level;
            if (level == null) return;
            if (!hintCache.TryGetValue(loadedStage, out var text))
            {
                var sol = Solver.Solve(level);
                if (sol == null) text = "풀이를 찾지 못했다.";
                else
                {
                    var sb = new StringBuilder("처음부터의 최단 풀이: ");
                    foreach (var a in sol.Path)
                        sb.Append(a switch { Act.Up => "↑", Act.Down => "↓", Act.Left => "←", Act.Right => "→", Act.Keep => " KEEP ", _ => " SWITCH " });
                    text = sb.ToString();
                }
                hintCache[loadedStage] = text;
            }
            message = text;
            HintShown?.Invoke();
            Refresh();
        }

        // ---------- Session updates ----------

        void OnSessionUpdated(SessionUpdate u)
        {
            switch (u.Kind)
            {
                case SessionUpdateKind.Rebuild:
                    message = u.State.Level.Hint;
                    break;
                case SessionUpdateKind.Feedback:
                    if (u.Result != null && u.Result.Has(GameEventType.Bump))
                    {
                        Play(assets.sfxBump);
                        board.Bump(FirstAt(u.Result, GameEventType.Bump));
                    }
                    else if (u.State.Status == Status.Choose && u.Command.Type == CommandType.Move)
                    {
                        message = ChooseNudge;
                        hud.NudgeChoice();
                    }
                    break;
                default:
                    if (u.Result != null)
                    {
                        foreach (var e in u.Result.Events)
                        {
                            if (Messages.TryGetValue(e.Type, out var m)) message = m;
                            if (e.Type == GameEventType.Step) message = e.StepIdx == Rules.Steps - 1 ? "다음 걸음이 행운의 7번째 걸음이다." : "";
                        }
                        PlaySounds(u.Result);
                    }
                    break;
            }

            var s = u.State;
            if (s.Status == Status.Win && u.Kind == SessionUpdateKind.Present)
                Won?.Invoke(s); // the flow saves the clear before the HUD redraws; the result panel waits for OnPresented
            Refresh();
            if (s.Status == Status.Lose && !lost) { lost = true; Lost?.Invoke(s); }
            else if (s.Status != Status.Lose && lost) { lost = false; Recovered?.Invoke(); }
        }

        /// A presentation finished (docs/architecture.md 4.2). A clear shows the Result panel only now.
        void OnPresented(State s)
        {
            if (s.Status == Status.Win) WinPresented?.Invoke(s);
        }

        static int FirstAt(StepResult r, GameEventType t)
        {
            foreach (var e in r.Events) if (e.Type == t) return e.At;
            return -1;
        }

        void PlaySounds(StepResult r)
        {
            foreach (var e in r.Events)
            {
                if (e.StepIdx == Rules.Steps) Play(assets.sfxLucky);
                switch (e.Type)
                {
                    case GameEventType.Step: Play(assets.sfxStep, 0.9f + 0.06f * e.StepIdx); break;
                    case GameEventType.Bump: Play(assets.sfxBump); break;
                    case GameEventType.Crumble: Play(assets.sfxCrumble); break;
                    case GameEventType.Stone: Play(assets.sfxStone); break;
                    case GameEventType.Switch:
                    case GameEventType.Flip: Play(assets.sfxSwitch); break;
                    case GameEventType.Bridge:
                    case GameEventType.Seal: Play(assets.sfxBridge); break;
                    case GameEventType.Fall: Play(assets.sfxFall); break;
                    case GameEventType.Win: Play(assets.sfxWin); break;
                    case GameEventType.Lose: Play(assets.sfxLose); break;
                    case GameEventType.Undo:
                    case GameEventType.Restart: Play(assets.sfxUndo); break;
                }
            }
        }

        void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip);
        }

        void ApplyVolume(SettingsData s)
        {
            AudioListener.volume = Mathf.Clamp01(s.masterVolume);
            if (sfx != null) sfx.volume = Mathf.Clamp01(s.sfxVolume);
        }

        void Refresh() => hud.Refresh(session.State, loadedStage, levels.Length, message, save.Progress);
    }
}
