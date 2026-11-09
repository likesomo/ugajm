using UnityEngine;
namespace SevenSteps.Art
{
    /// <summary>Deterministic cosmetic variation; does not change the wall tile or colliders.</summary>
    public sealed class RuinsWallVariations : MonoBehaviour
    {
        public GameObject[] alternatives;
        void Start()
        {
            if(alternatives==null||alternatives.Length<3)return;
            int hash=Mathf.Abs(Mathf.RoundToInt(transform.position.x)*17+Mathf.RoundToInt(transform.position.z)*31);
            int choice=hash%7;if(choice>2)return;
            var original=transform.Find("Model");if(original)original.gameObject.SetActive(false);
            var variant=Instantiate(alternatives[choice],transform,false);variant.name="CosmeticWallVariant";
        }
    }
}
