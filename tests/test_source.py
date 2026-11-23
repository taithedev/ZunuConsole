"""Offline source checks. These do not compile or run the Gorilla Tag plugin."""
import pathlib
import re
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class ZunuConsoleSourceTests(unittest.TestCase):
    def test_plugin_identity(self):
        text = (ROOT / "src/ZunuPlugin.cs").read_text(encoding="utf-8")
        self.assertIn('BepInPlugin("com.zuzify.zunuconsole", "ZunuConsole", "1.1.1")', text)
        self.assertIn('KeyCode.F8', text)

    def test_unique_setting_names(self):
        text = (ROOT / "src/Core/SettingStore.cs").read_text(encoding="utf-8")
        names = re.findall(r'\bAdd(?:Num|Bool|Choice)\("([^"]+)"', text)
        self.assertEqual(len(names), 66)
        self.assertEqual(len(set(names)), len(names))

    def test_ui_resource_cleanup(self):
        text = (ROOT / "src/UI/ConsoleOverlay.cs").read_text(encoding="utf-8")
        self.assertIn('textures.Count > 320', text)
        self.assertIn('textures.Clear();', text)
        self.assertIn('public void Dispose()', text)
        self.assertIn('searchFocusRequested', text)
        self.assertIn('KeyCode.K', text)

    def test_screenshot_module_reference(self):
        project = (ROOT / "ZunuConsole.csproj").read_text(encoding="utf-8")
        source = (ROOT / "src/Commands/GeneralCommands.cs").read_text(encoding="utf-8")
        self.assertIn('<Reference Include="UnityEngine.ScreenCaptureModule">', project)
        self.assertIn('$(GameManaged)\\UnityEngine.ScreenCaptureModule.dll', project)
        self.assertIn("!Exists('$(GameManaged)\\UnityEngine.ScreenCaptureModule.dll')", project)
        self.assertIn('UnityEngine.ScreenCapture.CaptureScreenshot(file)', source)

    def test_no_bundled_external_assemblies(self):
        for path in ROOT.rglob('*'):
            if path.is_file():
                self.assertNotIn(path.suffix.lower(), {'.dll', '.exe', '.pdb'}, str(path))

    def test_game_references_not_copied(self):
        text = (ROOT / "ZunuConsole.csproj").read_text(encoding="utf-8")
        self.assertIn('<Private>false</Private>', text)
        self.assertIn('ValidateGameReferences', text)


if __name__ == "__main__":
    unittest.main()
