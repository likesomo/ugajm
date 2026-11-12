using System;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>
    /// What <see cref="ChoiceController"/> drives at the 7th step (docs 4.4, 14.11): the current HUD's KEEP / SWITCH cards
    /// (<see cref="UI.HudView"/>) or the U5 glyph pair (<see cref="UI.U5.U5GameHud"/>, docs/ui-u5-kit.md 3). The scene root
    /// picks one when it builds the game screen (UiStyleToggle); the controller, rules and telemetry are the same.
    /// </summary>
    public interface IChoiceView
    {
        /// A choice got the pointer, the keyboard focus or a first tap: true = KEEP. The controller moves the board ghost.
        event Action<bool> ChoiceFocused;
        /// Show (c.On) or hide the choice with the controller's focus and explanation state.
        void ShowChoice(UI.HudView.ChoiceCards c);
        /// Put the choice beside <paramref name="board"/> (projected board area) near <paramref name="brother"/> (his head),
        /// both canvas units with a top-left origin. False when no candidate fits.
        bool PlaceChoice(Rect board, Vector2 brother);
        /// A move was pressed while the choice is pending: shake it.
        void NudgeChoice();
    }
}
