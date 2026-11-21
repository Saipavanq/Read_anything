"""Tests for the main-thread settings snapshot used by the read worker."""
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from config import DEFAULTS
from main import snapshot_settings


class FakeVar:
    def __init__(self, value):
        self.value = value

    def get(self):
        return self.value


class BrokenVar:
    def get(self):
        raise RuntimeError("TclError: called from wrong thread")


class SnapshotTests(unittest.TestCase):
    def test_snapshot_reflects_current_controls(self):
        vars_dict = {
            "tts_engine": FakeVar("edge"),
            "llm_mode": FakeVar("simplify"),
            "volume": FakeVar(42),
        }
        result = snapshot_settings(vars_dict, dict(DEFAULTS))
        self.assertEqual("edge", result["tts_engine"])
        self.assertEqual("simplify", result["llm_mode"])
        self.assertEqual(42, result["volume"])

    def test_snapshot_contains_only_plain_values(self):
        vars_dict = {
            "volume": FakeVar(37),
            "tts_engine": FakeVar("offline"),
            "bad": FakeVar(object()),
        }
        result = snapshot_settings(vars_dict, dict(DEFAULTS))
        for value in result.values():
            self.assertIsInstance(value, (str, int, float, bool, type(None)))
        self.assertNotIn("bad", result)

    def test_snapshot_ignores_unreadable_vars(self):
        vars_dict = {"volume": BrokenVar(), "tts_engine": FakeVar("edge")}
        result = snapshot_settings(vars_dict, dict(DEFAULTS))
        self.assertEqual(DEFAULTS["volume"], result["volume"])
        self.assertEqual("edge", result["tts_engine"])

    def test_snapshot_normalises_string_numbers(self):
        vars_dict = {"volume": FakeVar("42"), "voice_seconds": FakeVar("3.5")}
        result = snapshot_settings(vars_dict, dict(DEFAULTS))
        self.assertEqual(42, result["volume"])
        self.assertEqual(3.5, result["voice_seconds"])


if __name__ == "__main__":
    unittest.main()
