using System;
using System.Globalization;
using ZunuConsole.Core;

namespace ZunuConsole.Commands
{
    internal static class PreferenceCommands
    {
        public static void Register(ZunuPlugin p)
        {
            foreach (var setting in p.Settings.All)
            {
                SettingSpec spec = setting;
                string group = "Settings / " + spec.Category.ToUpperInvariant();
                p.Engine.Register(spec.Key, spec.Description + " (read current value)", group, spec.Key, (c, a) => c.Say(spec.Key + " = " + p.Settings.Get(spec.Key) + " (default " + spec.Default + ")"));
                if (spec.Type == SettingType.Toggle)
                {
                    p.Engine.Register(spec.Key + ".on", "Enable " + spec.Key, group, spec.Key + ".on", (c, a) => Change(p, c, spec.Key, "true"));
                    p.Engine.Register(spec.Key + ".off", "Disable " + spec.Key, group, spec.Key + ".off", (c, a) => Change(p, c, spec.Key, "false"));
                    p.Engine.Register(spec.Key + ".toggle", "Invert " + spec.Key, group, spec.Key + ".toggle", (c, a) => Change(p, c, spec.Key, p.Settings.Bool(spec.Key) ? "false" : "true"));
                }
                else
                {
                    p.Engine.Register(spec.Key + ".set", "Update " + spec.Key, group, spec.Key + ".set <value>", (c, a) =>
                    {
                        if (a.Length == 0) { c.Warn("Usage: " + spec.Key + ".set <value>"); return; }
                        Change(p, c, spec.Key, a[0]);
                    });
                    p.Engine.Register(spec.Key + ".reset", "Restore default for " + spec.Key, group, spec.Key + ".reset", (c, a) => Change(p, c, spec.Key, spec.Default));
                    if (spec.Type == SettingType.Number)
                    {
                        p.Engine.Register(spec.Key + ".inc", "Increase " + spec.Key + " by one step", group, spec.Key + ".inc", (c, a) => Adjust(p, c, spec, +1));
                        p.Engine.Register(spec.Key + ".dec", "Decrease " + spec.Key + " by one step", group, spec.Key + ".dec", (c, a) => Adjust(p, c, spec, -1));
                    }
                }
            }
        }
        private static void Change(ZunuPlugin p, CommandContext c, string key, string value)
        {
            string error;
            if (!p.Settings.Set(key, value, out error)) c.Error(error);
            else c.Success(key + " = " + p.Settings.Get(key));
        }
        private static void Adjust(ZunuPlugin p, CommandContext c, SettingSpec s, int direction)
        {
            float newValue = Math.Max(s.Minimum, Math.Min(s.Maximum, p.Settings.Number(s.Key) + direction * s.Step));
            Change(p, c, s.Key, newValue.ToString(CultureInfo.InvariantCulture));
        }
    }
}
