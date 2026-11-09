using System;
using SevenSteps.Contracts;
using SevenSteps.Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace SevenSteps.Art {
 public sealed class RuinsHengeEnvironment : MonoBehaviour {
  public Material surface,space;public Transform gallery;
  GameController game;RuinsHengePlanet planet;int stage=-1;
  void Start(){game=FindFirstObjectByType<GameController>();planet=gameObject.AddComponent<RuinsHengePlanet>();planet.surface=surface;var sky=gameObject.AddComponent<RuinsHengeSky>();sky.space=space;
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"-artfxoff")>=0||Application.absoluteURL.Contains("artfx=off")){foreach(var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))v.enabled=false;if(Camera.main)Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing=false;}}
  void LateUpdate(){if(!game||game.Levels==null||game.Levels.Length==0||stage==game.LevelIndex)return;stage=game.LevelIndex;var level=game.Levels[stage];planet.Fit(new BoardGrid(level));if(gallery)gallery.position=new Vector3(0,0,-level.H*.5f-.8f);}
 }
}