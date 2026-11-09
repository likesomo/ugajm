using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game;
using SevenSteps.Game.UI;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// Domain reload is disabled in the editor, so every static that outlives a Play needs a SubsystemRegistration reset
    /// (docs/scene-flow.md 3.1 risk 1, S0). This fails if a reset hook is removed or its load type changes.
    public sealed class PlayResetTests
    {
        static readonly Type[] TypesWithPlayState =
        {
            typeof(Services), typeof(Bootstrap), typeof(LevelCatalog), typeof(GameController), typeof(Ui),
        };

        [Test]
        public void EveryStaticHolderResetsOnSubsystemRegistration()
        {
            foreach (var type in TypesWithPlayState)
            {
                var hooks = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(m => m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>())
                    .Where(a => a != null && a.loadType == RuntimeInitializeLoadType.SubsystemRegistration)
                    .ToArray();
                Assert.AreEqual(1, hooks.Length, $"{type.Name} needs exactly one SubsystemRegistration reset");
            }
        }

        [Test]
        public void BootstrapFlagStartsFalseAfterReset()
        {
            typeof(Bootstrap).GetMethod("ResetForPlay", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Assert.IsFalse(Bootstrap.HasRunThisPlay);
        }
    }
}
