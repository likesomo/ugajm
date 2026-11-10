using System;
using System.Collections;
using System.Collections.Generic;
using SevenSteps.Core;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
namespace SevenSteps.Art.Review
{
    public sealed class ReviewChoiceController : MonoBehaviour, IBoardPresenter
    {
        public SevenSteps.Contracts.ChapterPreset[] chapters; public BoardSurfaceReviewView surface; public GameSession Session; public Font font; public string language = "ko", planet = "moss"; public int encounters; public bool focusKeep = true; public Canvas canvas; public Rect boardRect; public ChoicePanelView panel; public GhostPreviewView ghosts; public StarTrailReviewView trail; public FootStarsView feet;
        public readonly List<GameObject> drawn = new List<GameObject>(); public float animationPhase = 1; public CommandType lastChoice; Action pending; float endAt;
        public Color Ink => new Color(.94f, .91f, .79f); public Color BodyColor(int body) => new[] { new Color(.40f, .88f, .88f), new Color(1f, .76f, .28f), new Color(1f, .43f, .40f) }[Mathf.Clamp(body, 0, 2)]; public Color Scarf => BodyColor(Session == null ? 0 : Session.Level.Bodies - Session.State.Bodies); public Color Next => BodyColor(Session == null ? 1 : Session.Level.Bodies - Session.State.Bodies + 1);
        public void Initialize(Level level)
        {
            if (!font)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!GetComponent<Canvas>())
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 2;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = .5f;
                gameObject.AddComponent<GraphicRaycaster>();
            }
            else
                canvas = GetComponent<Canvas>();
            if (!FindFirstObjectByType<EventSystem>())
            {
                var input = new GameObject("Review input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                input.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            surface = new BoardSurfaceReviewView(this);
            panel = new ChoicePanelView(this);
            ghosts = new GhostPreviewView(this);
            trail = new StarTrailReviewView(this);
            feet = new FootStarsView(this);
            Session = new GameSession(this);
            Session.Updated += OnSession;
            Session.Load(level);
        }
        void Start()
        {
            if (Session == null)
            {
                var data = JsonUtility.FromJson<LevelFileDto>(Resources.Load<TextAsset>("ReviewLevels").text);
                Initialize(data.levels[3].ToLevel());
            }
        }
        void OnSession(SessionUpdate u)
        {
            if (u.Kind == SessionUpdateKind.Present && (u.Command.Type == CommandType.Keep || u.Command.Type == CommandType.Switch))
                encounters++;
            if (u.Command.Type == CommandType.Keep || u.Command.Type == CommandType.Switch)
            {
                lastChoice = u.Command.Type;
                animationPhase = 0;
            }
            Draw();
        }
        void Update()
        {
            if (pending != null)
            {
                animationPhase = Mathf.Clamp01(1 - (endAt - Time.realtimeSinceStartup) / .38f);
                Draw();
                if (Time.realtimeSinceStartup >= endAt)
                    Finish();
            }
            else if (Session?.State.Used == 6)
                Draw();
            PollKeyboard();
        }
        readonly HashSet<KeyCode> heldKeys = new HashSet<KeyCode>();
        bool IsKeyHeld(KeyCode key)
        {
            var k = Keyboard.current;
            if (k == null)
                return false;
            switch (key)
            {
                case KeyCode.Z:
                    return k.zKey.isPressed;
                case KeyCode.K:
                    return k.kKey.isPressed;
                case KeyCode.X:
                    return k.xKey.isPressed;
                case KeyCode.Tab:
                    return k.tabKey.isPressed;
                case KeyCode.Return:
                    return k.enterKey.isPressed;
                case KeyCode.UpArrow:
                    return k.upArrowKey.isPressed;
                case KeyCode.DownArrow:
                    return k.downArrowKey.isPressed;
                case KeyCode.LeftArrow:
                    return k.leftArrowKey.isPressed;
                case KeyCode.RightArrow:
                    return k.rightArrowKey.isPressed;
                default:
                    return false;
            }
        }
        public void PollKeyboard()
        {
            foreach (var key in new[] { KeyCode.Z, KeyCode.K, KeyCode.X, KeyCode.Tab, KeyCode.Return, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow })
            {
                bool down = IsKeyHeld(key);
                if (down)
                {
                    if (heldKeys.Add(key))
                        HandleKey(key);
                }
                else
                    heldKeys.Remove(key);
            }
        }
        public void HandleKey(KeyCode key)
        {
            if (Session == null)
                return;
            if (key == KeyCode.Z)
            {
                Send(Command.Undo);
                return;
            }
            if (Session.IsLocked)
                return;
            if (Session.State.Status == Status.Choose)
            {
                if (key == KeyCode.Tab || key == KeyCode.LeftArrow || key == KeyCode.RightArrow)
                {
                    focusKeep = !focusKeep;
                    Draw();
                }
                if (key == KeyCode.K)
                    Choose(true);
                if (key == KeyCode.X)
                    Choose(false);
                if (key == KeyCode.Return)
                    Choose(focusKeep);
            }
            else
            {
                if (key == KeyCode.UpArrow)
                    Send(Command.Move(Dir.Up));
                if (key == KeyCode.DownArrow)
                    Send(Command.Move(Dir.Down));
                if (key == KeyCode.LeftArrow)
                    Send(Command.Move(Dir.Left));
                if (key == KeyCode.RightArrow)
                    Send(Command.Move(Dir.Right));
            }
        }

        public void Send(Command c)
        {
            Session.Send(c);
        }
        public void Choose(bool keep)
        {
            if (Session.IsLocked || Session.State.Status != Status.Choose)
                return;
            focusKeep = keep;
            Send(keep ? Command.Keep : Command.Switch);
        }
        public void Focus(bool keep)
        {
            if (focusKeep == keep)
                return;
            focusKeep = keep;
            Draw();
        }
        public void Finish()
        {
            var cb = pending;
            pending = null;
            animationPhase = 1;
            cb?.Invoke();
            Draw();
        }
        public void Rebuild(State s)
        {
            pending = null;
            animationPhase = 1;
        }
        public void Snap(State s)
        {
            pending = null;
            animationPhase = 1;
        }
        public void Present(State p, StepResult r, Action done)
        {
            pending = done;
            endAt = Time.realtimeSinceStartup + .38f;
        }
        public Vector2 Cell(int cell)
        {
            if (surface != null && chapters != null && chapters.Length > 0)
                return surface.Project(cell);
            var l = Session.Level;
            float sz = Size;
            return new Vector2(115 + (l.X(cell) + .5f) * sz, 180 + (l.H - l.Y(cell) - .5f) * sz);
        }
        public float Size => Mathf.Min(61, 620f / Session.Level.W, 430f / Session.Level.H);
        public GameObject Box(string name, Rect r, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = r.size;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
            drawn.Add(go);
            return go;
        }
        public void Label(string text, Rect r, int size, Color? color = null)
        {
            var go = Box(text, r, Color.clear);
            DestroyImmediate(go.GetComponent<Image>());
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Ink;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
        }
        public void Line(Vector2 a, Vector2 b, Color color, float width = 3)
        {
            var g = Box("Starlight", new Rect(a, new Vector2((b - a).magnitude, width)), color);
            ((RectTransform)g.transform).pivot = new Vector2(0, .5f);
            g.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }
        public void Star(Vector2 p, float size, Color color)
        {
            var g = Box("Scarf star", new Rect(p - Vector2.one * size / 2, Vector2.one * size), color);
            DestroyImmediate(g.GetComponent<Image>());
            var star = g.AddComponent<ReviewStarGraphic>();
            star.color = color;
            star.raycastTarget = false;
        }
        public void Figure(int cell, Color color, bool stone = false)
        {
            var p = Cell(cell);
            Box(stone ? "Standing stone" : "Sibling", new Rect(p + new Vector2(-12, -12), new Vector2(24, 34)), color);
            Box("Head", new Rect(p + new Vector2(-9, 19), new Vector2(18, 16)), Ink);
            Box("Scarf", new Rect(p + new Vector2(-15, 9), new Vector2(34, 8)), color);
            if (stone)
                Star(p + new Vector2(0, 30), 12, Scarf);
        }
        public void Draw()
        {
            if (Session?.State == null)
                return;
            foreach (var g in drawn)
                if (g)
                    DestroyImmediate(g);
            drawn.Clear();
            var s = Session.State;
            var bg = planet == "moss" ? new Color(.075f, .13f, .14f) : planet == "ember" ? new Color(.17f, .095f, .12f) : new Color(.07f, .10f, .2f);
            Box("Sky", new Rect(0, 0, 1280, 720), bg);
            Label("SEVEN STEPS    /    " + planet.ToUpper() + "    /    04", new Rect(70, 644, 1100, 48), 28);
            Label(language == "ko" ? "일곱 걸음, 다음 형제에게" : "Seven steps, then the next sibling", new Rect(70, 602, 1000, 35), 20);
            boardRect = surface.Bounds;
            if (chapters != null && chapters.Length > 0)
                surface.Draw(s);
            else
                Box("Board fallback", boardRect, new Color(.2f, .25f, .24f));
            for (int i = 0; i < s.Level.Count; i++)
            {
                char tile = s.Level.Tile(i);
                if (tile != '#' && tile != '.' && tile != 'E')
                {
                    var p = Cell(i);
                    Label(tile.ToString(), new Rect(p - Vector2.one * 15, new Vector2(30, 30)), 22);
                }
            }

            if (s.Status != Status.Choose)
                panel.Hide();
            trail.Draw(s);
            foreach (int cell in s.Statues)
                Figure(cell, new Color(.55f, .59f, .54f), true);
            Figure(s.Pos, Scarf);
            feet.Draw(s);
            if (s.Status == Status.Choose)
            {
                Box("Choice dim", boardRect, new Color(0, 0, 0, .3f));
                var p = Cell(s.Pos);
                for (int j = 0; j < 24; j++)
                {
                    float a = j * Mathf.PI / 12;
                    Star(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 35, 4, Next);
                }
                ghosts.Draw(s, focusKeep);
                panel.Draw(s);
                Label(language == "ko" ? "선택 중 / 걷기 잠김" : "Choosing / movement locked", new Rect(panel.Bounds.x, panel.Bounds.y - 54, 390, 35), 20);
            }
            else
            {
                Label(s.Status == Status.Win ? (language == "ko" ? "별자리가 이어졌어요" : "Constellation complete") : language == "ko" ? "방향키 / 터치로 걷기\n일곱 번째 걸음에서 바통을 골라요" : "Arrows / touch to walk\nChoose the baton on step seven", new Rect(810, 380, 400, 110), 25);
            }
            if (animationPhase < 1)
            {
                var p = Cell(lastChoice == CommandType.Keep ? s.Prev.Pos : s.Pos);
                Star(p, 25 + animationPhase * 40, lastChoice == CommandType.Keep ? Ink : Next);
                Label(lastChoice == CommandType.Keep ? "KEEP  *" : "SWITCH  <> ", new Rect(820, 270, 350, 60), 30);
            }
            Label(language == "ko" ? "Z 되돌리기   ·   K 남기기   ·   X 넘기기" : "Z Undo   ·   K Keep   ·   X Switch", new Rect(70, 53, 1100, 35), 20);
            for (int d = 0; d < 4; d++)
            {
                int n = d;
                var go = Box("Touch move", new Rect(810 + d * 91, 130, 82, 48), (s.Status == Status.Play ? new Color(.22f, .3f, .33f) : new Color(.1f, .16f, .18f)));
                Label(new[] { "UP", "DOWN", "LEFT", "RIGHT" }[d], new Rect(815 + d * 91, 130, 80, 48), 18);
                var b = go.AddComponent<Button>();
                b.interactable = s.Status == Status.Play && !Session.IsLocked;
                go.GetComponent<Image>().raycastTarget = true;
                var trigger = go.AddComponent<EventTrigger>();
                var press = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                press.callback.AddListener(_ => { if (!Session.IsLocked && Session.State.Status == Status.Play) Send(Command.Move((Dir)n)); });
                trigger.triggers.Add(press);
            }
        }
    }
}
