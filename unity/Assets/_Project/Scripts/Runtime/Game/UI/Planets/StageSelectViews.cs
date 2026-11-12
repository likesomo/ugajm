using System;
using System.Collections.Generic;
using SevenSteps.Game.Flow;
using UnityEngine;

namespace SevenSteps.Game.UI.Planets
{
    /// <summary>
    /// The v4 stage select views as one <see cref="IStageSelectView"/> for <see cref="StageSelectController"/>: the star map
    /// and the surface path (an overlay on the Title scene's OverlayStack, so Esc / ‹ close it like any panel).
    /// </summary>
    public sealed class StageSelectViews : IStageSelectView
    {
        readonly PlanetMap3DView map;
        readonly PlanetSurfaceView surface;
        readonly OverlayStack overlays;

        public event Action<int> PlanetChosen;
        public event Action<int, int> PlaceChosen;

        public PlanetMap3DView Map => map;
        public PlanetSurfaceView Surface => surface;
        /// The planet whose surface path was opened last (the map refocuses it when the path closes), -1 none.
        public int LastSurfacePlanet { get; private set; } = -1;
        public void ForgetSurface() => LastSurfacePlanet = -1;

        public StageSelectViews(PlanetMap3DView map, PlanetSurfaceView surface, OverlayStack overlays)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            this.overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
            map.PlanetChosen += p => PlanetChosen?.Invoke(p);
            surface.PlaceChosen += (p, s) => PlaceChosen?.Invoke(p, s);
        }

        public void ShowMap(StageSelectModel model, int focus, int held) => map.Show(model, focus, held);
        public void LightTrail(StageSelectModel model, int planet, int focus) => map.LightTrail(model, planet, focus);

        public void OpenSurface(StageSelectModel model, int planet, IReadOnlyList<ClearNote> news)
        {
            if (overlays.Count > 0) return;
            surface.Bind(model, planet, news);
            LastSurfacePlanet = planet;
            overlays.Push(surface);
        }

        public void SetPending(bool pending)
        {
            map.SetPending(pending);
            surface.SetPending(pending);
        }

        public PlanetBody Body(int planet, StageChoiceSource source)
        {
            if (source == StageChoiceSource.Surface)
            {
                if (surface.Body == null || surface.Planet != planet) throw new InvalidOperationException($"[SevenSteps] surface choice on planet {planet} but the surface shows {surface.Planet}");
                return surface.Body;
            }
            return map.Body(planet);
        }

        public Camera MapCamera => map.MapCamera;

        public void MapPose(out Vector3 position, out Quaternion rotation, out float orthoSize) => map.MapPose(out position, out rotation, out orthoSize);
    }
}
