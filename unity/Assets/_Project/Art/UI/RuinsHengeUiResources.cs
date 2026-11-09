using UnityEngine;
using UnityEngine.U2D;
using SevenSteps.Game.UI;
namespace SevenSteps.Art
{
    [CreateAssetMenu(menuName="Seven Steps/Art/UI Resources")]
    public sealed class RuinsHengeUiResources : ScriptableObject
    {
        public UiTheme theme;
        public SpriteAtlas atlas;
        public Sprite buttonDefault,buttonHover,buttonPressed,buttonDisabled,panelStone,panelWood;
        public Sprite undo,restart,hint,settings,sound,starEmpty,starFilled,bodyA,bodyB,bodyC,bodyStone;
        [Tooltip("Normalized screen positions; bowl 1-2-3-4-1; handle 4-5-6-7.")]
        public Vector2[] dipper = {new Vector2(.12f,.12f),new Vector2(.10f,.36f),new Vector2(.35f,.40f),new Vector2(.42f,.18f),new Vector2(.62f,.12f),new Vector2(.79f,.20f),new Vector2(.98f,.28f)};
    }
}
