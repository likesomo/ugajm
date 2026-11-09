using NUnit.Framework;
using SevenSteps.Contracts;

namespace SevenSteps.Tests
{
    /// docs/architecture.md 11.3: settings commands change one value, save at once and raise SettingsChanged.
    public sealed class SaveSettingsCommandTests
    {
        [Test]
        public void VolumeIsRoundedClampedAndSaved()
        {
            var store = new MemoryStore();
            var save = new SaveSystem(store);
            int changed = 0;
            save.SettingsChanged += _ => changed++;

            save.SetMasterVolume(0.94f);
            Assert.AreEqual(0.9f, save.Settings.masterVolume, 1e-6f);
            save.SetSfxVolume(1.1f);
            Assert.AreEqual(1f, save.Settings.sfxVolume, 1e-6f);
            save.SetSfxVolume(-0.1f);
            Assert.AreEqual(0f, save.Settings.sfxVolume, 1e-6f);
            Assert.AreEqual(3, changed);
            Assert.AreEqual(0.9f, new SaveSystem(store).Settings.masterVolume, 1e-6f, "saved to the store");
            Assert.Throws<System.ArgumentException>(() => save.SetMasterVolume(float.NaN));
        }

        [Test]
        public void TelemetryOptOutForgetsPlayerId()
        {
            var save = new SaveSystem(new MemoryStore());
            Assert.IsTrue(save.TelemetryOn(true), "no choice yet = build default");
            Assert.IsFalse(save.TelemetryOn(false));
            save.Settings.playerId = "abc";
            save.SetTelemetryChoice(false);
            Assert.AreEqual(2, save.Settings.telemetryChoice);
            Assert.AreEqual("", save.Settings.playerId);
            Assert.IsFalse(save.TelemetryOn(true));
            save.SetTelemetryChoice(true);
            Assert.AreEqual(1, save.Settings.telemetryChoice);
            Assert.IsTrue(save.TelemetryOn(false));
        }
    }
}
