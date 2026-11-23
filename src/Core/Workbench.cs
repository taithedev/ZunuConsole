using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ZunuConsole.Core
{
    internal sealed class LocalWorkbench
    {
        private readonly string filename;
        public readonly Dictionary<string, string> Notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> timers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private float watchStart;
        private float watchAccumulated;
        private bool watchRunning;
        public LocalWorkbench(string path) { filename = path; Load(); }
        public IEnumerable<string> TimerNames { get { return timers.Keys.OrderBy(x => x); } }
        public bool StopwatchRunning { get { return watchRunning; } }
        public float StopwatchSeconds { get { return watchAccumulated + (watchRunning ? Time.realtimeSinceStartup - watchStart : 0); } }
        public void WatchStart() { if (!watchRunning) { watchStart = Time.realtimeSinceStartup; watchRunning = true; } }
        public void WatchStop() { if (watchRunning) { watchAccumulated += Time.realtimeSinceStartup - watchStart; watchRunning = false; } }
        public void WatchReset() { watchRunning = false; watchAccumulated = 0; }
        public void SetTimer(string name, float durationSeconds) { timers[name] = Time.realtimeSinceStartup + durationSeconds; }
        public bool TimerRemaining(string name, out float seconds)
        {
            float finish;
            if (!timers.TryGetValue(name, out finish)) { seconds = 0; return false; }
            seconds = Math.Max(0, finish - Time.realtimeSinceStartup);
            return true;
        }
        public bool RemoveTimer(string name) { return timers.Remove(name); }
        public void ClearTimers() { timers.Clear(); }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filename));
                var lines = Notes.OrderBy(x => x.Key).Select(x => "N|" + B64(x.Key) + "|" + B64(x.Value)).ToList();
                lines.AddRange(Favorites.OrderBy(x => x).Select(x => "F|" + B64(x)));
                File.WriteAllLines(filename, lines.ToArray());
            }
            catch (Exception e) { Debug.LogWarning("ZunuConsole workbench save failed: " + e.Message); }
        }
        private void Load()
        {
            if (!File.Exists(filename)) return;
            try
            {
                foreach (string line in File.ReadAllLines(filename))
                {
                    var p = line.Split('|');
                    if (p.Length == 3 && p[0] == "N") Notes[Decode(p[1])] = Decode(p[2]);
                    if (p.Length == 2 && p[0] == "F") Favorites.Add(Decode(p[1]));
                }
            }
            catch (Exception e) { Debug.LogWarning("ZunuConsole workbench load failed: " + e.Message); }
        }
        private static string B64(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
        private static string Decode(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
    }
}
