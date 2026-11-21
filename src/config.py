"""Settings storage for the Read Aloud assistant."""
from __future__ import annotations

import json
import os
from pathlib import Path

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


def load_config() -> dict:
    cfg = dict(DEFAULTS)
    if CONFIG_PATH.exists():
        try:
            with CONFIG_PATH.open("r", encoding="utf-8") as fh:
                stored = json.load(fh)
            if isinstance(stored, dict):
                cfg.update({k: v for k, v in stored.items() if k in DEFAULTS})
        except Exception as exc:  # corrupt config should not block startup
            print(f"Could not read config: {exc}")
    return cfg


def save_config(cfg: dict) -> None:
    CONFIG_DIR.mkdir(parents=True, exist_ok=True)
    clean = {k: cfg.get(k, v) for k, v in DEFAULTS.items()}
    with CONFIG_PATH.open("w", encoding="utf-8") as fh:
        json.dump(clean, fh, indent=2, ensure_ascii=False)
