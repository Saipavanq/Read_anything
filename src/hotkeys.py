"""Helpers for translating friendly hotkey strings into pynput bindings."""
from __future__ import annotations

MODIFIERS = {
    "ctrl": "<ctrl>",
    "control": "<ctrl>",
    "shift": "<shift>",
    "alt": "<alt>",
    "win": "<cmd>",
    "cmd": "<cmd>",
    "super": "<cmd>",
    "meta": "<cmd>",
}

SPECIAL_KEYS = {
    "space": "<space>",
    "enter": "<enter>",
    "return": "<enter>",
    "esc": "<esc>",
    "escape": "<esc>",
    "tab": "<tab>",
    "backspace": "<backspace>",
    "delete": "<delete>",
    "home": "<home>",
    "end": "<end>",
    "up": "<up>",
    "down": "<down>",
    "left": "<left>",
    "right": "<right>",
}


def to_pynput(combo: str) -> str:
    """Convert 'ctrl+shift+r' into pynput's '<ctrl>+<shift>+r' format."""
    parts = [p.strip().lower() for p in (combo or "").split("+") if p.strip()]
    if not parts:
        raise ValueError("Empty hotkey")
    out = []
    for part in parts:
        if part in MODIFIERS:
            out.append(MODIFIERS[part])
        elif part in SPECIAL_KEYS:
            out.append(SPECIAL_KEYS[part])
        elif len(part) == 1:
            out.append(part)
        else:
            out.append(f"<{part}>")
    return "+".join(out)


def describe(combo: str) -> str:
    """Return a display-friendly version of a hotkey string."""
    import re

    return "+".join(
        part.capitalize() if len(part) > 1 else part.upper()
        for part in re.split(r"[+\-]", combo or "")
        if part
    )
