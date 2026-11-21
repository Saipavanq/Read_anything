import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from config import DEFAULTS
from hotkeys import to_pynput
from llm import build_messages
from voice import parse_command


class HotkeyTests(unittest.TestCase):
    def test_basic_combo(self):
        self.assertEqual(to_pynput("ctrl+alt+r"), "<ctrl>+<alt>+r")

    def test_modifier_aliases(self):
        self.assertEqual(to_pynput("Control + Shift + Space"), "<ctrl>+<shift>+<space>")

    def test_empty_raises(self):
        with self.assertRaises(ValueError):
            to_pynput("")


class VoiceCommandTests(unittest.TestCase):
    def test_read(self):
        self.assertEqual(parse_command("Read this please."), ("read", "verbatim"))

    def test_modes(self):
        self.assertEqual(parse_command("summarize that"), ("read", "summarize"))
        self.assertEqual(parse_command("simplify it"), ("read", "simplify"))
        self.assertEqual(parse_command("translate this"), ("read", "translate"))

    def test_stop(self):
        self.assertEqual(parse_command("stop"), ("stop", None))

    def test_unknown(self):
        self.assertEqual(parse_command("weather tomorrow"), ("unknown", "weather tomorrow"))

    def test_empty(self):
        self.assertEqual(parse_command("   "), ("none", None))


class ConfigTests(unittest.TestCase):
    def test_default_hotkey(self):
        self.assertEqual(DEFAULTS["hotkey_read"], "ctrl+alt+r")

    def test_default_engine_offline(self):
        self.assertEqual(DEFAULTS["tts_engine"], "offline")


class LLMPromptTests(unittest.TestCase):
    def test_translate_prompt(self):
        cfg = dict(DEFAULTS, llm_translate_to="Telugu")
        messages = build_messages("hello", "translate", cfg)
        self.assertIn("Telugu", messages[0]["content"])
        self.assertEqual(messages[1]["content"], "hello")

    def test_input_truncation(self):
        messages = build_messages("x" * 10000, "summarize", dict(DEFAULTS))
        self.assertEqual(len(messages[1]["content"]), 6000)


if __name__ == "__main__":
    unittest.main()
