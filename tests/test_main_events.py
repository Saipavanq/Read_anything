"""Regression tests for main-thread event handling in main.App."""
import os
import queue
import sys
import unittest
from unittest.mock import MagicMock, patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

from config import DEFAULTS
import main


class FakeVar:
    def __init__(self, value):
        self.value = value

    def get(self):
        return self.value


def make_app() -> main.App:
    # Build an App without running __init__, but keep its real methods intact.
    app = object.__new__(main.App)
    app.root = MagicMock()
    app.status_label = MagicMock()
    app.status_var = MagicMock()
    app.config = dict(DEFAULTS)
    app.control_vars = {}
    app.requests_queue = queue.Queue()
    app.engine_var = MagicMock()
    app.mode_var = MagicMock()
    app.tts = MagicMock()
    app.icon = None
    app._append_log = MagicMock()
    app.refresh_controls = MagicMock()
    app.shutdown = MagicMock()
    app.apply_setting = MagicMock()
    return app


class MainEventTests(unittest.TestCase):
    def test_settings_event_stays_off_activity_log(self):
        app = make_app()
        saved = lambda: None
        events = queue.Queue()
        events.put(("__settings__", saved))

        with patch.object(main, "ui_queue", events), patch.object(
            main, "open_settings"
        ) as open_settings:
            app.poll_events()

        open_settings.assert_called_once_with(app.config, saved, app.root)
        app._append_log.assert_not_called()
        app.status_var.set.assert_not_called()
        app.root.after.assert_called_once_with(120, app.poll_events)

    def test_internal_events_stay_off_activity_log(self):
        app = make_app()
        events = queue.Queue()
        events.put(("__refresh__", ""))
        events.put(("__show__", ""))
        events.put(("__unknown__", "ignored"))

        with patch.object(main, "ui_queue", events):
            app.poll_events()

        app.refresh_controls.assert_called_once_with()
        app.root.deiconify.assert_called_once_with()
        app.root.lift.assert_called_once_with()
        app._append_log.assert_not_called()
        app.status_var.set.assert_not_called()

    def test_request_read_only_queues_main_thread_event(self):
        app = make_app()
        app.control_vars = {"tts_engine": FakeVar("edge")}
        events = queue.Queue()

        with patch.object(main, "ui_queue", events), patch.object(
            main, "snapshot_settings", side_effect=AssertionError("wrong thread")
        ):
            app.request_read("simplify", "extra")

        app.tts.stop.assert_not_called()
        self.assertEqual(("__read__", "simplify"), events.get_nowait())

    def test_read_event_snapshot_and_stop_happen_on_main_thread(self):
        app = make_app()
        app.control_vars = {"tts_engine": FakeVar("edge")}
        events = queue.Queue()
        events.put(("__read__", "simplify"))

        with patch.object(main, "ui_queue", events):
            app.poll_events()

        app.tts.stop.assert_called_once_with()
        mode, snapshot = app.requests_queue.get_nowait()
        self.assertEqual("simplify", mode)
        self.assertEqual("edge", snapshot["tts_engine"])
        app._append_log.assert_not_called()

    def test_tray_setters_only_queue_main_thread_events(self):
        cases = [
            (main.App.set_engine, "edge", ("__engine__", "edge")),
            (main.App.set_mode, "simplify", ("__mode__", "simplify")),
        ]
        for setter, value, expected in cases:
            with self.subTest(setter=setter.__name__):
                app = make_app()
                app.engine_var.set.side_effect = AssertionError("wrong thread")
                app.mode_var.set.side_effect = AssertionError("wrong thread")
                app.apply_setting.side_effect = AssertionError("wrong thread")
                events = queue.Queue()

                with patch.object(main, "ui_queue", events):
                    setter(app, value)

                self.assertEqual(expected, events.get_nowait())

    def test_engine_and_mode_events_apply_on_main_thread(self):
        app = make_app()
        events = queue.Queue()
        events.put(("__engine__", "edge"))
        events.put(("__mode__", "simplify"))

        with patch.object(main, "ui_queue", events):
            app.poll_events()

        app.engine_var.set.assert_called_once_with("edge")
        app.mode_var.set.assert_called_once_with("simplify")
        app.apply_setting.assert_any_call("tts_engine", "edge")
        app.apply_setting.assert_any_call("llm_mode", "simplify")


if __name__ == "__main__":
    unittest.main()
