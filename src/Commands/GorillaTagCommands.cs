using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZunuConsole.Core;

namespace ZunuConsole.Commands
{
    /// <summary>
    /// Safe, read-only inspection commands for the current local Unity game.
    /// Does not inspect remote players, change networking, or require Gorilla Tag internals.
    /// </summary>
    internal static class GorillaTagCommands
    {
        private static void Add(ZunuPlugin p, string key, string desc, string usage, Action<CommandContext,string[]> action)
        { p.Engine.Register(key, desc, "Gorilla Tag / Local", usage, action); }
        private static Camera MainCamera(CommandContext c)
        {
            Camera camera = Camera.main;
            if (camera == null) c.Warn("No Camera.main found in the current scene.");
            return camera;
        }
        public static void Register(ZunuPlugin p)
        {
            Add(p,"gtag.scene","Show the active Unity scene","gtag.scene",(c,a)=>
            {var scene=SceneManager.GetActiveScene();c.Say("Active scene: "+scene.name+" (build "+scene.buildIndex+")");});
            Add(p,"gtag.scene.loaded","List locally loaded Unity scenes","gtag.scene.loaded",(c,a)=>
            {for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);c.Say(i+": "+scene.name+" (loaded="+scene.isLoaded+")");}});
            Add(p,"gtag.scene.roots","List active scene root GameObjects (first 30)","gtag.scene.roots",(c,a)=>
            {var scene=SceneManager.GetActiveScene();int count=0;foreach(var go in scene.GetRootGameObjects()){if(count++>=30)break;c.Say(go.name+" (active="+go.activeSelf+")");}c.Say("Total root objects: "+scene.rootCount);});
            Add(p,"gtag.scene.find","Read transform of a named local GameObject","gtag.scene.find <object-name>",(c,a)=>
            {if(a.Length==0){c.Warn("Usage: gtag.scene.find <object-name>");return;}var go=GameObject.Find(CommandEngine.ArgText(a));if(go==null){c.Warn("No active GameObject with that exact name.");return;}c.Say(go.name+" at "+go.transform.position+"; layer="+go.layer);});
            Add(p,"gtag.camera","Describe the current main camera","gtag.camera",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Camera: "+cam.gameObject.name+"; enabled="+cam.enabled+"; stereo="+cam.stereoEnabled);});
            Add(p,"gtag.camera.position","Display local main camera position","gtag.camera.position",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Main camera position: "+cam.transform.position);});
            Add(p,"gtag.camera.rotation","Display main camera Euler orientation","gtag.camera.rotation",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Main camera rotation: "+cam.transform.eulerAngles);});
            Add(p,"gtag.camera.forward","Display main camera forward vector","gtag.camera.forward",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Main camera forward: "+cam.transform.forward);});
            Add(p,"gtag.camera.fov","Display main camera field of view","gtag.camera.fov",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Camera FOV: "+cam.fieldOfView);});
            Add(p,"gtag.camera.clip","Display main camera near/far clipping planes","gtag.camera.clip",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Near "+cam.nearClipPlane+"; far "+cam.farClipPlane);});
            Add(p,"gtag.camera.viewport","Display main camera viewport rect","gtag.camera.viewport",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Viewport: "+cam.rect);});
            Add(p,"gtag.camera.depth","Display main camera depth","gtag.camera.depth",(c,a)=>
            {var cam=MainCamera(c);if(cam!=null)c.Say("Camera depth: "+cam.depth);});
            Add(p,"gtag.physics.gravity","Read local Unity gravity","gtag.physics.gravity",(c,a)=>c.Say("Physics.gravity: "+Physics.gravity));
            Add(p,"gtag.physics.fixed","Read local fixed timestep","gtag.physics.fixed",(c,a)=>c.Say("Fixed timestep: "+Time.fixedDeltaTime+"s"));
            Add(p,"gtag.performance.delta","Read latest unscaled frame delta","gtag.performance.delta",(c,a)=>c.Say("Unscaled delta: "+Time.unscaledDeltaTime+"s"));
            Add(p,"gtag.performance.smooth","Read Unity smoothed frame delta","gtag.performance.smooth",(c,a)=>c.Say("Smooth delta: "+Time.smoothDeltaTime+"s"));
            Add(p,"gtag.performance.timescale","Read global Unity timescale (read-only)","gtag.performance.timescale",(c,a)=>c.Say("Time.timeScale: "+Time.timeScale));
            Add(p,"gtag.performance.frame","Read current Unity frame number","gtag.performance.frame",(c,a)=>c.Say("Frame: "+Time.frameCount));
            Add(p,"gtag.performance.realtime","Read Unity realtime since startup","gtag.performance.realtime",(c,a)=>c.Say(Time.realtimeSinceStartup+"s"));
            Add(p,"gtag.world.objects","Count locally active Unity GameObjects","gtag.world.objects",(c,a)=>c.Say("Active GameObjects: "+UnityEngine.Object.FindObjectsOfType<GameObject>().Length));
            Add(p,"gtag.world.colliders","Count locally active colliders","gtag.world.colliders",(c,a)=>c.Say("Active colliders: "+UnityEngine.Object.FindObjectsOfType<Collider>().Length));
            Add(p,"gtag.world.lights","Count locally active lights","gtag.world.lights",(c,a)=>c.Say("Active lights: "+UnityEngine.Object.FindObjectsOfType<Light>().Length));
            Add(p,"gtag.world.audiosources","Count locally active AudioSource components","gtag.world.audiosources",(c,a)=>c.Say("Audio sources: "+UnityEngine.Object.FindObjectsOfType<AudioSource>().Length));
            Add(p,"gtag.tags.find","Count active GameObjects with an existing Unity tag","gtag.tags.find <tag>",(c,a)=>
            {if(a.Length==0){c.Warn("Usage: gtag.tags.find <tag>");return;}c.Say("Active objects tagged '"+a[0]+"': "+GameObject.FindGameObjectsWithTag(a[0]).Length);});
        }
    }
}
