import os
import sys
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import selection
from config import DEFAULTS
from hotkeys import to_pynput
from llm import build_messages
from voice import VoiceCommandListener, parse_command


def _raise_for_bitmap(fmt, value):
    if fmt == 2:
        raise RuntimeError("unsupported clipboard format")


class ObservabilityTests(unittest.TestCase):
    def test_clipboard_restore_warns_on_unsupported_format(self):
        with patch.object(selection.win32clipboard, "OpenClipboard"), \
             patch.object(selection.win32clipboard, "EmptyClipboard"), \
             patch.object(selection.win32clipboard, "CloseClipboard"), \
             patch.object(selection.win32clipboard, "SetClipboardData",
                          side_effect=_raise_for_bitmap), \
             patch.object(selection.log, "warning") as warning:
            selection._restore_clipboard({13: "text", 2: b"binary"})
        warning.assert_called_once()
        self.assertEqual(2, warning.call_args[0][1])

    def test_voice_model_load_failure_is_logged(self):
        manager = VoiceCommandListener(dict(DEFAULTS))
        with patch("faster_whisper.WhisperModel", side_effect=RuntimeError("no model")), \
             patch("voice.log.error") as error:
            with self.assertRaises(RuntimeError):
                manager._get_model()
        error.assert_called_once()


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
