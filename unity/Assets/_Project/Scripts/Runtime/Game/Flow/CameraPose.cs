using System;
using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game.Flow
{
    /// <summary>An orthographic camera pose (position, rotation, size) that the journey transitions blend between.</summary>
    public readonly struct CameraPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Size;

        public CameraPose(Vector3 position, Quaternion rotation, float size)
        {
            if (float.IsNaN(size) || size <= 0f) throw new ArgumentOutOfRangeException(nameof(size), size, "CameraPose: size must be positive");
            Position = position;
            Rotation = rotation;
            Size = size;
        }

        public static CameraPose Capture(Camera cam)
        {
            if (cam == null) throw new ArgumentNullException(nameof(cam));
            if (!cam.orthographic) throw new InvalidOperationException($"CameraPose: camera {cam.name} is not orthographic (game camera: ortho pitch 50 yaw 0)");
            return new CameraPose(cam.transform.position, cam.transform.rotation, cam.orthographicSize);
        }

        public void Apply(Camera cam)
        {
            if (cam == null) throw new ArgumentNullException(nameof(cam));
            cam.transform.SetPositionAndRotation(Position, Rotation);
            cam.orthographicSize = Size;
        }

        /// Size and position blend linearly, rotation spherically.
        public static CameraPose Lerp(CameraPose a, CameraPose b, float t) =>
            new CameraPose(Vector3.Lerp(a.Position, b.Position, t), Quaternion.Slerp(a.Rotation, b.Rotation, t), Mathf.Lerp(a.Size, b.Size, t));

        public CameraPose Moved(Vector3 by) => new CameraPose(Position + by, Rotation, Size);

        /// Same view centre (orthographic: the axis does not move), size times <paramref name="factor"/>.
        public CameraPose Zoomed(float factor)
        {
            if (float.IsNaN(factor) || factor <= 0f) throw new ArgumentOutOfRangeException(nameof(factor), factor, "CameraPose: zoom factor must be positive");
            return new CameraPose(Position, Rotation, Size * factor);
        }

        /// Looking at <paramref name="target"/> along <paramref name="rotation"/> from <paramref name="distance"/> away.
        public static CameraPose LookingAt(Vector3 target, Quaternion rotation, float size, float distance = 30f) =>
            new CameraPose(target - rotation * Vector3.forward * distance, rotation, size);

        /// The pose the board framing (BoardView.Fit) gives <paramref name="cam"/> for <paramref name="grid"/>. The camera is
        /// put back as it was; only the pose is returned.
        public static CameraPose Fitted(Camera cam, BoardGrid grid, CameraRig rig)
        {
            var was = Capture(cam);
            float near = cam.nearClipPlane, far = cam.farClipPlane;
            BoardView.Fit(cam, grid, rig);
            var fitted = Capture(cam);
            was.Apply(cam);
            cam.nearClipPlane = near;
            cam.farClipPlane = far;
            return fitted;
        }
    }
}
