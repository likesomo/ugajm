using System;
using UnityEngine;

namespace SevenSteps.Game.UI
{
    /// Calls <see cref="Resized"/> when its RectTransform changes size (window resize, CanvasScaler update) while active.
    /// For code-built layouts that place things by measured width (PlanetMapView's Dipper lines).
    public sealed class ResizeHook : MonoBehaviour
    {
        public Action Resized;

        void OnRectTransformDimensionsChange() => Resized?.Invoke();
    }
}
