using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Game.Telemetry;
using SevenSteps.Game.UI;
using UnityEditor;
using UnityEngine;

namespace SevenSteps.Tests
{
    /// <summary>
    /// The facilitator's telemetry line in the settings panel (playtest-1-prep E): off when nothing is sent, otherwise
    /// events sent / waiting, the last reply and a short player id. Owner: gameplay/UI.
    /// </summary>
    public sealed class SettingsStatusTests
    {
        static Localizer Ko()
        {
            StringTable T(Language l) => StringTable.FromJson(l,
                AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/_Project/Data/Strings/{Languages.Code(l)}.json").text);
            return new Localizer(new[] { T(Language.Ko), T(Language.En) }, Language.Ko);
        }

        [Test]
        public void OffWhenNoHttpTelemetryOrDisabled()
        {
            var loc = Ko();
            Assert.AreEqual("보내지 않음", SettingsPanel.StatusLine(loc, null));
            var t = new HttpTelemetry(new TelemetryContext { PlayerId = "3f2a9c1e-aaaa-4bbb-8ccc-dddddddddddd" }, new MemoryStore(), _ => 1);
            Assert.AreEqual("보내지 않음", SettingsPanel.StatusLine(loc, t));
        }

        [Test]
        public void OnShowsCountsNoReplyYetAndShortId()
        {
            var loc = Ko();
            var t = new HttpTelemetry(new TelemetryContext { PlayerId = "3f2a9c1e-aaaa-4bbb-8ccc-dddddddddddd" }, new MemoryStore(), _ => 1);
            t.SetEnabled(true);
            Assert.AreEqual($"보냄 0, 대기 {t.PendingEvents}, 마지막 응답 -, ID 3f2a9c1e", SettingsPanel.StatusLine(loc, t));
            loc.Set(Language.En);
            StringAssert.StartsWith("Sent 0, waiting ", SettingsPanel.StatusLine(loc, t));
        }
    }
}
