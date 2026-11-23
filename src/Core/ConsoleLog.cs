using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ZunuConsole.Core
{
    internal enum LogSeverity { Info, Success, Warning, Error }
    internal sealed class LogEntry
    {
        public DateTime Timestamp;
        public string Message, StackTrace;
        public LogSeverity Severity;
    }
    internal sealed class ConsoleLog
    {
        private readonly List<LogEntry> lines = new List<LogEntry>();
        private readonly SettingStore settings;
        public ConsoleLog(SettingStore s) { settings = s; }
        public IReadOnlyList<LogEntry> Entries { get { return lines; } }
        public int Count { get { return lines.Count; } }
        public void Write(string text, LogSeverity severity = LogSeverity.Info, string stack = null)
        {
            lines.Add(new LogEntry { Message = text ?? string.Empty, Severity = severity, StackTrace = stack, Timestamp = DateTime.Now });
            int max = settings.Int("ui.maxLines");
            if (lines.Count > max) lines.RemoveRange(0, lines.Count - max);
        }
        public void Clear() { lines.Clear(); }
        public IEnumerable<LogEntry> Visible()
        {
            string level = settings.Choice("ui.logLevel");
            return lines.Where(x => level == "all" || level == "info" || (level == "warn" && (x.Severity == LogSeverity.Warning || x.Severity == LogSeverity.Error)) || (level == "error" && x.Severity == LogSeverity.Error));
        }
        public string ExportText()
        {
            return string.Join(Environment.NewLine, lines.Select(x => x.Timestamp.ToString("HH:mm:ss") + " [" + x.Severity + "] " + x.Message + (settings.Bool("console.includeStackTraces") && !string.IsNullOrEmpty(x.StackTrace) ? Environment.NewLine + x.StackTrace : "")));
        }
        public string ExportTo(string directory)
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "zunu-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            File.WriteAllText(file, ExportText(), Encoding.UTF8);
            return file;
        }
    }
}
