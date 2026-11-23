using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using ZunuConsole.Core;

namespace ZunuConsole.Commands
{
    internal static class WorkbenchCommands
    {
        private static void Add(ZunuPlugin p, string key, string desc, string usage, Action<CommandContext, string[]> action)
        { p.Engine.Register(key, desc, "Tools", usage, action); }
        public static void Register(ZunuPlugin p)
        {
            Add(p,"note.set","Save a local named note","note.set <name> <text>",(c,a)=>
            { if(a.Length < 2){ c.Warn("Usage: note.set <name> <text>"); return;} p.Tools.Notes[a[0]]=CommandEngine.ArgText(a,1);p.Tools.Save();c.Success("Note saved: "+a[0]); });
            Add(p,"note.get","Read a named note","note.get <name>",(c,a)=>
            { if(a.Length==0){c.Warn("Usage: note.get <name>");return;}string value;if(p.Tools.Notes.TryGetValue(a[0],out value))c.Say(a[0]+": "+value);else c.Error("No such note."); });
            Add(p,"note.list","List saved note names","note.list",(c,a)=>c.Say(p.Tools.Notes.Count==0?"No notes.":string.Join(", ",p.Tools.Notes.Keys.OrderBy(x=>x).ToArray())));
            Add(p,"note.remove","Delete one named note","note.remove <name>",(c,a)=>
            {if(a.Length==0){c.Warn("Usage: note.remove <name>");return;}if(p.Tools.Notes.Remove(a[0])){p.Tools.Save();c.Success("Removed note.");}else c.Warn("Note not found.");});
            Add(p,"note.clear","Remove all saved notes","note.clear confirm",(c,a)=>
            {if(a.Length==0||a[0]!="confirm"){c.Warn("Usage: note.clear confirm");return;}p.Tools.Notes.Clear();p.Tools.Save();c.Success("Notes cleared.");});
            Add(p,"favorite.add","Star a command in the browser","favorite.add <command>",(c,a)=>
            {if(a.Length==0||p.Engine.Find(a[0])==null){c.Warn("Give a registered command.");return;}p.Tools.Favorites.Add(a[0]);p.Tools.Save();c.Success("Favorited "+a[0]);});
            Add(p,"favorite.remove","Unstar a command","favorite.remove <command>",(c,a)=>
            {if(a.Length==0){c.Warn("Usage: favorite.remove <command>");return;}if(p.Tools.Favorites.Remove(a[0])){p.Tools.Save();c.Success("Unstarred.");}else c.Warn("Not in favorites.");});
            Add(p,"favorite.list","Show favorite commands","favorite.list",(c,a)=>c.Say(p.Tools.Favorites.Count==0?"No favorites.":string.Join(", ",p.Tools.Favorites.OrderBy(x=>x).ToArray())));
            Add(p,"favorite.clear","Remove all command favorites","favorite.clear confirm",(c,a)=>
            {if(a.Length==0||a[0]!="confirm"){c.Warn("Usage: favorite.clear confirm");return;}p.Tools.Favorites.Clear();p.Tools.Save();c.Success("Favorites cleared.");});
            Add(p,"timer.start","Start or replace a named countdown","timer.start <name> <seconds>",(c,a)=>
            {float s;if(a.Length<2||!float.TryParse(a[1],NumberStyles.Float,CultureInfo.InvariantCulture,out s)||float.IsNaN(s)||float.IsInfinity(s)||s<.1f||s>86400){c.Warn("Usage: timer.start <name> <0.1..86400 seconds>");return;}p.Tools.SetTimer(a[0],s);c.Success("Started: "+a[0]);});
            Add(p,"timer.remaining","Read time left on named countdown","timer.remaining <name>",(c,a)=>
            {if(a.Length==0){c.Warn("Usage: timer.remaining <name>");return;}float remaining;if(!p.Tools.TimerRemaining(a[0],out remaining)){c.Error("Timer not found.");return;}c.Say(a[0]+": "+remaining.ToString("F1")+" sec remaining");});
            Add(p,"timer.list","List active countdowns","timer.list",(c,a)=>
            {if(!p.Tools.TimerNames.Any()){c.Say("No countdowns.");return;}foreach(var name in p.Tools.TimerNames){float remaining;p.Tools.TimerRemaining(name,out remaining);c.Say(name+": "+remaining.ToString("F1")+" sec");}});
            Add(p,"timer.remove","Remove one countdown","timer.remove <name>",(c,a)=>
            {if(a.Length==0){c.Warn("Usage: timer.remove <name>");return;}c.Say(p.Tools.RemoveTimer(a[0])?"Removed.":"Timer not found.");});
            Add(p,"timer.clear","Remove all countdowns","timer.clear",(c,a)=>{p.Tools.ClearTimers();c.Success("Timers cleared.");});
            Add(p,"stopwatch.start","Start local stopwatch","stopwatch.start",(c,a)=>{p.Tools.WatchStart();c.Success("Stopwatch running.");});
            Add(p,"stopwatch.stop","Pause local stopwatch","stopwatch.stop",(c,a)=>{p.Tools.WatchStop();c.Success("Stopped at "+p.Tools.StopwatchSeconds.ToString("F2")+" sec");});
            Add(p,"stopwatch.read","Read elapsed stopwatch time","stopwatch.read",(c,a)=>c.Say(p.Tools.StopwatchSeconds.ToString("F2")+" sec"));
            Add(p,"stopwatch.lap","Record a stopwatch lap in output","stopwatch.lap",(c,a)=>c.Success("Lap: "+p.Tools.StopwatchSeconds.ToString("F2")+" sec"));
            Add(p,"stopwatch.reset","Reset local stopwatch","stopwatch.reset",(c,a)=>{p.Tools.WatchReset();c.Success("Stopwatch reset.");});
            Add(p,"clipboard.copy","Copy entered text to local clipboard","clipboard.copy <text>",(c,a)=>{GUIUtility.systemCopyBuffer=CommandEngine.ArgText(a);c.Success("Text copied.");});
            Add(p,"clipboard.read","Show current local clipboard text","clipboard.read",(c,a)=>c.Say(GUIUtility.systemCopyBuffer));
            Add(p,"timestamp.now","Print current local date and time","timestamp.now",(c,a)=>c.Say(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            Add(p,"timestamp.utc","Print current UTC time","timestamp.utc",(c,a)=>c.Say(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")+" UTC"));
            Add(p,"random.number","Generate a non-security random whole number","random.number <min> <max>",(c,a)=>
            {int min,max;if(a.Length<2||!int.TryParse(a[0],out min)||!int.TryParse(a[1],out max)||max<min||max==int.MaxValue){c.Warn("Usage: random.number <min> <max> (max < 2147483647)");return;}c.Say(UnityEngine.Random.Range(min,max+1).ToString());});
            Add(p,"math.add","Add two numbers","math.add <a> <b>",(c,a)=>MathOp(c,a,(x,y)=>x+y));
            Add(p,"math.subtract","Subtract two numbers","math.subtract <a> <b>",(c,a)=>MathOp(c,a,(x,y)=>x-y));
            Add(p,"math.multiply","Multiply two numbers","math.multiply <a> <b>",(c,a)=>MathOp(c,a,(x,y)=>x*y));
            Add(p,"math.divide","Divide two numbers","math.divide <a> <b>",(c,a)=>MathOp(c,a,(x,y)=>x/y));
        }
        private static void MathOp(CommandContext c, string[] a, Func<double,double,double> op)
        {
            double x,y;
            if(a.Length<2||!double.TryParse(a[0],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!double.TryParse(a[1],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||double.IsNaN(x)||double.IsNaN(y)||double.IsInfinity(x)||double.IsInfinity(y))
            {c.Warn("Expected two finite numbers.");return;}
            double answer=op(x,y);
            if(double.IsInfinity(answer)||double.IsNaN(answer)){c.Error("Result is not a finite number.");return;}
            c.Say(answer.ToString("G",CultureInfo.InvariantCulture));
        }
    }
}
