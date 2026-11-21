"""Small Tkinter settings window for the Read Aloud assistant."""
from __future__ import annotations

import logging
import threading
import tkinter as tk
from tkinter import messagebox, ttk

from config import DEFAULTS, save_config
from llm import LocalLLM
from tts import list_offline_voices

log = logging.getLogger("read_aloud")

MODES = ["verbatim", "summarize", "simplify", "translate", "custom"]


def open_settings(config: dict, on_save) -> None:
    threading.Thread(target=_show, args=(config, on_save), daemon=True).start()


def _show(config: dict, on_save) -> None:
    root = tk.Tk()
    root.title("Read Aloud — Settings")
    root.geometry("620x720")
    root.resizable(False, False)

    frame = ttk.Frame(root, padding=12)
    frame.pack(fill=tk.BOTH, expand=True)
    frame.columnconfigure(1, weight=1)

    text_vars: dict[str, tk.StringVar] = {}
    bool_vars: dict[str, tk.BooleanVar] = {}

    def text_row(label: str, key: str, row: int, width: int = 38):
        ttk.Label(frame, text=label).grid(row=row, column=0, sticky=tk.W, pady=3)
        var = tk.StringVar(value=str(config.get(key, DEFAULTS.get(key, "")) or ""))
        ttk.Entry(frame, textvariable=var, width=width).grid(
            row=row, column=1, sticky=tk.EW, pady=3, padx=(8, 0)
        )
        text_vars[key] = var

    def choice_row(label: str, key: str, values: list[str], row: int):
        ttk.Label(frame, text=label).grid(row=row, column=0, sticky=tk.W, pady=3)
        var = tk.StringVar(value=str(config.get(key, DEFAULTS.get(key, "")) or ""))
        combo = ttk.Combobox(frame, textvariable=var, values=values, state="normal")
        combo.grid(row=row, column=1, sticky=tk.EW, pady=3, padx=(8, 0))
        text_vars[key] = var

    def check_row(label: str, key: str, row: int):
        var = tk.BooleanVar(value=bool(config.get(key, False)))
        ttk.Checkbutton(frame, text=label, variable=var).grid(
            row=row, column=0, columnspan=2, sticky=tk.W, pady=3
        )
        bool_vars[key] = var

    heading = ttk.Label(frame, text="Triggers", font=("Segoe UI", 10, "bold"))
    heading.grid(row=0, column=0, columnspan=2, sticky=tk.W, pady=(0, 4))
    text_row("Read selection", "hotkey_read", 1)
    text_row("Voice command", "hotkey_voice", 2)
    text_row("Stop speaking", "hotkey_stop", 3)
    choice_row("Selection capture", "capture_method", ["auto", "uia", "clipboard"], 4)

    ttk.Label(frame, text="Speech", font=("Segoe UI", 10, "bold")).grid(
        row=5, column=0, columnspan=2, sticky=tk.W, pady=(12, 4)
    )
    choice_row("Engine", "tts_engine", ["offline", "edge"], 6)
    choice_row("Offline voice", "offline_voice", list_offline_voices(), 7)
    text_row("Offline rate (-10..10)", "sapi_rate", 8, 10)
    text_row("Edge voice", "edge_voice", 9)
    text_row("Edge rate", "edge_rate", 10, 10)
    text_row("Volume (0..100)", "volume", 11, 10)
    text_row("Max characters", "max_chars", 12, 10)

    ttk.Label(frame, text="Local LLM", font=("Segoe UI", 10, "bold")).grid(
        row=13, column=0, columnspan=2, sticky=tk.W, pady=(12, 4)
    )
    check_row("Use local LLM when mode is not Verbatim", "llm_enabled", 14)
    text_row("Base URL (blank = auto)", "llm_base_url", 15)
    text_row("Model", "llm_model", 16)
    choice_row("Mode", "llm_mode", MODES, 17)
    text_row("Translate to", "llm_translate_to", 18, 18)
    text_row("Custom prompt", "llm_prompt", 19, 38)
    status = ttk.Label(frame, text="")
    status.grid(row=20, column=0, columnspan=2, sticky=tk.W, pady=2)

    def detect_llm() -> None:
        probe = dict(config)
        for key, var in text_vars.items():
            probe[key] = var.get()
        base, models, label = LocalLLM(probe).discover()
        if base:
            text_vars["llm_base_url"].set(base)
            if models:
                text_vars["llm_model"].set(models[0])
            status.configure(text=f"Found {label}: {len(models)} model(s)")
        else:
            status.configure(text="No local server found (start LM Studio or Ollama)")

    ttk.Button(frame, text="Detect local LLM", command=detect_llm).grid(
        row=21, column=0, sticky=tk.W, pady=6
    )

    ttk.Label(frame, text="Voice commands", font=("Segoe UI", 10, "bold")).grid(
        row=22, column=0, columnspan=2, sticky=tk.W, pady=(12, 4)
    )
    text_row("Listen seconds", "voice_seconds", 23, 10)
    text_row("Whisper cache", "whisper_cache", 24)
    text_row("Language (blank = auto)", "whisper_language", 25, 18)

    def save() -> None:
        try:
            for key, var in text_vars.items():
                default = DEFAULTS.get(key)
                value = var.get()
                if isinstance(default, bool):
                    config[key] = str(value).lower() in ("1", "true", "yes")
                elif isinstance(default, int):
                    config[key] = int(float(value))
                elif isinstance(default, float):
                    config[key] = float(value)
                else:
                    config[key] = value
            for key, var in bool_vars.items():
                config[key] = bool(var.get())
            if not config.get("hotkey_read") or not config["hotkey_read"]:
                raise ValueError("Read hotkey cannot be empty")
            save_config(config)
            on_save(config)
        except Exception as exc:
            messagebox.showerror("Settings", str(exc))
            return
        root.destroy()

    buttons = ttk.Frame(frame)
    buttons.grid(row=26, column=0, columnspan=2, pady=(16, 0))
    ttk.Button(buttons, text="Save", command=save).pack(side=tk.RIGHT, padx=4)
    ttk.Button(buttons, text="Cancel", command=root.destroy).pack(side=tk.RIGHT)

    root.mainloop()
