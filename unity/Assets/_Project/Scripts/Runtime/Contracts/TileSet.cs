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

        [Header("Actors and markers")]
        public TileVisual player;
        public TileVisual statue;
        public TileVisual startMarker;
        public TileVisual oldStartMarker;
        public TileVisual footprint;
        [Tooltip("Lucky 7th step stamp.")]
        public TileVisual luckyRing;

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
