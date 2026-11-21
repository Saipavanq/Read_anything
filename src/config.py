"""Settings storage for the Read Aloud assistant."""
from __future__ import annotations

import json
import logging
import os
from pathlib import Path

log = logging.getLogger("read_aloud")

ROOT = Path(__file__).resolve().parent.parent
CONFIG_DIR = ROOT / "config"
CONFIG_PATH = CONFIG_DIR / "settings.json"
LOG_DIR = ROOT / "logs"

DEFAULT_WHISPER_CACHE = os.path.join(
    os.path.expanduser("~"), "voicebox models"
)

DEFAULTS = {
    # Triggers
    "hotkey_read": "ctrl+alt+r",
    "hotkey_voice": "ctrl+alt+v",   # editable in Settings (choose later)
    "hotkey_stop": "ctrl+alt+x",
    # Selection capture: auto (UIA then clipboard), uia, clipboard
    "capture_method": "auto",
    # Text-to-speech
    "tts_engine": "offline",          # offline | edge
    "offline_voice": "",              # empty = system default
    "sapi_rate": 0,                   # -10 .. 10
    "edge_voice": "en-US-AriaNeural",
    "edge_rate": "+0%",
    "volume": 100,                    # 0 .. 100
    "max_chars": 4000,
    # Local LLM (LM Studio / Ollama compatible API)
    "llm_enabled": False,
    "llm_base_url": "",               # empty = auto-detect localhost:1234 / 11434
    "llm_model": "",                  # empty = first model reported by the server
    "llm_mode": "verbatim",           # verbatim | summarize | simplify | translate | custom
    "llm_translate_to": "English",
    "llm_prompt": "",
    "llm_timeout": 120,
    # Local speech-to-text for voice commands
    "whisper_cache": DEFAULT_WHISPER_CACHE,
    "whisper_model_path": "",         # optional explicit snapshot directory
    "whisper_model": "Systran/faster-whisper-base",
    "voice_seconds": 4.0,
    "input_device": None,             # None = system default microphone
    "whisper_language": "",           # empty = auto-detect
}


# Inclusive bounds for numeric settings. Values outside these are clamped.
RANGES = {
    "volume": (0, 100),
    "sapi_rate": (-10, 10),
    "max_chars": (1, 1_000_000),
    "voice_seconds": (0.5, 3600.0),
    "llm_timeout": (1, 3600),
}

# For these, zero or negative is meaningless rather than merely out of range,
# so the stored value is discarded in favour of the default.
NON_POSITIVE_FALLBACK = {"max_chars", "voice_seconds", "llm_timeout"}


def normalise_config(cfg: dict) -> dict:
    """Return a copy with known keys coerced to their default types and clamped."""
    result = {}
    for key, default in DEFAULTS.items():
        value = cfg.get(key, default)
        try:
            if isinstance(default, bool):
                if isinstance(value, str):
                    value = value.strip().lower() in ("1", "true", "yes", "on")
                else:
                    value = bool(value)
            elif isinstance(default, int):
                value = int(float(value)) if value not in (None, "") else default
            elif isinstance(default, float):
                value = float(value) if value not in (None, "") else default
            elif default is None:
                if value in (None, ""):
                    value = None
            else:
                if value is None:
                    value = default
                elif not isinstance(value, str):
                    value = str(value)
            if isinstance(value, (int, float)) and not isinstance(value, bool):
                low, high = RANGES.get(key, (None, None))
                if key in NON_POSITIVE_FALLBACK and value <= 0:
                    value = default
                elif low is not None:
                    value = max(low, min(high, value))
        except (TypeError, ValueError, OverflowError) as exc:
            log.warning("Invalid setting %s=%r; using default %r (%s)", key, value, default, exc)
            value = default
        result[key] = value
    return result


def load_config() -> dict:
    cfg = dict(DEFAULTS)
    if CONFIG_PATH.exists():
        try:
            with CONFIG_PATH.open("r", encoding="utf-8") as fh:
                stored = json.load(fh)
            if isinstance(stored, dict):
                cfg.update({k: v for k, v in stored.items() if k in DEFAULTS})
            else:
                log.warning("Settings file is not an object; using defaults")
        except Exception as exc:  # corrupt config should not block startup
            log.warning("Could not read config (%s); using defaults", exc)
            return dict(DEFAULTS)
    return normalise_config(cfg)


def save_config(cfg: dict) -> None:
    CONFIG_DIR.mkdir(parents=True, exist_ok=True)
    clean = {k: cfg.get(k, v) for k, v in DEFAULTS.items()}
    with CONFIG_PATH.open("w", encoding="utf-8") as fh:
        json.dump(clean, fh, indent=2, ensure_ascii=False)
