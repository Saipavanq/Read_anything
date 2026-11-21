"""Tests for logging setup in windowed (no console) builds."""
import logging
import logging.handlers
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "src"))

import logging_setup


class LoggingSetupTests(unittest.TestCase):
    def setUp(self):
        logger = logging.getLogger("read_aloud")
        self.saved_handlers = list(logger.handlers)
        logger.handlers.clear()
        self.tmp = tempfile.TemporaryDirectory()
        patcher = patch.object(logging_setup, "LOG_DIR", Path(self.tmp.name))
        patcher.start()
        self.addCleanup(patcher.stop)
        self.addCleanup(self.tmp.cleanup)

        def restore():
            logger.handlers.clear()
            logger.handlers.extend(self.saved_handlers)

        self.addCleanup(restore)

    def test_no_console_handler_when_disabled(self):
        logger = logging_setup.setup_logging(include_console=False)
        console_handlers = [
            handler
            for handler in logger.handlers
            if isinstance(handler, logging.StreamHandler)
            and not isinstance(handler, logging.handlers.RotatingFileHandler)
        ]
        self.assertEqual([], console_handlers)

    def test_rotating_file_handler_configured(self):
        logger = logging_setup.setup_logging(include_console=False)
        self.assertTrue(
            any(
                isinstance(handler, logging.handlers.RotatingFileHandler)
                for handler in logger.handlers
            )
        )


if __name__ == "__main__":
    unittest.main()
