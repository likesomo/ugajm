#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using SevenSteps.Game;
using SevenSteps.Contracts;
using SevenSteps.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SevenSteps.Art
{
    /// <summary>Captures the actual Game scene and camera, including UI, at a fixed render target size.</summary>
    [InitializeOnLoad]
    public static class HollowLightReview
    {
        const string Key="HollowLightReview.active";
        const string Out="C:/unityjam/shared-assets/seven-steps/concepts/themes/planet-dressing-2026-11-12/hollow-light";
        static double ready;
        static bool armed;
        static HollowLightReview(){EditorApplication.playModeStateChanged+=Changed;if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        static string Arg(string key,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        public static void Run()
        {
            Directory.CreateDirectory(Out);SessionState.SetBool(Key,true);
            SessionState.SetString("HollowLightReview.phase",Arg("-hollow-phase","before"));
            SessionState.SetBool("HollowLightReview.winTriggered",false);
            SessionState.SetInt("HollowLightReview.stage",int.Parse(Arg("-level","8")));
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
            UnityEngine.Object.FindAnyObjectByType<GameController>().startLevel=SessionState.GetInt("HollowLightReview.stage",8);
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){ready=EditorApplication.timeSinceStartup+5;armed=true;}
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||!armed||EditorApplication.timeSinceStartup<ready)return;
            try
            {
                var cam=Camera.main;var game=UnityEngine.Object.FindAnyObjectByType<GameController>();
                if(!cam||game==null||game.Board?.State==null)throw new Exception("Actual Game scene did not initialize");
                int stage=SessionState.GetInt("HollowLightReview.stage",8);string phase=SessionState.GetString("HollowLightReview.phase","before");
                // Match the 1280x720 Game camera's aspect before BoardView's next fit update.
                if(cam.targetTexture==null)
                {
                    cam.targetTexture=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);cam.aspect=1280f/720;
                    ready=EditorApplication.timeSinceStartup+.5;return;
                }
                if(phase.Contains("clear")&&!SessionState.GetBool("HollowLightReview.winTriggered",false))
                {
                    string path=LevelCatalog.PathOf(stage-1);
                    game.Board.Replay(path.Substring(0,path.Length-1));
                    if(!Rules.TryParseLetter(path[path.Length-1],out var action))throw new Exception("Missing clear capture solution");
                    game.Board.Send(Command.FromAct(action));SessionState.SetBool("HollowLightReview.winTriggered",true);
                    ready=EditorApplication.timeSinceStartup+5;return;
                }
                if(game.LevelIndex!=stage-1)throw new Exception("Capture stage mismatch");
                foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                    if(canvas.isRootCanvas&&canvas.renderMode==RenderMode.ScreenSpaceOverlay){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;}
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.SingleCameraRequest{destination=cam.targetTexture});
                var old=RenderTexture.active;RenderTexture.active=cam.targetTexture;
                var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=old;
                File.WriteAllBytes(Path.Combine(Out,$"{phase}-stage-{stage:D2}.png"),tex.EncodeToPNG());
                var state=game.Board.State;var view=UnityEngine.Object.FindAnyObjectByType<BoardView>();var grid=view.Grid;
                float Sample(Vector3 p){var uv=cam.WorldToViewportPoint(p);float sum=0;for(int dx=-2;dx<=2;dx++)for(int dy=-2;dy<=2;dy++){var c=tex.GetPixel(Mathf.RoundToInt(uv.x*1280)+dx,Mathf.RoundToInt(uv.y*720)+dy).linear;sum+=.2126f*c.r+.7152f*c.g+.0722f*c.b;}return sum/25;}
                var floors=new System.Collections.Generic.List<float>();var walls=new System.Collections.Generic.List<float>();
                for(int y=0;y<grid.H;y++)for(int x=0;x<grid.W;x++)
                {
                    char tile=state.Level.Tile(y*grid.W+x);
                    if(tile=='.')floors.Add(Sample(grid.CellToWorld(x,y)+Vector3.up*.01f));
                    if(tile=='#')walls.Add(Sample(grid.CellToWorld(x,y)+Vector3.up*.58f));
                }
                floors.Sort();walls.Sort();float floor=floors[floors.Count/2],wall=walls[walls.Count/2],contrast=(floor-wall)/floor;
                string csv=Path.Combine(Out,"luminance.csv");if(!File.Exists(csv))File.WriteAllText(csv,"phase,stage,floorLuma,blockerLuma,contrast,orthoSize\n");
                File.AppendAllText(csv,string.Join(",",phase,stage,floor.ToString("F4",CultureInfo.InvariantCulture),wall.ToString("F4",CultureInfo.InvariantCulture),contrast.ToString("F4",CultureInfo.InvariantCulture),cam.orthographicSize.ToString("F3",CultureInfo.InvariantCulture))+"\n");
                Debug.Log($"[HollowLight] {phase} stage {stage}: floor={floor:F4} blocker={wall:F4} contrast={contrast:P1}");
                if(phase=="after"&&(floor<.18f||floor>.25f||contrast<.30f))throw new Exception("Hollow target luminance or contrast failed");
                if(stage==8||stage==11)
                {
                    int next=stage==8?11:14;SessionState.SetInt("HollowLightReview.stage",next);
                    Services.Get<AppFlow>().GoToGame(next-1);ready=EditorApplication.timeSinceStartup+3;return;
                }
                SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
#endif
