using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZunuConsole.Core
{
    internal sealed class ConsoleHistory
    {
        private readonly List<string> lines = new List<string>();
        private readonly SettingStore settings;
        private readonly string filename;
        public ConsoleHistory(SettingStore s, string file) { settings = s; filename = file; Load(); }
        public IReadOnlyList<string> Entries { get { return lines; } }
        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            lines.Add(command.Trim());
            int max = settings.Int("ui.historyLimit");
            if (lines.Count > max) lines.RemoveRange(0, lines.Count - max);
            if (settings.Bool("console.persistHistory")) Save();
        }
        public void Clear() { lines.Clear(); Save(); }
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filename));
                File.WriteAllLines(filename, lines.Select(x => Convert.ToBase64String(Encoding.UTF8.GetBytes(x))).ToArray());
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("ZunuConsole history write failed: " + e.Message); }
        }
        public void Load()
        {
            lines.Clear();
            if (!File.Exists(filename)) return;
            try
            {
                foreach (var l in File.ReadAllLines(filename).TakeLastCompat(settings.Int("ui.historyLimit")))
                    lines.Add(Encoding.UTF8.GetString(Convert.FromBase64String(l)));
            }
            catch (Exception e) { UnityEngine.Debug.LogWarning("ZunuConsole history load failed: " + e.Message); }
        }
    }
    internal static class HistoryExtensions
    {
        public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> entries, int n) { var a = entries.ToArray(); return a.Skip(Math.Max(0, a.Length - n)); }
    }
}
