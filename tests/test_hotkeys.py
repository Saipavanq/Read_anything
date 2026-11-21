"""Tests for hotkey parsing and validation."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from config import DEFAULTS
from hotkeys import describe, to_pynput, validate_hotkeys


class HotkeyTests(unittest.TestCase):
    def test_basic_combo(self):
        self.assertEqual(to_pynput("ctrl+alt+r"), "<ctrl>+<alt>+r")

    def test_modifier_aliases(self):
        self.assertEqual(to_pynput("Control + Shift + Space"), "<ctrl>+<shift>+<space>")

    def test_empty_raises(self):
        with self.assertRaises(ValueError):
            to_pynput("")

    def test_function_key_uses_angle_brackets(self):
        self.assertEqual(to_pynput("ctrl+alt+f13"), "<ctrl>+<alt>+<f13>")

    def test_describe(self):
        self.assertEqual(describe("ctrl+alt+r"), "Ctrl+Alt+R")


class ValidateHotkeysTests(unittest.TestCase):
    def test_validate_returns_all_three_actions_when_valid(self):
        bindings, problems = validate_hotkeys(dict(DEFAULTS))
        self.assertEqual({"read", "voice", "stop"}, set(bindings))
        self.assertEqual([], problems)

    def test_duplicate_hotkeys_rejected(self):
        cfg = dict(DEFAULTS, hotkey_stop="ctrl+alt+r")
        bindings, problems = validate_hotkeys(cfg)
        self.assertNotIn("stop", bindings)
        self.assertIn("read", bindings)
        self.assertTrue(any("duplicate" in p.lower() for p in problems))

    def test_malformed_combo_rejected(self):
        cfg = dict(DEFAULTS, hotkey_voice="ctrl+shift+")
        bindings, problems = validate_hotkeys(cfg)
        self.assertNotIn("voice", bindings)
        self.assertTrue(any("malformed" in p.lower() for p in problems))

    def test_modifiers_only_is_malformed(self):
        cfg = dict(DEFAULTS, hotkey_read="ctrl+alt")
        bindings, problems = validate_hotkeys(cfg)
        self.assertNotIn("read", bindings)
        self.assertTrue(problems)

    def test_unknown_key_accepted(self):
        cfg = dict(DEFAULTS, hotkey_read="ctrl+alt+f13")
        bindings, problems = validate_hotkeys(cfg)
        self.assertEqual("<ctrl>+<alt>+<f13>", bindings["read"])
        self.assertEqual([], problems)


if __name__ == "__main__":
    unittest.main()
