using System;
using UnityEngine;

namespace SevenSteps.Contracts
{
    /// <summary>
    /// How one tile or actor is drawn. Lets the same board code show 2D top-down and 3D quarter-view art:
    /// everything that differs between art sets (scale, offset, rotation, pivot correction, animation hooks) is data here.
    ///
    /// Cell anchor: the center of the cell's top surface (world y = 0, see <see cref="BoardGrid"/>).
    /// The prefab is instantiated at the anchor with localPosition = offset * cellSize, localRotation = RotationFor(cell),
    /// localScale = Scale * cellSize. A prefab whose pivot is not "top-center of a 1x1 cell" is corrected here with
    /// offset/scale, never by editing board code.
    /// </summary>
    [Serializable]
    public struct TileVisual
    {
        [Tooltip("Prefab to instantiate. Null = draw nothing for this kind.")]
        public GameObject prefab;

        [Header("Transform (relative to the cell anchor: top-center of the cell, y = 0)")]
        [Tooltip("In cells. Pivot correction goes here.")]
        public Vector3 offset;
        [Tooltip("Euler angles in degrees.")]
        public Vector3 rotation;
        [Tooltip("(0,0,0) means (1,1,1).")]
        public Vector3 scale;
        [Tooltip("Add a deterministic 0/90/180/270 yaw per cell (variation for floors and walls).")]
        public bool randomYaw90;

        [Header("State hooks. Door/CountDoor: active = open. Lever: active = pulled. (FlipWall shape is its variant.) Gate: active = passable on the next step. Plate: active = pressed. Exit: active = next step is the 7th.")]
        [Tooltip("Child shown only while active (e.g. an open-door mesh). Empty = none.")]
        public string activeChild;
        [Tooltip("Child shown only while inactive (e.g. \"Bars\" of a closed door). Empty = none.")]
        public string inactiveChild;
        [Tooltip("Child whose renderers get activeMaterial/inactiveMaterial. Empty = the whole instance.")]
        public string materialTarget;
        public Material activeMaterial;
        public Material inactiveMaterial;

        [Header("Animator triggers. Empty = the view uses its code tween.")]
        public string appearTrigger;
        public string vanishTrigger;
        public string activateTrigger;
        public string deactivateTrigger;

        [Header("World label anchor (gate digit, plate/door letter), in cells from the cell anchor")]
        public Vector3 labelOffset;
        [Tooltip("Label anchor while active (e.g. lowered with an open door). (0,0,0) = same as labelOffset.")]
        public Vector3 activeLabelOffset;
        [Tooltip("Optional label font size as a fraction of projected cell width; 0 keeps the view's existing default.")]
        public float labelScale;
        [Tooltip("The model carries its meaning as geometry; suppress the fallback world text.")]
        public bool hideLabel;

        /// labelScale range (docs/architecture.md 7.2): 0 = view default, otherwise a fraction of the projected cell side.
        public const float LabelScaleMin = 0.2f, LabelScaleMax = 0.8f;
        public static bool IsValidLabelScale(float s) => s == 0f || (s >= LabelScaleMin && s <= LabelScaleMax);

        public bool HasPrefab => prefab != null;
        public Vector3 Scale => scale == Vector3.zero ? Vector3.one : scale;

        /// Local rotation for a cell. The seed is the cell index, so the same board always looks the same.
        public Quaternion RotationFor(int seed)
        {
            var q = Quaternion.Euler(rotation);
            if (!randomYaw90) return q;
            uint h = unchecked((uint)seed * 2654435761u);
            return Quaternion.Euler(0f, 90f * ((h >> 16) & 3), 0f) * q;
        }

        public Vector3 LabelOffset(bool active) => active && activeLabelOffset != Vector3.zero ? activeLabelOffset : labelOffset;
    }
}
