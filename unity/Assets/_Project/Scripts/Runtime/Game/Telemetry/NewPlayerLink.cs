using System;
using System.Runtime.InteropServices;
using SevenSteps.Contracts;
using UnityEngine;

namespace SevenSteps.Game.Telemetry
{
    /// <summary>
    /// The facilitator's "next tester" link (-newplayer / ?newplayer=1, playtest 1): before telemetry starts, this
    /// browser's progress and anonymous playerId are erased (SaveSystem.StartNewPlayer), so telemetry makes a new id and
    /// sends session_start as a first time. Settings and language stay. Events the last tester left in the queue keep
    /// that tester's id (HttpTelemetry items carry their player). WebGL drops the parameter from the address bar so a
    /// reload continues as the same new player. Called by the bootstrap right after the save is read.
    /// </summary>
    public static class NewPlayerLink
    {
        public const string UrlParam = "newplayer";

        /// True when a new player was started.
        public static bool Apply(LaunchOptions launch, SaveSystem save)
        {
            if (launch == null) throw new ArgumentNullException(nameof(launch));
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (!launch.NewPlayer) return false;
            save.StartNewPlayer();
            Debug.Log("[SevenSteps] New player: progress and playerId erased (-newplayer / ?newplayer=1)");
#if UNITY_WEBGL && !UNITY_EDITOR
            if (SevenSteps_RemoveUrlParam(UrlParam) == 0) Debug.LogWarning("[SevenSteps] could not remove ?newplayer from the address bar");
#endif
            return true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SevenSteps_RemoveUrlParam(string name);
#endif
    }
}
