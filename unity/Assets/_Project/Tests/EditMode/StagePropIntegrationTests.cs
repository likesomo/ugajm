#if UNITY_EDITOR
using System.Linq;
using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;
using SevenSteps.Contracts;
using SevenSteps.Core;
using SevenSteps.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace SevenSteps.Tests
{
 public sealed class StagePropIntegrationTests
 {
  const string SetPath="Assets/_Project/Art/RuinsHenge/RuinsHenge_Chapter01.asset";
  [Test]
  public void ProductionPrefabsContainAllExportedGeometryVariants()
  {
   var set=AssetDatabase.LoadAssetAtPath<TileSet>(SetPath);
   Assert.That(set.exit.hideLabel&&set.gate.hideLabel&&set.countDoor.hideLabel);
   Assert.That(set.plates.Length,Is.EqualTo(4));Assert.That(set.doors.Length,Is.EqualTo(4));
   foreach(var visual in set.plates.Concat(set.doors))
   {
    Assert.That(visual.hideLabel);var prop=visual.prefab.GetComponent<TilePropView>();
    Assert.That(prop,Is.Not.Null);Assert.That(prop.glyphs.Length,Is.EqualTo(4));Assert.That(prop.glyphs.All(g=>g!=null));Assert.That(prop.slab,Is.Not.Null);
   }
   Assert.That(set.gate.prefab.GetComponent<TilePropView>().lamps.Length,Is.EqualTo(7));
   var count=set.countDoor.prefab.GetComponent<TilePropView>();Assert.That(count.sockets.Length,Is.EqualTo(7));Assert.That(count.tokens.Length,Is.EqualTo(7));Assert.That(count.slab,Is.Not.Null);
  }
  [Test]
  public void CounterMeshesAreCountableIn1280x720Stage15Camera()
  {
   var set=AssetDatabase.LoadAssetAtPath<TileSet>(SetPath);
   var root=new GameObject("Counter pixel projection");var rt=new RenderTexture(1280,720,24);
   try{
    var cam=root.AddComponent<Camera>();cam.targetTexture=rt;cam.aspect=1280f/720;
    var level=JsonUtility.FromJson<LevelFileDto>(System.IO.File.ReadAllText("Assets/_Project/Data/levels.json")).levels[14].ToLevel();BoardView.Fit(cam,new BoardGrid(level),set.cameraRig);
    var go=Object.Instantiate(set.gate.prefab);go.transform.SetParent(root.transform,true);go.transform.position=new BoardGrid(level).CellToWorld(2,2);var prop=go.GetComponent<TilePropView>();
    for(int count=1;count<=7;count++){
     prop.SetData(count,0,0);prop.SetActiveVisual(false,false);
     Assert.That(prop.lamps.Count(t=>t.gameObject.activeSelf),Is.EqualTo(count));
     var points=prop.lamps.Take(count).Select(t=>cam.WorldToScreenPoint(t.position)).ToArray();
     for(int i=0;i<count;i++)for(int j=0;j<i;j++)Assert.That(Vector2.Distance(points[i],points[j]),Is.GreaterThanOrEqualTo(6),"Physical lamp spacing in the game camera");
     var pip=prop.lamps[0].GetComponentsInChildren<Renderer>().Single(r=>r.name.StartsWith("inlay_lamp"));var b=pip.bounds;
     Assert.That(Mathf.Abs(cam.WorldToScreenPoint(b.center+Vector3.right*b.extents.x).x-cam.WorldToScreenPoint(b.center-Vector3.right*b.extents.x).x),Is.GreaterThanOrEqualTo(6),"Lamp diameter at native game resolution");
    }
   }finally{Object.DestroyImmediate(root);Object.DestroyImmediate(rt);}
  }
  [UnityTest]
  public IEnumerator RealStageStateOpensPairedDoorAndSnapSettlesItsSlabWithoutLabels()
  {
   yield return new EnterPlayMode();
   var dto=JsonUtility.FromJson<LevelFileDto>(System.IO.File.ReadAllText("Assets/_Project/Data/levels.json")).levels[4];
   var set=AssetDatabase.LoadAssetAtPath<TileSet>(SetPath);var root=new GameObject("Integration");
   var previousTheme=SevenSteps.Game.UI.Ui.HasTheme?SevenSteps.Game.UI.Ui.Theme:null;SevenSteps.Game.UI.Ui.Theme=AssetDatabase.LoadAssetAtPath<SevenSteps.Game.UI.UiTheme>("Assets/_Project/Art/UI/RuinsHengeTheme.asset");
   try {
    var cam=new GameObject("Camera",typeof(Camera)).GetComponent<Camera>();cam.transform.SetParent(root.transform);
    var canvas=new GameObject("Labels",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(root.transform);
    var view=root.AddComponent<BoardView>();view.Init(set,canvas.GetComponent<RectTransform>(),cam);var initial=Rules.NewState(dto.ToLevel());view.Rebuild(initial);
    var moonDoor=view.GetComponentsInChildren<TilePropView>().Single(p=>p.name.StartsWith("Door1 "));
    Assert.That(moonDoor.glyphs[1].gameObject.activeSelf);Assert.That(moonDoor.glyphs.Count(g=>g.gameObject.activeSelf),Is.EqualTo(1));
    var pairedDoor=view.GetComponentsInChildren<TilePropView>().Single(p=>p.name.StartsWith("Door0 "));var closed=pairedDoor.slab.position;var state=initial;
    foreach(char a in dto.expected.path){state=Rules.Dispatch(state,Command.FromAct(a switch{'L'=>Act.Left,'R'=>Act.Right,'D'=>Act.Down,'U'=>Act.Up,'K'=>Act.Keep,_=>Act.Switch})).State;if(state.Statues.Length>0)break;}
    Assert.That(Rules.DoorOpen(state,'A'));view.Snap(state);Assert.That(pairedDoor.slab.position.y,Is.LessThan(closed.y-.5f));
    var open=pairedDoor.slab.position;Assert.That(open.x,Is.EqualTo(closed.x).Within(.001f));Assert.That(open.z,Is.EqualTo(closed.z).Within(.001f),"Imported FBX axes must not move the slab across its cell");pairedDoor.slab.position=Vector3.Lerp(closed,open,.5f);view.Snap(state);Assert.That(pairedDoor.slab.position,Is.EqualTo(open),"Snap settles an interrupted slide");
    Assert.That(canvas.GetComponentsInChildren<Text>(true).All(t=>!t.enabled||!(t.text=="A"||t.text=="B"||t.text=="EXIT")));
   }finally{Object.DestroyImmediate(root);if(previousTheme!=null)SevenSteps.Game.UI.Ui.Theme=previousTheme;}
   yield return new ExitPlayMode();
  }
 }
}
#endif
