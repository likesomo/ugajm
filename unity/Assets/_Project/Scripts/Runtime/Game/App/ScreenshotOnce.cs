using System.Collections;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// "-screenshot out.png" (smoke test): wait for the screen to settle, save one frame, log it and quit.
    /// Used by the Game scene (with the replay state in the log line) and the Title scene.
    /// </summary>
    public static class ScreenshotOnce
    {
        public static IEnumerator Run(string path, System.Func<string> describe)
        {
            yield return new WaitForSeconds(1.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[SevenSteps] Screenshot {path} ({describe()})");
            Application.Quit();
        }
    }
}
