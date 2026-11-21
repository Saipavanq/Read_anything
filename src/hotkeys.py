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


ACTION_CONFIG_KEYS = {
    "read": "hotkey_read",
    "voice": "hotkey_voice",
    "stop": "hotkey_stop",
}


def _is_well_formed(combo: str) -> bool:
    """A hotkey needs exactly one non-modifier key and no empty segments."""
    parts = [p.strip().lower() for p in (combo or "").split("+")]
    if not parts or any(not part for part in parts):
        return False
    keys = [part for part in parts if part not in MODIFIERS]
    return len(keys) == 1


def validate_hotkeys(config: dict) -> tuple[dict[str, str], list[str]]:
    """Return usable {action: pynput binding} bindings and human-readable problems.

    Invalid or duplicate combinations are dropped; the first action wins in
    read -> voice -> stop priority so the primary read hotkey is never lost.
    """
    bindings: dict[str, str] = {}
    problems: list[str] = []
    seen: dict[str, str] = {}
    for action, config_key in ACTION_CONFIG_KEYS.items():
        combo = str(config.get(config_key, "") or "")
        if not _is_well_formed(combo):
            problems.append(f"{config_key}: malformed hotkey {combo!r}")
            continue
        binding = to_pynput(combo)
        if binding in seen:
            problems.append(
                f"{config_key}: duplicate hotkey {combo!r} (same as {seen[binding]})"
            )
            continue
        seen[binding] = config_key
        bindings[action] = binding
    return bindings, problems


def describe(combo: str) -> str:
    """Return a display-friendly version of a hotkey string."""
    import re

    return "+".join(
        part.capitalize() if len(part) > 1 else part.upper()
        for part in re.split(r"[+\-]", combo or "")
        if part
    )
