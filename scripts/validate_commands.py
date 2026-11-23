"""Static repository checks. Does not replace compiling against your game assemblies."""
from collections import Counter
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / "src" / "Core" / "SettingStore.cs").read_text(encoding="utf-8")
commands = []
keys = set()
counts = {"number": 0, "toggle": 0, "choice": 0, "explicit": 0}
for line in source.splitlines():
    hit = re.search(r'\b(AddNum|AddBool|AddChoice)\("([^"]+)"', line)
    if not hit:
        continue
    kind, key = hit.groups()
    assert key not in keys, f"Duplicate setting {key}"
    keys.add(key)
    suffixes = {"AddNum": ("", "set", "reset", "inc", "dec"),
                "AddBool": ("", "on", "off", "toggle"),
                "AddChoice": ("", "set", "reset")}[kind]
    counts[{"AddNum": "number", "AddBool": "toggle", "AddChoice": "choice"}[kind]] += 1
    commands.extend(key + ("." + suffix if suffix else "") for suffix in suffixes)

for path in (ROOT / "src" / "Commands").glob("*.cs"):
    if path.name == "PreferenceCommands.cs":
        continue
    hits = re.findall(r'\bAdd\(p,\s*"([^"]+)"', path.read_text(encoding="utf-8"))
    counts["explicit"] += len(hits)
    commands.extend(hits)

for path in (ROOT / "src").rglob("*.cs"):
    body = path.read_text(encoding="utf-8")
    for key in re.findall(r'(?:S|Settings|settings)\.(?:Bool|Number|Int|Choice|Get|Set|Reset)\(\s*"([^"]+)"', body):
        assert key in keys, f"Unknown setting {key} referenced in {path.name}"

duplicates = [k for k, count in Counter(commands).items() if count != 1]
assert not duplicates, f"Duplicate commands: {duplicates}"
assert len(commands) >= 256, f"Only {len(commands)} commands"
print(f"PASS: {len(commands)} unique commands; {len(keys)} settings; {counts}")
