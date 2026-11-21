"""Application logging setup."""
from __future__ import annotations

import logging
from logging.handlers import RotatingFileHandler

from config import LOG_DIR


def setup_logging() -> logging.Logger:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    logger = logging.getLogger("read_aloud")
    logger.setLevel(logging.DEBUG)
    logger.propagate = False
    if logger.handlers:
        return logger

    fmt = logging.Formatter(
        "%(asctime)s %(levelname)-8s [%(threadName)s] %(message)s"
    )

    file_handler = RotatingFileHandler(
        LOG_DIR / "app.log", maxBytes=1_000_000, backupCount=3, encoding="utf-8"
    )
    file_handler.setLevel(logging.DEBUG)
    file_handler.setFormatter(fmt)
    logger.addHandler(file_handler)

    console = logging.StreamHandler()
    console.setLevel(logging.INFO)
    console.setFormatter(fmt)
    logger.addHandler(console)
    return logger
