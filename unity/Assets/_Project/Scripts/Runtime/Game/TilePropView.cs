using System.Collections;
using UnityEngine;

namespace SevenSteps.Game
{
    /// <summary>Presentation only: Blender geometry reads the existing tile variant and state, never rules.</summary>
    public sealed class TilePropView : MonoBehaviour
    {
        public Transform slab;
        public Transform handle;
        public Transform[] glyphs;
        public Transform[] lamps;
        public Transform[] sockets;
        public Transform[] tokens;
        public Renderer[] accents;
        public Color accentColor = new Color(.95f,.79f,.38f);
        void Awake() { SetData(0,0,0); }
        public float slideDepth = .61f;
        Vector3 closedPosition;
        Quaternion handleRest;
        bool initialized;
        Coroutine motion;
        int lastVariant = -1;
        static readonly Color[] PairColors = {
            new Color(.93f,.66f,.22f), new Color(.31f,.75f,.91f),
            new Color(.77f,.48f,.89f), new Color(.27f,.78f,.56f)
        };
        void Init()
        {
            if(initialized)return;
            initialized=true;
            if(slab!=null)closedPosition=slab.localPosition;
            if(handle!=null)handleRest=handle.localRotation;
        }
        /// The existing chapter material handles six planets. Joinery props use its atlas's timber swatch,
        /// while retaining the same shared material as the surrounding slate board.
        public void UsePlanet(string id)
        {
            if(id!="joinery")return;
            var block=new MaterialPropertyBlock();
            block.SetFloat("_BoardStoneSlot",2);block.SetFloat("_BoardMoss",0);block.SetColor("_BoardRockTint",Color.white);
            foreach(var r in GetComponentsInChildren<Renderer>(true))if(r.name.StartsWith("stone_"))r.SetPropertyBlock(block);
        }
        public void SetData(int variant, int required, int statues)
        {
            Init();
            if(lastVariant!=variant)
            {
                lastVariant=variant;
                for(int i=0;i<glyphs.Length;i++)if(glyphs[i]!=null)glyphs[i].gameObject.SetActive(i==Mathf.Clamp(variant,0,glyphs.Length-1));
                for(int i=0;i<lamps.Length;i++)if(lamps[i]!=null)lamps[i].gameObject.SetActive(i<variant);
                var color=glyphs.Length>0?PairColors[Mathf.Clamp(variant,0,3)]:accentColor;
                Tint(color);
            }
            for(int i=0;i<sockets.Length;i++)if(sockets[i]!=null)sockets[i].gameObject.SetActive(i<required);
            for(int i=0;i<tokens.Length;i++)if(tokens[i]!=null)tokens[i].gameObject.SetActive(i<Mathf.Min(required,statues));
        }
        void Tint(Color color)
        {
            var block=new MaterialPropertyBlock();
            block.SetColor("_BaseColor",color);block.SetFloat("_UseVertexColor",0);block.SetFloat("_UseAtlas",0);block.SetFloat("_Unlit",.8f);
            foreach(var r in accents)if(r!=null)r.SetPropertyBlock(block);
        }
        public void SetActiveVisual(bool active,bool animate)
        {
            Init();
            if(motion!=null){StopCoroutine(motion);motion=null;}
            if(handle!=null)handle.localRotation=handleRest*Quaternion.Euler(active?42:0,0,0);
            if(slab==null)return;
            // FBX roots may retain a coordinate-system rotation; motion follows the board root, not FBX local Y.
            var drop=slab.parent.InverseTransformVector(transform.TransformVector(Vector3.down*slideDepth));
            var target=closedPosition+(active?drop:Vector3.zero);
            if(animate&&isActiveAndEnabled)motion=StartCoroutine(Slide(target));
            else slab.localPosition=target;
        }
        IEnumerator Slide(Vector3 target)
        {
            var start=slab.localPosition;
            for(float t=0;t<.12f;t+=Time.deltaTime){slab.localPosition=Vector3.Lerp(start,target,Mathf.SmoothStep(0,1,t/.12f));yield return null;}
            slab.localPosition=target;motion=null;
        }
    }
}
