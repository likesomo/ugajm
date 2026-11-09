using System;
using System.Collections;
using System.IO;
using SevenSteps.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace SevenSteps.Art
{
    // Art-only review scene: renders actual saved presets, then the bilingual UI capture scene.
    public sealed class PlanetReviewCapture : MonoBehaviour
    {
        public ChapterPreset[] chapters;
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int k=Array.IndexOf(args,"-planet-capture");
            if(k<0||k+1>=args.Length)yield break;
            string output=args[k+1];Directory.CreateDirectory(output);var cam=Camera.main;
            foreach(var chapter in chapters)
            {
                var root=new GameObject("Review_"+chapter.id);
                var planet=Instantiate(chapter.sky.planetPrefab,root.transform).GetComponent<RuinsHengePlanet>();planet.Preview(5,5);
                var visual=chapter.tileSet.floorAlt.HasPrefab?chapter.tileSet.floorAlt:chapter.tileSet.floor;
                for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++){var floor=Instantiate(visual.prefab,root.transform);floor.transform.localPosition=new Vector3(x,0,z)+visual.offset;floor.transform.localRotation=visual.RotationFor(x+z*5);floor.transform.localScale=visual.Scale;
                    foreach(var renderer in floor.GetComponentsInChildren<Renderer>()){var mats=renderer.sharedMaterials;for(int m=0;m<mats.Length;m++)mats[m]=chapter.Override(mats[m]);renderer.sharedMaterials=mats;}
                }
                var sky=Instantiate(chapter.sky.skyPrefab,cam.transform);
                foreach(var rig in new[]{chapter.lighting.key,chapter.lighting.fill,chapter.lighting.rim})
                {
                    var light=new GameObject("ReviewLight",typeof(Light)).GetComponent<Light>();light.transform.SetParent(root.transform);light.type=LightType.Directional;light.enabled=rig.enabled;light.color=rig.color;light.intensity=rig.intensity;light.transform.rotation=Quaternion.Euler(rig.euler);light.shadows=rig.shadows?LightShadows.Soft:LightShadows.None;
                }
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=chapter.lighting.ambient;cam.backgroundColor=chapter.sky.background;
                yield return null;yield return new WaitForSeconds(.25f);yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output,chapter.id+".png"));yield return new WaitForSeconds(.3f);
                Destroy(sky);Destroy(root);yield return null;
            }
            Debug.Log("[PlanetReview] Captured seven actual Unity presets, 5x5 board, one-cell prop exclusion");
            Screen.SetResolution(1280,720,FullScreenMode.Windowed);yield return new WaitForSeconds(.5f);SceneManager.LoadScene("HandwrittenUI");
        }
    }
}
