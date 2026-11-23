using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZunuConsole.Core
{
    internal sealed class Command
    {
        public string Name, Description, Category, Usage;
        public Action<CommandContext, string[]> Handler;
    }
    internal sealed class CommandContext
    {
        public readonly ZunuPlugin Plugin;
        public CommandContext(ZunuPlugin plugin) { Plugin = plugin; }
        public void Say(string message) { Plugin.Log.Write(message); }
        public void Success(string message) { Plugin.Log.Write(message, LogSeverity.Success); }
        public void Warn(string message) { Plugin.Log.Write(message, LogSeverity.Warning); }
        public void Error(string message) { Plugin.Log.Write(message, LogSeverity.Error); }
    }
    internal sealed class CommandEngine
    {
        private readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
        private readonly ZunuPlugin plugin;
        public CommandEngine(ZunuPlugin owner) { plugin = owner; }
        public IEnumerable<Command> Commands { get { return commands.Values.OrderBy(x => x.Name); } }
        public int Count { get { return commands.Count; } }
        public void Register(string name, string description, string group, string usage, Action<CommandContext, string[]> action)
        {
            if (string.IsNullOrWhiteSpace(name) || commands.ContainsKey(name)) throw new InvalidOperationException("Duplicate command: " + name);
            commands.Add(name, new Command { Name = name, Description = description, Category = group, Usage = usage, Handler = action });
        }
        public void Run(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            plugin.History.Add(input);
            if (plugin.Settings.Bool("console.echoCommands")) plugin.Log.Write("> " + input);
            string[] tokens;
            try { tokens = Tokenize(input).ToArray(); }
            catch (FormatException e) { plugin.Log.Write("Parse error: " + e.Message, LogSeverity.Error); return; }
            if (tokens.Length == 0) return;
            Command match;
            if (!commands.TryGetValue(tokens[0], out match))
            {
                plugin.Log.Write("Unknown command '" + tokens[0] + "'. Try help or search <term>.", LogSeverity.Error);
                return;
            }
            try { match.Handler(new CommandContext(plugin), tokens.Skip(1).ToArray()); }
            catch (Exception e)
            {
                plugin.Log.Write("'" + match.Name + "' failed: " + e.Message, LogSeverity.Error);
                plugin.ReportException(e);
            }
        }
        public Command Find(string name) { Command c; commands.TryGetValue(name, out c); return c; }
        public IEnumerable<Command> Search(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Commands;
            return Commands.Where(x => plugin.Settings.Choice("console.searchMode") == "prefix"
                ? x.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                : x.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 || x.Description.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
        }
        public IEnumerable<string> Complete(string prefix)
        {
            return Commands.Select(c => c.Name).Where(c => c.StartsWith(prefix ?? "", StringComparison.OrdinalIgnoreCase));
        }
        public static List<string> Tokenize(string input)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            bool begun = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '"') { quoted = !quoted; begun = true; continue; }
                if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (begun) { result.Add(current.ToString()); current.Clear(); begun = false; }
                }
                else { current.Append(c); begun = true; }
            }
            if (quoted) throw new FormatException("Unclosed quote");
            if (begun) result.Add(current.ToString());
            return result;
        }
        public static string ArgText(string[] a, int start = 0) { return string.Join(" ", a.Skip(start)); }
    }
}
