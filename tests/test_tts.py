"""Tests for TTS routing, cancellation and fallback."""
import os
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from config import DEFAULTS
from tts import TTSManager


class QuietComm:
    def __init__(self, *args, **kwargs):
        pass

    async def save(self, path):
        return None


class TTSManagerTests(unittest.TestCase):
    def test_empty_text_does_nothing(self):
        manager = TTSManager(dict(DEFAULTS))
        with patch.object(manager, "_speak_offline") as offline:
            manager.speak("")
        offline.assert_not_called()

    def test_edge_failure_falls_back(self):
        manager = TTSManager(dict(DEFAULTS, tts_engine="edge"))
        with patch.object(manager, "_speak_edge", side_effect=RuntimeError("offline")), \
             patch.object(manager, "_speak_offline") as offline:
            manager.speak("hello")
        offline.assert_called_once_with("hello")

    def test_stop_increments_generation(self):
        manager = TTSManager(dict(DEFAULTS))
        before = manager.generation
        manager.stop()
        self.assertEqual(before + 1, manager.generation)
        self.assertTrue(manager.stop_event.is_set())

    def test_stop_during_edge_save_aborts_playback(self):
        manager = TTSManager(dict(DEFAULTS, tts_engine="edge"))
        manager._mixer_ready = True

        class StoppingComm(QuietComm):
            async def save(self, path):
                manager.stop()

        with patch("edge_tts.Communicate", StoppingComm), \
             patch("pygame.mixer.music.load") as load, \
             patch("pygame.mixer.music.play") as play:
            manager.speak("hello")

        load.assert_not_called()
        play.assert_not_called()


if __name__ == "__main__":
    unittest.main()
