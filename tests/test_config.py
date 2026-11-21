"""Tests for configuration loading, coercion and persistence."""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import config as config_module
from config import DEFAULTS, load_config, save_config


class ConfigTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.path = Path(self.tmp.name) / "settings.json"
        self.patcher_path = patch.object(config_module, "CONFIG_PATH", self.path)
        self.patcher_dir = patch.object(
            config_module, "CONFIG_DIR", Path(self.tmp.name)
        )
        self.patcher_path.start()
        self.patcher_dir.start()
        self.addCleanup(self.patcher_path.stop)
        self.addCleanup(self.patcher_dir.stop)
        self.addCleanup(self.tmp.cleanup)

    def _write(self, data):
        self.path.write_text(json.dumps(data), encoding="utf-8")

    def test_defaults_complete(self):
        expected = {
            "hotkey_read", "hotkey_voice", "hotkey_stop", "capture_method",
            "tts_engine", "offline_voice", "sapi_rate", "edge_voice",
            "edge_rate", "volume", "max_chars", "llm_enabled",
            "llm_base_url", "llm_model", "llm_mode", "llm_translate_to",
            "llm_prompt", "llm_timeout", "whisper_cache",
            "whisper_model_path", "whisper_model", "voice_seconds",
            "input_device", "whisper_language",
        }
        self.assertEqual(expected, set(DEFAULTS))

    def test_invalid_values_are_clamped(self):
        cases = [
            ({"volume": "abc"}, 100),
            ({"volume": 200}, 100),
            ({"volume": -5}, 0),
            ({"volume": 42}, 42),
            ({"sapi_rate": 99}, 10),
            ({"sapi_rate": -99}, -10),
            ({"max_chars": -1}, 4000),
            ({"voice_seconds": -2}, 4.0),
            ({"voice_seconds": "not-a-number"}, 4.0),
            ({"llm_timeout": -5}, 120),
        ]
        for stored, expected in cases:
            with self.subTest(stored=stored):
                self._write(stored)
                key = next(iter(stored))
                self.assertEqual(expected, load_config()[key])

    def test_unknown_keys_dropped(self):
        self._write({"bogus_key": 1, "volume": 55})
        cfg = load_config()
        self.assertNotIn("bogus_key", cfg)
        self.assertEqual(55, cfg["volume"])

    def test_corrupt_json_falls_back_to_defaults(self):
        self.path.write_text("{bad json", encoding="utf-8")
        cfg = load_config()
        self.assertEqual(DEFAULTS, cfg)

    def test_round_trip_preserves_values(self):
        cfg = dict(DEFAULTS, volume=37, llm_mode="summarize", hotkey_read="ctrl+alt+t")
        save_config(cfg)
        self.assertEqual(cfg, load_config())

    def test_missing_file_returns_defaults(self):
        self.assertEqual(DEFAULTS, load_config())


if __name__ == "__main__":
    unittest.main()
