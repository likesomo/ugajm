using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    public enum BoardProjection { TopDown, Quarter }

    /// <summary>
    /// Camera preset of an art set. The board grid is the same in every projection; only the camera and the prefabs change.
    /// The view frames the whole board inside the HUD-free band using these values.
    /// </summary>
    [Serializable]
    public struct CameraRig
    {
        public bool orthographic;
        [Tooltip("Degrees down from the horizon. Top-down v0: 58.")]
        public float pitch;
        [Tooltip("Degrees around Y. Top-down: 0. Quarter view: 45.")]
        public float yaw;
        [Tooltip("Perspective only.")]
        public float fieldOfView;
        [Tooltip("Distance from the board center along the view direction.")]
        public float distance;
        [Tooltip("Extra margin around the board, in cells.")]
        public float padding;

        public static CameraRig TopDown => new CameraRig { orthographic = true, pitch = 58f, yaw = 0f, fieldOfView = 30f, distance = 30f, padding = 0.6f };
        public static CameraRig Quarter => new CameraRig { orthographic = true, pitch = 35f, yaw = 45f, fieldOfView = 30f, distance = 30f, padding = 0.6f };

        public Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Shared contract between art (fills it) and the board view (reads it): tile/actor kind -> <see cref="TileVisual"/>.
    /// Swapping art = assigning a different TileSet asset. Board code gets prefabs only through here.
    /// Unit: 1 cell = <see cref="cellSize"/> world units (1 by default). Floor top surface at y = 0.
    /// </summary>
    [CreateAssetMenu(menuName = "Seven Steps/Tile Set", fileName = "TileSet")]
    public sealed class TileSet : ScriptableObject
    {
        public BoardProjection projection = BoardProjection.TopDown;
        [Min(0.1f)] public float cellSize = 1f;
        public CameraRig cameraRig = CameraRig.TopDown;

        [Header("Tiles")]
        public TileVisual floor;
        [Tooltip("Checker variant. No prefab = use floor.")]
        public TileVisual floorAlt;
        public TileVisual wall;
        public TileVisual pit;
        public TileVisual ice;
        public TileVisual cracked;
        public TileVisual bridge;
        public TileVisual exit;
        [Tooltip("Number gate 1-7. One prefab; the digit is a world label.")]
        public TileVisual gate;
        [Tooltip("Index 0/1/2 = plate a/b/c.")]
        public TileVisual[] plates = new TileVisual[3];
        [Tooltip("Index 0/1/2 = door A/B/C.")]
        public TileVisual[] doors = new TileVisual[3];
        [Tooltip("Statue-count door Q. Active = open. The number N is a world label.")]
        public TileVisual countDoor;
        [Tooltip("Lever L. Active = pulled (State.Flipped).")]
        public TileVisual lever;
        [Tooltip("Index 0 = raised now (blocks), 1 = lowered now (passable). Rules.TileAt gives the current shape.")]
        public TileVisual[] flipWalls = new TileVisual[2];
        [Tooltip("Mud m: entering costs 2 steps (docs 6.3). Floor-like, top at y = 0. No prefab = drawn as floor (validator warns).")]
        public TileVisual mud;

        [Header("Actors and markers")]
        public TileVisual player;
        public TileVisual statue;
        public TileVisual startMarker;
        public TileVisual oldStartMarker;
        public TileVisual footprint;
        [Tooltip("Lucky 7th step stamp.")]
        public TileVisual luckyRing;

        [Header("Per body (triplets, docs/architecture.md 12.6)")]
        [Tooltip("Player look per body index: 0 = first body (A), 1 = B, 2 = C. Cycles when a stage has more bodies. Empty or no prefab = player.")]
        public TileVisual[] playerBodies = new TileVisual[0];
        [Tooltip("Statue left by KEEP (or by the last body), per body index: stone with that sibling's trace. Cycles like playerBodies. Empty or no prefab = statue.")]
        public TileVisual[] statueBodies = new TileVisual[0];
        [Tooltip("HUD body icon color per body index (A teal #568C88, B ochre #B99A57, C rust #B96C59; docs 12.6). Cycles like playerBodies.")]
        public Color[] bodyColors =
        {
            new Color(0x56 / 255f, 0x8C / 255f, 0x88 / 255f),
            new Color(0xB9 / 255f, 0x9A / 255f, 0x57 / 255f),
            new Color(0xB9 / 255f, 0x6C / 255f, 0x59 / 255f),
        };

        /// HUD icon color for a body index. White when no colors are set.
        public Color BodyColor(int bodyIndex) =>
            bodyColors == null || bodyColors.Length == 0 || bodyIndex < 0 ? Color.white : bodyColors[bodyIndex % bodyColors.Length];

        /// Player look for a body index (<see cref="BodyIndex.Current"/>). Falls back to <see cref="player"/>.
        public TileVisual PlayerFor(int bodyIndex) => Cycle(playerBodies, bodyIndex, player);

        /// Statue look for the body that left it (<see cref="BodyIndex.OfStatue"/>). Falls back to <see cref="statue"/>.
        public TileVisual StatueFor(int bodyIndex) => Cycle(statueBodies, bodyIndex, statue);

        static TileVisual Cycle(TileVisual[] arr, int k, TileVisual fallback)
        {
            if (arr == null || arr.Length == 0 || k < 0) return fallback;
            var v = arr[k % arr.Length];
            return v.HasPrefab ? v : fallback;
        }

        public BoardGrid GridFor(SevenSteps.Core.Level level) => new BoardGrid(level, cellSize);

        public TileVisual Get(TileId id)
        {
            switch (id.Kind)
            {
                case TileKind.Floor: return id.Variant == 1 && floorAlt.HasPrefab ? floorAlt : floor;
                case TileKind.Wall: return wall;
                case TileKind.Pit: return pit;
                case TileKind.Ice: return ice;
                case TileKind.Cracked: return cracked;
                case TileKind.Bridge: return bridge;
                case TileKind.Exit: return exit;
                case TileKind.Gate: return gate;
                case TileKind.Plate: return Pick(plates, id.Variant);
                case TileKind.Door: return Pick(doors, id.Variant);
                case TileKind.CountDoor: return countDoor;
                case TileKind.Lever: return lever;
                case TileKind.FlipWall: return Pick(flipWalls, id.Variant);
                case TileKind.Mud: return mud.HasPrefab ? mud : floor;
                default: return default;
            }
        }

        public TileVisual Get(ActorKind kind)
        {
            switch (kind)
            {
                case ActorKind.Player: return player;
                case ActorKind.Statue: return statue;
                case ActorKind.StartMarker: return startMarker;
                case ActorKind.OldStartMarker: return oldStartMarker;
                case ActorKind.Footprint: return footprint;
                case ActorKind.LuckyRing: return luckyRing;
                default: return default;
            }
        }

        static TileVisual Pick(TileVisual[] arr, int k) =>
            arr == null || arr.Length == 0 ? default : arr[Mathf.Clamp(k, 0, arr.Length - 1)];
    }
}
